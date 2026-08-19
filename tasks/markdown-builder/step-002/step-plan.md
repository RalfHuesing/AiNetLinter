---
status: open
type: step-plan
task: markdown-builder
step: 002
corrects: step-001
title: "MarkdownTableBuilder zeilenweise API + EPIC-01 DoD präzisieren"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-19
related_to: [step-001/step-review.md]
---

# Step 002: MarkdownTableBuilder zeilenweise API + EPIC-01 DoD präzisieren

## Bezug

- **Task:** `markdown-builder`
- **Epic:** `EPIC-01` (vom korrigierten Step übernommen) — *„MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen"*
- **Korrektur von:** `step-001` — Verdict `issues`, zwei MAJOR-Findings (siehe `step-001/step-review.md` §Findings).
- **Scope dieses Steps:** ausschließlich die zwei in `step-001/step-review.md` §Findings gelisteten Punkte. Keine Ausweitung auf EPIC-02.

## Aktueller Projektzustand (JIT-Kontext)

- **Finding #1 — bestätigt beim erneuten Lesen von `GetViolationsScanner.cs:235-282` und `MarkdownBuilder.cs:40-74`:** `AppendSection` baut Header (`"| " + string.Join(" | ", columns.Select(EscapeCell)) + " |"`), Separator (`"|" + string.Join("", columns.Select(c => c.Align switch { Right => "---:", Center => ":---:", _ => ":---" } + "|"))`) und Row (`"| " + string.Join(" | ", cells.Select(EscapeCell)) + " |"`) inline als `string.Join` + `align switch` + `EscapeCell`-Select, obwohl `MarkdownTableBuilder.AppendTo` Z.40-74 exakt dieselbe Format-Semantik besitzt. Alignment-Format-Strings (`:---|`, `---:|`, `:---:|`) und die `"| " + … + " |"`-Row-Pipe-Konstruktion sind damit in **zwei** Dateien definiert. `AddRow` Z.28-38 + `AppendTo` Z.40-74 sind im Builder die einzige andere Quelle derselben Bytes — Drift-Risiko bei jeder `ColumnAlign`-Enum-Erweiterung oder Separator-/Header-Format-Anpassung.
- **Finding #2 — bestätigt beim erneuten Lesen von `ViolationMarkdownFormatter.cs:82-91` + `ViolationMarkdownFormatterTests.cs:274`:** `BuildSummaryTable` emittiert für nicht-strukturelle Regeln in `hasStructural`-Tabellen `| EnforceSealedClasses | 1 | 1 | 0 | - |` (Cell-Wert `-`); Original-Implementierung emittierte `| EnforceSealedClasses | 1 | 1 | 0 |  |` (Cell-Wert Leerstring). Direkte Konsequenz von `EscapeCell`-Kontrakt aus `MarkdownBuilder.cs:85` (`if (string.IsNullOrWhiteSpace(text)) return "-"`). Plan-DoD in `step-001/step-plan.md` Z.137 fordert „byte-genau identische" Ausgabe — die zwei Stellen sind intern widersprüchlich, der Coder hat zugunsten der API-Konsistenz entschieden. Architektonisch korrekt, DoD-technisch ein Bruch, der explizit dokumentiert gehört.
- **Bestehende Tests, die als Output-Vertrag gelten:**
  - `MarkdownBuilderTests.VollstaendigeTabelle_SnapshotDesOutputs` (Z.132-150) — byte-genauer Snapshot für `table.Build()`. Nach dem Refactor von `AppendTo` auf `BuildHeaderLine`/`BuildSeparatorLine`/`FormatRow` muss dieser Test byte-identisch grün bleiben.
  - `GetViolationsToolTests` (16 Tests, inkl. `ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable`) — assertet `"| Datei | Zeile | Regel | Details |"` und Row-Inhalte für `AppendSection`. Nach dem Refactor müssen alle 16 byte-stabil grün bleiben.
  - `ViolationMarkdownFormatterTests` (31 Tests, inkl. `Format_SummaryTable_MarksStructuralRulesWithWarning` Z.262-275) — `Format_SummaryTable_MarksStructuralRulesWithWarning` Z.274 assertet bereits `"| EnforceSealedClasses | 1 | 1 | 0 | - |"` (vom Coder in step-001 angepasst). Wird in step-002 **nicht** zurückgedreht — der Vertrag ist die `EscapeCell`-Konsequenz.
