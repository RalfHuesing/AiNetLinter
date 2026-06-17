#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;
using System;
using System.Collections.Generic;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling naming rules such as PascalCase, semantic parameter names, and XML documentation.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Interface");
        CheckPascalCase(node.Identifier, "Interface");
        base.VisitInterfaceDeclaration(node);
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        CheckPascalCase(node.Identifier, "Eigenschaft");
        base.VisitPropertyDeclaration(node);
    }

    private void CheckPascalCase(SyntaxToken identifier, string kind)
    {
        if (ShouldSkipPascalCase()) return;

        var name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return;
        if (!char.IsUpper(name[0]))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                RuleName = nameof(_config.Global.EnforcePascalCase),
                Details = $"Der Name '{name}' ({kind}) ist nicht in PascalCase geschrieben.",
                Guidance = "Aendere den ersten Buchstaben des Namens in einen Grossbuchstaben."
            });
        }
    }

    private bool ShouldSkipPascalCase()
    {
        if (!_config.Global.EnforcePascalCase) return true;
        return _isTestFile;
    }

    private void CheckSemanticNaming(ParameterListSyntax parameterList, bool isPublicMethod, string? methodName = null)
    {
        if (ShouldSkipSemanticNaming(isPublicMethod, methodName)) return;

        var genericNames = ForbiddenNames;
        foreach (var param in parameterList.Parameters)
        {
            CheckParameterSemantic(param, genericNames, methodName);
        }
    }

    private bool ShouldSkipSemanticNaming(bool isPublicMethod, string? methodName)
    {
        if (!_config.Global.EnforceSemanticNaming) return true;
        if (!isPublicMethod) return true;
        if (_isTestFile) return true;
        if (methodName is not null
            && _config.Global.SemanticNamingExemptMethodNames.Contains(
                   methodName, StringComparer.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "temp", "obj", "val", "tmp", "item", "param"
    };

    private void CheckParameterSemantic(ParameterSyntax param, HashSet<string> genericNames, string? methodName)
    {
        var name = param.Identifier.Text;
        if (!genericNames.Contains(name)) return;

        if (_config.Global.SemanticNamingAllowSubstringOfMethodName
            && methodName is not null
            && methodName.Contains(name, StringComparison.OrdinalIgnoreCase))
            return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(param),
            RuleName = nameof(_config.Global.EnforceSemanticNaming),
            Details = $"Der Parameter '{name}' in einer oeffentlichen Methode hat einen generischen, nicht-semantischen Namen.",
            Guidance = "Verwende einen aussagekraeftigen Parameternamen, der die Absicht und den Typ des Parameters beschreibt."
        });
    }

    private void CheckXmlDoc(SyntaxNode node, string name, string kind)
    {
        if (ShouldSkipXmlDoc(node)) return;

        if (!HasXmlDocumentation(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.EnforceXmlDocumentation),
                Details = $"Das oeffentliche Element '{name}' ({kind}) hat keine XML-Dokumentation (/// <summary>).",
                Guidance = "Fuege ein XML-Dokumentationskommentar hinzu, um die Absicht des Elements zu beschreiben."
            });
        }
    }

    private bool ShouldSkipXmlDoc(SyntaxNode node)
    {
        if (!_config.Global.EnforceXmlDocumentation) return true;
        if (_isTestFile) return true;

        // XML-Dokumentation wird nur für Typen (Klassen, Interfaces, Structs, Records) verlangt,
        // um redundanten Boilerplate-Kommentierungstext (Token-Müll) zu vermeiden.
        if (node is not BaseTypeDeclarationSyntax)
        {
            return true;
        }

        return !IsInPublicContext(node);
    }

    private static bool IsInPublicContext(SyntaxNode node)
    {
        var current = node;
        while (current != null)
        {
            if (!IsPublic(current)) return false;
            current = current.Parent;
        }
        return true;
    }

    private static bool IsPublic(SyntaxNode node)
    {
        if (node is BaseTypeDeclarationSyntax typeDecl)
        {
            return typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
        if (node is MethodDeclarationSyntax method)
        {
            return method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        }
        return true;
    }

    private static bool HasXmlDocumentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        return trivia.Any(t => 
            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || 
            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
    }
}
