#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class SealedClassChecker
{
    internal static void Check(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceSealedClasses) return;
        if (IsSealedOrStaticOrAbstract(node)) return;
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) && ctx.Config.Global.AllowUnsealedPartialClasses) return;
        if (HasExemptSuffix(node.Identifier.Text, ctx)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Global.EnforceSealedClasses),
            Details = $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
            Guidance = "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu, um unkontrollierte Vererbung zu verhindern."
        });
    }

    internal static bool IsSealedOrStaticOrAbstract(ClassDeclarationSyntax node) =>
        node.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.AbstractKeyword));

    private static bool HasExemptSuffix(string className, CheckerContext ctx)
    {
        var suffixes = ctx.Config.Global.SealedClassExemptSuffixes;
        if (suffixes == null || suffixes.Count == 0) return false;
        return suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }
}