- **Anti-Loop-Check (CodeMap konsultiert):** Keine früheren Entscheidungen in `codemap.md` widersprechen Finding #1 oder #2. `MarkdownTableBuilder` ist als „API-Builder für Tabellen" markiert; die zeilenweise API-Erweiterung ist eine natürliche Folge der zentralen Tabellen-Senke (Finding #1). `BuildSummaryTable` ist als „umgebaut (Prio 3)" markiert mit Hinweis auf den `EscapeCell`-Kontrakt; der 1-Byte-Drift ist als Konsequenz davon dokumentiert (Finding #2).
- **Bestehende Konvention (verifiziert in `AiNetLinterRichtlinien.mdc` + `AiNetLinter.mdc`):** `sealed` für konkrete Klassen, `#nullable enable` am Dateianfang, `MaxMethodLineCount: 60` (100 für Test-Methoden), xUnit v3 + `[Trait("Category", "Unit")]`, keine Task-/Step-Bezüge in Code-Kommentaren, sparsame Kommentare.

## Intention

Nach diesem Step existieren `MarkdownTableBuilder.BuildHeaderLine()` / `BuildSeparatorLine()` / `BuildRowLine(params object?[])` als zeilenweise Public-API; `AppendTo` ist auf diese Methoden + einen privaten `FormatRow`-Helper refaktoriert (kein Verhaltens-Drift); `GetViolationsScanner.AppendSection` ist von der Inline-Reimplementierung der Tabellen-Format-Logik befreit und nutzt durchgängig `MarkdownTableBuilder` als Single-Source-of-Truth für Header-/Separator-/Row-Format. Der Cell-Content-`EscapeCell`-Vertrag ist in der step-002-DoD und in der Roadmap-EPIC-01-DoD explizit als akzeptiert dokumentiert, sodass der 1-Byte-Drift in `BuildSummaryTable` (` ` → `-` für leere Cells) nicht mehr als Finding auslöst.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Output/MarkdownBuilder.cs`

- **Was:** Drei neue `internal`-Methoden auf `MarkdownTableBuilder` (Klasse Z.17) **nach** `AddRow` (Z.28-38) und **vor** `AppendTo` (Z.40-74) einfügen; `AppendTo` refaktorieren, sodass es die neuen Methoden + einen privaten `FormatRow`-Helper verwendet; einen `private static string FormatRow(string[] escapedCells)`-Helper ergänzen.
- **Konkrete Form:**

  ```csharp
  internal string BuildHeaderLine()
  {
      var parts = new string[_columns.Count];
      for (var i = 0; i < _columns.Count; i++)
      {
          parts[i] = EscapeCell(_columns[i].Header);
      }
      return "| " + string.Join(" | ", parts) + " |";
  }

  internal string BuildSeparatorLine()
  {
      var sb = new StringBuilder();
      sb.Append('|');
      foreach (var (_, align) in _columns)
      {
          sb.Append(align switch
          {
              ColumnAlign.Right => "---:|",
              ColumnAlign.Center => ":---:|",
              _ => ":---|",
          });
      }
      return sb.ToString();
  }

  internal string BuildRowLine(params object?[] cells)
  {
      var escaped = new string[_columns.Count];
      for (var i = 0; i < _columns.Count; i++)
      {
          var raw = i < cells.Length ? cells[i]?.ToString() ?? string.Empty : string.Empty;
          escaped[i] = EscapeCell(raw);
      }
      return FormatRow(escaped);
  }

  private static string FormatRow(string[] escapedCells)
  {
      return "| " + string.Join(" | ", escapedCells) + " |";
  }
  ```

  `AppendTo` ersetzt werden durch:

  ```csharp
  internal void AppendTo(StringBuilder sb)
  {
      if (_columns.Count == 0) return;
      sb.Append(BuildHeaderLine()).Append('\n');
      sb.Append(BuildSeparatorLine()).Append('\n');
      foreach (var row in _rows)
      {
          sb.Append(FormatRow(row)).Append('\n');
      }
  }
  ```

  `Build` (Z.76-81) bleibt **unverändert** (delegiert weiterhin an `AppendTo`).

- **Warum:** Single-Source-of-Truth für Header-Format, Separator-Format (inkl. `align switch` → `":---"`, `"---:"`, `":---:"`) und Row-Pipe-Format. Zukünftige `ColumnAlign`-Enum-Erweiterung oder Separator-Syntax-Anpassung erfordert nur **eine** Edit-Stelle (`BuildSeparatorLine`); die `GetViolationsScanner`-Inline-Reimplementierung entfällt komplett. `AppendTo` verliert keine Funktionalität (alle vorhandenen `MarkdownTableBuilder`-Tests bleiben byte-stabil grün), gewinnt aber eine klare Trennung zwischen „Format-Definition" (die drei Public-Methoden) und „Ausgabe" (`AppendTo`).
- **Byte-Stabilitäts-Vertrag:** `BuildHeaderLine()` ≡ `"| " + string.Join(" | ", columns.Select(EscapeCell)) + " |"` (aktuelle Inline-Form in `GetViolationsScanner.cs:255`); `BuildSeparatorLine()` ≡ `"|" + string.Join("", columns.Select(c => c.Align switch { … } + "|"))` (Z.256-261); `BuildRowLine(params object?[])` ≡ `"| " + string.Join(" | ", cells.Select(EscapeCell)) + " |"` (Z.271) inkl. Short-Cell-Padding auf `-` (gleiche Semantik wie `AddRow` Z.28-38). `FormatRow` wird **nicht** in `AppendTo` für `_rows` re-escaped (die Cells in `_rows` sind bereits `EscapeCell`-konform — siehe `AddRow` Z.34), sondern nur mit den bereits-escaped Werten gejoined.

### Datei 2: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs`

