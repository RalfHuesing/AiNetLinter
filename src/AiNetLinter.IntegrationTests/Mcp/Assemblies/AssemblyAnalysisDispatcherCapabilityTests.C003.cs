#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

public sealed partial class AssemblyAnalysisDispatcherCapabilityTests
{
    [Fact]
    public async Task AssemblyRoute_FindSymbolIncludeReferencesKeepsMatchCountsConsistentUnderStandardBudget()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-c003-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "C003SymbolDependency",
            "namespace Probe; public sealed class NeedleDependency { public int Marker => 42; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "C003SymbolRoot",
            "namespace Probe; public sealed class Root { public NeedleDependency Value { get; } = new(); }",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest(rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindSymbolTool.ExecuteAsync(
                        lease,
                        new AssemblyFindSymbolRequest(["Needle"], null, 50, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var payload = result.StructuredContent!.Value;
        var pattern = Assert.Single(payload.GetProperty("results").EnumerateArray());
        var matches = pattern.GetProperty("matches").EnumerateArray().ToArray();
        Assert.NotEmpty(matches);
        Assert.Contains(matches, match => match.GetProperty("name").GetString() == "Probe.NeedleDependency");

        // Red-Test-Nachweis: aktuell werden diese DTO-Zähler im Assembly-includeReferences-
        // Pfad nicht aus den tatsächlich gelieferten Treffern befüllt und bleiben 0.
        Assert.Equal(matches.Length, pattern.GetProperty("totalCount").GetInt32());
        Assert.Equal(matches.Length, pattern.GetProperty("returnedCount").GetInt32());
        Assert.Equal(matches.Length, payload.GetProperty("totalCount").GetInt32());
        Assert.Equal(matches.Length, payload.GetProperty("returnedCount").GetInt32());

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Probe.NeedleDependency", text, StringComparison.Ordinal);
        AssertFinalCombinedBudget(result, AssemblyAnalysisResponseLimits.DefaultResponseBytes);
    }
}
