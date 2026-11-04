namespace Tpos.Core.Authentication;

public sealed record AuthResult
{
    private AuthResult(AuthError error, int remainingAttempts, TimeSpan lockRemaining)
    {
        Error = error;
        RemainingAttempts = remainingAttempts;
        LockRemaining = lockRemaining;
    }

    public AuthError Error { get; }

    public int RemainingAttempts { get; }

    public TimeSpan LockRemaining { get; }

    public bool IsSuccess => Error == AuthError.None;

    public static AuthResult Success() => new(AuthError.None, 0, TimeSpan.Zero);

    public static AuthResult Failure(
        AuthError error,
        int remainingAttempts = 0,
        TimeSpan? lockRemaining = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(remainingAttempts);
        return new AuthResult(error, remainingAttempts, lockRemaining ?? TimeSpan.Zero);
    }
}
