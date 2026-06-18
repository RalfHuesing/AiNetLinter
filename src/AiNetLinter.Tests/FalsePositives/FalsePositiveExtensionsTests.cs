#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;

namespace AiNetLinter.Tests.FalsePositives;

/// <summary>
/// Tests für die False-Positive-Erweiterungen: AllowOutParametersInPrivateMethods,
/// SemanticNamingExemptMethodNames, FootprintIgnoreTypeNames, SemanticNamingAllowSubstringOfMethodName.
/// </summary>
public sealed class FalsePositiveExtensionsTests
{
    private static LinterConfig CreateBaseConfig() => new()
    {
        Global = new GlobalConfig
        {
            EnforceSealedClasses = false,
            AllowDynamic = false,
            AllowOutParameters = false,
            AllowTryPatternOutParameters = true,
            EnforceValueObjectContracts = false,
            EnforcePascalCase = false,
            EnforceXmlDocumentation = false,
            EnforceSemanticNaming = false,
            EnforceNullableEnable = false,
            EnforceNoSilentCatch = false,
            EnforceNoVariableShadowing = false,
            EnforceReadonlyParameters = false,
            EnforceReadonlyFields = false,
            EnforceNoMagicValues = false,
            EnforceExplicitStateImmutability = false,            PreventContextDependentOverloads = false,            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false,
        },
        Metrics = new MetricsConfig
        {
            MaxLineCount = 500,
            MaxMethodParameterCount = 4,
            MaxCyclomaticComplexity = 12,
            MaxCognitiveComplexity = 15,
        },
    };

    private static SemanticModel GetSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("FpExtTestAssembly")
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

    // ─── Feature 1: AllowOutParametersInPrivateMethods ───────────────────────

