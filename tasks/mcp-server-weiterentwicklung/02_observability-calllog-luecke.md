---
status: vorschlag
type: bug-kandidat + feature-luecke
priority: P1
last_updated: 2026-08-21
verified_against: src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs, Cli/CliOptionFactory.cs
---

# 02 — Observability: Call-Log wird geschrieben, aber nie ausgewertet

## Befund A (verifiziert): `get_server_health` meldet hartcodierte Nullen

`GetServerHealthTool.BuildPayload` (`GetServerHealthTool.cs:62-67`):

```csharp
var callLogPayload = observabilityLogPath is null
    ? null
    : new CallLogPayload(observabilityLogPath, 0, 0, new Dictionary<string, int>());
```

Die `CallLogPayload`-Doku (`GetServerHealthModels.cs:28-29`) verweist auf die vorhandenen
`McpCallLog`-Aggregate `EntryCount`, `ErrorCount`, `CallCountsByTool`. Diese Aggregate
existieren — aber dem Health-Tool wird nur der **Pfad** übergeben, nie die Log-Instanz.
Ergebnis: Bei aktivem `--mcp-log` meldet `get_server_health` immer
`EntryCount=0, ErrorCount=0, CallCountsByTool={}`, selbst nach hunderten Calls.

Das ist entweder ein bekannter Platzhalter oder ein Oversight. Aus Agentensicht ist es
irreführend: Das Tool verspricht Observability-Daten und liefert konstant Nullen.

**Fix-Skizze:** Die `McpObservabilityOptions`/Log-Instanz an das Tool durchreichen
(analog zum `mcpState`-Closure-Muster) und die echten Aggregate lesen. Kleiner,
isoliert testbarer Change.

## Befund B (verifiziert): Es gibt keine Auswertung von `calls.jsonl`

EPIC-09 schreibt bei Opt-in strukturierte JSONL-Einträge pro Tool-Call (inkl. Error-Sink).
Eine Suche nach Auswertung/Analyse des Logs findet **nur** die CLI-Option
(`Cli/CliOptionFactory.cs:204-206`) — kein Analyzer, kein Report, kein Kommando.

Damit fehlt dem Projekt genau das, was `90_bewusst-nicht-umsetzen.md` als
Wiederöffnungsbedingung fordert: **reproduzierbare Nutzungsdaten**. Konkrt unentschieden
bleiben aktuell:

- Entfernung/Zusammenlegung von Tools (Entscheidung Nr. 4: "Erst Observability-Nutzungs-
  daten ... sammeln") — es gibt keinen Mechanismus, der diese Daten sichtbar macht.
- Tool-Profile (Nr. 5), Output-Schema-Pilot (Nr. 6): alle hängen an derselben fehlenden
  Evidenzbasis.

## Vorschlag: Offline-Auswertung als CLI-Kommando (kein neues MCP-Tool)

Bewusst **nicht** als MCP-Tool (widerspräche der Anti-Proliferations-Entscheidung), sondern
als CLI-Batch-Funktion der bestehenden .exe, z. B.:

```
ainetlinter --analyze-mcp-log <pfad-auf-calls.jsonl-oder-logdir> [--format text|json]
```

Report-Inhalte (alle rein aus dem vorhandenen Log ableitbar):

| Metrik | Beantwortet |
|---|---|
| Calls pro Tool | Welche Tools werden wirklich genutzt? (Grundlage für Removal-Entscheidung) |
| Fehlercode-Verteilung + isError-Rate | Welche recoverable-Bedingungen treten am häufigsten auf? (Hint-/Description-Qualität) |
| Loading-Retry-Bursts (gleiches Tool mehrfach in Sekundenabstand) | Wie oft pollt der Client den Loading-Zustand? (Evidenz für Progress-Notification, siehe Datei 05) |
| Truncation-/Completeness-Häufigkeit | Welche Limits greifen in der Praxis? (Kontextbudget-Feintuning) |
| Session-/Prozess-Korrelation | Wie lang sind typische Sessions? Welche Call-Sequenzen (Workflows) entstehen real? |

Zusatznutzen: Die Sequenzanalyse zeigt, welche der in `OverviewResourceRegistration`
empfohlenen Workflows Agenten tatsächlich laufen — direktes Feedback auf die eigene
Agenten-Führung.

## Definition of Done

- `get_server_health` liefert echte Aggregate (Unit-Test mit injiziertem Fake-Log).
- `--analyze-mcp-log` erzeugt aus einer Fixture-JSONL einen deterministischen Report
  (Text + JSON), inkl. aller Metriken oben.
- Doc-Sync: `Docs/integration.md` (Abschnitt Observability) + ggf. ROADMAP.
