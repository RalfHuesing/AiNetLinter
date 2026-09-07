# Finding: `metrics_tree`

Audit-Stand: 2026-09-07. Nur Schema-Lookup (`GetDynamicTools`) plus Calls dieses Tools. Kein anderes MCP-Tool, kein Build, kein Test.

## 1 Schema-Kurzfazit

Beschreibung steuert den Call **richtig**: Verzeichnisbaum Ebene für Ebene, aggregierte Werte plus Top-N-Kinder. Pflicht `targetType` + absoluter `targetPath`. Cross-Target laut Beschreibung (`project` und `assembly`) trifft live zu.

Optionale Parameter stehen in Beschreibung und Schema: `mode` (`code_size` Default, `comment_density`, `violation_density`, `complexity`), `root` (Teilbaum), `depth` (1–5, Default 1), `topN` (Default 10), `fileFilter` (Regex auf den Pfad). Folgeschritt ist in der Antwort erklärt: `root` auf einen Kind-Pfad setzen und/oder `depth` erhöhen.

JSON-Schema schwächer als die Prosa: `targetType`/`mode` ohne Enum, `depth`/`topN` ohne min/max. Live-Validierung existiert trotzdem (`depth` 1–5, `topN` ≥ 1). `fileFilter` heißt Regex, akzeptiert aber glob-ähnliches `*Handler*` ohne Fehler (gleicher Trefferraum wie Substring `Handler`).

Antwort ist ein ASCII-Baum plus fester Truncation-Hinweis. Keine Symbol-IDs. Assembly-Antworten tragen Session-Header (`origin=decompiled`, `completeness=partial`, `status=partial`).

## 2 Calls

Projektroot: `C:\Workspace\Sample.Project`. Assembly: `C:\ExternalAssemblies\Version-9\Example.External.Core.dll`. Alle Calls **schnell** (kein Timeout).

