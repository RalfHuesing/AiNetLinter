---
task: flaky-and-test-performance
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-07T10:00:00+02:00
---

# Tech-Debt-Log: flaky-and-test-performance

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `src/AiNetLinter/Cli/` + `konzept.md` | mittel | Konzept-/roadmap.md verweisen auf `--self-lint` als Self-Lint-Befehl, CLI-Option existiert nicht. |

## Einträge

### TD-001 — Fehlende CLI-Option `--self-lint` (Konzept-/CLI-Diskrepanz) [Priorität: mittel]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-07T10:00:00+02:00)
- **Ort:** `tasks/flaky-and-test-performance/roadmap.md:26`, `tasks/flaky-and-test-performance/step-001/step-plan.md:60,131,151` (Plan/DoD-Referenz), `src/AiNetLinter/Cli/CliOptionFactory.cs` (fehlend — verifiziert per `grep "--self-lint"` über `src/AiNetLinter/Cli/`, kein Treffer)
- **Befund:** Sowohl das Konzept des Tasks (`konzept.md` implizit über die `roadmap.md`-Linie 26) als auch der `step-001/step-plan.md` (Definition of Done) referenzieren `dotnet run --project src/AiNetLinter -- --self-lint` als Self-Lint-Verifikation. Die aktuelle CLI in `CliOptionFactory.cs` kennt diese Option nicht (vorhandene Optionen reichen von `--config` über `--playbook` bis `--mcp-server` — kein `--self-lint`). Der Coder hat im `step-result.md` korrekt dokumentiert und mit `dotnet run --project src/AiNetLinter -- --config rules.json --path .` (Ausgabe `OK`) semantisch identisch ersetzt. Die **Konzept-Spec ist also veraltet** (oder die CLI-Option wurde bei einem früheren Refactoring vergessen).
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 (Spike zur Fixture-Sharing-Mechanik). Die Diskrepanz betrifft zwei Ebenen — eine CLI-Option nachrüsten *und* das Konzept/roadmap/Plan-DoD korrigieren — beides ist Orchestrator-/Nutzer-Entscheidung, kein Spike-Fix.
- **Vorschlag:** Nutzer entscheidet eine der beiden Richtungen: (a) CLI-Option `--self-lint` als Convenience-Alias für `--config rules.json --path .` (oder mit hartcodiertem Self-Path) in `CliOptionFactory.cs` nachrüsten und in `Docs/configuration.md` dokumentieren; oder (b) Verweise in `roadmap.md` und ggf. Konzept/Plan-DoD auf den existierenden Befehl korrigieren. Variante (a) ist die weniger invasive (Spec-treu) — die Spec war vermutlich Absicht, nicht Versehen.
- **Status:** offen
