#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Core.TestCoverage;

/// <summary>
/// Einheitliche Konstanten fuer Zuordnungsgruende von Testabdeckungen.
/// </summary>
public static class TestCoverageMatchReasons
{
    public const string DirectMemberMatch = "Direct Member Match / Invocation";
    public const string ExplicitMemberCoverage = "Explicit Member Coverage";
    public const string MemberNameMatch = "Member Name Match";
    public const string DirectTypeUsage = "Direct Type Usage / Invocation";
    public const string NamingConventionMatch = "Naming Convention Match";
    public const string ExplicitCoversComment = "Explicit @covers Comment";
    public const string DirectTypeofReference = "Direct typeof Reference";

    internal static string For(TestEvidenceKind kind) => kind switch
    {
        TestEvidenceKind.DirectInvocation => DirectMemberMatch,
        TestEvidenceKind.ExplicitMemberCoverage => ExplicitMemberCoverage,
        TestEvidenceKind.MemberNameMatch => MemberNameMatch,
        TestEvidenceKind.DirectTypeUse => DirectTypeUsage,
        TestEvidenceKind.ExplicitTypeCoverage => ExplicitCoversComment,
        TestEvidenceKind.TypeNamingConvention => NamingConventionMatch,
        _ => NamingConventionMatch,
    };

    internal static TestEvidenceKind ToEvidenceKind(string reason) => reason switch
    {
        DirectMemberMatch => TestEvidenceKind.DirectInvocation,
        ExplicitMemberCoverage => TestEvidenceKind.ExplicitMemberCoverage,
        MemberNameMatch => TestEvidenceKind.MemberNameMatch,
        DirectTypeUsage => TestEvidenceKind.DirectTypeUse,
        ExplicitCoversComment or DirectTypeofReference => TestEvidenceKind.ExplicitTypeCoverage,
        NamingConventionMatch => TestEvidenceKind.TypeNamingConvention,
        _ => TestEvidenceKind.TypeNamingConvention,
    };
}

/// <summary>
/// Einheitliche Konstanten fuer Test-Kategorien.
/// </summary>
public static class TestCategories
{
    public const string Unit = "Unit";
    public const string Integration = "Integration";
    public const string Component = "Component";
}

/// <summary>
/// Ergebnis des TestCoverageScanner-Laufs.
/// </summary>
public sealed record TestCoverageScannerResult(
    int TotalMatchingTests,
    IReadOnlyList<TestFileCoverageResult> TestFiles);

/// <summary>
/// Zugeordnete Testdatei mit Details.
/// </summary>
public sealed record TestFileCoverageResult(
    string FilePath,
    string TestClassName,
    string Category,
    string MatchReason,
    IReadOnlyList<string> TestMethods,
    int TotalClassTests,
    string? ProjectDirectory = null,
    TestEvidenceKind EvidenceKind = TestEvidenceKind.TypeNamingConvention,
    string Confidence = "low",
    int? MatchingTestCount = null,
    IReadOnlyList<string> TestClassNames = null!);

/// <summary>
/// Ergebnis eines gebatchten Test-Zuordnungslaufs ueber alle Ziel-Symbole.
/// </summary>
/// <param name="Symbols">Je Ziel-Symbol (in Eingabereihenfolge) die zugeordneten Testdateien.</param>
/// <param name="DistinctTestFileCount">Solutionweite Dedup-Info: Anzahl verschiedener Testdateien
/// ueber alle Ziele hinweg (Pfadvergleich case-insensitive).</param>
/// <param name="DistinctTestFilePaths">Dieselben Pfade dedupliziert und deterministisch (ordinal) sortiert.</param>
public sealed record TestCoverageBatchScanResult(
    IReadOnlyList<TestCoverageBatchSymbolResult> Symbols,
    int DistinctTestFileCount,
    IReadOnlyList<string> DistinctTestFilePaths);

/// <summary>
/// Testzuordnung fuer ein einzelnes Ziel aus dem Batch-Durchlauf — feldgleich zur
/// per-Symbol-API (<see cref="TestCoverageScannerResult"/>), ergaenzt um die stabile Symbol-ID.
/// </summary>
public sealed record TestCoverageBatchSymbolResult(
    string SymbolId,
    int TotalMatchingTests,
    IReadOnlyList<TestFileCoverageResult> TestFiles);
