#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Core.DuplicateDetection;

// Parameter-, Options- und Ergebnis-Records fuer DuplicateDetectionEngine — aus der
// Verhaltens-Datei ausgelagert, damit deren AIContextFootprint/MaxLineCount nicht durch reine
// Datentraeger aufgeblaeht wird (Pattern konsistent mit DependencyGraphModels.cs/SafeguardModels.cs).

/// <summary>
/// Gestaffelte Aehnlichkeits-Klassifikation eines <see cref="DuplicateCluster"/> (jscpd-Pattern).
/// Statt eines harten Cut wird jeder Cluster einem von drei Buckets zugeordnet —
/// <see cref="Fuzzy"/> ist die niedrigste noch gemeldete Stufe, unterhalb von
/// <c>FuzzyThreshold</c> wird nichts ausgegeben (Signal-Rauschen). Deklarationsreihenfolge =
/// Aufsteigende Aehnlichkeit, nutzbar fuer Vergleiche
/// (<c>Bucket &gt;= DuplicateSimilarityBucket.Near</c>).
/// </summary>
internal enum DuplicateSimilarityBucket
{
    Fuzzy,
    Near,
    Exact,
}

/// <summary>
/// Konfiguration eines <see cref="DuplicateDetectionEngine.ScanAsync"/>-Laufs. Getrennt von
/// <see cref="Configuration.GlobalConfig"/>, damit die Engine keine Kenntnis der
/// Config-Infrastruktur braucht (reine Domain-Logik, siehe Klassen-Doc-Kommentar der Engine) —
/// sowohl <c>DuplicateCodeChecker</c> als auch <c>find_duplicates</c> bauen diesen Record aus
/// ihrer jeweiligen Konfigurationsquelle (<c>GlobalConfig</c> bzw. Tool-Argumente mit
/// <c>GlobalConfig</c>-Defaults).
/// </summary>
internal sealed record DuplicateDetectionOptions(
    int MinTokens,
    int NgramSize,
    int MinSharedNgrams,
    double ExactThreshold,
    double NearThreshold,
    double FuzzyThreshold,
    bool NormalizeIdentifiers,
    string? PathScopeFilter = null,
    string? ScopeType = null);

/// <summary>
/// Grund, aus dem eine aufgeloeste Helper-Methode keinen Fingerprint fuer die
/// Duplikat- oder Refactoring-Drift-Erkennung liefern kann.
/// </summary>
internal enum MethodFingerprintEligibility
{
    Eligible,
    SourceUnavailable,
    SourceFileExcluded,
    PermanentlyExcludedPath,
    OutsideScope,
    OutsideScopeType,
    GeneratedCode,
    TooFewTokens,
    TooFewTokensForNgrams,
}

/// <summary>
/// Ergebnis der zielgenauen Helper-Pruefung. <see cref="TokenCount"/> ist nur fuer
/// tokenbezogene Ausschluesse gesetzt.
/// </summary>
internal sealed record MethodFingerprintEligibilityResult(
    MethodFingerprintEligibility Eligibility,
    int? TokenCount = null);

/// <summary>Vom Ground-Truth-Beispiel abgeleitete Default-Werte — Quelle der Wahrheit fuer
/// <see cref="Configuration.GlobalConfig"/>s <c>DuplicateCode*</c>-Property-Defaults und fuer
/// Tests/Tool-Aufrufe ohne explizite Config.</summary>
internal static class DuplicateDetectionDefaults
{
    internal const int MinTokens = 30;
    internal const int NgramSize = 5;
    internal const int MinSharedNgrams = 3;
    internal const double ExactThreshold = 0.95;
    internal const double NearThreshold = 0.80;
    internal const double FuzzyThreshold = 0.65;
    internal const bool NormalizeIdentifiers = false;
    internal const int MaxResults = 20;
    internal const double StructuralExactThreshold = 0.90;
    internal const double StructuralNearThreshold = 0.80;
    internal const double StructuralFuzzyThreshold = 0.70;
}

