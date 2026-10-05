using ClubStack.Application.References;
using ClubStack.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class ConcurrentReferenceGenerationTests : DatabaseTestBase
{
    public ConcurrentReferenceGenerationTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Parallel_generations_never_duplicate_a_reference()
    {
        const int attemptCount = 32;
        var prefix = UniquePrefix("CON");

        var generations = await Task.WhenAll(Enumerable.Range(0, attemptCount).Select(async _ =>
        {
            await using var scope = Services.CreateAsyncScope();
            var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();
            return await generator.GenerateAsync(prefix);
        }));

        Assert.Equal(attemptCount, generations.Length);
        Assert.Equal(attemptCount, generations.Distinct(StringComparer.Ordinal).Count());

        var sequences = generations.Select(g => BusinessReference.Parse(g).Sequence).ToArray();
        Assert.Equal(attemptCount, sequences.Distinct().Count());
        Assert.Equal(attemptCount, sequences.Max());
        Assert.Equal(1, sequences.Min());
    }

    [Fact]
    public async Task Parallel_generations_persist_the_final_counter()
    {
        const int attemptCount = 32;
        var prefix = UniquePrefix("CNT");

        await Task.WhenAll(Enumerable.Range(0, attemptCount).Select(async _ =>
        {
            await using var scope = Services.CreateAsyncScope();
            var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();
            return await generator.GenerateAsync(prefix);
        }));

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.ReferenceSequences.AsNoTracking().SingleAsync(e => e.Prefix == prefix);

        Assert.Equal(attemptCount, row.LastNumber);
    }

    [Fact]
    public async Task Sequential_and_parallel_mixes_stay_consistent()
    {
        var prefix = UniquePrefix("MIX");
        const int serialCount = 5;
        const int parallelCount = 20;

        await using (var scope = Services.CreateAsyncScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();
            for (var i = 0; i < serialCount; i++)
            {
                await generator.GenerateAsync(prefix);
            }
        }

        var parallel = await Task.WhenAll(Enumerable.Range(0, parallelCount).Select(async _ =>
        {
            await using var scope = Services.CreateAsyncScope();
            var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();
            return await generator.GenerateAsync(prefix);
        }));

        var total = serialCount + parallelCount;
        var all = parallel.Select(p => BusinessReference.Parse(p).Sequence)
            .Concat(Enumerable.Range(1, serialCount).Select(i => (long)i))
            .ToArray();

        Assert.Equal(total, all.Distinct().Count());
        Assert.Equal(1, all.Min());
        Assert.Equal(total, all.Max());
    }
}