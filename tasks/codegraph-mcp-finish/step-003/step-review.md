---
status: done
type: step-review
task: codegraph-mcp-finish
step: 003
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: approved
tech_debt_ids: [TD-003]
---

# Review Step 003: Core/-Testordner sub-gliedern + MaxDirectoryChildren aktivieren (F.3)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `Konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle 23 Dateien wie im Plan spezifiziert verschoben (20 → `Core/Checkers/`, 3 → `Metrics/`, Zielverzeichnisse und -namespaces exakt wie in „Konkrete Änderungen" A/B aufgelistet — per `git show 8cae25c --name-status` gegen die Plan-Liste abgeglichen, keine Abweichung), `rules.json`/`AiNetLinter.mdc` synchron, `Docs/configuration.md` geprüft und begründet unverändert gelassen.

### Rules-Konformität

`EnforceNamespaceDirectoryMapping` eingehalten: alle 23 verschobenen Dateien tragen exakt den zum neuen Pfad passenden Namespace (`AiNetLinter.Tests.Core.Checkers` bzw. `AiNetLinter.Tests.Metrics`, verifiziert per Diff jeder einzelnen Datei — je Datei ausschließlich die `namespace`-Zeile geändert, sonst 0 Inhaltsänderung). `AiNetLinter.mdc`-Tabellenzeile `MaxDirectoryChildren` korrekt auf `30` synchronisiert. `AiNetLinterRichtlinien.mdc` §4 (Update-Pflicht) erfüllt: `Docs/configuration.md` geprüft; §5 (keine Task-Referenzen in Code-Kommentaren) nicht berührt, da keine Kommentare verändert wurden.

### Logische Korrektheit

Directory-Sweep nach dem Schritt bestätigt: `Core/` = 19, `Core/Checkers/` = 27, `Metrics/` = 7 Dateien — exakt das im Plan vorhergesagte Zielbild, alle drei unter dem neuen Grenzwert 30. Kein projektweites Verzeichnis überschreitet 30 Einträge (eigener Sweep über `src/`, ohne `bin`/`obj`/`.git`); `src/AiNetLinter/Core/Checkers` bleibt mit 28 wie vorhergesehen unterhalb der Schwelle. Keine verwaisten `using AiNetLinter.Tests.Core;`-Referenzen auf verschobene Klassen gefunden.

### Konzept-Treue (Ebene 4)

`Konzept.md` Block F.3 verlangt Sub-Gliederung + anschließende `MaxDirectoryChildren`-Aktivierung — beides erfüllt. Eine Abweichung ist bewusst dokumentiert: `Konzept.md` nennt als Vorbild die Kategorisierung „agent-resilience/architecture/general/test-coverage" aus `AiNetLinter.mdc` (Formulierung „analog zur Kategorisierung"), der Step führt stattdessen die bereits vorgefundene Konvention „ein Ordner pro 1:1-Checker-Test" in `Core/Checkers/` fort (7 von 27 Dateien lagen dort schon vor diesem Step). Das ist im Rahmen des Wortes „analog" (Vorbild, keine 1:1-Vorschrift) vertretbar und explizit im Plan begründet (Vermeidung einer zweiten, konkurrierenden Taxonomie neben einer bereits etablierten Struktur) — kein Muss-Haben-Punkt fehlt dadurch, kein Non-Goal wurde berührt, der Scope entspricht der Intention. Kein Finding.

### Build-/Test-Status

Selbst nachvollzogen (nach `taskkill /F /IM testhost.exe` für zwei verwaiste Prozesse):

```
dotnet build AiNetLinter.slnx           → grün, 0 Warnungen
dotnet test --filter Category=Unit      → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build → grün (1186 Tests, 0 Fehler, 1m42s)
```

Deckt sich mit den Angaben in `step-result.md`.

## Tech-Debt-Einträge aus diesem Review

- `TD-003` (siehe `tech-debt.md`) — `--sync-agent-rules-only` fehlt in `LinterArgs.HasStandaloneCommand()` und verlangt dadurch unnötig `--path`/`--config`; vom Kritiker reproduziert (`[ERROR]: --path ist erforderlich…`), Priorität niedrig, außerhalb des Scopes von F.3.
