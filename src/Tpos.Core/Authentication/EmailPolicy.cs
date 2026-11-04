using System.Net.Mail;

namespace Tpos.Core.Authentication;

public static class EmailPolicy
{
    public const int MaximumLength = 254;

    public static AuthError Validate(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return AuthError.EmailRequired;
        }

        var normalized = email.Trim();
        if (normalized.Length > MaximumLength)
        {
            return AuthError.EmailInvalid;
        }

        try
        {
            var address = new MailAddress(normalized);
            return string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)
                ? AuthError.None
                : AuthError.EmailInvalid;
        }
        catch (FormatException)
        {
            return AuthError.EmailInvalid;
        }
    }

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
