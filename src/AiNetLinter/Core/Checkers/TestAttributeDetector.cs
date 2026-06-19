#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class TestAttributeDetector
{
    internal static bool CheckForTestMethods(TypeDeclarationSyntax node, CheckerContext ctx) =>
        node.Members.OfType<MethodDeclarationSyntax>()
            .SelectMany(m => m.AttributeLists)
            .SelectMany(al => al.Attributes)
            .Any(attr => IsTestAttribute(attr, ctx));

    private static bool IsTestAttribute(AttributeSyntax attr, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetSymbolInfo(attr).Symbol;
        var attrType = symbol?.ContainingType;
        if (attrType == null) return false;
        var ns = attrType.ContainingNamespace?.ToDisplayString();
        if (ns == null) return false;
        return ns.StartsWith("Xunit", StringComparison.OrdinalIgnoreCase)
            || ns.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)
            || ns.StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting", StringComparison.OrdinalIgnoreCase);
    }
}
