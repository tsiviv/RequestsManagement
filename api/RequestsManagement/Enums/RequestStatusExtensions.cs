namespace RequestsManagement.Enums;

public static class RequestStatusExtensions
{
    /// <summary>The status as users see it, for error messages returned by the API (same labels as the UI).</summary>
    public static string DisplayName(this RequestStatus status) => status switch
    {
        RequestStatus.New => "חדש",
        RequestStatus.InProgress => "בטיפול",
        RequestStatus.Waiting => "ממתין",
        RequestStatus.Completed => "הושלם",
        _ => status.ToString()
    };
}
