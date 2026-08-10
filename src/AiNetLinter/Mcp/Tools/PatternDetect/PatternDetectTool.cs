#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.PatternDetect;

/// <summary>
/// MCP-Tool <c>pattern_detect</c>: gruppiert die aktuellen Lint-Regelverstoesse der resident
/// gehaltenen Solution nach Pattern-Kategorie (<see cref="PatternCatalog"/>) statt der flachen
/// Datei-für-Datei-Liste von <c>get_violations</c> — Solution-weite Audit-Sicht ("finde alle
/// God-Classes/async-void/..."). Bewusst duenner Dispatch auf
/// <see cref="PatternDetectScanner.BuildReportAsync"/>: Parameter-Validierung (unbekannte
/// pattern-IDs) hier (analog <see cref="MetricsTree.MetricsTreeTool.ExecuteAsync"/>), Scan-/
/// Aggregationslogik im Scanner. <c>state.Console</c> wird an den Scanner durchgereicht, damit
/// <see cref="AiNetLinter.Core.LinterEngine"/> auf demselben Kanal loggt wie der MCP-Server
/// selbst (nicht stdout).
/// </summary>
internal static class PatternDetectTool
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, IReadOnlyList<string>? patterns, string? scopeFilter,
        int maxResultsPerPattern, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (resolvedPatterns, error) = ResolvePatterns(patterns);
        if (error is not null) return error;

        var configSnapshot = state.GetConfigSnapshot();
        var result = await PatternDetectScanner.BuildReportAsync(new PatternDetectScannerParameters(
            Solution: solution,
            Config: configSnapshot.Config,
            Console: state.Console,
            ScopeFilter: scopeFilter,
            Patterns: resolvedPatterns!,
            CancellationToken: ct,
            MaxResultsPerPattern: Math.Max(1, maxResultsPerPattern)));

        // Echte Malfunction (unerwartete Exception in der LinterEngine) -> IsError=true mit
        // Retry-once-Hinweis, siehe IsErrorPolicy.md — Pattern 1:1 von GetViolationsTool.
        if (result.IsMalfunction)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler bei der Pattern-Analyse.",
                context: result.Context,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen (workspace-load-Diagnosen?).");
        }

        // "Keine Dateien im Scope" hat keinen strukturierten Payload (kein Report gebaut) —
        // Text-only, analog get_violations. Der normale Report traegt sowohl Text als auch
        // StructuredContent (S1.3-Praezedenzfall, siehe SafeguardTool).
        if (result.Payload is null)
        {
            return McpToolResults.Text(result.Text!);
        }

        var text = McpSufficiencyHints.Append(result.Text!);
        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            StructuredContent = JsonSerializer.SerializeToElement(result.Payload, SerializerOptions),
        };
    }

    /// <summary>
    /// <c>null</c> oder leere Liste = alle Patterns (Default). Unbekannte pattern-IDs sind ein
    /// recoverable Fehler (IsError=false, konkrete Handlungsanleitung mit den gueltigen IDs im
    /// Text) statt stillschweigendem Ignorieren — ein Tippfehler soll nicht kommentarlos 0
    /// Treffer fuer das gemeinte Pattern liefern.
    /// </summary>
    private static (IReadOnlyList<PatternDefinition>? Patterns, CallToolResult? Error) ResolvePatterns(
        IReadOnlyList<string>? requested)
    {
        if (requested is null || requested.Count == 0) return (PatternCatalog.Patterns, null);

        var unknown = requested
            .Where(id => !PatternCatalog.Patterns.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (unknown.Count > 0)
        {
            var validIds = string.Join(", ", PatternCatalog.Patterns.Select(p => p.Id));
            return (null, McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Unbekannte pattern-ID(s): {string.Join(", ", unknown)}.",
                hint: $"Gueltige Werte: {validIds}."));
        }

        var selected = PatternCatalog.Patterns
            .Where(p => requested.Any(id => string.Equals(id, p.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        return (selected, null);
    }
}
