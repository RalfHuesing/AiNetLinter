#nullable enable

using System;
using System.Text;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Tests fuer <see cref="McpServerOptionsFactory"/>: zentraler Instructions-Vertrag
/// (Target-Pflicht + kompakter Bootstrap-Verweis) inkl. UTF8-Budget und
/// Toolbestands-Paritaet.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpServerOptionsFactoryTests
{
    [Fact]
    public void Create_ServerInstructionsCarriesAnalysisTargetContract()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        Assert.False(string.IsNullOrEmpty(options.ServerInstructions));
        Assert.Contains("targetPath", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains(".slnx", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains(".dll", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains(".exe", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("search_pattern", options.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("Content enthält", options.ServerInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ServerInstructionsStaysWithinUtf8Budget()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        Assert.InRange(
            Encoding.UTF8.GetByteCount(ServerInstructions.Text),
            1,
            ServerInstructions.MaxUtf8Bytes);
        Assert.DoesNotContain("\n- ", ServerInstructions.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ServerInstructionsContainsWorkflowGuidance()
    {
        Assert.Contains("C#-Symbole", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("tools/list", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("ainetlinter://agent-guide", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.Contains("Vollständigkeit", ServerInstructions.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("kopierfaehigem Template", ServerInstructions.Text, StringComparison.Ordinal);
    }
}
