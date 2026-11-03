using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Tpos.Core.Authentication;

namespace Tpos.Infrastructure.Security;

public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private readonly Argon2Parameters _parameters;

    public Argon2PasswordHasher(Argon2Parameters? parameters = null)
    {
        _parameters = parameters ?? Argon2Parameters.SecureDefault;
        _parameters.Validate();
    }

    public string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(_parameters.SaltSize);
        var hash = DeriveHash(password, salt, _parameters);

        return string.Join(
            '$',
            "$argon2id",
            "v=19",
            $"m={_parameters.MemorySizeKiB.ToString(CultureInfo.InvariantCulture)}," +
                $"t={_parameters.Iterations.ToString(CultureInfo.InvariantCulture)}," +
                $"p={_parameters.DegreeOfParallelism.ToString(CultureInfo.InvariantCulture)}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool VerifyPassword(string password, string encodedHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(encodedHash);

        var parsed = Parse(encodedHash);
        var actualHash = DeriveHash(password, parsed.Salt, parsed.Parameters);

        try
        {
            return actualHash.Length == parsed.ExpectedHash.Length &&
                CryptographicOperations.FixedTimeEquals(actualHash, parsed.ExpectedHash);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actualHash);
            CryptographicOperations.ZeroMemory(parsed.ExpectedHash);
        }
    }

    private static byte[] DeriveHash(string password, byte[] salt, Argon2Parameters parameters)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt,
                MemorySize = parameters.MemorySizeKiB,
                Iterations = parameters.Iterations,
                DegreeOfParallelism = parameters.DegreeOfParallelism
            };

            return argon2.GetBytes(parameters.HashSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private static ParsedHash Parse(string encodedHash)
    {
        try
        {
            var parts = encodedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 ||
                parts[0] != "argon2id" ||
                parts[1] != "v=19")
            {
                throw new InvalidDataException("密码哈希格式不受支持。");
            }

            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in parts[2].Split(','))
            {
                var pair = item.Split('=', StringSplitOptions.TrimEntries);
                if (pair.Length != 2 ||
                    !int.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
                    !values.TryAdd(pair[0], value))
                {
                    throw new InvalidDataException("密码哈希参数无效。");
                }
            }

            if (values.Count != 3 ||
                !values.TryGetValue("m", out var memory) ||
                !values.TryGetValue("t", out var iterations) ||
                !values.TryGetValue("p", out var parallelism))
            {
                throw new InvalidDataException("密码哈希参数不完整。");
            }

            var parameters = new Argon2Parameters(memory, iterations, parallelism, 16, 32);
            parameters.Validate();

            var salt = Convert.FromBase64String(parts[3]);
            var expectedHash = Convert.FromBase64String(parts[4]);
            if (salt.Length != parameters.SaltSize || expectedHash.Length != parameters.HashSize)
            {
                throw new InvalidDataException("密码哈希长度无效。");
            }

            return new ParsedHash(parameters, salt, expectedHash);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("密码哈希编码无效。", exception);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("密码哈希参数超出允许范围。", exception);
        }
    }

    private sealed record ParsedHash(
        Argon2Parameters Parameters,
        byte[] Salt,
        byte[] ExpectedHash);
}
