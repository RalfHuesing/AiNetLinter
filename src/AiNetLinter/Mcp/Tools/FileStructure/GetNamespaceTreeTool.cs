#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.Common;
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

        if (!McpResponseBudgetLimits.IsPublicBudget(input.MaxResponseBytes))
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumContentBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
                $"maxResponseBytes weglassen oder einen Wert zwischen {McpResponseBudgetLimits.MinimumContentBytes} und {McpResponseBudgetLimits.MaxBytes} setzen.",
                "$.maxResponseBytes");
        }

        var clampedDepth = Math.Clamp(input.Depth < 1 ? DefaultDepth : input.Depth, 1, MaxDepthCap);
        var clampedMaxResults = Math.Clamp(input.MaxResults < 1 ? DefaultMaxResults : input.MaxResults, 1, MaxResultsCap);
        var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
        var execution = new NamespaceTreeExecutionContext(
            solution,
            input,
            clampedDepth,
            clampedMaxResults,
            solutionDir,
            state.HandoffSymbolIdentity,
            state.AssemblySymbolIdentity is not null);

        try
        {
            return await ExecuteTreeAsync(state, execution, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError(
                $"Unerwarteter Fehler in get_namespace_tree: {ex.Message}",
                context: input.Project);
        }
    }

    private static async Task<CallToolResult> ExecuteTreeAsync(
        ISolutionStateProvider state,
        NamespaceTreeExecutionContext execution,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(execution.Input.Project))
        {
            var result = string.IsNullOrWhiteSpace(execution.Input.NamespacePrefix)
                ? execution.IsAssemblyTarget
                    ? await ExecuteAssemblyRootDrilldownAsync(execution, ct)
                    : await ExecuteSolutionOverviewAsync(execution, ct)
                : await ExecuteAutoProjectDrilldownAsync(execution, ct);
            return AddAssemblyOverviewHeader(state, execution.Solution, result, execution.Input.MaxResponseBytes);
        }

        var drilldown = await ExecuteProjectDrilldownAsync(execution, ct);
        return AddAssemblyOverviewHeader(state, execution.Solution, drilldown, execution.Input.MaxResponseBytes);
    }

    private static CallToolResult AddAssemblyOverviewHeader(
        ISolutionStateProvider state,
        Solution solution,
        CallToolResult result,
        int maxResponseBytes)
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
        };
    }

    private static async Task<CallToolResult> ExecuteSolutionOverviewAsync(
        NamespaceTreeExecutionContext execution,
        CancellationToken ct)
    {
        var (overviewText, overviewPayload) = await GetNamespaceTreeScanner.ScanSolutionProjectsAsync(execution.Solution, ct);
        overviewPayload = overviewPayload with
        {
            RequestedDepth = execution.Input.Depth,
            EffectiveDepth = execution.ClampedDepth,
            DepthWasClamped = execution.Input.Depth != execution.ClampedDepth,
        };
        return ApplyResponseBudget(
            AppendDepthEvidence(overviewText, execution.Input.Depth, execution.ClampedDepth),
            overviewPayload,
            execution.Input.MaxResponseBytes,
            execution.Input.DeferResponseBudgetToNavigation);
    }

    private static Task<CallToolResult> ExecuteAssemblyRootDrilldownAsync(
        NamespaceTreeExecutionContext execution,
        CancellationToken ct)
    {
        var rootProject = execution.Solution.Projects.FirstOrDefault();
        return rootProject is null
            ? ExecuteSolutionOverviewAsync(execution, ct)
            : ExecuteProjectDrilldownInternalAsync(execution, rootProject, ct);
    }

    private static async Task<CallToolResult> ExecuteAutoProjectDrilldownAsync(
        NamespaceTreeExecutionContext execution,
        CancellationToken ct)
    {
        var matchingProjects = new List<Project>();

        foreach (var project in execution.Solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;

            var startNs = GetNamespaceTreeScanner.FindNamespace(compilation.GlobalNamespace, execution.Input.NamespacePrefix);
            if (startNs is null) continue;

            var projectTrees = await GetNamespaceTreeScanner.GetProjectSyntaxTreesAsync(project, execution.SolutionDir, ct);
            if (GetNamespaceTreeScanner.HasAnySourceTypesInHierarchy(startNs, projectTrees))
            {
                matchingProjects.Add(project);
            }
        }

        if (matchingProjects.Count == 0)
        {
            if (execution.IsAssemblyTarget)
            {
                return McpToolResults.Recoverable(
                    LinterErrorCodes.InvalidArgument,
                    $"Namespace '{execution.Input.NamespacePrefix}' wurde im Assembly-Snapshot nicht gefunden.",
                    hint: "namespacePrefix pruefen oder get_namespace_tree ohne namespacePrefix fuer den Assembly-Ueberblick aufrufen.");
            }

            var available = string.Join(", ", execution.Solution.Projects.Select(p => p.Name));
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                $"Namespace '{execution.Input.NamespacePrefix}' wurde in keinem Projekt der Solution gefunden.",
                hint: $"Verfuegbare Projekte: {available}");
        }

        if (matchingProjects.Count > 1)
        {
            var candidates = matchingProjects.Select(p => $"- {p.Name} ({p.FilePath})");
            return McpToolResults.Recoverable(
                LinterErrorCodes.AmbiguousSymbol,
                $"Namespace '{execution.Input.NamespacePrefix}' existiert in mehreren Projekten — Zielprojekt bitte explizit angeben.",
                context: string.Join("\n", candidates),
                hint: "Parameter 'project' mit einem der oben genannten Projektnamen uebergeben.");
        }

        return await ExecuteProjectDrilldownInternalAsync(
            execution,
            matchingProjects[0],
            ct);
    }

    private static async Task<CallToolResult> ExecuteProjectDrilldownAsync(
        NamespaceTreeExecutionContext execution,
        CancellationToken ct)
    {
        var exactMatch = execution.Solution.Projects
            .FirstOrDefault(p => p.Name.Equals(execution.Input.Project, StringComparison.OrdinalIgnoreCase));

        Project targetProject;
        if (exactMatch is not null)
        {
            targetProject = exactMatch;
        }
        else
        {
            var matchingProjects = execution.Solution.Projects
                .Where(p => p.Name.Contains(execution.Input.Project!, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingProjects.Count == 0)
            {
                var available = string.Join(", ", execution.Solution.Projects.Select(p => p.Name));
                return McpToolResults.Recoverable(
                    LinterErrorCodes.InvalidArgument,
                    $"Projekt '{execution.Input.Project}' wurde in der Solution nicht gefunden.",
                    hint: $"Verfuegbare Projekte: {available}");
            }

            if (matchingProjects.Count > 1)
            {
                var candidates = matchingProjects.Select(p => $"- {p.Name} ({p.FilePath})");
                return McpToolResults.Recoverable(
                    LinterErrorCodes.AmbiguousSymbol,
                    $"Projektname '{execution.Input.Project}' ist mehrdeutig — mehrere Projekte gefunden.",
                    context: string.Join("\n", candidates),
                    hint: "Projektnamen praezisieren (vollstaendigen Projektnamen uebergeben).");
            }

            targetProject = matchingProjects[0];
        }

        return await ExecuteProjectDrilldownInternalAsync(
            execution,
            targetProject,
            ct);
    }

    private static async Task<CallToolResult> ExecuteProjectDrilldownInternalAsync(
        NamespaceTreeExecutionContext execution,
        Project targetProject,
        CancellationToken ct)
    {
        var scanParams = new NamespaceTreeScanParameters(
            Project: targetProject,
            NamespacePrefix: execution.Input.NamespacePrefix,
            Depth: execution.ClampedDepth,
            IncludeTypes: execution.Input.IncludeTypes,
            KindFilter: execution.Input.Kind,
            MaxResults: execution.ClampedMaxResults,
            SolutionDir: execution.SolutionDir,
            HandoffIdentity: execution.HandoffIdentity);

        var (treeText, treePayload) = await GetNamespaceTreeScanner.ScanProjectNamespacesAsync(scanParams, ct);
        treePayload = treePayload with
        {
            RequestedDepth = execution.Input.Depth,
            EffectiveDepth = execution.ClampedDepth,
            DepthWasClamped = execution.Input.Depth != execution.ClampedDepth,
        };
        var finalText = AppendDepthEvidence(treeText, execution.Input.Depth, execution.ClampedDepth);
        return ApplyResponseBudget(finalText, treePayload, execution.Input.MaxResponseBytes, execution.Input.DeferResponseBudgetToNavigation);
    }

    private static CallToolResult ApplyResponseBudget(
        string originalText,
        NamespaceTreePayload payload,
        int maxResponseBytes,
        bool deferToNavigation) =>
        deferToNavigation
            ? McpToolResults.Text(originalText)
            : GetNamespaceTreeResponseBudget.Apply(originalText, payload, maxResponseBytes);

    private static string AppendDepthEvidence(string text, int requestedDepth, int effectiveDepth)
    {
        var clamped = requestedDepth == effectiveDepth ? string.Empty : " (gekappt)";
        return $"{text.TrimEnd()}\n\nTiefe: angefragt {requestedDepth}, effektiv {effectiveDepth}{clamped}";
    }
}
