#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Core.DuplicateDetection;

// Parameter-, Options- und Ergebnis-Records fuer DuplicateDetectionEngine — aus der
// Verhaltens-Datei ausgelagert, damit deren AIContextFootprint/MaxLineCount nicht durch reine
// Datentraeger aufgeblaeht wird (Pattern konsistent mit DependencyGraphModels.cs/SafeguardModels.cs).

/// <summary>
/// Gestaffelte Aehnlichkeits-Klassifikation eines <see cref="DuplicateCluster"/> (jscpd-Pattern,
/// siehe <c>tasks/features/07-drift-audit-ideen.md</c> §A.4). Statt eines harten Cut wird jeder
/// Cluster einem von drei Buckets zugeordnet — <see cref="Fuzzy"/> ist die niedrigste noch
/// gemeldete Stufe, unterhalb von <c>FuzzyThreshold</c> wird nichts ausgegeben (Signal-Rauschen).
/// Deklarationsreihenfolge = Aufsteigende Aehnlichkeit, nutzbar fuer Vergleiche
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
    string? PathScopeFilter = null);

/// <summary>Vom Ground-Truth-Beispiel abgeleitete Default-Werte (siehe
/// <c>tasks/features/07-drift-audit-ideen.md</c> §A.3/A.5) — Quelle der Wahrheit fuer
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
    int TokenCount);

/// <summary>
/// Eine Gruppe transitiv aehnlicher Methoden (A~B, B~C ⇒ Cluster {A,B,C} statt isolierter Paare,
/// siehe <c>tasks/features/07-drift-audit-ideen.md</c> §A.2 Schritt 7 / §A.6). <see cref="Score"/>
/// ist das Minimum aller innerhalb des Clusters tatsaechlich berechneten paarweisen
/// Jaccard-Scores (konservativ — "mindestens so aehnlich", siehe
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
