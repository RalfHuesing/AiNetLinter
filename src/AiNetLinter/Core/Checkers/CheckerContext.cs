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
    internal Config Config { get; }
    internal SemanticModel SemanticModel { get; }
    internal bool IsTestFile { get; }
    internal string? ProjectName { get; }

    /// <summary>
    /// <see langword="true"/>, wenn das Projekt dieses Dokuments beim Solution-Load erkennbare
    /// Lade-Probleme hatte (aktuell: fehlender/veralteter <c>dotnet restore</c>, siehe
    /// <see cref="AiNetLinter.Baseline.ProjectRestoreState"/>). Checker, die auf vollstaendig
    /// aufgeloeste Referenzen angewiesen sind (z. B. <see cref="PhantomDependencyChecker"/>),
    /// nutzen dieses Flag, um Folgefehler eines Lade-Problems nicht als eigenstaendige Verstoesse
    /// zu melden.
    /// </summary>
    internal bool ProjectHasLoadDiagnostics { get; }

    internal string CurrentNamespace { get; set; } = "";

    internal List<ClassInfo> Classes { get; } = new();
    internal List<PartialClassPart> PartialClassParts { get; } = new();

    internal CheckerContext(string filePath, Config config, SemanticModel semanticModel, string? projectName, DocumentLoadState loadState)
    {
        FilePath = filePath;
        Config = config;
        SemanticModel = semanticModel;
        ProjectName = projectName;
        IsTestFile = loadState.IsTestFile;
        ProjectHasLoadDiagnostics = loadState.ProjectHasLoadDiagnostics;
    }

    internal void AddViolation(RuleViolation violation) => _violations.Add(violation);

    /// <summary>
    /// Kurzform für AddViolation — FilePath und LineNumber werden automatisch gesetzt.
    /// </summary>
    internal void ReportViolation(SyntaxNode node, ViolationDescription desc) =>
        AddViolation(new RuleViolation
        {
            FilePath          = FilePath,
            LineNumber        = node.LineOf(),
            RuleName          = desc.RuleName,
            Details           = desc.Details,
            Guidance          = desc.Guidance,
            EffectiveSeverity = desc.EffectiveSeverity,
        });

    internal void ReportViolation(SyntaxToken token, ViolationDescription desc) =>
        AddViolation(new RuleViolation
        {
            FilePath          = FilePath,
            LineNumber        = token.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            RuleName          = desc.RuleName,
            Details           = desc.Details,
            Guidance          = desc.Guidance,
            EffectiveSeverity = desc.EffectiveSeverity,
        });

    internal void ReportViolationAtLine(int lineNumber, ViolationDescription desc) =>
        AddViolation(new RuleViolation
        {
            FilePath          = FilePath,
            LineNumber        = lineNumber,
            RuleName          = desc.RuleName,
            Details           = desc.Details,
            Guidance          = desc.Guidance,
            EffectiveSeverity = desc.EffectiveSeverity,
        });

    internal IReadOnlyList<RuleViolation> Violations => _violations;

    internal void ReplaceViolations(IEnumerable<RuleViolation> active)
    {
        _violations.Clear();
        _violations.AddRange(active);
    }
}

/// <summary>
/// Beschreibt einen Regelverstoß (Regel-ID, Nachricht, Leitfaden, Severity).
/// Wird an ReportViolation-Overloads übergeben.
/// </summary>
internal sealed record ViolationDescription(
    string RuleName,
    string Details,
    string Guidance,
    string? EffectiveSeverity = null);

/// <summary>
/// Buendelt die beiden Lade-Zustands-Flags eines Dokuments (Test-Datei? Projekt mit Lade-Problemen?)
/// in einem Parameter-Object — haelt den <see cref="CheckerContext"/>-Konstruktor unter dem
/// projektweiten Bool-Parameter-Limit (siehe AiNetLinter.mdc), statt zwei rohe bool-Parameter
/// nebeneinander zu fuehren.
/// </summary>
internal sealed record DocumentLoadState(bool IsTestFile, bool ProjectHasLoadDiagnostics);

