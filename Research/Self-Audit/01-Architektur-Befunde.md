# 01 — Architektur-Befunde

> Detaillierte Analyse der architektonischen Schwachstellen mit Code-Referenzen.  
> Empfehlungen siehe [`03-Architektur-Refactoring-Vorschlaege.md`](03-Architektur-Refactoring-Vorschlaege.md).

---

## A1 — `LinterAnalyzer` ruft zu viele Checker zentral auf

**Datei:** `src/AiNetLinter/Core/LinterAnalyzer.cs` (271 LOC)

### Befund

`LinterAnalyzer` ist ein einzelner `CSharpSyntaxWalker`, der **14 Checker direkt aufruft** über starre `VisitXxx`-Methoden. Jede neue Regel erfordert eine Änderung der Klasse selbst:

```csharp
// LinterAnalyzer.cs, Zeilen 79–94
public override void VisitClassDeclaration(ClassDeclarationSyntax node)
{
    if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && ArchitectureChecker.IsGeneratedCode(node, _ctx))
        return;
    NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Klasse", _ctx);
    NamingChecker.CheckPascalCase(node.Identifier, "Klasse", _ctx);
    ArchitectureChecker.CheckSealedClass(node, _ctx);
    ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: false, _ctx);
    ScopeChecker.CheckMethodOverloads(node, _ctx);
    StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
    ImmutabilityChecker.CheckClass(node, _ctx);
    WpfSeparationChecker.Check(node, _ctx);
    NestedTypesChecker.Check(node, _ctx);
    PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
    ArchitectureChecker.CollectClassInfo(node, _ctx);
    base.VisitClassDeclaration(node);
}
```

**Konsequenzen:**

- **Schwer testbar isoliert** — ein Test für `CheckSealedClass` muss immer den ganzen `LinterAnalyzer` hochfahren
- **`ArchitectureChecker` sammelt 18 Methoden** (303 LOC), die nichts miteinander zu tun haben
- **Bug:** Bei Records fehlt `CollectClassInfo` → Records erscheinen nicht in Playbook/Footprint-Statistiken (→ F11)
- **Hohe kognitive Last** — 271 LOC für eine Klasse mit 12+ Verantwortlichkeiten

### Lösungsansatz

Checker-God-Klassen in fokussierte statische Klassen aufteilen (siehe R2). Der `LinterAnalyzer` bleibt der **explizite Dispatcher** — keine Reflection, keine Interface-Discovery. Die Verdrahtung bleibt sichtbar in `LinterAnalyzer.cs`, aber die eigentliche Logik zieht in kleine, fokussierte Dateien um.

### Klassifikation

- **Prio:** 🔴 Hoch
- **Aufwand:** M (2–3 Tage für alle Checker)
- **Nutzen:** ★★★★★ — neuer Checker hinzufügen wird 5× schneller, direkte Test-Isolation

---

## A2 — `Program.cs` als CLI-Mono-Router (568 LOC)

**Datei:** `src/AiNetLinter/Program.cs`

### Befund

`Program.cs` enthält **alle 8 Sub-Befehle** in einer einzigen Klasse:

| Methode                      | Zeile | Zweck                                                   |
| ---------------------------- | ----- | ------------------------------------------------------- |
| `RunAuditAsync`              | 156   | Standard-Lint-Run                                       |
| `RunAuditWithBaselineAsync`  | 203   | Audit mit Baseline-Filter                               |
| `RunSyncCursorRules`         | 487   | Cursor-Regel-Sync                                       |
| `RunPrintReadme`             | 539   | Eingebettete README ausgeben                            |
| `RunPlaybookCheckAsync`      | 350   | Playbook-Drift-Check                                    |
| `TryRunMaintenanceModeAsync` | 374   | Maintenance-Modi (CreateBaseline, Add/RemoveDisableAll) |
| `AuditWithBaselineAsync`     | 395   | Baseline-Workflow                                       |
| `ToLinterArgs`               | 66    | CLI → Domain-Mapping                                    |
| `ApplyAutoFixIfNeededAsync`  | 263   | Auto-Fix-Wrapper                                        |

**Probleme:**

