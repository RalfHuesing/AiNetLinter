#nullable enable

using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class NullCoalescingInitializerClassifierTests
{
    private static SemanticModel CreateSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSemanticModel(tree);
    }

    private static MethodDeclarationSyntax GetMethod(SemanticModel model, string methodName)
    {
        var root = model.SyntaxTree.GetRoot();
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);
    }

    private static LinterConfig CreateConfig(
        int maxComplexity,
        bool excludeNullCoalescing,
        double maxNonCoalescingRatio)
    {
        return new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxCyclomaticComplexity = maxComplexity,
                MaxCognitiveComplexity = maxComplexity,
                ComplexityNearMissTolerance = 0,
                ExcludeNullCoalescingInitializerComplexity = excludeNullCoalescing,
                NullCoalescingInitializerMaxNonCoalescingRatio = maxNonCoalescingRatio
            }
        };
    }

    [Fact]
    public void WithExpression_AllNullCoalescing_WithGuardAndLocal_ReturnsTrue()
    {
        const string source = """
            class C {
                C Apply(C? o) {
                    if (o == null) return this;
                    var local = o;
                    return this with { X = local.X ?? X, Y = local.Y ?? Y };
                }
                public int X;
                public int Y;
            }
            """;

        var model = CreateSemanticModel(source);
        var method = GetMethod(model, "Apply");

        var result = MethodClassifier.IsNullCoalescingInitializer(method, 0.0);
        Assert.True(result);
    }

    [Fact]
    public void SingleReturnWithExpression_ExpressionBody_ReturnsFalse()
    {
        const string source = """
            record R(int X, int Y) {
                R Apply(R? o) => o == null ? this : this with { X = o.X ?? X };
            }
            """;

        var model = CreateSemanticModel(source);
        var method = GetMethod(model, "Apply");

        var result = MethodClassifier.IsNullCoalescingInitializer(method, 0.0);
        Assert.False(result);
    }

    [Fact]
    public void BlockBody_SingleReturn_AllCoalescing_ReturnsTrue()
    {
        const string source = """
            class C {
                C Apply(C o) {
                    return this with { X = o.X ?? X, Y = o.Y ?? Y };
                }
                public int X;
                public int Y;
            }
            """;

        var model = CreateSemanticModel(source);
        var method = GetMethod(model, "Apply");

        var result = MethodClassifier.IsNullCoalescingInitializer(method, 0.0);
        Assert.True(result);
    }

    [Fact]
    public void BlockBody_HasNonCoalescingAssignment_ReturnsFalse()
    {
        const string source = """
            class C {
                C Apply(C o) {
                    return this with { X = o.X ?? X, Y = o.Y.ToString() };
                }
                public int X;
                public string Y = "";
            }
            """;

        var model = CreateSemanticModel(source);
        var method = GetMethod(model, "Apply");

        // Bei 0.0 als Limit: false, weil 1 von 2 Zuweisungen kein ?? ist (Ratio = 0.5)
        var resultDefault = MethodClassifier.IsNullCoalescingInitializer(method, 0.0);
        Assert.False(resultDefault);

        // Bei 0.6 als Limit: true, da Ratio = 0.5 <= 0.6
        var resultRelaxed = MethodClassifier.IsNullCoalescingInitializer(method, 0.6);
        Assert.True(resultRelaxed);
    }

    [Fact]
    public void ObjectCreationExpression_AllCoalescing_ReturnsTrue()
    {
        const string source = """
            class C {
                C Apply(C o) {
                    return new C { X = o.X ?? X };
                }
                public int X;
            }
            """;

        var model = CreateSemanticModel(source);
        var method = GetMethod(model, "Apply");

        var result = MethodClassifier.IsNullCoalescingInitializer(method, 0.0);
        Assert.True(result);
    }

    [Fact]
    public void GlobalConfigApply_Integration_ExemptFromComplexityViolations()
    {
        const string source = """
            public class GlobalConfig
            {
                public bool EnforceSealedClasses { get; init; } = true;
                public bool AllowUnsealedPartialClasses { get; init; } = false;
                public bool AllowDynamic { get; init; } = false;
                public bool AllowOutParameters { get; init; } = false;

                public GlobalConfig Apply(GlobalConfig? @override)
                {
                    if (@override == null) return this;
                    var o = @override;
                    return this with
                    {
                        EnforceSealedClasses        = o.EnforceSealedClasses        ?? EnforceSealedClasses,
                        AllowUnsealedPartialClasses = o.AllowUnsealedPartialClasses ?? AllowUnsealedPartialClasses,
                        AllowDynamic                = o.AllowDynamic                ?? AllowDynamic,
                        AllowOutParameters          = o.AllowOutParameters          ?? AllowOutParameters,
                    };
                }
            }
            """;

        var model = CreateSemanticModel(source);
        
        // Komplexitätslimit auf 2 setzen (Methode hat CC = 5: 1 Basis + 1 guard-if + 3 coalescing-ifs)
        var configWithExemption = CreateConfig(maxComplexity: 2, excludeNullCoalescing: true, maxNonCoalescingRatio: 0.0);
        var violationsWithExemption = LinterAnalyzer.Analyze("Test.cs", model, configWithExemption);

        // Mit Ausnahme: Keine Fehler bezüglich Komplexität
        Assert.Empty(violationsWithExemption.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
        Assert.Empty(violationsWithExemption.Where(v => v.RuleName == nameof(MetricsConfig.MaxCognitiveComplexity)));

        // Ohne Ausnahme: Fehlermeldung
        var configWithoutExemption = CreateConfig(maxComplexity: 2, excludeNullCoalescing: false, maxNonCoalescingRatio: 0.0);
        var violationsWithoutExemption = LinterAnalyzer.Analyze("Test.cs", model, configWithoutExemption);

        Assert.NotEmpty(violationsWithoutExemption.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
        Assert.NotEmpty(violationsWithoutExemption.Where(v => v.RuleName == nameof(MetricsConfig.MaxCognitiveComplexity)));
    }
}
