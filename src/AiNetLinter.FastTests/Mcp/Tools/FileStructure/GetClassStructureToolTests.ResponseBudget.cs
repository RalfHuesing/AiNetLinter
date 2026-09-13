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
    public async Task ExecuteAsync_MaxResponseBytes_UsesVisibleMemberText()
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("BudgetClass", text, StringComparison.Ordinal);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= 900);
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("CandidateClass", text, StringComparison.Ordinal);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(text) <= 1200);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResponseBytes_ThatCannotFitOneMember_ReturnsRetryErrorInsteadOfEmptySuccess()
    {
        const string source = """
            namespace BudgetNs;
            public class MinimumMemberClass
            {
                public MinimumMemberClass(string parameterOneWithLongName, string parameterTwoWithLongName, string parameterThreeWithLongName, string parameterFourWithLongName, string parameterFiveWithLongName, string parameterSixWithLongName, string parameterSevenWithLongName, string parameterEightWithLongName, string parameterNineWithLongName, string parameterTenWithLongName, string parameterElevenWithLongName, string parameterTwelveWithLongName) { }
                public void VeryLongMemberNameForBudgetValidation(string parameterOneWithLongName, string parameterTwoWithLongName, string parameterThreeWithLongName, string parameterFourWithLongName, string parameterFiveWithLongName, string parameterSixWithLongName) { }
            }
            """;
        using var context = new McpInMemoryTestContext(RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ClassBudgetMinimumMember.slnx",
            new ProjectSpec("BudgetProject", [("MinimumMemberClass.cs", source)])));
        var response = await GetClassStructureTool.ExecuteAsync(
            context.CreateServer(),
            new GetClassStructureArgs("MinimumMemberClass", MaxResponseBytes: 512),
            CancellationToken.None);
        var visibleText = Assert.IsType<TextContentBlock>(Assert.Single(response.Content)).Text;

        Assert.Contains("Member Count: 1 von 2", visibleText, StringComparison.Ordinal);
        Assert.DoesNotContain("Keine Member gefunden.", visibleText, StringComparison.Ordinal);

        var budgeted = McpToolResults.WithNavigation(
            response,
            maxResponseBytes: 512,
            postNavigationResponseBudget: GetClassStructureResponseBudget.ApplyFinalResponseBudget);
        var errorText = Assert.IsType<TextContentBlock>(Assert.Single(budgeted.Content)).Text;
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", errorText, StringComparison.Ordinal);
        Assert.Contains("minimumResponseBytes", errorText, StringComparison.Ordinal);
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Alpha", text, StringComparison.Ordinal);
        Assert.Contains("Beta", text, StringComparison.Ordinal);
        Assert.DoesNotContain("maxResponseBytes", text, StringComparison.Ordinal);
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("maxMembers", text, StringComparison.Ordinal);
        Assert.Contains("Method", text, StringComparison.Ordinal);
    }

}
