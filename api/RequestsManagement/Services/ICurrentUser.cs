namespace RequestsManagement.Services;

// The single seam to replace when real authentication is added.
public interface ICurrentUser
{
    string UserName { get; }
}
