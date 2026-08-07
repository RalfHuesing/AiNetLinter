---
status: done
type: step-result
task: flaky-and-test-performance
step: 014
epic: EPIC-02
step_type: batch
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T16:37:00+02:00
code_commit_hash: c46d8399b31e6e02e94a938a92b25ba1b7b82403
status_after: done
blocker_category: n/a
---

# Result Step 014: Category-Traits für Rest-EPIC-02 (Mcp/-Root + Baseline/ + Commands/-Teil + Cli/)

## Zusammenfassung

Alle 20 im Plan spezifizierten Testklassen in `Mcp/`-Root, `Baseline/`,
`Commands/` (ohne `McpServerCommandTests`) und `Cli/` haben jetzt
Klassen-Ebene `[Trait("Category", ...)]` (15 Unit, 5 Integration),
exakt wie im Plan pro Item vorgegeben (Insert-Variante, Zeile,
Kategorie). Alle vier Insert-Varianten (Standard, XML-Doc,
`[Collection(...)]`, `// @covers`) sowie der byte-genaue LF-only-Insert
per Python für `OverviewResourceRegistrationTests.cs` wurden 1:1 nach
Plan umgesetzt. `McpServerCommandTests.cs` bewusst nicht angefasst.

## Geänderte Dateien

- item-01: `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` — `[Trait("Category", "Unit")]` vor Klasse (Standard-Insert)
- item-02: `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs` — Trait zwischen `</summary>` und Klasse (XML-Doc-Variante), Unit
- item-03: `src/AiNetLinter.Tests/Mcp/McpToolResultsTests.cs` — Standard-Insert, Unit
- item-04: `src/AiNetLinter.Tests/Mcp/OverviewResourceRegistrationTests.cs` — XML-Doc-Variante, Unit, byte-genauer Python-Insert (LF-only erhalten)
- item-05: `src/AiNetLinter.Tests/Mcp/SymbolGraphToolRegistrationsTests.cs` — XML-Doc-Variante, Unit
- item-06: `src/AiNetLinter.Tests/Baseline/BaselineComparerTests.cs` — Standard-Insert, Unit
- item-07: `src/AiNetLinter.Tests/Baseline/BaselineReaderWriterTests.cs` — Standard-Insert, Unit
- item-08: `src/AiNetLinter.Tests/Baseline/BaselineViolationFilterTests.cs` — Standard-Insert, Unit
- item-09: `src/AiNetLinter.Tests/Baseline/FileChecksumCalculatorTests.cs` — Standard-Insert, Unit
- item-10: `src/AiNetLinter.Tests/Baseline/FileSystemExclusionHelpersTests.cs` — Standard-Insert, Unit
- item-11: `src/AiNetLinter.Tests/Baseline/SourceFileCatalogBlazorPartialTests.cs` — XML-Doc-Variante, Unit
- item-12: `src/AiNetLinter.Tests/Baseline/SourceFileCatalogTests.cs` — Standard-Insert, Unit
- item-13: `src/AiNetLinter.Tests/Baseline/WebBaselineTests.cs` — Standard-Insert, Integration
- item-14: `src/AiNetLinter.Tests/Commands/ListRulesCommandTests.cs` — XML-Doc-Variante, Unit
- item-15: `src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs` — XML-Doc-Variante, Integration
- item-16: `src/AiNetLinter.Tests/Commands/McpServerCommandFindReferencesTests.cs` — `[Collection(...)]`-Variante, Integration
- item-17: `src/AiNetLinter.Tests/Commands/McpServerCommandFindSymbolTests.cs` — `[Collection(...)]`-Variante, Integration
- item-18: `src/AiNetLinter.Tests/Commands/McpServerCommandGetImpactTests.cs` — XML-Doc + `[Collection(...)]` kombiniert, Integration
- item-19: `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsCliTests.cs` — `// @covers`-Variante, Unit (Namens-Fehlschluss dokumentiert)
- item-20: `src/AiNetLinter.Tests/Cli/IgnoreSuppressionsIntegrationTests.cs` — `// @covers`-Variante, Unit (Namens-Fehlschluss dokumentiert)

## Commit

