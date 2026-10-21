namespace RequestsManagement.Services;

internal static class RequestMessages
{
    public static string NotFound(int id) => $"פנייה מספר {id} לא נמצאה.";

    public static string Conflict(int id) => $"פנייה {id} עודכנה על ידי משתמש אחר מאז שנטענה. יש לרענן ולנסות שוב.";
}
