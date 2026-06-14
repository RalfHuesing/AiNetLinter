#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling class immutability and record contracts.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private void CheckClassImmutability(ClassDeclarationSyntax node)
    {
        if (!_config.Global.EnforceExplicitStateImmutability) return;
        if (_isTestFile) return;

        var className = node.Identifier.Text;
        if (IsDtoOrEntity(node, className)) return;

        CheckPropertiesImmutability(node, className);
        CheckFieldsImmutability(node, className);
    }

    private void CheckPropertiesImmutability(ClassDeclarationSyntax node, string className)
    {
        foreach (var prop in node.Members.OfType<PropertyDeclarationSyntax>())
        {
            CheckPropertyImmutability(prop, className);
        }
    }

    private void CheckPropertyImmutability(PropertyDeclarationSyntax prop, string className)
    {
        if (prop.AccessorList == null) return;
        var setAccessor = prop.AccessorList.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));

        if (setAccessor == null) return;
        if (setAccessor.Modifiers.Any(SyntaxKind.InitKeyword)) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(prop),
            RuleName = "EnforceExplicitStateImmutability",
            Details = $"Die Eigenschaft '{prop.Identifier.Text}' der Klasse '{className}' hat einen veränderbaren 'set'-Accessor.",
            Guidance = "Verwende 'init' oder mache die Eigenschaft get-only, um Immutability zu garantieren."
        });
    }

    private void CheckFieldsImmutability(ClassDeclarationSyntax node, string className)
    {
        foreach (var fieldDecl in node.Members.OfType<FieldDeclarationSyntax>())
        {
            if (IsMutableField(fieldDecl))
            {
                if (_config.Global.ImmutabilityAllowPrivateBackingFields && IsPrivateBackingField(fieldDecl))
                {
                    continue;
                }
                AddMutableFieldViolations(fieldDecl, className);
            }
        }
    }

    private static bool IsMutableField(FieldDeclarationSyntax fieldDecl)
    {
        var isConst = fieldDecl.Modifiers.Any(SyntaxKind.ConstKeyword);
        var isReadonly = fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        return !isConst && !isReadonly;
    }

    private void AddMutableFieldViolations(FieldDeclarationSyntax fieldDecl, string className)
    {
        foreach (var variable in fieldDecl.Declaration.Variables)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(variable),
                RuleName = "EnforceExplicitStateImmutability",
                Details = $"Das Feld '{variable.Identifier.Text}' in der Klasse '{className}' ist nicht als 'readonly' deklariert.",
                Guidance = "Füge den Modifikator 'readonly' hinzu."
            });
        }
    }

    private bool IsDtoOrEntity(ClassDeclarationSyntax node, string className)
    {
        if (HasImmutabilityExemptSuffix(className))
        {
            return true;
        }
        if (HasImmutabilityExemptPattern(className))
        {
            return true;
        }
        if (IsConfigurationBindingOrJsonSerializable(node))
        {
            return true;
        }
        if (HasExemptBaseType(node))
        {
            return true;
        }
        return HasDtoOrEntityAttribute(node);
    }

    private bool HasImmutabilityExemptSuffix(string className)
    {
        var suffixes = _config.Global.ImmutabilityExemptSuffixes;
        return suffixes != null && suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private bool HasImmutabilityExemptPattern(string className)
    {
        var patterns = _config.Global.ImmutabilityExemptPatterns;
        if (patterns == null) return false;
        return patterns.Any(p => MatchWildcard(className, p));
    }

    private static bool MatchWildcard(string text, string pattern)
    {
        if (string.IsNullOrEmpty(pattern)) return false;
        if (pattern == "*") return true;

        if (pattern.StartsWith("*") && pattern.EndsWith("*"))
        {
            var middle = pattern.Substring(1, pattern.Length - 2);
            return text.Contains(middle, StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.StartsWith("*"))
        {
            var end = pattern.Substring(1);
            return text.EndsWith(end, StringComparison.OrdinalIgnoreCase);
        }
        if (pattern.EndsWith("*"))
        {
            var start = pattern.Substring(0, pattern.Length - 1);
            return text.StartsWith(start, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(text, pattern, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsConfigurationBindingOrJsonSerializable(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        if (HasBindingOrSerializableAttribute(symbol)) return true;

        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.Name.Contains("IOptions") || iface.Name.Contains("IConfiguration"))
                return true;
        }

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
            if (attrName != null && (
                attrName.Contains("JsonSerializable") ||
                attrName.Contains("Configure") ||
                attrName.Contains("Options")))
            {
                return true;
            }
        }
        return false;
    }

    private bool HasDtoOrEntityAttribute(ClassDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        return symbol.GetAttributes().Any(IsDtoOrEntityAttribute);
    }

    private static bool IsDtoOrEntityAttribute(AttributeData attr)
    {
        var name = attr.AttributeClass?.Name;
        if (name == null) return false;
        return name.Contains("Dto") || name.Contains("Entity");
    }

    private bool HasExemptBaseType(ClassDeclarationSyntax node)
    {
        var exemptTypes = _config.Global.ImmutabilityExemptBaseTypes;
        if (exemptTypes == null || exemptTypes.Count == 0) return false;

        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        return IsSymbolExemptByBaseType(symbol, exemptTypes);
    }

    private static bool IsSymbolExemptByBaseType(
        INamedTypeSymbol symbol,
        IReadOnlyCollection<string> exemptTypes)
    {
        // Prüfe Basisklassen transitiv
        var current = symbol.BaseType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (exemptTypes.Contains(current.Name, StringComparer.OrdinalIgnoreCase))
                return true;
            current = current.BaseType;
        }

        // Prüfe implementierte Interfaces
        foreach (var iface in symbol.AllInterfaces)
        {
            if (exemptTypes.Contains(iface.Name, StringComparer.OrdinalIgnoreCase))
                return true;
        }

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
