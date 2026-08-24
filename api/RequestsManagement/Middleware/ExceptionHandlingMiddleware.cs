using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Exceptions;

namespace RequestsManagement.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Concurrency conflict"),
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning("{Title}: {Message}", title, ex.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            // Never leak internal exception details for 500s; app exceptions carry a safe message.
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred. Please try again later."
                : ex.Message,
            Instance = context.Request.Path
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
