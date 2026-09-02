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
/// <param name="ChangedSymbolsTotal">Anzahl erkannter geaenderter Symbole VOR einer Kappung; ohne
/// wirksamen Cap identisch mit <c>ChangedSymbols.Count</c>.</param>
/// <param name="ShownSymbolHandles">Die ISymbol-Handles der GEZEIGTEN (ggf. gekappten) Symbole in
/// identischer Reihenfolge wie <see cref="DiffImpactAnalysis.ChangedSymbols"/> — Basis fuer
/// Folgeanalysen ohne Symbol-Re-Resolution.</param>
internal sealed record DiffImpactAnalysis(
    string RepositoryRoot,
    string? SinceRef,
    IReadOnlyList<ChangedFileRange> ChangedFiles,
    IReadOnlyList<ChangedSymbolEntry> ChangedSymbols,
    ReferenceTraversalResult References,
    int ChangedSymbolsTotal = 0,
    IReadOnlyList<ISymbol>? ShownSymbolHandles = null);

/// <summary>
/// Instrumentierte Laufzaehler fuer die Einmal-Ausfuehrungs-Nachweise des diff-bezogenen
/// Analysepfads (Git genau einmal, Testsolution genau einmal, Linter genau einmal). Die
/// Uebergabe ist optional: Ohne Zaehler verhaelt sich der Produktivpfad exakt wie bisher.
/// Inkrementiert wird an der jeweiligen Stufe per <see cref="Interlocked"/> — genau ein
/// Inkrement je Durchlauf, nicht je Symbol; jedes Feld hat genau eine Produktions-Inkrement-Stelle:
/// GitRuns im Analyzer-Kern vor dem Git-Aufruf, TestSolutionScans im gebatchten
/// Testzuordnungs-Kern, LintRuns in der diff-bezogenen Violations-Stufe (DiffViolationScanner)
/// unmittelbar vor dem einen Lint-Lauf.
/// </summary>
internal sealed class DiffImpactCounters
{
    /// <summary>Anzahl tatsaechlich ausgefuehrter Git-Diff-Laeufe.</summary>
    public int GitRuns;

    /// <summary>Anzahl gebatchter Solution-Durchlaeufe der Testzuordnung (nicht je Symbol).</summary>
    public int TestSolutionScans;

    /// <summary>
    /// Anzahl solutionweiter Linter-Laeufe der diff-bezogenen Violations-Stufe. Bei leerem Input
    /// (keine Hunks UND keine gezeigten Symbole) wird weder gelaufen noch inkrementiert.
    /// </summary>
    public int LintRuns;
}

/// <summary>
/// Interne Paarung eines geaenderten Roslyn-Symbols mit seinem strukturierten Eintrag — der
/// Eintrag geht ins <see cref="DiffImpactAnalysis"/>-Ergebnisobjekt, das Symbol in die
/// Call-Site-Suche.
/// </summary>
internal sealed record ChangedSymbolMatch(ISymbol Symbol, ChangedSymbolEntry Entry);

/// <summary>
/// Eingangsdaten eines Analyse-Kern-Laufs: Solution, Ziel-Pfad, Git-Ref, Protokollierung und der
/// Symbolermittlungs-Scope (<see cref="DiffSymbolScope"/>) — Parameter-Object fuer den gemeinsamen
/// Kern beider benannter Eintrittspunkte. Die Zaehler sind optional (Null-Verhalten ohne Uebergabe).
/// <see cref="ChangedSymbolCap"/> ist die Obergrenze GEZEIGTER geaenderter Symbole; die Kappung
/// greift im Kern VOR der Referenz-Stufe. Default <see cref="int.MaxValue"/> = unbegrenzt
/// (Bestandsverhalten beider Scopes).
/// </summary>
internal sealed record DiffAnalysisRequest(
    Solution Solution,
    string TargetPath,
    string? GitSinceRef,
    bool Verbose,
    DiffSymbolScope Scope,
    DiffImpactCounters? Counters = null,
    int ChangedSymbolCap = int.MaxValue);

/// <summary>
/// StructuredContent-Eintrag fuer <c>find_references</c>/<c>get_impact</c> — eine Aufrufstelle
/// eines Symbols (Pfad, Zeile, aufgerufenes Symbol, Projekt). 1:1-Struktur zum Text-Format von
/// <see cref="DiffImpactAnalyzer.FormatCallSite"/>.
/// </summary>
internal sealed record CallSiteEntry(string FilePath, int Line, string SymbolName, string ProjectName, string? CallerMemberName = null);

/// <summary>
/// Signalisiert, dass ein explizit angegebener <c>gitRef</c> von <c>git diff</c> nicht aufgeloest
/// werden konnte (Tippfehler, geloeschter Branch, unbekannte Commit-Ref). Getrennt von einem
/// leeren-aber-validen Diff, damit Aufrufer (MCP <c>get_impact</c>) einen
/// falschen gitRef nicht mit "keine Aenderungen" verwechseln.
/// </summary>
internal sealed class GitDiffFailedException(string gitRef, string gitStdErr) : Exception(gitStdErr)
{
    internal string GitRef { get; } = gitRef;
}
