#nullable enable

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Unit")]
public sealed class GetClassStructureAssemblyHandoffTests
{
    [Fact]
    public async Task ExecuteAsync_AssemblyMemberHandoff_IsReusableByGetSymbolBody()
    {
        using var temp = TestTempDirectory.Create("class-structure-assembly-handoff-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "ClassStructureProbe", """
            namespace Probe;
            public sealed class Api
            {
                public string Name { get; set; } = "";
                public void Execute() { }
            }
            """);
        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        using var lease = Assert.IsType<AssemblyAnalysisLease>(leaseResult.Lease);

        var structure = await GetClassStructureTool.ExecuteAsync(lease.Server, "Probe.Api", "lines", CancellationToken.None);
        var structureText = AssemblyAnalysisTestSupport.TextOf(structure);
        Assert.DoesNotContain("M:Probe.Api.#ctor", structureText, StringComparison.Ordinal);
        var handoffId = Regex.Match(
            structureText,
            @"\| Method \| Execute \| [^\r\n]*handoffId: `(?<id>h:[^`]+)`").Groups["id"].Value;

        Assert.NotEmpty(handoffId);
        var body = await GetSymbolBodyTool.ExecuteAsync(lease, [handoffId], 80, CancellationToken.None);
        Assert.NotEqual(true, body.IsError);
        Assert.Contains("Execute", AssemblyAnalysisTestSupport.TextOf(body), StringComparison.Ordinal);
    }
}
