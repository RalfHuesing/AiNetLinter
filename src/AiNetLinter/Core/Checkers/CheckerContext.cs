#nullable enable

using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
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

    internal FieldReadonlyTracker FieldTracker { get; } = new();
    internal ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>? SharedFieldTrackers { get; set; }

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

    internal IReadOnlyList<RuleViolation> Violations => _violations;

    internal void ReplaceViolations(IEnumerable<RuleViolation> active)
    {
        _violations.Clear();
        _violations.AddRange(active);
    }
}
