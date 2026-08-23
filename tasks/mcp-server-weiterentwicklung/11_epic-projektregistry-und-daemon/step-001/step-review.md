---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 001              # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
epic: EPIC-A
step_type: single  # single | batch — aus step-plan.md übernehmen
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha (openrouter)
reviewed_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
reviewed_at: 2026-08-23T14:05:41+02:00
verdict: approved  # approved | issues | blocked
tech_debt_ids: [TD-001, TD-002, TD-003]  # welche tech-debt.md-Einträge dieser Review-Durchgang erzeugt hat
---

# Review Step 001: Projektregistry-Grundlage: Definitionsdatei, Loader, Fehlerverträge, Config-Materialisierung

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-001`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

Geprüft wurden die Commits `e0b25033` (Code+Tests) und `7ee7d805` (Step-Doku) über den vollen Diff, step-result.md sowie Stichproben per AiNetLinter-MCP (`get_violations` Scope `Projects`, `get_symbol_body` für `ConfigLoader.TryLoadConfig`/`McpCodeGraphServerOptions.From`, `get_test_context` für `ResolveMaxLineCount`).

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: im Plan zitierte `<rules_dir>/**`-Refs eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [ ] Build: selbst nachgeprüft — nicht wiederholt (Nutzervorgabe Effizienz); Coder-Gate laut step-result.md grün
- [ ] Tests: selbst nachgeprüft — nicht wiederholt (Nutzervorgabe Effizienz); Coder-Gate laut step-result.md grün

## Befund

### Plan-Erfüllung

Alle sieben geplanten Dateiänderungen sind umgesetzt (fünf neue `Mcp/Projects/`-Klassen inkl. Result-Record, Delegation von `ResolveConfig`/`ResolveMaxLineCount`, zwei Testdateien mit 11+3 Contract-Tests), die bestehenden Batch-Pinning-Tests (`ResolveMaxLineCount_*` in `McpServerCommandTests`) blieben unverändert grün, und die Codemap wurde korrekt fortgeschrieben.

### Rules-Konformität

Alle im Plan zitierten Rules-Refs eingehalten: Grenzwerte (`get_violations` Scope `Projects`: 0 Verstöße in 7 Dateien, eigene Stichprobe; `sealed`, `#nullable enable`, Namespace=Verzeichnis), agent-resilience (nur typisierte, nicht-leere catches; kein `.Wait`/`.Result`), Richtlinien §4 (xUnit v3, durchgängig `TestTempDirectory`, keine Serialisierungs-Collection), §5 (Result-Pattern statt Exceptions für erwartbare Fehler, Zero-Warning, `ProjectErrorCodes` als einzige Konstantenquelle, keine Task-/Step-ID-Referenzen in Kommentaren) und §1 (Dogfooding belegt, Doku bewusst noch nicht beschrieben).

### Logische Korrektheit

Die Delegation ist zur bisherigen Batch-Logik äquivalent (Whitespace-Pfad → Defaults, `TryLoadConfig` → null → Defaults); der Loader deckt alle vier loader-seitigen Fehlerfälle deterministisch ab, der Template-Block stimmt wörtlich mit A.5 überein (per Text-Assertion gepinnt), und die Ankerregel wird gegen die Lage der Definitionsdatei getestet, nicht gegen das cwd. Die `<root>`-Substitution in der ersten Template-Zeile (Konkretisierung des erwarteten Pfads bei wörtlichem JSON-Block) entspricht der Konzept-Intention „erwarteter Pfad + kopierfähiges Template". Verifikation am Options-Bau: `Catalog: null` ist über `From(...)` gültig (nullable Parameter, `Console` defaultet auf `LinterConsole.Instance`) — der Registry-Optionsaufbau ist semantisch sauber.

### Konzept-Treue (Ebene 4)

Kein Non-Goal berührt — Solution-Auto-Suche/Nachbar-Fallback unangetastet (F8), Batch-CLI unverändert, keine neuen Tools/Abhängigkeiten — und Review 3 ist als „eine Pipeline statt zweier" konkret umgesetzt, wobei Registry/Entry/Lease plan-konform späteren Steps vorbehalten bleiben.

### Build-/Test-Status

Nicht erneut ausgeführt (Nutzervorgabe Effizienz); dokumentiertes Coder-Gate laut step-result.md:

```
dotnet build                                                              → grün (0 Warnungen)
dotnet test FastTests --filter Category=Unit                              → grün (1154/1154)
dotnet test FastTests --filter "Category=Unit&FullyQualifiedName~Projects" → grün (16/16)
Gate: FastTests --filter Category!=Stress                                  → grün (1642, 0 Fehler)
Gate: IntegrationTests --filter Category!=Stress                           → grün (350, 0 Fehler)
```

Eigene Quality-Gate-Stichprobe: `get_violations` (scopeFilter `Projects`) → 0 Verstöße in 7 Dateien.

## Sonstige Beobachtungen / MINOR / NITPICK

- NITPICK `src/AiNetLinter.FastTests/Mcp/Projects/ProjectInstanceFactoryTests.cs:44` — `Create_MaxLineCount_MatchesLegacyBatchPipeline` vergleicht nach der Delegation beide Seiten über denselben geteilten Kern (`MaterializeRules`); der eigenständige Pinning-Wert liegt primär in den unveränderten Bestands-Tests (`ResolveMaxLineCount_*` in `McpServerCommandTests`). Der Test bleibt als Drift-Wächter sinnvoll, misst aber keine Legacy-Pipeline mehr.

## Tech-Debt-Einträge aus diesem Review

Volltext ausschließlich in `tech-debt.md` (Pointer-Prinzip):

- `TD-001` (siehe `tech-debt.md`) — Defekte (lesbare, aber ungültige) `rules.json` fällt im künftigen Registry-Pfad stumm auf Defaults zurück; Konzept A.5 kennt dafür keinen Fehlercode.
- `TD-002` (siehe `tech-debt.md`) — Diagnosen von `ConfigLoader.TryLoadConfig` gehen hart auf `Console.Error` (nicht injizierbar) — relevant erst mit dem Daemon (Epic B).
- `TD-003` (siehe `tech-debt.md`) — `ProjectDefinitionLoader.Load` toleriert null/leeren `projectRoot` mit implizit cwd-relativer Auflösung bis der Wiring-Guard existiert.
