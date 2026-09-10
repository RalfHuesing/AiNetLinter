#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using AiNetLinter.Core.Documents;
using AiNetLinter.Core.Git;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;

namespace AiNetLinter.Core;

/// <summary>
/// Analysiert das Git-Diff und findet alle Call-Sites in der Solution, die von geaenderten Methodensignaturen betroffen sind.
/// </summary>
public sealed class DiffImpactAnalyzer
{
    /// <summary>
    /// Führt die semantische Diff-Impact-Analyse aus und gibt eine Liste der betroffenen Aufrufstellen zurück.
    /// </summary>
    /// <param name="solution">Die geladene Roslyn-Solution.</param>
    /// <param name="targetPath">Der Zielpfad des Projekts/der Solution.</param>
    /// <param name="gitSinceRef">Der Git-Commit-Verweis (z. B. HEAD~1) oder null/leer für uncommitteten Code.</param>
    /// <param name="verbose">Aktiviert detailliertes Protokoll-Logging.</param>
    /// <returns>Eine Liste von formatierten Aufrufstellen (Call-Sites).</returns>
    public static async Task<List<string>> AnalyzeAsync(Solution solution, string targetPath, string? gitSinceRef, bool verbose)
    {
        var entries = await AnalyzeEntriesAsync(solution, targetPath, gitSinceRef, verbose);
        return entries.Select(FormatCallSite).ToList();
    }

    /// <summary>
    /// Wie <see cref="AnalyzeAsync"/>, liefert die <see cref="CallSiteEntry"/>-Liste statt fertig
    /// formatierter Strings — Grundlage fuer <c>get_impact</c>s <c>StructuredContent</c>
    /// (Git-Diff-Zweig). Duenner Wrapper auf <see cref="AnalyzeDiffAsync"/>: die Ausgabe (inklusive
    /// Reihenfolge) bleibt feldidentisch zum Traversal-Ergebnis des strukturierten Kerns.
    /// </summary>
    internal static async Task<List<CallSiteEntry>> AnalyzeEntriesAsync(
        Solution solution, string targetPath, string? gitSinceRef, bool verbose)
    {
        var analysis = await AnalyzeDiffAsync(solution, targetPath, gitSinceRef, verbose);
        return analysis is null ? [] : ToCallSiteEntries(analysis.References);
    }

    /// <summary>
    /// Strukturierter Analyse-Kern im bisherigen <c>callers</c>-Scope: baut ein
    /// <see cref="DiffImpactAnalysis"/> (Repository-Wurzel, angefragter Ref, geaenderte Dateien
    /// mit kompakten Hunk-Ranges, geaenderte Symbole mit stabiler ID, Aufrufstellen als
    /// Traversal-Ergebnis). Git läuft pro Aufruf genau einmal (<see cref="RunGitDiff"/>); nicht
    /// aufloesender <paramref name="gitSinceRef"/> wirft <see cref="GitDiffFailedException"/>
    /// unverändert durch, kein Repo oder leerer Diff liefert null.
    /// </summary>
    internal static Task<DiffImpactAnalysis?> AnalyzeDiffAsync(
        Solution solution, string targetPath, string? gitSinceRef, bool verbose) =>
        RunAnalysisAsync(new DiffAnalysisRequest(solution, targetPath, gitSinceRef, verbose, DiffSymbolScope.Callers));

    /// <summary>
    /// Strukturierter Analyse-Kern im breiten <c>change-context</c>-Scope: identische Mechanik
    /// wie <see cref="AnalyzeDiffAsync"/> (ein Git-Lauf, gleiches Ergebnisobjekt, gleiche
    /// Referenz-Stufe), aber ohne Accessibility-Filter und mit vollem Symbolscope inklusive
    /// privater Symbole, Properties/Indexer, Events, Felder, Typdeklarationen und lokaler
    /// Funktionen (Ermittlung über <see cref="DiffSymbolScanner"/>).
    /// </summary>
    internal static Task<DiffImpactAnalysis?> AnalyzeChangeContextAsync(
        Solution solution, string targetPath, string? gitSinceRef, bool verbose) =>
        RunAnalysisAsync(new DiffAnalysisRequest(
            solution, targetPath, gitSinceRef, verbose, DiffSymbolScope.ChangeContext));

