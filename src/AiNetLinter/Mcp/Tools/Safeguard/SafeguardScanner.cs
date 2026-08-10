#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Metrics;
using AiNetLinter.Models;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Safeguard;

/// <summary>
/// Reine Score-Berechnungs- und Remediation-Logik fuer die Safeguard-Auswertung (EPIC-01) — in eine
/// eigene Datei ausgelagert, damit der spaetere Tool-Wrapper (EPIC-02) nur noch ein duenner Dispatch
/// bleibt. Delegiert die Lint-Arbeit an <see cref="LinterEngine.RunAsync(Solution, bool, int, CancellationToken)"/>
/// (mit <c>noCache: true</c> analog zu <see cref="GetViolationsScanner"/>), sammelt zusaetzlich
/// Klassenmetriken (Cognitive Complexity, AI-Context-Footprint, Sealed-Quote) ueber einen direkten
/// Roslyn-Walk und aggregiert alles zu einem deterministischen 0-10-Score.
///
/// Determinismus: keine Zeit-/Zufalls-/Externer-IO-Operatoren, Sortierung der Top-Violations nach
/// (Severity, FilePath, LineNumber, RuleName), symmetrische Rundung der Komponenten-Scores. Ein
/// defensiver <c>try/catch</c> faengt LinterEngine-Malfunctions ab und liefert
/// <see cref="SafeguardScoreResult.IsMalfunction"/>=true mit <see cref="SafeguardScoreResult.Context"/>
/// (Pattern analog <see cref="GetViolationsScanner"/>). Score-Gewichte sind benannte Konstanten, damit
/// Tests und Dokumentation dieselben Werte sehen — Anpassung nur bei offensichtlich unplausiblen
/// Test-Scores (siehe Tech-Stack-Notiz / "Bekannte Ausnahmen" des Step-Plans).
/// </summary>
internal static class SafeguardScanner
{
    /// <summary>Standard-Mindest-Score fuer <c>Passed</c> (siehe Konzept §"Muss-Haven").</summary>
    internal const double DefaultMinScoreThreshold = 8.0;

    /// <summary>Standard-Obergrenze fuer Top-Remediation-Eintraege.</summary>
    internal const int DefaultMaxRemediationEntries = 20;

    /// <summary>
    /// Severity-Gewicht fuer eine Lint-Error-Verletzung. Plan-Wert 0.1 wuerde 20 Errors brauchen,
    /// um den Score unter 8.0 zu druecken — fuer den Test "SingleViolation_LowersScoreBelowThreshold"
    /// unplausibel. Auf 1.5 angehoben: 1 Error senkt den Score um 3.0 (Severity 2 * 1.5), liegt
    /// damit klar unter 8.0. Anpassung im Commit-Body dokumentiert.
    /// </summary>
    internal const double ViolationPenaltyUnit = 1.5;

    /// <summary>Severity-Stufe fuer eine Lint-Error-Verletzung (siehe Konzept §"Wie").</summary>
    internal const double ViolationErrorSeverity = 2.0;

    /// <summary>Severity-Stufe fuer eine Lint-Warning (siehe Konzept §"Wie").</summary>
    internal const double ViolationWarningSeverity = 1.0;

    /// <summary>Severity-Stufe fuer einen Lint-Info-Hinweis (siehe Konzept §"Wie").</summary>
    internal const double ViolationInfoSeverity = 0.25;

    /// <summary>
    /// Penalty pro Cognitive-Complexity-Einheit ueber <c>Metrics.MaxCognitiveComplexity</c>, gemittelt
    /// ueber alle Klassen. Plan-Default beibehalten (0.05).
    /// </summary>
    internal const double CcPenaltyPerUnitOverThreshold = 0.05;

    /// <summary>
    /// Penalty pro AI-Context-Footprint-Einheit ueber <c>Metrics.MaxAIContextFootprint</c>, gemittelt
    /// ueber alle Klassen. Plan-Default beibehalten (0.02).
    /// </summary>
    internal const double FootprintPenaltyPerUnitOverLimit = 0.02;

    /// <summary>
    /// Sealed-Bonus pro Viertel ueber 50 % versiegelter Klassen. Bei 75 % sealed = +0.5, bei 100 %
    /// sealed = +1.0. Wird deaktiviert, wenn <c>Global.EnforceSealedClasses</c> false ist (dann 0).
    /// </summary>
    internal const double SealedBonusPerQuarterOverHalf = 0.5;

