namespace Tpos.Core.Authentication;

public enum AuthError
{
    None,
    NotInitialized,
    AlreadyInitialized,
    EmailRequired,
    EmailInvalid,
    PasswordRequired,
    PasswordTooShort,
    PasswordTooLong,
    PasswordMismatch,
    WeakPassword,
    InvalidPassword,
    Locked,
    SamePassword,
    CredentialDataInvalid,
    StorageError
}
