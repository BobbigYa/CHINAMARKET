using Microsoft.Data.Sqlite;
using Tpos.Core.Authentication;
using Tpos.Infrastructure.Persistence;
using Xunit;

namespace Tpos.Tests;

public sealed class EmailAuthenticationTests
{
    private const string Email = "admin@example.test";
    private const string Password = "Email-Test-Password-2026!";
    private const string WrongPassword = "Wrong-Email-Test-Password!";

    [Fact]
    public async Task Initialization_RequiresValidEmail_AndStoresNormalizedAddress()
    {
        using var harness = await TestHarness.CreateAsync();

        var missing = await harness.Service.InitializeAdministratorAsync("", Password, Password);
        var invalid = await harness.Service.InitializeAdministratorAsync("not-an-email", Password, Password);
        Assert.Equal(AuthError.EmailRequired, missing.Error);
        Assert.Equal(AuthError.EmailInvalid, invalid.Error);
        Assert.Null(await harness.Store.GetCredentialAsync());

        var initialized = await harness.Service.InitializeAdministratorAsync(
            "  Admin@Example.TEST  ", Password, Password);
        Assert.True(initialized.IsSuccess);
        var credential = await harness.Store.GetCredentialAsync();
        Assert.NotNull(credential);
        Assert.Equal(Email, credential.Email);

        var login = await harness.Service.AuthenticateAsync(" ADMIN@example.test ", Password);
        Assert.True(login.IsSuccess);
    }

