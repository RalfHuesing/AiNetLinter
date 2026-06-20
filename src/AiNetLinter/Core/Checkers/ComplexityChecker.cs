#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core.Checkers;

internal sealed record ComplexityCheck(int Complexity, int Limit, string RuleName, string Label, string Guidance);

internal sealed record ParamViolationArgs(
    MethodDeclarationSyntax Node,
    CheckerContext Ctx,
    int EffectiveLimit,
    CompoundSuppression? Configured,
    int BaseLimit,
    int EffectiveParamCount,
    Dictionary<string, int> Metrics,
    string? SeverityOverride = null
);

internal static class ComplexityChecker
{
    internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        var (cc, cogC) = ComputeComplexities(node, ctx);
        CheckParamCount(node, ctx, cc, cogC);
        CheckMethodComplexities(node, ctx, cc, cogC);
        CheckMethodLineCount(node, ctx, cc, cogC);
        CheckSwitchArms(node, ctx);
    }

    private static (int cc, int cogC) ComputeComplexities(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        var isDispatcher = ctx.Config.Metrics.ExcludeSwitchDispatcherCases
            && SwitchDispatcherDetector.IsDispatcher(node, ctx.Config.Metrics.SwitchDispatcherMaxCaseBodyLines);

        var cc = isDispatcher
            ? SwitchDispatcherDetector.GetAdjustedCyclomaticComplexity(node)
            : ComplexityCalculator.GetCyclomaticComplexity(node);

        var cogC = isDispatcher
            ? SwitchDispatcherDetector.GetAdjustedCognitiveComplexity(node)
            : ComplexityCalculator.GetCognitiveComplexity(node);

        return (cc, cogC);
    }

    private static void CheckParamCount(MethodDeclarationSyntax node, CheckerContext ctx, int cc, int cogC)
    {
        var baseLimit = GetEffectiveParamLimit(ctx);
        var effectiveParamCount = CountEffectiveParameters(node.ParameterList.Parameters, ctx);
        var codeLineCount = MethodLineCounter.GetCodeLineCount(node);

        var suppressions = ctx.Config.Metrics.CompoundSuppressions;

        var metrics = new Dictionary<string, int>
        {
            [MetricNames.CyclomaticComplexity] = cc,
            [MetricNames.CognitiveComplexity]  = cogC,
            [MetricNames.ParameterCount]       = node.ParameterList.Parameters.Count,
            [MetricNames.LineCount]            = codeLineCount,
        };

        var effectiveLimit = CompoundSuppressionEvaluator.Evaluate(
            LinterRuleIds.MaxMethodParameterCount, suppressions, metrics);

        if (effectiveLimit == 0) return; // vollständig supprimiert

        var configured = CompoundSuppressionEvaluator.FindConfigured(
            LinterRuleIds.MaxMethodParameterCount, suppressions);

        var activeLimit = effectiveLimit > 0 ? effectiveLimit : baseLimit;

        if (effectiveParamCount <= activeLimit || IsOverrideOrInterfaceImplementation(node, ctx)) return;

        var severityOverride = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
            LinterRuleIds.MaxMethodParameterCount, suppressions, metrics);

        ReportParamViolation(new ParamViolationArgs(node, ctx, effectiveLimit, configured, baseLimit, effectiveParamCount, metrics, severityOverride));
    }

    private static void ReportParamViolation(ParamViolationArgs args)
    {
        var methodName = args.Node.Identifier.Text;
        var total = args.Node.ParameterList.Parameters.Count;
        var ignTypes = args.Ctx.Config.Metrics.MethodParameterCountIgnoreTypeNames;
        var ignPfx = args.Ctx.Config.Metrics.MethodParameterCountIgnoreTypePrefixes;
        var hasIgnored = (ignTypes?.Count ?? 0) > 0 || (ignPfx?.Count ?? 0) > 0;

        string details;
        if (args.EffectiveLimit > 0)
        {
            // Scenario A: Suppression active, but RelaxedLimit exceeded
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(args.Configured!.WhenAllOf, args.Metrics);
            details = $"Die Methode '{methodName}' hat {total} Parameter, davon {args.EffectiveParamCount} gewertet (Compound-Limit: {args.EffectiveLimit}; Standard: {args.BaseLimit} · {condSummary}).";
            var severityHint = args.SeverityOverride == "warning"
                ? " Severity auf 'warning' herabgestuft — kein Build-Fehler."
                : string.Empty;
            args.Ctx.ReportViolation(args.Node, new ViolationDescription(
                LinterRuleIds.MaxMethodParameterCount,
                details,
                $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten. Erstelle ein Parameter-Object für die Parameter. Ziel: ≤ {args.EffectiveLimit} Parameter bei weiterhin {CompoundSuppressionEvaluator.BuildThresholdSummary(args.Configured.WhenAllOf)}.{severityHint}",
                EffectiveSeverity: args.SeverityOverride));
            return;
        }

        if (args.Configured != null)
        {
            // Scenario B: Suppression configured, but not active
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(args.Configured.WhenAllOf, args.Metrics);
            var relaxedLimit = args.Configured.RelaxedLimit.HasValue ? $"effektives Limit steigt auf {args.Configured.RelaxedLimit}." : "Violation wird vollständig supprimiert.";
            details = $"Die Methode '{methodName}' hat {total} Parameter, davon {args.EffectiveParamCount} gewertet (erlaubt: {args.BaseLimit} · Compound-Suppression inaktiv: {condSummary}).";
            args.Ctx.ReportViolation(args.Node, new ViolationDescription(
                LinterRuleIds.MaxMethodParameterCount,
                details,
                $"Optionen: (1) Komplexität/Metriken senken auf {CompoundSuppressionEvaluator.BuildThresholdSummary(args.Configured.WhenAllOf)} → {relaxedLimit} (2) Erstelle ein Parameter-Object für die Parameter."));
            return;
        }

        // Scenario C: Classic, no suppression configured
        if (hasIgnored)
        {
            var parts = new List<string>();
            if (ignTypes?.Count > 0) parts.AddRange(ignTypes);
            if (ignPfx?.Count > 0) parts.AddRange(ignPfx.Select(p => p + "*"));
            details = $"Die Methode '{methodName}' hat {total} Parameter, davon {args.EffectiveParamCount} gewertet (erlaubt sind maximal {args.BaseLimit}); nicht mitgezählt: {string.Join(", ", parts)}.";
        }
        else
        {
            details = $"Die Methode '{methodName}' hat {total} Parameter (erlaubt sind maximal {args.BaseLimit}).";
        }
        args.Ctx.ReportViolation(args.Node, new ViolationDescription(
            LinterRuleIds.MaxMethodParameterCount,
            details,
            $"Erstelle 'sealed record {methodName}Parameters(...)' mit den bisherigen Parametern als Properties und ersetze die Parameterliste der Methode durch diesen einen Record-Parameter (Parameter-Object-Pattern)."));
    }

    internal static bool IsOverrideOrInterfaceImplementation(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        if (node.Modifiers.Any(SyntaxKind.OverrideKeyword)) return true;
        if (node.ExplicitInterfaceSpecifier != null) return true;

        var symbol = ctx.SemanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        if (symbol.ExplicitInterfaceImplementations.Length > 0) return true;
        return IsImplicitInterfaceImplementation(symbol);
    }

    internal static int GetMaxMethodComplexity(TypeDeclarationSyntax node)
    {
        var max = 0;
        foreach (var method in node.Members.OfType<MethodDeclarationSyntax>())
            max = Math.Max(max, ComplexityCalculator.GetCognitiveComplexity(method));
        return max;
    }

    private static void CheckMethodLineCount(MethodDeclarationSyntax node, CheckerContext ctx, int cc, int cogC)
    {
        var codeLineCount = MethodLineCounter.GetCodeLineCount(node);
        if (codeLineCount == 0) return;

        var methodName = node.Identifier.Text;
        var baseLimit = ctx.Config.Metrics.MaxMethodLineCount;
        var suppressions = ctx.Config.Metrics.CompoundSuppressions;

        var metrics = new Dictionary<string, int>
        {
            [MetricNames.CyclomaticComplexity] = cc,
            [MetricNames.CognitiveComplexity]  = cogC,
            [MetricNames.ParameterCount]       = node.ParameterList.Parameters.Count,
            [MetricNames.LineCount]            = codeLineCount,
        };

        var effectiveLimit = CompoundSuppressionEvaluator.Evaluate(
            LinterRuleIds.MaxMethodLineCount, suppressions, metrics);

        if (effectiveLimit == 0) return; // vollständig supprimiert

        var configured = CompoundSuppressionEvaluator.FindConfigured(
            LinterRuleIds.MaxMethodLineCount, suppressions);

        if (effectiveLimit > 0 && codeLineCount <= effectiveLimit) return; // Relaxed-Limit eingehalten

        if (effectiveLimit > 0)
        {
            // Szenario A: Suppression aktiv, aber RelaxedLimit überschritten
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured!.WhenAllOf, metrics);
            var severityOverride = CompoundSuppressionEvaluator.GetActiveSeverityOverride(
                LinterRuleIds.MaxMethodLineCount, suppressions, metrics);
            var severityHint = severityOverride == "warning"
                ? " Severity auf 'warning' herabgestuft — kein Build-Fehler."
                : string.Empty;
            ctx.ReportViolation(node, new ViolationDescription(
                LinterRuleIds.MaxMethodLineCount,
                $"Die Methode '{methodName}' hat {codeLineCount} Codezeilen (Compound-Limit: {effectiveLimit}; Standard: {baseLimit} · {condSummary}).",
                $"Compound-Bedingungen erfüllt, aber relaxiertes Limit ebenfalls überschritten. Lagere weitere Abschnitte aus. Ziel: ≤ {effectiveLimit} Zeilen bei weiterhin {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)}.{severityHint}",
                EffectiveSeverity: severityOverride));
            return;
        }

        if (codeLineCount <= baseLimit) return; // unter Basis-Limit, kein Problem

        if (configured != null)
        {
            // Szenario B: Suppression konfiguriert, aber nicht aktiv
            var condSummary = CompoundSuppressionEvaluator.BuildConditionSummary(configured.WhenAllOf, metrics);
            var relaxedLimit = configured.RelaxedLimit.HasValue ? $"effektives Limit steigt auf {configured.RelaxedLimit}." : "Violation wird vollständig supprimiert.";
            ctx.ReportViolation(node, new ViolationDescription(
                LinterRuleIds.MaxMethodLineCount,
                $"Die Methode '{methodName}' hat {codeLineCount} Codezeilen (erlaubt: {baseLimit} · Compound-Suppression inaktiv: {condSummary}).",
                $"Optionen: (1) Komplexität senken auf {CompoundSuppressionEvaluator.BuildThresholdSummary(configured.WhenAllOf)} → {relaxedLimit} (2) Methode auf ≤ {baseLimit} Zeilen kürzen."));
            return;
        }

        // Szenario C: Klassisch, keine Suppression konfiguriert
        var scenarioCGuidance = codeLineCount > baseLimit * 2
            ? $"Die Methode ist mehr als doppelt so lang wie erlaubt — prüfe ob ein vollständiges Refactoring sinnvoll ist: Zerlege in Handler, Steps oder Strategies (jede Einheit ≤ {baseLimit} Zeilen, eine Aufgabe pro Methode). Extract Method allein reicht hier wahrscheinlich nicht."
            : "Lagere logische Abschnitte in kleinere Hilfsmethoden aus (Extract Method), um den Code fuer KI-Agenten besser editierbar zu halten.";
        ctx.ReportViolation(node, new ViolationDescription(
            LinterRuleIds.MaxMethodLineCount,
            $"Die Methode '{methodName}' hat {codeLineCount} Codezeilen (erlaubt sind maximal {baseLimit}, ohne Kommentare und Leerzeilen).",
            scenarioCGuidance));
    }

    private static void CheckMethodComplexities(MethodDeclarationSyntax node, CheckerContext ctx, int cc, int cogC)
    {
        if (ctx.Config.Metrics.ExcludeNullCoalescingInitializerComplexity
            && MethodClassifier.IsNullCoalescingInitializer(node, ctx.Config.Metrics.NullCoalescingInitializerMaxNonCoalescingRatio))
        {
            return;
        }

        ReportComplexityIfViolation(node, new ComplexityCheck(
            cc, ctx.Config.Metrics.MaxCyclomaticComplexity,
            LinterRuleIds.MaxCyclomaticComplexity, "Zyklomatische Komplexitaet",
            "Teile die Methode in kleinere Hilfsmethoden auf und reduziere Verzweigungen (ifs, Schleifen, logische Ketten)."), ctx);

        ReportComplexityIfViolation(node, new ComplexityCheck(
            cogC, ctx.Config.Metrics.MaxCognitiveComplexity,
            LinterRuleIds.MaxCognitiveComplexity, "Kognitive Komplexitaet",
            CognitiveComplexityGuidance.Build(node, cogC, ctx.Config.Metrics.MaxCognitiveComplexity)), ctx);
    }

    private static int GetEffectiveParamLimit(CheckerContext ctx)
    {
        var testLimit = ctx.Config.Metrics.MaxMethodParameterCountInTestFiles;
        if (ctx.IsTestFile && testLimit > 0) return testLimit;
        return ctx.Config.Metrics.MaxMethodParameterCount;
    }

    internal static int CountEffectiveParameters(SeparatedSyntaxList<ParameterSyntax> parameters, CheckerContext ctx)
    {
        var ignoreTypes = ctx.Config.Metrics.MethodParameterCountIgnoreTypeNames;
        var ignorePrefixes = ctx.Config.Metrics.MethodParameterCountIgnoreTypePrefixes;
        var hasNames = ignoreTypes != null && ignoreTypes.Count > 0;
        var hasPrefixes = ignorePrefixes != null && ignorePrefixes.Count > 0;

        if (!hasNames && !hasPrefixes) return parameters.Count;

        return parameters.Count(p => !IsIgnoredParamType(p, ignoreTypes, ignorePrefixes));
    }

    private static bool IsIgnoredParamType(
        ParameterSyntax param,
        IReadOnlyCollection<string>? ignoreTypes,
        IReadOnlyCollection<string>? ignorePrefixes)
    {
        if (param.Type == null) return false;
        var name = SyntaxHelper.GetSimpleTypeName(param.Type);
        if (name == null) return false;

        if (ignoreTypes != null && ignoreTypes.Count > 0 && ignoreTypes.Contains(name, StringComparer.Ordinal))
            return true;

        if (ignorePrefixes != null && ignorePrefixes.Count > 0)
        {
            foreach (var prefix in ignorePrefixes)
                if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static void ReportComplexityIfViolation(MethodDeclarationSyntax node, ComplexityCheck check, CheckerContext ctx)
    {
        if (check.Complexity <= check.Limit) return;

        var tolerance = ctx.Config.Metrics.ComplexityNearMissTolerance;
        var isNearMiss = tolerance > 0 && check.Complexity <= check.Limit + tolerance;
        var nearMissHint = isNearMiss ? " [near-miss: knapp über Limit]" : "";

        ctx.ReportViolation(node, new ViolationDescription(
            check.RuleName,
            $"Die Methode '{node.Identifier.Text}' hat eine {check.Label} von {check.Complexity} (erlaubt sind maximal {check.Limit}).{nearMissHint}",
            check.Guidance));
    }

    private static bool IsImplicitInterfaceImplementation(IMethodSymbol symbol)
    {
        var type = symbol.ContainingType;
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers().OfType<IMethodSymbol>())
            {
                var impl = type.FindImplementationForInterfaceMember(member);
                if (impl != null && SymbolEqualityComparer.Default.Equals(impl, symbol))
                    return true;
            }
        }
        return false;
    }

    private static void CheckSwitchArms(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        var limit = ctx.Config.Metrics.MaxSwitchArms;
        if (limit <= 0) return;

        // Dispatcher-Exemption: gesamte Methode ausschließen wenn sie als Dispatcher gilt
        if (ctx.Config.Metrics.MaxSwitchArmsExcludeDispatcher
            && SwitchDispatcherDetector.IsDispatcher(node, ctx.Config.Metrics.SwitchDispatcherMaxCaseBodyLines))
            return;

        // Typ-Exemption: Methoden in bestimmten Klassen/Records ausschließen
        var exemptTypes = ctx.Config.Metrics.MaxSwitchArmsExemptTypes;
        if (exemptTypes != null && exemptTypes.Count > 0)
        {
            var typeName = node.Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Select(t => t.Identifier.Text)
                .FirstOrDefault() ?? string.Empty;

            if (exemptTypes.Contains(typeName, StringComparer.Ordinal)) return;
        }

        // Switch-Expressions: Arms.Count direkt
        foreach (var switchExpr in node.DescendantNodes().OfType<SwitchExpressionSyntax>())
        {
            var count = switchExpr.Arms.Count;
            if (count > limit)
                ctx.ReportViolation(switchExpr, new ViolationDescription(
                    LinterRuleIds.MaxSwitchArms,
                    $"Switch-Expression hat {count} Arms (erlaubt: {limit}).",
                    $"Refaktoriere zu einem Dictionary-Dispatch oder extrahiere das Switch in eine dedizierte Dispatcher-Methode. Alternativ: 'MaxSwitchArmsExemptTypes' fuer legitime State-Machines nutzen."));
        }

        // Switch-Statements: Labels zaehlen (nicht Sections — eine Section kann mehrere Labels haben)
        foreach (var switchStmt in node.DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            var count = switchStmt.Sections.SelectMany(s => s.Labels).Count();
            if (count > limit)
                ctx.ReportViolation(switchStmt, new ViolationDescription(
                    LinterRuleIds.MaxSwitchArms,
                    $"Switch-Statement hat {count} Labels (erlaubt: {limit}).",
                    $"Refaktoriere zu einem Dictionary-Dispatch oder extrahiere das Switch in eine dedizierte Dispatcher-Methode. Alternativ: 'MaxSwitchArmsExemptTypes' fuer legitime State-Machines nutzen."));
        }
    }
}
