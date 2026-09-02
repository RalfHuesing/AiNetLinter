#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Core;
using AiNetLinter.Core.Documents;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
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
    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, string[]? filePaths, CancellationToken ct)
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
            return await RenderFileSkeletonsAsync(solution, paths, state.AssemblySymbolIdentity, ct);
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
        CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var mb = new MarkdownBuilder();

        for (var i = 0; i < paths.Count; i++)
        {
            if (i > 0) mb.Divider();

            var earlyError = await RenderSingleFileSkeletonAsync(
                new RenderSingleFileSkeletonRequest(
                    solution, paths[i], solutionDir, mb, paths.Count, assemblyIdentity),
                ct);

            if (earlyError != null) return earlyError;
        }

        var markdown = mb.Build().TrimEnd();
        return McpToolResults.Text(markdown);
    }

    private static async Task<CallToolResult?> RenderSingleFileSkeletonAsync(
        RenderSingleFileSkeletonRequest request,
        CancellationToken ct)
    {
        var solution = request.Solution;
        var path = request.Path;
        var solutionDir = request.SolutionDir;
        var mb = request.Markdown;
        var totalCount = request.TotalCount;
        var assemblyIdentity = request.AssemblyIdentity;
        var candidates = SolutionDocumentPathResolver.FindCandidates(solution, path);
        if (candidates.Count > 1)
        {
            var candidateNames = candidates
                .Select(candidate => $"{candidate.Project.Name}/{candidate.Name}")
                .ToList();
            if (totalCount == 1) return McpToolResults.AmbiguousPath(path, candidateNames);
            mb.Heading(3, $"Datei nicht eindeutig: `{path}`").BlankLine();
            mb.Line(McpToolResults.AmbiguousPath(path, candidateNames).Content.OfType<TextContentBlock>().Single().Text);
            return null;
        }

        var document = candidates.SingleOrDefault();

        if (document is null)
        {
            if (totalCount == 1) return McpToolResults.FileNotFound(path);
            mb.Heading(3, $"Datei nicht gefunden: `{path}`").BlankLine();
            mb.Line($"[HINWEIS] Datei '{path}' existiert nicht in der Solution.");
            return null;
        }

        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(
            document,
            solutionDir,
            ct,
            assemblyIdentity is null ? null : symbolId => assemblyIdentity.Format(symbolId));

        if (types.Count == 0)
        {
            mb.Heading(3, $"Skelett: `{path}`").BlankLine();
            mb.Line($"Keine Typen gefunden in '{path}'");
        }
        else
        {
            var content = SkeletonMarkdownRenderer.Render(types, path);
            mb.Line(content.TrimEnd());
        }

        return null;
    }

    private sealed record RenderSingleFileSkeletonRequest(
        Solution Solution,
        string Path,
        string SolutionDir,
        MarkdownBuilder Markdown,
        int TotalCount,
        AnalysisSymbolIdentity? AssemblyIdentity);
}
