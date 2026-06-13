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

    private void CheckSemanticNaming(ParameterListSyntax parameterList, bool isPublicMethod)
    {
        if (ShouldSkipSemanticNaming(isPublicMethod)) return;

        var genericNames = GetForbiddenNames();
        foreach (var param in parameterList.Parameters)
        {
            CheckParameterSemantic(param, genericNames);
        }
    }

    private bool ShouldSkipSemanticNaming(bool isPublicMethod)
    {
        if (!_config.Global.EnforceSemanticNaming) return true;
        if (!isPublicMethod) return true;
        return _isTestFile;
    }

    private static HashSet<string> GetForbiddenNames()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "data", "temp", "obj", "val", "tmp", "item", "param"
        };
    }

    private void CheckParameterSemantic(ParameterSyntax param, HashSet<string> genericNames)
    {
        var name = param.Identifier.Text;
        if (genericNames.Contains(name))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(param),
                RuleName = nameof(_config.Global.EnforceSemanticNaming),
                Details = $"Der Parameter '{name}' in einer oeffentlichen Methode hat einen generischen, nicht-semantischen Namen.",
                Guidance = "Verwende einen aussagekraeftigen Parameternamen, der die Absicht und den Typ des Parameters beschreibt."
            });
        }
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
        if (node is MethodDeclarationSyntax method && method.Modifiers.Any(m => m.IsKind(SyntaxKind.OverrideKeyword)))
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
