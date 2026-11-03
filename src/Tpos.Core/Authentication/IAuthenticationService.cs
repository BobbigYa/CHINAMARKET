namespace Tpos.Core.Authentication;

public interface IAuthenticationService
{
    Task<LoginStatus> GetLoginStatusAsync(CancellationToken cancellationToken = default);

    Task<AuthResult> InitializeAdministratorAsync(
        string password,
        string confirmation,
        CancellationToken cancellationToken = default);

    Task<AuthResult> AuthenticateAsync(
        string password,
        CancellationToken cancellationToken = default);

    Task<AuthResult> ChangePasswordAsync(
        string currentPassword,
        string newPassword,
        string confirmation,
        CancellationToken cancellationToken = default);
}
