using System.ComponentModel.DataAnnotations;

namespace ClubStack.Api.Contracts;

public sealed record LoginRequest
{
    [Required, MaxLength(256)]
    public string UserName { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponse
{
    public bool RequiresTwoFactor { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}

public sealed record CurrentUserResponse
{
    public required string UserName { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public required IReadOnlyList<string> Permissions { get; init; }

    public required IReadOnlyList<string> Roles { get; init; }
}