- **Was:** `AppendSection` Z.235-282 refaktorieren. Konkret: Z.248-263 (Inline-Header-/Separator-Konstruktion + `mb.Line(headerLine)` + `mb.Line(separator)`) sowie Z.270-272 (Inline-Row-Konstruktion) ersetzen durch:
  - Ein `MarkdownTableBuilder`-Instanz-Objekt mit `AddColumn`-Aufrufen (Z.248-254 `columns`-Array → Methoden-Aufrufe).
  - `mb.Line(table.BuildHeaderLine())` statt Z.255 + Z.262.
  - `mb.Line(table.BuildSeparatorLine())` statt Z.256-261 + Z.263.
  - Im `foreach` (Z.265-278) `mb.Line(table.BuildRowLine(relativePath, v.LineNumber.ToString(), v.RuleName ?? string.Empty, v.Details ?? string.Empty))` statt Z.270-272.
- **Konkrete Form (Z.235-282, neu):**

  ```csharp
  private static void AppendSection(
      StringBuilder sb, string heading, IReadOnlyList<RuleViolation> violations, string solutionDir)
  {
      var mb = new MarkdownBuilder();
      mb.Heading(2, heading).BlankLine();

      if (violations.Count == 0)
      {
          mb.AppendTo(sb);
          sb.Append("Keine.\n\n");
          return;
      }

      var table = new MarkdownTableBuilder()
          .AddColumn("Datei")
          .AddColumn("Zeile", ColumnAlign.Right)
          .AddColumn("Regel")
          .AddColumn("Details");

      mb.Line(table.BuildHeaderLine());
      mb.Line(table.BuildSeparatorLine());

      foreach (var v in violations.OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                                  .ThenBy(x => x.LineNumber)
                                  .ThenBy(x => x.RuleName, StringComparer.OrdinalIgnoreCase))
      {
          var relativePath = Path.GetRelativePath(solutionDir, v.FilePath).Replace('\\', '/');
          mb.Line(table.BuildRowLine(relativePath, v.LineNumber.ToString(), v.RuleName ?? string.Empty, v.Details ?? string.Empty));
          if (!string.IsNullOrWhiteSpace(v.Snippet))
          {
              mb.CodeBlock("csharp", v.Snippet!);
              mb.BlankLine();
          }
      }

      mb.AppendTo(sb);
      sb.Append('\n');
  }
  ```

