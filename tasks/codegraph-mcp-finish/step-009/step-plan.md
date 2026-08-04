---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 009
title: "rules.json-Auto-Discovery (B.1) + Verzeichnis-Sweep für neue/gelöschte .cs-Dateien (B.2) — silent-falsche Tool-Antworten beheben (EPIC-04 / Muss-Haben B, Punkte 1-2)"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-04
related_to:
  - step-008/step-review.md  # EPIC-03 entkoppelt Config-Property strukturell, vereinfacht B.1
---

# Step 009: rules.json-Auto-Discovery + Verzeichnis-Sweep für neue/gelöschte .cs-Dateien

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-04` aus `roadmap.md` — Betriebsrisiko-Fixes, behebt
  zwei Klassen von silent-falschen Tool-Antworten, bevor die
  zeitbasierten Punkte (EPIC-05) angegangen werden.
- **Konzept-Referenz:** `konzept.md` „Muss-Haben B" Punkte **1**
  (Auto-Discovery) und **2** (Verzeichnis-Sweep), Zeile 188-217.
  Begründung der Reihenfolge: „Betriebsrisiko vor Komfort — Nutzer-
  Entscheidung: silent-falsche Tool-Antworten zuerst beheben" sowie
  DoD Zeile 650-653 („alle sieben Punkte aus Muss-Haben B sind
  umgesetzt, reviewt, mit Integrationstest abgesichert"; B.1 + B.2 sind
  die ersten beiden).
- **Reihenfolge:** direkt nach EPIC-03 (step-008, approved), vor
  EPIC-05/06/07/08. Die Position ist im Konzept wie im `roadmap.md`
  vorgegeben.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Stands direkt vor diesem Plan vorgefunden
(Stand 2026-08-04, nach step-008 `fd395c2`/`be6ff6a`):

1. **`McpServerCommand.RunAsync` (Z. 29-44):** Reihenfolge ist
   `ResolveSolutionPathOrError(args.TargetPath, c)` (Z. 32) →
   `TryLoadSolutionAsync(solutionPath, …)` (Z. 35) →
   `new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog, c,
   ResolveMaxLineCount(args), ResolveConfig(args)))` (Z. 36-37). Die
   Solution-Datei ist also Z. 32 bereits aufgelöst, wird aber
   weder in `ResolveMaxLineCount` (Z. 55-62) noch in `ResolveConfig`
   (Z. 73-80) für eine `rules.json`-Suche genutzt.

2. **`ResolveConfig` (Z. 73-80) — heutige Logik (verifiziert):**

   ```csharp
   internal static Config ResolveConfig(LinterArgs args)
   {
       if (string.IsNullOrWhiteSpace(args.ConfigPath))
           return new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

       return ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false)
           ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };
   }
   ```

   `args.ConfigPath` (`LinterArgs.cs:10`) wird nur gesetzt, wenn der
   Nutzer `args: ["--mcp-server", "--config", "…"]` an den Host übergibt.
   Die in `Docs/integration.md` Z. 232-238 dokumentierte
   Standard-Registrierung `args: ["--mcp-server"]` (ohne `--config`)
   führt daher **immer** zu `new Config { Global, Metrics }` — ohne
   `PathOverrides`, ohne `ProjectOverrides`, ohne `ForbiddenNamespace
   Dependencies`, ohne `RuleMetadata`-Anpassungen. Das ist exakt der
   konzeptuell beschriebene Fehlmodus: `get_violations` liefert
   irreführende Ergebnisse still, ohne jeden Hinweis im Tool-Output.

3. **`ResolveMaxLineCount` (Z. 55-62) — identisches Pattern,** gibt den
   `Metrics.MaxLineCount` der Default-`MetricsConfig` zurück, wenn kein
   `--config` gesetzt ist. `MaxLineCount` ist `int` → derselbe Bug an
   einer zweiten Stelle; Doppelung der Such-Logik ist in Kauf zu nehmen,
   eine `TryResolveRulesJsonPath`-Hilfsmethode behebt beides mit einem
   Aufruf.

4. **`ConfigLoader.TryLoadConfig(string?, bool)` (Z. 19-55):** ist
   strukturell passend — akzeptiert `null`, gibt bei nicht-existentem
   Pfad `null` zurück (mit `ConfigNotFound`-Stderr-Log), lädt und
   synct bei Erfolg via `ConfigSyncer.SyncIfNeeded`. Für B.1 nur als
   vorhandenes Rad wiederzuverwenden, kein Eingriff nötig.

5. **`McpCodeGraphServer.RefreshStaleDocuments` (Z. 121-140) — heutige
   Logik (verifiziert):**

   ```csharp
   private void RefreshStaleDocuments()
   {
       var solutionDir = Path.GetDirectoryName(_catalog!.Solution.FilePath);
       var updated = _catalog.Solution;
       var anyChanged = false;

       foreach (var project in _catalog.Solution.Projects)
       {
           foreach (var document in project.Documents)
           {
               if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
               if (TryRefreshDocument(document, ref updated)) anyChanged = true;
           }
       }

       if (anyChanged)
       {
           _catalog = _catalog.WithUpdatedSolution(updated);
       }
   }
   ```

   Iteriert **ausschließlich** über `project.Documents` (vom MSBuild-
   Workspace beim Solution-Load bekannte Dokumente). Eine während der
   Server-Session neu angelegte `.cs`-Datei ist bis zum nächsten
   Server-Neustart unsichtbar — der Agent fragt `find_symbol` und
   bekommt „keine Treffer" für tatsächlich existierenden, gerade
   erstellten Code (silent-falsch). `TryRefreshDocument` (Z. 142-154)
   skippt gelöschte Dateien stillschweigend (`if (!File.Exists(path))
   return false;` Z. 145), d. h. das Document bleibt im Solution-
   Workspace resident und liefert veraltete Treffer.

6. **Wiederverwendbare Strukturen (verifiziert, alle bereits
   `internal`):**
   - `SourceFileCatalog.IsValidDocument(Document, string?)` (Z. 145-153)
     — Filter: `.cs`-Endung, nicht-generiert (delegiert an
     `IsGeneratedPath`), unter `solutionDir`. `internal static`,
     direkt aufrufbar.
   - `SourceFileCatalog.IsGeneratedPath(string)` (Z. 215-221) —
     prüft `obj/`, `bin/`, `.g.cs`, `.AssemblyAttributes.cs`. Aktuell
     **`private static`** (Sichtbarkeits-Patch nötig, siehe
     Konkrete Änderungen Datei 1, Schritt 2).
   - `SourceFileCatalog.WithUpdatedSolution(Solution)` (Z. 66-69) —
     etablierte Mechanik zum Solution-Austausch; identische
     Verwendungsstelle ist heute schon Z. 138 in `RefreshStaleDocuments`.
   - `solutionDir` ist in `RefreshStaleDocuments` Z. 123 bereits
     berechnet (`Path.GetDirectoryName(_catalog.Solution.FilePath)`),
     kann in der neuen Sweep-Schleife ohne weitere Berechnung
     wiederverwendet werden.

7. **Schritt-Verzahnung mit step-008:** `McpCodeGraphServer.Config`
   ist seit step-008 vom Typ `ILinterEngineConfig` (Interface-Typ). B.1
   gibt eine `Config` (per `ConfigLoader` geladen oder `new Config{}`)
   in `McpCodeGraphServerOptions.From(...)` (Z. 42-55) — die
   Interface-Verschmälerung macht den `Config`-Typ-Wechsel an
   `RunAsync` Z. 36-37 strukturell trivial. Ohne step-008 wäre B.1
   ebenfalls machbar, aber der explizite Cast am `ResolveConfig`-
   Aufrufer wäre notwendig. **Bauen auf step-008, daher
   `related_to: [step-008]`.**

8. **Konzept-DoD-Vorgaben für B.1+B.2 (Konzept Zeile 196-217):**
   - B.1: „ohne `--config` neben der aufgelösten Solution-Datei nach
     `rules.json` suchen; keine gefunden → `[WARN]` auf stderr **und**
     Vermerk in der `get_violations`-Antwort selbst" → Default-Regeln
     + Tool-sichtbarer Header.
   - B.2: „zusätzlicher Verzeichnis-Sweep, der `.cs`-Dateien ohne
     zugehöriges `Document` einhängt und Dokumente ohne existierende
     Datei entfernt. **Bewusste Grenze:** `<Compile Remove=...>`-
     Ausschlüsse werden nicht erkannt."

9. **Tests-Landkarte (verifiziert, `Get-ChildItem`):**
   - `McpServerCommandTests.cs` Z. 292-348 — bereits Tests für
     `ResolveMaxLineCount` und `ResolveConfig` (jeweils `null`- und
     `ConfigPath`-Pfad). Tests sind **ohne** `Category`-Trait, müssen
     in `[Trait("Category", "Unit")]` umgewandelt werden (analog
     `McpServerCommandCacheBypassTests.cs:29`).
   - `McpServerCommandStalenessTests.cs` — bereits Staleness-Tests
     für modifizierte Dateien; analoges Pattern für die neue
     New-File/Deleted-File-Logik wiederverwendbar.
   - `McpLiveRepositoryTests` — Dogfooding gegen die eigene Solution,
     ideale End-zu-End-Bestätigung, dass die Auto-Discovery
     `C:\Daten\Entwicklung\Ralf\AiNetLinter\rules.json` tatsächlich
     findet (sonst weicht der Lint-Output von der Baseline ab → implizite
     Smoke-Test-Verifikation im Volllauf).

10. **TD-Index-Check:** Kein TD-Eintrag berührt B.1/B.2 inhaltlich
    (TD-005 ist Gate-Last, TD-006 ist `IsGeneratedPath`-Duplikation
    in `GetIndexScopeScanner`/`WebFileCatalog` — siehe Bekannte
    Ausnahmen).

## Intention

Nach diesem Step verhält sich der MCP-Server so, wie es der
Host-Standard-Registrierung in `Docs/integration.md` bereits
suggeriert: ohne expliziten `--config` sucht `McpServerCommand`
automatisch nach `rules.json` neben der aufgelösten Solution-Datei.
Wird keine gefunden, wird der `get_violations`-Antwort eine
sichtbare Header-Zeile `Basis: Default-Regeln, keine rules.json
gefunden` vorangestellt, parallel ein `[WARN]` auf stderr. Zusätzlich
erkennt `McpCodeGraphServer` ab dem nächsten Tool-Call automatisch
neu angelegte `.cs`-Dateien (Verzeichnis-Sweep hängt sie in die
Solution ein) und gelöschte Dateien (Document wird via
`Solution.RemoveDocument` aus dem Workspace entfernt). Beide Pfade
beheben die im Konzept beschriebenen Klassen von silent-falschen
Tool-Antworten strukturell, nicht per Disziplin.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Baseline/SourceFileCatalog.cs` (Z. 215-221)

