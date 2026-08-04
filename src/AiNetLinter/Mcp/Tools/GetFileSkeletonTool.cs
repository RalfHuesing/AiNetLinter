#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Core;
using AiNetLinter.Maps.Skeleton;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools;

/// <summary>
/// MCP-Tool <c>get_file_skeleton</c>: liefert das Struktur-Skelett (Typen, Signaturen ohne Bodies)
/// einer einzelnen C#-Datei per relativem (oder absolutem) Dateipfad. Bewusst duenner Dispatch auf
/// die bereits pro-Dokument arbeitende <see cref="SkeletonMapBuilder.ExtractFromDocumentAsync"/> +
/// <see cref="SkeletonMarkdownRenderer.Render"/> — keine eigene Extraktions-/Rendering-Logik. Deckt
/// nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien.
/// </summary>
internal static class GetFileSkeletonTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string filePath, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var absolutePath = Path.GetFullPath(Path.Combine(solutionDir, filePath));
        var document = DiffImpactAnalyzer.FindDocumentByPath(solution, absolutePath);
        if (document is null) return McpToolResults.FileNotFound(filePath);

        var args = new LinterArgs { TargetPath = "", Verbose = false };
        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(document, solutionDir, args, ct);

        var diagnosticsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(solution, ct);
        var fileWarning = McpCompileDiagnostics.FormatFileWarning(
            diagnosticsByFile.GetValueOrDefault(absolutePath, []));
        var markdown = types.Count == 0
            ? $"Keine Typen gefunden in '{filePath}'"
            : SkeletonMarkdownRenderer.Render(types, filePath, System.DateTimeOffset.Now);
        return McpToolResults.Text(
            string.IsNullOrEmpty(fileWarning) ? markdown : fileWarning + "\n\n" + markdown);
    }
}
