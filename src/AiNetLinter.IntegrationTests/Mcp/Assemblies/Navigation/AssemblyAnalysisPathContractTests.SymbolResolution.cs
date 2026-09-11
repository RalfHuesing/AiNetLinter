#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

public sealed partial class AssemblyAnalysisPathContractTests
{
    [Fact]
    public async Task AssemblyRoute_ExactSymbolMatchPrecedesSubstringMatch()
    {
        using var temp = TestTempDirectory.Create("assembly-exact-before-substring-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp, "ExactBeforeSubstringProbe",
            "namespace Probe; public sealed class ServiceExtended { } public sealed class Service { }");
        await using var registry = new AssemblyAnalysisRegistry();

        var result = await DispatchAsync(registry, assemblyPath, lease => AssemblyFindSymbolTool.ExecuteAsync(
            lease, new AssemblyFindSymbolRequest(["Service"], "class", 10, false), CancellationToken.None));

        Assert.False(result.IsError == true, Text(result));
        var matches = result.StructuredContent!.Value.GetProperty("results")[0]
            .GetProperty("matches").EnumerateArray().ToArray();
        Assert.Equal("Probe.Service", matches[0].GetProperty("name").GetString());
        Assert.Equal("Probe.ServiceExtended", matches[1].GetProperty("name").GetString());
    }
}
