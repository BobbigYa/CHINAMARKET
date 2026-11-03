using Xunit;

namespace Tpos.Tests;

public sealed class OfflineFactAttribute : FactAttribute
{
    public OfflineFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("TPOS_OFFLINE_TEST"), "1", StringComparison.Ordinal))
        {
            Skip = "仅在 Windows CI 的出站网络阻断测试中执行。";
        }
    }
}
