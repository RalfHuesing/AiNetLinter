#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class AsyncVoidChecker
{
    internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanAsyncVoid) return;
        if (!IsAsyncVoid(node.Modifiers, node.ReturnType)) return;
        if (IsEventHandlerSignature(node.ParameterList, ctx)) return;

        ctx.ReportViolation(node,
            LinterRuleIds.BanAsyncVoid,
            $"Methode '{node.Identifier.Text}' ist als 'async void' deklariert.",
            "Verwende 'async Task' als Rueckgabetyp. 'async void' ist das haeufigste async-Anti-Pattern in LLM-generiertem Code: Exceptions werden unkontrolliert in den SynchronizationContext geschleudert und sind fuer aufrufende try/catch-Bloecke unsichtbar. Ausnahme: Event-Handler mit Signatur '(object sender, EventArgs e)' bleiben erlaubt wenn 'AsyncVoidAllowEventHandlers: true' gesetzt ist.");
    }

    internal static void CheckLocalFunction(LocalFunctionStatementSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.BanAsyncVoid) return;
        if (!IsAsyncVoid(node.Modifiers, node.ReturnType)) return;

        ctx.ReportViolation(node,
            LinterRuleIds.BanAsyncVoid,
            $"Lokale Funktion '{node.Identifier.Text}' ist als 'async void' deklariert.",
            "Verwende 'async Task' als Rueckgabetyp. Lokale 'async void'-Funktionen sind nie als Event-Handler gedacht, erzeugen unkontrollierbare Exception-Propagation und werden von LLM-Agenten haeufig in verschachtelten async-Aufrufen generiert.");
    }

    private static bool IsAsyncVoid(SyntaxTokenList modifiers, TypeSyntax returnType) =>
        modifiers.Any(SyntaxKind.AsyncKeyword)
        && returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword };

    private static bool IsEventHandlerSignature(ParameterListSyntax paramList, CheckerContext ctx)
    {
        if (!ctx.Config.Global.AsyncVoidAllowEventHandlers) return false;

        var parameters = paramList.Parameters;
        if (parameters.Count != 2) return false;

        var firstParamType = parameters[0].Type;
        if (firstParamType == null) return false;

        string? firstType = firstParamType switch
        {
            PredefinedTypeSyntax predefined => predefined.Keyword.Text,
            _ => SyntaxHelper.GetSimpleTypeName(firstParamType)
        };
        if (firstType is not ("object" or "Object")) return false;

        var secondParamType = parameters[1].Type;
        if (secondParamType == null) return false;

        var secondType = SyntaxHelper.GetSimpleTypeName(secondParamType);
        return secondType != null && secondType.EndsWith("EventArgs");
    }
}