- `ToLinterArgs` (Zeile 66–94) ist ein **25-Felder-1:1-Mapper** ohne Validierung
- Verschachtelte Call-Ketten (`RunAuditAsync` → `RunAuditWithBaselineAsync` → `AuditWithBaselineAsync`) → schwer nachvollziehbar
- Keine Trennung zwischen "was ausführen" und "wie ausgeben"
- Direkter Zugriff auf `PerformanceProfiler.Instance.StartPhase(...)` an 6+ Stellen

### Lösungsansatz

Jeden Command in eine eigene `internal static class` auslagern (z. B. `AuditCommand`, `DebtReportCommand`, `SyncCursorRulesCommand`). `Program.cs` bleibt der **explizite Router** mit derselben if-Kaskade — aber jetzt nur ~60 LOC, weil die eigentliche Logik in den Command-Klassen liegt. Keine Interfaces, keine Discovery (siehe R3).

### Klassifikation

- **Prio:** 🔴 Hoch
- **Aufwand:** M (1–3 Tage)
- **Nutzen:** ★★★★ — Testbarkeit, klare Verantwortlichkeiten

---


## A4 — `PerformanceProfiler` als globaler Singleton

**Datei:** `src/AiNetLinter/Diagnostics/PerformanceProfiler.cs` (349 LOC)

### Befund

```csharp
// PerformanceProfiler.cs, Zeilen 19–24
private static readonly Lazy<PerformanceProfiler> LazyInstance = new(() => new PerformanceProfiler());
public static PerformanceProfiler Instance => LazyInstance.Value;
```

**Probleme:**

- **Singletons in Produktionscode** verstecken Abhängigkeiten und blockieren Test-Isolation
- Schreibt in `measurements/<Solution>-<Timestamp>-<Guid>/performance.json` **bei jedem Lauf** wenn aktiviert → ungewollte Disk-IO in Tests
- `RecordDocumentAnalysis` sammelt **alle Datei-Pfade ungefiltert** → Speicher wächst linear mit Solution-Größe
- `_initialized`-Flag ist **nicht thread-safe** — Race-Condition bei parallelen Tests möglich

### Lösungsansatz

`PerformanceProfiler.Instance` aus dem Produktionscode entfernen. Stattdessen `IPerformanceProfiler`-Interface mit zwei Implementierungen (siehe R4):
- `NullPerformanceProfiler` — Tests nutzen diesen, kein Disk-IO, kein Speicherwachstum
- `PerformanceProfiler` — Konstruktor statt Singleton, nur aktiv wenn `--profile`-Flag gesetzt

Übergabe via Konstruktor-Parameter in `LinterEngine` (kein DI-Container — explizite Verdrahtung in `Program.cs`).

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** S (< 1 Tag)
- **Nutzen:** ★★★★ — Test-Isolation, Performance-Klarheit

---

## A5 — `LinterEngine` hat 3× nahezu identische `RunAsync`-Overloads

**Datei:** `src/AiNetLinter/Core/LinterEngine.cs` (Zeilen 35–57)

### Befund

```csharp
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(string path, bool noCache = false, int cacheTtlMinutes = 60) { ... }
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(SourceFileCatalog catalog, bool noCache = false, int cacheTtlMinutes = 60) { ... }
public async Task<IReadOnlyCollection<RuleViolation>> RunAsync(Solution solution, bool noCache = false, int cacheTtlMinutes = 60) { ... }
```

Jeder Overload unterscheidet sich nur in der **Eingabe-Extraktion**, nicht im Verhalten. Der Cache-Schlüssel wird dabei leicht unterschiedlich berechnet → inkonsistente Cache-Misses möglich.

### Lösungsansatz

Einen primären `RunAsync(SourceFileCatalog catalog, ...)` behalten; die anderen zwei als dünne Wrapper, die zunächst ein Catalog-Objekt aufbauen und dann delegieren (siehe R10).

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** S (< 1 Tag)
- **Nutzen:** ★★★ — Konsistenz, weniger Duplikation

---

## A6 — `Apply`-Methode mit 5 unnötigen Klonings

**Datei:** `src/AiNetLinter/Configuration/LinterConfig.cs` (Zeilen 195–252)