    [Fact]
    public async Task WrongEmail_StillVerifiesArgon2AndRegistersFailure()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, Password, Password);
        var countingHasher = new CountingPasswordHasher(harness.Hasher);
        var service = new AuthenticationService(harness.Store, countingHasher, harness.Time);

        var result = await service.AuthenticateAsync("other@example.test", Password);

        Assert.Equal(AuthError.InvalidPassword, result.Error);
        Assert.Equal(4, result.RemainingAttempts);
        Assert.Equal(1, countingHasher.VerifyCalls);
        var credential = await harness.Store.GetCredentialAsync();
        Assert.NotNull(credential);
        Assert.Equal(1, credential.FailedAttempts);
        Assert.Equal(Email, credential.Email);
    }

    [Fact]
    public async Task MalformedEmail_StillVerifiesArgon2AndRegistersFailure()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, Password, Password);
        var countingHasher = new CountingPasswordHasher(harness.Hasher);
        var service = new AuthenticationService(harness.Store, countingHasher, harness.Time);

        var result = await service.AuthenticateAsync("not-an-email", Password);

        Assert.Equal(AuthError.InvalidPassword, result.Error);
        Assert.Equal(4, result.RemainingAttempts);
        Assert.Equal(1, countingHasher.VerifyCalls);
    }

    [Fact]
    public async Task FiveWrongEmails_UseExistingLockout_CorrectCredentialsCannotBypassIt()
    {
        using var harness = await TestHarness.CreateAsync();
        await harness.Service.InitializeAdministratorAsync(Email, Password, Password);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var failure = await harness.Service.AuthenticateAsync("other@example.test", Password);
            Assert.Equal(AuthError.InvalidPassword, failure.Error);
            Assert.Equal(5 - attempt, failure.RemainingAttempts);
        }

        var fifth = await harness.Service.AuthenticateAsync("other@example.test", Password);
        Assert.Equal(AuthError.Locked, fifth.Error);
        var locked = await harness.Service.AuthenticateAsync(Email, Password);
        Assert.Equal(AuthError.Locked, locked.Error);

        harness.Time.Advance(AuthenticationService.LockoutDuration);
        var unlocked = await harness.Service.AuthenticateAsync(Email, Password);
        Assert.True(unlocked.IsSuccess);
    }

    [Fact]
    public async Task LegacyV1Database_MigratesInPlace_BindsOnlyAfterCorrectPassword()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TposTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "legacy.db");
        try
        {
            using var harness = await TestHarness.CreateAsync();
            var originalHash = harness.Hasher.HashPassword(Password);
            await CreateLegacyV1DatabaseAsync(databasePath, originalHash);

            var store = new SqliteCredentialStore(databasePath);
            await store.InitializeAsync();
            await AssertLegacyRowPreservedAsync(databasePath, originalHash);
            var service = new AuthenticationService(store, harness.Hasher, harness.Time);

            var wrongPassword = await service.AuthenticateAsync(Email, WrongPassword);
            Assert.Equal(AuthError.InvalidPassword, wrongPassword.Error);
            var afterWrongPassword = await store.GetCredentialAsync();
            Assert.NotNull(afterWrongPassword);
            Assert.Null(afterWrongPassword.Email);

            var bound = await service.AuthenticateAsync(" Admin@Example.TEST ", Password);
            Assert.True(bound.IsSuccess);
            var afterBinding = await store.GetCredentialAsync();
            Assert.NotNull(afterBinding);
            Assert.Equal(Email, afterBinding.Email);
            Assert.Equal(originalHash, afterBinding.PasswordHash);
            Assert.Equal(0, afterBinding.FailedAttempts);

            SqliteConnection.ClearAllPools();
            store = new SqliteCredentialStore(databasePath);
            await store.InitializeAsync();
            service = new AuthenticationService(store, harness.Hasher, harness.Time);
            var wrongEmail = await service.AuthenticateAsync("other@example.test", Password);
            Assert.Equal(AuthError.InvalidPassword, wrongEmail.Error);
            var restartedLogin = await service.AuthenticateAsync(Email, Password);
            Assert.True(restartedLogin.IsSuccess);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(null, AuthError.EmailRequired)]
    [InlineData(" ", AuthError.EmailRequired)]
    [InlineData("person@example.test", AuthError.None)]
    [InlineData(" Person@Example.Test ", AuthError.None)]
    [InlineData("Display Name <person@example.test>", AuthError.EmailInvalid)]
    [InlineData("two@example.test@example.test", AuthError.EmailInvalid)]
    public void EmailPolicy_ValidatesExpectedForms(string? email, AuthError expected) =>
        Assert.Equal(expected, EmailPolicy.Validate(email));

    private static async Task CreateLegacyV1DatabaseAsync(string databasePath, string passwordHash)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE AdministratorCredential (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                PasswordHash TEXT NOT NULL,
                FailedAttempts INTEGER NOT NULL DEFAULT 0 CHECK (FailedAttempts >= 0),
                LockoutUntilUtc INTEGER NULL,
                CreatedAtUtc INTEGER NOT NULL,
                UpdatedAtUtc INTEGER NOT NULL);
            INSERT INTO AdministratorCredential (
                Id, PasswordHash, FailedAttempts, LockoutUntilUtc, CreatedAtUtc, UpdatedAtUtc)
            VALUES (1, $hash, 0, NULL, 1700000000000, 1700000000000);
            PRAGMA user_version=1;
            """;
        command.Parameters.AddWithValue("$hash", passwordHash);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertLegacyRowPreservedAsync(string databasePath, string originalHash)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PasswordHash, CreatedAtUtc, UpdatedAtUtc,
                   (SELECT COUNT(*) FROM pragma_table_info('AdministratorCredential') WHERE name = 'Email' AND \"notnull\" = 0),
                   (SELECT user_version FROM pragma_user_version())
            FROM AdministratorCredential WHERE Id = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(originalHash, reader.GetString(0));
        Assert.Equal(1700000000000, reader.GetInt64(1));
        Assert.Equal(1700000000000, reader.GetInt64(2));
        Assert.Equal(1, reader.GetInt64(3));
        Assert.Equal(2, reader.GetInt64(4));
    }

    private sealed class CountingPasswordHasher(IPasswordHasher inner) : IPasswordHasher
    {
        public int VerifyCalls { get; private set; }

        public string HashPassword(string password) => inner.HashPassword(password);

        public bool VerifyPassword(string password, string encodedHash)
        {
            VerifyCalls++;
            return inner.VerifyPassword(password, encodedHash);
        }
    }
}
