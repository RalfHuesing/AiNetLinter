#nullable enable

using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Generators;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.IntegrationTests.Core;

[Trait("Category", "Integration")]
public sealed class PlaybookGeneratorRound2FileTests
{
    private static Solution BuildSolution(string source, string projectName = "TestProj", string docName = "Doc.cs")
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), projectName, projectName, LanguageNames.CSharp)
            .WithMetadataReferences(new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        var docId = DocumentId.CreateNewId(projectId);
        return solution.AddDocument(docId, docName, source);
    }

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
