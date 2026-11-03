using System.Windows;
using System.Windows.Media.Animation;

namespace Tpos.Desktop.Views;

public partial class AuthenticationView
{
    public AuthenticationView()
    {
        InitializeComponent();
        Loaded += (_, _) => StartLogoAnimation();
        IsVisibleChanged += (_, _) => StartLogoAnimation();
    }

    private void StartLogoAnimation()
    {
        if (Resources["LogoFloat"] is not Storyboard storyboard)
        {
            return;
        }

        if (IsVisible && IsLoaded && SystemParameters.ClientAreaAnimation)
        {
            storyboard.Begin(this, true);
        }
        else
        {
            storyboard.Stop(this);
        }
    }
}
