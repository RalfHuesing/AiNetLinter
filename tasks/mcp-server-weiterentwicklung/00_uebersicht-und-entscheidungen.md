---
status: ready
type: konzept (orchestrator-einstiegspunkt)
project_kind: brownfield
priority: uebersicht
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-24
open_questions: []
---
**EPIC-A-Status (2026-08-24):** Die transportneutrale Multi-Solution-Registry
ist umgesetzt; absolute `projectRoot`-Adressierung, die eigene
Definitionsdatei-/MCP-Migration und der Overview-Resource-Read sind in
`11_epic-projektregistry-und-daemon/step-008` nachgewiesen.

# MCP-Server-Weiterentwicklung: konsolidierte Aufgaben (Einstiegspunkt)

## Rolle und Arbeitsmodus

Dieses Verzeichnis ist ab sofort der **einzige Ort für neue Tasks** rund um den
MCP-Server-Modus. Es konsolidiert (Stand 2026-08-21):

- `tasks/mcp-agenten-effizienz/` — übernommen: alle noch offenen Aufgaben; die dort
  erledigten Aufgaben 01–03 sind hier nur noch als Historie vermerkt.
- `tasks/features/` — übernommen: das offene Feature `similar_names` sowie alle
  Festlegungen "bewusst nicht umsetzen".
- Eigene Review-Findings vom 2026-08-21 (Staleness-Performance, Observability,
  Sprachstrategie, Architektur-Beobachtungen, Regel-Design).

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

## Sprachregelung (Entscheidung 2026-08-21)

- **MCP-Kommunikation intern komplett auf Englisch umstellen:** `ServerInstructions.Text`,
  Tool-Descriptions, Antworttexte (`[INFO]/[ERROR]`-Texte). Fehler-Codes sind bereits
  englisch. → Aufgabe 05.
- **Externe Dokumentation bleibt Deutsch:** `README.md`, `Docs/**` — sofern der
  MCP-Server diese Inhalte nicht direkt ausgibt.
- **Projekt-Kommunikation und Konzepte (tasks/, .agents/rules-Richtlinien) bleiben Deutsch.**
- Offener Punkt: Das generierte `.agents/rules/AiNetLinter.mdc` wird aus `rules.json`
  erzeugt und spiegelt u. U. Tool-Descriptions. Folgt der Sync automatisch der englischen
  Beschreibung, ist das akzeptiert (agentengerichteter Kontext); andernfalls bei der
  Umsetzung von Aufgabe 05 prüfen und hier dokumentieren.

## Ausführungsreihenfolge (Offene Tasks)

| Reihenfolge | Aufgabe | Status | Priorität | Herkunft |
|---:|---|---|---|---|
| 1 | [04_tool-annotations-korrekt-setzen.md](04_tool-annotations-korrekt-setzen/Konzept.md) | offen | P2 | mcp-agenten-effizienz/06 |
| 2 | [05_wire-texte-mcp-intern-auf-englisch.md](05_wire-texte-mcp-intern-auf-englisch/Konzept.md) | offen | P2 | Review-Finding + Entscheidung |
| 3 | [06_similar-names-naming-drift.md](06_similar-names-naming-drift/Konzept.md) | offen | P2 | features/04 |
| 4 | [07_tools-list-cachehinweise-setzen.md](07_tools-list-cachehinweise-setzen/Konzept.md) | offen | P3 | mcp-agenten-effizienz/07 |
| 5 | [08_config-resource-und-kleine-mcp-erweiterungen.md](08_config-resource-und-kleine-mcp-erweiterungen/Konzept.md) | offen | P3 | Review-Finding |
| 6 | [09_regel-design-audit-kandidaten.md](09_regel-design-audit-kandidaten/Konzept.md) | offen | P3 | Review-Finding |
| 7 | [10_architektur-monitoring.md](10_architektur-monitoring/Konzept.md) | laufend | P3 | Review-Finding |
| — | [90_bewusst-nicht-umsetzen.md](90_bewusst-nicht-umsetzen/Konzept.md) | Festlegung | P9 | konsolidiert |

## Begründung der Reihenfolge

1. **04 (Tool-Annotations):** Definiert formale MCP-SDK-Annotations (ReadOnly, Idempotent etc.) für standardkonforme Host-Interaktion.
2. **05 (Wire-Texte auf Englisch):** Stellt Tool-Beschreibungen und Antworttexte konsistent auf Englisch um.
3. **06 (Similar Names):** Audit-Funktion für Naming-Drift auf Symbol-/Signatur-Ebene.
4. **07–10:** Additive Optimierungen, Ressourcen und dauerhaftes Monitoring.

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
| mcp-agenten-effizienz/05 (get_impact change-context) | offen | → 03 hier |
| mcp-agenten-effizienz/06 (Tool-Annotations) | offen | → 04 hier |
| mcp-agenten-effizienz/07 (tools/list Cache-Hinweise) | offen | → 07 hier |
| mcp-agenten-effizienz/90 (bewusst nicht umsetzen) | Festlegung | → 90 hier (merged) |
| features/00 (Übersicht) | Verzeichnis | → diese Datei |
| features/04 (similar_names) | offen | → 06 hier |
| features/05 (bedingt sinnvoll: ASP.NET-Suite) | zurückgestellt | → 90 hier |
| features/06 (nicht umsetzen) | Festlegung | → 90 hier (merged) |

## Definition of Done für die Konsolidierung

- Alle offenen Aufgaben und alle "bewusst nicht umsetzen"-Festlegungen sind hier
  inhaltlich vollständig vorhanden (die Quellverzeichnisse können nach Abschluss der
  laufenden Hybridsuche gelöscht werden, ohne Inhalt zu verlieren).
- Keine Aufgabe existiert doppelt; die laufende Hybridsuche wird nur referenziert.
- Die Ausführungsreihenfolge ist priorisiert und mit Abhängigkeiten begründet.
