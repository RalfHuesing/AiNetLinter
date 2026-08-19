---
status: open
type: step-plan
task: markdown-builder
step: 001
corrects: null
title: "MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-19
related_to: []
---

# Step 001: MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen

## Bezug

- **Task:** `markdown-builder`
- **Epic:** `EPIC-01` aus `roadmap.md` — *„MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen"* (bündelt Konzept-Schritte 2 + 3 + 4 + 5 + 6: Builder-Klasse anlegen, Testklasse anlegen, drei Callsites mit aktiven Escaping-Bugs umstellen).
- **Konzept-Referenz:** `tasks/markdown-builder/konzept.md` §2 (API), §3 Prio 1–3, §4 (Teststrategie, ≥22 Fälle), §8 Schritte 2–6 (Implementierungsreihenfolge).
- **Größen-Entscheidung (warum **ein** Step, nicht zwei):** Ralf-Vorgabe 2026-08-19 „sinnvoll große Code Steps, keine Mini oder Micro Steps". Der Builder ohne Callsites hat keinen Wert; die Bug-Fix-Callsites ohne Builder sind nicht committbar. Beide Teile zusammen sind ein in sich geschlossener, in einer Review-Runde prüfbarer Cluster (~480 Zeilen Diff, fünf Dateien, ein thematischer Strang). `step_type: batch` scheidet aus: Spec §10.6 deckt Batch auf max. 40 Diff-Lines und 8 trivialer Low-Risk-Items — beides weit unter dem hier anfallenden Volumen und der Kopplung.

## Aktueller Projektzustand (JIT-Kontext)

- **Greenfield für den Builder:** `src/AiNetLinter/Output/` enthält 13 Helper, **keinen** MarkdownBuilder. Namespace `AiNetLinter.Output` ist etabliert (DebtReportBuilder, HotspotSectionFormatter, PathNormalizer, RuleLegendRegistry, ViolationMarkdownFormatter, ViolationSummaryBuilder, …) — `MarkdownBuilder.cs` reiht sich nahtlos ein, ohne neuen Ordner.
- **Bestehende Tabellen-Patterns (gesehen, nicht geraten):** Roher `sb.AppendLine("| " + … + " |")`-Stil an zehn Stellen (Konzept §1). Drei davon — die aktiven Bug-Quellen — sind in `GetClassStructureTool.AppendMemberRows` (Z.322–339 + toter Helper `FormatMemberRow` Z.341–351), `GetViolationsScanner.AppendSection` (Z.235–266, Tabelle + zeilenweise Snippet-Block) und `ViolationMarkdownFormatter.BuildSummaryTable` (Z.55–105, bedingte `hasStructural`-Spalte).
- **Bestehende Tests, die als Output-Vertrag gelten:**
  - `src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs` — bestehend, **muss grün bleiben** (Prio 3 berührt `BuildSummaryTable`).
  - Konzept-DoD: alle bestehenden Fast- und Integration-Tests grün; Prio 1 + 2 sind keine MCP-Token-Verträge (kein externer Agent-Consumer), aber `ViolationMarkdownFormatter`-Output ist im CLI-Report Pflicht (Bytes müssen identisch bleiben).
