using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

/// <summary>
/// Zählt Codezeilen einer Methode ohne Kommentare und Leerzeilen.
/// </summary>
public static class MethodLineCounter
{
    /// <summary>
    /// Ermittelt die Anzahl der Codezeilen einer Methode (Signatur und Körper, ohne Kommentare und Leerzeilen).
    /// Gibt 0 zurück, wenn die Methode keinen implementierten Körper hat.
    /// </summary>
    public static int GetCodeLineCount(MethodDeclarationSyntax method)
    {
        if (method.Body == null && method.ExpressionBody == null)
        {
            return 0;
        }

        var codeLines = new HashSet<int>();
        foreach (var token in method.DescendantTokens(descendIntoTrivia: false))
        {
            if (token.IsKind(SyntaxKind.EndOfFileToken))
            {
                continue;
            }

            var line = token.GetLocation().GetLineSpan().StartLinePosition.Line;
            codeLines.Add(line);
        }

        return codeLines.Count;
    }
}