    [Fact]
    public void AllowOutParametersInPrivateMethods_True_DoesNotFlagPrivateMethod()
    {
        const string source = """
            #nullable enable
            public sealed class Parser
            {
                private static void Split(string s, out int a, out int b)
                {
                    a = 1; b = 2;
                }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                AllowOutParameters = false,
                AllowTryPatternOutParameters = true,
                AllowOutParametersInPrivateMethods = true,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Parser.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void AllowOutParametersInPrivateMethods_False_FlagsPrivateMethod()
    {
        const string source = """
            #nullable enable
            public sealed class Parser
            {
                private static void Split(string s, out int a, out int b)
                {
                    a = 1; b = 2;
                }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                AllowOutParameters = false,
                AllowTryPatternOutParameters = true,
                AllowOutParametersInPrivateMethods = false,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Parser.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void AllowOutParametersInPrivateMethods_True_StillFlagsPublicMethod()
    {
        const string source = """
            #nullable enable
            public sealed class Converter
            {
                public static void GetRange(int n, out int start, out int end)
                {
                    start = 0; end = n;
                }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                AllowOutParameters = false,
                AllowTryPatternOutParameters = true,
                AllowOutParametersInPrivateMethods = true,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Converter.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void AllowOutParametersInPrivateMethods_True_DoesNotAffectProtectedMethod()
    {
        const string source = """
            #nullable enable
            public class Base
            {
                protected void GetParts(string s, out int a, out int b)
                {
                    a = 1; b = 2;
                }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                AllowOutParameters = false,
                AllowTryPatternOutParameters = false,
                AllowOutParametersInPrivateMethods = true,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Base.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
    }

    // ─── Feature 2: SemanticNamingExemptMethodNames ───────────────────────────

    [Fact]
    public void SemanticNaming_EqualsOverride_ObjNotFlagged_ByDefault()
    {
        const string source = """
            #nullable enable
            public sealed class MyType
            {
                public override bool Equals(object? obj) => obj is MyType;
                public override int GetHashCode() => 0;
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                EnforceSemanticNaming = true,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MyType.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceSemanticNaming");
    }

    [Fact]
    public void SemanticNaming_EqualsOverride_FlaggedWhenRemovedFromExemptList()
    {
        const string source = """
            #nullable enable
            public sealed class MyType
            {
                public override bool Equals(object? obj) => obj is MyType;
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                EnforceSemanticNaming = true,
                SemanticNamingExemptMethodNames = [],
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MyType.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "EnforceSemanticNaming");
    }

    [Fact]
    public void SemanticNaming_CustomExemptMethod_NotFlagged()
    {
        const string source = """
            #nullable enable
            public sealed class Processor
            {
                public void Process(object? data) { }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                EnforceSemanticNaming = true,
                SemanticNamingExemptMethodNames = ["Process"],
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Processor.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceSemanticNaming");
    }

    [Fact]
    public void SemanticNaming_NormalPublicMethod_DataStillFlagged()
    {
        const string source = """
            #nullable enable
            public sealed class Service
            {
                public void Handle(object? data) { }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                EnforceSemanticNaming = true,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Service.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "EnforceSemanticNaming");
    }

    // ─── Feature 3: FootprintIgnoreTypeNames ─────────────────────────────────

    [Fact]
    public void FootprintIgnoreTypeNames_ExcludesNamedType_ReducesFootprint()
    {
        // HeavyDependency liegt in einer eigenen Datei (eigenem SyntaxTree),
        // damit das Ausschließen tatsächlich die Footprint-Zeilenzahl reduziert.
        const string depSource = """
            namespace TestNs;
            public class HeavyDependency
            {
                public int Value1 { get; set; }
                public int Value2 { get; set; }
                public int Value3 { get; set; }
                public int Value4 { get; set; }
            }
            """;
        const string targetSource = """
            using TestNs;
            namespace TestNs;
            public class TargetClass
            {
                private HeavyDependency _dep = new HeavyDependency();
            }
            """;

        var depTree = CSharpSyntaxTree.ParseText(depSource, path: "HeavyDependency.cs");
        var targetTree = CSharpSyntaxTree.ParseText(targetSource, path: "TargetClass.cs");
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("FpFootprintTest")
            .AddSyntaxTrees(depTree, targetTree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var targetModel = compilation.GetSemanticModel(targetTree);
        var targetNode = targetTree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "TargetClass");
        var targetSymbol = targetModel.GetDeclaredSymbol(targetNode) as INamedTypeSymbol;
        Assert.NotNull(targetSymbol);

        var footprintWithDep = AIContextFootprintCalculator.Calculate(targetSymbol);
        var footprintIgnored = AIContextFootprintCalculator.Calculate(
            targetSymbol,
            ignoreTypeNames: ["HeavyDependency"]);

        Assert.True(footprintIgnored < footprintWithDep,
            $"Erwartet: footprintIgnored ({footprintIgnored}) < footprintWithDep ({footprintWithDep})");
    }

    [Fact]
    public void FootprintIgnoreTypeNames_CaseInsensitive()
    {
        const string source = """
            namespace TestNs;
            public class SqlExecutor
            {
                public void Execute(string sql) { }
            }
            public class Repository
            {
                private SqlExecutor _executor = new SqlExecutor();
            }
            """;

        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("FpFootprintCaseTest")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var repoNode = tree.GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == "Repository");
        var repoSymbol = model.GetDeclaredSymbol(repoNode) as INamedTypeSymbol;
        Assert.NotNull(repoSymbol);

        var footprintLower = AIContextFootprintCalculator.Calculate(
            repoSymbol,
            ignoreTypeNames: ["sqlexecutor"]);
        var footprintExact = AIContextFootprintCalculator.Calculate(
            repoSymbol,
            ignoreTypeNames: ["SqlExecutor"]);

        Assert.Equal(footprintExact, footprintLower);
    }

    // ─── Feature 4: SemanticNamingAllowSubstringOfMethodName ─────────────────

    [Fact]
    public void SemanticNaming_SubstringOfMethod_NotFlagged_WhenOptionEnabled()
    {
        const string source = """
            #nullable enable
            public sealed class Scheduler
            {
                public void AppendTimelineItemAsync(string id, object? item) { }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                EnforceSemanticNaming = true,
                SemanticNamingAllowSubstringOfMethodName = true,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Scheduler.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceSemanticNaming");
    }

    [Fact]
    public void SemanticNaming_SubstringOfMethod_StillFlagged_WhenOptionDisabled()
    {
        const string source = """
            #nullable enable
            public sealed class Scheduler
            {
                public void AppendTimelineItemAsync(string id, object? item) { }
            }
            """;

        var config = CreateBaseConfig() with
        {
            Global = CreateBaseConfig().Global with
            {
                EnforceSemanticNaming = true,
                SemanticNamingAllowSubstringOfMethodName = false,
            }
        };

        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Scheduler.cs", model, config);
        Assert.Contains(violations, v => v.RuleName == "EnforceSemanticNaming");
    }
}
