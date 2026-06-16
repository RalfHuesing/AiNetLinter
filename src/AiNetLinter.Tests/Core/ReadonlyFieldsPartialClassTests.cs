using System.Collections.Concurrent;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class ReadonlyFieldsPartialClassTests
{
    private static LinterConfig CreateConfig() => new()
    {
        Global = new GlobalConfig
        {
            EnforceSealedClasses = false,
            AllowDynamic = false,
            AllowOutParameters = true,
            EnforceValueObjectContracts = false,
            EnforcePascalCase = false,
            EnforceXmlDocumentation = false,
            EnforceSemanticNaming = false,
            EnforceNullableEnable = false,
            EnforceNoSilentCatch = false,
            EnforceNoVariableShadowing = false,
            EnforceReadonlyParameters = false,
            EnforceReadonlyFields = true,
            EnforceNoMagicValues = false,
            EnforceExplicitStateImmutability = false,
            EnforceStrictBoundaryForBusinessLogic = false,
            PreventContextDependentOverloads = false,
            RequireExplicitTruncationHandling = false,
            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false
        },
        Metrics = new MetricsConfig
        {
            MaxLineCount = 500,
            MaxMethodParameterCount = 10,
            MaxCyclomaticComplexity = 20,
            MaxCognitiveComplexity = 20
        }
    };

    private static System.Collections.Generic.List<AiNetLinter.Models.RuleViolation> CollectPartialViolations(
        ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker> sharedTrackers)
    {
        var list = new System.Collections.Generic.List<AiNetLinter.Models.RuleViolation>();
        foreach (var (_, tracker) in sharedTrackers)
        {
            foreach (var field in tracker.GetReadonlyCandidates())
            {
                var filePath = field.DeclaringSyntaxReferences.Length > 0
                    ? field.DeclaringSyntaxReferences[0].SyntaxTree.FilePath : "";
                list.Add(FieldReadonlyTracker.BuildViolation(field, filePath));
            }
        }
        return list;
    }

    private static (SemanticModel ModelA, SemanticModel ModelB) CreatePartialClassCompilation(string sourceA, string sourceB)
    {
        var treeA = CSharpSyntaxTree.ParseText(sourceA, path: "FileA.cs");
        var treeB = CSharpSyntaxTree.ParseText(sourceB, path: "FileB.cs");
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(treeA, treeB)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));

        return (compilation.GetSemanticModel(treeA), compilation.GetSemanticModel(treeB));
    }

    private static SemanticModel CreateSingleFileCompilation(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "File.cs");
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void PartialClass_FieldDeclaredInFileA_WrittenInFileB_NoViolation()
    {
        const string sourceA = @"
public partial class SplitLayout
{
    private string? _activeSplitId;
    private bool _isDragging;
}";
        const string sourceB = @"
public partial class SplitLayout
{
    public void StartDrag(string splitId)
    {
        _activeSplitId = splitId;
        _isDragging = true;
    }
}";
        var config = CreateConfig();
        var sharedTrackers = new ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>(SymbolEqualityComparer.Default);
        var (modelA, modelB) = CreatePartialClassCompilation(sourceA, sourceB);

        var analyzerA = new LinterAnalyzer("FileA.cs", modelA, config, isTestFile: false);
        analyzerA.UseSharedFieldTrackers(sharedTrackers);
        analyzerA.RunAnalysis();

        var analyzerB = new LinterAnalyzer("FileB.cs", modelB, config, isTestFile: false);
        analyzerB.UseSharedFieldTrackers(sharedTrackers);
        analyzerB.RunAnalysis();

        var partialViolations = CollectPartialViolations(sharedTrackers);

        var allViolations = analyzerA.Violations
            .Concat(analyzerB.Violations)
            .Concat(partialViolations)
            .Where(v => v.RuleName == "EnforceReadonlyFields")
            .ToList();

        Assert.Empty(allViolations);
    }

    [Fact]
    public void PartialClass_FieldDeclaredInFileA_NeverWrittenOutsideConstructor_Violation()
    {
        const string sourceA = @"
public partial class Logger
{
    private string _prefix;

    public Logger(string prefix)
    {
        _prefix = prefix;
    }
}";
        const string sourceB = @"
public partial class Logger
{
    public string GetPrefix() => _prefix;
}";
        var config = CreateConfig();
        var sharedTrackers = new ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>(SymbolEqualityComparer.Default);
        var (modelA, modelB) = CreatePartialClassCompilation(sourceA, sourceB);

        var analyzerA = new LinterAnalyzer("FileA.cs", modelA, config, isTestFile: false);
        analyzerA.UseSharedFieldTrackers(sharedTrackers);
        analyzerA.RunAnalysis();

        var analyzerB = new LinterAnalyzer("FileB.cs", modelB, config, isTestFile: false);
        analyzerB.UseSharedFieldTrackers(sharedTrackers);
        analyzerB.RunAnalysis();

        var partialViolations = CollectPartialViolations(sharedTrackers);

        var readonlyViolations = analyzerA.Violations
            .Concat(analyzerB.Violations)
            .Concat(partialViolations)
            .Where(v => v.RuleName == "EnforceReadonlyFields")
            .ToList();

        Assert.Single(readonlyViolations);
        Assert.Contains("_prefix", readonlyViolations[0].Details);
    }

    [Fact]
    public void SingleFile_FieldDeclaredAfterWritingMethod_NoViolation()
    {
        const string source = @"
using System.Threading;
public sealed class DbService
{
    public static void EnsureSchema()
    {
        Volatile.Write(ref s_schemaEnsured, 1);
    }

    private static int s_schemaEnsured;
}";
        var model = CreateSingleFileCompilation(source);
        var violations = LinterAnalyzer.Analyze("File.cs", model, CreateConfig());
        Assert.Empty(violations.Where(v => v.RuleName == "EnforceReadonlyFields"));
    }

    [Fact]
    public void SingleFile_FieldDeclaredBeforeWritingMethod_NoViolation()
    {
        const string source = @"
public sealed class Counter
{
    private int _count;

    public void Increment()
    {
        _count++;
    }
}";
        var model = CreateSingleFileCompilation(source);
        var violations = LinterAnalyzer.Analyze("File.cs", model, CreateConfig());
        Assert.Empty(violations.Where(v => v.RuleName == "EnforceReadonlyFields"));
    }

    [Fact]
    public void SingleFile_BlazorComponentRefField_NoViolation()
    {
        const string source = @"
public interface IComponent { }
public class ComponentBase : IComponent { }
public class ErrorBoundary : ComponentBase { }

public sealed class MainLayout
{
    private ErrorBoundary? _bodyErrorBoundary;

    public ErrorBoundary? GetBoundary() => _bodyErrorBoundary;
}";
        var model = CreateSingleFileCompilation(source);
        var violations = LinterAnalyzer.Analyze("MainLayout.razor.cs", model, CreateConfig());
        Assert.Empty(violations.Where(v => v.RuleName == "EnforceReadonlyFields"));
    }

    [Fact]
    public void SingleFile_BlazorGenericComponentRefField_NoViolation()
    {
        const string source = @"
public interface IComponent { }
public class ComponentBase : IComponent { }
public class MudDataGrid<T> : ComponentBase { }
public record MyRow(int Id);

public sealed class DataTable
{
    private MudDataGrid<MyRow>? _mudTable;

    public MudDataGrid<MyRow>? GetGrid() => _mudTable;
}";
        var model = CreateSingleFileCompilation(source);
        var violations = LinterAnalyzer.Analyze("DataTable.razor.cs", model, CreateConfig());
        Assert.Empty(violations.Where(v => v.RuleName == "EnforceReadonlyFields"));
    }

    [Fact]
    public void SingleFile_NonComponentField_Violation()
    {
        const string source = @"
public sealed class SomeService { }

public sealed class MyComponent
{
    private SomeService? _service;

    public SomeService? GetService() => _service;
}";
        var model = CreateSingleFileCompilation(source);
        var violations = LinterAnalyzer.Analyze("MyComponent.cs", model, CreateConfig());
        Assert.Single(violations.Where(v => v.RuleName == "EnforceReadonlyFields"));
    }

    [Fact]
    public void SingleFile_FieldNeverWrittenOutsideConstructor_Violation()
    {
        const string source = @"
public sealed class Logger
{
    private string _prefix;

    public Logger(string prefix)
    {
        _prefix = prefix;
    }

    public string GetPrefix() => _prefix;
}";
        var model = CreateSingleFileCompilation(source);
        var violations = LinterAnalyzer.Analyze("File.cs", model, CreateConfig());
        var readonlyViolations = violations.Where(v => v.RuleName == "EnforceReadonlyFields").ToList();
        Assert.Single(readonlyViolations);
        Assert.Contains("_prefix", readonlyViolations[0].Details);
    }
}
