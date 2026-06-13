#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Domain-specific partial class file handling state rules such as readonly fields/parameters, constructor dependencies and out parameters.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        CheckConstructorDependencies(node);
        base.VisitConstructorDeclaration(node);
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        CheckParameterReassignment(node.Left);
        RegisterFieldWrite(node.Left);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PostIncrementExpression) || node.IsKind(SyntaxKind.PostDecrementExpression))
        {
            CheckParameterReassignment(node.Operand);
            RegisterFieldWrite(node.Operand);
        }
        base.VisitPostfixUnaryExpression(node);
    }

    public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.PreIncrementExpression) || node.IsKind(SyntaxKind.PreDecrementExpression))
        {
            CheckParameterReassignment(node.Operand);
            RegisterFieldWrite(node.Operand);
        }
        base.VisitPrefixUnaryExpression(node);
    }

    public override void VisitArgument(ArgumentSyntax node)
    {
        if (node.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword) || node.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword))
        {
            CheckParameterReassignment(node.Expression);
            RegisterFieldWrite(node.Expression);
        }
        base.VisitArgument(node);
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        AnalyzePrivateFields(node);
        base.VisitFieldDeclaration(node);
    }

    private void CheckOutParameter(ParameterSyntax node)
    {
        if (ShouldReportOutParameter(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Global.AllowOutParameters),
                Details = $"Der Parameter '{node.Identifier.Text}' verwendet das verbotene 'out'-Schluesselwort.",
                Guidance = "Verwende C#-Tuples oder Records fuer mehrere Rueckgabewerte."
            });
        }
    }

    private bool ShouldReportOutParameter(ParameterSyntax node)
    {
        if (_config.Global.AllowOutParameters)
        {
            return false;
        }

        if (!node.Modifiers.Any(SyntaxKind.OutKeyword))
        {
            return false;
        }

        return !IsAllowedTryPatternOut(node);
    }

    private bool IsAllowedTryPatternOut(ParameterSyntax node)
    {
        if (!_config.Global.AllowTryPatternOutParameters)
        {
            return false;
        }

        if (node.Parent?.Parent is not MethodDeclarationSyntax method)
        {
            return false;
        }

        if (!method.Identifier.Text.StartsWith("Try", StringComparison.Ordinal))
        {
            return false;
        }

        return method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword };
    }

    private void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node)
    {
        if (node.ParameterList == null) return;
        var count = node.ParameterList.Parameters.Count;
        if (count > _config.Metrics.MaxConstructorDependencies)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Primaerkonstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}).",
                Guidance = "Reduziere die Anzahl der Abhaengigkeiten, indem du den Typ in kleinere Klassen aufteilst."
            });
        }
    }

    private void CheckConstructorDependencies(ConstructorDeclarationSyntax node)
    {
        var count = node.ParameterList.Parameters.Count;
        if (count > _config.Metrics.MaxConstructorDependencies)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Konstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}).",
                Guidance = "Reduziere die Anzahl der Abhaengigkeiten (Constructor Injection), indem du die Klasse in kleinere Services aufteilst."
            });
        }
    }

    private void CheckParameterReassignment(ExpressionSyntax expression)
    {
        if (!_config.Global.EnforceReadonlyParameters) return;

        var symbol = _semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IParameterSymbol parameter && parameter.RefKind != RefKind.Out)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(expression),
                RuleName = "EnforceReadonlyParameters",
                Details = $"Der Parameter '{parameter.Name}' wird innerhalb der Methode neu zugewiesen.",
                Guidance = "Behandle Parameter als readonly. Nutze stattdessen eine lokale Variable, um den geaenderten Wert zu speichern."
            });
        }
    }

    private void RegisterFieldWrite(ExpressionSyntax expression)
    {
        if (!_config.Global.EnforceReadonlyFields) return;

        var symbol = _semanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
        if (symbol == null || !_privateFieldsToAnalyze.Contains(symbol)) return;

        if (!IsInsideConstructorOfDeclaringType(expression, symbol.ContainingType))
        {
            _fieldsModifiedOutsideConstructor.Add(symbol);
        }
    }

    private bool IsInsideConstructorOfDeclaringType(SyntaxNode node, INamedTypeSymbol declaringType)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (IsConstructorOf(current, declaringType)) return true;
            if (current is TypeDeclarationSyntax or NamespaceDeclarationSyntax or CompilationUnitSyntax) return false;
            current = current.Parent;
        }
        return false;
    }

    private bool IsConstructorOf(SyntaxNode node, INamedTypeSymbol declaringType)
    {
        if (node is not ConstructorDeclarationSyntax constructorDecl) return false;
        var symbol = _semanticModel.GetDeclaredSymbol(constructorDecl);
        return symbol != null && SymbolEqualityComparer.Default.Equals(symbol.ContainingType, declaringType);
    }

    private void AnalyzePrivateFields(FieldDeclarationSyntax node)
    {
        if (!_config.Global.EnforceReadonlyFields) return;
        if (!node.Modifiers.Any(SyntaxKind.PrivateKeyword)) return;

        foreach (var variable in node.Declaration.Variables)
        {
            RegisterPrivateFieldSymbol(variable);
        }
    }

    private void RegisterPrivateFieldSymbol(VariableDeclaratorSyntax variable)
    {
        var symbol = _semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
        if (symbol == null || symbol.IsReadOnly || symbol.IsConst) return;

        _privateFieldsToAnalyze.Add(symbol);
    }
}
