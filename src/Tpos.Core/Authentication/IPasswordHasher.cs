namespace Tpos.Core.Authentication;

public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyPassword(string password, string encodedHash);
}
