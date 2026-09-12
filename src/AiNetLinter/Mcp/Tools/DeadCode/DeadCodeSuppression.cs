#nullable enable

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.DeadCode;

/// <summary>
/// Erkennt symbolgenaue Suppressions für bewusst indirekt verwendeten Code.
/// </summary>
internal static class DeadCodeSuppression
{
    private const string Marker = "ainetlinter-disable DeadCode";

    internal static bool IsSuppressed(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);

        return symbol.DeclaringSyntaxReferences.Any(reference =>
            HasSuppressionComment(GetDeclarationNode(reference.GetSyntax())));
    }

    private static SyntaxNode GetDeclarationNode(SyntaxNode syntax) =>
        syntax.Parent is VariableDeclarationSyntax { Parent: { } declaration }
            ? declaration
            : syntax;

    private static bool HasSuppressionComment(SyntaxNode declaration)
    {
        var leadingTrivia = declaration.GetLeadingTrivia();
        for (var index = leadingTrivia.Count - 1; index >= 0; index--)
        {
            var trivia = leadingTrivia[index];
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                continue;
            }

            return IsSuppressionComment(trivia);
        }

        return false;
    }

    private static bool IsSuppressionComment(SyntaxTrivia trivia)
    {
        if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)) return false;

        var comment = trivia.ToFullString().Trim();
        if (!comment.StartsWith("//", StringComparison.Ordinal)) return false;

        var directive = comment[2..].TrimStart();
        if (!directive.StartsWith(Marker, StringComparison.OrdinalIgnoreCase)) return false;

        var reason = directive[Marker.Length..].TrimStart();
        if (reason.StartsWith("—", StringComparison.Ordinal)) return reason[1..].Trim().Length > 0;
        if (reason.StartsWith("--", StringComparison.Ordinal)) return reason[2..].Trim().Length > 0;

        return false;
    }
}
