#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.CallTree;

internal sealed record AssemblyGetCallTreeRequest(
    GetCallTreeInput Input,
    bool IncludeReferences);

internal static class AssemblyGetCallTreeTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        AssemblyGetCallTreeRequest request,
        CancellationToken cancellationToken) =>
        request.IncludeReferences
            ? ExecuteWithReferencesAsync(lease, request.Input, cancellationToken)
            : GetCallTreeTool.ExecuteAsync(
                lease.Server,
                request.Input,
                cancellationToken);

    private static async Task<CallToolResult> ExecuteWithReferencesAsync(
        AssemblyAnalysisLease lease,
        GetCallTreeInput input,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateInput(input);
        if (validationError is not null) return validationError;

        try
        {
            return await BuildResponseAsync(lease, input, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_call_tree: {exception.Message}",
                context: $"{input.SymbolIdentifier}; includeReferences=true");
        }
    }

    private static CallToolResult? ValidateInput(GetCallTreeInput input)
    {
        if (string.IsNullOrEmpty(input.SymbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: McpToolResults.SymbolIdentifierHint);
        }

        return GetCallTreeTool.TryParseDirection(input.Direction, out _)
            ? null
            : McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Ungueltiger Wert fuer 'direction': '{input.Direction}'.",
                hint: "direction muss 'incoming', 'outgoing' oder 'both' sein.");
    }

    private static async Task<CallToolResult> BuildResponseAsync(
        AssemblyAnalysisLease lease,
        GetCallTreeInput input,
        CancellationToken cancellationToken)
    {
        var (target, error, navigation) = await AssemblySymbolResolver.ResolveAsync(
            lease,
            input.SymbolIdentifier!,
            cancellationToken).ConfigureAwait(false);
        if (error is not null) return error;

        var (root, truncated, diagnostics) = await AssemblyReferenceNavigator.BuildCallTreeAsync(
            AssemblyNavigationSourceFactory.CreateSources(lease, target!),
            target!.Symbol,
            input,
            cancellationToken).ConfigureAwait(false);
        var topN = input.TopN < 1 ? 1 : input.TopN;
        var body = GetCallTreeTool.RenderTree(root, input.Format, topN);
        var topNTruncated = GetCallTreeTool.HasTreeOverflow(root, topN);
        var treeTruncationMessage = truncated
            ? BuildTruncationMeta()
            : topNTruncated
                ? BuildTopNTruncationMeta()
                : null;
        return TransitiveCallGraphFormatter.FormatAssemblyCallTreeResponse(
            new AssemblyCallTreeResponseRequest(
                root,
                body,
                navigation,
                diagnostics,
                truncated,
                topNTruncated,
                treeTruncationMessage));
    }

    private static string BuildTruncationMeta() =>
        $"[Baum trunkiert — hard-cap {CallGraphTreeBuilder.MaxCallTreeNodes} Knoten erreicht, " +
        "depth oder topN reduzieren fuer einen vollstaendigeren Teilbaum]";

    private static string BuildTopNTruncationMeta() =>
        "[Baum trunkiert — mindestens eine Ebene hat mehr Kinder als topN, siehe " +
        "\"... und N weitere\"-Zeilen; topN erhoehen fuer einen vollstaendigeren Teilbaum]";
}
