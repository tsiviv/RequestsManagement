namespace RequestsManagement.Exceptions;

public class ConcurrencyConflictException(string message) : Exception(message);
