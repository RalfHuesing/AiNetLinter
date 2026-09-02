#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
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
        ISolutionStateProvider state, string? symbolIdentifier, int maxResults, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrEmpty(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: \"T:Namespace.Klasse\", \"Datei.cs:10:5\" oder \"Klasse\".");
        }

        var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, symbolIdentifier, ct, state.AssemblySymbolIdentity);
        if (error is not null) return error;

        if (symbol is not INamedTypeSymbol type)
        {
            return McpToolResults.InvalidArgument(
                $"'{symbolIdentifier}' loest zu '{symbol!.Kind}' auf, nicht zu einem Typ (Klasse/Interface/Struct).");
        }

        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
        var payload = await GetTypeHierarchyFormatter.BuildHierarchyAsync(type, solution, normalizedMaxResults, ct);
        var text = GetTypeHierarchyFormatter.FormatText(payload);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        // Basisklassen/Interfaces trunkieren nie (durch die Deklaration des Typs selbst begrenzt),
        // aber abgeleitete/implementierende Typen sind transitiv ueber die gesamte Solution
        // aufgeloest und koennen bei weit verbreiteten Basistypen/Interfaces (z. B. IDisposable)
        // das maxResults-Limit ueberschreiten — Sufficiency-Hinweis daher nur im nicht-trunkierten
        // Fall (analog zu FindReferencesTool/GetViolationsTool).
        var finalText = payload.SubtypesTruncated ? text : McpSufficiencyHints.Append(text);
        return McpToolResults.Text(FindSymbolTool.PrependWarning(warning, finalText), payload);
    }
}
