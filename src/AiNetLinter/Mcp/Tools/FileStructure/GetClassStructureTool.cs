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
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Parameter fuer <see cref="GetClassStructureTool.ExecuteAsync(ISolutionStateProvider, GetClassStructureArgs, CancellationToken)"/>.
/// </summary>
internal sealed record GetClassStructureArgs(
    string? SymbolIdentifier,
    string? SortBy = "lines",
    int MaxMembers = GetClassStructureTool.DefaultMaxMembers,
    string? KindFilter = null,
    string? NameFilter = null,
    int MaxResponseBytes = 0)
{
    internal string? EffectiveSymbolIdentifier =>
        string.IsNullOrWhiteSpace(SymbolIdentifier) ? null : SymbolIdentifier;
}

/// <summary>
/// MCP-Tool <c>get_class_structure</c>: liefert eine tabellarische Übersicht über alle Member eines
/// C#-Typs (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur). Unterstützt partial
/// classes über mehrere Dateien.
/// </summary>
internal static class GetClassStructureTool
{
    private const string PrimaryConstructorParameterKind = "PrimaryCtor-Param";

    /// <summary>Default für <c>maxMembers</c> — konsistent mit <see cref="McpTruncation"/>.</summary>
    internal const int DefaultMaxMembers = 50;

    /// <summary>Harter Cap — Antwort bleibt damit immer unter ~50 KB.</summary>
    internal const int MaxMembersCap = 200;

