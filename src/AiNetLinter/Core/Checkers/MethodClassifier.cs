#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class MethodClassifier
{
    internal static bool IsNullCoalescingInitializer(
        MethodDeclarationSyntax method,
        double maxNonCoalescingRatio)
    {
        ExpressionSyntax? expr = null;

        if (method.Body is not null)
        {
            var stmts = method.Body.Statements;
            if (stmts.Count == 0) return false;

            var lastStmt = stmts[^1];
            if (lastStmt is not ReturnStatementSyntax ret) return false;
            expr = ret.Expression;

            if (!ArePriorStatementsValid(stmts)) return false;
        }
        else if (method.ExpressionBody is not null)
        {
            expr = method.ExpressionBody.Expression;
        }
        else
        {
            return false;
        }

        expr = expr?.UnwrapParentheses();
        if (expr is not (WithExpressionSyntax or ObjectCreationExpressionSyntax)) return false;

        var assignments = expr.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .ToList();
        if (assignments.Count == 0) return false;

        var coalescing = assignments.Count(a =>
        {
            var right = a.Right.UnwrapParentheses();
            return (right is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.CoalesceExpression))
                || right is ConditionalExpressionSyntax;
        });

        var ratio = 1.0 - (double)coalescing / assignments.Count;
        return ratio <= maxNonCoalescingRatio;
    }

    private static bool ArePriorStatementsValid(SyntaxList<StatementSyntax> stmts)
    {
        for (int i = 0; i < stmts.Count - 1; i++)
        {
            var stmt = stmts[i];
            if (stmt is LocalDeclarationStatementSyntax) continue;
            if (stmt is IfStatementSyntax ifStmt && IsValidGuard(ifStmt)) continue;
            return false;
        }
        return true;
    }

    private static bool IsValidGuard(IfStatementSyntax ifStmt)
    {
        var body = ifStmt.Statement;
        if (body is BlockSyntax block)
        {
            return block.Statements.Count == 1 && block.Statements[0] is ReturnStatementSyntax;
        }
        return body is ReturnStatementSyntax;
    }

    internal static ExpressionSyntax? UnwrapParentheses(this ExpressionSyntax? expr)
    {
        while (expr is ParenthesizedExpressionSyntax paren)
        {
            expr = paren.Expression;
        }
        return expr;
    }
}
