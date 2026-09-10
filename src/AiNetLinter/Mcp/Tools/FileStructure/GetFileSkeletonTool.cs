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
    internal static Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, string[]? filePaths, CancellationToken ct) =>
        ExecuteAsync(state, filePaths, 0, ct);

    internal static async Task<CallToolResult> ExecuteAsync(
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

        if (maxResponseBytes < 0)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes darf nicht negativ sein.",
                "maxResponseBytes weglassen, 0 verwenden oder einen positiven Wert setzen.",
                "$.maxResponseBytes");
        }
        if (maxResponseBytes > McpResponseBudgetLimits.MaxBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes darf höchstens {McpResponseBudgetLimits.MaxBytes} sein.",
                $"maxResponseBytes auf höchstens {McpResponseBudgetLimits.MaxBytes} setzen.",
                "$.maxResponseBytes");
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
        var sections = new List<string>();

        for (var i = 0; i < paths.Count; i++)
        {
            var rendered = await RenderSingleFileSkeletonAsync(
                new RenderSingleFileSkeletonRequest(solution, paths[i], solutionDir, paths.Count, assemblyIdentity), ct);
            if (rendered.Error is not null) return rendered.Error;
            sections.AddRange(rendered.Sections);
        }

        var units = sections.Where(section => section.Length > 0).ToList();
        var markdown = string.Join("\n\n---\n\n", units);
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

            var kept = new List<string>();
            foreach (var unit in units)
            {
                var candidate = string.Join("\n\n---\n\n", kept.Append(unit));
                var remaining = units.Count - kept.Count - 1;
                var marker = string.Format(markerFormat, remaining);
                if (Encoding.UTF8.GetByteCount(candidate + "\n\n" + marker) > maxResponseBytes) break;
                kept.Add(unit);
            }

            var omitted = units.Count - kept.Count;
            markdown = string.Join("\n\n---\n\n", kept);
            var markerText = string.Format(markerFormat, omitted);
            markdown = string.IsNullOrEmpty(markdown) ? markerText : markdown + "\n\n" + markerText;
        }
        return McpToolResults.Text(markdown);
    }

    /// <summary>Reapplies the text budget after the shared navigation footer was appended.</summary>
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0) return result;
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (text is null) return result;
        var structured = result.StructuredContent is { ValueKind: JsonValueKind.Object } value
            ? JsonNode.Parse(value.GetRawText()) as JsonObject
            : new JsonObject();
        if (structured is null) return result;
        if (Encoding.UTF8.GetByteCount(text) + SerializedSize(structured) <= maxResponseBytes) return result;

        var navigationIndex = text.IndexOf("## Navigation", StringComparison.Ordinal);
        var footer = navigationIndex < 0 ? string.Empty : "\n\n" + text[navigationIndex..].Trim();
        var body = navigationIndex < 0 ? text : text[..navigationIndex].TrimEnd();
        var skeletonIndex = body.IndexOf("# AiNetLinter — Skeleton Map", StringComparison.Ordinal);
        var prefix = skeletonIndex > 0 ? body[..skeletonIndex].TrimEnd() + "\n\n" : string.Empty;
        if (skeletonIndex > 0) body = body[skeletonIndex..];
        var units = body.Split("\n\n---\n\n", StringSplitOptions.RemoveEmptyEntries).ToList();
        const string markerFormat = "[Antwort wegen maxResponseBytes begrenzt — {0} vollständige Skeleton-Einheiten ausgelassen; maxResponseBytes erhöhen oder filePaths verfeinern]";
        var kept = new List<string>();
        for (var i = 0; i < units.Count; i++)
        {
            var omitted = units.Count - i - 1;
            var marker = string.Format(markerFormat, omitted);
            var candidate = prefix + string.Join("\n\n---\n\n", kept.Append(units[i])) + "\n\n" + marker + footer;
            if (Encoding.UTF8.GetByteCount(candidate) + SerializedSize(structured) > maxResponseBytes) break;
            kept.Add(units[i]);
        }

        var remaining = units.Count - kept.Count;
        var finalMarker = string.Format(markerFormat, remaining);
        var finalText = prefix + string.Join("\n\n---\n\n", kept);
        finalText = string.IsNullOrEmpty(finalText)
            ? finalMarker + footer
            : finalText + "\n\n" + finalMarker + footer;
        if (Encoding.UTF8.GetByteCount(finalText) + SerializedSize(structured) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Navigation-/Trunkierungs-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen; Skeletons werden nur an vollständigen Typ-/Datei-Einheiten gekürzt.",
                "$.maxResponseBytes");
        }
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = finalText } },
            StructuredContent = JsonSerializer.SerializeToElement(structured, McpJsonOptions.Default),
        };
    }

    private static int SerializedSize(JsonObject envelope) =>
        JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

    private static async Task<(CallToolResult? Error, IReadOnlyList<string> Sections)> RenderSingleFileSkeletonAsync(
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
            return (null, [$"### Datei nicht eindeutig: `{path}`\n\n" + McpToolResults.AmbiguousPath(path, candidateNames).Content.OfType<TextContentBlock>().Single().Text]);
        }

        var document = candidates.SingleOrDefault();

        if (document is null)
        {
            if (totalCount == 1) return (McpToolResults.FileNotFound(path), []);
            return (null, [$"### Datei nicht gefunden: `{path}`\n\n[HINWEIS] Datei '{path}' existiert nicht in der Solution."]);
        }

        var types = await SkeletonMapBuilder.ExtractFromDocumentAsync(
            document,
            solutionDir,
            ct,
            assemblyIdentity is null ? null : symbolId => assemblyIdentity.Format(symbolId));

        if (types.Count == 0)
        {
            return (null, [$"### Skelett: `{path}`\n\nKeine Typen gefunden in '{path}'"]);
        }
        return (null, types.Select(type => SkeletonMarkdownRenderer.Render([type], path).TrimEnd()).ToList());
    }

    private sealed record RenderSingleFileSkeletonRequest(
        Solution Solution,
        string Path,
        string SolutionDir,
        int TotalCount,
        AnalysisSymbolIdentity? AssemblyIdentity);
}
