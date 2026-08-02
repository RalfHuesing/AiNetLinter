#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

/// <summary>
/// Erkennt Switch-Dispatcher-Methoden: Methoden die nur als Routing-Tabelle
/// fungieren und deren Cases alle trivial (Methodenaufruf + return / throw) sind.
/// </summary>
internal static class SwitchDispatcherDetector
{
    /// <summary>
    /// Gibt true zurück wenn die Methode als Switch-Dispatcher klassifiziert wird.
    /// </summary>
    public static bool IsDispatcher(MethodDeclarationSyntax node, int maxCaseBodyLines)
    {
        if (node.Body != null)
        {
            var statements = node.Body.Statements;
            if (statements.Count == 0) return false;
            return AllStatementsAreTrivialBranches(statements, maxCaseBodyLines);
        }
        else if (node.ExpressionBody != null)
        {
            return IsTrivialExpressionBody(node.ExpressionBody.Expression, maxCaseBodyLines);
        }

        return false;
    }

    private static bool AllStatementsAreTrivialBranches(
        SyntaxList<StatementSyntax> statements,
        int maxCaseBodyLines)
    {
        int branchCount = 0;
        foreach (var stmt in statements)
        {
            if (!IsTrivialStatement(stmt, maxCaseBodyLines, ref branchCount))
                return false;
        }
        return branchCount >= 3;
    }

    private static bool IsTrivialStatement(StatementSyntax stmt, int maxCaseBodyLines, ref int branchCount)
    {
        if (stmt is IfStatementSyntax ifStmt)
            return IsTrivialIfStatement(ifStmt, maxCaseBodyLines, ref branchCount);

        if (stmt is SwitchStatementSyntax switchStmt)
            return IsTrivialSwitchStatement(switchStmt, maxCaseBodyLines, ref branchCount);

        if (stmt is ReturnStatementSyntax retStmt)
            return retStmt.Expression == null || IsTrivialExpression(retStmt.Expression);

        if (stmt is ThrowStatementSyntax throwStmt)
            return throwStmt.Expression == null || IsTrivialExpression(throwStmt.Expression);

        return false;
    }

    private static bool IsTrivialIfStatement(IfStatementSyntax ifStmt, int maxCaseBodyLines, ref int branchCount)
    {
        if (!IsTrivialBranch(ifStmt.Statement, maxCaseBodyLines)) return false;
        if (ifStmt.Else != null && !IsTrivialElseBranch(ifStmt.Else.Statement, maxCaseBodyLines, ref branchCount))
            return false;
        branchCount++;
        return true;
    }

    private static bool IsTrivialSwitchStatement(SwitchStatementSyntax switchStmt, int maxCaseBodyLines, ref int branchCount)
    {
        foreach (var section in switchStmt.Sections)
        {
            if (!IsTrivialSwitchSection(section, maxCaseBodyLines)) return false;
            branchCount++;
        }
        return true;
    }

    private static bool IsTrivialElseBranch(StatementSyntax stmt, int maxCaseBodyLines, ref int branchCount)
    {
        if (stmt is IfStatementSyntax nestedIf)
        {
            if (!IsTrivialBranch(nestedIf.Statement, maxCaseBodyLines)) return false;
            branchCount++;
            if (nestedIf.Else != null)
            {
                return IsTrivialElseBranch(nestedIf.Else.Statement, maxCaseBodyLines, ref branchCount);
            }
            return true;
        }
        return IsTrivialBranch(stmt, maxCaseBodyLines);
    }

    private static bool IsTrivialBranch(StatementSyntax stmt, int maxLines)
    {
        if (stmt is ReturnStatementSyntax retStmt)
        {
            return retStmt.Expression == null || IsTrivialExpression(retStmt.Expression);
        }

        if (stmt is BlockSyntax block)
        {
            var lineCount = MethodLineCounter.GetCodeLineCount(block);
            return lineCount <= maxLines
                && block.Statements.All(s => s is ReturnStatementSyntax
                    or BreakStatementSyntax
                    or ExpressionStatementSyntax
                    or ThrowStatementSyntax);
        }

        if (stmt is ExpressionStatementSyntax exprStmt)
        {
            return IsTrivialExpression(exprStmt.Expression);
        }

        if (stmt is ThrowStatementSyntax throwStmt)
        {
            return throwStmt.Expression == null || IsTrivialExpression(throwStmt.Expression);
        }

        return false;
    }

    private static bool IsTrivialSwitchSection(SwitchSectionSyntax section, int maxLines)
    {
        var stmts = section.Statements;
        if (stmts.Count == 0) return true; // Fall-through case

        var lineCount = stmts.Sum(s => MethodLineCounter.GetCodeLineCount(s));
        if (lineCount > maxLines) return false;

        return stmts.All(s => s is ReturnStatementSyntax
            or BreakStatementSyntax
            or ExpressionStatementSyntax
            or ThrowStatementSyntax);
    }

    private static bool IsTrivialExpressionBody(ExpressionSyntax expr, int maxLines)
    {
        if (expr is SwitchExpressionSyntax switchExpr)
        {
            int branchCount = 0;
            foreach (var arm in switchExpr.Arms)
            {
                if (!IsTrivialExpression(arm.Expression)) return false;
                branchCount++;
            }
            return branchCount >= 3;
        }
        return false;
    }

    private static bool IsTrivialExpression(ExpressionSyntax expr)
    {
        if (expr is InvocationExpressionSyntax) return true;
        if (expr is AwaitExpressionSyntax) return true;
        if (expr is MemberAccessExpressionSyntax) return true;
        if (expr is ObjectCreationExpressionSyntax) return true;
        if (expr is LiteralExpressionSyntax) return true;
        if (expr is IdentifierNameSyntax) return true;
        if (expr is ThrowExpressionSyntax) return true;

        if (expr is ParenthesizedExpressionSyntax parenExpr)
        {
            return IsTrivialExpression(parenExpr.Expression);
        }
        if (expr is CastExpressionSyntax castExpr)
        {
            return IsTrivialExpression(castExpr.Expression);
        }

        return false;
    }

    /// <summary>
    /// Berechnet die angepasste McCabe-Komplexität ohne Dispatcher-Branches.
    /// Gibt 1 zurück (Basis-Komplexität der Methode selbst).
    /// </summary>
    public static int GetAdjustedCyclomaticComplexity(MethodDeclarationSyntax node) => 1;

    /// <summary>
    /// Berechnet die angepasste Kognitive Komplexität ohne Dispatcher-Branches.
    /// </summary>
    public static int GetAdjustedCognitiveComplexity(MethodDeclarationSyntax node) => 1;
}
