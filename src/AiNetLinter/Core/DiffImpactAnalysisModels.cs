#nullable enable

using System.Collections.Generic;
using System.Threading;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Kompakte Hunk-Range aus einem Git-Diff-Hunk-Kopf (1-basisch, wie aus <c>@@ -a,b +c,d @@</c>):
/// <see cref="StartLine"/> ist die erste geaenderte Zeile der neuen Dateiversion, <see cref="LineCount"/>
/// ihre Anzahl (<c>+c</c> ohne <c>,d</c> zaehlt als 1; ein explizites <c>d = 0</c> bleibt als Range
/// erhalten und expandiert zu keiner Zeile).
/// </summary>
internal sealed record HunkRange(int StartLine, int LineCount);

/// <summary>
/// Eine im Git-Diff geaenderte Datei mit ihren kompakten Hunk-Ranges.
/// <see cref="FilePath"/> ist repo-root-relativ mit nativen Trennern — dieselbe Bedeutung wie die
/// Schluessel der von <see cref="DiffImpactAnalyzer.ParseGitDiffHunks"/> gelieferten Map.
/// </summary>
internal sealed record ChangedFileRange(string FilePath, IReadOnlyList<HunkRange> Ranges);

/// <summary>
/// Ein vom Diff getroffenes Symbol mit stabiler Identitaet und Kontext. <see cref="SymbolId"/> ist
/// die gemeinsame stabile ID (<see cref="CallGraphTraversal.GetStableSymbolId"/>: DocCommentId oder
/// deterministischer Fallback; lokale Funktionen mit deterministischem <c>#lf:</c>-Sonderfall aus
/// Name + Deklarationsposition), <see cref="DisplayName"/> das Format
/// „EnthaltenderTyp.Membername“ konsistent zu <see cref="CallSiteEntry.SymbolName"/>, und
/// <see cref="FilePath"/> ist solution-relativ (wie <see cref="CallSiteEntry.FilePath"/>) — nicht
/// repo-root-relativ wie <see cref="ChangedFileRange.FilePath"/>.
/// </summary>
internal sealed record ChangedSymbolEntry(
    string SymbolId,
    string DisplayName,
    string Kind,
    Accessibility Accessibility,
    string ProjectName,
    string FilePath,
    int StartLine,
    int EndLine);

/// <summary>
/// Strukturiertes Zwischenergebnis einer Diff-Impact-Analyse: Repository-Wurzel, der angefragte
/// Git-Ref, die geaenderten Dateien samt Hunk-Ranges, die erkannten geaenderten Symbole und die
/// gefundenen Aufrufstellen als vollstaendiges Traversal-Ergebnis. Der Analyzer-Kern baut genau
/// dieses Objekt; die bestehende <see cref="DiffImpactAnalyzer.AnalyzeEntriesAsync"/>-Ausgabe ist
/// der feldidentische Wrapper auf <see cref="References.CallSites"/>.
/// </summary>
internal sealed record DiffImpactAnalysis(
    string RepositoryRoot,
    string? SinceRef,
    IReadOnlyList<ChangedFileRange> ChangedFiles,
    IReadOnlyList<ChangedSymbolEntry> ChangedSymbols,
    ReferenceTraversalResult References);

/// <summary>
/// Instrumentierte Laufzaehler fuer die Einmal-Ausfuehrungs-Nachweise des diff-bezogenen
/// Analysepfads (Git genau einmal, Testsolution genau einmal, Linter genau einmal). Die
/// Uebergabe ist optional: Ohne Zaehler verhaelt sich der Produktivpfad exakt wie bisher.
/// Inkrementiert wird an der jeweiligen Stufe per <see cref="Interlocked"/> — genau ein
/// Inkrement je Durchlauf, nicht je Symbol. Der Linter-Zaehler hat noch keine
/// Inkrement-Stelle; er folgt mit der Violations-Stufe, das Feld existiert bereits.
/// </summary>
internal sealed class DiffImpactCounters
{
    /// <summary>Anzahl tatsaechlich ausgefuehrter Git-Diff-Laeufe.</summary>
    public int GitRuns;

    /// <summary>Anzahl gebatchter Solution-Durchlaeufe der Testzuordnung (nicht je Symbol).</summary>
    public int TestSolutionScans;

    /// <summary>Anzahl solutionweiter Linter-Laeufe (wird von der Violations-Stufe inkrementiert).</summary>
    public int LintRuns;
}
