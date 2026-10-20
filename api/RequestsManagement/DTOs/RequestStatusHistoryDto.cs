using RequestsManagement.Enums;

namespace RequestsManagement.DTOs;

public class RequestStatusHistoryDto
{
    public long Id { get; set; }
    public RequestStatus PreviousStatus { get; set; }
    public RequestStatus NewStatus { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = "";
}
