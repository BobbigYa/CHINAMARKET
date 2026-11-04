namespace Tpos.Core.Authentication;

public sealed class AuthenticationService : IAuthenticationService
{
    public const int MaximumFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly ICredentialStore _store;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public AuthenticationService(
        ICredentialStore store,
        IPasswordHasher passwordHasher,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LoginStatus> GetLoginStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = await _store.GetCredentialAsync(cancellationToken);
            if (credential is null)
            {
                return new LoginStatus(false, 0, TimeSpan.Zero);
            }

            var now = _timeProvider.GetUtcNow();
            var lockRemaining = GetPositiveLockRemaining(credential.LockoutUntilUtc, now);
            var effectiveFailures = credential.FailedAttempts;

            if (credential.LockoutUntilUtc is not null && lockRemaining == TimeSpan.Zero)
            {
                await _store.ResetExpiredLockoutAsync(now, cancellationToken);
                effectiveFailures = 0;
            }

            var remainingAttempts = lockRemaining > TimeSpan.Zero
                ? 0
                : Math.Max(0, MaximumFailedAttempts - effectiveFailures);

            return new LoginStatus(true, remainingAttempts, lockRemaining);
        }
        catch (Exception exception) when (IsExpectedStorageException(exception))
        {
            return new LoginStatus(false, 0, TimeSpan.Zero, AuthError.StorageError);
        }
    }

    public async Task<AuthResult> InitializeAdministratorAsync(
        string email,
        string password,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _store.GetCredentialAsync(cancellationToken);
            if (existing is not null)
            {
                return AuthResult.Failure(AuthError.AlreadyInitialized);
            }

            var emailError = EmailPolicy.Validate(email);
            if (emailError != AuthError.None)
            {
                return AuthResult.Failure(emailError);
            }

            var validationError = PasswordPolicy.Validate(password, confirmation);
            if (validationError != AuthError.None)
            {
                return AuthResult.Failure(validationError);
            }

            var passwordHash = await Task.Run(
                () => _passwordHasher.HashPassword(password),
                cancellationToken);

            var created = await _store.CreateCredentialAsync(
                EmailPolicy.Normalize(email),
                passwordHash,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            return created
                ? AuthResult.Success()
                : AuthResult.Failure(AuthError.AlreadyInitialized);
        }
        catch (Exception exception) when (IsExpectedStorageException(exception))
        {
            return AuthResult.Failure(AuthError.StorageError);
        }
    }

    public async Task<AuthResult> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var emailError = EmailPolicy.Validate(email);
        var normalizedEmail = emailError == AuthError.None ? EmailPolicy.Normalize(email) : string.Empty;
        if (string.IsNullOrEmpty(password))
        {
            return AuthResult.Failure(AuthError.PasswordRequired);
        }

        if (password.Length > PasswordPolicy.MaximumLength)
        {
            return AuthResult.Failure(AuthError.PasswordTooLong);
        }

        try
        {
            var credential = await _store.GetCredentialAsync(cancellationToken);
            if (credential is null)
            {
                return AuthResult.Failure(AuthError.NotInitialized);
            }

            var now = _timeProvider.GetUtcNow();
            var lockRemaining = GetPositiveLockRemaining(credential.LockoutUntilUtc, now);
            if (lockRemaining > TimeSpan.Zero)
            {
                return AuthResult.Failure(AuthError.Locked, 0, lockRemaining);
            }

            if (credential.LockoutUntilUtc is not null)
            {
                await _store.ResetExpiredLockoutAsync(now, cancellationToken);
                credential = credential with { FailedAttempts = 0, LockoutUntilUtc = null };
            }

            bool passwordIsValid;
            try
            {
                passwordIsValid = await Task.Run(
                    () => _passwordHasher.VerifyPassword(password, credential.PasswordHash),
                    cancellationToken);
            }
            catch (InvalidDataException)
            {
                return AuthResult.Failure(AuthError.CredentialDataInvalid);
            }

            if (emailError != AuthError.None)
            {
                return await RegisterFailureAsync(cancellationToken);
            }

            if (passwordIsValid)
            {
                if (credential.Email is null)
                {
                    var bound = await _store.BindEmailAsync(
                        normalizedEmail,
                        credential.PasswordHash,
                        now,
                        cancellationToken);
                    if (!bound)
                    {
                        return AuthResult.Failure(AuthError.StorageError);
                    }
                }
                else if (!string.Equals(
                    credential.Email,
                    normalizedEmail,
                    StringComparison.Ordinal))
                {
                    return await RegisterFailureAsync(cancellationToken);
                }

                await _store.ResetFailedAttemptsAsync(cancellationToken);
                return AuthResult.Success();
            }

            return await RegisterFailureAsync(cancellationToken);
        }
        catch (Exception exception) when (IsExpectedStorageException(exception))
        {
            return AuthResult.Failure(AuthError.StorageError);
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentPassword))
        {
            return AuthResult.Failure(AuthError.PasswordRequired);
        }

        if (currentPassword.Length > PasswordPolicy.MaximumLength)
        {
            return AuthResult.Failure(AuthError.PasswordTooLong);
        }

        var validationError = PasswordPolicy.Validate(newPassword, confirmation);
        if (validationError != AuthError.None)
        {
            return AuthResult.Failure(validationError);
        }

        try
        {
            var credential = await _store.GetCredentialAsync(cancellationToken);
            if (credential is null)
            {
                return AuthResult.Failure(AuthError.NotInitialized);
            }

            var now = _timeProvider.GetUtcNow();
            var lockRemaining = GetPositiveLockRemaining(credential.LockoutUntilUtc, now);
            if (lockRemaining > TimeSpan.Zero)
            {
                return AuthResult.Failure(AuthError.Locked, 0, lockRemaining);
            }

            if (credential.LockoutUntilUtc is not null)
            {
                await _store.ResetExpiredLockoutAsync(now, cancellationToken);
                credential = credential with { FailedAttempts = 0, LockoutUntilUtc = null };
            }

            bool currentPasswordIsValid;
            try
            {
                currentPasswordIsValid = await Task.Run(
                    () => _passwordHasher.VerifyPassword(currentPassword, credential.PasswordHash),
                    cancellationToken);
            }
            catch (InvalidDataException)
            {
                return AuthResult.Failure(AuthError.CredentialDataInvalid);
            }

            if (!currentPasswordIsValid)
            {
                return await RegisterFailureAsync(cancellationToken);
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                return AuthResult.Failure(AuthError.SamePassword);
            }

            var newHash = await Task.Run(
                () => _passwordHasher.HashPassword(newPassword),
                cancellationToken);

            var updated = await _store.UpdatePasswordHashAsync(
                credential.PasswordHash,
                newHash,
                _timeProvider.GetUtcNow(),
                cancellationToken);

            return updated
                ? AuthResult.Success()
                : AuthResult.Failure(AuthError.StorageError);
        }
        catch (Exception exception) when (IsExpectedStorageException(exception))
        {
            return AuthResult.Failure(AuthError.StorageError);
        }
    }

    private async Task<AuthResult> RegisterFailureAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var credential = await _store.RegisterFailedAttemptAsync(
            MaximumFailedAttempts,
            LockoutDuration,
            now,
            cancellationToken);

        if (credential is null)
        {
            return AuthResult.Failure(AuthError.NotInitialized);
        }

        var lockRemaining = GetPositiveLockRemaining(credential.LockoutUntilUtc, now);
        if (lockRemaining > TimeSpan.Zero)
        {
            return AuthResult.Failure(AuthError.Locked, 0, lockRemaining);
        }

        return AuthResult.Failure(
            AuthError.InvalidPassword,
            Math.Max(0, MaximumFailedAttempts - credential.FailedAttempts));
    }

    private static TimeSpan GetPositiveLockRemaining(DateTimeOffset? lockoutUntilUtc, DateTimeOffset nowUtc)
    {
        if (lockoutUntilUtc is null)
        {
            return TimeSpan.Zero;
        }

        var remaining = lockoutUntilUtc.Value - nowUtc;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static bool IsExpectedStorageException(Exception exception) =>
        exception is CredentialStoreException or IOException or UnauthorizedAccessException;
}
