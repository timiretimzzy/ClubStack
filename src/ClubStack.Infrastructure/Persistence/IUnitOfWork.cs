using ClubStack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClubStack.Infrastructure.Persistence;

/// <summary>
/// Ambient transaction boundary for command execution. Operations performed on
/// the scoped <see cref="ClubStackDbContext"/> (audit rows, idempotency records,
/// outbox messages) join the transaction opened here.
/// </summary>
public interface IUnitOfWork
{
    Task BeginAsync(CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly ClubStackDbContext _context;
    private IDbContextTransaction? _transaction;

    public EfUnitOfWork(ClubStackDbContext context)
    {
        _context = context;
    }

    public async Task BeginAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already active for this unit of work.");
        }

        // Npgsql's retry execution strategy cannot interoperate with a
        // user-initiated transaction unless the transaction is started from
        // within the strategy. The strategy only needs to wrap the BEGIN here;
        // commands executed on the transaction afterwards use the same connection.
        var strategy = _context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        });
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No active transaction to commit.");
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _transaction.CommitAsync(cancellationToken);

        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;

        // Nothing from this unit of work was persisted, so the tracked Added/Modified
        // graph must not be re-saved by any later SaveChanges on the same scoped context
        // (for example idempotency failure marking after a rollback).
        //
        // ChangeTracker.Clear is required rather than setting EntityState.Detached
        // entity by entity: detaching one side of a graph runs the navigation fixup,
        // which throws when a required foreign key would be orphaned. That turned a
        // failed commit into a misleading "association has been severed" error and hid
        // the real cause. Clear abandons the graph without touching foreign keys.
        _context.ChangeTracker.Clear();
    }
}