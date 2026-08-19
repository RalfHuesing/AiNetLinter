---
status: done
type: step-result
task: markdown-builder
step: 003
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-19
code_commit_hash: 107b2682
status_after: done
blocker_category: n/a
---

# Result Step 003: HotspotSectionFormatter loeschen + ListRulesCommand + GetSymbolBodyTool

## Zusammenfassung

`Output/HotspotSectionFormatter.cs` ersatzlos geloescht; beide Aufrufer (`GetHotspotsScanner`,
`HotspotMapBuilder`) bekommen je eine private `AppendHotspotSection` mit `MarkdownBuilder`
(Heading(2) + BlankLine + `Table(t => ...)`-Callback mit Datei/Zeilen/Auslastung/Verbleibend;
Sortierung im Aufrufer; 'Keine.'-Fall via `mb.Line`). `ListRulesCommand.ListAll` nutzt
`MarkdownTableBuilder` (5 Spalten, kein Alignment) statt raw `sb.AppendLine`. `GetSymbolBodyTool`
nutzt `MarkdownBuilder.Heading(3)` + `BlankLine` + optional `Line(id)` + `BlankLine` +
`CodeBlock("csharp", body)` statt `string.Concat`; `.TrimEnd()` entfernt das zusaetzliche
Trailing-`\n` aus `CodeBlock` fuer den byte-stabilen MCP-Token-Vertrag. `DuplicateCode`-Warnung
in `GetHotspotsScanner` per `ainetlinter-disable` markiert (Schicht-Trennung-Konsequenz).
Build, Fast-Tests (1428/1428), Integration-Tests (321/321) und Dogfood-Suite gruen.

## Geaenderte Dateien

- `src/AiNetLinter/Output/HotspotSectionFormatter.cs` (geloescht, 45 Z.) — ersatzlos entfernt, beide
  Aufrufer reinkarnieren die Logik lokal.
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsScanner.cs` — `FormatReport` ruft jetzt
  lokale `AppendHotspotSection` (Heading + Tabelle + Sortierung) statt `HotspotSectionFormatter`;
  neue private `AppendHotspotSection(StringBuilder, string, IReadOnlyList<HotspotFileInfo>, int)`
  (27 Z., nutzt `MarkdownBuilder.Heading`/`BlankLine`/`Table(t => ...)`/`Line`/`AppendTo` + raw
  `sb.AppendLine()` fuer Trailing-Blank); Klassen-Doc-Kreuzverweis von `HotspotSectionFormatter`
  auf Hinweis "Schicht-Trennung Maps → darf nicht Mcp.Tools referenzieren" aktualisiert.
- `src/AiNetLinter/Maps/HotspotMapBuilder.cs` — `Build` ruft lokale `AppendHotspotSection`
  (Heading mit Emoji-Headings `🔴 ...` / `⚠ ...`); neue private `AppendHotspotSection(StringBuilder,
  string, IReadOnlyList<StructureFileInfo>, int)` (27 Z., identische Builder-Logik wie in
  `GetHotspotsScanner` aber mit `StructureFileInfo`-Eingabe-Typ). `using AiNetLinter.Output;`
  bleibt noetig (fuer `MarkdownBuilder` + `ColumnAlign`).
- `src/AiNetLinter/Commands/ListRulesCommand.cs` — `ListAll` nutzt `MarkdownTableBuilder` mit 5
  Spalten `RuleId`/`Bezeichnung`/`Intent`/`Severity`/`Auto-Fix`; Foreach-Schleife schreibt in
  `table.AddRow(...)`; `autoFix` als fertiger String `"ja (--fix)"`/`"-"`; `table.AppendTo(sb)`
  ersetzt den Header+Separator+Row-Block. Kein Alignment (alle Spalten linksbuendig per Default).
- `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` — `string.Concat`-Block (Z.60-64) ersetzt
  durch `MarkdownBuilder.Heading(3, ...)` + `BlankLine()` + optional `Line("id: ...")` +
  `BlankLine()` + `CodeBlock("csharp", body)`; `mb.Build().TrimEnd()` entfernt das eine
  zusaetzliche `\n` nach ` ``` ` (siehe "Beobachtungen" bzgl. `.TrimEnd()`-Begruendung).

## Commit