- **Warum:** Eliminiert die in `step-001/step-review.md` Finding #1 beanstandete Inline-Reimplementierung der Tabellen-Format-Logik. Header-/Separator-Format (inkl. `align switch`) ist jetzt **nur noch** in `MarkdownTableBuilder` definiert. Reihenfolge-Treue (Plan-DoD aus step-001 Z.137: „byte-genau identisch") bleibt erhalten — `mb.Line(table.BuildHeaderLine())` / `mb.Line(table.BuildSeparatorLine())` / `mb.Line(table.BuildRowLine(...))` / `mb.CodeBlock` / `mb.BlankLine` emittieren in derselben Reihenfolge wie die aktuelle `string.Join` + `Line`-Konstruktion. Die ungenutzten `using System.Linq`-Imports für `Select` entfallen ggf. — vor dem Einchecken mit `rg "Select\("` in der Datei verifizieren, falls der `OrderBy(...).ThenBy(...)` weiterhin gebraucht wird (er braucht `System.Linq`, das bleibt).

### Datei 3: `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs`

- **Was:** Drei neue Unit-Tests in `MarkdownBuilderTests` (Klasse Z.9-10, `[Trait("Category", "Unit")]` an der Klasse vorhanden) **nach** dem letzten Test `DokumentMix_HeadingBulletsTableLine_Snapshot` (Z.282-306) anhängen. Jeder Test ≤100 Zeilen, **kein** `sealed` (Test-Konvention). Sparsame Kommentare erlaubt (Richtlinie §5), aber keine Task-/Step-Bezüge.
- **Konkrete Test-Methoden:**

  ```csharp
  [Fact]
  public void BuildHeaderLine_Standardfall_GibtEscapedHeaderMitPipes()
  {
      var table = new MarkdownTableBuilder()
          .AddColumn("Spalte A")
          .AddColumn("Spalte B", ColumnAlign.Right);

      var line = table.BuildHeaderLine();

      Assert.Equal("| Spalte A | Spalte B |", line);
  }

  [Fact]
  public void BuildHeaderLine_HeaderMitPipe_WirdEscaped()
  {
      var table = new MarkdownTableBuilder().AddColumn("A | B");

      var line = table.BuildHeaderLine();

      Assert.Equal(@"| A \| B |", line);
  }

  [Fact]
  public void BuildSeparatorLine_LeftRightCenter_KorrekteFormatierung()
  {
      var table = new MarkdownTableBuilder()
          .AddColumn("L")
          .AddColumn("R", ColumnAlign.Right)
          .AddColumn("C", ColumnAlign.Center);

      var line = table.BuildSeparatorLine();

      Assert.Equal("|:---|---:|:---:|", line);
  }

  [Fact]
  public void BuildRowLine_Standardfall_GibtEscapedCellsMitPipes()
  {
      var table = new MarkdownTableBuilder()
          .AddColumn("A")
          .AddColumn("B");

      var line = table.BuildRowLine("eins | zwei", "drei");

      Assert.Equal(@"| eins \| zwei | drei |", line);
  }

  [Fact]
  public void BuildRowLine_ZuWenigCells_FuellstandMitMinus()
  {
      var table = new MarkdownTableBuilder()
          .AddColumn("A")
          .AddColumn("B")
          .AddColumn("C");

      var line = table.BuildRowLine("nur", "eins");

      Assert.Equal("| nur | eins | - |", line);
  }

  [Fact]
  public void BuildRowLine_NullCells_WerdenMinus()
  {
      var table = new MarkdownTableBuilder()
          .AddColumn("A")
          .AddColumn("B");

      var line = table.BuildRowLine(null, "x");

      Assert.Equal("| - | x |", line);
  }
  ```

