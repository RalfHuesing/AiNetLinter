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
