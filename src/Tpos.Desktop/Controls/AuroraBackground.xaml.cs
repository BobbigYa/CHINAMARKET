using System.Windows;
using System.Windows.Media.Animation;

namespace Tpos.Desktop.Controls;

public partial class AuroraBackground
{
    private static readonly string[] AnimationKeys = ["DriftOne", "DriftTwo", "DriftThree"];

    public AuroraBackground()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAnimations();
        IsVisibleChanged += (_, _) => UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        foreach (var key in AnimationKeys)
        {
            if (Resources[key] is not Storyboard storyboard)
            {
                continue;
            }

            if (IsVisible && SystemParameters.ClientAreaAnimation)
            {
                storyboard.Begin(this, true);
            }
            else
            {
                storyboard.Stop(this);
            }
        }
    }
}
