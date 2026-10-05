namespace ClubStack.Contracts.Common;

/// <summary>
/// Stable machine-readable error codes mapping 1:1 to Problem Details
/// <c>code</c> values emitted by the API error handling foundation.
/// </summary>
public static class ApiErrorCode
{
    public const string Validation = "validation";
    public const string AuthenticationRequired = "authentication_required";
    public const string AccessDenied = "access_denied";
    public const string NotFound = "not_found";
    public const string Conflict = "conflict";
    public const string IdempotentDuplicate = "idempotent_duplicate";
    public const string ConcurrencyConflict = "concurrency_conflict";
    public const string ServiceUnavailable = "service_unavailable";
    public const string InternalError = "internal_error";
}