    internal static Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, string? symbolIdentifier, string? sortBy, CancellationToken ct) =>
        ExecuteAsync(state, new GetClassStructureArgs(symbolIdentifier, sortBy), ct);

    internal static Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state, string? symbolIdentifier, string? sortBy, int maxMembers, CancellationToken ct) =>
        ExecuteAsync(state, new GetClassStructureArgs(symbolIdentifier, sortBy, maxMembers), ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        GetClassStructureArgs args,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var effectiveIdentifier = args.EffectiveSymbolIdentifier;
        if (string.IsNullOrWhiteSpace(effectiveIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: z. B. 'MyClass', 'Namespace.MyClass' oder 'Datei.cs:42:10'.");
        }

        if (args.MaxResponseBytes < 0)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes darf nicht negativ sein.",
                "maxResponseBytes weglassen, 0 verwenden oder einen positiven Wert setzen.",
                "$.maxResponseBytes");
        }
        if (args.MaxResponseBytes > McpResponseBudgetLimits.MaxBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes darf höchstens {McpResponseBudgetLimits.MaxBytes} sein.",
                $"maxResponseBytes auf höchstens {McpResponseBudgetLimits.MaxBytes} setzen.",
                "$.maxResponseBytes");
        }
        if (args.MaxResponseBytes > 0 && args.MaxResponseBytes < McpResponseBudgetLimits.MinimumStructuredBytes)
        {
            return McpToolResults.InvalidArgument(
                $"maxResponseBytes muss fuer eine markierte strukturierte Antwort mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} Bytes betragen.",
                $"maxResponseBytes weglassen, 0 verwenden oder mindestens {McpResponseBudgetLimits.MinimumStructuredBytes} setzen.",
                "$.maxResponseBytes");
        }

        var clampedMaxMembers = Math.Clamp(args.MaxMembers, 1, MaxMembersCap);

        try
        {
            var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(
                solution,
                effectiveIdentifier,
                ct,
                state.HandoffSymbolIdentity);
            if (error is not null) return error;
            if (resolvedSymbol is null) return McpToolResults.SymbolNotFound(effectiveIdentifier);

            if (!TryResolveNamedType(resolvedSymbol, out var namedType) || namedType is null)
            {
                return McpToolResults.InvalidArgument(
                    $"Symbol '{resolvedSymbol.ToDisplayString()}' ist kein Typ.",
                    hint: "Typname (z. B. 'MyClass', 'Namespace.MyClass') oder Member darin angeben.");
            }

            var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
            var (files, totalLines) = await CollectDeclarationFilesAsync(namedType, solutionDir, ct);
            var allMembers = ExtractMembers(namedType, solutionDir);
            var filteredMembers = FilterMembers(allMembers, args.KindFilter, args.NameFilter);
            var sortedMembers = SortMembers(filteredMembers, args.SortBy);
            var truncated = sortedMembers.Count > clampedMaxMembers;
            var shownMembers = truncated
                ? sortedMembers.Take(clampedMaxMembers).ToList()
                : sortedMembers;

            var truncatedBy = truncated ? new List<string> { "maxMembers" } : new List<string>();
            var payload = new ClassStructurePayload(
                TypeName: namedType.ToDisplayString(),
                Kind: SymbolKindClassifier.DescribeNamedTypeKind(namedType, specificRecord: true),
                Files: files,
                TotalLines: totalLines,
                TotalMemberCount: sortedMembers.Count,
                ShownMemberCount: shownMembers.Count,
                Truncated: truncated,
                Members: shownMembers,
                TruncatedBy: truncatedBy,
                Next: truncated
                    ? new ClassStructureNext("request_detail", "maxMembers erhöhen oder sortBy/kindFilter/nameFilter verfeinern.")
                    : null);

            while (args.MaxResponseBytes > 0 && payload.Members.Count > 0)
            {
                var budgetCandidate = payload with
                {
                    Truncated = true,
                    TruncatedBy = truncatedBy.Append("maxResponseBytes").Distinct(StringComparer.Ordinal).ToArray(),
                    Next = new ClassStructureNext("request_detail", "maxResponseBytes erhöhen oder symbolIdentifier/kindFilter/nameFilter verfeinern."),
                };
                if (CombinedResponseBytes(RenderBudgetText(budgetCandidate, string.Empty), budgetCandidate) <= args.MaxResponseBytes) break;
                shownMembers.RemoveAt(shownMembers.Count - 1);
                if (!truncatedBy.Contains("maxResponseBytes", StringComparer.Ordinal)) truncatedBy.Add("maxResponseBytes");
                payload = payload with
                {
                    ShownMemberCount = shownMembers.Count,
                    Truncated = true,
                    Members = shownMembers.ToList(),
                    TruncatedBy = truncatedBy,
                    Next = new ClassStructureNext("request_detail", "maxResponseBytes erhöhen oder symbolIdentifier/kindFilter/nameFilter verfeinern."),
                };
            }

            var markdown = RenderMarkdown(payload);
            return McpToolResults.Text(payload.Truncated ? markdown : McpSufficiencyHints.Append(markdown), payload);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError($"Unerwarteter Fehler in get_class_structure: {ex.Message}");
        }
    }

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        if (result.StructuredContent is not { ValueKind: JsonValueKind.Object } structured
            || maxResponseBytes <= 0)
        {
            return result;
        }

        ClassStructurePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ClassStructurePayload>(
                structured.GetRawText(), McpJsonOptions.Default);
        }
        catch (JsonException)
        {
            return result;
        }
        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        var envelope = JsonNode.Parse(structured.GetRawText()) as JsonObject;
        if (payload is null || payload.Members is null || payload.Files is null || text is null || envelope is null) return result;

        var members = payload.Members.ToList();
        var truncatedBy = (payload.TruncatedBy ?? Array.Empty<string>()).ToList();
        var candidate = payload;
        while (CombinedResponseBytes(RenderBudgetText(candidate, text), ProjectEnvelope(envelope, candidate)) > maxResponseBytes
            && members.Count > 0)
        {
            members.RemoveAt(members.Count - 1);
            if (!truncatedBy.Contains("maxResponseBytes", StringComparer.Ordinal)) truncatedBy.Add("maxResponseBytes");
            candidate = candidate with
            {
                Members = members.ToList(),
                ShownMemberCount = members.Count,
                Truncated = true,
                TruncatedBy = truncatedBy,
                Next = new ClassStructureNext("request_detail", "maxResponseBytes erhöhen oder symbolIdentifier/kindFilter/nameFilter verfeinern."),
            };
        }

        var finalText = RenderBudgetText(candidate, text);
        var finalEnvelope = ProjectEnvelope(envelope, candidate);
        if (CombinedResponseBytes(finalText, finalEnvelope) > maxResponseBytes)
        {
            return McpToolResults.InvalidArgument(
                "maxResponseBytes ist zu klein, um den festen Navigation-/Trunkierungs-Envelope vollständig auszugeben.",
                "maxResponseBytes erhöhen; die Antwort wird nur an vollständigen Member-Einheiten gekürzt.",
                "$.maxResponseBytes");
        }
        return new CallToolResult
        {
            IsError = result.IsError,
            Content = new List<ContentBlock> { new TextContentBlock { Text = finalText } },
            StructuredContent = JsonSerializer.SerializeToElement(finalEnvelope, McpJsonOptions.Default),
        };
    }

    private static JsonObject ProjectEnvelope(JsonObject original, ClassStructurePayload payload)
    {
        var projected = (JsonObject)original.DeepClone();
        var payloadNode = JsonSerializer.SerializeToNode(payload, McpJsonOptions.Default) as JsonObject
            ?? new JsonObject();
        foreach (var name in ClassPayloadFields)
        {
            projected.Remove(name);
            if (payloadNode[name] is { } value) projected[name] = value.DeepClone();
        }
        return projected;
    }

    private static readonly string[] ClassPayloadFields =
    [
        "typeName", "kind", "files", "totalLines", "totalMemberCount", "shownMemberCount",
        "truncated", "members", "truncatedBy", "next",
    ];

    private static string RenderBudgetText(ClassStructurePayload payload, string fallbackText)
    {
        var rendered = payload.Truncated
            ? RenderMarkdown(payload)
            : McpSufficiencyHints.Append(RenderMarkdown(payload));
        var headingIndex = rendered.IndexOf("# Typ:", StringComparison.Ordinal);
        var originalHeading = fallbackText.IndexOf("# Typ:", StringComparison.Ordinal);
        if (originalHeading > 0 && headingIndex == 0)
        {
            rendered = fallbackText[..originalHeading].TrimEnd() + "\n\n" + rendered;
        }
        var navigationIndex = fallbackText.IndexOf("## Navigation", StringComparison.Ordinal);
        return navigationIndex < 0
            ? rendered
            : rendered.TrimEnd() + "\n\n" + fallbackText[navigationIndex..].Trim();
    }

    private static int CombinedResponseBytes(string text, ClassStructurePayload payload) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(payload, McpJsonOptions.Default).Length;

    private static int CombinedResponseBytes(string text, JsonObject envelope) =>
        Encoding.UTF8.GetByteCount(text)
        + JsonSerializer.SerializeToUtf8Bytes(envelope, McpJsonOptions.Default).Length;

    private static List<ClassStructureMemberEntry> FilterMembers(
        List<ClassStructureMemberEntry> members, string? kindFilter, string? nameFilter)
    {
        var result = (IEnumerable<ClassStructureMemberEntry>)members;
        if (!string.IsNullOrWhiteSpace(kindFilter) && !kindFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedKind = kindFilter.Trim();
            result = result.Where(m => MatchesKind(m.Kind, normalizedKind));
        }

        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            var normalizedName = nameFilter.Trim();
            result = result.Where(m => m.Name.Contains(normalizedName, StringComparison.OrdinalIgnoreCase));
        }

        return result.ToList();
    }

    private static bool MatchesKind(string memberKind, string filter)
    {
        if (string.Equals(memberKind, filter, StringComparison.OrdinalIgnoreCase)) return true;
        return filter.ToLowerInvariant() switch
        {
            "method" or "methods" => string.Equals(memberKind, "Method", StringComparison.OrdinalIgnoreCase),
            "property" or "properties" => string.Equals(memberKind, "Property", StringComparison.OrdinalIgnoreCase),
            "field" or "fields" => string.Equals(memberKind, "Field", StringComparison.OrdinalIgnoreCase),
            "constructor" or "constructors" => string.Equals(memberKind, "Constructor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(memberKind, PrimaryConstructorParameterKind, StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool TryResolveNamedType(ISymbol symbol, out INamedTypeSymbol? namedType)
    {
        namedType = symbol as INamedTypeSymbol ?? symbol.ContainingType;
        return namedType is not null;
    }

    private static async Task<(List<string> Files, int TotalLines)> CollectDeclarationFilesAsync(
        INamedTypeSymbol namedType, string solutionDir, CancellationToken ct)
    {
        var files = new List<string>();
        int totalLines = 0;

        foreach (var syntaxRef in namedType.DeclaringSyntaxReferences)
        {
            var tree = syntaxRef.SyntaxTree;
            if (!string.IsNullOrEmpty(tree.FilePath))
            {
                files.Add(PathNormalizer.ToRelative(solutionDir, tree.FilePath));
            }
            var rootNode = await syntaxRef.GetSyntaxAsync(ct);
            var span = rootNode.GetLocation().GetLineSpan();
            totalLines += span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        }

        if (files.Count == 0 && namedType.Locations.Length > 0)
        {
            foreach (var loc in namedType.Locations)
            {
                if (loc.SourceTree is not null)
                {
                    files.Add(PathNormalizer.ToRelative(solutionDir, loc.SourceTree.FilePath));
                }
            }
        }

        return (files.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), totalLines);
    }

    private static List<ClassStructureMemberEntry> ExtractMembers(INamedTypeSymbol namedType, string solutionDir)
    {
        var result = new List<ClassStructureMemberEntry>();
        if (namedType.IsRecord)
        {
            result.AddRange(ExtractRecordPrimaryCtorParams(namedType));
        }
        foreach (var m in namedType.GetMembers())
        {
            if (IsExcludedMember(m)) continue;
            result.Add(CreateMemberEntry(m, solutionDir));
        }
        return result;
    }

    private static IEnumerable<ClassStructureMemberEntry> ExtractRecordPrimaryCtorParams(INamedTypeSymbol namedType)
    {
        IMethodSymbol? primaryCtor = namedType.InstanceConstructors
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (primaryCtor is null || primaryCtor.Parameters.Length == 0)
        {
            yield break;
        }

        var recordLine = namedType.Locations
            .Where(l => l.IsInSource)
            .Select(l => l.GetLineSpan().StartLinePosition.Line + 1)
            .DefaultIfEmpty(0)
            .Min();
        if (recordLine <= 0) recordLine = 0;

        foreach (var p in primaryCtor.Parameters)
        {
            yield return new ClassStructureMemberEntry(
                Kind: PrimaryConstructorParameterKind,
                Name: p.Name,
                Visibility: "public",
                StartLine: recordLine,
                EndLine: recordLine,
                LineCount: 0,
                Signature: $"{p.Name} : {p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}",
                FilePath: "");
        }
    }

    private static bool IsExcludedMember(ISymbol m)
    {
        if (m.IsImplicitlyDeclared && m is not IMethodSymbol { MethodKind: MethodKind.Constructor })
        {
            return true;
        }
        if (m is IMethodSymbol method)
        {
            if (method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet
                or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise)
            {
                return true;
            }
            if (method.Name.StartsWith("<") || method.Name.EndsWith("$"))
            {
                return true;
            }
        }
        if (m is IFieldSymbol field && (field.Name.StartsWith("<") || field.Name.EndsWith("$")))
        {
            return true;
        }
        return false;
    }

    private static ClassStructureMemberEntry CreateMemberEntry(ISymbol m, string solutionDir)
    {
        var syntaxNode = m.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
        var loc = syntaxNode?.GetLocation() ?? m.Locations.FirstOrDefault(l => l.IsInSource) ?? m.Locations.FirstOrDefault();
        var memberFilePath = loc?.SourceTree?.FilePath is not null
            ? PathNormalizer.ToRelative(solutionDir, loc.SourceTree.FilePath)
            : "";

        int startLine = 0;
        int endLine = 0;
        int lineCount = 0;
        if (loc is not null && loc.IsInSource)
        {
            var span = loc.GetLineSpan();
            startLine = span.StartLinePosition.Line + 1;
            endLine = span.EndLinePosition.Line + 1;
            lineCount = endLine - startLine + 1;
        }

        var signature = m is IFieldSymbol { HasConstantValue: true } field
            ? $"{m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} = {CSharpLiteralFormatter.Format(field.ConstantValue)}"
            : m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return new ClassStructureMemberEntry(
            Kind: ResolveMemberKind(m),
            Name: m.Name,
            Visibility: SymbolVisibilityResolver.ResolveVisibility(m),
            StartLine: startLine,
            EndLine: endLine,
            LineCount: lineCount,
            Signature: signature,
            FilePath: memberFilePath);
    }

    private static string ResolveMemberKind(ISymbol m)
    {
        if (m is IMethodSymbol method)
        {
            return method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor ? "Constructor" : "Method";
        }
        if (m is IPropertySymbol) return "Property";
        if (m is IFieldSymbol field) return field.IsConst ? "Constant" : "Field";
        if (m is IEventSymbol) return "Event";
        if (m is INamedTypeSymbol nts)
        {
            return nts.TypeKind switch
            {
                TypeKind.Enum => "Enum",
                TypeKind.Interface => "Interface",
                TypeKind.Struct => "Struct",
                _ => "Class",
            };
        }
        return m.Kind.ToString();
    }

    private static List<ClassStructureMemberEntry> SortMembers(
        List<ClassStructureMemberEntry> members, string? sortBy)
    {
        return (sortBy?.Trim().ToLowerInvariant()) switch
        {
            "name" => members.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            "kind" => members.OrderBy(m => m.Kind, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => members.OrderBy(m => m.FilePath, StringComparer.OrdinalIgnoreCase).ThenBy(m => m.StartLine).ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    private static string RenderMarkdown(ClassStructurePayload p)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Typ: {p.TypeName}");
        sb.AppendLine($"- Kind: {p.Kind}");
        var filesStr = p.Files.Count == 0 ? "unbekannt" : string.Join(", ", p.Files);
        var fileCountStr = p.Files.Count == 1 ? "1 Datei" : $"{p.Files.Count} Dateien";
        sb.AppendLine($"- Files: {filesStr} ({fileCountStr})");
        sb.AppendLine($"- Total Lines: {p.TotalLines}");
        sb.AppendLine($"- Member Count: {p.ShownMemberCount} von {p.TotalMemberCount}");
        sb.AppendLine();

        if (p.Members.Count == 0)
        {
            sb.AppendLine("Keine Member gefunden.");
            return sb.ToString().TrimEnd();
        }

        AppendMemberRows(sb, p.Members, p.Files.Count > 1);

        if (p.Truncated)
        {
            sb.AppendLine();
            var reason = p.TruncatedBy?.Contains("maxResponseBytes", StringComparer.Ordinal) == true
                ? "maxResponseBytes erhöhen oder Filter verfeinern"
                : "maxMembers erhöhen oder sortBy wechseln";
            sb.AppendLine($"[{p.TotalMemberCount} Member gesamt, {p.ShownMemberCount} gezeigt — {reason}]");
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendMemberRows(StringBuilder sb, IReadOnlyList<ClassStructureMemberEntry> members, bool isMultiFile)
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("Kind")
            .AddColumn("Name")
            .AddColumn("Visibility");

        if (isMultiFile)
        {
            table.AddColumn("File");
        }

        table.AddColumn("Lines", ColumnAlign.Right)
            .AddColumn("LineCount", ColumnAlign.Right)
            .AddColumn("Signature");

        foreach (var m in members)
        {
            var linesStr = m.StartLine > 0 ? $"{m.StartLine}-{m.EndLine}" : "-";
            var countStr = m.LineCount > 0 ? m.LineCount.ToString() : "-";
            if (isMultiFile)
            {
                var fileName = !string.IsNullOrEmpty(m.FilePath) ? Path.GetFileName(m.FilePath) : "-";
                table.AddRow(m.Kind, m.Name, m.Visibility, fileName, linesStr, countStr, m.Signature);
            }
            else
            {
                table.AddRow(m.Kind, m.Name, m.Visibility, linesStr, countStr, m.Signature);
            }
        }
        table.AppendTo(sb);
    }
}

internal static class CSharpLiteralFormatter
{
    internal static string Format(object? value) => value is null
        ? "null"
        : Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false)
            ?? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            ?? string.Empty;
}
