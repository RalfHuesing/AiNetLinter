#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Gemeinsame statische Erweiterungsmethoden für Syntax-Analyse.
/// </summary>
internal static class SyntaxNodeExtensions
{
    internal static int LineOf(this SyntaxNode node) =>
        node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    internal static string? GetSimpleTypeName(this TypeSyntax type)
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
