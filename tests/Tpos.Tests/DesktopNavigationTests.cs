using Tpos.Core.Authentication;
using Tpos.Desktop.Services;
using Tpos.Desktop.ViewModels;
using Xunit;

namespace Tpos.Tests;

public sealed class DesktopNavigationTests
{
    private const string InitialPassword = "Navigation-Test-Password-2026!";

    [Fact]
    public async Task FirstRun_LoginDashboardAndLogout_UseExpectedPagesAndSessionState()
    {
        using var harness = await TestHarness.CreateAsync();
        var localization = new LocalizationService();
        var session = new SessionManager();
        var main = new MainViewModel(harness.Service, localization, session);

        await main.InitializeAsync();
        var authenticationPage = Assert.IsType<AuthenticationViewModel>(main.CurrentPage);
        Assert.Same(authenticationPage.Setup, authenticationPage.CurrentForm);
        Assert.False(session.IsAuthenticated);

        await authenticationPage.Setup.InitializeAsync(InitialPassword, InitialPassword);
        Assert.Same(authenticationPage.Login, authenticationPage.CurrentForm);
        Assert.False(session.IsAuthenticated);

        await authenticationPage.Login.LoginAsync("Wrong-Navigation-Password!");
        Assert.Same(authenticationPage, main.CurrentPage);
        Assert.False(session.IsAuthenticated);
        Assert.True(authenticationPage.Login.HasMessage);

        await authenticationPage.Login.LoginAsync(InitialPassword);
        var dashboard = Assert.IsType<DashboardViewModel>(main.CurrentPage);
        Assert.True(session.IsAuthenticated);

        await dashboard.LogoutCommand.ExecuteAsync();
        Assert.Same(authenticationPage, main.CurrentPage);
        Assert.Same(authenticationPage.Login, authenticationPage.CurrentForm);
        Assert.False(session.IsAuthenticated);
    }
}
