#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.TestContext;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TestContext;

public sealed partial class GetTestContextToolTests
{
    [Fact]
    public void StructuredStaticCandidateStatus_IsProjectedBySharedNavigation()
    {
        using var tempDir = TestTempDirectory.Create("test-context-navigation-");
        var solutionPath = tempDir.CreateFile("workspace.slnx", string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);
        var payload = new TestContextPayload(
            "CoreLib.Calculator", "NamedType", "src/CoreLib/Calculator.cs", 2, 2, [], [], false, true,
            Completeness: "truncated", ReturnedTestFiles: 0, ReturnedTestMethods: 0,
            TruncatedBy: ["maxResults"], NextStep: "Abschnitt testContext: maxResults erhöhen.");

        var result = McpToolResults.WithNavigation(McpToolResults.Text("statische Testkandidaten", payload), target);
        var navigation = result.StructuredContent!.Value.GetProperty("navigation");

        Assert.Equal("truncated", navigation.GetProperty("status").GetProperty("completeness").GetString());
        Assert.Equal("request_detail", navigation.GetProperty("next").GetProperty("kind").GetString());
        Assert.Contains("testContext", navigation.GetProperty("next").GetProperty("action").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NUnitAndMSTestCategories_DetectedCorrectly()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\CustomFrameworkSolution.slnx",
            new ProjectSpec("App.Domain", [("Order.cs", "namespace App.Domain; public class Order { public void Process() { } }")], VirtualProjectDirectory: "src/App.Domain"),
            new ProjectSpec("App.Domain.IntegrationTests", [("OrderIntegrationTests.cs", """
                namespace App.Domain.IntegrationTests;
                public class OrderIntegrationTests
                {
                    [NUnit.Framework.Category("Integration")]
                    [NUnit.Framework.Test]
                    public void Process_IntegrationTest() { var o = new App.Domain.Order(); o.Process(); }
                }
                """)], VirtualProjectDirectory: "tests/App.Domain.IntegrationTests"));

        var result = await GetTestContextTool.ExecuteAsync(
            CreateServer(solutionOwner.Solution), new TestContextOptions("Order"), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var structured = JsonSerializer.Deserialize<TestContextPayload>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default)!;
        Assert.False(structured.IsUntested);
        var file = Assert.Single(structured.TestFiles);
        Assert.Equal("Integration", file.Category);
        Assert.Equal("tests/App.Domain.IntegrationTests", file.ProjectDirectory);
        Assert.Contains(structured.RecommendedTestCommands, c => c.Contains("dotnet test tests/App.Domain.IntegrationTests --filter FullyQualifiedName~OrderIntegrationTests"));
    }
}
