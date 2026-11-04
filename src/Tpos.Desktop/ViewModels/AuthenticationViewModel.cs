using Tpos.Core.Authentication;
using Tpos.Desktop.Mvvm;
using Tpos.Desktop.Services;

namespace Tpos.Desktop.ViewModels;

public sealed class AuthenticationViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private object? _currentForm;

    public AuthenticationViewModel(
        IAuthenticationService authenticationService,
        LocalizationService localization,
        Action authenticated)
    {
        _authenticationService = authenticationService;
        Localization = localization;
        Setup = new SetupViewModel(authenticationService, localization, ShowLoginAfterInitializationAsync);
        Login = new LoginViewModel(authenticationService, localization, authenticated);
        Localization.LanguageChanged += (_, _) => RefreshLocalizedText();
    }

    public LocalizationService Localization { get; }

    public SetupViewModel Setup { get; }

    public LoginViewModel Login { get; }

    public object? CurrentForm
    {
        get => _currentForm;
        private set => SetProperty(ref _currentForm, value);
    }

    public async Task InitializeAsync()
    {
        var status = await _authenticationService.GetLoginStatusAsync();
        if (status.Error == AuthError.None && !status.IsInitialized)
        {
            CurrentForm = Setup;
            return;
        }

        CurrentForm = Login;
        Login.ApplyStatus(status);
    }

    public async Task ShowLoginAsync()
    {
        var status = await _authenticationService.GetLoginStatusAsync();
        CurrentForm = Login;
        Login.ApplyStatus(status);
    }

    private async Task ShowLoginAfterInitializationAsync()
    {
        await ShowLoginAsync();
        Login.ShowAdministratorCreatedMessage();
    }

    private void RefreshLocalizedText()
    {
        Setup.RefreshLocalizedText();
        Login.RefreshLocalizedText();
    }
}
