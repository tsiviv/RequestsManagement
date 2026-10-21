using RequestsManagement.DTOs;
using RequestsManagement.Enums;
using RequestsManagement.Exceptions;

namespace RequestsManagement.Services;

// Request-level checks (400, nothing updated), done before touching the database.
internal static class StatusUpdateValidation
{
    public static RequestStatus TargetStatus(RequestStatus? status)
    {
        if (status is null)
        {
            throw new ValidationException("יש לציין סטטוס.");
        }

        if (!Enum.IsDefined(status.Value))
        {
            throw new ValidationException($"'{status}' אינו סטטוס חוקי. ערכים אפשריים: New, InProgress, Waiting, Completed.");
        }

        return status.Value;
    }

    public static byte[] RowVersion(string rowVersion, int? requestId = null)
    {
        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException(requestId is null
                ? "ערך ה-RowVersion אינו תקין (נדרש ערך בקידוד base64)."
                : $"ערך ה-RowVersion של פנייה {requestId} אינו תקין (נדרש ערך בקידוד base64).");
        }
    }

    public static Dictionary<int, byte[]> BulkItems(IReadOnlyCollection<BulkUpdateStatusItemDto>? items)
    {
        if (items is not { Count: > 0 and <= BulkUpdateStatusDto.MaxItems })
        {
            throw new ValidationException($"יש לשלוח בין 1 ל-{BulkUpdateStatusDto.MaxItems} פניות.");
        }

        var duplicateIds = items.GroupBy(i => i.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
        {
            throw new ValidationException($"כל פנייה יכולה להופיע פעם אחת בלבד. מזהים כפולים: {string.Join(", ", duplicateIds)}.");
        }

        return items.ToDictionary(i => i.Id, i => RowVersion(i.RowVersion, i.Id));
    }
}
