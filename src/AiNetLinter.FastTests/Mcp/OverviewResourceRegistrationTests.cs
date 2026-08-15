#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Tests fuer die MCP-Resource <c>ainetlinter://overview</c> (<see cref="OverviewResourceRegistration"/>):
/// dynamischer Server-Status-Text (Solution-Pfad, Config-Quelle, Loading-Zustand) und
/// Parität der gepflegten Tool-Kurzbeschreibungen gegen die tatsaechlich registrierten Tools —
/// damit ein neues oder umbenanntes Tool hier nicht stillschweigend fehlt.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OverviewResourceRegistrationTests
{
    private static Config CreateConfig() => new() { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

    [Fact]
    public void BuildOverviewText_DefaultConfig_MentionsDefaultRulesNotProjectConfig()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, UsedDefaultConfig: true, ResolvedConfigPath: null)));

        var text = OverviewResourceRegistration.BuildOverviewText(state);

        Assert.Contains("keine rules.json gefunden", text, StringComparison.Ordinal);
        Assert.Contains("Default-Regeln", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOverviewText_ExplicitConfig_ShowsResolvedConfigPath()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(
                null, UsedDefaultConfig: false, ResolvedConfigPath: @"C:\Projekt\rules.json")));

        var text = OverviewResourceRegistration.BuildOverviewText(state);

        Assert.Contains(@"C:\Projekt\rules.json", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Default-Regeln", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOverviewText_ListsAllTools()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var text = OverviewResourceRegistration.BuildOverviewText(state);

        Assert.Contains($"Tools ({OverviewResourceRegistration.ToolSummaries.Count})", text, StringComparison.Ordinal);
        foreach (var (name, _) in OverviewResourceRegistration.ToolSummaries)
        {
            Assert.Contains($"`{name}`", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BuildOverviewText_LoadingState_ShowsLoadingPlaceholder()
    {
        var neverCompletes = new TaskCompletionSource<SourceFileCatalog?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var state = new McpCodeGraphServer(new McpCodeGraphServerOptions
        {
            Catalog = null,
            Console = LinterConsole.Instance,
            MaxLineCount = 700,
            Config = CreateConfig(),
            UsedDefaultConfig = false,
            LoadFunc = _ => neverCompletes.Task,
        });

        var text = OverviewResourceRegistration.BuildOverviewText(state);

        Assert.Contains("wird noch geladen", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolSummaries_MatchesRegisteredToolNames()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var options = McpServerOptionsFactory.Create(state);

        var registeredNames = options.ToolCollection!.Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);
        var summarizedNames = OverviewResourceRegistration.ToolSummaries.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(registeredNames, summarizedNames);
    }
}
