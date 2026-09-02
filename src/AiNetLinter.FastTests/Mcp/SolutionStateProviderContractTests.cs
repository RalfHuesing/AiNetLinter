#nullable enable

using System.Reflection;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Component")]
public sealed class SolutionStateProviderContractTests
{
    [Fact]
    public async Task AssemblyAnalysisBoundary_AcceptsOnlySolutionStateProvider()
    {
        using var temp = TestTempDirectory.Create("solution-state-provider-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SolutionStateProviderProbe",
            "namespace Probe; public sealed class Target { }");
        var state = new TestSolutionStateProvider();

        var preparation = await AssemblyAnalysisToolSupport.PrepareAsync(
            state,
            assemblyPath,
            receiverType: null,
            CancellationToken.None);

        Assert.Null(preparation.Error);
        Assert.NotNull(preparation.Context);
        Assert.Equal(1, state.GetCurrentSolutionCallCount);
        Assert.Equal(typeof(ISolutionStateProvider), GetLeaseServerParameterType());
        Assert.Equal(typeof(ISolutionStateProvider), GetLeaseServerPropertyType());
        Assert.Equal(typeof(ISolutionStateProvider), GetToolStatePropertyType());
        Assert.True(typeof(ISolutionStateProvider).IsAssignableFrom(typeof(McpCodeGraphServer)));
    }

    private static Type GetLeaseServerParameterType() =>
        typeof(AssemblyAnalysisLease)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single()
            .GetParameters()[2]
            .ParameterType;

    private static Type GetLeaseServerPropertyType() =>
        typeof(AssemblyAnalysisLease)
            .GetProperty("Server", BindingFlags.Instance | BindingFlags.NonPublic)!
            .PropertyType;

    private static Type GetToolStatePropertyType() =>
        typeof(AssemblyToolExecutionParameters)
            .GetProperty("State", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .PropertyType;

    private sealed class TestSolutionStateProvider : ISolutionStateProvider
    {
        internal int GetCurrentSolutionCallCount { get; private set; }

        public AnalysisSymbolIdentity? AssemblySymbolIdentity => null;
        public ServerLoadState LoadState => ServerLoadState.Loaded;
        public ILintConsole Console => null!;

        public Solution? GetCurrentSolution()
        {
            GetCurrentSolutionCallCount++;
            return null;
        }

        public (ILinterEngineConfig Config, bool UsedDefaultConfig, string? ResolvedConfigPath)
            GetConfigSnapshot() => (null!, false, null);
    }
}
