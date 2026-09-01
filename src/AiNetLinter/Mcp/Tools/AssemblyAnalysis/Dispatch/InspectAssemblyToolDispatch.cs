#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Responses;
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

    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer? state,
        InspectAssemblyArguments arguments,
        CancellationToken cancellationToken,
        IAssemblySourceSelectionResolver sourceSelection) =>
        AssemblyAnalysisSourceToolSupport.ExecuteAsync(
            CreateParameters(state, arguments, cancellationToken),
            sourceSelection);

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
            (fullPath, context, maxResults) => InspectAssemblyResponseBuilder.Build(
                new InspectAssemblyBuildRequest(fullPath, context, arguments, maxResults, null)));
}
