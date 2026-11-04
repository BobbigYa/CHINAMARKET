using Tpos.Core.Authentication;
using Tpos.Desktop.Mvvm;
using Tpos.Desktop.Services;

namespace Tpos.Desktop.ViewModels;

public sealed class SetupViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly LocalizationService _localization;
    private readonly Func<Task> _initialized;
    private AuthError _error = AuthError.None;
    private string _errorMessage = string.Empty;
    private string _email = string.Empty;
    private bool _isBusy;

    public SetupViewModel(
        IAuthenticationService authenticationService,
        LocalizationService localization,
        Func<Task> initialized)
    {
        _authenticationService = authenticationService;
        _localization = localization;
        _initialized = initialized;
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(SubmitText));
                OnPropertyChanged(nameof(IsInputEnabled));
            }
        }
    }

    public bool IsInputEnabled => !IsBusy;

    public string SubmitText => IsBusy
        ? _localization["CreatingAdministrator"]
        : _localization["CreateAdministrator"];

    public async Task InitializeAsync(string password, string confirmation)
    {
        if (IsBusy)
        {
            return;
        }

        _error = AuthError.None;
        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _authenticationService.InitializeAdministratorAsync(Email, password, confirmation);
            if (result.IsSuccess)
            {
                await _initialized();
                return;
            }

            _error = result.Error;
            ErrorMessage = _localization[$"Error.{_error}"];
        }
        catch (Exception)
        {
            _error = AuthError.StorageError;
            ErrorMessage = _localization["Error.StorageError"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(SubmitText));
        if (_error != AuthError.None)
        {
            ErrorMessage = _localization[$"Error.{_error}"];
        }
    }
}
