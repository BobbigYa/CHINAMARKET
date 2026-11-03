using System.IO;
using System.Windows;
using System.Windows.Threading;
using Tpos.Core.Authentication;
using Tpos.Desktop.Services;
using Tpos.Desktop.ViewModels;
using Tpos.Infrastructure.Persistence;
using Tpos.Infrastructure.Security;

namespace Tpos.Desktop;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Local\Tpos.Desktop.SingleInstance";
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsMutex);
        if (!_ownsMutex)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            MessageBox.Show(
                "TPOS^ 已经在运行。",
                "TPOS^",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            var localization = new LocalizationService();
            Resources["Loc"] = localization;

            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Tpos",
                "Security");
            var databasePath = Path.Combine(dataDirectory, "tpos-auth.db");
            LocalDataSecurity.SecureDataDirectory(dataDirectory);
            LocalDataSecurity.SecureExistingDatabaseFiles(dataDirectory);
            var store = new SqliteCredentialStore(databasePath);
            await store.InitializeAsync();
            LocalDataSecurity.SecureExistingDatabaseFiles(dataDirectory);

            var authenticationService = new AuthenticationService(
                store,
                new Argon2PasswordHasher());
            var session = new SessionManager();
            var viewModel = new MainViewModel(authenticationService, localization, session);
            var window = new MainWindow { DataContext = viewModel };
            MainWindow = window;
            window.Show();
            await viewModel.InitializeAsync();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "TPOS^ 无法初始化本地安全数据。请检查程序数据目录权限后重试。",
                "TPOS^",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "操作未能完成。TPOS^ 将安全关闭，请重新打开后再试。",
            "TPOS^",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Current.Shutdown(-1);
    }
}
