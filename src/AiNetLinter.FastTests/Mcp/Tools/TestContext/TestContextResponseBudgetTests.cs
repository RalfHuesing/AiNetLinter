#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.Mcp.Wire;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TestContext;

[Trait("Category", "Component")]
public sealed class TestContextResponseBudgetTests
{
    [Fact]
    public void Apply_BudgetProjectionProtectsThreeConcreteCandidatesBeforeConventionEvidence()
    {
        var payload = CreatePayload();

        var result = TestContextResponseBudget.Apply(payload, 3_500);

        Assert.NotEqual(true, result.IsError);
        var projected = ReadPayload(result);
        Assert.True(projected.TestFiles.Count >= 3);
        Assert.All(projected.TestFiles.Take(3), file => Assert.Equal("directInvocation", file.EvidenceKind));
        Assert.Equal(projected.TestFiles.Count, projected.ReturnedTestFiles);
        Assert.Equal(projected.TestFiles.Sum(file => file.TestMethods.Count), projected.ReturnedTestMethods);
        Assert.Equal("truncated", projected.Completeness);
        Assert.Contains("responseBudget", projected.TruncatedBy!);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.All(projected.TestFiles, file => Assert.Contains(file.FilePath, text, System.StringComparison.Ordinal));
        Assert.True(McpResponseSize.From(result).TotalBytes <= 3_500);
    }

    [Fact]
    public void Apply_TooSmallForProtectedCandidatesReturnsRecoverableMinimumError()
    {
        var result = TestContextResponseBudget.Apply(CreatePayload(), 512);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("RESPONSE_BUDGET_TOO_SMALL", text, System.StringComparison.Ordinal);
        Assert.Contains("fieldPath: $.maxResponseBytes", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyFinal_ReprojectsTextAndStructuredContentAfterNavigation()
    {
        var payload = CreatePayload();
        var report = TestContextFormatter.FormatReport(payload);
        var result = new CallToolResult
        {
            Content = [new TextContentBlock { Text = report + "\nStatus: operation=ok, completeness=complete" }],
            StructuredContent = JsonSerializer.SerializeToElement(new JsonObject
            {
                ["targetSymbol"] = payload.TargetSymbol,
                ["targetKind"] = payload.TargetKind,
                ["targetFilePath"] = payload.TargetFilePath,
                ["totalMatchingTests"] = payload.TotalMatchingTests,
                ["totalTestFiles"] = payload.TotalTestFiles,
                ["testFiles"] = JsonSerializer.SerializeToNode(payload.TestFiles, McpJsonOptions.Default),
                ["recommendedTestCommands"] = new JsonArray(),
                ["isUntested"] = false,
                ["isTruncated"] = false,
                ["navigation"] = new JsonObject { ["status"] = new JsonObject { ["operation"] = "ok" } },
            }, McpJsonOptions.Default),
        };

        var projected = TestContextResponseBudget.ApplyFinal(result, 5_000);

        Assert.NotEqual(true, projected.IsError);
        Assert.True(projected.StructuredContent!.Value.TryGetProperty("navigation", out _));
        Assert.Contains("Status: operation=ok", Assert.IsType<TextContentBlock>(Assert.Single(projected.Content)).Text, System.StringComparison.Ordinal);
        Assert.True(McpResponseSize.From(projected).TotalBytes <= 5_000);
    }

    private static TestContextPayload CreatePayload()
    {
        var direct = Enumerable.Range(1, 3)
            .Select(index => new StaticTestCandidateFile(
                $"tests/ZDirect{index}Tests.cs", $"ZDirect{index}Tests", "Unit", "methodInvocation",
                [$"Run_{index}"], 1, "tests", "directInvocation", "high", 1, [$"ZDirect{index}Tests"]))
            .ToList();
        var convention = Enumerable.Range(1, 4)
            .Select(index => new StaticTestCandidateFile(
                $"tests/AConvention{index}Tests.cs", $"AConvention{index}Tests", "Unit", "namingConvention",
                [], 1, "tests", "typeNamingConvention", "low", 1, [$"AConvention{index}Tests"]));
        var files = convention.Concat(direct).ToList();
        return new TestContextPayload(
            "Demo.Target.Run", "Method", "src/Target.cs", 7, files.Count, files, [], false, false,
            ReturnedTestFiles: files.Count,
            ReturnedTestMethods: 3);
    }

    private static TestContextPayload ReadPayload(CallToolResult result) =>
        JsonSerializer.Deserialize<TestContextPayload>(result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;

}
