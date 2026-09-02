#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
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
        ISolutionStateProvider state,
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
                if (string.IsNullOrWhiteSpace(input.NamespacePrefix))
                {
                    return AddAssemblyOverviewHeader(
                        state,
                        solution,
                        await ExecuteSolutionOverviewAsync(solution, ct));
                }

                return AddAssemblyOverviewHeader(
                    state,
                    solution,
                    await ExecuteAutoProjectDrilldownAsync(solution, input, clampedDepth, clampedMaxResults, solutionDir, ct));
            }

            return AddAssemblyOverviewHeader(
                state,
                solution,
                await ExecuteProjectDrilldownAsync(solution, input, clampedDepth, clampedMaxResults, solutionDir, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_namespace_tree: {ex.Message}",
                context: input.Project);
        }
    }

    private static CallToolResult AddAssemblyOverviewHeader(
        ISolutionStateProvider state,
        Solution solution,
        CallToolResult result)
    {
        if (state.AssemblySymbolIdentity is null || result.IsError == true)
        {
            return result;
        }

        var textBlock = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        if (textBlock is null || string.IsNullOrEmpty(textBlock.Text))
        {
            return result;
        }

        var assemblyName = solution.Projects.FirstOrDefault()?.AssemblyName
            ?? solution.Projects.FirstOrDefault()?.Name
            ?? Path.GetFileNameWithoutExtension(solution.FilePath)
            ?? "Assembly";
        var text = textBlock.Text;
        var solutionHeadingIndex = text.IndexOf("# Solution Overview:", StringComparison.Ordinal);
        if (solutionHeadingIndex >= 0)
        {
            var lineBreak = text.IndexOf('\n', solutionHeadingIndex);
            var assemblyHeading = $"# Assembly Overview: {assemblyName}";
            text = lineBreak < 0
                ? string.Concat(text.AsSpan(0, solutionHeadingIndex), assemblyHeading)
                : string.Concat(text.AsSpan(0, solutionHeadingIndex), assemblyHeading, text.AsSpan(lineBreak));
        }
        else
        {
            text = $"# Assembly Overview: {assemblyName}\n\n{text}";
        }

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
            StructuredContent = result.StructuredContent,
        };
    }

    private static async Task<CallToolResult> ExecuteSolutionOverviewAsync(
        Solution solution, CancellationToken ct)
    {
        var (overviewText, overviewPayload) = await GetNamespaceTreeScanner.ScanSolutionProjectsAsync(solution, ct);
        return McpToolResults.Text(overviewText, overviewPayload);
    }

    private static async Task<CallToolResult> ExecuteAutoProjectDrilldownAsync(
        Solution solution,
        GetNamespaceTreeInput input,
        int clampedDepth,
        int clampedMaxResults,
        string solutionDir,
        CancellationToken ct)
    {
        var matchingProjects = new List<Project>();

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            var startNs = GetNamespaceTreeScanner.FindNamespace(compilation.GlobalNamespace, input.NamespacePrefix);
            if (startNs is null) continue;

            var projectTrees = await GetNamespaceTreeScanner.GetProjectSyntaxTreesAsync(project, solutionDir, ct);
            if (GetNamespaceTreeScanner.HasAnySourceTypesInHierarchy(startNs, projectTrees))
            {
                matchingProjects.Add(project);
            }
        }

        if (matchingProjects.Count == 0)
        {
            var available = string.Join(", ", solution.Projects.Select(p => p.Name));
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Namespace '{input.NamespacePrefix}' wurde in keinem Projekt der Solution gefunden.",
                hint: $"Verfuegbare Projekte: {available}");
        }

        if (matchingProjects.Count > 1)
        {
            var candidates = matchingProjects.Select(p => $"- {p.Name} ({p.FilePath})");
            return McpToolResults.Recoverable(
                LinterErrorCodes.AmbiguousSymbol,
                $"Namespace '{input.NamespacePrefix}' existiert in mehreren Projekten — Zielprojekt bitte explizit angeben.",
                context: string.Join("\n", candidates),
                hint: "Parameter 'project' mit einem der oben genannten Projektnamen uebergeben.");
        }

        return await ExecuteProjectDrilldownInternalAsync(
            solution,
            matchingProjects[0],
            input,
            clampedDepth,
            clampedMaxResults,
            solutionDir,
            ct);
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

        return await ExecuteProjectDrilldownInternalAsync(
            solution,
            targetProject,
            input,
            clampedDepth,
            clampedMaxResults,
            solutionDir,
            ct);
    }

    private static async Task<CallToolResult> ExecuteProjectDrilldownInternalAsync(
        Solution solution,
        Project targetProject,
        GetNamespaceTreeInput input,
        int clampedDepth,
        int clampedMaxResults,
        string solutionDir,
        CancellationToken ct)
    {
        var scanParams = new NamespaceTreeScanParameters(
            Project: targetProject,
            NamespacePrefix: input.NamespacePrefix,
            Depth: clampedDepth,
            IncludeTypes: input.IncludeTypes,
            KindFilter: input.Kind,
            MaxResults: clampedMaxResults,
            SolutionDir: solutionDir);

        var (treeText, treePayload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(scanParams, ct);
        var finalText = treeText;

        if (!treePayload.Truncated)
        {
            finalText = McpSufficiencyHints.Append(finalText);
        }

        return McpToolResults.Text(finalText, treePayload);
    }
}

