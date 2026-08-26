#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Output;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Extrahiert Skeleton-Informationen für Dokumente.
/// </summary>
internal static class SkeletonMapBuilder
{
    /// <summary>
    /// Extrahiert die Skeleton-Typen eines einzelnen Dokuments. Wird von
    /// <see cref="AiNetLinter.Mcp.Tools.GetFileSkeletonTool"/> (MCP) fuer die Einzeldatei-Extraktion
    /// verwendet.
    /// </summary>
    internal static async Task<IReadOnlyList<SkeletonTypeInfo>> ExtractFromDocumentAsync(
        Document document,
        string solutionDir,
        CancellationToken ct = default)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel == null) return [];

        var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath ?? document.Name);
        var walker = new SkeletonSyntaxWalker(semanticModel, relativePath);
        var root = await semanticModel.SyntaxTree.GetRootAsync(ct);
        walker.Visit(root);
        return walker.Types;
    }
}
