using System.ComponentModel;
using System.Windows;
using Tpos.Desktop.ViewModels;

namespace Tpos.Desktop.Views;

public partial class AuthenticationView
{
    public AuthenticationView()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyPreferredWindowHeight();
        Unloaded += (_, _) => UnsubscribeViewModel();
        DataContextChanged += (_, _) => SubscribeViewModel();
    }

    private void SubscribeViewModel()
    {
        UnsubscribeViewModel();
        if (DataContext is AuthenticationViewModel viewModel)
        {
            viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyPreferredWindowHeight();
    }

    private void UnsubscribeViewModel()
    {
        if (DataContext is AuthenticationViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AuthenticationViewModel.CurrentForm))
        {
            ApplyPreferredWindowHeight();
        }
    }

    private void ApplyPreferredWindowHeight()
    {
        if (!IsLoaded || DataContext is not AuthenticationViewModel viewModel)
        {
            return;
        }

        var window = Window.GetWindow(this);
        if (window is null || window.WindowState != WindowState.Normal)
        {
            return;
        }

        window.Height = viewModel.CurrentForm is SetupViewModel ? 700 : 540;
        var workArea = SystemParameters.WorkArea;
        window.Top = workArea.Top + (workArea.Height - window.Height) / 2;
        window.Left = workArea.Left + (workArea.Width - window.Width) / 2;
    }
}
