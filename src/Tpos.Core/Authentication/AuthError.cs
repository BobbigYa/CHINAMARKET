namespace Tpos.Core.Authentication;

public enum AuthError
{
    None,
    NotInitialized,
    AlreadyInitialized,
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
