using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Metrics;

/// <summary>
/// Berechnet Komplexitätsmetriken für C#-Syntaxknoten.
/// </summary>
public static class ComplexityCalculator
{
    /// <summary>
    /// Berechnet die Zyklomatische Komplexität (McCabe) für eine Methode oder Eigenschaft.
    /// </summary>
    public static int GetCyclomaticComplexity(MethodDeclarationSyntax method)
    {
        var walker = new CyclomaticComplexityWalker();
        walker.Visit(method);
        return walker.Complexity;
    }

    /// <summary>
    /// Berechnet die Kognitive Komplexität (SonarSource) für eine Methode oder Eigenschaft.
    /// </summary>
    public static int GetCognitiveComplexity(MethodDeclarationSyntax method)
    {
        var walker = new CognitiveComplexityWalker();
        walker.Visit(method);
        return walker.Complexity;
    }

    /// <summary>
    /// Walker zur Berechnung der zyklomatischen Komplexität (McCabe).
    /// </summary>
    private sealed class CyclomaticComplexityWalker : CSharpSyntaxWalker
    {
        public int Complexity { get; private set; } = 1; // Basis-Komplexität ist immer 1

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Complexity++;
            base.VisitIfStatement(node);
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            Complexity++;
            base.VisitWhileStatement(node);
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            Complexity++;
            base.VisitDoStatement(node);
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            Complexity++;
            base.VisitForStatement(node);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            Complexity++;
            base.VisitForEachStatement(node);
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            Complexity++;
            base.VisitCatchClause(node);
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Complexity++;
            base.VisitConditionalExpression(node);
        }

        public override void VisitSwitchSection(SwitchSectionSyntax node)
        {
            // Jedes Nicht-Default case-Label erhöht die Verzweigung
            Complexity += node.Labels.Count(l => !l.IsKind(SyntaxKind.DefaultSwitchLabel));
            base.VisitSwitchSection(node);
        }

        public override void VisitSwitchExpressionArm(SwitchExpressionArmSyntax node)
        {
            Complexity++;
            base.VisitSwitchExpressionArm(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            // Logische Operatoren und Null-Koaleszenz zählen als Verzweigungspunkte
            if (node.IsKind(SyntaxKind.LogicalAndExpression) ||
                node.IsKind(SyntaxKind.LogicalOrExpression) ||
                node.IsKind(SyntaxKind.CoalesceExpression))
            {
                Complexity++;
            }
            base.VisitBinaryExpression(node);
        }
    }

    /// <summary>
    /// Walker zur Berechnung der Kognitiven Komplexität (SonarSource).
    /// </summary>
    private sealed class CognitiveComplexityWalker : CSharpSyntaxWalker
    {
        public int Complexity { get; private set; }
        private int _nestingLevel;

        private void VisitNestingStructure(SyntaxNode node, Action visitChildren)
        {
            Complexity++; // Struktureller Zuwachs
            Complexity += _nestingLevel; // Schachtelungs-Zuwachs

            var prevNesting = _nestingLevel;
            _nestingLevel++;
            visitChildren();
            _nestingLevel = prevNesting;
        }

        public override void VisitIfStatement(IfStatementSyntax node)
        {
            Complexity++;

            // Schachtelungs-Zuwachs entfällt bei 'else if'
            bool isElseIf = node.Parent is ElseClauseSyntax;
            if (!isElseIf)
            {
                Complexity += _nestingLevel;
            }

            Visit(node.Condition);

            var prevNesting = _nestingLevel;
            _nestingLevel++;
            Visit(node.Statement);
            _nestingLevel = prevNesting;

            if (node.Else != null)
            {
                Visit(node.Else);
            }
        }

