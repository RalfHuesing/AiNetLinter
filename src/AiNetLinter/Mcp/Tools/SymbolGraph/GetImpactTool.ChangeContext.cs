#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Core.Git;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
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
/// Assembly-Ziele verwenden denselben Symbol-Branch ueber die Assembly-Session und akzeptieren
/// keine Git-Referenz oder einen leeren Aufruf. Bewusst duenner Dispatch ohne eigene Analyse-/
/// Parsing-Logik.
/// </summary>
internal static partial class GetImpactTool
{
    private static async Task<CallToolResult> ExecuteChangeContextBranchAsync(
        ISolutionStateProvider state,
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
            if (GitRepositoryLocator.FindRoot(Path.GetDirectoryName(solution.FilePath) ?? "") is null)
            {
                return FormatChangeContextEmpty("not_git_repository");
            }
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
            return FormatChangeContextEmpty("invalid_ref", ex.Message, GitRefUnresolvableHint);
        }

        if (analysis is null)
        {
            return FormatChangeContextEmpty("clean_worktree");
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
        return McpToolResults.Text(BuildChangeContextText(payload, input.MaxResults));
    }

    internal static CallToolResult FormatChangeContextEmpty(string impactStatus, string? context = null, string? hint = null)
    {
        var payload = ChangeContextResponseMapper.BuildEmptyPayload(impactStatus);
        var text = impactStatus switch
        {
            "not_git_repository" => "Impact: kein Git-Repository am targetPath.",
            "invalid_ref" => "Impact: gitRef konnte nicht aufgeloest werden.",
            _ => "Impact: keine ungecommitten Aenderungen.",
        };
        if (impactStatus != "invalid_ref") return McpToolResults.Text(text);
        var recoverable = McpToolResults.Recoverable(LinterErrorCodes.AnalysisFailed, text, context, hint);
        return new CallToolResult
        {
            IsError = recoverable.IsError,
            Content = recoverable.Content,
        };
    }

    /// <summary>Eine solutionweite Violations-Stufe pro Aufruf — Config/Console beschafft der
    /// Tool-Zweig wie <c>get_violations</c> (atomarer Config-Schnappschuss, Server-Konsolen-Kanal).</summary>
    private static Task<DiffViolationScanResult> CollectDiffViolationsAsync(
        ISolutionStateProvider state,
        Solution solution,
        DiffImpactAnalysis analysis,
        DiffImpactCounters? counters,
        CancellationToken ct)
    {
        var configSnapshot = state.GetConfigSnapshot();
        return DiffViolationScanner.CollectAsync(new DiffViolationScanRequest(
            solution,
            state.GetConfigSnapshot().Config!,
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
            ? text
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
    int MaxTestsPerSymbol = ChangeContextContract.DefaultMaxTestsPerSymbol,
    bool IncludeReferences = false)
{
    public string? EffectiveSymbolIdentifier =>
        string.IsNullOrWhiteSpace(SymbolIdentifier) ? null : SymbolIdentifier;
}
