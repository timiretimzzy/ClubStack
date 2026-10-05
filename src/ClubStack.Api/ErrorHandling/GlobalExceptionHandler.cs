using ClubStack.Application.Correlation;
using ClubStack.Application.Exceptions;
using ClubStack.Contracts.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UnauthorizedAccessException = ClubStack.Application.Exceptions.UnauthorizedAccessException;

namespace ClubStack.Api.ErrorHandling;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 Problem Details. Internal server
/// errors never leak stack traces, connection strings or secrets.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, detail) = Map(exception);

        var correlation = context.RequestServices
            .GetService<ICorrelationContext>()?.CorrelationId;

        if (status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing request. CorrelationId={CorrelationId}", correlation ?? "(none)");
        }
        else if (exception is DbUpdateConcurrencyException)
        {
            _logger.LogWarning(exception, "Optimistic concurrency conflict while processing request. CorrelationId={CorrelationId}", correlation ?? "(none)");
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ProblemTitle(code),
            Type = $"https://clubstack.local/problems/{code}",
            Detail = detail,
        };

        if (exception is ValidationException validation)
        {
            problem.Extensions["errors"] = validation.Errors;
        }

        problem.Extensions["code"] = code;
        if (!string.IsNullOrWhiteSpace(correlation))
        {
            problem.Extensions["correlationId"] = correlation;
        }

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private static (int Status, string Code, string? Detail) Map(Exception exception) => exception switch
    {
        NotFoundException => ((int)StatusCodes.Status404NotFound, ApiErrorCode.NotFound, exception.Message),
        ConflictException => ((int)StatusCodes.Status409Conflict, ApiErrorCode.Conflict, exception.Message),
        ConcurrencyException => ((int)StatusCodes.Status409Conflict, ApiErrorCode.ConcurrencyConflict, ConcurrencyDetail),
        DbUpdateConcurrencyException => ((int)StatusCodes.Status409Conflict, ApiErrorCode.ConcurrencyConflict, ConcurrencyDetail),
        IdempotentDuplicateException => ((int)StatusCodes.Status409Conflict, ApiErrorCode.IdempotentDuplicate, exception.Message),
        ValidationException => ((int)StatusCodes.Status400BadRequest, ApiErrorCode.Validation, exception.Message),
        UnauthorizedAccessException => ((int)StatusCodes.Status401Unauthorized, ApiErrorCode.AuthenticationRequired, "Authentication is required."),
        ForbiddenException => ((int)StatusCodes.Status403Forbidden, ApiErrorCode.AccessDenied, exception.Message),
        _ => ((int)StatusCodes.Status500InternalServerError, ApiErrorCode.InternalError, "An unexpected error occurred."),
    };

    private const string ConcurrencyDetail = "The resource was modified by another request. Retry the operation.";

    private static string ProblemTitle(string code) => code switch
    {
        ApiErrorCode.Validation => "Validation failed.",
        ApiErrorCode.AuthenticationRequired => "Authentication required.",
        ApiErrorCode.AccessDenied => "Access denied.",
        ApiErrorCode.NotFound => "Resource not found.",
        ApiErrorCode.Conflict => "The operation conflicts with the current state.",
        ApiErrorCode.IdempotentDuplicate => "Duplicate idempotent request.",
        ApiErrorCode.ConcurrencyConflict => "Concurrency conflict.",
        ApiErrorCode.ServiceUnavailable => "Service unavailable.",
        _ => "An error occurred while processing the request.",
    };
}