- **Warum:** Verriegelt die neue Public-API gegen Drift. `BuildHeaderLine_HeaderMitPipe_WirdEscaped` deckt ab, dass `EscapeCell` auch auf Header-Text angewendet wird (Header-Spalten mit `|` im Namen wären sonst ein Escaping-Bug). `BuildRowLine_ZuWenigCells_FuellstandMitMinus` und `BuildRowLine_NullCells_WerdenMinus` spiegeln das `AddRow`-Verhalten und stellen sicher, dass `BuildRowLine` exakt dieselbe Cell-Padding-Semantik hat wie `AddRow` (relevant für Konsistenz, falls ein Caller `BuildRowLine` direkt nutzt, ohne vorher `AddRow` aufzurufen).
- **Wichtig:** Der bestehende Snapshot-Test `VollstaendigeTabelle_SnapshotDesOutputs` Z.132-150 wird **nicht** angefasst. Er beweist, dass `table.Build()` (= `AppendTo` + `ToString()`) nach dem Refactor byte-identisch zur Vor-Step-Version ist. Falls dieser Test fehlschlägt, hat der Refactor einen Bug — stoppen und korrigieren.

### Datei 4: `tasks/markdown-builder/roadmap.md` (Roadmap-Diff für Finding #2)

- **Was:** Die EPIC-01-DoD-Zeile (Z.61) von

  > *Definition of Done:* ... Diff im Output-Format **byte-genau identisch** (kein User-sichtbarer Markdown-Drift).

  auf

  > *Definition of Done:* ... Diff im Markdown-Output **strukturell byte-stabil** (Header, Separator, Spaltenreihenfolge, Reihenfolge, Leerzeilen unverändert) und im Cell-Content **`EscapeCell`-konform** (leer/whitespace Cells emittieren `-`, Pipes werden `\|`); kein User-sichtbarer Drift in der dokumentierten Struktur.

  präzisieren. (Konkret nur das **fettgesetzte** Schlüsselwort-Cluster ersetzen; Rest der Zeile identisch.)
