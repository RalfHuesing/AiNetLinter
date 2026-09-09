#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisCatalogBudgetTests
{
    [Fact]
    public async Task InspectAssembly_BudgetPreservesCatalogIdentityForHandoff()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-catalog-budget-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "CatalogProbe", """
            namespace Probe;
            public sealed class First { public string Value => "first"; }
            public sealed class Second { public string Value => "second"; }
            """);

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, null, null, true, 1),
            CancellationToken.None);
        var payload = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(result);
        var type = Assert.Single(payload.Types);

        Assert.False(string.IsNullOrWhiteSpace(type.Name));
        Assert.False(string.IsNullOrWhiteSpace(type.Kind));
        Assert.False(string.IsNullOrWhiteSpace(type.Id));
        Assert.True(type.Handoff);
    }
}
