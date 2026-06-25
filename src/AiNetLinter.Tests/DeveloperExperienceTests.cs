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
using AiNetLinter.Generators;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.Tests;

// @covers RepoPlaybookGenerator
// @covers PlaybookSyntaxWalker
// @covers AIContextFootprintCalculator
// @covers ProjectConfigResolver
// @covers ConfigLoader
// @covers ImpactExecutor
// @covers PostAnalysisChecks
// @covers TestProjectDetector
// @covers CursorRulesGenerator

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
        var globalConfig = new Config
        {
            Global = new GlobalConfig { EnforceSemanticNaming = true },
            Metrics = new MetricsConfig { MaxLineCount = 100 }
        };

        var resolved = ProjectConfigResolver.ResolveForProject("SomeProj", globalConfig);

        Assert.True(resolved.Global.EnforceSemanticNaming);
        Assert.Equal(100, resolved.Metrics.MaxLineCount);
    }

    [Fact]
    public void ProjectConfigResolver_WithWildcardMatch_MergesOverrides()
    {
        var globalConfig = new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = true,
                EnforceExplicitStateImmutability = true,
                AllowedExceptions = new[] { "Exception" },                PreventContextDependentOverloads = true,                EnforceNamespaceDirectoryMapping = true,
                DetectAndBanPhantomDependencies = true,
                ImmutabilityExemptSuffixes = new[] { "Dto" },
                SealedClassExemptSuffixes = new[] { "Base" }
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 100,
                MaxMethodLineCount = 10,
                MaxDirectoryDepth = 4
            },
            ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["*.Tests"] = new()
                {
                    Global = new GlobalConfigOverride
                    {                        EnforceExplicitStateImmutability = false,
                        AllowedExceptions = new[] { "CustomException" },                        PreventContextDependentOverloads = false,                        EnforceNamespaceDirectoryMapping = false,
                        DetectAndBanPhantomDependencies = false,
                        ImmutabilityExemptSuffixes = new[] { "TestDto" },
                        SealedClassExemptSuffixes = new[] { "Exempt" }
                    },
                    Metrics = new MetricsConfigOverride
                    {
                        MaxMethodLineCount = 50,
                        MaxDirectoryDepth = 8
                    }
                }
            }
        };

        var resolved = ProjectConfigResolver.ResolveForProject("MyLibrary.Tests", globalConfig);

        Assert.True(resolved.Global.EnforceSealedClasses);   // Kept from global
        Assert.Equal(100, resolved.Metrics.MaxLineCount);   // Kept from global
        Assert.Equal(50, resolved.Metrics.MaxMethodLineCount); // Overridden

        // Verify Epic 20 rules
        Assert.False(resolved.Global.EnforceExplicitStateImmutability);
        Assert.Contains("CustomException", resolved.Global.AllowedExceptions);
        Assert.False(resolved.Global.PreventContextDependentOverloads);
        Assert.False(resolved.Global.EnforceNamespaceDirectoryMapping);
        Assert.False(resolved.Global.DetectAndBanPhantomDependencies);
        Assert.Contains("TestDto", resolved.Global.ImmutabilityExemptSuffixes);
        Assert.Contains("Exempt", resolved.Global.SealedClassExemptSuffixes);
        
        // Verify Metrics
        Assert.Equal(8, resolved.Metrics.MaxDirectoryDepth);
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
            // ainetlinter-disable EnforceSemanticNaming
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
            await RepoPlaybookGenerator.GenerateAsync(solution, tempPath);

            Assert.True(File.Exists(tempPath));
            var content = File.ReadAllText(tempPath);
            Assert.Contains("AI Repository Playbook", content);
            Assert.Contains("EnforceSemanticNaming:** 1 mal deaktiviert.", content);
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

    [Fact]
    public async Task RepoPlaybookGenerator_WithAllowedException_FiltersThrowFromMetric()
    {
        const string source = """
            namespace TestNamespace;
            public class WorkClass
            {
                public string GetResult()
                {
                    throw new System.ArgumentNullException();
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

        var config = new Config
        {
            Global = new GlobalConfig
            {
                AllowedExceptions = new[] { "ArgumentNullException" }
            },
            Metrics = new MetricsConfig()
        };

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_playbook.md");
        try
        {
            await RepoPlaybookGenerator.GenerateAsync(solution, tempPath, new PlaybookOptions(Config: config));

            Assert.True(File.Exists(tempPath));
            var content = File.ReadAllText(tempPath);
            // Since ArgumentNullException is allowed, the throws count should be 0.
            Assert.Contains("Kontrollfluss-Exceptions:** 0", content);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void ConfigLoader_WithNonExistentFile_ReturnsNull()
    {
        var result = ConfigLoader.TryLoadConfig("nonexistent_file.json", isRequired: false);
        Assert.Null(result);
    }

    [Fact]
    public void ConfigLoader_WithValidJson_LoadsConfig()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_config.json");
        var json = """
            {
                "global": {
                    "enforceSealedClasses": true,
                    "sealedClassExemptSuffixes": ["Base"]
                },
                "metrics": {
                    "maxLineCount": 500
                }
            }
            """;
        File.WriteAllText(tempPath, json);
        try
        {
            var result = ConfigLoader.TryLoadConfig(tempPath, isRequired: false);
            Assert.NotNull(result);
            Assert.True(result.Global.EnforceSealedClasses);
            Assert.Contains("Base", result.Global.SealedClassExemptSuffixes);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void SyncCursorRules_GeneratesMdcFile_WritesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var config = new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = true,
                EnforceNoSilentCatch = true,
                EnforceResultPatternOverExceptions = true
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 500,
                MaxMethodLineCount = 42
            },
            ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
            {
                ["*.Tests"] = new()
                {
                    Global = new GlobalConfigOverride
                    {
                        EnforceExplicitStateImmutability = false
                    }
                }
            }
        };

        try
        {
            CursorRulesGenerator.Sync(tempDir, config, verbose: false);

            var mdcPath = Path.Combine(tempDir, ".cursor", "rules", "AiNetLinter.mdc");
            Assert.True(File.Exists(mdcPath));

            var content = File.ReadAllText(mdcPath);
            Assert.Contains("description: C#-Codequalität", content);
            Assert.Contains("MaxLineCount", content);
            Assert.Contains("EnforceSealedClasses", content);
            Assert.Contains("*.Tests", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SyncCursorRules_OnSelfRepository_UpdatesMdc()
    {
        var root = FindProjectRoot();
        var configPath = Path.Combine(root, "rules.json");
        var config = ConfigLoader.TryLoadConfig(configPath, isRequired: true);
        Assert.NotNull(config);

        CursorRulesGenerator.Sync(root, config, verbose: true);

        var mdcPath = Path.Combine(root, ".cursor", "rules", "AiNetLinter.mdc");
        Assert.True(File.Exists(mdcPath));
    }

    [Fact]
    public void GuidanceD_CursorRulesContainsCompoundSuppressionsTable()
    {
        var config = new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig
            {
                CompoundSuppressions = new List<CompoundSuppression>
                {
                    new()
                    {
                        TargetRule = "MaxMethodLineCount",
                        WhenAllOf = new List<MetricCondition>
                        {
                            new() { Metric = "CyclomaticComplexity", AtMost = 3 }
                        },
                        RelaxedLimit = 150,
                        Reason = "Test reason here"
                    }
                }
            }
        };

        var content = CursorRulesGenerator.GenerateContent(config, "rules.json");

        Assert.Contains("## Compound Suppressions (kontextabhängige Limiten)", content);
        Assert.Contains("| `MaxMethodLineCount` | CyclomaticComplexity ≤ 3 | **150** | — | Test reason here |", content);
    }

    [Fact]
    public void GuidanceE_CursorRulesContainsNoCompoundSuppressionsTable()
    {
        var config = new Config
        {
            Global = new GlobalConfig(),
            Metrics = new MetricsConfig
            {
                CompoundSuppressions = new List<CompoundSuppression>()
            }
        };

        var content = CursorRulesGenerator.GenerateContent(config, "rules.json");

        Assert.DoesNotContain("## Compound Suppressions (kontextabhängige Limiten)", content);
    }

    private static string FindProjectRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir != null)
        {
            if (dir.GetFiles("rules.json").Any())
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Project root not found.");
    }
}
