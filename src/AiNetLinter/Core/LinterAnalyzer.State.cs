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

        var returnsBool = method.ReturnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword };
        if (!returnsBool) return false;

        var methodName = method.Identifier.Text;
        return methodName.StartsWith("Try", StringComparison.Ordinal)
            || methodName.StartsWith("Is", StringComparison.Ordinal);
    }

    private void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node)
    {
        if (node.ParameterList == null) return;

        // Records und Structs definieren über ihren Primärkonstruktor Datenfelder, keine DI-Abhängigkeiten.
        // MaxConstructorDependencies zielt auf Kopplungs-Smell in Service-Klassen — nicht auf Datenbreite.
        if (node is RecordDeclarationSyntax or StructDeclarationSyntax)
            return;

        if (IsExemptByClassSuffix(node.Identifier.Text))
            return;

        var ignorePrefixes = _config.Metrics.ConstructorDependencyIgnoreTypePrefixes;
        int count;

        if (ignorePrefixes == null || ignorePrefixes.Count == 0)
        {
            count = node.ParameterList.Parameters.Count;
        }
        else
        {
            count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ignorePrefixes);
        }

        if (count > _config.Metrics.MaxConstructorDependencies)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Primaerkonstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}, Framework-Typen nicht gezaehlt).",
                Guidance = $"Zu viele Abhaengigkeiten in '{node.Identifier.Text}': Gruppiere thematisch zusammengehoerende Services in einen Facade-Service (z. B. 'XyzContext') und injiziere nur diesen — oder splitte die Klasse nach Single-Responsibility in zwei eigenstaendige Typen."
            });
        }
    }

    private void CheckConstructorDependencies(ConstructorDeclarationSyntax node)
    {
        if (node.Parent is TypeDeclarationSyntax parentType && IsExemptByClassSuffix(parentType.Identifier.Text))
            return;

        var ignorePrefixes = _config.Metrics.ConstructorDependencyIgnoreTypePrefixes;
        int count;

        if (ignorePrefixes == null || ignorePrefixes.Count == 0)
        {
            count = node.ParameterList.Parameters.Count;
        }
        else
        {
            count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ignorePrefixes);
        }

        if (count > _config.Metrics.MaxConstructorDependencies)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Konstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}, Framework-Typen nicht gezaehlt).",
                Guidance = $"Zu viele Abhaengigkeiten in '{node.Identifier.Text}': Fuehre einen Facade-Service ein, der zusammengehoerende Services buendelt (z. B. 'OrderContext(IRepository, IEventBus)'), und injiziere nur diesen — oder splitte die Klasse nach Single-Responsibility."
            });
        }
    }

    private bool IsExemptByClassSuffix(string className)
    {
        var exemptSuffixes = _config.Metrics.ConstructorDependencyExemptClassSuffixes;
        if (exemptSuffixes == null || exemptSuffixes.Count == 0) return false;

        foreach (var suffix in exemptSuffixes)
        {
            if (className.EndsWith(suffix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private int CountNonFrameworkDependencies(
        SeparatedSyntaxList<ParameterSyntax> parameters,
        IReadOnlyCollection<string> ignorePrefixes)
    {
        int count = 0;
        foreach (var param in parameters)
        {
            if (!IsFrameworkDependency(param, ignorePrefixes))
                count++;
        }
        return count;
    }

    private bool IsFrameworkDependency(
        ParameterSyntax param,
        IReadOnlyCollection<string> ignorePrefixes)
    {
        if (param.Type == null) return false;

        var typeName = GetSimpleTypeName(param.Type);
        if (typeName == null) return false;

        foreach (var prefix in ignorePrefixes)
        {
            if (typeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? GetSimpleTypeName(TypeSyntax type)
    {
        if (type is NullableTypeSyntax nullable)
        {
            type = nullable.ElementType;
        }

        return type switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            GenericNameSyntax generic => generic.Identifier.Text,
            QualifiedNameSyntax q => q.Right.Identifier.Text,
            _ => null
        };
    }

    private void CheckParameterReassignment(ExpressionSyntax expression)
    {
        if (!_config.Global.EnforceReadonlyParameters) return;

        var symbol = _semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IParameterSymbol parameter
            && parameter.RefKind is not (RefKind.Out or RefKind.Ref or RefKind.In))
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
        if (symbol == null) return;

        if (_sharedFieldTrackers != null && IsPartialType(symbol.ContainingType))
        {
            if (!IsInsideConstructorOfDeclaringType(expression, symbol.ContainingType))
            {
                var sharedTracker = _sharedFieldTrackers.GetOrAdd(symbol.ContainingType, _ => new FieldReadonlyTracker());
                sharedTracker.MarkModifiedOutsideConstructor(symbol);
            }
            return;
        }

        if (!_fieldTracker.IsCandidate(symbol)) return;
        if (!IsInsideConstructorOfDeclaringType(expression, symbol.ContainingType))
        {
            _fieldTracker.MarkModifiedOutsideConstructor(symbol);
        }
    }

    private static bool IsPartialType(INamedTypeSymbol? type) =>
        type != null && type.DeclaringSyntaxReferences.Length > 1;

    private static bool IsBlazorComponentType(ITypeSymbol type) =>
        type.AllInterfaces.Any(static i => i.Name == "IComponent");

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
        if (symbol.Type.Name == "ElementReference") return;
        // Blazor @ref-Felder auf Komponenten werden in der generierten .razor.g.cs zugewiesen,
        // die AiNetLinter auslässt. IComponent-Implementierungen sind daher kein gültiger readonly-Kandidat.
        if (IsBlazorComponentType(symbol.Type)) return;

        if (_sharedFieldTrackers != null && IsPartialType(symbol.ContainingType))
        {
            var sharedTracker = _sharedFieldTrackers.GetOrAdd(symbol.ContainingType, _ => new FieldReadonlyTracker());
            sharedTracker.RegisterCandidate(symbol);
            return;
        }

        _fieldTracker.RegisterCandidate(symbol);
    }
}