    /// <summary>
    /// Gemeinsamer Analyse-Kern hinter beiden benannten Eintrittspunkten; internal, damit
    /// auch instrumentierte Laeufe (optionale <see cref="DiffImpactCounters"/>) durch
    /// exakt denselben Pfad gehen wie die Produktion. Git läuft pro Aufruf genau einmal —
    /// der Zaehler wird unmittelbar vor dem einzigen <see cref="RunGitDiff"/>-Aufruf inkrementiert.
    /// </summary>
    internal static async Task<DiffImpactAnalysis?> RunAnalysisAsync(DiffAnalysisRequest request)
    {
        var repoRoot = GitRepositoryLocator.FindRoot(request.TargetPath);
        if (repoRoot == null)
        {
            LogGitWarning(request.Verbose);
            return null;
        }

        if (request.Counters is { } counters)
        {
            Interlocked.Increment(ref counters.GitRuns);
        }

        var diffOutput = RunGitDiff(repoRoot, request.GitSinceRef);
        if (string.IsNullOrEmpty(diffOutput))
        {
            return null;
        }

        var hunkRanges = ParseGitDiffHunkRanges(diffOutput);
        var changedSymbols = await GetChangedSymbolsFromHunksAsync(
            request.Solution, repoRoot, hunkRanges, request.Scope);
        var shown = ApplyChangedSymbolCap(changedSymbols, request.ChangedSymbolCap);

        return new DiffImpactAnalysis(
            repoRoot,
            request.GitSinceRef,
            GitDiffParser.BuildChangedFiles(hunkRanges),
            shown.Matches.Select(match => match.Entry).ToList(),
            await BuildReferencesAsync(shown.Matches, request.Solution),
            shown.TotalBeforeCap,
            shown.Matches.Select(match => match.Symbol).ToList());
    }

    /// <summary>
    /// Deterministische Kappung der geaenderten Symbole NACH der Symbolermittlung und VOR der
    /// teuren Referenz-Stufe: bei wirksamem Cap wird erst nach Projekt → Datei → Startzeile →
    /// Symbol-ID sortiert (Pfadvergleich ordinal case-insensitive konsistent zum Bestand) und
    /// dann gekappt, sodass Call-Site-Suche und Folgeanalysen nur noch GEZEIGTE Symbole sehen.
    /// Ohne wirksamen Cap bleibt die bestehende Reihenfolge unangetastet — der callers-Pfad ist
    /// damit verhaltensidentisch. Die Gesamtzahl VOR der Kappung wird mitgeliefert.
    /// </summary>
    private static (List<ChangedSymbolMatch> Matches, int TotalBeforeCap) ApplyChangedSymbolCap(
        List<ChangedSymbolMatch> matches, int cap)
    {
        if (matches.Count <= cap)
        {
            return (matches, matches.Count);
        }

        var ordered = matches
            .OrderBy(match => match.Entry.ProjectName, StringComparer.Ordinal)
            .ThenBy(match => match.Entry.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.Entry.StartLine)
            .ThenBy(match => match.Entry.SymbolId, StringComparer.Ordinal)
            .ToList();
        return (ordered.Take(cap).ToList(), matches.Count);
    }

    private static void LogGitWarning(bool verbose)
    {
        if (verbose)
        {
            Console.WriteLine("[WARNING]: Kein Git-Repository gefunden.");
        }
    }

    // find_magic_values/changedOnly ruft RunGitDiff direkt auf, damit die
    // git-diff-Mechanik nicht dupliziert wird. Delegiert an GitDiffParser.
    internal static string? RunGitDiff(string repoRoot, string? gitSinceRef) =>
        GitDiffParser.RunGitDiff(repoRoot, gitSinceRef);

    internal static string? RunGitUntrackedFiles(string repoRoot) =>
        GitDiffParser.RunGitUntrackedFiles(repoRoot);

    /// <summary>
    /// Eine Parse-Wahrheit: die bestehende Zeilen-Expansion wird aus den kompakten
    /// <see cref="HunkRange"/>s abgeleitet, damit Range- und Zeilen-Sicht nicht auseinanderdriften.
    /// Delegiert an <see cref="GitDiffParser"/>.
    /// </summary>
    internal static Dictionary<string, List<int>> ParseGitDiffHunks(string gitDiffOutput) =>
        GitDiffParser.ParseGitDiffHunks(gitDiffOutput);

    internal static Dictionary<string, List<HunkRange>> ParseGitDiffHunkRanges(string gitDiffOutput) =>
        GitDiffParser.ParseGitDiffHunkRanges(gitDiffOutput);

    internal static List<int> ExpandHunkRanges(IReadOnlyList<HunkRange> ranges) =>
        GitDiffParser.ExpandHunkRanges(ranges);

