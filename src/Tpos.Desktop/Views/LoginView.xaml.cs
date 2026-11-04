using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tpos.Desktop.Services;
using Tpos.Desktop.ViewModels;

namespace Tpos.Desktop.Views;

public partial class LoginView
{
    private bool _passwordIsVisible;

    public LoginView()
    {
        InitializeComponent();
        Loaded += (_, _) => OnViewLoaded();
        Unloaded += (_, _) => ClearPasswordInputs();
    }

    private LoginViewModel ViewModel => (LoginViewModel)DataContext;

    private LocalizationService Localization => (LocalizationService)Application.Current.Resources["Loc"];

    private void OnViewLoaded()
    {
        UpdateRevealAccessibility();
        if (string.IsNullOrWhiteSpace(ViewModel.Email))
        {
            EmailInput.Focus();
        }
        else
        {
            PasswordInput.Focus();
        }
    }

    private async void Login_OnClick(object sender, RoutedEventArgs e) => await SubmitPasswordAsync();

    private async void Password_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await SubmitPasswordAsync();
    }

    private async Task SubmitPasswordAsync()
    {
        if (!ViewModel.IsInputEnabled)
        {
            return;
        }

        var password = _passwordIsVisible ? VisiblePasswordInput.Text : PasswordInput.Password;
        try
        {
            await ViewModel.LoginAsync(password);
        }
        finally
        {
            password = string.Empty;
            ClearPasswordInputs();
        }
    }

    private void RevealPassword_OnClick(object sender, RoutedEventArgs e)
    {
        if (_passwordIsVisible)
        {
            PasswordInput.Password = VisiblePasswordInput.Text;
            VisiblePasswordInput.Clear();
            VisiblePasswordInput.Visibility = Visibility.Collapsed;
            PasswordInput.Visibility = Visibility.Visible;
            PasswordInput.Focus();
        }
        else
        {
            VisiblePasswordInput.Text = PasswordInput.Password;
            PasswordInput.Clear();
            PasswordInput.Visibility = Visibility.Collapsed;
            VisiblePasswordInput.Visibility = Visibility.Visible;
            VisiblePasswordInput.Focus();
            VisiblePasswordInput.CaretIndex = VisiblePasswordInput.Text.Length;
        }

        _passwordIsVisible = !_passwordIsVisible;
        UpdateRevealAccessibility();
    }

    private void UpdateRevealAccessibility()
    {
        var text = Localization[_passwordIsVisible ? "HidePassword" : "ShowPassword"];
        RevealPasswordButton.ToolTip = text;
        RevealPasswordButton.SetValue(
            System.Windows.Automation.AutomationProperties.NameProperty,
            text);
        EyeOpen.Visibility = _passwordIsVisible ? Visibility.Collapsed : Visibility.Visible;
        EyeClosed.Visibility = _passwordIsVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClearPasswordInputs()
    {
        PasswordInput.Clear();
        VisiblePasswordInput.Clear();
    }

    private void ChangePassword_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ChangePasswordDialog(ViewModel.CreateChangePasswordViewModel())
        {
            Owner = Window.GetWindow(this)
        };
        var changed = dialog.ShowDialog() == true;
        if (changed)
        {
            ClearPasswordInputs();
            ViewModel.ShowPasswordChangedMessage();
        }
    }
}
