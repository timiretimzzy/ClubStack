namespace ClubStack.Application.Exceptions;

public abstract class ClubStackException : Exception
{
    protected ClubStackException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a requested resource does not exist. Maps to HTTP 404.</summary>
public sealed class NotFoundException : ClubStackException
{
    public NotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>Thrown when authentication state is invalid. Maps to HTTP 401.</summary>
public sealed class UnauthorizedAccessException : ClubStackException
{
    public UnauthorizedAccessException(string message)
        : base(message)
    {
    }
}

/// <summary>Thrown when the caller is authenticated but lacks permission. Maps to HTTP 403.</summary>
public sealed class ForbiddenException : ClubStackException
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}

/// <summary>Thrown when a request violates an invariant. Maps to HTTP 409.</summary>
public sealed class ConflictException : ClubStackException
{
    public ConflictException(string message)
        : base(message)
    {
    }
}

/// <summary>Thrown when a second writer loses an optimistic-concurrency race. Maps to HTTP 409.</summary>
public sealed class ConcurrencyException : ClubStackException
{
    public ConcurrencyException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a duplicate idempotency key is used with a different payload. Maps to HTTP 409.</summary>
public sealed class IdempotentDuplicateException : ClubStackException
{
    public IdempotentDuplicateException(string message)
        : base(message)
    {
    }
}

/// <summary>Thrown when request validation fails. Maps to HTTP 400 with field errors.</summary>
public sealed class ValidationException : ClubStackException
{
    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IDictionary<string, string[]> Errors { get; }
}