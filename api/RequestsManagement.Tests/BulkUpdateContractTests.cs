using System.ComponentModel.DataAnnotations;
using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using Xunit;

namespace RequestsManagement.Tests;

// The attribute-level part of the bulk contract (what [ApiController] rejects with 400 before the
// service runs). Cross-item rules (duplicate ids, base64) belong to the service and are tested with it.
public class BulkUpdateContractTests
{
    private static List<string> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "").ToList();
    }

    private static BulkUpdateStatusDto WithItems(int count, RequestStatus? status = RequestStatus.InProgress) => new()
    {
        Status = status,
        Items = Enumerable.Range(1, count).Select(i => new BulkUpdateStatusItemDto { Id = i, RowVersion = "AAAAAAAAB9E=" }).ToList()
    };

    [Theory]
    [InlineData(1)]
    [InlineData(BulkUpdateStatusDto.MaxItems)]
    public void ItemCountWithinLimits_IsValid(int count)
    {
        Assert.Empty(Validate(WithItems(count)));
    }

    [Theory]
    [InlineData(0, "at least 1")]
    [InlineData(BulkUpdateStatusDto.MaxItems + 1, "at most 100")]
    public void ItemCountOutsideLimits_IsRejected(int count, string expectedMessage)
    {
        Assert.Contains(Validate(WithItems(count)), m => m.Contains(expectedMessage));
    }

    [Fact]
    public void MissingStatus_IsRejected_RatherThanDefaultingToNew()
    {
        Assert.Contains("Status is required.", Validate(WithItems(1, status: null)));
    }

    [Fact]
    public void MissingItems_IsRejected()
    {
        Assert.Contains("Items is required.", Validate(new BulkUpdateStatusDto { Status = RequestStatus.InProgress, Items = null! }));
    }

    [Theory]
    [InlineData(0, "AAAAAAAAB9E=", "Id must be a positive integer.")]
    [InlineData(-5, "AAAAAAAAB9E=", "Id must be a positive integer.")]
    [InlineData(1, "", "RowVersion is required.")]
    public void InvalidItem_IsRejected(int id, string rowVersion, string expectedMessage)
    {
        Assert.Contains(expectedMessage, Validate(new BulkUpdateStatusItemDto { Id = id, RowVersion = rowVersion }));
    }
}
