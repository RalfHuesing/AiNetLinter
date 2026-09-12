#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Core;
using AiNetLinter.Core.Documents;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.Common;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// MCP-Tool <c>get_file_skeleton</c>: liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies)
/// einer oder mehrerer C#-Dateien per relativem (oder absolutem) Dateipfad.
/// Erwartet ausschliesslich das <c>filePaths</c>-Array; eine einzelne Datei ist ein Array-Eintrag.
/// </summary>
internal static class GetFileSkeletonTool
{
    internal const int DefaultMaxResponseBytes = 24 * 1024;
    internal static Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, string[]? filePaths, CancellationToken ct) =>
        ExecuteAsync(state, filePaths, DefaultMaxResponseBytes, ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, string[]? filePaths, int maxResponseBytes, CancellationToken ct)
    {
        if (!McpResponseBudgetLimits.IsPublicBudget(maxResponseBytes))
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumContentBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
                $"maxResponseBytes weglassen oder einen Wert zwischen {McpResponseBudgetLimits.MinimumContentBytes} und {McpResponseBudgetLimits.MaxBytes} setzen.",
                "$.maxResponseBytes");
        }

        return await ExecuteCoreAsync(state, filePaths, maxResponseBytes, ct);
    }

    private static async Task<CallToolResult> ExecuteCoreAsync(
        ISolutionStateProvider state, string[]? filePaths, int maxResponseBytes, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var paths = McpBatchArguments.Normalize(filePaths, StringComparer.OrdinalIgnoreCase);
        if (paths.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'filePaths' fehlt oder ist leer.",
                hint: McpToolResults.FilePathsBatchHint);
        }

        try
        {
            return await RenderFileSkeletonsAsync(solution, paths, state.HandoffSymbolIdentity, maxResponseBytes, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_file_skeleton: {ex.Message}",
                context: string.Join(", ", paths));
        }
    }

    private static async Task<CallToolResult> RenderFileSkeletonsAsync(
        Solution solution,
        IReadOnlyList<string> paths,
        AnalysisSymbolIdentity? assemblyIdentity,
        int maxResponseBytes,
        CancellationToken ct)
    {
        var solutionDir = SolutionDocumentPathResolver.GetSolutionDirectory(solution) ?? "";
        var units = new List<SkeletonRenderUnit>();

        for (var i = 0; i < paths.Count; i++)
        {
            var rendered = await RenderSingleFileSkeletonAsync(
                new RenderSingleFileSkeletonRequest(solution, paths[i], solutionDir, paths.Count, assemblyIdentity), ct);
            if (rendered.Error is not null) return rendered.Error;
            units.AddRange(rendered.Units);
        }

        units = units.Where(unit => unit.Markdown.Length > 0).ToList();
        var markdown = string.Join("\n\n---\n\n", units.Select(unit => unit.Markdown));
        var visibleUnits = units;
        if (maxResponseBytes > 0 && Encoding.UTF8.GetByteCount(markdown) > maxResponseBytes)
        {
            const string markerFormat = "[Antwort wegen maxResponseBytes begrenzt — {0} vollständige Skeleton-Einheiten ausgelassen; maxResponseBytes erhöhen oder filePaths verfeinern]";
            var markerBytes = Encoding.UTF8.GetByteCount(string.Format(markerFormat, units.Count));
            if (markerBytes >= maxResponseBytes)
            {
                return McpToolResults.InvalidArgument(
                    "maxResponseBytes ist zu klein, um selbst den Trunkierungshinweis vollständig auszugeben.",
                    "maxResponseBytes erhöhen; Skeletons werden nur an vollständigen Typ-/Datei-Einheiten gekürzt.",
                    "$.maxResponseBytes");
            }

            var kept = new List<SkeletonRenderUnit>();
            foreach (var unit in units)
            {
                var candidate = string.Join("\n\n---\n\n", kept.Select(item => item.Markdown).Concat([unit.Markdown]));
                var remaining = units.Count - kept.Count - 1;
                var marker = string.Format(markerFormat, remaining);
                if (Encoding.UTF8.GetByteCount(candidate + "\n\n" + marker) > maxResponseBytes) break;
                kept.Add(unit);
            }

            var omitted = units.Count - kept.Count;
            visibleUnits = kept;
            markdown = string.Join("\n\n---\n\n", kept.Select(unit => unit.Markdown));
            var markerText = string.Format(markerFormat, omitted);
            markdown = string.IsNullOrEmpty(markdown) ? markerText : markdown + "\n\n" + markerText;
        }
        return McpToolResults.Text(markdown);
    }

    private static async Task<(CallToolResult? Error, IReadOnlyList<SkeletonRenderUnit> Units)> RenderSingleFileSkeletonAsync(
        RenderSingleFileSkeletonRequest request,
        CancellationToken ct)
    {
        var solution = request.Solution;
        var path = request.Path;
        var solutionDir = request.SolutionDir;
        var totalCount = request.TotalCount;
        var assemblyIdentity = request.AssemblyIdentity;
        var candidates = SolutionDocumentPathResolver.FindCandidates(solution, path);
        if (candidates.Count > 1)
        {
            var candidateNames = candidates
                .Select(candidate => $"{candidate.Project.Name}/{candidate.Name}")
                .ToList();
            if (totalCount == 1) return (McpToolResults.AmbiguousPath(path, candidateNames), []);
            return (null, [new SkeletonRenderUnit($"### Datei nicht eindeutig: `{path}`\n\n" + McpToolResults.AmbiguousPath(path, candidateNames).Content.OfType<TextContentBlock>().Single().Text, path, [])]);
        }

        var document = candidates.SingleOrDefault();

        if (document is null)
        {
            if (totalCount == 1) return (McpToolResults.FileNotFound(path), []);
            return (null, [new SkeletonRenderUnit($"### Datei nicht gefunden: `{path}`\n\n[HINWEIS] Datei '{path}' existiert nicht in der Solution.", path, [])]);
        }

        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(
            document,
            solutionDir,
            ct,
            assemblyIdentity is null ? null : symbolId => assemblyIdentity.Format(symbolId));

        if (types.Count == 0)
        {
            return (null, [new SkeletonRenderUnit($"### Skelett: `{path}`\n\nKeine Typen gefunden in '{path}'", path, [])]);
        }
        return (null, types.Select(type => new SkeletonRenderUnit(SkeletonMarkdownRenderer.Render([type], path).TrimEnd(), path, [type])).ToList());
    }

    private sealed record RenderSingleFileSkeletonRequest(
        Solution Solution,
        string Path,
        string SolutionDir,
        int TotalCount,
        AnalysisSymbolIdentity? AssemblyIdentity);

}
