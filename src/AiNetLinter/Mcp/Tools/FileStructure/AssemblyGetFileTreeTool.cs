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

        // The assembly route has its own configured wire budget. Apply it while the file-tree
        // scanner still owns the collection so it can retain a usable prefix instead of letting
        // the generic assembly envelope projection discard every file entry.
        var effectiveInput = input.MaxResponseBytes > 0
            ? input
            : input with { MaxResponseBytes = lease.Context.ResponseBudgetBytes };
        return GetFileTreeTool.ExecutePhysicalAsync(root, effectiveInput, cancellationToken);
    }

    internal static string? ResolveRoot(AssemblyAnalysisLease lease)
    {
        var generatedRoot = lease.Context.DecompiledProjectPaths?.DecompiledSourceRoot;
        return !string.IsNullOrWhiteSpace(generatedRoot) && Directory.Exists(generatedRoot)
            ? generatedRoot
            : null;
    }
}
