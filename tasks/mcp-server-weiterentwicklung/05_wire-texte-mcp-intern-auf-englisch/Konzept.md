---
status: Nochmal 360Grad Audit machen
type: konzept (entscheidung umgesetzt)
project_kind: brownfield
estimated_scope: medium-large
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
entscheidung: "2026-08-21 durch Nutzer bestaetigt"
---

# MCP-Kommunikation intern auf Englisch umstellen

## Entscheidung (2026-08-21)

Die Kommunikation des MCP-Servers wird **komplett intern auf Englisch** umgestellt.
Externe Dokumentation (`README.md`, `Docs/**`) bleibt Deutsch — sofern der MCP-Server
diese Inhalte nicht direkt ausgibt. Projekt-Kommunikation und Konzepte (tasks/,
Richtlinien) bleiben Deutsch.

## Befund (Ausgangslage, verifiziert)

Alle Texte, die den Server verlassen (Wire-Ebene), sind derzeit Deutsch ohne Umlaute:
"geaendert", "Loesung", "fuer". Beispiele: `ServerInstructions.Text` (2557 Bytes),
alle Tool-Descriptions in `*ToolRegistrations.cs`, Antworttexte der Scanner
(`"[INFO]: Server laedt die Solution noch."`). Dagegen verwenden Docs und Task-Dateien
korrekte Umlaute.

Die ASCII-Transliteration ist worst-of-both:

1. Kein messbarer Byte-Vorteil gegenüber echtem Deutsch (nur Umlaut-Wörter wären betroffen).
2. Nicht-Wörter ("geaendert", "aufgeloest") sind im Training von Sprachmodellen selten;
   Tokenizer können sie ungünstiger zerlegen als echtes Deutsch oder Englisch.
3. Ökosystem-Norm für MCP-Server ist Englisch; Host-Modelle sind darauf am stärksten
   kalibriert. Ein international nutzbares Analysewerkzeug mit deutschsprachigen
   Tool-Descriptions ist eine unnötige Zugangshürde.
4. Inkonsistenz zwischen Ebenen ("gelöscht" in Docs vs. "geloescht" auf dem Wire) kostet
   einen Agenten Aufmerksamkeit beim exakten Zitieren und Suchen.

## Scope

### Must-have (Phasen)

1. **Phase 1 — Discovery-Ebene:** `ServerInstructions.Text` + alle Tool-Descriptions in
   `*ToolRegistrations.cs` + Kurzsummaries in `OverviewResourceRegistration.ToolSummaries`
    - `BuildOverviewText`. Geschlossener Surface; Raw-Wire-Probes existieren bereits für die
      Byte-Messung vorher/nachher.
2. **Phase 2 — Antworttexte:** `[INFO]/[ERROR]`-Texte der Scanner und `McpToolResults`
   (Loading-Text, Hints, Sufficiency-/Drilldown-Hints). Fehler-_Codes_
   (SYMBOL_NOT_FOUND etc.) sind bereits englisch — Phase 2 macht die Antworten konsistent.
3. **Phase 3 — Abhängige Artefakte prüfen:** Das generierte `.agents/rules/AiNetLinter.mdc`
   spiegelt u. U. Tool-Descriptions. Ergebnis dokumentieren (entweder: Sync folgt automatisch,
   akzeptiert; oder: Sync-Generator anpassen). XML-Docs im Code bleiben wie sie sind
   (Entwickler-/Konzeptebene, deutsch).

### Sprachregelung (fortgeltend, siehe auch 00_uebersicht)

| Ebene                                                  | Sprache               |
| ------------------------------------------------------ | --------------------- |
| Wire (Instructions, Descriptions, Antworten)           | Englisch              |
| Fehler-Codes                                           | Englisch (bereits so) |
| `README.md`, `Docs/**`                                 | Deutsch               |
| tasks/, `.agents/rules/*Richtlinien*`, Commit-Messages | Deutsch               |

### Non-Goals

- Keine Vertragsänderungen: Toolnamen, Parameternamen, JSON-Feldnamen, Fehler-Codes
  bleiben unverändert — nur freier Text wandert.
- Keine Übersetzung der Code-Kommentare oder XML-Docs.
- Keine Token-Ersparnisbehauptung; Messung erfolgt als UTF-8-Bytes vorher/nachher.

## Tests / Nachweise

- Bestehende Tests, die deutsche Wire-Texte asserten (Loading-Text, Overview,
  Fehler-Hints), werden auf englische Erwartung umgestellt — reiner Textumbau.
- Byte-Zahlen für `tools/list`, `server/discover` vor/nachher dokumentieren
  (Messhelper aus der Hybridsuche-Initiative).
- `--sync-agent-rules-only`-Lauf nach Phase 1; Diff von `AiNetLinter.mdc` prüfen und das
  Ergebnis unter "Offener Punkt" in `00_uebersicht-und-entscheidungen.md` nachtragen.

## Definition of Done

- Kein deutscher (oder transliterierter) Text verlässt den Server auf dem Wire.
- `Docs/agent-api.md` zeigt Beispielantworten in der neuen Sprache; README bleibt Deutsch.
- Byte-Messung vorher/nachher dokumentiert.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.

