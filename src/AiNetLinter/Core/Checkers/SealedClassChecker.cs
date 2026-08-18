#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class SealedClassChecker
{
    internal static void Check(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceSealedClasses) return;
        if (IsSealedOrStaticOrAbstract(node)) return;
        if (node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) && ctx.Config.Global.AllowUnsealedPartialClasses) return;
        if (ExemptBaseTypeResolver.HasExemptSuffix(node.Identifier.Text, ctx.Config.Global.SealedClassExemptSuffixes)) return;

        ctx.ReportViolation(node, new ViolationDescription(
            nameof(ctx.Config.Global.EnforceSealedClasses),
            $"Die Klasse '{node.Identifier.Text}' ist nicht als 'sealed' deklariert.",
            "Fuege den 'sealed' Modifikator zur Klassendeklaration hinzu, um unkontrollierte Vererbung zu verhindern."));
    }

    internal static bool IsSealedOrStaticOrAbstract(ClassDeclarationSyntax node) =>
        node.Modifiers.Any(m => m.IsKind(SyntaxKind.SealedKeyword) || m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.AbstractKeyword));
}
