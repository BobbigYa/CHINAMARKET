namespace Tpos.Core.Authentication;

public sealed class CredentialStoreException : Exception
{
    public CredentialStoreException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