| # | Absicht | Argumente | Größe / Completeness | Ergebnis |
|---|---|---|---|---|
| 1 | Happy Path Projekt | `project` + Root, Defaults | ~15 Zeilen, ~1,2k Zeichen; Hint „nicht vollständig“ | 2287 Dateien, 264.946 LoC, 10,1 MB. Top: Platform 852 / 80.422, Tests.Logic 452 / 66.493. `… und 5 weitere`. |
| 2 | Happy Path Assembly | `assembly` + extern-Core.dll, Defaults | ~20 Zeilen inkl. Header; `completeness=partial` | 198 Dateien, 17.201 LoC. Flache Dateiliste (decompiled). Top: `KombinationenBase.cs` 1073 LoC. `… und 187 weitere`. |
| 3 | `mode=complexity` | `depth=2`, `topN=5` | ~45 Zeilen | Root Ø CC 2.0, max CC 21, max CogC 26. Sortierung nach Ø CC, nicht LoC. Platform Ø 3.0 vor Tests. |
| 4 | `fileFilter=\.cs$` | `depth=2`, `topN=8` | ~70 Zeilen | **Gleiche 2287 Dateien** wie Default — Index ist bereits C#-Dokumente (`*.cs` inkl. `*.razor.cs`). |
| 5 | Leer, falscher `root` | `mode=comment_density`, `root=…Domain.Schedule` | 1 Zeile | `Keine Dateien unter root='…Domain.Schedule' — Pfad/Filter pruefen.` Ordner existiert nicht (`EmployeeSchedule` schon). |
| 6 | `mode=violation_density` | `depth=2`, `topN=6` | ~45 Zeilen | 1 Violation (0 Fehler, 1 Warnung) in `Setup/Setup`. Kinder mit 0 Violations füllen Top-N; die treffende Datei ist auf Ebene 2 unsichtbar. |
| 7 | `root` Präfix + Filter | `root=Sample.Project`, `fileFilter=Handler`, `depth=2`, `topN=5` | ~40 Zeilen | 680 Dateien / 90.772 LoC — **gleicher Umfang wie Filter ohne `root`**. Geschwister `Tests.Logic`, `Tests.Ama` erscheinen, Prefix ohne Pfadtrenner. |
| 8 | Truncation `depth=5` | `topN=3` | **groß** (~200+ Zeilen, Token-Last) | Tiefer Baum bis Dateien (`SchedulerBindings.cs` 600 LoC). Hint: weiter `depth` erhöhen — würde noch größer. |
| 9 | Assembly `complexity` | `depth=2`, `topN=5` | ~20 Zeilen, `completeness=partial` | 196 Dateien, Ø CC 3.5, max CC **226**, max CogC 253. `Encryption.cs` Ø 58.6 / max 226. |
| 10 | Leer, Filter | `fileFilter=ZZZNoSuchFilter_xyz123` | 1 Zeile | `Keine Dateien unter root='' mit file_filter`. Klar, kein leerer Baum. |
| 11 | Fehler, kein Projekt | `project`, `C:\Workspace\DoesNotExist` | ~12 Zeilen | `PROJECT_NOT_INITIALIZED` + JSON-Vorlage für `ainetlinter.project.json`. |
| 12 | Relativer Pfad | `targetPath=Sample.Project` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss absolut sein. |
| 13 | `depth=0` | Defaults sonst | ~3 Zeilen | `INVALID_ARGUMENT`: depth 1–5. |
| 14 | `depth=6` | `topN=2` | ~3 Zeilen | `INVALID_ARGUMENT`: depth 1–5. Kein stilles Clamping. |
| 15 | Fehlende DLL | `assembly` + `DoesNotExist.dll` | ~3 Zeilen | `INVALID_ARGUMENT`: Pfad muss existierende `.dll`/`.exe` sein. |
| 16 | `comment_density` gültig | `root=…Domain.EmployeeSchedule`, `topN=8` | ~15 Zeilen | 89 Dateien, 4 % (311/7.399). Sortierung: wenig Kommentare zuerst. Nenner ≠ LoC aus Call 1 (8.578). |
| 17 | Assembly `fileFilter` | `fileFilter=Encryption`, `depth=2` | ~12 Zeilen | 1 Datei, `Encryption.cs` 579 LoC. Trotzdem Hint „nicht vollständig“. |
| 18 | Truncation `topN=1` | Defaults | ~8 Zeilen | 1 Kind (Platform) + `… und 14 weitere`. Hint korrekt sinnvoll. |
| 19 | Drill `violation_density` | `root=…Setup/Setup`, `topN=15` | ~25 Zeilen | Trefferdatei `SetupText.cs` (1 Warnung) steht oben; Rest 0. |
| 20 | `fileFilter=\.razor` | Defaults | ~8 Zeilen | 103 Dateien / 8.779 LoC — trifft **`.razor.cs`**, nicht Markup. |
| 21 | Assembly `violation_density` | `topN=5` | ~20 Zeilen, `completeness=partial` | **326 Violations** (221 Fehler, 105 Warnungen) auf dekompilierter DLL. Top: `KombinationenBase.cs` 38. |
| 22 | `root` Projektordner | `root=…Tests.Logic`, `topN=5` | ~12 Zeilen | 452 Dateien / 66.493 LoC — deckungsgleich mit Kind aus Call 1. |
| 23 | `root` Slash-Pfad | `root=Sample.Project/Handlers`, `depth=2`, `topN=4` | ~30 Zeilen | 233 Dateien / 25.657 LoC. Echter Teilbaum, keine Geschwister-Leaks. |
| 24 | `topN=0` | — | ~3 Zeilen | `INVALID_ARGUMENT`: `top_n` mindestens 1. Kein stilles Default. |
| 25 | Filter ohne `root` | `fileFilter=Handler`, `topN=5` | ~12 Zeilen | 680 / 90.772 — identisch zu Call 7. Recordt den Präfix-Leak. |
| 26 | `fileFilter=\.razor$` | — | 1 Zeile | Leer. Razor-Markup ist nicht im Index. |
| 27 | `fileFilter=\.js$` | — | 1 Zeile | Leer. JS/`wwwroot` nicht im Index. |
| 28 | `topN=50` voll | `depth=1` | ~20 Zeilen | Alle **15** Solution-Projekte, Summe 2287 Dateien. Trotzdem Hint „nicht vollständig“. |
| 29 | Assembly `comment_density` | `topN=5` | ~20 Zeilen | 198 Dateien, 0 % (29/15.367). Dekompilat fast kommentarlos. Nenner ≠ 17.201 LoC. |
| 30 | Assembly `root=Encryption.cs` | — | ~10 Zeilen | Blattknoten, 579 LoC, keine Kinder. Hint trotzdem „nicht vollständig“. |
| 31 | `topN=-5` | — | ~3 Zeilen | `INVALID_ARGUMENT` wie Call 24. |
| 32 | Glob-ähnlicher Filter | `fileFilter=*Handler*`, `topN=3` | ~10 Zeilen | Wieder 680 Dateien — `*` wird wie Wildcard akzeptiert, kein Regex-Fehler. |
| 33 | `assembly` auf Ordner | `targetPath`=Projektroot | ~3 Zeilen | `INVALID_ARGUMENT`: muss Datei `.dll`/`.exe` sein. |
| 34 | `fileFilter=wwwroot` | — | 1 Zeile | Leer. |
| 35 | Complexity-Drill | `root=…/Components/UI/Scheduler`, `mode=complexity`, `depth=3`, `topN=5` | ~15 Zeilen | 167 Dateien, Ø CC 2.8, max 12 / CogC 15. Größe-Hotspot `SchedulerBindings.cs` nicht in Top-N (andere Metrik). |
| 36 | Leerer `fileFilter` | `fileFilter=""` | wie Call 1 | Wie Default, kein Fehler. |
| 37 | Ungültiges `targetType` | `bogus` | — | Call vom Auto-Review blockiert; Retry/Freigabe fehlgeschlagen. Nicht live gesehen. |
| 38 | Ungültiges `mode` | `not_a_mode` | — | Ebenso blockiert, nicht live gesehen. |