- **Code-Commit-Hash:** `c46d8399b31e6e02e94a938a92b25ba1b7b82403`
- **Message:**
  ```
  test: EPIC-02 Rest-Batch Traits nachziehen [flaky-and-test-performance]

  Category-Traits fuer die 20 verbleibenden, homogen klassifizierbaren
  Testklassen in Mcp/-Root, Baseline/, Commands/ (ohne
  McpServerCommandTests) und Cli/ nachgezogen. 15 Unit + 5 Integration,
  Klassen-Ebene, reine Tag-Ergaenzung ohne Verhaltensaenderung.

  Refs: tasks/flaky-and-test-performance/step-014
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build                              → grün (0 Warnungen, 0 Fehler)
dotnet run -- --config rules.json --path . → OK (Self-Lint)
dotnet test --filter "Category=Unit"       → grün (1184 Tests, 0 Fehler)
dotnet test --filter "Category=Integration" → grün (121 Tests, 0 Fehler; bekannter EPIC-06-Flaky trat in diesem Lauf nicht auf)
dotnet test (Voll)                         → grün (1325 Tests, 0 Fehler) — bereits im 1. Lauf grün, kein 2. Lauf nötig
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Alle 20 Items exakt an der im Plan
genannten Zeile mit der vorgegebenen Insert-Variante und Kategorie
umgesetzt; Filter-Delta traf exakt zu (Unit 1184, Integration 121,
Total 1325 — alle drei Zahlen stimmen exakt mit der Plan-Prognose
überein).

## Beobachtungen

- Der bekannte EPIC-06-Flaky (`McpServerCommandLoadingStateTests…`)
  ist im Integration-Filter-Lauf dieses Steps **nicht** aufgetreten —
  reiner Zufall der Thread-Pool-Terminierung, kein Hinweis, dass er
  behoben wäre; weiterhin außerhalb dieses Scopes.
- Beim `git add` der LF-only-Datei
  (`Mcp/OverviewResourceRegistrationTests.cs`) gibt Git die übliche
  Warnung „LF will be replaced by CRLF the next time Git touches it"
  aus (abhängig von `core.autocrlf`). Das betrifft nur einen
  *zukünftigen* Checkout-Vorgang, nicht den jetzt erstellten Commit —
  verifiziert direkt vor und nach `git add`/`git commit`: die Datei
  bleibt im Arbeitsverzeichnis byte-genau LF-only (CR=0, LF=97). Kein
  Handlungsbedarf in diesem Step, aber falls ein künftiger Step an
  dieser Datei erneut per Editor statt Python arbeitet, sollte die
  EOL-Prüfung wie hier explizit vor/nach erfolgen.
- `tasks/flaky-and-test-performance/task-state.md` war bereits vor
  Beginn dieses Steps im Arbeitsverzeichnis verändert (Status-Zeile
  `step-014` auf `in_progress`, vermutlich vom Orchestrator gesetzt) —
  wurde von mir nicht angefasst und ist nicht Teil dieses Commits.

## Bekannte Unschärfen

Keine über die im Plan bereits benannten (`McpServerCommandTests.cs`
als expliziter Folge-Step) hinaus. Alle 20 EOL-/BOM-/Fact-Zählungen
wurden vor und nach den Edits verifiziert (siehe EOL-Tabelle unten).

## EOL-Tabelle (20 Dateien, vor/nach)

| Datei | EOL-Typ | CR/LF vorher | CR/LF nachher | BOM |
|---|---|---|---|---|
| Mcp/McpCodeGraphServerTests.cs | CRLF | 133/133 | 134/134 | nein |
| Mcp/McpServerOptionsFactoryTests.cs | CRLF | 29/29 | 30/30 | nein |
| Mcp/McpToolResultsTests.cs | CRLF | 51/51 | 52/52 | nein |
| Mcp/OverviewResourceRegistrationTests.cs | LF-only | 0/96 | 0/97 | nein |
| Mcp/SymbolGraphToolRegistrationsTests.cs | CRLF | 32/32 | 33/33 | nein |
| Baseline/BaselineComparerTests.cs | CRLF | 68/68 | 69/69 | nein |
| Baseline/BaselineReaderWriterTests.cs | CRLF | 62/62 | 63/63 | nein |
| Baseline/BaselineViolationFilterTests.cs | CRLF | 48/48 | 49/49 | nein |
| Baseline/FileChecksumCalculatorTests.cs | CRLF | 28/28 | 29/29 | nein |
| Baseline/FileSystemExclusionHelpersTests.cs | CRLF | 82/82 | 83/83 | nein |
| Baseline/SourceFileCatalogBlazorPartialTests.cs | CRLF | 70/70 | 71/71 | nein |
| Baseline/SourceFileCatalogTests.cs | CRLF | 75/75 | 76/76 | nein |
| Baseline/WebBaselineTests.cs | CRLF | 114/114 | 115/115 | nein |
| Commands/ListRulesCommandTests.cs | CRLF | 112/112 | 113/113 | nein |
| Commands/McpServerCommandErrorHandlingTests.cs | CRLF | 137/137 | 138/138 | nein |
| Commands/McpServerCommandFindReferencesTests.cs | CRLF | 29/29 | 30/30 | nein |
| Commands/McpServerCommandFindSymbolTests.cs | CRLF | 29/29 | 30/30 | nein |
| Commands/McpServerCommandGetImpactTests.cs | CRLF | 49/49 | 50/50 | nein |
| Cli/IgnoreSuppressionsCliTests.cs | CRLF | 92/92 | 93/93 | nein |
| Cli/IgnoreSuppressionsIntegrationTests.cs | CRLF | 60/60 | 61/61 | nein |

Alle 19 CRLF-Dateien: CR==LF vor und nach, jeweils +1 Zeile, Trailing-NL
erhalten (`endswith \r\n`). 1 LF-only-Datei: CR=0 konstant, LF+1,
Trailing-NL erhalten (`endswith \n`). 0/20 mit BOM, vorher wie nachher.

## Filter-Delta (verifiziert)

Unit 1130 → 1184 (+54), Integration 113 → 121 (+8), Total 1325 (±0) —
exakt wie im Plan prognostiziert.
