#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FileStructure;

[Trait("Category", "Component")]
public sealed class GetIndexScopeToolContractTests
{
    [Fact]
    public async Task ExecuteAsync_MixedFixture_ProjectsTextStructuredContentAndRouting()
    {
        using var scenario = CreateScenario("namespace Project; public sealed class Valid { }");
        foreach (var extension in new[] { ".css", ".js", ".razor", ".xaml", ".html", ".json", ".md" })
        {
            File.WriteAllText(Path.Combine(scenario.ProjectPath, $"asset{extension}"), "content");
        }
        using var state = CreateServer(scenario.Solution);

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains(".cs: 1 Datei (voll vom Symbolgraph abgedeckt)", text, StringComparison.Ordinal);
        Assert.Contains(".css: 1 Datei (nicht vom Symbolgraph abgedeckt)", text, StringComparison.Ordinal);
        Assert.Contains("routing=search_pattern(pattern, scopeType=all, includePatterns=**/*.razor)", text, StringComparison.Ordinal);
        Assert.Contains("Population:", text, StringComparison.Ordinal);

        var payload = PayloadOf(result);
        var entries = payload.GetProperty("breakdown").EnumerateArray().ToArray();
        var csEntry = Assert.Single(entries, entry => entry.GetProperty("extension").GetString() == ".cs");
        Assert.True(csEntry.GetProperty("symbolGraphCovered").GetBoolean());
        var cssEntry = Assert.Single(entries, entry => entry.GetProperty("extension").GetString() == ".css");
        Assert.False(cssEntry.GetProperty("symbolGraphCovered").GetBoolean());
        Assert.Equal("search_pattern", cssEntry.GetProperty("routingTool").GetString());
        Assert.Equal("all", cssEntry.GetProperty("scopeType").GetString());
        Assert.Equal(JsonValueKind.Array, cssEntry.GetProperty("includePatterns").ValueKind);
        Assert.Equal("find_symbol", payload.GetProperty("routing").GetProperty("cSharp").GetProperty("tool").GetString());
        Assert.Equal("search_pattern", payload.GetProperty("routing").GetProperty("nonCSharp").GetProperty("tool").GetString());
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedObjBinDirectories_AreExcludedFromBreakdown()
    {
        using var scenario = CreateScenario("namespace Project; public sealed class Valid { }");
        File.WriteAllText(Path.Combine(scenario.ProjectPath, "page.xaml"), "<Page />");
        var generated = Path.Combine(scenario.ProjectPath, "obj", "Debug");
        Directory.CreateDirectory(generated);
        File.WriteAllText(Path.Combine(generated, "Generated.xaml"), "<Page />");
        File.WriteAllText(Path.Combine(generated, "Generated.html"), "<html></html>");
        using var state = CreateServer(scenario.Solution);

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.Contains(".xaml: 1 Datei", TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain(".html:", TextOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_ReturnsBreakdownWithoutCompileErrorHint()
    {
        using var scenario = CreateScenario("namespace Project; public sealed class Broken {");
        using var state = CreateServer(scenario.Solution);

        var result = await GetIndexScopeTool.ExecuteAsync(state, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.DoesNotContain("Compile-Fehler", TextOf(result), StringComparison.Ordinal);
        Assert.NotEmpty(PayloadOf(result).GetProperty("breakdown").EnumerateArray());
    }

    private static IndexScopeScenario CreateScenario(string source)
    {
        var directory = TestTempDirectory.Create("index-scope-tool-");
        var projectPath = Path.Combine(directory.DirectoryPath, "src", "Project");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, "Project.cs"), source);
        File.WriteAllText(Path.Combine(directory.DirectoryPath, "Fixture.slnx"), string.Empty);
        var solution = RoslynTestSolutionFactory.CreateSolution(
            Path.Combine(directory.DirectoryPath, "Fixture.slnx"),
            new ProjectSpec("Project", [("Project.cs", source)], VirtualProjectDirectory: Path.Combine("src", "Project")));
        return new IndexScopeScenario(directory, solution, projectPath);
    }

    private static McpCodeGraphServer CreateServer(Microsoft.CodeAnalysis.Solution solution) => new(
        McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: solution)));

    private static JsonElement PayloadOf(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent!.Value;
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private sealed class IndexScopeScenario(
        TestTempDirectory directory,
        RoslynTestSolution solution,
        string projectPath) : IDisposable
    {
        internal string ProjectPath => projectPath;
        internal Microsoft.CodeAnalysis.Solution Solution => solution.Solution;

        public void Dispose()
        {
            solution.Dispose();
            directory.Dispose();
        }
    }
}