- **Warum:** Finding #2 resultiert aus einer DoD-Sprachen-Unschärfe in step-001 („byte-genau identische") — die mit dem `EscapeCell`-Kontrakt aus `MarkdownBuilder.cs:85` (`leer/whitespace → '-'`) für den Spezialfall `hasStructural=true, structMarker=string.Empty` unvereinbar ist. Die Präzisierung in der Roadmap schließt die Lücke projektweit (jeder künftige Planer/Kritiker kann sich auf den präzisierten Vertrag berufen), nicht nur step-001-spezifisch. Die Test-Assertion-Änderung in `ViolationMarkdownFormatterTests.cs:274` von `| 1 | 1 | 0 |  |` auf `| 1 | 1 | 0 | - |` bleibt **unangetastet** — sie ist die korrekte Konsequenz des präzisierten Vertrags.
- **Hinweis:** Die Roadmap-Anpassung wird vom Orchestrator gemeinsam mit dem step-002-Plan-Commit zusammengeführt (siehe SKILL Fix-Modus §6 „Roadmap wird in diesem Modus nicht angefasst" — Ausnahme gilt für DoD-Präzisierungen explizit gemäß Orchestrator-Auftrag).

## Tests

- [ ] `MarkdownBuilderTests.BuildHeaderLine_Standardfall_GibtEscapedHeaderMitPipes` (neu)
- [ ] `MarkdownBuilderTests.BuildHeaderLine_HeaderMitPipe_WirdEscaped` (neu)
- [ ] `MarkdownBuilderTests.BuildSeparatorLine_LeftRightCenter_KorrekteFormatierung` (neu)
- [ ] `MarkdownBuilderTests.BuildRowLine_Standardfall_GibtEscapedCellsMitPipes` (neu)
- [ ] `MarkdownBuilderTests.BuildRowLine_ZuWenigCells_FuellstandMitMinus` (neu)
- [ ] `MarkdownBuilderTests.BuildRowLine_NullCells_WerdenMinus` (neu)
- [ ] `MarkdownBuilderTests.VollstaendigeTabelle_SnapshotDesOutputs` weiterhin grün (byte-stabile Refactor-Verifikation für `table.Build()`)
- [ ] `MarkdownBuilderTests.Build_GibtVolleTabelleAlsString` weiterhin grün
- [ ] `MarkdownBuilderTests.AlignmentRow_LeftRightCenter_KorrekteSeparatoren` weiterhin grün
- [ ] `MarkdownBuilderTests.AddRow_ZuWenigCells_FehlendeWerdenMinus` weiterhin grün
- [ ] `MarkdownBuilderTests.TableCallback_und_InstanceUeberladung_GleicherOutput` weiterhin grün
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category=Unit` grün
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetViolationsToolTests` grün (16/16, byte-stabile Verifikation von `AppendSection`)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ViolationMarkdownFormatterTests` grün (31/31, `EscapeCell`-Vertrag-Konsequenz in `Format_SummaryTable_MarksStructuralRulesWithWarning` Z.274 bestätigt)
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün (Stichproben-getrieben wie in step-001: `CliRepositoryDogfoodTests`, `BaselineCliTests`, `McpServerCommandGetImpactTests`, `McpServerCommandStalenessTests`, `SourceFileCatalog*Tests`)
- [ ] `dotnet build` fehler- und warnungsfrei (`TreatWarningsAsErrors = true`)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" in Datei 1–3 umgesetzt (1 Datei modifiziert `MarkdownBuilder.cs` — Refactor + 3 neue Methoden + 1 privater Helper; 1 Datei modifiziert `GetViolationsScanner.cs` — `AppendSection` nutzt die neue API; 1 Datei modifiziert `MarkdownBuilderTests.cs` — 6 neue Tests).
- [ ] Roadmap-Diff in Datei 4 angewendet (EPIC-01-DoD präzisiert); vom Orchestrator gemeinsam mit dem step-002-Plan-Commit zusammengeführt.
- [ ] `dotnet build` grün, ohne neue Warnings.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün (≥1428 Tests, +6 neue = 1428).
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün (Stichproben-getrieben).
- [ ] `MarkdownTableBuilder` hat weiterhin `sealed`; `MarkdownBuilder.cs` beginnt mit `#nullable enable`; keine `// step-002` / `// EPIC-01` / Task-IDs in Code-Kommentaren (Richtlinie §5).
- [ ] `MarkdownBuilderTests` (Testklasse) trägt `[Trait("Category", "Unit")]` an der Klasse, **nicht** `sealed`; Testmethoden ≤100 Zeilen.
- [ ] **Byte-Stabilität von `GetViolationsScanner.AppendSection`** gegen die bestehenden `GetViolationsToolTests` (16/16) verifiziert — `mb.Line(table.BuildHeaderLine())` / `mb.Line(table.BuildSeparatorLine())` / `mb.Line(table.BuildRowLine(...))` emittieren in derselben Reihenfolge dieselben Bytes wie die vorherige `string.Join` + `mb.Line` + `string.Join + EscapeCell.Select` + `mb.Line`-Konstruktion.
- [ ] **`AppendTo`-Byte-Stabilität** gegen `VollstaendigeTabelle_SnapshotDesOutputs` verifiziert — der Snapshot-Test Z.132-150 darf nicht angefasst werden und muss byte-identisch grün bleiben.
- [ ] **`EscapeCell`-Vertrag-Dokumentation:** Cell-Content folgt `EscapeCell`-Kontrakt (`leer/whitespace → '-'`, `|` → `\|`, `\r`/`\n` → ` `); der 1-Byte-Drift in `BuildSummaryTable` (für `hasStructural=true, structMarker=string.Empty`) ist **akzeptiert** und in der Roadmap-EPIC-01-DoD explizit als Vertragskonform dokumentiert.
- [ ] Commit auf aktuellem Branch, Conventional Commit auf Deutsch, imperativ, Suffix `[markdown-builder]`, Body mit `Refs: tasks/markdown-builder/step-002`-Trailer. **Kein** Roadmap-Diff im Code-Commit — der Orchestrator committet `roadmap.md` separat (oder gemeinsam mit dem Plan-Commit, je nach Orchestrator-Entscheidung).
- [ ] `step-002/step-result.md` geschrieben mit: tatsächlich committeter Hash, Beobachtungen aus der Umsetzung, Verweis auf die 6 neuen Test-Fälle + die 16+31 byte-stabilen Bestandstests.
- [ ] `status` in dieser `step-plan.md` von `open` auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — harte Code-Qualitätsmetriken: `MaxLineCount: 500` (MarkdownBuilder.cs wächst von 150 auf ca. 180 Z., weiterhin sicher); `MaxMethodLineCount: 60` (längste neue Methode: `BuildSeparatorLine` ~14 Z., alle im Limit); `MaxMethodParameterCount: 4` (neu: `BuildRowLine(params object?[])` — `params` zählt nicht unter die Regel, wie bereits in step-001 für `AddRow` akzeptiert).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Doku-Objektivität: nur Implementiertes dokumentieren, keine Superlative), §3 (Windows/PowerShell-Workflow, TRX-Logging bei Test-Failures), §5 (sparsame Code-Kommentare, **kein** Verweis auf `step-002`/`EPIC-01` im Code, Zero-Warning-Direktive), §6 (`rg`/`grep` durch MCP-Dogfooding ersetzen wo möglich — hier nicht relevant, da Coder direkt in den zwei kleinen Dateien arbeitet).

