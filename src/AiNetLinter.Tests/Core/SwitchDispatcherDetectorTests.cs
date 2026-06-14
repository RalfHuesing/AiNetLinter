#nullable enable

using System.Linq;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class SwitchDispatcherDetectorTests
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

    private static LinterConfig CreateConfig(
        int maxComplexity,
        int nearMissTolerance,
        bool excludeDispatcher,
        int maxCaseBodyLines)
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
                ComplexityNearMissTolerance = nearMissTolerance,
                ExcludeSwitchDispatcherCases = excludeDispatcher,
                SwitchDispatcherMaxCaseBodyLines = maxCaseBodyLines
            }
        };
    }

    [Fact]
    public void SwitchDispatcher_IsExempt_WhenExcludeSwitchDispatcherCases_IsTrue()
    {
        const string source = @"
public class TestClass
{
    public int Route(string cmd, int p)
    {
        if (cmd == ""a"") return HandleA(p);
        if (cmd == ""b"") return HandleB(p);
        if (cmd == ""c"") return HandleC(p);
        return 0;
    }

    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
}";

        var model = CreateSemanticModel(source);
        var config = CreateConfig(maxComplexity: 2, nearMissTolerance: 0, excludeDispatcher: true, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        // Standard-Komplexität wäre 4 (1 Basis + 3 ifs).
        // Da ExcludeSwitchDispatcherCases=true, wird es als Dispatcher erkannt -> Komplexität 1 <= Limit 2 -> Kein Fehler.
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
    }

    [Fact]
    public void SwitchDispatcher_IsNotExempt_WhenExcludeSwitchDispatcherCases_IsFalse()
    {
        const string source = @"
public class TestClass
{
    public int Route(string cmd, int p)
    {
        if (cmd == ""a"") return HandleA(p);
        if (cmd == ""b"") return HandleB(p);
        if (cmd == ""c"") return HandleC(p);
        return 0;
    }

    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
}";

        var model = CreateSemanticModel(source);
        var config = CreateConfig(maxComplexity: 2, nearMissTolerance: 0, excludeDispatcher: false, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        // ExcludeSwitchDispatcherCases=false -> Schlägt fehl wegen Komplexität 4 > Limit 2.
        Assert.NotEmpty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
    }

    [Fact]
    public void SwitchDispatcher_IsNotExempt_WhenBranchHasComplexLogic()
    {
        const string source = @"
public class TestClass
{
    public int Route(string cmd, int p)
    {
        if (cmd == ""a"") 
        {
            if (p > 0) return HandleA(p); // Nested logic -> Not trivial!
            return 0;
        }
        if (cmd == ""b"") return HandleB(p);
        if (cmd == ""c"") return HandleC(p);
        return 0;
    }

    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
}";

        var model = CreateSemanticModel(source);
        var config = CreateConfig(maxComplexity: 2, nearMissTolerance: 0, excludeDispatcher: true, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        // Ein Zweig ist nicht trivial -> Kein reiner Dispatcher -> Fehler!
        Assert.NotEmpty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
    }

    [Fact]
    public void SwitchDispatcher_IsNotExempt_WhenFewerThanThreeBranches()
    {
        const string source = @"
public class TestClass
{
    public int Route(string cmd, int p)
    {
        if (cmd == ""a"") return HandleA(p);
        if (cmd == ""b"") return HandleB(p);
        return 0;
    }

    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
}";

        var model = CreateSemanticModel(source);
        // Limit 1, Komplexität 3 (1 Basis + 2 ifs)
        var config = CreateConfig(maxComplexity: 1, nearMissTolerance: 0, excludeDispatcher: true, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        // Nur 2 Branches -> Kein Dispatcher -> Fehler!
        Assert.NotEmpty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
    }

    [Fact]
    public void ExpressionBodiedSwitch_IsExempt_WhenExcludeSwitchDispatcherCases_IsTrue()
    {
        const string source = @"
public class TestClass
{
    public int Route(string cmd, int p) => cmd switch
    {
        ""a"" => HandleA(p),
        ""b"" => HandleB(p),
        ""c"" => HandleC(p),
        _ => 0
    };

    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
}";

        var model = CreateSemanticModel(source);
        var config = CreateConfig(maxComplexity: 2, nearMissTolerance: 0, excludeDispatcher: true, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        // Expression bodied switch mit 4 Armen -> Dispatcher -> Komplexität 1 <= Limit 2 -> Kein Fehler!
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity)));
    }

    [Fact]
    public void NearMissTolerance_WarningLabel_WhenWithinTolerance()
    {
        const string source = @"
public class TestClass
{
    public void ComplexMethod(bool a, bool b, bool c)
    {
        if (a) {}
        if (b) {}
        if (c) {}
    }
}";

        var model = CreateSemanticModel(source);
        // Limit 2, Komplexität 4 (1 Basis + 3 ifs). Toleranz 2.
        // Komplexität 4 <= Limit 2 + Toleranz 2 -> Near-Miss!
        var config = CreateConfig(maxComplexity: 2, nearMissTolerance: 2, excludeDispatcher: false, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        var violation = violations.FirstOrDefault(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity));
        Assert.NotNull(violation);
        Assert.Contains("[near-miss: knapp über Limit]", violation.Details);
    }

    [Fact]
    public void NearMissTolerance_NormalError_WhenBeyondTolerance()
    {
        const string source = @"
public class TestClass
{
    public void ComplexMethod(bool a, bool b, bool c, bool d)
    {
        if (a) {}
        if (b) {}
        if (c) {}
        if (d) {}
    }
}";

        var model = CreateSemanticModel(source);
        // Limit 2, Komplexität 5 (1 Basis + 4 ifs). Toleranz 1.
        // Komplexität 5 > Limit 2 + Toleranz 1 -> Normaler Fehler (kein Near-Miss)!
        var config = CreateConfig(maxComplexity: 2, nearMissTolerance: 1, excludeDispatcher: false, maxCaseBodyLines: 3);

        var violations = LinterAnalyzer.Analyze("Test.cs", model, config);

        var violation = violations.FirstOrDefault(v => v.RuleName == nameof(MetricsConfig.MaxCyclomaticComplexity));
        Assert.NotNull(violation);
        Assert.DoesNotContain("[near-miss: knapp über Limit]", violation.Details);
    }
}
