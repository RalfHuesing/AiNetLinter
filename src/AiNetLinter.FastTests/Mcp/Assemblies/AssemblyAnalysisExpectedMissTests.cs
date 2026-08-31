#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Unit")]
// @covers AssemblyAnalysisDispatcher
// @covers AssemblySymbolResolver
public sealed class AssemblyAnalysisExpectedMissTests
{
    [Fact]
    public async Task AssemblyRoute_ExpectedReferenceMissIsNotAddedToNavigationDiagnostics()
    {
        using var temp = TestTempDirectory.Create("assembly-dispatcher-expected-miss-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ExpectedMissDependency",
            "namespace Probe; public sealed class DependencyOnly { public int Value => 1; }");
        var rootPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ExpectedMissRoot",
            "namespace Probe; public sealed class Root { public int Read() => 1; }",
            dependencyPath);
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await AnalysisToolCall.ExecuteRouted(
            AssemblyAnalysisDispatcher.CreateRoute(registry),
            new AnalysisToolCallRequest(
                new AnalysisTargetRequest("assembly", rootPath),
                new AnalysisToolDispatch(
                    AssemblySessionCall: lease => AssemblyFindReferencesTool.ExecuteAsync(
                        lease,
                        new AssemblyFindReferencesRequest("Probe.Root.Read", 50, 1, true),
                        CancellationToken.None),
                    ExpandAssemblyReferences: true),
                CancellationToken.None));

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value;
        var navigation = payload.GetProperty("navigation");
        var diagnostics = navigation.GetProperty("diagnostics")
            .EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
        Assert.False(diagnostics.Any(diagnostic => diagnostic.Contains("SYMBOL_NOT_FOUND", StringComparison.Ordinal)));
    }
}