## 3 Verdict

**Teilweise wie beschrieben.** Happy Path Projekt/Assembly, alle vier Modi, Slash-`root`, eindeutige Ordnernamen, `fileFilter`-Leermenge, harte Bounds für `depth`/`topN` und Assembly-Dateipfad stimmen. Default-Antwort ist kompakt und zum Drilldown geeignet.

Abweichungen: Truncation-Hinweis behauptet **immer** Unvollständigkeit, auch wenn alle Kinder oder ein einziges Blatt sichtbar sind. `root=Sample.Project` ist ein **String-Präfix** und zieht `Sample.Project.Tests.*` u. a. mit. `fileFilter` ist Regex-plus-Glob, Schema sagt nur Regex. Razor-Markup und JS fehlen ohne Hinweis in der Toolbeschreibung. `violation_density` auf der externe Assembly wendet Projektregeln auf Dekompilat an (hunderte Treffer). JSON-Schema ohne Enums/Bounds.

## 4 Schwere

**`friction`**

Kernnutzen (wo ist die Codebase groß / komplex, dann `root` setzen) ist korrekt und schnell. Reibung: Schema vs. Prosa, Dauerhinweis „nicht vollständig“, `root`-Präfix ohne `/`, fehlende Nicht-C#-Dateien, irreführende Assembly-Violations. Nicht `broken` (keine falschen LoC in der Stichprobe). Nicht `degraded` in der Default-Nutzung (depth 1, topN 10, ~1k Zeichen). Token-Risiko nur bei bewusst `depth=5`.

## 5 Nutzbarkeit

**ja** — erste Orientierung einer unbekannten Solution und Drill in einen Ordner (`Handlers`, Domain-Vertical). Assembly-`code_size`/`complexity` ebenfalls ja, um große dekompilierte Typen zu finden.

Workaround: `root` mit Slash (`Sample.Project/Handlers`) oder eindeutigem Projektordner (`…Tests.Logic`); Hint nur glauben, wenn `… und N weitere` steht; `violation_density` nicht auf externe Assemblies; für Markup/JS anderes Tool.

## 6 Bugs+FP/FN

| Befund | Art | Call |
|---|---|---|
| Hinweis immer „Top-N-Ausschnitt, nicht vollständig“, auch bei voller Kinderliste oder 1-Datei-Blatt | Falsche Completeness | 17, 28, 30 |
| `root=Sample.Project` matcht alle Pfade mit diesem Präfix inkl. Geschwisterprojekte | Scope-FP | 7 vs. 23, 25 |
| `fileFilter=\.razor` trifft `.razor.cs`, `\.razor$` ist leer | FP vs. Agent-Intent „Razor“ | 20 vs. 26 |
| `*.js` / `wwwroot` leer, Beschreibung sagt „Codebase“ ohne C#-Grenze | FN für JS | 27, 34 |
| Assembly `violation_density`: 326 Violations auf Dekompilat vs. 1 im Platform-Projekt | FP relativ zu „echte Projekt-Schulden“ | 21 vs. 6 |
| `*Handler*` = 680 wie Substring, kein Regex-Parsefehler | Undokumentiertes Glob | 32 vs. 25 |
| Parent-max CC 21 (Call 3) in keinem sichtbaren Top-5-Kind | Metrik ohne Zeiger auf den Träger | 3 |
| comment_density-Nenner ≠ code_size-LoC (Schedule 7399 vs. 8578; Assembly 15367 vs. 17201) | Metrik-Inkonsistenz | 16, 29 vs. 1, 2 |

