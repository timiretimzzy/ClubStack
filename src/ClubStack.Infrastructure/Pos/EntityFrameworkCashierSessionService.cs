using ClubStack.Application.Exceptions;
using ClubStack.Application.Pos;
using ClubStack.Application.References;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Domain.Pos;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClubStack.Infrastructure.Pos;

/// <summary>
/// Cashier session lifecycle (D5 Cashiering). Sessions open with a float, activate
/// on their first sale, close with a cash declaration and are reconciled. The cash
/// drawer's expected cash is derived from the opening float plus pay-ins/pay-outs
/// plus cash sales minus cash refunds recorded on the session.
/// </summary>
public sealed class EntityFrameworkCashierSessionService : ICashierSessionService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;
    private readonly IReferenceGenerator _referenceGenerator;

    public EntityFrameworkCashierSessionService(
        ClubStackDbContext context,
        ISystemClock clock,
        IReferenceGenerator referenceGenerator)
    {
        _context = context;
        _clock = clock;
        _referenceGenerator = referenceGenerator;
    }

    public async Task<CashierSessionDto> OpenAsync(OpenSessionInput input, string operatorUserId, CancellationToken cancellationToken = default)
    {
        PosServiceHelpers.EnsureNotNegative(input.OpeningFloat, "openingFloat");

        var register = await _context.PosRegisters
            .Include(r => r.PosOutlet)
            .Include(r => r.InventoryLocation)
            .FirstOrDefaultAsync(r => r.Id == input.RegisterId, cancellationToken)
            ?? throw new NotFoundException($"POS register '{input.RegisterId}' was not found.");

        if (register.Status != PosRegisterStatus.Active)
        {
            throw new ConflictException($"POS register '{register.Code}' is inactive.");
        }

        var openSession = await _context.CashierSessions
            .FirstOrDefaultAsync(s => s.RegisterId == register.Id
                                      && (s.Status == CashierSessionStatus.Open
                                          || s.Status == CashierSessionStatus.Active
                                          || s.Status == CashierSessionStatus.Closing), cancellationToken);
        if (openSession is not null)
        {
            throw new ConflictException($"POS register '{register.Code}' already has an active cashier session ('{openSession.SessionNumber}').");
        }

        var now = _clock.UtcNow;
        var session = new CashierSessionEntity
        {
            SessionNumber = await _referenceGenerator.GenerateAsync(CashierSessionNumber.Prefix, CashierSessionNumber.Width, cancellationToken),
            RegisterId = register.Id,
            CashierUserId = operatorUserId,
            Status = CashierSessionStatus.Open,
            OpeningFloat = input.OpeningFloat,
            DeclaredCash = input.OpeningFloat,
            ExpectedCash = input.OpeningFloat,
            VarianceApproved = false,
            Note = PosServiceHelpers.NormalizeOptional(input.Note),
            OpenedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        session.Movements.Add(new CashMovementEntity
        {
            MovementType = CashMovementType.Float,
            Amount = input.OpeningFloat,
            SignedAmount = input.OpeningFloat,
            Reason = "Opening float",
            CreatedByUserId = operatorUserId,
            CreatedAtUtc = now,
        });

        _context.CashierSessions.Add(session);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Lost the race against a concurrent open: the partial unique index on
            // cashier_sessions(RegisterId) for unfinished sessions rejected this insert.
            _context.ChangeTracker.Clear();
            throw new ConflictException(
                $"POS register '{register.Code}' already has an active cashier session.");
        }

        var created = await LoadSessionAsync(session.Id, tracked: true, cancellationToken);
        var expected = await ComputeExpectedCashAsync(session.Id, cancellationToken);
        return PosMappings.ToSessionDto(created, expected);
    }

    /// <summary>Detects a Postgres unique-constraint violation (SQLSTATE 23505).</summary>
    private static bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    public async Task<CashierSessionDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(id, tracked: false, cancellationToken);
        var expected = await ComputeExpectedCashAsync(id, cancellationToken);
        return PosMappings.ToSessionDto(session, expected);
    }

    public async Task<CashierSessionSearchResult> SearchAsync(CashierSessionSearchInput input, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = PosServiceHelpers.NormalizePage(input.Page, input.PageSize);

        IQueryable<CashierSessionEntity> query = _context.CashierSessions.AsNoTracking();

        if (input.RegisterId is { } registerId)
        {
            query = query.Where(s => s.RegisterId == registerId);
        }

        if (input.Status is { } statusText)
        {
            var status = PosServiceHelpers.ParseEnum<CashierSessionStatus>(statusText, "status");
            query = query.Where(s => s.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.OpenedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var sessionIds = items.Select(s => s.Id).ToList();
        var registerIds = items.Select(s => s.RegisterId).Distinct().ToList();
        var registers = await _context.PosRegisters
            .AsNoTracking()
            .Where(r => registerIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var expectedCash = await ComputeExpectedCashForSessionsAsync(sessionIds, cancellationToken);

        return new CashierSessionSearchResult(
            items.Select(s =>
            {
                s.Register = registers[s.RegisterId];
                return PosMappings.ToSessionDto(s, expectedCash[s.Id], includeMovements: false);
            }).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<CashierSessionDto> AddCashMovementAsync(Guid id, CashMovementInput input, string operatorUserId, CancellationToken cancellationToken = default)
    {
        PosServiceHelpers.EnsureGreaterThanZero(input.Amount, "amount");

        var movementType = PosServiceHelpers.ParseEnum<CashMovementType>(input.MovementType, "movementType");
        if (movementType == CashMovementType.Float)
        {
            throw new ValidationException(
                "The cash movement could not be recorded.",
                PosServiceHelpers.FieldError("movementType", "The opening float is recorded when the session opens; only pay-ins and pay-outs can be added later."));
        }

        var session = await LoadSessionAsync(id, tracked: true, cancellationToken);
        EnsureSessionAcceptingMovements(session);

        var signed = movementType == CashMovementType.PayIn ? input.Amount : -input.Amount;

        var now = _clock.UtcNow;
        var movement = new CashMovementEntity
        {
            SessionId = id,
            MovementType = movementType,
            Amount = input.Amount,
            SignedAmount = signed,
            Reason = PosServiceHelpers.NormalizeOptional(input.Reason),
            CreatedByUserId = operatorUserId,
            CreatedAtUtc = now,
        };
        // Add through the context so the row is tracked as Added; relying on
        // navigation-collection fixup alone promotes the pre-keyed child to
        // Modified and emits an UPDATE that matches zero rows.
        _context.CashMovements.Add(movement);
        session.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        var updated = await LoadSessionAsync(id, tracked: true, cancellationToken);
        var expected = await ComputeExpectedCashAsync(id, cancellationToken);
        return PosMappings.ToSessionDto(updated, expected);
    }

    public async Task<CashierSessionDto> CloseAsync(Guid id, CloseSessionInput input, string operatorUserId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(id, tracked: true, cancellationToken);
        if (!CashierSessionStateTransitions.CanTransition(session.Status, CashierSessionStatus.Closing))
        {
            throw new ConflictException($"Session '{session.SessionNumber}' cannot be closed from its '{session.Status}' state.");
        }

        PosServiceHelpers.EnsureNotNegative(input.DeclaredCash, "declaredCash");

        var expected = await ComputeExpectedCashAsync(id, cancellationToken);
        var variance = PosCashMath.Variance(input.DeclaredCash, expected);
        var now = _clock.UtcNow;

        session.Status = CashierSessionStatus.Closing;
        session.ClosingStartedAtUtc = now;
        session.UpdatedAtUtc = now;

        if (!CashierSessionStateTransitions.CanTransition(session.Status, CashierSessionStatus.Closed))
        {
            throw new ConflictException($"Session '{session.SessionNumber}' cannot reach its closed state.");
        }

        session.Status = CashierSessionStatus.Closed;
        session.ClosedAtUtc = now;
        session.DeclaredCash = input.DeclaredCash;
        session.ExpectedCash = expected;
        session.Variance = variance;
        session.VarianceApproved = variance == 0m;
        session.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        var closed = await LoadSessionAsync(id, tracked: true, cancellationToken);
        return PosMappings.ToSessionDto(closed, expected);
    }

    public async Task<CashierSessionDto> ReconcileAsync(Guid id, string operatorUserId, CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(id, tracked: true, cancellationToken);
        if (!CashierSessionStateTransitions.CanTransition(session.Status, CashierSessionStatus.Reconciled))
        {
            throw new ConflictException($"Session '{session.SessionNumber}' cannot be reconciled from its '{session.Status}' state.");
        }

        if (session.Variance is { } variance && variance != 0m && !session.VarianceApproved)
        {
            throw new ConflictException($"Session '{session.SessionNumber}' has a cash variance of {variance:F2} that must be approved before reconciliation.");
        }

        var now = _clock.UtcNow;
        session.Status = CashierSessionStatus.Reconciled;
        session.ReconciledAtUtc = now;
        session.ReconciledByUserId = operatorUserId;
        session.UpdatedAtUtc = now;

        await _context.SaveChangesAsync(cancellationToken);

        var reconciled = await LoadSessionAsync(id, tracked: true, cancellationToken);
        var expected = await ComputeExpectedCashAsync(id, cancellationToken);
        return PosMappings.ToSessionDto(reconciled, expected);
    }

    private static void EnsureSessionAcceptingMovements(CashierSessionEntity session)
    {
        if (session.Status is not (CashierSessionStatus.Open or CashierSessionStatus.Active or CashierSessionStatus.Closing))
        {
            throw new ConflictException($"Session '{session.SessionNumber}' is {session.Status.ToString().ToLowerInvariant()} and no longer accepts cash movements.");
        }
    }

    private async Task<CashierSessionEntity> LoadSessionAsync(Guid id, bool tracked = true, CancellationToken cancellationToken = default)
    {
        IQueryable<CashierSessionEntity> query = _context.CashierSessions
            .Include(s => s.Register)
                .ThenInclude(r => r.PosOutlet)
            .Include(s => s.Register)
                .ThenInclude(r => r.InventoryLocation)
            .Include(s => s.Movements);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
               ?? throw new NotFoundException($"Cashier session '{id}' was not found.");
    }

    /// <summary>Derived expected cash on hand for a session (opening float + cash sales − cash refunds + pay-ins − pay-outs).</summary>
    internal Task<decimal> ComputeExpectedCashAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        return ComputeForSessionsAsync(new[] { sessionId }, cancellationToken);
    }

    private async Task<decimal> ComputeForSessionsAsync(IReadOnlyList<Guid> sessionIds, CancellationToken cancellationToken)
    {
        var dictionary = await ComputeExpectedCashForSessionsAsync(sessionIds, cancellationToken);
        return dictionary[sessionIds[0]];
    }

    internal async Task<Dictionary<Guid, decimal>> ComputeExpectedCashForSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, decimal>();
        if (sessionIds.Count == 0)
        {
            return result;
        }

        var movements = await _context.CashMovements
            .AsNoTracking()
            .Where(m => sessionIds.Contains(m.SessionId))
            .ToListAsync(cancellationToken);

        var cashFromSales = await _context.SalePayments
            .AsNoTracking()
            .Where(p => p.CashierSessionId != null && sessionIds.Contains(p.CashierSessionId.Value))
            .Where(p => p.Kind == "Cash")
            .Where(p => !p.IsRefund)
            .GroupBy(p => p.CashierSessionId!.Value)
            .Select(g => new { SessionId = g.Key, Net = g.Sum(p => (decimal?)(p.Amount - (p.RefundedAmount ?? 0m))) })
            .ToListAsync(cancellationToken);

        var cashRefunded = await _context.SalePayments
            .AsNoTracking()
            .Where(p => p.CashierSessionId != null && sessionIds.Contains(p.CashierSessionId.Value))
            .Where(p => p.Kind == "Cash")
            .Where(p => p.IsRefund)
            .GroupBy(p => p.CashierSessionId!.Value)
            .Select(g => new { SessionId = g.Key, PaidOut = g.Sum(p => (decimal?)(p.Amount - (p.RefundedAmount ?? 0m))) })
            .ToListAsync(cancellationToken);

        foreach (var sessionId in sessionIds)
        {
            var openingFloat = movements.Where(m => m.SessionId == sessionId && m.MovementType == CashMovementType.Float).Sum(m => m.Amount);
            var payIns = movements.Where(m => m.SessionId == sessionId && m.MovementType == CashMovementType.PayIn).Sum(m => m.Amount);
            var payOuts = movements.Where(m => m.SessionId == sessionId && m.MovementType == CashMovementType.PayOut).Sum(m => m.Amount);
            var cashSales = cashFromSales.FirstOrDefault(c => c.SessionId == sessionId)?.Net ?? 0m;
            var cashRefunds = cashRefunded.FirstOrDefault(c => c.SessionId == sessionId)?.PaidOut ?? 0m;

            result[sessionId] = PosCashMath.ExpectedCashOnHand(openingFloat, cashSales, cashRefunds, payIns, payOuts);
        }

        return result;
    }
}