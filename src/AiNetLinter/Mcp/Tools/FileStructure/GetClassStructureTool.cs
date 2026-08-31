#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Parameter fuer <see cref="GetClassStructureTool.ExecuteAsync(McpCodeGraphServer, GetClassStructureArgs, CancellationToken)"/>.
/// </summary>
internal sealed record GetClassStructureArgs(
    string? SymbolIdentifier,
    string? SortBy = "lines",
    int MaxMembers = GetClassStructureTool.DefaultMaxMembers,
    string? KindFilter = null,
    string? NameFilter = null);

/// <summary>
/// MCP-Tool <c>get_class_structure</c>: liefert eine tabellarische Übersicht über alle Member eines
/// C#-Typs (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur). Unterstützt partial
/// classes über mehrere Dateien.
/// </summary>
internal static class GetClassStructureTool
{
    /// <summary>Default für <c>maxMembers</c> — konsistent mit <see cref="McpTruncation"/>.</summary>
    internal const int DefaultMaxMembers = 50;

    /// <summary>Harter Cap — Antwort bleibt damit immer unter ~50 KB.</summary>
    internal const int MaxMembersCap = 200;

    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, string? sortBy, CancellationToken ct) =>
        ExecuteAsync(state, new GetClassStructureArgs(symbolIdentifier, sortBy), ct);

    internal static Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbolIdentifier, string? sortBy, int maxMembers, CancellationToken ct) =>
        ExecuteAsync(state, new GetClassStructureArgs(symbolIdentifier, sortBy, maxMembers), ct);

    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state,
        GetClassStructureArgs args,
        CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrWhiteSpace(args.SymbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: z. B. 'MyClass', 'Namespace.MyClass' oder 'Datei.cs:42:10'.");
        }

        var clampedMaxMembers = Math.Clamp(args.MaxMembers, 1, MaxMembersCap);

        try
        {
            var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(
                solution,
                args.SymbolIdentifier,
                ct,
                state.AssemblySymbolIdentity);
            if (error is not null) return error;
            if (resolvedSymbol is null) return McpToolResults.SymbolNotFound(args.SymbolIdentifier);

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

            var payload = new ClassStructurePayload(
                TypeName: namedType.ToDisplayString(),
                Kind: SymbolKindClassifier.DescribeNamedTypeKind(namedType, englishClass: true),
                Files: files,
                TotalLines: totalLines,
                TotalMemberCount: sortedMembers.Count,
                ShownMemberCount: shownMembers.Count,
                Truncated: truncated,
                Members: shownMembers);

            var markdown = RenderMarkdown(payload);
            return McpToolResults.Text(McpSufficiencyHints.Append(markdown), payload);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError($"Unerwarteter Fehler in get_class_structure: {ex.Message}");
        }
    }

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
                || string.Equals(memberKind, "PrimaryCtor-Param", StringComparison.OrdinalIgnoreCase),
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
                Kind: "PrimaryCtor-Param",
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

        return new ClassStructureMemberEntry(
            Kind: ResolveMemberKind(m),
            Name: m.Name,
            Visibility: SymbolVisibilityResolver.ResolveVisibility(m),
            StartLine: startLine,
            EndLine: endLine,
            LineCount: lineCount,
            Signature: m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
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
            sb.AppendLine($"[{p.TotalMemberCount} Member gesamt, {p.ShownMemberCount} gezeigt — maxMembers erhöhen oder sortBy wechseln]");
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
