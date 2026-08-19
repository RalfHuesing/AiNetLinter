---
status: done
type: step-review
task: markdown-builder
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-19
verdict: issues
tech_debt_ids: [TD-001, TD-002]
---

# Review Step 001: MarkdownBuilder-Foundation + Bug-Fix-Callsites umstellen

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-<MMM>` angelegt (`corrects: step-001`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (oder Baselines beachtet)

## Befund

### Plan-Erfüllung

Alle fünf im Plan genannten Dateien umgesetzt (2 neu, 3 modifiziert, 1 Helper `FormatMemberRow` ersatzlos entfernt), `AppendViolationItem` Z.253–274 byte-identisch zur Vor-Step-Version, beide Commits tragen Suffix `[markdown-builder]` + `Refs:`-Trailer, `codeMap.md` ist aktualisiert (Prio 1–3 als „umgebaut", `FormatMemberRow` aus der Karte entfernt, `MarkdownBuilder`/`MarkdownBuilderTests` als „angelegt" markiert). Build-Output: 0 Warnings, 0 Fehler; FastTests `Category!=Stress` 1422/1422 grün; Stichproben der Integration-Tests bestätigt (3/3 `CliRepositoryDogfoodTests`, 4/4 `BaselineCliTests`, 2/2 `McpServerCommandGetImpactTests`, 1/1 `McpServerCommandStalenessTests`, 7/7 `SourceFileCatalog*Tests`).

### Rules-Konformität

`sealed` auf `MarkdownTableBuilder` und `MarkdownBuilder` (Z.17 + Z.90 in `MarkdownBuilder.cs`); `#nullable enable` am Anfang beider neuen Dateien; `MaxMethodLineCount` für alle Prod-Methoden ≤60 (längste: `MarkdownTableBuilder.AppendTo` Z.40–74 mit 33 Z., `GetViolationsScanner.AppendSection` Z.235–282 mit 47 Z., `ViolationMarkdownFormatter.BuildSummaryTable` Z.55–109 mit 54 Z.); `MaxMethodLineCount: 100` für Tests eingehalten; Testklasse `MarkdownBuilderTests` mit `[Trait("Category", "Unit")]` an der Klasse, **nicht** `sealed` (Test-Konvention); `MaxLineCount: 500` für `MarkdownBuilder.cs` (128 Z.) und `MarkdownBuilderTests.cs` (245 Z.) deutlich unterschritten. Keine `// step-001` / `// EPIC-01` / Task-IDs in Code-Kommentaren (verifiziert per `git show` der beiden Commits). Zero-Warning-Direktive eingehalten.

### Logische Korrektheit

