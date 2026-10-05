using ClubStack.Application.Idempotency;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class IdempotencyPersistenceTests : DatabaseTestBase
{
    private const string Operation = "test.operation";
    private const string Actor = "actor-1";

    public IdempotencyPersistenceTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task TryAcquire_creates_an_in_progress_record()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var (created, existing) = await store.TryAcquireAsync(
            $"key-{Guid.NewGuid():N}", Operation, Actor, null, Guid.NewGuid().ToString("N"));

        Assert.True(created);
        Assert.Null(existing);
    }

    [Fact]
    public async Task CompleteAsync_stores_result_and_GetAsync_reads_completed()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"key-{Guid.NewGuid():N}";

        await store.TryAcquireAsync(key, Operation, Actor, null, Guid.NewGuid().ToString("N"));
        await store.CompleteAsync(key, Operation, Actor, "{\"ok\":true}");

        var result = await store.GetAsync(key, Operation, Actor);

        Assert.NotNull(result);
        Assert.Equal(IdempotencyStatus.Completed, result.Status);
        Assert.Equal("{\"ok\":true}", result.ResultJson);
        Assert.NotNull(result.CompletedAtUtc);
    }

    [Fact]
    public async Task Acquiring_an_existing_key_returns_it_without_creating()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"key-{Guid.NewGuid():N}";

        await store.TryAcquireAsync(key, Operation, Actor, null, "corr-1");
        var (created, existing) = await store.TryAcquireAsync(key, Operation, Actor, null, "corr-2");

        Assert.False(created);
        Assert.NotNull(existing);
        Assert.Equal(key, existing.IdempotencyKey);
        Assert.Equal("corr-1", existing.CorrelationId);
    }

    [Fact]
    public async Task FailAsync_marks_a_failed_record()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"key-{Guid.NewGuid():N}";

        await store.TryAcquireAsync(key, Operation, Actor, null, Guid.NewGuid().ToString("N"));
        await store.FailAsync(key, Operation, Actor, "boom");

        var result = await store.GetAsync(key, Operation, Actor);

        Assert.NotNull(result);
        Assert.Equal(IdempotencyStatus.Failed, result.Status);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_key()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();

        var result = await store.GetAsync($"missing-{Guid.NewGuid():N}", Operation, Actor);

        Assert.Null(result);
    }

    [Fact]
    public async Task The_same_key_is_independent_per_operation()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"shared-{Guid.NewGuid():N}";

        var (firstCreated, _) = await store.TryAcquireAsync(key, "test.operation.a", Actor, null, "corr-1");
        var (secondCreated, secondExisting) = await store.TryAcquireAsync(key, "test.operation.b", Actor, null, "corr-2");

        Assert.True(firstCreated);
        Assert.True(secondCreated);
        Assert.Null(secondExisting);
    }

    [Fact]
    public async Task The_same_key_is_independent_per_actor()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"shared-{Guid.NewGuid():N}";

        var (firstCreated, _) = await store.TryAcquireAsync(key, Operation, "actor-1", null, "corr-1");
        var (secondCreated, secondExisting) = await store.TryAcquireAsync(key, Operation, "actor-2", null, "corr-2");

        Assert.True(firstCreated);
        Assert.True(secondCreated);
        Assert.Null(secondExisting);
    }

    [Fact]
    public async Task A_failed_key_can_be_reacquired_with_the_same_key()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"retry-{Guid.NewGuid():N}";

        await store.TryAcquireAsync(key, Operation, Actor, null, "corr-1");
        await store.FailAsync(key, Operation, Actor, "boom");

        var (recreated, existing) = await store.TryAcquireAsync(key, Operation, Actor, null, "corr-2");

        Assert.True(recreated);
        Assert.Null(existing);

        var result = await store.GetAsync(key, Operation, Actor);
        Assert.NotNull(result);
        Assert.Equal(IdempotencyStatus.InProgress, result.Status);
        Assert.Null(result.ResultJson);
    }

    [Fact]
    public async Task The_original_request_hash_is_preserved_for_comparison()
    {
        await using var scope = Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var key = $"hash-{Guid.NewGuid():N}";
        var hash = IdempotencyRequestHash.Compute("{\"amount\":10}");

        await store.TryAcquireAsync(key, Operation, Actor, hash, "corr-1");
        await store.CompleteAsync(key, Operation, Actor, "{\"ok\":true}");

        var result = await store.GetAsync(key, Operation, Actor);

        Assert.NotNull(result);
        Assert.Equal(hash, result.RequestHash);
        Assert.Equal(Operation, result.Operation);
        Assert.Equal(Actor, result.ActorUserId);
    }
}
