#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

internal static class AssemblyGetFileTreeTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        AssemblyAnalysisLease lease,
        GetFileTreeInput input,
        CancellationToken cancellationToken)
    {
        var root = ResolveRoot(lease);
        if (root is null)
        {
            return Task.FromResult(McpToolResults.Recoverable(
                LinterErrorCodes.AssemblyTargetUnsupported,
                "Für diese Assembly ist kein lokaler Source- oder dekompilierter SourceRoot verfügbar.",
                context: lease.CanonicalPath,
                hint: "Source-Zuordnung oder dekompilierte Projektpfade bereitstellen; alternativ get_class_structure verwenden."));
        }

        return GetFileTreeTool.ExecuteAsync(root, input, cancellationToken);
    }

    private static string? ResolveRoot(AssemblyAnalysisLease lease)
    {
        var generatedRoot = lease.Context.DecompiledProjectPaths?.DecompiledSourceRoot;
        if (!string.IsNullOrWhiteSpace(generatedRoot) && Directory.Exists(generatedRoot))
        {
            return generatedRoot;
        }

        var sourceProject = lease.Context.Origin.SourceProjectPath;
        if (string.IsNullOrWhiteSpace(sourceProject)) return null;
        var sourceRoot = Path.GetDirectoryName(sourceProject);
        return !string.IsNullOrWhiteSpace(sourceRoot) && Directory.Exists(sourceRoot)
            ? sourceRoot
            : null;
    }
}
