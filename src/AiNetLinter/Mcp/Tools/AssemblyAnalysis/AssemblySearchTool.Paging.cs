#nullable enable

using System.Threading;
using AiNetLinter.Mcp.Assemblies.Analysis.References;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static partial class AssemblySearchTool
{
    private static string CreatePagingBinding(AssemblyAnalysisLease lease, AssemblySearchArguments arguments) =>
        AssemblyPaging.CreateSearchBinding(lease.CanonicalPath, lease.Context.Origin.ContentHash, arguments);

    private static AssemblySearchPayload BuildPayloadWithBinding(
        string root,
        AssemblySearchArguments arguments,
        string binding,
        CancellationToken cancellationToken)
    {
        var payload = Scan(root, arguments, cancellationToken);
        return payload with
        {
            ContinuationToken = payload.ContinuationToken is null
                ? null
                : AssemblyPaging.CreateToken(
                    AssemblyPaging.ReadOffset(payload.ContinuationToken),
                    binding),
        };
    }
}
