# AiNetLinter-MCP-Findings

Append-only. Kurz, aber später behebbar: Tool, Repro-Parameter, erwartet vs. Ist, Auswirkung. Keine Secrets, keine vollen Tool-Transkripte.

## 2026-09-05 — Orchestrator, tasks/setup (Planungsphase)

### 1. `find_symbol` + `kind: Class` übersieht Records

- **Tool:** `find_symbol`
- **Parameter:** `targetType=project`, `targetPath=<repo-root>`, `kind=Class`, `namePatterns` inkl. `SqlPackDefinition`
- **Erwartet:** Treffer auf `San.smart.Planner.Platform.Setup/Engine/Sql/SqlPackDefinition.cs` oder klarer Hinweis „Record, Kind-Filter ausschließend“.
- **Ist:** „Keine Treffer … (Kind-Filter: Class)“ plus „nicht Teil des Symbolgraphs“. Datei existiert (172 B). Ohne `kind`-Filter: `record SqlPackDefinition`, DocCommentId `T:San.smart.Planner.Platform.Setup.Engine.Sql.SqlPackDefinition`.
- **Auswirkung:** Agenten halten den Typ für abwesend und planen unnötige Fallbacks (`search_pattern` / Dateilesen).
- **Wunsch:** `kind=Class` Records einschließen **oder** explizit „0 wegen Kind-Filter, Record in Datei X“ statt „nicht im Symbolgraph“.

### 2. `get_feature_context` Test-Zuordnung verfehlt echte Testdateien

- **Tool:** `get_feature_context`
- **Fälle:**
  1. `T:San.smart.Planner.Platform.Setup.Engine.Sql.SetupSqlPackSelector` — Callers zeigen `SetupFeatureAndConnectionTests.cs`, Abschnitt Test-Kontext: „0 Testdateien, 0 Tests“.
  2. `T:San.smart.Planner.Platform.Setup.Engine.Sql.SqlPackRunner` — Callers zeigen `SqlGoAndJournalTests.cs`; Test-Kontext bindet stattdessen `SetupEngineInvariantTests` (`@covers`).
- **Erwartet:** Testdateien, die den Typ aufrufen, erscheinen im Test-Kontext (Call-Graph), nicht nur Naming/`@covers`.
- **Auswirkung:** Orchestrator/Implementierer unterschätzen vorhandene Abdeckung und legen Doppeltests an bzw. übersehen Journal-Tests.
- **Wunsch:** Test-Kontext = Naming/`@covers` **plus** eingehende Test-Callers, als getrennte Listen.

## 2026-09-05 — Implementierer Epic 1

### 1. `find_magic_values` `changedOnly=true` unterzählt neue Dateien

- **Parameter:** `targetType=project`, `targetPath=<repo-root>`, `changedOnly=true`, `scopeFilter=San.smart.Planner.Platform.Setup`, `includeTests=false`
- **Erwartet:** alle im Working Tree geänderten und neu angelegten Setup-Produktionsdateien (Katalog, Selector, Scanner, Exception, Planner).
- **Ist:** „0 Treffer in 0 eindeutigen Einträgen über 5 Dateien“. Ohne `changedOnly` (Scope `…/Engine`): 59 Dateien, 1 vorbestehender `SqlPackRunner`-Treffer.
- **Auswirkung:** Untracked/neue Dateien fallen aus dem changed-Only-Audit; Magic Values in frischem Code würden übersehen.
- **Wunsch:** `changedOnly` soll untracked und neu hinzugefügte Dateien im Git-Worktree einbeziehen oder die gezählten Pfade explizit listen.

## 2026-09-05 — Reviewer Epic 1

### 1. `get_symbol_body` Datei:Zeile ohne Spalte

- **Parameter:** `symbolIdentifier=San.smart.Planner.Platform.Setup/Engine/SetupPlanner.cs:190`, `targetType=project`
- **Erwartet:** Methode um Zeile 190 (`BuildSummary` / ERP-Notice) oder Kandidatenliste.
- **Ist:** `SYMBOL_NOT_FOUND`. Datei:Zeile ohne Spalte wird nicht aufgelöst.
- **Auswirkung:** Reviewer muss Skeleton/`find_symbol` vorschalten statt gezielt in eine bekannte Zeile zu springen.
- **Wunsch:** `Datei.cs:Zeile` analog `find_references` akzeptieren oder klar „Spalte nötig / nutze find_symbol“.

### 2. `get_symbol_body` Kurz-DocCommentId ohne Parameterliste

- **Parameter:** `M:San.smart.Planner.Platform.Setup.Engine.SetupPlanner.PlanSqlPacks` (ohne Parameter)
- **Erwartet:** eindeutige private Methode oder Hinweis mit voller Signatur.
- **Ist:** `SYMBOL_NOT_FOUND` ohne Signaturvorschlag.
- **Auswirkung:** Extra-`find_symbol`-Roundtrip für bekannte Methodennamen.
- **Wunsch:** bei genau einem Treffer auflösen oder die volle `M:`-Id nennen.

## 2026-09-05 — Implementierer Epic 2

### 1. `search_pattern` Brace-Glob plus Regex liefert 0 Treffer

- **Parameter:** `targetType=project`, `targetPath=<repo-root>`, `scope=San.smart.Planner.Platform.Setup`, `includePatterns=["**/*.{xaml,cs}"]`, `isRegex=true`, `pattern=ShowAuthOverride|_mitarbeiterPlantafel|_xrmTermine|AuthDatabaseName`
- **Erwartet:** Treffer in `ConnectionPage.xaml` (`ShowAuthOverride`) und `WizardShellViewModel.cs` (namentliche Felder).
- **Ist:** 0 Treffer. Derselbe Scope mit `includePatterns=["**/*.xaml"]` und Plain-Pattern `ShowAuthOverride` fand die XAML-Stelle.
- **Auswirkung:** Agent hält Symbole für abwesend und muss auf einzelne Plain-Suchen oder Dateilesen ausweichen.
- **Wunsch:** Brace-Globs in `includePatterns` auflösen **oder** klar „Glob nicht unterstützt“; Regex-Alternation nicht still auf 0 sinken lassen, wenn Plain-Suche trifft.

## 2026-09-05 — Reviewer Epic 3

### 1. `get_symbol_body` volle, aber falsche Parameterliste ohne Kandidaten

- **Tool:** `get_symbol_body`
- **Parameter:** `symbolIdentifier=M:San.smart.Planner.Platform.Setup.Engine.Sql.SqlPackRunner.RecordAppliedAsync(San.smart.Planner.Platform.Setup.Engine.SetupSqlWorkItem,System.String,System.Threading.CancellationToken)~System.Threading.Tasks.Task` (Parameterreihenfolge vertauscht gegenüber der echten Signatur `connectionString, item, cancellationToken`), `targetType=project`
- **Erwartet:** `SYMBOL_NOT_FOUND` plus die korrekte DocCommentId aus dem File-Skeleton, oder eine Kandidatenliste der einen privaten Methode.
- **Ist:** `SYMBOL_NOT_FOUND` ohne Signaturvorschlag. Mit der richtigen Reihenfolge löst dasselbe Tool den Body auf.
- **Auswirkung:** Extra-`find_symbol`-Roundtrip, obwohl Skeleton und vertauschte `M:`-Id denselben Methodennamen tragen.
- **Wunsch:** bei genau einem Namens-Treffer die volle Id vorschlagen (wie bei fehlender Parameterliste).

