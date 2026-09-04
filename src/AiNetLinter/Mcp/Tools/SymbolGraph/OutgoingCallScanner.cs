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
        ISymbol symbol, Solution solution, CancellationToken ct, bool includeBcl = false)
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
            AddOutgoingSymbols(body, semanticModel, byCallee, includeBcl, ct);
        }

        return byCallee
            .Select(pair => new OutgoingCallGroup(pair.Key, pair.Value))
            .ToList();
    }

    private static void AddOutgoingSymbols(
        SyntaxNode body,
        SemanticModel semanticModel,
        Dictionary<ISymbol, List<Location>> byCallee,
        bool includeBcl,
        CancellationToken ct)
    {
        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = ResolveInvocationSymbol(invocation, semanticModel, ct);
            AddOutgoingSymbol(symbol, invocation.GetLocation(), byCallee, includeBcl);
        }

        foreach (var creation in body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            var symbol = ResolveCreationSymbol(creation, semanticModel, ct);
            AddOutgoingSymbol(symbol, creation.GetLocation(), byCallee, includeBcl);
        }

        foreach (var memberAccess in body.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (memberAccess.Parent is InvocationExpressionSyntax) continue;
            var symbol = ResolveMemberAccessSymbol(memberAccess, semanticModel, ct);
            AddOutgoingSymbol(symbol, memberAccess.GetLocation(), byCallee, includeBcl);
        }
    }

    private static ISymbol? ResolveInvocationSymbol(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var info = semanticModel.GetSymbolInfo(invocation, ct);
        if (info.Symbol is not null) return info.Symbol;
        if (info.CandidateSymbols.Length > 0) return info.CandidateSymbols[0];

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            return ResolveMemberAccessSymbol(memberAccess, semanticModel, ct);
        }

        return null;
    }

    private static ISymbol? ResolveCreationSymbol(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var info = semanticModel.GetSymbolInfo(creation, ct);
        if (info.Symbol is not null) return info.Symbol;
        if (info.CandidateSymbols.Length > 0) return info.CandidateSymbols[0];
        return semanticModel.GetTypeInfo(creation, ct).Type;
    }

    private static ISymbol? ResolveMemberAccessSymbol(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel,
        CancellationToken ct)
    {
        var info = semanticModel.GetSymbolInfo(memberAccess, ct);
        if (info.Symbol is not null) return info.Symbol;
        if (info.CandidateSymbols.Length > 0) return info.CandidateSymbols[0];

        var memberGroup = semanticModel.GetMemberGroup(memberAccess, ct);
        return memberGroup.Length > 0 ? memberGroup[0] : null;
    }

    private static void AddOutgoingSymbol(
        ISymbol? symbol,
        Location location,
        Dictionary<ISymbol, List<Location>> byCallee,
        bool includeBcl)
    {
        if (symbol is null) return;
        var isExternal = !symbol.Locations.Any(candidate => candidate.IsInSource);
        if (isExternal && !includeBcl && IsBclSymbol(symbol))
        {
            return;
        }

        if (!byCallee.TryGetValue(symbol, out var locations))
        {
            locations = new List<Location>();
            byCallee[symbol] = locations;
        }
        locations.Add(location);
    }

    internal static bool IsBclSymbol(ISymbol symbol)
    {
        var asmName = symbol.ContainingAssembly?.Name;
        if (!string.IsNullOrEmpty(asmName) && IsBclAssemblyName(asmName))
        {
            return true;
        }

        var ns = symbol.ContainingNamespace?.ToDisplayString();
        return !string.IsNullOrEmpty(ns) && IsBclNamespace(ns);
    }

    private static bool IsBclAssemblyName(string name) =>
        string.Equals(name, "System", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.NETCore.", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "mscorlib", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "netstandard", StringComparison.OrdinalIgnoreCase);

    private static bool IsBclNamespace(string ns) =>
        string.Equals(ns, "System", StringComparison.Ordinal)
        || ns.StartsWith("System.", StringComparison.Ordinal)
        || ns.StartsWith("Microsoft.Win32", StringComparison.Ordinal);
}

internal sealed record OutgoingCallGroup(ISymbol Symbol, IReadOnlyList<Location> Locations);
