#nullable enable

using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.Common;
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
    internal const int DefaultMaxResponseBytes = 24 * 1024;

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
        if (!McpResponseBudgetLimits.IsPublicBudget(request.MaxResponseBytes))
        {
            return InvalidResponseBudget();
        }
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
        return ApplyResponseBudget(payload, request.MaxResponseBytes);
    }

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (result.StructuredContent is not { } structured
            || result.Content.OfType<TextContentBlock>().FirstOrDefault() is not { } textBlock) return result;
        // Navigation is added to recoverable errors too.  They are already compact
        // and do not have the hierarchy shape required by this projector.
        if (!structured.TryGetProperty("baseTypes", out _)
            || !structured.TryGetProperty("subtypes", out _)) return result;
        var payload = JsonSerializer.Deserialize<TypeHierarchyPayload>(structured.GetRawText(), McpJsonOptions.Default);
        if (payload is null) return result;
        var originalBody = GetTypeHierarchyFormatter.FormatText(payload);
        var projected = ApplyResponseBudget(payload, maxResponseBytes);
        if (projected.IsError == true || projected.StructuredContent is not { } projectedStructured) return projected;

        var root = JsonNode.Parse(projectedStructured.GetRawText())!.AsObject();
        var originalRoot = JsonNode.Parse(structured.GetRawText())!.AsObject();
        if (originalRoot["navigation"] is { } navigation) root["navigation"] = navigation.DeepClone();
        var projectedBody = projected.Content.OfType<TextContentBlock>().First().Text;
        var suffix = textBlock.Text.StartsWith(originalBody, System.StringComparison.Ordinal)
            ? textBlock.Text[originalBody.Length..]
            : string.Empty;
        var finalResult = new CallToolResult
        {
            Content = [new TextContentBlock { Text = projectedBody + suffix }],
            StructuredContent = JsonSerializer.SerializeToElement(root, McpJsonOptions.Default),
        };
        return Mcp.Wire.McpResponseSize.From(finalResult).TotalBytes <= maxResponseBytes
            ? finalResult
            : BudgetTooSmall(maxResponseBytes);
    }

    private static CallToolResult ApplyResponseBudget(TypeHierarchyPayload payload, int maxResponseBytes)
    {
        var current = payload;
        while (CombinedBytes(current) > maxResponseBytes && current.DiRegistrations.Count > 0)
        {
            current = current with { DiRegistrations = current.DiRegistrations.Take(current.DiRegistrations.Count - 1).ToList() };
        }
        while (CombinedBytes(current) > maxResponseBytes && current.Subtypes.Count > 0)
        {
            var subtypes = current.Subtypes.Take(current.Subtypes.Count - 1).ToList();
            current = current with
            {
                Subtypes = subtypes,
                ShownSubtypeCount = subtypes.Count,
                SubtypesTruncated = true,
                SubtypesTruncatedBy = current.SubtypesTruncatedBy.Contains("maxResponseBytes", System.StringComparer.Ordinal)
                    ? current.SubtypesTruncatedBy
                    : current.SubtypesTruncatedBy.Append("maxResponseBytes").ToList(),
            };
        }
        var text = GetTypeHierarchyFormatter.FormatText(current);
        if (Encoding.UTF8.GetByteCount(text) + JsonSerializer.SerializeToUtf8Bytes(current, McpJsonOptions.Default).Length > maxResponseBytes)
        {
            return BudgetTooSmall(maxResponseBytes);
        }
        return McpToolResults.Text(text, current);
    }

    private static int CombinedBytes(TypeHierarchyPayload payload)
    {
        var text = GetTypeHierarchyFormatter.FormatText(payload);
        return Encoding.UTF8.GetByteCount(text) + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;
    }

    private static CallToolResult InvalidResponseBudget() =>
        McpToolResults.InvalidArgument(
            $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
            "maxResponseBytes weglassen oder einen Wert innerhalb dieses Bereichs setzen.",
            "$.maxResponseBytes");

    private static CallToolResult BudgetTooSmall(int budget) => McpToolResults.Error(
        LinterErrorCodes.ResponseBudgetTooSmall,
        $"maxResponseBytes={budget} ist zu klein für die vollständige minimale Typ-Hierarchie.",
        new McpErrorParameters(Hint: "maxResponseBytes erhöhen; Hierarchie-Einträge werden nur vollständig gekürzt.", FieldPath: "$.maxResponseBytes"));
}
