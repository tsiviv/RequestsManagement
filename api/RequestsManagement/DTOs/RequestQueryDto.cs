using System.ComponentModel.DataAnnotations;
using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class RequestQueryDto
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    // Partial match on Title or OrganizationName.
    [MaxLength(200, ErrorMessage = "Search must be at most 200 characters.")]
    public string? Search { get; set; }

    public RequestStatus? Status { get; set; }

    public RequestPriority? Priority { get; set; }

    // Exact match (unlike Search) so it can seek on IX_Requests_OrganizationName_CreatedAt.
    [MaxLength(200, ErrorMessage = "OrganizationName must be at most 200 characters.")]
    public string? OrganizationName { get; set; }

    // Exact match; seeks on IX_Requests_AssignedTo_CreatedAt.
    [MaxLength(100, ErrorMessage = "AssignedTo must be at most 100 characters.")]
    public string? AssignedTo { get; set; }

    // Half-open range on CreatedAt: CreatedFrom <= CreatedAt < CreatedTo.
    // To include a whole last day, pass the following day as CreatedTo.
    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = DefaultPageSize;

    // Whitelisted against SortableFields in RequestService; not free-form SQL.
    public string SortBy { get; set; } = "createdAt";

    public string SortDirection { get; set; } = "desc";
}