- **Was:** Sichtbarkeit von `IsGeneratedPath` von `private static` auf
  **`internal static`** erweitern. **Eine** Zeile ändern, keine Logik-
  Änderung. Begründung in XML-Doc um den Hinweis ergänzen, dass die
  Methode auch von `McpCodeGraphServer` für den B.2-Sweep genutzt wird
  (nicht nur intern von `IsValidDocument`).
- **Warum:** Minimaler Sichtbarkeits-Patch, damit
  `McpCodeGraphServer.RefreshStaleDocuments` denselben
  Generated-File-Filter wiederverwendet, ohne ihn zu duplizieren
  (würde TD-006-Verschärfung bedeuten). Die volle TD-006-
  Konsolidierung (`GetIndexScopeScanner.cs`/`WebFileCatalog.cs`
  mitumziehen) bleibt **explizit EPIC-07** und ist nicht Scope dieses
  Steps (siehe Bekannte Ausnahmen).

### Datei 2: `src/AiNetLinter/Commands/McpServerCommand.cs`

- **Was:** Neue `internal static string? TryResolveRulesJsonPath(
  string? configPath, string solutionPath)`-Hilfsmethode anlegen
  (zwischen `ResolveMaxLineCount` und `ResolveConfig` einsortieren).
  Logik:
  1. `configPath` gesetzt und nicht leer → `configPath` zurückgeben
     (1:1-Übernahme des bisherigen Verhaltens, kein Disk-IO, keine
     Suche).
  2. Sonst: `Path.Combine(Path.GetDirectoryName(solutionPath)!,
     "rules.json")` bilden, `File.Exists` prüfen, bei Treffer
     zurückgeben, sonst `null`.
