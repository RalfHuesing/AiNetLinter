#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class GeneratedCodeDetector
{
    internal static bool IsGenerated(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        return symbol is not null && IsGenerated(symbol);
    }

    internal static bool IsGenerated(SyntaxNode root, SemanticModel semanticModel)
    {
        foreach (var declaration in root.DescendantNodesAndSelf().OfType<MemberDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(declaration) is { } symbol && IsGenerated(symbol))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsGenerated(ISymbol symbol) =>
        symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name is "GeneratedCodeAttribute" or "GeneratedCode");
}
