using RequestsManagement.Enums;
using RequestsManagement.Exceptions;

namespace RequestsManagement.Policies;

/// <summary>
/// The single definition of which status changes are allowed. Used by single and bulk
/// updates alike, and exposed so clients can offer only valid next statuses.
/// </summary>
public static class StatusTransitionPolicy
{
    // Completed is terminal; New must be picked up (InProgress) before it can wait or complete.
    private static readonly Dictionary<RequestStatus, RequestStatus[]> AllowedTransitions = new()
    {
        [RequestStatus.New] = [RequestStatus.InProgress],
        [RequestStatus.InProgress] = [RequestStatus.Waiting, RequestStatus.Completed],
        [RequestStatus.Waiting] = [RequestStatus.InProgress, RequestStatus.Completed],
        [RequestStatus.Completed] = []
    };

    public static IReadOnlyList<RequestStatus> GetAllowedNextStatuses(RequestStatus current) =>
        AllowedTransitions.GetValueOrDefault(current, []);

    public static bool IsAllowed(RequestStatus from, RequestStatus to) =>
        GetAllowedNextStatuses(from).Contains(to);

    /// <summary>Returns why the transition is not allowed, or null when it is.</summary>
    public static string? Validate(RequestStatus from, RequestStatus to)
    {
        if (from == to)
        {
            return $"Request is already in status '{to}'.";
        }

        if (!IsAllowed(from, to))
        {
            var allowed = GetAllowedNextStatuses(from);
            var allowedText = allowed.Count == 0
                ? $"'{from}' is a final status."
                : $"Allowed: {string.Join(", ", allowed)}.";
            return $"Cannot change status from '{from}' to '{to}'. {allowedText}";
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
