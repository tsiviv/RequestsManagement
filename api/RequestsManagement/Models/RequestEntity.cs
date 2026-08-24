using RequestsManagement.Enums;

namespace RequestsManagement.Models;

public class RequestEntity
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string OrganizationName { get; set; }
    public RequestStatus Status { get; set; }
    public RequestPriority Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
