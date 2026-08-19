---
status: done
type: step-result
task: markdown-builder
step: 002
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-19
code_commit_hash: b1a39ab1
status_after: done
blocker_category: n/a
---

# Result Step 002: MarkdownTableBuilder zeilenweise API + EPIC-01 DoD präzisieren

## Zusammenfassung

Finding #1 aus `step-001/step-review.md` (drift-anfällige Inline-Reimplementierung der
Markdown-Tabellen-Format-Logik in `GetViolationsScanner.AppendSection`) durch eine
zeilenweise Public-API auf `MarkdownTableBuilder` (`BuildHeaderLine`,
`BuildSeparatorLine`, `BuildRowLine(params)`) + privaten `FormatRow`-Helper aufgelöst;
`AppendTo` und `GetViolationsScanner.AppendSection` nutzen diese API jetzt als
Single-Source-of-Truth. `AppendTo`-Byte-Stabilität durch `VollstaendigeTabelle_SnapshotDesOutputs`
(grün) verifiziert; `GetViolationsScanner`-Byte-Stabilität durch alle 16 `GetViolationsToolTests`
(grün) bestätigt. 6 neue Unit-Tests verriegeln die neue API gegen Drift. Finding #2 war
reine DoD-Sprachpräzisierung — vom Orchestrator bereits in `roadmap.md` Z.61 angewendet,
hier nur verifiziert (nicht im Code-Commit, separat committed). Build, Fast-Tests-Gate
(1428/1428) und Dogfood-Suite grün.

## Geänderte Dateien

- `src/AiNetLinter/Output/MarkdownBuilder.cs` — Refactor: `MarkdownTableBuilder` um drei zeilenweise `internal`-Methoden (`BuildHeaderLine()`, `BuildSeparatorLine()`, `BuildRowLine(params object?[])`) und einen privaten `static string FormatRow(string[])`-Helper erweitert; `AppendTo` refaktoriert, sodass es Header/Separator/Row aus diesen Methoden + `FormatRow` zusammensetzt (Byte-Output identisch); ungenutzten `using System.Linq`-Import entfernt.
- `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` — Refactor: `AppendSection` Z.235-282 nutzt jetzt `MarkdownTableBuilder` mit `.AddColumn(...)` + `table.BuildHeaderLine()` / `table.BuildSeparatorLine()` / `table.BuildRowLine(...)` statt der Inline-`string.Join` + `align switch` + `Select`-Reimplementierung. Reihenfolge-Treue Zeile → Snippet → Zeile → Snippet bleibt erhalten (siehe DoD-Assertion `ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable`, grün). `using System.Linq` bleibt (für `OrderBy`/`ThenBy`).
- `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs` — Erweitert: 6 neue Unit-Tests (`BuildHeaderLine_Standardfall_GibtEscapedHeaderMitPipes`, `BuildHeaderLine_HeaderMitPipe_WirdEscaped`, `BuildSeparatorLine_LeftRightCenter_KorrekteFormatierung`, `BuildRowLine_Standardfall_GibtEscapedCellsMitPipes`, `BuildRowLine_ZuWenigCells_FuellstandMitMinus`, `BuildRowLine_NullCells_WerdenMinus`) — verriegeln die neue Public-API gegen Drift. Bestehende Tests unangetastet (insbesondere `VollstaendigeTabelle_SnapshotDesOutputs`).

## Commit

