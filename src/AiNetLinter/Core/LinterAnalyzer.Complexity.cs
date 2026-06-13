using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

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
        var cyclomaticComplexity = ComplexityCalculator.GetCyclomaticComplexity(node);
        if (cyclomaticComplexity > _config.Metrics.MaxCyclomaticComplexity)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxCyclomaticComplexity),
                Details = $"Die Methode '{node.Identifier.Text}' hat eine Zyklomatische Komplexitaet von {cyclomaticComplexity} (erlaubt sind maximal {_config.Metrics.MaxCyclomaticComplexity}).",
                Guidance = "Teile die Methode in kleinere Hilfsmethoden auf und reduziere Verzweigungen (ifs, Schleifen, logische Ketten)."
            });
        }

        var cognitiveComplexity = ComplexityCalculator.GetCognitiveComplexity(node);
        if (cognitiveComplexity > _config.Metrics.MaxCognitiveComplexity)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = nameof(_config.Metrics.MaxCognitiveComplexity),
                Details = $"Die Methode '{node.Identifier.Text}' hat eine Kognitive Komplexitaet von {cognitiveComplexity} (erlaubt sind maximal {_config.Metrics.MaxCognitiveComplexity}).",
                Guidance = CognitiveComplexityGuidance.Build(
                    node,
                    cognitiveComplexity,
                    _config.Metrics.MaxCognitiveComplexity),
            });
        }
    }
}
