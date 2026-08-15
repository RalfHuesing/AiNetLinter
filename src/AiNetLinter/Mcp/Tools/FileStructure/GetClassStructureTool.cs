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
/// MCP-Tool <c>get_class_structure</c>: liefert eine tabellarische Übersicht über alle Member eines
/// C#-Typs (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur). Unterstützt partial
/// classes über mehrere Dateien.
/// </summary>
internal static class GetClassStructureTool
{
    internal static async Task<CallToolResult> ExecuteAsync(
        McpCodeGraphServer state, string? symbol, string? sortBy, CancellationToken ct)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrWhiteSpace(symbol))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbol' fehlt oder ist leer.",
                hint: "symbol angeben: z. B. 'MyClass', 'Namespace.MyClass' oder 'Datei.cs:42:10'.");
        }

        try
        {
            var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(solution, symbol, ct);
            if (error is not null) return error;
            if (resolvedSymbol is null) return McpToolResults.SymbolNotFound(symbol);

            if (!TryResolveNamedType(resolvedSymbol, out var namedType) || namedType is null)
            {
                return McpToolResults.InvalidArgument(
                    $"Symbol '{resolvedSymbol.ToDisplayString()}' ist kein Typ.",
                    hint: "Typname (z. B. 'MyClass', 'Namespace.MyClass') oder Member darin angeben.");
            }

            var solutionDir = Path.GetDirectoryName(solution.FilePath) ?? "";
            var (files, totalLines) = await CollectDeclarationFilesAsync(namedType, solutionDir, ct);
            var members = ExtractMembers(namedType, solutionDir);
            var sortedMembers = SortMembers(members, sortBy);

            var payload = new ClassStructurePayload(
                TypeName: namedType.ToDisplayString(),
                Kind: GetTypeKindDescription(namedType),
                Files: files,
                TotalLines: totalLines,
                MemberCount: sortedMembers.Count,
                Members: sortedMembers);

            var markdown = RenderMarkdown(payload);
            return McpToolResults.Text(McpSufficiencyHints.Append(markdown), payload);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return McpToolResults.CompilationError($"Unerwarteter Fehler in get_class_structure: {ex.Message}");
        }
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

    private static string GetTypeKindDescription(INamedTypeSymbol namedType)
    {
        if (namedType.IsRecord)
        {
            return namedType.TypeKind == TypeKind.Struct ? "record struct" : "record class";
        }
        return namedType.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => namedType.TypeKind.ToString().ToLowerInvariant(),
        };
    }

    private static List<ClassStructureMemberEntry> ExtractMembers(INamedTypeSymbol namedType, string solutionDir)
    {
        var result = new List<ClassStructureMemberEntry>();
        foreach (var m in namedType.GetMembers())
        {
            if (IsExcludedMember(m)) continue;
            result.Add(CreateMemberEntry(m, solutionDir));
        }
        return result;
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
        var loc = m.Locations.FirstOrDefault(l => l.IsInSource) ?? m.Locations.FirstOrDefault();
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
            Visibility: ResolveVisibility(m),
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

    private static string ResolveVisibility(ISymbol m)
    {
        return m.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "private",
        };
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
        sb.AppendLine($"- Member Count: {p.MemberCount}");
        sb.AppendLine();

        if (p.Members.Count == 0)
        {
            sb.AppendLine("Keine Member gefunden.");
            return sb.ToString().TrimEnd();
        }

        sb.AppendLine("| Kind | Name | Visibility | Lines | LineCount | Signature |");
        sb.AppendLine("|:---|:---|:---|---:|---:|:---|");
        foreach (var m in p.Members)
        {
            var linesStr = m.StartLine > 0 ? $"{m.StartLine}-{m.EndLine}" : "-";
            var countStr = m.LineCount > 0 ? m.LineCount.ToString() : "-";
            sb.AppendLine($"| {m.Kind} | {m.Name} | {m.Visibility} | {linesStr} | {countStr} | {m.Signature} |");
        }

        return sb.ToString().TrimEnd();
    }
}
