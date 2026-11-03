using Microsoft.Data.Sqlite;
using Tpos.Core.Authentication;
using Tpos.Infrastructure.Persistence;
using Tpos.Infrastructure.Security;

namespace Tpos.Tests;

internal sealed class TestHarness : IDisposable
{
    private readonly string _directory;

    private TestHarness(string directory)
    {
        _directory = directory;
        DatabasePath = Path.Combine(directory, "auth-test.db");
        Time = new ManualTimeProvider(new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero));
        Hasher = new Argon2PasswordHasher(new Argon2Parameters(8 * 1024, 1, 1, 16, 32));
        Store = new SqliteCredentialStore(DatabasePath);
        Service = new AuthenticationService(Store, Hasher, Time);
    }

    public string DatabasePath { get; }

    public ManualTimeProvider Time { get; }

    public Argon2PasswordHasher Hasher { get; }

    public SqliteCredentialStore Store { get; private set; }

    public AuthenticationService Service { get; private set; }

    public static async Task<TestHarness> CreateAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TposTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var harness = new TestHarness(directory);
        await harness.Store.InitializeAsync();
        return harness;
    }

    public async Task ReopenAsync()
    {
        SqliteConnection.ClearAllPools();
        Store = new SqliteCredentialStore(DatabasePath);
        await Store.InitializeAsync();
        Service = new AuthenticationService(Store, Hasher, Time);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A failed cleanup must not hide the actual test result.
        }
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset initialValue) : TimeProvider
{
    private DateTimeOffset _utcNow = initialValue;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
}
