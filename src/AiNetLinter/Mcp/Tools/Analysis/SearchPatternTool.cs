#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.Analysis;

/// <summary>
/// MCP-Tool <c>search_pattern</c>: Plain-Text- oder Regex-Suche ueber den Solution-Dateibestand
/// (alle Dateitypen, nicht nur C#) — Fallback fuer Namen/Strings, die kein C#-Symbol sind (z. B.
/// JS-Funktionen in .js, Razor-Komponenten in .razor, WPF-Elemente in .xaml, Konfigwerte in .html/
/// .css). Argument-Validierung lebt im Tool (nicht im Scanner), damit der Scanner reine Daten
/// bekommt und einfacher unit-testbar bleibt. Bewusst duenner Dispatch auf
/// <see cref="SearchPatternScanner.SearchAndFormat"/> — keine eigene Scan- oder Formatierungslogik
///, damit dieser Klasse eigener
/// <c>AIContextFootprint</c> (siehe <c> klein bleibt.
/// </summary>
internal static class SearchPatternTool
{
    /// <summary>
   /// Scannt die resident gehaltene Solution nach <paramref name="pattern"/>. Liefert bei
    /// ungueltiger Regex-Syntax (nur <paramref name="isRegex"/>=true) einen
    /// <c>INVALID_ARGUMENT</c>-Fehler statt zu crashen (Result-Pattern, siehe
    /// <c>. <see cref="Task.Run"/> umschliesst den
    /// CPU-/IO-bound Scan, damit der <c>McpCodeGraphServer</c>-Lock nicht unnoetig gehalten wird
    /// (siehe Plan: bewusst eingesetzt, <c>Task.Run</c> ist hier kein Ueber-Engineering).
    /// </summary>
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        string pattern,
        bool isRegex,
        int maxResults,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "pattern darf nicht leer sein.",
                hint: "Pattern angeben — leeres Pattern ist nicht erlaubt.");
        }

        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;

        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        string text;
        try
        {
            text = await Task.Run(
                () => SearchPatternScanner.SearchAndFormat(solution, pattern, isRegex, normalizedMaxResults),
                ct);
        }
        catch (ArgumentException ex)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Ungueltige Regex: {ex.Message}",
                hint: "Pruefe pattern auf gueltige Regex-Syntax.");
        }

        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, text));
    }
}