24/24 `MarkdownBuilderTests` grün, 31/31 `ViolationMarkdownFormatterTests` grün, 16/16 `GetViolationsToolTests` grün. `EscapeCell`-Edge-Cases (Pipe, CRLF, Whitespace, Generics, Bold/Backticks, Mehrfach-Pipes) korrekt abgedeckt. `MarkdownBuilder.CodeBlock` mit und ohne Trailing-Newline korrekt getestet. `MarkdownTableBuilder.AddRow` ignoriert überschüssige Cells (Test bestätigt) und füllt fehlende mit `-` (Test bestätigt). Test-Sentinel-Workaround (`_ = typeof(MarkdownTableBuilder)` + `@covers`-Kommentare) erfüllt `EnableTestSentinel`-Regel sauber, ist aber ein kleiner Stilbruch (siehe MINOR #4). **MAJOR-Befund #1 (Logik):** In `GetViolationsScanner.AppendSection` wird die Tabellen-Format-Logik (Header, Separator, Row-Pipe-Konstruktion) inline als `string.Join` + `switch` nachgebaut, obwohl `MarkdownTableBuilder.AppendTo` exakt dieselbe Format-Semantik besitzt. Konsequenz: das Alignment-Format (`":---"`, `"---:"`, `":---:"`) ist jetzt in zwei Dateien definiert — eine zukünftige Erweiterung des `ColumnAlign`-Enum erfordert Updates an beiden Stellen, sonst Drift.

### Konzept-Treue (Ebene 4)

Drei EPIC-01-Callsites (Prio 1, 2, 3) sind auf den Builder umgestellt. `FormatMemberRow` ist ersatzlos entfernt. `AppendViolationItem` (Z.253–274) ist unverändert (eingerückter Code-Block bleibt raw `sb`-Code). `SkeletonMarkdownRenderer` wird respektiert (nicht angefasst). Die unescaped-`|`-Bugs in `v.Signature`/`v.Details` sind über `EscapeCell` automatisch behoben. `sealed`/`#nullable enable`/Test-Konventionen entsprechen Konzept §2. **MAJOR-Befund #1 (Konzept-Treue):** Das Konzept zeigt in §3 Prio 2 den Code `mb.Table(table)` am Ende der `foreach`-Schleife, was die zentrale API `MarkdownTableBuilder` als Tabellen-Senke nutzt. Die Implementierung weicht davon ab: `MarkdownTableBuilder` wird nur als `EscapeCell`-Utility genutzt, nicht als Tabellen-Senke. Das Konzept nennt als „Netto-Effekt" die Konsolidierung der Tabellen-Boilerplate in 10 Callsites — dieser Konsolidierungs-Gewinn wird in Prio 2 partiell zurückgenommen. **MAJOR-Befund #2 (Plan-Erfüllung + Konzept-Treue):** Plan-DoD fordert „byte-genau identische" Markdown-Output-Bytes für die drei migrierten Callsites. Für `BuildSummaryTable` ändert sich die Ausgabe für nicht-strukturelle Regeln in `hasStructural`-Tabellen um genau 1 Byte (`| 1 | 1 | 0 |  |` → `| 1 | 1 | 0 | - |`) — direkte Konsequenz der `EscapeCell`-Semantik „leer/whitespace → `-`" aus Plan §„Datei 1". Der Plan ist an dieser Stelle intern widersprüchlich (zwei Stellen fordern unterschiedliche Dinge für genau diesen Fall); der Coder hat zugunsten der API-Konsistenz (Konzept-weite Gültigkeit) gegen die DoD-Byte-Stabilität (lokale Test-Erwartung) entschieden. Architektonisch die richtige Wahl, aber DoD-technisch ein Bruch.

### Build-/Test-Status

```
dotnet build                                                                              → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress                            → grün (1422 Tests, 0 Fehler, 7 s)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~MarkdownBuilderTests     → grün (24/24)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ViolationMarkdownFormatterTests → grün (31/31)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetViolationsToolTests   → grün (16/16)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~CliRepositoryDogfoodTests   → grün (3/3, 30 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~BaselineCliTests          → grün (4/4, 13 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandGetImpactTests → grün (2/2, 16 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpServerCommandStalenessTests → grün (1/1, 10 s)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~SourceFileCatalog → grün (7/7, 24 s)
```

## Findings

1. `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs:248-263` — [MAJOR] [Konzept-Treue] [Logik] `AppendSection` reimplementiert die Markdown-Tabellen-Format-Logik (Header-Construction, Alignment-Separator, Row-Pipe-Join) inline als `string.Join` + `align switch` (Z.255, Z.256-261, Z.271), obwohl `MarkdownTableBuilder.AppendTo` (`src/AiNetLinter/Output/MarkdownBuilder.cs:40-74`) exakt dieselbe Format-Semantik besitzt. Die Alignment-Format-Strings (`":---"`, `"---:"`, `":---:"`) sind damit in zwei Dateien definiert. Konsequenz: jede zukünftige Erweiterung des `ColumnAlign`-Enum oder Anpassung der Tabellen-Format-Semantik (z. B. Trailing-Whitespace, alternative Separator-Syntax) erfordert synchrone Updates an beiden Stellen — Drift-Risiko, exakt das Anti-Pattern, das der Builder auflösen sollte. Der Coder begründet die Abweichung mit Reihenfolge-Treue (Zeile→Snippet→Zeile→Snippet), die der Plan DoD fordert; diese Anforderung steht aber im Konflikt mit dem Plan-Beispiel `mb.Table(table)`, das eine strikte Tabellensenke am Schleifenende zeigt. **Fix:** Entweder (a) `MarkdownTableBuilder` um eine API erweitern, die zeilenweises Emittieren erlaubt (z. B. `BuildLines()` → `IReadOnlyList<string>`, oder `AppendTo` mit Row-Callback), dann `AppendSection` so refaktorieren, dass `table` als Senke für Header+Separator genutzt wird und nur die Row-Erzeugung im `foreach` bleibt; oder (b) die Inline-Format-Strings als 1:1-Kopie von `MarkdownTableBuilder.AppendTo` belassen und mit einem Kommentar + Test verriegeln, der bei Format-Drift sofort fehlschlägt (Snapshot-Test der Inline-Format-Strings). Option (a) ist die saubere Lösung; Option (b) ist die schnelle.

2. `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs:82-86` (zusammen mit Test-Assertion `src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs:274`) — [MAJOR] [Plan-Erfüllung] `BuildSummaryTable` emittiert für nicht-strukturelle Regeln in `hasStructural`-Tabellen den Cell-Wert `-` statt ` ` (Plan-DoD fordert „byte-genau identische" Ausgabe für die drei migrierten Callsites, bricht hier um 1 Byte pro betroffener Row). Ursache: `MarkdownTableBuilder.EscapeCell("")` gibt per Vertrag (Plan §„Datei 1" + Konzept §2a) `-` zurück; die Original-Implementierung schrieb den leeren `string`-Wert unescaped in die Cell. Der Plan ist an dieser Stelle intern widersprüchlich (`EscapeCell`-Kontrakt vs. byte-stabile DoD); der Coder hat zugunsten der API-Konsistenz (Konzept-weite Gültigkeit von `EscapeCell`) gegen die DoD-Byte-Stabilität entschieden und die Test-Assertion von `| 1 | 1 | 0 |  |` auf `| 1 | 1 | 0 | - |` angepasst. Architektonisch die richtige Wahl — eine „EscapeCell(keepEmpty: true)"-Variante würde die API verwässern — aber DoD-technisch ein Bruch, der explizit dokumentiert gehört. **Fix:** Entweder (a) Plan-DoD-Sprache anpassen (im Folge-Step-Plan klarstellen: „byte-stabil für nicht-Cell-Content; Cell-Content folgt `EscapeCell`-Kontrakt"), sodass diese 1-Byte-Drift kein Findings-Auslöser mehr ist; oder (b) `BuildSummaryTable` vor `table.AddRow(...)` für `structMarker == string.Empty` einen alternativen Codepfad wählen lassen, der die Cell leer hält (würde den Builder-Kontrakt jedoch untergraben — nicht empfohlen). Empfehlung: (a).

