using Microsoft.Data.Sqlite;
using Tpos.Core.Authentication;

namespace Tpos.Infrastructure.Persistence;

public sealed class SqliteCredentialStore : ICredentialStore
{
    private readonly string _connectionString;

    public SqliteCredentialStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;

                CREATE TABLE IF NOT EXISTS AdministratorCredential (
                    Id                  INTEGER PRIMARY KEY CHECK (Id = 1),
                    PasswordHash        TEXT NOT NULL,
                    FailedAttempts      INTEGER NOT NULL DEFAULT 0 CHECK (FailedAttempts >= 0),
                    LockoutUntilUtc     INTEGER NULL,
                    CreatedAtUtc        INTEGER NOT NULL,
                    UpdatedAtUtc        INTEGER NOT NULL
                );

                PRAGMA user_version=1;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    public async Task<CredentialState?> GetCredentialAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            return await ReadCredentialAsync(connection, null, cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    public async Task<bool> CreateCredentialAsync(
        string passwordHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO AdministratorCredential (
                    Id, PasswordHash, FailedAttempts, LockoutUntilUtc, CreatedAtUtc, UpdatedAtUtc)
                VALUES (
                    1, $passwordHash, 0, NULL, $now, $now);
                """;
            command.Parameters.AddWithValue("$passwordHash", passwordHash);
            command.Parameters.AddWithValue("$now", ToTimestamp(nowUtc));

            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    public async Task<CredentialState?> RegisterFailedAttemptAsync(
        int maximumAttempts,
        TimeSpan lockoutDuration,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        if (lockoutDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration));
        }

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE AdministratorCredential
                SET FailedAttempts = FailedAttempts + 1,
                    LockoutUntilUtc = CASE
                        WHEN FailedAttempts + 1 >= $maximumAttempts THEN $lockoutUntil
                        ELSE LockoutUntilUtc
                    END
                WHERE Id = 1;
                """;
            command.Parameters.AddWithValue("$maximumAttempts", maximumAttempts);
            command.Parameters.AddWithValue(
                "$lockoutUntil",
                ToTimestamp(nowUtc.Add(lockoutDuration)));

            await command.ExecuteNonQueryAsync(cancellationToken);
            var state = await ReadCredentialAsync(connection, transaction, cancellationToken);
            transaction.Commit();
            return state;
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    public async Task ResetFailedAttemptsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE AdministratorCredential
                SET FailedAttempts = 0, LockoutUntilUtc = NULL
                WHERE Id = 1;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    public async Task ResetExpiredLockoutAsync(
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE AdministratorCredential
                SET FailedAttempts = 0, LockoutUntilUtc = NULL
                WHERE Id = 1
                  AND LockoutUntilUtc IS NOT NULL
                  AND LockoutUntilUtc <= $now;
                """;
            command.Parameters.AddWithValue("$now", ToTimestamp(nowUtc));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    public async Task<bool> UpdatePasswordHashAsync(
        string expectedCurrentHash,
        string newPasswordHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCurrentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPasswordHash);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE AdministratorCredential
                SET PasswordHash = $newPasswordHash,
                    FailedAttempts = 0,
                    LockoutUntilUtc = NULL,
                    UpdatedAtUtc = $now
                WHERE Id = 1 AND PasswordHash = $expectedCurrentHash;
                """;
            command.Parameters.AddWithValue("$newPasswordHash", newPasswordHash);
            command.Parameters.AddWithValue("$expectedCurrentHash", expectedCurrentHash);
            command.Parameters.AddWithValue("$now", ToTimestamp(nowUtc));

            return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        }
        catch (SqliteException exception)
        {
            throw StoreFailure(exception);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<CredentialState?> ReadCredentialAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT PasswordHash, FailedAttempts, LockoutUntilUtc, CreatedAtUtc, UpdatedAtUtc
            FROM AdministratorCredential
            WHERE Id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CredentialState(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.IsDBNull(2) ? null : FromTimestamp(reader.GetInt64(2)),
            FromTimestamp(reader.GetInt64(3)),
            FromTimestamp(reader.GetInt64(4)));
    }

    private static long ToTimestamp(DateTimeOffset value) => value.ToUniversalTime().ToUnixTimeMilliseconds();

    private static DateTimeOffset FromTimestamp(long value) => DateTimeOffset.FromUnixTimeMilliseconds(value);

    private static CredentialStoreException StoreFailure(SqliteException exception) =>
        new("本地凭据数据库操作失败。", exception);
}