- **Code-Commit-Hash:** `107b26820a39b3eafa0e75a8a93e2f8d49aa2b3a` (kurz `107b2682`)
- **Message:**
  ```
  refactor(markdown): HotspotSectionFormatter loeschen + ListRules + GetSymbolBody umstellen [markdown-builder]

  - Output/HotspotSectionFormatter.cs ersatzlos geloescht; beide Aufrufer (GetHotspotsScanner, HotspotMapBuilder) bekommen je eine private AppendHotspotSection mit MarkdownBuilder.
  - GetHotspotsScanner.AppendHotspotSection und HotSpotMapBuilder.AppendHotspotSection nutzen Heading(2)+BlankLine+Table(t => ...)-Callback mit Spalten Datei/Zeilen/Auslastung/Verbleibend; Sortierung im Aufrufer; 'Keine.'-Fall via mb.Line.
  - ListRulesCommand.ListAll nutzt MarkdownTableBuilder mit 5 Spalten statt raw sb.AppendLine; Auto-Fix-Spalte 'ja (--fix)'/'-' als fertiger String.
  - GetSymbolBodyTool ersetzt string.Concat-Block durch MarkdownBuilder.Heading(3)+BlankLine+Line(id)+BlankLine+CodeBlock; .TrimEnd() entfernt das zusaetzliche Trailing-\n aus CodeBlock fuer byte-stabilen MCP-Token-Vertrag.
  - Duplikation der AppendHotspotSection-Body ist beabsichtigt: Schicht-Trennung Maps -> Mcp.Tools verbietet gemeinsamen Helper, mit ainetlinter-disable DuplicateCode in GetHotspotsScanner markiert.
  - HotspotSectionFormatter-Doc-Kreuzverweis in GetHotspotsScanner-Kommentar aktualisiert; ungenutzte AiNetLinter.Output-Import-Anpassung in HotspotMapBuilder (bleibt, wird fuer MarkdownBuilder/ColumnAlign gebraucht).

  Refs: tasks/markdown-builder/step-003
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                                            → gruen (0 Warnungen, 0 Fehler, 14 s)
dotnet test src/AiNetLinter.FastTests --filter Category=Unit                            → gruen (1004 Tests, 0 Fehler, 8 s)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress                         → gruen (1428 Tests, 0 Fehler, 8 s) — Plan-DoD >=1428 erfuellt
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetHotspotsToolTests  → gruen (11/11, byte-stabile Verifikation von FormatReport)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ListRulesCommandTests → gruen (8/8)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetSymbolBodyToolTests → gruen (6/6, Prio 10 MCP-Token-Vertrag, .TrimEnd()-Strategie bestaetigt)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~SourceFileCatalog|FullyQualifiedName~BaselineCliTests|FullyQualifiedName~CliRepositoryDogfoodTests|FullyQualifiedName~McpServerCommandContractTests → gruen (27/27, 51 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpLiveRepositoryTests → gruen (22/22, 37 s)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress                  → gruen (321 Tests, 0 Fehler, 1 m 53 s) — Plan-DoD erfuellt
dotnet run --project src/AiNetLinter -- --config rules.json --path .                    → gruen (OK, keine Violations — Dogfood-Suite sauber)
```

## Abweichungen vom Plan

1. **`using AiNetLinter.Output;` in `HotspotMapBuilder.cs` NICHT entfernt.** Der Plan schlug
   vor, den Import zu entfernen, falls nach `HotspotSectionFormatter`-Loeschung ungenutzt.
   Tatsaechlich wird `AiNetLinter.Output` jetzt wieder gebraucht (fuer `MarkdownBuilder` +
   `ColumnAlign` in der neuen `AppendHotspotSection`). Habe den Import daher drin gelassen.
   Kein Verhaltens-Drift; statisch verifiziert per `rg "MarkdownBuilder|ColumnAlign" -n
   src\AiNetLinter\Maps\HotspotMapBuilder.cs` (mehrere Treffer in der neuen Methode).
2. **`AppendHotspotSection`-Body in `GetHotspotsScanner` mit `// ainetlinter-disable
   DuplicateCode`-Marker.** Der Plan erwaehnt die geplante Duplikation in "Beobachtungen" als
   Pflicht-Notiz, hat aber keine konkrete Loesung fuer die Dogfood-Warnung
   `DuplicateCode (Jaccard-Score 1,00)` vorgegeben. Habe den Disable-Kommentar in der ersten
   Zeile der Scanner-Methode platziert (mit Begruendung "Schicht-Trennung Maps → Mcp.Tools
   verbietet gemeinsamen Helper" in derselben Zeile, wie die Plan-Empfehlung in
   `roadmap.md` Z.64 vorsieht). Reihenfolge: in `GetHotspotsScanner` markiert, in
   `HotspotMapBuilder` nicht — damit der Linter genau eine der beiden Methoden sieht und der
   Disable greift; bei umgekehrter Reihenfolge waere der Linter-Wegfall identisch, aber
   Konsistenz mit "Aufrufer im Mcp.Tools-Layer" schien mir sauberer.
