#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class ImmutabilityChecker
{
    internal static void CheckClass(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceExplicitStateImmutability) return;
        if (ctx.IsTestFile) return;

        var className = node.Identifier.Text;
        if (IsDtoOrEntity(node, className, ctx)) return;

        CheckProperties(node, className, ctx);
        CheckFields(node, className, ctx);
    }

    private static void CheckProperties(ClassDeclarationSyntax node, string className, CheckerContext ctx)
    {
        foreach (var prop in node.Members.OfType<PropertyDeclarationSyntax>())
        {
            if (prop.AccessorList == null) continue;
            var setAccessor = prop.AccessorList.Accessors
                .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

            if (setAccessor == null) continue;
            if (setAccessor.Modifiers.Any(SyntaxKind.InitKeyword)) continue;

            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(prop),
                RuleName = "EnforceExplicitStateImmutability",
                Details = $"Die Eigenschaft '{prop.Identifier.Text}' der Klasse '{className}' hat einen veränderbaren 'set'-Accessor.",
                Guidance = "Verwende 'init' oder mache die Eigenschaft get-only, um Immutability zu garantieren."
            });
        }
    }

    private static void CheckFields(ClassDeclarationSyntax node, string className, CheckerContext ctx)
    {
        foreach (var fieldDecl in node.Members.OfType<FieldDeclarationSyntax>())
        {
            if (!IsMutableField(fieldDecl)) continue;
            if (ctx.Config.Global.ImmutabilityAllowPrivateBackingFields && IsPrivateBackingField(fieldDecl)) continue;

            foreach (var variable in fieldDecl.Declaration.Variables)
            {
                ctx.AddViolation(new RuleViolation
                {
                    FilePath = ctx.FilePath,
                    LineNumber = SyntaxHelper.LineOf(variable),
                    RuleName = "EnforceExplicitStateImmutability",
                    Details = $"Das Feld '{variable.Identifier.Text}' in der Klasse '{className}' ist nicht als 'readonly' deklariert.",
                    Guidance = "Füge den Modifikator 'readonly' hinzu."
                });
            }
        }
    }

    private static bool IsMutableField(FieldDeclarationSyntax fieldDecl)
    {
        var isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);
        var isReadonly = fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        return !isConst && !isReadonly;
    }

    private static bool IsDtoOrEntity(ClassDeclarationSyntax node, string className, CheckerContext ctx)
    {
        if (HasImmutabilityExemptSuffix(className, ctx)) return true;
        if (HasImmutabilityExemptPattern(className, ctx)) return true;
        if (IsConfigurationBindingOrJsonSerializable(node, ctx)) return true;
        if (HasExemptBaseType(node, ctx)) return true;
        return HasDtoOrEntityAttribute(node, ctx);
    }

    private static bool HasImmutabilityExemptSuffix(string className, CheckerContext ctx)
    {
        var suffixes = ctx.Config.Global.ImmutabilityExemptSuffixes;
        return suffixes != null && suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasImmutabilityExemptPattern(string className, CheckerContext ctx)
    {
        var patterns = ctx.Config.Global.ImmutabilityExemptPatterns;
        if (patterns == null) return false;
        return patterns.Any(p => MatchWildcard(className, p));
    }

    private static bool MatchWildcard(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (pattern == "*") return true;
        if (pattern.StartsWith("*") && pattern.EndsWith("*"))
            return text.Contains(pattern.Substring(1, pattern.Length - 2), StringComparison.OrdinalIgnoreCase);
        if (pattern.StartsWith("*"))
            return text.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
        if (pattern.EndsWith("*"))
            return text.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase);
        return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigurationBindingOrJsonSerializable(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        if (HasBindingOrSerializableAttribute(symbol)) return true;

        foreach (var iface in symbol.AllInterfaces)
            if (iface.Name.Contains("IOptions") || iface.Name.Contains("IConfiguration"))
                return true;

        var currentBase = symbol.BaseType;
        while (currentBase != null)
        {
            if (currentBase.Name.Contains("IOptions") || currentBase.Name.Contains("IConfiguration"))
                return true;
            currentBase = currentBase.BaseType;
        }

        return false;
    }

    private static bool HasBindingOrSerializableAttribute(INamedTypeSymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName != null && (attrName.Contains("JsonSerializable") || attrName.Contains("Configure") || attrName.Contains("Options")))
                return true;
        }
        return false;
    }

    private static bool HasDtoOrEntityAttribute(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;
        return symbol.GetAttributes().Any(a =>
        {
            var name = a.AttributeClass?.Name;
            return name != null && (name.Contains("Dto") || name.Contains("Entity"));
        });
    }

    private static bool HasExemptBaseType(ClassDeclarationSyntax node, CheckerContext ctx)
    {
        var exemptTypes = ctx.Config.Global.ImmutabilityExemptBaseTypes;
        if (exemptTypes == null || exemptTypes.Count == 0) return false;

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (exemptTypes.Contains(current.Name, StringComparer.OrdinalIgnoreCase)) return true;
            current = current.BaseType;
        }

        foreach (var iface in symbol.AllInterfaces)
            if (exemptTypes.Contains(iface.Name, StringComparer.OrdinalIgnoreCase)) return true;

        return false;
    }

    private static bool IsPrivateBackingField(FieldDeclarationSyntax fieldDecl)
    {
        var isPrivate = fieldDecl.Modifiers.Any(SyntaxKind.PrivateKeyword)
            || !fieldDecl.Modifiers.Any(m =>
                m.IsKind(SyntaxKind.PublicKeyword) ||
                m.IsKind(SyntaxKind.ProtectedKeyword) ||
                m.IsKind(SyntaxKind.InternalKeyword));

        if (!isPrivate) return false;
        return fieldDecl.Declaration.Variables
            .All(v => v.Identifier.Text.StartsWith("_", StringComparison.Ordinal));
    }
}
