#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

internal sealed record ComplexityCheck(int Complexity, int Limit, string RuleName, string Label, string Guidance);

internal static class ComplexityChecker
{
    internal static void CheckMethod(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        CheckParamCount(node, ctx);
        CheckMethodComplexities(node, ctx);
        CheckMethodLineCount(node, ctx);
    }

    private static void CheckParamCount(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        var effectiveLimit = GetEffectiveParamLimit(ctx);
        var effectiveParamCount = CountEffectiveParameters(node.ParameterList.Parameters, ctx);
        if (effectiveParamCount <= effectiveLimit || IsOverrideOrInterfaceImplementation(node, ctx)) return;

        var total = node.ParameterList.Parameters.Count;
        var ignTypes = ctx.Config.Metrics.MethodParameterCountIgnoreTypeNames;
        var ignPfx = ctx.Config.Metrics.MethodParameterCountIgnoreTypePrefixes;
        var hasIgnored = (ignTypes?.Count ?? 0) > 0 || (ignPfx?.Count ?? 0) > 0;
        string details;
        if (hasIgnored)
        {
            var parts = new List<string>();
            if (ignTypes?.Count > 0) parts.AddRange(ignTypes);
            if (ignPfx?.Count > 0) parts.AddRange(ignPfx.Select(p => p + "*"));
            details = $"Die Methode '{node.Identifier.Text}' hat {total} Parameter, davon {effectiveParamCount} gewertet (erlaubt sind maximal {effectiveLimit}); nicht mitgezählt: {string.Join(", ", parts)}.";
        }
        else
        {
            details = $"Die Methode '{node.Identifier.Text}' hat {total} Parameter (erlaubt sind maximal {effectiveLimit}).";
        }
        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = nameof(ctx.Config.Metrics.MaxMethodParameterCount),
            Details = details,
            Guidance = $"Erstelle 'sealed record {node.Identifier.Text}Parameters(...)' mit den bisherigen Parametern als Properties und ersetze die Parameterliste der Methode durch diesen einen Record-Parameter (Parameter-Object-Pattern)."
        });
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

    private static void CheckMethodLineCount(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        var codeLineCount = MethodLineCounter.GetCodeLineCount(node);
        if (codeLineCount == 0) return;

        if (codeLineCount > ctx.Config.Metrics.MaxMethodLineCount)
        {
            ctx.AddViolation(new RuleViolation
            {
                FilePath = ctx.FilePath,
                LineNumber = SyntaxHelper.LineOf(node),
                RuleName = nameof(ctx.Config.Metrics.MaxMethodLineCount),
                Details = $"Die Methode '{node.Identifier.Text}' hat {codeLineCount} Codezeilen (erlaubt sind maximal {ctx.Config.Metrics.MaxMethodLineCount}, ohne Kommentare und Leerzeilen).",
                Guidance = "Lagere logische Abschnitte in kleinere Hilfsmethoden aus (Extract Method), um den Code für KI-Agenten besser editierbar zu halten."
            });
        }
    }

    private static void CheckMethodComplexities(MethodDeclarationSyntax node, CheckerContext ctx)
    {
        var isDispatcher = ctx.Config.Metrics.ExcludeSwitchDispatcherCases
            && SwitchDispatcherDetector.IsDispatcher(node, ctx.Config.Metrics.SwitchDispatcherMaxCaseBodyLines);

        var cyclomaticComplexity = isDispatcher
            ? SwitchDispatcherDetector.GetAdjustedCyclomaticComplexity(node)
            : ComplexityCalculator.GetCyclomaticComplexity(node);

        ReportComplexityIfViolation(node, new ComplexityCheck(
            cyclomaticComplexity, ctx.Config.Metrics.MaxCyclomaticComplexity,
            nameof(ctx.Config.Metrics.MaxCyclomaticComplexity), "Zyklomatische Komplexitaet",
            "Teile die Methode in kleinere Hilfsmethoden auf und reduziere Verzweigungen (ifs, Schleifen, logische Ketten)."), ctx);

        var cognitiveComplexity = isDispatcher
            ? SwitchDispatcherDetector.GetAdjustedCognitiveComplexity(node)
            : ComplexityCalculator.GetCognitiveComplexity(node);

        ReportComplexityIfViolation(node, new ComplexityCheck(
            cognitiveComplexity, ctx.Config.Metrics.MaxCognitiveComplexity,
            nameof(ctx.Config.Metrics.MaxCognitiveComplexity), "Kognitive Komplexitaet",
            CognitiveComplexityGuidance.Build(node, cognitiveComplexity, ctx.Config.Metrics.MaxCognitiveComplexity)), ctx);
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

        ctx.AddViolation(new RuleViolation
        {
            FilePath = ctx.FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName = check.RuleName,
            Details = $"Die Methode '{node.Identifier.Text}' hat eine {check.Label} von {check.Complexity} (erlaubt sind maximal {check.Limit}).{nearMissHint}",
            Guidance = check.Guidance
        });
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
}
