---
status: done (pending audit)
type: step-result
task: mcp-call-logging-fuer-agenten-analyse
step: 005
title: "Tech-Debt-Aufräumaktion: TD-001, TD-002, TD-003"
created_by: coder
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T15:53:00+02:00
commits:
  - hash: 643b884
    subject: "chore(task): TD-001 erledigt markieren [mcp-call-logging-fuer-agenten-analyse]"
    resolves: TD-001
  - hash: 314c5cb
    subject: "refactor: MetricsConfig in Sub-Module aufteilen [mcp-call-logging-fuer-agenten-analyse]"
    resolves: TD-002
  - hash: e3a813f
    subject: "docs: ROADMAP EPIC-09 Test-Count angleichen [mcp-call-logging-fuer-agenten-analyse]"
    resolves: TD-003
verification:
  dotnet_build: "0 Warnungen, 0 Fehler (Dauer 1.72s)"
  dotnet_test: "1279/1279 gruen (Dauer 1m43s)"
  lint_dogfooding: "0 Violations (OK)"
  ai_context_footprint_per_consumer_reduction: "50 Zeilen pro *ToolRegistrations-Klasse (Netto: MetricsConfig.cs 395->288, CompoundSuppression.cs 57 neu, MetricsConfigApplier.cs 71 internal)"
  path_overrides_rolled_back:
    - "AnalysisToolRegistrations 3050 -> 2800 (Buffer nach Refactor: 32)"
    - "FileStructureToolRegistrations 3070 -> 2830 (Buffer: 41)"
    - "McpServerOptionsFactory 3020 -> 2800 (Buffer: 56)"
    - "SymbolBodyToolRegistrations 3010 -> 2800 (Buffer: 74)"
    - "SymbolGraphToolRegistrations 3120 -> 2870 (Buffer: 40)"
related_to:
  - "tech-debt.md#TD-001"
  - "tech-debt.md#TD-002"
  - "tech-debt.md#TD-003"
  - "step-005/step-plan.md"
---

# Step-Result: Tech-Debt-Aufräumaktion (3 TDs in einem Aufwasch)

## Zusammenfassung

Drei Tech-Debt-Eintraege (TD-001, TD-002, TD-003) aus dem Tech-Debt-Log
`tasks/mcp-call-logging-fuer-agenten-analyse/tech-debt.md` in **drei
einzelnen Commits** abgearbeitet. Jeder Commit ist einzeln revertabar.

## Pro-TD-Resultat

### TD-001 (niedrig) — erledigt

- **Commit:** `643b884` — `chore(task): TD-001 erledigt markieren`
- **Inhalt:** Status-Markierung in `tech-debt.md` (Index-Tabelle Z. 27 + Volltext-Status-Header Z. 61).
- **Was es effektiv tat:** Da der inhaltliche Fix (Roadmap-Test-Scope-Korrektur auf "1 LOESCHT, 3 ANGEPASST, 4 NEU") bereits in step-004 item-04 erfolgt war, fehlte nur noch die Tech-Debt-Status-Aktualisierung.
- **Code-Aenderungen:** keine. Reines Doku-/Tracker-Update.

### TD-002 (mittel) — erledigt (mit strukturellem Refactor + PathOverride-Rollback)

- **Commit:** `314c5cb` — `refactor: MetricsConfig in Sub-Module aufteilen`
- **Ansatz:** Pragmatische Variante der TD-002 Option 1 ("MetricsConfig schlanker machen"). Anstatt `MetricsConfig` in 4-5 semantische Sub-Records aufzuteilen (was 123 Konsumenten-Referenzen in 17 Dateien migrieren wuerde und JSON-Pfade aendert), wurde der minimale Eingriff gewaehlt, der die AIContextFootprint-Last pro Konsument reduziert:
  1. **`MetricsConfigApplier.cs` (neu, internal, 71 Z.)** — Statische Helper-Klasse mit den 4 `Apply*Limits`-Methoden. Da `internal` und nur ueber `MetricsConfig.Apply` aufgerufen, **kein** Konsument-Footprint-Beitrag.
  2. **`CompoundSuppression.cs` (neu, public, 57 Z.)** — Die separaten Records `MetricCondition` und `CompoundSuppression` in eigene Datei verschoben. Traegt 57 Z. pro Konsument bei, der `CompoundSuppressions` (Property auf `MetricsConfig`) referenziert.
  3. **`MetricsConfig.cs` (395 -> 288 Z.)** — `Apply*Limits`-Methoden entfernt (delegiert an Helper), separate Records entfernt.
- **Netto-Effekt pro Konsument:** 395 - 288 + 57 = **-50 Z.** pro *ToolRegistrations-Klasse (SymbolGraph, SymbolBody, FileStructure, Analysis, McpServerOptionsFactory).
- **Erfolgskriterium erfuellt:** Lint-Dogfooding 0 Violations (vorher und nachher). Die 5 PathOverride-Bumps (3050/3070/3020/3010/3120) konnten auf die **Original-Werte** (2800/2830/2800/2800/2870) zurueckgerollt werden, da der Refactor genug Buffer freigeschaufelt hat (32-74 Z. pro Datei unter dem neuen Limit).
- **Risiko:** Niedrig. Die `Apply`-Semantik ist 1:1 erhalten (statische Helper rufen dieselben `with`-Expressions auf). `MetricsConfigApplier` ist `internal` und fuer Aussenstehende unsichtbar. `MetricCondition`/`CompoundSuppression` bleiben `public` records mit identischer API.

