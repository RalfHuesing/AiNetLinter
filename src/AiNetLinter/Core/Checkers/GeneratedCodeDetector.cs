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
        if (symbol == null) return false;
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "GeneratedCodeAttribute" ||
            a.AttributeClass?.Name == "GeneratedCode");
    }
}
