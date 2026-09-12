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
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Scope;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Mcp.Tools.Common;
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
    internal const int DefaultMaxResponseBytes = 16 * 1024;

    internal static async Task<CallToolResult> ExecuteAsync(
        ISolutionStateProvider state,
        string? symbolIdentifier,
        int maxResults = DefaultMaxResults,
        CancellationToken ct = default)
        => await ExecuteAsync(new FindImplementationsRequest(
            state,
            symbolIdentifier,
            maxResults,
            McpScopeType.All,
            IncludeGenerated: false,
            ct)).ConfigureAwait(false);

    internal static async Task<CallToolResult> ExecuteAsync(
        FindImplementationsRequest request)
    {
        var state = request.State;
        var symbolIdentifier = request.SymbolIdentifier;
        var maxResults = request.MaxResults;
        var scopeType = request.ScopeType;
        var includeGenerated = request.IncludeGenerated;
        var ct = request.CancellationToken;
        if (!McpResponseBudgetLimits.IsPublicBudget(request.MaxResponseBytes))
        {
            return InvalidResponseBudget();
        }
        if (state.LoadState == ServerLoadState.Loading) return McpToolResults.Loading();
        var solution = state.GetCurrentSolution();
        if (solution is null) return McpToolResults.SolutionNotLoaded();

        if (string.IsNullOrWhiteSpace(symbolIdentifier))
        {
            return McpToolResults.Recoverable(
                LinterErrorCodes.InvalidArgument,
                "Pflichtparameter 'symbolIdentifier' fehlt oder ist leer.",
                hint: "symbolIdentifier angeben: z. B. \"IProcessor\", \"IProcessor.Execute\" oder \"BaseClass.Run\".");
        }

        var (resolvedSymbol, error) = await FindReferencesTool.ResolveSymbolAsync(
            solution, symbolIdentifier, ct, state.HandoffSymbolIdentity);
        if (error is not null) return error;

        var (rawSymbols, errorMessage) = await FindRawImplementationsAsync(resolvedSymbol!, solution, ct);
        if (errorMessage is not null)
        {
            return McpToolResults.InvalidArgument(errorMessage);
        }

        var absolutePaths = state.HandoffSymbolIdentity?.IsAssembly == true;
        var normalizedMax = maxResults < 1 ? 1 : maxResults;
        var resultDto = await BuildResultDtoAsync(
            new BuildResultRequest(
                resolvedSymbol!,
                rawSymbols ?? [],
                solution,
                normalizedMax,
                absolutePaths,
                state.HandoffSymbolIdentity,
                scopeType,
                includeGenerated,
                new McpScopeClassifier()),
            ct).ConfigureAwait(false);
        return ApplyResponseBudget(resultDto, request.MaxResponseBytes);
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

    private static async Task<FindImplementationsResultDto> BuildResultDtoAsync(
        BuildResultRequest request,
        CancellationToken cancellationToken)
    {
        var targetSymbol = request.TargetSymbol;
        var symbols = request.Symbols;
        var solution = request.Solution;
        var maxResults = request.MaxResults;
        var absolutePaths = request.AbsolutePaths;
        var handoffIdentity = request.HandoffIdentity;
        var scopeType = request.ScopeType;
        var includeGenerated = request.IncludeGenerated;
        var classifier = request.Classifier;
        var scoped = new List<(ISymbol Symbol, McpSymbolScope Scope)>();
        foreach (var symbol in symbols)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scope = await classifier.ClassifySymbolAsync(
                symbol, solution, scopeType, includeGenerated, cancellationToken).ConfigureAwait(false);
            if (scope.IsVisible) scoped.Add((symbol, scope));
        }

        var items = scoped
            .Select(item => MapToDto(item.Symbol, item.Scope, solution, absolutePaths, handoffIdentity))
            .OrderBy(item => ProjectRank(item.ScopeType))
            .ThenBy(item => SourceRank(item.SourceKind))
            .ThenBy(item => item.TypeName, StringComparer.Ordinal)
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
            isTruncated ? ["maxResults"] : [],
            new FindSymbolScopeDto(McpScopeValues.ToWireValue(scopeType), includeGenerated));
    }

    private static ImplementationItemDto MapToDto(
        ISymbol symbol,
        McpSymbolScope scope,
        Solution solution,
        bool absolutePaths,
        AnalysisSymbolIdentity? handoffIdentity)
    {
        var (typeName, memberName, kind) = DescribeSymbol(symbol);
        var status = DetermineStatus(symbol);
        var displayLoc = FormatLocation(symbol, solution, absolutePaths, out var filePath, out var line, out var column);
        var id = handoffIdentity is null ? null : CallGraphTraversal.GetStableSymbolId(symbol, handoffIdentity);
        var handoffKind = id is null ? null : symbol is INamedTypeSymbol ? "type" : "member";

        return new ImplementationItemDto(
            typeName,
            memberName,
            kind,
            status,
            filePath,
            line,
            column,
            displayLoc,
            id,
            handoffKind,
            McpScopeValues.ToWireValue(scope.ProjectKind),
            McpScopeValues.ToWireValue(scope.SourceKind));
    }

    private static int ProjectRank(string? scopeType) => scopeType switch
    {
        "production" => 0,
        "tests" => 1,
        _ => 2,
    };

    private static int SourceRank(string? sourceKind) => sourceKind == "editable" ? 0 : 1;

    private sealed record BuildResultRequest(
        ISymbol TargetSymbol,
        IReadOnlyList<ISymbol> Symbols,
        Solution Solution,
        int MaxResults,
        bool AbsolutePaths,
        AnalysisSymbolIdentity? HandoffIdentity,
        McpScopeType ScopeType,
        bool IncludeGenerated,
        McpScopeClassifier Classifier);

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

    internal static CallToolResult ApplyResponseBudget(FindImplementationsResultDto dto, int maxResponseBytes)
    {
        var current = dto;
        while (CombinedBytes(current) > maxResponseBytes && current.Implementations.Count > 0)
        {
            var implementations = current.Implementations.Take(current.Implementations.Count - 1).ToList();
            var reasons = current.TruncationReasons.Contains("maxResponseBytes", StringComparer.Ordinal)
                ? current.TruncationReasons
                : current.TruncationReasons.Append("maxResponseBytes").ToList();
            current = current with
            {
                Implementations = implementations,
                ShownCount = implementations.Count,
                IsTruncated = true,
                TruncationReasons = reasons,
            };
        }
        if (CombinedBytes(current) > maxResponseBytes)
        {
            return BudgetTooSmall(maxResponseBytes);
        }
        return McpToolResults.Text(FormatResultText(current), current);
    }

    private static CallToolResult InvalidResponseBudget() =>
        McpToolResults.InvalidArgument(
            $"maxResponseBytes muss zwischen {McpResponseBudgetLimits.MinimumContentBytes} und {McpResponseBudgetLimits.MaxBytes} Bytes liegen.",
            "maxResponseBytes weglassen oder einen Wert innerhalb dieses Bereichs setzen.",
            "$.maxResponseBytes");

    private static CallToolResult BudgetTooSmall(int budget) => McpToolResults.Error(
        LinterErrorCodes.ResponseBudgetTooSmall,
        $"maxResponseBytes={budget} ist zu klein für die vollständige minimale Implementierungsprojektion.",
        new McpErrorParameters(Hint: "maxResponseBytes erhöhen; Implementierungen werden nur vollständig gekürzt.", FieldPath: "$.maxResponseBytes"));

    internal static CallToolResult ApplyFinalResponseBudget(CallToolResult result, int maxResponseBytes)
    {
        return Mcp.Wire.McpResponseSize.From(result).TotalBytes <= maxResponseBytes
            ? result
            : BudgetTooSmall(maxResponseBytes);
    }

    private static int CombinedBytes(FindImplementationsResultDto dto) =>
        Encoding.UTF8.GetByteCount(FormatResultText(dto))
        + JsonSerializer.SerializeToUtf8Bytes(dto, McpJsonOptions.Default).Length;

    internal static string FormatResultText(FindImplementationsResultDto dto)
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
