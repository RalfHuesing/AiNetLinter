#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

// Parameter- und Ergebnis-Records fuer RefactoringDriftScanner/DuplicateDetectionTools
// mode="refactoring-drift"-Zweig — eigene Datei statt Erweiterung von DuplicateDetectionModels.cs,
// weil Refactoring-Drift ein eigenes Response-Schema braucht (Kandidaten statt Cluster/Buckets)
// und das bestehende clone-Antwortformat dadurch nicht verkompliziert werden soll.

/// <summary>Ein Refactoring-Drift-Kandidat fuer die <c>find_duplicates</c>-Ausgabe im
/// <c>mode="refactoring-drift"</c>-Zweig — 1:1-Projektion von
/// <see cref="Core.DuplicateDetection.RefactoringDriftCandidate"/> auf einen solution-relativen
/// Pfad. Bewusst NICHT <c>DuplicateClusterEntry</c> wiederverwendet, obwohl die Felder identisch
/// waeren — eigener Typ haelt die beiden Response-Schemata (clone/refactoring-drift) unabhaengig
/// voneinander erweiterbar.</summary>
internal sealed record RefactoringDriftCandidateEntry(
    string FilePath,
    int Line,
    string SignatureName,
    int TokenCount,
    double Score);

/// <summary>Aggregat-Summary fuer den <c>refactoring-drift</c>-Zweig von <c>find_duplicates</c>.
/// <see cref="HelperSymbol"/> ist der aufgeloeste, vollqualifizierte Anzeigename von <c>H</c>
/// (Bestaetigung, welches Symbol tatsaechlich als Vergleichsbasis diente). <see cref="TotalCandidates"/>
/// ist die volle (ungekappte) Trefferzahl, <see cref="ShownCandidates"/> die nach <c>maxResults</c>
/// gekappte Anzahl.</summary>
internal sealed record RefactoringDriftSummary(
    string HelperSymbol,
    int MethodsScanned,
    int TotalCandidates,
    int ShownCandidates,
    bool Truncated);

/// <summary>StructuredContent-Wurzel fuer den <c>refactoring-drift</c>-Zweig von
/// <c>find_duplicates</c> — Feldname bewusst <see cref="Candidates"/>, nicht <c>Violations</c>
/// (False-Positive-Budget ist hoeher als bei der Clone-Erkennung, strukturelle Aehnlichkeit ist
/// keine automatische Regelverletzung). In ein benanntes Objekt gewrappt statt eines nackten
/// Arrays (siehe <see cref="McpToolResults.Text{T}"/>).</summary>
internal sealed record RefactoringDriftPayload(
    IReadOnlyList<RefactoringDriftCandidateEntry> Candidates,
    RefactoringDriftSummary Summary);

/// <summary>Ergebnis von <see cref="RefactoringDriftScanner.ScanAsync"/> — reine Daten, keine
/// Text-/JSON-Formatierung (die macht <see cref="DuplicateDetectionTool"/>).</summary>
internal sealed record RefactoringDriftScanResultForTool(
    string HelperSymbolDisplayName,
    IReadOnlyList<Core.DuplicateDetection.RefactoringDriftCandidate> ShownCandidates,
    int TotalCandidates,
    int MethodsScanned,
    bool Truncated);
