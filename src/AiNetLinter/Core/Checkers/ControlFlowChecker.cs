#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class ControlFlowChecker
{
    internal static void CheckCatch(CatchClauseSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceNoSilentCatch) return;
        if (ctx.IsTestFile) return;
        if (!IsSwallowed(node)) return;
        if (IsAllowedCancellationCatch(node, ctx)) return;
        if (IsAllowedSilentCatchByConfig(node, ctx)) return;
        if (IsExplicitlyIgnored(node)) return;

        ctx.ReportViolation(node, new ViolationDescription(
            nameof(ctx.Config.Global.EnforceNoSilentCatch),
            "Stummes Abfangen (Silent Swallowing) einer Exception erkannt.",
            "Wirf die Exception erneut (throw;) oder protokolliere sie, um Fehler im agentischen Loop sichtbar zu machen. Falls das Abfangen bewusst geschieht, benenne die Exception-Variable 'ignored' oder nutze die Inline-Unterdrueckung '// ainetlinter-disable EnforceNoSilentCatch' an der catch-Zeile."));
    }

    internal static void CheckThrow(SyntaxNode node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceResultPatternOverExceptions) return;
        if (IsAllowedFatalExceptionThrow(node, ctx)) return;
        if (IsInAllowedNamespace(ctx)) return;
        if (IsAllowedCatchRethrow(node, ctx)) return;
        if (IsThrowAllowed(node)) return;

        ctx.ReportViolation(node, new ViolationDescription(
            nameof(ctx.Config.Global.EnforceResultPatternOverExceptions),
            "Verwendung von 'throw' fuer Kontrollfluss erkannt.",
            "Verwende fuer fachliche Fehlerzustaende das Result-Pattern (Result<T>) statt Exceptions, um den Kontrollfluss fuer KI-Agenten explizit zu machen. 'throw' ist nur in Konstruktoren oder Validierungs-Guards (Methoden mit Suffix 'Guard' oder 'Validate') erlaubt."));
    }

    private static bool IsAllowedSilentCatchByConfig(CatchClauseSyntax node, CheckerContext ctx)
    {
        var allowedTypes = ctx.Config.Global.AllowedSilentCatchExceptionTypes;
        if (allowedTypes == null || allowedTypes.Count == 0) return false;
        if (node.Declaration?.Type == null) return false;
        if (node.Declaration.Identifier.Text != "") return false;

        var typeInfo = ctx.SemanticModel.GetTypeInfo(node.Declaration.Type);
        var typeName = typeInfo.Type?.Name ?? node.Declaration.Type.ToString().Split('.').Last();
        return allowedTypes.Contains(typeName, StringComparer.Ordinal);
    }

    private static bool IsAllowedCancellationCatch(CatchClauseSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.AllowCancellationShutdownCatch) return false;
        if (node.Declaration?.Type == null) return false;

        var typeInfo = ctx.SemanticModel.GetTypeInfo(node.Declaration.Type);
        var typeName = typeInfo.Type?.ToDisplayString();
        return IsCancellationExceptionName(typeName) || IsCancellationExceptionName(node.Declaration.Type.ToString());
    }

    private static bool IsCancellationExceptionName(string? name) =>
        name == "OperationCanceledException" || name == "TaskCanceledException" ||
        name == "System.OperationCanceledException" || name == "System.Threading.Tasks.TaskCanceledException";

    private static bool IsSwallowed(CatchClauseSyntax node)
    {
        if (node.Block.Statements.Count == 0) return true;
        if (node.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any()) return false;
        if (node.Block.DescendantNodes().OfType<InvocationExpressionSyntax>().Any()) return false;

        var hasReturn = node.Block.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Any(r => GetEnclosingContainer(r) == GetEnclosingContainer(node));
        if (hasReturn) return false;

        var hasAssignment = node.Block.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(a => GetEnclosingContainer(a) == GetEnclosingContainer(node));
        if (hasAssignment) return false;

        return true;
    }

    private static bool IsExplicitlyIgnored(CatchClauseSyntax node)
    {
        if (node.Declaration == null) return false;
        var name = node.Declaration.Identifier.Text;
        return name.StartsWith("ignored", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("expected", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInAllowedNamespace(CheckerContext ctx)
    {
        var allowed = ctx.Config.Global.ResultPatternAllowThrowInNamespaceSuffixes;
        if (allowed == null || allowed.Count == 0) return false;
        if (string.IsNullOrEmpty(ctx.CurrentNamespace)) return false;

        foreach (var suffix in allowed)
        {
            if (ctx.CurrentNamespace.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase)
                || ctx.CurrentNamespace.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsAllowedCatchRethrow(SyntaxNode node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.ResultPatternAllowCatchRethrow) return false;
        if (node is ThrowStatementSyntax throwStmt && throwStmt.Expression == null)
            return node.Ancestors().OfType<CatchClauseSyntax>().Any();
        return false;
    }

    private static bool IsAllowedFatalExceptionThrow(SyntaxNode node, CheckerContext ctx)
    {
        var expr = node switch
        {
            ThrowStatementSyntax ts => ts.Expression,
            ThrowExpressionSyntax te => te.Expression,
            _ => null
        };
        if (expr is not ObjectCreationExpressionSyntax creation) return false;

        var typeSymbol = ctx.SemanticModel.GetTypeInfo(creation).Type;
        if (typeSymbol == null) return false;

        var allowed = ctx.Config.Global.AllowedExceptions;
        return allowed != null && allowed.Contains(typeSymbol.Name);
    }

    private static bool IsThrowAllowed(SyntaxNode node)
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
            if (current is TypeDeclarationSyntax or NamespaceDeclarationSyntax or CompilationUnitSyntax) return null;
            if (current is MethodDeclarationSyntax or ConstructorDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)
                return current;
            current = current.Parent;
        }
        return null;
    }

    private static bool IsGuardOrValidateName(string name) =>
        name.EndsWith("Guard", StringComparison.Ordinal) || name.EndsWith("Validate", StringComparison.Ordinal);
}