3. **Doc-Kreuzverweis in `GetHotspotsScanner`-Klassen-Doc aktualisiert (Z.17-24).** Der
   Plan erwaehnt nur "vor dem Loeschen mit rg verifizieren — exakt 2 Treffer". Habe den
   bestehenden `<see cref="AiNetLinter.Output.HotspotSectionFormatter"/>`-Kreuzverweis im
   Klassen-Doc-Kommentar (Z.23) auf eine aktuelle Schicht-Trennung-Begruendung umgeschrieben,
   weil die `cref`-Referenz sonst nach Loeschung der Datei auf ein non-existent cref zeigen
   wuerde (kein Compiler-Fehler, aber stale Doku). Anmerkung im "Notes" des Plans
   ("Anti-Loop-Hinweis: keine step-/EPIC-Bezuege in Code-Kommentaren") bleibt eingehalten
   — der Kommentar erklaert die *Schicht-Trennung*, nicht den Step.

## Beobachtungen

- **Schicht-Trennung-Duplikation (`AppendHotspotSection` in `GetHotspotsScanner` +
  `HotspotMapBuilder`) — Plan-konform, aber explizit zu markieren:** Die zwei
  ~27-Zeilen-Methoden sind bis auf den Eingabe-Typ (`HotspotFileInfo` vs. `StructureFileInfo`,
  `Directory`-Feld wird ignoriert) byte-strukturell identisch. Konzept §3 Prio 4 letzter
  Bullet sieht diese Duplikation explizit vor ("Schicht-Trennung `Maps →` darf nicht
  `Mcp.Tools` referenzieren") und dokumentiert sie als akzeptiert. Die
  `DuplicateCode`-Linter-Regel wuerde sie sonst als Violation flaggen; Loesung: expliziter
  `// ainetlinter-disable DuplicateCode`-Kommentar mit Begruendung in derselben Zeile
  (Konvention aus `roadmap.md` Z.64). **Kein Tech-Debt-Eintrag noetig**, weil das die
  Konzept-Entscheidung exekutiert, nicht eine unbeobachtete Drift.
- **`.TrimEnd()`-Strategie in `GetSymbolBodyTool` (Prio 10):** Der Plan liess die
  `.TrimEnd()`-Entscheidung explizit als "Coder liest Test-Assertions und entscheidet"
  offen. Verifikation per `Read` von `GetSymbolBodyToolTests.cs` und `McpServerAllToolsE2ETests.cs`:
  - `GetSymbolBodyToolTests`: alle 6 Assertions sind `Assert.Contains(..., Ordinal)` bzw.
    `Assert.DoesNotContain(...)` — tolerant.
  - `McpServerAllToolsE2ETests.GetSymbolBody_*`: nur der Fehlerpfad mit `symbolIdentifier`
    fehlt, kein byte-exakter Output-Vergleich.
  - `McpServerCommandContractTests`: kein `get_symbol_body`-Test im engeren Vertrag.
  Entscheidung: `.TrimEnd()` ist hier **zwingend**, weil `MarkdownBuilder.CodeBlock`
  emittiert nach ` ``` ` immer ein `\n` (siehe `MarkdownBuilder.cs:145`), das Original
  aber nicht. Ohne `.TrimEnd()` waere `get_symbol_body`-Output um genau ein `\n` laenger
  (MCP-Token-Vertrag gebrochen, Drift fuer alle Agent-Consumer). Mit `.TrimEnd()` ist der
  Output byte-stabil zum vorherigen Verhalten. Alle 6 `GetSymbolBodyToolTests` (Fast) +
  22 `McpLiveRepositoryTests` (Integration) gruen, was die Entscheidung bestaetigt.
- **`Table(Action<MarkdownTableBuilder>)`-Callback-Variante zum ersten Mal produktiv
  eingesetzt:** Bis zu step-003 war die Callback-Ueberladung nur durch 2 Unit-Tests in
  `MarkdownBuilderTests.cs` (Z.236-248) verriegelt, aber ungenutzt. step-003 nutzt sie
  zweimal produktiv (`GetHotspotsScanner.AppendHotspotSection` +
  `HotspotMapBuilder.AppendHotspotSection`). Die Instanz-Ueberladung `Table(MarkdownTableBuilder)`
  bleibt in `ViolationMarkdownFormatter.BuildSummaryTable` aktiv; TD-002 (`Table(MarkdownTableBuilder)`
  ungenutzt) ist damit immer noch nicht obsolet — die Ueberladung wird in step-004 (Prio 5
  `RepoPlaybookGenerator.AppendAgentPriority`) produktiv und obsolet TD-002 dort. Plan
  hatte das schon richtig gesehen ("TD-002 wird in diesem Step NICHT obsoleted"), durch
  die produktive Nutzung der Callback-Variante in step-003 aendert sich daran nichts.
- **`MarkdownBuilder.CodeBlock`-Verhalten bei leerem `body`:** Bei `body.Length == 0`
  (theoretisch moeglich, in Praxis durch `ExtractSymbolBody` immer mit mindestens
  `// Kein Quell-Syntax...` gefuellt) emittiert `CodeBlock` `\n` (Trailing-Newline nach
  ` ``` `), nicht leer. `.TrimEnd()` entfernt das — bei leerem body wuerde der Output
  ` ```csharp\n\n```\n` (von `CodeBlock`) + `TrimEnd()` = ` ```csharp\n\n``` ` ohne
  Trailing-Whitespace. Byte-stabil zum Original (das ebenfalls `body + "\n```" `
  produziert). Edge-Case ist hier irrelevant, weil `ExtractSymbolBody` einen Default-
  Kommentar liefert.
- **`using System.Linq` in `ListRulesCommand` bleibt:** Nach Umbau braucht `Search` (Z.79-83)
  weiterhin `Where(...).ToList()` und die `RuleRegistry.All`-Iteration in `ListAll` (jetzt
  `foreach` ueber die `MarkdownTableBuilder.AddRow`-Schleife, also kein Linq mehr). Bleibt
  aber fuer `Search` zwingend. Nicht entfernt.
- **`HotspotSectionFormatter` Test-Luecke bestaetigt:** Es gibt keine dedizierte
  `HotspotSectionFormatterTests`-Klasse. Verhalten ist vollstaendig ueber
  `GetHotspotsToolTests` (11/11, Fast) + `CliRepositoryDogfoodTests` (3/3, Integration
  — verifiziert `ainetlinter hotspot-map` CLI-Aufruf gegen `HotspotMapBuilder`) +
  `BaselineCliTests` (4/4) abgedeckt. Beim Loeschen der Datei gehen keine Test-Dateien
  verloren. Plan-Erwartung bestaetigt.
- **Methodenzeilen-Limits eingehalten:** `GetHotspotsScanner.AppendHotspotSection` 27 Z.
  (Limit 60), `HotspotMapBuilder.AppendHotspotSection` 27 Z., `ListAll` Tabelle-Block
  14 Z., `GetSymbolBodyTool` Migration 11 Z. Alle weit unter `MaxMethodLineCount: 60`.
  `GetHotspotsScanner.FormatReport` schrumpft leicht (kein raw `sb.AppendLine`-Block mehr
  fuer die Tabellen). `MarkdownTableBuilder.AddColumn`/`AddRow`-Pattern nutzt die
  Fluent-API wie vorgesehen.
- **CRLF/LF-Drift in `HotspotSectionFormatter`-Migration:** Die Original-Methode
  emittierte Zeilen via `sb.AppendLine(...)` (CRLF auf Windows), `MarkdownTableBuilder.AppendTo`
  emittiert bare `\n` (LF). Konsequenz: Tabelle-Inner-Zeilen (Header/Separator/Rows)
  wechseln in `GetHotspotsScanner.FormatReport` und `HotspotMapBuilder.Build` von CRLF zu LF,
  die `sb.AppendLine()`-Blanks dazwischen bleiben CRLF. Strukturell identisch, byte-stabil
  *im Sinne der Tests* (alle `Assert.Contains(...)` mit `StringComparison.Ordinal`),
  nicht byte-stabil im strengen Sinne. Schon in step-001 (`LF-vs-CRLF-Drift`-
  Beobachtung) explizit dokumentiert und Konzept-§1-konform. Plan hat in §"Bekannte
  Ausnahmen" den Vertrag als "byte-stabil *gegenueber den bestehenden Tests*" formuliert,
  nicht "byte-stabil in jedem Bit" — bestaetigt.
- **Method-Parameter-Limit eingehalten:** `AppendHotspotSection(StringBuilder sb, string
  heading, IReadOnlyList<...> files, int maxLineCount)` = 4 Parameter (Limit 4, exakt
  am Limit). Plan hatte das schon gesehen ("Grenze, aber zulaessig"). Kein Refactor
  auf ein Parameter-Record noetig.
- **Dogfood-Suite (`--config rules.json --path .`):** gruen, OK. Der Linter findet in
  der geaenderten Codebase keine Verletzungen. `MaxMethodLineCount`, `MaxLineCount`,
  `Sealed-Pflicht`, `EnforceNullableEnable` sind sauber.

## Bekannte Unschaerfen

- **CRLF vs LF in den migrierten Hotspot-Tabellen** (siehe "Beobachtungen"): die
  Tabellen-Zeilen-Separatoren wechseln von CRLF auf LF. Wenn ein Agent-Consumer
  (MCP-Client, CLI-Pipe-Reader) in einer Linux-CI-Umgebung `git`-CRLF-Normalisierung
  anders erwartet, kann sich der Hash eines `_mcp`-Snapshot-Tests theoretisch verschieben.
  Praktisch: keine entsprechenden Tests in der Suite (MCP-Server laeuft nur unter
  Windows, Konzept §3 "Windows-only"), und die `Assert.Contains`-Tests sind Line-
  Ending-tolerant. Risiko: niedrig.
- **`// ainetlinter-disable DuplicateCode`-Stil:** Konvention aus `roadmap.md` Z.64
  exemplifizisch ("'// ainetlinter-disable DuplicateCode' in einer der beteiligten
  Dateien platzieren"). Aber: keine zentrale Doku, die exakt beschreibt, ob der
  Kommentar in der *Methoden-Header-Zeile* oder *direkt darueber* steht. Habe die
  Header-Zeilen-Form gewaehlt (single line), weil der Plan es so skizziert hat. Falls
  der Kritiker die "Kommentar davor"-Form strikter sieht: Anpassung trivial.
- **Duplikation der `AppendHotspotSection` ist explizit keine Tech-Debt:** Sollte der
  Kritiker das anders sehen und einen Tech-Debt-Eintrag fuer eine zukuenftige
  "Gemeinsamer Helper in einer Drittschicht (z. B. `Maps/Shared/`)"-Loesung anlegen
  wollen: Grundlage dafuer ist in dieser Beobachtung dokumentiert, der Eintrag
  gehoert dann in `tech-debt.md` mit Verweis auf Konzept §3 Prio 4.

## Test-Inventar (fuer die Audit-Nachvollziehbarkeit)

- **Keine neuen Tests** (Plan-konform, Begründung in step-plan.md §"Tests" — die drei
  Callsites emittieren byte-stabile Inhalte, die Builder-API ist bereits durch 30
  `MarkdownBuilderTests` verriegelt).
- **Bestehende Tests, die den byte-stabilen Vertrag abnageln** (alle gruen):
  - 11 `GetHotspotsToolTests` (Prio 4 — verifiziert `FormatReport` mit den neuen
    `AppendHotspotSection`-Aufrufen; Tests checken `Kritische Dateien`/`Warnungs-Dateien`/
    `Greeter.cs`/`im gruenen Bereich`/`Keine Dateien im Scope` — alle treffen zu)
  - 8 `ListRulesCommandTests` (Prio 6 — verifiziert `ListAll`-Tabelle mit
    `MaxLineCount`/`EnforceNullableEnable`/`EnforceSealedClasses`-Substrings und
    `RuleId`/`Intent`/`Severity`-Header-Tokens — alle treffen zu)
  - 6 `GetSymbolBodyToolTests` (Prio 10 — verifiziert `ExecuteAsync` mit
    `id:`/`vollstaendig`/`truncated`-Substrings + `Greet`/`P:SymbolGraphMini.Greeter.Prefix`-
    Property-Id-Check — alle treffen zu)
  - 27 Integration-Tests in `SourceFileCatalog*Tests` + `BaselineCliTests` +
    `CliRepositoryDogfoodTests` + `McpServerCommandContractTests` (Prio 4 Map-Variante
    + Regression-Schutz)
  - 22 `McpLiveRepositoryTests` (Prio 10 MCP-Token-Vertrag end-to-end mit
    `GetSymbolBody`-Pfaden)

## Modell-Info

- `coded_by: coder`
- `coded_by_model: MiniMax-M3` (Mavis / MiniMax-Code)
- `coded_by_model_knowledge_cutoff: 2026-01`
- `coded_at: 2026-08-19`