## Bekannte Ausnahmen

- **`ViolationMarkdownFormatterTests.Format_SummaryTable_MarksStructuralRulesWithWarning` Z.274** assertet seit step-001 `"| EnforceSealedClasses | 1 | 1 | 0 | - |"` (Cell-Wert `-` statt ` `). Diese Assertion bleibt in step-002 **unverändert** — der Wert ist `EscapeCell`-Vertragskonform und durch die präzisierte EPIC-01-DoD gedeckt. Kein Re-Assert in step-002 nötig.
- **`GetViolationsScanner.AppendSection` nutzt `MarkdownTableBuilder` jetzt nur als Format-Definition**, nicht als Tabellen-Senke (d. h. nicht via `table.AppendTo(sb)`). Das ist **kein** Verstoß gegen Konzept §3 Prio 2 — das Konzept-Beispiel zeigt `mb.Table(table)` am Schleifenende, aber der Plan in step-001 fordert explizit „Reihenfolge-Treue Zeile → Snippet → Zeile → Snippet", was mit `mb.Table(table)` (gesamte Tabelle am Stück) nicht vereinbar ist. Die in step-001 gewählte Aufteilung — `MarkdownTableBuilder` als Format-Definition + `MarkdownBuilder`/`mb.Line` als Ausgabe-Reihenfolge-Steuerung — ist die einzige tragfähige Lösung. Der Refactor in step-002 verstärkt diese Trennung, ohne sie zu ändern.
- **Method-Länge von `GetViolationsScanner.AppendSection`:** aktuell 47 Z. (Z.235-281), nach Refactor ca. 45 Z. (LoC-äquivalent) — bleibt unter `MaxMethodLineCount: 60`. Kein Splitting nötig.

