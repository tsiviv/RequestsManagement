using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class BulkUpdateStatusResultDto
{
    public int Requested { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }

    // One entry per input item, in input order.
    public List<BulkUpdateStatusItemResultDto> Results { get; set; } = [];
}

public class BulkUpdateStatusItemResultDto
{
    public int Id { get; set; }
    public BulkUpdateOutcome Outcome { get; set; }

    // Why the item failed; null when Updated.
    public string? Error { get; set; }

    // The updated request (with its new RowVersion) when Updated; null otherwise.
    public RequestDto? Request { get; set; }
}
