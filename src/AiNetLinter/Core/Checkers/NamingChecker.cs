#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using System.Text.RegularExpressions;

namespace AiNetLinter.Core.Checkers;

internal static partial class NamingChecker
{
    private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "temp", "obj", "val", "tmp", "item", "param"
    };

    [GeneratedRegex(@"^(?:s_|m_|_)?(?:MyRegex|NewMethod|Class1|Interface1|Struct1|Record1|MyClass|MyInterface|MyStruct|MyRecord)\d*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DummyNameRegex();

    internal static void CheckDummyName(SyntaxToken identifier, string kind, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceSemanticNaming) return;

        var name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return;

        if (DummyNameRegex().IsMatch(name))
        {
            ctx.ReportViolation(identifier, new ViolationDescription(
                nameof(ctx.Config.Global.EnforceSemanticNaming),
                $"Der Name '{name}' ({kind}) ist ein generischer Platzhalter- oder Werkzeugname.",
                "Ersetze den Bezeichner durch einen aussagekraeftigen Namen, der den Zweck des Elements beschreibt."));
        }
    }

    internal static void CheckPascalCase(SyntaxToken identifier, string kind, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforcePascalCase) return;
        if (ctx.IsTestFile) return;

        var name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return;
        if (!char.IsUpper(name[0]))
        {
            ctx.ReportViolation(identifier, new ViolationDescription(
                nameof(ctx.Config.Global.EnforcePascalCase),
                $"Der Name '{name}' ({kind}) ist nicht in PascalCase geschrieben.",
                "Aendere den ersten Buchstaben des Namens in einen Grossbuchstaben."));
        }
    }

    internal static void CheckSemanticNaming(ParameterListSyntax parameterList, bool isPublicMethod, CheckerContext ctx, string? methodName = null)
    {
        if (!ctx.Config.Global.EnforceSemanticNaming) return;
        if (!isPublicMethod) return;
        if (ctx.IsTestFile) return;
        if (methodName is not null
            && ctx.Config.Global.SemanticNamingExemptMethodNames.Contains(methodName, StringComparer.OrdinalIgnoreCase))
            return;

        foreach (var param in parameterList.Parameters)
        {
            var name = param.Identifier.Text;
            if (!ForbiddenNames.Contains(name)) continue;

            if (ctx.Config.Global.SemanticNamingAllowSubstringOfMethodName
                && methodName is not null
                && methodName.Contains(name, StringComparison.OrdinalIgnoreCase))
                continue;

            ctx.ReportViolation(param, new ViolationDescription(
                nameof(ctx.Config.Global.EnforceSemanticNaming),
                $"Der Parameter '{name}' in einer oeffentlichen Methode hat einen generischen, nicht-semantischen Namen.",
                "Verwende einen aussagekraeftigen Parameternamen, der die Absicht und den Typ des Parameters beschreibt."));
        }
    }

    internal static void CheckXmlDoc(SyntaxNode node, string name, string kind, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceXmlDocumentation) return;
        if (ctx.IsTestFile) return;

        if (node is not BaseTypeDeclarationSyntax) return;
        if (!IsInPublicContext(node)) return;

        if (!HasXmlDocumentation(node))
        {
            ctx.ReportViolation(node, new ViolationDescription(
                nameof(ctx.Config.Global.EnforceXmlDocumentation),
                $"Das oeffentliche Element '{name}' ({kind}) hat keine XML-Dokumentation (/// <summary>).",
                "Fuege ein XML-Dokumentationskommentar hinzu, um die Absicht des Elements zu beschreiben."));
        }
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
            return typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        if (node is MethodDeclarationSyntax method)
            return method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        return true;
    }

    private static bool HasXmlDocumentation(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        return trivia.Any(t =>
            t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
            t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
    }

    internal static void CheckAscii(SyntaxToken identifier, string kind, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceAsciiIdentifiers) return;

        var name = identifier.Text;
        if (string.IsNullOrEmpty(name)) return;

        if (name.StartsWith("@"))
        {
            name = name.Substring(1);
        }

        foreach (var c in name)
        {
            if (!IsAsciiIdentifierChar(c))
            {
                ctx.ReportViolation(identifier, new ViolationDescription(
                    nameof(ctx.Config.Global.EnforceAsciiIdentifiers),
                    $"Der Name '{identifier.Text}' ({kind}) enthaelt Nicht-ASCII-Zeichen (z. B. '{c}').",
                    "Verwende ausschliesslich ASCII-Zeichen (a-z, A-Z, 0-9, _) fuer Bezeichner. Umschreibe Umlaute (ae, oe, ue, ss) oder waehle englische Bezeichner."));
                return;
            }
        }
    }

    internal static void CheckAsciiNamespace(BaseNamespaceDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceAsciiIdentifiers) return;

        var fullname = node.Name.ToString();
        if (string.IsNullOrEmpty(fullname)) return;

        var segments = fullname.Split('.');
        foreach (var segment in segments)
        {
            var name = segment.StartsWith("@") ? segment.Substring(1) : segment;
            foreach (var c in name)
            {
                if (!IsAsciiIdentifierChar(c))
                {
                    ctx.ReportViolation(node.Name, new ViolationDescription(
                        nameof(ctx.Config.Global.EnforceAsciiIdentifiers),
                        $"Der Namespace-Name '{fullname}' enthaelt Nicht-ASCII-Zeichen (z. B. '{c}').",
                        "Verwende ausschliesslich ASCII-Zeichen (a-z, A-Z, 0-9, _) und Punkte (.) fuer Namespaces."));
                    return;
                }
            }
        }
    }

    private static bool IsAsciiIdentifierChar(char c)
    {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
    }
}
