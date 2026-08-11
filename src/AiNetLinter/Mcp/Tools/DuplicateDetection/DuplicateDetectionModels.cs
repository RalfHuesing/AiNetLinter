#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

// Parameter- und Ergebnis-Records fuer DuplicateDetectionTool/DuplicateDetectionScanner — aus den
// beiden Verhaltens-Dateien ausgelagert (Pattern konsistent mit DependencyGraphModels.cs).

/// <summary>
/// Parameter-Record fuer <c>find_duplicates</c>. <see cref="Mode"/>/<see cref="HelperSymbol"/>
/// wurden als zusaetzliche, optionale Trailing-Felder ergaenzt (Teil C, Refactoring-Drift) — ohne
/// bestehende Konstruktions-Aufrufe mit 5 Positionsargumenten umzusortieren (Default <see
/// langword="null"/> haelt sie kompatibel). <see cref="Mode"/> ist <c>"clone"</c> (Default, Teil A)
/// oder <c>"refactoring-drift"</c> (Teil C, <see cref="HelperSymbol"/> dann Pflicht) — Format von
/// <see cref="HelperSymbol"/> identisch zu <c>find_references</c>/<c>get_impact</c>s
/// <c>symbolIdentifier</c> (stabile DocumentationCommentId, Datei:Zeile:Spalte oder qualifizierter
/// Name).
/// </summary>
internal sealed record DuplicateDetectionInput(
    int? MinTokens,
    string? SimilarityThreshold,
    bool? NormalizeIdentifiers,
    string? ScopeDir,
    int? MaxResults,
    string? Mode = null,
    string? HelperSymbol = null);

/// <summary>Ein Cluster-Mitglied fuer die <c>find_duplicates</c>-Ausgabe (Text und
/// StructuredContent gemeinsam) — 1:1-Projektion von
/// <see cref="Core.DuplicateDetection.DuplicateClusterMember"/> auf solution-relative Pfade.</summary>
internal sealed record DuplicateClusterEntry(
    string FilePath,
    int Line,
    string SignatureName,
    int TokenCount);

/// <summary>Ein Klon-Cluster fuer die <c>find_duplicates</c>-Ausgabe. <see cref="Bucket"/> ist
/// klein geschrieben (<c>exact</c>/<c>near</c>/<c>fuzzy</c>) fuer die JSON-Ausgabe.</summary>
internal sealed record DuplicateClusterPayloadEntry(
    string Bucket,
    double Score,
    IReadOnlyList<DuplicateClusterEntry> Members);

/// <summary>Aggregat-Summary fuer <c>find_duplicates</c> — <see cref="TotalClusters"/> ist die
/// volle (ungekappte) Trefferzahl nach Schwellwert-Filter, <see cref="ShownClusters"/> die nach
/// <c>maxResults</c> gekappte Anzahl.</summary>
internal sealed record DuplicateDetectionSummary(
    int MethodsScanned,
    int TotalClusters,
    int ShownClusters,
    bool Truncated);

/// <summary>StructuredContent-Wurzel fuer <c>find_duplicates</c> — in ein benanntes Objekt
/// gewrappt statt eines nackten Arrays (siehe <see cref="McpToolResults.Text{T}"/>-Doc-Kommentar).
/// </summary>
internal sealed record DuplicateDetectionPayload(
    IReadOnlyList<DuplicateClusterPayloadEntry> Clusters,
    DuplicateDetectionSummary Summary);

/// <summary>Ergebnis von <see cref="DuplicateDetectionScanner.ScanAsync"/> — reine Daten, keine
/// Text-/JSON-Formatierung (die macht <see cref="DuplicateDetectionTool"/>, analog
/// <c>DependencyGraphScanner</c>/<c>DependencyGraphTool</c>). <see cref="Truncated"/> ist ein
/// echtes Bool-Feld statt einer String-Heuristik (siehe <c>get_call_tree</c>-Lehre).</summary>
internal sealed record DuplicateDetectionScanResultForTool(
    IReadOnlyList<Core.DuplicateDetection.DuplicateCluster> ShownClusters,
    int TotalClusters,
    int MethodsScanned,
    bool Truncated);
