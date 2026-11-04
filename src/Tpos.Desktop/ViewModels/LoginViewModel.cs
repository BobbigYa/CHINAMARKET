using System.Globalization;
using System.Windows.Threading;
using Tpos.Core.Authentication;
using Tpos.Desktop.Mvvm;
using Tpos.Desktop.Services;

namespace Tpos.Desktop.ViewModels;

public sealed class LoginViewModel : ObservableObject
{
    private readonly IAuthenticationService _authenticationService;
    private readonly LocalizationService _localization;
    private readonly Action _authenticated;
    private readonly DispatcherTimer _lockTimer;
    private DateTimeOffset _lockoutEndsAtUtc;
    private AuthError _error = AuthError.None;
    private string? _successMessageKey;
    private int _remainingAttempts = AuthenticationService.MaximumFailedAttempts;
    private string _message = string.Empty;
    private string _lockRemainingText = string.Empty;
    private bool _isBusy;
    private bool _isLocked;

    public LoginViewModel(
        IAuthenticationService authenticationService,
        LocalizationService localization,
        Action authenticated)
    {
        _authenticationService = authenticationService;
        _localization = localization;
        _authenticated = authenticated;
        _lockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _lockTimer.Tick += (_, _) => UpdateLockRemaining();
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(HasMessage));
            }
        }
    }

    public string LockRemainingText
    {
        get => _lockRemainingText;
        private set
        {
            if (SetProperty(ref _lockRemainingText, value))
            {
                OnPropertyChanged(nameof(HasLockRemaining));
            }
        }
    }

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool HasLockRemaining => !string.IsNullOrWhiteSpace(LockRemainingText);

    public bool IsSuccessMessage { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsInputEnabled));
                OnPropertyChanged(nameof(SubmitText));
            }
        }
    }

    public bool IsLocked
    {
        get => _isLocked;
        private set
        {
            if (SetProperty(ref _isLocked, value))
            {
                OnPropertyChanged(nameof(IsInputEnabled));
                OnPropertyChanged(nameof(SubmitText));
            }
        }
    }

    public bool IsInputEnabled => !IsBusy && !IsLocked;

    public string SubmitText => IsBusy ? _localization["Verifying"] : _localization["EnterSystem"];

    public void ApplyStatus(LoginStatus status)
    {
        if (status.Error != AuthError.None)
        {
            SetError(status.Error);
            return;
        }

        _remainingAttempts = status.RemainingAttempts;
        if (status.IsLocked)
        {
            SetError(AuthError.Locked);
            BeginLockout(status.LockRemaining);
            return;
        }

        IsLocked = false;
        _lockTimer.Stop();
        LockRemainingText = string.Empty;
        if (_error == AuthError.Locked)
        {
            ClearMessage();
        }
    }

    public async Task LoginAsync(string password)
    {
        if (!IsInputEnabled)
        {
            return;
        }

        ClearMessage();
        IsBusy = true;
        AuthResult result;
        try
        {
            result = await _authenticationService.AuthenticateAsync(password);
        }
        catch (Exception)
        {
            SetError(AuthError.StorageError);
            IsBusy = false;
            return;
        }

        IsBusy = false;
        if (result.IsSuccess)
        {
            _lockTimer.Stop();
            _authenticated();
            return;
        }

        _remainingAttempts = result.RemainingAttempts;
        SetError(result.Error);
        if (result.Error == AuthError.Locked)
        {
            BeginLockout(result.LockRemaining);
        }
    }

    public ChangePasswordViewModel CreateChangePasswordViewModel() =>
        new(_authenticationService, _localization);

    public void ShowPasswordChangedMessage() => SetSuccess("PasswordChanged");

    public void ShowAdministratorCreatedMessage() => SetSuccess("AdministratorCreated");

    public void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(SubmitText));
        RenderMessage();
        UpdateLockRemaining();
    }

    private void SetError(AuthError error)
    {
        _error = error;
        _successMessageKey = null;
        IsSuccessMessage = false;
        OnPropertyChanged(nameof(IsSuccessMessage));
        RenderMessage();
    }

    private void SetSuccess(string messageKey)
    {
        _error = AuthError.None;
        _successMessageKey = messageKey;
        IsSuccessMessage = true;
        OnPropertyChanged(nameof(IsSuccessMessage));
        RenderMessage();
    }

    private void ClearMessage()
    {
        _error = AuthError.None;
        _successMessageKey = null;
        IsSuccessMessage = false;
        OnPropertyChanged(nameof(IsSuccessMessage));
        Message = string.Empty;
    }

    private void RenderMessage()
    {
        if (_successMessageKey is not null)
        {
            Message = _localization[_successMessageKey];
            return;
        }

        if (_error == AuthError.None)
        {
            Message = string.Empty;
            return;
        }

        var text = _localization[$"Error.{_error}"];
        if (_error == AuthError.InvalidPassword && _remainingAttempts > 0)
        {
            text += " " + string.Format(
                CultureInfo.CurrentUICulture,
                _localization["RemainingAttempts"],
                _remainingAttempts);
        }

        Message = text;
    }

    private void BeginLockout(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        _lockoutEndsAtUtc = DateTimeOffset.UtcNow.Add(remaining);
        IsLocked = true;
        UpdateLockRemaining();
        _lockTimer.Start();
    }

    private void UpdateLockRemaining()
    {
        if (!IsLocked)
        {
            LockRemainingText = string.Empty;
            return;
        }

        var remaining = _lockoutEndsAtUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            IsLocked = false;
            _lockTimer.Stop();
            LockRemainingText = string.Empty;
            _remainingAttempts = AuthenticationService.MaximumFailedAttempts;
            if (_error == AuthError.Locked)
            {
                ClearMessage();
            }

            return;
        }

        var display = TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds));
        LockRemainingText = string.Format(
            CultureInfo.CurrentUICulture,
            _localization["LockRemaining"],
            display.ToString(@"mm\:ss", CultureInfo.CurrentCulture));
    }
}