**Stichprobe LoC:** `SchedulerBindings.cs` — Tool (Call 8): 600 LoC. Bekannter `MaxLineCount`-Hotspot. `Handlers` 233 Dateien / 25.657 LoC über Calls 1/23/4 identisch. Solution-Summe Call 28: 15 Projekte = 2287 Dateien, identisch zum Root. **Kein LoC-False-Positive** in der Stichprobe.

**Stichprobe Komplexität:** `Encryption.cs` max CC 226 auf Dekompilat ist plausibel (große generierte/entpackte Methoden), nicht als Platform-Schuld lesbar. `SchedulerBindings.cs` erscheint unter `code_size`, nicht unter Complexity-Top-N — Metrikwechsel, kein FN der Komplexität.

Leermenge (Calls 5, 10, 26, 27, 34) ist klar, kein 200-mit-leerem-Baum.

## 7 Token/IDs

Default (Call 1): kompakt, agententauglich. Call 8 (`depth=5`, `topN=3`): große Nested-Liste, unnötig für den dokumentierten Zweck „Ebene für Ebene“.

Keine Symbol-IDs, kein Cursor/`continuationToken`. Folge-Call über **Pfadstrings** in `root` (Kindname bzw. `Parent/Kind`). StructuredContent nicht sichtbar; der Hint ist der einzige Next-Step. Assembly-Header (`generatedPath`, `generation`) ist für Session-Nachvollzug nützlich, nicht für Symbol-Navigation.

## 8 Roslyn-konforme Wünsche

- JSON-Schema: Enums für `targetType` und `mode`; `depth` 1–5, `topN` ≥ 1 als Bounds.
- Completeness-Hint nur wenn wirklich Kinder abgeschnitten sind (`… und N weitere` oder depth-Cap).
- `root` pfadsegment-scharf: `Sample.Project` nicht `Sample.Project.Tests.Logic` matchen, außer mit `/` oder als existierender Relativordner.
- `fileFilter`: Glob vs. Regex in Beschreibung; optional `scopeType` production/tests analog anderer Tools.
- Toolbeschreibung: Index = Roslyn-C#-Dokumente, nicht Razor-Markup/JS.
- `violation_density` auf `origin=decompiled`: Regeln nicht anwenden oder Ergebnis als „Regeln vs. Dekompilat, nicht Autoren-Schuld“ kennzeichnen.
- Dateiknoten mit stabiler Datei-/Symbol-ID für Folge `get_file_skeleton` / `get_violations` / `metrics_lookup`.
- Aggregiertes max CC/CogC am Parent mit Pfad des Träger-Kinds, auch wenn es außerhalb von `topN` liegt.

## 9 Phase 3

Quelle: `C:\Daten\Entwicklung\Ralf\AiNetLinter`. Kein Patch in diesem Task.

### Completeness-Hint immer „nicht vollständig“ (Calls 17, 28, 30) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\McpDrillDownHints.cs` (Aufruf in `MetricsTreeTool.ExecuteAsync`)
- **Symbol:** `McpDrillDownHints.Append` (Kommentar: Output sei per Definition nie vollständig)
- **Ansatz:** Hint nur wenn `MetricsTreeRenderer` tatsächlich `… und N weitere` schreibt **oder** `level >= depth` Kinder abschneidet. Dafür Truncation-Flag aus `RenderChildren` (`sorted.Count > visible.Count`) bzw. `BuildNode` (`level >= depth` bei noch Restsegmenten) an den Tool-Layer reichen. Kein Roslyn.

### `root`-Präfix ohne Pfadtrenner (Calls 7 vs. 23, 25) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeScanner.cs` (Kopie in `MetricsTreeRoslynScanner.BuildTreeResultAsync`)
- **Symbol:** `BuildTreeResult`: `RelativePath.StartsWith(rootRelative)`; `GetRemainder` nimmt `prefixLen = root.Length + 1` (verschluckt den Punkt in `.Tests.`)
- **Ansatz:** Wie `SearchPatternScanner.MatchesScope`: `== root` oder `StartsWith(root + "/")`. `NormalizeRoot` unverändert. Dann matcht `Sample.Project` nicht `….Tests.Logic`.

### `fileFilter=\.razor` trifft `.razor.cs` (Calls 20 vs. 26) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeTool.cs` → `RegexAutoDetector.TryCreateFilterRegex`; Walk: `SolutionFileWalker.CollectFiles`
- **Symbol:** `TryBuildFileFilter` / `SolutionFileWalker.CollectFiles` (`fileFilter.IsMatch(relativePath)`)
- **Ansatz:** Unverankerte Regex ist Substring. Beschreibung: Glob vs. Regex. Optional Globs mit `$`-Anker (`ConvertWildcardToRegex(..., anchored: true)` tut das schon für `*`). Markup/JS fehlen sowieso (nächster Punkt).

