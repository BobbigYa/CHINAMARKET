using System.Windows;
using System.Windows.Media.Animation;

namespace Tpos.Desktop.Views;

public partial class AuthenticationView
{
    private bool _animationsPaused;
    private bool _animationStarted;

    public AuthenticationView()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateLogoAnimation();
        Unloaded += (_, _) => StopLogoAnimation();
        IsVisibleChanged += (_, _) => UpdateLogoAnimation();
    }

    public void SetAnimationsPaused(bool paused)
    {
        _animationsPaused = paused;
        UpdateLogoAnimation();
    }

    private void UpdateLogoAnimation()
    {
        if (Resources["LogoFloat"] is not Storyboard storyboard)
        {
            return;
        }

        if (!IsVisible || !IsLoaded || !SystemParameters.ClientAreaAnimation)
        {
            StopLogoAnimation();
            BrandMark.RenderTransform = new System.Windows.Media.TranslateTransform();
            return;
        }

        if (_animationsPaused)
        {
            if (_animationStarted)
            {
                storyboard.Pause(this);
            }

            return;
        }

        if (_animationStarted)
        {
            storyboard.Resume(this);
        }
        else
        {
            storyboard.Begin(this, true);
            _animationStarted = true;
        }
    }

    private void StopLogoAnimation()
    {
        if (Resources["LogoFloat"] is Storyboard storyboard)
        {
            storyboard.Stop(this);
        }

        _animationStarted = false;
    }
}
