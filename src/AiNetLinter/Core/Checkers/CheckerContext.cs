#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Configuration;
using AiNetLinter.Metrics;
using AiNetLinter.Models;

namespace AiNetLinter.Core.Checkers;

/// <summary>
/// Gemeinsamer Kontext für alle Checker-Klassen: Dateiinformationen, Konfiguration und Verstoß-Sammlung.
/// </summary>
internal sealed class CheckerContext
{
    private readonly List<RuleViolation> _violations = new();

    internal string FilePath { get; }
    internal LinterConfig Config { get; }
    internal SemanticModel SemanticModel { get; }
    internal bool IsTestFile { get; }
    internal string? ProjectName { get; }
    internal string CurrentNamespace { get; set; } = "";

    internal List<ClassInfo> Classes { get; } = new();
    internal List<PartialClassPart> PartialClassParts { get; } = new();

    internal CheckerContext(string filePath, LinterConfig config, SemanticModel semanticModel, bool isTestFile, string? projectName)
    {
        FilePath = filePath;
        Config = config;
        SemanticModel = semanticModel;
        IsTestFile = isTestFile;
        ProjectName = projectName;
    }

    internal void AddViolation(RuleViolation violation) => _violations.Add(violation);

    /// <summary>
    /// Kurzform für AddViolation — FilePath und LineNumber werden automatisch gesetzt.
    /// </summary>
    internal void ReportViolation(SyntaxNode node, string ruleName, string details, string guidance) =>
        AddViolation(new RuleViolation
        {
            FilePath   = FilePath,
            LineNumber = SyntaxHelper.LineOf(node),
            RuleName   = ruleName,
            Details    = details,
            Guidance   = guidance,
        });

    internal void ReportViolation(SyntaxToken token, string ruleName, string details, string guidance) =>
        AddViolation(new RuleViolation
        {
            FilePath   = FilePath,
            LineNumber = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            RuleName   = ruleName,
            Details    = details,
            Guidance   = guidance,
        });

    internal void ReportViolationAtLine(int lineNumber, string ruleName, string details, string guidance) =>
        AddViolation(new RuleViolation
        {
            FilePath   = FilePath,
            LineNumber = lineNumber,
            RuleName   = ruleName,
            Details    = details,
            Guidance   = guidance,
        });

    internal IReadOnlyList<RuleViolation> Violations => _violations;

    internal void ReplaceViolations(IEnumerable<RuleViolation> active)
    {
        _violations.Clear();
        _violations.AddRange(active);
    }
}
