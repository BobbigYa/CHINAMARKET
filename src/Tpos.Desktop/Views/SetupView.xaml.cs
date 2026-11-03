using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Tpos.Desktop.ViewModels;

namespace Tpos.Desktop.Views;

public partial class SetupView
{
    public SetupView()
    {
        InitializeComponent();
        Loaded += (_, _) => OnViewLoaded();
        Unloaded += (_, _) => ClearPasswordInputs();
    }

    private SetupViewModel ViewModel => (SetupViewModel)DataContext;

    private void OnViewLoaded()
    {
        if (SystemParameters.ClientAreaAnimation)
        {
            ((Storyboard)Resources["CardIn"]).Begin(this, true);
        }
        else
        {
            SetupCard.Opacity = 1;
            ((TranslateTransform)SetupCard.RenderTransform).Y = 0;
        }
    }

    private async void Initialize_OnClick(object sender, RoutedEventArgs e) => await InitializeAsync();

    private async void Confirmation_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!ViewModel.IsInputEnabled)
        {
            return;
        }

        try
        {
            await ViewModel.InitializeAsync(NewPasswordInput.Password, ConfirmationInput.Password);
        }
        finally
        {
            ClearPasswordInputs();
        }
    }

    private void ClearPasswordInputs()
    {
        NewPasswordInput.Clear();
        ConfirmationInput.Clear();
    }
}
