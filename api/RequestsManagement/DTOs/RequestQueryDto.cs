using System.ComponentModel.DataAnnotations;
using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class RequestQueryDto
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    public string? Search { get; set; }

    public RequestStatus? Status { get; set; }

    public RequestPriority? Priority { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "PageSize must be between 1 and 100.")]
    public int PageSize { get; set; } = DefaultPageSize;

    // Whitelisted against SortableFields in RequestService; not free-form SQL.
    public string SortBy { get; set; } = "createdAt";

    public string SortDirection { get; set; } = "desc";
}
