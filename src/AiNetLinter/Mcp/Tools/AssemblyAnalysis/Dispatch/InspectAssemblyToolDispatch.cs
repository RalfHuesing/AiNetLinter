#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;

internal static class InspectAssemblyToolDispatch
{
    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken cancellationToken) =>
        AssemblyAnalysisToolSupport.ExecuteAsync(
            CreateParameters(state, arguments, cancellationToken));


    private static AssemblyToolExecutionParameters CreateParameters(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken cancellationToken) =>
        new(
            state,
            arguments.AssemblyPath,
            null,
            AssemblyAnalysisService.NormalizeLimit(arguments.MaxResults, 1, AssemblyAnalysisService.MaxResults),
            cancellationToken,
            (fullPath, context, maxResults) =>
            {
                var binding = AssemblyPaging.CreateInspectBinding(fullPath, context.Origin.ContentHash, arguments);
                return AssemblyPaging.TryReadBoundOffset(arguments.Cursor, binding, out _)
                    ? InspectAssemblyResponseBuilder.Build(
                        new InspectAssemblyBuildRequest(fullPath, context, arguments, maxResults, null))
                    : McpToolResults.InvalidArgument(
                        "cursor/continuationToken ist nicht an Target, Assembly-Hash und Abfrage gebunden oder abgelaufen.",
                        "den zuletzt gelieferten continuationToken unverändert mit derselben Abfrage wiederverwenden.");
            });
}
