#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal static class StateChecker
{
    internal static void CheckConstructorDependencies(ConstructorDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.Parent is TypeDeclarationSyntax parentType && IsExemptByClassSuffix(parentType.Identifier.Text, ctx))
            return;

        var count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ctx);
        if (count > ctx.Config.Metrics.MaxConstructorDependencies)
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Konstruktor hat {count} Parameter (erlaubt sind maximal {ctx.Config.Metrics.MaxConstructorDependencies}, Framework-Typen nicht gezaehlt).",
                Guidance = $"Zu viele Abhaengigkeiten in '{node.Identifier.Text}': Fuehre einen Facade-Service ein, der zusammengehoerende Services buendelt (z. B. 'OrderContext(IRepository, IEventBus)'), und injiziere nur diesen — oder splitte die Klasse nach Single-Responsibility."
            });
        }
    }

    internal static void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.ParameterList == null) return;
        if (node is RecordDeclarationSyntax or StructDeclarationSyntax) return;
        if (IsExemptByClassSuffix(node.Identifier.Text, ctx)) return;

        var count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ctx);
        if (count > ctx.Config.Metrics.MaxConstructorDependencies)
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = "MaxConstructorDependencies",
                Details = $"Der Primaerkonstruktor hat {count} Parameter (erlaubt sind maximal {ctx.Config.Metrics.MaxConstructorDependencies}, Framework-Typen nicht gezaehlt).",
                Guidance = $"Zu viele Abhaengigkeiten in '{node.Identifier.Text}': Gruppiere thematisch zusammengehoerende Services in einen Facade-Service (z. B. 'XyzContext') und injiziere nur diesen — oder splitte die Klasse nach Single-Responsibility in zwei eigenstaendige Typen."
            });
        }
    }

    internal static void CheckOutParameter(ParameterSyntax node, CheckerContext ctx)
    {
        if (!ShouldReportOutParameter(node, ctx)) return;

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Global.AllowOutParameters),
            Details = $"Der Parameter '{node.Identifier.Text}' verwendet das verbotene 'out'-Schluesselwort.",
            Guidance = "Verwende C#-Tuples oder Records fuer mehrere Rueckgabewerte."
        });
    }

    internal static void CheckParameterReassignment(ExpressionSyntax expression, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceReadonlyParameters) return;

        var symbol = ctx.SemanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is IParameterSymbol parameter
            && parameter.RefKind is not (RefKind.Out or RefKind.Ref or RefKind.In))
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(expression),
                RuleName = "EnforceReadonlyParameters",
                Details = $"Der Parameter '{parameter.Name}' wird innerhalb der Methode neu zugewiesen.",
                Guidance = "Behandle Parameter als readonly. Nutze stattdessen eine lokale Variable, um den geaenderten Wert zu speichern."
            });
        }
    }

    internal static void RegisterFieldWrite(ExpressionSyntax expression, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceReadonlyFields) return;

        var symbol = ctx.SemanticModel.GetSymbolInfo(expression).Symbol as IFieldSymbol;
        if (symbol == null) return;

        if (ctx.SharedFieldTrackers != null && IsPartialType(symbol.ContainingType))
        {
            if (!IsInsideConstructorOfDeclaringType(expression, symbol.ContainingType, ctx))
            {
                var sharedTracker = ctx.SharedFieldTrackers.GetOrAdd(symbol.ContainingType, _ => new FieldReadonlyTracker());
                sharedTracker.MarkModifiedOutsideConstructor(symbol);
            }
            return;
        }

        if (!ctx.FieldTracker.IsCandidate(symbol)) return;
        if (!IsInsideConstructorOfDeclaringType(expression, symbol.ContainingType, ctx))
            ctx.FieldTracker.MarkModifiedOutsideConstructor(symbol);
    }

    internal static void AnalyzePrivateField(FieldDeclarationSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceReadonlyFields) return;
        if (!node.Modifiers.Any(SyntaxKind.PrivateKeyword)) return;

        foreach (var variable in node.Declaration.Variables)
            RegisterPrivateFieldSymbol(variable, ctx);
    }

    internal static void CheckReadonlyFields(CheckerContext ctx)
    {
        if (!ctx.Config.Global.EnforceReadonlyFields) return;

        foreach (var field in ctx.FieldTracker.GetReadonlyCandidates())
            ctx.AddViolation(FieldReadonlyTracker.BuildViolation(field, ctx.FilePath));
    }

    private static void RegisterPrivateFieldSymbol(VariableDeclaratorSyntax variable, CheckerContext ctx)
    {
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
        if (symbol == null || symbol.IsReadOnly || symbol.IsConst) return;
        if (symbol.Type.Name == "ElementReference") return;
        if (IsBlazorComponentType(symbol.Type)) return;

        if (ctx.SharedFieldTrackers != null && IsPartialType(symbol.ContainingType))
        {
            var sharedTracker = ctx.SharedFieldTrackers.GetOrAdd(symbol.ContainingType, _ => new FieldReadonlyTracker());
            sharedTracker.RegisterCandidate(symbol);
            return;
        }

        ctx.FieldTracker.RegisterCandidate(symbol);
    }

    private static bool ShouldReportOutParameter(ParameterSyntax node, CheckerContext ctx)
    {
        if (ctx.Config.Global.AllowOutParameters) return false;
        if (!node.Modifiers.Any(SyntaxKind.OutKeyword)) return false;
        if (IsAllowedTryPatternOut(node, ctx)) return false;
        if (IsOutParamInInterfaceImplementationOrOverride(node, ctx)) return false;
        if (IsOutParamInContractDefinition(node)) return false;
        if (ctx.Config.Global.AllowOutParametersInPrivateMethods && IsPrivateMethod(node)) return false;
        return true;
    }

    private static bool IsPrivateMethod(ParameterSyntax node)
    {
        var method = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null) return false;
        if (method.Modifiers.Any(SyntaxKind.PrivateKeyword)) return true;
        var hasAccessibility = method.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword) || m.IsKind(SyntaxKind.InternalKeyword));
        return !hasAccessibility;
    }

    private static bool IsOutParamInContractDefinition(ParameterSyntax node)
    {
        if (node.Parent?.Parent is not MethodDeclarationSyntax method) return false;
        if (method.Modifiers.Any(SyntaxKind.AbstractKeyword)) return true;
        return method.Parent is InterfaceDeclarationSyntax;
    }

    private static bool IsAllowedTryPatternOut(ParameterSyntax node, CheckerContext ctx)
    {
        if (!ctx.Config.Global.AllowTryPatternOutParameters) return false;

        string? methodName;
        bool returnsAllowedType;

        if (node.Parent?.Parent is MethodDeclarationSyntax method)
        {
            methodName = method.Identifier.Text;
            returnsAllowedType = IsAllowedTryPatternReturnType(method.ReturnType);
        }
        else if (node.Parent?.Parent is LocalFunctionStatementSyntax localFunc)
        {
            methodName = localFunc.Identifier.Text;
            returnsAllowedType = IsAllowedTryPatternReturnType(localFunc.ReturnType);
        }
        else { return false; }

        if (methodName == "Deconstruct") return true;
        if (!returnsAllowedType) return false;

        return methodName.StartsWith("Try", StringComparison.Ordinal)
            || methodName.StartsWith("Is", StringComparison.Ordinal);
    }

    private static bool IsAllowedTryPatternReturnType(TypeSyntax returnType) =>
        returnType is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.BoolKeyword }
        || returnType is NullableTypeSyntax { ElementType: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.StringKeyword } };

    private static bool IsOutParamInInterfaceImplementationOrOverride(ParameterSyntax node, CheckerContext ctx)
    {
        if (node.Parent?.Parent is not MethodDeclarationSyntax method) return false;
        return ComplexityChecker.IsOverrideOrInterfaceImplementation(method, ctx);
    }

    private static int CountNonFrameworkDependencies(SeparatedSyntaxList<ParameterSyntax> parameters, CheckerContext ctx)
    {
        var ignorePrefixes = ctx.Config.Metrics.ConstructorDependencyIgnoreTypePrefixes;
        if (ignorePrefixes == null || ignorePrefixes.Count == 0) return parameters.Count;

        int count = 0;
        foreach (var param in parameters)
        {
            if (param.Type == null) { count++; continue; }
            var typeName = SyntaxHelper.GetSimpleTypeName(param.Type);
            var isFramework = typeName != null && ignorePrefixes.Any(p => typeName.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            if (!isFramework) count++;
        }
        return count;
    }

    private static bool IsExemptByClassSuffix(string className, CheckerContext ctx)
    {
        var exemptSuffixes = ctx.Config.Metrics.ConstructorDependencyExemptClassSuffixes;
        if (exemptSuffixes == null || exemptSuffixes.Count == 0) return false;
        foreach (var suffix in exemptSuffixes)
            if (className.EndsWith(suffix, StringComparison.Ordinal)) return true;
        return false;
    }

    private static bool IsPartialType(INamedTypeSymbol? type) =>
        type != null && type.DeclaringSyntaxReferences.Length > 1;

    private static bool IsBlazorComponentType(ITypeSymbol type) =>
        type.AllInterfaces.Any(static i => i.Name == "IComponent");

    private static bool IsInsideConstructorOfDeclaringType(SyntaxNode node, INamedTypeSymbol declaringType, CheckerContext ctx)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (IsConstructorOf(current, declaringType, ctx)) return true;
            if (current is TypeDeclarationSyntax or NamespaceDeclarationSyntax or CompilationUnitSyntax) return false;
            current = current.Parent;
        }
        return false;
    }

    private static bool IsConstructorOf(SyntaxNode node, INamedTypeSymbol declaringType, CheckerContext ctx)
    {
        if (node is not ConstructorDeclarationSyntax constructorDecl) return false;
        var symbol = ctx.SemanticModel.GetDeclaredSymbol(constructorDecl);
        return symbol != null && SymbolEqualityComparer.Default.Equals(symbol.ContainingType, declaringType);
    }
}
