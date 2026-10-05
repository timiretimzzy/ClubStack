using ClubStack.Application.Exceptions;
using ClubStack.Application.Pos;
using ClubStack.Domain.Common;
using ClubStack.Domain.Inventory;
using ClubStack.Domain.Pos;
using ClubStack.Infrastructure.Persistence;
using ClubStack.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClubStack.Infrastructure.Pos;

/// <summary>
/// POS outlet and register configuration (D5 POS). Outlets are the selling points;
/// registers bind an outlet to the inventory location their sales draw stock from.
/// </summary>
public sealed class EntityFrameworkPosConfigurationService : IPosConfigurationService
{
    private readonly ClubStackDbContext _context;
    private readonly ISystemClock _clock;

    public EntityFrameworkPosConfigurationService(ClubStackDbContext context, ISystemClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<PosConfigurationState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var outlets = await _context.PosOutlets
            .AsNoTracking()
            .Where(o => o.Status == PosOutletStatus.Active)
            .OrderBy(o => o.Code)
            .ToListAsync(cancellationToken);

        var registers = await _context.PosRegisters
            .AsNoTracking()
            .Include(r => r.PosOutlet)
            .Include(r => r.InventoryLocation)
            .Where(r => r.Status == PosRegisterStatus.Active)
            .OrderBy(r => r.Code)
            .ToListAsync(cancellationToken);

        var sessionIds = new Dictionary<Guid, Guid>();
        if (registers.Count > 0)
        {
            var registerIds = registers.Select(r => r.Id).ToList();
            var openSessions = await _context.CashierSessions
                .AsNoTracking()
                .Where(s => s.Status == CashierSessionStatus.Open || s.Status == CashierSessionStatus.Active || s.Status == CashierSessionStatus.Closing)
                .Where(s => registerIds.Contains(s.RegisterId))
                .ToListAsync(cancellationToken);
            sessionIds = openSessions
                .GroupBy(s => s.RegisterId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.OpenedAtUtc).First().Id);
        }

        return new PosConfigurationState(
            outlets.Select(PosMappings.ToOutletDto).ToList(),
            registers.Select(r => PosMappings.ToRegisterDto(r, sessionIds.GetValueOrDefault(r.Id))).ToList());
    }

    public async Task<PosOutletDto> CreateOutletAsync(CreatePosOutletInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var code = PosServiceHelpers.NormalizeOptional(input.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(
                "The POS outlet could not be created.",
                PosServiceHelpers.FieldError("code", "A POS outlet code is required."));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The POS outlet could not be created.",
                PosServiceHelpers.FieldError("name", "A POS outlet name is required."));
        }

        if (await _context.PosOutlets.AnyAsync(o => o.Code == code, cancellationToken))
        {
            throw new ConflictException($"A POS outlet with code '{code}' already exists.");
        }

        var now = _clock.UtcNow;
        var outlet = new PosOutletEntity
        {
            Code = code,
            Name = input.Name.Trim(),
            Notes = PosServiceHelpers.NormalizeOptional(input.Notes),
            Status = PosOutletStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.PosOutlets.Add(outlet);
        await _context.SaveChangesAsync(cancellationToken);
        return PosMappings.ToOutletDto(outlet);
    }

    public async Task<PosRegisterDto> CreateRegisterAsync(CreatePosRegisterInput input, string actorUserId, CancellationToken cancellationToken = default)
    {
        var code = PosServiceHelpers.NormalizeOptional(input.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ValidationException(
                "The POS register could not be created.",
                PosServiceHelpers.FieldError("code", "A POS register code is required."));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException(
                "The POS register could not be created.",
                PosServiceHelpers.FieldError("name", "A POS register name is required."));
        }

        var outlet = await _context.PosOutlets
            .FirstOrDefaultAsync(o => o.Id == input.PosOutletId, cancellationToken)
            ?? throw new NotFoundException($"POS outlet '{input.PosOutletId}' was not found.");

        if (outlet.Status != PosOutletStatus.Active)
        {
            throw new ConflictException($"POS outlet '{outlet.Code}' is inactive.");
        }

        if (await _context.PosRegisters.AnyAsync(r => r.Code == code, cancellationToken))
        {
            throw new ConflictException($"A POS register with code '{code}' already exists.");
        }

        var location = await _context.InventoryLocations
            .FirstOrDefaultAsync(l => l.Id == input.InventoryLocationId, cancellationToken)
            ?? throw new ValidationException(
                "The POS register could not be created.",
                PosServiceHelpers.FieldError("inventoryLocationId", $"Inventory location '{input.InventoryLocationId}' was not found."));

        if (location.Status != StockLocationStatus.Active)
        {
            throw new ConflictException($"Inventory location '{location.Code}' is inactive.");
        }

        var now = _clock.UtcNow;
        var register = new PosRegisterEntity
        {
            Code = code,
            Name = input.Name.Trim(),
            PosOutletId = outlet.Id,
            InventoryLocationId = location.Id,
            Status = PosRegisterStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _context.PosRegisters.Add(register);
        await _context.SaveChangesAsync(cancellationToken);

        var created = await LoadRegisterAsync(register.Id, cancellationToken);
        return PosMappings.ToRegisterDto(created);
    }

    private async Task<PosRegisterEntity> LoadRegisterAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.PosRegisters
                   .Include(r => r.PosOutlet)
                   .Include(r => r.InventoryLocation)
                   .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
               ?? throw new NotFoundException($"POS register '{id}' was not found.");
    }
}