namespace Tpos.Core.Authentication;

public sealed record LoginStatus(
    bool IsInitialized,
    int RemainingAttempts,
    TimeSpan LockRemaining,
    AuthError Error = AuthError.None)
{
    public bool IsLocked => LockRemaining > TimeSpan.Zero;
}
