#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// MCP-Tool <c>get_type_hierarchy</c>: loest einen Typ-Identifikator (Datei:Zeile:Spalte oder
/// qualifizierter/teil-qualifizierter Name) ueber <see cref="FindReferencesTool.ResolveSymbolAsync"/>
/// zu einem Symbol auf, prueft, dass es ein Typ ist (Klasse/Interface/Struct), und delegiert an
/// <see cref="GetTypeHierarchyFormatter.BuildHierarchyTextAsync"/> fuer die eigentliche
/// Traversierung/Formatierung. Bewusst duenner Dispatch ohne eigene Traversierungs-/
/// Formatierungslogik. Deckt nur.cs-Dateien ab.
/// </summary>
internal static class GetTypeHierarchyTool
{
    internal const int DefaultMaxResults = 50;

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string typeIdentifier, int maxResults, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, typeIdentifier, ct);
        if (error is not null) return error;

        if (symbol is not INamedTypeSymbol type)
        {
            return McpToolResults.InvalidArgument(
                $"'{typeIdentifier}' loest zu '{symbol!.Kind}' auf, nicht zu einem Typ (Klasse/Interface/Struct).");
        }

        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
        var (text, isTruncated) = await GetTypeHierarchyFormatter.BuildHierarchyTextAsync(type, solution, normalizedMaxResults, ct);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        // Basisklassen/Interfaces trunkieren nie (durch die Deklaration des Typs selbst begrenzt),
        // aber abgeleitete/implementierende Typen sind transitiv ueber die gesamte Solution
        // aufgeloest und koennen bei weit verbreiteten Basistypen/Interfaces (z. B. IDisposable)
        // das maxResults-Limit ueberschreiten — Sufficiency-Hinweis daher nur im nicht-trunkierten
        // Fall (Q5-Muster, analog zu FindReferencesTool/GetViolationsTool).
        var finalText = isTruncated ? text : McpSufficiencyHints.Append(text);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, finalText));
    }
}
