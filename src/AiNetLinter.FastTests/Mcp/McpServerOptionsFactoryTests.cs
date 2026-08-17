#nullable enable

using System;
using System.Linq;
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
    }

    [Fact]
    public void Create_ServerInstructionsContainsAllRegisteredTools()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));
        var options = McpServerOptionsFactory.Create(state);

        var registeredNames = options.ToolCollection!.Select(t => t.ProtocolTool.Name).ToList();
        Assert.Equal(22, registeredNames.Count);

        foreach (var name in registeredNames)
        {
            Assert.Contains($"- {name}:", ServerInstructions.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ServerInstructions_MatchesOverviewResourceTools()
    {
        var overviewNames = OverviewResourceRegistration.ToolSummaries.Select(t => t.Name).ToList();
        Assert.Equal(22, overviewNames.Count);

        foreach (var name in overviewNames)
        {
            Assert.Contains($"- {name}:", ServerInstructions.Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Create_ServerInstructionsContainsWorkflowGuidance()
    {
        Assert.Contains("Empfohlene Workflows:", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Code erkunden:", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Refactoring & Impact:", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Quality-Gate vor Commit:", ServerInstructions.Text, StringComparison.Ordinal);
    }
}
