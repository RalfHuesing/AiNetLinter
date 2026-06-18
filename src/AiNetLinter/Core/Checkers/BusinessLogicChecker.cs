#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class BusinessLogicChecker
{
    private static readonly string[] LogicSuffixes = ["Calculator", "Rule", "Policy", "Engine"];
    private static readonly string[] ForbiddenIoSuffixes = ["DbContext", "Repository", "Client", "Connection", "Store", "HttpClient"];
    private static readonly string[] StaticIoClasses = ["File", "Directory", "Console", "Path", "Socket"];

    internal static void Check(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceStrictBoundaryForBusinessLogic) return;
        if (ctx.IsTestFile) return;
        if (!IsLogicMethod(node)) return;

        CheckStaticDeclaration(node, ctx);
        CheckForbiddenParameters(node, ctx);
        CheckForbiddenIoCalls(node, ctx);
    }

    private static void CheckStaticDeclaration(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.Modifiers.Any(SyntaxKind.StaticKeyword)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = "EnforceStrictBoundaryForBusinessLogic",
            Details = $"Die Logik-Methode '{node.Identifier.Text}' ist nicht als 'static' deklariert.",
            Guidance = "Deklariere die Methode als 'static', um Zustandslosigkeit zu garantieren und Mocks in Unit-Tests zu vermeiden."
        });
    }

    private static void CheckForbiddenParameters(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        foreach (var param in node.ParameterList.Parameters)
            CheckParameterIo(node, param, ctx);
    }

    private static void CheckParameterIo(MethodDeclarationSyntax methodNode, ParameterSyntax param, CheckerContext ctx)
    {
        if (param.Type == null) return;
        var typeSymbol = ctx.SemanticModel.GetTypeInfo(param.Type).Type;
        if (typeSymbol == null) return;
        if (!IsForbiddenIoType(typeSymbol.Name)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(param),
            RuleName = "EnforceStrictBoundaryForBusinessLogic",
            Details = $"Die Logik-Methode '{methodNode.Identifier.Text}' akzeptiert ein verbotenes I/O-Objekt '{param.Identifier.Text}' vom Typ '{typeSymbol.Name}'.",
            Guidance = "Uebergib stattdessen nur die notwendigen primitiven Werte oder einfache Value Objects/Records."
        });
    }

    private static void CheckForbiddenIoCalls(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.Body == null) return;
        foreach (var invocation in node.Body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbol = ctx.SemanticModel.GetSymbolInfo(invocation).Symbol;
            if (symbol == null) continue;

            var containingType = symbol.ContainingType?.Name ?? "";
            if (!IsForbiddenIoType(containingType) && !IsStaticIoClass(containingType)) continue;

            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(invocation),
                RuleName = "EnforceStrictBoundaryForBusinessLogic",
                Details = $"Unerlaubter I/O-Aufruf von '{symbol.ToDisplayString()}' innerhalb der zustandslosen Logik-Methode.",
                Guidance = $"Verschiebe den I/O-Aufruf '{symbol.Name}' aus der Logik-Methode heraus: Deklariere ein Interface (z. B. 'I{containingType}') und injiziere es per Konstruktor."
            });
        }
    }

    private static bool IsLogicMethod(MethodDeclarationSyntax node)
    {
        if (node.Body == null && node.ExpressionBody == null) return false;
        return IsInLogicClass(node) || HasPureLogicAttribute(node);
    }

    private static bool HasPureLogicAttribute(MethodDeclarationSyntax node) =>
        node.AttributeLists
            .SelectMany(static al => al.Attributes)
            .Any(static a => a.Name.ToString().EndsWith("PureLogic", StringComparison.Ordinal));

    private static bool IsInLogicClass(MethodDeclarationSyntax node)
    {
        if (node.Parent is ClassDeclarationSyntax classDecl)
        {
            var className = classDecl.Identifier.Text;
            return LogicSuffixes.Any(s => className.EndsWith(s, StringComparison.Ordinal));
        }
        return false;
    }

    private static bool IsForbiddenIoType(string typeName) =>
        ForbiddenIoSuffixes.Any(s => typeName.EndsWith(s, StringComparison.OrdinalIgnoreCase));

    private static bool IsStaticIoClass(string className) =>
        StaticIoClasses.Contains(className);
}
