using ClubStack.Application.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class ConcurrentIdempotencyTests : DatabaseTestBase
{
    private const string Operation = "test.concurrent.operation";
    private const string Actor = "actor-concurrent";

    public ConcurrentIdempotencyTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Parallel_claims_of_the_same_key_yield_exactly_one_creator()
    {
        const int attemptCount = 24;
        var key = $"race-{Guid.NewGuid():N}";

        var claims = await Task.WhenAll(Enumerable.Range(0, attemptCount).Select(async i =>
        {
            await using var scope = Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            return await store.TryAcquireAsync(key, Operation, Actor, null, $"corr-{i}");
        }));

        var creators = claims.Count(c => c.Created);
        Assert.Equal(1, creators);

        var duplicates = claims.Where(c => !c.Created && c.Existing is not null).ToArray();
        Assert.Equal(attemptCount - 1, duplicates.Length);
        Assert.All(duplicates, d => Assert.Equal(key, d.Existing!.IdempotencyKey));
    }

    [Fact]
    public async Task Key_claimed_concurrently_then_completed_reads_back_completed()
    {
        const int attemptCount = 12;
        var key = $"finish-{Guid.NewGuid():N}";

        await Task.WhenAll(Enumerable.Range(0, attemptCount).Select(async i =>
        {
            await using var scope = Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            return await store.TryAcquireAsync(key, Operation, Actor, null, $"corr-{i}");
        }));

        await using (var scope = Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            await store.CompleteAsync(key, Operation, Actor, "{\"done\":true}");
        }

        await using var verifyScope = Services.CreateAsyncScope();
        var verifyStore = verifyScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var result = await verifyStore.GetAsync(key, Operation, Actor);

        Assert.NotNull(result);
        Assert.Equal(IdempotencyStatus.Completed, result.Status);
        Assert.Equal("{\"done\":true}", result.ResultJson);
    }

    [Fact]
    public async Task Duplicate_claims_expose_the_original_correlation_id()
    {
        const int attemptCount = 8;
        var key = $"corr-{Guid.NewGuid():N}";

        var claims = await Task.WhenAll(Enumerable.Range(0, attemptCount).Select(async i =>
        {
            await using var scope = Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            return await store.TryAcquireAsync(key, Operation, Actor, null, $"claim-{i}");
        }));

        for (var i = 0; i < attemptCount; i++)
        {
            if (!claims[i].Created)
            {
                Assert.NotNull(claims[i].Existing);
            }
        }

        await using var verifyScope = Services.CreateAsyncScope();
        var verifyStore = verifyScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var stored = await verifyStore.GetAsync(key, Operation, Actor);

        Assert.NotNull(stored);
        Assert.Equal(IdempotencyStatus.InProgress, stored.Status);
    }

    [Fact]
    public async Task Interleaved_queries_and_writes_are_not_corrupted_by_uniqueness_races()
    {
        var key = $"interleave-{Guid.NewGuid():N}";
        var totalCreators = 0;
        var roundsSucceeded = 0;

        for (var round = 0; round < 3; round++)
        {
            var attempts = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
            {
                await using var scope = Services.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                return await store.TryAcquireAsync(key, Operation, Actor, null, $"round-{round}-{i}");
            }));

            totalCreators += attempts.Count(a => a.Created);

            // Every attempt that did not create the row must have observed the
            // existing row instead of throwing (no state corruption).
            Assert.All(attempts.Where(a => !a.Created), a => Assert.NotNull(a.Existing));

            roundsSucceeded++;
        }

        Assert.Equal(3, roundsSucceeded);
        Assert.Equal(1, totalCreators);

        await using var verifyScope = Services.CreateAsyncScope();
        var verifyStore = verifyScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
        var stored = await verifyStore.GetAsync(key, Operation, Actor);

        Assert.NotNull(stored);
        Assert.Equal(IdempotencyStatus.InProgress, stored.Status);
    }
}