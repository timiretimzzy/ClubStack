using ClubStack.Application.Auditing;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class D1AuditQueryTests : DatabaseTestBase
{
    public D1AuditQueryTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task QueryAsync_filters_and_pages()
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var action = $"Audit.D1Query.{Guid.NewGuid():N}";

        await using (var scope = Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            for (var i = 1; i <= 5; i++)
            {
                await recorder.RecordAsync(new AuditRecord
                {
                    Id = Guid.CreateVersion7(),
                    TimestampUtc = DateTimeOffset.UtcNow,
                    ActorUserId = "d1-actor",
                    ActorName = "D1 Auditor",
                    Action = i % 2 == 0 ? $"{action}.B" : action,
                    EntityType = "Test",
                    Result = "Success",
                    CorrelationId = correlationId,
                });
            }
        }

        await using (var scope = Services.CreateAsyncScope())
        {
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditRecorder>();

            var filtered = await recorder.QueryAsync(new AuditQueryInput(
                Action: action,
                Page: 1,
                PageSize: 10));

            Assert.Equal(3, filtered.TotalCount);
            Assert.Equal(3, filtered.Items.Count);

            var paged = await recorder.QueryAsync(new AuditQueryInput(
                ActorName: "D1 Auditor",
                Page: 1,
                PageSize: 2));

            Assert.Equal(5, paged.TotalCount);
            Assert.Equal(2, paged.Items.Count);

            var pageTwo = await recorder.QueryAsync(new AuditQueryInput(
                ActorName: "D1 Auditor",
                Page: 2,
                PageSize: 2));

            Assert.Equal(2, pageTwo.Items.Count);
            Assert.True(paged.Items[0].TimestampUtc >= paged.Items[1].TimestampUtc);
        }
    }
}