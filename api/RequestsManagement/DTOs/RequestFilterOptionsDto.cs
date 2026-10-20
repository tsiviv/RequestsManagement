namespace RequestsManagement.DTOs;

/// <summary>Distinct values for the exact-match filters, so clients offer a list instead of free text.</summary>
public class RequestFilterOptionsDto
{
    public List<string> Organizations { get; set; } = [];
    public List<string> Assignees { get; set; } = [];
}
