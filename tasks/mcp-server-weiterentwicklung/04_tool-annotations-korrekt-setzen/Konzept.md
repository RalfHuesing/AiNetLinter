---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
herkunft: "1:1 uebernommen aus tasks/mcp-agenten-effizienz/06_tool-annotations-korrekt-setzen.md (Konsolidierung 2026-08-21)"
---

# MCP-Tool-Annotations fachlich korrekt setzen

## Ziel

Alle registrierten Tools sollen ihre tatsächlichen Seiteneffekte und Systemgrenzen über MCP-Annotations beschreiben. Der aktuelle SDK-Default darf read-only Solution-Abfragen nicht als potenziell schreibend bzw. open-world erscheinen lassen.

## Warum / Kontext

Alle eigenen Registrierungen setzen derzeit nur `Name` und `Description`. SDK 2.2.0 unterstützt in `McpServerToolCreateOptions` die nullable Properties `ReadOnly`, `Destructive`, `Idempotent` und `OpenWorld`. Ohne explizite Werte müssen Hosts konservative Defaults annehmen.

Annotations sind keine Zugriffssteuerung und laut MCP-Spezifikation für Clients nicht vertrauenswürdig, solange der Server nicht vertrauenswürdig ist. Sie sind dennoch der standardisierte Vertrag, mit dem ein Host Side-Effects, Bestätigungsbedarf und Trust-Grenzen beurteilt. Quelle: [MCP Tools – Tool annotations](https://modelcontextprotocol.io/specification/draft/server/tools#tool).

Der Nutzen ist Protokollkorrektheit und bessere Hostentscheidung; eine Tokenersparnis wird nicht behauptet. Der zusätzliche `tools/list`-Payload muss gemessen werden.

## Verbindliche Klassifikation

| Toolgruppe | ReadOnly | Destructive | Idempotent | OpenWorld | Begründung |
|---|---:|---:|---:|---:|---|
| alle Solution-/Symbol-/Metrik-/Analyse-/Health-Abfragen | `true` | `false` | `true` | `false` | lesen residenten Solutionzustand/lokale Dateien; keine externen Entitäten und keine persistenten Writes |
| `reload_config` | `false` | `false` | `true` | `false` | ersetzt atomar die In-Memory-Konfiguration; Wiederholung mit gleichem Pfad/Zustand hat keinen zusätzlichen Effekt |
| `report_observability_feedback` | `false` | `false` | `false` | `false` | schreibt einen neuen lokalen Feedbackeintrag; Wiederholung erzeugt einen weiteren Eintrag |

`get_impact` bleibt `OpenWorld=false`: Git wird als lokaler Prozess gegen das geladene Repository gelesen; es erfolgt kein Netz-/Remotezugriff. `search_pattern` und `reload_config` bleiben ebenfalls closed-world, da ihre Pfade durch Solution-/Sicherheitsgrenzen beschränkt sind.

## Scope

### Must-have

- Einen zentralen Helper für Options-Erzeugung einführen, beispielsweise:

```csharp
internal static McpServerToolCreateOptions ReadOnlyTool(string name, string description);
internal static McpServerToolCreateOptions ReloadConfigTool(string name, string description);
internal static McpServerToolCreateOptions FeedbackTool(string name, string description);
```

- Alle eigenen Registrierungen verwenden diesen Helper statt vier boolesche Werte zu duplizieren.
- Das vom Observability-Paket registrierte Feedback-Tool über öffentliche SDK-Properties annotieren. Bevorzugt einen vorhandenen Paket-Overload nutzen; falls keiner existiert, nach `AddFeedbackTool` das exakt benannte `McpServerTool` in der Collection bestimmen und dessen öffentliches `ProtocolTool.Annotations` setzen. Keine Reflection.
- Ein zentraler Test klassifiziert jeden registrierten Toolnamen. Ein neues Tool ohne explizite Klassifikation muss den Test fehlschlagen lassen.
- Raw-Wire-`tools/list` prüft exemplarisch je eine read-only Query, `reload_config` und Feedback.
- Dokumentation erklärt, dass Annotations Hinweise und keine Security-Garantie sind.
- Payloadgröße vor/nach Umsetzung mit dem Messhelper aus der Hybridsuche-Initiative dokumentieren.

### Non-Goals

- Keine Änderung der tatsächlichen Berechtigungs- oder Pfadprüfungen.
- Keine UI-Annahmen über einen bestimmten Host.
- Keine Output-Schemas in diesem Task.
- Kein `OpenWorld=true`, nur weil Roslyn oder Git als Bibliothek/Prozess verwendet wird.
- Keine pauschale Annotation des gesamten Servers; Metadaten gehören an jedes Tool.

## Tests

- Menge aller registrierten Namen entspricht exakt der Menge der Klassifikationstabelle im Test.
- Jedes Analysewerkzeug hat `readOnlyHint=true`, `destructiveHint=false`, `idempotentHint=true`, `openWorldHint=false` auf dem Wire.
- `reload_config` hat die Werte `false/false/true/false`.
- Feedback hat `false/false/false/false`.
- Kein Tool bleibt mit `null`-Annotation zurück.
- Legacy- und modernes `tools/list` liefern semantisch dieselben Annotations.
- Der dokumentierte Payload-Delta ist eine Bytezahl, keine geschätzte Tokenzahl.

## Definition of Done

- Alle Tools sind explizit und fachlich korrekt annotiert.
- Zukünftige unklassifizierte Tools brechen einen verständlichen Test.
- Es gibt keine Reflection und keine duplizierten Annotation-Blöcke in jeder Registrierungsdatei.
- Security-Grenze der Hints ist dokumentiert.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.
