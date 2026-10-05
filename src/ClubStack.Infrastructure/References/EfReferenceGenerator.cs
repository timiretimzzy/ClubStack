using System.Data;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubStack.Infrastructure.References;

/// <summary>
/// Database-backed reference generator. Uses PostgreSQL's
/// <c>UPDATE ... RETURNING</c> to allocate the next sequence number atomically
/// per prefix; concurrent requests never observe the same number.
/// </summary>
public sealed class EfReferenceGenerator : IReferenceGenerator
{
    private readonly ClubStackDbContext _context;

    public EfReferenceGenerator(ClubStackDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateAsync(string prefix, int width = 6, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("A reference prefix is required.", nameof(prefix));
        }

        prefix = prefix.Trim().ToUpperInvariant();

        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await EnsureSequenceRowAsync(connection, prefix, width, cancellationToken);

            await using var allocate = connection.CreateCommand();
            allocate.CommandText =
                "UPDATE \"reference_sequences\" SET \"LastNumber\" = \"LastNumber\" + 1 " +
                "WHERE \"Prefix\" = @prefix RETURNING \"LastNumber\"";
            allocate.Parameters.Add(new NpgsqlParameter("prefix", prefix));

            var raw = await allocate.ExecuteScalarAsync(cancellationToken);
            var sequence = Convert.ToInt64(raw);

            return new BusinessReference(prefix, sequence).ToFormattedString(width);
        }
        finally
        {
            if (wasClosed)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureSequenceRowAsync(
        System.Data.Common.DbConnection connection,
        string prefix,
        int width,
        CancellationToken cancellationToken)
    {
        await using var ensure = connection.CreateCommand();
        ensure.CommandText =
            "INSERT INTO \"reference_sequences\" (\"Id\", \"Prefix\", \"LastNumber\", \"FormatWidth\") " +
            "VALUES (@id, @prefix, 0, @width) ON CONFLICT (\"Prefix\") DO NOTHING";
        ensure.Parameters.Add(new NpgsqlParameter("id", Guid.CreateVersion7()));
        ensure.Parameters.Add(new NpgsqlParameter("prefix", prefix));
        ensure.Parameters.Add(new NpgsqlParameter("width", Math.Max(1, width)));
        await ensure.ExecuteNonQueryAsync(cancellationToken);
    }
}