using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class BulkUpdateStatusResultDto
{
    public int Requested { get; set; }
    public int Updated { get; set; }
    public int Failed { get; set; }

    public List<BulkUpdateStatusItemResultDto> Results { get; set; } = [];
}

public class BulkUpdateStatusItemResultDto
{
    public int Id { get; set; }
    public BulkUpdateOutcome Outcome { get; set; }

    public string? Error { get; set; }

    public RequestDto? Request { get; set; }
}
