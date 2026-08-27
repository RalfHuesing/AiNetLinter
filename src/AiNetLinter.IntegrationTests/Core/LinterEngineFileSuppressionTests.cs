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

    private static async Task<DiskBackedSolution> CreateSolutionWithFileOnDiskAsync(string fileName, string content)
    {
        var tempDir = TestTempDirectory.Create("ainetlinter-engine-");
        var workspace = new AdhocWorkspace();
        try
        {
            var filePath = tempDir.CreateFile(fileName, content);
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
            return new DiskBackedSolution(tempDir, workspace, solution.WithDocumentFilePath(documentId, filePath));
        }
        catch
        {
            workspace.Dispose();
            tempDir.Dispose();
            throw;
        }
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

        using var diskSolution = await CreateSolutionWithFileOnDiskAsync("ComplexDomainService.cs", sourceClass);
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnableTestSentinel = true }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(diskSolution.Solution);

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

        using var diskSolution = await CreateSolutionWithFileOnDiskAsync("Classes.cs", sourceCode);
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxInheritanceDepth = 1 }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(diskSolution.Solution);

        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
    }

    private sealed class DiskBackedSolution(
        TestTempDirectory tempDirectory,
        AdhocWorkspace workspace,
        Solution solution) : IDisposable
    {
        public Solution Solution { get; } = solution;

        public void Dispose()
        {
            try
            {
                workspace.Dispose();
            }
            finally
            {
                tempDirectory.Dispose();
            }
        }
    }
}
