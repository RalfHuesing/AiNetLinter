#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.DuplicateDetection;

internal static class MethodBodyLocator
{
    internal static SyntaxNode? GetBody(SyntaxNode declaration) => declaration switch
    {
        BaseMethodDeclarationSyntax method => method.Body ?? (SyntaxNode?)method.ExpressionBody,
        AccessorDeclarationSyntax accessor => accessor.Body ?? (SyntaxNode?)accessor.ExpressionBody,
        LocalFunctionStatementSyntax localFunction => localFunction.Body ?? (SyntaxNode?)localFunction.ExpressionBody,
        _ => null,
    };
}
