#nullable enable

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class BoolParameterChecker
{
    internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        if (IsPrivateOrProtected(node.Modifiers) && ctx.Config.Metrics.MaxBoolParameterCountAllowPrivate) return;
        Check(node.ParameterList, node.Identifier.Text, node, ctx);
    }

    internal static void CheckConstructor(ConstructorDeclarationSyntax node, CheckerContext ctx)
    {
        if (IsPrivateOrProtected(node.Modifiers) && ctx.Config.Metrics.MaxBoolParameterCountAllowPrivate) return;
        Check(node.ParameterList, node.Identifier.Text, node, ctx);
    }

    private static void Check(ParameterListSyntax paramList, string memberName, SyntaxNode node, CheckerContext ctx)
    {
        var limit = ctx.Config.Metrics.MaxBoolParameterCount;
        if (limit <= 0) return;

        foreach (var prefix in ctx.Config.Metrics.MaxBoolParameterCountExemptMethodPrefixes)
        {
            if (memberName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return;
        }

        var boolCount = CountBoolParameters(paramList, ctx);
        if (boolCount > limit)
        {
            ctx.ReportViolation(node,
                nameof(ctx.Config.Metrics.MaxBoolParameterCount),
                $"'{memberName}' hat {boolCount} bool-Parameter (erlaubt: {limit}). Bool-Parameter sind an der Call-Site opak: 'DoWork(true, false)' trägt keine semantische Information.",
                "Fasse die bool-Parameter in ein Parameter-Object zusammen: 'sealed record WorkOptions(bool EnableX, bool EnableY)' — damit werden Call-Sites selbsterklärend.");
        }
    }

    private static int CountBoolParameters(ParameterListSyntax paramList, CheckerContext ctx)
    {
        var count = 0;
        foreach (var param in paramList.Parameters)
        {
            if (param.Type != null && IsBoolType(param.Type, ctx))
                count++;
        }
        return count;
    }

    private static bool IsBoolType(TypeSyntax type, CheckerContext ctx)
    {
        var typeInfo = ctx.SemanticModel.GetTypeInfo(type);
        var t = typeInfo.Type;
        if (t == null) return false;

        if (t.SpecialType == SpecialType.System_Boolean) return true;

        if (t is INamedTypeSymbol named
            && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && named.TypeArguments.Length == 1
            && named.TypeArguments[0].SpecialType == SpecialType.System_Boolean)
            return true;

        return false;
    }

    internal static bool IsPrivateOrProtected(SyntaxTokenList modifiers) =>
        modifiers.Any(SyntaxKind.PrivateKeyword) || modifiers.Any(SyntaxKind.ProtectedKeyword);
}