- **Code-Commit-Hash:** `b1a39ab1a86a84179bf60f03cffe602a8634a6b8` (kurz `b1a39ab1`)
- **Message:**
  ```
  refactor(output): MarkdownTableBuilder zeilenweise Public-API + GetViolationsScanner umstellen [markdown-builder]

  - MarkdownTableBuilder.BuildHeaderLine/BuildSeparatorLine/BuildRowLine(params) als zeilenweise Public-API ergaenzt, privater FormatRow-Helper.
  - AppendTo auf die neuen Methoden + FormatRow refaktoriert (byte-stabil: bestehende Snapshot-Tests gruen).
  - GetViolationsScanner.AppendSection nutzt die neue API statt Inline-string.Join + align switch. Alignment-Format-Strings nur noch in MarkdownTableBuilder definiert.
  - 6 neue Unit-Tests in MarkdownBuilderTests: HeaderLine, SeparatorLine, RowLine (Standard, Pipe-Escape, Cell-Padding, Null-Cells).
  Refs: tasks/markdown-builder/step-002
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).
- **Roadmap-Diff:** bereits vom Orchestrator vorab in `roadmap.md` Z.61 angewendet; **nicht** im Code-Commit (separat, vorab).

## Build-/Test-Output

```
dotnet build                                                                               → grün (0 Warnungen, 0 Fehler, 14 s)
dotnet test src/AiNetLinter.FastTests --filter Category=Unit                               → grün (1004 Tests, 0 Fehler, 7 s)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress                            → grün (1428 Tests, 0 Fehler, 8 s) — Plan-DoD ≥1428 erfüllt
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~MarkdownBuilderTests     → grün (30/30, +6 neu gegen 24 in step-001)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetViolationsToolTests   → grün (16/16, byte-stabile Verifikation von AppendSection)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ViolationMarkdownFormatterTests → grün (31/31, EscapeCell-Vertrag-Konsequenz bestätigt)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~CliRepositoryDogfoodTests → grün (3/3, 31 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~BaselineCliTests          → grün (4/4, 14 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandStaleness|FullyQualifiedName~ReloadConfig → grün (9/9, 27 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~GetImpact|FullyQualifiedName~SourceFileCatalogBlazor  → grün (15/15, 34 s)
dotnet run -- --config rules.json --path .                                                 → grün (OK, keine Violations — Dogfood-Suite sauber)
```

## Abweichungen vom Plan

1. **`using System.Linq` in `MarkdownBuilder.cs` entfernt.** Der Plan erwähnt diese Möglichkeit implizit ("Die ungenutzten `using System.Linq`-Imports für `Select` entfallen ggf." in `GetViolationsScanner`); bei `MarkdownBuilder.cs` ist sie konsequenterweise ebenfalls weggefallen, weil keiner der verbliebenen Code-Pfade (`AddColumn`, `AddRow`, `Build*Line`, `AppendTo`, `Build`, `EscapeCell`) noch `Linq` nutzt. Kein Verhaltens-Drift; statische Verifikation per `rg "\.Select\(|\.OrderBy\(|\.ThenBy\("` zeigt null Treffer. Der Plan fordert diese Verifikation explizit nur für `GetViolationsScanner`; die parallele Anwendung auf `MarkdownBuilder.cs` ist eine konsequente Mitnahme und ein DRY-Vorteil (weniger ungenutzte Imports).
2. **Code-Commit als ein einziger Commit, nicht zwei Teil-Commits.** Der Plan lässt beide Optionen offen ("Logische Trennung in zwei Teil-Commits ist OK (analog zu step-001)"). Im Gegensatz zu step-001 sind hier API-Erweiterung und Consumer-Migration funktional gekoppelt (Finding #1 nennt beide explizit als同一个 Refactor) — eine Zwischen-State ohne Consumer-Migration würde die neue API sofort zu totem Code machen und das DoD-Argument "API konsolidiert die Format-Logik" nicht greifbar belegen. Daher ein atomarer Commit über alle drei Dateien. Der Diff (113 Insertions, 35 Deletions) ist klein genug, dass eine Aufteilung keinen Review-Vorteil bringt.

## Beobachtungen

- **`BuildRowLine`-Implementierung** in `MarkdownBuilder.cs` (Z.66-75) ist exakt spiegelbildlich zu `AddRow` (Z.28-38) — beide nutzen dieselbe Cell-Padding-/Null-Coalescing-/EscapeCell-Semantik. Die Pläne-Tests `BuildRowLine_ZuWenigCells_FuellstandMitMinus` und `BuildRowLine_NullCells_WerdenMinus` sind die direkten API-Spiegel der bestehenden `AddRow_ZuWenigCells_FehlendeWerdenMinus`-Tests; bewusste Verriegelung gegen Drift, falls jemand nur eine der beiden Methoden anpasst.
- **`FormatRow(string[])`** ist `private static` und nimmt bereits-escapte Cells. `AppendTo` reicht die Cells aus `_rows` (die in `AddRow` schon durch `EscapeCell` gelaufen sind) unverändert durch — kein doppeltes Escaping. `BuildRowLine` baut das `escaped[]`-Array selbst und reicht es an `FormatRow`. Die Trennung "Cell-Format" (`EscapeCell` + Cell-Padding) vs. "Zeilen-Format" (`FormatRow`) ist sauber: `FormatRow` kennt keine Cell-Inhalte, nur Pipes.
- **`using System.Linq` in `GetViolationsScanner.cs` bleibt zwingend erhalten** — nach Refactor werden `OrderBy(...).ThenBy(...).ThenBy(...)` (Z.257-259) und `Take(maxResults).ToList()` (Z.219) sowie `Where(...).ToList()` (Z.221-222) weiterhin gebraucht. Der Plan-Hinweis ("vor Einchecken mit `rg "Select\("` verifizieren — die `OrderBy().ThenBy()`-Aufrufe brauchen es weiterhin") hat sich bestätigt: alle `Select(`-Aufrufe sind weg, `System.Linq` aber nicht.
- **Plan-Anmerkung zu `MarkdownBuilder.cs` Z.137 (Schicht-Trennung `Maps →` darf nicht `Mcp.Tools` referenzieren)** ist nicht relevant für diesen Step — die geänderte Datei ist reine Builder-API ohne Schicht-Bezug.
- **Anti-Regression-Sichtprobe:** `VollstaendigeTabelle_SnapshotDesOutputs` (Byte-Snapshot für `table.Build()`) bleibt nach dem Refactor von `AppendTo` auf die neuen Methoden byte-identisch grün. Das ist der direkte Beweis, dass `BuildHeaderLine + \n + BuildSeparatorLine + \n + for each row: FormatRow(row) + \n` semantisch identisch zur vorherigen Inline-Implementierung ist. `Format_SummaryTable_MarksStructuralRulesWithWarning` (Z.274) assertet weiterhin `| EnforceSealedClasses | 1 | 1 | 0 | - |` — durch präzisierte DoD gedeckt, kein Test-Re-Assert nötig.
- **Methodenzeilen-Limits** eingehalten: `BuildHeaderLine` 8 Z., `BuildSeparatorLine` 14 Z., `BuildRowLine` 9 Z., `FormatRow` 3 Z. (alle weit unter `MaxMethodLineCount: 60`). `AppendTo` schrumpft von 33 auf 9 Z. Datei `MarkdownBuilder.cs` wächst von 150 auf 167 Z. (unter `MaxLineCount: 500`).
- **Roadmap-Verifikation:** `roadmap.md` Z.61 enthält die präzisierte DoD-Sprache ("Diff im Markdown-Output **strukturell byte-stabil** (Header, Separator, Spaltenreihenfolge, Reihenfolge, Leerzeilen unverändert) und im Cell-Content **`EscapeCell`-konform** (leer/whitespace Cells emittieren `-`, Pipes werden `\|`); kein User-sichtbarer Drift in der dokumentierten Struktur."). Wurde vorab vom Orchestrator angewendet, hier nur verifiziert (per `Read` Z.61) — kein Code-Change nötig, nicht im Code-Commit.
- **Dogfood-Suite (`--config rules.json --path .`):** grün, OK. Der Linter findet in der geänderten Codebase keine Verletzungen; `MaxMethodLineCount`, `MaxLineCount`, `Sealed-Pflicht`, `EnableTestSentinel` (durch bestehende `// @covers` + `typeof`-Referenz in `MarkdownBuilderTests`) sind sauber.

## Bekannte Unschärfen

- **Plan sagt "Logische Trennung in zwei Teil-Commits ist OK (analog zu step-001)"** — habe ich nicht gemacht (siehe Abweichungen #2). Falls der Kritiker explizit zwei Commits fordert, müsste der zweite Commit eine No-Op-Trennung werden, was das Git-Log verschmutzt. Empfehlung: bei künftigen Schritten den Plan präziser fassen ("atomar EIN Commits, weil X" / "explizit ZWEI Commits, weil Y"), statt der Default-Formulierung "ist OK".
- **Plan §„Datei 2" Z.121-162 (Konkrete Form)** zeigt `mb.Line(table.BuildHeaderLine())` — das ist die Implementierung, die ich umgesetzt habe. Aber: das Pattern `mb.Line(...)` für alle drei Zeilen + `mb.CodeBlock` + `mb.BlankLine` ist im Wesentlichen identisch zur step-001-Implementierung, nur dass jetzt `table.Build*Line(...)` statt der Inline-`string.Join`-Berechnung im Spiel ist. `mb.Table(table)` (die Konzept-empfohlene Senke) wird hier **nicht** genutzt — `AppendSection` braucht die interleavte Zeile→Snippet→Zeile→Snippet-Reihenfolge (Konzept §3 Prio 2 + step-001 Reihenfolge-Treue-Anforderung). Der Plan übernimmt diese Entscheidung explizit; ich weise nur darauf hin, dass die "neue API" damit im Wesentlichen eine Escaping-/Format-Konsolidierung ist, nicht eine Architektur-Umstellung (der `mb.Table(table)`-Pfad bleibt ungenutzt, wäre aber über die `MarkdownBuilder.Table(MarkdownTableBuilder)`-Überladung für andere Callsites verfügbar, siehe TD-002).

## Test-Inventar (für die Audit-Nachvollziehbarkeit)

- **Neu in step-002 (6 Tests):**
  - `BuildHeaderLine_Standardfall_GibtEscapedHeaderMitPipes`
  - `BuildHeaderLine_HeaderMitPipe_WirdEscaped` — verriegelt `EscapeCell`-Anwendung auf Header-Text
  - `BuildSeparatorLine_LeftRightCenter_KorrekteFormatierung` — verriegelt Alignment-Format-Strings
  - `BuildRowLine_Standardfall_GibtEscapedCellsMitPipes`
  - `BuildRowLine_ZuWenigCells_FuellstandMitMinus` — spiegelt `AddRow_ZuWenigCells_FehlendeWerdenMinus`
  - `BuildRowLine_NullCells_WerdenMinus` — spiegelt `EscapeCell` Null-Semantik
- **Byte-stabile Bestandstests (verifiziert in step-002, kein Test-Re-Assert nötig):**
  - `MarkdownBuilderTests.VollstaendigeTabelle_SnapshotDesOutputs` — byte-stabil grün (Refactor-Verifikation `AppendTo`)
  - `MarkdownBuilderTests.Build_GibtVolleTabelleAlsString`, `AlignmentRow_LeftRightCenter_KorrekteSeparatoren`, `AddRow_ZuWenigCells_FehlendeWerdenMinus`, `TableCallback_und_InstanceUeberladung_GleicherOutput` — alle grün
  - `GetViolationsToolTests` 16/16 — byte-stabile Verifikation `AppendSection`
  - `ViolationMarkdownFormatterTests` 31/31 — `EscapeCell`-Vertrag-Konsequenz bestätigt
- **Test-Klassen-Inventar nach step-002:**
  - `MarkdownBuilderTests`: 30 Unit-Tests (24 aus step-001 + 6 neu aus step-002)
  - FastTests `Category!=Stress`-Gate: 1428 Tests (1422 aus step-001 + 6 neu = 1428)
