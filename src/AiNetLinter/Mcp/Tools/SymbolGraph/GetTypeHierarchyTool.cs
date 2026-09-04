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
        ISolutionStateProvider state, string? symbolIdentifier, int maxResults, CancellationToken ct, string? symbol = null)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
        if (string.IsNullOrEmpty(effectiveIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: \"T:Namespace.Klasse\", \"Datei.cs:10:5\" oder \"Klasse\".");
        }

        var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, effectiveIdentifier, ct, state.AssemblySymbolIdentity);
        if (error is not null) return error;

        if (resolvedSymbol is not INamedTypeSymbol type)
        {
            return McpToolResults.InvalidArgument(
                $"'{effectiveIdentifier}' loest zu '{resolvedSymbol!.Kind}' auf, nicht zu einem Typ (Klasse/Interface/Struct).");
        }

        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
        var payload = await GetTypeHierarchyFormatter.BuildHierarchyAsync(
            type,
            solution,
            normalizedMaxResults,
            ct,
            absolutePaths: state.AssemblySymbolIdentity is not null);
        var text = GetTypeHierarchyFormatter.FormatText(payload);
        // Basisklassen/Interfaces trunkieren nie (durch die Deklaration des Typs selbst begrenzt),
        // aber abgeleitete/implementierende Typen sind transitiv ueber die gesamte Solution
        // aufgeloest und koennen bei weit verbreiteten Basistypen/Interfaces (z. B. IDisposable)
        // das maxResults-Limit ueberschreiten — Sufficiency-Hinweis daher nur im nicht-trunkierten
        // Fall (analog zu FindReferencesTool/GetViolationsTool).
        var finalText = payload.SubtypesTruncated ? text : McpSufficiencyHints.Append(text);
        return McpToolResults.Text(finalText, payload);
    }
}
