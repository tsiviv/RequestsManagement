using RequestsManagement.Exceptions;
using RequestsManagement.Models;

namespace RequestsManagement.Services;

/// <summary>
/// Reads the acting user from the X-User-Name header. There is no authentication in this
/// system, so the header is trusted as-is; with real auth this would read the token's claims.
/// </summary>
/// <remarks>
/// HTTP header values cannot carry non-Latin-1 text (browsers refuse to send it, Kestrel rejects it),
/// so the value is percent-encoded UTF-8 (encodeURIComponent on the client). Plain ASCII names
/// are unaffected by decoding.
/// </remarks>
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
            throw new ValidationException($"The {HeaderName} header must be specified once.");
        }

        var raw = values.ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ValidationException($"The {HeaderName} header is required for changes.");
        }

        var userName = Uri.UnescapeDataString(raw).Trim();
        if (userName.Length == 0 ||
            userName.Length > RequestStatusHistoryEntity.ChangedByMaxLength ||
            userName.Any(char.IsControl))
        {
            throw new ValidationException(
                $"The {HeaderName} header must be 1-{RequestStatusHistoryEntity.ChangedByMaxLength} characters " +
                "(percent-encoded UTF-8) without control characters.");
        }

        return userName;
    }
}
