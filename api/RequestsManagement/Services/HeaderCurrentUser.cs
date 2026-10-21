using RequestsManagement.Exceptions;
using RequestsManagement.Models;

namespace RequestsManagement.Services;

// Header values cannot carry Hebrew (browsers refuse it, Kestrel rejects it), so the name is
// percent-encoded UTF-8 (encodeURIComponent on the client).
public sealed class HeaderCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string HeaderName = "X-User-Name";

    private string? _userName;

    public string UserName => _userName ??= ReadUserName();

    private string ReadUserName()
    {
        var values = httpContextAccessor.HttpContext?.Request.Headers[HeaderName] ?? default;

        if (values.Count > 1)
        {
            throw new ValidationException($"יש לשלוח את הכותרת {HeaderName} פעם אחת בלבד.");
        }

        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ValidationException($"חסר שם משתמש: הכותרת {HeaderName} נדרשת לכל שינוי, כי השם נרשם בהיסטוריית השינויים.");
        }

        var userName = Uri.UnescapeDataString(raw).Trim();
        if (userName.Length == 0 ||
            userName.Length > RequestStatusHistoryEntity.ChangedByMaxLength ||
            userName.Any(char.IsControl))
        {
            throw new ValidationException(
                $"שם המשתמש בכותרת {HeaderName} חייב להכיל 1 עד {RequestStatusHistoryEntity.ChangedByMaxLength} תווים " +
                "(UTF-8 בקידוד percent-encoding), ללא תווי בקרה.");
        }

        return userName;
    }
}