- `ResolveMaxLineCount` (Z. 55-62) und `ResolveConfig` (Z. 73-80)
  umstellen auf: einmaliger Aufruf von `TryResolveRulesJsonPath` (in
  `RunAsync` Z. 35 nach `TryLoadSolutionAsync`), das Ergebnis als
  zusätzlicher Parameter (`string? resolvedConfigPath`) in beide
  Methoden durchgereicht. Die Methoden behalten ihre Signatur
  (`internal static int ResolveMaxLineCount(LinterArgs args, string?
  resolvedConfigPath = null)` bzw. `internal static Config
  ResolveConfig(LinterArgs args, string? resolvedConfigPath = null)`),
  Aufrufer in Tests/anderen Stellen bleiben kompatibel.
- In `RunAsync` Z. 35-37 die Aufrufe entsprechend anpassen:
  `var resolvedConfigPath = TryResolveRulesJsonPath(args.ConfigPath,
  solutionPath);` direkt nach `ResolveSolutionPathOrError`. Falls
  `args.ConfigPath` gesetzt aber nicht existent → identisches
  `ConfigNotFound`-Verhalten wie bisher, aber jetzt
  zentral in `TryResolveRulesJsonPath`/`ConfigLoader.TryLoadConfig`.
- **Stderr-Warnung bei "nicht gefunden":** wenn
  `args.ConfigPath` leer UND `TryResolveRulesJsonPath` `null`
  liefert, einmalig `console.WriteError($"[WARN]: Keine
  rules.json neben der Solution gefunden ({Path.GetDirectoryName(
  solutionPath)}); get_violations laeuft mit Default-Regeln.")`
  ausgeben. Diese Warnung muss **vor** dem Server-Start erscheinen,
  damit sie nicht mit dem stdio-MCP-Verkehr kollidiert. Der
  `ILintConsole`-Kanal ist explizit stderr, also strukturell
  sauber.
- **Warum:** Erfüllt die Konzept-Vorgabe „ohne `--config` neben der
  aufgelösten Solution-Datei nach `rules.json` suchen; keine
  gefunden → `[WARN]` auf stderr" strukturell an **einer** Stelle
  (TryResolveRulesJsonPath), beide Aufrufer (ResolveConfig,
  ResolveMaxLineCount) bekommen das gleiche Verhalten ohne
  Coder-Drift.

### Datei 3: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`

- **Was (Eigenschaft):** Neue Property `public bool UsedDefaultConfig {
  get; }` anlegen, gesetzt im Konstruktor aus `options` (in
  `McpCodeGraphServerOptions.UsedDefaultConfig` durchgereicht).
  XML-Doc: „True, wenn `RunAsync` keine `rules.json` neben der
  aufgelösten Solution finden konnte und der Server mit der
  `Config`-Default-Konfiguration läuft. `get_violations` zeigt in
  diesem Fall eine sichtbare Header-Zeile an."
- **Was (RefreshStaleDocuments-Erweiterung):** `RefreshStaleDocuments`
  (Z. 121-140) um zwei Schritte erweitern — VOR dem bestehenden
  `project.Documents`-Loop:
  1. **Document-Removal:** Iteriere über alle `project.Documents`,
     prüfe `File.Exists(document.FilePath)`. Wenn false: `updated =
     updated.WithDocumentRemoved(document.Id)`, `_fileState.Remove(
     document.FilePath)`. Sammele entfernte Ids in einer
     `HashSet<DocumentId>`, damit der nachfolgende Sweep sie nicht
     versehentlich wieder einhängt.
  2. **New-File-Sweep:** `Directory.EnumerateFiles(solutionDir,
     "*.cs", SearchOption.AllDirectories)` (mit `try/catch
     UnauthorizedAccessException`/`IOException` per Datei, damit
     z. B. `node_modules/`-ähnliche Verzeichnisse nicht den ganzen
     Sweep killen). Pro Datei: `SourceFileCatalog.IsGeneratedPath
     (path)` → skip. Sonst: sammle alle existierenden
     `document.FilePath`-Strings in `solution.Projects[…].Documents`
     (Set, `StringComparer.OrdinalIgnoreCase`). Ist der neue
     Pfad nicht im Set: hinzufügen via `updated =
     updated.AddDocument(DocumentInfo.Create(documentId, filePath,
     text: SourceText.From(File.ReadAllText(path)),
     projectId: <erstes passendes Projekt>))`. Heuristik für die
     Projekt-Wahl: erstes Projekt, dessen `OutputFilePath`/`Path`
     denselben Parent-Pfad hat wie die neue Datei; Fallback: erstes
     nicht-Test-Projekt; letzter Fallback: erstes Projekt der
     Solution. Nach dem Hinzufügen: `_fileState[path] = new
     FileState(File.GetLastWriteTimeUtc(path),
     FileChecksumCalculator.ComputeSha256Hex(path))` über die
     vorhandene `TryCacheInitialFileState` (Z. 105-119) — Refactor
     dieser Methode auf `internal static` (Sichtbarkeits-Patch) und
     aus `RefreshStaleDocuments` aufrufen.
- Bestehender `project.Documents`-Loop bleibt **unverändert** für
  modifizierte Dateien (Hash-Vergleich wie heute, der findet die
  gerade hinzugefügten Dateien nicht, weil sie erst nach dem Loop
  im Solution-Modell sind — sie werden in einem zweiten
  inner-Loop-Durchgang verarbeitet, **nachdem** die Solution
  zugewiesen wurde; alternativ: vor dem Hinzufügen den
  `project.Documents`-Snapshot nehmen, dann in derselben
  Pass-Schleife `TryRefreshDocument` aufrufen — Code-Skizze
  unten).
