#nullable enable

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>safeguard</c>: liefert einen deterministischen 0-10-Quality-Score plus Pass/Fail-Threshold,
/// Top-Violations und Remediation-Hints fuer die resident gehaltene Solution. Bewusst duenner Dispatch
/// auf <see cref="SafeguardScanner.ComputeScoreAsync(SafeguardScannerParameters)"/> — Score-Berechnung
/// und Komponentenaggregation liegen komplett im Scanner, dieser Wrapper kapselt nur den MCP-Layer
/// (State-Management, JSON-Serialisierung, IsError-Policy). <c>state.Console</c> wird an den Scanner
/// durchgereicht, damit <see cref="AiNetLinter.Core.LinterEngine"/> auf demselben Kanal loggt wie der
/// MCP-Server selbst (nicht stdout, wo es mit dem stdio-MCP-Verkehr kollidieren wuerde).
///
/// IsError-Policy (siehe <c>src/AiNetLinter/Mcp/IsErrorPolicy.md</c>): IsError=true ausschliesslich bei
/// <c>SOLUTION_NOT_LOADED</c> (Pre-Scanner) und echter LinterEngine-Malfunction (Post-Scanner via
/// <see cref="SafeguardScoreResult.IsMalfunction"/>). Ein normaler Score-Output — auch mit
/// <c>Passed=false</c> — ist explizit IsError=false, weil ein Quality-Gate, das FAIL sagt, exakt
/// das ist, wofuer das Tool existiert (kein per Handlungsanleitung behebbarer Nutzerfehler).
/// </summary>
internal static class SafeguardTool
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    internal static Task<CallToolResult> ExecuteAsync(SafeguardToolParameters p)
        => ExecuteAsync(p.State, p.ScopeFilter, p.MinScore, p.MaxViolations, p.CancellationToken);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? scopeFilter, double minScore,
        int maxViolations, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var configSnapshot = state.GetConfigSnapshot();
        var result = await SafeguardScanner.ComputeScoreAsync(new SafeguardScannerParameters(
            Solution: solution,
            Config: configSnapshot.Config,
            Console: state.Console,
            ScopeFilter: scopeFilter,
            CancellationToken: ct,
            MinScoreThreshold: Math.Clamp(minScore, 0.0, 10.0),
            MaxRemediationEntries: Math.Max(0, maxViolations)));

        if (result.IsMalfunction)
        {
            return McpToolResults.Error(
                LinterErrorCodes.AnalysisFailed,
                "Unerwarteter Fehler bei der Safeguard-Berechnung.",
                context: result.Context,
                hint: "Einmal erneut versuchen — bleibt der Fehler bestehen, LinterEngine-Log pruefen.");
        }

        var score = result.Score!;
        var text = $"{score.Summary}\n\n" +
            "[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten " +
            "Scope — kein zusaetzliches Read/Grep noetig.";
        return new CallToolResult
        {
            IsError = false,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            StructuredContent = JsonSerializer.SerializeToElement(score, SerializerOptions),
        };
    }
}

/// <summary>
/// Parameter-Record fuer <see cref="SafeguardTool.ExecuteAsync"/>. Kapselt 5 Konfigurations-Eingaenge
/// in einem Record, damit <c>MaxMethodParameterCount: 4</c> (siehe <c>AiNetLinter.mdc</c>) eingehalten
/// wird (Pattern 1:1 von <see cref="SafeguardScannerParameters"/>).
/// </summary>
internal sealed record SafeguardToolParameters(
    McpCodeGraphServer State,
    string? ScopeFilter,
    double MinScore,
    int MaxViolations,
    CancellationToken CancellationToken);