## Code-Skizze (optional)

Siehe „Konkrete Änderungen" Datei 1 und Datei 2 oben — die Code-Snippets dort sind die verbindliche Vorlage. Abweichungen sind im `step-result.md` zu begründen.

## Notes

- **Reihenfolge der Schritte im Commit:** Der Coder kann die drei Datei-Änderungen in **einem** Commit bündeln (geringe Größe, ~80 Z. Diff) oder in zwei Teil-Commits trennen: (a) `MarkdownBuilder.cs` + `MarkdownBuilderTests.cs` (API-Erweiterung + Tests, atomar grün), (b) `GetViolationsScanner.cs` (Callsite-Migration, atomar grün). Beide Wege sind offen — der Plan lässt sie zu, solange am Ende **ein** Code-Commit mit allen Step-Änderungen existiert (analog zu step-001, der zwei Teil-Commits hatte). Der Roadmap-Diff wird vom Orchestrator verwaltet, nicht vom Coder.
- **Anti-Loop-Hinweis für Folge-Step-Planer:** Falls in EPIC-02 die `MarkdownTableBuilder`-Instanz-Überladung `MarkdownBuilder.Table(MarkdownTableBuilder)` (Datei 1 Z.141-145) produktiv genutzt wird (Prio 4 + 5), kann die hier eingeführte zeilenweise API optional auch in `MarkdownBuilder.Table(MarkdownTableBuilder)` als interner Pfad dienen — aber das ist Architektur-Ermessen für EPIC-02, nicht Aufgabe dieses Steps. **Nicht** in step-002 vorziehen.
- **API-Disziplin:** Die drei neuen `BuildXxxLine`-Methoden sind explizit als „zeilenweise Format-Definition" gedacht — sie emittieren **eine** Zeile **ohne** Trailing-Newline. Der `mb.Line(...)`-Wrapper im Caller setzt den Newline. `Build()` und `AppendTo(StringBuilder)` sind weiterhin die Bulk-Output-APIs und bleiben unangetastet (außer der internen Refaktorierung).
- **MCP-Dogfooding-Hinweis (Richtlinie §1):** Der Coder kann `ainetlinter.find_symbol` für „MarkdownTableBuilder" / „GetViolationsScanner" / „BuildSummaryTable" nutzen, um Aufrufstellen sicher zu identifizieren — `rg`/`grep` ist hier nur für die Verifikation der gelöschten Inline-Format-Strings nötig.
- **Kein `// step-002` im Code:** strikt durchhalten (Richtlinie §5). Die `// @covers`-Kommentare in `MarkdownBuilderTests` Z.13-14 sind `TestSentinel`-spezifisch (semantisch, nicht step-bezogen) und bleiben unverändert.
- **Roadmap-Diff-Vollständiger Wortlaut** (vom Orchestrator 1:1 in `roadmap.md` Z.61 zu übernehmen):

  ```diff
  -  *Definition of Done:* Builder + Tests grün, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün, `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün, `FormatMemberRow` gelöscht, `ViolationMarkdownFormatterTests` weiterhin grün, Diff im Output-Format **byte-genau identisch** (kein User-sichtbarer Markdown-Drift).
  +  *Definition of Done:* Builder + Tests grün, `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` grün, `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` grün, `FormatMemberRow` gelöscht, `ViolationMarkdownFormatterTests` weiterhin grün, Diff im Markdown-Output **strukturell byte-stabil** (Header, Separator, Spaltenreihenfolge, Reihenfolge, Leerzeilen unverändert) und im Cell-Content **`EscapeCell`-konform** (leer/whitespace Cells emittieren `-`, Pipes werden `\|`); kein User-sichtbarer Drift in der dokumentierten Struktur.
  ```