- **Warum:** Erfüllt die Konzept-Vorgabe „zusätzlicher
  Verzeichnis-Sweep, der `.cs`-Dateien ohne zugehöriges `Document`
  einhängt und Dokumente ohne existierende Datei entfernt".
  Der Verzeichnis-Walk ist zunächst **naiv** (kein Directory-`mtime`-
  Cache) — die Optimierung folgt in EPIC-05 / B.5 (siehe
  Konzept: „kombinierbar mit Punkt 5"), bewusst out-of-scope
  dieses Steps.

### Datei 4: `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`

- **Was:** Neue Property `public bool UsedDefaultConfig { get; init; }`
  (= `false` Default). In `From(...)` (Z. 42-55) als
  optionaler Parameter `bool usedDefaultConfig = false` ergänzen,
  durchgereicht. XML-Doc: Verweis auf `McpCodeGraphServer.UsedDefault
  Config`.
- **Warum:** Strukturell saubere Durchreichung des
  Default-Config-Flags vom `McpServerCommand.RunAsync` (das die
  `rules.json`-Suche durchführt) bis zum `GetViolationsScanner`,
  der den Header in der Antwort ergänzt. Kein Service-Locator,
  keine globale Variable, keine Verzweigung im Scanner auf den
  Solution-Pfad.

### Datei 5: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (zweiter Patch-Punkt)

- **Was:** Im Konstruktor (Z. 33-45) `UsedDefaultConfig =
  options.UsedDefaultConfig;` zuweisen (analog zu `Config =
  options.Config;` Z. 39).
- **Warum:** Speichert das Flag im Server für Tool-Zugriff.

### Datei 6: `src/AiNetLinter/Commands/McpServerCommand.cs` (zweiter Patch-Punkt)

- **Was:** In `RunAsync` Z. 36-37 nach `TryResolveRulesJsonPath`:
  `using var mcpState = new McpCodeGraphServer(McpCodeGraphServer
  Options.From(catalog, c, ResolveMaxLineCount(args,
  resolvedConfigPath), ResolveConfig(args, resolvedConfigPath),
  usedDefaultConfig: resolvedConfigPath is null));`
  (Reihenfolge: `Config` muss vor `UsedDefaultConfig` ermittelt sein —
  wenn `ResolveConfig` eine `Config` aus dem Loader liefert, ist der
  `resolvedConfigPath` nicht null; wenn `TryResolveRulesJsonPath`
  `null` liefert UND `args.ConfigPath` leer war, dann Default.)
- **Warum:** `UsedDefaultConfig` ist eine reine Funktion des
  `resolvedConfigPath`-Werts und der expliziten `--config`-Angabe.

### Datei 7: `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs`

- **Was:** Signatur von `BuildViolationsTextAsync` (Z. 43-49) um
  `bool usedDefaultConfig` ergänzen. In `FormatReport` (Z. 105-140)
  **vor** der bestehenden Header-Zeile (`sb.AppendLine(
  $"Lint-Violations: {filtered.Count} Verstoesse in {fileCount}
  Dateien{scopeSuffix}");`) eine zusätzliche Zeile
  `if (usedDefaultConfig) sb.AppendLine("Basis: Default-Regeln,
  keine rules.json gefunden");` einfügen. Der bestehende Header
  bleibt semantisch unverändert, der neue Header ist additiv
  (1 Zeile).
- Im XML-Doc der Methode dokumentieren: „Wenn `usedDefaultConfig`
  true ist, wird der Antwort die Header-Zeile `Basis: Default-
  Regeln, keine rules.json gefunden` vorangestellt, damit der
  Agent-LLM erkennt, dass die Lint-Ergebnisse nicht aus der
  projekteigenen `rules.json` stammen."
- **Warum:** Erfüllt die Konzept-Vorgabe explizit („Vermerk in der
  `get_violations`-Antwort selbst"). Der Hinweis erscheint **nur in
  `get_violations`**, nicht in den anderen 8 Tools — der Konzept-
  Text nennt das Tool namentlich.

### Datei 8: `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs`

- **Was:** Im `ExecuteAsync`-Pfad `state.UsedDefaultConfig` an den
  Scanner durchreichen. Konkret: `GetViolationsScanner.BuildViolations
  TextAsync(…, usedDefaultConfig: state.UsedDefaultConfig, …)`.
- **Warum:** Verdrahtet das Flag vom `McpCodeGraphServer` zum
  Scanner. Eine Zeile.

### Datei 9: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** Drei neue Tests in der bestehenden Klasse (oder in
  `McpServerCommandAutoDiscoveryTests.cs` als separate Datei, je
  nach Coder-Präferenz), alle mit
  `[Trait("Category", "Unit")]`:
  1. `ResolveConfig_ExplicitConfigPath_TakesPrecedenceOverAuto
     Discovered` — Temp-Dir mit `.slnx` + `rules.json` (custom
     `MaxLineCount`); zusätzlich wird `args.ConfigPath` auf eine
     andere `rules.json` mit anderem `MaxLineCount` gesetzt;
     prüfen, dass die explizite Version gewinnt.
  2. `ResolveConfig_NoExplicitConfigPath_AutoDiscoversRulesJsonIn
     SolutionDirectory` — Temp-Dir mit `.slnx` + `rules.json`
     (custom `Metrics.MaxLineCount`); `args.ConfigPath = null`;
     `TryResolveRulesJsonPath(null, slnxPath)` aufrufen, prüfen
     dass der zurückgegebene Pfad der erwartete ist; anschließend
     `ResolveConfig` mit diesem Pfad aufrufen, prüfen dass
     `result.Metrics.MaxLineCount` dem der Test-`rules.json`
     entspricht.
  3. `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_LogsWarn
     AndUsesDefault` — Temp-Dir mit `.slnx` ohne `rules.json`;
     `args.ConfigPath = null`; prüfen dass `TryResolveRulesJsonPath`
     `null` liefert und `console.Errors` einen `[WARN]` enthält;
     prüfen dass `ResolveConfig` einen Default mit
     `new MetricsConfig().MaxLineCount` liefert.
- **Warum:** Auto-Discovery ist Verhalten, das ohne
  Regressions-Test gerne wieder verloren geht. Test-Granularität
  bewusst **Unit** (kein Subprozess, kein McpTestClient), da die
  reine Logik von `TryResolveRulesJsonPath`/`ResolveConfig` keinen
  End-to-End-Roundtrip braucht — der implizite Dogfooding-Lauf
  (`McpLiveRepositoryTests`) bestätigt das Verhalten gegen die
  echte AiNetLinter-Solution.

### Datei 10: `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerFileDiscoveryTests.cs` (NEU)

- **Was:** Neue Test-Datei, `[Trait("Category", "Unit")]`,
  IClassFixture mit einem `SymbolGraphMcpFixture` (oder einer
  schlanken `WorkspaceFixture`, die nur die Test-Solution lädt).
  Drei Tests:
  1. `GetCurrentSolution_NewFileAddedAfterStart_AppearsInSolution`
     — Solution laden, im Solution-Verzeichnis eine neue
     `MyNewClass.cs` mit bekanntem Klassennamen + einer
     `public class MyNewClass {}` anlegen,
     `server.GetCurrentSolution()` aufrufen, prüfen dass
     `solution.Projects[…].Documents` einen Eintrag mit
     `FilePath` = dem neuen Pfad enthält.
  2. `GetCurrentSolution_FileDeletedAfterStart_RemovedFromSolution`
     — eine in der Test-Solution existierende `.cs`-Datei löschen,
     `GetCurrentSolution` aufrufen, prüfen dass das entsprechende
     Document nicht mehr in der Solution enthalten ist
     (`solution.GetDocumentIdsWithFilePath(path)` liefert leere
     Sequenz).
  3. `GetCurrentSolution_GeneratedFile_NotAdded` — `obj/Foo.g.cs`
     anlegen, prüfen dass `GetCurrentSolution` sie **nicht** in
     die Solution einhängt (IsGeneratedPath-Filter).
- **Warum:** B.2 ändert das Verhalten von
  `GetCurrentSolution` messbar, ein Test verhindert, dass
  künftige Refactorings die neue Logik versehentlich
  zurücksetzen.

### Datei 11: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (zweiter Patch-Punkt)

- **Was:** Bestehende Tests für `ResolveMaxLineCount_ConfigWith
  CustomMaxLineCount_ReturnsConfiguredValue` (Z. 292) und
  `ResolveConfig_ConfigWithCustomMaxLineCount_UsesConfigFromArgs`
  (Z. 322) auf die neue Signatur `ResolveConfig(args,
  resolvedConfigPath)` anpassen (zweiter Parameter default `null`).
  Verhalten der bestehenden Tests bleibt identisch (explizites
  `args.ConfigPath` überschreibt Default), keine inhaltliche
  Test-Änderung außer der Parameter-Übergabe.
- **Warum:** Migrationsverifikation, dass die existierenden
  Aufrufer kompatibel bleiben. Wenn `MaxConstructorDependencies: 5`
  die Methoden-Signatur nicht blockiert (es ist nur ein zusätzlicher
  optionaler Parameter), passt der Default-Parameter die Tests ohne
  Inhaltsänderung an.

### Datei 12: `Docs/agent-api.md`

- **Was:** Im Abschnitt `get_violations` (Tool-Referenz) eine
  Klausel ergänzen: „Wenn der Server ohne `--config` gestartet
  wurde **und** keine `rules.json` neben der aufgelösten
  Solution-Datei findet, wird der Antwort die Header-Zeile
  `Basis: Default-Regeln, keine rules.json gefunden`
  vorangestellt. Beim Server-Start erscheint parallel ein
  `[WARN]: Keine rules.json neben der Solution gefunden …` auf
  stderr." Konkrete Empfehlung an den Agent-LLM: „Beim Auftauchen
  dieser Header-Zeile den Nutzer darauf hinweisen, dass die
  Lint-Ergebnisse nicht aus der projekteigenen `rules.json`
  stammen — entweder `args: ["--mcp-server", "--config",
  "<pfad>"]` setzen oder `rules.json` neben der Solution-Datei
  anlegen."
- Im Konfigurations-Abschnitt: Hinweis „MCP-Server sucht
  automatisch nach `rules.json` neben der aufgelösten
  Solution-Datei, wenn `--config` nicht gesetzt ist."
- **Warum:** Agent-Loop-Transparenz. Ohne diese Doku weiß der
  Agent-LLM nicht, dass die Header-Zeile eine Bedeutung hat.

### Datei 13: `Docs/integration.md`

- **Was:** Sektion „MCP-Server registrieren" (Z. 220-281) um eine
  kurze Konfigurations-Klausel erweitern: „`args: ["--mcp-server"]`
  ohne `--config` ist die empfohlene Registrierung. Der Server
  sucht automatisch nach `rules.json` neben der aufgelösten
  Solution-Datei. Wird keine gefunden, läuft er mit den
  Default-Regeln und signalisiert das in `get_violations`
  (siehe `agent-api.md#mcp-server-modus`)."
- Im Block „Mehrere parallele Server-Instanzen" (Z. 279-281) den
  Hinweis ergänzen, dass die Auto-Discovery pro Server-Prozess
  unabhängig vom `cwd` ist (sie läuft relativ zur aufgelösten
  Solution, nicht zum `cwd` des Host-Prozesses).
- **Warum:** Schließt die im Konzept genannte Doku-Lücke
  („`Docs/agent-api.md`, `Docs/integration.md`, `Docs/ROADMAP.md`
  Zeilen 478-493 von 'Geplant' auf den tatsächlichen Stand"),
  die zu jedem B-Schritt gehört.

### Datei 14: `Docs/ROADMAP.md` (Zeilen 478-493)

- **Was:** Den „Geplant"-Block für B.1 und B.2 auf „Umgesetzt →
  step-009" verschieben (analog dem EPIC-01/02/03-Stil oben im
  Dokument). B.3-B.7 bleiben unverändert.
- **Warum:** Konzept-DoD Zeile 659-661: „`Docs/ROADMAP.md` Zeilen
  478-493 sind von 'Geplant' auf den tatsächlichen Stand
  aktualisiert".

## Tests

- [ ] **Build grün mit 0 Warnungen** (Zero-Warning-Direktive,
      `AiNetLinterRichtlinien.mdc` §5) — `dotnet build
      AiNetLinter.slnx`.
- [ ] **Volllauf grün** (1186 oder begründete Abweichung bei
      TD-005-Last-Flake) — `dotnet test AiNetLinter.slnx --no-build`.
      Falls TD-005-Flake wieder auftritt: wie in step-007/fix-01
      und step-008 als **infrastructure** behandeln (kein
      Fix-Versuch, Scope-Drift vermeiden), im `step-result.md`
      unter „Bekannte Unschärfen" vermerken.
- [ ] **B.1-Unit-Tests** (Datei 9) — `dotnet test --filter
      "FullyQualifiedName~ResolveConfig" --filter "Category=Unit"`,
      3 neue Tests grün, 2 bestehende Tests grün nach
      Signatur-Anpassung (Datei 11).
- [ ] **B.2-Unit-Tests** (Datei 10) — `dotnet test --filter
      "FullyQualifiedName~McpCodeGraphServerFileDiscovery"`, 3 neue
      Tests grün.
- [ ] **Bestehende McpServerCommandTests grün** — Migrations-
      verifikation, dass die `TryResolveRulesJsonPath`-Umstellung
      keine bestehenden Aufrufer bricht.
- [ ] **McpLiveRepositoryTests grün** — implizite
      End-zu-End-Bestätigung, dass `rules.json` aus
      `C:\Daten\Entwicklung\Ralf\AiNetLinter\rules.json` via
      Auto-Discovery gefunden wird (sonst weicht `get_violations`
      von der Baseline ab).
- [ ] **Vor jedem Build/Test:** offene `AiNetLinter.exe`/
      `testhost.exe`-Prozesse prüfen und ggf. beenden
      (Konzept-Warnung, siehe `roadmap.md` Tech-Stack-Notiz).

## Definition of Done

- [ ] `SourceFileCatalog.IsGeneratedPath` ist `internal static`
      (Datei 1).
- [ ] `McpServerCommand.TryResolveRulesJsonPath(string?, string)`
      existiert und liefert konsistent für beide Aufrufer
      (`ResolveConfig`/`ResolveMaxLineCount`) den aufgelösten
      `rules.json`-Pfad (Datei 2).
- [ ] `McpServerCommand.RunAsync` ruft `TryResolveRulesJsonPath`
      einmalig auf, gibt eine `[WARN]`-Meldung auf stderr aus,
      wenn weder `--config` noch Auto-Discovery eine `rules.json`
      findet, und reicht das `usedDefaultConfig`-Flag an
      `McpCodeGraphServerOptions.From` weiter (Dateien 2, 6).
- [ ] `McpCodeGraphServer` hat die neue Property `UsedDefaultConfig`
      (Datei 3 Eigenschaft, Datei 5 Konstruktor).
- [ ] `McpCodeGraphServer.RefreshStaleDocuments` entfernt
      Dokumente ohne existierende Datei via
      `Solution.WithDocumentRemoved` und hängt neue `.cs`-Dateien
      via `Solution.AddDocument` ein, mit
      `IsGeneratedPath`-Filter (Datei 3 Erweiterung).
- [ ] `GetViolationsScanner.BuildViolationsTextAsync` ergänzt die
      Header-Zeile `Basis: Default-Regeln, keine rules.json
      gefunden`, wenn `usedDefaultConfig` true ist (Datei 7).
- [ ] `GetViolationsTool` reicht `state.UsedDefaultConfig` an den
      Scanner durch (Datei 8).
- [ ] 3 neue Unit-Tests in `McpServerCommandTests.cs` (oder
      neuer Test-Datei, Datei 9) für B.1-Auto-Discovery.
- [ ] 3 neue Unit-Tests in der neuen
      `McpCodeGraphServerFileDiscoveryTests.cs` für B.2-
      Verzeichnis-Sweep (Datei 10).
- [ ] 2 bestehende Tests in `McpServerCommandTests.cs` auf die
      neue `ResolveConfig(args, resolvedConfigPath)`-Signatur
      angepasst (Datei 11).
- [ ] `Docs/agent-api.md`, `Docs/integration.md`,
      `Docs/ROADMAP.md` aktualisiert (Dateien 12, 13, 14).
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün,
      Zero-Warning-Direktive eingehalten.
- [ ] Test-Command aus Tech-Stack-Notiz grün (1186 Tests oder
      begründete TD-005-Last-Flake-Abweichung).
- [ ] Code-Commit (Conventional Commit auf Deutsch, imperativ,
      Task-Suffix `[codegraph-mcp-finish]`): z. B.
      `fix(mcp): rules.json-auto-discovery und verzeichnis-sweep
      fuer neue-und-geloeschte-dateien [codegraph-mcp-finish]`.
- [ ] Doku-Commit: Status-Update dieses `step-plan.md` +
      `step-009/step-result.md`. Beispiel: `docs(task): step-009
      abgeschlossen [codegraph-mcp-finish]`.
- [ ] `step-009/step-result.md` geschrieben mit:
      - Vor-/Nachher-Verhalten für beide Auto-Discovery-Pfade
        (mit/ohne `rules.json` neben der Solution).
      - Vor-/Nachher-Verhalten für Verzeichnis-Sweep (Anzahl
        neu eingehängter Dateien, Anzahl entfernter Dateien im
        Smoke-Test-Fixture).
      - Begründung der Projekt-Wahl-Heuristik für B.2 (warum
        „erstes passendes Projekt", welche Edge-Cases traten im
        Test auf).
      - TD-005-Flake-Klassifikation, falls aufgetreten.
- [ ] **Kein Push** in diesem Step (Orchestrator-Konvention,
      lokale Commits nur).

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Grundprinzipien:
  monolithisch, statische Kompilierung, „Einfachheit vor Abstraktion")
  — `TryResolveRulesJsonPath` ist eine reine Hilfsmethode, keine
  neue Abstraktionsebene; der `Solution.AddDocument`-/`RemoveDocument`-
  Aufruf ist Roslyn-Standard-API.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §2 (Architektur-Verbote:
  „Kein DI-Container", „kein `AssemblyLoadContext`/Plugin-System")
  — B.1 ist eine reine zusätzliche Suche im bestehenden
  Pfad-Resolver-Pfad, B.2 ist eine Roslyn-Standard-API-
  Erweiterung. Kein DI, kein Plugin, keine Reflection-Loader.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 (Windows-Shell,
  Test-Logging, `latest.trx`, Prozess-Bereinigung) — vor jedem
  Build/Test offene `AiNetLinter.exe`/`testhost.exe`-Prozesse
  prüfen (Konzept-Warnung), Tests als PowerShell-Befehle.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (xUnit v3, MCP via
  C#-Testinfra, `Docs\ROADMAP.md`/`configuration.md` Update-Pflicht,
  Commit-Vorschlag-Pflicht) — `Category=Unit` für die neue Logik,
  E2E via `McpTestClient`/`McpLiveRepositoryTests` für die
  implizite Verifikation, Doku-Commits in `Docs/ROADMAP.md`
  Zeilen 478-493.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Zero-Warning,
  Result-Pattern, **Verbot von Task-/Planungsartefakt-Referenzen
  im Code**, Clean Code) — keine `step-NNN`/`TD-NNN`/`EPIC-NN`-
  Verweise in Code-Kommentaren; Kommentare beschreiben das **Was
  und Warum** der Code-Änderung, nicht den Task-Kontext.
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) Zeile 15/28
  (`MaxAIContextFootprint ≤ 2500`) — bei den neuen Test-Dateien
  prüfen, dass der Footprint nicht überschritten wird (typische
  Unit-Tests liegen unter 1500, kein Risiko).
