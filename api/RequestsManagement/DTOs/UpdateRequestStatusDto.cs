using System.ComponentModel.DataAnnotations;
using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class UpdateRequestStatusDto
{
    // Nullable so [Required] can tell "missing" apart from the enum default (New).
    [Required(ErrorMessage = "יש לציין סטטוס.")]
    public RequestStatus? Status { get; set; }

    // Base64-encoded RowVersion from the RequestDto previously fetched by the client.
    [Required(ErrorMessage = "יש לציין RowVersion.")]
    public string RowVersion { get; set; } = "";
}
