#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Generators;

internal sealed class PlaybookSyntaxWalker : CSharpSyntaxWalker
{
    private readonly SemanticModel _semanticModel;
    private readonly IReadOnlyCollection<string>? _allowedExceptions;

    public int ResultPatternCount { get; private set; }
    public int ThrowCount { get; private set; }

    public PlaybookSyntaxWalker(SemanticModel semanticModel, IReadOnlyCollection<string>? allowedExceptions) : base(SyntaxWalkerDepth.Node)
    {
        _semanticModel = semanticModel;
        _allowedExceptions = allowedExceptions;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol != null && IsOrContainsResult(symbol.ReturnType))
        {
            ResultPatternCount++;
        }
        base.VisitMethodDeclaration(node);
    }

    private bool IsProjectInternal(ITypeSymbol typeSymbol)
    {
        return SymbolEqualityComparer.Default.Equals(
            typeSymbol.ContainingAssembly,
            _semanticModel.Compilation.Assembly);
    }

    public override void VisitThrowStatement(ThrowStatementSyntax node)
    {
        if (!IsAllowedException(node.Expression))
        {
            ThrowCount++;
        }
        base.VisitThrowStatement(node);
    }

    public override void VisitThrowExpression(ThrowExpressionSyntax node)
    {
        if (!IsAllowedException(node.Expression))
        {
            ThrowCount++;
        }
        base.VisitThrowExpression(node);
    }

    private bool IsAllowedException(ExpressionSyntax? expression)
    {
        if (expression is not ObjectCreationExpressionSyntax creation) return false;
        if (_allowedExceptions == null) return false;

        var typeSymbol = _semanticModel.GetTypeInfo(creation).Type;
        if (typeSymbol == null) return false;

        return _allowedExceptions.Contains(typeSymbol.Name);
    }

    private bool IsOrContainsResult(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.Name == "Result")
        {
            return true;
        }

        if (IsProjectInternal(typeSymbol) && typeSymbol.Name.EndsWith("Result", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            return IsGenericResultWrapper(namedType);
        }

        return false;
    }

    private bool IsGenericResultWrapper(INamedTypeSymbol namedType)
    {
        if (!namedType.IsGenericType)
        {
            return false;
        }

        if (namedType.Name != "Task" && namedType.Name != "ValueTask")
        {
            return false;
        }

        var innerType = namedType.TypeArguments.FirstOrDefault();
        return innerType != null && IsOrContainsResult(innerType);
    }
}
