namespace RequestsManagement.DTOs;

public class RequestFilterOptionsDto
{
    public List<string> Organizations { get; set; } = [];
    public List<string> Assignees { get; set; } = [];
}
