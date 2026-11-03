namespace Tpos.Core.Authentication;

public interface ICredentialStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<CredentialState?> GetCredentialAsync(CancellationToken cancellationToken = default);

    Task<bool> CreateCredentialAsync(
        string passwordHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<CredentialState?> RegisterFailedAttemptAsync(
        int maximumAttempts,
        TimeSpan lockoutDuration,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task ResetFailedAttemptsAsync(CancellationToken cancellationToken = default);

    Task ResetExpiredLockoutAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);

    Task<bool> UpdatePasswordHashAsync(
        string expectedCurrentHash,
        string newPasswordHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
}
