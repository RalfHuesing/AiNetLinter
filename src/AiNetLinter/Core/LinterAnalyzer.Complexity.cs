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

        var paramCount = node.ParameterList.Parameters.Count;
        if (paramCount > _config.Metrics.MaxMethodParameterCount)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxMethodParameterCount),
                Details = $"Die Methode '{node.Identifier.Text}' hat {paramCount} Parameter (erlaubt sind maximal {_config.Metrics.MaxMethodParameterCount}).",
                Guidance = "Kapsle die Parameter in einen C# record (Parameter Object)."
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
