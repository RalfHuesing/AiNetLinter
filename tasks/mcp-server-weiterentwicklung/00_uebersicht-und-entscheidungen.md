---
status: ready
type: konzept (orchestrator-einstiegspunkt)
project_kind: brownfield
priority: uebersicht
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions:
  - "Wann wird tasks/mcp-agenten-effizienz manuell geloescht? Erst NACH Abschluss der laufenden Hybridsuche-Umsetzung (siehe Warnung unten)."
---

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


> [!WARNING]
> **`tasks/mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/` wird
> GERADE von einem anderen Agenten umgesetzt** (Arbeitsverzeichnis mit codemap/roadmap/
> step-00x liegt dort). Diese Aufgabe ist hier bewusst **nicht dupliziert**. Das
> Originalverzeichnis `tasks/mcp-agenten-effizienz/` darf erst **nach Abschluss** dieser
> Umsetzung manuell gelöscht werden — vorher würde dem Agenten die Arbeitsgrundlage
> entzogen. Nach Abschluss: Status hier in der Tabelle auf "erledigt" setzen, erst dann löschen.

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

## Ausführungsreihenfolge

| Reihenfolge | Aufgabe | Status | Priorität | Abhängigkeit | Herkunft |
|---:|---|---|---|---|---|
| — | Hybridsuche repositoryweit + Kontextbudget (Original: `../mcp-agenten-effizienz/04_repositoryweite-hybridsuche-und-kontextbudget/`) | **in Umsetzung** (anderer Agent) | P1 | 01–03 dort erledigt | mcp-agenten-effizienz/04 |
| 1 | [01_observability-calllog-fix-und-auswertung.md](01_observability-calllog-fix-und-auswertung/Konzept.md) | offen | P1 | keine | Review-Finding |
| 2 | [02_staleness-check-performance.md](02_staleness-check-performance/Konzept.md) | offen | P1 | keine | Review-Finding |
| 3 | [03_get-impact-zum-diff-kontext-erweitern.md](03_get-impact-zum-diff-kontext-erweitern/Konzept.md) | offen | P2 | Hybridsuche (strukturierte transitive Ausgaben, dort erledigt als deren Aufgabe 03) | mcp-agenten-effizienz/05 |
| 4 | [04_tool-annotations-korrekt-setzen.md](04_tool-annotations-korrekt-setzen/Konzept.md) | offen | P2 | Byte-Messhelper aus Hybridsuche-Initiative (erledigt) | mcp-agenten-effizienz/06 |
| 5 | [05_wire-texte-mcp-intern-auf-englisch.md](05_wire-texte-mcp-intern-auf-englisch/Konzept.md) | offen | P2 | keine (Textumbau) | Review-Finding + Entscheidung |
| 6 | [06_similar-names-naming-drift.md](06_similar-names-naming-drift/Konzept.md) | offen | P2 | empfohlen: nach 01 (Nutzungsdaten für Audit-Tools) | features/04 |
| 7 | [07_tools-list-cachehinweise-setzen.md](07_tools-list-cachehinweise-setzen/Konzept.md) | offen | P3 | keine | mcp-agenten-effizienz/07 |
| 8 | [08_config-resource-und-kleine-mcp-erweiterungen.md](08_config-resource-und-kleine-mcp-erweiterungen/Konzept.md) | offen | P3 | 01 (Loading-/Nutzungsevidenz für Teile 2+3) | Review-Finding |
| 9 | [09_regel-design-audit-kandidaten.md](09_regel-design-audit-kandidaten/Konzept.md) | offen | P3 | 01 (Nutzungsevidenz), Hybridsuche | Review-Finding |
| 10 | [10_architektur-monitoring.md](10_architektur-monitoring/Konzept.md) | offen | P3 | laufend, keine harte Abhängigkeit | Review-Finding |
| — | [90_bewusst-nicht-umsetzen.md](90_bewusst-nicht-umsetzen/Konzept.md) | Festlegung | P9 | fortlaufend | konsolidiert |

## Begründung der Reihenfolge

1. **01 vor fast allem:** Der `get_server_health`-Null-Bug ist ein Korrektheitsfix; die
   Call-Log-Auswertung liefert die Nutzungsdaten, die mehrere spätere Entscheidungen
   (Tool-Removal, Profile, Prompts, Regel-Priorisierung) erst entscheidungsfähig machen.
2. **02 parallel zu 01 möglich:** Performance-Fix mit kleinem, isoliertem Scope; entlastet
   jede fremde Codebase bei jedem Tool-Call.
3. **03 und 04** setzen auf der abgeschlossenen Hybridsuche-Initiative auf und sind
   unabhängig voneinander.
4. **05** ist ein reiner Textumbau ohne Vertragsänderung — jederzeit einwerfbar, aber nach
   01/02 priorisiert, weil Antworttexte (Loading, Fehler-Hints) von Agenten am häufigsten
   gelesen werden.
5. **06–10** sind eigenständig; 10 ist ein dauerhafter Monitoring-Auftrag, kein einmaliger Task.

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
| mcp-agenten-effizienz/04 (Hybridsuche) | **in Umsetzung** | bleibt im Original bis Abschluss |
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

