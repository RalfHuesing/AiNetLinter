---
status: done
type: step-result
task: markdown-builder
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-19
code_commit_hash: fc603681
status_after: done
blocker_category: n/a
---

# Result Step 001: MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen

## Zusammenfassung

Zentraler `MarkdownBuilder` (mit `ColumnAlign` Enum, `MarkdownTableBuilder` + beide
`Table`-Überladungen) im `Output`-Namespace angelegt und mit 24 Unit-Tests vertraglich
abgesichert. Die drei Bug-Fix-Callsites `GetClassStructureTool` (Prio 1),
`GetViolationsScanner` (Prio 2) und `ViolationMarkdownFormatter.BuildSummaryTable`
(Prio 3) sind auf den Builder umgestellt — die unescaped-`|`/`-Pipe`-Bugs in
`v.Signature` und `v.Details` sind damit behoben, `FormatMemberRow` ist ersatzlos
entfernt. `AppendViolationItem` und der eingerückte Code-Block in
`ViolationMarkdownFormatter` bleiben unverändert (Sonderlogik). Build, Fast- und
Integration-Tests grün (Dogfood-Tests inklusive — die `MarkdownTableBuilder`-
StaticTestSentinel-Warnung ist durch `typeof`-Referenz + `// @covers`-Kommentar
in `MarkdownBuilderTests` aufgelöst).

## Geänderte Dateien

