#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

// Parameter- und Ergebnis-Records fuer DuplicateDetectionTool/DuplicateDetectionScanner — aus den
// beiden Verhaltens-Dateien ausgelagert (Pattern konsistent mit DependencyGraphModels.cs).

/// <summary>
/// Parameter-Record fuer <c>find_duplicates</c>. Als Record (nicht positional an der
/// Tool-Signatur) gehalten, damit ein spaeterer optionaler <c>mode</c>-Parameter (Refactoring-
/// Drift-Erweiterung, separater Folge-Task) sich sauber als zusaetzliches Feld ergaenzen laesst,
/// ohne bestehende Konstruktions-Aufrufe umzusortieren — hier bewusst noch NICHT angelegt (totes
/// Feld waere Scope-Kriechen), aber die Struktur erzwingt keine spaetere Bruchaenderung.
/// </summary>
internal sealed record DuplicateDetectionInput(
    int? MinTokens,
    string? SimilarityThreshold,
    bool? NormalizeIdentifiers,
    string? ScopeDir,
    int? MaxResults);

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
/// gewrappt statt eines nackten Arrays (siehe <see cref="McpToolResults.Text{T}"/>-Doc-Kommentar,
/// M2-Regressionslehre).</summary>
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