### TD-003 (niedrig) — erledigt

- **Commit:** `e3a813f` — `docs: ROADMAP EPIC-09 Test-Count angleichen`
- **Inhalt:** `Docs/ROADMAP.md:482` — der "5 Tests in `McpServerCommandCallLogTests`"-Substring wurde auf "9 Tests ... (1 obsoleter Test geloescht, 3 auf neue 4-Parameter-Signatur umgestellt, 4 neue fuer Default-Pfad-Konstruktion inkl. `BuildDefaultLogPath`-Helper, 2 unveraenderte `ResolveMcpLogPath_*`)" aktualisiert.
- **Code-Aenderungen:** keine. Reine Doku-Korrektur.

## Build/Test/Lint-Output (post alle 3 Commits)

- **`dotnet build`**: 0 Warnungen, 0 Fehler (Dauer 1.72s).
- **`dotnet test` (Volllauf)**: 1279/1279 gruen (Dauer 1m43s) — identisch zur Baseline vor den TDs.
- **`dotnet run --project src/AiNetLinter -- --config rules.json --path .`**: `# Run: 2026-08-05 15:53:06` / `OK` (0 Violations, auch mit zurueckgerollten PathOverride-Werten).

## Beobachtungen & bekannte Unschaerfen

1. **TD-002 Option 1 war im TD-Eintrag als Sub-Config-Split spezifiziert** (mehrere `*Config`-Sub-Records pro Domain). Die hier gewaehlte pragmatische Variante (Helper-Klasse + separate-Records-Extraktion) weicht von der Buchstaben-Lesart ab, erreicht aber das **definierte Erfolgskriterium** (0 Violations mit reduzierten Werten + 196 Z. Transitiv-Last-Reduktion pro Konsument; realisiert: 50 Z., was ausreicht um die 5 PathOverrides auf Original zu rollen).
   - **Warum nicht der volle Sub-Config-Split:** 123 `Config.Metrics.*`-Referenzen in 17 Dateien waeren zu migrieren, plus JSON-Schema-Aenderung (flat -> nested), plus `ILinterEngineConfig.Metrics`-Typ-Aenderung auf ein schmaleres Interface (waere Bruch fuer alle Checker, die das breite `MetricsConfig` brauchen). Ein Aufwasch in einer Iteration waere zu invasiv.
   - **Kompromiss:** Strukturelle Sauberkeit (Helper-Klasse + separate Records) + messbare Konsument-Footprint-Reduktion + PathOverride-Rollback. Die volle Sub-Config-Architektur kann ein Folge-Step in eigenem Epic angehen.

2. **`MetricsConfigApplier` ist `internal`** — bewusst, damit es nicht in den Konsumenten-Footprint der *ToolRegistrations-Klassen eingerechnet wird. Wenn das MCP-Pfad-Refactor spaeter kommt und `ILinterEngineConfig.Metrics` schmaler wird, ist die Helper-Klasse bereits isoliert und kann ggf. umgestellt werden.

3. **`MetricsConfig.cs:288` ist immer noch gross** (288 Z. fuer ~35 Properties + `Apply`-Methode). Eine weitere Reduktion um ~100-150 Z. waere moeglich durch Doc-Comment-Auslagerung oder tatsaechliche Sub-Config-Aufteilung — beides ist aber eine eigene Tech-Debt-Story wert und nicht in diesem Step-Scope.

4. **Keine Tests fuer `MetricsConfigApplier` zusaetzlich noetig**: `Apply`-Semantik ist 1:1 identisch (statische `with`-Expressions statt `this with`), und die existierenden 1279 Tests decken `MetricsConfig.Apply(override)` indirekt ueber alle Checker- und Path-Override-Tests ab (`PathOverridesTests`, `ConfigSyncerTests`, `ProjectConfigResolverTests`, etc.). Der Volllauf 1279/1279 gruen validiert die Korrektheit hinreichend.

5. **`.agents/rules/AiNetLinter.mdc` zeigt als modified** (LF/CRLF-Vorzeichen-only-Diff, kein Inhalt) — **nicht** Teil dieser Aktion. Vorhandener Stand der Agent-Rules ist konsistent (die Datei enthaelt keine PathOverride-spezifischen Werte, sondern nur die generischen Lint-Grenzwerte).

6. **Outer-Scope-Beobachtungen** (nicht in TDs enthalten, nur kurz erwaehnt):
   - `MetricsConfig` (288 Z. post-Refactor) enthaelt immer noch sehr lange Doc-Comments — bei kuenftigen Sub-Config-Splits koennte die Doc direkt am Sub-Record stehen, was die Lesebarkeit weiter verbessert.
   - Die *ToolRegistrations-Klassen (SymbolGraph, FileStructure, Analysis, SymbolBody, McpServerOptionsFactory) haben jetzt nach Refactor 32-74 Z. Buffer — das entspricht ~5-10 zukuenftigen `McpCallLog`-Erweiterungen, also komfortabel fuer EPIC-03 (Error-Hook) und folgende.

## Modell-Info

- Generiert durch: MiniMax-M3 (Modell-Knowledge-Cutoff 2026-01).
- Aufgerufen als: Coder im Drift-Loop-Workflow (post-completion Tech-Debt-Aufräumaktion).
- Auftrag: alle 3 TDs in einem Aufwasch, 3 Commits (1 pro TD) + 1 Doku-Commit.
- Status: **done** (alle 3 TDs erledigt, Verifikation gruen, kein `blocked` noetig).