### Razor-Markup / JS nicht im Index (Calls 26, 27, 34) — `friction`

- **Pfad:** `src\AiNetLinter\Baseline\SourceFileCatalog.cs` und `src\AiNetLinter\Mcp\Tools\FileStructure\SolutionFileWalker.cs`
- **Symbol:** `SourceFileCatalog.IsValidDocument` (`path.EndsWith(".cs")`); `SolutionFileWalker.CollectFiles` nur `project.Documents`
- **Ansatz:** Index = Roslyn-C#-Dokumente, nicht AdditionalDocuments/wwwroot. In `MetricsTreeDescription` (`AnalysisToolRegistrations.MetricsTreeDescription`) C#-Grenze nennen. Markup/JS bleibt `search_pattern`/`get_file_tree`. Kein zweiter Filesystem-Walk in diesem Tool nötig.

### Assembly `violation_density` auf Dekompilat (Call 21) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeRoslynScanner.cs`
- **Symbol:** `ComputeViolationDensityMetricsAsync` (`new LinterEngine` + `RunAsync` auf der Session-`Solution`)
- **Ansatz:** Vor dem Lint `TestDetector.IsDecompiledAssemblyProject` (bereits vorhanden). Dann Modus ablehnen oder Ergebnis als „Regeln vs. Dekompilat, nicht Autoren-Schuld“ kennzeichnen (`AssemblyAnalysisResponse` setzt `origin=decompiled` schon im Envelope). Complexity/`code_size` dürfen bleiben (`ComplexityCalculator` auf `MethodDeclarationSyntax`).

### `*Handler*` = Substring-Glob, Schema sagt Regex (Call 32) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\Common\RegexAutoDetector.cs`
- **Symbol:** `TryCreateFilterRegex` (Glob `*`/`?` → `ConvertWildcardToRegex`, sonst Raw-Regex)
- **Ansatz:** Beschreibung `fileFilter: Regex-Filter` in `MetricsTreeDescription` um Glob-Zweig ergänzen. Verhalten belassen oder bei reinem Substring ohne Regex-Metazeichen `INVALID_ARGUMENT`. Kein Roslyn.

### Parent-max-CC ohne Träger-Pfad (Call 3) — `wish`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeScanner.cs` / `MetricsTreeRoslynScanner.cs`
- **Symbol:** `AggregateWithChildren` (`Max(c => c.MaxCyclomatic)` ohne Herkunft); `ComputeFileComplexityMetricAsync` (`ComplexityCalculator.GetCyclomaticComplexity` / `GetCognitiveComplexity` auf `MethodDeclarationSyntax`)
- **Ansatz:** Am `BuilderNode` den `RelativePath` des Max-CC-Kinds mitführen (beim `Math.Max` den Pfad übernehmen). In `FormatDisplayLine` ausgeben, auch wenn das Kind außerhalb `topN` liegt. Stabiles DocComment-ID optional über `SemanticModel.GetDeclaredSymbol(method)`.

### comment_density-Nenner ≠ code_size-LoC (Calls 16, 29) — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeScanner.cs`
- **Symbol:** `ComputeCodeSizeMetric` (`lines.Length`); `CountCommentLines` (Leerzeilen weder Code noch Kommentar)
- **Ansatz:** Nenner dokumentieren oder code_size auf Nicht-Leerzeilen stellen, damit beide Modi denselben Zähler teilen. Heuristik, kein Tokenizer.

### JSON-Schema ohne Enums/Bounds — `friction`

- **Pfad:** `src\AiNetLinter\Mcp\Registration\AnalysisToolRegistrations.cs`
- **Symbol:** `AddMetricsTree`-Lambda (`string? mode`, `int depth`, `int topN`)
- **Ansatz:** Runtime validiert bereits (`MetricsTreeModeParser.TryParse`, `depth` 1–5, `topN ≥ 1`). Schema an die Signatur koppeln (Enum `mode`). Kein Roslyn.

### Dateiknoten ohne Symbol-/Datei-ID — `wish`

- **Pfad:** `src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeModels.cs`
- **Symbol:** `MetricsTreeNode` (Name/RelativePath/DisplayLine)
- **Ansatz:** `RelativePath` ist schon Folgeschlüssel für `root=`. Optional DocComment-ID des ersten Typs in der Datei via `SemanticModel` + `DescendantNodes().OfType<BaseTypeDeclarationSyntax>()`.
