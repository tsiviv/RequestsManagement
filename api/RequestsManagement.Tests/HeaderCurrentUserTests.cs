using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;
using RequestsManagement.Services;
using Xunit;
using static RequestsManagement.Tests.TestData;

namespace RequestsManagement.Tests;

public class HeaderCurrentUserTests
{
    private static HeaderCurrentUser WithHeader(params string[] values)
    {
        var context = new DefaultHttpContext();
        if (values.Length > 0)
        {
            context.Request.Headers[HeaderCurrentUser.HeaderName] = values;
        }
        return new HeaderCurrentUser(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void PlainAsciiName_IsReturnedTrimmed()
    {
        Assert.Equal("dana", WithHeader("  dana ").UserName);
    }

    [Fact]
    public void PercentEncodedUtf8_IsDecoded()
    {
        // What the browser sends for encodeURIComponent("דנה כהן").
        var encoded = Uri.EscapeDataString("דנה כהן");
        Assert.Equal("דנה כהן", WithHeader(encoded).UserName);
    }

    [Theory]
    [InlineData]                // header absent
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("%20%20")]      // decodes to whitespace only
    [InlineData("a%0Ab")]       // decodes to a control character (newline)
    public void MissingOrInvalid_ThrowsValidationException(params string[] values)
    {
        Assert.Throws<ValidationException>(() => WithHeader(values).UserName);
    }

    [Fact]
    public void LongerThanHistoryColumn_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => WithHeader(new string('a', 101)).UserName);
        Assert.Equal(100, WithHeader(new string('a', 100)).UserName.Length);
    }

    [Fact]
    public void SpecifiedTwice_ThrowsValidationException()
    {
        Assert.Throws<ValidationException>(() => WithHeader("dana", "yossi").UserName);
    }

    [Fact]
    public async Task UpdateStatusAsync_WithoutUser_Returns400BeforeTouchingTheRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        int id;
        string rowVersion;
        await using (var seed = CreateContext(dbName))
        {
            var entity = NewRequest("A", "Org", RequestStatus.New, RequestPriority.Low, DateTime.UtcNow);
            seed.Requests.Add(entity);
            await seed.SaveChangesAsync();
            (id, rowVersion) = (entity.Id, Convert.ToBase64String(entity.RowVersion));
        }

        await using (var context = CreateContext(dbName))
        {
            var service = CreateStatusService(context, new MissingCurrentUser());

            await Assert.ThrowsAsync<ValidationException>(() => service.UpdateStatusAsync(id,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion }));
            // Rejected as bad input even for a request that does not exist (400, not 404).
            await Assert.ThrowsAsync<ValidationException>(() => service.UpdateStatusAsync(999,
                new UpdateRequestStatusDto { Status = RequestStatus.InProgress, RowVersion = rowVersion }));
        }

        await using var verify = CreateContext(dbName);
        Assert.Equal(RequestStatus.New, (await verify.Requests.SingleAsync(r => r.Id == id)).Status);
    }
}
