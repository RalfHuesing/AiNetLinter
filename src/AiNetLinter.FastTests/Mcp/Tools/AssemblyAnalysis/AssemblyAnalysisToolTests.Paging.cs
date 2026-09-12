#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
    [Fact]
    public async Task InspectAssembly_RejectsUnboundOrQueryMismatchedContinuation()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-paging-binding-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "BoundPagingProbe", """
            namespace Probe;
            public sealed class Alpha { }
            public sealed class Beta { }
            """);

        var first = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1),
            CancellationToken.None);
        var firstToken = AssemblyAnalysisTestSupport.ContinuationTokenOf(first);

        var unbound = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1, Cursor: "1"),
            CancellationToken.None);
        Assert.Contains("INVALID_ARGUMENT", AssemblyAnalysisTestSupport.TextOf(unbound), StringComparison.Ordinal);

        var changedQuery = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe", null, null, true, 1, Cursor: firstToken),
            CancellationToken.None);
        Assert.Contains("INVALID_ARGUMENT", AssemblyAnalysisTestSupport.TextOf(changedQuery), StringComparison.Ordinal);
    }
}
