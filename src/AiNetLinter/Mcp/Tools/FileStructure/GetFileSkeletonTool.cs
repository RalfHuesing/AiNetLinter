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
                $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
                $"maxResponseBytes weglassen oder einen Wert zwischen {McpResponseBudgetLimits.MinimumStructuredBytes} und {McpResponseBudgetLimits.MaxBytes} setzen.",
                "$.maxResponseBytes");
        }

        return await ExecuteCoreAsync(state, filePaths, maxResponseBytes, ct);
    }

    // The final navigation envelope owns the public budget. This pre-navigation route
    // deliberately avoids a second, premature trim while preserving the public contract.
    internal static Task<CallToolResult> ExecuteBeforeNavigationAsync(
        ISolutionStateProvider state, string[]? filePaths, CancellationToken ct) =>
        ExecuteCoreAsync(state, filePaths, 0, ct);

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
        var payload = FileSkeletonPayload.Create(visibleUnits, units.Count, visibleUnits.Count < units.Count);
        return McpToolResults.Text(markdown, payload);
    }

    /// <summary>Reapplies the text budget after the shared navigation footer was appended.</summary>
    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (maxResponseBytes <= 0) return result;
        if (!TryReadBudgetResponse(result, out var text, out var structured)) return result;
        if (FitsBudget(text, structured, maxResponseBytes)) return result;

        return BuildBudgetedResponse(result, text, structured, maxResponseBytes);
    }

    private static bool TryReadBudgetResponse(
        CallToolResult result,
        out string text,
        out JsonObject structured)
    {
        text = string.Empty;
        structured = new JsonObject();

        var textBlock = result.Content.OfType<TextContentBlock>().FirstOrDefault();
        if (textBlock is null || result.StructuredContent is not { ValueKind: JsonValueKind.Object } value)
        {
            return false;
        }

        var parsed = JsonNode.Parse(value.GetRawText()) as JsonObject;
        if (parsed is null)
        {
            return false;
        }

        text = textBlock.Text;
        structured = parsed;
        return true;
    }

    private static bool FitsBudget(string text, object structured, int maxResponseBytes) =>
        Encoding.UTF8.GetByteCount(text) + SerializedSize(structured) <= maxResponseBytes;

    private static CallToolResult BuildBudgetedResponse(
        CallToolResult result,
        string text,
        JsonObject structured,
        int maxResponseBytes)
    {
        var parts = SplitResponse(text);
        var payload = FileSkeletonPayload.From(structured);
        var kept = KeepUnits(parts, payload, structured, maxResponseBytes);
        var finalText = BuildFinalText(parts, kept);
        var finalPayload = payload.ProjectToText(finalText);
        var finalNode = JsonSerializer.SerializeToNode(finalPayload, McpJsonOptions.Default) as JsonObject ?? new JsonObject();
        PreserveNavigation(structured, finalNode);

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

    private static SkeletonResponseParts SplitResponse(string text)
    {
        var navigationIndex = text.IndexOf("## Navigation", StringComparison.Ordinal);
        var footer = navigationIndex < 0 ? string.Empty : "\n\n" + text[navigationIndex..].Trim();
        var body = navigationIndex < 0 ? text : text[..navigationIndex].TrimEnd();
        var skeletonIndex = body.IndexOf("# AiNetLinter — Skeleton Map", StringComparison.Ordinal);
        var prefix = skeletonIndex > 0 ? body[..skeletonIndex].TrimEnd() + "\n\n" : string.Empty;
        if (skeletonIndex > 0) body = body[skeletonIndex..];
        return new(prefix, footer, body.Split("\n\n---\n\n", StringSplitOptions.RemoveEmptyEntries));
    }

    private static IReadOnlyList<string> KeepUnits(
        SkeletonResponseParts parts,
        FileSkeletonPayload payload,
        JsonObject structured,
        int maxResponseBytes)
    {
        const string markerFormat = "[Antwort wegen maxResponseBytes begrenzt — {0} vollständige Skeleton-Einheiten ausgelassen; maxResponseBytes erhöhen oder filePaths verfeinern]";
        var kept = new List<string>();
        for (var i = 0; i < parts.Units.Count; i++)
        {
            var omitted = parts.Units.Count - i - 1;
            var marker = string.Format(markerFormat, omitted);
            var candidate = parts.Prefix + string.Join("\n\n---\n\n", kept.Append(parts.Units[i])) + "\n\n" + marker + parts.Footer;
            var candidatePayload = JsonSerializer.SerializeToNode(payload.ProjectToText(candidate), McpJsonOptions.Default) as JsonObject
                ?? new JsonObject();
            PreserveNavigation(structured, candidatePayload);
            if (!FitsBudget(candidate, candidatePayload, maxResponseBytes)) break;
            kept.Add(parts.Units[i]);
        }
        return kept;
    }

    private static string BuildFinalText(SkeletonResponseParts parts, IReadOnlyList<string> kept)
    {
        const string markerFormat = "[Antwort wegen maxResponseBytes begrenzt — {0} vollständige Skeleton-Einheiten ausgelassen; maxResponseBytes erhöhen oder filePaths verfeinern]";
        var remaining = parts.Units.Count - kept.Count;
        var marker = string.Format(markerFormat, remaining);
        var body = parts.Prefix + string.Join("\n\n---\n\n", kept);
        return string.IsNullOrEmpty(body) ? marker + parts.Footer : body + "\n\n" + marker + parts.Footer;
    }

    private static void PreserveNavigation(JsonObject source, JsonObject target)
    {
        if (source["navigation"] is JsonNode navigation)
        {
            target["navigation"] = navigation.DeepClone();
        }
    }

    private sealed record SkeletonResponseParts(string Prefix, string Footer, IReadOnlyList<string> Units);

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
        IReadOnlyList<SkeletonRenderUnit> units,
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
    IReadOnlyList<FileSkeletonMemberDto> Members,
    string? HandoffKind = null)
{
    internal static FileSkeletonTypeDto From(SkeletonTypeInfo type) => new(
        type.Namespace, type.TypeKind, type.Modifiers, type.Name, type.BaseTypes,
        type.RelativePath, type.Id, type.Members.Select(FileSkeletonMemberDto.From).ToList(),
        type.Id is null ? null : "type");
}

internal sealed record FileSkeletonMemberDto(
    string Kind,
    string Signature,
    string? MetaComment,
    string? Id,
    string? HandoffKind = null)
{
    internal static FileSkeletonMemberDto From(SkeletonMemberInfo member) => new(
        member.Kind.ToString(), member.Signature, member.MetaComment, member.Id,
        member.Id is null ? null : "member");
}