- `.agents/rules/AiNetLinter.mdc` Zeile 144-147
  (`FootprintIgnoreTypeNames: ["LinterEngine", "NamingChecker"]`)
  — Vorlage für die Argumentation, warum
  `IsGeneratedPath` nur `internal static` wird (kein
  Whitelist-Workaround, sondern minimale Sichtbarkeits-Erweiterung).

## Bekannte Ausnahmen

- **TD-005 (Last-Flake in `McpServerCommandErrorHandlingTests`):**
  kann unter Volllauf-Last weiterhin 1-2 Failures am
  `SubprocessConcurrencyGate.AcquireAsync`-Timeout produzieren —
  ist in `tech-debt.md` dokumentiert, nicht Scope dieses Steps.
  Falls der Volllauf dadurch nicht grün wird: wie in
  step-007/fix-01 und step-008 mit dreimaligem Re-Run als
  **infrastructure** klassifizieren, nicht eigenhändig fixen.
- **TD-006 (`IsGeneratedPath`-Duplikation in `GetIndexScopeScanner
  .cs`/`WebFileCatalog.cs`):** nicht Scope dieses Steps, B.2 nutzt
  den bereits in `SourceFileCatalog` vorhandenen Filter via
  minimaler Sichtbarkeits-Erweiterung (`internal static`). Die
  vollständige DRY-Konsolidierung in eine gemeinsame Hilfsklasse
  (mit Umstellung der drei Aufrufer) bleibt **explizit EPIC-07** —
  kein Mitnahme-Refactor in diesem Step, um Scope-Drift zu
  vermeiden. Der Coder darf `IsGeneratedPath` nicht in eine
  neue Datei ziehen, ohne den Planer zu fragen.
