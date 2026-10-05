namespace ClubStack.Application.Bootstrap;

public sealed record BootstrapStatus(
    bool Completed,
    bool AdministratorCreated,
    bool ClubCreated,
    Guid? ClubId,
    string? CompletedAtUtc);

public sealed record CreateAdministratorInput(
    string UserName,
    string Email,
    string Password,
    string FirstName,
    string LastName);

public sealed record CreateClubInput(
    string Name,
    string Code,
    string? CurrencyCode,
    string? Country);

/// <summary>
/// Controlled first-run bootstrap (Phase 9 §27). The bootstrap endpoints are
/// only functional when the corresponding marker is not yet present
/// (<c>AnonymouslyDiscoverable</c>/allowlist); once the D1 acceptance state is
/// reached the markers are set and bootstrap is permanently disabled.
/// </summary>
public interface IBootstrapService
{
    Task<BootstrapStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<BootstrapStatus> CreateAdministratorAsync(CreateAdministratorInput input, CancellationToken cancellationToken = default);

    Task<BootstrapStatus> CreateClubAsync(CreateClubInput input, CancellationToken cancellationToken = default);
}