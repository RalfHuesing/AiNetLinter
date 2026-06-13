using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;
using System;
using System.Linq;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling control flow rules such as silent catches and exceptions for control flow.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        CheckSilentCatch(node);
        base.VisitCatchClause(node);
    }

    private void CheckSilentCatch(CatchClauseSyntax node)
    {
        if (ShouldSkipSilentCatch(node)) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = nameof(_config.Global.EnforceNoSilentCatch),
            Details = "Stummes Abfangen (Silent Swallowing) einer Exception erkannt.",
            Guidance = "Wirf die Exception erneut (throw;) oder protokolliere sie, um Fehler im agentischen Loop sichtbar zu machen. Falls das Abfangen bewusst geschieht, benenne die Exception-Variable 'ignored' oder nutze die Inline-Unterdrueckung '// ainetlinter-disable EnforceNoSilentCatch' an der catch-Zeile."
        });
    }

    private bool ShouldSkipSilentCatch(CatchClauseSyntax node)
    {
        if (!_config.Global.EnforceNoSilentCatch) return true;
        if (_isTestFile) return true;
        if (!IsSwallowed(node)) return true;
        if (IsAllowedCancellationCatch(node)) return true;
        return IsExplicitlyIgnored(node);
    }

    private bool IsAllowedCancellationCatch(CatchClauseSyntax node)
    {
        if (!_config.Global.AllowCancellationShutdownCatch) return false;
        if (node.Declaration?.Type is not IdentifierNameSyntax { Identifier.Text: "OperationCanceledException" }) return false;
        return node.Filter != null;
    }

    private static bool IsSwallowed(CatchClauseSyntax node)
    {
        if (node.Block.Statements.Count == 0) return true;

        var hasThrow = node.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any();
        var hasInvoke = node.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any();
        return !hasThrow && !hasInvoke;
    }

    private static bool IsExplicitlyIgnored(CatchClauseSyntax node)
    {
        if (node.Declaration == null) return false;
        var name = node.Declaration.Identifier.Text;
        return name.StartsWith("ignored", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("expected", StringComparison.OrdinalIgnoreCase);
    }

    public override void VisitThrowStatement(ThrowStatementSyntax node)
    {
        CheckResultPatternViolation(node);
        base.VisitThrowStatement(node);
    }

    public override void VisitThrowExpression(ThrowExpressionSyntax node)
    {
        CheckResultPatternViolation(node);
        base.VisitThrowExpression(node);
    }

    private void CheckResultPatternViolation(SyntaxNode node)
    {
        if (!_config.Global.EnforceResultPatternOverExceptions) return;

        if (!IsThrowAllowed(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "EnforceResultPatternOverExceptions",
                Details = "Verwendung von 'throw' fuer Kontrollfluss erkannt.",
                Guidance = "Verwende fuer fachliche Fehlerzustaende das Result-Pattern (Result<T>) statt Exceptions, um den Kontrollfluss fuer KI-Agenten explizit zu machen. 'throw' ist nur in Konstruktoren oder Validierungs-Guards (Methoden mit Suffix 'Guard' oder 'Validate') erlaubt."
            });
        }
    }

    private bool IsThrowAllowed(SyntaxNode node)
    {
        var container = GetEnclosingContainer(node);
        if (container is ConstructorDeclarationSyntax) return true;
        if (container is MethodDeclarationSyntax method) return IsGuardOrValidateName(method.Identifier.Text);
        if (container is LocalFunctionStatementSyntax localFunc) return IsGuardOrValidateName(localFunc.Identifier.Text);
        return false;
    }

    private static SyntaxNode? GetEnclosingContainer(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (IsEnclosingType(current)) return null;
            if (IsContainer(current)) return current;
            current = current.Parent;
        }
        return null;
    }

    private static bool IsEnclosingType(SyntaxNode node) =>
        node is TypeDeclarationSyntax || node is NamespaceDeclarationSyntax || node is CompilationUnitSyntax;

    private static bool IsContainer(SyntaxNode node)
    {
        if (node is MethodDeclarationSyntax || node is ConstructorDeclarationSyntax) return true;
        return node is LocalFunctionStatementSyntax lf && IsGuardOrValidateName(lf.Identifier.Text);
    }

    private static bool IsGuardOrValidateName(string name) =>
        name.EndsWith("Guard", StringComparison.Ordinal) || name.EndsWith("Validate", StringComparison.Ordinal);
}
