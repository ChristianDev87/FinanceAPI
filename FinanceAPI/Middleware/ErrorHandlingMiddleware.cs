using System.Text.Json;
using FinanceAPI.Exceptions;

namespace FinanceAPI.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // Map provider-specific unique-constraint violations to 409 Conflict so that
        // race conditions on category names produce a clean API response instead of 500.
        if (IsUniqueConstraintViolation(ex))
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "A duplicate value violates a unique constraint.", statusCode = StatusCodes.Status409Conflict }),
                context.RequestAborted);
            return;
        }

        int statusCode = ex switch
        {
            UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
            FinanceAPI.Exceptions.NotFoundException => StatusCodes.Status404NotFound,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            FinanceAPI.Exceptions.ForbiddenException => StatusCodes.Status403Forbidden,
            ConflictException => StatusCodes.Status409Conflict,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        string message = statusCode == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : ex.Message;

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            string errorId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogError(ex, "Unhandled exception [{ErrorId}]", errorId);
            message = $"An unexpected error occurred. Reference: {errorId}";
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message, statusCode }), context.RequestAborted);
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        if (ex is Microsoft.Data.Sqlite.SqliteException sqEx)
        {
            return sqEx.SqliteErrorCode == 19 && sqEx.Message.Contains("UNIQUE");
        }

        if (ex is Npgsql.PostgresException pgEx)
        {
            return pgEx.SqlState == "23505";
        }

        if (ex is MySqlConnector.MySqlException myEx)
        {
            return myEx.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry;
        }

        return false;
    }
}
