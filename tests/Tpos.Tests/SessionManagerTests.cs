using Tpos.Core.Authentication;
using Xunit;

namespace Tpos.Tests;

public sealed class SessionManagerTests
{
    [Fact]
    public void Session_StartsSignedOut_LogoutClearsIt_AndNewSessionRequiresPassword()
    {
        var session = new SessionManager();
        Assert.False(session.IsAuthenticated);

        session.SignIn(DateTimeOffset.UtcNow);
        Assert.True(session.IsAuthenticated);

        session.SignOut();
        Assert.False(session.IsAuthenticated);
        Assert.Null(session.AuthenticatedAtUtc);

        var afterApplicationRestart = new SessionManager();
        Assert.False(afterApplicationRestart.IsAuthenticated);
        Assert.Null(afterApplicationRestart.AuthenticatedAtUtc);
    }
}
