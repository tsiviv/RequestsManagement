using RequestsManagement.Enums;

namespace RequestsManagement.Models;

public class RequestStatusHistoryEntity
{
    public long Id { get; set; }
    public int RequestId { get; set; }
    public RequestStatus PreviousStatus { get; set; }
    public RequestStatus NewStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public required string ChangedBy { get; set; }
}
