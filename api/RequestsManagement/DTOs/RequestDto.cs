using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class RequestDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public RequestStatus Status { get; set; }
    public RequestPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public IReadOnlyList<RequestStatus> AllowedNextStatuses { get; set; } = [];

    public string RowVersion { get; set; } = "";
}
