using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AiNetLinter.Tests;

public sealed class FileFilterEvaluatorTests
{
    private static Config CreateTestConfig(FileFiltersConfig filters)
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
                MaxLineCount = 100, // Increased to avoid line count errors during tests
                MaxMethodParameterCount = 2,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
                MaxInheritanceDepth = 2,
                MinCognitiveComplexityForTest = 3
            },
            FileFilters = filters
        };
    }

    private static Solution CreateAdhocSolution(params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var generatedCodeLib = MetadataReference.CreateFromFile(typeof(System.CodeDom.Compiler.GeneratedCodeAttribute).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib, generatedCodeLib })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    [Theory]
    [InlineData("MainWindow.designer.cs", true)]
    [InlineData("MyApp.GlobalUsings.g.cs", true)]
    [InlineData("MyService.cs", false)]
    [InlineData("mainwindow.Designer.CS", true)] // Case-insensitivity check
    public void FileFilterEvaluator_ExcludeFilePatterns_WorksCorrectly(string fileName, bool expectedExcluded)
    {
        var filters = new FileFiltersConfig
        {
            ExcludeFilePatterns = new[] { "*.designer.cs", "*.g.cs" },
            ExcludeDirectoryPatterns = Array.Empty<string>()
        };

        var result = FileFilterEvaluator.IsExcluded(fileName, filters);
        Assert.Equal(expectedExcluded, result);
    }

    [Theory]
    [InlineData("C:/src/MyApp/obj/Debug/net10.0/MyApp.g.cs", true)]
    [InlineData("C:/src/MyApp/bin/Release/MyApp.cs", true)]
    [InlineData("C:/src/MyApp/src/MyService.cs", false)]
    [InlineData("C:/src/MyApp/src/ObjectService.cs", false)] // "obj" segment should not match "ObjectService"
    [InlineData("C:/src/MyObjProject/Service.cs", false)] // "obj" segment should not match "MyObjProject"
    public void FileFilterEvaluator_ExcludeDirectoryPatterns_WorksCorrectly(string filePath, bool expectedExcluded)
    {
        var filters = new FileFiltersConfig
        {
            ExcludeFilePatterns = Array.Empty<string>(),
            ExcludeDirectoryPatterns = new[] { "obj/", "bin" }
        };

        var result = FileFilterEvaluator.IsExcluded(filePath, filters);
        Assert.Equal(expectedExcluded, result);
    }

    [Fact]
    public async Task LinterEngine_WithExcludedFile_SkipsAnalysisAndReturnsNoViolations()
    {
        // Diese Datei hat mehr als 10 Zeilen und wuerde normalerweise einen Verstoß melden, da MaxLineCount=5.
        const string sourceCode = @"
namespace Test;

public class MyClass
{
    public void Method1() {}
    public void Method2() {}
    public void Method3() {}
    public void Method4() {}
    public void Method5() {}
    public void Method6() {}
    public void Method7() {}
    public void Method8() {}
    public void Method9() {}
}";

        var solution = CreateAdhocSolution(("MainWindow.designer.cs", sourceCode));
        var config = CreateTestConfig(new FileFiltersConfig
        {
            ExcludeFilePatterns = new[] { "*.designer.cs" }
        }) with
        {
            Metrics = new MetricsConfig { MaxLineCount = 5 }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        // Da die Datei ausgeschlossen wurde, darf es keine Verstöße geben
        Assert.Empty(violations);
    }

    [Fact]
    public async Task LinterAnalyzer_WithSkipGeneratedCodeAttributeTrue_SkipsDecoratedClasses()
    {
        const string sourceCode = @"
using System.CodeDom.Compiler;

namespace Test;

[GeneratedCode(""MyGenerator"", ""1.0"")]
public class MyGeneratedClass
{
    // Unsealed class + magic values should be ignored here
    public void DoWork()
    {
        int x = 42; // Magic value
    }
}

public class NormalClass
{
    // Dies sollte einen Verstoß melden (nicht sealed)
}";

        var solution = CreateAdhocSolution(("Service.cs", sourceCode));
        var config = CreateTestConfig(new FileFiltersConfig
        {
            SkipGeneratedCodeAttribute = true
        });
        
        config = config with
        {
            Global = config.Global with
            {
                EnforceSealedClasses = true,
                EnforceXmlDocumentation = false
            },
            Metrics = config.Metrics with
            {
                MaxLineCount = 100
            }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        // Sollte nur für NormalClass meckern (EnforceSealedClasses), nicht aber für MyGeneratedClass.
        Assert.Single(violations);
        var v = Assert.Single(violations);
        Assert.Equal(nameof(GlobalConfig.EnforceSealedClasses), v.RuleName);
        Assert.Contains("NormalClass", v.Details);
    }

    [Fact]
    public async Task LinterAnalyzer_WithSkipGeneratedCodeAttributeFalse_DoesNotSkipDecoratedClasses()
    {
        const string sourceCode = @"
using System.CodeDom.Compiler;

namespace Test;

[GeneratedCode(""MyGenerator"", ""1.0"")]
public class MyGeneratedClass
{
    // Unsealed class
}";

        var solution = CreateAdhocSolution(("Service.cs", sourceCode));
        var config = CreateTestConfig(new FileFiltersConfig
        {
            SkipGeneratedCodeAttribute = false
        });
        
        config = config with
        {
            Global = config.Global with
            {
                EnforceSealedClasses = true,
                EnforceXmlDocumentation = false
            },
            Metrics = config.Metrics with
            {
                MaxLineCount = 100
            }
        };

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        // Sollte einen Verstoß melden, da SkipGeneratedCodeAttribute false ist
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceSealedClasses) && v.Details.Contains("MyGeneratedClass"));
    }
}
