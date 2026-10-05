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
/// D5 continuation: the guarantees that keep POS and Cashiering safe under retries
/// and concurrency — idempotency scoping to the authenticated actor and operation,
/// payload-mismatch detection, no wedged keys after a failure, optimistic-concurrency
/// conflicts surfacing as 409, and one unfinished cashier session per register.
/// </summary>
public sealed class D5ContinuationApiTests : IAsyncLifetime
{
    private const string AdminUserName = "d5c-admin";
    private const string AdminPassword = "Str0ng!Pass12";
    private const string CashierUserName = "d5c-cashier";
    private const string CashierPassword = "Str0ng!Pass12";

    /// <summary>Unit price 100.00 with 15% exclusive tax makes the sale total 115.00.</summary>
    private const decimal SaleTotal = 115.00m;

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _cashierClient = null!;

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private sealed record Till(Guid RegisterId, Guid ProductId, Guid MemberId);

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
                AdminUserName, $"{AdminUserName}@example.com", "D5C", "Admin", "0000000000",
                AdminPassword, ["System Administrator"]));
            await identity.CreateUserAsync(new CreateUserInput(
                CashierUserName, $"{CashierUserName}@example.com", "D5C", "Cashier", "0000000000",
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
    public async Task Repeating_a_completion_with_the_same_key_replays_the_stored_result()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var draft = await CreateSaleAsync(till);
        var body = CompleteBody("repeat-key", SaleTotal);

        var first = await PostJsonAsync<SaleResponse>($"/api/v1/sales/{draft.Id}/complete", body, _client);
        var replay = await PostJsonAsync<SaleResponse>($"/api/v1/sales/{draft.Id}/complete", body, _client);

        Assert.Equal("Completed", first.Status);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.SaleNumber, replay.SaleNumber);
        Assert.Equal(SaleTotal, replay.GrandTotal);

        // The replay must not post the sale a second time.
        await using var context = NewContext();
        Assert.Equal(1, await context.SalePayments.CountAsync(p => p.SaleId == draft.Id));
        Assert.Equal(1, await context.Receipts.CountAsync(r => r.SaleId == draft.Id));
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_is_rejected()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var first = await CreateSaleAsync(till);
        var completed = await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{first.Id}/complete", CompleteBody("shared-key", SaleTotal), _client);
        Assert.Equal("Completed", completed.Status);

        // Same key, different operation target: must not replay the first sale.
        var second = await CreateSaleAsync(till);
        var conflicting = await _client.PostAsJsonAsync(
            $"/api/v1/sales/{second.Id}/complete", CompleteBody("shared-key", SaleTotal));

        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);

        // The second sale is untouched by the rejected request.
        var stillOpen = await GetJsonAsync<SaleResponse>($"/api/v1/sales/{second.Id}", _client);
        Assert.Equal("Open", stillOpen.Status);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_amount_is_rejected()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var first = await CreateSaleAsync(till);
        await PostJsonAsync<SaleResponse>($"/api/v1/sales/{first.Id}/complete", CompleteBody("amount-key", SaleTotal), _client);

        // Same key, materially different payload for the same sale target.
        var second = await CreateSaleAsync(till);
        var conflicting = await _client.PostAsJsonAsync(
            $"/api/v1/sales/{second.Id}/complete", CompleteBody("amount-key", SaleTotal * 2));

        Assert.Equal(HttpStatusCode.Conflict, conflicting.StatusCode);
    }

    [Fact]
    public async Task Concurrent_completions_with_one_key_produce_exactly_one_effect()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var draft = await CreateSaleAsync(till);
        var body = CompleteBody("concurrent-key", SaleTotal);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
            await _client.PostAsJsonAsync($"/api/v1/sales/{draft.Id}/complete", body)));

        var accepted = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToArray();
        var rejected = responses.Where(r => r.StatusCode != HttpStatusCode.OK).ToArray();

        // No request may surface as a server error.
        Assert.All(rejected, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.NotEmpty(accepted);

        var saleIds = new List<Guid>();
        foreach (var response in accepted)
        {
            saleIds.Add((await response.Content.ReadFromJsonAsync<SaleResponse>(_json))!.Id);
        }

        Assert.Single(saleIds.Distinct());

        await using var context = NewContext();
        Assert.Equal(1, await context.SalePayments.CountAsync(p => p.SaleId == draft.Id));
        Assert.Equal(1, await context.Receipts.CountAsync(r => r.SaleId == draft.Id));
        Assert.Equal(0, await context.SaleRefunds.CountAsync(r => r.SaleId == draft.Id));
    }

    [Fact]
    public async Task A_failed_completion_can_be_retried_with_the_same_key()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var draft = await CreateSaleAsync(till);

        // First attempt fails validation after the key has been claimed.
        var failed = await _client.PostAsJsonAsync(
            $"/api/v1/sales/{draft.Id}/complete", CompleteBody("retry-key", 1m));
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);

        // The key must not be wedged: the corrected request succeeds with the same key.
        var retried = await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{draft.Id}/complete", CompleteBody("retry-key", SaleTotal), _client);
        Assert.Equal("Completed", retried.Status);

        await using var context = NewContext();
        Assert.Equal(1, await context.SalePayments.CountAsync(p => p.SaleId == draft.Id));
    }

    [Fact]
    public async Task One_key_is_independent_for_completion_and_refund()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var draft = await CreateSaleAsync(till);
        var key = "cross-operation-key";
        var completed = await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{draft.Id}/complete", CompleteBody(key, SaleTotal), _client);
        Assert.Equal("Completed", completed.Status);

        // The refund namespace is separate, so the same key is a fresh claim here.
        var refund = await PostJsonAsync<SaleRefundResponse>($"/api/v1/sales/{draft.Id}/refund", new RefundSaleRequest
        {
            Reason = "Same key, different operation",
            Payments = new List<SalePaymentRequest> { new() { PaymentMethodCode = "CASH", Amount = SaleTotal } },
            IdempotencyKey = key,
        }, _client);

        Assert.Equal("Completed", refund.Status);
        Assert.StartsWith("RFD-", refund.RefundNumber, StringComparison.Ordinal);

        // Replaying the refund with the same key and payload replays the stored refund.
        var replay = await PostJsonAsync<SaleRefundResponse>($"/api/v1/sales/{draft.Id}/refund", new RefundSaleRequest
        {
            Reason = "Same key, different operation",
            Payments = new List<SalePaymentRequest> { new() { PaymentMethodCode = "CASH", Amount = SaleTotal } },
            IdempotencyKey = key,
        }, _client);
        Assert.Equal(refund.RefundNumber, replay.RefundNumber);

        await using var context = NewContext();
        Assert.Equal(1, await context.SaleRefunds.CountAsync(r => r.SaleId == draft.Id));
    }

    [Fact]
    public async Task One_key_is_independent_per_authenticated_actor()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        await LoginAsync(_cashierClient, CashierUserName, CashierPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var key = "per-actor-key";

        var adminDraft = await CreateSaleAsync(till);
        var adminSale = await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{adminDraft.Id}/complete", CompleteBody(key, SaleTotal), _client);
        Assert.Equal("Completed", adminSale.Status);

        // A different authenticated actor with the same key is a separate claim.
        var cashierDraft = await CreateSaleAsync(till);
        var cashierSale = await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{cashierDraft.Id}/complete", CompleteBody(key, SaleTotal), _cashierClient);
        Assert.Equal("Completed", cashierSale.Status);

        Assert.NotEqual(adminSale.Id, cashierSale.Id);
        Assert.NotEqual(adminSale.SaleNumber, cashierSale.SaleNumber);

        // Each actor's own replay still resolves to its own sale.
        var adminReplay = await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{adminDraft.Id}/complete", CompleteBody(key, SaleTotal), _client);
        Assert.Equal(adminSale.Id, adminReplay.Id);
    }

    [Fact]
    public async Task Concurrent_voids_of_one_sale_yield_one_success_and_one_conflict()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        var draft = await CreateSaleAsync(till);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            await _client.PostAsJsonAsync($"/api/v1/sales/{draft.Id}/void", new VoidSaleRequest
            {
                Reason = "Concurrent void attempt",
            })));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.DoesNotContain(responses, r => r.StatusCode == HttpStatusCode.InternalServerError);

        var final = await GetJsonAsync<SaleResponse>($"/api/v1/sales/{draft.Id}", _client);
        Assert.Equal("Voided", final.Status);
    }

    [Fact]
    public async Task Only_one_cashier_session_may_be_unfinished_on_a_register()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(async i =>
            await _client.PostAsJsonAsync("/api/v1/cashier-sessions", new OpenCashierSessionRequest
            {
                RegisterId = till.RegisterId,
                OpeningFloat = 100m + i,
                Note = "Concurrent open",
            })));

        var opened = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToArray();
        var refused = responses.Where(r => r.StatusCode != HttpStatusCode.OK).ToArray();

        Assert.Single(opened);
        // The database-level guard must reject the rest as conflicts, never as 500s.
        Assert.All(refused, r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        await using var context = NewContext();
        var unfinished = await context.CashierSessions
            .CountAsync(s => s.RegisterId == till.RegisterId
                             && (s.Status == ClubStack.Domain.Pos.CashierSessionStatus.Open
                                 || s.Status == ClubStack.Domain.Pos.CashierSessionStatus.Active
                                 || s.Status == ClubStack.Domain.Pos.CashierSessionStatus.Closing));
        Assert.Equal(1, unfinished);
    }

    [Fact]
    public async Task A_new_session_may_be_opened_once_the_previous_one_is_finished()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();

        var first = await OpenSessionAsync(till.RegisterId);

        // A session only becomes closable once its first sale activates it.
        var draft = await CreateSaleAsync(till);
        await PostJsonAsync<SaleResponse>(
            $"/api/v1/sales/{draft.Id}/complete", CompleteBody("closing-sale", SaleTotal), _client);

        await PostJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{first.Id}/close", new CloseCashierSessionRequest
        {
            DeclaredCash = SaleTotal + 500m,
        }, _client);
        await PostJsonAsync<CashierSessionResponse>($"/api/v1/cashier-sessions/{first.Id}/reconcile", new { }, _client);

        // The partial unique index only covers unfinished sessions, so this must succeed.
        var second = await OpenSessionAsync(till.RegisterId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Open", second.Status);
    }

    [Fact]
    public async Task Sale_rows_reject_an_unknown_product_line()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var till = await CreateTillAsync();
        await OpenSessionAsync(till.RegisterId);

        // sale_lines.ProductId is a real foreign key, so an unknown product cannot be sold.
        var response = await _client.PostAsJsonAsync("/api/v1/sales", new CreateSaleRequest
        {
            RegisterId = till.RegisterId,
            Lines = new List<SaleLineRequest>
            {
                new() { ProductId = Guid.NewGuid(), Quantity = 1m },
            },
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Expected a client error for an unknown product, got {(int)response.StatusCode}.");
    }

    // ---- helpers ----

    private ClubStackDbContext NewContext()
        => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<ClubStackDbContext>();

    private async Task<Till> CreateTillAsync()
    {
        var tag = Guid.NewGuid().ToString("N");

        var taxRate = await PostJsonAsync<TaxRateResponse>("/api/v1/tax-rates", new TaxRateRequest
        {
            Name = $"VAT 15 {tag[..6]}",
            Rate = 0.15m,
            IsActive = true,
        }, _client);

        var category = await PostJsonAsync<ProductCategoryResponse>("/api/v1/product-categories", new CreateProductCategoryRequest
        {
            Code = $"CC{tag[..6]}".ToUpperInvariant(),
            Name = $"Continuation category {tag}",
        }, _client);

        var location = await PostJsonAsync<InventoryLocationResponse>("/api/v1/inventory-locations", new CreateInventoryLocationRequest
        {
            Code = $"CL{tag[..6]}".ToUpperInvariant(),
            Name = "Continuation store",
            Type = "Store",
        }, _client);

        var product = await PostJsonAsync<ProductResponse>("/api/v1/products", new CreateProductRequest
        {
            Sku = $"CS{tag[..6]}".ToUpperInvariant(),
            Name = "Continuation item",
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = false,
            Cost = 50m,
            PriceAmount = 100m,
            ReorderLevel = 1m,
            TaxRateId = taxRate.Id,
            TaxTreatment = "Exclusive",
        }, _client);

        var outlet = await PostJsonAsync<PosOutletResponse>("/api/v1/pos/outlets", new CreatePosOutletRequest
        {
            Code = $"CO{tag[..4]}".ToUpperInvariant(),
            Name = "Continuation outlet",
        }, _client);

        var register = await PostJsonAsync<PosRegisterResponse>("/api/v1/pos/registers", new CreatePosRegisterRequest
        {
            PosOutletId = outlet.Id,
            Code = $"CR{tag[..4]}".ToUpperInvariant(),
            Name = "Continuation register",
            InventoryLocationId = location.Id,
        }, _client);

        return new Till(register.Id, product.Id, Guid.Empty);
    }

    private Task<CashierSessionResponse> OpenSessionAsync(Guid registerId)
        => PostJsonAsync<CashierSessionResponse>("/api/v1/cashier-sessions", new OpenCashierSessionRequest
        {
            RegisterId = registerId,
            OpeningFloat = 500m,
        }, _client);

    private Task<SaleResponse> CreateSaleAsync(Till till)
        => PostJsonAsync<SaleResponse>("/api/v1/sales", new CreateSaleRequest
        {
            RegisterId = till.RegisterId,
            Lines = new List<SaleLineRequest>
            {
                new() { ProductId = till.ProductId, Quantity = 1m },
            },
        }, _client);

    private static CompleteSaleRequest CompleteBody(string key, decimal amount) => new()
    {
        Payments = new List<SalePaymentRequest>
        {
            new() { PaymentMethodCode = "CASH", Amount = amount },
        },
        IdempotencyKey = key,
    };

    private async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<T> PostJsonAsync<T>(string url, object body, HttpClient client)
    {
        var response = await client.PostAsJsonAsync(url, body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"POST {url} expected OK, got {(int)response.StatusCode} {response.StatusCode}: {payload}");
        return JsonSerializer.Deserialize<T>(payload, _json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> GetJsonAsync<T>(string url, HttpClient client)
        => await client.GetFromJsonAsync<T>(url, _json) ?? throw new InvalidOperationException("Empty response body.");
}