- **Anti-Loop-Check (CodeMap konsultiert):** Keine früheren Entscheidungen in `codemap.md` widersprechen dem Vorhaben. CodeMap-Einträge für die drei Callsites sind mit Priorität EPIC-01 markiert — der Step setzt genau diese. CodeMap-Hinweis zum eingerückten Code-Block in `AppendViolationItem` Z.263–268 („bleibt absichtlich unberührt") wird im Step respektiert.
- **Bestehende Konvention (verifiziert in `AiNetLinterRichtlinien.mdc` + `AiNetLinter.mdc`):** `sealed` für konkrete Klassen, `#nullable enable` am Dateianfang, `MaxMethodLineCount: 60` (100 für Test-Methoden), `MaxMethodParameterCount: 4`, xUnit v3 + `[Trait("Category", "Unit")]`, keine Task-/Step-Bezüge in Code-Kommentaren, keine `dynamic`/`out`-außer-Try, sparsame Kommentare.
- **Reale Inkonsistenz im Bestand (gefunden beim Lesen, nicht im Konzept dokumentiert):** `ViolationMarkdownFormatter.BuildSummaryTable` Z.62/65/70 verwendet `sb.Append('\n')` bzw. `sb.Append("| … |\n")` mit **bare LF** statt `sb.AppendLine` (CRLF auf Windows). `GetClassStructureTool` und `GetViolationsScanner` hingegen nutzen `AppendLine` (CRLF). Das hat Auswirkungen auf die byte-genau Output-Stabilität — siehe *Bekannte Ausnahmen* und `## Notes` weiter unten.

## Intention

Nach diesem Step existiert der zentrale `MarkdownBuilder` (mit `MarkdownTableBuilder` + `ColumnAlign` Enum) im `Output`-Namespace, ist durch ≥22 Unit-Tests vertraglich abgesichert, und die drei Callsites mit aktiven Escaping-Bugs (`GetClassStructureTool`, `GetViolationsScanner`, `ViolationMarkdownFormatter.BuildSummaryTable`) sind auf den Builder umgestellt — der Bug „unescaped `|` in `v.Signature`/`v.Details` zerschießt Tabelle" ist damit behoben, `FormatMemberRow` ist als toter Code gelöscht. Alle bestehenden Tests bleiben grün; die Markdown-Output-Bytes der drei Callsites bleiben identisch zum vorherigen Stand (Token-/Output-Vertrag gegenüber Agenten und CLI-Consumern).

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Output/MarkdownBuilder.cs` (neu)

- **Was:** Neue Datei, `#nullable enable` am Anfang. Drei Typen in dieser Reihenfolge:
  - `internal enum ColumnAlign { Left, Right, Center }`.
  - `internal sealed class MarkdownTableBuilder` mit `AddColumn(header, align = Left)`, `AddRow(params object?[] cells)` (fehlende Cells → `EscapeCell` → `"-"`; zu viele Cells werden ignoriert), `AppendTo(StringBuilder)`, `Build()` (ruft `AppendTo` intern auf), `internal static string EscapeCell(string?)` (Pipe → `\|`, `\r`/`\n` → ` `, `Trim`, leer/whitespace → `"-"`).
  - `internal sealed class MarkdownBuilder` fluent: `Heading(int level, string)`, `BlankLine()`, `Line(string)`, `BulletList(IEnumerable<string>)`, `CodeBlock(string language, string content)` (kein doppeltes `\n` wenn `content` schon mit `\n` endet), **`beide** `Table`-Überladungen: `Table(Action<MarkdownTableBuilder> configure)` **und** `Table(MarkdownTableBuilder instance)` (Instanz-Übergabe ist Pflicht, weil `GetClassStructureTool`, `GetViolationsScanner`, `ViolationMarkdownFormatter` Rows erst nach `AddColumn`-Aufrufen anhängen), `AppendTo(StringBuilder)`, `Build()`.
- **Warum:** Ein einziger Owner für Escaping + Tabellenformat verhindert die zehn unabhängigen Tabellen-Pfade, die das aktuelle Anti-Pattern erzeugt hat. Beide `Table`-Überladungen werden schon in EPIC-01 gebraucht (Prio 1 nutzt Instanz-Übergabe, weil die `isMultiFile`-bedingte Spalte zwischen Header und Rows hängt).
- **Bauen aus:** Konzept §2a + §2b, verbatim — keine eigene API-Erfindung.

### Datei 2: `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs` (neu)

- **Was:** Neue Testklasse, `#nullable enable`, **kein** `sealed` (Test-Klassen-Konvention), `[Trait("Category", "Unit")]` an der Klasse. Methoden ≤100 Zeilen (`MaxMethodLineCount`-Override für Tests). Mindestens die 22 Fälle aus Konzept §4:
  - `MarkdownTableBuilder` (10): `EscapeCell` mit `|`, `\r\n`, leer/whitespace, Generics (`<>` bleibt), Bold/Backticks (bleiben), Alignment-Row (Left/Right/Center), zu wenig Cells, leere Tabelle (kein `AddColumn`), `Build()` Smoke, vollständiger Snapshot (Header + Separator + Rows).
  - `MarkdownBuilder` (12): `Heading(1)` + `Heading(3)`, `CodeBlock` mit/ohne trailing newline, `CodeBlock` mit Truncation-Marker, `BlankLine`, `BulletList`, `Table(Action<>)` Callback, `Table(MarkdownTableBuilder)` Überladung, `AppendTo(StringBuilder)`, `Build()`, Dokument-Mix-Snapshot (`Heading(2) + BulletList + Table + Line("**bold**")`).
- **Warum:** Vertragspinning, bevor Callsites migriert werden — wer den Builder umbaut, bricht die Tests, nicht die Output-Bytes. Die Konzept-Vorgabe ist explizit „Tests sichern Verhalten ab, bevor viele Callsites umgebaut werden".

### Datei 3: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` (Prio 1 — Bug-Fix)

- **Was:**
  - Z.322–339 `AppendMemberRows` umbauen: `MarkdownTableBuilder` mit bedingtem `AddColumn("File")` zwischen `Visibility` und `Lines` (genau wie die aktuelle `isMultiFile`-Verzweigung), dann `AddColumn("Lines", Right) + "LineCount" + "Signature"`. Rows über `AddRow(...)`. Aufruf am Ende: `table.AppendTo(sb)`.
  - Z.341–351 `FormatMemberRow` **ersatzlos löschen** (Konzept §5, explizit als toter Code nach Migration markiert).
  - Sicherstellen: Reihenfolge der Spalten, Alignment (`:---|`, `---:`, `:---:`) und Inhalt (`m.StartLine > 0 ? "{Start}-{End}" : "-"`, `m.LineCount > 0 ? m.LineCount.ToString() : "-"`, `!string.IsNullOrEmpty(m.FilePath) ? Path.GetFileName(m.FilePath) : "-"`) bleiben **byte-genau identisch** zur aktuellen Ausgabe.
  - Umgebende `RenderMarkdown` (Z.293–320) bleibt **unverändert** — `AppendMemberRows` bekommt weiterhin den `StringBuilder sb` der Methode, `TrimEnd()` am Ende bleibt.
- **Warum:** Aktiver Bug — `v.Signature` mit `|` (z. B. `where T : IFoo<X, Y>`) zerschießt die Tabelle, weil die Spalte unescaped eingefügt wird. `FormatMemberRow` ist der einzige Ort, der diesen String bastelt, und wird nach dem Builder-Umbau tot.

### Datei 4: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` (Prio 2 — Bug-Fix)

- **Was:**
  - Z.235–266 `AppendSection` umbauen. Der `foreach`-Block (Z.250–264) muss die **Reihenfolge Zeile → Snippet → Zeile → Snippet** beibehalten (Konzept §3 Prio 2, explizit als „inner-foreach" entschieden — Snippet-Block bleibt im selben Loop wie die Tabellen-Row).
  - Konkrete Form: pro Violation `table.AddRow(relativePath, v.LineNumber, v.RuleName, v.Details)` (Escaping passiert automatisch in `AddRow` → `EscapeCell`), gefolgt von — wenn `v.Snippet` nicht leer — `mb.CodeBlock("csharp", v.Snippet!)` + `mb.BlankLine()`. `mb` ist ein **eigener** `MarkdownBuilder` (parallel zum `MarkdownTableBuilder`), der am Ende einmal via `mb.AppendTo(sb)` (nach `mb.Table(table)`) in den `sb` der Methode gemerged wird.
  - Vor der Tabelle: `mb.Heading(2, heading).BlankLine()` ersetzt die zwei `sb.AppendLine($"## {heading}")` + `sb.AppendLine()` (Z.238–239). Heading wird via `Line` mit zwei `##` emittiert, gefolgt von Leerzeile.
  - „Keine."-Fall (Z.241–246): unverändert `sb.AppendLine("Keine.")` + `sb.AppendLine()`.
  - Am Ende der Methode: `sb.AppendLine()` für die Leerzeile nach der Tabelle bleibt (Z.265) — semantisch identisch zur aktuellen trailing blank line.
- **Warum:** Aktiver Bug — `v.Details` (kann freien Markdown-Text mit Pipes enthalten, z. B. generierte Diagnostik-Beschreibungen) landet unescaped in der Tabelle, defekte Spaltenstruktur. Migration **muss** die inner-foreach-Reihenfolge bewahren, sonst ändert sich der LLM-Output strukturell.

### Datei 5: `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs` (Prio 3 — Bug-Fix)

- **Was:**
  - Z.55–105 `BuildSummaryTable` umbauen. `var hasStructural = …` (Z.60) bleibt. Dann:
    - `var table = new MarkdownTableBuilder().AddColumn("Regel").AddColumn("Gesamt", Right).AddColumn("Prod", Right).AddColumn("Tests", Right);`
    - `if (hasStructural) table.AddColumn("Struktur", Center);`
    - `foreach (var r in byRule)` mit der unveränderten `prodCount`/`testCount`/`structMarker`-Berechnung: `if (hasStructural) table.AddRow(r.RuleName, r.Count, prodCount, testCount, structMarker); else table.AddRow(r.RuleName, r.Count, prodCount, testCount);`
    - `return mb.Build();` mit `var mb = new MarkdownBuilder(); mb.BlankLine().Table(table);` (führendes `\n` wie bisher Z.62).
  - Der `hasWarnings`-Block (Z.96–102, das `> ℹ …`-Zitat) bleibt **unverändert** als `sb.Append("\n> ℹ …")` — das ist *kein* Tabellen-Pattern, das ist ein Blockquote.
  - **Wichtig:** Da `BuildSummaryTable` aktuell `string` zurückgibt (Z.104), brauchen wir auch in der neuen Form einen `string`-Return. Der `mb` muss also VOR dem `hasWarnings`-Block geschlossen und in einen `sb` gemerged werden, an den dann der `hasWarnings`-Text angehängt wird. Konkret:
    ```csharp
    var sb = new StringBuilder();
    sb.Append('\n');
    var mb = new MarkdownBuilder();
    mb.Table(table);
    mb.AppendTo(sb);
    if (hasWarnings) { sb.Append("\n> ℹ `[warn]`-Violations …\n\n"); }
    return sb.ToString();
    ```
  - `AppendViolationItem` (Z.249–270) bleibt **unverändert** — der eingerückte Code-Block ist Sonderlogik (Konzept §3 Code-Block-Tabelle + §7 explizit „nicht umbauen").
- **Warum:** Bedingte `hasStructural`-Spalte ist exakt der Anwendungsfall, für den die `MarkdownTableBuilder`-API designt wurde (bedingt Spalten anhängen, einheitliche Row-Schleife). Bonus-Bug: Cell-Content wird ab jetzt einheitlich durch `EscapeCell` geschickt — wenn je eine RuleDescription `|` enthält, wird's nicht mehr die Tabelle zerreißen.
- **Vorsicht:** Die bestehende Methode nutzt an mehreren Stellen `sb.Append("| … |\n")` mit bare-`\n` (Z.65, 70, 88, 92). Der Builder nutzt `AppendLine` → CRLF auf Windows. **Vor dem Einchecken prüfen**, ob `ViolationMarkdownFormatterTests` die Ausgabe byte-genau assertet (siehe *Bekannte Ausnahmen*); falls ja, muss die Builder-Ausgabe auf `\n` normalisiert werden (kleinste Änderung am Builder, klar begründet).

## Tests

- [ ] `MarkdownBuilderTests.EscapeCell_Pipe_WirdEscaped`
- [ ] `MarkdownBuilderTests.EscapeCell_Zeilenumbruch_WirdZuSpace`
- [ ] `MarkdownBuilderTests.EscapeCell_LeerOderWhitespace_WirdMinus`
- [ ] `MarkdownBuilderTests.EscapeCell_Generics_KeineAenderung`
- [ ] `MarkdownBuilderTests.EscapeCell_BoldUndBackticks_KeineAenderung`
- [ ] `MarkdownBuilderTests.AlignmentRow_LeftRightCenter_KorrekteSeparatoren`
- [ ] `MarkdownBuilderTests.AddRow_ZuWenigCells_FehlendeWerdenMinus`
- [ ] `MarkdownBuilderTests.AppendTo_OhneColumns_SchreibtNichts`
- [ ] `MarkdownBuilderTests.Build_GibtVolleTabelleAlsString`
- [ ] `MarkdownBuilderTests.VollstaendigeTabelle_SnapshotDesOutputs`
- [ ] `MarkdownBuilderTests.Heading1Und3_KorrektePräfixe`
- [ ] `MarkdownBuilderTests.CodeBlock_MitUndOhneTrailingNewline`
- [ ] `MarkdownBuilderTests.CodeBlock_MitTruncationMarker_ bleibtSichtbar`
- [ ] `MarkdownBuilderTests.BlankLine_ErzeugtLeereZeile`
- [ ] `MarkdownBuilderTests.BulletList_PraefixMinusProElement`
- [ ] `MarkdownBuilderTests.TableCallback_und_InstanceUeberladung_GleicherOutput`
- [ ] `MarkdownBuilderTests.AppendTo_LandetInAeusseremStringBuilder`
- [ ] `MarkdownBuilderTests.Build_GibtGesamtausgabeAlsString`
- [ ] `MarkdownBuilderTests.DokumentMix_HeadingBulletsTableLine_Snapshot`
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` grün
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün (insbesondere `ViolationMarkdownFormatterTests` byte-stabil)
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün
- [ ] `dotnet build` fehler- und warnungsfrei (`TreatWarningsAsErrors = true`)

## Definition of Done

- [ ] Alle fünf Dateien in „Konkrete Änderungen" umgesetzt (2 neu, 3 modifiziert, 1 Helper gelöscht)
- [ ] `dotnet build` grün, ohne neue Warnings
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün
- [ ] `FormatMemberRow` ist in `GetClassStructureTool.cs` **ersatzlos** entfernt (nicht auskommentiert, nicht `[Obsolete]` markiert — tot nach Builder-Einführung)
- [ ] `AppendViolationItem` in `ViolationMarkdownFormatter.cs` ist **unverändert** (eingerückter Code-Block bleibt raw `sb`-Code, wie in CodeMap vermerkt)
- [ ] Markdown-Output-Bytes der drei migrierten Callsites sind **byte-genau identisch** zum vorherigen Stand (gegen die bestehenden Tests verifiziert — insbesondere `ViolationMarkdownFormatterTests`)
- [ ] `MarkdownBuilder`, `MarkdownTableBuilder` tragen `sealed`; `MarkdownBuilder.cs` beginnt mit `#nullable enable`; keine `// step-001`/`// EPIC-01`/Task-IDs in Code-Kommentaren (Richtlinien §5 „Sparsamer Einsatz von Code-Kommentaren")
- [ ] Testklasse trägt `[Trait("Category", "Unit")]` an der Klasse, Testmethoden ≤100 Zeilen, ohne `sealed`
- [ ] `tasks/markdown-builder/codemap.md` ist aktualisiert (MarkdownBuilder + Tests als „angelegt" statt „geplant" markiert; Prio-1–3-Callsites als „umgebaut" markiert)
- [ ] Commit auf aktuellem Branch, Conventional Commit auf Deutsch, imperativ, Suffix `[markdown-builder]`, Body mit `Refs: tasks/markdown-builder/step-001`-Trailer
- [ ] `step-001/step-result.md` geschrieben mit: tatsächlich committeter Hash, Beobachtungen aus der Umsetzung, Verweis auf die ≥22 Test-Fälle (Punkt-Liste reicht)
- [ ] `status` in dieser `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — harte Code-Qualitätsmetriken (insbesondere `MaxLineCount: 500` für `MarkdownBuilder.cs` ≤200 Zeilen ist sicher; `MaxMethodLineCount: 60/100-Override` für Tests; `MaxMethodParameterCount: 4` ist im Builder nicht angerissen — `AddRow(params object?[])` ist `params`, fällt nicht unter die Regel).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Dokumentations-Objektivität — keine Superlative, nur Implementiertes dokumentieren), §3 (Windows/PowerShell-Workflow, `dotnet test` TRX-Logging bei Failures), §5 (Sparsame Code-Kommentare, **kein** Verweis auf `step-001`/`EPIC-01` im Code, Zero-Warning-Direktive).

## Bekannte Ausnahmen

- **`ViolationMarkdownFormatter.BuildSummaryTable` LF-vs-CRLF-Drift** (siehe `## Notes`): Falls `ViolationMarkdownFormatterTests` den Output **byte-genau** assertet, ist nach dem Builder-Umbau ein Line-Ending-Unterschied möglich (vorher bare `\n`, nachher `AppendLine` → CRLF auf Windows). Coder MUSS vor dem finalen Commit mit `dotnet test src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs` validieren. Falls Tests failen: minimaler Eingriff — `MarkdownTableBuilder.AppendTo` auf bare `\n` umstellen ODER den `hasWarnings`-Block ebenfalls auf `AppendLine` migrieren, mit Begründung im Commit-Body. Ergebnis MUSS byte-stabil sein; das ist Token-Vertrag.
- **Eingerückter Code-Block in `ViolationMarkdownFormatter.AppendViolationItem` Z.263–268:** bleibt absichtlich raw `sb`-Code (Sonderlogik mit 2-Leerzeichen-Präfix, kein Tabellen-Pattern). Nicht migrieren.
- **Snippet-Block in `GetViolationsScanner.AppendSection` Z.256–263:** wird im selben Step via `MarkdownBuilder.CodeBlock` ersetzt — das ist Prio 2, nicht der Sonderfall aus Konzept §3 Tabelle „Code-Block-Stellen, die NICHT umgebaut werden" (der bezieht sich nur auf `SkeletonMarkdownRenderer` und `AppendViolationItem`).

## Code-Skizze (optional)

Siehe Konzept §2a + §2b + §3 Prio 1–3 — die Skizzen dort sind die verbindliche Vorlage. Abweichungen sind im `step-result.md` zu begründen.

## Notes

- **`ColumnAlign`-Default `Left`:** Die Konzept-Skizze verwendet `Left` als Default für `AddColumn` — die drei Callsites in EPIC-01 rufen `AddColumn(...)` für Text-Spalten ohne explizites `Left` auf, das passt. `Right` für Zahlen (`Lines`, `LineCount`, `Gesamt`, `Prod`, `Tests`), `Center` für den `Struktur`-Marker in `BuildSummaryTable` — exakt wie im Konzept.
- **API-Disziplin:** Beide `Table`-Überladungen müssen in EPIC-01 entstehen, weil `GetClassStructureTool.AppendMemberRows` die Rows erst nach `AddColumn(...)` (inkl. der bedingten `File`-Spalte) hinzufügt — die Instanz-Übergabe ist hier Pflicht, nicht nur „vielleicht später gebraucht". Wer eine der beiden Überladungen weglässt, bricht entweder Prio 1 oder Prio 3.
- **Anti-Loop-Hinweis für Folge-Steps:** `CodeMap` enthält für Prio 4 (`HotspotSectionFormatter` löschen) bereits den Hinweis „Duplikation wird akzeptiert und in Tech-Debt-Log aufgenommen" — EPIC-02 macht das, nicht EPIC-01.
- **Commit-Disziplin:** Ein Commit für den gesamten Step (Ralf-Vorgabe „sinnvoll große Code Steps"). Innerhalb des Commits kann der Coder logisch trennen: zuerst „Builder + Tests anlegen, alles grün" (atomar committbar), dann „Callsites umstellen" (ebenfalls atomar). Der Coder entscheidet das — der Plan lässt beide Wege offen, solange am Ende **ein** Commit mit allen Änderungen existiert.
- **MCP-Dogfooding-Hinweis (Richtlinien §1):** Der Coder kann `ainetlinter.find_symbol` für „MarkdownBuilder" / „MarkdownTableBuilder" / „FormatMemberRow" / „ViolationMarkdownFormatter" nutzen, um Aufrufstellen sicher zu identifizieren — `rg`/`grep` ist hier nur für die Verifikation der gelöschten `FormatMemberRow`-Methode nötig.
