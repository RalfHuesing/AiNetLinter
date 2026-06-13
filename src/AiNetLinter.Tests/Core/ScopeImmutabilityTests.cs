using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;

namespace AiNetLinter.Tests.Core;

public sealed class ScopeImmutabilityTests
{
    private static LinterConfig CreateConfig(
        bool shadowing,
        bool readonlyParams,
        bool readonlyFields,
        int maxOverloads)
    {
        return new LinterConfig
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
                EnforceNoVariableShadowing = shadowing,
                EnforceReadonlyParameters = readonlyParams,
                EnforceReadonlyFields = readonlyFields,
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
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
                MaxMethodOverloads = maxOverloads
            }
        };
    }

    private static SemanticModel GetSemanticContext(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Any())
        {
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));
        }

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void Shadowing_ParameterShadowsField_IsDisallowed()
    {
        const string source = @"
public sealed class Person
{
    private string name;
    public void SetName(string name)
    {
        this.name = name;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(shadowing: true, false, false, 2));
        Assert.Single(violations);
        Assert.Equal("EnforceNoVariableShadowing", violations.First().RuleName);
    }

    [Fact]
    public void Shadowing_LocalFunctionParameterShadowsOuterParameter_IsDisallowed()
    {
        const string source = @"
public sealed class Test
{
    public void Process(int value)
    {
        void Inner(int value)
        {
        }
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(shadowing: true, false, false, 2));
        Assert.Single(violations);
        Assert.Equal("EnforceNoVariableShadowing", violations.First().RuleName);
    }

    [Fact]
    public void Overloads_CountExceeded_IsDisallowed()
    {
        const string source = @"
public sealed class Calc
{
    public void Compute() {}
    public void Compute(int x) {}
    public void Compute(string s) {}
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, false, false, maxOverloads: 2));
        Assert.Single(violations);
        Assert.Equal("MaxMethodOverloads", violations.First().RuleName);
        Assert.Contains("deklariert 3 Ueberladungen fuer die Methode 'Compute'", violations.First().Details);
    }

    [Fact]
    public void ParameterReassignment_NormalAssignment_IsDisallowed()
    {
        const string source = @"
public sealed class Test
{
    public void Scale(int factor)
    {
        factor = factor * 2;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, readonlyParams: true, false, 2));
        Assert.Single(violations);
        Assert.Equal("EnforceReadonlyParameters", violations.First().RuleName);
    }

    [Fact]
    public void ParameterReassignment_OutParameter_IsAllowed()
    {
        const string source = @"
public sealed class Test
{
    public void GetValue(out int value)
    {
        value = 42;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, readonlyParams: true, false, 2));
        Assert.Empty(violations);
    }

    [Fact]
    public void ParameterReassignment_PropertyModification_IsAllowed()
    {
        const string source = @"
public class Data { public int Age { get; set; } }
public sealed class Test
{
    public void Update(Data data)
    {
        data.Age = 10;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, readonlyParams: true, false, 2));
        Assert.Empty(violations);
    }

    [Fact]
    public void ReadonlyFields_PrivateFieldUnmodifiedOutsideConstructor_IsDisallowed()
    {
        const string source = @"
public sealed class Logger
{
    private string prefix;
    public Logger(string prefix)
    {
        this.prefix = prefix;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, false, readonlyFields: true, 2));
        Assert.Single(violations);
        Assert.Equal("EnforceReadonlyFields", violations.First().RuleName);
        Assert.Contains("wird nur im Konstruktor oder Initialisierer zugewiesen", violations.First().Details);
    }

    [Fact]
    public void ReadonlyFields_PrivateFieldModifiedOutsideConstructor_IsAllowed()
    {
        const string source = @"
public sealed class Counter
{
    private int count;
    public void Increment()
    {
        count++;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, false, readonlyFields: true, 2));
        Assert.Empty(violations);
    }

    [Fact]
    public void ReadonlyFields_ReadonlyFieldUnmodifiedOutsideConstructor_IsAllowed()
    {
        const string source = @"
public sealed class Logger
{
    private readonly string prefix;
    public Logger(string prefix)
    {
        this.prefix = prefix;
    }
}";
        var model = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(false, false, readonlyFields: true, 2));
        Assert.Empty(violations);
    }
}
