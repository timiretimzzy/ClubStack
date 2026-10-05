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
/// HTTP-level verification of the D4 inventory surface through the real API
/// pipeline: product catalogue, categories, locations, suppliers, receipts,
/// stock positions, movements, transfers, counts, adjustments and wastage —
/// including permission gating, Problem Details status codes and the audit trail.
/// </summary>
public sealed class D4InventoryApiTests : IAsyncLifetime
{
    private const string AdminUserName = "d4-admin";
    private const string AdminPassword = "Str0ng!Pass12";
    private const string LimitedUserName = "d4-procurement";
    private const string LimitedPassword = "Str0ng!Pass12";
    private const string WastageUserName = "d4-wastage";
    private const string AdjustUserName = "d4-adjust";
    private const string OfficerPassword = "Str0ng!Pass12";

    private string _databaseName = string.Empty;
    private string _connectionString = string.Empty;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private HttpClient _limitedClient = null!;
    private HttpClient _wastageClient = null!;
    private HttpClient _adjustClient = null!;

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

        _limitedClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        _wastageClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        _adjustClient = _factory.CreateClient(new WebApplicationFactoryClientOptions
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
                AdminUserName, $"{AdminUserName}@example.com", "D4", "Admin", "0000000000",
                AdminPassword, ["System Administrator"]));
            await identity.CreateUserAsync(new CreateUserInput(
                LimitedUserName, $"{LimitedUserName}@example.com", "D4", "Procurement", "0000000000",
                LimitedPassword, ["Procurement Officer"]));

            // Dedicated single-scope roles prove the wastage/adjust separation.
            await identity.CreateRoleAsync("Wastage Officer", ["Inventory.Stock.Wastage"]);
            await identity.CreateRoleAsync("Adjustment Officer", ["Inventory.Stock.Adjust"]);
            await identity.CreateUserAsync(new CreateUserInput(
                WastageUserName, $"{WastageUserName}@example.com", "D4", "Wastage", "0000000000",
                OfficerPassword, ["Wastage Officer"]));
            await identity.CreateUserAsync(new CreateUserInput(
                AdjustUserName, $"{AdjustUserName}@example.com", "D4", "Adjust", "0000000000",
                OfficerPassword, ["Adjustment Officer"]));
        }
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        _client.Dispose();
        _limitedClient.Dispose();
        _wastageClient.Dispose();
        _adjustClient.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            await PostgreSqlTestFixture.DropDatabaseAsync(_databaseName);
        }
    }

    [Fact]
    public async Task Anonymous_requests_to_d4_endpoints_are_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/transfers")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/counts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/adjustments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/adjustments?adjustmentType=Wastage")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/adjustments?adjustmentType=Adjustment")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/adjustments", new
        {
            adjustmentType = "Wastage",
            reason = "anonymous attempt",
            lines = new[] { new { } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/v1/suppliers")).StatusCode);
    }

    [Fact]
    public async Task Inventory_flows_run_over_http_with_audit_trail()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var tag = Guid.NewGuid().ToString("N");
        var prefix = $"D4{tag[..6]}".ToUpperInvariant();

        // ---- Master data: category, location, supplier, product (INV-001.., PROC-001). ----
        var category = await PostJsonAsync<ProductCategoryResponse>("/api/v1/product-categories", new CreateProductCategoryRequest
        {
            Code = $"CAT{tag[..6]}".ToUpperInvariant(),
            Name = $"HTTP category {tag}",
            Description = "Integration test category",
        }, _client);
        Assert.Equal("Active", ToDisplayStatus(category.IsActive));

        var locationA = await PostJsonAsync<InventoryLocationResponse>("/api/v1/inventory-locations", new CreateInventoryLocationRequest
        {
            Code = $"LOCA{tag[..4]}".ToUpperInvariant(),
            Name = "Warehouse A",
            Type = "Warehouse",
        }, _client);
        Assert.Equal("Active", locationA.Status);

        var locationB = await PostJsonAsync<InventoryLocationResponse>("/api/v1/inventory-locations", new CreateInventoryLocationRequest
        {
            Code = $"LOCB{tag[..4]}".ToUpperInvariant(),
            Name = "Store B",
            Type = "Store",
        }, _client);

        var supplier = await PostJsonAsync<SupplierResponse>("/api/v1/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"SUP{tag[..6]}".ToUpperInvariant(),
            Name = "HTTP supplier",
            ContactPerson = "Buyer One",
            Email = $"buyer-{tag}@clubstack.test",
            PaymentTerms = "Net 30",
        }, _client);
        Assert.Equal("Active", supplier.Status);

        var product = await PostJsonAsync<ProductResponse>("/api/v1/products", new CreateProductRequest
        {
            Sku = $"SKU{tag[..8]}".ToUpperInvariant(),
            Name = "HTTP product",
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = true,
            Cost = 50m,
            PriceAmount = 120m,
            ReorderLevel = 10m,
            SupplierId = supplier.Id,
        }, _client);
        Assert.Equal("Active", product.Status);
        Assert.Equal("ZAR", product.CurrencyCode);

        var prices = await GetJsonAsync<ProductPriceResponse[]>($"/api/v1/products/{product.Id}/prices", _client);
        Assert.Single(prices);
        Assert.Equal(120m, prices[0].Amount);
        Assert.Equal("Standard", prices[0].PriceType);

        var found = await GetJsonAsync<ProductSearchResponse>($"/api/v1/products?query={product.Sku}", _client);
        Assert.Equal(1, found.TotalCount);

        // ---- Receipt (INV-024): 100 units in + 50 units in. ----
        var received = await PostJsonAsync<List<StockMovementResponse>>("/api/v1/stock/receive", new ReceiveStockRequest
        {
            LocationId = locationA.Id,
            Reference = $"PO-{tag[..6]}",
            Lines = new List<ReceiveStockLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 100m },
            },
        }, _client);
        Assert.Single(received);
        Assert.Equal("Receipt", received[0].MovementType);
        Assert.StartsWith("STM-", received[0].Reference, StringComparison.Ordinal);

        await PostJsonAsync<List<StockMovementResponse>>("/api/v1/stock/receive", new ReceiveStockRequest
        {
            LocationId = locationA.Id,
            Lines = new List<ReceiveStockLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 50m },
            },
        }, _client);

        var stock = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationA.Id}", _client);
        var balance = Assert.Single(stock.Items);
        Assert.Equal(150m, balance.OnHand);
        Assert.Equal("Ok", balance.Status);

        // ---- Transfer draft -> complete (INV-025): 30 to location B. ----
        var transfer = await PostJsonAsync<StockTransferResponse>("/api/v1/transfers", new CreateStockTransferRequest
        {
            FromLocationId = locationA.Id,
            ToLocationId = locationB.Id,
            Lines = new List<CreateStockTransferLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 30m },
            },
        }, _client);
        Assert.Equal("Draft", transfer.Status);
        Assert.StartsWith("STF-", transfer.TransferNumber, StringComparison.Ordinal);

        transfer = await PostJsonAsync<StockTransferResponse>($"/api/v1/transfers/{transfer.Id}/complete", new { }, _client);
        Assert.Equal("Completed", transfer.Status);

        var stockA = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationA.Id}", _client);
        Assert.Equal(120m, Assert.Single(stockA.Items).OnHand);
        var stockB = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationB.Id}", _client);
        Assert.Equal(30m, Assert.Single(stockB.Items).OnHand);

        // ---- Movement history shows the linked transfer pair (Phase 3 §33). ----
        var movements = await GetJsonAsync<MovementSearchResponse>($"/api/v1/stock/movements?productId={product.Id}", _client);
        Assert.Equal(4, movements.TotalCount);
        Assert.Equal(2, movements.Items.Count(m => m.SourceType == "StockTransfer" && m.SourceId == transfer.Id));
        Assert.Contains(movements.Items, m => m.MovementType == "TransferOut" && m.Quantity == -30m);
        Assert.Contains(movements.Items, m => m.MovementType == "TransferIn" && m.Quantity == 30m);

        // ---- Stock count initiate -> record -> complete -> approve (INV-041..045). ----
        var count = await PostJsonAsync<StockCountResponse>("/api/v1/counts", new InitiateStockCountRequest
        {
            LocationId = locationA.Id,
            ProductId = product.Id,
        }, _client);
        Assert.Equal("InProgress", count.Status);
        Assert.StartsWith("SCN-", count.CountNumber, StringComparison.Ordinal);
        var countLine = Assert.Single(count.Lines);
        Assert.Equal(120m, countLine.ExpectedQuantity);

        // Count 118 at A (variance -2) and 32 at B (variance +2).
        await PutJsonAsync<StockCountResponse>($"/api/v1/counts/{count.Id}/lines", new RecordStockCountLinesRequest
        {
            Items = new List<RecordStockCountLineRequest>
            {
                new() { ProductId = product.Id, CountedQuantity = 118m },
            },
        }, _client);

        count = await PostJsonAsync<StockCountResponse>($"/api/v1/counts/{count.Id}/complete", new { }, _client);
        Assert.Equal("Completed", count.Status);

        count = await PostJsonAsync<StockCountResponse>($"/api/v1/counts/{count.Id}/approve", new { }, _client);
        Assert.Equal("Approved", count.Status);
        Assert.Equal(-2m, Assert.Single(count.Lines).Variance);

        var stockA2 = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationA.Id}", _client);
        Assert.Equal(118m, Assert.Single(stockA2.Items).OnHand);

        // ---- Wastage and adjustment (INV-026/027) post immediately. ----
        var wastage = await PostJsonAsync<StockAdjustmentResponse>("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Wastage",
            LocationId = locationA.Id,
            Reason = "Damaged during handling",
            Lines = new List<CreateStockAdjustmentLineRequest>
            {
                new() { ProductId = product.Id, Quantity = 8m },
            },
        }, _client);
        Assert.Equal("Wastage", wastage.AdjustmentType);
        Assert.StartsWith("SADJ-", wastage.AdjustmentNumber, StringComparison.Ordinal);

        var adjustment = await PostJsonAsync<StockAdjustmentResponse>("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Adjustment",
            LocationId = locationA.Id,
            Reason = "Variance write-down",
            Lines = new List<CreateStockAdjustmentLineRequest>
            {
                new() { ProductId = product.Id, Quantity = -10m },
            },
        }, _client);
        Assert.Equal("Adjustment", adjustment.AdjustmentType);

        var stockA3 = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationA.Id}", _client);
        Assert.Equal(100m, Assert.Single(stockA3.Items).OnHand);

        // A decrease below zero is rejected.
        var overdraw = await _client.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Adjustment",
            LocationId = locationA.Id,
            Reason = "Overdraw",
            Lines = new List<CreateStockAdjustmentLineRequest>
            {
                new() { ProductId = product.Id, Quantity = -99999m },
            },
        });
        Assert.Equal(HttpStatusCode.Conflict, overdraw.StatusCode);

        // ---- Reorder alert (INV-030): a product whose aggregate on-hand across all
        // locations is at or below its reorder level is flagged Low anywhere it sits. ----
        var stockB2 = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationB.Id}&reorderOnly=true", _client);
        Assert.Empty(stockB2.Items);

        await PutJsonAsync<ProductResponse>($"/api/v1/products/{product.Id}", new UpdateProductRequest
        {
            Name = product.Name,
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = true,
            Cost = product.Cost,
            ReorderLevel = 200m,
            SupplierId = supplier.Id,
        }, _client);

        var alerts = await GetJsonAsync<StockSearchResponse>($"/api/v1/stock?locationId={locationA.Id}&reorderOnly=true", _client);
        Assert.Contains(alerts.Items, b => b.ProductId == product.Id && b.Status == "Low");

        // ---- Sensitive commands are audited (Phase 8 §5). ----
        await using (var context = _factory.Services.CreateScope().ServiceProvider
                         .GetRequiredService<ClubStackDbContext>())
        {
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Procurement.Supplier.Create" && a.EntityId == supplier.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Product.Create" && a.EntityId == product.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Stock.Receive"));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Stock.Transfer" && a.EntityId == transfer.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Stock.Adjust" && a.EntityId == adjustment.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Stock.Wastage" && a.EntityId == wastage.Id.ToString()));
        }
    }

    [Fact]
    public async Task Missing_permissions_are_forbidden()
    {
        await LoginAsync(_limitedClient, LimitedUserName, LimitedPassword);

        // Procurement Officer sees stock and products but cannot change them.
        var stock = await _limitedClient.GetAsync("/api/v1/stock");
        Assert.Equal(HttpStatusCode.OK, stock.StatusCode);

        var receive = await _limitedClient.PostAsJsonAsync("/api/v1/stock/receive", new ReceiveStockRequest
        {
            LocationId = Guid.NewGuid(),
            Lines = new List<ReceiveStockLineRequest> { new() { ProductId = Guid.NewGuid(), Quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, receive.StatusCode);

        var product = await _limitedClient.PostAsJsonAsync("/api/v1/products", new CreateProductRequest
        {
            Sku = "FORBIDDEN-SKU",
            Name = "x",
            CategoryId = Guid.NewGuid(),
            UnitOfMeasure = "Each",
        });
        Assert.Equal(HttpStatusCode.Forbidden, product.StatusCode);

        var adjustment = await _limitedClient.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Adjustment",
            LocationId = Guid.NewGuid(),
            Reason = "n/a",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = Guid.NewGuid(), Quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, adjustment.StatusCode);

        // Wastage is its own permission: a role without it cannot post wastage either.
        var wastage = await _limitedClient.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Wastage",
            LocationId = Guid.NewGuid(),
            Reason = "n/a",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = Guid.NewGuid(), Quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, wastage.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _limitedClient.GetAsync("/api/v1/adjustments?adjustmentType=Wastage")).StatusCode);
    }

    [Fact]
    public async Task Wastage_permission_is_separate_from_adjustment_permission()
    {
        await LoginAsync(_client, AdminUserName, AdminPassword);
        var tag = Guid.NewGuid().ToString("N");
        var prefix = $"D4{tag[..6]}".ToUpperInvariant();

        var category = await PostJsonAsync<ProductCategoryResponse>("/api/v1/product-categories", new CreateProductCategoryRequest
        {
            Code = $"CAT{tag[..6]}".ToUpperInvariant(),
            Name = $"Separation category {tag}",
            Description = "Integration test category",
        }, _client);

        var location = await PostJsonAsync<InventoryLocationResponse>("/api/v1/inventory-locations", new CreateInventoryLocationRequest
        {
            Code = $"LOCS{tag[..4]}".ToUpperInvariant(),
            Name = "Separation Store",
            Type = "Store",
        }, _client);

        var supplier = await PostJsonAsync<SupplierResponse>("/api/v1/suppliers", new CreateSupplierRequest
        {
            SupplierCode = $"SUPS{tag[..6]}".ToUpperInvariant(),
            Name = "Separation supplier",
            ContactPerson = "Buyer Two",
            Email = $"sep-{tag}@clubstack.test",
            PaymentTerms = "Net 30",
        }, _client);

        var product = await PostJsonAsync<ProductResponse>("/api/v1/products", new CreateProductRequest
        {
            Sku = $"SKUS{tag[..8]}".ToUpperInvariant(),
            Name = "Separation product",
            CategoryId = category.Id,
            UnitOfMeasure = "Each",
            StockManaged = true,
            Cost = 25m,
            PriceAmount = 60m,
            SupplierId = supplier.Id,
        }, _client);

        await PostJsonAsync<List<StockMovementResponse>>("/api/v1/stock/receive", new ReceiveStockRequest
        {
            LocationId = location.Id,
            Lines = new List<ReceiveStockLineRequest> { new() { ProductId = product.Id, Quantity = 100m } },
        }, _client);

        // ---- Wastage-only officer: can see and post wastage, but not adjustments. ----
        await LoginAsync(_wastageClient, WastageUserName, OfficerPassword);

        var wastageResponse = await _wastageClient.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Wastage",
            LocationId = location.Id,
            Reason = "Spoiled stock",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = product.Id, Quantity = 5m } },
        });
        Assert.Equal(HttpStatusCode.OK, wastageResponse.StatusCode);
        var wastage = await wastageResponse.Content.ReadFromJsonAsync<StockAdjustmentResponse>(_json);
        Assert.NotNull(wastage);
        Assert.Equal("Wastage", wastage.AdjustmentType);

        Assert.Equal(HttpStatusCode.OK,
            (await _wastageClient.GetAsync("/api/v1/adjustments?adjustmentType=Wastage")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _wastageClient.GetAsync($"/api/v1/adjustments/{wastage.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _wastageClient.GetAsync("/api/v1/adjustments?adjustmentType=Adjustment")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _wastageClient.GetAsync("/api/v1/adjustments")).StatusCode);

        var wastageOnlyAdjustmentPost = await _wastageClient.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Adjustment",
            LocationId = location.Id,
            Reason = "not allowed",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = product.Id, Quantity = -1m } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, wastageOnlyAdjustmentPost.StatusCode);

        // ---- Adjustment-only officer: can see and post adjustments, but never wastage. ----
        await LoginAsync(_adjustClient, AdjustUserName, OfficerPassword);

        var adjustResponse = await _adjustClient.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Adjustment",
            LocationId = location.Id,
            Reason = "Theft write-down",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = product.Id, Quantity = -5m } },
        });
        Assert.Equal(HttpStatusCode.OK, adjustResponse.StatusCode);
        var adjustment = await adjustResponse.Content.ReadFromJsonAsync<StockAdjustmentResponse>(_json);
        Assert.NotNull(adjustment);
        Assert.Equal("Adjustment", adjustment.AdjustmentType);

        Assert.Equal(HttpStatusCode.OK,
            (await _adjustClient.GetAsync("/api/v1/adjustments?adjustmentType=Adjustment")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await _adjustClient.GetAsync($"/api/v1/adjustments/{adjustment.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _adjustClient.GetAsync("/api/v1/adjustments?adjustmentType=Wastage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _adjustClient.GetAsync("/api/v1/adjustments")).StatusCode);

        var adjustOnlyWastagePost = await _adjustClient.PostAsJsonAsync("/api/v1/adjustments", new CreateStockAdjustmentRequest
        {
            AdjustmentType = "Wastage",
            LocationId = location.Id,
            Reason = "not allowed either",
            Lines = new List<CreateStockAdjustmentLineRequest> { new() { ProductId = product.Id, Quantity = 5m } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, adjustOnlyWastagePost.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _adjustClient.GetAsync($"/api/v1/adjustments/{wastage.Id}")).StatusCode);

        // ---- A wastage-only user must never see another user's adjustment document. ----
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _wastageClient.GetAsync($"/api/v1/adjustments/{adjustment.Id}")).StatusCode);

        // ---- Each audit action tracks its own permission (Phase 8 §5). ----
        await using (var context = _factory.Services.CreateScope().ServiceProvider
                         .GetRequiredService<ClubStackDbContext>())
        {
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Stock.Wastage" && a.EntityId == wastage.Id.ToString()));
            Assert.NotNull(await context.AuditLogs.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Action == "Inventory.Stock.Adjust" && a.EntityId == adjustment.Id.ToString()));
        }
    }

    // ---- helpers ----

    private static string ToDisplayStatus(bool isActive) => isActive ? "Active" : "Inactive";

    private async Task LoginAsync(HttpClient client, string userName, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<T> PostJsonAsync<T>(string url, object body, HttpClient client)
    {
        var response = await client.PostAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(_json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> PutJsonAsync<T>(string url, object body, HttpClient client)
    {
        var response = await client.PutAsJsonAsync(url, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(_json) ?? throw new InvalidOperationException("Empty response body.");
    }

    private async Task<T> GetJsonAsync<T>(string url, HttpClient client)
        => await client.GetFromJsonAsync<T>(url, _json) ?? throw new InvalidOperationException("Empty response body.");
}