#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class InspectAssemblyTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        InspectAssemblyArguments arguments) =>
        !AssemblyPaging.TryReadBoundOffset(
            arguments.Cursor,
            AssemblyPaging.CreateInspectBinding(lease.CanonicalPath, lease.Context.Origin.ContentHash, arguments),
            out _)
            ? Task.FromResult(McpToolResults.InvalidArgument(
                "cursor/continuationToken ist nicht an Target, Assembly-Hash und Abfrage gebunden oder abgelaufen.",
                "den zuletzt gelieferten continuationToken unverändert mit derselben Abfrage wiederverwenden."))
            :
        AssemblyAnalysisToolSupport.ExecuteLeaseAsync(
            lease,
            arguments,
            arguments.MaxResults,
            (fullPath, context, buildArguments, maxResults, activeLease) =>
                InspectAssemblyResponseBuilder.Build(new InspectAssemblyBuildRequest(
                    fullPath,
                    context,
                    buildArguments,
                    maxResults,
                    activeLease)));
}
