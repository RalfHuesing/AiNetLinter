---
task: markdown-builder
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-19
last_updated_by: coder (step-003)
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

- **`src/AiNetLinter/Output/MarkdownBuilder.cs`** — *API erweitert (EPIC-01, step-002, Refactor)*: zentraler Owner für `ColumnAlign` (Left/Right/Center), `MarkdownTableBuilder` (Tabelle mit Escaping via `EscapeCell` + zeilenweise Public-API `BuildHeaderLine`/`BuildSeparatorLine`/`BuildRowLine(params)` + privater `FormatRow`-Helper + `AppendTo` + `Build` + beide `Table`-Überladungen via `MarkdownBuilder`) und `MarkdownBuilder` (fluent: `Heading`, `BlankLine`, `Line`, `BulletList`, `CodeBlock`, `Table(Action<>)`, `Table(MarkdownTableBuilder)`, `AppendTo`, `Build`) — emittiert bare `\n` (kein `AppendLine`), damit byte-stabile Migration bestehender bare-`\n`-Callsites möglich ist; `AppendTo` nutzt die zeilenweisen Methoden + `FormatRow` als Single-Source-of-Truth.
- **`src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`** — *umgebaut (EPIC-01, step-001, Prio 3)*: `BuildSummaryTable` nutzt jetzt `MarkdownTableBuilder` mit bedingtem `Struktur`-Spaltenpfad (`hasStructural`); `hasWarnings`-Blockquote bleibt raw-`sb`; `AppendViolationItem` Z.249–270 ist **unverändert** (eingerückter Code-Block bleibt raw `sb`-Code, Sonderlogik).
- **`src/AiNetLinter/Output/PathNormalizer.cs`** — konzept-relevant, aber nicht umzubauen: `ToRelative`/`IsTestFile`-Hilfsroutinen, die sowohl `ViolationMarkdownFormatter` als auch `RepoPlaybookGenerator` für Dateipfade benutzen — `Table(...)` darf hier nichts brechen.
- **`src/AiNetLinter/Output/RuleLegendRegistry.cs`** — Nachbar, kein Eingriff: rendert einzelne `Regellegende`-Zeilen, die neben den Tabellen im selben Report liegen — keine Migration nötig, weil kein Tabellen-Pattern.
- **`src/AiNetLinter/Output/DebtReportBuilder.cs`** — konzept-irrelevant: baut plain-text-Listen (kein Markdown-Tabellen-Pattern), bleibt unverändert.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`** — *umgebaut (EPIC-01, step-001, Prio 1)*: `AppendMemberRows` nutzt `MarkdownTableBuilder` mit bedingtem `File`-Spaltenpfad (`isMultiFile`); `FormatMemberRow` ist ersatzlos entfernt.
- **`src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`** — *umgebaut (EPIC-01, step-002, Prio 2 abgeschlossen)*: `AppendSection` nutzt jetzt `MarkdownTableBuilder` mit `.AddColumn(...)` + `table.BuildHeaderLine()` / `table.BuildSeparatorLine()` / `table.BuildRowLine(...)` als Single-Source-of-Truth für Tabellen-Format (kein Inline-`string.Join` + `align switch` mehr) — Alignment-Format-Strings (`:---|`, `---:|`, `:---:|`) sind damit nur noch in `MarkdownTableBuilder` definiert. Reihenfolge-Treue Zeile → Snippet → Zeile → Snippet bleibt erhalten (byte-stabil); Cell-Escaping via `MarkdownTableBuilder.EscapeCell` (intern in `BuildRowLine`); Snippet-Block via `MarkdownBuilder.CodeBlock`.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs`** — *umgebaut (EPIC-02, step-003, Prio 4)*: `FormatReport` Z.96–124 ruft jetzt lokale `AppendHotspotSection` (Heading + Tabelle + Sortierung) statt `HotspotSectionFormatter`; neue private `AppendHotspotSection(StringBuilder, string, IReadOnlyList<HotspotFileInfo>, int)` nutzt `MarkdownBuilder.Heading(2)` + `BlankLine` + `Table(t => ...)`-Callback mit Spalten Datei/Zeilen/Auslastung/Verbleibend; `// ainetlinter-disable DuplicateCode`-Marker wegen Schicht-Trennung-Duplikation mit `HotspotMapBuilder.AppendHotspotSection`.
- **`src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupFormatter.cs`** — *Prio 9 (EPIC-02)*: komplette Datei wird auf `MarkdownBuilder` umgestellt; `Format`, `FormatMethodDetails`, `FormatTypeDetails`, `FormatPropertyDetails` ändern ihre Signatur von `StringBuilder sb` zu `MarkdownBuilder mb` — vier verschiedene Pattern-Arten in einer Methode (Heading + Key-Value-Bullets + Threshold-Tabelle + Detail-Sektionen).
- **`src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`** — *umgebaut (EPIC-02, step-003, Prio 10)*: `ExecuteAsync` Z.60–69 nutzt jetzt `MarkdownBuilder.Heading(3)` + `BlankLine` + optional `Line("id: ...")` + `BlankLine` + `CodeBlock("csharp", body)` statt `string.Concat`; `mb.Build().TrimEnd()` entfernt das zusaetzliche Trailing-`\n` aus `CodeBlock` fuer den byte-stabilen MCP-Token-Vertrag gegenueber Agent-Consumern.
- **`src/AiNetLinter/Maps/HotspotMapBuilder.cs`** — *umgebaut (EPIC-02, step-003, Prio 4)*: CLI-Variante der Hotspot-Map; `Build` ruft lokale `AppendHotspotSection` (Heading mit Emojis `🔴 ...`/`⚠ ...`) statt `HotspotSectionFormatter`; neue private `AppendHotspotSection(StringBuilder, string, IReadOnlyList<StructureFileInfo>, int)` (27 Z., identische Builder-Logik wie in `GetHotspotsScanner` aber Eingabe-Typ `StructureFileInfo`). Schicht-Trennung `Maps →` darf nicht `Mcp.Tools` referenzieren — Duplikation der Methode ist Konzept-akzeptiert (siehe `GetHotspotsScanner`-Marker).
- **`src/AiNetLinter/Maps/Skeleton/SkeletonMarkdownRenderer.cs`** — Sonderfall, **nicht** umzubauen: `AppendType` Z.71–82 schreibt Code-Block-Inhalt **zeilenweise** direkt in den `sb` (kein fertiger String) — `MarkdownBuilder.CodeBlock(string)` passt hier nicht.
- **`src/AiNetLinter/Generators/RepoPlaybookGenerator.cs`** — *umgebaut (EPIC-02, step-004, Prio 5)*: `AppendAgentPriority` nutzt jetzt `MarkdownTableBuilder` mit 3 Spalten (Intent / Offene Verstöße wave-ready mit `ColumnAlign.Right` / Regeln); Sonderfall `intentGroups.Count == 0` emittiert literale Row `| - | 0 | Keine offenen Verstöße |` via `table.AddRow("-", 0, "Keine offenen Verstöße")`; `mb.Table(table); mb.AppendTo(sb)`-Pattern produktiv (analog `ViolationMarkdownFormatter.BuildSummaryTable`).
- **`src/AiNetLinter/Generators/AgentRulesGenerator.cs`** — *umgebaut (EPIC-02, step-004, Prio 7 + 8)*: `AppendMetricsTable` (3 Spalten Regel/Limit mit `ColumnAlign.Center`/Praxis) und `AppendCompoundSuppressions` (5 Spalten Regel/Bedingung/Effektives Limit/Severity/Grund) nutzen beide `MarkdownTableBuilder` + `mb.Table(table); mb.AppendTo(sb)`-Pattern; `using AiNetLinter.Output;` neu (Z.11) für `MarkdownTableBuilder`/`ColumnAlign`/`MarkdownBuilder`; Backtick-/Bold-Inline-Formatierung in Cell-Values bleibt erhalten (`EscapeCell` transformiert nur `|`/CR/LF/Whitespace-Only).
- **`src/AiNetLinter/Commands/ListRulesCommand.cs`** — *umgebaut (EPIC-02, step-003, Prio 6)*: `ListAll` nutzt jetzt `MarkdownTableBuilder` mit 5 Spalten `RuleId`/`Bezeichnung`/`Intent`/`Severity`/`Auto-Fix` (kein Alignment, alle Spalten linksbuendig per Default); Foreach-Schleife schreibt in `table.AddRow(...)`; `autoFix` als fertiger String `"ja (--fix)"`/`"-"`; `table.AppendTo(sb)` ersetzt den raw `sb.AppendLine`-Block.
- **`src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs`** — *erweitert (EPIC-01, step-002, +6 Unit-Tests)*: 30 Unit-Tests gesamt (24 aus step-001 + 6 neu in step-002 für `BuildHeaderLine`/`BuildSeparatorLine`/`BuildRowLine`-API: Standardfall, Header-Pipe-Escape, Alignment-Format, Cell-Padding, Null-Cells), `[Trait("Category", "Unit")]`.
- **`src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs`** — bestehend, **muss grün bleiben** (Byte-genaue Output-Stabilität für `ViolationMarkdownFormatter` nach Prio-3-Migration).
- **`src/AiNetLinter.IntegrationTests/McpServerCommand*Tests`** + **`McpLiveRepositoryTests`** — bestehend, **müssen grün bleiben** (Output-Bytes von `metrics_lookup` und `get_symbol_body` sind Token-Vertrag gegenüber Agenten; Prio 9 + 10 dürfen die Antworten nicht verändern).
- **`.agents/rules/AiNetLinterRichtlinien.mdc`** §1 (Dogfooding) und `Docs/integration.md` (Tool-vs-`rg`-Empfehlung) — Pflichtlektüre für die Step-Planer: C#-Symbol-Queries über MCP-Server `ainetlinter` (`find_symbol`, `find_references`, `get_impact`, `get_violations`, …) **vor** `rg`/`grep` — stattdessen `rg`/`grep` nur für reine String-/Kommentarsuche oder Nicht-C#-Dateien.
