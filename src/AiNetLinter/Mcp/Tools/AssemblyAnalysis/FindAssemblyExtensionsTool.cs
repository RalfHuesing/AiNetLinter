#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class FindAssemblyExtensionsTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        FindAssemblyExtensionsArguments arguments) =>
        AssemblyAnalysisResponseLimits.ValidateDetailLevel(arguments.DetailLevel) is { } detailLevelError
            ? Task.FromResult(detailLevelError)
            : !AssemblyPaging.TryReadBoundOffset(
            arguments.Cursor,
            AssemblyPaging.CreateExtensionsBinding(lease.CanonicalPath, lease.Context.Origin.ContentHash, arguments),
            out _)
            ? Task.FromResult(McpToolResults.InvalidArgument(
                "continuationToken ist nicht an Target, Assembly-Hash und Abfrage gebunden oder abgelaufen.",
                "den zuletzt gelieferten continuationToken unverändert mit derselben Abfrage wiederverwenden."))
            : AssemblyAnalysisToolSupport.ExecuteLeaseAsync(
            lease,
            arguments,
            arguments.MaxResults,
            (fullPath, context, buildArguments, maxResults, activeLease) =>
                FindAssemblyExtensionsResponseBuilder.Build(new FindAssemblyExtensionsBuildRequest(
                    fullPath,
                    context,
                    buildArguments,
                    maxResults,
                    activeLease)));
}
