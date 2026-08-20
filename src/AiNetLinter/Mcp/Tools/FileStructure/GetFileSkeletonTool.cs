#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Core;
using AiNetLinter.Maps.Skeleton;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// MCP-Tool <c>get_file_skeleton</c>: liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies)
/// einer oder mehrerer C#-Dateien per relativem (oder absolutem) Dateipfad.
/// Akzeptiert einzelne Dateipfade oder Batch-Arrays fuer effizientes Laden ganzer Ordner in einem Turn.
/// </summary>
internal static class GetFileSkeletonTool
{
    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? filePath, CancellationToken ct) =>
        ExecuteAsync(state, filePath, null, ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? filePath, string[]? filePaths, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var paths = ExtractFilePaths(filePath, filePaths);
        if (paths.Count == 0)
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'filePath' oder 'filePaths' fehlt oder ist leer.",
                hint: "filePath: \"src/MyClass.cs\" oder filePaths: [\"src/ClassA.cs\", \"src/ClassB.cs\"].");
        }

        try
        {
            return await RenderFileSkeletonsAsync(solution, paths, ct);
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
        CancellationToken ct)
    {
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var diagnosticsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(solution, ct);
        var mb = new MarkdownBuilder();

        for (var i = 0; i < paths.Count; i++)
        {
            if (i > 0) mb.Divider();

            var earlyError = await RenderSingleFileSkeletonAsync(
                solution, paths[i], solutionDir, diagnosticsByFile, mb, paths.Count, ct);

            if (earlyError != null) return earlyError;
        }

        var markdown = mb.Build().TrimEnd();
        return McpToolResults.Text(markdown);
    }

    private static async Task<CallToolResult?> RenderSingleFileSkeletonAsync(
        Solution solution,
        string path,
        string solutionDir,
        IReadOnlyDictionary<string, IReadOnlyList<Diagnostic>> diagnosticsByFile,
        MarkdownBuilder mb,
        int totalCount,
        CancellationToken ct)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, path));
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, absolutePath);

        if (document is null)
        {
            if (totalCount == 1) return McpToolResults.FileNotFound(path);
            mb.Heading(3, $"Datei nicht gefunden: `{path}`").BlankLine();
            mb.Line($"[HINWEIS] Datei '{path}' existiert nicht in der Solution.");
            return null;
        }

        var args = new LinterArgs { TargetPath = "", Verbose = false };
        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(document, solutionDir, args, ct);

        var fileWarning = McpCompileDiagnostics.FormatFileWarning(
            diagnosticsByFile.GetValueOrDefault(absolutePath, []));

        if (!string.IsNullOrEmpty(fileWarning))
        {
            mb.Line(fileWarning).BlankLine();
        }

        if (types.Count == 0)
        {
            mb.Heading(3, $"Skelett: `{path}`").BlankLine();
            mb.Line($"Keine Typen gefunden in '{path}'");
        }
        else
        {
            var content = SkeletonMarkdownRenderer.Render(types, path, DateTimeOffset.Now);
            mb.Line(content.TrimEnd());
        }

        return null;
    }

    private static List<string> ExtractFilePaths(string? filePath, string[]? filePaths) =>
        McpBatchArguments.Collect(filePath, filePaths, StringComparer.OrdinalIgnoreCase);
}
