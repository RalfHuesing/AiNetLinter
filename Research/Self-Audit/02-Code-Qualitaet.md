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
- [C15 — Tests (`src/AiNetLinter.Tests`)](#c15--tests-srcainetlintertests)

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

**Empfehlung:** Mapper-Generator via Reflection oder Konstruktor-basiertes Mapping mit `required`-Properties.

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

**Befund:** 8 sequentielle if-Klauseln; jede prüft eine andere Sub-Befehls-Bedingung. Schwer testbar, keine klare Priorität der Modi.

**Empfehlung:** `ICommandExecutor` Interface mit `CanHandle(LinterArgs)` und `ExecuteAsync(LinterArgs)`; Pipeline sortiert nach Priorität.

### C1.3 — `static` Klassenmethoden überall

**Befund:** Alle Methoden in `Program.cs` sind `static` → Tests müssen `InternalsVisibleTo` aktivieren oder via Konsolen-Redirect testen.

**Empfehlung:** `Program.cs` bleibt `static`, aber extrahiere Logik in `instance methods` auf `LinterRunner` o.ä., die per DI testbar sind.

### C1.4 — Keine `CancellationToken`-Behandlung in `Main`

```csharp
// Program.cs, Zeilen 39–64
public static async Task<int> Main(string[] args)
{
    ...
    return await root.Parse(args).InvokeAsync();
}
```

**Befund:** `InvokeAsync()` ohne Token. Ctrl+C wird von System.CommandLine wahrscheinlich abgefangen, aber unkoordiniert.

---

## C2 — `LinterAnalyzer.cs` (271 LOC)

**Datei:** `src/AiNetLinter/Core/LinterAnalyzer.cs`

### C2.1 — Starrer Visitor-Dispatcher

(siehe A1)

### C2.2 — `VisitRecordDeclaration` ohne `CollectClassInfo`

```csharp
// LinterAnalyzer.cs, Zeilen 96–108
public override void VisitRecordDeclaration(RecordDeclarationSyntax node)
{
    if (_ctx.Config.FileFilters.SkipGeneratedCodeAttribute && ArchitectureChecker.IsGeneratedCode(node, _ctx))
        return;
    NamingChecker.CheckXmlDoc(node, node.Identifier.Text, "Record", _ctx);
    NamingChecker.CheckPascalCase(node.Identifier, "Record", _ctx);
    ArchitectureChecker.CheckValueObjectContract(node, node.Identifier.Text, isRecord: true, _ctx);
    ScopeChecker.CheckMethodOverloads(node, _ctx);
    StateChecker.CheckPrimaryConstructorDependencies(node, _ctx);
    NestedTypesChecker.Check(node, _ctx);
    PublicMembersChecker.Check(node, node.Identifier.Text, _ctx);
    // FEHLT: ArchitectureChecker.CollectClassInfo(node, _ctx)  <- im Gegensatz zu ClassDeclaration!
    base.VisitRecordDeclaration(node);
}
```

**Befund:** **Bug-Potential**: `VisitRecordDeclaration` ruft KEIN `CollectClassInfo` auf → Record-Klassen werden in `ClassInfo`-Statistiken (z. B. Playbook, Footprint) **nicht erfasst**.

**Vergleich:**

| Method                            | CollectClassInfo? |
| --------------------------------- | ----------------- |
| `VisitClassDeclaration` (Z. 79)   | ✅ Ja             |
| `VisitRecordDeclaration` (Z. 96)  | ❌ **Fehlt**      |
| `VisitStructDeclaration` (Z. 110) | ❌ Fehlt          |

→ **Inkonsistenz / Bug** (in Playbook-Ausgabe oder `--footprint` für Records fehlen Daten)

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

**Befund:** `_tree.GetRoot().DescendantNodes()` läuft **doppelt**: einmal in `CheckLineCount`, dann nochmal in `Visit(_tree.GetRoot())`. Bei großen Dateien redundante Tree-Walks.

### C2.4 — `internal` Konstruktor statt `public`

```csharp
// LinterAnalyzer.cs, Zeile 23
internal LinterAnalyzer(string filePath, SemanticModel semanticModel, LinterConfig config, bool isTestFile, string? projectName = null)
```

**Befund:** `internal` Konstruktor + `[InternalsVisibleTo]` (siehe `LinterEngine.cs:14`) → Tests müssen in separatem Assembly liegen, aber das versteckt die Klasse für potenzielle Erweiterungen.

---

## C3 — `LinterEngine.cs` (360 LOC)

**Datei:** `src/AiNetLinter/Core/LinterEngine.cs`

### C3.1 — 3 nahezu identische `RunAsync`-Overloads

(siehe A5)

### C3.2 — `AnalyzeDocumentAsync` ist 40 Zeilen mit zu vielen Verantwortlichkeiten

```csharp
// LinterEngine.cs, Zeilen 224–263
private async Task AnalyzeDocumentAsync(Document document, bool isTestProj, AnalysisState state, AnalysisCacheManager? cache)
{
    var filePath = document.FilePath ?? document.Name;
    if (FileFilterEvaluator.IsExcluded(filePath, _config.FileFilters)) return;
    var solutionDir = GetSolutionDir(state.Solution);
    var relativePath = GetRelativePath(solutionDir, filePath);
    bool isTestFile = isTestProj || IsTestFile(filePath);
    string? checksum = null;
    if (cache != null && File.Exists(filePath))
    {
        checksum = FileChecksumCalculator.ComputeSha256Hex(filePath);
        if (cache.TryGet(relativePath, checksum, out var cached) && cached != null)
        {
            CacheEntryMapper.RestoreToState(cached, state, isTestFile);
            return;
        }
    }
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var semanticModel = await document.GetSemanticModelAsync();
    if (semanticModel == null) return;
    var sourceText = await document.GetTextAsync();
    state.FileContents[filePath] = sourceText.ToString();
    var effectiveConfig = ProjectConfigResolver.ResolveForDocument(document, _config, solutionDir);
    var context = new DocumentContext(filePath, semanticModel, isTestFile, effectiveConfig, document.Project.Name);
    var analyzer = new LinterAnalyzer(context.FilePath, context.SemanticModel, context.EffectiveConfig, context.IsTestFile, context.ProjectName);
    analyzer.RunAnalysis();
    CollectAnalyzerResults(analyzer, context, state);
    SaveToCache(new CacheDestination(cache, checksum, relativePath), analyzer, context);
    stopwatch.Stop();
    AiNetLinter.Diagnostics.PerformanceProfiler.Instance.RecordDocumentAnalysis(relativePath, stopwatch.Elapsed.TotalMilliseconds, analyzer.Violations.Count);
}
```

**Befund:** Macht **6 Dinge** auf einmal:

1. File-Filter
2. Cache-Lookup
3. Profiler-Timing
4. SemanticModel-Laden
5. Analyse-Ausführung
6. Cache-Write
7. Profiler-Recording

→ Schwer testbar, schwer refactorisierbar.

### C3.3 — `IsTestFile` benutzt naive String-Heuristik

```csharp
// LinterEngine.cs, Zeilen 354–359
private static bool IsTestFile(string file)
{
    if (file.EndsWith("Tests.cs")) return true;
    if (file.EndsWith("Test.cs")) return true;
    return file.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}");
}
```

**Befund:** Funktioniert nur, weil `TestProjectDetector` (separate Logik) bereits das Projekt prüft. Diese Methode ist redundant und kann zu false-positives führen (z. B. `MyControllerTests.cs` in einem Service-Projekt).

### C3.4 — `BuildCache` mit Magic-Strings

```csharp
// LinterEngine.cs, Zeile 65
var exeDir = Path.GetDirectoryName(
    System.Reflection.Assembly.GetExecutingAssembly().Location)!;
```

**Befund:** Cache-Pfad wird relativ zur Executable-Location berechnet → bei dotnet-tool-Installation (`~/.dotnet/tools/ainetlinter/`) ungewöhnliche Cache-Pfade. Kein Override möglich.

---

## C4 — `LinterConfig.cs` (575 LOC)

**Datei:** `src/AiNetLinter/Configuration/LinterConfig.cs`

### C4.1 — `GlobalConfig` mit 30+ Properties

**Befund:** Eine einzige `record`-Klasse mit **30+ Konfigurations-Properties**. Mischung aus:

- Boolean-Flags (`EnforceSealedClasses`, `AllowDynamic`, ...)
- String-Listen (`SealedClassExemptSuffixes`, `ImmutabilityExemptSuffixes`, ...)
- Strings (`NamespaceDirectoryMappingMode`)
- Numerische Werte (`NamespaceDirectoryMappingRequiredTrailingSegments`)

→ Das ist **kein** Konfigurations-Objekt, das ist eine **Ansammlung von Features**. Strukturelle Gruppierung (z. B. in Sub-Records `NamingRules`, `CatchRules`, `NamespaceRules`) würde Klarheit schaffen.

### C4.2 — `Apply`-Methode mit 5 unnötigen Klonings

(siehe A6)

### C4.3 — Keine `JsonConverter` für Wildcards

**Befund:** Konfiguration akzeptiert Wildcards (`ImmutabilityExemptPatterns`), aber die Wildcard-Parsing-Logik (`MatchWildcard` in `ImmutabilityChecker.cs:99`) ist handgeschrieben und kennt nur `*`, `prefix*`, `*suffix`, `*contains*`. Komplexere Patterns (z. B. `A?.B`) werden nicht unterstützt.

### C4.4 — `LinterConfig` ist `public sealed record` aber `with`-Mutationen in Code

```csharp
// LinterConfig.cs, Zeile 36
public string? SolutionBasePath { get; init; }
```

**Befund:** `SolutionBasePath` ist `init`-only, aber `LinterEngine.ResolvePostAnalysisConfig` (siehe A12) mutiert die Config über `with { SolutionBasePath = dir }` → Workaround-Lösung.

---

## C5 — `PerformanceProfiler.cs` (349 LOC)

**Datei:** `src/AiNetLinter/Diagnostics/PerformanceProfiler.cs`

### C5.1 — Singleton-Anti-Pattern

(siehe A4)

### C5.2 — `_initialized` Flag nicht thread-safe

```csharp
// PerformanceProfiler.cs, Zeilen 49–80
public void Initialize(bool enabled, string[]? args = null)
{
    if (_initialized) return;
    _enabled = enabled;
    _initialized = true;
    // ...
}
```

**Befund:** Kein `Interlocked` oder `lock` → bei parallelem Zugriff könnten 2 Threads die Initialisierung starten, einer davon "gewinnt" das `_initialized = true`, aber die Race Condition kann zu inkonsistenten Daten führen.

### C5.3 — IO-Operationen im Singleton

```csharp
// PerformanceProfiler.cs, Zeilen 192–199
private static string SetupTargetDirectory(ProfilerContext ctx)
{
    var dirName = $"{ctx.SolutionName}-{ctx.Timestamp:yyyy-MM-dd-HH-mm-ss-fff}-{Guid.NewGuid().ToString("N")[..8]}";
    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
    var targetDir = Path.Combine(baseDir, "measurements", ctx.SolutionName, ctx.Timestamp.ToString("yyyy-MM-dd"), dirName);
    Directory.CreateDirectory(targetDir);
    return targetDir;
}
```

**Befund:** IO-Operationen (Directory.CreateDirectory) in **statischer Methode** → nicht testbar ohne tatsächliche Disk-Operationen.

### C5.4 — Speicher wächst unbegrenzt

**Befund:** `_documentEntries` (Zeile 34) ist `ConcurrentBag<DocumentPerformanceEntry>` ohne Limit. Bei einer Solution mit 10.000 Dateien wird die gesamte Performance-Info im Speicher gehalten.

---

## C6 — `RepoPlaybookGenerator.cs` (514 LOC)

**Datei:** `src/AiNetLinter/Core/RepoPlaybookGenerator.cs`

### C6.1 — Hardcoded falsche Werte in `RuleDescriptions`

(siehe A3, F10)

### C6.2 — `ScanSolutionAsync` macht sequentielles Doc-Walk

```csharp
// RepoPlaybookGenerator.cs, Zeilen 145–187
foreach (var project in solution.Projects)
{
    foreach (var document in project.Documents)
    {
        var docScan = await ScanDocumentAsync(document, suppressionCounts, opts.Config);
        // ...
    }
}
```

**Befund:** Sequentiell! Bei 1.000 Dateien → 1.000 sequentielle `await`-Aufrufe. Der Rest des Linters (`LinterEngine.AnalyzeSolutionAsync`) macht **parallel**.

### C6.3 — `PlaybookSyntaxWalker` als nested class

```csharp
// RepoPlaybookGenerator.cs, Zeilen 418–513
private sealed class PlaybookSyntaxWalker : CSharpSyntaxWalker
{
    ...
}
```

**Befund:** Die Walker-Logik ist **innerhalb** des Generators definiert, nicht wiederverwendbar. Eine zentrale `SyntaxHelpers`-Klasse wäre konsistenter.

### C6.4 — `PlaybookStats.Violations` doppelt-zwischengespeichert

**Befund:** `PlaybookStats` enthält sowohl `DocInfos` als auch `Violations` (Z. 82) — letztere werden aus `opts.PrecomputedViolations` ODER `engine.RunAsync(solution)` (Z. 182) befüllt. Doppelter Cache → wenn der Caller bereits Violations hat, werden sie nochmal aggregiert.

### C6.5 — Magic Numbers

```csharp
// RepoPlaybookGenerator.cs, Zeilen 352–354
.OrderByDescending(x => x.FileCount).Take(5).ToList();
```

→ `5` ist nicht konfigurierbar; `3` (Zeile 320) ebenfalls nicht.

---

## C7 — `CursorRulesGenerator.cs` (335 LOC)

**Datei:** `src/AiNetLinter/Core/CursorRulesGenerator.cs`

### C7.1 — `GlobalRules[]` ist eine 20+ Eintrag-Inline-Liste

```csharp
// CursorRulesGenerator.cs, Zeilen 29–91
private static readonly RuleDefinition[] GlobalRules = [
    new("EnforceSealedClasses", g => g.EnforceSealedClasses, ...),
    new("AllowUnsealedPartialClasses", g => g.AllowUnsealedPartialClasses, ...),
    // ... 18 weitere Einträge
];
```

**Befund:** Jede Regel hat 4 Felder: Name, IsEnabled, DeactiveDesc, CursorHint. Diese Daten sind statisch, aber im Code verstreut → Reflection-basierte Generierung wäre konsistenter.

### C7.2 — `RuleDefinition` ist ein `private sealed record`

```csharp
// CursorRulesGenerator.cs, Zeilen 17–22
private sealed record RuleDefinition(
    string Name,
    Func<GlobalConfig, bool> IsEnabled,
    string DeactiveDesc,
    string CursorHint
);
```

**Befund:** Wegen `private` nicht von anderen Generatoren wiederverwendbar. Sollte in ein gemeinsames `RuleMetadata`-Modell.

### C7.3 — Reflection-Hack in `AppendProjectOverridesDelta`

```csharp
// CursorRulesGenerator.cs, Zeilen 316–323
private static void CollectMetricOverrideParts(ProjectOverrideEntry overrides, List<string> parts)
{
    if (overrides.Metrics == null) return;
    foreach (var metric in MetricsList)
    {
        var prop = typeof(MetricsConfigOverride).GetProperty(metric.Name);
        if (prop?.GetValue(overrides.Metrics) is int val)
            parts.Add($"`{metric.Name}` **{val}**");
    }
}
```

**Befund:** Reflection zur Laufzeit → langsam, kein Compile-Time-Check. Wenn ein `metric.Name` umbenannt wird, bricht die Reflection.

### C7.4 — `intent-order` ist hartkodiert

```csharp
// CursorRulesGenerator.cs, Zeile 27
private static readonly string[] IntentOrder =
    ["agent-resilience", "agent-context", "architecture", "aspnet-binding", "test-coverage", "control-flow", "csharp-idiom", "general"];
```

**Befund:** Reihenfolge ist hartkodiert; nicht erweiterbar für neue `Intent`-Werte.

---

## C8 — `LinterAutoFixer.cs` (322 LOC)

**Datei:** `src/AiNetLinter/Core/LinterAutoFixer.cs`

### C8.1 — `CollectBaseTypesAsync` ist O(N×M)

```csharp
// LinterAutoFixer.cs, Zeilen 72–88
private static async Task<HashSet<INamedTypeSymbol>> CollectBaseTypesAsync(Solution solution)
{
    var baseTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    foreach (var project in solution.Projects)
    {
        var compilation = await project.GetCompilationAsync();
        if (compilation == null) continue;
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var root = await syntaxTree.GetRootAsync();
            CollectBasesFromRoot(root, semanticModel, baseTypes);
        }
    }
    return baseTypes;
}
```

**Befund:** Iteriert durch **alle** SyntaxTrees in der Solution, nur um BaseTypes zu sammeln. Bei großen Solutions langsam. Roslyn bietet `ITypeSymbol.AllInterfaces` + `INamedTypeSymbol.BaseType` direkt; könnte selektiver sein.

### C8.2 — File-IO statt Roslyn-Workspace-Update

```csharp
// LinterAutoFixer.cs, Zeile 123
await File.WriteAllTextAsync(document.FilePath, newText.ToString(), newText.Encoding ?? Encoding.UTF8);
```

**Befund:** Schreibt direkt auf Disk statt `workspace.TryApplyChanges()` zu verwenden → kein atomares Update, keine Change-Tracking.

### C8.3 — Fix-Matching basiert auf `v.Details.Contains(...)`

```csharp
// LinterAutoFixer.cs, Zeile 295
return violations.Any(v => v.LineNumber == GetLineNumber(variable) && v.Details.Contains($"'{symbol.Name}'"));
```

**Befund:** Matching über String-Contains → spröde. Wenn der Violation-Detail-Text sich ändert, bricht der Fix. Besser: explizite `RuleName`-Filter.

---

## C9 — `ViolationTextFormatter.cs` (144 LOC)

**Datei:** `src/AiNetLinter/Output/ViolationTextFormatter.cs`

### C9.1 — `RuleInstructions` Dict mit 26 Einträgen

(siehe A3 — 3-fach-Duplikation)

### C9.2 — Fallback-Instruction für unbekannte Regeln

```csharp
// ViolationTextFormatter.cs, Zeilen 136–143
private static string GetRuleInstruction(string ruleName)
{
    if (RuleInstructions.TryGetValue(ruleName, out var instruction))
    {
        return instruction;
    }
    return $"-> {ruleName}: Bitte behebe diesen Verstoss gemaess den Richtlinien.";
}
```

**Befund:** Wenn eine neue Regel hinzukommt, fällt sie auf einen generischen Fallback zurück → LLM bekommt keine klare Anleitung.

### C9.3 — `BuildInstructionBlock` greift auf Filesystem zu

```csharp
// ViolationTextFormatter.cs, Zeilen 60–66
var cursorRulesPath = Path.Combine(projectRoot, ".cursor", "rules");
var claudeMdPath = Path.Combine(projectRoot, "CLAUDE.md");

if (Directory.Exists(cursorRulesPath))
    sb.Append("Projektkonfiguration erkannt: ...");
if (File.Exists(claudeMdPath))
    sb.Append("Projektkonfiguration erkannt: ...");
```

**Befund:** FS-Zugriffe in einer **reinen Format-Klasse** → Verletzung der SRP (Single Responsibility Principle). Format-Klasse sollte nicht über Filesystem wissen.

---

## C10 — `SourceFileCatalog.cs` (235 LOC)

**Datei:** `src/AiNetLinter/Baseline/SourceFileCatalog.cs`

### C10.1 — `IsGeneratedPath` dupliziert `FileFilters`

```csharp
// SourceFileCatalog.cs, Zeilen 192–198
private static bool IsGeneratedPath(string path)
{
    return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
           path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
           path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) ||
           path.EndsWith(".AssemblyAttributes.cs", StringComparison.OrdinalIgnoreCase);
}
```

**Befund:** Hardcoded Pfad-Filter, die `FileFilters.ExcludeDirectoryPatterns` und `ExcludeFilePatterns` aus `LinterConfig` duplizieren → **drift-anfällig**.

### C10.2 — Zwei Konstruktoren mit unterschiedlichen Feldern

(siehe A9)

### C10.3 — `LoadAsync` macht synchrones `Console.WriteLine`

```csharp
// SourceFileCatalog.cs, Zeile 52
foreach (var msg in diagnostics.Distinct(StringComparer.Ordinal))
{
    Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {msg}");
}
```

**Befund:** Logging direkt im Konstruktor → keine Trennung von Konstruktion und Reporting.

---

## C11 — `ArchitectureChecker.cs` (303 LOC)

**Datei:** `src/AiNetLinter/Core/Checkers/ArchitectureChecker.cs`

### C11.1 — Sammelt 10+ Verantwortlichkeiten

`ArchitectureChecker` enthält folgende Methoden:

- `CollectClassInfo` (Z. 16)
- `CheckSealedClass` (Z. 43)
- `CheckValueObjectContract` (Z. 60)
- `CheckForbiddenNamespace` (Z. 93)
- `CheckPhantomNamespace` (Z. 115)
- `CheckDynamic` (Z. 135)
- `CheckForbiddenSymbolNamespace` (Z. 150)
- `CheckPhantomReflection` (Z. 173)
- `IsGeneratedCode` (Z. 196)
- `IsSealedOrStaticOrAbstract` (Z. 212)
- `HasExemptSuffix` (Z. 215)
- `IsStructOrReadOnly` (Z. 222)
- `NamespaceMatches` (Z. 228)
- `GetInheritanceDepth` (Z. 239)
- `IsFrameworkBaseType` (Z. 252)
- `CheckForTestMethods` (Z. 268)
- `IsTestAttribute` (Z. 274)
- `GetBaseTypeNames` (Z. 286)

→ **18 Funktionen** in einem `static class`! Deutlich zu groß.

### C11.2 — `IsTestAttribute` hat hardcoded Test-Frameworks

```csharp
// ArchitectureChecker.cs, Zeilen 281–284
return ns.StartsWith("Xunit", StringComparison.OrdinalIgnoreCase)
    || ns.StartsWith("NUnit", StringComparison.OrdinalIgnoreCase)
    || ns.StartsWith("Microsoft.VisualStudio.TestTools.UnitTesting", StringComparison.OrdinalIgnoreCase);
```

**Befund:** 3 Frameworks hardcoded → MSTest nicht vollständig (`Microsoft.VisualStudio.TestPlatform`), kein Support für xUnit.v3 (`Xunit.v3`). Konfigurierbar wäre besser.

### C11.3 — `IsForbiddenReflectionCall` mit Hardcoded Strings

```csharp
// ArchitectureChecker.cs, Zeilen 205–210
private static bool IsForbiddenReflectionCall(string containingType, string methodName)
{
    if (containingType == "System.Type" && methodName == "GetType") return true;
    if (containingType.StartsWith("System.Reflection.Assembly") && (methodName.StartsWith("Load") || methodName.StartsWith("LoadFrom"))) return true;
    return containingType == "System.Activator" && methodName == "CreateInstance";
}
```

**Befund:** Hardcoded Liste → nicht erweiterbar.

### C11.4 — `CheckForbiddenSymbolNamespace` macht Tree-Walk

```csharp
// ArchitectureChecker.cs, Zeilen 150–171
internal static void CheckForbiddenSymbolNamespace(IdentifierNameSyntax node, CheckerContext ctx)
{
    SyntaxNode target = node;
    while (target.Parent is NameSyntax || target.Parent is MemberAccessExpressionSyntax)
        target = target.Parent;
    var symbol = ctx.SemanticModel.GetSymbolInfo(target).Symbol ?? ctx.SemanticModel.GetSymbolInfo(node).Symbol;
    // ...
}
```

**Befund:** Manueller Tree-Walk statt `GetSymbolInfo` direkt auf das `node` → Performance-Overhead.

---

## C12 — `Checkers/*` — wiederkehrende Anti-Patterns

### C12.1 — Alle Checker sind `internal static`

**Befund:** `ArchitectureChecker`, `NamingChecker`, `ComplexityChecker`, etc. — alle `internal static class` → Checker-Tests müssen `InternalsVisibleTo` nutzen (siehe `LinterEngine.cs:14`). Kein Hooking für Plugin-System.

### C12.2 — Magic-String-Matching für Exception-Typen

**Beispiel:** `ControlFlowChecker.cs:73–75`

```csharp
private static bool IsCancellationExceptionName(string? name) =>
    name == "OperationCanceledException" || name == "TaskCanceledException" ||
    name == "System.OperationCanceledException" || name == "System.Threading.Tasks.TaskCanceledException";
```

→ Hardcoded Strings.

### C12.3 — Regel-Verstoß-Generierung ist überall gleich

**Befund:** In jedem Checker:

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

Wiederholt sich 30+ Mal → könnte eine `ctx.ReportViolation(ruleName, line, details, guidance)`-Helper-Methode sein.

### C12.4 — `ForbiddenNames` Set in `NamingChecker` hardcoded

```csharp
// NamingChecker.cs, Zeile 15–18
private static readonly HashSet<string> ForbiddenNames = new(StringComparer.OrdinalIgnoreCase)
{
    "data", "temp", "obj", "val", "tmp", "item", "param"
};
```

**Befund:** Nicht via Konfiguration erweiterbar.

### C12.5 — `ScopeChecker.GetRelativePath` und `FindProjectDirectory` machen Disk-IO

```csharp
// ScopeChecker.cs, Zeilen 204–213
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

**Befund:** Dateisystem-Walk nach oben pro Datei → langsam bei tiefen Verzeichnissen. **Außerdem:** macht das für JEDE Datei.

---

## C14 — `Configuration/*` — Overrides

**Datei:** `src/AiNetLinter/Configuration/LinterConfigOverrides.cs`

### C14.1 — Overrides mit doppelter Property-Pflege

**Befund:** `GlobalConfigOverride` (266 LOC) dupliziert ~30 Properties aus `GlobalConfig`. Jede neue Property muss in **beiden** Klassen ergänzt werden → drift-anfällig.

### C14.2 — `MetricsConfigOverride` mit ~40 nullable Properties

**Befund:** Noch größer als `GlobalConfigOverride` → gleiches Problem wie C14.1.

### C14.3 — Reflection im `CursorRulesGenerator`

(siehe C7.3)

---

## C15 — Tests (`src/AiNetLinter.Tests`)

### C15.1 — Test-Pfad inkonsistent

(siehe A11)

### C15.2 — Kein Coverage-Reporting

(siehe A15)

### C15.3 — `ConsoleTestCollector` deutet auf Test-Pattern-Limitationen

**Befund:** Eine Datei namens `ConsoleTestCollector.cs` existiert → vermutlich Tests, die `Console.SetOut` mocken. Das deutet darauf hin, dass das `Console.WriteLine`-Anti-Pattern (siehe A8) bereits Tests **blockiert**.

### C15.4 — `xUnit v3` ist die Wahl, aber kein Mutation-Testing

**Befund:** xUnit v3 ist eine moderne Wahl. Kein Mutation-Testing (Stryker.NET o.ä.) → Coverage-Prozent sagt wenig über Test-Qualität aus.

---

## 🎯 Zusammenfassung Code-Qualität

Die Code-Basis ist **funktional korrekt**, aber **strukturell gewachsen** ohne Refactoring-Pausen. Hot-Spots:

| Datei                      | LOC | Verantwortlichkeiten                     | Risiko |
| -------------------------- | --- | ---------------------------------------- | ------ |
| `Program.cs`               | 568 | 8 Sub-Befehle, Profiler-Orchestrierung   | Hoch   |
| `LinterConfig.cs`          | 575 | 30+ Config-Properties + 3 Apply-Methoden | Mittel |
| `RepoPlaybookGenerator.cs` | 514 | Generierung + Walker + Stats + Bug-Werte | Mittel |
| `LinterEngine.cs`          | 360 | Orchestration + Cache + Profiler         | Mittel |
| `PerformanceProfiler.cs`   | 349 | Singleton + IO + Globals                 | Hoch   |
| `CursorRulesGenerator.cs`  | 335 | Regel-Liste + Reflection + MDC-Template  | Mittel |
| `LinterAutoFixer.cs`       | 322 | 3 Fix-Typen + BaseType-Scan + IO         | Mittel |
| `ArchitectureChecker.cs`   | 303 | **18** Funktionen                        | Hoch   |
| `LinterAnalyzer.cs`        | 271 | Visitor + 14 Checker-Aufrufe             | Hoch   |

→ Diese Top-9-Dateien machen ~3.500 LOC aus = **30%** des Produktions-Codes. Siehe `03-Architektur-Refactoring-Vorschlaege.md` für Refactoring-Vorschläge.
