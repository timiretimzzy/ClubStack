using ClubStack.Application.Correlation;
using ClubStack.Contracts.Common;

namespace ClubStack.Api.Correlation;

/// <summary>
/// Ensures every request has a correlation ID: an incoming valid value is
/// accepted, otherwise one is generated. The ID is exposed on the response,
/// available through <see cref="ICorrelationContext"/> for application/infrastructure
/// layers, and copied to ASP.NET's TraceIdentifier so diagnostics stay linked.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContextWriter writer)
    {
        string correlationId;
        var incoming = context.Request.Headers[ApiConstants.CorrelationHeaderName].FirstOrDefault();

        if (CorrelationIdPolicy.TryNormalize(incoming, out var normalized))
        {
            correlationId = normalized;
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        writer.Set(correlationId, incoming is null || normalized is null);

        context.TraceIdentifier = correlationId;

        if (!context.Response.HasStarted)
        {
            context.Response.Headers[ApiConstants.CorrelationHeaderName] = correlationId;
        }

        await _next(context);
    }
}