        public override void VisitElseClause(ElseClauseSyntax node)
        {
            // plain 'else' erhöht Komplexität um 1, aber kein Schachtelungs-Zuwachs
            bool isElseIf = node.Statement is IfStatementSyntax;
            if (!isElseIf)
            {
                Complexity++;
            }

            var prevNesting = _nestingLevel;
            if (!isElseIf)
            {
                _nestingLevel++;
            }
            Visit(node.Statement);
            _nestingLevel = prevNesting;
        }

        public override void VisitWhileStatement(WhileStatementSyntax node)
        {
            VisitNestingStructure(node, () =>
            {
                Visit(node.Condition);
                Visit(node.Statement);
            });
        }

        public override void VisitDoStatement(DoStatementSyntax node)
        {
            VisitNestingStructure(node, () =>
            {
                Visit(node.Statement);
                Visit(node.Condition);
            });
        }

        public override void VisitForStatement(ForStatementSyntax node)
        {
            VisitNestingStructure(node, () => VisitForStatementChildren(node));
        }

        private void VisitForStatementChildren(ForStatementSyntax node)
        {
            if (node.Declaration != null) Visit(node.Declaration);
            foreach (var init in node.Initializers) Visit(init);
            if (node.Condition != null) Visit(node.Condition);
            foreach (var incr in node.Incrementors) Visit(incr);
            Visit(node.Statement);
        }

        public override void VisitForEachStatement(ForEachStatementSyntax node)
        {
            VisitNestingStructure(node, () =>
            {
                Visit(node.Expression);
                Visit(node.Statement);
            });
        }

        public override void VisitCatchClause(CatchClauseSyntax node)
        {
            VisitNestingStructure(node, () =>
            {
                if (node.Filter != null) Visit(node.Filter);
                Visit(node.Block);
            });
        }

        public override void VisitSwitchStatement(SwitchStatementSyntax node)
        {
            VisitNestingStructure(node, () =>
            {
                Visit(node.Expression);
                foreach (var section in node.Sections)
                {
                    Visit(section);
                }
            });
        }

        public override void VisitSwitchExpression(SwitchExpressionSyntax node)
        {
            VisitNestingStructure(node, () =>
            {
                Visit(node.GoverningExpression);
                foreach (var arm in node.Arms)
                {
                    Visit(arm);
                }
            });
        }

        public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
        {
            Complexity++;
            Complexity += _nestingLevel;
            base.VisitConditionalExpression(node);
        }

        public override void VisitBinaryExpression(BinaryExpressionSyntax node)
        {
            if (IsLogicalExpression(node))
            {
                HandleLogicalExpression(node);
            }
            else if (node.IsKind(SyntaxKind.CoalesceExpression))
            {
                Complexity++;
            }

            base.VisitBinaryExpression(node);
        }

        private static bool IsLogicalExpression(BinaryExpressionSyntax node)
        {
            return node.IsKind(SyntaxKind.LogicalAndExpression) || node.IsKind(SyntaxKind.LogicalOrExpression);
        }

        private void HandleLogicalExpression(BinaryExpressionSyntax node)
        {
            if (IsChainParent(node))
            {
                if (node.Parent != null && node.Kind() != node.Parent.Kind())
                {
                    Complexity++;
                }
            }
            else
            {
                Complexity++;
            }
        }

        private static bool IsChainParent(BinaryExpressionSyntax node)
        {
            return node.Parent is BinaryExpressionSyntax parentBin && IsLogicalExpression(parentBin);
        }

        public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        {
            var prevNesting = _nestingLevel;
            _nestingLevel++;
            base.VisitLocalFunctionStatement(node);
            _nestingLevel = prevNesting;
        }

        public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
        {
            var prevNesting = _nestingLevel;
            _nestingLevel++;
            base.VisitSimpleLambdaExpression(node);
            _nestingLevel = prevNesting;
        }

        public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
        {
            var prevNesting = _nestingLevel;
            _nestingLevel++;
            base.VisitParenthesizedLambdaExpression(node);
            _nestingLevel = prevNesting;
        }
    }
}
