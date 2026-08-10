#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Wendet die <see cref="TestSentinelConfigOverride"/>-Sektion auf eine
/// <see cref="TestSentinelConfig"/>-Instanz an. Bewusst als statische Helper-Klasse
/// extrahiert (nicht als Instanzmethode auf <see cref="TestSentinelConfig"/>),
/// damit der <c>TestSentinelConfig</c>-Record selbst schmaler bleibt und die
/// <c>AIContextFootprint</c>-Last pro transitivem Konsumenten (z. B. die
/// <c>*ToolRegistrations</c>-Klassen im MCP-Pfad) sinkt.
/// </summary>
internal static class TestSentinelConfigApplier
{
    public static TestSentinelConfig Apply(TestSentinelConfig config, TestSentinelConfigOverride? @override)
    {
        if (@override == null) return config;
        return config with
        {
            ExemptClassNameSuffixes = @override.ExemptClassNameSuffixes ?? config.ExemptClassNameSuffixes,
            ExemptWhenInheritsFrom = @override.ExemptWhenInheritsFrom ?? config.ExemptWhenInheritsFrom,
            ExemptStaticClasses = @override.ExemptStaticClasses ?? config.ExemptStaticClasses,
            TestProjectNameSuffixes = @override.TestProjectNameSuffixes ?? config.TestProjectNameSuffixes,
        };
    }
}
