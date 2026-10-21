using RequestsManagement.Enums;
using RequestsManagement.Exceptions;

namespace RequestsManagement.Policies;

public static class StatusTransitionPolicy
{
    private static readonly Dictionary<RequestStatus, IReadOnlyList<RequestStatus>> AllowedTransitions = new()
    {
        [RequestStatus.New] = Array.AsReadOnly([RequestStatus.InProgress]),
        [RequestStatus.InProgress] = Array.AsReadOnly([RequestStatus.Waiting, RequestStatus.Completed]),
        [RequestStatus.Waiting] = Array.AsReadOnly([RequestStatus.InProgress, RequestStatus.Completed]),
        [RequestStatus.Completed] = Array.AsReadOnly(Array.Empty<RequestStatus>())
    };

    public static IReadOnlyList<RequestStatus> GetAllowedNextStatuses(RequestStatus current) =>
        AllowedTransitions.GetValueOrDefault(current, []);

    public static bool IsAllowed(RequestStatus from, RequestStatus to) =>
        GetAllowedNextStatuses(from).Contains(to);

    // Null when allowed; otherwise the reason.
    public static string? Validate(RequestStatus from, RequestStatus to)
    {
        if (from == to)
        {
            return $"הפנייה כבר בסטטוס '{to.DisplayName()}'.";
        }

        if (!IsAllowed(from, to))
        {
            var allowed = GetAllowedNextStatuses(from);
            var allowedText = allowed.Count == 0
                ? $"'{from.DisplayName()}' הוא סטטוס סופי."
                : $"מעברים מותרים: {string.Join(", ", allowed.Select(s => s.DisplayName()))}.";
            return $"לא ניתן לשנות סטטוס מ'{from.DisplayName()}' ל'{to.DisplayName()}'. {allowedText}";
        }

        return null;
    }

    public static void EnsureAllowed(RequestStatus from, RequestStatus to)
    {
        if (Validate(from, to) is { } error)
        {
            throw new ValidationException(error);
        }
    }
}
