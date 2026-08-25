#nullable enable

using System;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Tests fuer <see cref="McpServerOptionsFactory"/>: zentraler Instructions-Vertrag
/// (projectRoot-Pflicht + Definitionsdatei) inkl. UTF8-Budget und Toolbestands-Paritaet.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpServerOptionsFactoryTests
{
    [Fact]
    public void Create_ServerInstructionsCarriesProjectRootContract()
    {
        var options = McpServerOptionsFactory.Create(ProjectRegistryFixture.CreateInspectionRegistry());

        Assert.False(string.IsNullOrEmpty(options.ServerInstructions));
        Assert.Contains("projectRoot", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("ainetlinter.project.json", options.ServerInstructions, StringComparison.Ordinal);
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
        var options = McpServerOptionsFactory.Create(ProjectRegistryFixture.CreateInspectionRegistry());

        Assert.InRange(
            Encoding.UTF8.GetByteCount(ServerInstructions.Text),
            1,
            ServerInstructions.MaxUtf8Bytes);
        Assert.DoesNotContain("\n- ", ServerInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ServerInstructionsContainsWorkflowGuidance()
    {
        Assert.Contains("C#-Symbolgraph-Grenze", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("tools/list", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://overview", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://agent-guide", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Sufficiency", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("isError=true", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("get_feature_context -> get_symbol_body", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("find_references/get_impact", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("get_violations", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("enrichCSharp=true", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("RULES_INVALID", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("PROJECT_NOT_INITIALIZED", ServerInstructions.Text, StringComparison.Ordinal);
    }
}
