---
task: mcp-call-logging-fuer-agenten-analyse
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-05T12:55:00+02:00
---

# Tech-Debt-Log: mcp-call-logging-fuer-agenten-analyse

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem Fix-Step oder einem neuen Epic. Das entscheidet ausschließlich der
Nutzer (manuell, z. B. durch Ergänzen eines Epics in `roadmap.md` mit
Verweis auf die Tech-Debt-ID).

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `tasks/mcp-call-logging-fuer-agenten-analyse/roadmap.md:61` | niedrig | Roadmap-Notiz „ersetzt/erweitert die zwei betroffenen Tests" widerspricht der im step-001-Plan korrigierten Test-Scope-Lesart (1 LÖSCHT, 4 NEU, 3 ANGEPASST); Doku-Inkonsistenz. |

## Einträge

### TD-001 — Roadmap-Test-Scope-Notiz inkonsistent zu step-001-Plan [Priorität: niedrig]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-05T12:55:00+02:00)
- **Ort:** `tasks/mcp-call-logging-fuer-agenten-analyse/roadmap.md:61`
- **Befund:** Die Roadmap-Beschreibung von EPIC-01 enthält die Notiz
  „… und ersetzt/erweitert die zwei betroffenen Tests in
  `Tests/Commands/McpServerCommandCallLogTests.cs`". Diese Aussage
  widerspricht dem im step-001-Plan korrekt dokumentierten tatsächlichen
  Test-Scope: 1 Test gelöscht (`TryCreateCallLog_WhitespacePath_ReturnsNull`),
  3 Tests auf neue Signatur umgestellt, 4 Tests neu
  (`TryCreateCallLog_WhitespacePath_CreatesDefaultLog`,
  `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`,
  `BuildDefaultLogPath_WithSolution_IncludesSolutionName`,
  `BuildDefaultLogPath_DateIsLocal`). Der Planer hat das im Plan explizit
  richtig dokumentiert und die Roadmap-Aussage stillschweigend ignoriert
  (zu Recht — der Plan ist die maßgebliche Quelle). Die Inkonsistenz
  bleibt aber in der Roadmap stehen und kann künftige Step-Mode-Planer
  verwirren, die die Roadmap-Notiz gegen den Plan abgleichen müssen.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001.
  Roadmap-Updates sind gemäß Plan §Bekannte Ausnahmen + §Regel-Index
  explizit für den EPIC-04-Sammel-Step zurückgestellt (analog zur
  `--mcp-log`-Description in `CliOptionFactory.cs:232`). Wird dort
  voraussichtlich mit den übrigen Doku-Syncs gebündelt.
- **Vorschlag:** Bei EPIC-04 die Roadmap-Notiz an die tatsächliche
  Test-Scope-Lesart anpassen (z. B. „1 obsoleter Test wird gelöscht, 3
  bestehende Tests werden auf die neue 4-Parameter-Signatur umgestellt,
  4 neue Tests dokumentieren Default-Pfad-Konstruktion und
  Failure-Signalisierung"). Kein separater Fix-Step nötig.
- **Status:** offen
