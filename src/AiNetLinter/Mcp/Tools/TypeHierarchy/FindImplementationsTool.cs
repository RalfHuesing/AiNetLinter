#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.TypeHierarchy;

/// <summary>
/// MCP-Tool <c>find_implementations</c>: Findet konkrete Implementierungen und Overrides
/// von Interfaces, abstrakten Klassen, virtuellen Methoden und Properties.
/// </summary>
internal static class FindImplementationsTool
{
    internal const int DefaultMaxResults = 50;

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string? symbolIdentifier,
        int maxResults = DefaultMaxResults,
        CancellationToken ct = default,
        string? symbol = null)
    {
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        var effectiveIdentifier = !string.IsNullOrWhiteSpace(symbolIdentifier) ? symbolIdentifier : symbol;
        if (string.IsNullOrEmpty(effectiveIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' (oder 'symbol') fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: z. B. \"IProcessor\", \"IProcessor.Execute\" oder \"BaseClass.Run\".");
        }

        var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, effectiveIdentifier, ct, state.AssemblySymbolIdentity);
        if (error is not null) return error;

        var (rawSymbols, errorMessage) = await FindRawImplementationsAsync(resolvedSymbol!, solution, ct);
        if (errorMessage is not null)
        {
            return McpToolResults.InvalidArgument(errorMessage);
        }

        var absolutePaths = state.AssemblySymbolIdentity is not null;
        var normalizedMax = maxResults < 1 ? 1 : maxResults;
        var resultDto = BuildResultDto(resolvedSymbol!, rawSymbols ?? [], solution, normalizedMax, absolutePaths);
        var text = FormatResultText(resultDto);
        var finalText = resultDto.IsTruncated ? text : McpSufficiencyHints.Append(text);

        return McpToolResults.Text(finalText, resultDto);
    }

    private static async Task<(IReadOnlyList<ISymbol>? Symbols, string? ErrorMessage)> FindRawImplementationsAsync(
        ISymbol symbol,
        Solution solution,
        CancellationToken ct) => symbol switch
    {
        INamedTypeSymbol type => await FindTypeImplementationsAsync(type, solution, ct),
        IMethodSymbol method => await FindMethodImplementationsAsync(method, solution, ct),
        IPropertySymbol prop => await FindPropertyImplementationsAsync(prop, solution, ct),
        _ => (null, $"Symbol '{symbol.ToDisplayString()}' ({symbol.Kind}) kann keine Implementierungen oder Overrides haben."),
    };

    private static async Task<(IReadOnlyList<ISymbol>? Symbols, string? ErrorMessage)> FindTypeImplementationsAsync(
        INamedTypeSymbol type,
        Solution solution,
        CancellationToken ct)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var impls = await SymbolFinder.FindImplementationsAsync(type, solution, transitive: true, cancellationToken: ct);
            return (impls.ToList(), null);
        }

        if (type.TypeKind == TypeKind.Class)
        {
            var derived = await SymbolFinder.FindDerivedClassesAsync(type, solution, transitive: true, cancellationToken: ct);
            return (derived.ToList(), null);
        }

        return (null, $"Typ '{type.ToDisplayString()}' ist weder ein Interface noch eine vererbbare Klasse.");
    }

    private static async Task<(IReadOnlyList<ISymbol>? Symbols, string? ErrorMessage)> FindMethodImplementationsAsync(
        IMethodSymbol method,
        Solution solution,
        CancellationToken ct)
    {
        if (method.ContainingType?.TypeKind == TypeKind.Interface)
        {
            var impls = await SymbolFinder.FindImplementationsAsync(method, solution, cancellationToken: ct);
            return (impls.ToList(), null);
        }

        if (method.IsVirtual || method.IsAbstract || method.IsOverride)
        {
            var overrides = await SymbolFinder.FindOverridesAsync(method, solution, cancellationToken: ct);
            return (overrides.ToList(), null);
        }

        return (null, $"Methode '{method.ToDisplayString()}' ist weder Teil eines Interface noch virtuell/abstrakt.");
    }

    private static async Task<(IReadOnlyList<ISymbol>? Symbols, string? ErrorMessage)> FindPropertyImplementationsAsync(
        IPropertySymbol prop,
        Solution solution,
        CancellationToken ct)
    {
        if (prop.ContainingType?.TypeKind == TypeKind.Interface)
        {
            var impls = await SymbolFinder.FindImplementationsAsync(prop, solution, cancellationToken: ct);
            return (impls.ToList(), null);
        }

        if (prop.IsVirtual || prop.IsAbstract || prop.IsOverride)
        {
            var overrides = await SymbolFinder.FindOverridesAsync(prop, solution, cancellationToken: ct);
            return (overrides.ToList(), null);
        }

        return (null, $"Eigenschaft '{prop.ToDisplayString()}' ist weder Teil eines Interface noch virtuell/abstrakt.");
    }

    private static FindImplementationsResultDto BuildResultDto(
        ISymbol targetSymbol,
        IReadOnlyList<ISymbol> symbols,
        Solution solution,
        int maxResults,
        bool absolutePaths)
    {
        var items = symbols
            .Select(s => MapToDto(s, solution, absolutePaths))
            .OrderBy(item => item.TypeName, StringComparer.Ordinal)
            .ThenBy(item => item.MemberName ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        var total = items.Count;
        var isTruncated = total > maxResults;
        var shown = isTruncated ? items.Take(maxResults).ToList() : items;

        return new FindImplementationsResultDto(
            targetSymbol.ToDisplayString(),
            targetSymbol.Kind.ToString().ToLowerInvariant(),
            shown,
            total,
            shown.Count,
            isTruncated,
            isTruncated ? ["maxResults"] : []);
    }

    private static ImplementationItemDto MapToDto(ISymbol symbol, Solution solution, bool absolutePaths)
    {
        var (typeName, memberName, kind) = DescribeSymbol(symbol);
        var status = DetermineStatus(symbol);
        var displayLoc = FormatLocation(symbol, solution, absolutePaths, out var filePath, out var line, out var column);

        return new ImplementationItemDto(
            typeName,
            memberName,
            kind,
            status,
            filePath,
            line,
            column,
            displayLoc);
    }

    private static (string TypeName, string? MemberName, string Kind) DescribeSymbol(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol type)
        {
            var kindStr = type.TypeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Interface => "interface",
                TypeKind.Struct => "struct",
                _ => type.TypeKind.ToString().ToLowerInvariant(),
            };
            return (type.ToDisplayString(), null, kindStr);
        }

        var containingTypeName = symbol.ContainingType?.ToDisplayString()
            ?? symbol.ContainingNamespace?.ToDisplayString()
            ?? string.Empty;

        var memberKind = symbol switch
        {
            IMethodSymbol => "method",
            IPropertySymbol => "property",
            IEventSymbol => "event",
            _ => symbol.Kind.ToString().ToLowerInvariant(),
        };

        return (containingTypeName, symbol.Name, memberKind);
    }

    private static string DetermineStatus(ISymbol symbol)
    {
        if (symbol.IsAbstract) return "abstract";
        if (symbol is IMethodSymbol { IsVirtual: true, IsOverride: false }) return "virtual";
        if (symbol is IPropertySymbol { IsVirtual: true, IsOverride: false }) return "virtual";
        if (symbol is IEventSymbol { IsVirtual: true, IsOverride: false }) return "virtual";
        return "concrete";
    }

    private static string FormatLocation(
        ISymbol symbol,
        Solution solution,
        bool absolutePaths,
        out string? filePath,
        out int? line,
        out int? column)
    {
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? symbol.Locations.FirstOrDefault();
        if (loc is null || !loc.IsInSource || loc.SourceTree is null)
        {
            filePath = null;
            line = null;
            column = null;
            return "[extern/metadata]";
        }

        var lineSpan = loc.GetLineSpan();
        var rawPath = loc.SourceTree.FilePath;
        var outputRoot = Path.GetDirectoryName(solution.FilePath) ?? string.Empty;
        filePath = !absolutePaths && !string.IsNullOrWhiteSpace(outputRoot)
            ? PathNormalizer.ToRelative(outputRoot, rawPath)
            : Path.GetFullPath(rawPath);
        line = lineSpan.StartLinePosition.Line + 1;
        column = lineSpan.StartLinePosition.Character + 1;
        return $"{filePath}:{line}:{column}";
    }

    private static string FormatResultText(FindImplementationsResultDto dto)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Implementierungen / Overrides für '{dto.TargetSymbol}' ({dto.TargetKind}):");
        sb.AppendLine($"Gefunden: {dto.TotalCount} Implementierung(en)");

        if (dto.Implementations.Count == 0)
        {
            sb.Append("\nKeine konkreten Implementierungen oder Overrides gefunden.");
            return sb.ToString();
        }

        sb.AppendLine();
        foreach (var item in dto.Implementations)
        {
            var symbolLabel = string.IsNullOrEmpty(item.MemberName)
                ? item.TypeName
                : $"{item.TypeName}.{item.MemberName}";
            sb.AppendLine($"- [{item.Status}] {symbolLabel} ({item.Kind})");
            sb.AppendLine($"  {item.DisplayLocation}");
        }

        if (dto.IsTruncated)
        {
            sb.Append($"\n[Ergebnis trunkiert — {dto.ShownCount} von {dto.TotalCount} Implementierungen gezeigt; maxResults erhöhen]");
        }

        return sb.ToString().TrimEnd();
    }
}
