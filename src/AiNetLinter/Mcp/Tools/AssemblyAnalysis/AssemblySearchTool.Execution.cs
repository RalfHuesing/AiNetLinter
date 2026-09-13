#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Handoffs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static partial class AssemblySearchTool
{
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

        var solution = lease.Server.GetCurrentSolution();
        var identity = lease.Server.AssemblySymbolIdentity;
        if (solution is null || identity is null) return payload;

        var documentsByPath = solution.Projects
            .SelectMany(project => project.Documents)
            .Where(document => !string.IsNullOrWhiteSpace(document.FilePath))
            .GroupBy(document => NormalizeFullPath(document.FilePath!), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var matches = new AssemblySearchMatch[payload.Results.Count];
        for (var index = 0; index < payload.Results.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = payload.Results[index];
            var path = NormalizeFullPath(Path.Combine(root, match.FilePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!documentsByPath.TryGetValue(path, out var document))
            {
                matches[index] = match;
                continue;
            }

            var handoffId = await ResolveDeclarationHandoffAsync(
                document, match, arguments.Kind!, identity, cancellationToken).ConfigureAwait(false);
            matches[index] = match with { HandoffId = handoffId };
        }

        return payload with { Results = matches };
    }

    private static bool IsHandoffEligibleKind(string? kind) =>
        string.Equals(kind, "type", StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, "method", StringComparison.OrdinalIgnoreCase)
        || string.Equals(kind, "property", StringComparison.OrdinalIgnoreCase);

    private static async Task<string?> ResolveDeclarationHandoffAsync(
        Document document,
        AssemblySearchMatch match,
        string kind,
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
            .Select(symbol => identity.FormatHandoff(symbol!))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return handoffIds.Length == 1 ? handoffIds[0] : null;
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
}
