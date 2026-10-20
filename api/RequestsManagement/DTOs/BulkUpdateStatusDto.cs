using System.ComponentModel.DataAnnotations;
using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

/// <summary>
/// Sets the same target status on up to <see cref="MaxItems"/> requests. Each item carries its own
/// RowVersion, so every request is concurrency-checked independently (partial success).
/// </summary>
public class BulkUpdateStatusDto
{
    public const int MaxItems = 100;

    // Nullable so [Required] can tell "missing" apart from the enum default (New).
    [Required(ErrorMessage = "Status is required.")]
    public RequestStatus? Status { get; set; }

    [Required(ErrorMessage = "Items is required.")]
    [MinLength(1, ErrorMessage = "Items must contain at least 1 request.")]
    [MaxLength(MaxItems, ErrorMessage = "Items must contain at most 100 requests.")]
    public List<BulkUpdateStatusItemDto> Items { get; set; } = [];
}

public class BulkUpdateStatusItemDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Id must be a positive integer.")]
    public int Id { get; set; }

    // Base64-encoded RowVersion from the RequestDto the client last read.
    [Required(ErrorMessage = "RowVersion is required.")]
    public string RowVersion { get; set; } = "";
}
