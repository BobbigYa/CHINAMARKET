using System.Net;
using System.Net.Sockets;
using Xunit;

namespace Tpos.Tests;

public sealed class OfflineAuthenticationTests
{
    private const string Email = "offline@example.test";
    private const string Password = "Offline-Test-Password-2026!";

    [OfflineFact]
    public async Task OutboundNetworkBlocked_LocalSqliteLoginStillSucceeds()
    {
        using var harness = await TestHarness.CreateAsync();
        var initialized = await harness.Service.InitializeAdministratorAsync(Email, Password, Password);
        Assert.True(initialized.IsSuccess);

        var networkIsBlocked = false;
        using (var client = new TcpClient())
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            try
            {
                await client.ConnectAsync(IPAddress.Parse("1.1.1.1"), 443, timeout.Token);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                networkIsBlocked = true;
            }
        }

        Assert.True(networkIsBlocked, "测试进程的外网连接未被阻断，不能把本项视为离线测试。");

        var result = await harness.Service.AuthenticateAsync(Email, Password);
        Assert.True(result.IsSuccess);
    }
}
