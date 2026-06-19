# 02 — Code-Qualität (Konkrete Befunde pro Datei)

> Detail-Befunde mit Zeilennummern, ergänzend zu den Architektur-Befunden in [`01-Architektur-Befunde.md`](01-Architektur-Befunde.md).  
> Empfehlungen siehe [`03-Architektur-Refactoring-Vorschlaege.md`](03-Architektur-Refactoring-Vorschlaege.md).

---

## Inhaltsverzeichnis

- [C1 — `Program.cs` (568 LOC)](#c1--programcs-568-loc)
- [C2 — `LinterAnalyzer.cs` (271 LOC)](#c2--linteranalyzercs-271-loc)
- [C3 — `LinterEngine.cs` (360 LOC)](#c3--linterenginecs-360-loc)
- [C4 — `LinterConfig.cs` (575 LOC)](#c4--linterconfigcs-575-loc)
- [C5 — `PerformanceProfiler.cs` (349 LOC)](#c5--performanceprofilercs-349-loc)
- [C6 — `RepoPlaybookGenerator.cs` (514 LOC)](#c6--repoplaybookgeneratorcs-514-loc)
- [C7 — `CursorRulesGenerator.cs` (335 LOC)](#c7--cursorrulesgeneratorcs-335-loc)
- [C8 — `LinterAutoFixer.cs` (322 LOC)](#c8--linterautofixercs-322-loc)
- [C9 — `ViolationTextFormatter.cs` (144 LOC)](#c9--violationtextformattercs-144-loc)
- [C10 — `SourceFileCatalog.cs` (235 LOC)](#c10--sourcefilecatalogcs-235-loc)
- [C11 — `ArchitectureChecker.cs` (303 LOC)](#c11--architecturecheckercs-303-loc)
- [C12 — `Checkers/*` — wiederkehrende Anti-Patterns](#c12--checkers----wiederkehrende-anti-patterns)
- [C14 — `Configuration/*` — Overrides](#c14--configuration----overrides)
- [C15 — Tests](#c15--tests)

---

## C1 — `Program.cs` (568 LOC)

**Datei:** `src/AiNetLinter/Program.cs`

### C1.1 — `ToLinterArgs` ist ein 25-Felder-Mapper ohne Validierung

```csharp
// Program.cs, Zeilen 66–94
private static LinterArgs ToLinterArgs(CliParsedArgs parsed)
{
    return new LinterArgs
    {
        ConfigPath = parsed.ConfigPath,
        TargetPath = parsed.TargetPath,
        // ... 22 weitere Felder
    };
}
```

**Befund:** 1:1-Mapping ohne Validierung oder Default-Logik. Wenn ein neues Feld zu `LinterArgs` hinzukommt, muss diese Methode aktualisiert werden — keine Compile-Time-Prüfung erzwingt das.

**Empfehlung:** `required`-Properties in `LinterArgs` nutzen, damit der Compiler fehlende Zuweisungen meldet.

### C1.2 — Verschachtelte If-Kaskaden in `ExecuteLinterAsync`

```csharp
// Program.cs, Zeilen 96–142
private static async Task<int> ExecuteLinterAsync(LinterArgs args)
{
    if (args.Readme) return RunPrintReadme();
    var validationError = ValidateArgs(args);
    if (validationError.HasValue) return validationError.Value;
    if (args.Check && args.PlaybookPath != null) return await RunPlaybookCheckAsync(args);
    if (args.SyncCursorRules && args.PlaybookPath == null) return RunSyncCursorRules(args);
    if (args.Footprint != null) return await FootprintExecutor.RunAsync(args);
    var maintenanceExitCode = await TryRunMaintenanceModeAsync(args);
    if (maintenanceExitCode.HasValue) return maintenanceExitCode.Value;
    if (args.DebtReport) return await DebtReportExecutor.RunDebtReportAsync(args);
    if (args.HasImpact) return await ImpactExecutor.RunImpactAnalysisAsync(args);
    return await RunAuditAsync(args);
}
```

**Befund:** 8 sequentielle if-Klauseln; jede prüft eine andere Sub-Befehls-Bedingung. Die eigentliche Logik jeder Methode liegt direkt in Program.cs (weitere 400+ LOC).

**Empfehlung:** Die Logik jedes Zweigs in eine eigene statische Command-Klasse auslagern (`AuditCommand`, `DebtReportCommand` etc.). Die if-Kaskade bleibt, wird aber zum dünnen Router auf 1-Zeiler (→ R3).

### C1.3 — Alle Methoden sind `static` → Logging nicht testbar

**Befund:** Alle Methoden in `Program.cs` sind `static` und rufen direkt `Console.WriteLine` auf → Tests müssen `Console.SetOut` mocken, was fragil und aufwändig ist.

**Empfehlung:** Logik in Command-Klassen auslagern, die `ILintConsole` via Konstruktor erhalten (→ R7).

### C1.4 — Keine `CancellationToken`-Behandlung in `Main`

```csharp
// Program.cs, Zeilen 39–64
return await root.Parse(args).InvokeAsync();
//                                           ^ kein Token
```

**Empfehlung:** `Console.CancelKeyPress` registrieren und Token durch die gesamte Pipeline reichen (→ R5).

---

## C2 — `LinterAnalyzer.cs` (271 LOC)

**Datei:** `src/AiNetLinter/Core/LinterAnalyzer.cs`

### C2.1 — Starrer Visitor-Dispatcher

(siehe A1)

### C2.2 — **Bug:** `VisitRecordDeclaration` ohne `CollectClassInfo`

```csharp
// LinterAnalyzer.cs, Zeilen 96–108
public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
{
    NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Record", _ctx);
    NamingChecker.CheckPascalCase(node.Identifier, "Record", _ctx);
    ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: true, _ctx);
    ScopeChecker.CheckMethodOverloads(node, _ctx);
    StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
    NestedTypesChecker.Check(node, _ctx);
    PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
    // FEHLT: ArchitectureChecker.CollectClassInfo(node, _ctx)  ← im Gegensatz zu ClassDeclaration!
    base.VisitRecordDeclaration(node);
}
```

**Befund:** Records erscheinen nicht in `ClassInfo`-Statistiken (Playbook, Footprint). Gleiches Problem in `VisitStructDeclaration`.

| Methode                           | CollectClassInfo? |
| --------------------------------- | ----------------- |
| `VisitClassDeclaration` (Z. 79)   | ✅ Ja             |
| `VisitRecordDeclaration` (Z. 96)  | ❌ **Fehlt**      |
| `VisitStructDeclaration` (Z. 110) | ❌ Fehlt          |

**Fix:** `ArchitectureChecker.CollectClassInfo(node, _ctx)` zu `VisitRecordDeclaration` und `VisitStructDeclaration` hinzufügen (→ R10 — XS-Aufwand).

### C2.3 — `CheckLineCount` führt Syntax-Walk durch, der später wiederholt wird

```csharp
// LinterAnalyzer.cs, Zeilen 199–234
private void CheckLineCount()
{
    var lineCount = _tree.GetText().Lines.Count;
    var hasPartials = _tree.GetRoot().DescendantNodes()
        .OfType<TypeDeclarationSyntax>()
        .Any(t => t.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
    ...
}
```

**Befund:** `_tree.GetRoot().DescendantNodes()` läuft doppelt: einmal hier, dann nochmal in `Visit(_tree.GetRoot())`. Bei großen Dateien redundanter Tree-Walk.

---

## C3 — `LinterEngine.cs` (360 LOC)

**Datei:** `src/AiNetLinter/Core/LinterEngine.cs`

### C3.1 — 3 nahezu identische `RunAsync`-Overloads

(siehe A5)

### C3.2 — `AnalyzeDocumentAsync` hat zu viele Verantwortlichkeiten (40 Zeilen, 7 Aufgaben)

```csharp
// LinterEngine.cs, Zeilen 224–263
private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisState state, AnalysisCacheManager? cache)
{
    // 1. File-Filter
    // 2. Cache-Lookup
    // 3. SemanticModel-Laden
    // 4. Profiler-Timing starten
    // 5. Analyse-Ausführung
    // 6. Cache-Write
    // 7. Profiler-Recording
    AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordDocumentAnalysis(...);
}
```

**Befund:** Direktzugriff auf `PerformanceProfiler.Instance` ist der Hauptblocker für Tests. Nach R4 (Profiler via Konstruktor-Parameter) wird diese Methode automatisch testbar.

### C3.3 — `IsTestFile` benutzt naive String-Heuristik

```csharp
private static bool IsTestFile(string file)
{
    if (file.EndsWith("Tests.cs")) return true;
    if (file.EndsWith("Test.cs")) return true;
    return file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}");
}
```

**Befund:** Funktioniert nur korrekt weil `TestProjectDetector` das Projekt bereits prüft. Kann zu false-positives führen (z. B. `MyControllerTests.cs` in einem Service-Projekt).

---

## C4 — `LinterConfig.cs` (575 LOC)

**Datei:** `src/AiNetLinter/Configuration/LinterConfig.cs`

### C4.1 — `GlobalConfig` mit 30+ Properties als eine Klasse

**Befund:** Eine einzige `record`-Klasse mischt Boolean-Flags, String-Listen, Strings und numerische Werte ohne strukturelle Gruppierung. Schwer zu navigieren; jede neue Regel verdichtet die Klasse weiter.

### C4.2 — `Apply`-Methode mit 5 unnötigen Klonings

(siehe A6 — → R6)

### C4.3 — Keine `JsonConverter` für Wildcards

**Befund:** Wildcard-Parsing-Logik (`MatchWildcard` in `ImmutabilityChecker.cs:99`) ist handgeschrieben und kennt nur `*`, `prefix*`, `*suffix`, `*contains*`. Komplexere Patterns sind nicht dokumentiert.

### C4.4 — `SolutionBasePath` wird nach Analyse gesetzt

```csharp
// LinterEngine.cs
return _config with { SolutionBasePath = dir };
```

**Befund:** Workaround-Mutation nach Analyse-Start. `SolutionBasePath` sollte bereits beim Laden bekannt sein (→ A9).

---

## C5 — `PerformanceProfiler.cs` (349 LOC)

**Datei:** `src/AiNetLinter/Diagnostics/PerformanceProfiler.cs`

### C5.1 — Singleton-Anti-Pattern

(siehe A4 — → R4)

### C5.2 — `_initialized` Flag nicht thread-safe

```csharp
public void Initialize(bool enabled, string[]? args = null)
{
    if (_initialized) return;
    _enabled = enabled;
    _initialized = true;
}
```

**Befund:** Kein `Interlocked` oder `lock` → Race-Condition bei parallelen Tests möglich.

### C5.3 — IO-Operationen in statischer Methode

```csharp
private static string SetupTargetDirectory(ProfilerContext ctx)
{
    Directory.CreateDirectory(targetDir);
    return targetDir;
}
```

**Befund:** Disk-IO in statischer Methode → nicht testbar ohne tatsächliche Disk-Operationen.

### C5.4 — Speicher wächst unbegrenzt

**Befund:** `_documentEntries` (Zeile 34) ist `ConcurrentBag<DocumentPerformanceEntry>` ohne Limit. Bei 10.000 Dateien wird die gesamte Performance-Info im Speicher gehalten.

---

## C6 — `RepoPlaybookGenerator.cs` (514 LOC)

**Datei:** `src/AiNetLinter/Core/RepoPlaybookGenerator.cs`

### C6.1 — **Bug:** Hardcoded falsche Werte in `RuleDescriptions`

(siehe A3, F9 — → R11 für Quick-Win-Fix)

### C6.2 — `ScanSolutionAsync` sequenziell statt parallel

```csharp
foreach (var project in solution.Projects)
{
    foreach (var document in project.Documents)
    {
        var docScan = await ScanDocumentAsync(document, ...);
    }
}
```

**Befund:** Sequenziell! `LinterEngine.AnalyzeSolutionAsync` verwendet `Parallel.ForEachAsync`. Konsistenz fehlt.

### C6.3 — `PlaybookSyntaxWalker` als nested class

**Befund:** Walker-Logik ist innerhalb des Generators definiert, nicht wiederverwendbar.

### C6.4 — Magic Numbers

```csharp
.OrderByDescending(x => x.FileCount).Take(5).ToList();
```

`5` und `3` an mehreren Stellen: nicht konfigurierbar.

---

## C7 — `CursorRulesGenerator.cs` (335 LOC)

**Datei:** `src/AiNetLinter/Core/CursorRulesGenerator.cs`

### C7.1 — `GlobalRules[]` ist eine 20+ Eintrag-Inline-Liste

(Duplikat der Regel-Metadaten — → R1 für zentrale RuleRegistry)

### C7.2 — `RuleDefinition` ist `private sealed record`

**Befund:** Wegen `private` nicht von anderen Generatoren wiederverwendbar. Sollte in ein gemeinsames `RuleMetadata`-Modell (→ R1).

### C7.3 — Reflection-Hack in `AppendProjectOverridesDelta`

```csharp
var prop = typeof(MetricsConfigOverride).GetProperty(metric.Name);
if (prop?.GetValue(overrides.Metrics) is int val)
```

**Befund:** Reflection zur Laufzeit → kein Compile-Time-Check. Wenn ein `metric.Name` umbenannt wird, bricht die Reflection.

### C7.4 — `intent-order` ist hartkodiert

```csharp
private static readonly string[] IntentOrder =
    ["agent-resilience", "agent-context", "architecture", ...];
```

**Befund:** Reihenfolge nicht erweiterbar ohne Code-Änderung.

---

## C8 — `LinterAutoFixer.cs` (322 LOC)

**Datei:** `src/AiNetLinter/Core/LinterAutoFixer.cs`

### C8.1 — `CollectBaseTypesAsync` ist O(N×M)

**Befund:** Iteriert durch alle SyntaxTrees in der Solution nur um BaseTypes zu sammeln. Roslyn bietet direktere Wege via `INamedTypeSymbol.BaseType`.

### C8.2 — File-IO statt Roslyn-Workspace-Update

```csharp
await File.WriteAllTextAsync(document.FilePath, newText.ToString(), newText.Encoding ?? Encoding.UTF8);
```

**Befund:** Direktes Schreiben auf Disk statt `workspace.TryApplyChanges()` → kein atomares Update, kein Change-Tracking.

### C8.3 — Fix-Matching über `v.Details.Contains(...)`

```csharp
return violations.Any(v => v.LineNumber == GetLineNumber(variable) && v.Details.Contains($"'{symbol.Name}'"));
```

**Befund:** String-Contains-Matching → spröde. Wenn Violation-Detail-Text sich ändert, bricht der Fix. Besser: expliziter `RuleName`-Filter.

---

## C9 — `ViolationTextFormatter.cs` (144 LOC)

**Datei:** `src/AiNetLinter/Output/ViolationTextFormatter.cs`

### C9.1 — `RuleInstructions` Dict mit 26 Einträgen (Duplikat)

(→ Duplikat der Regel-Metadaten — wird durch R1 RuleRegistry gelöst)

### C9.2 — Fallback-Instruction für unbekannte Regeln

```csharp
return $"-> {ruleName}: Bitte behebe diesen Verstoss gemaess den Richtlinien.";
```

**Befund:** Wenn eine neue Regel hinzukommt, fällt sie auf einen generischen Fallback zurück → LLM bekommt keine klare Anleitung. Mit R1 (RuleRegistry) automatisch behoben, weil alle Regeln dort vollständig beschrieben sind.

### C9.3 — Filesystem-Zugriff in einer Format-Klasse

```csharp
if (Directory.Exists(cursorRulesPath))
    sb.Append("Projektkonfiguration erkannt: ...");
if (File.Exists(claudeMdPath))
    sb.Append("Projektkonfiguration erkannt: ...");
```

**Befund:** FS-Zugriffe in einer reinen Format-Klasse → Verletzung von SRP.

---

## C10 — `SourceFileCatalog.cs` (235 LOC)

**Datei:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`

### C10.1 — `IsGeneratedPath` dupliziert `FileFilters`

```csharp
private static bool IsGeneratedPath(string path)
{
    return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
           path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
           ...
}
```

**Befund:** Hardcoded Pfad-Filter, die `FileFilters.ExcludeDirectoryPatterns` aus `LinterConfig` duplizieren → drift-anfällig.

### C10.2 — `LoadAsync` macht synchrones `Console.Error.WriteLine`

```csharp
Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {msg}");
```

**Befund:** Logging direkt in der Lade-Methode → nach R7 (ILintConsole) durch `_console.Warn(...)` ersetzen.

---

## C11 — `ArchitectureChecker.cs` (303 LOC)

**Datei:** `src/AiNetLinter/Core/Checkers/ArchitectureChecker.cs`

### C11.1 — Sammelt 18 unzusammenhängende Methoden

`ArchitectureChecker` enthält:

- `CollectClassInfo` (Z. 16) — Statistik
- `CheckSealedClass` (Z. 43) — Sealed-Regel
- `CheckValueObjectContract` (Z. 60) — Value-Objects
- `CheckForbiddenNamespace` (Z. 93) — Namespace-Verbote
- `CheckPhantomNamespace` (Z. 115) — Phantom-Dependencies
- `CheckDynamic` (Z. 135) — dynamic-Verbot
- `CheckForbiddenSymbolNamespace` (Z. 150) — Symbol-Namespaces
- `CheckPhantomReflection` (Z. 173) — Reflection-Verbote
- `IsGeneratedCode` (Z. 196) — Helper
- `IsSealedOrStaticOrAbstract` (Z. 212) — Helper
- `HasExemptSuffix` (Z. 215) — Helper
- `IsStructOrReadOnly` (Z. 222) — Helper
- `NamespaceMatches` (Z. 228) — Helper
- `GetInheritanceDepth` (Z. 239) — Tiefe-Berechnung
- `IsFrameworkBaseType` (Z. 252) — Helper
- `CheckForTestMethods` (Z. 268) — Test-Erkennung
- `IsTestAttribute` (Z. 274) — Helper
- `GetBaseTypeNames` (Z. 286) — Helper

→ **18 Methoden** für völlig unterschiedliche Domänen — das ist keine kohärente Klasse.

**Empfehlung:** Aufteilen in fokussierte statische Klassen (→ R2):

| Neue Klasse              | Methoden aus ArchitectureChecker               |
| ------------------------ | ---------------------------------------------- |
| `SealedClassChecker`     | `CheckSealedClass`, `IsSealedOrStaticOrAbstract`, `HasExemptSuffix` |
| `ValueObjectChecker`     | `CheckValueObjectContract`, `IsStructOrReadOnly`|
| `NamespaceCouplingChecker`| `CheckForbiddenNamespace`, `CheckForbiddenSymbolNamespace`, `NamespaceMatches` |
| `PhantomDependencyChecker`| `CheckPhantomNamespace`, `CheckPhantomReflection`, `IsForbiddenReflectionCall` |
| `DynamicTypeChecker`     | `CheckDynamic`                                  |
| `ClassInfoCollector`     | `CollectClassInfo`, `GetBaseTypeNames`          |
| `GeneratedCodeDetector`  | `IsGeneratedCode`                               |
| `InheritanceDepthChecker`| `GetInheritanceDepth`, `IsFrameworkBaseType`    |
| `TestAttributeDetector`  | `CheckForTestMethods`, `IsTestAttribute`        |

Alle bleiben `internal static class`. `LinterAnalyzer` verdrahtet sie explizit.

### C11.2 — `IsTestAttribute` kennt nicht alle Frameworks

```csharp
return ns.StartsWith("Xunit", ...) || ns.StartsWith("NUnit", ...) || ...;
```

`xUnit.v3` (`Xunit.v3.*`) fehlt. Konfigurierbare Liste wäre robuster.

### C11.3 — `IsForbiddenReflectionCall` mit hardcoded Strings

Nicht erweiterbar ohne Code-Änderung. Nach R1 (RuleRegistry) könnte diese Liste konfigurierbar werden.

---

## C12 — `Checkers/*` — wiederkehrende Anti-Patterns

### C12.1 — Alle Checker sind `internal static`

**Befund:** `internal static class` ist das richtige Pattern für das Projekt (kein DI-Container, statische Kompilierung). Tests nutzen `[assembly: InternalsVisibleTo("AiNetLinter.Tests")]`. Dies ist kein Anti-Pattern, sondern ein projektspezifisches Design — nach dem Aufteilen der God-Klassen werden einzelne Checker direkt testbar.

### C12.2 — Magic-String-Matching für Exception-Typen

**Beispiel:** `ControlFlowChecker.cs:73–75`

```csharp
private static bool IsCancellationExceptionName(string? name) =>
    name == "OperationCanceledException" || name == "TaskCanceledException" || ...;
```

**Befund:** Strings sind nicht als Konstanten definiert → nach R8 (`LinterRuleIds`) ähnliches Muster auch für Exception-Typ-Namen einführen.

### C12.3 — Violation-Erstellung überall gleich, aber kein Helper

**Befund:** In jedem Checker wiederholt sich:

```csharp
ctx.AddViolation(new RuleViolation
{
    FilePath = ctx.FilePath,
    LineNumber = SyntaxHelper.LineOf(node),
    RuleName = nameof(ctx.Config.X.Y),
    Details = "...",
    Guidance = "..."
});
```

30+ Mal im Code. Eine `ctx.ReportViolation(ruleName, node, details, guidance)`-Methode würde Boilerplate eliminieren.

### C12.4 — `ForbiddenNames` Set in `NamingChecker` hardcoded

```csharp
private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
{
    "data", "temp", "obj", "val", "tmp", "item", "param"
};
```

**Befund:** Nicht via Konfiguration erweiterbar.

### C12.5 — `ScopeChecker.FindProjectDirectory` macht Disk-IO pro Datei

```csharp
private static string FindProjectDirectory(string startDir)
{
    var current = startDir;
    while (!string.IsNullOrEmpty(current))
    {
        if (Directory.GetFiles(current, "*.csproj").Any()) return current;
        current = Path.GetDirectoryName(current);
    }
    return "";
}
```

**Befund:** Walk nach oben für **jede Datei** → bei 500 Dateien in tiefen Verzeichnissen langsam. Ergebnis sollte gecacht werden.

---

## C14 — `Configuration/*` — Overrides

**Datei:** `src/AiNetLinter/Configuration/LinterConfigOverrides.cs`

### C14.1 — Overrides duplizieren Properties aus Haupt-Config

**Befund:** `GlobalConfigOverride` (266 LOC) dupliziert ~30 Properties aus `GlobalConfig`, aber als `bool?`. Jede neue Regel muss in beiden Klassen ergänzt werden → drift-anfällig.

### C14.2 — `MetricsConfigOverride` mit ~40 nullable Properties

Gleiche Problem-Struktur wie C14.1, aber noch größer.

### C14.3 — Reflection im `CursorRulesGenerator`

(→ C7.3)

---

## C15 — Tests

### C15.1 — `ConsoleTestCollector` deutet auf Test-Pattern-Limitationen

**Befund:** Eine Datei `ConsoleTestCollector.cs` existiert im Test-Projekt — vermutlich Workaround für `Console.SetOut`-Mocking. Das ist ein direktes Symptom von A8 (Console.WriteLine in Produktionsklassen). Nach R7 (ILintConsole) kann dieser Workaround entfallen.

### C15.2 — Kein Coverage-Reporting

(→ A12)

### C15.3 — kein Mutation-Testing

**Befund:** xUnit v3 ist eine gute Wahl. Coverage-Prozentzahlen sagen ohne Mutation-Testing wenig über Test-Qualität aus. `Stryker.NET` wäre ein optionaler nächster Schritt.

---

## Zusammenfassung Code-Qualität

Die Code-Basis ist **funktional korrekt**, aber **strukturell gewachsen** ohne Refactoring-Pausen. Hot-Spots:

| Datei                      | LOC | Verantwortlichkeiten                       | Risiko |
| -------------------------- | --- | ------------------------------------------ | ------ |
| `Program.cs`               | 568 | 8 Sub-Befehle, Profiler-Orchestrierung     | Hoch   |
| `LinterConfig.cs`          | 575 | 30+ Config-Properties + 3 Apply-Methoden  | Mittel |
| `RepoPlaybookGenerator.cs` | 514 | Generierung + Walker + Stats + Bug-Werte  | Mittel |
| `LinterEngine.cs`          | 360 | Orchestration + Cache + Profiler           | Mittel |
| `PerformanceProfiler.cs`   | 349 | Singleton + IO + Globals                   | Hoch   |
| `CursorRulesGenerator.cs`  | 335 | Regel-Liste + Reflection + MDC-Template   | Mittel |
| `LinterAutoFixer.cs`       | 322 | 3 Fix-Typen + BaseType-Scan + IO           | Mittel |
| `ArchitectureChecker.cs`   | 303 | **18** unzusammenhängende Methoden         | Hoch   |
| `LinterAnalyzer.cs`        | 271 | Visitor + 14 Checker-Aufrufe              | Hoch   |

→ Diese Top-9-Dateien machen ~3.500 LOC aus = **30 %** des Produktions-Codes. Konkrete Lösungsansätze in `03-Architektur-Refactoring-Vorschlaege.md`.
