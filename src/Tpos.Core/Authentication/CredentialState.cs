namespace Tpos.Core.Authentication;

public sealed record CredentialState(
    string? Email,
    string PasswordHash,
    int FailedAttempts,
    DateTimeOffset? LockoutUntilUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
