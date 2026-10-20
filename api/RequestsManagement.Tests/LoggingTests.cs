using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Middleware;
using RequestsManagement.Services;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class LoggingTests
{
    private const string PersonalName = "דנה כהן";

    // --- RequestService --------------------------------------------------------

    [Fact]
    public async Task SuccessfulUpdate_LogsInformationWithIdsAndStatuses_ButNotTheUser()
    {
        var (dbName, id, rowVersion) = await SeedAsync();
        var logger = new ListLogger<RequestService>();

        await using (var context = CreateContext(dbName))
        {
            await CreateService(context, new StubCurrentUser(PersonalName), logger).UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion });
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(id, entry.Values["RequestId"]);
        Assert.Equal(RequestStatus.New, entry.Values["PreviousStatus"]);
        Assert.Equal(RequestStatus.InProgress, entry.Values["NewStatus"]);
        Assert.DoesNotContain(PersonalName, entry.Message);
        Assert.DoesNotContain(entry.Values.Values, v => v as string == PersonalName);
    }

    [Fact]
    public async Task StaleRowVersion_LogsWarningWithRequestId()
    {
        var (dbName, id, _) = await SeedAsync();
        var logger = new ListLogger<RequestService>();

        await using (var context = CreateContext(dbName))
        {
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
                CreateService(context, logger: logger).UpdateStatusAsync(id, new UpdateRequestStatusDto
                {
                    Status = RequestStatus.InProgress,
                    RowVersion = Convert.ToBase64String(BitConverter.GetBytes(99L))
                }));
        }

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(id, entry.Values["RequestId"]);
    }

    // --- ExceptionHandlingMiddleware -------------------------------------------

    [Fact]
    public async Task ClientCancelledRequest_IsLoggedAsInformation_WithoutWritingA500()
    {
        var logger = new ListLogger<ExceptionHandlingMiddleware>();
        using var aborted = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ =>
        {
            aborted.Cancel(); // the client disconnects while the query is running
            throw new OperationCanceledException(aborted.Token);
        }, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    [Fact]
    public async Task OperationCanceledWithoutClientAbort_IsStillAServerError()
    {
        // Only a *client* abort is benign; a timeout/cancellation inside the server is a real 500.
        var logger = new ListLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new OperationCanceledException(), logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }

    [Theory]
    [InlineData(typeof(ValidationException), StatusCodes.Status400BadRequest)]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound)]
    [InlineData(typeof(ConcurrencyConflictException), StatusCodes.Status409Conflict)]
    public async Task ClientErrors_AreLoggedAsInformation(Type exceptionType, int expectedStatus)
    {
        var logger = new ListLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw (Exception)Activator.CreateInstance(exceptionType, "boom")!, logger);

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(expectedStatus, entry.Values["StatusCode"]);
    }

    private static async Task<(string DbName, int Id, string RowVersion)> SeedAsync()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var context = CreateContext(dbName);
        var entity = NewRequest("A", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow.AddDays(-1));
        context.Requests.Add(entity);
        await context.SaveChangesAsync();
        return (dbName, entity.Id, Convert.ToBase64String(entity.RowVersion));
    }
}
