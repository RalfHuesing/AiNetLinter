---
status: ready (auditiert)
type: konzept (entscheidung umgesetzt)
project_kind: brownfield
estimated_scope: medium-large
priority: P2
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
audit: zweiter Pass 2026-08-21 abgeschlossen (Abschnitt unten); Phase 3 durch Nutzer bestaetigt
open_questions: []
entscheidung: "2026-08-21 durch Nutzer bestaetigt; Phase-3-Folgeentscheidung (CLI schaltet mit) 2026-08-21 bestaetigt"
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

---

# Audit zweiter Pass (2026-08-21): Funde, Scope-Korrektur, 360°-Blick

Verifiziert per Code-/Test-Grep über `src/AiNetLinter/Mcp`, `Core/RuleRegistry*`,
`Models/RuleViolation.cs`, `Output/RuleLegendRegistry.cs`, beide Testprojekte und
`Docs/`. Kernbefund: Die Phasen 1–2 sind korrekt, aber der Wire enthält eine **zweite,
bisher nicht gescoppte Textklasse** — die Regel-Fachtexte. Neu geordnet ergibt sich
Phase 3; außerdem sechs Präzisierungen.

## A. Neue Textklasse entdeckt: Regel-Fachtexte auf dem Wire (wird Phase 3)

`get_violations` liefert je Treffer `Details` und `Guidance`
(`Models/RuleViolation.cs:11-12`) — deutsche Fachtexte aus der `RuleRegistry`
(z. B. Remediation "Methode zu 'async Task' umwandeln …", `RuleRegistry.cs:444`;
Warum-Texte `RuleRegistry.DuplicateDetection.cs:15`). Das sind die **am häufigsten
konsumierten Texte überhaupt** (jeder `get_violations`-Aufruf) — ein Englisch-Umbau,
der sie auslässt, läge unter der eigenen Zielsetzung "komplett intern Englisch".

**Komplikation:** Dieselben Texte erscheinen im CLI-Output
(`Output/RuleLegendRegistry.cs` greift auf dieselben Registry-Felder zu). Die Umstellung
ist also nicht MCP-lokal:

- **Empfehlung (Phase 3):** RuleRegistry-Texte (`Details`/`Guidance`/Remediation/Warum)
  auf Englisch umstellen; die CLI-Violations-Ausgabe schaltet damit bewusst mit.
  CLI ist keine "externe Doku" — die Sprachregelung verbietet das nicht, und ein
  Formatter-Split (CLI Deutsch / MCP Englisch) wäre dauerhafte Duplikation pro Regel.
- **Bestätigt durch Nutzer 2026-08-21:** CLI-Violations-Ausgabe schaltet auf Englisch mit;
  kein Formatter-Split. Phase 3 ist damit freigegeben (eigener Step, eigener Commit).
- Umfang: ~30+ Regeln mit Warum/Remediation-Texten in `Core/RuleRegistry*.cs` —
  eigenständiger Coding-Step, nicht an Phase 1/2 dranhängen.

## B. Bestätigt: Umfang der Framework-Texte (Phase 1+2)

Verifizierte deutsche/transliterierte Wire-Quellen: alle 26 Tool-Descriptions
("Wann nutzen: …" in den fünf `*ToolRegistrations.cs`), `ServerInstructions.Text`,
`OverviewResourceRegistration` (Summaries + Overview-Markdown),
`McpSufficiencyHints.Append` ("[HINWEIS]: Diese Daten sind vollstaendig …"),
`McpDrillDownHints`, `McpToolResults.Loading()` ("[INFO]: Server laedt …"),
`LinterErrorFormatter`-Messages/Hints je Fehlercode. Fehler-Codes sind bereits Englisch.

## C. Test-Landschaft: 8 Dateien fixieren deutsche Strings

Grep-Treffer für deutsche Serverstrings in Tests: u. a.
`McpServerCommandLoadingStateTests`, `GetCallTreeToolTests`,
`McpServerCommandErrorHandlingTests`, `McpCodeGraphServerStalenessMtimeCacheTests`,
`MarkdownBuilderTests`, `AnalysisCacheManagerIsolationTests`,
`LoadFixtureMeasurementsTests`, `McpProcessHost`.

**Triage-Regel für die Umsetzung:** Nur Assertions auf **Wire-Texte des MCP-Pfads**
stellen um. Assertions auf CLI-/Cache-interne deutsche Strings (CacheManager, Markdown-
Builder, LoadFixtures) bleiben unverändert — sie gehören nicht zum MCP-Wire und würden
sonst den Scope unbegründet aufblähen.

## D. Budget-Konstante mitziehen

`ServerInstructions.MaxUtf8Bytes = 2557` wird von
`McpServerOptionsFactoryTests` gegen den aktuellen Text geprüft. Die englische Fassung
ändert die Byte-Zahl in beide Richtungen möglich — die Konstante ist mit der neuen
Fassung neu festzulegen (nicht blind zu übernehmen), und die Raw-Wire-Probes messen
vorher/nachher gemäß Konzept.

## E. Docs-Detail: Beispiele in Deutschsprachiger Doku werden englisch

`Docs/agent-api.md` und `Docs/integration.md` enthalten jeweils mindestens einen
deutschen Serverstring in Beispiel-Ausgaben; README laut Grep nicht. Präzisierung der
Sprachregelung: Prosa bleibt Deutsch, **abgedruckte Server-Ausgaben** werden auf die
neue Wire-Sprache gesetzt — sonst driftet die Doku sofort vom realen Verhalten
(Verstoß gegen die Dokumentations-Objektivität, Richtlinien §1).

## F. IsErrorPolicy.md ist Teil der Änderung

Die Policy-Tabelle zitiert deutsche Hint-Formulierungen als Spezifikation der
Antworttexte. Der Umbau ohne Mitführung der Policy-Datei erzeugt sofortige
Spezifikations-Drift — Policy-Update gehört in denselben Step wie Phase 2.

## G. Ergänzte DoD-Punkte

- Phase 1/2 wie oben, ergänzt um: `McpSufficiencyHints`, `McpDrillDownHints`,
  `LinterErrorFormatter`-Messages/Hints, `McpToolResults.Loading()`, Overview-Markdown.
- `ServerInstructions.MaxUtf8Bytes` nach neuer Fassung neu festgelegt und im
  Options-Factory-Test grün.
- Test-Triage dokumentiert: Liste der umgestellten vs. bewusst nicht angefassten
  Testdateien liegt dem Step-Result bei.
- `Docs/agent-api.md`/`Docs/integration.md`: Beispiel-Ausgaben auf Wire-Sprache,
  Prosa Deutsch.
- Phase 3 (RuleRegistry) nur nach Bestätigung der offenen Frage; dann eigener Step mit
  eigenem Commit, inkl. Review der CLI-Auswirkung (`RuleLegendRegistry`).



