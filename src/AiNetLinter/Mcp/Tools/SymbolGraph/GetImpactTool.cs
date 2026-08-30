#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// MCP-Tool <c>get_impact</c>: findet Aufrufstellen geaenderter C#-Signaturen und liefert im
/// Git-Diff-Modus optional den vollen Diff-Kontext. Drei gegenseitig ausschliessliche Zweige —
/// Git-Diff (gitRef optional, leer = uncommittete Aenderungen) mit <c>detailLevel=callers</c>
/// (Default) delegiert an <see cref="DiffImpactAnalyzer.AnalyzeEntriesAsync"/>, Git-Diff mit
/// <c>detailLevel=change-context</c> an den strukturierten Antwortvertrag
/// (<see cref="ChangeContextPayload"/>: geaenderte Dateien/Symbole, Call-Sites, statische
/// Test-Zuordnung, diffbezogene Violations, empfohlene dotnet test-Befehle), und der
/// Symbol-Zweig ueber symbolIdentifier delegiert an
/// <see cref="FindReferencesTool.ResolveSymbolAsync"/> + <see cref="DiffImpactAnalyzer.FindCallSitesAsync"/>.
/// Optionaler <c>depth</c>-Parameter (Default 1, hard cap 3) wirkt nur im Symbol-Branch; er ist im
/// gesamten Git-Branch wirkungslos, weil eine Git-Diff-Symboltiefe nicht sinnvoll definiert ist.
/// Bewusst duenner Dispatch ohne eigene Analyse-/Parsing-Logik. Deckt nur .cs-Dateien ab.
/// </summary>
internal static class GetImpactTool
{
    private const string GitRefUnresolvableHint =
        "gitRef pruefen (z. B. via 'git log'/'git branch') oder ohne gitRef aufrufen fuer uncommittete Aenderungen.";

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, GetImpactInput input, CancellationToken ct, DiffImpactCounters? counters = null)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        var hasGitRef = !string.IsNullOrEmpty(input.GitRef);
        var hasSymbolIdentifier = !string.IsNullOrEmpty(input.SymbolIdentifier);
        if (hasGitRef && hasSymbolIdentifier)
        {
            return McpToolResults.InvalidArgument(
                "gitRef und symbolIdentifier sind gegenseitig exklusiv — genau einen angeben oder " +
                "beide weglassen fuer Git-Diff gegen uncommittete Aenderungen.",
                hint: "Entweder gitRef ODER symbolIdentifier angeben, nie beide.");
        }

        var detailLevel = ResolveDetailLevel(input.DetailLevel);
        if (detailLevel is null)
        {
            return McpToolResults.InvalidArgument(
                $"Unbekannter detailLevel-Wert '{input.DetailLevel}' — erlaubt sind " +
                $"'{ChangeContextContract.DetailLevelCallers}' (Default) und " +
                $"'{ChangeContextContract.DetailLevelChangeContext}'.",
                hint: "detailLevel weglassen oder einen der erlaubten Werte uebergeben.");
        }

        if (detailLevel == ChangeContextContract.DetailLevelChangeContext && hasSymbolIdentifier)
        {
            return McpToolResults.InvalidArgument(
                "detailLevel='change-context' ist nur im Git-Diff-Modus zulaessig und kann nicht " +
                "mit symbolIdentifier kombiniert werden.",
                hint: "Fuer den Kontext eines einzelnen Symbols get_feature_context nutzen.");
        }

        return await (detailLevel == ChangeContextContract.DetailLevelChangeContext
            ? ExecuteChangeContextBranchAsync(state, solution, input, ct, counters)
            : hasSymbolIdentifier
                ? ExecuteSymbolBranchAsync(solution, input, state.AssemblySymbolIdentity, ct)
                : ExecuteGitRefBranchAsync(solution, input, ct));
    }

    // Case-insensitive; null/leer waehlt den Bestands-Pfad (callers). Rueckgabe null = unbekannter Wert.
    private static string? ResolveDetailLevel(string? detailLevel)
    {
        if (string.IsNullOrWhiteSpace(detailLevel))
        {
            return ChangeContextContract.DetailLevelCallers;
        }

        var normalized = detailLevel.Trim();
        return string.Equals(normalized, ChangeContextContract.DetailLevelCallers, StringComparison.OrdinalIgnoreCase)
            ? ChangeContextContract.DetailLevelCallers
            : string.Equals(normalized, ChangeContextContract.DetailLevelChangeContext, StringComparison.OrdinalIgnoreCase)
                ? ChangeContextContract.DetailLevelChangeContext
                : null;
    }

    private static async Task<CallToolResult> ExecuteSymbolBranchAsync(
        Solution solution,
        GetImpactInput input,
        AnalysisSymbolIdentity? assemblyIdentity,
        CancellationToken ct)
    {
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, input.SymbolIdentifier!, ct, assemblyIdentity);
        if (error is not null) return error;

        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var effectiveMax = input.MaxResults < 1 ? 1 : input.MaxResults;
        var traversal = await CallGraphTraversal.ExpandAsync(
            new ReferenceTraversalRequest(
                solution,
                symbol!,
                input.Depth,
                effectiveMax,
                ct,
                AssemblySymbolIdentity: assemblyIdentity));
        var body = TransitiveCallGraphFormatter.Format(traversal);
        if (traversal.Completeness.TotalCallSiteCount == 0)
        {
            body = $"Keine Aufrufstellen gefunden fuer '{input.SymbolIdentifier}'";
        }

        // Wie find_references: auch ein leeres, aber vollstaendiges Ergebnis gilt als
        // abschliessend und bekommt den Sufficiency-Hinweis statt stillschweigend zu enden.
        var finalBody = TransitiveCallGraphFormatter.IsComplete(traversal)
            ? McpSufficiencyHints.Append(body)
            : body;
        var finalText = FindSymbolTool.PrependWarning(warning, finalBody);
        return McpToolResults.Text(finalText, traversal);
    }

    private static async Task<CallToolResult> ExecuteGitRefBranchAsync(Solution solution, GetImpactInput input, CancellationToken ct)
    {
        var targetPath = Path.GetDirectoryName(solution.FilePath) ?? "";
        List<CallSiteEntry> callSiteEntries;
        try
        {
            callSiteEntries = await DiffImpactAnalyzer.AnalyzeEntriesAsync(
                solution, targetPath, input.GitRef, verbose: false);
        }
        catch (GitDiffFailedException ex)
        {
            // Recoverable statt Error: eine nicht aufloesende gitRef ist ein behebbarer
            // Nutzereingabe-Fehler (Tippfehler, falscher Branch-Name), kein Tool-Malfunction —
            // siehe IsErrorPolicy.md.
            return McpToolResults.Recoverable(
                LinterErrorCodes.AnalysisFailed,
                $"Git-Diff fuer gitRef '{ex.GitRef}' fehlgeschlagen — Ref loest nicht auf.",
                context: ex.Message,
                hint: GitRefUnresolvableHint);
        }
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var effectiveMax = input.MaxResults < 1 ? 1 : input.MaxResults;

        if (callSiteEntries.Count == 0)
        {
            var refLabel = string.IsNullOrEmpty(input.GitRef) ? "uncommittete Aenderungen" : input.GitRef;
            return McpToolResults.Text(FindSymbolTool.PrependWarning(
                warning, $"Keine betroffenen Aufrufstellen gefunden fuer '{refLabel}'"));
        }

        var callSites = callSiteEntries.Select(DiffImpactAnalyzer.FormatCallSite).ToList();
        var finalText = FindSymbolTool.PrependWarning(
            warning, McpTruncation.TruncateLines(callSites, callSiteEntries.Count, effectiveMax));
        var shownEntries = callSiteEntries.Count <= effectiveMax
            ? callSiteEntries
            : callSiteEntries.Take(effectiveMax).ToList();
        return McpToolResults.Text(finalText, new { CallSites = shownEntries });
    }

    /// <summary>
    /// Git-Diff-Zweig im breiten Scope mit strukturiertem Antwortvertrag: die deterministische
    /// Symbol-Kappung greift im Analyzer-Kern VOR der teuren Referenz-Stufe; danach laufen die
    /// gebatchte Testzuordnung und die solutionweit-diffbezogene Violations-Stufe ueber denselben
    /// Zaehler-Kanal. Die Antwort ist immer ein strukturiertes Objekt — auch "kein Repo / leerer
    /// Diff" liefert eine leere, aber vertragsgueltige Struktur samt Sufficiency-Hinweis.
    /// </summary>
    private static async Task<CallToolResult> ExecuteChangeContextBranchAsync(
        McpCodeGraphServer state,
        Solution solution,
        GetImpactInput input,
        CancellationToken ct,
        DiffImpactCounters? counters)
    {
        var (maxChangedSymbols, maxTestsPerSymbol) =
            ChangeContextContract.NormalizeCaps(input.MaxChangedSymbols, input.MaxTestsPerSymbol);
        DiffImpactAnalysis? analysis;
        try
        {
            analysis = await DiffImpactAnalyzer.RunAnalysisAsync(new DiffAnalysisRequest(
                solution,
                Path.GetDirectoryName(solution.FilePath) ?? "",
                input.GitRef,
                Verbose: false,
                DiffSymbolScope.ChangeContext,
                Counters: counters,
                ChangedSymbolCap: maxChangedSymbols));
        }
        catch (GitDiffFailedException ex)
        {
            // Dasselbe Recoverable-Muster wie der callers-Zweig: nicht aufloesende gitRef ist
            // behebbarer Nutzereingabe-Fehler, kein Tool-Malfunction (siehe IsErrorPolicy.md).
            return McpToolResults.Recoverable(
                LinterErrorCodes.AnalysisFailed,
                $"Git-Diff fuer gitRef '{ex.GitRef}' fehlgeschlagen — Ref loest nicht auf.",
                context: ex.Message,
                hint: GitRefUnresolvableHint);
        }

        if (analysis is null)
        {
            return McpToolResults.Text(
                McpSufficiencyHints.Append("Kein Git-Repository oder leerer Diff — keine geaenderten Dateien/Symbole."),
                ChangeContextResponseMapper.BuildEmptyPayload());
        }

        var batch = await TestCoverageScanner.FindTestsForSymbolsCoreAsync(
            analysis.ShownSymbolHandles ?? [], solution, counters, ct);
        var violationsStage = await CollectDiffViolationsAsync(state, solution, analysis, counters, ct);
        if (violationsStage.IsMalfunction)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler bei der Violations-Analyse.",
                context: violationsStage.Context,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen.");
        }

        var payload = ChangeContextResponseMapper.BuildPayload(new ChangeContextResponseInput(
            analysis, batch, violationsStage.Violations, maxTestsPerSymbol));
        return McpToolResults.Text(BuildChangeContextText(payload, input.MaxResults), payload);
    }

    /// <summary>Eine solutionweite Violations-Stufe pro Aufruf — Config/Console beschafft der
    /// Tool-Zweig wie <c>get_violations</c> (atomarer Config-Schnappschuss, Server-Konsolen-Kanal).</summary>
    private static Task<DiffViolationScanResult> CollectDiffViolationsAsync(
        McpCodeGraphServer state,
        Solution solution,
        DiffImpactAnalysis analysis,
        DiffImpactCounters? counters,
        CancellationToken ct)
    {
        var configSnapshot = state.GetConfigSnapshot();
        return DiffViolationScanner.CollectAsync(new DiffViolationScanRequest(
            solution,
            configSnapshot.Config,
            state.Console,
            analysis.RepositoryRoot,
            analysis.ChangedFiles,
            analysis.ChangedSymbols,
            counters,
            ct));
    }

    private static string BuildChangeContextText(ChangeContextPayload payload, int maxResults)
    {
        var effectiveMax = Math.Max(maxResults, 1);
        var completeness = payload.Completeness;
        var lines = new List<string>
        {
            $"Change-Context: {payload.ChangedFiles.Count} geaenderte Dateien, " +
            $"{completeness.ChangedSymbolsShown}/{completeness.ChangedSymbolsTotal} geaenderte Symbole, " +
            $"{payload.CallSites.Count} Aufrufstellen, {payload.TestAssociations.Count} Test-Treffer, " +
            $"{payload.Violations.Count} Violations."
        };
        if (payload.ChangedSymbols.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Geaenderte Symbole:");
            lines.AddRange(payload.ChangedSymbols.Take(effectiveMax).Select(FormatSymbolLine));
        }

        if (payload.Violations.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Violations:");
            lines.AddRange(payload.Violations.Take(effectiveMax).Select(FormatViolationLine));
        }

        lines.AddRange(payload.RecommendedTestCommands.Select(command => $"Empfohlen: {command}"));
        var text = string.Join("\n", lines);
        return IsComplete(payload, effectiveMax)
            ? McpSufficiencyHints.Append(text)
            : $"{text}\n{BuildTruncationMeta(completeness, effectiveMax, payload.ChangedSymbols.Count)}";
    }

    private static string FormatSymbolLine(ChangedSymbolPayload symbol) =>
        $"- {symbol.DisplayName} ({symbol.Kind}, {symbol.Accessibility}) {symbol.FilePath}:{symbol.StartLine}-{symbol.EndLine}";

    private static string FormatViolationLine(ViolationPayload violation) =>
        $"- {violation.FilePath}:{violation.LineNumber} {violation.RuleName} ({violation.Severity})";

    private static bool IsComplete(ChangeContextPayload payload, int effectiveMax) =>
        !payload.Completeness.SymbolsTruncated &&
        !payload.Completeness.CallSitesTruncated &&
        !payload.Completeness.TestsTruncated &&
        payload.ChangedSymbols.Count <= effectiveMax;

    private static string BuildTruncationMeta(CompletenessPayload completeness, int effectiveMax, int symbolCount)
    {
        var parts = new List<string>(3);
        if (completeness.SymbolsTruncated || symbolCount > effectiveMax)
        {
            parts.Add($"Symbole {Math.Min(symbolCount, effectiveMax)} von {completeness.ChangedSymbolsShown} gezeigt");
        }

        if (completeness.CallSitesTruncated)
        {
            parts.Add("Aufrufstellen trunkiert");
        }

        if (completeness.TestsTruncated)
        {
            parts.Add("Testtreffer gekappt");
        }

        return $"[Teilergebnis: {string.Join(", ", parts)} — maxChangedSymbols/maxTestsPerSymbol/maxResults erhoehen]";
    }
}

/// <summary>
/// Parameter-Record fuer <see cref="GetImpactTool.ExecuteAsync"/>. Kapselt die
/// Konfigurations-Eingaenge in einem Record (additiv gewachsen um die drei change-context-Optionen
/// mit Defaults), damit <c>MaxMethodParameterCount: 4</c> fuer Methoden eingehalten wird. Solution
/// und Zaehler werden separat uebergeben, weil der Linter keine internal nested types erlaubt.
/// </summary>
internal sealed record GetImpactInput(
    string? GitRef,
    string? SymbolIdentifier,
    int MaxResults,
    int Depth,
    string? DetailLevel = null,
    int MaxChangedSymbols = ChangeContextContract.DefaultMaxChangedSymbols,
    int MaxTestsPerSymbol = ChangeContextContract.DefaultMaxTestsPerSymbol);
