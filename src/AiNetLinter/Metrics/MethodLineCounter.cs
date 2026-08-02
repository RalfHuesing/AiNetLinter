using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

public static class MethodLineCounter
{
    public static int GetCodeLineCount(MethodDeclarationSyntax method)
    {
        if (method.Body == null && method.ExpressionBody == null)
        {
            return 0;
        }

        return GetCodeLineCount((SyntaxNode)method);
    }

    public static int GetCodeLineCount(SyntaxNode node)
    {
        var codeLines = new HashSet<int>();
        foreach (var token in node.DescendantTokens(descendIntoTrivia: false))
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
