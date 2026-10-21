using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Exceptions;
using RequestsManagement.Extensions;

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
            // Client aborted (e.g. switchMap cancelling a search): not a server error, so no 500 and no Error log.
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

    private static string WithoutControlCharacters(string value) =>
        string.Create(value.Length, value, (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsControl(source[i]) ? ' ' : source[i];
            }
        });

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "המשאב לא נמצא"),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "התנגשות בעדכון"),
            ValidationException => (StatusCodes.Status400BadRequest, ValidationMessagesExtensions.InvalidInputTitle),
            _ => (StatusCodes.Status500InternalServerError, "שגיאה בלתי צפויה")
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            // Some messages echo a query value: control characters are replaced to prevent forged log lines.
            logger.LogInformation("{Method} {Path} -> {StatusCode} {Title}: {Message}",
                context.Request.Method, context.Request.Path, statusCode, title, WithoutControlCharacters(ex.Message));
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError
                ? "אירעה שגיאה בלתי צפויה. נסו שוב מאוחר יותר."
                : ex.Message,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = statusCode;
        // WriteAsJsonAsync would otherwise overwrite the Content-Type with application/json.
        await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
    }
}