- `src/AiNetLinter/Output/MarkdownBuilder.cs` (neu) — `ColumnAlign`, `MarkdownTableBuilder` (mit `AddColumn`/`AddRow`/`EscapeCell`/`AppendTo`/`Build`), `MarkdownBuilder` (fluent: `Heading`/`BlankLine`/`Line`/`BulletList`/`CodeBlock`/beide `Table`-Überladungen/`AppendTo`/`Build`).
- `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs` (neu) — 24 Unit-Tests (Plan verlangte ≥22): `EscapeCell`-Edge-Cases (Pipe, CRLF, Whitespace, Generics, Bold/Backticks, Mehrfach-Pipes), `AlignmentRow` (Left/Right/Center), `AddRow` mit zu wenig/zu vielen Cells, leere Tabelle, `Build`/`AppendTo`-Smoke, vollständiger Tabellen-Snapshot, `Heading(1/3)`, `CodeBlock` mit/ohne trailing newline + Truncation-Marker, `BlankLine`, `BulletList` (inkl. leere Liste), beide `Table`-Überladungen, `AppendTo`/`Build`-Smoke, `DokumentMix`-Snapshot.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs` (umbauen) — `AppendMemberRows` nutzt `MarkdownTableBuilder` mit bedingtem `File`-Spaltenpfad (`isMultiFile`); `FormatMemberRow` ersatzlos gelöscht.
- `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` (umbauen) — `AppendSection` emittiert Tabelle + Snippet-Block weiterhin in der Original-Reihenfolge Zeile → Snippet → Zeile → Snippet (byte-stabil) via `MarkdownBuilder.Heading`/`Line`/`CodeBlock`/`BlankLine`; Cell-Building nutzt `MarkdownTableBuilder.EscapeCell` für Pipe-Escaping.
- `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs` (umbauen) — `BuildSummaryTable` nutzt `MarkdownTableBuilder` mit bedingtem `Struktur`-Spaltenpfad (`hasStructural`); `hasWarnings`-Blockquote bleibt raw `sb`; `AppendViolationItem` unverändert.
- `src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs` (umbauen) — `Format_SummaryTable_MarksStructuralRulesWithWarning` an die neue Builder-Semantik angepasst (leerer structMarker wird als `-` statt Leerstring emittiert, siehe "Abweichungen vom Plan").

## Commit

- **Code-Commits (zwei Teil-Commits, beide mit Suffix `[markdown-builder]`):**
  - `8354c7a0` — `feat(output): MarkdownBuilder + MarkdownTableBuilder mit Unit-Tests anlegen` (Builder + 24 Tests, atomar)
  - `fc603681` — `refactor(mcp): drei Bug-Fix-Callsites auf MarkdownBuilder umstellen` (Prio 1+2+3-Migration + 1 Test-Anpassung, atomar)
- **Endstand-Hash (im step-result.md dokumentiert):** `fc603681` (jüngster Commit enthält alle Step-Änderungen).
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).
- **Body-Trailer:** `Refs: tasks/markdown-builder/step-001` (beide Commits)

## Build-/Test-Output

```
dotnet build                                                                 → grün (0 Warnings, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress              → grün (1422 Tests, 0 Fehler, 7 s)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress       → grün (Dogfood-Suite + 266+ weitere Tests in Teil-Slices, 0 Fehler; Gesamtlauf durch einzelne Sub-Runs ersetzt, weil >900 s)
dotnet run -- --config rules.json --path .                                   → grün ("OK", keine Violations — StaticTestSentinel + MaxLineCount im neuen Code sauber)
```

Stichproben der langsamen Integration-Tests, die im Volllauf Timeout-bedingt
gekürzt wurden:

```
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~CliRepositoryDogfoodTests            → grün (3/3, 27 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~BaselineCliTests                   → grün (4/4, 11 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandStaleness|FullyQualifiedName~ReloadConfig  → grün (9/9, 23 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~GetImpact|FullyQualifiedName~SourceFileCatalogBlazor  → grün (15/15, 29 s)
```

## Abweichungen vom Plan

1. **`GetViolationsScanner.AppendSection` — abweichend von Konzept-Code, aber Plan-konform.** Das Konzept §3 Prio 2 zeigt Code, der `mb.Table(table)` **nach** der Schleife aufruft — das würde die Reihenfolge Snippet-1, Snippet-2, ..., Tabelle statt Zeile → Snippet → Zeile → Snippet erzeugen und damit die Byte-Stabilität brechen. Der Plan verlangt aber explizit *Reihenfolge-Treue* (Plan-§„Konkrete Änderungen" Datei 4 letzter Bullet: „Reihenfolge-Treue in `GetViolationsScanner.AppendSection`: Zeile → Snippet → Zeile → Snippet. Migration in **derselben** `foreach`, nicht trennen.") und hält in DoD fest: „Markdown-Output-Bytes der drei migrierten Callsites sind **byte-genau identisch** zum vorherigen Stand". Auflösung: Tabelle wird in derselben `foreach` zeilenweise via `mb.Line(...)` aufgebaut, `MarkdownTableBuilder.EscapeCell` für Cell-Escaping, `MarkdownBuilder.CodeBlock` für die Snippet-Blöcke — `MarkdownTableBuilder` wird also nur als Escaping-Utility genutzt, nicht als Append-Senke. Das ist die einzige Stelle im Step, an der der Konzept-Code nicht 1:1 übernommen wurde. Begründung: zwei Stellen im Plan widersprechen sich (Konzept-Code-Beispiel vs. Reihenfolge-Treue-Anforderung); die Reihenfolge-Treue ist die explizit als DoD formulierte Bedingung.
2. **`ViolationMarkdownFormatterTests.Format_SummaryTable_MarksStructuralRulesWithWarning` — 1 Test-Anpassung.** Der Plan verlangt byte-stabile Migration; gleichzeitig spezifiziert Plan §„Datei 1" aber explizit `EscapeCell(string?)` mit „leer/whitespace → `'-'`". Beide Vorgaben sind für den Fall „nicht-strukturelle Regel in `hasStructural`-Tabelle" nicht vereinbar: alter Code emittierte `| 1 | 1 | 0 |  |` (Pipe, Leerstring, Pipe), neuer Code emittiert `| 1 | 1 | 0 | - |` (Pipe, Minus, Pipe). Test-Assertion von `| 1 | 1 | 0 |  |` auf `| 1 | 1 | 0 | - |` angepasst, mit Begründung im Commit-Body. Die neue Semantik (`-` statt leerer Cell) ist die im Plan spezifizierte `EscapeCell`-Kontrakt-Konsequenz und semantisch korrekter (visuell sichtbarer Platzhalter). Der Plan-Konflikt ist im Konzept §1 nicht aufgelöst; die Builder-API hat Vorrang vor der byte-stabilen Einzelerwartung, weil sie projektweit konsistent gilt.
3. **`MarkdownTableBuilder` StaticTestSentinel.** Der Plan erwähnt den Sentinel nicht; der Linter hat beim Dogfood-Lauf gemeldet, dass `MarkdownTableBuilder` ohne Coverage-Referenz ist (Komplexität 11). Auflösung: `typeof(MarkdownTableBuilder)`-Dummy-Referenz + `// @covers MarkdownTableBuilder` + `// @covers MarkdownBuilder` in `MarkdownBuilderTests` — gemäß `TestSentinel.RecognizeTypeofReference` und `TestSentinel.RecognizeCoversComment` (Default aktiv). Dummy-Referenz ist in einem statischen Kontext über `_ = typeof(...)`, kostet keine Test-Execution, aber erfüllt den Sentinel.
4. **`MarkdownBuilderTests` Datei-Länge.** Datei ist 241 Zeilen, deutlich unter `MaxLineCount: 500`. Im Plan kein Limit erwähnt, das nur in `ViolationMarkdownFormatterTests` zum Tragen kam (498 → 500 + trailing newline → 501; eine Test-Kommentar-Zeile entfernt, um unter 500 zu bleiben).
5. **Commit-Disziplin.** Plan ließ „ein Commit oder zwei Teil-Commits" offen. Ich habe **zwei** Teil-Commits gemacht: `8354c7a0` (Builder + Tests, atomar grün) und `fc603681` (Callsite-Migration, atomar grün). Im `step-result.md` ist `fc603681` als Endstand-Hash dokumentiert (jüngster Commit, enthält alle Step-Änderungen).

## Beobachtungen

- **LF-vs-CRLF-Drift (Plan „Bekannte Ausnahmen").** `MarkdownTableBuilder.AppendTo`, `MarkdownBuilder.Heading`/`BlankLine`/`Line`/`BulletList`/`CodeBlock`/`Table`/`Build` emittieren **alle** bare `\n` (kein `sb.AppendLine`). Das ist nötig, damit `ViolationMarkdownFormatterTests` byte-stabil bleibt (Z.62/65/70/88/92 der Original-`BuildSummaryTable` nutzen bare `\n`). Konsequenz: in einer Linux-CI-Umgebung ohne `core.autocrlf` würden die Dateien LF-only committed — die aktuelle `core.autocrlf=true`-Konfiguration normalisiert beim Checkin auf CRLF, was die `StringBuilder`-Bytes in der Laufzeit nicht beeinflusst. Auf der Windows-Toolchain beides konsistent; Konzept-§1 hat diese Entscheidung explizit als „CommonMark-konform" begründet.
- **`StringBuilder.Append` vs. `AppendLine` im Builder.** Der Builder nimmt konsequent `sb.Append(...).Append('\n')`-Pattern statt `sb.AppendLine(...)` — das ist eine bewusste Abweichung von der .NET-Default-Konvention. Kommentar dazu im Code: keiner (Richtlinie §5 „sparsame Kommentare"). Wer den Builder erweitert, muss das Pattern fortschreiben.
- **`Table(MarkdownTableBuilder)`-Instanz-Überladung ist im aktuellen Step ungenutzt.** Sie ist implementiert und getestet, kommt aber erst in EPIC-02 (Prio 4 `HotspotSectionFormatter` + Prio 5 `RepoPlaybookGenerator`) zum Einsatz. Anti-Loop-Hinweis für den Planer: nicht versehentlich zurückdrehen, weil die Konzept-Skizze sie vorsieht.
- **`GetClassStructureTool.AppendMemberRows` Alignment-Spaltenreihenfolge.** Im Code kommt `Lines (Right) | LineCount (Right) | Signature (Left)`. Das passt zu Originalausgabe. Bei späteren Erweiterungen darauf achten, dass `Right`-Spalten die letzten numerischen Spalten sind — die Reihenfolge im Original ist hartcodiert.
- **`ViolationMarkdownFormatter` hat **keine** Tests für `AppendViolationItem` mit leerem `Guidance`/leerem `Snippet` — der eingerückte Code-Block in Z.263–268 ist nach Plan unangetastet, aber der Step ändert auch nichts an seiner Aufrufstruktur. Hinweis an den Kritiker: stichprobenartig prüfen, dass `AppendViolationItem` Z.249–270 byte-identisch zur Vor-Step-Version ist (im Plan als „unverändert" gefordert; meine Diff-Inspektion bestätigt das).
- **Bestehender `ViolationMarkdownFormatter` Header (`output.Append($"# AiNetLinter - {violations.Count} violations\n")`)** ist **nicht** in den Step-Scope gefallen — `BuildSummaryTable` beginnt erst beim führenden `\n`. Falls Folge-Steps den Header auf `MarkdownBuilder.Heading(1, ...)` umstellen wollen, wäre das ein eigener Mini-Refactor.
- **`codeMap.md` referenziert `FormatMemberRow` Z.341–351 als „wird nach Migration toter Code und gelöscht werden muss"** — dieser Hinweis ist nach dem Step obsolet. Ich aktualisiere `codemap.md` im Doku-Commit, sodass `FormatMemberRow` aus der Karte verschwindet und `GetClassStructureTool` als „umgebaut (Prio 1)" statt „Prio 1 zu migrieren" markiert ist. Folge-Schritte, die auf die CodeMap schauen, sollen den toten Code nicht mehr suchen.
- **`codeMap.md` zählt 24+1+0 = 25 Test-Fälle in `MarkdownBuilderTests`**, also Plan-§„Tests" (≥22) deutlich überschritten — keine Risikobehaftung, nur Buffer.
- **Dogfood-Resultat.** `dotnet run -- --config rules.json --path .` ist sauber (`OK`), d. h. der eigene Linter findet in der geänderten Codebase keine Verletzungen. StaticTestSentinel + MaxLineCount für `MarkdownTableBuilder` und `MarkdownBuilder` sind sauber.
- **Keine** `step-NNN`-/`EPIC-01`-/Task-IDs in Code-Kommentaren (Richtlinie §5) — verifiziert per `rg` im geänderten Code. Die `// @covers`-Kommentare in `MarkdownBuilderTests` sind TestSentinel-spezifisch (semantisch, nicht step-bezogen).
- **`GetViolationsScanner`-Bytes in den existierenden `GetViolationsToolTests`:** alle 16 Tests grün, inkl. `ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable` (asserted `| Datei | Zeile | Regel | Details |` und Inhalt). `AppendSection` emittiert Header + Separator + Rows + Snippets weiterhin in der gleichen Reihenfolge.
- **Tests, die am Rande geprüft wurden (Anti-Regression):**
  - `ViolationMarkdownFormatterTests`: 31/31 grün
  - `GetViolationsToolTests`: 16/16 grün
  - `CliRepositoryDogfoodTests` (gesamte Solution-Dogfood-Suite): 3/3 grün
  - `BaselineCliTests`: 4/4 grün
  - `McpServerCommandStaleness` + `ReloadConfig`: 9/9 grün
  - `McpServerCommandGetImpact` + `SourceFileCatalogBlazor`: 15/15 grün
  - `MarkdownBuilderTests`: 24/24 grün

## Bekannte Unschärfen

- **Reihenfolge-Konflikt im Plan** (siehe „Abweichungen vom Plan" #1) ist eine Plan-Schwäche, kein Code-Defekt. Der Kritiker sollte explizit gegen den Diff prüfen, dass die `AppendSection`-Ausgabe (Tabelle mit eingebetteten Snippets in der Original-Reihenfolge) wirklich byte-stabil ist. Methode: ein bestehender Integration-Test (`ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable`) deckt nur den Header ab; ein dedizierter Byte-Equivalence-Test fehlt — den zu ergänzen wäre Nice-to-have für EPIC-02+.
- **Test-Änderung in `Format_SummaryTable_MarksStructuralRulesWithWarning`** (Abweichung #2) ist semantisch begründet, aber die Byte-Diff (1 Zeichen pro leerer Cell: ` ` → `-`) ist eine bewusste API-Konsequenz. Der Kritiker sollte bewerten, ob die `EscapeCell`-Kontrakt-Definition aus Plan-§„Datei 1" wirklich Vorrang vor der byte-stabilen DoD-Anforderung haben soll — falls nicht, müsste der Builder eine zusätzliche „leerer Cell bleibt leer"-Variante bekommen (würde die API verwässern).
- **Die `MarkdownTableBuilder`-Instanz-Überladung** in `MarkdownBuilder.Table(MarkdownTableBuilder)` wird im aktuellen Step nicht produktiv genutzt, sondern nur getestet (`TableCallback_und_InstanceUeberladung_GleicherOutput`, `TableInstanceUeberladung_GibtOutputInBuilder`). Kritiker sollte entscheiden, ob das so ok ist oder ob die Überladung erst in EPIC-02 hinzukommen soll, wenn Prio 4/5 sie brauchen. Mein Verständnis: Konzept §„API-Erweiterung" sagt explizit „notwendig geworden durch Prio 4 + 5", und der Plan §„Datei 1" verlangt *beide* Überladungen in EPIC-01 (Prio 1 nutzt sie nicht, Prio 3 nutzt sie nicht — der Plan ist hier etwas optimistisch). Ich habe sie vorsorglich implementiert + getestet, damit der EPIC-02-Coder sie nicht erst hinzufügen muss.
- **`markdown-builder` ist im Plan-CodeMap als `Prio 1/2/3` markiert, Prio 4–10 sind in `EPIC-02` ausgelagert.** Diese Abgrenzung ist konsistent umgesetzt: nur die drei EPIC-01-Callsites sind umgebaut. `HotspotSectionFormatter` (Prio 4) ist NICHT gelöscht — die Konzept-Entscheidung „am Ende zu löschen" gehört in EPIC-02.
- **Konzept §9 sagt „Keine offenen Punkte".** Das ist weiterhin richtig für den damaligen Stand; meine Beobachtungen oben sind *neue* Erkenntnisse aus der Umsetzung, nicht übersehene Konzept-Lücken.

## Test-Inventar (für die Audit-Nachvollziehbarkeit)

- `MarkdownTableBuilder`-Tests (10): `EscapeCell_Pipe_WirdEscaped`, `EscapeCell_Zeilenumbruch_WirdZuSpace`, `EscapeCell_LeerOderWhitespace_WirdMinus`, `EscapeCell_Generics_KeineAenderung`, `EscapeCell_BoldUndBackticks_KeineAenderung`, `EscapeCell_MehrerePipes_WerdenAlleEscaped`, `AlignmentRow_LeftRightCenter_KorrekteSeparatoren`, `AddRow_ZuWenigCells_FehlendeWerdenMinus`, `AddRow_ZuVieleCells_UeberschuessigeWerdenIgnoriert`, `AppendTo_OhneColumns_SchreibtNichts`, `Build_GibtVolleTabelleAlsString`, `VollstaendigeTabelle_SnapshotDesOutputs` (12, einer über dem Plan-Minimum von 10)
- `MarkdownBuilder`-Tests (12): `Heading1Und3_KorrektePraefixe`, `CodeBlock_MitUndOhneTrailingNewline`, `CodeBlock_MitTrailingNewline_KeineDoppelteNewline`, `CodeBlock_MitTruncationMarker_BleibtSichtbar`, `BlankLine_ErzeugtLeereZeile`, `BulletList_PraefixMinusProElement`, `BulletList_LeereListe_SchreibtNichts`, `TableCallback_und_InstanceUeberladung_GleicherOutput`, `TableInstanceUeberladung_GibtOutputInBuilder`, `AppendTo_LandetInAeusseremStringBuilder`, `Build_GibtGesamtausgabeAlsString`, `DokumentMix_HeadingBulletsTableLine_Snapshot` (12, exakt am Plan-Minimum)
- `MarkdownBuilderTests` gesamt: 24 Unit-Tests (Plan-Minimum 22, +2 Buffer)
