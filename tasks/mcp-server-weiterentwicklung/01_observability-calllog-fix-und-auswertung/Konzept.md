---
status: erledigt (2026-08-21, Commit 7ef3faed)
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: Review-Finding 2026-08-21 (ox-alpha)
umsetzung: "2026-08-21 umgesetzt (Commit 7ef3faed, EPIC-12 in Docs/ROADMAP.md): get_server_health liefert echte Call-Log-Aggregate via McpLogAnalyzer.TryAnalyze (statt hartkodierter Nullen); CLI-Kommando --analyze-mcp-log <datei|verzeichnis|glob> [--format text|json] mit allen Konzept-Metriken (Calls pro Tool, Fehlercodes/isError-Rate, Loading-Retry-Bursts, Truncation-/Completeness-Heuristik, PID/InstanceId-Sessions). Doc-Sync: ROADMAP/agent-api/configuration/integration/README. Verifiziert 2026-08-21 gegen den Code (nicht nur Doku) inkl. Live-Health-Check."
---

# Call-Log: Health-Nullen fixen und Log-Auswertung als CLI-Kommando

## Ziel

Zwei Lücken in der Observability-Kette schließen:

1. **Bug-Fix:** `get_server_health` meldet bei aktivem `--mcp-log` hartcodierte Nullen
   statt der vorhandenen Aggregate.
2. **Feature:** Das geschriebene `calls.jsonl` wird durch ein Offline-CLI-Kommando
   auswertbar — die Nutzungsdaten-Evidenzbasis, die mehrere Architektur-Entscheidungen
   dieses Projekts als Wiederöffnungsbedingung nennt.

## Befund A (verifiziert 2026-08-21): `get_server_health` meldet hartcodierte Nullen

`GetServerHealthTool.BuildPayload` (`GetServerHealthTool.cs:62-67`):

```csharp
var callLogPayload = observabilityLogPath is null
    ? null
    : new CallLogPayload(observabilityLogPath, 0, 0, new Dictionary<string, int>());
```

Die `CallLogPayload`-Doku (`GetServerHealthModels.cs:28-29`) verweist auf die vorhandenen
`McpCallLog`-Aggregate `EntryCount`, `ErrorCount`, `CallCountsByTool`. Diese existieren —
aber dem Health-Tool wird nur der **Pfad** übergeben, nie die Log-Instanz. Ergebnis: Bei
aktivem `--mcp-log` stehen dort immer `0/0/{}`, selbst nach hunderten Calls. Das ist aus
Agentensicht irreführend: Das Tool verspricht Observability-Daten und liefert konstant Nullen.

**Fix-Skizze:** Die Observability-Log-Instanz an das Tool durchreichen (analog zum
`mcpState`-Closure-Muster der übrigen Tools) und die echten Aggregate lesen. Kleiner,
isoliert testbarer Change.

## Befund B (verifiziert 2026-08-21): Es gibt keine Auswertung von `calls.jsonl`

EPIC-09 schreibt bei Opt-in strukturierte JSONL-Einträge pro Tool-Call (inkl. Error-Sink).
Eine Suche nach Auswertung/Analyse des Logs findet **nur** die CLI-Option
(`Cli/CliOptionFactory.cs:204-206`) — kein Analyzer, kein Report, kein Kommando.

