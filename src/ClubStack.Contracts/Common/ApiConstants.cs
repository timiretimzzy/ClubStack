namespace ClubStack.Contracts.Common;

/// <summary>Stable cross-cutting constants shared by API, application and infrastructure.</summary>
public static class ApiConstants
{
    public const int ApiVersionMajor = 1;
    public const string ApiVersion = "1.0";
    public const string ApiVersionRoute = "v1";
    public const string ApiRoutePrefix = "/api/v1";

    public const string CorrelationHeaderName = "X-Correlation-ID";
    public const string RequestIdHeaderName = "X-Request-ID";

    public const string HealthLivenessPath = "/health";
    public const string HealthReadinessPath = "/health/ready";
}