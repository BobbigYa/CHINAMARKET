using System.Windows;
using System.Windows.Media.Animation;

namespace Tpos.Desktop.Controls;

public partial class AuroraBackground
{
    private static readonly string[] AnimationKeys = ["DriftOne", "DriftTwo", "DriftThree"];
    private bool _animationsPaused;
    private bool _animationsStarted;

    public AuroraBackground()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAnimations();
        Unloaded += (_, _) => StopAnimations();
        IsVisibleChanged += (_, _) => UpdateAnimations();
    }

    public void SetAnimationsPaused(bool paused)
    {
        _animationsPaused = paused;
        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        if (!IsLoaded || !IsVisible || !SystemParameters.ClientAreaAnimation)
        {
            StopAnimations();
            return;
        }

        foreach (var key in AnimationKeys)
        {
            if (Resources[key] is not Storyboard storyboard)
            {
                continue;
            }

            if (_animationsPaused)
            {
                if (_animationsStarted)
                {
                    storyboard.Pause(this);
                }
            }
            else if (_animationsStarted)
            {
                storyboard.Resume(this);
            }
            else
            {
                storyboard.Begin(this, true);
            }
        }

        if (!_animationsPaused)
        {
            _animationsStarted = true;
        }
    }

    private void StopAnimations()
    {
        foreach (var key in AnimationKeys)
        {
            if (Resources[key] is Storyboard storyboard)
            {
                storyboard.Stop(this);
            }
        }

        _animationsStarted = false;
    }
}
