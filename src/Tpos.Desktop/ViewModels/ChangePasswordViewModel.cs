using Tpos.Core.Authentication;
using Tpos.Desktop.Mvvm;
using Tpos.Desktop.Services;

namespace Tpos.Desktop.ViewModels;

public sealed class ChangePasswordViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly LocalizationService _localization;
    private string _errorMessage = string.Empty;
    private bool _isBusy;

    public ChangePasswordViewModel(
        IAuthenticationService authenticationService,
        LocalizationService localization)
    {
        _authenticationService = authenticationService;
        _localization = localization;
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

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsInputEnabled));
            }
        }
    }

    public bool IsInputEnabled => !IsBusy;

    public async Task<bool> ChangeAsync(string currentPassword, string newPassword, string confirmation)
    {
        if (IsBusy)
        {
            return false;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;
        try
        {
            var result = await _authenticationService.ChangePasswordAsync(
                currentPassword,
                newPassword,
                confirmation);

            if (result.IsSuccess)
            {
                return true;
            }

            ErrorMessage = _localization[$"Error.{result.Error}"];
            return false;
        }
        catch (Exception)
        {
            ErrorMessage = _localization["Error.StorageError"];
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
