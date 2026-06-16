#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

sealed record ComplexityCheck(int Complexity, int Limit, string RuleName, string Label, string Guidance);

/// <summary>
/// Domain-specific partial class file handling code complexity rules such as cyclomatic and cognitive complexity metrics.
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        CheckXmlDoc(node, node.Identifier.Text, "Methode");
        CheckPascalCase(node.Identifier, "Methode");
        
        bool isPublicMethod = node.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        CheckSemanticNaming(node.ParameterList, isPublicMethod);

        var effectiveLimit = GetEffectiveParamLimit();
        var totalParamCount = node.ParameterList.Parameters.Count;
        var effectiveParamCount = CountEffectiveParameters(node.ParameterList.Parameters);
        if (effectiveParamCount > effectiveLimit
            && !IsOverrideOrInterfaceImplementation(node))
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxMethodParameterCount),
                Details = BuildParamCountDetails(node.Identifier.Text, totalParamCount, effectiveParamCount, effectiveLimit),
                Guidance = $"Erstelle 'sealed record {node.Identifier.Text}Parameters(...)' mit den bisherigen Parametern als Properties und ersetze die Parameterliste der Methode durch diesen einen Record-Parameter (Parameter-Object-Pattern)."
            });
        }

        CheckMethodComplexities(node);
        CheckMethodLineCount(node);
        CheckBusinessLogicBoundary(node);

        base.VisitMethodDeclaration(node);
    }

    private void CheckMethodLineCount(MethodDeclarationSyntax node)
    {
        var codeLineCount = MethodLineCounter.GetCodeLineCount(node);
        if (codeLineCount == 0) return;

        if (codeLineCount > _config.Metrics.MaxMethodLineCount)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxMethodLineCount),
                Details = $"Die Methode '{node.Identifier.Text}' hat {codeLineCount} Codezeilen (erlaubt sind maximal {_config.Metrics.MaxMethodLineCount}, ohne Kommentare und Leerzeilen).",
                Guidance = "Lagere logische Abschnitte in kleinere Hilfsmethoden aus (Extract Method), um den Code für KI-Agenten besser editierbar zu halten."
            });
        }
    }

    private void CheckMethodComplexities(MethodDeclarationSyntax node)
    {
        var isDispatcher = _config.Metrics.ExcludeSwitchDispatcherCases
            && SwitchDispatcherDetector.IsDispatcher(node, _config.Metrics.SwitchDispatcherMaxCaseBodyLines);

        var cyclomaticComplexity = isDispatcher
            ? SwitchDispatcherDetector.GetAdjustedCyclomaticComplexity(node)
            : ComplexityCalculator.GetCyclomaticComplexity(node);

        ReportComplexityIfViolation(node, new ComplexityCheck(
            cyclomaticComplexity,
            _config.Metrics.MaxCyclomaticComplexity,
            nameof(_config.Metrics.MaxCyclomaticComplexity),
            "Zyklomatische Komplexitaet",
            "Teile die Methode in kleinere Hilfsmethoden auf und reduziere Verzweigungen (ifs, Schleifen, logische Ketten)."));

        var cognitiveComplexity = isDispatcher
            ? SwitchDispatcherDetector.GetAdjustedCognitiveComplexity(node)
            : ComplexityCalculator.GetCognitiveComplexity(node);

        ReportComplexityIfViolation(node, new ComplexityCheck(
            cognitiveComplexity,
            _config.Metrics.MaxCognitiveComplexity,
            nameof(_config.Metrics.MaxCognitiveComplexity),
            "Kognitive Komplexitaet",
            CognitiveComplexityGuidance.Build(node, cognitiveComplexity, _config.Metrics.MaxCognitiveComplexity)));
    }

    private int GetEffectiveParamLimit()
    {
        var testLimit = _config.Metrics.MaxMethodParameterCountInTestFiles;
        if (_isTestFile && testLimit > 0)
            return testLimit;
        return _config.Metrics.MaxMethodParameterCount;
    }

    private string BuildParamCountDetails(string methodName, int total, int effective, int limit)
    {
        var ignoreTypes = _config.Metrics.MethodParameterCountIgnoreTypeNames;
        var hasIgnored = ignoreTypes != null && ignoreTypes.Count > 0;

        if (hasIgnored)
        {
            var ignored = string.Join(", ", ignoreTypes!);
            return $"Die Methode '{methodName}' hat {total} Parameter, davon {effective} gewertet (erlaubt sind maximal {limit}); nicht mitgezählt: {ignored}.";
        }

        return $"Die Methode '{methodName}' hat {total} Parameter (erlaubt sind maximal {limit}).";
    }

    private int CountEffectiveParameters(SeparatedSyntaxList<ParameterSyntax> parameters)
    {
        var ignoreTypes = _config.Metrics.MethodParameterCountIgnoreTypeNames;
        if (ignoreTypes == null || ignoreTypes.Count == 0)
            return parameters.Count;
        return parameters.Count(p => !IsIgnoredParamType(p, ignoreTypes));
    }

    private static bool IsIgnoredParamType(ParameterSyntax param, IReadOnlyCollection<string> ignoreTypes)
    {
        if (param.Type == null) return false;
        var name = GetSimpleTypeName(param.Type);
        return name != null && ignoreTypes.Contains(name, StringComparer.Ordinal);
    }

    private bool IsOverrideOrInterfaceImplementation(MethodDeclarationSyntax node)
    {
        if (node.Modifiers.Any(SyntaxKind.OverrideKeyword)) return true;
        if (node.ExplicitInterfaceSpecifier != null) return true;

        var symbol = _semanticModel.GetDeclaredSymbol(node);
        if (symbol == null) return false;

        if (symbol.ExplicitInterfaceImplementations.Length > 0) return true;
        return IsImplicitInterfaceImplementation(symbol);
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

    private void ReportComplexityIfViolation(MethodDeclarationSyntax node, ComplexityCheck check)
    {
        if (check.Complexity <= check.Limit) return;

        var tolerance = _config.Metrics.ComplexityNearMissTolerance;
        var isNearMiss = tolerance > 0 && check.Complexity <= check.Limit + tolerance;
        var nearMissHint = isNearMiss ? " [near-miss: knapp über Limit]" : "";

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = check.RuleName,
            Details = $"Die Methode '{node.Identifier.Text}' hat eine {check.Label} von {check.Complexity} " +
                      $"(erlaubt sind maximal {check.Limit}).{nearMissHint}",
            Guidance = check.Guidance,
        });
    }
}
