#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling business logic boundaries.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    private void CheckBusinessLogicBoundary(MethodDeclarationSyntax node)
    {
        if (!_config.Global.EnforceStrictBoundaryForBusinessLogic) return;
        if (_isTestFile) return;
        if (!IsLogicMethod(node)) return;

        CheckStaticDeclaration(node);
        CheckForbiddenParameters(node);
        CheckForbiddenIoCalls(node);
    }

    private void CheckStaticDeclaration(MethodDeclarationSyntax node)
    {
        var isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
        if (isStatic) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "EnforceStrictBoundaryForBusinessLogic",
            Details = $"Die Logik-Methode '{node.Identifier.Text}' ist nicht als 'static' deklariert.",
            Guidance = "Deklariere die Methode als 'static', um Zustandslosigkeit zu garantieren und Mocks in Unit-Tests zu vermeiden."
        });
    }

    private void CheckForbiddenParameters(MethodDeclarationSyntax node)
    {
        foreach (var param in node.ParameterList.Parameters)
        {
            CheckParameterIo(node, param);
        }
    }

    private void CheckParameterIo(MethodDeclarationSyntax methodNode, ParameterSyntax param)
    {
        if (param.Type == null) return;
        var typeSymbol = _semanticModel.GetTypeInfo(param.Type).Type;
        if (typeSymbol == null) return;
        if (!IsForbiddenIoType(typeSymbol.Name)) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(param),
            RuleName = "EnforceStrictBoundaryForBusinessLogic",
            Details = $"Die Logik-Methode '{methodNode.Identifier.Text}' akzeptiert ein verbotenes I/O-Objekt '{param.Identifier.Text}' vom Typ '{typeSymbol.Name}'.",
            Guidance = "Uebergib stattdessen nur die notwendigen primitiven Werte oder einfache Value Objects/Records."
        });
    }

    private void CheckForbiddenIoCalls(MethodDeclarationSyntax node)
    {
        if (node.Body == null) return;
        var invocations = node.Body.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            CheckInvocationIo(invocation);
        }
    }

    private void CheckInvocationIo(InvocationExpressionSyntax invocation)
    {
        var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol;
        if (symbol == null) return;

        var containingType = symbol.ContainingType?.Name ?? "";
        if (!IsForbiddenIoType(containingType) && !IsStaticIoClass(containingType)) return;

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(invocation),
            RuleName = "EnforceStrictBoundaryForBusinessLogic",
            Details = $"Unerlaubter I/O-Aufruf von '{symbol.ToDisplayString()}' innerhalb der zustandslosen Logik-Methode.",
            Guidance = "Kapsle Berechnungen so, dass sie keine Datenbanken, APIs oder Dateisysteme direkt aufrufen."
        });
    }

    private bool IsLogicMethod(MethodDeclarationSyntax node)
    {
        if (IsInLogicClass(node)) return true;
        if (node.Body == null && node.ExpressionBody == null) return false;
        
        var text = node.ToString();
        return HasMathOperations(text) && HasComplexLogic(text);
    }

    private bool IsInLogicClass(MethodDeclarationSyntax node)
    {
        if (node.Parent is ClassDeclarationSyntax classDecl)
        {
            var className = classDecl.Identifier.Text;
            var logicSuffixes = new[] { "Calculator", "Rule", "Policy", "Engine" };
            return logicSuffixes.Any(s => className.EndsWith(s, StringComparison.Ordinal));
        }
        return false;
    }

    private static bool HasMathOperations(string text)
    {
        return text.Contains('+') || text.Contains('-') || text.Contains('*') || text.Contains('/');
    }

    private static bool HasComplexLogic(string text)
    {
        return text.Contains("&&") || text.Contains("||") || text.Contains("switch") || text.Contains("if");
    }

    private static bool IsForbiddenIoType(string typeName)
    {
        var forbiddenSuffixes = new[] { "DbContext", "Repository", "Client", "Connection", "Store", "HttpClient" };
        return forbiddenSuffixes.Any(s => typeName.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsStaticIoClass(string className)
    {
        var ioClasses = new[] { "File", "Directory", "Console", "Path", "Socket" };
        return ioClasses.Contains(className);
    }
}
