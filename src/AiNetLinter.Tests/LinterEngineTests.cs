using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class LinterEngineTests
{
    [Fact]
    public void LinterEngine_CanBeInitialized()
    {
        var config = new LinterConfig
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig()
        };
        var engine = new LinterEngine(config);
        Assert.NotNull(engine);
    }
}
