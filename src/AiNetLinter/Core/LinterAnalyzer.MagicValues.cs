#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling magic values and non-semantic literal detection.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitLiteralExpression(LiteralExpressionSyntax node)
    {
        CheckMagicValue(node);
        base.VisitLiteralExpression(node);
    }

    private void CheckMagicValue(LiteralExpressionSyntax node)
    {
        if (!_config.Global.EnforceNoMagicValues) return;
        if (IsMagicValue(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "EnforceNoMagicValues",
                Details = $"Magischer Wert '{node.ToString()}' im Code gefunden.",
                Guidance = "Deklariere den Wert als 'const' oder 'static readonly' Feld, oder nutze ein 'enum', um die Semantik explizit zu benennen."
            });
        }
    }

    private bool IsMagicValue(LiteralExpressionSyntax node)
    {
        if (!IsTargetLiteral(node)) return false;
        if (IsExceptionValue(node)) return false;
        if (IsConstDeclaration(node)) return false;
        if (IsAttributeArgument(node)) return false;
        return IsInsideBody(node);
    }

    private static bool IsTargetLiteral(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        return kind == SyntaxKind.NumericLiteralExpression || kind == SyntaxKind.StringLiteralExpression;
    }

    private static bool IsExceptionValue(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        if (kind == SyntaxKind.StringLiteralExpression) return node.Token.ValueText == "";
        if (kind == SyntaxKind.NumericLiteralExpression) return IsExceptionNumeric(node.Token.Value);
        return false;
    }

    private static bool IsExceptionNumeric(object? value)
    {
        if (value == null) return false;
        var type = value.GetType();
        if (type == typeof(decimal))
        {
            return (decimal)value is 0m or 1m or -1m;
        }
        return IsPrimitiveNumeric(type, value);
    }

    private static bool IsPrimitiveNumeric(Type type, object value)
    {
        if (!type.IsPrimitive || type == typeof(bool) || type == typeof(char))
        {
            return false;
        }
        var d = Convert.ToDouble(value);
        return d is 0.0 or 1.0 or -1.0;
    }

    private static bool IsAttributeArgument(SyntaxNode node)
    {
        return node.Ancestors().OfType<AttributeArgumentSyntax>().Any();
    }

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
        {
            return (localDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)), shouldBreak);
        }
        if (node is FieldDeclarationSyntax fieldDecl)
        {
            return (fieldDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.ConstKeyword)), shouldBreak);
        }
        return (false, shouldBreak);
    }
}
