#nullable enable

using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Generators;
using AiNetLinter.Models;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Core;

[Trait("Category", "Integration")]
public sealed class PlaybookGeneratorRound2FileTests
{
    private static Solution BuildSolution(string source, string projectName = "TestProj", string docName = "Doc.cs")
        => RoslynTestSolutionFactory.CreateSolution(source, projectName, docName).Solution;

    [Fact]
    public async Task BuildContentAsync_ProducesIdenticalContentToGenerateAsync()
    {
        const string source = """
            namespace TestNamespace;
            public class SomeService { }
            """;

        var solution = BuildSolution(source, "UpToDateProj");
        var tempPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + "_playbook.md");
        try
        {
            await RepoPlaybookGenerator.GenerateAsync(solution, tempPath);
            var generatedContent = await RepoPlaybookGenerator.BuildContentAsync(solution);
            var writtenContent = await File.ReadAllTextAsync(tempPath);
            Assert.Equal(generatedContent, writtenContent);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task GenerateAsync_ArchitectureSliceHeading_UsesOrdner()
    {
        const string source = """
            namespace TestNamespace;
            public class MyClass { }
            """;

        var solution = BuildSolution(source, "SliceProj");
        var config = new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig()
        };
        var tempPath = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString() + "_playbook.md");
        try
        {
            await RepoPlaybookGenerator.GenerateAsync(solution, tempPath, new PlaybookOptions(Config: config));
            var content = File.ReadAllText(tempPath);
            Assert.Contains("Architektur-Slices (nach Ordner)", content);
            Assert.DoesNotContain("Architektur-Slices (aus Namespace)", content);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
