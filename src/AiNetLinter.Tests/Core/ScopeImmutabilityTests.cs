using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;

namespace AiNetLinter.Tests.Core;

public sealed class ScopeImmutabilityTests
{
    private static Config CreateConfig(int maxOverloads = 2)
    {
        return new Config
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
                EnforceNoSilentCatch = false,                EnforceExplicitStateImmutability = false,
                PreventContextDependentOverloads = false,
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
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxOverloads: 2));
        Assert.Single(violations);
        Assert.Equal("MaxMethodOverloads", violations.First().RuleName);
        Assert.Contains("deklariert 3 Ueberladungen fuer die Methode 'Compute'", violations.First().Details);
    }

    private static Config CreateImmutabilityTestConfig(
        bool allowPrivateBackingFields = false,
        string[]? exemptBaseTypes = null)
    {
        return new Config
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
                EnforceNoSilentCatch = false,                EnforceExplicitStateImmutability = true,
                PreventContextDependentOverloads = false,
                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
                ImmutabilityAllowPrivateBackingFields = allowPrivateBackingFields,
                ImmutabilityExemptBaseTypes = exemptBaseTypes ?? Array.Empty<string>()
            },
            Metrics = new MetricsConfig()
        };
    }

    [Fact]
    public void Immutability_ExemptBaseType_IsSkipped()
    {
        const string source = @"
public class ComponentBase {}
public sealed class MyComponent : ComponentBase
{
    private bool _isLoading;
    public string Value { get; set; }
}";
        var model = GetSemanticContext(source);
        var config = CreateImmutabilityTestConfig(exemptBaseTypes: new[] { "ComponentBase" });
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Empty(violations);
    }

    [Fact]
    public void Immutability_ExemptBaseTypeTransitive_IsSkipped()
    {
        const string source = @"
public class ComponentBase {}
public class MyBaseComponent : ComponentBase {}
public sealed class MyComponent : MyBaseComponent
{
    private bool _state;
    public string Name { get; set; }
}";
        var model = GetSemanticContext(source);
        var config = CreateImmutabilityTestConfig(exemptBaseTypes: new[] { "ComponentBase" });
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Empty(violations);
    }

    [Fact]
    public void Immutability_ExemptInterface_IsSkipped()
    {
        const string source = @"
public interface INotifyPropertyChanged {}
public sealed class MyViewModel : INotifyPropertyChanged
{
    private string _name = """";
    public string Name
    {
        get => _name;
        set => _name = value;
    }
}";
        var model = GetSemanticContext(source);
        var config = CreateImmutabilityTestConfig(exemptBaseTypes: new[] { "INotifyPropertyChanged" });
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Empty(violations);
    }

    [Fact]
    public void Immutability_NonExemptClass_HasViolations()
    {
        const string source = @"
public sealed class OrderService
{
    private string _mutableField = """";
    public string Name { get; set; }
}";
        var model = GetSemanticContext(source);
        var config = CreateImmutabilityTestConfig(exemptBaseTypes: new[] { "ComponentBase" });
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.RuleName == "EnforceExplicitStateImmutability" && v.Details.Contains("Name"));
        Assert.Contains(violations, v => v.RuleName == "EnforceExplicitStateImmutability" && v.Details.Contains("_mutableField"));
    }

    [Fact]
    public void Immutability_PrivateBackingFieldsAllowed_Skipped()
    {
        const string source = @"
public sealed class ViewModelStub
{
    private string _name = """";
    public string PublicField = """";
    public string Name { get; set; }
}";
        var model = GetSemanticContext(source);
        var config = CreateImmutabilityTestConfig(allowPrivateBackingFields: true);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Equal(2, violations.Count);
        Assert.Contains(violations, v => v.Details.Contains("PublicField"));
        Assert.Contains(violations, v => v.Details.Contains("Name"));
        Assert.DoesNotContain(violations, v => v.Details.Contains("_name"));
    }

    [Fact]
    public void Immutability_PrivateBackingFieldsNotAllowed_HasViolations()
    {
        const string source = @"
public sealed class ViewModelStub
{
    private string _name = """";
}";
        var model = GetSemanticContext(source);
        var config = CreateImmutabilityTestConfig(allowPrivateBackingFields: false);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);
        Assert.Single(violations);
        Assert.Contains(violations, v => v.Details.Contains("_name"));
    }
}
