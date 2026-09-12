#nullable enable

using System;
using System.IO;
using AiNetLinter.TestKit;

namespace AiNetLinter.IntegrationTests.Architecture;

[Trait("Category", "Integration")]
public sealed class AssemblyAnalysisContextOwnershipTests
{
    [Fact]
    public void Composite_UsesTheTypedInspectionPayloadBeforeRendering()
    {
        var root = SolutionRootLocator.Find();
        var sourcePath = Path.Combine(root, "src", "AiNetLinter", "Mcp", "Tools", "AssemblyAnalysis", "AssemblyAnalysisContextTool.cs");
        var source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("InspectAssemblyTool.ExecuteAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("inspection.StructuredContent", source, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonObject", source, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", source, StringComparison.Ordinal);
        Assert.Contains("InspectAssemblyTool.BuildPayload", source, StringComparison.Ordinal);
        Assert.Contains("AssemblyAnalysisContextTextModel", source, StringComparison.Ordinal);
    }
}
