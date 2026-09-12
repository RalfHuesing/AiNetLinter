#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.Analysis;

[Trait("Category", "Component")]
public sealed class SearchPatternToolContractTests
{
    [Fact]
    public async Task ExecuteAsync_RendersMatchesAndFollowUpFactsInContent()
    {
        using var scenario = CreateScenario("Greeter");
        using var state = CreateServer(scenario.Solution);

        var result = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Greeter", false, 50, 0, 0, 0, null, null, null),
            CancellationToken.None);

        var text = TextOf(result);
        Assert.Contains("Greeter.cs", text, StringComparison.Ordinal);
        Assert.Contains("[NEXT: none]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("semantic", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_NoMatchAndDefaultCall_RetainContentEmptySemantics()
    {
        using var scenario = CreateScenario("Greeter");
        using var state = CreateServer(scenario.Solution);

        var result = await SearchPatternTool.ExecuteAsync(
            state, "does-not-exist", false, 50, CancellationToken.None);

        Assert.Contains("0 Treffer", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidArguments_ReturnPreciseRecoverablePayload()
    {
        using var scenario = CreateScenario("Greeter");
        using var state = CreateServer(scenario.Solution);
        var cases = new[]
        {
            new SearchPatternToolArguments("", false, 50, 0, 0, 0, null, null, null),
            new SearchPatternToolArguments("Greeter", false, 50, -1, 0, 0, null, null, null),
            new SearchPatternToolArguments("Greeter", false, 2_001, 0, 0, 0, null, null, null),
            new SearchPatternToolArguments("Greeter", false, 50, 0, 0, 65_537, null, null, null),
            new SearchPatternToolArguments("Greeter", false, 50, 0, 0, 0, null, null, null, false, "staging"),
        };

        foreach (var arguments in cases)
        {
            var result = await SearchPatternTool.ExecuteAsync(state, arguments, CancellationToken.None);
            Assert.NotEqual(true, result.IsError);
            Assert.Contains("INVALID_ARGUMENT", TextOf(result), StringComparison.Ordinal);
        }

        var capped = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("Greeter", false, 2_001, 0, 0, 0, null, null, null),
            CancellationToken.None);
        Assert.Contains("fieldPath: $.maxResults", TextOf(capped), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_RegexAndResponseLimits_PreserveCompletenessReasons()
    {
        using var scenario = CreateScenario("anchor\nanchor\nanchor");
        File.WriteAllText(Path.Combine(scenario.RootPath, "src", "Project", "second.txt"), "anchor");
        using var state = CreateServer(scenario.Solution);

        var regex = await SearchPatternTool.ExecuteAsync(state, "(unclosed", true, 50, CancellationToken.None);
        Assert.Contains("INVALID_ARGUMENT", TextOf(regex), StringComparison.Ordinal);
        Assert.Contains("Pruefe pattern auf gueltige Regex-Syntax", TextOf(regex), StringComparison.Ordinal);

        var limited = await SearchPatternTool.ExecuteAsync(
            state,
            new SearchPatternToolArguments("anchor", false, 1, 1, 1, 200, null, null, null),
            CancellationToken.None);
        var text = TextOf(limited);
        Assert.Contains("maxFiles", text, StringComparison.Ordinal);
        Assert.Contains("maxResults", text, StringComparison.Ordinal);
    }

    private static SearchPatternScenario CreateScenario(string content)
    {
        var directory = TestTempDirectory.Create("search-pattern-tool-");
        Directory.CreateDirectory(Path.Combine(directory.DirectoryPath, "src", "Project"));
        File.WriteAllText(Path.Combine(directory.DirectoryPath, "src", "Project", "Greeter.cs"), content);
        var solution = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(directory.DirectoryPath, "Fixture.slnx"),
            new ProjectSpec("Project", [("Greeter.cs", content)], VirtualProjectDirectory: Path.Combine("src", "Project")));
        return new SearchPatternScenario(directory, solution);
    }

    private static McpCodeGraphServer CreateServer(Microsoft.CodeAnalysis.Solution solution) => new(
        McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: solution)));

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private sealed class SearchPatternScenario(TestTempDirectory directory, RoslynTestSolution solution) : IDisposable
    {
        internal string RootPath => directory.DirectoryPath;
        internal Microsoft.CodeAnalysis.Solution Solution => solution.Solution;

        public void Dispose()
        {
            solution.Dispose();
            directory.Dispose();
        }
    }
}
