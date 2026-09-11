#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
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
        => await ExecuteAsync(
            new GetTypeHierarchyRequest(
                state,
                symbolIdentifier,
                maxResults,
                McpScopeType.All,
                IncludeGenerated: false,
                CancellationToken: ct)).ConfigureAwait(false);

    internal static async Task<CallToolResult> ExecuteAsync(
        GetTypeHierarchyRequest request)
    {
        var state = request.State;
        var symbolIdentifier = request.SymbolIdentifier;
        var maxResults = request.MaxResults;
        var scopeType = request.ScopeType;
        var includeGenerated = request.IncludeGenerated;
        var ct = request.CancellationToken;
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

        var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, symbolIdentifier, ct, state.HandoffSymbolIdentity);
        if (error is not null) return error;

        if (resolvedSymbol is not INamedTypeSymbol type)
        {
            return McpToolResults.InvalidArgument(
                $"'{symbolIdentifier}' loest zu '{resolvedSymbol!.Kind}' auf, nicht zu einem Typ (Klasse/Interface/Struct).");
        }

        var normalizedMaxResults = maxResults < 1 ? 1 : maxResults;
        var payload = await GetTypeHierarchyFormatter.BuildHierarchyAsync(
            type,
            solution,
            normalizedMaxResults,
            ct,
            new HierarchyBuildOptions(
                AbsolutePaths: state.AssemblySymbolIdentity is not null,
                HandoffIdentity: state.HandoffSymbolIdentity,
                ScopeType: scopeType,
                IncludeGenerated: includeGenerated,
                ScopeClassifier: new McpScopeClassifier()));
        var text = GetTypeHierarchyFormatter.FormatText(payload);
        // Basisklassen/Interfaces trunkieren nie (durch die Deklaration des Typs selbst begrenzt),
        // aber abgeleitete/implementierende Typen sind transitiv ueber die gesamte Solution
        // aufgeloest und koennen bei weit verbreiteten Basistypen/Interfaces (z. B. IDisposable)
        // das maxResults-Limit ueberschreiten — Sufficiency-Hinweis daher nur im nicht-trunkierten
        // Fall (analog zu FindReferencesTool/GetViolationsTool).
        var finalText = text;
        return McpToolResults.Text(finalText, payload);
    }
}
