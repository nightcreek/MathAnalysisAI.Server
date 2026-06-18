using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.ExceptionHandling;

public static class ApiExceptionClassifier
{
    public static bool IsTransientDependencyFailure(Exception exception)
    {
        return exception is HttpRequestException
            or TimeoutException
            or TaskCanceledException
            or OperationCanceledException
            || IsDatabaseFailure(exception);
    }

    public static bool IsDatabaseFailure(Exception exception)
    {
        if (exception is DbUpdateException)
        {
            return true;
        }

        return FindAny(exception, candidate =>
            candidate.GetType().Name is "SqlException" or "DbException");
    }

    public static (int statusCode, string errorCode, string message, bool isRetryable) Classify(Exception exception)
    {
        if (IsDatabaseFailure(exception))
        {
            return (
                StatusCodes.Status503ServiceUnavailable,
                "DATABASE_UNAVAILABLE",
                "Database is temporarily unavailable or schema is not ready.",
                true);
        }

        if (exception is HttpRequestException or TimeoutException or TaskCanceledException)
        {
            return (
                StatusCodes.Status503ServiceUnavailable,
                "DEPENDENCY_UNAVAILABLE",
                "Upstream dependency is temporarily unavailable.",
                true);
        }

        if (exception is ArgumentException or FormatException)
        {
            return (
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                exception.Message,
                false);
        }

        return (
            StatusCodes.Status500InternalServerError,
            "INTERNAL_SERVER_ERROR",
            "An unexpected error occurred.",
            false);
    }

    private static bool FindAny(Exception exception, Func<Exception, bool> predicate)
    {
        for (var current = exception; current != null; current = current.InnerException!)
        {
            if (predicate(current))
            {
                return true;
            }
        }

        return false;
    }
}
