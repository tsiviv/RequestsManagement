using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class RequestDto
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string OrganizationName { get; set; } = "";
    public RequestStatus Status { get; set; }
    public RequestPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Base64-encoded concurrency token. Clients must echo this back in
    // UpdateRequestStatusDto so the service can detect stale updates.
    public string RowVersion { get; set; } = "";
}
