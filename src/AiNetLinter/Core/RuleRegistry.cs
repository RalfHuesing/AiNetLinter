// ainetlinter-disable MaxLineCount
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Configuration;

namespace AiNetLinter.Core;

public sealed record RuleMetadata(
    string RuleId,
    string DisplayName,
    Func<LinterConfig, string> GetShortDescription,
    string Warum,
    string[] Alternativen,
    string? SicherheitsHinweis,
    string Intent,
    string Severity,
    string CursorHint,
    bool HasAutoFix,
    Func<LinterConfig, bool> IsEnabled,
    bool IsMetric,
    bool IncludeInCursorRules,
    Func<LinterConfig, int>? GetMetricLimit = null,
    string? ConfigKeyHint = null
);

internal static class RuleRegistry
{
    public static readonly IReadOnlyList<RuleMetadata> All = BuildAll();

    public static RuleMetadata Resolve(string ruleId) =>
        TryResolve(ruleId) ?? throw new KeyNotFoundException($"Unknown rule: {ruleId}");

    public static RuleMetadata? TryResolve(string ruleId) =>
        All.FirstOrDefault(r => r.RuleId.Equals(ruleId, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<RuleMetadata> ByIntent(string intent) =>
        All.Where(r => r.Intent.Equals(intent, StringComparison.OrdinalIgnoreCase));

    // Delegiert an Intent-Gruppen — jede Methode ≤ 60 Zeilen (MaxMethodLineCount).
    private static IReadOnlyList<RuleMetadata> BuildAll() =>
    [
        ..BuildMetricsSizeRules(),       // MaxLineCount, MaxMethodLineCount, MaxMethodParameterCount (3)
        ..BuildMetricsComplexityRules(), // MaxCyclomaticComplexity, MaxCognitiveComplexity, MaxInheritanceDepth, MaxMethodOverloads (4)
        ..BuildMetricsDependencyRules(), // MaxConstructorDependencies, AIContextFootprint (2)
        ..BuildMetricsStructureRules(),  // MaxDirectoryDepth/Children, MaxBoolParameter, MaxPartialClass, MaxPublicMembers, MaxLinqChain (6)
        ..BuildAgentResilientRules(),    // EnforceNoSilentCatch, BanAsyncVoid, BanBlockingTaskAccess (3)
        ..BuildArchitectureRules(),      // EnforceNamespaceDirectoryMapping, DetectAndBanPhantomDependencies (2)
        ..BuildTestCoverageRules(),      // StaticTestSentinel (1)
        ..BuildGeneralRules(),           // Sealed, PascalCase, Naming, Nullable, Allow-Rules, Advanced, UI-Separation (20+)
    ];

    private static RuleMetadata[] BuildMetricsSizeRules() =>
    [
        new(
            RuleId: "MaxLineCount",
            DisplayName: "Maximale Dateilaenge",
            GetShortDescription: c => $"Dateizeilenlimit (max. {c.Metrics.MaxLineCount} Zeilen) ueberschritten.",
            Warum: "Lange Dateien übersteigen das lesbare Kontextfenster. Der Agent sieht nur Ausschnitte und übersieht Invarianten am Datei-Anfang oder -Ende.",
            Alternativen:
            [
                "**Vertical Slices**: Datei nach Verantwortlichkeit aufteilen (Command/Query, Domain/Infrastructure).",
                "**Helper-Klassen auslagern**: Hilfsmethoden und innere Logik in dedizierte Klassen extrahieren.",
                "**Partial** (letztes Mittel): Nur wenn Datei bereits partial ist — `MaxPartialClassFiles`-Grenze beachten."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Datei splitten wenn sie wächst.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxLineCount > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxLineCount
        ),
        new(
            RuleId: "MaxMethodLineCount",
            DisplayName: "Maximale Methodenlaenge",
            GetShortDescription: c => $"Methode hat zu viele Codezeilen (max. {c.Metrics.MaxMethodLineCount} Zeilen).",
            Warum: "Lange Methoden übersteigen den analysierbaren Ausschnitt — Seiteneffekte am Methodenende werden vom Agenten übersehen.",
            Alternativen:
            [
                "**Hilfsmethoden extrahieren**: Abschnitte mit klarem Zweck in private Methoden auslagern (Named-Block-Muster).",
                "**Early Return**: Validierung an den Anfang, Hauptlogik flach halten.",
                "**Command/Query aufteilen**: Wenn die Methode sowohl schreibt als auch liest — zwei separate Methoden."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Eine Aufgabe pro Methode; Rest extrahieren.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxMethodLineCount > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxMethodLineCount
        ),
        new(
            RuleId: "MaxMethodParameterCount",
            DisplayName: "Maximale Parameteranzahl",
            GetShortDescription: c => $"Zu viele Methodenparameter (max. {c.Metrics.MaxMethodParameterCount}).",
            Warum: "Viele Parameter erhöhen die Wahrscheinlichkeit, dass Agenten Argumente in falscher Reihenfolge übergeben oder Pflichtparameter übersehen.",
            Alternativen:
            [
                "**Parameter-Object** (bevorzugt): `record WorkOptions(bool WithLogging, bool ForceRefresh)` — die Call-Site wird selbsterklärend.",
                "**Builder-Pattern**: Für optionale Parameter mit vielen Kombinationen.",
                "**Methode aufteilen**: Wenn Parameter verschiedene Anwendungsfälle kodieren — separierte Methoden mit eindeutigen Namen."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "Ab Überschreitung: `record` als Parameter-Object.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxMethodParameterCount > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxMethodParameterCount
        ),
    ];

    private static RuleMetadata[] BuildMetricsComplexityRules() =>
    [
        new(
            RuleId: "MaxCyclomaticComplexity",
            DisplayName: "Zyklomatische Komplexitaet",
            GetShortDescription: c => $"Zu hohe zyklomatische Komplexitaet (max. {c.Metrics.MaxCyclomaticComplexity}).",
            Warum: "Hohe zyklomatische Komplexität (McCabe) bedeutet viele mögliche Ausführungspfade. Agenten analysieren typischerweise den Happy-Path und übersehen Randfälle.",
            Alternativen:
            [
                "**Methode aufteilen**: Jeden größeren Zweig in eine benannte Hilfsmethode auslagern.",
                "**Dictionary-Dispatch**: `switch`-Kaskaden über Typen/Werte → `Dictionary<Key, Action>` oder Strategy-Pattern.",
                "**Guard Clauses**: Frühe Rückgaben für Fehlerfälle reduzieren Verschachtelung ohne Logikänderung."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Weniger `if`/`switch`/`&&`/`||` pro Methode (McCabe).",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxCyclomaticComplexity > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxCyclomaticComplexity
        ),
        new(
            RuleId: "MaxCognitiveComplexity",
            DisplayName: "Kognitive Komplexitaet",
            GetShortDescription: c => $"Zu hohe kognitive Komplexitaet (max. {c.Metrics.MaxCognitiveComplexity}).",
            Warum: "Kognitive Komplexität (SonarSource) misst die mentale Last beim Lesen — tief verschachtelter Code wird vom Agenten falsch interpretiert.",
            Alternativen:
            [
                "**Early Return**: Validierung zuerst, Hauptpfad danach flach.",
                "**Bedingungen benennen**: Komplexe boolean-Ausdrücke in benannte Methoden/Properties extrahieren (`bool IsEligible => ...`).",
                "**Schleifenrumpf auslagern**: Schleifeninhalt in Hilfsmethode — die Schleife wird zur Iteration, die Methode zur Verarbeitungslogik."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Weniger Verschachtelung; Early Return bevorzugen (kognitiv).",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxCognitiveComplexity > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxCognitiveComplexity
        ),
        new(
            RuleId: "MaxInheritanceDepth",
            DisplayName: "Vererbungstiefe",
            GetShortDescription: c => $"Vererbungstiefe ueberschreitet Limit (max. {c.Metrics.MaxInheritanceDepth}).",
            Warum: "Tiefe Vererbungshierarchien sind für Agenten schwer zu durchdringen — sie sehen nicht alle Basisklassen-Methoden und übersehen Overrides.",
            Alternativen:
            [
                "**Komposition statt Vererbung**: Funktionalität per Aggregation einbinden statt abzuleiten.",
                "**Interface statt Basisklasse**: Vertrag definieren, nicht Implementierung teilen — reduziert Hierarchietiefe."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "Komposition vor Vererbung.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxInheritanceDepth > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxInheritanceDepth
        ),
        new(
            RuleId: "MaxMethodOverloads",
            DisplayName: "Methodenueberladungen",
            GetShortDescription: c => $"Zu viele Methodenueberladungen (max. {c.Metrics.MaxMethodOverloads}).",
            Warum: "Viele Überladungen mit ähnlicher Semantik erschweren dem Agenten die Auswahl der richtigen Signatur — er wählt die falsche und verursacht subtile Fehler.",
            Alternativen:
            [
                "**Optionale Parameter**: `Foo(string x, bool flag = false)` statt zwei Überladungen.",
                "**Explizite Namen**: `FooWithLogging(...)` statt `Foo(...)` + `Foo(..., ILogger)` — semantisch klar, keine Überladungsauflösung nötig."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "Methoden mit eindeutigen Namen bevorzugen.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxMethodOverloads > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxMethodOverloads
        ),
    ];

    private static RuleMetadata[] BuildMetricsDependencyRules() =>
    [
        new(
            RuleId: "MaxConstructorDependencies",
            DisplayName: "Konstruktorabhaengigkeiten",
            GetShortDescription: c => $"Zu viele Konstruktorabhaengigkeiten (max. {c.Metrics.MaxConstructorDependencies}).",
            Warum: "Konstruktoren mit vielen Abhängigkeiten signalisieren zu viele Verantwortlichkeiten — Agenten übergeben falsche Abhängigkeiten oder erzeugen inkorrekte Objekte.",
            Alternativen:
            [
                "**Klasse aufteilen**: Verantwortlichkeiten separieren — jede Teil-Klasse hat weniger Abhängigkeiten.",
                "**Parameter-Aggregation**: Zusammenhängende Abhängigkeiten in ein Konfigurations-Record bündeln."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "Verantwortlichkeit aufteilen bei Überschreitung.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxConstructorDependencies > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxConstructorDependencies
        ),
        new(
            RuleId: "AIContextFootprint",
            DisplayName: "AI Context Footprint",
            GetShortDescription: c => $"AI-Context-Footprint (transitive Codezeilen aller Abhaengigkeiten) ueberschreitet Limit (max. {c.Metrics.MaxAIContextFootprint}).",
            Warum: "Ein zu großer transitiver Code-Footprint bedeutet: der Agent braucht das volle Kontextbudget für eine einzige Klasse. Er sieht nie den vollständigen Kontext und übersieht Invarianten.",
            Alternativen:
            [
                "**Schlankes Interface einführen**: Die größten Abhängigkeiten (s. Details) hinter einem minimalen Interface verstecken — reduziert den transitiven Footprint direkt.",
                "**Klasse aufteilen**: Klasse nach Verantwortlichkeiten teilen und die Teile separat halten — jeder Teil hat kleineren Footprint.",
                "**Abhängigkeit kapseln**: Statt direkter Abhängigkeit eine Facade oder ein Data-Transfer-Objekt übergeben."
            ],
            SicherheitsHinweis: "Interfaces einführen kann Architekturentscheidungen ändern. Nutzer fragen ob Interfaces im Projekt erlaubt sind.",
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "Kopplung reduzieren; eigene Typen-Abhängigkeiten minimieren.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxAIContextFootprint > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxAIContextFootprint,
            ConfigKeyHint: "rules.json → Metrics.MaxAIContextFootprint | Ausnahmen via PathOverrides"
        ),
    ];

    private static RuleMetadata[] BuildMetricsStructureRules() =>
    [
        new(
            RuleId: "MaxDirectoryDepth",
            DisplayName: "Verzeichnistiefe",
            GetShortDescription: c => $"Verzeichnistiefe ueberschreitet Limit (max. {c.Metrics.MaxDirectoryDepth}).",
            Warum: "Tief verschachtelte Verzeichnisse sind für Agenten schwer zu navigieren — File-Listings überschreiten das Kontextfenster.",
            Alternativen:
            [
                "**Flache Struktur bevorzugen**: Features/Domains auf weniger Verzeichnisebenen zusammenfassen.",
                "**Namespace-Segmente zusammenfassen**: Verzeichnisse, die nur einen Unterordner enthalten, mit dem Elternverzeichnis zusammenführen."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "Ordner nicht unnötig tief schachteln.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxDirectoryDepth > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxDirectoryDepth
        ),
        new(
            RuleId: "MaxDirectoryChildren",
            DisplayName: "Verzeichniseintraege",
            GetShortDescription: c => $"Zu viele Kind-Eintraege im Verzeichnis (max. {c.Metrics.MaxDirectoryChildren}).",
            Warum: "Zu viele Dateien in einem Verzeichnis übersteigen die Darstellbarkeit in einem File-Listing — Agenten wählen aus einem unvollständigen Satz und übersehen Dateien.",
            Alternativen:
            [
                "**Unterverzeichnis anlegen**: Verwandte Dateien in einen Unterordner mit sprechendem Namen gruppieren."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "0 = deaktiviert; zu viele Dateien/Unterordner → Unterverzeichnis anlegen.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxDirectoryChildren > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxDirectoryChildren
        ),
        new(
            RuleId: "MaxBoolParameterCount",
            DisplayName: "Bool Parameteranzahl",
            GetShortDescription: c => $"Zu viele bool-Parameter in einer Methode (max. {c.Metrics.MaxBoolParameterCount}).",
            Warum: "`DoWork(true, false)` trägt an der Call-Site keine semantische Information — der Agent ordnet Flags falsch zu und macht Aufruffehler.",
            Alternativen:
            [
                "**Parameter-Object** (bevorzugt): `record WorkOptions(bool WithLogging, bool ForceRefresh)` — die Call-Site wird selbsterklärend.",
                "**Enum**: Bei zwei oder mehr Flags, die verschiedene Modi darstellen, ein Enum statt bool-Kombination.",
                "**Named Arguments** (kurzfristig): `DoWork(withLogging: true, forceRefresh: false)` — kein Strukturumbau nötig, rein syntaktische Verbesserung.",
                "**Separierte Methoden**: Wenn die Pfade fachlich distinct sind — `ProcessSingle()` / `ProcessBatch()` statt `Process(bool isBatch)`."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "0 = deaktiviert; bool-Parameter in Parameter-Object bündeln.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxBoolParameterCount > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxBoolParameterCount
        ),
        new(
            RuleId: "MaxPartialClassFiles",
            DisplayName: "Partial Class Files",
            GetShortDescription: c => $"Typ ist in zu vielen partial-Dateien aufgeteilt (max. {c.Metrics.MaxPartialClassFiles}).",
            Warum: "Agenten sehen nur die aktuell geöffnete Datei. Invarianten, Felder und Methoden aus anderen Partial-Dateien derselben Klasse sind unsichtbar — der Agent erkennt Konflikte nicht und dupliziert Logik.",
            Alternativen:
            [
                "**Eigenständige Klassen extrahieren** (bevorzugt): Logik aus Partials in dedizierte, fachlich benannte Klassen auslagern — z. B. `FooCommandHandler`, `FooQueryHandler`, `FooValidator`.",
                "**Facade-Klasse**: Wenn Partials verschiedene Subsysteme bedienen, eine schlanke Fassadenklasse pro Subsystem einführen.",
                "**Namespace-basierte Trennung**: Verwandte Methoden in eigenständige Klassen im selben Namespace verschieben statt Partials.",
                "**Interface** (nur wenn Projektregeln es erlauben): Wenn Partials verschiedene Rollen abbilden — Interfaces extrahieren und Implementierungen trennen."
            ],
            SicherheitsHinweis: "Partials aufzulösen ist ein tiefgreifender Architektureingriff. **Nutzer ZWINGEND fragen bevor du beginnst** — die gewählte Alternativarchitektur muss dem Projektstil entsprechen.",
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "0 = deaktiviert; Logik in eigenständige Klassen auslagern (z. B. XyzChecker).",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxPartialClassFiles > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxPartialClassFiles,
            ConfigKeyHint: "rules.json → Metrics.MaxPartialClassFiles | Ausnahmen via PathOverrides"
        ),
        new(
            RuleId: "MaxPublicMembersPerType",
            DisplayName: "Public Members Pro Typ",
            GetShortDescription: c => $"Zu viele oeffentliche Member in einem Typ (max. {c.Metrics.MaxPublicMembersPerType}).",
            Warum: "Breite API-Fläche erhöht die Wahrscheinlichkeit, dass Agenten existierende Methoden übersehen und duplizieren. Der Agent wählt aus dem sichtbaren Ausschnitt, nicht der vollständigen Klasse.",
            Alternativen:
            [
                "**Klasse nach Verantwortlichkeit aufteilen**: z. B. Command/Query, Read/Write, Domain/Infrastructure als separate Klassen.",
                "**Facade-Prinzip**: Hilfsmethoden auf `private` oder `internal` reduzieren — nur die echte öffentliche API exponieren.",
                "**Extension-Methoden auslagern**: Optional-/Hilfsmethoden als `*Extensions`-Klasse im selben Namespace (Suffix `Extensions` ist per Default exempt).",
                "**State-Objekt**: Zusammengehörige Properties in ein dediziertes `record`-Zustandsobjekt auslagern."
            ],
            SicherheitsHinweis: "Oft ein SRP-Signal. Vor größerem Refactoring Nutzer fragen und Architektur-Constraints (`.cursor/rules`, `CLAUDE.md`) lesen.",
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "0 = deaktiviert; Typ aufteilen oder Member kapseln.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxPublicMembersPerType > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxPublicMembersPerType,
            ConfigKeyHint: "rules.json → Metrics.MaxPublicMembersPerType | Ausnahmen via PathOverrides"
        ),
        new(
            RuleId: LinterRuleIds.MaxLinqChainLength,
            DisplayName: "Maximale LINQ-Kettenlaenge",
            GetShortDescription: c => $"LINQ-Kette ueberschreitet das Limit (max. {c.Metrics.MaxLinqChainLength} Methoden).",
            Warum: "Lange LINQ-Ketten erzeugen sequenzielle kognitive Last, die weder zyklomatische noch kognitive " +
                   "Komplexitaet messen. Ein LLM-Agent der eine 8-gliedrige Kette erweitern soll, macht haeufig " +
                   "Typfehler an der Einschnittstelle. (Evidenz: moderat — 0 = deaktiviert per Default.)",
            Alternativen:
            [
                "**Kette aufteilen**: Zwischenergebnis in benannte Variable extrahieren ('var activeOrders = orders.Where(...);').",
                "**Private Hilfsmethoden**: Teilketten in benannte Methoden auslagern ('FilterActiveOrders()', 'RankByRevenue()').",
                "**Query-Syntax**: Fuer mehrstufige Abfragen kann 'from x in ... where ... select ...' lesbarer sein.",
                "**Suppression**: '// ainetlinter-disable MaxLinqChainLength' fuer legitime komplexe Datentransformationen."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "warning",
            CursorHint: "0 = deaktiviert; lange LINQ-Ketten in Teilschritte aufteilen.",
            HasAutoFix: false,
            IsEnabled: c => c.Metrics.MaxLinqChainLength > 0,
            IsMetric: true,
            IncludeInCursorRules: true,
            GetMetricLimit: c => c.Metrics.MaxLinqChainLength,
            ConfigKeyHint: "rules.json → Metrics.MaxLinqChainLength | Metrics.LinqMethodNames"
        ),
    ];

    private static RuleMetadata[] BuildAgentResilientRules() =>
    [
        new(
            RuleId: "EnforceNoSilentCatch",
            DisplayName: "Keine leeren catch Bloecke",
            GetShortDescription: c => "Keine stummen catch-Bloecke.",
            Warum: "Leere catch-Blöcke verbergen Fehler. Agenten können nicht erkennen ob ein Fehler normal oder kritisch ist — führt zu Silent Data Loss.",
            Alternativen:
            [
                "**Logging + Rethrow**: `catch (Exception ex) { _logger.LogError(ex, \"...\"); throw; }`",
                "**Gezieltes Abfangen**: Nur die erwartete Exception-Type abfangen und spezifisch behandeln oder in ein Ergebnisobjekt umwandeln.",
                "**Exception-Variable `ignored`**: `catch (SomeException ignored)` — der Linter erkennt den Variablennamen als explizit gewolltes Ignorieren.",
                "**Suppression** (letztes Mittel, nur nach Freigabe): `// ainetlinter-disable EnforceNoSilentCatch` an der catch-Zeile."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-resilience",
            Severity: "error",
            CursorHint: "`catch` immer mit Log + sichtbarem Fehler oder `throw;` — nie leer.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceNoSilentCatch,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: LinterRuleIds.BanAsyncVoid,
            DisplayName: "Kein async void",
            GetShortDescription: c => "'async void' ist verboten (ausser Event-Handler).",
            Warum: "'async void' schleudert Exceptions in den SynchronizationContext — sie werden von keinem aufrufenden 'try/catch' gefangen. " +
                   "Agenten produzieren dieses Muster systematisch wenn sie void-Methoden zu async umwandeln.",
            Alternativen:
            [
                "**'async Task' statt 'async void'**: Minimale Aenderung — Rückgabetyp ersetzen, Aufrufer await ergaenzen.",
                "**Event-Handler-Ausnahme**: Signaturen mit '(object sender, EventArgs e)' bleiben erlaubt.",
                "**Suppression** (letztes Mittel): '// ainetlinter-disable BanAsyncVoid' fuer Legacy-Code."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-resilience",
            Severity: "error",
            CursorHint: "'async void' verboten; Ausnahme: Event-Handler mit '(object sender, EventArgs e)'.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.BanAsyncVoid,
            IsMetric: false,
            IncludeInCursorRules: true,
            ConfigKeyHint: "rules.json → Global.BanAsyncVoid | Global.AsyncVoidAllowEventHandlers"
        ),
        new(
            RuleId: LinterRuleIds.BanBlockingTaskAccess,
            DisplayName: "Kein blockierender Task-Zugriff",
            GetShortDescription: c => "'.Wait()', '.Result' und '.GetAwaiter().GetResult()' auf Tasks sind verboten.",
            Warum: "Blockierende Task-Zugriffe blockieren ThreadPool-Threads und sind in SynchronizationContext-Umgebungen " +
                   "(ASP.NET Classic, WPF) deadlock-anfaellig. Agenten produzieren dieses Muster systematisch " +
                   "wenn sie synchrone Methoden mit async-APIs verbinden.",
            Alternativen:
            [
                "**'await task'**: Methode zu 'async Task' umwandeln und await verwenden — loest das Problem vollstaendig.",
                "**Aufrufkette async machen**: Von der blockierenden Methode nach oben migrieren bis alle Aufrufer async sind.",
                "**'BanBlockingTaskAccessAllowInMain: true'**: Fuer Programm-Einstiegspunkte die kein async Main haben.",
                "**Suppression** (letztes Mittel): '// ainetlinter-disable BanBlockingTaskAccess' fuer unvermeidliche Stellen."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-resilience",
            Severity: "error",
            CursorHint: "'.Wait()'/'.Result'/'.GetAwaiter().GetResult()' verboten; verwende 'await'.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.BanBlockingTaskAccess,
            IsMetric: false,
            IncludeInCursorRules: true,
            ConfigKeyHint: "rules.json → Global.BanBlockingTaskAccess | BanBlockingTaskAccessAllowInMain | BanBlockingTaskAccessAllowInTests"
        ),
    ];

    private static RuleMetadata[] BuildArchitectureRules() =>
    [
        new(
            RuleId: "ForbiddenNamespaceDependency",
            DisplayName: "Namespace Abhaengigkeiten",
            GetShortDescription: c => "Unerlaubte Namespace-Abhaengigkeit gemaess Architektur-Regeln.",
            Warum: "Architektur-Slices sollen entkoppelt sein. Direkte Abhängigkeiten zwischen verbotenen Namespaces erzeugen Zyklen, die Agenten nicht erkennen und weiterverstärken.",
            Alternativen:
            [
                "**Interface/Abstraktion**: Die Abhängigkeit hinter einem Interface im erlaubten Namespace verstecken.",
                "**Events/Messages**: Kommunikation über Events statt direkten Aufruf (Inversion of Control).",
                "**Shared-Kernel**: Gemeinsam genutzte Typen in einen neutral erlaubten Namespace verschieben."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Unerlaubte Namespace-Abhaengigkeit gemaess Architektur-Regeln.",
            HasAutoFix: false,
            IsEnabled: c => c.ForbiddenNamespaceDependencies.Any(),
            IsMetric: false,
            IncludeInCursorRules: false
        ),
        new(
            RuleId: "EnforceNamespaceDirectoryMapping",
            DisplayName: "Namespace Pfadmapping",
            GetShortDescription: c => "Namespace entspricht nicht dem Verzeichnis-Pfad.",
            Warum: "Wenn Namespace und Dateipfad nicht übereinstimmen, können Agenten Dateien nicht über den Namespace lokalisieren und erzeugen fehlerhafte `using`-Direktiven.",
            Alternativen:
            [
                "**Namespace anpassen**: Namespace an den Verzeichnispfad angleichen.",
                "**Datei verschieben**: Datei in das zum Namespace passende Verzeichnis verschieben."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Namespace muss Verzeichnispfad entsprechen (Modus: `rules.json`).",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceNamespaceDirectoryMapping,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "DetectAndBanPhantomDependencies",
            DisplayName: "Keine Phantom Dependencies",
            GetShortDescription: c => "Phantom-Abhaengigkeiten (unaufloesbare Namespaces oder Reflection-Laden) verboten.",
            Warum: "Nicht-auflösbare Namespaces und Reflection-Lade-APIs sind die häufigste Halluzinations-Quelle in KI-generiertem Code — der Compiler sieht sie nicht, das Programm scheitert erst zur Laufzeit.",
            Alternativen:
            [
                "**Korrekte `using`-Direktiven**: Nur explizit referenzierte NuGet-Pakete und Projekt-Namespaces verwenden.",
                "**NuGet-Referenzen prüfen**: Ob das benötigte Paket in der `.csproj` steht.",
                "**Reflection-Load ersetzen**: `Assembly.LoadFrom` / `Activator.CreateInstance` durch statische Registrierung."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Keine unauflösbaren `using`; kein `Type.GetType`/`Activator.CreateInstance` für App-Typen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.DetectAndBanPhantomDependencies,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildTestCoverageRules() =>
    [
        new(
            RuleId: "StaticTestSentinel",
            DisplayName: "Testabdeckung Sentinel",
            GetShortDescription: c => "Fehlende Testabdeckung (Unit-Test) fuer komplexe Klasse.",
            Warum: "Komplexe Typen ohne Testabdeckung sind für Agenten eine Black Box — sie können keine Regression bei Änderungen erkennen.",
            Alternativen:
            [
                "**Testklasse anlegen**: `{Name}Tests.cs` im entsprechenden Test-Projekt.",
                "**`typeof(T)`-Referenz**: `typeof(FooClass)` in einer Testklasse — `EnableTestSentinel` erkennt das als Sentinel.",
                "**`// @covers T`-Kommentar**: In einer bestehenden Testklasse ergänzen."
            ],
            SicherheitsHinweis: null,
            Intent: "test-coverage",
            Severity: "warning",
            CursorHint: "Für komplexe Typen: Testklasse, `typeof(T)` oder `// @covers T`.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnableTestSentinel,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    // Delegiert an Unter-Gruppen — zu viele Regeln für eine einzelne Methode.
    private static RuleMetadata[] BuildGeneralRules() =>
    [
        ..BuildGeneralCoreRules(),
        ..BuildGeneralAllowRules(),
        ..BuildGeneralAdvancedRules(),
        ..BuildUiSeparationRules(),
    ];

    private static RuleMetadata[] BuildGeneralCoreRules() =>
    [
        new(
            RuleId: "EnforceSealedClasses",
            DisplayName: "Sealed Classes Pflicht",
            GetShortDescription: c => "Konkrete Klassen muessen 'sealed' sein (oder 'sealed partial').",
            Warum: "Nicht-`sealed` Klassen signalisieren irrtümlich, dass Vererbung gewollt ist. Agenten leiten dann von Klassen ab, die nie dafür gedacht waren — erzeugt fragile Hierarchien.",
            Alternativen:
            [
                "**`sealed` ergänzen**: `public sealed class Foo` — auto-fix via `--fix` verfügbar.",
                "**Bei partial-Klassen**: `sealed partial class Foo` (alle Partial-Dateien erhalten `sealed`).",
                "**Wenn Vererbung gewollt**: Suffix prüfen (`Base`, `Foundation`, `Host` sind exempt) oder Klasse explizit `abstract` make."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`sealed` für konkrete Klassen; Ausnahmen: Suffixe in `rules.json → SealedClassExemptSuffixes`.",
            HasAutoFix: true,
            IsEnabled: c => c.Global.EnforceSealedClasses,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "BanPublicNestedTypes",
            DisplayName: "Keine Nested Typen",
            GetShortDescription: c => "Verbot oeffentlicher nested Typen.",
            Warum: "Nested Typen (auch `internal`) erscheinen nicht in Dateilisten und Grep-Ergebnissen auf Namespace-Ebene. Agenten lokalisieren sie über File-Lookup nicht, halluzinieren FQNs (`Outer.Inner` statt `Inner`) und duplizieren sie unbemerkt.",
            Alternativen:
            [
                "**Top-Level-Typ extrahieren** (bevorzugt): Typ in eigene `.cs`-Datei im selben Ordner verschieben. Bei Namenskonflikt Hostnamen als Prefix: `DataTableColumnDefinition` statt `ColumnDefinition`.",
                "**Privat machen**: Wenn der Typ ausschließlich klassenintern genutzt wird — auf `private nested` reduzieren (nur wenn `BanPublicNestedTypesAllowPrivate: true`).",
                "**In Host-Datei als Top-Level verschieben**: Als Top-Level-Typ direkt über oder unter der Host-Klasse in derselben Datei — nur für sehr kleine Hilfstypen sinnvoll."
            ],
            SicherheitsHinweis: "Bei > 5 betroffenen Typen: Nutzer fragen. Externe Referenzen auf `HostClass.NestedType` sind Breaking Changes — Scope prüfen.",
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Verbot oeffentlicher nested Typen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.BanPublicNestedTypes,
            IsMetric: false,
            IncludeInCursorRules: false,
            ConfigKeyHint: "rules.json → Global.NestedTypeExemptSuffixes"
        ),
        new(
            RuleId: "EnforcePascalCase",
            DisplayName: "PascalCase Bezeichner",
            GetShortDescription: c => "PascalCase fuer oeffentliche Bezeichner erforderlich.",
            Warum: "Agenten orientieren sich an Namenskonventionen, um Typen und Methoden zu finden. Inkonsistente Schreibweise führt zu 'Type not found'-Fehlern und Halluzinationen.",
            Alternativen:
            [
                "**Umbenennen**: `public string myField` → `public string MyField` — auto-fix via `--fix` verfügbar."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Öffentliche Typen/Methoden/Properties: PascalCase.",
            HasAutoFix: true,
            IsEnabled: c => c.Global.EnforcePascalCase,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceSemanticNaming",
            DisplayName: "Semantische Namensgebung",
            GetShortDescription: c => "Generische Namen (data, temp, obj) sind in oeffentlichen Signaturen verboten.",
            Warum: "Generische Namen (`data`, `temp`, `obj`) in öffentlichen Signaturen geben keine Information über den Zweck — Agenten wählen falsche Variablen.",
            Alternativen:
            [
                "**Sprechende Namen**: `data` → `userRecord`, `temp` → `formattedLabel`, `obj` → `siteConfiguration`.",
                "**Typ als Namenspräfix**: Wenn kein fachlicher Name greifbar — zumindest den Typ kodieren (`configEntry` statt `obj`)."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Keine `data`/`temp`/`obj` in öffentlichen Signaturen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceSemanticNaming,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceNullableEnable",
            DisplayName: "Nullable Enable Pflicht",
            GetShortDescription: c => "#nullable enable fehlt am Dateianfang.",
            Warum: "Ohne `#nullable enable` kann der Agent nicht zwischen null-sicheren und unsicheren Pfaden unterscheiden — erzeugt potenzielle NullReferenceExceptions.",
            Alternativen:
            [
                "**Dateikopf ergänzen**: `#nullable enable` als erste Zeile der `.cs`-Datei — auto-fix via `--fix` verfügbar."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`#nullable enable` am Dateianfang jeder `.cs`-Datei.",
            HasAutoFix: true,
            IsEnabled: c => c.Global.EnforceNullableEnable,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildGeneralAllowRules() =>
    [
        new(
            RuleId: "AllowDynamic",
            DisplayName: "Verbot Dynamic Typen",
            GetShortDescription: c => "'dynamic' ist verboten. Nutze statische Typen.",
            Warum: "`dynamic` deaktiviert statische Typanalyse. Agenten können keine korrekten Typ-Inferenzen machen und erzeugen Code, der erst zur Laufzeit scheitert.",
            Alternativen:
            [
                "**Interface oder abstrakte Klasse**: Gemeinsame Schnittstelle statt `dynamic`.",
                "**Generics**: Typparameter `T` statt `object`/`dynamic` für typsichere Flexibilität.",
                "**`Dictionary<string, object>`**: Für Schlüssel-Wert-Szenarien mit expliziten Casts."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`dynamic` ist verboten.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowDynamic,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowOutParameters",
            DisplayName: "Verbot Out Parameter",
            GetShortDescription: c => "'out'-Parameter sind verboten. Benutze Tuples oder Records.",
            Warum: "`out`-Parameter erzeugen Seiteneffekte, die nicht aus der Methodensignatur erkennbar sind — Agenten übersehen Out-Parameter oder setzen sie falsch.",
            Alternativen:
            [
                "**Tuple-Rückgabe**: `(bool Success, int Value) TryGet(string key)`.",
                "**Record**: `TryGetResult TryGet(string key)` mit `record TryGetResult(bool Success, int Value)`.",
                "**Try-Pattern** (erlaubt in `Try*`-Methoden): `bool TryGet(out int value)` — `AllowTryPatternOutParameters` greift."
            ],
            SicherheitsHinweis: null,
            Intent: "csharp-idiom",
            Severity: "warning",
            CursorHint: "`out` Parameter verboten; Ausnahme: `Try*`-Methoden.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowOutParameters,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowUnsealedPartialClasses",
            DisplayName: "Allow Unsealed Partial Classes",
            GetShortDescription: c => "Unversiegelte partial Klassen erlaubt (z. B. Blazor-Komponenten).",
            Warum: "Blazor-Komponenten und generierte Code-Klassen benoetigen unversiegelte partial Klassen. Diese Option deaktiviert die EnforceSealedClasses-Pruefung fuer solche Typen.",
            Alternativen: ["**Standard**: `AllowUnsealedPartialClasses: false` — alle partial Klassen muessen sealed sein."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Unversiegelte `partial` Klassen erlaubt (z. B. Blazor-Komponenten).",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowUnsealedPartialClasses,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowTryPatternOutParameters",
            DisplayName: "Allow Try Pattern Out Parameters",
            GetShortDescription: c => "out-Parameter in Try*-Methoden erlaubt.",
            Warum: "Das Try*-Pattern (TryParse, TryGet) benoetigt out-Parameter. Diese Option deaktiviert die Pruefung auf out-Parameter fuer Methoden mit 'Try'-Praefix.",
            Alternativen: ["**Ohne out**: Gib Tuple<bool, T> oder Result<T> zurueck statt out-Parameter."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`out` in `Try*`-Methoden erlaubt.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowTryPatternOutParameters,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowCancellationShutdownCatch",
            DisplayName: "Allow Cancellation Shutdown Catch",
            GetShortDescription: c => "OperationCanceledException beim Shutdown abfangen erlaubt.",
            Warum: "Graceful-Shutdown-Logik muss OperationCanceledException abfangen koennen. Diese Option erlaubt den Catch in Shutdown-Methoden, die sonst durch die Pruefung blockiert waeren.",
            Alternativen: ["**Ohne Catch**: CancellationToken weiterpropagieren und kein Catch auf oberster Ebene."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "`OperationCanceledException` beim Shutdown abfangen erlaubt.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowCancellationShutdownCatch,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "AllowedEmptyReads",
            DisplayName: "Allowed Empty Reads",
            GetShortDescription: c => "Leseoperationen ohne unmittelbaren Guard verboten.",
            Warum: "Leseoperationen ohne unmittelbaren Null-Guard sind eine haeufige Fehlerquelle. Diese Option erlaubt spezifische Ausnahmen, z. B. fuer Properties mit garantiertem Wert nach Initialisierung.",
            Alternativen: ["**Mit Guard**: Null-Check direkt nach der Leseoperation."],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Leseoperationen immer mit unmittelbarem Guard versehen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.AllowedEmptyReads,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildGeneralAdvancedRules() =>
    [
        new(
            RuleId: "EnforceXmlDocumentation",
            DisplayName: "XML API Dokumentation",
            GetShortDescription: c => "Fehlende XML-Dokumentation fuer oeffentliche Schnittstellen.",
            Warum: "Fehlende XML-Dokumentation für öffentliche APIs zwingt Agenten, den Zweck aus dem Code zu inferieren — erhöht Fehlerrate bei komplexen Parametern.",
            Alternativen:
            [
                "**Summary ergänzen**: `/// <summary>Beschreibung des Zwecks.</summary>` vor dem public Member.",
                "**Parameter dokumentieren**: `/// <param name=\"x\">Bedeutung von x.</param>` für nicht-selbsterklärende Parameter."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "warning",
            CursorHint: "XML-Dokumentation für öffentliche APIs.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceXmlDocumentation,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceExplicitStateImmutability",
            DisplayName: "Immutability Pflicht",
            GetShortDescription: c => "Felder und Properties muessen readonly oder init-only sein.",
            Warum: "Veränderlicher Zustand ist für Agenten schwer zu verfolgen — sie übersehen, wo Zustand geändert wird, und erzeugen Race Conditions.",
            Alternativen:
            [
                "**`readonly` Felder**: Initialisierung im Konstruktor, keine spätere Mutation.",
                "**`init`-only Properties**: `public string Name { get; init; }` — nur bei Objekterstellung setzbar.",
                "**`record`-Typ**: Strukturell unveränderlich — Mutation via `with`-Ausdruck (Copy-and-Modify)."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-resilience",
            Severity: "error",
            CursorHint: "Felder und Properties `readonly`/`init`-only.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceExplicitStateImmutability,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceMinimalApiAsParameters",
            DisplayName: "Minimal API Bindung",
            GetShortDescription: c => "Minimal-API: Parameter muessen per [AsParameters] gebunden werden.",
            Warum: "Minimal-API-Endpunkte ohne Parameter-Binding-Records verleiten Agenten zu inkonsistentem Parameterhandling.",
            Alternativen:
            [
                "**Parameter-Record**: Eingabeparameter in einen `record` zusammenfassen — `record CreateFooRequest(string Name, int Count)`."
            ],
            SicherheitsHinweis: null,
            Intent: "aspnet-binding",
            Severity: "error",
            CursorHint: "Minimal-API: >4 Parameter → `[AsParameters]` + `record`.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceMinimalApiAsParameters,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceResultPatternOverExceptions",
            DisplayName: "Result Pattern Pflicht",
            GetShortDescription: c => "Fachlicher Kontrollfluss muss Result-Pattern statt Exceptions (throw) nutzen.",
            Warum: "Exceptions für Business-Logik sind für Agenten schwer zu verfolgen — sie übersehen `throw`-Pfade und schreiben keine Tests für Fehlerfälle.",
            Alternativen:
            [
                "**Result-Typ**: Rückgabetyp `Result<T, Error>` oder `OneOf<Success, Error>` (Bibliothek je nach Projekt wählen).",
                "**Discriminated Union**: Erfolg/Fehler explizit als Typ modellieren — keine Exception für erwartete Fehlerfälle."
            ],
            SicherheitsHinweis: null,
            Intent: "control-flow",
            Severity: "error",
            CursorHint: "`Result<T>` für Domänenfehler; `throw` nur für Infrastruktur-Fehler.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceResultPatternOverExceptions,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "EnforceValueObjectContracts",
            DisplayName: "ValueObject Immutability",
            GetShortDescription: c => "ValueObject-Klassen muessen record oder readonly struct sein.",
            Warum: "Klassen mit `*ValueObject`-Suffix sollten strukturell unveränderlich sein. Agenten fügen sonst mutierbare Properties hinzu und brechen das Invariant.",
            Alternativen:
            [
                "**`record`**: `public sealed record PriceValueObject(decimal Amount, string Currency)` — primäres Konstrukt für Value Objects.",
                "**`readonly struct`**: Für kleine, häufig kopierte Value Objects ohne Vererbungsbedarf."
            ],
            SicherheitsHinweis: null,
            Intent: "general",
            Severity: "error",
            CursorHint: "Klassen mit `*ValueObject`-Suffix: `record` oder `readonly struct`.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.EnforceValueObjectContracts,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
        new(
            RuleId: "PreventContextDependentOverloads",
            DisplayName: "Keine primitives-only Ueberladungen",
            GetShortDescription: c => "Keine Überladungen mit identischer Parameteranzahl für primitive Typen.",
            Warum: "Überladungen mit identischer Parameteranzahl, die sich nur durch primitive Typen unterscheiden, sind für Agenten nicht disambiguierbar — falscher Aufruf bleibt kompilierbar.",
            Alternativen:
            [
                "**Explizite Methodennamen**: `ParseFromString(string)` + `ParseFromInt(int)` statt `Parse(string)` + `Parse(int)`.",
                "**Named-Constructor-Pattern**: Statische Factory-Methoden mit klaren Namen statt Überladungen."
            ],
            SicherheitsHinweis: null,
            Intent: "agent-context",
            Severity: "error",
            CursorHint: "Keine Überladungen mit identischer Parameteranzahl für primitive Typen.",
            HasAutoFix: false,
            IsEnabled: c => c.Global.PreventContextDependentOverloads,
            IsMetric: false,
            IncludeInCursorRules: true
        ),
    ];

    private static RuleMetadata[] BuildUiSeparationRules() =>
    [
        new(
            RuleId: "BlazorRequireCodeBehind",
            DisplayName: "Blazor Code Behind",
            GetShortDescription: c => "Blazor-Komponenten muessen Code-Behind nutzen.",
            Warum: "Logik in `@code { }` Blöcken ist für Agenten ohne Razor-Unterstützung unsichtbar — sie modifizieren nur die `.razor.cs`-Datei und übersehen den `@code`-Block.",
            Alternativen:
            [
                "**Code-Behind-Datei anlegen**: `@code { ... }` → separate `.razor.cs` partial class verschieben.",
                "**Suppression** (in `.razor`-Dateien): `@* ainetlinter-disable BlazorRequireCodeBehind *@`"
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "Blazor-Komponenten muessen Code-Behind nutzen.",
            HasAutoFix: false,
            IsEnabled: c => c.UiSeparation.BlazorRequireCodeBehind,
            IsMetric: false,
            IncludeInCursorRules: false
        ),
        new(
            RuleId: "BlazorRequireCssIsolation",
            DisplayName: "Blazor CSS Isolation",
            GetShortDescription: c => "Blazor-Komponenten muessen CSS-Isolation nutzen.",
            Warum: "Inline `<style>`-Tags in `.razor`-Dateien werden vom Agenten oft übersehen und nicht in CSS-Isolation-Dateien migriert — erzeugt Style-Konflikte.",
            Alternativen:
            [
                "**CSS-Isolation-Datei anlegen**: `<style>` → `.razor.css` Datei im gleichen Ordner.",
                "**Suppression** (wenn keine Styles nötig): `@* ainetlinter-disable BlazorRequireCssIsolation *@`"
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "warning",
            CursorHint: "Blazor-Komponenten muessen CSS-Isolation nutzen.",
            HasAutoFix: false,
            IsEnabled: c => c.UiSeparation.BlazorRequireCssIsolation,
            IsMetric: false,
            IncludeInCursorRules: false
        ),
        new(
            RuleId: "WpfRequireMinimalCodeBehind",
            DisplayName: "WPF Minimal Code Behind",
            GetShortDescription: c => "WPF-Code-Behind darf keine Business-Logik enthalten.",
            Warum: "Umfangreiches Code-Behind in WPF verletzt MVVM. Agenten fügen Logik ins Code-Behind ein, wenn sie den ViewModel nicht finden — kumuliert technische Schulden.",
            Alternativen:
            [
                "**ViewModel**: Logik, Commands und Properties in den zugehörigen ViewModel verschieben.",
                "**EventToCommand-Binding**: Event-Handler durch Command-Bindings ersetzen (MVVM-Infrastruktur des Projekts nutzen)."
            ],
            SicherheitsHinweis: null,
            Intent: "architecture",
            Severity: "error",
            CursorHint: "WPF-Code-Behind darf keine Business-Logik enthalten.",
            HasAutoFix: false,
            IsEnabled: c => c.UiSeparation.WpfRequireMinimalCodeBehind,
            IsMetric: false,
            IncludeInCursorRules: false
        ),
    ];
}