    /// <summary>
    /// Anzahl Gesamt-Versuche (inkl. Erstversuch) fuer <c>Project.GetCompilationAsync</c> pro Projekt,
    /// bevor ein kompilierbares Projekt (<c>SupportsCompilation == true</c>) als echte Malfunction statt
    /// stillschweigend uebersprungen gilt. Unter paralleler Prozess-Last (z. B. mehrere gleichzeitig
    /// ladende MSBuild-Workspaces) kann <c>GetCompilationAsync</c> transient fehlschlagen — ohne Retry
    /// wuerde das Projekt dann lautlos aus der Klassen-Aggregation fallen, was <c>avgCC</c>/<c>avgFootprint</c>
    /// nicht-deterministisch ueber eine zufaellige Teilmenge der Klassen berechnet.
    /// </summary>
    internal const int CompilationRetryAttempts = 3;

    /// <summary>
    /// Basis-Verzoegerung zwischen Compilation-Retries in Millisekunden, linear skaliert mit der
    /// Versuchsnummer (200ms vor Versuch 2, 400ms vor Versuch 3). Reines Backoff-Timing — beeinflusst
    /// nicht die Score-Formel selbst (die bleibt frei von Zeit-/Zufalls-Operatoren), sondern nur, wie
    /// lange auf eine erfolgreiche Compilation gewartet wird, bevor eine Malfunction gemeldet wird.
    /// </summary>
    internal const int CompilationRetryBaseDelayMs = 200;

