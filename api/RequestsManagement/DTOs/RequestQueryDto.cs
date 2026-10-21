using System.ComponentModel.DataAnnotations;
using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class RequestQueryDto
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    [MaxLength(200, ErrorMessage = "החיפוש יכול להכיל עד 200 תווים.")]
    public string? Search { get; set; }

    public RequestStatus? Status { get; set; }

    public RequestPriority? Priority { get; set; }

    [MaxLength(200, ErrorMessage = "שם הארגון יכול להכיל עד 200 תווים.")]
    public string? OrganizationName { get; set; }

    [MaxLength(100, ErrorMessage = "שם המטפל יכול להכיל עד 100 תווים.")]
    public string? AssignedTo { get; set; }

    // Half-open range on CreatedAt: CreatedFrom <= CreatedAt < CreatedTo.
    public DateTime? CreatedFrom { get; set; }

    public DateTime? CreatedTo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "מספר העמוד חייב להיות 1 לפחות.")]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize, ErrorMessage = "גודל העמוד חייב להיות בין 1 ל-100.")]
    public int PageSize { get; set; } = DefaultPageSize;

    public const string DefaultSortBy = "createdAt";
    public const string DefaultSortDirection = "desc";

    // Nullable: "?sortBy=" binds as null, not as the default; the service applies the default.
    public string? SortBy { get; set; } = DefaultSortBy;

    public string? SortDirection { get; set; } = DefaultSortDirection;
}
