using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Threading.Tasks;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class MaxInheritanceDepthTests
{
    private static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false, // Disable other rules to isolate MaxInheritanceDepth
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxInheritanceDepth = 2
            }
        };
    }

    private static (SyntaxTree, SemanticModel) GetSemanticContext(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Any())
        {
            throw new System.Exception("Compilation errors: " + string.Join("\n", errors.Select(e => e.ToString())));
        }

        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    private static Solution CreateAdhocSolution(params (string fileName, string content)[] files)
    {
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
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    [Fact]
    public async Task Analyze_WithInheritanceWithinLimit_ReturnsNoViolations()
    {
        const string sourceCode = @"
namespace Test;
public class A {}
public class B : A {}
public sealed class C : B {} // Depth = 2 (A, B) -> exactly at the limit of 2
";
        var config = CreateDefaultConfig();
        var solution = CreateAdhocSolution(("MyClasses.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
    }

    [Fact]
    public async Task Analyze_WithInheritanceExceedingLimit_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
public class A {}
public class B : A {}
public class C : B {}
public sealed class D : C {} // Depth = 3 (A, B, C) -> exceeds limit of 2
";
        var config = CreateDefaultConfig();
        var solution = CreateAdhocSolution(("MyClasses.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth));
    }

    [Fact]
    public async Task Analyze_WithWpfFalsePositiveExclusion_ReturnsNoViolations()
    {
        const string sourceCode = @"
namespace System.Windows
{
    public class DependencyObject {}
    public class Visual : DependencyObject {}
    public class UIElement : Visual {}
    public class FrameworkElement : UIElement {}
    public class Control : FrameworkElement {}
    public class UserControl : Control {}
}

namespace MyProject
{
    using System.Windows;
    public sealed class MyControl : UserControl {} // Depth would be 6, but System.Windows. is ignored, so depth = 0
}";
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxInheritanceDepth = 2,
                InheritanceDepthFrameworkPrefixes = new[] { "System.Windows." }
            }
        };
        var solution = CreateAdhocSolution(("MyClasses.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
    }

    [Fact]
    public async Task Analyze_WithMixedHierarchy_ReturnsCorrectDepth()
    {
        const string sourceCode = @"
namespace System.Windows
{
    public class UserControl {}
}

namespace MyProject
{
    using System.Windows;
    public class MyBaseControl : UserControl {} // UserControl is System.Windows. -> ignored. Depth = 0.
    public class MyIntermediateControl : MyBaseControl {} // MyBaseControl counts. Depth = 1.
    public class MyControl : MyIntermediateControl {} // MyBaseControl, MyIntermediateControl count. Depth = 2.
    public sealed class MyDeepControl : MyControl {} // MyBaseControl, MyIntermediateControl, MyControl count. Depth = 3 -> exceeds limit of 2
}";
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxInheritanceDepth = 2,
                InheritanceDepthFrameworkPrefixes = new[] { "System.Windows." }
            }
        };
        var solution = CreateAdhocSolution(("MyClasses.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        // MyDeepControl should have a violation, MyControl and others should not
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth) && v.Details.Contains("MyDeepControl"));
        Assert.DoesNotContain(violations, v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth) && v.Details.Contains("MyControl"));
    }

    [Fact]
    public async Task Analyze_WithCaseInsensitivePrefixMatch_ExcludesCorrectly()
    {
        const string sourceCode = @"
namespace System.Windows
{
    public class UserControl {}
}

namespace MyProject
{
    using System.Windows;
    public sealed class MyControl : UserControl {} // Depth would be 1 (UserControl), but matches case-insensitively, so depth = 0
}";
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxInheritanceDepth = 0, // Limit is 0, so any non-ignored class causes violation
                InheritanceDepthFrameworkPrefixes = new[] { "system.windows." } // Lowercase prefix
            }
        };
        var solution = CreateAdhocSolution(("MyClasses.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth)));
    }

    [Fact]
    public async Task Analyze_WithEmptyExclusionList_DoesNotExcludeFrameworkClasses()
    {
        const string sourceCode = @"
namespace System.Windows
{
    public class UserControl {}
}

namespace MyProject
{
    using System.Windows;
    public sealed class MyControl : UserControl {} // Depth = 1 (UserControl), exceeds limit 0
}";
        var config = CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig
            {
                MaxInheritanceDepth = 0,
                InheritanceDepthFrameworkPrefixes = System.Array.Empty<string>()
            }
        };
        var solution = CreateAdhocSolution(("MyClasses.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxInheritanceDepth));
    }
}
