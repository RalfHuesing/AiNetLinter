#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

// Parameter- und Ergebnis-Records fuer RefactoringDriftScanner/DuplicateDetectionTools
// mode="refactoring-drift"-Zweig — eigene Datei statt Erweiterung von DuplicateDetectionModels.cs,
// weil Teil C ein eigenes Response-Schema braucht (Kandidaten statt Cluster/Buckets, siehe
// tasks/features/05-roadmap.md "Teil C" Punkt 5) und das bestehende clone-Antwortformat (Teil A)
// dadurch nicht verkompliziert werden soll.

/// <summary>Ein Refactoring-Drift-Kandidat fuer die <c>find_duplicates</c>-Ausgabe im
/// <c>mode="refactoring-drift"</c>-Zweig — 1:1-Projektion von
/// <see cref="Core.DuplicateDetection.RefactoringDriftCandidate"/> auf einen solution-relativen
/// Pfad. Bewusst NICHT <c>DuplicateClusterEntry</c> wiederverwendet, obwohl die Felder identisch
/// waeren — eigener Typ haelt die beiden Response-Schemata (clone/refactoring-drift) unabhaengig
/// voneinander erweiterbar (siehe Roadmap "Teil C" Punkt 5).</summary>
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
/// (Roadmap "Teil C" Punkt 4: False-Positive-Budget ist hoeher als bei Teil A, strukturelle
/// Aehnlichkeit ist keine automatische Regelverletzung). In ein benanntes Objekt gewrappt statt
/// eines nackten Arrays (M2-Regressionslehre, siehe <see cref="McpToolResults.Text{T}"/>).</summary>
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
