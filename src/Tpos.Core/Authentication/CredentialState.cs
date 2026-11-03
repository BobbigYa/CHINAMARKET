namespace Tpos.Core.Authentication;

public sealed record CredentialState(
    string PasswordHash,
    int FailedAttempts,
    DateTimeOffset? LockoutUntilUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
