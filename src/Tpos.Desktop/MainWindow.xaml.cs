using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using Tpos.Desktop.Services;
using Tpos.Desktop.ViewModels;

namespace Tpos.Desktop;

public partial class MainWindow : Window
{
    private readonly NativeWindowManager _nativeWindow;
    private bool? _authenticationMode;

    public MainWindow()
    {
        InitializeComponent();
        _nativeWindow = new NativeWindowManager(this);
        SourceInitialized += MainWindow_OnSourceInitialized;
        DataContextChanged += MainWindow_OnDataContextChanged;
        StateChanged += (_, _) => _nativeWindow.UpdateWindowFrame();
        Closed += (_, _) =>
        {
            _nativeWindow.Detach();
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
            }
        };
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
    }

    private void UpdateAutomationStatus() =>
        AutomationProperties.SetItemStatus(this, "Backdrop=None;Corners=Standard;Background=ShapeWaves");

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