    /// <summary>
    /// Berechnet den deterministischen Safeguard-Score fuer die uebergebene Solution.
    /// Defensive <c>try/catch</c> um LinterEngine-Lauf UND Klassen-Aggregation: beides sind echte
    /// Malfunctions, wenn sie fehlschlagen. Projekte mit <c>SupportsCompilation == false</c> (z. B.
    /// echte Nicht-C#-Projekte) werden weiterhin normal uebersprungen — das ist kein Fehler. Ein
    /// Projekt mit <c>SupportsCompilation == true</c>, dessen Compilation auch nach Retries
    /// fehlschlaegt (siehe <see cref="TryGetCompilationAsync"/>), wird dagegen NICHT mehr
    /// stillschweigend uebersprungen, sondern als Malfunction gemeldet — lieber ehrlich "konnte
    /// nicht zuverlaessig scoren" als ein Score aus einer zufaelligen Teilmenge der Klassen.
    /// </summary>
    internal static async Task<SafeguardScoreResult> ComputeScoreAsync(SafeguardScannerParameters p)
    {
        var solution = p.Solution;
        var config = p.Config;
        var console = p.Console;
        var scopeFilter = p.ScopeFilter;
        var ct = p.CancellationToken;

        // LinterEngine verlangt den konkreten Config-Typ (Record-Semantik fuer `with {...}`
        // und durchgereichte Sub-Properties); ILinterEngineConfig wird projektweit ausschliesslich
        // von Config implementiert, der Downcast ist daher nicht spekulativ.
        var concreteConfig = (Config)config;

        IReadOnlyCollection<RuleViolation> violations;
        IReadOnlyList<ScannedClass> classes;
        try
        {
            var engine = new LinterEngine(
                config: concreteConfig,
                rulesJsonContent: null,
                profiler: null,
                console: console,
                args: null);
            violations = await engine.RunAsync(solution, noCache: true, cacheTtlMinutes: 0, ct);

            // Im selben try/catch wie die LinterEngine: ein kompilierbares Projekt, das auch nach
            // Retries (siehe TryGetCompilationAsync) keine Compilation liefert, ist genauso eine
            // echte Malfunction wie eine LinterEngine-Exception — beides wuerde sonst entweder den
            // Score verfaelschen (stilles Ueberspringen) oder inkonsistent behandelt werden.
            classes = await EnumerateConcreteClassesAsync(solution, scopeFilter, concreteConfig, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SafeguardScoreResult(
                Score: null, IsMalfunction: true, Context: ex.Message);
        }

        var score = BuildScoreResult(new BuildScoreResultParameters(
            Violations: violations,
            Classes: classes,
            Config: concreteConfig,
            Threshold: p.MinScoreThreshold,
            MaxRemediationEntries: p.MaxRemediationEntries));
        return new SafeguardScoreResult(Score: score, IsMalfunction: false);
    }

    /// <summary>
    /// Deterministische Score-Berechnung. Getrennt von <see cref="ComputeScoreAsync"/> fuer
    /// isolierte Tests (Klemmverhalten, Threshold-Logik, Sealed-Bonus-Berechnung) ohne
    /// LinterEngine-Setup.
    /// </summary>
    internal static ScoreResult BuildScoreResult(BuildScoreResultParameters p)
    {
        var violationPenalty = ComputeViolationPenalty(p.Violations);
        var ccPenalty = ComputeCcPenalty(p.Classes, p.Config.Metrics.MaxCognitiveComplexity);
        var footprintPenalty = ComputeFootprintPenalty(p.Classes, p.Config.Metrics.MaxAIContextFootprint);
        var sealedBonus = ComputeSealedBonus(p.Classes, p.Config.Global.EnforceSealedClasses);

        var raw = 10.0 - violationPenalty - ccPenalty - footprintPenalty + sealedBonus;
        var score = Math.Clamp(raw, 0.0, 10.0);
        var passed = score >= p.Threshold;

        // Sortierung: Errors zuerst, dann Warnings, dann Info; innerhalb gleicher Severity
        // stabil nach (FilePath, LineNumber, RuleName) — garantiert Byte-fuer-Byte-Identitaet
        // fuer zwei aufeinanderfolgende Aufrufe mit identischem Input (Determinismus-Test).
        var sortedViolations = p.Violations
            .OrderBy(SeverityRank)
            .ThenBy(v => v.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(v => v.LineNumber)
            .ThenBy(v => v.RuleName, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, p.MaxRemediationEntries))
            .Select(v => new ViolationEntry(
                FilePath: v.FilePath,
                LineNumber: v.LineNumber,
                RuleName: v.RuleName,
                Details: v.Details,
                Severity: ResolveSeverity(v),
                Guidance: v.Guidance))
            .ToList();

        var remediation = BuildRemediation(sortedViolations, p.Config);
        var summary = BuildSummary(score, p.Threshold, passed, sortedViolations, p.Classes, p.Config);

        return new ScoreResult(
            Passed: passed,
            Score: score,
            Threshold: p.Threshold,
            Violations: sortedViolations,
            Remediation: remediation,
            Summary: summary);
    }

    /// <summary>
    /// Erzeugt einen strukturierten Remediation-Hint auf Basis der Top-Violations.
    /// Mapping-Tabelle pro <c>RuleName</c>; unbekannte RuleNames erhalten einen generischen
    /// Default-Hinweis. Aufgeteilt in "TopIssue" (die haeufigste Regel unter den Top-Violations)
    /// und "ActionableSteps" (eine Empfehlung pro vorkommender Regel).
    /// </summary>
    internal static RemediationHint BuildRemediation(
        IReadOnlyList<ViolationEntry> topViolations,
        Config config)
    {
        if (topViolations.Count == 0)
        {
            return new RemediationHint(
                TopIssue: "Keine Lint-Verstoesse im Scope.",
                ActionableSteps: Array.Empty<string>(),
                DocumentationHint: "Docs/configuration.md");
        }

        var grouped = topViolations
            .GroupBy(v => v.RuleName, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topIssue = grouped[0].Key;
        var steps = grouped
            .Select(g => ResolveHintForRule(g.Key, config))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();

        return new RemediationHint(
            TopIssue: topIssue,
            ActionableSteps: steps,
            DocumentationHint: "Docs/configuration.md");
    }

    private static double ComputeViolationPenalty(IReadOnlyCollection<RuleViolation> violations)
    {
        if (violations.Count == 0) return 0.0;

        double penalty = 0.0;
        foreach (var v in violations)
        {
            var severity = ResolveSeverity(v);
            if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                penalty += ViolationErrorSeverity * ViolationPenaltyUnit;
            }
            else if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase))
            {
                penalty += ViolationWarningSeverity * ViolationPenaltyUnit;
            }
            else
            {
                penalty += ViolationInfoSeverity * ViolationPenaltyUnit;
            }
        }
        return penalty;
    }

    private static double ComputeCcPenalty(IReadOnlyList<ScannedClass> classes, int ccThreshold)
    {
        if (classes.Count == 0) return 0.0;
        var avgCc = classes.Average(c => (double)c.MaxCognitiveComplexity);
        var overage = Math.Max(0, avgCc - ccThreshold);
        return overage * CcPenaltyPerUnitOverThreshold;
    }

    private static double ComputeFootprintPenalty(IReadOnlyList<ScannedClass> classes, int footprintLimit)
    {
        if (classes.Count == 0) return 0.0;
        var avgFootprint = classes.Average(c => (double)c.AIContextFootprint);
        var overage = Math.Max(0, avgFootprint - footprintLimit);
        return overage * FootprintPenaltyPerUnitOverLimit;
    }

    private static double ComputeSealedBonus(IReadOnlyList<ScannedClass> classes, bool enforceSealed)
    {
        if (!enforceSealed || classes.Count == 0) return 0.0;
        var sealedCount = classes.Count(c => c.IsSealed);
        var sealedQuote = (double)sealedCount / classes.Count;
        var quartersOverHalf = Math.Max(0.0, (sealedQuote - 0.5) / 0.25);
        return quartersOverHalf * SealedBonusPerQuarterOverHalf;
    }

    private static int SeverityRank(RuleViolation v)
    {
        var severity = ResolveSeverity(v);
        if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase)) return 0;
        if (string.Equals(severity, "warning", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private static string ResolveSeverity(RuleViolation v)
    {
        if (!string.IsNullOrEmpty(v.EffectiveSeverity)) return v.EffectiveSeverity;
        return RuleRegistry.TryResolve(v.RuleName)?.Severity ?? "warning";
    }

    private static string BuildSummary(
        double score,
        double threshold,
        bool passed,
        IReadOnlyList<ViolationEntry> topViolations,
        IReadOnlyList<ScannedClass> classes,
        Config config)
    {
        var classCount = classes.Count;
        var violationCount = topViolations.Count;
        var verdict = passed ? "PASS" : "FAIL";
        return $"Safeguard-Score: {score:F2}/10 (Threshold {threshold:F2}) — {verdict}. " +
               $"{violationCount} Top-Verstoesse, {classCount} Klassen analysiert.";
    }

    /// <summary>Lookup-Tabelle pro bekannter Regel-ID; vermeidet <c>MaxSwitchArms</c>-Verstoss
    /// und ermoeglicht das Hinzufuegen weiterer Regeln ohne Steuerungslogik-Aenderung.
    /// Unbekannte RuleNames erhalten einen generischen Default-Hinweis.</summary>
    private static readonly IReadOnlyDictionary<string, string> RuleHints =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [LinterRuleIds.MaxLineCount] =
                "Datei aufteilen — Klassen/Methoden extrahieren, Partial-Klassen pruefen.",
            [LinterRuleIds.MaxMethodLineCount] =
                "Methode aufteilen — Hilfsmethoden extrahieren, Verantwortlichkeit aufspalten.",
            [LinterRuleIds.MaxMethodParameterCount] =
                "Parameter-Record einfuehren — verwandte Argumente in einem Werteobjekt buendeln.",
            [LinterRuleIds.MaxCyclomaticComplexity] =
                "Komplexitaet reduzieren — fruehe Returns, kleinere Methoden, Polymorphie statt Switch.",
            [LinterRuleIds.MaxCognitiveComplexity] =
                "Komplexitaet reduzieren — fruehe Returns, kleinere Methoden, Polymorphie statt Switch.",
            [LinterRuleIds.AIContextFootprint] =
                "Footprint reduzieren — Abhaengigkeiten aufsloesen, kleine Typen favorisieren.",
            [LinterRuleIds.EnforceSealedClasses] =
                "Klasse versiegeln (`sealed`) — Vererbungsabsicht klaeren oder Blatt-Klasse markieren.",
            [LinterRuleIds.MaxConstructorDependencies] =
                "DI-Law-of-Demeter pruefen — Aggregate-Fassade einfuehren, Konstruktor-Injektion reduzieren.",
            [LinterRuleIds.BanAsyncVoid] =
                "Async void durch `async Task` ersetzen — Ausnahmen werden sonst verschluckt.",
            [LinterRuleIds.BanBlockingTaskAccess] =
                "Blocking-Calls (.Wait/.Result/.GetAwaiter().GetResult()) durch `await` ersetzen.",
            [LinterRuleIds.EnforceNoSilentCatch] =
                "Catch-Block sichtbar machen — Log schreiben oder Exception re-throwen.",
        };

    private static string ResolveHintForRule(string ruleName, Config config)
        => RuleHints.TryGetValue(ruleName, out var hint)
            ? hint
            : $"Regel-Verstoss '{ruleName}' pruefen — Details in Docs/configuration.md.";

    private static async Task<IReadOnlyList<ScannedClass>> EnumerateConcreteClassesAsync(
        Solution solution, string? scopeFilter, Config config, CancellationToken ct)
    {
        var collected = new List<ScannedClass>();
        foreach (var project in solution.Projects)
        {
            var compilation = await TryGetCompilationAsync(project, ct);
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                if (!ShouldIncludeDocument(document, project, scopeFilter)) continue;
                collected.AddRange(
                    await CollectClassDeclarationsAsync(document, compilation, config, ct));
            }
        }
        return collected;
    }

    /// <summary>
    /// Liefert die Compilation oder null, wenn das Projekt grundsaetzlich nicht kompilierbar ist
    /// (<c>SupportsCompilation == false</c> — legitimer, erwartbarer Fall, z. B. echtes
    /// Nicht-C#-Projekt). Fuer kompilierbare Projekte wird <see cref="GetCompilationWithRetryAsync"/>
    /// aufgerufen, die transiente Fehlschlaege per Retry abfaengt und einen dauerhaften Fehlschlag
    /// als <see cref="SafeguardCompilationException"/> wirft (von <see cref="ComputeScoreAsync"/>
    /// als Malfunction behandelt).
    /// </summary>
    private static Task<Compilation?> TryGetCompilationAsync(Project project, CancellationToken ct)
    {
        if (!project.SupportsCompilation) return Task.FromResult<Compilation?>(null);
        return GetCompilationWithRetryAsync(project.GetCompilationAsync, project.Name, ct);
    }

    /// <summary>
    /// Retried eine Compilation-Beschaffungsfunktion bis zu <see cref="CompilationRetryAttempts"/> mal
    /// (linearer Backoff via <see cref="CompilationRetryBaseDelayMs"/>), um transiente Fehlschlaege
    /// (z. B. MSBuild-/Ressourcen-Kontention unter paralleler Last) von echten, dauerhaften
    /// Compile-Problemen zu unterscheiden. <paramref name="getCompilation"/> statt direkt
    /// <c>Project.GetCompilationAsync</c>, damit die Retry-/Backoff-Logik isoliert von einer echten
    /// Roslyn-<c>Project</c>-Instanz testbar ist (Pattern konsistent mit <see cref="BuildScoreResult"/>).
    /// Wirft nach dem letzten erfolglosen Versuch eine <see cref="SafeguardCompilationException"/>
    /// statt still <c>null</c> zurueckzugeben — ein kompilierbares Projekt, das dauerhaft nicht
    /// kompiliert, darf nicht lautlos aus der Klassen-Aggregation fallen (siehe Determinismus-Hinweis
    /// an <see cref="TryGetCompilationAsync"/>).
    /// </summary>
    internal static async Task<Compilation?> GetCompilationWithRetryAsync(
        Func<CancellationToken, Task<Compilation?>> getCompilation, string projectName, CancellationToken ct)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= CompilationRetryAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var compilation = await getCompilation(ct);
                if (compilation is not null) return compilation;
                lastError = null;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                lastError = ex;
            }

            if (attempt < CompilationRetryAttempts)
            {
                await Task.Delay(CompilationRetryBaseDelayMs * attempt, ct);
            }
        }

        throw new SafeguardCompilationException(
            $"Compilation fuer Projekt '{projectName}' schlug nach {CompilationRetryAttempts} " +
            "Versuchen fehl (SupportsCompilation=true, aber GetCompilationAsync lieferte wiederholt " +
            "keine Compilation).",
            lastError);
    }

    private static bool ShouldIncludeDocument(Document document, Project project, string? scopeFilter)
    {
        if (string.IsNullOrEmpty(scopeFilter)) return true;
        if (document.FilePath is { } p && p.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase)) return true;
        return project.Name.Contains(scopeFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<ScannedClass>> CollectClassDeclarationsAsync(
        Document document, Compilation compilation, Config config, CancellationToken ct)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync(ct);
        if (syntaxTree is null) return Array.Empty<ScannedClass>();

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var root = await syntaxTree.GetRootAsync(ct);
        var result = new List<ScannedClass>();
        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (TryBuildScannedClass(classDecl, semanticModel, config) is { } scanned) result.Add(scanned);
        }
        return result;
    }

    private static ScannedClass? TryBuildScannedClass(
        ClassDeclarationSyntax classDecl, SemanticModel semanticModel, Config config)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDecl) as INamedTypeSymbol;
        if (symbol is null || symbol.TypeKind != TypeKind.Class || symbol.IsAbstract) return null;
        return BuildScannedClass(symbol, classDecl, config);
    }

    private static ScannedClass BuildScannedClass(
        INamedTypeSymbol symbol, ClassDeclarationSyntax classDecl, Config config)
    {
        var maxCc = 0;
        foreach (var method in classDecl.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            maxCc = Math.Max(maxCc, ComplexityCalculator.GetCognitiveComplexity(method));
        }

        var footprint = AIContextFootprintCalculator.Calculate(
            symbol,
            config.Metrics.FootprintIgnoreNamespacePrefixes,
            config.Metrics.FootprintIgnoreTypeNames);

        return new ScannedClass(
            Name: symbol.Name,
            MaxCognitiveComplexity: maxCc,
            AIContextFootprint: footprint,
            IsSealed: symbol.IsSealed);
    }
}
