#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.FalsePositives;

/// <summary>
/// Explorations-Suite: legitimer C#-Code, der vom Linter nicht als Fehler gemeldet werden darf.
/// Jeder Test beschreibt ein konkretes FP-Szenario. Fehlschläge beweisen echte False-Positives.
/// </summary>
public sealed class FalsePositiveTests
{
    private static LinterConfig CreateConfig(
        bool allowOut = false,
        bool allowTryPatternOut = true,
        int maxParams = 4,
        bool readonlyParams = false) => new()
    {
        Global = new GlobalConfig
        {
            EnforceSealedClasses = false,
            AllowDynamic = false,
            AllowOutParameters = allowOut,
            AllowTryPatternOutParameters = allowTryPatternOut,
            EnforceValueObjectContracts = false,
            EnforcePascalCase = false,
            EnforceXmlDocumentation = false,
            EnforceSemanticNaming = false,
            EnforceNullableEnable = false,
            EnforceNoSilentCatch = false,
            EnforceNoVariableShadowing = false,
            EnforceReadonlyParameters = readonlyParams,
            EnforceReadonlyFields = false,
            EnforceNoMagicValues = false,
            EnforceExplicitStateImmutability = false,
            PreventContextDependentOverloads = false,
            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false
        },
        Metrics = new MetricsConfig
        {
            MaxLineCount = 500,
            MaxMethodParameterCount = maxParams,
            MaxCyclomaticComplexity = 12,
            MaxCognitiveComplexity = 15
        }
    };

    private static SemanticModel GetSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("FpTestAssembly")
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

    // ─── FP #1: Deconstruct-Methode mit out-Parametern ───────────────────────
    // Deconstruct ist ein C#-Sprachmuster und MUSS out-Parameter verwenden.
    // Weder Prefix "Try"/"Is" noch Rückgabetyp bool — daher früher fälschlich gemeldet.

