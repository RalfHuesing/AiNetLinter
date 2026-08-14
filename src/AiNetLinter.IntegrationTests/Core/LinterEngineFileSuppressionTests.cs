#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using Xunit;

namespace AiNetLinter.IntegrationTests.Core;

[Trait("Category", "Integration")]
public sealed class LinterEngineFileSuppressionTests
{
    private static Config CreateDefaultConfig()
    {
        return new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = true,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 10,
                MaxMethodParameterCount = 2,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
                MaxInheritanceDepth = 2,
                MinCognitiveComplexityForTest = 3
            }
        };
    }

    private static async Task<Solution> CreateSolutionWithFileOnDiskAsync(string fileName, string content)
    {
        var tempDir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ainetlinter-engine-{Guid.NewGuid():N}")).FullName;
        var filePath = Path.Combine(tempDir, fileName);
        await File.WriteAllTextAsync(filePath, content);

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, fileName, content);
        return solution.WithDocumentFilePath(documentId, filePath);
    }

    [Fact]
    public async Task Run_WithDisableAllComment_SuppressesStaticTestSentinel()
    {
        const string sourceClass = """
            // ainetlinter-disable all
            namespace Domain;
            public sealed class ComplexDomainService
            {
                public void HighComplexityMethod(int x)
                {
                    if (x > 1)
                    {
                        if (x > 2)
                        {
                            if (x > 3) {}
                        }
                    }
                }
            }
            """;

        var solution = await CreateSolutionWithFileOnDiskAsync("ComplexDomainService.cs", sourceClass);
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "StaticTestSentinel"));
    }

    [Fact]
    public async Task Run_WithDisableAllComment_SuppressesMaxInheritanceDepth()
    {
        const string sourceCode = """
            // ainetlinter-disable all
            namespace Test;
            public class RootClass {}
            public class ParentClass : RootClass {}
            public sealed class ChildClass : ParentClass {}
            """;

        var solution = await CreateSolutionWithFileOnDiskAsync("Classes.cs", sourceCode);
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxInheritanceDepth = 1 }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
    }
}
