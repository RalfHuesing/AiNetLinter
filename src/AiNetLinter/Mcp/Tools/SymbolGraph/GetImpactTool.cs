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
    private const string GitRefUnresolvableHint =
        "gitRef pruefen (z. B. via 'git log'/'git branch') oder ohne gitRef aufrufen fuer uncommittete Aenderungen.";

    internal static async Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        GetImpactInput input,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(input.EffectiveSymbolIdentifier))
        {
            return McpToolResults.InvalidArgument(
                "Assembly-Ziele benoetigen symbolIdentifier; gitRef-basierter Impact ist fuer Assemblies nicht verfuegbar.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        if (!string.IsNullOrEmpty(input.GitRef))
        {
            return McpToolResults.InvalidArgument(
                "gitRef ist fuer Assembly-Ziele nicht zulaessig; nur symbolIdentifier verwenden.",
                hint: McpToolResults.SymbolIdentifierHint);
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

        if (detailLevel == ChangeContextContract.DetailLevelChangeContext)
        {
            return McpToolResults.InvalidArgument(
                "detailLevel='change-context' ist nur im Git-Diff-Modus zulaessig und kann nicht " +
                "mit symbolIdentifier kombiniert werden.",
                hint: "Fuer den Kontext eines einzelnen Symbols get_feature_context nutzen.");
        }

        return await ExecuteAssemblySymbolBranchAsync(lease, input, ct).ConfigureAwait(false);
    }

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, GetImpactInput input, CancellationToken ct, DiffImpactCounters? counters = null)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();
        var hasGitRef = !string.IsNullOrEmpty(input.GitRef);
        var hasSymbolIdentifier = !string.IsNullOrEmpty(input.EffectiveSymbolIdentifier);
        var isAssemblyTarget = state.AssemblySymbolIdentity is not null;
        var targetError = ValidateTargetArguments(isAssemblyTarget, hasGitRef, hasSymbolIdentifier);
        if (targetError is not null) return targetError;

        var detailLevel = ResolveDetailLevel(input.DetailLevel);
        if (detailLevel is null)
        {
            return McpToolResults.InvalidArgument(
                $"Unbekannter detailLevel-Wert '{input.DetailLevel}' — erlaubt sind " +
                $"'{ChangeContextContract.DetailLevelCallers}' (Default) und " +
                $"'{ChangeContextContract.DetailLevelChangeContext}'.",
                hint: "detailLevel weglassen oder einen der erlaubten Werte uebergeben.");
        }

        var detailError = ValidateDetailLevel(detailLevel, hasSymbolIdentifier);
        if (detailError is not null) return detailError;
        if (detailLevel == ChangeContextContract.DetailLevelChangeContext
            && state.GetConfigSnapshot().Config is null)
        {
            return McpToolResults.NotConfigured(solution.FilePath);
        }

        return await ExecuteBranchAsync(
            state,
            solution,
            input,
            detailLevel,
            hasSymbolIdentifier,
            ct,
            counters);
    }

    private static CallToolResult? ValidateTargetArguments(
        bool isAssemblyTarget,
        bool hasGitRef,
        bool hasSymbolIdentifier)
    {
        if (isAssemblyTarget && !hasSymbolIdentifier)
        {
            return McpToolResults.InvalidArgument(
                "Assembly-Ziele benoetigen symbolIdentifier; gitRef-basierter Impact ist fuer Assemblies nicht verfuegbar.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        if (isAssemblyTarget && hasGitRef)
        {
            return McpToolResults.InvalidArgument(
                "gitRef ist fuer Assembly-Ziele nicht zulaessig; nur symbolIdentifier verwenden.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        return hasGitRef && hasSymbolIdentifier
            ? McpToolResults.InvalidArgument(
                "gitRef und symbolIdentifier sind gegenseitig exklusiv — genau einen angeben oder " +
                "beide weglassen fuer Git-Diff gegen uncommittete Aenderungen.",
                hint: "Entweder gitRef ODER symbolIdentifier angeben, nie beide.")
            : null;
    }

    private static CallToolResult? ValidateDetailLevel(string? detailLevel, bool hasSymbolIdentifier) =>
        detailLevel == ChangeContextContract.DetailLevelChangeContext && hasSymbolIdentifier
            ? McpToolResults.InvalidArgument(
                "detailLevel='change-context' ist nur im Git-Diff-Modus zulaessig und kann nicht " +
                "mit symbolIdentifier kombiniert werden.",
                hint: "Fuer den Kontext eines einzelnen Symbols get_feature_context nutzen.")
            : null;

    private static Task<CallToolResult> ExecuteBranchAsync(
        ISolutionStateProvider state,
        Solution solution,
        GetImpactInput input,
        string detailLevel,
        bool hasSymbolIdentifier,
        CancellationToken ct,
        DiffImpactCounters? counters) =>
        detailLevel == ChangeContextContract.DetailLevelChangeContext
            ? ExecuteChangeContextBranchAsync(state, solution, input, ct, counters)
            : hasSymbolIdentifier
                ? ExecuteSymbolBranchAsync(solution, input, state.HandoffSymbolIdentity, ct)
                : ExecuteGitRefBranchAsync(solution, input, ct);

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
        var symbolIdentifier = input.EffectiveSymbolIdentifier!;
        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, symbolIdentifier, ct, assemblyIdentity);
        if (error is not null) return error;

        var effectiveMax = input.MaxResults < 1 ? 1 : input.MaxResults;
        var traversal = await CallGraphTraversal.ExpandAsync(
            new ReferenceTraversalRequest(
                solution,
                symbol!,
                input.Depth,
                effectiveMax,
                ct,
                AssemblySymbolIdentity: assemblyIdentity));

        var testCoverage = await TestCoverageScanner.FindTestsForSymbolAsync(symbol!, solution, ct);
        var affectedProjects = TransitiveCallGraphFormatter.ResolveAffectedProjects(solution, symbol, traversal.CallSites);
        var formatted = TransitiveCallGraphFormatter.FormatResponse(
            traversal,
            traversal.Completeness.TotalCallSiteCount == 0
                ? $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'"
                : null);

        var finalBody = TransitiveCallGraphFormatter.FormatSymbolImpactText(
            symbol!, affectedProjects, testCoverage, formatted);

        var payload = new SymbolImpactPayload(
            traversal.CallSites,
            traversal.Completeness,
            affectedProjects,
            DetermineImpactStatus(traversal.CallSites.Count),
            new SymbolTestImpactDto(testCoverage.TotalMatchingTests, testCoverage.TestFiles.Count, testCoverage.TestFiles),
            traversal.Navigation,
            formatted.StructuredPayload.Handoff);

        return McpToolResults.Text(finalBody, payload);
    }

    private static async Task<CallToolResult> ExecuteAssemblySymbolBranchAsync(
        AssemblyAnalysisLease lease,
        GetImpactInput input,
        CancellationToken ct)
    {
        var symbolIdentifier = input.EffectiveSymbolIdentifier!;
        var (target, error, navigation) = await AssemblySymbolResolver.ResolveAsync(
            lease,
            symbolIdentifier,
            ct).ConfigureAwait(false);
        if (error is not null) return error;

        var traversal = await AssemblyReferenceNavigator.FindReferencesAsync(
            new AssemblyReferenceTraversalRequest(
                AssemblyNavigationSourceFactory.CreateSources(lease, target!),
                input.MaxResults,
                input.Depth,
                navigation,
                lease.CanonicalPath),
            ct).ConfigureAwait(false);

        var formatted = TransitiveCallGraphFormatter.FormatResponse(
            traversal,
            traversal.Completeness.TotalCallSiteCount == 0
                ? $"Keine Aufrufstellen gefunden fuer '{symbolIdentifier}'"
                : null);

        return McpToolResults.Text(formatted.Text, formatted.StructuredPayload);
    }

    private static async Task<CallToolResult> ExecuteGitRefBranchAsync(Solution solution, GetImpactInput input, CancellationToken ct)
    {
        var targetPath = Path.GetDirectoryName(solution.FilePath) ?? "";
        DiffImpactAnalysis? analysis;
        try
        {
            if (GitRepositoryLocator.FindRoot(targetPath) is null)
            {
                return FormatGitImpact(new GitImpactFormatRequest("not_git_repository", [], 0, 0));
            }

            analysis = await DiffImpactAnalyzer.AnalyzeDiffAsync(
                solution, targetPath, input.GitRef, verbose: false);
        }
        catch (GitDiffFailedException ex)
        {
            // Recoverable statt Error: eine nicht aufloesende gitRef ist ein behebbarer
            // Nutzereingabe-Fehler (Tippfehler, falscher Branch-Name), kein Tool-Malfunction —
            // siehe IsErrorPolicy.md.
            return FormatGitImpact(new GitImpactFormatRequest("invalid_ref", [], 0, 0, ex.Message, GitRefUnresolvableHint));
        }
        var effectiveMax = input.MaxResults < 1 ? 1 : input.MaxResults;

        if (analysis is null)
        {
            return FormatGitImpact(new GitImpactFormatRequest("clean_worktree", [], 0, 0));
        }

        var callSiteEntries = DiffImpactAnalyzer.ToCallSiteEntries(analysis.References);
        if (callSiteEntries.Count == 0)
        {
            return FormatGitImpact(new GitImpactFormatRequest("diff_without_callsite_impact", [], 0, 0));
        }
        var callSites = callSiteEntries.Select(DiffImpactAnalyzer.FormatCallSite).ToList();
        var finalText = McpTruncation.TruncateLines(callSites, callSiteEntries.Count, effectiveMax);
        var shownEntries = callSiteEntries.Count <= effectiveMax
            ? callSiteEntries
            : callSiteEntries.Take(effectiveMax).ToList();
        return McpToolResults.Text(
            $"Impact: statische Aufrufstellen gefunden.\n{finalText}",
            new GitImpactPayload("impact_found", shownEntries, callSiteEntries.Count, shownEntries.Count));
    }

    private static string DetermineImpactStatus(int callSiteCount) =>
        callSiteCount == 0 ? "diff_without_callsite_impact" : "impact_found";

    internal static CallToolResult FormatGitImpact(GitImpactFormatRequest request)
    {
        var text = request.ImpactStatus switch
        {
            "not_git_repository" => "Impact: kein Git-Repository am targetPath.",
            "invalid_ref" => "Impact: gitRef konnte nicht aufgeloest werden.",
            "clean_worktree" => "Impact: keine ungecommitten Aenderungen.",
            "diff_without_callsite_impact" => "Impact: Diff ohne statische Aufrufstellen-Auswirkung.",
            _ => "Impact: statische Aufrufstellen gefunden.",
        };
        var payload = new GitImpactPayload(request.ImpactStatus, request.CallSites, request.TotalCount, request.ShownCount);
        if (request.ImpactStatus != "invalid_ref") return McpToolResults.Text(text, payload);

        var recoverable = McpToolResults.Recoverable(
            LinterErrorCodes.AnalysisFailed, text, context: request.Context, hint: request.Hint);
        return new CallToolResult
        {
            IsError = recoverable.IsError,
            Content = recoverable.Content,
            StructuredContent = System.Text.Json.JsonSerializer.SerializeToElement(payload, McpJsonOptions.Default),
        };
    }

    /// <summary>
    /// Git-Diff-Zweig im breiten Scope mit strukturiertem Antwortvertrag: die deterministische
    /// Symbol-Kappung greift im Analyzer-Kern VOR der teuren Referenz-Stufe; danach laufen die
    /// gebatchte Testzuordnung und die solutionweit-diffbezogene Violations-Stufe ueber denselben
    /// Zaehler-Kanal. Die Antwort ist immer ein strukturiertes Objekt — auch "kein Repo / leerer
    /// Diff" liefert eine leere, aber vertragsgueltige Struktur samt Sufficiency-Hinweis.
    /// </summary>
}

internal sealed record GitImpactFormatRequest(
    string ImpactStatus,
    IReadOnlyList<CallSiteEntry> CallSites,
    int TotalCount,
    int ShownCount,
    string? Context = null,
    string? Hint = null);