    [Fact]
    public void FP_Deconstruct_OutParameters_ShouldNotViolate()
    {
        const string source = @"
public sealed class Point
{
    public int X { get; }
    public int Y { get; }
    public Point(int x, int y) { X = x; Y = y; }

    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Point.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void FP_Deconstruct_WithThreeComponents_ShouldNotViolate()
    {
        const string source = @"
public sealed class Color
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }
    public Color(byte r, byte g, byte b) { R = r; G = g; B = b; }

    public void Deconstruct(out byte r, out byte g, out byte b)
    {
        r = R; g = G; b = B;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Color.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    // ─── FP #2: Lokale Funktion mit Try*-Muster und out-Parameter ────────────
    // bool TryParse(string s, out int n) als lokale Funktion ist idiomatisches C#.
    // Der Parent ist LocalFunctionStatementSyntax, nicht MethodDeclarationSyntax —
    // früher wurde der AllowTryPatternOut-Check dadurch nicht erreicht.

    [Fact]
    public void FP_LocalFunction_TryPattern_OutParameter_ShouldNotViolate()
    {
        const string source = @"
public sealed class Parser
{
    public int[] ParseAll(string[] inputs)
    {
        var results = new System.Collections.Generic.List<int>();
        foreach (var s in inputs)
        {
            if (TryParseNumber(s, out var n))
                results.Add(n);
        }
        return results.ToArray();

        static bool TryParseNumber(string s, out int number)
        {
            return int.TryParse(s, out number);
        }
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Parser.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void FP_LocalFunction_IsPattern_OutParameter_ShouldNotViolate()
    {
        const string source = @"
public sealed class Validator
{
    public bool ValidateAll(string[] inputs)
    {
        foreach (var s in inputs)
        {
            if (!IsNonEmpty(s, out var trimmed))
                return false;
        }
        return true;

        static bool IsNonEmpty(string s, out string result)
        {
            result = s.Trim();
            return result.Length > 0;
        }
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Validator.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    // ─── FP #3: Interface-Implementierung mit erzwungenen out-Parametern ──────
    // Wer ein fremdes Interface implementiert, darf die Signatur nicht ändern.
    // Auch non-Try*-Methoden dürfen out-Parameter haben, wenn ein Interface es vorschreibt.

    [Fact]
    public void FP_InterfaceImplementation_ForcedOutParameter_ShouldNotViolate()
    {
        const string source = @"
public interface ICache
{
    bool GetValue(string key, out string value);
}

public sealed class MemoryCache : ICache
{
    public bool GetValue(string key, out string value)
    {
        value = string.Empty;
        return false;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MemoryCache.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void FP_AbstractOverride_ForcedOutParameter_ShouldNotViolate()
    {
        const string source = @"
public abstract class ProviderBase
{
    public abstract bool Resolve(string key, out object result);
}

public sealed class ConcreteProvider : ProviderBase
{
    public override bool Resolve(string key, out object result)
    {
        result = new object();
        return true;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("ConcreteProvider.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    // ─── FP #4: Closure modifiziert lokale Variable (nicht Parameter) ─────────
    // totalProcessed ist eine lokale Variable, kein Parameter.
    // EnforceReadonlyParameters darf hier nicht feuern.

    [Fact]
    public void FP_LocalVariable_ClosureModification_ShouldNotViolate()
    {
        const string source = @"
public sealed class InventoryProcessor
{
    public int ProcessStock(string[] items)
    {
        int totalProcessed = 0;

        void Aggregate(string item)
        {
            if (item.Length > 0)
                totalProcessed++;
        }

        foreach (var item in items)
            Aggregate(item);

        return totalProcessed;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("InventoryProcessor.cs", model, CreateConfig(readonlyParams: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceReadonlyParameters");
    }

    [Fact]
    public void FP_LambdaCapture_LocalCounter_ShouldNotViolate()
    {
        const string source = @"
public sealed class Aggregator
{
    public int Sum(int[] values)
    {
        int total = 0;
        System.Array.ForEach(values, v => total += v);
        return total;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Aggregator.cs", model, CreateConfig(readonlyParams: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceReadonlyParameters");
    }

    // ─── FP #5: Switch-Expression Komplexität ─────────────────────────────────
    // Eine switch-expression mit 5 Arms hat McCabe-Komplexität 6 (weit unter 12).
    // Kognitiv zählt die gesamte switch-expression als +1, nicht als N Arms.

    [Fact]
    public void FP_SwitchExpression_FiveArms_CyclomaticUnderLimit()
    {
        const string source = @"
public sealed class DiscountEngine
{
    public static decimal CalculateDiscount(int amount, bool isPremium) => (amount, isPremium) switch
    {
        (> 500, true)  => 0.20m,
        (> 200, true)  => 0.15m,
        (> 500, false) => 0.10m,
        (> 100, false) => 0.05m,
        _              => 0.00m
    };
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("DiscountEngine.cs", model, CreateConfig());
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxCyclomaticComplexity");
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxCognitiveComplexity");
    }

    [Fact]
    public void FP_SwitchExpression_PatternMatching_WithRelationalPatterns_UnderLimit()
    {
        const string source = @"
public sealed class StatusMapper
{
    public string Map(int code) => code switch
    {
        200 => ""OK"",
        201 => ""Created"",
        400 => ""Bad Request"",
        401 => ""Unauthorized"",
        403 => ""Forbidden"",
        404 => ""Not Found"",
        500 => ""Internal Server Error"",
        _   => ""Unknown""
    };
}";
        var model = GetSemanticModel(source);
        // 7 Arms + default = cyclomatic 8, kognitive 1 — beides unter 12/15
        var violations = LinterAnalyzer.Analyze("StatusMapper.cs", model, CreateConfig());
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxCyclomaticComplexity");
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxCognitiveComplexity");
    }

    // ─── FP #6: string? TryXxx(out T) — Error-String-Try*-Muster ────────────
    // null = Erfolg, non-null = Fehlermeldung. Variante des BCL-Try*-Musters mit
    // string? statt bool als Rückgabetyp. out-Parameter ist erlaubt.

    [Fact]
    public void FP_StringNullable_TryPattern_OutSideData_ShouldNotViolate()
    {
        const string source = @"
public sealed class SiteMetadata { public string Title { get; set; } = """"; }

public static class MetadataPatches
{
    public static string? TryRemovePage(
        SiteMetadata metadata,
        string pageRoute,
        out string normalizedRoute)
    {
        normalizedRoute = pageRoute.Trim();
        if (normalizedRoute.Length == 0)
            return ""Route darf nicht leer sein."";

        metadata.Title = normalizedRoute;
        return null;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MetadataPatches.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void FP_StringNullable_TryPattern_MultipleOutParams_ShouldNotViolate()
    {
        const string source = @"
public static class RouteParser
{
    public static string? TryNormalizePath(
        string raw,
        out string normalized,
        out string segment)
    {
        normalized = raw.Trim().ToLowerInvariant();
        segment = normalized.Split('/')[0];
        return normalized.Length == 0 ? ""Leer"" : null;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("RouteParser.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.DoesNotContain(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void FP_StringNullable_TryPattern_Disabled_ShouldViolate()
    {
        const string source = @"
public static class Patches
{
    public static string? TryUpdate(string input, out string result)
    {
        result = input.Trim();
        return null;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Patches.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: false));
        Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
    }

    [Fact]
    public void FP_StringNullable_NonTryPrefix_ShouldViolate()
    {
        const string source = @"
public static class Converter
{
    public static string? ConvertAndReport(string input, out string result)
    {
        result = input.Trim();
        return null;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Converter.cs", model, CreateConfig(allowOut: false, allowTryPatternOut: true));
        Assert.Contains(violations, v => v.RuleName == "AllowOutParameters");
    }

    // ─── FP #7: CancellationToken als 5. Parameter ───────────────────────────
    // CancellationToken ist per rules.json in MethodParameterCountIgnoreTypeNames.
    // Dieser Test dokumentiert, dass die Config korrekt greift.

    [Fact]
    public void FP_CancellationToken_FifthParameter_WithIgnoreConfig_ShouldNotViolate()
    {
        const string source = @"
public sealed class CancellationToken {}
public sealed class OrderService
{
    public void CreateOrder(string userId, string productId, int quantity, string couponCode, CancellationToken ct)
    {
    }
}";
        var config = CreateConfig(maxParams: 4) with
        {
            Metrics = CreateConfig(maxParams: 4).Metrics with
            {
                MethodParameterCountIgnoreTypeNames = ["CancellationToken"]
            }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("OrderService.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "MaxMethodParameterCount");
    }

    // ─── FP #8: Record mit with-Expression (funktionale Mutation) ────────────
    // `record with { ... }` erzeugt eine neue Instanz — kein Zustandsmutation.
    // EnforceExplicitStateImmutability darf hier nicht feuern.

    [Fact]
    public void FP_Record_WithExpression_FunctionalTransition_ShouldNotViolate()
    {
        const string source = @"
public record GroupState(string Name, int MemberCount);

public static class GroupOperations
{
    public static GroupState AddMember(GroupState current)
    {
        return current with { MemberCount = current.MemberCount + 1 };
    }
}";
        var config = CreateConfig() with
        {
            Global = CreateConfig().Global with { EnforceExplicitStateImmutability = true }
        };
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("GroupOperations.cs", model, config);
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceExplicitStateImmutability");
    }
}
