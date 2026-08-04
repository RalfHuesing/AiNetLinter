#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

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
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string typeIdentifier, CancellationToken ct)
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

        var text = await GetTypeHierarchyFormatter.BuildHierarchyTextAsync(type, solution, ct);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, text));
    }
}