- **B.2-Bewusste Grenze** (Konzept-Vorgabe): `<Compile Remove=…>`-
  Ausschlüsse aus `.csproj` werden **nicht** gelesen, neue Dateien
  landen im ersten passenden Projekt (Heuristik). Akzeptiert, im
  Konzept explizit so dokumentiert. Edge-Case: Datei außerhalb
  aller Projekt-Pfade → erstes Projekt der Solution, was zu
  Compile-Fehlern führen kann, wenn die Datei auf projekt-spezifische
  Namespaces/References zugreift. Der Coder soll in der
  Heuristik-Heuristik-Doku klar vermerken, dass dies eine
  „best-effort"-Sichtbarkeit ist, nicht eine Build-Korrektur.
- **B.2-Performance:** Verzeichnis-Sweep ist zunächst naiv
  (`Directory.EnumerateFiles(..., AllDirectories)` bei jedem
  `GetCurrentSolution`-Aufruf, kein Directory-`mtime`-Cache).
  EPIC-05 / B.5 wird das in einem Folge-Step optimieren — out-of-
  scope hier, im Konzept Zeile 238-244 explizit als eigenes
  „kombinierbar mit B.2-Sweep-Mechanismus" markiert.
- **Volllauf-Dauer:** durch den neuen Verzeichnis-Sweep kann der
  Volllauf minimal länger dauern als die in step-006 gemessenen
  ~1 m 35-40 s. Falls eine deutliche Verschlechterung eintritt
  (> +30 s): im `step-result.md` dokumentieren, **kein** sofortiger
  Optimierungs-Schritt, das ist EPIC-05-Scope.

