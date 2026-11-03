using Tpos.Infrastructure.Security;
using Xunit;

namespace Tpos.Tests;

public sealed class Argon2PasswordHasherTests
{
    private const string SamplePassword = "Test-Only-Password-2026!";

    [Fact]
    public void HashPassword_UsesArgon2idRandomSaltAndNeverStoresPlaintext()
    {
        using var harness = TestHarness.CreateAsync().GetAwaiter().GetResult();

        var first = harness.Hasher.HashPassword(SamplePassword);
        var second = harness.Hasher.HashPassword(SamplePassword);

        Assert.StartsWith("$argon2id$v=19$", first, StringComparison.Ordinal);
        Assert.DoesNotContain(SamplePassword, first, StringComparison.Ordinal);
        Assert.NotEqual(first, second);
        Assert.True(harness.Hasher.VerifyPassword(SamplePassword, first));
        Assert.False(harness.Hasher.VerifyPassword("Wrong-Test-Password-2026!", first));
    }

    [Fact]
    public void VerifyPassword_MalformedHash_ThrowsControlledInvalidDataException()
    {
        using var harness = TestHarness.CreateAsync().GetAwaiter().GetResult();

        Assert.Throws<InvalidDataException>(
            () => harness.Hasher.VerifyPassword(SamplePassword, "not-a-valid-hash"));
    }
}
