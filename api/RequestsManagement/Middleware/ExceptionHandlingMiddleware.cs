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
        catch (Exception) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away (e.g. Angular switchMap cancelling a superseded search), so the
            // CancellationToken aborted the I/O. Not a server error: nobody is left to read a 500,
            // and logging it as Error would flood the log during normal typing. Filtering on the
            // token rather than the exception type also covers providers that surface the abort
            // as their own exception.
            logger.LogInformation("Request {Method} {Path} was cancelled by the client", context.Request.Method, context.Request.Path);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            }
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
            // Client errors are expected traffic, not warnings. Messages are the app's own
            // exception texts (ids, enum names), never request bodies or header values.
            logger.LogInformation("{Method} {Path} -> {StatusCode} {Title}: {Message}",
                context.Request.Method, context.Request.Path, statusCode, title, ex.Message);
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

        context.Response.StatusCode = statusCode;
        // The content type must be passed here: WriteAsJsonAsync otherwise overwrites a previously
        // set Content-Type with application/json (caught by the integration tests).
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
    }
}
