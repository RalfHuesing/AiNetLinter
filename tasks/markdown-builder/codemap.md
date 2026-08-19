---
task: markdown-builder
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-19
last_updated_by: coder (step-001)
---

# CodeMap: markdown-builder

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
`<task-dir>` gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Verhaltensbehauptungen veralten,
Ortsangaben kaum. Wer mehr wissen muss, liest die Datei selbst nach —
das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt (anders als generische Doku):**
Der gesamte Loop läuft strikt seriell — genau ein Subagent gleichzeitig
(`../spec.md` §6). Zwischen einem Coder-Update und dem nächsten Lesezugriff
kann sich am Code strukturell nichts geändert haben, was hier nicht auch
eingetragen wurde. Die Map ist also, solange sie gepflegt wird, tatsächlich
aktuell — kein Snapshot mit Drift-Risiko. **Schritt 2 im Step-Modus des
Planers („tatsächlichen Projektzustand lesen", `../spec.md` §7.2) bleibt
trotzdem Pflicht** — die Map sagt *wo* nachschauen, ersetzt nie das
Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick, den er beim Ableiten der Epics ohnehin über den
  Bestandscode gewinnt (`../skills/planer/SKILL.md` Roadmap-Modus
  Schritt 1).
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (`../skills/coder/SKILL.md` Schritt 6a).
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht (Teil von Ebene 1, Plan-Erfüllung) — schreibt selbst nur bei
  offensichtlicher Lücke/Fehler nach, ist aber nicht Haupt-Pfleger.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand (z. B. ein späterer Step würde zurückdrehen, was ein
früherer Schritt laut Map bewusst so gebaut hat): entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.
Das verhindert kein Kreisen zu 100 %, macht ein Hin-und-Her aber
wenigstens sichtbar und begründungspflichtig statt stillschweigend.

## Karte

- **`src/AiNetLinter/Output/MarkdownBuilder.cs`** — *angelegt (EPIC-01, step-001)*: zentraler Owner für `ColumnAlign` (Left/Right/Center), `MarkdownTableBuilder` (Tabelle mit Escaping via `EscapeCell` + beide `Table`-Überladungen) und `MarkdownBuilder` (fluent: `Heading`, `BlankLine`, `Line`, `BulletList`, `CodeBlock`, `Table(Action<>)`, `Table(MarkdownTableBuilder)`, `AppendTo`, `Build`) — emittiert bare `\n` (kein `AppendLine`), damit byte-stabile Migration bestehender bare-`\n`-Callsites möglich ist.
- **`src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`** — *umgebaut (EPIC-01, step-001, Prio 3)*: `BuildSummaryTable` nutzt jetzt `MarkdownTableBuilder` mit bedingtem `Struktur`-Spaltenpfad (`hasStructural`); `hasWarnings`-Blockquote bleibt raw-`sb`; `AppendViolationItem` Z.249–270 ist **unverändert** (eingerückter Code-Block bleibt raw `sb`-Code, Sonderlogik).
- **`src/AiNetLinter/Output/HotspotSectionFormatter.cs`** — *Prio 4 (EPIC-02), am Ende zu löschen*: 44-Zeilen-`static class`, die heute noch von `GetHotspotsScanner` und `HotspotMapBuilder` gemeinsam genutzt wird (Heading + Tabelle + Sortierung als atomare Einheit) — Konzept entscheidet, dass Sortierung in die Aufrufer wandert und der Helper ersatzlos wegfällt.
- **`src/AiNetLinter/Output/PathNormalizer.cs`** — konzept-relevant, aber nicht umzubauen: `ToRelative`/`IsTestFile`-Hilfsroutinen, die sowohl `ViolationMarkdownFormatter` als auch `RepoPlaybookGenerator` für Dateipfade benutzen — `Table(...)` darf hier nichts brechen.
- **`src/AiNetLinter/Output/RuleLegendRegistry.cs`** — Nachbar, kein Eingriff: rendert einzelne `Regellegende`-Zeilen, die neben den Tabellen im selben Report liegen — keine Migration nötig, weil kein Tabellen-Pattern.
- **`src/AiNetLinter/Output/DebtReportBuilder.cs`** — konzept-irrelevant: baut plain-text-Listen (kein Markdown-Tabellen-Pattern), bleibt unverändert.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`** — *umgebaut (EPIC-01, step-001, Prio 1)*: `AppendMemberRows` nutzt `MarkdownTableBuilder` mit bedingtem `File`-Spaltenpfad (`isMultiFile`); `FormatMemberRow` ist ersatzlos entfernt.
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`** — *umgebaut (EPIC-01, step-001, Prio 2)*: `AppendSection` emittiert Tabelle + Snippet-Block weiterhin in der Original-Reihenfolge Zeile → Snippet → Zeile → Snippet (byte-stabil); Cell-Escaping via `MarkdownTableBuilder.EscapeCell`, Snippet-Block via `MarkdownBuilder.CodeBlock`; `MarkdownTableBuilder` wird nur als Escaping-Utility genutzt (nicht als Append-Senke), weil der `mb.Table`-am-Ende-Pfad aus dem Konzept die Reihenfolge-Treue gebrochen hätte.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs`** — *Prio 4 (EPIC-02)*: `FormatReport` Z.96–124 ruft `HotspotSectionFormatter.AppendSection` zweimal (kritisch/warnend) — beide Aufrufe werden durch eine private `AppendHotspotSection` mit `MarkdownBuilder` ersetzt.
- **`src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupFormatter.cs`** — *Prio 9 (EPIC-02)*: komplette Datei wird auf `MarkdownBuilder` umgestellt; `Format`, `FormatMethodDetails`, `FormatTypeDetails`, `FormatPropertyDetails` ändern ihre Signatur von `StringBuilder sb` zu `MarkdownBuilder mb` — vier verschiedene Pattern-Arten in einer Methode (Heading + Key-Value-Bullets + Threshold-Tabelle + Detail-Sektionen).
- **`src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`** — *Prio 10 (EPIC-02)*: baut einzelne Markdown-Antwort mit Heading + optionaler `id:`-Zeile + einzeiligem `csharp`-Code-Block (Z.60–64) — Migration auf `MarkdownBuilder.Heading` + `Line` + `CodeBlock`.
- **`src/AiNetLinter/Maps/HotspotMapBuilder.cs`** — *Prio 4 (EPIC-02)*: CLI-Variante der Hotspot-Map; nutzt `HotspotSectionFormatter` identisch wie `GetHotspotsScanner` — bekommt eine **eigene** private `AppendHotspotSection`, weil Schicht-Trennung `Maps →` darf nicht `Mcp.Tools` referenzieren; Duplikation wird in Tech-Debt-Log aufgenommen.
- **`src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs`** — Sonderfall, **nicht** umzubauen: `AppendType` Z.71–82 schreibt Code-Block-Inhalt **zeilenweise** direkt in den `sb` (kein fertiger String) — `MarkdownBuilder.CodeBlock(string)` passt hier nicht.
- **`src/AiNetLinter/Generators/RepoPlaybookGenerator.cs`** — *Prio 5 (EPIC-02)*: `AppendAgentPriority` Z.313–335 mit Sonderfall „leere intentGroups" (eigene Row) + Header/Alignment-Spalten.
- **`src/AiNetLinter/Generators/AgentRulesGenerator.cs`** — *Prio 7 + 8 (EPIC-02)*: zwei Tabellen an unterschiedlichen Stellen — `AppendCompoundSuppressions` Z.175–196 (5-Spalten-Tabelle, war im ursprünglichen Konzept übersehen) und `AppendMetricsTable` Z.258–269 (3-Spalten-Tabelle mit Backtick-/Bold-Inline-Formatierung in Zellen, die `EscapeCell` bewusst nicht antastet).
- **`src/AiNetLinter/Commands/ListRulesCommand.cs`** — *Prio 6 (EPIC-02)*: `ListAll` Z.16–33, 5-Spalten-Header ohne Alignment, `Severity`/`Auto-Fix`-Spalte — einfachster Migrationsfall.
- **`src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs`** — *angelegt (EPIC-01, step-001)*: 24 Unit-Tests (Plan-Minimum 22, +2 Buffer) aus Konzept §4 (EscapeCell-Edge-Cases, Alignment, Header, `Build`, CodeBlock-Edge-Cases, beide `Table`-Überladungen, `DokumentMix`-Snapshot), `[Trait("Category", "Unit")]`.
- **`src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs`** — bestehend, **muss grün bleiben** (Byte-genaue Output-Stabilität für `ViolationMarkdownFormatter` nach Prio-3-Migration).
- **`src/AiNetLinter.IntegrationTests/McpServerCommand*Tests`** + **`McpLiveRepositoryTests`** — bestehend, **müssen grün bleiben** (Output-Bytes von `metrics_lookup` und `get_symbol_body` sind Token-Vertrag gegenüber Agenten; Prio 9 + 10 dürfen die Antworten nicht verändern).
- **`.agents/rules/AiNetLinterRichtlinien.mdc`** §1 (Dogfooding) und `Docs/integration.md` (Tool-vs-`rg`-Empfehlung) — Pflichtlektüre für die Step-Planer: C#-Symbol-Queries über MCP-Server `ainetlinter` (`find_symbol`, `find_references`, `get_impact`, `get_violations`, …) **vor** `rg`/`grep` — stattdessen `rg`/`grep` nur für reine String-/Kommentarsuche oder Nicht-C#-Dateien.