## Code-Skizze (optional)

**B.1 — TryResolveRulesJsonPath (Datei 2):**

```csharp
/// <summary>
/// Loest den rules.json-Pfad auf: bei gesetztem <see cref="LinterArgs.ConfigPath"/>
/// wird dieser 1:1 zurueckgegeben (mit Existenzpruefung durch den Loader),
/// sonst wird neben der aufgeloesten Solution-Datei nach `rules.json` gesucht.
/// Liefert null, wenn weder explizit noch per Auto-Discovery ein Pfad gefunden
/// wurde — der Aufrufer faellt in diesem Fall auf die Config-Defaults zurueck
/// und signalisiert das per [WARN] auf stderr bzw. Header-Zeile in get_violations.
/// </summary>
internal static string? TryResolveRulesJsonPath(string? configPath, string solutionPath)
{
    if (!string.IsNullOrWhiteSpace(configPath))
    {
        return configPath;
    }

    var solutionDir = Path.GetDirectoryName(solutionPath);
    if (string.IsNullOrEmpty(solutionDir)) return null;

    var candidate = Path.Combine(solutionDir, "rules.json");
    return File.Exists(candidate) ? candidate : null;
}
```

**B.2 — RefreshStaleDocuments-Erweiterung (Datei 3, vereinfacht):**

```csharp
private void RefreshStaleDocuments()
{
    var solutionDir = Path.GetDirectoryName(_catalog!.Solution.FilePath);
    var updated = _catalog.Solution;
    var anyChanged = false;

    // (NEU) Schritt 1: gelöschte Dateien entfernen.
    var removedIds = new HashSet<DocumentId>();
    foreach (var project in _catalog.Solution.Projects)
    {
        foreach (var document in project.Documents)
        {
            if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
            if (File.Exists(document.FilePath!)) continue;

            updated = updated.WithDocumentRemoved(document.Id);
            _fileState.Remove(document.FilePath!);
            removedIds.Add(document.Id);
        }
    }

    // (NEU) Schritt 2: neue Dateien einhängen.
    if (!string.IsNullOrEmpty(solutionDir) && Directory.Exists(solutionDir))
    {
        var knownPaths = new HashSet<string>(
            updated.Projects.SelectMany(p => p.Documents)
                  .Where(d => d.FilePath != null)
                  .Select(d => d.FilePath!),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateCsFilesSafe(solutionDir))
        {
            if (SourceFileCatalog.IsGeneratedPath(path)) continue;
            if (knownPaths.Contains(path)) continue;

            var projectId = PickProjectForNewFile(updated, path)
                ?? updated.ProjectIds.FirstOrDefault();
            if (projectId is null) continue;

            try
            {
                var text = SourceText.From(File.ReadAllText(path));
                var docInfo = DocumentInfo.Create(
                    DocumentId.CreateNewId(projectId),
                    Path.GetFileName(path),
                    loader: new FileTextLoader(path, Encoding.UTF8),
                    filePath: path).WithText(text);

                updated = updated.AddDocument(docInfo);
                TryCacheInitialFileState(path);
                anyChanged = true;
            }
            catch (IOException ex)
            {
                _console.WriteError($"[WARN]: Neue Datei konnte nicht eingehängt werden ({path}): {ex.Message}");
            }
        }
    }

    // (BESTEHEND) Schritt 3: modifizierte Dateien.
    foreach (var project in updated.Projects)
    {
        foreach (var document in project.Documents)
        {
            if (removedIds.Contains(document.Id)) continue;
            if (!SourceFileCatalog.IsValidDocument(document, solutionDir)) continue;
            if (TryRefreshDocument(document, ref updated)) anyChanged = true;
        }
    }

    if (anyChanged)
    {
        _catalog = _catalog.WithUpdatedSolution(updated);
    }
}

private static IEnumerable<string> EnumerateCsFilesSafe(string solutionDir)
{
    IEnumerable<string> files;
    try
    {
        files = Directory.EnumerateFiles(solutionDir, "*.cs", SearchOption.AllDirectories);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        yield break;
    }
    foreach (var path in files) yield return path;
}

private static ProjectId? PickProjectForNewFile(Solution solution, string newFilePath)
{
    var dir = Path.GetDirectoryName(newFilePath);
    if (string.IsNullOrEmpty(dir)) return null;
    return solution.Projects
        .Where(p => !p.Name.Contains("Test", StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault(p => p.FilePath != null
            && Path.GetDirectoryName(p.FilePath) is { } pdir
            && dir.StartsWith(pdir, StringComparison.OrdinalIgnoreCase))
        ?.Id;
}
```

