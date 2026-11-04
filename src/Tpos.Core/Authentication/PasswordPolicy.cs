namespace Tpos.Core.Authentication;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static AuthError Validate(string? password, string? confirmation)
    {
        if (string.IsNullOrEmpty(password))
        {
            return AuthError.PasswordRequired;
        }

        if (password.Length < MinimumLength)
        {
            return AuthError.PasswordTooShort;
        }

        if (password.Length > MaximumLength)
        {
            return AuthError.PasswordTooLong;
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            return AuthError.PasswordMismatch;
        }

        if (password.All(char.IsWhiteSpace) ||
            password.All(char.IsDigit) ||
            password.Distinct().Count() == 1)
        {
            return AuthError.WeakPassword;
        }

        return AuthError.None;
    }
}
