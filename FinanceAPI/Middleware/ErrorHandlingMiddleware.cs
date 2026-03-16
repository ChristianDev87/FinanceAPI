using System.Text.Json;

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
        (int statusCode, string? message) = ex switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, ex.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
            InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == 500)
        {
            _logger.LogError(ex, "Unhandled exception");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new { error = message, statusCode };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
