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
        return HasDtoOrEntityAttribute(node);
    }

    private bool HasImmutabilityExemptSuffix(string className)
    {
        var suffixes = _config.Global.ImmutabilityExemptSuffixes;
        return suffixes != null && suffixes.Any(s => className.EndsWith(s, StringComparison.OrdinalIgnoreCase));
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
}
