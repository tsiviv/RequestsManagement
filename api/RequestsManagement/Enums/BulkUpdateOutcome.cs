namespace RequestsManagement.Enums;

public enum BulkUpdateOutcome
{
    Updated = 0,
    NotFound = 1,
    Conflict = 2,
    InvalidTransition = 3
}
