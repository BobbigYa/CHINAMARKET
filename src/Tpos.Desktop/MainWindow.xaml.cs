using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Tpos.Desktop.Controls;
using Tpos.Desktop.Services;
using Tpos.Desktop.ViewModels;
using Tpos.Desktop.Views;

namespace Tpos.Desktop;

public partial class MainWindow : Window
{
    private readonly NativeWindowManager _nativeWindow;
    private readonly DispatcherTimer _idleAnimationTimer;
    private bool _animationsPaused;
    private bool _authenticationMode = true;
    private DateTimeOffset _lastAnimationActivity = DateTimeOffset.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _nativeWindow = new NativeWindowManager(this, WindowSurface);
        _idleAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _idleAnimationTimer.Tick += IdleAnimationTimer_OnTick;

        SourceInitialized += MainWindow_OnSourceInitialized;
        Loaded += (_, _) => NotifyAnimationActivity();
        DataContextChanged += MainWindow_OnDataContextChanged;
        Activated += (_, _) => NotifyAnimationActivity();
        Deactivated += (_, _) => PauseAnimations();
        StateChanged += MainWindow_OnStateChanged;
        PreviewMouseMove += (_, _) => NotifyAnimationActivity();
        PreviewMouseDown += (_, _) => NotifyAnimationActivity();
        PreviewKeyDown += (_, _) => NotifyAnimationActivity();
        Closed += MainWindow_OnClosed;
    }

    private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            _nativeWindow.Attach(source);
            UpdateAutomationStatus();
        }
    }

    private void MainWindow_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        if (e.NewValue is MainViewModel newViewModel)
        {
            newViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            ApplyCurrentPageMode(newViewModel);
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage) && sender is MainViewModel viewModel)
        {
            ApplyCurrentPageMode(viewModel);
        }
    }

    private void ApplyCurrentPageMode(MainViewModel viewModel)
    {
        var authenticationMode = viewModel.CurrentPage is not DashboardViewModel;
        if (_authenticationMode == authenticationMode)
        {
            Dispatcher.BeginInvoke(new Action(ApplyAnimationState));
            return;
        }

        _authenticationMode = authenticationMode;
        MinWidth = authenticationMode ? 360 : 760;
        MinHeight = authenticationMode ? 400 : 600;
        WindowState = WindowState.Normal;
        Width = authenticationMode ? 440 : 1100;
        Height = authenticationMode ? 540 : 700;

        if (IsLoaded)
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        _nativeWindow.SetAuthenticationMode(authenticationMode);
        UpdateAutomationStatus();
        Dispatcher.BeginInvoke(new Action(ApplyAnimationState));
    }

    private void UpdateAutomationStatus()
    {
        AutomationProperties.SetItemStatus(
            this,
            $"Backdrop={_nativeWindow.EffectiveBackdrop};Corners={_nativeWindow.EffectiveCorners}");
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        _nativeWindow.UpdateWindowFrame();
        UpdateAutomationStatus();
        if (WindowState == WindowState.Minimized)
        {
            PauseAnimations();
        }
        else
        {
            NotifyAnimationActivity();
        }
    }

    private void IdleAnimationTimer_OnTick(object? sender, EventArgs e)
    {
        _idleAnimationTimer.Stop();
        _animationsPaused = true;
        ApplyAnimationState();
    }

    private void NotifyAnimationActivity()
    {
        var now = DateTimeOffset.UtcNow;
        if (!_animationsPaused && now - _lastAnimationActivity < TimeSpan.FromMilliseconds(750))
        {
            return;
        }

        _lastAnimationActivity = now;
        _animationsPaused = false;
        ApplyAnimationState();
        _idleAnimationTimer.Stop();
        if (IsActive && WindowState != WindowState.Minimized)
        {
            _idleAnimationTimer.Start();
        }
    }

    private void PauseAnimations()
    {
        _idleAnimationTimer.Stop();
        _animationsPaused = true;
        ApplyAnimationState();
    }

    private void ApplyAnimationState()
    {
        ApplyAnimationState(PageContent, _animationsPaused || !IsActive || WindowState == WindowState.Minimized);
    }

    private static void ApplyAnimationState(DependencyObject element, bool paused)
    {
        switch (element)
        {
            case AuroraBackground aurora:
                aurora.SetAnimationsPaused(paused);
                break;
            case AuthenticationView authentication:
                authentication.SetAnimationsPaused(paused);
                break;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            ApplyAnimationState(VisualTreeHelper.GetChild(element, index), paused);
        }
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _idleAnimationTimer.Stop();
        _idleAnimationTimer.Tick -= IdleAnimationTimer_OnTick;
        _nativeWindow.Detach();
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleMaximize();
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_OnClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_OnClick(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