### Befund

```csharp
public GlobalConfig Apply(GlobalConfigOverride? @override)
{
    if (@override == null) return this;
    return ApplyStructuralRules(@override)
        .ApplyNamingAndStyleRules(@override)
        .ApplyCatchRules(@override)
        .ApplyImmutabilityRules(@override)
        .ApplyNamespaceAndAnalysisRules(@override);
}
// Jede der 5 Methoden macht: this with { ... }
```

5 verkettete `with`-Aufrufe erzeugen 5 Zwischen-Records. Bei 100 Dateien × 5 Overrides = 500 unnötige Heap-Allokationen.

### Lösungsansatz

Alle Properties in **einem einzigen `with { }` Block** zusammenfassen (siehe R6). Die Lesbarkeitsteilung in 5 Methoden kann durch Kommentare im einzelnen `with`-Block erreicht werden.

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** S (< 1 Tag)
- **Nutzen:** ★★★ — Performance + Lesbarkeit

---

## A7 — Fehlende `CancellationToken`-Propagation

**Dateien:** `LinterEngine.cs`, `LinterAnalyzer.cs`, `Program.cs`

### Befund

Keine einzige `async`-Methode akzeptiert ein `CancellationToken`-Argument:

```csharp
// LinterEngine.cs, Zeile 131
await Parallel.ForEachAsync(workItems, CreateParallelOptions(), (item, _) =>
    AnalyzeWorkItemAsync(item, state, cache));
//                                  ^ CancellationToken wird ignoriert
```

Bei `Ctrl+C` läuft die gesamte Pipeline (inkl. MSBuildWorkspace und alle Parallel-Threads) weiter bis zum nächsten IO-Punkt.

### Lösungsansatz

`CancellationToken ct = default` als letzten Parameter zu allen async-Methoden hinzufügen. `Main()` registriert `Console.CancelKeyPress` und übergibt das Token. `Parallel.ForEachAsync` bekommt den Token via `ParallelOptions` (siehe R5).

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** S (Routine-Refactoring, kein Architekturbruch)
- **Nutzen:** ★★★★ — UX + Robustheit

---

## A8 — `Console.WriteLine` als universelle Logging-Schnittstelle

**Vorkommen:** 25+ Stellen in Produktionscode

### Befund

Direktes `Console.WriteLine` / `Console.Error.WriteLine` in Produktionsklassen wie `Program.cs`, `LinterAutoFixer.cs`, `SourceFileCatalog.cs`, `CursorRulesGenerator.cs`.

**Konsequenzen:**

- Tests müssen `Console.SetOut` mocken — aufwändig und fragil
- Für Agenten: keine strukturierte Trennung zwischen Info/Warn/Error
- `ConsoleTestCollector.cs` existiert im Testprojekt als Workaround

### Lösungsansatz

`ILintConsole`-Interface (nicht `ILogger` — das würde mit `Microsoft.Extensions.Logging.ILogger` kollidieren) mit zwei Implementierungen: `ConsoleLintConsole` für Produktion und `TestLintConsole` für Tests. Übergabe via Konstruktor-Parameter (kein DI-Container). Alle `Console.WriteLine`-Aufrufe in Produktionsklassen durch `_console.Info(...)` ersetzen (siehe R7).

### Klassifikation

- **Prio:** 🟠 Mittel
- **Aufwand:** M (1–2 Tage, alle Stellen anpassen)
- **Nutzen:** ★★★★ — Test-Isolation, saubere Ausgabe-Kontrolle

---

## A9 — `SourceFileCatalog` Lifecycle-Unklarheiten

