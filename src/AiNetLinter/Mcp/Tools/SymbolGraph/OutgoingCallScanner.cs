#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core.DuplicateDetection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

internal static class OutgoingCallScanner
{
    internal static async Task<List<OutgoingCallGroup>> ScanAsync(
        ISymbol symbol, Solution solution, CancellationToken ct)
    {
        var byCallee = new Dictionary<ISymbol, List<Location>>(SymbolEqualityComparer.Default);
        foreach (var syntaxReference in symbol.DeclaringSyntaxReferences)
        {
            var declaration = await syntaxReference.GetSyntaxAsync(ct);
            var body = MethodBodyLocator.GetBody(declaration);
            if (body is null) continue;

            var document = solution.GetDocument(declaration.SyntaxTree);
            if (document is null) continue;
            var semanticModel = await document.GetSemanticModelAsync(ct);
            if (semanticModel is null) continue;
            AddOutgoingSymbols(body, semanticModel, byCallee, ct);
        }

        return byCallee
            .Select(pair => new OutgoingCallGroup(pair.Key, pair.Value))
            .ToList();
    }

    private static void AddOutgoingSymbols(
        SyntaxNode body,
        SemanticModel semanticModel,
        Dictionary<ISymbol, List<Location>> byCallee,
        CancellationToken ct)
    {
        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            AddOutgoingSymbol(semanticModel.GetSymbolInfo(invocation, ct).Symbol, invocation.GetLocation(), byCallee);
        }

        foreach (var creation in body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var symbol = semanticModel.GetSymbolInfo(creation, ct).Symbol ?? semanticModel.GetTypeInfo(creation, ct).Type;
            AddOutgoingSymbol(symbol, creation.GetLocation(), byCallee);
        }

        foreach (var memberAccess in body.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (memberAccess.Parent is InvocationExpressionSyntax) continue;
            AddOutgoingSymbol(semanticModel.GetSymbolInfo(memberAccess, ct).Symbol, memberAccess.GetLocation(), byCallee);
        }
    }

    private static void AddOutgoingSymbol(
        ISymbol? symbol, Location location, Dictionary<ISymbol, List<Location>> byCallee)
    {
        if (symbol is null || !symbol.Locations.Any(candidate => candidate.IsInSource)) return;
        if (!byCallee.TryGetValue(symbol, out var locations))
        {
            locations = new List<Location>();
            byCallee[symbol] = locations;
        }
        locations.Add(location);
    }
}

internal sealed record OutgoingCallGroup(ISymbol Symbol, IReadOnlyList<Location> Locations);
