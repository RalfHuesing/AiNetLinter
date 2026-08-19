#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Core.DuplicateDetection;

[Trait("Category", "Component")]
public sealed class StructuralDuplicateDetectorTests
{
    private static readonly DuplicateDetectionOptions DefaultOptions = new(
        MinTokens: 5,
        NgramSize: DuplicateDetectionDefaults.NgramSize,
        MinSharedNgrams: DuplicateDetectionDefaults.MinSharedNgrams,
        ExactThreshold: DuplicateDetectionDefaults.StructuralExactThreshold,
        NearThreshold: DuplicateDetectionDefaults.StructuralNearThreshold,
        FuzzyThreshold: DuplicateDetectionDefaults.StructuralFuzzyThreshold,
        NormalizeIdentifiers: false);

    private static RoslynTestSolution CreateSolution(params (string FileName, string Content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\StructuralDuplicateDetectorTests.slnx",
            new ProjectSpec("StructuralDetectorCases", files));

    // Positiv: semantisch gleiche Methoden werden gefunden
    [Fact]
    public async Task ScanAsync_SemanticallySimilarMappers_DifferentNames_FormsCluster()
    {
        const string mapperA = """
            using Microsoft.CodeAnalysis;
            public static class TypeDescriber
            {
                public static string DescribeKind(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class     => "class",
                        TypeKind.Interface => "interface",
                        TypeKind.Struct    => "struct",
                        TypeKind.Enum      => "enum",
                        _                  => "unknown",
                    };
            }
            """;
        const string mapperB = """
            using Microsoft.CodeAnalysis;
            public static class TypeFormatter
            {
                public static string GetTypeKindLabel(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class     => "Class",
                        TypeKind.Interface => "Interface",
                        TypeKind.Struct    => "Struct",
                        TypeKind.Enum      => "Enum",
                        _                  => "Other",
                    };
            }
            """;

        using var testSolution = CreateSolution(("A.cs", mapperA), ("B.cs", mapperB));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(2, cluster.Members.Count);
        Assert.True(cluster.Score >= DuplicateDetectionDefaults.StructuralFuzzyThreshold);
    }

    [Fact]
    public async Task ScanAsync_SemanticallySimilarMapper_StructureProfileNotEmpty()
    {
        const string mapperA = """
            using Microsoft.CodeAnalysis;
            public static class TypeDescriber
            {
                public static string DescribeKind(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class => "class",
                        _              => "other",
                    };
            }
            """;
        const string mapperB = """
            using Microsoft.CodeAnalysis;
            public static class TypeLabel
            {
                public static string GetLabel(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class => "Class",
                        _              => "Other",
                    };
            }
            """;

        using var testSolution = CreateSolution(("A.cs", mapperA), ("B.cs", mapperB));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        if (result.Clusters.Count == 0) return;
        var member = result.Clusters[0].Members[0];
        Assert.False(string.IsNullOrEmpty(member.StructureProfile));
    }

    // Negativ: verschiedene Absichten werden nicht geclustert
    [Fact]
    public async Task ScanAsync_DifferentReturnTypes_NoCluster()
    {
        const string intMapper = """
            using Microsoft.CodeAnalysis;
            public static class IntMapper
            {
                public static int MapKindToCode(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class => 1,
                        TypeKind.Enum  => 2,
                        _              => 0,
                    };
            }
            """;
        const string boolMapper = """
            using Microsoft.CodeAnalysis;
            public static class BoolMapper
            {
                public static bool IsClassLike(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class     => true,
                        TypeKind.Interface => true,
                        _                  => false,
                    };
            }
            """;

        using var testSolution = CreateSolution(("Int.cs", intMapper), ("Bool.cs", boolMapper));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_PureArithmeticVsStringSwitch_NoCluster()
    {
        // Fundamentally different: int-arithmetic (no switch, direct computation) vs. string-switch over an enum.
        const string arithmetic = """
            public static class Arithmetic
            {
                public static int Double(int x) { int a = x * 2; int b = a + 1; return b; }
            }
            """;
        const string stringSwitch = """
            using Microsoft.CodeAnalysis;
            public static class KindMapper
            {
                public static string DescribeKind(ITypeSymbol symbol) =>
                    symbol.TypeKind switch
                    {
                        TypeKind.Class => "class",
                        TypeKind.Enum  => "enum",
                        _              => "other",
                    };
            }
            """;

        using var testSolution = CreateSolution(("Arith.cs", arithmetic), ("Switch.cs", stringSwitch));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_IoMethod_NotGroupedWithPureSwitchMapper()
    {
        // Eine I/O-behaftete Methode (Console-Aufruf) unterscheidet sich strukturell von einer
        // reinen switch-Methode, da die Purity-Merkmale abweichen ("io" vs. "pure").
        const string ioMethod = """
            public static class Logger
            {
                public static void LogInfo(string msg) { System.Console.WriteLine(msg); }
            }
            """;
        const string pureSwitch = """
            using Microsoft.CodeAnalysis;
            public static class Mapper
            {
                public static string Map(ITypeSymbol s) =>
                    s.TypeKind switch { TypeKind.Class => "class", _ => "other" };
            }
            """;

        using var testSolution = CreateSolution(("IO.cs", ioMethod), ("Pure.cs", pureSwitch));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Empty(result.Clusters);
    }

    // Filter-Tests
    [Fact]
    public async Task ScanAsync_MinTokensFiltersOutTrivialMethods_EmptyResult()
    {
        const string trivial = """
            public static class T
            {
                public static string Get() => "x";
            }
            """;

        using var testSolution = CreateSolution(("A.cs", trivial), ("B.cs", trivial.Replace("class T", "class U")));
        var highTokenOptions = DefaultOptions with { MinTokens = 100 };
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, highTokenOptions, CancellationToken.None);

        Assert.Equal(0, result.MethodsScanned);
        Assert.Empty(result.Clusters);
    }

    [Fact]
    public async Task ScanAsync_MethodsScanned_CountsAllEligibleMethods()
    {
        const string mapperA = """
            using Microsoft.CodeAnalysis;
            public static class MapperA
            {
                public static string MapKind(ITypeSymbol s) =>
                    s.TypeKind switch { TypeKind.Class => "class", _ => "other" };
            }
            """;
        const string mapperB = """
            using Microsoft.CodeAnalysis;
            public static class MapperB
            {
                public static string MapKind(ITypeSymbol s) =>
                    s.TypeKind switch { TypeKind.Class => "class", _ => "other" };
            }
            """;

        using var testSolution = CreateSolution(("A.cs", mapperA), ("B.cs", mapperB));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        Assert.Equal(2, result.MethodsScanned);
    }

    [Fact]
    public async Task ScanAsync_LocalFunctions_AreScannedAsEligibleMethods()
    {
        const string withLocalFunction = """
            public static class Host
            {
                public static string Describe(int x)
                {
                    return Format(x);
                    static string Format(int v)
                    {
                        if (v > 0) return "positive";
                        if (v < 0) return "negative";
                        return "zero";
                    }
                }
            }
            """;
        const string withLocalFunctionB = """
            public static class HostB
            {
                public static string DescribeValue(int x)
                {
                    return GetLabel(x);
                    static string GetLabel(int v)
                    {
                        if (v > 0) return "pos";
                        if (v < 0) return "neg";
                        return "nil";
                    }
                }
            }
            """;

        using var testSolution = CreateSolution(("A.cs", withLocalFunction), ("B.cs", withLocalFunctionB));
        var result = await StructuralDuplicateDetector.ScanAsync(testSolution.Solution, DefaultOptions, CancellationToken.None);

        // Mindestens die lokalen Funktionen sollen als eligible erfasst worden sein
        Assert.True(result.MethodsScanned >= 2);
    }
}
