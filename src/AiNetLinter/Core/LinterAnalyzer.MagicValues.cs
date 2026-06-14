#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;
using AiNetLinter.Configuration;

using System.Collections.Generic;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling magic values and non-semantic literal detection.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MagicValuesConfig, IReadOnlyList<System.Text.RegularExpressions.Regex>> RegexCache = new();

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
        if (!IsInsideBody(node)) return false;

        // Neue Checks:
        if (IsIgnoredByMode(node)) return false;
        if (IsIgnoredByStringPattern(node)) return false;
        if (IsIgnoredByInvocationContext(node)) return false;
        if (IsIgnoredByCollectionInitializer(node)) return false;

        return true;
    }

    private static bool IsTargetLiteral(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        return kind == SyntaxKind.NumericLiteralExpression || kind == SyntaxKind.StringLiteralExpression;
    }

    private bool IsExceptionValue(LiteralExpressionSyntax node)
    {
        var kind = node.Kind();
        if (kind == SyntaxKind.StringLiteralExpression) return node.Token.ValueText == "";
        if (kind == SyntaxKind.NumericLiteralExpression) return IsExceptionNumeric(node.Token.Value);
        return false;
    }

    private bool IsExceptionNumeric(object? value)
    {
        if (value == null) return false;

        // Neue: konfigurierbare Werte-Whitelist
        var extras = _config.MagicValues.IgnoreNumericValues;
        if (extras != null && extras.Count > 0)
        {
            try
            {
                var d = Convert.ToDouble(value);
                if (extras.Contains(d)) return true;
            }
            catch (Exception ignored)
            {
                // Ignorieren bei Konvertierungsfehlern
                _ = ignored;
            }
        }

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

    private bool IsIgnoredByMode(LiteralExpressionSyntax node)
    {
        var mode = _config.MagicValues.Mode;
        if (string.Equals(mode, "numeric-only", StringComparison.OrdinalIgnoreCase))
        {
            // Im numeric-only Mode: Strings sind nie magic
            return node.IsKind(SyntaxKind.StringLiteralExpression);
        }
        if (string.Equals(mode, "numeric-and-short-string", StringComparison.OrdinalIgnoreCase))
        {
            if (node.IsKind(SyntaxKind.StringLiteralExpression))
            {
                var text = node.Token.ValueText;
                var minLen = _config.MagicValues.MinStringLength;
                return text.Length < minLen;
            }
        }
        return false;
    }

    private bool IsIgnoredByStringPattern(LiteralExpressionSyntax node)
    {
        if (!node.IsKind(SyntaxKind.StringLiteralExpression)) return false;
        var regexes = GetCompiledRegexes(_config.MagicValues);
        if (regexes.Count == 0) return false;

        var value = node.Token.ValueText;
        foreach (var regex in regexes)
        {
            if (regex.IsMatch(value))
                return true;
        }
        return false;
    }

    private bool IsIgnoredByInvocationContext(LiteralExpressionSyntax node)
    {
        var prefixes = _config.MagicValues.IgnoreInvocationPrefixes;
        if (prefixes == null || prefixes.Count == 0) return false;

        // Prüfe ob das Literal direkt als Argument einer bestimmten Methode übergeben wird
        if (node.Parent is not ArgumentSyntax arg) return false;
        if (arg.Parent is not ArgumentListSyntax argList) return false;
        if (argList.Parent is not InvocationExpressionSyntax invocation) return false;

        var methodName = GetInvocationMethodName(invocation);
        if (methodName == null) return false;

        foreach (var prefix in prefixes)
        {
            if (methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? GetInvocationMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberBindingExpressionSyntax mb => mb.Name.Identifier.Text,
            _ => null
        };
    }

    private bool IsIgnoredByCollectionInitializer(LiteralExpressionSyntax node)
    {
        if (!_config.MagicValues.IgnoreCollectionInitializers) return false;
        return node.Ancestors().OfType<InitializerExpressionSyntax>().Any();
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