Damit fehlt dem Projekt genau das, was `90_bewusst-nicht-umsetzen/Konzept.md` als
Wiederöffnungsbedingung fordert: **reproduzierbare Nutzungsdaten**. Konkret unentschieden
bleiben aktuell: Tool-Entfernung/Zusammenlegung (Entscheidung Nr. 4 dort: "Erst
Observability-Nutzungsdaten sammeln"), Tool-Profile (Nr. 5), Output-Schema-Pilot (Nr. 6).

## Befund C (verifiziert 2026-08-21): Mehrere parallele Agenten/Server sind log-technisch unkritisch

Auslöser: Der Nutzer nutzt `--mcp-log` nicht, weil es früher Probleme gab, wenn mehrere
Agenten gleichzeitig "denselben" MCP-Server starteten. Code-Review der Log-Kette
(inkl. Paketquelle `c:\Daten\Entwicklung\Ralf\RalfHuesing.Mcp.Observability`, lokal,
Version 1.0.3 = referenzierte Version):

1. **Ein File pro Prozess:** `ObservabilityContext` erzeugt
   `{ServerName}_{PID}_{InstanceId}.jsonl` (PID + GUID, `ObservabilityContext.cs:45-46`,
   `:103`). Zwei Server-Prozesse schreiben **niemals** in dieselbe Datei — auch nicht bei
   gemeinsamem explizitem `--mcp-log`-Verzeichnis. Das frühere Kollisionsrisiko
   (mehrere Prozesse appenden auf eine gemeinsame `calls.jsonl`) ist im aktuellen Stand
   konstruktiv ausgeschlossen.
2. **Thread-Sicherheit innerhalb des Prozesses:** `JsonlLogWriterBase` serialisiert über
   `SemaphoreSlim`, öffnet den Stream lazy im Append-Modus für die Prozesslaufzeit,
   `FileShare.ReadWrite` erlaubt gleichzeitiges Lesen (Tests, Analyzer).
3. **Feedback-Log ebenfalls prozessisoliert** (`..._{PID}_{InstanceId}.feedback.jsonl`).

**Konsequenz:** `--mcp-log` kann wieder genutzt werden. Falls die ursprünglichen Probleme
nicht vom Log kamen, bleiben als plausible Ursachen: RAM/CPU-Druck durch N residente
Solution-Loads, obj/bin-Lock-Konflikte zwischen laufenden Builds und geladenen
Workspaces, bzw. das harte Exit-1 beim Start mit parameterlosem `--mcp-log`, wenn die
Solution noch nicht auflösbar war (EPIC-09-Verhalten). Diese Hypothesen sind hier nicht
verifiziert und bedürfen ggf. separater Klärung.

**Doku-Drift nebenbei gefunden:** `Docs/ROADMAP.md` (EPIC-09) beschreibt als Default-Pfad
noch `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` mit hartem Abbruch bei
nicht auflösbarer Solution. Tatsächlich ist der Default heute
`%LOCALAPPDATA%\RalfHuesing\McpObservability\ainetlinter\<yyyy-MM-dd>\` mit
prozessunique Dateinamen und ohne Abbruch (`McpServerCommand.cs:104-141`,
`ObservabilityContext.cs:85-107`). Bei Gelegenheit korrigieren (passt in die Tradition
von "01_dokumentations-und-begriffsdrift-beseitigen").

**Anpassung des Analyzer-Designs (Teil 2 dieses Tasks):**
- Eingabe ist ein **Verzeichnis oder Glob** (`{serverName}_*_*.jsonl` über Tagesordner),
  nie eine einzelne Datei.
- **Session-Korrelation wird trivial:** Eine Datei = ein Prozess = eine Session. Die
  bisherige Metrik "Session-/Prozess-Korrelation" entfällt als Analyseproblem; stattdessen
  Aggregation über Dateien plus Ausweisung von PID/InstanceId je Report-Abschnitt.


## Vorschlag: Offline-Auswertung als CLI-Kommando (kein neues MCP-Tool)

Bewusst **nicht** als MCP-Tool (Anti-Proliferations-Entscheidung), sondern als
CLI-Batch-Funktion der bestehenden .exe:

```
ainetlinter --analyze-mcp-log <pfad-auf-calls.jsonl-oder-logdir> [--format text|json]
```

Report-Inhalte (alle rein aus dem vorhandenen Log ableitbar):

| Metrik | Beantwortet |
|---|---|
| Calls pro Tool | Welche Tools werden wirklich genutzt? (Grundlage für Removal-Entscheidung) |
| Fehlercode-Verteilung + isError-Rate | Welche recoverable-Bedingungen treten am häufigsten auf? (Hint-/Description-Qualität) |
| Loading-Retry-Bursts (gleiches Tool mehrfach in Sekundenabstand) | Wie oft pollt der Client den Loading-Zustand? (Evidenz für Aufgabe 08, Teil 3) |
| Truncation-/Completeness-Häufigkeit | Welche Limits greifen in der Praxis? (Kontextbudget-Feintuning) |
| Session-/Prozess-Korrelation | Wie lang sind typische Sessions? Welche Call-Sequenzen (Workflows) laufen real? |

Zusatznutzen: Die Sequenzanalyse zeigt, welche der in `OverviewResourceRegistration`
empfohlenen Workflows Agenten tatsächlich laufen — direktes Feedback auf die eigene
Agenten-Führung.

## Scope

### Must-have

- Echte Aggregate in `get_server_health` (Text-Sektion + `structuredContent`).
- `--analyze-mcp-log` mit deterministischem Report (Text + JSON) über alle Metriken oben.
- Unit-Tests mit Fixture-JSONL; kein Netz, kein externes Tool.

### Non-Goals

- Kein neues MCP-Tool, keine Live-Abfrage des Logs über den Server.
- Kein Log-Rotation-/Aufbewahrungskonzept in diesem Task.
- Keine Token-Schätzungen; Zeichen-/Byte-Zahlen und Counts sind die Metriken.

## Definition of Done

- `get_server_health` liefert echte Aggregate (Unit-Test mit injiziertem Fake-Log).
- `--analyze-mcp-log` erzeugt aus einer Fixture-JSONL einen deterministischen Report.
- Doc-Sync: `Docs/integration.md` (Abschnitt Observability) + ggf. `Docs/ROADMAP.md`.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.
