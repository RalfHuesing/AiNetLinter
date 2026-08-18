#nullable enable

using AiNetLinter.Configuration;

namespace AiNetLinter.TestKit;

/// <summary>
/// Zentrale Factory für Test-Konfigurationen, um redundante Ad-hoc-Initialisierungen in Testsuiten zu vermeiden.
/// </summary>
public static class TestConfigFactory
{
    public static Config CreateEmpty() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };
}
