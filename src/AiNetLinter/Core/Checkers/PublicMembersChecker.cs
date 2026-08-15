#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Core.Checkers;

internal static class PublicMembersChecker
{
    internal static void Check(TypeDeclarationSyntax node, string typeName, CheckerContext ctx)
    {
        if (IsExempt(typeName, ctx)) return;

        var limit = ctx.Config.Metrics.MaxPublicMembersPerType;
        var count = CountPublicMembers(node);
        var constructorDeps = StateChecker.GetConstructorDependencies(node, ctx);

        var metrics = new Dictionary<string, int>
        {
            [MetricNames.PublicMemberCount]       = count,
            [MetricNames.ConstructorDependencies] = constructorDeps,
        };

        var suppressions = ctx.Config.Metrics.CompoundSuppressions;
        var effectiveLimit = CompoundSuppressionEvaluator.Evaluate(
            LinterRuleIds.MaxPublicMembersPerType, suppressions, metrics);

        if (effectiveLimit == 0) return; // vollständig supprimiert

        var activeLimit = effectiveLimit > 0 ? effectiveLimit : limit;
        if (count <= activeLimit) return;

        var configured = CompoundSuppressionEvaluator.FindConfigured(
            LinterRuleIds.MaxPublicMembersPerType, suppressions);

        ReportExcessiveMembers(node, typeName, ctx, count, limit, effectiveLimit, configured, metrics, suppressions);
    }

    private static bool IsExempt(string typeName, CheckerContext ctx)
    {
        if (ctx.IsTestFile && !ctx.Config.Metrics.MaxPublicMembersPerTypeApplyToTestFiles) return true;
        if (ctx.Config.Metrics.MaxPublicMembersPerType <= 0) return true;

        foreach (var suffix in ctx.Config.Metrics.MaxPublicMembersPerTypeExemptSuffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void ReportExcessiveMembers(
        TypeDeclarationSyntax node,
        string typeName,
        CheckerContext ctx,
        int count,
        int limit,
        int effectiveLimit,
        CompoundSuppression? configured,
        Dictionary<string, int> metrics,
        IReadOnlyList<CompoundSuppression>? suppressions)
    {
        if (effectiveLimit > 0)
        {
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured!.WhenAllOf, metrics);
            var severityOverride = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
                LinterRuleIds.MaxPublicMembersPerType, suppressions, metrics);
            var severityHint = severityOverride == "warning"
                ? " Severity auf 'warning' herabgestuft — kein Build-Fehler."
                : string.Empty;
            ctx.ReportViolation(node, new ViolationDescription(
                LinterRuleIds.MaxPublicMembersPerType,
                $"'{typeName}' hat {count} öffentliche Member (Compound-Limit: {effectiveLimit}; Standard: {limit} · {condSummary}).",
                $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten. Teile den Typ nach Single-Responsibility auf. Ziel: ≤ {effectiveLimit} Member bei weiterhin {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)}.{severityHint}",
                EffectiveSeverity: severityOverride));
            return;
        }

        if (configured != null)
        {
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured.WhenAllOf, metrics);
            var relaxedLimit = configured.RelaxedLimit.HasValue ? $"effektives Limit steigt auf {configured.RelaxedLimit}." : "Violation wird vollständig supprimiert.";
            ctx.ReportViolation(node, new ViolationDescription(
                LinterRuleIds.MaxPublicMembersPerType,
                $"'{typeName}' hat {count} öffentliche Member (erlaubt: {limit} · Compound-Suppression inaktiv: {condSummary}).",
                $"Optionen: (1) Metriken senken auf {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)} → {relaxedLimit} (2) Teile den Typ nach Single-Responsibility auf."));
            return;
        }

        ctx.ReportViolation(node, new ViolationDescription(
            LinterRuleIds.MaxPublicMembersPerType,
            $"'{typeName}' hat {count} öffentliche Member (erlaubt: {limit}). Eine breite API-Oberfläche erhöht die Wahrscheinlichkeit, dass Agenten vorhandene Methoden übersehen und duplizieren.",
            "Teile den Typ nach Single-Responsibility auf (z. B. QueryService / CommandService). Prüfe, ob Methoden auf 'internal' oder 'private' reduziert werden können."));
    }

    internal static int CountPublicMembers(TypeDeclarationSyntax node)
    {
        var count = 0;
        foreach (var member in node.Members)
        {
            if (!IsPublicMember(member)) continue;
            if (IsOverrideOrExplicitImpl(member)) continue;
            count++;
        }
        return count;
    }

    private static bool IsPublicMember(MemberDeclarationSyntax member)
    {
        if (!member.Modifiers.Any(SyntaxKind.PublicKeyword)) return false;
        return member is MethodDeclarationSyntax
            or PropertyDeclarationSyntax
            or EventDeclarationSyntax
            or EventFieldDeclarationSyntax
            or FieldDeclarationSyntax;
    }

    private static bool IsOverrideOrExplicitImpl(MemberDeclarationSyntax member)
    {
        if (member.Modifiers.Any(SyntaxKind.OverrideKeyword)) return true;
        if (member is MethodDeclarationSyntax method && method.ExplicitInterfaceSpecifier != null) return true;
        if (member is PropertyDeclarationSyntax prop && prop.ExplicitInterfaceSpecifier != null) return true;
        if (member is EventDeclarationSyntax evt && evt.ExplicitInterfaceSpecifier != null) return true;
        return false;
    }
}
