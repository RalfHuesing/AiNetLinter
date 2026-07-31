---
status: done
type: step-review
task: codegraph-mcp
step: 002
epic: EPIC-02
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T16:00:00Z
verdict: approved
tech_debt_ids: [TD-003]
---

# Review Step 002: Resident Server-Zustand: McpCodeGraphServer mit Lazy Staleness-Invalidierung

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Fix-Step `step-<NNN>/fix-<XX>` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (ein Rerun nötig, siehe unten)

## Befund

### Plan-Erfüllung

Beide „Konkrete Änderungen"-Punkte (neue `McpCodeGraphServer`, Signaturänderung `TryLoadSolutionAsync` + `using`-Wrap in `RunAsync`) sowie alle 6 geplanten Testfälle 1:1 umgesetzt, gegen den Diff (`81cf007`) verifiziert.

### Rules-Konformität

Alle im Plan referenzierten Regeln (kein DI-Container, Zero-Warning, Nullable-Result-Pattern, `sealed`, `#nullable enable`, kein leeres `catch`, `MaxMethodParameterCount`/`MaxBoolParameterCount`, `EnforceNamespaceDirectoryMapping`, `MaxMethodLineCount`) eingehalten — selbst nachvollzogen im Diff, zusätzlich bestätigt durch `CliIntegrationTests`, die den Linter gegen die eigene neue Testdatei laufen lassen (Teil von `dotnet test`).

### Logische Korrektheit

Staleness-Reihenfolge mtime → Hash → Content stimmt exakt mit `konzept.md` und Plan überein (mtime-Gleichheit überspringt Hash, Hash-Gleichheit überspringt `WithDocumentText`, nur echte Inhaltsänderung triggert `WithUpdatedSolution`); Reduktion auf **eine** akkumulierte `Solution` pro Refresh-Durchlauf (kein Zwischenschritt pro Datei) korrekt umgesetzt. Die dokumentierte Abweichung (`Task.WhenAll`/`Task.WhenAny` statt `.Result` im Concurrency-Test wegen `BanBlockingTaskAccess`) selbst nachvollzogen — Testcode ist sauber, keine verdeckte Blocking-Access-Stelle mehr. Die als „Bekannte Unschärfe" dokumentierte Situation (Datei, die beim Start fehlte und später valide wird, wird beim ersten Treffer als „geändert" behandelt, da kein gültiger gecachter Zustand vorliegt) ist konsistent mit der im Plan festgelegten Scope-Grenze („Bekannte Ausnahmen": nur Änderung an *bekannten, existierenden* Dateien ist Teil dieses Steps, Hinzufügen/Löschen ganzer Dateien explizit außerhalb) — kein übersehener Edge-Case, kein Finding. Thread-Safety: `lock (_lock)` umschließt den kompletten Refresh-Zyklus (Lesen, Hashen, `_catalog`-Reassignment) analog `AnalysisCacheManager`, keine Lücke zwischen Prüfen und Schreiben gefunden; `ConcurrentCalls_DoNotThrow` reproduziert parallele Reader gegen einen gleichzeitig schreibenden Writer ohne Deadlock/Exception.

### Konzept-Treue (Ebene 4)

Deckt die zitierten Muss-Haben-Punkte („Server lädt Solution einmal und hält sie resident", „Lazy Staleness-Invalidierung" inkl. mtime/Hash-Reihenfolge, „Thread-sicherer Zugriff") vollständig ab; bewusst kein `FileSystemWatcher` (konsistent mit „Verworfene Alternativen"). Kein Non-Goal verletzt. `McpCodeGraphServer` bleibt wie vorgegeben unangebunden an das MCP-Tool-Protokoll — `ToolCollection` in `McpServerCommand` weiterhin leer, kein Scope-Creep Richtung EPIC-03 im Diff gefunden.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün (0 Warnung(en), 0 Fehler)
dotnet test AiNetLinter.slnx  → 1. Lauf: 1 Fehler (McpCodeGraphServerTests.GetCurrentSolution_FileTouchedWithoutContentChange_SkipsSolutionUpdate,
                                  InvalidOperationException in SourceFileCatalog.RegisterMSBuild, vorbestehende Race Condition, siehe TD-003)
                                 2. Lauf: grün (1027 Tests, 0 Fehler)
```

Ursache verifiziert: nicht-thread-sicherer Check-then-Act in `RegisterMSBuild()` (Zeile 223 ff., **nicht** Teil des Diffs `81cf007`), bereits vor diesem Step latent vorhanden (`SourceFileCatalogTests.cs` ruft `LoadAsync` ohne Serialisierungs-Kollektion auf). `McpCodeGraphServerTests` erhöht durch fünf weitere parallele Erstaufrufe die Kollisionswahrscheinlichkeit, führt den Fehler aber nicht neu ein — kein Logikfehler in der review­ten `McpCodeGraphServer`-Klasse selbst, kein Blocker (siehe TD-003).

## Tech-Debt-Einträge aus diesem Review

- `TD-003` (siehe `tech-debt.md`) — Nicht-thread-sicherer Check-then-Act in `SourceFileCatalog.RegisterMSBuild` verursacht intermittierende Testfehler bei paralleler Testausführung, verschärft durch neue parallele `LoadAsync`-Aufrufe in `McpCodeGraphServerTests`.
