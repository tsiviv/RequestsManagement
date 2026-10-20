namespace RequestsManagement.Services;

/// <summary>
/// Identifies who is performing a change (recorded as ChangedBy in the status history).
/// The single seam to replace when real authentication is added.
/// </summary>
public interface ICurrentUser
{
    /// <summary>The validated user name. Throws ValidationException when it is missing or invalid.</summary>
    string UserName { get; }
}
