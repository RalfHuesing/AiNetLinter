using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class ArchitectureTests
{
    private static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
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
                MaxCognitiveComplexity = 5
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
        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    [Fact]
    public void Analyze_WithCompliantCode_ReturnsZeroViolations()
    {
        const string sourceCode = @"
namespace CompliantNamespace;
public sealed class GoodClass
{
    public void Calculate(int a, string b) {}
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("TestFile.cs", model, config);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithViolations_ReturnsExpectedViolations()
    {
        const string sourceCode = @"
namespace BadNamespace;
public class NonSealedClass
{
    public void DoSomething(int a, string b, double c, bool d)
    {
        dynamic badVariable = 42;
    }
    public void OutMethod(out int value)
    {
        value = 10;
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("TestFile.cs", model, config);

        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceSealedClasses));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxMethodParameterCount));
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.AllowDynamic));
        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.AllowOutParameters));
    }

    [Fact]
    public void Analyze_WithFileTooLong_ReturnsLineCountViolation()
    {
        const string sourceCode = @"
// Line 1
// Line 2
// Line 3
// Line 4
// Line 5
// Line 6
// Line 7
// Line 8
// Line 9
// Line 10
// Line 11
// Line 12
";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("LongFile.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxLineCount));
    }

    [Fact]
    public void Analyze_WithHighCyclomaticComplexity_ReturnsCyclomaticComplexityViolation()
    {
        const string sourceCode = @"
namespace ComplexNamespace;
public sealed class ComplexClass
{
    public void Verify(int x)
    {
        if (x > 1) {}
        if (x > 2) {}
        if (x > 3) {}
        if (x > 4) {}
        if (x > 5) {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("ComplexFile.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity));
    }

    [Fact]
    public void Analyze_WithHighCognitiveComplexity_ReturnsCognitiveComplexityViolation()
    {
        const string sourceCode = @"
namespace CognitiveNamespace;
public sealed class CognitiveClass
{
    public void Nesting(int a, int b, int c)
    {
        if (a > 0)
        {
            if (b > 0)
            {
                if (c > 0) {}
            }
        }
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("CognitiveFile.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxCognitiveComplexity));
    }

    [Fact]
    public void Analyze_WithForbiddenNamespaceDependency_ReturnsViolation()
    {
        const string sourceCode = @"
namespace MyApp.Features.Invoicing;
using MyApp.Features.Customer;
public sealed class InvoiceService {}";

        var config = CreateDefaultConfig() with
        {
            ForbiddenNamespaceDependencies = new[]
            {
                new NamespaceRule
                {
                    SourceNamespace = "MyApp.Features.Invoicing",
                    TargetNamespace = "MyApp.Features.Customer"
                }
            }
        };
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("InvoiceService.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == "ForbiddenNamespaceDependency");
    }

    [Fact]
    public void Analyze_WithValueObjectNotRecordOrStruct_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Domain;
public class EmailValueObject
{
    public string Address { get; }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Email.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceValueObjectContracts));
        Assert.Contains(violations, v => v.Details.Contains("ist als 'class' deklariert"));
    }

    [Fact]
    public void Analyze_WithValueObjectMutableProperty_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Domain;
public sealed record MoneyValueObject
{
    public decimal Amount { get; set; }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Money.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceValueObjectContracts));
        Assert.Contains(violations, v => v.Details.Contains("veraenderbare Eigenschaft"));
    }

    [Fact]
    public void Analyze_WithNonPascalCaseTypeName_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
public sealed class badClass {}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforcePascalCase = true }
        };
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Source.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforcePascalCase));
    }

    [Fact]
    public void Analyze_WithPublicMethodMissingXmlDoc_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
/// <summary>
/// Good doc.
/// </summary>
public sealed class GoodClass
{
    public void MissingDocMethod() {}
}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforceXmlDocumentation = true }
        };
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Source.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceXmlDocumentation));
    }

    [Fact]
    public void Analyze_WithGenericParameterName_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
/// <summary>
/// Good class.
/// </summary>
public sealed class GoodClass
{
    /// <summary>
    /// Method with bad param name.
    /// </summary>
    public void Save(string data) {}
}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforceSemanticNaming = true }
        };
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Source.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceSemanticNaming));
    }

    [Fact]
    public void Analyze_WithFileMissingNullableEnable_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
/// <summary>
/// Good class.
/// </summary>
public sealed class GoodClass {}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforceNullableEnable = true }
        };
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Source.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceNullableEnable));
    }

    [Fact]
    public void Analyze_WithSilentCatch_ReturnsViolation()
    {
        const string sourceCode = @"
namespace Test;
/// <summary>
/// Good class.
/// </summary>
public sealed class GoodClass
{
    /// <summary>
    /// Work.
    /// </summary>
    public void Work()
    {
        try {}
        catch {}
    }
}";
        var config = CreateDefaultConfig() with
        {
            Global = new GlobalConfig { EnforceNoSilentCatch = true }
        };
        var (tree, model) = GetSemanticContext(sourceCode);
        var violations = LinterAnalyzer.Analyze("Source.cs", model, config);

        Assert.Contains(violations, v => v.RuleName == nameof(GlobalConfig.EnforceNoSilentCatch));
    }
}
