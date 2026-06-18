#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class PublicMembersChecker
{
    internal static void Check(TypeDeclarationSyntax node, string typeName, CheckerContext ctx)
    {
        var limit = ctx.Config.Metrics.MaxPublicMembersPerType;
        if (limit <= 0) return;

        foreach (var suffix in ctx.Config.Metrics.MaxPublicMembersPerTypeExemptSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var count = CountPublicMembers(node);
        if (count > limit)
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = nameof(ctx.Config.Metrics.MaxPublicMembersPerType),
                Details = $"'{typeName}' hat {count} öffentliche Member (erlaubt: {limit}). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.",
                Guidance = "Teile den Typ nach Single-Responsibility auf (z. B. QueryService / CommandService). Prüfe, ob Methoden auf 'internal' oder 'private' reduziert werden können."
            });
        }
    }

    private static int CountPublicMembers(TypeDeclarationSyntax node)
    {
        var count = 0;
        foreach (var member in node.Members)
        {
            if (!IsPublicMember(member)) continue;
            if (IsOverrideOrExplicitImpl(member)) continue;
            count++;
        }
        return count;
    }

    private static bool IsPublicMember(MemberDeclarationSyntax member)
    {
        if (!member.Modifiers.Any(SyntaxKind.PublicKeyword)) return false;
        return member is MethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or EventDeclarationSyntax
            or EventFieldDeclarationSyntax
            or FieldDeclarationSyntax;
    }

    private static bool IsOverrideOrExplicitImpl(MemberDeclarationSyntax member)
    {
        if (member.Modifiers.Any(SyntaxKind.OverrideKeyword)) return true;
        if (member is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier != null) return true;
        if (member is PropertyDeclarationSyntax prop && prop.ExplicitInterfaceSpecifier != null) return true;
        if (member is EventDeclarationSyntax evt && evt.ExplicitInterfaceSpecifier != null) return true;
        return false;
    }
}
