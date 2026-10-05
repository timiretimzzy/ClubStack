using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClubStack.Api;
using ClubStack.Api.Contracts;
using ClubStack.Application.Identity;
using ClubStack.Infrastructure.Identity;
using ClubStack.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

/// <summary>
/// HTTP-level verification of the D5 POS + Cashiering surface through the real
/// API pipeline: outlets/registers, product and member identification lookup,
/// sale lifecycle (create/complete/void/refund), per-line pricing and tax,
/// split and member-account tenders, idempotent completion, the opening-float
/// and expected-cash cashiering model (pay-ins/pay-outs, cash sales minus cash
/// refunds), close variance and reconciliation — including permission gating,
/// Problem Details status codes and the audit trail.
/// </summary>
public sealed class D5PosCashieringApiTests : IAsyncLifetime
{
    private const string AdminUserName = "d5-admin";
    private const string AdminPassword = "Str0ng!Pass12";
    private const string CashierUserName = "d5-pos-operator";
    private const string CashierPassword = "Str0ng!Pass12";

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _cashierClient = null!;

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        (_connectionString, _databaseName) = await PostgreSqlTestFixture.CreateScratchDatabaseAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var optionsDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ClubStackDbContext>));
                if (optionsDescriptor is not null)
                {
                    services.Remove(optionsDescriptor);
                }

                services.AddSingleton<DbContextOptions<ClubStackDbContext>>(_ =>
                {
                    var optionsBuilder = new DbContextOptionsBuilder<ClubStackDbContext>();
                    optionsBuilder.UseNpgsql(_connectionString);
                    return optionsBuilder.Options;
                });

                services.PostConfigure<CookieAuthenticationOptions>(
                    IdentityConstants.ApplicationScheme,
                    options => options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest);
            });
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        _cashierClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            await provider.GetRequiredService<IDatabaseMigrator>().MigrateAsync();
            await provider.GetRequiredService<IReferenceDataSeeder>().SeedAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
            await identity.CreateUserAsync(new CreateUserInput(
                AdminUserName, $"{AdminUserName}@example.com", "D5", "Admin", "0000000000",
                AdminPassword, ["System Administrator"]));
            await identity.CreateUserAsync(new CreateUserInput(
                CashierUserName, $"{CashierUserName}@example.com", "D5", "Cashier", "0000000000",
                CashierPassword, ["POS Operator"]));
        }
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _client.Dispose();
        _cashierClient.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task Anonymous_requests_to_d5_endpoints_are_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/pos/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/pos/products/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/pos/configuration")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/cashier-sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/sales", new { })).StatusCode);
    }

    [Fact]
    public async Task Pos_flow_runs_over_http_with_expected_cash_and_variance()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var tag = Guid.NewGuid().ToString("N");

        // ---- Reference master data: tax rate, category, location, products, stock. ----
        var taxRate = await PostJsonAsync<TaxRateResponse>("/api/v1/tax-rates", new TaxRateRequest
        {
            Name = "VAT 15",
            Rate = 0.15m,
            IsActive = true,
        }, _client);

        var category = await PostJsonAsync<ProductCategoryResponse>("/api/v1/product-categories", new CreateProductCategoryRequest
        {
            Code = $"CAT{tag[..6]}".ToUpperInvariant(),
            Name = $"POS category {tag}",
            Description = "D5 integration category",
        }, _client);

        var location = await PostJsonAsync<InventoryLocationResponse>("/api/v1/inventory-locations", new CreateInventoryLocationRequest
        {
            Code = $"LOC{tag[..6]}".ToUpperInvariant(),
            Name = "Bar Store",
            Type = "Store",
        }, _client);

        var supplier = await PostJsonAsync<SupplierResponse>("/api/v1/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"SUP{tag[..6]}".ToUpperInvariant(),
            Name = "POS supplier",
            ContactPerson = "Buyer",
            Email = $"pos-{tag}@clubstack.test",
            PaymentTerms = "Net 30",
        }, _client);

        var productA = await PostJsonAsync<ProductResponse>("/api/v1/products", new CreateProductRequest
        {
            Sku = $"SKU{tag[..6]}A".ToUpperInvariant(),
            Name = "POS lager",
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = true,
            Cost = 40m,
            PriceAmount = 120m,
            ReorderLevel = 20m,
            SupplierId = supplier.Id,
            TaxRateId = taxRate.Id,
            TaxTreatment = "Inclusive",
        }, _client);

        var productB = await PostJsonAsync<ProductResponse>("/api/v1/products", new CreateProductRequest
        {
            Sku = $"SKU{tag[..6]}B".ToUpperInvariant(),
            Name = "POS spirits",
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = true,
            Cost = 30m,
            PriceAmount = 60m,
            ReorderLevel = 20m,
            SupplierId = supplier.Id,
            TaxRateId = taxRate.Id,
            TaxTreatment = "Exclusive",
        }, _client);

        var productC = await PostJsonAsync<ProductResponse>("/api/v1/products", new CreateProductRequest
        {
            Sku = $"SKU{tag[..6]}C".ToUpperInvariant(),
            Name = "POS snack",
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = true,
            Cost = 10m,
            PriceAmount = 50m,
            ReorderLevel = 20m,
            SupplierId = supplier.Id,
            TaxRateId = taxRate.Id,
            TaxTreatment = "Inclusive",
        }, _client);

        await PostJsonAsync<List<StockMovementResponse>>("/api/v1/stock/receive", new ReceiveStockRequest
        {
            LocationId = location.Id,
            Reference = $"PO-{tag[..6]}",
            Lines = new List<ReceiveStockLineRequest>
            {
                new() { ProductId = productA.Id, Quantity = 100m },
                new() { ProductId = productB.Id, Quantity = 100m },
                new() { ProductId = productC.Id, Quantity = 100m },
            },
        }, _client);

        // ---- Member identification source (Phase 5 §member identification). ----
        var memberId = await CreateActiveMemberAsync(tag);
        var cardValue = $"D5CARD{tag[..6]}".ToUpperInvariant();
        var issued = await PostJsonAsync<CredentialResponse>("/api/v1/credentials", new IssueCredentialRequest
        {
            CredentialType = "MagneticStripe",
            CredentialValue = cardValue,
        }, _client);
        var activated = await PostJsonAsync<CredentialResponse>($"/api/v1/credentials/{issued.Id}/activate",
            new ActivateCredentialRequest { MemberId = memberId }, _client);
        var memberAccount = await GetJsonAsync<MemberAccountResponse>($"/api/v1/members/{memberId}/account", _client);

        // ---- POS configuration: outlet + register bound to the store location. ----
        var outlet = await PostJsonAsync<PosOutletResponse>("/api/v1/pos/outlets", new CreatePosOutletRequest
        {
            Code = $"BAR{tag[..4]}".ToUpperInvariant(),
            Name = "Main Bar",
        }, _client);
        Assert.Equal("Active", outlet.Status);

        var register = await PostJsonAsync<PosRegisterResponse>("/api/v1/pos/registers", new CreatePosRegisterRequest
        {
            PosOutletId = outlet.Id,
            Code = $"R1{tag[..4]}".ToUpperInvariant(),
            Name = "Bar Register 1",
            InventoryLocationId = location.Id,
        }, _client);
        Assert.Equal("Active", register.Status);
        Assert.Equal(location.Code, register.InventoryLocationCode);

        var config = await GetJsonAsync<PosConfigurationResponse>("/api/v1/pos/configuration", _client);
        Assert.Contains(config.Outlets, o => o.Id == outlet.Id);
        Assert.Contains(config.Registers, r => r.Id == register.Id);

        // ---- POS catalog lookup: priced items + available quantity at the register. ----
        var posProducts = await GetJsonAsync<PosProductSearchResponse>(
            $"/api/v1/pos/products?locationId={register.InventoryLocationId}&query=POS", _client);
        Assert.Equal(3, posProducts.TotalCount);
        var posA = Assert.Single(posProducts.Items, p => p.Id == productA.Id);
        Assert.Equal(120m, posA.UnitPrice);
        Assert.Equal("Standard", posA.PriceSourceType);
        Assert.Equal(100m, posA.AvailableQuantity);
        Assert.Equal("Inclusive", posA.TaxTreatment);
        Assert.Equal(0.15m, posA.TaxRate);

        var members = await GetJsonAsync<PosMemberSearchResponse>(
            $"/api/v1/pos/products/members?credentialId={activated.Id}", _client);
        var identified = Assert.Single(members.Items);
        Assert.Equal(memberId, identified.MemberId);
        Assert.Equal("Active", identified.Status);
        Assert.Equal("Active", identified.CredentialStatus);

        // ---- Cashier session opens with a float (CSN-). ----
        var session = await PostJsonAsync<CashierSessionResponse>("/api/v1/cashier-sessions", new OpenCashierSessionRequest
        {
            RegisterId = register.Id,
            OpeningFloat = 500m,
            Note = "Bar shift opener",
        }, _client);
        Assert.Equal("Open", session.Status);
        Assert.StartsWith("CSN-", session.SessionNumber, StringComparison.Ordinal);
        Assert.Equal(500m, session.ExpectedCash);
        Assert.Equal(500m, session.OpeningFloat);
        Assert.Single(session.Movements);
        Assert.Equal("Float", session.Movements[0].MovementType);

        // Opening a second session on the same register is rejected.
        var duplicate = await _client.PostAsJsonAsync("/api/v1/cashier-sessions", new OpenCashierSessionRequest
        {
            RegisterId = register.Id,
            OpeningFloat = 100m,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // ---- Sale created as draft: pricing hierarchy + tax snapshot + per-line discount. ----
        var draft = await PostJsonAsync<SaleResponse>("/api/v1/sales", new CreateSaleRequest
        {
            RegisterId = register.Id,
            MemberId = memberId,
            Lines = new List<SaleLineRequest>
            {
                new() { ProductId = productA.Id, Quantity = 1m },
                new()
                {
                    ProductId = productB.Id,
                    Quantity = 1m,
                    Discount = new SaleDiscountLineRequest { DiscountType = "Percent", Value = 10m, Reason = "Staff discount" },
                },
            },
        }, _client);
        Assert.Equal("Open", draft.Status);
        Assert.StartsWith("SALE-", draft.SaleNumber, StringComparison.Ordinal);
        Assert.Equal(memberId, draft.MemberId);
        Assert.Equal("Open", draft.Status);

        // Product A inclusive: 120 gross -> base 104.3478, tax 15.6522.
        // Product B exclusive with 10% discount: 54 + 15% = 62.10.
        Assert.Equal(104.3478m, draft.Lines[0].TaxableBase);
        Assert.Equal(15.6522m, draft.Lines[0].TaxAmount);
        Assert.Equal(120m, draft.Lines[0].LineTotal);
        Assert.Equal(120m, draft.Lines[0].OriginalUnitPrice);
        Assert.Equal(120m, draft.Lines[0].UnitPrice);
        Assert.Equal(54m, draft.Lines[1].TaxableBase);
        Assert.Equal(54m, draft.Lines[1].UnitPrice);
        Assert.Equal(8.10m, draft.Lines[1].TaxAmount);
        Assert.Equal(62.10m, draft.Lines[1].LineTotal);
        Assert.Equal(6m, draft.Lines[1].DiscountAmount);
        Assert.Equal("Percent", draft.Discounts[0].DiscountType);
        Assert.Equal(182.10m, draft.GrandTotal);
        Assert.Equal(158.3478m, draft.Subtotal);
        Assert.Equal(23.7522m, draft.TaxTotal);

        // ---- Completion is balanced and idempotent; stock is consumed. ----
        var completeKey = $"complete-{tag}";
        var completeBody = new CompleteSaleRequest
        {
            Payments = new List<SalePaymentRequest>
            {
                new() { PaymentMethodCode = "CASH", Amount = 100m },
                new() { PaymentMethodCode = "CARD", Amount = 82.10m, ExternalReference = "auth-1234" },
            },
            IdempotencyKey = completeKey,
        };

        var completed = await PostJsonAsync<SaleResponse>($"/api/v1/sales/{draft.Id}/complete", completeBody, _client);
        Assert.Equal("Completed", completed.Status);
        Assert.NotNull(completed.CompletedAtUtc);
        Assert.Equal(182.10m, completed.GrandTotal);
        Assert.Equal(2, completed.Payments.Count);
        Assert.Contains(completed.Payments, p => p.PaymentMethodCode == "CASH" && p.Amount == 100m);
        Assert.Contains(completed.Payments, p => p.PaymentMethodCode == "CARD" && p.Amount == 82.10m);
        Assert.NotNull(completed.Receipt);
        Assert.StartsWith("RCT-", completed.Receipt.ReceiptNumber, StringComparison.Ordinal);
        Assert.Equal(completed.SaleNumber, completed.Receipt.SaleNumber);

        // Idempotent replay: same key returns the same completed sale, no double posting.
        var replay = await PostJsonAsync<SaleResponse>($"/api/v1/sales/{draft.Id}/complete", completeBody, _client);
        Assert.Equal(completed.Id, replay.Id);
        Assert.Equal("Completed", replay.Status);
        Assert.Equal(182.10m, replay.GrandTotal);

        // Session activates on first completed sale; cash sale is journaled into expected cash.
        var activatedSession = await GetJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}", _client);
        Assert.Equal("Active", activatedSession.Status);
        Assert.Equal(600m, activatedSession.ExpectedCash);

        var stockB = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={location.Id}", _client);
        Assert.Equal(99m, Assert.Single(stockB.Items, s => s.ProductId == productA.Id).OnHand);
        Assert.Equal(99m, Assert.Single(stockB.Items, s => s.ProductId == productB.Id).OnHand);
        Assert.Equal(100m, Assert.Single(stockB.Items, s => s.ProductId == productC.Id).OnHand);

        // Unbalanced completion is rejected (400 Validation Problem). Uses a fresh draft so the
        // status guard does not mask the total-mismatch validation with a 409 Conflict.
        var unbalancedDraft = await PostJsonAsync<SaleResponse>("/api/v1/sales", new CreateSaleRequest
        {
            RegisterId = register.Id,
            Lines = new List<SaleLineRequest>
            {
                new() { ProductId = productB.Id, Quantity = 1m },
            },
        }, _client);
        var unbalanced = await _client.PostAsJsonAsync($"/api/v1/sales/{unbalancedDraft.Id}/complete", new CompleteSaleRequest
        {
            Payments = new List<SalePaymentRequest> { new() { PaymentMethodCode = "CASH", Amount = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, unbalanced.StatusCode);

        // ---- Void: draft sale is voided before completion; no inventory movement. ----
        var voidable = await PostJsonAsync<SaleResponse>("/api/v1/sales", new CreateSaleRequest
        {
            RegisterId = register.Id,
            Lines = new List<SaleLineRequest>
            {
                new() { ProductId = productB.Id, Quantity = 1m },
            },
        }, _client);
        var voided = await PostJsonAsync<SaleResponse>($"/api/v1/sales/{voidable.Id}/void", new VoidSaleRequest
        {
            Reason = "Wrong product scanned",
        }, _client);
        Assert.Equal("Voided", voided.Status);
        Assert.NotNull(voided.VoidedAtUtc);

        var stockAfterVoid = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={location.Id}", _client);
        Assert.Equal(99m, Assert.Single(stockAfterVoid.Items, s => s.ProductId == productB.Id).OnHand);

        // Completing a voided sale is rejected.
        var completeVoided = await _client.PostAsJsonAsync($"/api/v1/sales/{voidable.Id}/complete", new CompleteSaleRequest
        {
            Payments = new List<SalePaymentRequest> { new() { PaymentMethodCode = "CASH", Amount = 1m } },
        });
        Assert.Equal(HttpStatusCode.Conflict, completeVoided.StatusCode);

        // ---- Member account tender: sale is charged to the member's account (CHG). ----
        var charged = await PostJsonAsync<SaleResponse>("/api/v1/sales", new CreateSaleRequest
        {
            RegisterId = register.Id,
            MemberId = memberId,
            Lines = new List<SaleLineRequest>
            {
                new() { ProductId = productC.Id, Quantity = 1m },
            },
        }, _client);
        charged = await PostJsonAsync<SaleResponse>($"/api/v1/sales/{charged.Id}/complete", new CompleteSaleRequest
        {
            Payments = new List<SalePaymentRequest> { new() { PaymentMethodCode = "ACCOUNT", Amount = 50m } },
        }, _client);
        Assert.Equal("Completed", charged.Status);
        var accountPayment = Assert.Single(charged.Payments);
        Assert.Equal("ACCOUNT", accountPayment.PaymentMethodCode);
        Assert.NotNull(accountPayment.AccountTransactionId);
        Assert.StartsWith("CHG-", accountPayment.AccountTransactionReference, StringComparison.Ordinal);

        var balanceAfterCharge = await GetJsonAsync<AccountBalanceResponse>(
            $"/api/v1/member-accounts/{memberAccount.Id}/balance", _client);
        Assert.Equal(50m, balanceAfterCharge.AmountDue);

        // Account tenders do not touch the cash drawer.
        var sessionAfterAccount = await GetJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}", _client);
        Assert.Equal(600m, sessionAfterAccount.ExpectedCash);

        // ---- Refund: account sale refunded; credit restores the member balance; stock returns. ----
        var refund = await PostJsonAsync<SaleRefundResponse>($"/api/v1/sales/{charged.Id}/refund", new RefundSaleRequest
        {
            Reason = "Member returned the item",
            Payments = new List<SalePaymentRequest> { new() { PaymentMethodCode = "ACCOUNT", Amount = 50m } },
            IdempotencyKey = $"refund-{tag}",
        }, _client);
        Assert.Equal("Completed", refund.Status);
        Assert.StartsWith("RFD-", refund.RefundNumber, StringComparison.Ordinal);
        Assert.Equal(charged.SaleNumber, refund.SaleNumber);
        Assert.Equal(50m, refund.Amount);

        var balanceAfterRefund = await GetJsonAsync<AccountBalanceResponse>(
            $"/api/v1/member-accounts/{memberAccount.Id}/balance", _client);
        Assert.Equal(0m, balanceAfterRefund.AmountDue);

        var sessionAfterRefund = await GetJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}", _client);
        Assert.Equal(600m, sessionAfterRefund.ExpectedCash);
        var stockAfterRefund = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={location.Id}", _client);
        Assert.Equal(100m, Assert.Single(stockAfterRefund.Items, s => s.ProductId == productC.Id).OnHand);

        // ---- Cash refund pulls cash back out of the drawer expectation. ----
        var cashRefund = await PostJsonAsync<SaleRefundResponse>($"/api/v1/sales/{draft.Id}/refund", new RefundSaleRequest
        {
            Reason = "Both products returned",
            Payments = new List<SalePaymentRequest>
            {
                new() { PaymentMethodCode = "CASH", Amount = 100m },
                new() { PaymentMethodCode = "CARD", Amount = 82.10m },
            },
        }, _client);
        Assert.Equal("Completed", cashRefund.Status);
        Assert.Equal(182.10m, cashRefund.Amount);

        var sessionAfterCashRefund = await GetJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}", _client);
        Assert.Equal(500m, sessionAfterCashRefund.ExpectedCash);

        // ---- Cash movements: pay-in and pay-out shift expected cash. ----
        session = await PostJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}/movements", new CashMovementRequest
        {
            MovementType = "PayIn",
            Amount = 100m,
            Reason = "Bank deposit float top-up",
        }, _client);
        Assert.Equal(600m, session.ExpectedCash);

        session = await PostJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}/movements", new CashMovementRequest
        {
            MovementType = "PayOut",
            Amount = 50m,
            Reason = "Petty cash",
        }, _client);
        Assert.Equal(550m, session.ExpectedCash);

        // Adding a second float is rejected.
        var secondFloat = await _client.PostAsJsonAsync($"/api/v1/cashier-sessions/{session.Id}/movements", new CashMovementRequest
        {
            MovementType = "Float",
            Amount = 1m,
            Reason = "n/a",
        });
        Assert.Equal(HttpStatusCode.BadRequest, secondFloat.StatusCode);

        // ---- Close: declared cash equal to expected -> zero variance, auto-approved. ----
        var closed = await PostJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}/close", new CloseCashierSessionRequest
        {
            DeclaredCash = 550m,
        }, _client);
        Assert.Equal("Closed", closed.Status);
        Assert.Equal(550m, closed.ExpectedCash);
        Assert.Equal(550m, closed.DeclaredCash);
        Assert.Equal(0m, closed.Variance);
        Assert.True(closed.VarianceApproved);

        // ---- Reconcile finalises the session. ----
        var reconciled = await PostJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{session.Id}/reconcile", new { }, _client);
        Assert.Equal("Reconciled", reconciled.Status);
        Assert.NotNull(reconciled.ReconciledAtUtc);

        // ---- Search surfaces completed sales and reconciled sessions. ----
        var sales = await GetJsonAsync<SaleSearchResponse>($"/api/v1/sales?status=Completed", _client);
        Assert.Contains(sales.Items, s => s.Id == draft.Id);
        Assert.Contains(sales.Items, s => s.Id == charged.Id);
        var sessions = await GetJsonAsync<CashierSessionSearchResponse>($"/api/v1/cashier-sessions?status=Reconciled", _client);
        Assert.Contains(sessions.Items, s => s.Id == session.Id);

        // ---- Receipt reprint. ----
        var receipt = await GetJsonAsync<ReceiptResponse>($"/api/v1/sales/{draft.Id}/receipt", _client);
        Assert.StartsWith("RCT-", receipt.ReceiptNumber, StringComparison.Ordinal);
        Assert.Equal(2, receipt.Lines.Count);

        // ---- Sensitive POS commands are audited (Phase 8 §5). ----
        await using (var context = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ClubStackDbContext>())
        {
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "POS.Sale.Complete" && a.EntityId == draft.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "POS.Sale.Void" && a.EntityId == voidable.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "POS.Sale.Refund" && a.EntityId == refund.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Cashiering.Session.Open"));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Cashiering.Session.Movement"));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Cashiering.Session.Close"));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Cashiering.Session.Reconcile"));
        }
    }

    [Fact]
    public async Task Missing_permissions_are_forbidden_for_pos_and_cashiering()
    {
        await LoginAsync(_cashierClient, CashierUserName, CashierPassword);

        // Cashier sees the catalog and can operate the till.
        Assert.Equal(HttpStatusCode.OK, (await _cashierClient.GetAsync("/api/v1/pos/products")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _cashierClient.GetAsync("/api/v1/pos/products/members")).StatusCode);

        // But has no void, refund or reconcile rights.
        Assert.Equal(HttpStatusCode.Forbidden, (await _cashierClient.PostAsJsonAsync($"/api/v1/sales/{Guid.NewGuid()}/void", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cashierClient.PostAsJsonAsync($"/api/v1/sales/{Guid.NewGuid()}/refund", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cashierClient.PostAsJsonAsync($"/api/v1/cashier-sessions/{Guid.NewGuid()}/reconcile", new { })).StatusCode);

        // POS configuration is management-only.
        Assert.Equal(HttpStatusCode.Forbidden, (await _cashierClient.GetAsync("/api/v1/pos/configuration")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cashierClient.PostAsJsonAsync("/api/v1/pos/registers", new { })).StatusCode);
    }

    // ---- helpers ----

    private async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> CreateActiveMemberAsync(string tag)
    {
        var category = await PostJsonAsync<MembershipCategoryResponse>("/api/v1/membership-categories",
            new MembershipCategoryRequest
            {
                Code = $"D5{tag[..6]}".ToUpperInvariant(),
                Name = "HTTP category",
                Description = "E2E category",
                RenewalPeriodMonths = 12,
                IsActive = true,
            }, _client);
        var person = await PostJsonAsync<PersonCreatedResponse>("/api/v1/persons", new PersonRequest
        {
            LegalName = $"E2E {tag}",
            Email = $"e2e-{tag}@clubstack.test",
            Phone = $"07{Guid.NewGuid():N}"[..10],
        }, _client);

        var application = await PostJsonAsync<MembershipApplicationResponse>("/api/v1/membership-applications",
            new CreateMembershipApplicationRequest { PersonId = person.Person.Id, CategoryId = category.Id }, _client);
        application = await PostJsonAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/submit", new { }, _client);
        application = await PostJsonAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/start-review", new { }, _client);
        application = await PostJsonAsync<MembershipApplicationResponse>($"/api/v1/membership-applications/{application.Id}/approve", new { }, _client);

        var memberships = await GetJsonAsync<MembershipResponse[]>($"/api/v1/members/{application.MemberId}/memberships", _client);
        await PostJsonAsync<MembershipResponse>($"/api/v1/memberships/{Assert.Single(memberships).Id}/activate", new { }, _client);
        return application.MemberId!.Value;
    }

    private async Task<T> PostJsonAsync<T>(string url, object body, HttpClient client)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(_json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> GetJsonAsync<T>(string url, HttpClient client)
        => await client.GetFromJsonAsync<T>(url, _json) ?? throw new InvalidOperationException("Empty response body.");
}