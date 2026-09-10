---
status: ready
execution_mode: autonomous
open_questions: []
---

# Konzept: Offene Live-360-MCP-UX-Befunde schliessen

## Ziel und Problem

Die Live-360-Pruefung hat allgemeingueltige MCP-Vertragsluecken im laufenden, dem Source entsprechenden Server gezeigt. Sie betreffen Routing, Argumentbindung, Begrenzung, Vollständigkeit und strukturierte Agenten-Handoffs. Ziel ist, alle in `FINDINGS.md` als offen markierten umsetzbaren Befunde mit Red-Test-First zu schliessen.

## Muss-Kriterien

1. Die Befunde L360-001 bis L360-012 werden behoben, soweit sie ohne Breaking Change und ohne neue Produktfunktion moeglich sind.
2. E-003 bleibt unveraendert deferred; keine semantische Neuinterpretation von `navigation.completeness`.
3. Oeffentliche MCP-Antworten bleiben rueckwaertskompatibel: Erweiterungen nur additiv, bestehende Felder nicht umbenennen.
4. Jeder Befund erhaelt vor dem Fix einen reproduzierbaren Test gegen die reale oeffentliche Route oder, bei reiner Logik, einen Unit-Test. Jeder Fix wird gezielt gruen nachgewiesen.
5. Tests verwenden ausschliesslich repo-eigene oder synthetische Fixtures. Keine externe Assembly oder Zielprojektdaten werden persistiert.
6. Betroffene MCP-Dokumentation und Agentenworkflow werden mit dem belegbaren Vertrag synchronisiert.

## Slices und technische Grenzen

### Slice 1: Health-Routing und Snapshot-Vertrag

L360-001, L360-002 und L360-010. Globale Health-Aggregation, positive Diagnose-Limits vor jedem Dispatch sowie korrelierbare oder klar nicht erhobene Snapshots. Keine Sessiondaten im globalen Detailpfad.

### Slice 2: Oeffentliche Argumentbindung, Limits und Fallbacks

L360-007, L360-009, L360-011 und L360-012. Feldgenaue Fehler fuer fehlende Pflichtfelder und Arrayelemente; konsistente Limit- und Clamp-Semantik; keine stillen Enum-Fallbacks ohne sichtbaren Effektivwert.

### Slice 3: Discovery-Budgets und strukturierte Vollständigkeit

L360-003, L360-004 und L360-005. Namespace-Praefixnormalisierung, wirksame Response-Budgets und eine einheitliche sichtbare Teilmenge fuer Text und StructuredContent.

### Slice 4: Symbolfilter und Handoff-DTOs

L360-006 und L360-008. Strikter `find_symbol.kind`-Filter und additive strukturierte Daten fuer Skeleton-/Feature-Context-Handoffs.

## Akzeptanzkriterien

- Jede betroffene Route liefert fuer identische Semantik dieselbe Validierung, effektive Begrenzung und Navigation.
- Text und StructuredContent widersprechen sich nicht bei Counts, sichtbarer Teilmenge, Trunkierung oder `next`.
- Ein Agent kann die in Skeleton und Feature-Context ausgegebenen IDs ohne Markdown-Parsing an den vorgesehenen Folgetoolvertrag uebergeben.
- Globales Health bleibt kompakt und nicht detailoffenlegend; zielgebundenes Health befolgt ein positives Diagnose-Limit.
- Alle Nicht-Stress-Gates sind gruen: `dotnet build`, FastTests und IntegrationTests.

## Non-Goals

- Keine Vertragsrevision fuer E-003.
- Keine neuen Analysefunktionen, keine Architekturrefactorings und keine projektspezifischen Sonderfaelle fuer die geprueften Source-Targets.
- Keine Release-, Deployment- oder Installationsaenderung.

## Risiken und Leitentscheidungen

- SDK-Bindungsfehler koennen vor Tooldelegates entstehen. Zentrale Filter duerfen nur Fehler normalisieren, die aus Requestargumenten eindeutig ableitbar sind; interne Fehler bleiben echte Fehler.
- Response-Budgets duerfen keine inkonsistenten Teilstrukturen erzeugen. Sichtbare Datenmenge ist einmal zu bestimmen und fuer Text sowie StructuredContent wiederzuverwenden.
- Additive Handoff-DTOs duerfen bestehende Markdown-Ausgaben nicht ersetzen.

## Verifikation und Dokumentation

- Pro Befund: roter Test, minimaler Fix, gezielter gruener Test.
- Nach jedem Slice: passender Testbereich und Diff-Pruefung.
- Abschluss: Build, beide Nicht-Stress-Suites, Aktualisierung von `Docs/agent-api.md`, `.agents/rules/AiNetLinter-McpWorkflow.mdc`, `README.md` nur wenn eine allgemeine Nutzerbeschreibung betroffen ist, sowie Audit-Artefakte.
