#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class BlockingTaskChecker
{
    // Bekannte Task-Typen (Kurznamen der Original-Definitionen)
    private static readonly string[] TaskTypeNames = ["Task", "ValueTask"];

    internal static void CheckInvocation(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanBlockingTaskAccess) return;
        if (ctx.IsTestFile && ctx.Config.Global.BanBlockingTaskAccessAllowInTests) return;

        CheckWait(node, ctx);
        CheckGetAwaiterGetResult(node, ctx);
    }

    private static void CheckWait(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        // Muster 1: .Wait() — InvocationExpression mit MemberAccess ".Wait"
        if (node.Expression is MemberAccessExpressionSyntax waitAccess
            && waitAccess.Name.Identifier.Text == "Wait"
            && IsTaskReceiver(waitAccess.Expression, ctx))
        {
            if (IsInMainMethod(node)) return;
            ctx.ReportViolation(node,
                LinterRuleIds.BanBlockingTaskAccess,
                "Blockierender Task-Zugriff '.Wait()' erkannt.",
                BuildGuidance(".Wait()", "await task;"));
        }
    }

    private static void CheckGetAwaiterGetResult(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        // Muster 3: .GetAwaiter().GetResult() — Kette GetAwaiter + GetResult
        if (node.Expression is MemberAccessExpressionSyntax getResultAccess
            && getResultAccess.Name.Identifier.Text == "GetResult"
            && getResultAccess.Expression is InvocationExpressionSyntax getAwaiterCall
            && getAwaiterCall.Expression is MemberAccessExpressionSyntax getAwaiterAccess
            && getAwaiterAccess.Name.Identifier.Text == "GetAwaiter"
            && IsTaskReceiver(getAwaiterAccess.Expression, ctx))
        {
            if (IsInMainMethod(node)) return;
            ctx.ReportViolation(node,
                LinterRuleIds.BanBlockingTaskAccess,
                "Blockierender Task-Zugriff '.GetAwaiter().GetResult()' erkannt.",
                BuildGuidance(".GetAwaiter().GetResult()", "await task;"));
        }
    }

    internal static void CheckMemberAccess(MemberAccessExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanBlockingTaskAccess) return;
        if (ctx.IsTestFile && ctx.Config.Global.BanBlockingTaskAccessAllowInTests) return;

        // Muster 2: .Result — MemberAccessExpression mit Name "Result"
        // Nur prüfen wenn der Knoten nicht bereits Teil einer Invocation ist (GetAwaiter().GetResult() hat .Result nicht)
        if (node.Name.Identifier.Text != "Result") return;
        if (node.Parent is InvocationExpressionSyntax) return; // .Result() wäre eine Methode, nicht die Property
        if (!IsTaskReceiver(node.Expression, ctx)) return;
        if (IsInMainMethod(node)) return;

        ctx.ReportViolation(node,
            LinterRuleIds.BanBlockingTaskAccess,
            "Blockierender Task-Zugriff '.Result' erkannt.",
            BuildGuidance(".Result", "await task;"));
    }

    private static bool IsTaskReceiver(ExpressionSyntax expression, CheckerContext ctx)
    {
        var typeInfo = ctx.SemanticModel.GetTypeInfo(expression);
        var type = typeInfo.Type;
        if (type == null || type is IErrorTypeSymbol) return IsSyntacticTaskHint(expression);

        // Semantischer Check: ist der Typ Task, ValueTask oder ein generisches Derivat?
        var originalDef = type is INamedTypeSymbol named ? named.OriginalDefinition : type;
        var name = originalDef.Name;
        return Array.Exists(TaskTypeNames, t => t.Equals(name, StringComparison.Ordinal))
            && IsSystemTasksNamespace(type.ContainingNamespace);
    }

    private static bool IsSyntacticTaskHint(ExpressionSyntax expression)
    {
        // Fallback ohne Semantic Model: prüfe ob der Bezeichner/Text wie Task, Awaitable oder Async aussieht
        var text = expression.ToString();
        return text.Contains("Task") || text.Contains("Awaitable") || text.Contains("Async");
    }

    private static bool IsSystemTasksNamespace(INamespaceSymbol? ns)
    {
        if (ns == null) return false;
        var full = ns.ToDisplayString();
        return full == "System.Threading.Tasks";
    }

    private static bool IsInMainMethod(SyntaxNode node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method == null) return false;
        return method.Identifier.Text == "Main"
            && method.Modifiers.Any(SyntaxKind.StaticKeyword);
    }

    private static string BuildGuidance(string pattern, string fix) =>
        $"Ersetze '{pattern}' durch '{fix}'. Blockierende Task-Zugriffe sind ein empirisch haeufiges LLM-Halluzinations-Muster in async-Kontexten " +
        $"und deadlock-anfaellig in SynchronizationContext-Umgebungen (ASP.NET Classic, WPF). " +
        $"Falls die aufrufende Methode nicht async sein kann: Methode zu 'async Task' umwandeln und " +
        $"die Aufrufkette von oben nach async migrieren (Async Propagation).";
}
