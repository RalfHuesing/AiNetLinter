#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task AssemblyRoute_ExtensionsWithoutIncludeReferences_StaysRootOnly()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-extensions-root-only-");
        var reference = new AssemblyReferenceDto(
            "UnrequestedExtensionDependency",
            "1.0.0.0",
            "neutral",
            Resolved: true,
            ResolvedPath: Path.Combine(temp.DirectoryPath, "UnrequestedExtensionDependency.dll"));
        await using var fixture = await SyntheticAssemblyFixture.CreateAsync(temp, [reference], FailingReferenceFactory);

        var result = await fixture.ExecuteExtensionsAsync(includeReferences: false);

        var payload = Structured(result);
        Assert.Equal(0, payload.GetProperty("referenceSessions").GetArrayLength());
        Assert.DoesNotContain(
            Diagnostics(payload),
            diagnostic => diagnostic.Contains("UnrequestedExtensionDependency", StringComparison.Ordinal));
        Assert.Equal(1, payload.GetProperty("referenceSummary").GetProperty("totalReferenceCount").GetInt32());
        Assert.Equal(0, payload.GetProperty("referenceSummary").GetProperty("shownReferenceSessionCount").GetInt32());
    }
}
