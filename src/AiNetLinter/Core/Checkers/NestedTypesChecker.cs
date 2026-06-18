#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Prueft ob ein Typ oeffentliche oder interne Typen verschachtelt.
/// Private nested Typen bleiben standardmaessig erlaubt (Implementierungsdetail)
/// und sind kein Grep-Target fuer KI-Agenten.
/// </summary>
internal static class NestedTypesChecker
{
    internal static void Check(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanPublicNestedTypes) return;

        var allowPrivate = ctx.Config.Global.BanPublicNestedTypesAllowPrivate;

        foreach (var nested in node.Members.OfType<BaseTypeDeclarationSyntax>())
        {
            if (!ShouldReport(nested, allowPrivate)) continue;
            Report(nested, node.Identifier.Text, ctx);
        }
    }

    private static bool ShouldReport(BaseTypeDeclarationSyntax nested, bool allowPrivate)
    {
        var isPublic = HasModifier(nested, SyntaxKind.PublicKeyword);
        var isInternal = HasModifier(nested, SyntaxKind.InternalKeyword);
        var isPrivate = HasModifier(nested, SyntaxKind.PrivateKeyword);

        if (isPublic || isInternal) return true;
        if (isPrivate) return !allowPrivate;
        return !allowPrivate;
    }

    private static void Report(BaseTypeDeclarationSyntax nested, string outerName, CheckerContext ctx)
    {
        var accessibility = DescribeAccessibility(nested);
        var kindLabel = nested is EnumDeclarationSyntax ? "enum" : "Typ";
        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(nested),
            RuleName = nameof(ctx.Config.Global.BanPublicNestedTypes),
            Details = $"Der {kindLabel} '{outerName}.{nested.Identifier.Text}' ist ein {accessibility} nested Type.",
            Guidance = "Extrahiere den Typ in eine eigene Datei (Namespace-Ebene), damit er per Datei-Listing/Grep fuer LLMs sichtbar ist."
        });
    }

    private static string DescribeAccessibility(BaseTypeDeclarationSyntax nested)
    {
        if (HasModifier(nested, SyntaxKind.PublicKeyword)) return "public";
        if (HasModifier(nested, SyntaxKind.InternalKeyword)) return "internal";
        return "private";
    }

    private static bool HasModifier(BaseTypeDeclarationSyntax node, SyntaxKind kind)
    {
        foreach (var modifier in node.Modifiers)
        {
            if (modifier.RawKind == (int)kind) return true;
        }
        return false;
    }
}
