namespace RequestsManagement.Enums;

public static class RequestStatusExtensions
{
    public static string DisplayName(this RequestStatus status) => status switch
    {
        RequestStatus.New => "חדש",
        RequestStatus.InProgress => "בטיפול",
        RequestStatus.Waiting => "ממתין",
        RequestStatus.Completed => "הושלם",
        _ => status.ToString()
    };
}
