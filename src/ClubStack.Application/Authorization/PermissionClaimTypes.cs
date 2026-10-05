namespace ClubStack.Application.Authorization;

public static class PermissionClaimTypes
{
    /// <summary>Claim type used for effective permission claims on a principal.</summary>
    public const string Permission = "permission";

    /// <summary>Claim type used to signal a deactivated account.</summary>
    public const string AccountStatus = "account_status";

    public const string Deactivated = "deactivated";

    /// <summary>
    /// Claim carrying the <c>user_sessions.id</c> of the signed-in session
    /// (Phase 8 §23). Added at sign-in and validated by the session enforcement
    /// middleware on every authenticated request.
    /// </summary>
    public const string Session = "session_id";
}

public static class PermissionPolicies
{
    /// <summary>Names the authorization policy for a permission claim.</summary>
    public static string For(string permission) => $"Permission:{permission}";
}