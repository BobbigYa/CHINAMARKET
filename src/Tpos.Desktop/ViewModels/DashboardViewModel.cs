using Tpos.Desktop.Mvvm;

namespace Tpos.Desktop.ViewModels;

public sealed class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(Func<Task> logout)
    {
        LogoutCommand = new AsyncRelayCommand(logout);
    }

    public AsyncRelayCommand LogoutCommand { get; }
}
