#nullable enable

using System;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Tests fuer <see cref="McpServerOptionsFactory"/>: konzentriert auf den zentralen Scope-Hint,
/// der via <c>McpServerOptions.ServerInstructions</c> in der
/// <c>initialize</c>-Antwort des Servers landet. Aus <c>McpServerCommandTests.cs</c> ausgelagert,
/// weil diese Datei bereits am <c>MaxLineCount</c>-Limit (500) liegt und das Hinzufuegen
/// weiterer Tests dort <c>CliIntegrationTests</c> brechen wuerde.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpServerOptionsFactoryTests
{
    [Fact]
    public void Create_ServerInstructionsContainsScopeHint()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var options = McpServerOptionsFactory.Create(state);

        Assert.False(string.IsNullOrEmpty(options.ServerInstructions));
        Assert.Contains(".cs", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("search_pattern", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains(".js", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains(".xaml", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("enrichCSharp=true", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("unavailable", options.ServerInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ServerInstructionsStaysWithinUtf8Budget()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var options = McpServerOptionsFactory.Create(state);

        var registeredNames = options.ToolCollection!
            .Select(t => t.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            OverviewResourceRegistration.ToolSummaries.Select(t => t.Name).ToHashSet(StringComparer.Ordinal),
            registeredNames);

        Assert.InRange(
            Encoding.UTF8.GetByteCount(ServerInstructions.Text),
            1,
            ServerInstructions.MaxUtf8Bytes);
        Assert.DoesNotContain("\n- ", ServerInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolRegistration_MatchesOverviewResourceTools()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var options = McpServerOptionsFactory.Create(state);
        var registeredNames = options.ToolCollection!
            .Select(t => t.ProtocolTool.Name)
            .ToHashSet(StringComparer.Ordinal);
        var overviewNames = OverviewResourceRegistration.ToolSummaries
            .Select(t => t.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(registeredNames, overviewNames);
    }

    [Fact]
    public void Create_ServerInstructionsContainsWorkflowGuidance()
    {
        Assert.Contains("C#-Symbolgraph-Grenze", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("tools/list", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://overview", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Sufficiency", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("isError=true", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Edits", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Impact", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Gate", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("enrichCSharp=true", ServerInstructions.Text, StringComparison.Ordinal);
    }
}