    /// <summary>
    /// Sucht ein <see cref="Document"/> ueber alle Projekte der Solution per (case-insensitivem)
    /// Dateipfad-Vergleich. Wird auch von <see cref="AiNetLinter.Mcp.Tools.FindReferencesTool"/>
    /// (MCP) fuer die positionsbasierte Symbolaufloesung wiederverwendet.
    /// </summary>
    internal static Document? FindDocumentByPath(Solution solution, string filePath)
        => SolutionDocumentPathResolver.Find(solution, filePath);

    private static async Task<List<ChangedSymbolMatch>> GetChangedSymbolsFromHunksAsync(
        Solution solution, string repoRoot, Dictionary<string, List<HunkRange>> hunks, DiffSymbolScope scope)
    {
        var changedSymbols = new List<ChangedSymbolMatch>();
        foreach (var pair in hunks)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(repoRoot, pair.Key));
            var document = FindDocumentByPath(solution, absolutePath);
            if (document == null) continue;

            changedSymbols.AddRange(
                await DiffSymbolScanner.FindChangedSymbolsAsync(document, pair.Value, scope));
        }

        return changedSymbols;
    }

    internal static ChangedSymbolEntry CreateChangedSymbolEntry(ISymbol symbol, Document document) =>
        CreateChangedSymbolEntry(symbol, document, symbol.Locations.First(location => location.IsInSource));

    /// <summary>
    /// Knotenbasierte Variante: Datei und Spanne stammen aus der uebergebenen
    /// Deklarations-Location statt aus der ersten Symbol-Location, damit partielle Deklarationen
    /// je geaenderter Teildeklaration erscheinen (gleiches Symbol, verschiedene Datei/Spanne).
    /// </summary>
    internal static ChangedSymbolEntry CreateChangedSymbolEntry(
        ISymbol symbol, Document document, Location declarationLocation)
    {
        var lineSpan = declarationLocation.GetLineSpan();
        var outputRoot = Path.GetDirectoryName(document.Project.Solution.FilePath) ?? "";
        return new ChangedSymbolEntry(
            CallGraphTraversal.GetStableSymbolId(symbol),
            DiffSymbolScanner.FormatDisplayName(symbol),
            symbol.Kind.ToString(),
            symbol.DeclaredAccessibility,
            document.Project.Name,
            PathNormalizer.ToRelative(outputRoot, declarationLocation.SourceTree!.FilePath),
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.EndLinePosition.Line + 1);
    }

    /// <summary>
    /// Mitgliedsschema „EnthaltenderTyp.Name“ — gemeinsame Quelle der Member-Anzeigenamen fuer
    /// Call-Sites und geaenderte Symbole.
    /// </summary>
    internal static string FormatMemberDisplayName(ISymbol symbol) =>
        $"{symbol.ContainingType?.Name}.{symbol.Name}";

    // Bewusst ohne TraversalState.CreateResult: das dedupliziert und sortiert mehrstufig — die
    // bestehende Ausgabe ist unsortiert/undedupliziert (nur die Symbole werden vorab per
    // Distinct vereinigt) und muss feld- und reihenfolgetreu erhalten bleiben. Die Completeness
    // dokumentiert den Ist-Zustand (eine Ebene, keine Kappung); Kappung bleibt Sache des Tools.
    private static async Task<ReferenceTraversalResult> BuildReferencesAsync(
        List<ChangedSymbolMatch> changedSymbols, Solution solution)
    {
        var callSites = new List<TransitiveCallSiteEntry>();
        var evaluatedSymbols = 0;

        foreach (var symbol in changedSymbols.Select(match => match.Symbol)
                     .Distinct<ISymbol>(SymbolEqualityComparer.Default))
        {
            evaluatedSymbols++;
            var symbolId = CallGraphTraversal.GetStableSymbolId(symbol);
            foreach (var callSite in await FindCallSiteEntriesAsync(symbol, solution))
            {
                callSites.Add(new TransitiveCallSiteEntry(
                    callSite.FilePath, callSite.Line, callSite.SymbolName, callSite.ProjectName,
                    Depth: 1, ReachedFromSymbolId: symbolId));
            }
        }

        var completeness = new TraversalCompleteness(
            RequestedDepth: 1,
            EffectiveDepth: 1,
            VisitedNodeCount: evaluatedSymbols,
            TotalCallSiteCount: callSites.Count,
            ShownCallSiteCount: callSites.Count,
            TruncatedByMaxResults: false,
            TruncatedByNodeLimit: false,
            DepthWasClamped: false);
        return new ReferenceTraversalResult(callSites, completeness);
    }

    /// <summary>
    /// Findet alle Aufrufstellen von <paramref name="symbol"/> ueber
    /// <see cref="SymbolFinder.FindReferencesAsync(ISymbol, Solution, System.Threading.CancellationToken)"/>
    /// und formatiert sie als "Datei:Zeile - Aufruf von ...". Wird auch von
    /// <see cref="AiNetLinter.Mcp.Tools.FindReferencesTool"/> (MCP) wiederverwendet. Duenner
    /// Wrapper um <see cref="FindCallSiteEntriesAsync"/> (bestehende Signatur/bestehendes
    /// Verhalten unveraendert).
    /// </summary>
    internal static async Task<List<string>> FindCallSitesAsync(
        ISymbol symbol, Solution solution, CancellationToken ct = default)
    {
        var entries = await FindCallSiteEntriesAsync(symbol, solution, ct);
        return entries.Select(FormatCallSite).ToList();
    }

    /// <summary>
    /// Wie <see cref="FindCallSitesAsync"/>, liefert die strukturierten <see cref="CallSiteEntry"/>
    /// statt fertig formatierter Strings — Grundlage fuer <c>find_references</c>/<c>get_impact</c>s
    /// <c>StructuredContent</c> (depth=1-Flachfall bzw. Symbol-Branch).
    /// </summary>
    internal static async Task<List<CallSiteEntry>> FindCallSiteEntriesAsync(
        ISymbol symbol, Solution solution, CancellationToken ct = default)
    {
        var entries = new List<CallSiteEntry>();
        var references = await SymbolFinder.FindReferencesAsync(symbol, solution, ct).ConfigureAwait(false);
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? "";

        foreach (var reference in references)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var location in reference.Locations)
            {
                ct.ThrowIfCancellationRequested();
                var lineSpan = location.Location.GetLineSpan();
                var relativePath = PathNormalizer.ToRelative(outputRoot, lineSpan.Path);
                var line = lineSpan.StartLinePosition.Line + 1;
                var caller = await ResolveCallerAsync(location, outputRoot, ct).ConfigureAwait(false);

                entries.Add(new CallSiteEntry(
                    relativePath,
                    line,
                    FormatMemberDisplayName(symbol),
                    location.Document.Project.Name,
                    caller.Name,
                    caller.Id,
                    caller.Location));
            }
        }

        return entries;
    }

    private static async Task<(string? Name, string? Id, CallerLocationDto? Location)> ResolveCallerAsync(
        ReferenceLocation location,
        string outputRoot,
        CancellationToken ct)
    {
        if (location.Document is not { } doc) return (null, null, null);

        var semanticModel = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
        var enclosingSymbol = semanticModel?.GetEnclosingSymbol(location.Location.SourceSpan.Start);
        if (enclosingSymbol is null) return (null, null, null);

        var name = enclosingSymbol switch
        {
            IMethodSymbol m => $"{m.ContainingType?.Name}.{m.Name}",
            IPropertySymbol p => $"{p.ContainingType?.Name}.{p.Name}",
            _ => enclosingSymbol?.Name
        };
        var callerSymbol = enclosingSymbol!;
        var id = CallGraphTraversal.GetStableSymbolId(callerSymbol);
        var syntax = callerSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (syntax is null) return (name, id, null);

        var lineSpan = syntax.SyntaxTree.GetLineSpan(syntax.Span);
        return (
            name,
            id,
            new CallerLocationDto(
                PathNormalizer.ToRelative(outputRoot, lineSpan.Path),
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.EndLinePosition.Line + 1));
    }

    /// <summary>Formatiert <see cref="CallSiteEntry"/> identisch zum bisherigen Text-Format von
    /// <see cref="FindCallSitesAsync"/> — einzige Quelle der Wahrheit, damit Text und
    /// <c>StructuredContent</c> nie auseinanderdriften.</summary>
    internal static string FormatCallSite(CallSiteEntry entry) =>
        $"{entry.FilePath}:{entry.Line} - Aufruf von '{entry.SymbolName}' in Projekt '{entry.ProjectName}'";

    /// <summary>
    /// Bildet die Traversal-Call-Sites feld- und reihenfolgetreu auf <see cref="CallSiteEntry"/>
    /// ab — reine Funktion, weder Sortierung noch Deduplizierung.
    /// </summary>
    internal static List<CallSiteEntry> ToCallSiteEntries(ReferenceTraversalResult references) =>
        references.CallSites.Select(ToCallSiteEntry).ToList();

    private static CallSiteEntry ToCallSiteEntry(TransitiveCallSiteEntry entry) =>
        new(entry.FilePath, entry.Line, entry.SymbolName, entry.ProjectName);
}
