using Microsoft.AspNetCore.Identity;

namespace ClubStack.Infrastructure.Identity;

public sealed class ClubStackUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ClubStackRole : IdentityRole<Guid>
{
    public ClubStackRole()
    {
    }

    public ClubStackRole(string roleName)
        : base(roleName)
    {
    }

    /// <summary>System roles come from the baseline catalogue and cannot be deleted.</summary>
    public bool IsSystem { get; set; }
}