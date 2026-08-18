#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// MCP-Tool <c>get_namespace_tree</c>: Ermoeglicht hierarchische Exploration von Codebases
/// entlang 3 Zoom-Stufen (Solution -> Projekte -> Namespaces -> Typen).
/// </summary>
internal static class GetNamespaceTreeTool
{
    internal const int DefaultDepth = 1;
    internal const int MaxDepthCap = 3;
    internal const int DefaultMaxResults = 50;
    internal const int MaxResultsCap = 200;

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        GetNamespaceTreeInput input,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (!GetNamespaceTreeScanner.IsValidKind(input.Kind))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Unbekannter kind-Filter '{input.Kind}'.",
                hint: "Gueltige Werte: class/klasse, interface, record, struct, enum, all.");
        }

        var clampedDepth = Math.Clamp(input.Depth < 1 ? DefaultDepth : input.Depth, 1, MaxDepthCap);
        var clampedMaxResults = Math.Clamp(input.MaxResults < 1 ? DefaultMaxResults : input.MaxResults, 1, MaxResultsCap);
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";

        try
        {
            if (string.IsNullOrWhiteSpace(input.Project))
            {
                return await ExecuteSolutionOverviewAsync(solution, ct);
            }

            return await ExecuteProjectDrilldownAsync(solution, input, clampedDepth, clampedMaxResults, solutionDir, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_namespace_tree: {ex.Message}",
                context: input.Project);
        }
    }

    private static async Task<CallToolResult> ExecuteSolutionOverviewAsync(
        Solution solution, CancellationToken ct)
    {
        var (overviewText, overviewPayload) = await GetNamespaceTreeScanner.ScanSolutionProjectsAsync(solution, ct);
        var warning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var textWithWarning = FindSymbolTool.PrependWarning(warning, overviewText);
        return McpToolResults.Text(textWithWarning, overviewPayload);
    }

    private static async Task<CallToolResult> ExecuteProjectDrilldownAsync(
        Solution solution,
        GetNamespaceTreeInput input,
        int clampedDepth,
        int clampedMaxResults,
        string solutionDir,
        CancellationToken ct)
    {
        var exactMatch = solution.Projects
            .FirstOrDefault(p => p.Name.Equals(input.Project, StringComparison.OrdinalIgnoreCase));

        Project targetProject;
        if (exactMatch is not null)
        {
            targetProject = exactMatch;
        }
        else
        {
            var matchingProjects = solution.Projects
                .Where(p => p.Name.Contains(input.Project!, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingProjects.Count == 0)
            {
                var available = string.Join(", ", solution.Projects.Select(p => p.Name));
                return McpToolResults.Recoverable(
                    LinterErrorCodes.InvalidArgument,
                    $"Projekt '{input.Project}' wurde in der Solution nicht gefunden.",
                    hint: $"Verfuegbare Projekte: {available}");
            }

            if (matchingProjects.Count > 1)
            {
                var candidates = matchingProjects.Select(p => $"- {p.Name} ({p.FilePath})");
                return McpToolResults.Recoverable(
                    LinterErrorCodes.AmbiguousSymbol,
                    $"Projektname '{input.Project}' ist mehrdeutig — mehrere Projekte gefunden.",
                    context: string.Join("\n", candidates),
                    hint: "Projektnamen praezisieren (vollstaendigen Projektnamen uebergeben).");
            }

            targetProject = matchingProjects[0];
        }
        var scanParams = new NamespaceTreeScanParameters(
            Project: targetProject,
            NamespacePrefix: input.NamespacePrefix,
            Depth: clampedDepth,
            IncludeTypes: input.IncludeTypes,
            KindFilter: input.Kind,
            MaxResults: clampedMaxResults,
            SolutionDir: solutionDir);

        var (treeText, treePayload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(scanParams, ct);
        var projWarning = await FindSymbolTool.BuildAggregateWarningAsync(solution, ct);
        var finalText = FindSymbolTool.PrependWarning(projWarning, treeText);

        if (!treePayload.Truncated)
        {
            finalText = McpSufficiencyHints.Append(finalText);
        }

        return McpToolResults.Text(finalText, treePayload);
    }
}
