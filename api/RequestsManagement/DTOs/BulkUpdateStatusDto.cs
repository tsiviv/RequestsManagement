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
    [Required(ErrorMessage = "יש לציין סטטוס.")]
    public RequestStatus? Status { get; set; }

    [Required(ErrorMessage = "יש לשלוח רשימת פניות.")]
    [MinLength(1, ErrorMessage = "יש לשלוח פנייה אחת לפחות.")]
    [MaxLength(MaxItems, ErrorMessage = "ניתן לעדכן עד 100 פניות בבקשה אחת.")]
    public List<BulkUpdateStatusItemDto> Items { get; set; } = [];
}

public class BulkUpdateStatusItemDto
{
    [Range(1, int.MaxValue, ErrorMessage = "מזהה הפנייה חייב להיות מספר חיובי.")]
    public int Id { get; set; }

    // Base64-encoded RowVersion from the RequestDto the client last read.
    [Required(ErrorMessage = "יש לציין RowVersion.")]
    public string RowVersion { get; set; } = "";
}
