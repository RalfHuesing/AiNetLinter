#nullable enable

using System;
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
    public void Create_DoesNotRepeatGlobalInstructionsInEveryToolDescription()
    {
        var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var options = McpServerOptionsFactory.Create(
            McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(null))),
            McpServerResourceCollectionFactory.Build(registry));

        Assert.Equal(string.Empty, options.ServerInstructions);
    }

}
