#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

// @covers DeadCodeIndirectUsage
// @covers DeadCodeReflectionUsage
[Trait("Category", "Component")]
public sealed class DeadCodeUsageIndexTests
{
    [Fact]
    public async Task IndexSeparatesWritesAndReadsAndKeepsUnrelatedMember()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Usage.slnx", new ProjectSpec("Host", [("State.cs", """
            public sealed class State
            {
                public int Written;
                public int Read;
                public int Unrelated;
                public int Run() { Written = 1; return Read; }
            }
            """)]));
        var index = await DeadCodeUsageIndex.CreateAsync(fixture.Solution, CancellationToken.None);
        var compilation = await fixture.Solution.Projects.Single().GetCompilationAsync();
        var type = compilation!.GetTypeByMetadataName("State")!;
        Assert.False(index.Analyze(type.GetMembers("Written").Single()).Production);
        Assert.Equal(1, index.Analyze(type.GetMembers("Written").Single()).Writes);
        Assert.True(index.Analyze(type.GetMembers("Read").Single()).Production);
        Assert.False(index.Analyze(type.GetMembers("Unrelated").Single()).Production);
    }

    [Fact]
    public async Task IndirectUsageBindsOnlySelectedField()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Reflection.slnx", new ProjectSpec("Host", [("State.cs", """
            public sealed class State
            {
                public int Value;
                public int Unrelated;
                public static object Read(State state) => typeof(State).GetField("Value")!.GetValue(state)!;
            }
            """)]));
        var index = await DeadCodeUsageIndex.CreateAsync(fixture.Solution, CancellationToken.None);
        new DeadCodeIndirectUsage(index).Collect(CancellationToken.None);
        var source = Assert.Single(index.Documents);
        var call = source.Root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .Single(call => call.ToString().EndsWith("GetField(\"Value\")", System.StringComparison.Ordinal));
        var method = (Microsoft.CodeAnalysis.IMethodSymbol)source.Model.GetSymbolInfo(call).Symbol!;
        new DeadCodeReflectionUsage(index).Collect(new(source, call, method));
        var type = source.Model.Compilation.GetTypeByMetadataName("State")!;
        Assert.True(index.Analyze(type.GetMembers("Value").Single()).Production);
        Assert.False(index.Analyze(type.GetMembers("Unrelated").Single()).Production);
    }
}
