using System.Text.RegularExpressions;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core;

/// <summary>
/// Extrahiert Testabdeckungssignale (typeof, nameof, @covers) aus Testdateien.
/// </summary>
public static partial class TestCoverageCollector
{
    /// <summary>
    /// Sammelt Abdeckungssignale aus einer Testdatei in den Index.
    /// </summary>
    public static void Collect(
        SyntaxTree tree,
        SemanticModel semanticModel,
        TestCoverageIndex index,
        TestSentinelConfig config)
    {
        if (config.RecognizeCoversComment)
        {
            CollectCoversComments(tree, index);
        }

        if (!config.RecognizeTypeofReference)
        {
            return;
        }

        CollectTypeReferences(tree, semanticModel, index);
    }

    private static void CollectTypeReferences(SyntaxTree tree, SemanticModel semanticModel, TestCoverageIndex index)
    {
        var root = tree.GetRoot();
        foreach (var typeOf in root.DescendantNodes().OfType<TypeOfExpressionSyntax>())
        {
            CollectTypeFromExpression(typeOf.Type, semanticModel, index);
        }

        foreach (var nameofExpr in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            CollectNameofReference(nameofExpr, index);
        }
    }

    private static void CollectNameofReference(InvocationExpressionSyntax invocation, TestCoverageIndex index)
    {
        if (!IsNameofInvocation(invocation))
        {
            return;
        }

        if (invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is IdentifierNameSyntax id)
        {
            index.AddReferencedType(id.Identifier.Text);
        }
    }

    private static void CollectCoversComments(SyntaxTree tree, TestCoverageIndex index)
    {
        var text = tree.GetText().ToString();
        foreach (Match match in CoversCommentPattern().Matches(text))
        {
            index.AddCoversComment(match.Groups[1].Value);
        }
    }

    private static void CollectTypeFromExpression(
        TypeSyntax typeSyntax,
        SemanticModel semanticModel,
        TestCoverageIndex index)
    {
        if (semanticModel.GetSymbolInfo(typeSyntax).Symbol is INamedTypeSymbol named)
        {
            index.AddReferencedType(named.Name);
        }
    }

    private static bool IsNameofInvocation(InvocationExpressionSyntax invocation) =>
        invocation.Expression is IdentifierNameSyntax { Identifier.Text: "nameof" };

    [GeneratedRegex(@"//\s*@covers\s+(\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex CoversCommentPattern();
}
