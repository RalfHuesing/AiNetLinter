#nullable enable

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

public sealed partial class GetClassStructureToolTests
{
    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_UsesSameVisibleMembersInTextAndStructuredContent()
    {
        var methods = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"    public void Method{i}() {{ }}"));
        var source = $$"""
            namespace BudgetNs;
            public class BudgetClass
            {
            {{methods}}
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ClassBudget.slnx",
            new ProjectSpec("BudgetProject", [("BudgetClass.cs", source)])));
        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(),
            new GetClassStructureArgs("BudgetClass", "name", MaxResponseBytes: 900),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.True(payload!.Truncated);
        Assert.Contains("maxResponseBytes", payload.TruncatedBy!);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText()) <= 900);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.All(payload.Members, member => Assert.Contains(member.Name, text, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_ReturnsTheFirstFittingCandidate()
    {
        var methods = string.Join("\n", Enumerable.Range(1, 8).Select(i => $"    public void Method{i}() {{ }}"));
        var source = $$"""
            namespace BudgetNs;
            public class CandidateClass
            {
            {{methods}}
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ClassBudgetCandidate.slnx",
            new ProjectSpec("BudgetProject", [("CandidateClass.cs", source)])));

        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(),
            new GetClassStructureArgs("CandidateClass", "name", MaxMembers: 4, MaxResponseBytes: 1200),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.True(payload!.Truncated);
        Assert.Contains("maxResponseBytes", payload.TruncatedBy!);
        Assert.Equal(payload.Members.Count, payload.ShownMemberCount);
        Assert.True(payload.Members.Count < 4);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(result.StructuredContent!.Value.GetRawText()) <= 1200);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_DoesNotMarkCompleteResponseAsBudgetTruncated()
    {
        const string source = """
            namespace BudgetNs;
            public class CompleteClass
            {
                public void Alpha() { }
                public void Beta() { }
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ClassBudgetComplete.slnx",
            new ProjectSpec("BudgetProject", [("CompleteClass.cs", source)])));

        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(),
            new GetClassStructureArgs("CompleteClass", "name", MaxResponseBytes: 16_384),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.False(payload!.Truncated);
        Assert.DoesNotContain("maxResponseBytes", payload.TruncatedBy ?? Array.Empty<string>());
        Assert.Equal(payload.TotalMemberCount, payload.ShownMemberCount);
    }

    [Fact]
    public async Task ExecuteAsync_MaxMembersWithinResponseBudget_DoesNotAddBudgetTruncationReason()
    {
        var methods = string.Join("\n", Enumerable.Range(1, 8).Select(i => $"    public void Method{i}() {{ }}"));
        var source = $$"""
            namespace BudgetNs;
            public class MaxMembersClass
            {
            {{methods}}
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ClassBudgetMaxMembers.slnx",
            new ProjectSpec("BudgetProject", [("MaxMembersClass.cs", source)])));

        var result = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(),
            new GetClassStructureArgs("MaxMembersClass", "name", MaxMembers: 4, MaxResponseBytes: 16_384),
            CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var payload = result.StructuredContent!.Value.Deserialize<ClassStructurePayload>(McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.True(payload!.Truncated);
        Assert.Equal(["maxMembers"], payload.TruncatedBy);
        Assert.Equal(4, payload.ShownMemberCount);
    }

    [Fact]
    public void FinalWireTrim_ReconcilesClassStructureCountsInTextAndStructuredContent()
    {
        var members = Enumerable.Range(1, 20)
            .Select(i => new
            {
                kind = "Method",
                name = $"Method{i}",
                visibility = "public",
                startLine = i,
                endLine = i,
                lineCount = 1,
                signature = new string('x', 400),
                filePath = "CandidateClass.cs",
            })
            .ToArray();
        var result = McpToolResults.Text(
            "# Typ: BudgetNs.CandidateClass\n- Member Count: 20 von 20",
            new
            {
                classStructure = new
                {
                    typeName = "BudgetNs.CandidateClass",
                    kind = "class",
                    files = new[] { "CandidateClass.cs" },
                    totalLines = 22,
                    totalMemberCount = 20,
                    shownMemberCount = 20,
                    truncated = false,
                    members,
                    truncatedBy = Array.Empty<string>(),
                },
            });

        var projected = AssemblyAnalysisResponse.ApplyWireBudget(result, 4096, 0);
        var classStructure = projected.StructuredContent!.Value.GetProperty("classStructure");
        var returned = classStructure.GetProperty("members").GetArrayLength();
        var text = Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text;

        Assert.True(returned < members.Length);
        Assert.Equal(returned, classStructure.GetProperty("shownMemberCount").GetInt32());
        Assert.Contains($"- Member Count: {returned} von 20", text, StringComparison.Ordinal);
    }
}
