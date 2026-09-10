#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        if (input.MaxResponseBytes < 0)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes darf nicht negativ sein.",
                "maxResponseBytes weglassen, 0 verwenden oder einen positiven Wert setzen.",
                "$.maxResponseBytes");
        }
        if (input.MaxResponseBytes > McpResponseBudgetLimits.MaxBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes darf höchstens {McpResponseBudgetLimits.MaxBytes} sein.",
                $"maxResponseBytes auf höchstens {McpResponseBudgetLimits.MaxBytes} setzen.",
                "$.maxResponseBytes");
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
                        await ExecuteSolutionOverviewAsync(solution, input, clampedDepth, ct), input.MaxResponseBytes);
                }

                return AddAssemblyOverviewHeader(
                    state,
                    solution,
                    await ExecuteAutoProjectDrilldownAsync(solution, input, clampedDepth, clampedMaxResults, solutionDir, ct), input.MaxResponseBytes);
            }

            return AddAssemblyOverviewHeader(
                state,
                solution,
                await ExecuteProjectDrilldownAsync(solution, input, clampedDepth, clampedMaxResults, solutionDir, ct), input.MaxResponseBytes);
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
            StructuredContent = result.StructuredContent,
        };
    }

    private static async Task<CallToolResult> ExecuteSolutionOverviewAsync(
        Solution solution, GetNamespaceTreeInput input, int effectiveDepth, CancellationToken ct)
    {
        var (overviewText, overviewPayload) = await GetNamespaceTreeScanner.ScanSolutionProjectsAsync(solution, ct);
        overviewPayload = overviewPayload with
        {
            RequestedDepth = input.Depth,
            EffectiveDepth = effectiveDepth,
            DepthWasClamped = input.Depth != effectiveDepth,
        };
        return ApplyResponseBudget(overviewText, overviewPayload, input.MaxResponseBytes);
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
        treePayload = treePayload with
        {
            RequestedDepth = input.Depth,
            EffectiveDepth = clampedDepth,
            DepthWasClamped = input.Depth != clampedDepth,
        };
        var finalText = treeText;

        if (!treePayload.Truncated)
        {
            finalText = McpSufficiencyHints.Append(finalText);
        }

        return ApplyResponseBudget(finalText, treePayload, input.MaxResponseBytes);
    }

    private static CallToolResult ApplyResponseBudget(
        string originalText,
        NamespaceTreePayload payload,
        int maxResponseBytes)
    {
        var candidate = payload;
        var truncatedBy = (payload.TruncatedBy ?? Array.Empty<string>()).ToList();
        var totalCount = payload.TotalCount;
        if (candidate.Namespaces is { Count: > 0 }
            && candidate.ShownCount < CountVisibleEntries(candidate))
        {
            candidate = candidate with
            {
                Namespaces = TakeNamespaceNodes(candidate.Namespaces, candidate.ShownCount, out _),
            };
        }

        if (maxResponseBytes <= 0)
        {
            return McpToolResults.Text(RenderVisibleText(candidate, originalText), candidate);
        }

        if (maxResponseBytes < McpResponseBudgetLimits.MinimumStructuredBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} Bytes betragen, damit Namespace-Text und StructuredContent gemeinsam markiert gekürzt werden können.",
                $"maxResponseBytes weglassen, 0 verwenden oder mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} setzen.",
                "$.maxResponseBytes");
        }

        while (true)
        {
            var budgetCandidate = candidate with
                {
                    ShownCount = CountVisibleEntries(candidate),
                    Truncated = true,
                    TruncatedBy = truncatedBy.Append("maxResponseBytes").Distinct(StringComparer.Ordinal).ToArray(),
                    Next = new NamespaceTreeNext("request_detail", "maxResponseBytes erhöhen oder project/namespacePrefix/maxResults verfeinern."),
                };
            if (CombinedResponseBytes(RenderVisibleText(budgetCandidate, originalText), budgetCandidate) <= maxResponseBytes
                || !HasVisibleEntries(candidate)) break;
            candidate = RemoveLastVisibleEntry(candidate);
        }

        var shownCount = CountVisibleEntries(candidate);
        if (shownCount < totalCount)
        {
            if (!truncatedBy.Contains("maxResponseBytes", StringComparer.Ordinal)) truncatedBy.Add("maxResponseBytes");
            candidate = candidate with
            {
                ShownCount = shownCount,
                Truncated = true,
                TruncatedBy = truncatedBy,
                Next = new NamespaceTreeNext("request_detail", "maxResponseBytes erhöhen oder project/namespacePrefix/maxResults verfeinern."),
            };
        }

        var text = RenderVisibleText(candidate, originalText);
        if (CombinedResponseBytes(text, candidate) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Navigation-/Trunkierungs-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Namespace-/Typ-Einheiten gekürzt.",
                "$.maxResponseBytes");
        }
        return McpToolResults.Text(text, candidate);
    }

    /// <summary>
    /// Reapplies the discovery budget after the shared target-navigation envelope has been
    /// appended. Navigation is part of the wire contract, so measuring only the tool-local
    /// projection would allow the final MCP response to exceed <c>maxResponseBytes</c>.
    /// </summary>
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || maxResponseBytes <= 0)
        {
            return result;
        }

        if (!structured.TryGetProperty("projects", out _)
            && !structured.TryGetProperty("namespaces", out _)
            && !structured.TryGetProperty("types", out _))
        {
            return result;
        }

        NamespaceTreePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<NamespaceTreePayload>(
                structured.GetRawText(), McpJsonOptions.Default);
        }
        catch (JsonException)
        {
            return result;
        }
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        var envelope = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        return payload is null || text is null || envelope is null
            ? result
            : ApplyFinalResponseBudget(result, text, payload, envelope, maxResponseBytes);
    }

    private static CallToolResult ApplyFinalResponseBudget(
        CallToolResult original,
        string originalText,
        NamespaceTreePayload payload,
        JsonObject envelope,
        int maxResponseBytes)
    {
        var candidate = payload;
        var truncatedBy = (payload.TruncatedBy ?? Array.Empty<string>()).ToList();
        var totalCount = payload.TotalCount;
        while (true)
        {
            var budgetCandidate = candidate with
            {
                ShownCount = CountVisibleEntries(candidate),
                Truncated = true,
                TruncatedBy = truncatedBy.Append("maxResponseBytes").Distinct(StringComparer.Ordinal).ToArray(),
                Next = new NamespaceTreeNext("request_detail", "maxResponseBytes erhöhen oder project/namespacePrefix/maxResults verfeinern."),
            };
            var projected = ProjectEnvelope(envelope, budgetCandidate);
            var rendered = RenderVisibleText(budgetCandidate, originalText);
            if (CombinedResponseBytes(rendered, projected) <= maxResponseBytes || !HasVisibleEntries(candidate)) break;
            candidate = RemoveLastVisibleEntry(candidate);
        }

        var shownCount = CountVisibleEntries(candidate);
        if (shownCount < totalCount)
        {
            if (!truncatedBy.Contains("maxResponseBytes", StringComparer.Ordinal)) truncatedBy.Add("maxResponseBytes");
            candidate = candidate with
            {
                ShownCount = shownCount,
                Truncated = true,
                TruncatedBy = truncatedBy,
                Next = new NamespaceTreeNext("request_detail", "maxResponseBytes erhöhen oder project/namespacePrefix/maxResults verfeinern."),
            };
        }

        var finalEnvelope = ProjectEnvelope(envelope, candidate);
        var finalText = RenderVisibleText(candidate, originalText);
        if (CombinedResponseBytes(finalText, finalEnvelope) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Navigation-/Trunkierungs-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Namespace-/Typ-Einheiten gekürzt.",
                "$.maxResponseBytes");
        }
        return new CallToolResult
        {
            IsError = original.IsError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = finalText } },
            StructuredContent = JsonSerializer.SerializeToElement(finalEnvelope, McpJsonOptions.Default),
        };
    }

    private static JsonObject ProjectEnvelope(JsonObject original, NamespaceTreePayload payload)
    {
        var projected = (JsonObject)original.DeepClone();
        var payloadNode = JsonSerializer.SerializeToNode(payload, McpJsonOptions.Default) as JsonObject
            ?? new JsonObject();
        foreach (var name in NamespacePayloadFields)
        {
            projected.Remove(name);
            if (payloadNode[name] is { } value) projected[name] = value.DeepClone();
        }
        return projected;
    }

    private static readonly string[] NamespacePayloadFields =
    [
        "solutionName", "project", "namespacePrefix", "kindFilter", "depth", "includeTypes",
        "totalCount", "shownCount", "truncated", "projects", "namespaces", "types",
        "requestedDepth", "effectiveDepth", "depthWasClamped", "truncatedBy", "next",
    ];

    private static int SerializedSize(NamespaceTreePayload payload) =>
        JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

    private static int CombinedResponseBytes(string text, NamespaceTreePayload payload) =>
        Encoding.UTF8.GetByteCount(text) + SerializedSize(payload);

    private static int CombinedResponseBytes(string text, JsonObject envelope) =>
        Encoding.UTF8.GetByteCount(text) + JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

    private static string TrimUtf8(string value, int maxBytes)
    {
        const string marker = "\n\n[Antwort wegen maxResponseBytes begrenzt — maxResponseBytes erhöhen oder den Scope verfeinern]";
        var markerBytes = Encoding.UTF8.GetByteCount(marker);
        var budget = Math.Max(0, maxBytes - markerBytes);
        var slice = value.AsSpan();
        while (slice.Length > 0 && Encoding.UTF8.GetByteCount(slice) > budget) slice = slice[..^1];
        return markerBytes >= maxBytes ? slice.ToString() : slice.ToString().TrimEnd() + marker;
    }

    private static bool HasVisibleEntries(NamespaceTreePayload payload) =>
        payload.Projects is { Count: > 0 } || payload.Types is { Count: > 0 } || payload.Namespaces is { Count: > 0 };

    private static int CountVisibleEntries(NamespaceTreePayload payload)
    {
        if (payload.Projects is not null) return payload.Projects.Count;
        if (payload.Types is not null) return payload.Types.Count;
        return payload.Namespaces?.Sum(CountNamespaceEntries) ?? 0;
    }

    private static int CountNamespaceEntries(NamespaceTreeNode node) =>
        1 + (node.SubNamespaces?.Sum(CountNamespaceEntries) ?? 0);

    private static NamespaceTreePayload RemoveLastVisibleEntry(NamespaceTreePayload payload)
    {
        if (payload.Projects is { Count: > 0 } projects)
        {
            return payload with { Projects = projects.Take(projects.Count - 1).ToList() };
        }
        if (payload.Types is { Count: > 0 } types)
        {
            return payload with { Types = types.Take(types.Count - 1).ToList() };
        }
        if (payload.Namespaces is { Count: > 0 } namespaces)
        {
            var projected = TakeNamespaceNodes(namespaces, Math.Max(0, CountVisibleEntries(payload) - 1), out _);
            return payload with { Namespaces = projected };
        }
        return payload;
    }

    private static IReadOnlyList<NamespaceTreeNode> TakeNamespaceNodes(
        IReadOnlyList<NamespaceTreeNode> nodes,
        int maxEntries,
        out int taken)
    {
        var result = new List<NamespaceTreeNode>();
        taken = 0;
        foreach (var node in nodes)
        {
            if (taken >= maxEntries) break;
            var remaining = maxEntries - taken - 1;
            var sub = node.SubNamespaces is null || remaining <= 0
                ? null
                : TakeNamespaceNodes(node.SubNamespaces, remaining, out var subTaken);
            result.Add(node with { SubNamespaces = sub is { Count: > 0 } ? sub : null });
            taken += 1 + (sub?.Sum(CountNamespaceEntries) ?? 0);
        }
        return result;
    }

    private static string RenderVisibleText(NamespaceTreePayload payload, string fallbackText)
    {
        var sb = new StringBuilder();
        if (payload.Projects is not null)
        {
            sb.AppendLine($"# Solution Overview: {payload.SolutionName} ({payload.TotalCount} Projekte)\n");
            foreach (var project in payload.Projects)
            {
                sb.AppendLine($"- {project.ProjectName} (Typ: {project.ProjectType}, {project.NamespaceCount} Namespaces, {project.TypeCount} Typen)");
            }
        }
        else if (payload.Types is not null)
        {
            sb.AppendLine($"# Typen in Namespace '{payload.NamespacePrefix}' (Projekt: {payload.Project}):\n");
            foreach (var type in payload.Types)
            {
                sb.AppendLine($"- {type.Name} ({type.Kind}) — {type.FilePath}:{type.Line}");
            }
        }
        else
        {
            var prefix = string.IsNullOrWhiteSpace(payload.NamespacePrefix) ? string.Empty : $" unter '{payload.NamespacePrefix}'";
            sb.AppendLine($"# Namespaces in Projekt '{payload.Project}'{prefix}:\n");
            foreach (var node in payload.Namespaces ?? Array.Empty<NamespaceTreeNode>())
            {
                AppendNamespaceText(sb, node, 0);
            }
        }

        if (payload.Truncated)
        {
            sb.AppendLine();
            sb.Append($"[{payload.TotalCount} Eintraege gesamt, {payload.ShownCount} gezeigt — maxResponseBytes/maxResults erhoehen]");
        }
        var rendered = sb.ToString().TrimEnd();
        var headingIndex = rendered.IndexOf('#');
        var originalHeading = fallbackText.IndexOf('#');
        if (originalHeading > 0 && headingIndex == 0)
        {
            var originalHeadingEnd = fallbackText.IndexOf('\n', originalHeading);
            var originalHeadingLine = originalHeadingEnd < 0
                ? fallbackText[originalHeading..].TrimEnd('\r')
                : fallbackText[originalHeading..originalHeadingEnd].TrimEnd('\r');
            if (originalHeadingLine.StartsWith("# Assembly Overview:", StringComparison.Ordinal)
                && rendered.StartsWith("# Solution Overview:", StringComparison.Ordinal))
            {
                var renderedLineEnd = rendered.IndexOf('\n');
                rendered = fallbackText[..originalHeading].TrimEnd() + "\n\n" + originalHeadingLine
                    + (renderedLineEnd < 0 ? string.Empty : rendered[renderedLineEnd..]);
            }
            else
            {
                rendered = fallbackText[..originalHeading].TrimEnd() + "\n\n" + rendered;
            }
        }

        var navigationIndex = fallbackText.IndexOf("## Navigation", StringComparison.Ordinal);
        if (navigationIndex >= 0)
        {
            rendered += "\n\n" + fallbackText[navigationIndex..].Trim();
        }
        return rendered.Length == 0 ? fallbackText : rendered;
    }

    private static void AppendNamespaceText(StringBuilder sb, NamespaceTreeNode node, int indent)
    {
        sb.AppendLine($"{new string(' ', indent * 2)}- {node.Namespace} ({node.TypeCount} Typen)");
        foreach (var child in node.SubNamespaces ?? Array.Empty<NamespaceTreeNode>())
        {
            AppendNamespaceText(sb, child, indent + 1);
        }
    }
}

