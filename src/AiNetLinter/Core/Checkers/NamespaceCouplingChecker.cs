#nullable enable

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class NamespaceCouplingChecker
{
    internal static void CheckForbiddenNamespace(string? referencedNamespace, SyntaxNode node, CheckerContext ctx)
    {
        if (string.IsNullOrEmpty(referencedNamespace)) return;

        foreach (var rule in ctx.Config.ForbiddenNamespaceDependencies)
        {
            if (rule.SourceNamespace == null || rule.TargetNamespace == null) continue;
            if (!NamespaceMatches(ctx.CurrentNamespace, rule.SourceNamespace)) continue;
            if (!NamespaceMatches(referencedNamespace, rule.TargetNamespace)) continue;

            ctx.ReportViolation(node,
                "ForbiddenNamespaceDependency",
                $"Der Namespace '{ctx.CurrentNamespace}' darf nicht vom Namespace '{referencedNamespace}' abhaengen (Referenz gefunden: '{node}').",
                "Entferne die Abhaengigkeit oder nutze Abstraktion/Events statt direkter Kopplung.");
        }
    }

    internal static void CheckForbiddenSymbolNamespace(IdentifierNameSyntax node, CheckerContext ctx)
    {
        SyntaxNode target = node;
        while (target.Parent is NameSyntax || target.Parent is MemberAccessExpressionSyntax)
            target = target.Parent;

        var symbol = ctx.SemanticModel.GetSymbolInfo(target).Symbol ?? ctx.SemanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        string? ns;
        if (symbol is INamedTypeSymbol typeSymbol)
        {
            CheckForbiddenNamespace(typeSymbol.ContainingNamespace?.ToDisplayString(), node, ctx);
            return;
        }

        ns = symbol is INamespaceSymbol nsSymbol
            ? nsSymbol.ToDisplayString()
            : symbol.ContainingType?.ContainingNamespace?.ToDisplayString();

        CheckForbiddenNamespace(ns, node, ctx);
    }

    private static bool NamespaceMatches(string ns, string pattern)
    {
        if (string.IsNullOrEmpty(ns) || string.IsNullOrEmpty(pattern)) return false;
        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(ns, regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return ns.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }
}
