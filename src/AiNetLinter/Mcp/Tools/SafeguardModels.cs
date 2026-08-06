#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using AiNetLinter.Configuration;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

// Parameter- und Ergebnis-Records sowie die Malfunction-Exception fuer SafeguardScanner — aus der
// Scanner-Datei ausgelagert, damit die eigentliche Score-Berechnungslogik unter dem
// MaxLineCount-Limit bleibt. Reine Datentraeger ohne eigenes Verhalten.

/// <summary>Parameter-Record fuer <see cref="SafeguardScanner.BuildScoreResult"/> — Pattern
/// konsistent mit <see cref="SafeguardScannerParameters"/>. Records sind vom
/// <c>MaxMethodParameterCount: 4</c>-Limit ausgenommen.</summary>
internal sealed record BuildScoreResultParameters(
    IReadOnlyCollection<RuleViolation> Violations,
    IReadOnlyList<ScannedClass> Classes,
    Config Config,
    double Threshold,
    int MaxRemediationEntries);

/// <summary>
/// Parameter-Record fuer <see cref="SafeguardScanner.ComputeScoreAsync"/>. Kapselt 7
/// Konfigurations-Eingaenge in einem Record, damit <c>MaxMethodParameterCount: 4</c>
/// (siehe <c>AiNetLinter.mdc</c>) eingehalten wird. <see cref="MinScoreThreshold"/> und
/// <see cref="MaxRemediationEntries"/> haben Defaults aus <see cref="SafeguardScanner.DefaultMinScoreThreshold"/>
/// / <see cref="SafeguardScanner.DefaultMaxRemediationEntries"/>.
/// </summary>
internal sealed record SafeguardScannerParameters(
    Solution Solution,
    ILinterEngineConfig Config,
    ILintConsole Console,
    string? ScopeFilter,
    CancellationToken CancellationToken,
    double MinScoreThreshold = SafeguardScanner.DefaultMinScoreThreshold,
    int MaxRemediationEntries = SafeguardScanner.DefaultMaxRemediationEntries);

/// <summary>
/// Ergebnis-Container fuer <see cref="SafeguardScanner.ComputeScoreAsync"/>. <see cref="IsMalfunction"/>
/// unterscheidet eine echte LinterEngine-Malfunction (<see cref="Context"/> non-null) von einem
/// normal berechneten Score (selbst bei 0 Verstoessen kein Malfunction).
/// </summary>
internal sealed record SafeguardScoreResult(
    ScoreResult? Score,
    bool IsMalfunction,
    string? Context = null);

/// <summary>
/// Score-Aggregat-Container mit den vier Score-Komponenten (Violations/CC/Footprint/Sealed-Bonus)
/// aggregiert in <see cref="Score"/>, der <see cref="Threshold"/> als Pass-Grenze, den
/// top-relevanten <see cref="Violations"/>, einem strukturierten <see cref="Remediation"/>-Hint
/// und einer kompakten <see cref="Summary"/>-Zeile.
/// </summary>
internal sealed record ScoreResult(
    bool Passed,
    double Score,
    double Threshold,
    IReadOnlyList<ViolationEntry> Violations,
    RemediationHint Remediation,
    string Summary);

/// <summary>
/// 1:1-Mapping aus <see cref="RuleViolation"/> fuer den JSON-Schema-Output:
/// sortier- und vergleichbar nach <c>(FilePath, LineNumber)</c>.
/// </summary>
internal sealed record ViolationEntry(
    string FilePath,
    int LineNumber,
    string RuleName,
    string Details,
    string Severity,
    string Guidance);

/// <summary>
/// Strukturierte Remediation statt freier Text, damit sich der Output in ein strukturiertes
/// JSON-Schema mappen laesst. <see cref="TopIssue"/> ist die haeufigste Regel unter den
/// Top-Violations, <see cref="ActionableSteps"/> ist die nach Haeufigkeit sortierte Liste der
/// kontextspezifischen Empfehlungen, <see cref="DocumentationHint"/> verweist auf die zentrale
/// Konfigurationsdokumentation.
/// </summary>
internal sealed record RemediationHint(
    string TopIssue,
    IReadOnlyList<string> ActionableSteps,
    string DocumentationHint);

/// <summary>
/// Interner Daten-Container fuer die von <c>SafeguardScanner.EnumerateConcreteClassesAsync</c>
/// gesammelten Klassen-Metriken. Wird intern zwischen Scanner und <see cref="SafeguardScanner.BuildScoreResult"/>
/// weitergereicht; bewusst kein <c>INamedTypeSymbol</c>, damit <see cref="SafeguardScanner.BuildScoreResult"/>
/// ohne Roslyn-Symbols testbar bleibt.
/// </summary>
internal sealed record ScannedClass(
    string Name,
    int MaxCognitiveComplexity,
    int AIContextFootprint,
    bool IsSealed);

/// <summary>
/// Wird geworfen, wenn ein grundsaetzlich kompilierbares Projekt (<c>Project.SupportsCompilation ==
/// true</c>) auch nach <see cref="SafeguardScanner.CompilationRetryAttempts"/> Versuchen keine
/// <see cref="Compilation"/> liefert. <see cref="SafeguardScanner.ComputeScoreAsync"/> faengt diese
/// Exception im selben <c>try/catch</c> wie LinterEngine-Malfunctions ab und meldet
/// <see cref="SafeguardScoreResult.IsMalfunction"/>=true — lieber ehrlich "konnte nicht zuverlaessig
/// scoren" melden als einen nicht-deterministischen Score auf einer unvollstaendigen, zufaellig
/// zusammengesetzten Teilmenge der Klassen zu liefern (Determinismus-Vertrag der Klasse).
/// </summary>
internal sealed class SafeguardCompilationException : Exception
{
    public SafeguardCompilationException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
