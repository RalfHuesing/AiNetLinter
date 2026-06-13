#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.Tests;

// @covers RepoPlaybookGenerator
// @covers PlaybookSyntaxWalker
// @covers AIContextFootprintCalculator
// @covers ProjectConfigResolver

/// <summary>
/// Tests für die neuen Developer-Experience-Features (Project Overrides, AI-Context-Footprint, Repo-Playbook).
/// </summary>
public sealed class DeveloperExperienceTests
{
    private static (SyntaxTree, SemanticModel) GetSemanticContext(string source, string assemblyName = "TestAssembly")
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var refs = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(assemblyName)
            .AddSyntaxTrees(tree)
            .AddReferences(refs)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    [Fact]
    public void ProjectConfigResolver_NoOverrides_ReturnsGlobalConfig()
    {
        var globalConfig = new LinterConfig
        {
            Global = new GlobalConfig { EnforceNoMagicValues = true },
            Metrics = new MetricsConfig { MaxLineCount = 100 }
        };

        var resolved = ProjectConfigResolver.ResolveForProject("SomeProj", globalConfig);

        Assert.True(resolved.Global.EnforceNoMagicValues);
        Assert.Equal(100, resolved.Metrics.MaxLineCount);
    }

    [Fact]
    public void ProjectConfigResolver_WithWildcardMatch_MergesOverrides()
    {
        var globalConfig = new LinterConfig
        {
            Global = new GlobalConfig { EnforceNoMagicValues = true, EnforceSealedClasses = true },
            Metrics = new MetricsConfig { MaxLineCount = 100, MaxMethodLineCount = 10 },
            ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["*.Tests"] = new()
                {
                    Global = new GlobalConfigOverride { EnforceNoMagicValues = false },
                    Metrics = new MetricsConfigOverride { MaxMethodLineCount = 50 }
                }
            }
        };

        var resolved = ProjectConfigResolver.ResolveForProject("MyLibrary.Tests", globalConfig);

        Assert.False(resolved.Global.EnforceNoMagicValues); // Overridden
        Assert.True(resolved.Global.EnforceSealedClasses);   // Kept from global
        Assert.Equal(100, resolved.Metrics.MaxLineCount);   // Kept from global
        Assert.Equal(50, resolved.Metrics.MaxMethodLineCount); // Overridden
    }

    [Fact]
    public void AIContextFootprintCalculator_SimpleClass_CalculatesLines()
    {
        const string source = """
            namespace TestNamespace;
            public class DependencyA
            {
                public int Value { get; set; }
            }

            public class TargetClass
            {
                private DependencyA _dep = new DependencyA();
            }
            """;

        var (tree, model) = GetSemanticContext(source);
        var classNodes = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().ToList();
        var targetNode = classNodes.First(c => c.Identifier.Text == "TargetClass");
        var targetSymbol = model.GetDeclaredSymbol(targetNode) as INamedTypeSymbol;

        Assert.NotNull(targetSymbol);
        var linesCount = AIContextFootprintCalculator.Calculate(targetSymbol);

        // Der Code-String hat 10 Zeilen. Da beide Klassen in derselben Datei liegen und DependencyA referenziert wird,
        // wird TargetClass und DependencyA geladen, die in derselben Datei liegen. Die Datei hat 10 Zeilen.
        Assert.Equal(10, linesCount);
    }

    [Fact]
    public async Task RepoPlaybookGenerator_ScansAndGeneratesMarkdown()
    {
        const string source = """
            // ainetlinter-disable EnforceNoMagicValues
            namespace TestNamespace;
            public class WorkClass
            {
                public string GetResult()
                {
                    throw new System.Exception();
                }
            }
            """;

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), "PlaybookProj", "PlaybookProj", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        var docId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(docId, "WorkClass.cs", source);

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");
        try
        {
            await RepoPlaybookGenerator.GenerateAsync(solution, tempPath, verbose: false);

            Assert.True(File.Exists(tempPath));
            var content = File.ReadAllText(tempPath);
            Assert.Contains("AI Repository Playbook", content);
            Assert.Contains("EnforceNoMagicValues:** 1 mal deaktiviert.", content);
            Assert.Contains("Kontrollfluss-Exceptions:** 1", content);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
