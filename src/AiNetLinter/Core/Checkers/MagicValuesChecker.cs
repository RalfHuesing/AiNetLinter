#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class MagicValuesChecker
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MagicValuesConfig, IReadOnlyList<System.Text.RegularExpressions.Regex>> RegexCache = new();

    internal static void Check(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceNoMagicValues) return;
        if (!IsMagicValue(node, ctx)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = "EnforceNoMagicValues",
            Details = $"Magischer Wert '{node.ToString()}' im Code gefunden.",
            Guidance = "Deklariere den Wert als 'const' oder 'static readonly' Feld, oder nutze ein 'enum', um die Semantik explizit zu benennen."
        });
    }

    private static bool IsMagicValue(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        if (!IsTargetLiteral(node)) return false;
        if (IsExceptionValue(node, ctx)) return false;
        if (IsConstDeclaration(node)) return false;
        if (IsAttributeArgument(node)) return false;
        if (!IsInsideBody(node)) return false;
        if (IsIgnoredByMode(node, ctx)) return false;
        if (IsIgnoredByStringPattern(node, ctx)) return false;
        if (IsIgnoredByInvocationContext(node, ctx)) return false;
        if (IsIgnoredByCollectionInitializer(node, ctx)) return false;
        return true;
    }

    private static bool IsTargetLiteral(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        return kind == SyntaxKind.NumericLiteralExpression || kind == SyntaxKind.StringLiteralExpression;
    }

    private static bool IsExceptionValue(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        var kind = node.Kind();
        if (kind == SyntaxKind.StringLiteralExpression) return node.Token.ValueText == "";
        if (kind == SyntaxKind.NumericLiteralExpression) return IsExceptionNumeric(node.Token.Value, ctx);
        return false;
    }

    private static bool IsExceptionNumeric(object? value, CheckerContext ctx)
    {
        if (value == null) return false;

        var extras = ctx.Config.MagicValues.IgnoreNumericValues;
        if (extras != null && extras.Count > 0)
        {
            try
            {
                var d = Convert.ToDouble(value);
                if (extras.Contains(d)) return true;
            }
            catch (Exception ignored)
            {
                _ = ignored;
            }
        }

        var type = value.GetType();
        if (type == typeof(decimal)) return (decimal)value is 0m or 1m or -1m;
        return IsPrimitiveNumeric(type, value);
    }

    private static bool IsPrimitiveNumeric(Type type, object value)
    {
        if (!type.IsPrimitive || type == typeof(bool) || type == typeof(char)) return false;
        var d = Convert.ToDouble(value);
        return d is 0.0 or 1.0 or -1.0;
    }

    private static bool IsIgnoredByMode(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        var mode = ctx.Config.MagicValues.Mode;
        if (string.Equals(mode, "numeric-only", StringComparison.OrdinalIgnoreCase))
            return node.IsKind(SyntaxKind.StringLiteralExpression);
        if (string.Equals(mode, "numeric-and-short-string", StringComparison.OrdinalIgnoreCase)
            && node.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return node.Token.ValueText.Length < ctx.Config.MagicValues.MinStringLength;
        }
        return false;
    }

    private static bool IsIgnoredByStringPattern(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        if (!node.IsKind(SyntaxKind.StringLiteralExpression)) return false;
        var regexes = GetCompiledRegexes(ctx.Config.MagicValues);
        if (regexes.Count == 0) return false;

        var value = node.Token.ValueText;
        foreach (var regex in regexes)
            if (regex.IsMatch(value)) return true;
        return false;
    }

    private static bool IsIgnoredByInvocationContext(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        var prefixes = ctx.Config.MagicValues.IgnoreInvocationPrefixes;
        if (prefixes == null || prefixes.Count == 0) return false;
        var methodName = GetEnclosingInvocationMethodName(node);
        if (methodName == null) return false;
        foreach (var prefix in prefixes)
            if (methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string? GetEnclosingInvocationMethodName(SyntaxNode node)
    {
        if (node.Parent is not ArgumentSyntax arg) return null;
        if (arg.Parent is not ArgumentListSyntax argList) return null;
        if (argList.Parent is not InvocationExpressionSyntax invocation) return null;
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberBindingExpressionSyntax mb => mb.Name.Identifier.Text,
            _ => null
        };
    }

    private static bool IsIgnoredByCollectionInitializer(LiteralExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.MagicValues.IgnoreCollectionInitializers) return false;
        return node.Ancestors().OfType<InitializerExpressionSyntax>().Any();
    }

    private static bool IsAttributeArgument(SyntaxNode node) =>
        node.Ancestors().OfType<AttributeArgumentSyntax>().Any();

    private static bool IsInsideBody(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (IsBodyContainer(current)) return true;
            current = current.Parent;
        }
        return false;
    }

    private static bool IsBodyContainer(SyntaxNode node)
    {
        if (node is not BlockSyntax and not ArrowExpressionClauseSyntax) return false;
        var p = node.Parent;
        return p is MethodDeclarationSyntax or ConstructorDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax;
    }

    private static bool IsConstDeclaration(LiteralExpressionSyntax node)
    {
        var parent = node.Parent;
        while (parent != null)
        {
            var (isConst, shouldBreak) = IsConstStatement(parent);
            if (isConst) return true;
            if (shouldBreak) break;
            parent = parent.Parent;
        }
        return false;
    }

    private static (bool IsConst, bool ShouldBreak) IsConstStatement(SyntaxNode node)
    {
        var shouldBreak = node is BlockSyntax or MethodDeclarationSyntax or ConstructorDeclarationSyntax;
        if (node is LocalDeclarationStatementSyntax localDecl)
            return (localDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)), shouldBreak);
        if (node is FieldDeclarationSyntax fieldDecl)
            return (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)), shouldBreak);
        return (false, shouldBreak);
    }

    private static IReadOnlyList<System.Text.RegularExpressions.Regex> GetCompiledRegexes(MagicValuesConfig config)
    {
        return RegexCache.GetValue(config, cfg =>
        {
            var list = new List<System.Text.RegularExpressions.Regex>();
            foreach (var pattern in cfg.IgnoreStringPatterns)
            {
                try
                {
                    list.Add(new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Compiled));
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine($"[WARNING]: Ungueltiges Regex-Muster '{pattern}' unter IgnoreStringPatterns wird ignoriert: {ex.Message}");
                }
            }
            return list;
        });
    }
}
