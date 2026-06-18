using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class LinterAnalyzerTests
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
                EnforceValueObjectContracts = true,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,
                EnforceNoVariableShadowing = false,
                EnforceReadonlyParameters = false,
                EnforceReadonlyFields = false,
                EnforceNoMagicValues = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 100,
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
            
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
            
        if (errors.Any())
        {
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));
        }

        var semanticModel = compilation.GetSemanticModel(tree);
        return (tree, semanticModel);
    }

    [Fact]
    public void Analyze_WithValidCode_HasNoViolations()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class TestClass
    {
        public void Work(int x, int y) {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithForbiddenNamespaceInStatement_ReturnsViolation()
    {
        const string source = @"
namespace MyFeature.Domain
{
    public sealed class DomainService
    {
        public void Run()
        {
            var helper = new MyFeature.Infrastructure.DbHelper();
        }
    }
}
namespace MyFeature.Infrastructure
{
    public sealed class DbHelper {}
}";
        var config = CreateDefaultConfig() with
        {
            ForbiddenNamespaceDependencies = new[]
            {
                new NamespaceRule { SourceNamespace = "MyFeature.Domain", TargetNamespace = "MyFeature.Infrastructure" }
            }
        };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "ForbiddenNamespaceDependency");
    }

    [Fact]
    public void Analyze_WithSuppressionComment_IgnoresViolation()
    {
        const string source = @"
// ainetlinter-disable MaxMethodParameterCount
namespace TestNamespace
{
    public sealed class TestClass
    {
        public void Work(int a, int b, int c, int d, int e) {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithDisableAllComment_IgnoresAllViolations()
    {
        const string source = @"
// ainetlinter-disable all
namespace TestNamespace
{
    public class UnsealedClass
    {
        public void Work(int a, int b, int c, int d, int e) {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithVariableNamedDynamic_DoesNotThrowViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class TestClass
    {
        public void Work()
        {
            var dynamic = 5;
        }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Metrics = config.Metrics with { MaxLineCount = 50 } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithUnsealedPartialClass_AllowUnsealedPartialClasses_NoViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public partial class PartialClass
    {
        public void Work() {}
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { AllowUnsealedPartialClasses = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithUnsealedPartialClass_DefaultEnforced_HasViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public partial class PartialClass
    {
        public void Work() {}
    }
}";
        var config = CreateDefaultConfig();
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
    }

    [Fact]
    public void Analyze_WithUnsealedClass_HavingExemptSuffix_NoViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public class OrderHandlerBase
    {
        public void Handle() {}
    }
}";
        var config = CreateDefaultConfig();
        config = config with
        {
            Global = config.Global with
            {
                SealedClassExemptSuffixes = new[] { "Base", "Foundation", "Host" }
            }
        };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithUnsealedClass_HavingNoExemptSuffix_HasViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public class OrderHandler
    {
        public void Handle() {}
    }
}";
        var config = CreateDefaultConfig();
        config = config with
        {
            Global = config.Global with
            {
                SealedClassExemptSuffixes = new[] { "Base", "Foundation", "Host" }
            }
        };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "EnforceSealedClasses");
    }

    [Fact]
    public void Analyze_WithUnsealedClass_ExactMatchExemptSuffix_NoViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public class Base
    {
        public void Handle() {}
    }
}";
        var config = CreateDefaultConfig();
        config = config with
        {
            Global = config.Global with
            {
                SealedClassExemptSuffixes = new[] { "Base" }
            }
        };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_WithUnsealedClass_ExemptSuffixesEmpty_HasViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public class OrderHandlerBase
    {
        public void Handle() {}
    }
}";
        var config = CreateDefaultConfig();
        config = config with
        {
            Global = config.Global with
            {
                SealedClassExemptSuffixes = System.Array.Empty<string>()
            }
        };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "EnforceSealedClasses");
    }

    private static (SyntaxTree, SemanticModel) GetSemanticContextWithErrors(string source)
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
    public void Analyze_Immutability_MutableProperty_HasViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class ImmutableTestClass
    {
        public string Name { get; set; }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { EnforceExplicitStateImmutability = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "EnforceExplicitStateImmutability");
    }

    [Fact]
    public void Analyze_Immutability_DtoClass_NoViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class UserDto
    {
        public string Name { get; set; }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { EnforceExplicitStateImmutability = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_Exceptions_AllowedFatalException_NoViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class ExceptionTest
    {
        public void Run(string arg)
        {
            if (arg == null) throw new System.ArgumentNullException(nameof(arg));
        }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { EnforceResultPatternOverExceptions = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Empty(violations);
    }

    [Fact]
    public void Analyze_Exceptions_CustomLogicException_HasViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class ExceptionTest
    {
        public void Run()
        {
            throw new System.Exception(""Custom logic error"");
        }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { EnforceResultPatternOverExceptions = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "EnforceResultPatternOverExceptions");
    }

    [Fact]
    public void Analyze_PrimitiveOverloads_HasViolation()
    {
        const string source = @"
namespace TestNamespace
{
    public sealed class OverloadTest
    {
        public void Process(int val) {}
        public void Process(string val) {}
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { PreventContextDependentOverloads = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "PreventContextDependentOverloads");
    }

    [Fact]
    public void Analyze_PhantomDependency_ReflectionCall_HasViolation()
    {
        const string source = @"
using System;
namespace TestNamespace
{
    public sealed class ReflectionTest
    {
        public void Load()
        {
            var type = Type.GetType(""SomePhantomClass"");
        }
    }
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { DetectAndBanPhantomDependencies = true } };
        var (tree, model) = GetSemanticContext(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "DetectAndBanPhantomDependencies");
    }

    [Fact]
    public void Analyze_PhantomDependency_UnresolvedNamespace_HasViolation()
    {
        const string source = @"
using SomePhantomNamespace;
namespace TestNamespace
{
    public sealed class PhantomTest {}
}";
        var config = CreateDefaultConfig();
        config = config with { Global = config.Global with { DetectAndBanPhantomDependencies = true } };
        var (tree, model) = GetSemanticContextWithErrors(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, config, isTestFile: false);
        Assert.Contains(violations, v => v.RuleName == "DetectAndBanPhantomDependencies");
    }

}
