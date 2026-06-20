#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core.Checkers;

internal static class StateChecker
{
    internal static void CheckConstructorDependencies(ConstructorDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.Parent is not TypeDeclarationSyntax parentType || IsExemptByClassSuffix(parentType.Identifier.Text, ctx))
            return;

        var count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ctx);
        var publicMemberCount = PublicMembersChecker.CountPublicMembers(parentType);

        var metrics = new Dictionary<string, int>
        {
            [MetricNames.ConstructorDependencies] = count,
            [MetricNames.PublicMemberCount]       = publicMemberCount,
        };

        var baseLimit = ctx.Config.Metrics.MaxConstructorDependencies;
        var suppressions = ctx.Config.Metrics.CompoundSuppressions;

        var effectiveLimit = CompoundSuppressionEvaluator.Evaluate(
            LinterRuleIds.MaxConstructorDependencies, suppressions, metrics);

        if (effectiveLimit == 0) return; // vollständig supprimiert

        var configured = CompoundSuppressionEvaluator.FindConfigured(
            LinterRuleIds.MaxConstructorDependencies, suppressions);

        var activeLimit = effectiveLimit > 0 ? effectiveLimit : baseLimit;

        if (count <= activeLimit) return;

        if (effectiveLimit > 0)
        {
            // Scenario A: Suppression active, but RelaxedLimit exceeded
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured!.WhenAllOf, metrics);
            var severityOverride = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
                LinterRuleIds.MaxConstructorDependencies, suppressions, metrics);
            var severityHint = severityOverride == "warning"
                ? " Severity auf 'warning' herabgestuft — kein Build-Fehler."
                : string.Empty;
            ctx.ReportViolation(node,
                LinterRuleIds.MaxConstructorDependencies,
                $"Der Konstruktor hat {count} Parameter (Compound-Limit: {effectiveLimit}; Standard: {baseLimit} · {condSummary}).",
                $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten. Lagere Abhaengigkeiten aus. Ziel: ≤ {effectiveLimit} Parameter bei weiterhin {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)}.{severityHint}",
                effectiveSeverity: severityOverride);
            return;
        }

        if (configured != null)
        {
            // Scenario B: Suppression configured, but not active
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured.WhenAllOf, metrics);
            var relaxedLimit = configured.RelaxedLimit.HasValue ? $"effektives Limit steigt auf {configured.RelaxedLimit}." : "Violation wird vollständig supprimiert.";
            ctx.ReportViolation(node,
                LinterRuleIds.MaxConstructorDependencies,
                $"Der Konstruktor hat {count} Parameter (erlaubt: {baseLimit} · Compound-Suppression inaktiv: {condSummary}).",
                $"Optionen: (1) Metriken senken auf {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)} → {relaxedLimit} (2) Lagere Abhaengigkeiten aus.");
            return;
        }

        // Scenario C: Classic
        ctx.ReportViolation(node,
            LinterRuleIds.MaxConstructorDependencies,
            $"Der Konstruktor hat {count} Parameter (erlaubt sind maximal {baseLimit}, Framework-Typen nicht gezaehlt).",
            $"Zu viele Abhaengigkeiten in '{node.Identifier.Text}': Fuehre einen Facade-Service ein, der zusammengehoerende Services buendelt (z. B. 'OrderContext(IRepository, IEventBus)'), und injiziere nur diesen — oder splitte die Klasse nach Single-Responsibility.");
    }

    internal static void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.ParameterList == null) return;
        if (node is RecordDeclarationSyntax or StructDeclarationSyntax) return;
        if (IsExemptByClassSuffix(node.Identifier.Text, ctx)) return;

        var count = CountNonFrameworkDependencies(node.ParameterList.Parameters, ctx);
        var publicMemberCount = PublicMembersChecker.CountPublicMembers(node);

        var metrics = new Dictionary<string, int>
        {
            [MetricNames.ConstructorDependencies] = count,
            [MetricNames.PublicMemberCount]       = publicMemberCount,
        };

        var baseLimit = ctx.Config.Metrics.MaxConstructorDependencies;
        var suppressions = ctx.Config.Metrics.CompoundSuppressions;

        var effectiveLimit = CompoundSuppressionEvaluator.Evaluate(
            LinterRuleIds.MaxConstructorDependencies, suppressions, metrics);

        if (effectiveLimit == 0) return; // vollständig supprimiert

        var configured = CompoundSuppressionEvaluator.FindConfigured(
            LinterRuleIds.MaxConstructorDependencies, suppressions);

        var activeLimit = effectiveLimit > 0 ? effectiveLimit : baseLimit;

        if (count <= activeLimit) return;

        if (effectiveLimit > 0)
        {
            // Scenario A: Suppression active, but RelaxedLimit exceeded
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured!.WhenAllOf, metrics);
            var severityOverride = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
                LinterRuleIds.MaxConstructorDependencies, suppressions, metrics);
            var severityHint = severityOverride == "warning"
                ? " Severity auf 'warning' herabgestuft — kein Build-Fehler."
                : string.Empty;
            ctx.ReportViolation(node,
                LinterRuleIds.MaxConstructorDependencies,
                $"Der Primaerkonstruktor hat {count} Parameter (Compound-Limit: {effectiveLimit}; Standard: {baseLimit} · {condSummary}).",
                $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten. Lagere Abhaengigkeiten aus. Ziel: ≤ {effectiveLimit} Parameter bei weiterhin {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)}.{severityHint}",
                effectiveSeverity: severityOverride);
            return;
        }

        if (configured != null)
        {
            // Scenario B: Suppression configured, but not active
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured.WhenAllOf, metrics);
            var relaxedLimit = configured.RelaxedLimit.HasValue ? $"effektives Limit steigt auf {configured.RelaxedLimit}." : "Violation wird vollständig supprimiert.";
            ctx.ReportViolation(node,
                LinterRuleIds.MaxConstructorDependencies,
                $"Der Primaerkonstruktor hat {count} Parameter (erlaubt: {baseLimit} · Compound-Suppression inaktiv: {condSummary}).",
                $"Optionen: (1) Metriken senken auf {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)} → {relaxedLimit} (2) Lagere Abhaengigkeiten aus.");
            return;
        }

        // Scenario C: Classic
        ctx.ReportViolation(node,
            LinterRuleIds.MaxConstructorDependencies,
            $"Der Primaerkonstruktor hat {count} Parameter (erlaubt sind maximal {baseLimit}, Framework-Typen nicht gezaehlt).",
            $"Zu viele Abhaengigkeiten in '{node.Identifier.Text}': Gruppiere thematisch zusammengehoerende Services in einen Facade-Service (z. B. 'XyzContext') und injiziere nur diesen — oder splitte die Klasse nach Single-Responsibility in zwei eigenstaendige Typen.");
    }

    internal static void CheckOutParameter(ParameterSyntax node, CheckerContext ctx)
    {
        if (!ShouldReportOutParameter(node, ctx)) return;

        ctx.ReportViolation(node,
            nameof(ctx.Config.Global.AllowOutParameters),
            $"Der Parameter '{node.Identifier.Text}' verwendet das verbotene 'out'-Schluesselwort.",
            "Verwende C#-Tuples oder Records fuer mehrere Rueckgabewerte.");
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

    internal static int CountNonFrameworkDependencies(SeparatedSyntaxList<ParameterSyntax> parameters, CheckerContext ctx)
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

    internal static int GetConstructorDependencies(TypeDeclarationSyntax node, CheckerContext ctx)
    {
        if (IsExemptByClassSuffix(node.Identifier.Text, ctx)) return 0;

        if (node.ParameterList != null)
        {
            if (node is RecordDeclarationSyntax or StructDeclarationSyntax) return 0;
            return CountNonFrameworkDependencies(node.ParameterList.Parameters, ctx);
        }

        var max = 0;
        foreach (var ctor in node.Members.OfType<ConstructorDeclarationSyntax>())
        {
            var count = CountNonFrameworkDependencies(ctor.ParameterList.Parameters, ctx);
            max = Math.Max(max, count);
        }
        return max;
    }

}