## Sonstige Beobachtungen / MINOR / NITPICK

- `tasks/markdown-builder/step-001/step-result.md:77` (Abweichung #4) behauptet, in `ViolationMarkdownFormatterTests.cs` sei „eine Test-Kommentar-Zeile entfernt" worden, um die Datei unter 500 Z. zu halten. Der Diff (`git show fc603681 -- src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs`) zeigt **keine** entfernte Zeile — nur die Assertion-String-Änderung in Z.274 (1 Zeichen ` ` → `-`). Die Datei hat aktuell 402 Z., deutlich unter dem Limit. Reine Doku-Ungenauigkeit, kein Code-Defekt. Empfehlung: `step-result.md`-Eintrag korrigieren, falls das File für Audit-Zwecke weiter existiert (wird nach Step-Abschluss gelöscht — siehe Konzept §10.7).
- `src/AiNetLinter.FastTests/Output/MarkdownBuilderTests.cs:15` nutzt `private static readonly System.Type _ = typeof(MarkdownTableBuilder);` als TestSentinel-Coverage-Referenz. Funktioniert (sowohl `EnableTestSentinel` als auch die `@covers`-Kommentare sind aktiv), aber der Feldname `_` liest sich wie ein Discard. Idiomatischer wäre `_ = typeof(MarkdownTableBuilder);` als Statement im Rumpf eines bestehenden Tests oder in einem `[ModuleInitializer]`. Striktgenommen ist die Sentinel-Referenz außerdem redundant: die `EscapeCell_*`-Tests referenzieren `MarkdownTableBuilder` ohnehin via `MarkdownTableBuilder.EscapeCell(...)` (statischer Aufruf zählt für den Linter). Sie stört aber auch nicht.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — `ViolationMarkdownFormatter.cs:40` Top-Level-Header `output.Append($"# AiNetLinter - {violations.Count} violations\n")` ist `raw sb.Append` statt `MarkdownBuilder.Heading(1, ...)`; gleiches File, gleicher Bereich, gleicher Konzept-Konsolidierungs-Gewinn — wurde in EPIC-01 nur `BuildSummaryTable` umgebaut, nicht `Format`. Builder-Kandidat für EPIC-02+ oder eigenes Mini-Refactor. Priorität niedrig, `auto_fixable: ja` (rein mechanisch, byte-stabil).
- `TD-002` (siehe `tech-debt.md`) — `MarkdownBuilder.Table(MarkdownTableBuilder)`-Instanz-Überladung (`src/AiNetLinter/Output/MarkdownBuilder.cs:141`) ist implementiert und getestet, wird aber in keinem EPIC-01-Callsite produktiv genutzt. Konzept sieht sie für Prio 4 + 5 (EPIC-02) vor. Produktiver Einsatz in EPIC-02 obsoleted TD-002 automatisch. Priorität niedrig, `auto_fixable: nein` (Architektur-Ermessen).
