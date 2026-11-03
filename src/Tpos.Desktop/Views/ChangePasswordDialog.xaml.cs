using System.Windows;
using Tpos.Desktop.ViewModels;

namespace Tpos.Desktop.Views;

public partial class ChangePasswordDialog : Window
{
    private readonly ChangePasswordViewModel _viewModel;

    public ChangePasswordDialog(ChangePasswordViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Closed += (_, _) => ClearPasswordInputs();
    }

    private async void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var changed = await _viewModel.ChangeAsync(
                CurrentPasswordInput.Password,
                NewPasswordInput.Password,
                ConfirmationInput.Password);
            if (changed)
            {
                DialogResult = true;
            }
        }
        finally
        {
            ClearPasswordInputs();
        }
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ClearPasswordInputs()
    {
        CurrentPasswordInput.Clear();
        NewPasswordInput.Clear();
        ConfirmationInput.Clear();
    }
}