**Datei:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`

### Befund

- `MSBuildWorkspace` ist `IDisposable`, wird aber nicht explizit durch `LinterEngine` verwaltet
- `WithUpdatedSolution` erstellt neue Instanz mit **demselben Workspace** → Workspace kann während AutoFix wiederverwendet werden, ohne dass `LinterEngine` den Lifecycle kennt
- Zwei Konstruktoren mit leicht unterschiedlichen Feld-Initialisierungen

### Lösungsansatz

`SolutionBasePath` bereits in `SourceFileCatalog.LoadAsync` setzen, statt es später in `LinterEngine.ResolvePostAnalysisConfig` per `with { SolutionBasePath = dir }` nachzupflegen. Lifecycle von `MSBuildWorkspace` über `using`-Block in dem Code steuern, der das Catalog erstellt.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** S
- **Nutzen:** ★★★ — Lifecycle-Klarheit, Code-Hygiene

---

## A10 — Rule-Namen als String-Literale

**Vorkommen:** 60+ Stellen im Code

### Befund

In Checkern wird `nameof(ctx.Config.Global.EnforceSealedClasses)` konsequent verwendet — gut. Aber in `CursorRulesGenerator` und `ViolationTextFormatter` stehen String-Literale:

```csharp
new("EnforceSealedClasses", g => g.EnforceSealedClasses, ...)   // CursorRulesGenerator
["EnforceSealedClasses"] = "-> EnforceSealedClasses: ..."        // ViolationTextFormatter
```

→ Umbenennen einer Config-Property bricht diese Stellen ohne Compile-Fehler.

### Lösungsansatz

Statische `LinterRuleIds`-Const-Klasse mit `public const string EnforceSealedClasses = nameof(GlobalConfig.EnforceSealedClasses);` für alle Regel-Namen (siehe R8). Compile-Time-Sicherheit; Refactoring propagiert automatisch.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** S (< 1 Tag)
- **Nutzen:** ★★★ — Refactoring-Sicherheit

---

## A11 — `SuppressionEvaluator.IsSuppressed` ist O(N×M)

**Datei:** `src/AiNetLinter/Suppression/SuppressionEvaluator.cs`

### Befund

```csharp
public static bool IsSuppressed(string fileContent, string ruleName, int lineNumber)
{
    var lines = fileContent.Split('\n');
    foreach (var line in lines) { ... }
}
```

Bei jeder Violation wird `fileContent.Split('\n')` erneut ausgeführt. Bei 100 Violations × 500 Zeilen → quadratische Komplexität pro Datei.

### Lösungsansatz

Unterdrückungsindex einmalig pro Datei aufbauen (z. B. `Dictionary<int, HashSet<string>> suppressedByLine`) und im `CheckerContext` zwischenspeichern.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** S
- **Nutzen:** ★★★ — Performance bei Dateien mit vielen Violations

---

## A12 — Keine Code-Coverage-Statistik verfügbar

### Befund

Kein Coverage-Reporting im Repo. Test-Fixtures sind minimal (16 Dateien in `tests/Fixtures/BaselineMini/`). Es ist nicht klar, welche Checker getestet sind und welche nicht.

### Lösungsansatz

`coverlet` als NuGet-Paket im Test-Projekt; `dotnet test --collect:"XPlat Code Coverage"` in CI; optionaler Coverage-Threshold (z. B. 60 %) als Pflicht-Gate.

### Klassifikation

- **Prio:** 🟡 Niedrig
- **Aufwand:** S
- **Nutzen:** ★★★ — Vertrauen in Refactorings

---

## A13 — `LinterConfigNormalizer` nicht vollständig auditiert

**Datei:** `src/AiNetLinter/Configuration/LinterConfigNormalizer.cs` (im Codegraph referenziert, aber nicht detailliert betrachtet)

→ Möglicherweise Quelle für Drift-Probleme zwischen Default-Werten und normalisierten Werten. Separat prüfen.

---

## Zusammenfassung Architektur-Befunde

**Hauptproblem:** Der Code wuchs **rule-orientiert** (jeder Epic brachte neue Checker hinzu), aber nicht **architektur-orientiert**. Das Resultat ist ein hochfunktionales, aber strukturell fragiles System:

- Checker-Logik in God-Klassen versteckt (A1)
- Regel-Beschreibungen 3-fach dupliziert mit falschen Werten (A3)
- Singleton-Anti-Pattern in `PerformanceProfiler` (A4)
- CLI-Routing mit Orchestrierung vermischt (A2)

→ Die folgenden Refactorings (siehe `03-Architektur-Refactoring-Vorschlaege.md`) sind **Voraussetzung** für die nächsten 5+ Epics ohne weiteres Architektur-Drift.
