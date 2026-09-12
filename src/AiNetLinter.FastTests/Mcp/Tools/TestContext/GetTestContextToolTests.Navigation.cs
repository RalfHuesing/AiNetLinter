#nullable enable

using System;
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
    public void StaticCandidateStatus_IsRenderedInContent()
    {
        using var tempDir = TestTempDirectory.Create("test-context-navigation-");
        var solutionPath = tempDir.CreateFile("workspace.slnx", string.Empty);
        var target = Assert.IsType<AnalysisTarget>(AnalysisTargetResolver.Resolve(
            new AnalysisTargetRequest(solutionPath)).Target);
        var payload = new TestContextPayload(
            "CoreLib.Calculator", "NamedType", "src/CoreLib/Calculator.cs", 2, 2, [], [], false, true,
            Completeness: "truncated", ReturnedTestFiles: 0, ReturnedTestMethods: 0,
            TruncatedBy: ["maxResults"], NextStep: "Abschnitt testContext: maxResults erhöhen.");

        var result = McpToolResults.WithNavigation(McpToolResults.Text(TestContextFormatter.FormatReport(payload)), target);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

        Assert.Contains("Status:** `truncated`", text, StringComparison.Ordinal);
        Assert.Contains("Abschnitt testContext: maxResults erhöhen.", text, StringComparison.Ordinal);
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
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("OrderIntegrationTests", text, StringComparison.Ordinal);
        Assert.Contains("(Integration,", text, StringComparison.Ordinal);
        Assert.Contains("tests/App.Domain.IntegrationTests", text, StringComparison.Ordinal);
    }
}
