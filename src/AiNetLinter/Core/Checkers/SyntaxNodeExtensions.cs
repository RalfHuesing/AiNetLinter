#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Gemeinsame statische Hilfsmethoden für Syntax-Analyse, die von mehreren Checker-Klassen benötigt werden.
/// </summary>
internal static class SyntaxHelper
{
    internal static int LineOf(SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    internal static string? GetSimpleTypeName(TypeSyntax type)
    {
        if (type is NullableTypeSyntax nullable)
        {
            type = nullable.ElementType;
        }

        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            _ => null
        };
    }
}
