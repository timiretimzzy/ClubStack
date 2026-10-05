using ClubStack.Application.References;
using ClubStack.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.IntegrationTests;

[Collection("Database")]
public sealed class ReferenceGenerationTests : DatabaseTestBase
{
    public ReferenceGenerationTests(PostgreSqlTestFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task GenerateAsync_allocates_sequential_zero_padded_references()
    {
        var prefix = UniquePrefix("MEM");

        await using var scope = Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();

        var first = await generator.GenerateAsync(prefix);
        var second = await generator.GenerateAsync(prefix);
        var third = await generator.GenerateAsync(prefix);

        Assert.Equal($"{prefix}-000001", first);
        Assert.Equal($"{prefix}-000002", second);
        Assert.Equal($"{prefix}-000003", third);
    }

    [Fact]
    public async Task GenerateAsync_normalizes_prefix_to_upper_and_trims()
    {
        var prefix = UniquePrefix("mbr");

        await using var scope = Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();

        var formatted = await generator.GenerateAsync($"  {prefix}  ");

        var reference = BusinessReference.Parse(formatted);
        Assert.Equal(prefix.ToUpperInvariant(), reference.Prefix);
        Assert.Equal(1, reference.Sequence);
    }

    [Fact]
    public async Task GenerateAsync_custom_width_pads_to_requested_width()
    {
        var prefix = UniquePrefix("PAD");

        await using var scope = Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();

        var reference = await generator.GenerateAsync(prefix, width: 8);

        Assert.Equal($"{prefix}-00000001", reference);
    }

    [Fact]
    public async Task GenerateAsync_keeps_sequence_counters_isolated_per_prefix()
    {
        var prefixA = UniquePrefix("ISO");
        var prefixB = UniquePrefix("ISO");

        await using var scope = Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();

        await generator.GenerateAsync(prefixA);
        await generator.GenerateAsync(prefixA);

        var firstB = await generator.GenerateAsync(prefixB);

        Assert.Equal($"{prefixB}-000001", firstB);
    }

    [Fact]
    public async Task GenerateAsync_rejects_blank_prefix()
    {
        await using var scope = Services.CreateAsyncScope();
        var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();

        await Assert.ThrowsAsync<ArgumentException>(() => generator.GenerateAsync("   "));
    }

    [Fact]
    public async Task Persisted_format_width_matches_first_generation_request()
    {
        var prefix = UniquePrefix("WID");

        await using (var scope = Services.CreateAsyncScope())
        {
            var generator = scope.ServiceProvider.GetRequiredService<IReferenceGenerator>();
            await generator.GenerateAsync(prefix, width: 4);
        }

        await using var context = await Fixture.ContextFactory.CreateDbContextAsync();
        var row = await context.ReferenceSequences.AsNoTracking().SingleAsync(e => e.Prefix == prefix);

        Assert.Equal(4, row.FormatWidth);
    }
}