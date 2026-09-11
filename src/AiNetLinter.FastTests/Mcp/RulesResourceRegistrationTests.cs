#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>Vertragstests fuer die dynamische Resource der effektiven Regelkonfiguration.</summary>
[Trait("Category", "Unit")]
public sealed class RulesResourceRegistrationTests
{
    [Fact]
    public void BuildRulesText_UsesEffectiveSnapshotAndConfigOrigin()
    {
        var state = CreateServer(new Config
        {
            Global = new GlobalConfig { EnforceSealedClasses = true },
            Metrics = new MetricsConfig { MaxLineCount = 42, MaxMethodLineCount = 17 },
        });
        using var harness = OverviewSnapshotHarness.Create(state);

        var text = RulesResourceRegistration.BuildRulesText(harness.Snapshot);

        Assert.StartsWith("# AiNetLinter — effektive Regelkonfiguration", text, StringComparison.Ordinal);
        Assert.Contains($"- targetPath: `{harness.RootPath}`", text, StringComparison.Ordinal);
        Assert.Contains("- Konfigurationsquelle: `C:\\Projekt\\ainetlinter-rules.json`", text, StringComparison.Ordinal);
        Assert.Contains("## Aktive Regeln", text, StringComparison.Ordinal);
        Assert.Contains("`EnforceSealedClasses`", text, StringComparison.Ordinal);
        Assert.Contains("## Effektive Schwellwerte", text, StringComparison.Ordinal);
        Assert.Contains("| `MaxLineCount` | 42 | aktiv |", text, StringComparison.Ordinal);
        Assert.Contains("| `MaxMethodLineCount` | 17 | aktiv |", text, StringComparison.Ordinal);
        Assert.Contains("## Deaktivierte Regeln", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRulesText_RefreshesAfterAtomicConfigReload()
    {
        var state = CreatePendingServer(new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig { MaxLineCount = 42 },
        });
        using var harness = OverviewSnapshotHarness.Create(state);

        var before = RulesResourceRegistration.BuildRulesText(harness.Snapshot);
        state.ReloadConfig(
            new Config
            {
                Global = new GlobalConfig { EnforceXmlDocumentation = true },
                Metrics = new MetricsConfig { MaxLineCount = 99 },
            },
            resolvedConfigPath: @"C:\Projekt\ainetlinter-rules.json");
        var after = RulesResourceRegistration.BuildRulesText(harness.Snapshot);

        Assert.Contains("| `MaxLineCount` | 42 |", before, StringComparison.Ordinal);
        Assert.Contains("| `MaxLineCount` | 99 |", after, StringComparison.Ordinal);
        Assert.DoesNotContain("| `MaxLineCount` | 42 |", after, StringComparison.Ordinal);
        Assert.Contains("`C:\\Projekt\\ainetlinter-rules.json`", after, StringComparison.Ordinal);
        Assert.Contains("`EnforceXmlDocumentation`", after, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRulesText_NoConfigStatesNotConfigured()
    {
        var state = CreateServer(new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig(),
        }, resolvedConfigPath: null);
        using var harness = OverviewSnapshotHarness.Create(state);

        var text = RulesResourceRegistration.BuildRulesText(harness.Snapshot);

        Assert.Contains("Konfigurationsquelle: `not_configured`", text, StringComparison.Ordinal);
        Assert.Contains("Navigation bleibt verfügbar", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildTemplatedResult_UsesRulesUriAndSharedProjectGuards()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "SymbolGraphMini");
        var solutionPath = Path.Combine(fixture.RootPath, "SymbolGraphMini.slnx");
        var state = CreatePendingServer(new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig { MaxLineCount = 42 },
        });
        await using var registry = ProjectRegistryFixture.Create(_ => ProjectInstanceCreation.Resident(state));
        var lease = registry.Lease(solutionPath);
        Assert.True(lease.Succeeded);
        lease.Lease!.Dispose();

        var result = RulesResourceRegistration.BuildTemplatedResult(registry, solutionPath);
        var content = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));

        Assert.Equal($"ainetlinter://rules?targetPath={Uri.EscapeDataString(solutionPath)}", content.Uri);
        Assert.Equal("text/markdown", content.MimeType);
        Assert.Contains("| `MaxLineCount` | 42 |", content.Text, StringComparison.Ordinal);
        Assert.Contains($"- targetPath: `{solutionPath}`", content.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("- origin: `source`", content.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("- snapshot: `", content.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("- status: operation=`ok`", content.Text, StringComparison.Ordinal);
        Assert.Throws<ModelContextProtocol.McpException>(
            () => RulesResourceRegistration.BuildTemplatedResult(registry, "relative/path"));
    }

    private static McpCodeGraphServer CreateServer(
        Config config,
        string? resolvedConfigPath = @"C:\Projekt\ainetlinter-rules.json") =>
        new(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null,
                Console: LinterConsole.Instance,
                Config: config,
                ResolvedConfigPath: resolvedConfigPath)));

    private static McpCodeGraphServer CreatePendingServer(Config config) =>
        new(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            Config = config,
            ResolvedConfigPath = @"C:\Projekt\ainetlinter-rules.json",
            LoadFunc = token =>
            {
                var pending = new TaskCompletionSource<SourceFileCatalog?>(TaskCreationOptions.RunContinuationsAsynchronously);
                token.Register(() => pending.TrySetCanceled(token));
                return pending.Task;
            },
        });
}
