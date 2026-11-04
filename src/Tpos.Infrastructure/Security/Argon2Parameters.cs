namespace Tpos.Infrastructure.Security;

public sealed record Argon2Parameters(
    int MemorySizeKiB,
    int Iterations,
    int DegreeOfParallelism,
    int SaltSize,
    int HashSize)
{
    public static Argon2Parameters SecureDefault { get; } = new(
        MemorySizeKiB: 64 * 1024,
        Iterations: 3,
        DegreeOfParallelism: 1,
        SaltSize: 16,
        HashSize: 32);

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MemorySizeKiB, 8 * 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MemorySizeKiB, 256 * 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Iterations, 10);
        ArgumentOutOfRangeException.ThrowIfLessThan(DegreeOfParallelism, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(DegreeOfParallelism, 4);
        ArgumentOutOfRangeException.ThrowIfNotEqual(SaltSize, 16);
        ArgumentOutOfRangeException.ThrowIfNotEqual(HashSize, 32);
    }
}