**B.1 — get_violations-Header (Datei 7):**

```csharp
// In FormatReport, vor dem bestehenden Header:
if (usedDefaultConfig)
{
    sb.AppendLine("Basis: Default-Regeln, keine rules.json gefunden");
    sb.AppendLine();
}

sb.AppendLine($"Lint-Violations: {filtered.Count} Verstoesse in {fileCount} Dateien{scopeSuffix}");
```

## Notes

- **Schritt-Größe:** 1 Step, ~14 Dateien Touch-Points (5 Produktion,
  2 Test, 3 Doku, 4 kleine Helper-/Verdrahtungs-Stellen), geschätzt
  250-400 Zeilen Diff inkl. Tests. Deutlich über dem 8-Item-Limit
  für `step_type: batch` (`max_batch_items: 8`,
  `max_batch_diff_lines: 40`) — also bewusst `step_type: single`.
- **Verzahnung mit step-008:** ohne `ILinterEngineConfig` (EPIC-03)
  wäre der `Config`-Typ-Wechsel in B.1 explizit notwendig gewesen
  (mehrere Downcasts an Aufruferstellen). Mit EPIC-03 ist die
  Verdrahtung strukturell sauber: `McpCodeGraphServerOptions.From`
  akzeptiert weiterhin `Config?`, intern wird die Interface-
  Verschmälerung transparent. Daher `related_to:
  [step-008/step-review.md]`.
- **B.1 + B.2 als ein Step:** technisch unabhängig (verschiedene
  Methoden, verschiedene Dateien), thematisch zusammengehörig
  (Konzept Zeile 188: „Betriebsrisiko vor Komfort — Nutzer-
  Entscheidung: silent-falsche Tool-Antworten zuerst beheben").
  Eine Trennung in zwei Steps wäre möglich, aber die Verzahnung
  ist konzeptuell (gleicher Lösungsraum), nicht technisch (kein
  Code-Sharing, keine gegenseitige Blockade). Konzept-Vorgabe
  „B.1 → B.2 → B.3" ist eine Prioritäten-Aussage, keine
  Pflicht-Trennung. Der Planer entscheidet sich für **einen**
  Step, weil (a) beide thematisch zusammengehören, (b) B.1 ist
  klein (eine Hilfsmethode + 2 Aufrufer-Änderungen) und B.2 ist
  bounded (eine Methode + 3 Tests), (c) ein zweiter Step mit nur
  B.2 hätte ein unverhältnismäßig kleines Code-/Test-Verhältnis
  und würde die `McpServerCommand`-Änderung aus B.1 zwei Mal
  durch den Review-Zyklus jagen.
- **Reihenfolge-Hinweis:** die DoD des Konzepts (Zeile 650-653)
  verlangt „alle sieben Punkte aus Muss-Haben B". Nach diesem
  Step sind B.1 + B.2 erledigt, B.3-B.7 bleiben für EPIC-05/06.
  Der nächste Planer-Roundtrip nach diesem Step wird EPIC-05
  angehen (B.3 Last-Fixture **vor** B.4/B.5, gemäß Konzept-
  Reihenfolge).
- **Commit-Strategie:** zwei lokale Commits in dieser Reihenfolge
  (gemäß `spec.md` §10.3):
  1. **Code-Commit** — die eigentlichen Änderungen in den 8
     Produktions-/Test-Dateien + 2 Test-Dateien. Conventional
     Commit auf Deutsch, imperativ, mit Task-Suffix
     `[codegraph-mcp-finish]`. Beispiel:
     `fix(mcp): rules.json-auto-discovery und verzeichnis-sweep
     fuer neue-und-geloeschte-dateien [codegraph-mcp-finish]`.
  2. **Doku-Commit** — Status-Update in diesem `step-plan.md`
     (von `open` auf `done (pending audit)`) +
     `step-009/step-result.md` + die drei `Docs/`-Aktualisierungen
     (in diesem Plan in Datei 12/13/14 separat aufgeführt — sie
     sind Bestandteil des Doku-Commits, nicht des Code-Commits,
     damit `git log --stat` sauber trennt).
- **Push:** keiner. Der Nutzer pusht selbst, gemäß `spec.md`
  §10.3 und Orchestrator-Konvention für diesen Task.
- **Bewusst NICHT in diesem Step** (zur Klarstellung gegen
  Scope-Drift-Versuchungen):
  - TD-006-Konsolidierung (`GetIndexScopeScanner`/`WebFileCatalog`
    mit-umziehen) — EPIC-07.
  - B.3-B.7 — EPIC-05/06.
  - B.5 Directory-`mtime`-Cache für B.2 — explizit EPIC-05, im
    Konzept Zeile 238-244 als „kombinierbar mit B.2-Sweep-
    Mechanismus" markiert, aber als eigener Schritt geplant.
  - Heuristik-Verbesserung der B.2-Projekt-Wahl — out-of-scope,
    Heuristik reicht für den im Konzept beschriebenen
    „best-effort"-Anspruch.
