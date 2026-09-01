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
        AssemblyAnalysisToolSupport.ExecuteLeaseAsync(
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
