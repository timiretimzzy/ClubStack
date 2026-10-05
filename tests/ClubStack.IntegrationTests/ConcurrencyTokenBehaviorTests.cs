using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class ConcurrencyTokenBehaviorTests : DatabaseTestBase
{
    public ConcurrencyTokenBehaviorTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task Second_writer_surfaces_a_concurrency_conflict()
    {
        var id = Guid.CreateVersion7();
        var prefix = $"XMX-{Guid.NewGuid():N}"[..16];

        await using (var seed = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            seed.ReferenceSequences.Add(new ReferenceSequenceEntity
            {
                Id = id,
                Prefix = prefix,
                LastNumber = 1,
                FormatWidth = 6,
            });
            await seed.SaveChangesAsync();
        }

        await using var writerA = await Fixture.ContextFactory.CreateDbContextAsync();
        await using var writerB = await Fixture.ContextFactory.CreateDbContextAsync();

        var rowA = await writerA.ReferenceSequences.SingleAsync(e => e.Id == id);
        var rowB = await writerB.ReferenceSequences.SingleAsync(e => e.Id == id);

        rowA.LastNumber = 2;
        await writerA.SaveChangesAsync();

        rowB.LastNumber = 99;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => writerB.SaveChangesAsync());
    }

    [Fact]
    public async Task Noop_update_surfaces_no_concurrency_conflict()
    {
        var id = Guid.CreateVersion7();
        var prefix = $"XNO-{Guid.NewGuid():N}"[..16];

        await using (var seed = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            seed.ReferenceSequences.Add(new ReferenceSequenceEntity
            {
                Id = id,
                Prefix = prefix,
                LastNumber = 1,
                FormatWidth = 6,
            });
            await seed.SaveChangesAsync();
        }

        await using var writerA = await Fixture.ContextFactory.CreateDbContextAsync();
        await using var writerB = await Fixture.ContextFactory.CreateDbContextAsync();

        var rowA = await writerA.ReferenceSequences.SingleAsync(e => e.Id == id);
        var rowB = await writerB.ReferenceSequences.SingleAsync(e => e.Id == id);

        rowB.LastNumber = 1;
        await writerB.SaveChangesAsync();

        rowA.LastNumber = 2;
        await writerA.SaveChangesAsync();
    }

    [Fact]
    public async Task Append_only_tables_have_no_concurrency_token()
    {
        var id = Guid.CreateVersion7();

        await using (var context = await Fixture.ContextFactory.CreateDbContextAsync())
        {
            var entity = context.AuditLogs.Add(new AuditLogEntity
            {
                Id = id,
                TimestampUtc = DateTimeOffset.UtcNow,
                Action = "Test.NoConcurrency",
                Result = "Success",
            });

            var concurrencyProperties = entity.Properties
                .Where(p => p.Metadata.IsConcurrencyToken)
                .Select(p => p.Metadata.Name)
                .ToArray();

            Assert.Empty(concurrencyProperties);
        }
    }
}