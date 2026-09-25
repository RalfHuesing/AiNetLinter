#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Verify;

internal static class GetVerifyAdvisoriesTool
{
    internal const string ToolName = "get_verify_advisories";
    internal const string DeadCodeCategory = "dead_code";
    internal const int ResponseBudgetBytes = 65_536;
    private const int UnboundedCandidateLimit = int.MaxValue;

    internal static CallToolResult InvalidCategory(string? category) => VerifyResponseFormatter.Error(
        "INVALID_ARGUMENT",
        $"category muss dead_code sein; erhalten: {category ?? "(fehlend)"}.",
        "category auf dead_code setzen.",
        "$.category");

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer server,
        CancellationToken cancellationToken)
    {
        if (server.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = server.GetCurrentSolution();
        if (solution is null) return VerifyResponseFormatter.Error(
            "ANALYSIS_FAILURE", "Die Source-Solution steht für den Advisory-Scan nicht bereit.", "Erneut aufrufen, sobald der Server die Solution geladen hat.");

        var configSnapshot = server.GetConfigSnapshot();
        if (configSnapshot.Config is null) return VerifyResponseFormatter.Error(
            "NOT_CONFIGURED", "Die Regelkonfiguration ist für den Advisory-Scan nicht verfügbar.", "Die Solution-Konfiguration prüfen und den Aufruf erneut starten.");
        if (configSnapshot.Config is not Config config) return VerifyResponseFormatter.Error(
            "ANALYSIS_FAILURE", "Die Dead-Code-Konfiguration konnte nicht gelesen werden.", "Konfiguration neu laden und den identischen Aufruf wiederholen.");

        var apiSurfaceIssues = DeadCodeAdvisoryScanner.ValidateApiSurface(solution, null, config);
        if (apiSurfaceIssues.Count > 0) return VerifyResponseFormatter.ApiSurfaceNotConfigured(apiSurfaceIssues);

        DeadCodeScanResult scan;
        try
        {
            scan = await DeadCodeAdvisoryScanner.ScanAsync(
                solution,
                new DeadCodeAdvisoryOptions(
                    Accessibility: DeadCodeAccessibilityFilter.All,
                    Confidence: DeadCodeConfidenceFilter.Both,
                    Kind: DeadCodeKindFilter.All,
                    IncludeTests: false,
                    Mode: DeadCodeMode.Members,
                    MaxResults: UnboundedCandidateLimit,
                    Config: config,
                    HandoffIdentity: server.HandoffSymbolIdentity),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return VerifyResponseFormatter.Error(
                "ANALYSIS_FAILURE",
                "Der Dead-Code-Advisory-Scan konnte nicht abgeschlossen werden.",
                $"Ursache: {exception.GetType().Name}. Scan nach Behebung erneut starten.");
        }

        return Render(scan);
    }

    internal static CallToolResult Render(DeadCodeScanResult scan)
    {
        var candidates = scan.DeadSymbols
            .OrderBy(entry => entry.Confidence == "high" ? 0 : 1)
            .ThenBy(entry => entry.ProjectName, StringComparer.Ordinal)
            .ThenBy(entry => entry.File, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.File, StringComparer.Ordinal)
            .ThenBy(entry => entry.Line)
            .ThenBy(entry => entry.InternalSymbolIdentifier, StringComparer.Ordinal)
            .ToArray();
        var candidateCount = scan.Summary.TotalDead;
        var selected = new List<DeadCodeEntry>();

        foreach (var candidate in candidates)
        {
            var trial = selected.Append(candidate).ToArray();
            var rendered = RenderText(scan, trial, candidateCount - trial.Length);
            if (Encoding.UTF8.GetByteCount(rendered) > ResponseBudgetBytes) break;
            selected.Add(candidate);
        }

        var omitted = candidateCount - selected.Count;
        var text = RenderText(scan, selected, omitted);
        if (Encoding.UTF8.GetByteCount(text) > ResponseBudgetBytes || (candidateCount > 0 && selected.Count == 0))
        {
            return VerifyResponseFormatter.Error(
                "RESPONSE_TOO_LARGE",
                "Die Advisory-Kopfzeile und mindestens ein vollständiger Eintrag passen nicht in das feste Antwortlimit.",
                "Symbol- und Pfadlängen der Solution prüfen; es wurde keine leere oder vollständige Liste behauptet.");
        }

        return McpToolResults.Text(text);
    }

    private static string RenderText(DeadCodeScanResult scan, IReadOnlyList<DeadCodeEntry> entries, int truncatedBy)
    {
        var summary = scan.Summary;
        var status = summary.Undecidable > 0 || scan.IsTruncated ? "partial" : "complete";
        var lines = new List<string>
        {
            $"status={status}; candidates={summary.TotalDead}; testOnly={scan.DeadSymbols.Count(entry => entry.Usage == "test_only")}; unreferenced={scan.DeadSymbols.Count(entry => entry.Usage == "unreferenced")}; apiProtected={summary.ApiProtected}; undecidable={summary.Undecidable}; shown={entries.Count}; truncatedBy={truncatedBy}",
            "columns: line | symbol | symbolIdentifier | usage | confidence",
        };
        if (truncatedBy > 0) lines.Add("truncated: weitere Kandidaten werden wegen des 65.536-Byte-Limits nicht angezeigt.");

        string? currentProject = null;
        string? currentFile = null;
        foreach (var entry in entries)
        {
            var project = entry.ProjectName ?? "?";
            if (!string.Equals(currentProject, project, StringComparison.Ordinal))
            {
                lines.Add($"project: {Clean(project)}");
                currentProject = project;
                currentFile = null;
            }

            if (!string.Equals(currentFile, entry.File, StringComparison.Ordinal))
            {
                lines.Add($"file: {Clean(entry.File)}");
                currentFile = entry.File;
            }

            var symbol = string.IsNullOrEmpty(entry.ContainerType)
                ? entry.SymbolName
                : $"{ShortTypeName(entry.ContainerType)}.{entry.SymbolName}";
            var identifier = entry.InternalSymbolIdentifier is { Length: > 0 } internalIdentifier
                ? HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(internalIdentifier)
                : string.Empty;
            lines.Add($"{entry.Line} | {Clean(symbol)} | {identifier} | {entry.Usage} | {entry.Confidence}");
        }

        return string.Join('\n', lines);
    }

    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private static string ShortTypeName(string value)
    {
        var separator = value.LastIndexOf('.');
        return separator >= 0 ? value[(separator + 1)..] : value;
    }
}
