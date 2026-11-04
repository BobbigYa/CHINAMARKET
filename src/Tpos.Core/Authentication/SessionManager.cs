namespace Tpos.Core.Authentication;

/// <summary>
/// Holds only the current in-memory UI session. It is never persisted.
/// </summary>
public sealed class SessionManager
{
    public bool IsAuthenticated { get; private set; }

    public DateTimeOffset? AuthenticatedAtUtc { get; private set; }

    public void SignIn(DateTimeOffset nowUtc)
    {
        IsAuthenticated = true;
        AuthenticatedAtUtc = nowUtc;
    }

    public void SignOut()
    {
        IsAuthenticated = false;
        AuthenticatedAtUtc = null;
    }
}
