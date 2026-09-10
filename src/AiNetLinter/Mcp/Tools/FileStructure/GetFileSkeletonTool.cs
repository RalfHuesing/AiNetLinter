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
        var payload = FileSkeletonPayload.Create(visibleUnits, units.Count, visibleUnits.Count < units.Count);
        return McpToolResults.Text(markdown, payload);
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
        var payload = FileSkeletonPayload.From(structured);
        const string markerFormat = "[Antwort wegen maxResponseBytes begrenzt — {0} vollständige Skeleton-Einheiten ausgelassen; maxResponseBytes erhöhen oder filePaths verfeinern]";
        var kept = new List<string>();
        for (var i = 0; i < units.Count; i++)
        {
            var omitted = units.Count - i - 1;
            var marker = string.Format(markerFormat, omitted);
            var candidate = prefix + string.Join("\n\n---\n\n", kept.Append(units[i])) + "\n\n" + marker + footer;
            var candidatePayload = payload.ProjectToText(candidate);
            if (Encoding.UTF8.GetByteCount(candidate) + SerializedSize(candidatePayload) > maxResponseBytes) break;
            kept.Add(units[i]);
        }

        var remaining = units.Count - kept.Count;
        var finalMarker = string.Format(markerFormat, remaining);
        var finalText = prefix + string.Join("\n\n---\n\n", kept);
        finalText = string.IsNullOrEmpty(finalText)
            ? finalMarker + footer
            : finalText + "\n\n" + finalMarker + footer;
        var finalPayload = payload.ProjectToText(finalText);
        var finalNode = JsonSerializer.SerializeToNode(finalPayload, McpJsonOptions.Default) as JsonObject ?? new JsonObject();
        if (structured["navigation"] is JsonNode finalNavigation)
        {
            finalNode["navigation"] = finalNavigation.DeepClone();
        }
        if (Encoding.UTF8.GetByteCount(finalText) + SerializedSize(finalNode) > maxResponseBytes)
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
            StructuredContent = JsonSerializer.SerializeToElement(finalNode, McpJsonOptions.Default),
        };
    }

    private static int SerializedSize(object envelope) =>
        JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

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

    internal sealed record SkeletonRenderUnit(string Markdown, string Path, IReadOnlyList<SkeletonTypeInfo> Types);
}

/// <summary>Additive, machine-readable handoff payload for <c>get_file_skeleton</c>.</summary>
internal sealed record FileSkeletonPayload(
    IReadOnlyList<FileSkeletonFileDto> Files,
    int TotalFiles,
    int ShownFiles,
    int TotalTypes,
    int ShownTypes,
    int TotalMembers,
    int ShownMembers,
    bool IsTruncated = false,
    IReadOnlyList<string>? TruncatedBy = null,
    string? NextStep = null)
{
    internal static FileSkeletonPayload Create(
        IReadOnlyList<GetFileSkeletonTool.SkeletonRenderUnit> units,
        int totalUnits,
        bool isTruncated)
    {
        var files = units
            .GroupBy(unit => unit.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FileSkeletonFileDto(
                group.Key,
                group.SelectMany(unit => unit.Types).Select(FileSkeletonTypeDto.From).ToList()))
            .ToList();
        var types = files.SelectMany(file => file.Types).ToList();
        return new(
            files,
            files.Count,
            files.Count,
            totalUnits,
            types.Count,
            types.Sum(type => type.Members.Count),
            types.Sum(type => type.Members.Count),
            isTruncated,
            isTruncated ? ["maxResponseBytes"] : [],
            isTruncated ? "maxResponseBytes erhöhen oder filePaths verfeinern." : null);
    }

    internal static FileSkeletonPayload From(JsonObject value)
    {
        try
        {
            return JsonSerializer.Deserialize<FileSkeletonPayload>(value.ToJsonString(), McpJsonOptions.Default)
                ?? new([], 0, 0, 0, 0, 0, 0);
        }
        catch
        {
            return new([], 0, 0, 0, 0, 0, 0);
        }
    }

    internal FileSkeletonPayload ProjectToText(string text)
    {
        if (Files.Count == 0) return this;
        var visibleFiles = Files
            .Select(file => file with
            {
                Types = file.Types.Where(type =>
                    (!string.IsNullOrEmpty(type.Id) && text.Contains(type.Id, StringComparison.Ordinal))
                    || text.Contains($"### {type.Name}", StringComparison.Ordinal)).ToList()
            })
            .Where(file => file.Types.Count > 0)
            .ToList();
        var types = visibleFiles.SelectMany(file => file.Types).ToList();
        return this with
        {
            Files = visibleFiles,
            ShownFiles = visibleFiles.Count,
            ShownTypes = types.Count,
            ShownMembers = types.Sum(type => type.Members.Count),
            IsTruncated = IsTruncated || visibleFiles.Count != Files.Count,
            TruncatedBy = (TruncatedBy ?? []).Count > 0 ? TruncatedBy : ["maxResponseBytes"],
            NextStep = NextStep ?? "maxResponseBytes erhöhen oder filePaths verfeinern."
        };
    }
}

internal sealed record FileSkeletonFileDto(string Path, IReadOnlyList<FileSkeletonTypeDto> Types);

internal sealed record FileSkeletonTypeDto(
    string Namespace,
    string TypeKind,
    string Modifiers,
    string Name,
    string? BaseTypes,
    string RelativePath,
    string? Id,
    IReadOnlyList<FileSkeletonMemberDto> Members)
{
    internal static FileSkeletonTypeDto From(SkeletonTypeInfo type) => new(
        type.Namespace, type.TypeKind, type.Modifiers, type.Name, type.BaseTypes,
        type.RelativePath, type.Id, type.Members.Select(FileSkeletonMemberDto.From).ToList());
}

internal sealed record FileSkeletonMemberDto(
    string Kind,
    string Signature,
    string? MetaComment,
    string? Id)
{
    internal static FileSkeletonMemberDto From(SkeletonMemberInfo member) => new(
        member.Kind.ToString(), member.Signature, member.MetaComment, member.Id);
}
