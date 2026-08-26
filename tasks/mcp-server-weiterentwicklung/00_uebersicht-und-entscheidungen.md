---
status: ready
type: konzept (orchestrator-einstiegspunkt)
project_kind: brownfield
priority: uebersicht
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-26
open_questions: []
---
**EPIC-A-Status (2026-08-24):** Die transportneutrale Multi-Solution-Registry
ist umgesetzt; absolute `projectRoot`-Adressierung, die eigene
Definitionsdatei-/MCP-Migration und der Overview-Resource-Read sind in
`11_epic-projektregistry-und-daemon/step-008` nachgewiesen.

# MCP-Server-Weiterentwicklung: konsolidierte Aufgaben (Einstiegspunkt)

## Rolle und Arbeitsmodus

Dieses Verzeichnis ist ab sofort der **einzige Ort für neue Tasks** rund um den
MCP-Server-Modus. Es konsolidiert (Stand 2026-08-26):

- `tasks/mcp-agenten-effizienz/` — übernommen: alle noch offenen Aufgaben; die dort
  erledigten Aufgaben 01–03 sind hier nur noch als Historie vermerkt.
- Eigene Review-Findings vom 2026-08-21 (Observability und Config-Resource).

Jede Einzelaufgabe ist eigenständig ausführbar und folgt dem Format der bewährten
Aufgaben aus `mcp-agenten-effizienz` (Ziel / Warum / Vertrag / Scope / Tests /
Definition of Done).

## Verzeichniskonvention

- Jede Aufgabe liegt in einem **eigenen Unterverzeichnis** (`NN_<name>/`) und dort als
  `Konzept.md` — das ist der Einstiegspunkt und die Single Source of Truth des Tasks.
- Umsetzungsschritte (falls verwendet) liegen als weitere Unterverzeichnisse daneben
  (`NN_<name>/step-001/step-plan.md`, `step-result.md`, ...), analog zum Muster in
  `tasks/mcp-agenten-effizienz/04_...`.
- Diese Datei (`00_uebersicht-und-entscheidungen.md`) ist der **Index** und ändert sich
  nur bei Priorisierung, Status oder neuen Aufgaben.


> [!NOTE]
> **`tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/` wurde am
> 2026-08-21 abgeschlossen** (Review approved, Ergebnis in `Docs/ROADMAP.md`). Das
> Quellverzeichnis `tasks/mcp-agenten-effizienz/` ist nach Abschluss gelöscht; die
> Arbeitsdokumente bleiben über die Git-Historie erreichbar.

## Ziel

AiNetLinter soll als allgemeiner MCP-Server in beliebigen C#/.NET-Codebasen einem
Coding-Agenten den kleinsten hinreichenden Kontext liefern — bei korrekten Verträgen,
nachvollziehbarer Performance und einer Evidenzbasis (Nutzungsdaten) für künftige
Tool-Entscheidungen. Nicht auf dieses Repository optimieren; Fixtures und Akzeptanztests
verwenden neutrale, mehrprojektige C#-Solutions.

## Ausführungsreihenfolge (Offene Tasks)

| Reihenfolge | Aufgabe | Status | Priorität | Herkunft |
|---:|---|---|---|---|
| 1 | [04_tool-annotations-korrekt-setzen](04_tool-annotations-korrekt-setzen/Konzept.md) | offen | P2 | mcp-agenten-effizienz/06 |
| 2 | [07_tools-list-cachehinweise-setzen](07_tools-list-cachehinweise-setzen/Konzept.md) | offen | P3 | mcp-agenten-effizienz/07 |
| 3 | [08_config-resource-und-kleine-mcp-erweiterungen](08_config-resource-und-kleine-mcp-erweiterungen/Konzept.md) | offen | P3 | Review-Finding |
| — | [90_bewusst-nicht-umsetzen.md](90_bewusst-nicht-umsetzen/Konzept.md) | Festlegung | P9 | konsolidiert |

## Begründung der Reihenfolge

1. **04 (Tool-Annotations):** Definiert formale MCP-SDK-Annotations (ReadOnly, Idempotent etc.) für standardkonforme Host-Interaktion.
2. **07 (Cache-Hinweise):** Ergänzt standardisierte Cache-Hints für die statische Toolliste.
3. **08 (Config-Resource):** Macht die effektive Regelkonfiguration über eine Resource sichtbar.

## Architektur- und Verfahrensentscheidungen (fortgeltend)

- Kein RAG, keine Embeddings, kein Semantic Kernel, kein Vektorspeicher.
- Keine neuen MCP-Tools ohne Nutzungsdaten und ohne Prüfung gegen `90_bewusst-nicht-umsetzen.md`.
- Keine mutierenden Refactoring-Tools; der Server bleibt strikt read-only Analysewerkzeug.
- Keine modellabhängigen Token-Schätzungen in Tests; UTF-8-Bytes und JSON-Zeichen sind die Proxies.
- Keine Breaking Removal bestehender Tools ohne Nutzungsdaten und Deprecation-Pfad.
- Kein DI-Container; Tools erreichen den residenten Serverzustand per Delegate-Closure.
- Dokumentations-Objektivität gemäß `AiNetLinterRichtlinien.mdc` §1 (nur Implementiertes,
  sachlich, gegen Code verifiziert).

## Historie / Herkunftsmapping (nach Löschung der Quellverzeichnisse)

| Quelle | Status dort | Verbleib |
|---|---|---|
| mcp-agenten-effizienz/01 (Doku-/Begriffsdrift) | erledigt | nur Historie |
| mcp-agenten-effizienz/02 (Discovery-Kontextbudget + Protokolltests) | erledigt | nur Historie |
| mcp-agenten-effizienz/03 (transitive Symbolgraph-Ausgaben) | erledigt | nur Historie |
| mcp-agenten-effizienz/04 (Hybridsuche) | erledigt (2026-08-21) | Verzeichnis gelöscht; Historie über Git, Ergebnis in `Docs/ROADMAP.md` |
| mcp-agenten-effizienz/06 (Tool-Annotations) | offen | → 04 hier |
| mcp-agenten-effizienz/07 (tools/list Cache-Hinweise) | offen | → 07 hier |
| mcp-agenten-effizienz/90 (bewusst nicht umsetzen) | Festlegung | → 90 hier (merged) |
| features/00 (Übersicht) | Verzeichnis | → diese Datei |
| features/05 (bedingt sinnvoll: ASP.NET-Suite) | zurückgestellt | → 90 hier |
| features/06 (nicht umsetzen) | Festlegung | → 90 hier (merged) |

## Definition of Done für die Konsolidierung

- Alle aktiven Aufgaben und alle "bewusst nicht umsetzen"-Festlegungen sind hier
  inhaltlich vollständig vorhanden.
- Keine Aufgabe existiert doppelt; die laufende Hybridsuche wird nur referenziert.
- Die Ausführungsreihenfolge ist priorisiert und mit Abhängigkeiten begründet.