/// <summary>
/// Eine Methode innerhalb eines <see cref="DuplicateCluster"/>. <see cref="FilePath"/> ist der
/// absolute Pfad (Konvention wie <see cref="Models.RuleViolation.FilePath"/> — Aufrufer
/// normalisieren bei Bedarf relativ, siehe <see cref="Output.PathNormalizer"/>).
/// </summary>
internal sealed record DuplicateClusterMember(
    string FilePath,
    int LineNumber,
    string SignatureName,
    int TokenCount,
    string? StructureProfile = null);

/// <summary>
/// Eine Gruppe transitiv aehnlicher Methoden (A~B, B~C ⇒ Cluster {A,B,C} statt isolierter Paare).
/// <see cref="Score"/> ist das Minimum aller innerhalb des Clusters tatsaechlich berechneten
/// paarweisen Jaccard-Scores (konservativ — "mindestens so aehnlich", siehe
/// <see cref="DuplicateDetectionEngine.BuildClusters"/>), <see cref="Bucket"/> daraus abgeleitet.
/// </summary>
internal sealed record DuplicateCluster(
    IReadOnlyList<DuplicateClusterMember> Members,
    double Score,
    DuplicateSimilarityBucket Bucket);

/// <summary>Gesamtergebnis eines <see cref="DuplicateDetectionEngine.ScanAsync"/>-Laufs.
/// <see cref="Clusters"/> ist unbegrenzt (absteigend nach <see cref="DuplicateCluster.Score"/>
/// sortiert) — Trunkierung ist Sache der Aufrufer (Tool/Checker), analog
/// <see cref="Mcp.Tools.DependencyGraph.DependencyGraphScanner"/>.</summary>
internal sealed record DuplicateDetectionScanResult(
    IReadOnlyList<DuplicateCluster> Clusters,
    int MethodsScanned);

// ── Refactoring-Drift (absence-of-calls-Heuristik, Murphy-Hill 2005) ───────────────────────────
// Eigene Records statt Wiederverwendung von DuplicateCluster/DuplicateClusterMember:
// Refactoring-Drift hat keine Buckets/Cluster (nur "aehnlich zu genau einem Helper H"), und die
// Ausgabe muss explizit als "Kandidaten" erkennbar sein, nicht als "Duplikat-Cluster"
// (hoeheres False-Positive-Budget).

/// <summary>Ein Kandidat fuer Teil C: eine Methode, die strukturell aehnlich zu einem Helper
/// <c>H</c> ist (Jaccard-Score ≥ <see cref="DuplicateDetectionOptions.NearThreshold"/>), <c>H</c>
/// aber nachweislich nicht aufruft ("absence-of-calls"). <see cref="Score"/> ist der exakte
/// Jaccard-Score zu <c>H</c>s Fingerprint (kein Cluster-Minimum wie bei <see cref="DuplicateCluster.Score"/>,
/// weil es hier nur eine Kante pro Kandidat gibt, nicht ein Cluster).</summary>
internal sealed record RefactoringDriftCandidate(
    string FilePath,
    int LineNumber,
    string SignatureName,
    int TokenCount,
    double Score);

/// <summary>Gesamtergebnis von <see cref="RefactoringDriftDetector.FindSimilarToAsync"/>.
/// <see cref="Candidates"/> ist unbegrenzt und absteigend nach <see cref="RefactoringDriftCandidate.Score"/>
/// sortiert — Trunkierung ist wie bei <see cref="DuplicateDetectionScanResult"/> Sache des
/// Aufrufers (<c>RefactoringDriftScanner</c>).</summary>
internal sealed record RefactoringDriftScanResult(
    IReadOnlyList<RefactoringDriftCandidate> Candidates,
    int MethodsScanned);

/// <summary>
/// Interner Fingerprint einer analysierten Methode mit Token-Zählung, N-Gram-Hashes und Symbol.
/// </summary>
internal sealed record MethodFingerprint(
    string FilePath,
    int LineNumber,
    string SignatureName,
    int TokenCount,
    HashSet<ulong> NgramHashes,
    Microsoft.CodeAnalysis.IMethodSymbol Symbol);
