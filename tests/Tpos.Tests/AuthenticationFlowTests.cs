using Tpos.Core.Authentication;
using Xunit;

namespace Tpos.Tests;

public sealed class AuthenticationFlowTests
{
    private const string Email = "admin@example.test";
    private const string InitialPassword = "Initial-Test-Password-2026!";
    private const string UpdatedPassword = "Updated-Test-Password-2027!";
    private const string WrongPassword = "Wrong-Test-Password-0000!";

    [Fact]
    public async Task FirstRun_CreatesAdministrator_ThenUsesNormalLogin()
    {
        using var harness = await TestHarness.CreateAsync();
        var initialStatus = await harness.Service.GetLoginStatusAsync();
        Assert.False(initialStatus.IsInitialized);

        var tooShort = await harness.Service.InitializeAdministratorAsync(Email, "short", "short");
        Assert.Equal(AuthError.PasswordTooShort, tooShort.Error);

        var mismatch = await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, UpdatedPassword);
        Assert.Equal(AuthError.PasswordMismatch, mismatch.Error);

        var initialized = await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);
        Assert.True(initialized.IsSuccess);

        var duplicate = await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);
        Assert.Equal(AuthError.AlreadyInitialized, duplicate.Error);

        var credential = await harness.Store.GetCredentialAsync();
        Assert.NotNull(credential);
        Assert.StartsWith("$argon2id$", credential.PasswordHash, StringComparison.Ordinal);
        Assert.DoesNotContain(InitialPassword, credential.PasswordHash, StringComparison.Ordinal);

        var login = await harness.Service.AuthenticateAsync(Email, InitialPassword);
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task Authenticate_WrongPassword_DeniesAccessAndDecrementsAttempts()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);

        var result = await harness.Service.AuthenticateAsync(Email, WrongPassword);

        Assert.Equal(AuthError.InvalidPassword, result.Error);
        Assert.Equal(4, result.RemainingAttempts);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task FiveWrongPasswords_LockForFifteenMinutes_EvenWithCorrectPassword()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var failure = await harness.Service.AuthenticateAsync(Email, WrongPassword);
            Assert.Equal(AuthError.InvalidPassword, failure.Error);
            Assert.Equal(5 - attempt, failure.RemainingAttempts);
        }

        var fifth = await harness.Service.AuthenticateAsync(Email, WrongPassword);
        Assert.Equal(AuthError.Locked, fifth.Error);
        Assert.Equal(TimeSpan.FromMinutes(15), fifth.LockRemaining);

        var correctWhileLocked = await harness.Service.AuthenticateAsync(Email, InitialPassword);
        Assert.Equal(AuthError.Locked, correctWhileLocked.Error);

        harness.Time.Advance(TimeSpan.FromMinutes(15));
        var afterExpiry = await harness.Service.AuthenticateAsync(Email, InitialPassword);
        Assert.True(afterExpiry.IsSuccess);

        var status = await harness.Service.GetLoginStatusAsync();
        Assert.Equal(5, status.RemainingAttempts);
        Assert.False(status.IsLocked);
    }

    [Fact]
    public async Task ReopenDatabase_PersistsPasswordFailuresAndLockout()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);
        await harness.Service.AuthenticateAsync(Email, WrongPassword);
        await harness.Service.AuthenticateAsync(Email, WrongPassword);

        await harness.ReopenAsync();
        var statusAfterRestart = await harness.Service.GetLoginStatusAsync();
        Assert.True(statusAfterRestart.IsInitialized);
        Assert.Equal(3, statusAfterRestart.RemainingAttempts);

        await harness.Service.AuthenticateAsync(Email, WrongPassword);
        await harness.Service.AuthenticateAsync(Email, WrongPassword);
        var fifth = await harness.Service.AuthenticateAsync(Email, WrongPassword);
        Assert.Equal(AuthError.Locked, fifth.Error);

        await harness.ReopenAsync();
        var correctAfterRestart = await harness.Service.AuthenticateAsync(Email, InitialPassword);
        Assert.Equal(AuthError.Locked, correctAfterRestart.Error);
        Assert.True(correctAfterRestart.LockRemaining > TimeSpan.FromMinutes(14));

        harness.Time.Advance(TimeSpan.FromMinutes(15));
        var unlocked = await harness.Service.AuthenticateAsync(Email, InitialPassword);
        Assert.True(unlocked.IsSuccess);
    }

    [Fact]
    public async Task ChangePassword_RequiresCurrentPassword_OldPasswordStopsWorking()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);

        var wrongCurrent = await harness.Service.ChangePasswordAsync(
            WrongPassword,
            UpdatedPassword,
            UpdatedPassword);
        Assert.Equal(AuthError.InvalidPassword, wrongCurrent.Error);

        var unchanged = await harness.Service.ChangePasswordAsync(
            InitialPassword,
            InitialPassword,
            InitialPassword);
        Assert.Equal(AuthError.SamePassword, unchanged.Error);

        var changed = await harness.Service.ChangePasswordAsync(
            InitialPassword,
            UpdatedPassword,
            UpdatedPassword);
        Assert.True(changed.IsSuccess);

        var oldPassword = await harness.Service.AuthenticateAsync(Email, InitialPassword);
        Assert.Equal(AuthError.InvalidPassword, oldPassword.Error);

        var newPassword = await harness.Service.AuthenticateAsync(Email, UpdatedPassword);
        Assert.True(newPassword.IsSuccess);
    }

    [Fact]
    public async Task DamagedCredentialHash_ReturnsControlledError_InsteadOfSuccessfulLogin()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, InitialPassword, InitialPassword);
        var credential = await harness.Store.GetCredentialAsync();
        Assert.NotNull(credential);
        await harness.Store.UpdatePasswordHashAsync(
            credential.PasswordHash,
            "damaged-hash",
            harness.Time.GetUtcNow());

        var result = await harness.Service.AuthenticateAsync(Email, InitialPassword);

        Assert.Equal(AuthError.CredentialDataInvalid, result.Error);
        Assert.False(result.IsSuccess);
    }
}
