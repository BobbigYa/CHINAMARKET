using Tpos.Core.Authentication;
using Tpos.Desktop.Mvvm;
using Tpos.Desktop.Services;

namespace Tpos.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly SessionManager _session;
    private readonly AuthenticationViewModel _authenticationPage;
    private object? _currentPage;

    public MainViewModel(
        IAuthenticationService authenticationService,
        LocalizationService localization,
        SessionManager session)
    {
        _session = session;
        _authenticationPage = new AuthenticationViewModel(
            authenticationService,
            localization,
            OnAuthenticated);
    }

    public object? CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value);
    }

    public async Task InitializeAsync()
    {
        CurrentPage = _authenticationPage;
        await _authenticationPage.InitializeAsync();
    }

    private void OnAuthenticated()
    {
        _session.SignIn(DateTimeOffset.UtcNow);
        CurrentPage = new DashboardViewModel(LogoutAsync);
    }

    private async Task LogoutAsync()
    {
        _session.SignOut();
        await _authenticationPage.ShowLoginAsync();
        CurrentPage = _authenticationPage;
    }
}
