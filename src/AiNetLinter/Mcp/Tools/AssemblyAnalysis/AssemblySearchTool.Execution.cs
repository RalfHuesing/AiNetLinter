#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static partial class AssemblySearchTool
{
    private static CallToolResult? ValidateOpaquePattern(string? pattern) =>
        !string.IsNullOrWhiteSpace(pattern)
        && pattern.StartsWith(HandoffCounterAlphabet.HandlePrefix, StringComparison.OrdinalIgnoreCase)
            ? McpToolResults.Recoverable(
                LinterErrorCodes.UnsupportedIdentifier,
                "pattern unterstützt keine Handoff-ID, weil search_assembly eine Text- und Mustersuche ist.",
                hint: "Die Handoff-ID direkt an get_symbol_body.symbolIdentifiers übergeben.")
            : null;

    internal static string? GetQualifiedTypeName(AssemblySearchArguments arguments)
    {
        if (!string.Equals(arguments.Kind, "type", StringComparison.OrdinalIgnoreCase)
            || arguments.IsRegex == true
            || string.IsNullOrWhiteSpace(arguments.Pattern))
        {
            return null;
        }

        var pattern = arguments.Pattern.Trim();
        var separator = pattern.LastIndexOf('.');
        return separator > 0 && separator < pattern.Length - 1 ? pattern : null;
    }

    private static string GetTypeLeafName(string qualifiedTypeName)
    {
        var leafName = qualifiedTypeName[(qualifiedTypeName.LastIndexOf('.') + 1)..];
        var genericStart = leafName.IndexOf('<');
        return genericStart >= 0 ? leafName[..genericStart] : leafName;
    }

    private static string CreatePagingBinding(AssemblyAnalysisLease lease, AssemblySearchArguments arguments) =>
        AssemblyPaging.CreateSearchBinding(lease.CanonicalPath, lease.Context.Origin.ContentHash, arguments);

    private static AssemblySearchPayload BuildPayloadWithBinding(
        string root,
        AssemblySearchArguments arguments,
        string binding,
        CancellationToken cancellationToken)
    {
        var payload = Scan(root, arguments, cancellationToken);
        return payload with
        {
            ContinuationToken = payload.ContinuationToken is null
                ? null
                : AssemblyPaging.CreateToken(
                    AssemblyPaging.ReadOffset(payload.ContinuationToken),
                    binding),
        };
    }

    private static async Task<AssemblySearchPayload> EnrichDeclarationHandoffsAsync(
        AssemblySearchPayload payload,
        AssemblySearchArguments arguments,
        AssemblyAnalysisLease lease,
        string root,
        CancellationToken cancellationToken)
    {
        if (!IsHandoffEligibleKind(arguments.Kind)) return payload;

        var qualifiedTypeName = GetQualifiedTypeName(arguments);

        var solution = lease.Server.GetCurrentSolution();
        var identity = lease.Server.AssemblySymbolIdentity;
        if (solution is null || identity is null) return payload;

        var documentsByPath = solution.Projects
            .SelectMany(project => project.Documents)
            .Where(document => !string.IsNullOrWhiteSpace(document.FilePath))
            .GroupBy(document => NormalizeFullPath(document.FilePath!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var matches = new List<AssemblySearchMatch>(payload.Results.Count);
        foreach (var match in payload.Results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = NormalizeFullPath(Path.Combine(root, match.FilePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!documentsByPath.TryGetValue(path, out var document))
            {
                if (qualifiedTypeName is null) matches.Add(match);
                continue;
            }

            var handoffId = await ResolveDeclarationHandoffAsync(
                document, match, arguments.Kind!, qualifiedTypeName, identity, cancellationToken).ConfigureAwait(false);
            if (qualifiedTypeName is null || handoffId is not null) matches.Add(match with { HandoffId = handoffId });
        }

        return qualifiedTypeName is null
            ? payload with { Results = matches }
            : payload with
            {
                Results = matches,
                TotalCount = matches.Count,
                ReturnedCount = matches.Count,
                MatchedFileCount = matches.Select(match => match.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ReturnedFileCount = matches.Select(match => match.FilePath).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            };
    }

    private static bool IsHandoffEligibleKind(string? kind) =>
        string.Equals(kind, "type", StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, "method", StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, "property", StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ResolveDeclarationHandoffAsync(
        Document document,
        AssemblySearchMatch match,
        string kind,
        string? qualifiedTypeName,
        AnalysisSymbolIdentity identity,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null || match.Line > text.Lines.Count) return null;

        var handoffIds = match.MatchRanges
            .Select(range => ResolveDeclarationSymbol(root, text, semanticModel, match.Line, range, kind))
            .Where(symbol => symbol is not null)
            .Where(symbol => qualifiedTypeName is null || MatchesQualifiedTypeName(symbol!, qualifiedTypeName))
            .Select(symbol => identity.FormatHandoff(symbol!))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return handoffIds.Length == 1 ? handoffIds[0] : null;
    }

    private static bool MatchesQualifiedTypeName(ISymbol symbol, string qualifiedTypeName) =>
        symbol is INamedTypeSymbol type
        && string.Equals(FormatQualifiedTypeName(type), qualifiedTypeName, StringComparison.Ordinal);

    private static string FormatQualifiedTypeName(INamedTypeSymbol type)
    {
        var typeName = FormatTypeName(type);
        var namespaceName = type.ContainingNamespace.ToDisplayString();
        return string.IsNullOrEmpty(namespaceName) ? typeName : $"{namespaceName}.{typeName}";
    }

    private static string FormatTypeName(INamedTypeSymbol type)
    {
        var containingType = type.ContainingType is null ? string.Empty : $"{FormatTypeName(type.ContainingType)}.";
        var typeParameters = type.TypeParameters.Length == 0
            ? string.Empty
            : $"<{string.Join(", ", type.TypeParameters.Select(parameter => parameter.Name))}>";
        return $"{containingType}{type.Name}{typeParameters}";
    }

    private static ISymbol? ResolveDeclarationSymbol(
        SyntaxNode root,
        SourceText text,
        SemanticModel semanticModel,
        int line,
        AssemblySearchMatchRange range,
        string kind)
    {
        var sourceLine = text.Lines[line - 1];
        var position = sourceLine.Start + range.Column - 1;
        if (position < sourceLine.Start || position >= sourceLine.End) return null;

        for (SyntaxNode? current = root.FindToken(position).Parent; current is not null; current = current.Parent)
        {
            var symbol = ResolveSymbolInDeclaration(current, semanticModel, kind);
            if (symbol is not null) return symbol;
        }

        return null;
    }

    private static ISymbol? ResolveSymbolInDeclaration(SyntaxNode node, SemanticModel semanticModel, string kind) =>
        kind.ToLowerInvariant() switch
        {
            "type" when node is BaseTypeDeclarationSyntax type => semanticModel.GetDeclaredSymbol(type),
            "type" when node is DelegateDeclarationSyntax @delegate => semanticModel.GetDeclaredSymbol(@delegate),
            "method" when node is MethodDeclarationSyntax method => semanticModel.GetDeclaredSymbol(method),
            "method" when node is ConstructorDeclarationSyntax constructor => semanticModel.GetDeclaredSymbol(constructor),
            "method" when node is DestructorDeclarationSyntax destructor => semanticModel.GetDeclaredSymbol(destructor),
            "property" when node is PropertyDeclarationSyntax property => semanticModel.GetDeclaredSymbol(property),
            "property" when node is IndexerDeclarationSyntax indexer => semanticModel.GetDeclaredSymbol(indexer),
            _ => null,
        };

    private static string NormalizeFullPath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string FormatHandoffSuffix(string? handoffId) =>
        string.IsNullOrWhiteSpace(handoffId)
            ? string.Empty
            : $"; handoffId: `{HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(handoffId)}`";

    internal static string RenderText(AssemblySearchPayload payload)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Assembly-Suche: {payload.SearchKind}; {payload.ReturnedCount} von {payload.TotalCount}");
        builder.AppendLine($"Scope: {payload.Scope}; Vollständigkeit: {payload.Completeness}");

        foreach (var fileGroup in payload.Results.GroupBy(match => match.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            RenderFileMatches(builder, fileGroup);
        }

        if (payload.IsTruncated)
        {
            AppendTruncationHint(builder, payload);
        }

        return builder.ToString().TrimEnd();
    }

    private static void RenderFileMatches(StringBuilder builder, IGrouping<string, AssemblySearchMatch> fileGroup)
    {
        var matchLines = fileGroup.Select(match => match.Line).ToHashSet();
        var lastPrintedLine = -1;

        foreach (var match in fileGroup)
        {
            lastPrintedLine = RenderContextBefore(builder, match, matchLines, lastPrintedLine);
            lastPrintedLine = RenderMatchLine(builder, match, lastPrintedLine);
            lastPrintedLine = RenderContextAfter(builder, match, matchLines, lastPrintedLine);
        }
    }

    private static int RenderContextBefore(
        StringBuilder builder,
        AssemblySearchMatch match,
        HashSet<int> matchLines,
        int lastPrintedLine)
    {
        for (var i = 0; i < match.ContextBefore.Count; i++)
        {
            var line = match.Line - match.ContextBefore.Count + i;
            if (line > lastPrintedLine && !matchLines.Contains(line))
            {
                builder.AppendLine($"{match.FilePath}-{line}- {match.ContextBefore[i]}");
                lastPrintedLine = line;
            }
        }

        return lastPrintedLine;
    }

    private static int RenderMatchLine(StringBuilder builder, AssemblySearchMatch match, int lastPrintedLine)
    {
        if (match.Line > lastPrintedLine)
        {
            builder.AppendLine($"{match.FilePath}:{match.Line}: {match.LineText}{FormatHandoffSuffix(match.HandoffId)}");
            return match.Line;
        }

        return lastPrintedLine;
    }

    private static int RenderContextAfter(
        StringBuilder builder,
        AssemblySearchMatch match,
        HashSet<int> matchLines,
        int lastPrintedLine)
    {
        for (var i = 0; i < match.ContextAfter.Count; i++)
        {
            var line = match.Line + 1 + i;
            if (line > lastPrintedLine && !matchLines.Contains(line))
            {
                builder.AppendLine($"{match.FilePath}-{line}- {match.ContextAfter[i]}");
                lastPrintedLine = line;
            }
        }

        return lastPrintedLine;
    }

    private static void AppendTruncationHint(StringBuilder builder, AssemblySearchPayload payload)
    {
        builder.AppendLine($"Ergebnis gekürzt ({string.Join(", ", payload.TruncatedBy)}); " +
                           (payload.ContinuationToken is null ? payload.DetailHint :
                           $"continuationToken={payload.ContinuationToken}; {payload.DetailHint}"));
    }
}
