#nullable enable

using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Generators;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.Tests.Core;

// @covers RepoPlaybookGenerator
// @covers PlaybookSyntaxWalker

/// <summary>
/// Tests für die in Round 2 eingeführten Playbook-Features:
/// BuildContentAsync, --playbook --check, Ordner-Slices, projektinternes Result.
/// </summary>
public sealed class PlaybookGeneratorRound2Tests
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
    public async Task BuildContentAsync_ReturnsPlaybookString()
    {
        const string source = """
            namespace TestNamespace;
            public class SomeService
            {
                public void DoWork() { }
            }
            """;

        var solution = BuildSolution(source);
        var content = await RepoPlaybookGenerator.BuildContentAsync(solution);

        Assert.Contains("AI Repository Playbook (Auto-Generated)", content);
        Assert.Contains("Result-Pattern-Nutzung:", content);
        Assert.Contains("Kontrollfluss-Exceptions:", content);
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
    public async Task BuildContentAsync_DiffersFromStaleFile()
    {
        const string source = """
            namespace TestNamespace;
            public class SomeService { }
            """;

        var solution = BuildSolution(source, "DriftProj");
        var generatedContent = await RepoPlaybookGenerator.BuildContentAsync(solution);

        Assert.NotEqual("outdated content", generatedContent);
    }

    [Fact]
    public async Task GenerateAsync_ArchitectureSliceHeading_UsesOrdner()
    {
        const string source = """
            namespace TestNamespace;
            public class MyClass { }
            """;

        var solution = BuildSolution(source, "SliceProj");
        var config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };
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

    [Fact]
    public async Task BuildContentAsync_ProjectInternalResultSuffix_CountsAsResultPattern()
    {
        const string source = """
            namespace MyApp;
            public class OperationResult { }
            public sealed class MyService
            {
                public OperationResult Execute() => new OperationResult();
                public OperationResult Validate() => new OperationResult();
            }
            """;

        var solution = BuildSolution(source, "MyApp", "MyService.cs");
        var content = await RepoPlaybookGenerator.BuildContentAsync(solution);

        Assert.Contains("Result-Pattern-Nutzung:** 2", content);
    }

    [Fact]
    public async Task BuildContentAsync_ExternalResultType_NotCountedAsProjectInternal()
    {
        const string source = """
            namespace MyApp;
            public sealed class MyService
            {
                public void DoWork() { }
            }
            """;

        var solution = BuildSolution(source, "ExternalProj", "MyService.cs");
        var content = await RepoPlaybookGenerator.BuildContentAsync(solution);

        Assert.Contains("Result-Pattern-Nutzung:** 0", content);
    }

    [Fact]
    public async Task BuildContentAsync_WithPrecomputedViolations_UsesThemDirectly()
    {
        const string source = """
            namespace MyApp;
            public class MyService
            {
                public void DoWork() { }
            }
            """;

        var solution = BuildSolution(source, "MyApp", "MyService.cs");
        var config = new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };
        
        var violations = new[]
        {
            new RuleViolation
            {
                FilePath = "MyService.cs",
                LineNumber = 10,
                RuleName = "EnforceSealedClasses",
                Details = "Class is not sealed",
                Guidance = "Make it sealed"
            }
        };

        var content = await RepoPlaybookGenerator.BuildContentAsync(
            solution,
            new PlaybookOptions(Config: config, ConfigPath: "rules.json", PrecomputedViolations: violations));

        // Verify that the playbook contains the precomputed violation in its Migrations-Status
        Assert.Contains("EnforceSealedClasses", content);
        Assert.Contains("Verstösse nur wave-ready (default rules):** 1", content);
    }

    [Fact]
    public async Task BuildContentAsync_SortsIntentsAndRulesDeterministically()
    {
        const string source = "namespace MyApp; public class MyService { }";
        var solution = BuildSolution(source, "MyApp", "MyService.cs");
        var config = new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig(),
            RuleMetadata = new Dictionary<string, RuleMetadataEntry>
            {
                { "MaxConstructorDependencies", new RuleMetadataEntry { Severity = "warning", Intent = "coupling" } }
            }
        };

        var violations = new[]
        {
            new RuleViolation { FilePath = "MyService.cs", LineNumber = 1, RuleName = "MaxSwitchArms", Details = "Details", Guidance = "Guidance" }, // Intent: general, Count: 1
            new RuleViolation { FilePath = "MyService.cs", LineNumber = 2, RuleName = "MaxConstructorDependencies", Details = "Details", Guidance = "Guidance" }, // Intent: coupling, Count: 1
            new RuleViolation { FilePath = "MyService.cs", LineNumber = 3, RuleName = "MaxMethodLineCount", Details = "Details", Guidance = "Guidance" }, // Intent: agent-context, Count: 1
            new RuleViolation { FilePath = "MyService.cs", LineNumber = 4, RuleName = "MaxLineCount", Details = "Details", Guidance = "Guidance" } // Intent: agent-context, Count: 1 (Total agent-context = 2)
        };

        var content = await RepoPlaybookGenerator.BuildContentAsync(
            solution,
            new PlaybookOptions(Config: config, ConfigPath: "rules.json", PrecomputedViolations: violations));

        // The intent groups have counts: agent-context (2), coupling (1), general (1).
        // Since agent-context has 2, it is first.
        // coupling and general both have 1, so coupling (alphabetically first) comes before general.
        // Inside agent-context, the rules MaxLineCount and MaxMethodLineCount must be sorted alphabetically: "MaxLineCount, MaxMethodLineCount".
        
        Assert.Contains("| agent-context | 2 | MaxLineCount, MaxMethodLineCount |", content);
        Assert.Contains("| coupling | 1 | MaxConstructorDependencies |", content);
        Assert.Contains("| general | 1 | MaxSwitchArms |", content);

        // Verify the exact sequence of the lines in the table
        var idxAgent = content.IndexOf("| agent-context | 2 |");
        var idxCoupling = content.IndexOf("| coupling | 1 |");
        var idxGeneral = content.IndexOf("| general | 1 |");

        Assert.True(idxAgent < idxCoupling, "agent-context should come before coupling");
        Assert.True(idxCoupling < idxGeneral, "coupling should come before general");
    }
}
