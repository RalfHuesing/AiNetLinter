#nullable enable

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class PhantomDependencyChecker
{
    internal static void CheckPhantomNamespace(UsingDirectiveSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.DetectAndBanPhantomDependencies) return;
        if (ctx.IsTestFile) return;
        if (node.Name == null) return;

        var symbolInfo = ctx.SemanticModel.GetSymbolInfo(node.Name);
        if (symbolInfo.Symbol == null)
        {
            ctx.ReportViolation(node,
                nameof(ctx.Config.Global.DetectAndBanPhantomDependencies),
                $"Der importierte Namespace '{node.Name}' kann nicht aufgeloest werden. Ist die NuGet-Abhaengigkeit in der csproj deklariert?",
                "Entferne das using-Statement oder fuege die entsprechende Projektreferenz/.csproj-Abhaengigkeit hinzu.");
        }
    }

    internal static void CheckPhantomReflection(InvocationExpressionSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.DetectAndBanPhantomDependencies) return;
        if (ctx.IsTestFile) return;

        var symbol = ctx.SemanticModel.GetSymbolInfo(node).Symbol;
        if (symbol == null) return;

        var containingType = symbol.ContainingType?.ToDisplayString() ?? "";
        var methodName = symbol.Name;

        if (!IsForbiddenReflectionCall(containingType, methodName)) return;

        ctx.ReportViolation(node,
            nameof(ctx.Config.Global.DetectAndBanPhantomDependencies),
            $"Die Verwendung von dynamischer Reflection '{containingType}.{methodName}' ist fuer KI-Lesbarkeit nicht gestattet.",
            "Verwende statische Typ-Ausdruecke wie 'typeof(MyClass)' oder Generics, um die Compile-Zeit-Sicherheit zu wahren.");
    }

    private static bool IsForbiddenReflectionCall(string containingType, string methodName)
    {
        if (containingType == "System.Type" && methodName == "GetType") return true;
        if (containingType.StartsWith("System.Reflection.Assembly") && (methodName.StartsWith("Load") || methodName.StartsWith("LoadFrom"))) return true;
        return containingType == "System.Activator" && methodName == "CreateInstance";
    }
}
