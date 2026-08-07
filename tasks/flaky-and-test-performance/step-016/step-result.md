---
status: done
type: step-result
task: flaky-and-test-performance
step: 016
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-07T19:40:00+02:00
code_commit_hash: 6dfd588
status_after: done
blocker_category: n/a
---

# Result Step 016: EPIC-03 Fixture-Sharing — SymbolGraphCatalogFixture + McpLiveRepositoryFixture auf ICollectionFixture umstellen

## Zusammenfassung

`SymbolGraphCatalogFixture` (18 Testklassen) und `McpLiveRepositoryFixture` (2 Testklassen)
von `IClassFixture<T>` auf `ICollectionFixture<T>` umgestellt, je eine neue
`[CollectionDefinition]`-Markerklasse analog zum step-001-Muster. Das im Plan dokumentierte
Dispose-Risiko wurde behoben: 14 Testmethoden in `GetViolationsToolTests`, `SafeguardToolTests`,
`SearchPatternToolTests` disposten bislang den per `_fixture.Catalog` übergebenen, jetzt
geteilten Catalog — `using` vor der jeweiligen `McpCodeGraphServer`-Instanziierung entfernt.
`FindSymbolToolTests` behält `IClassFixture<BaselineCatalogFixture>` zusätzlich zum neuen
`[Collection]`-Attribut (kein Sharing-Hebel für diese Fixture). Alle 4 vom Plan geforderten
Doku-Kommentar-Anpassungen umgesetzt. Vorher-/Nachher-Messung durchgeführt und ehrlich
dokumentiert (siehe unten) — Ergebnis gemischt, kein durchgängiger Gewinn.

## Geänderte Dateien

- `src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogCollection.cs` (neu) — `[CollectionDefinition("SymbolGraphCatalog")]`-Marker.
- `src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryCollection.cs` (neu) — `[CollectionDefinition("McpLiveRepository")]`-Marker.
- `src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogFixture.cs` — XML-Doc an neue Verwendungsform angepasst.
- `src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs` — dito.
- 16 einfache Verwender (A1-A16 lt. Plan: `CallGraphTraversalTests`, `DiRegistrationHeuristicsTests`, `FindReferencesToolTests`, `FindSymbolScannerTests`, `GetFileSkeletonToolTests`, `GetHotspotsToolTests`, `GetImpactToolTests`, `GetIndexScopeToolTests`, `GetServerHealthToolTests`, `GetSymbolBodyToolTests`, `GetTypeHierarchyToolTests`, `GetViolationsToolTests`, `SafeguardScannerTests`, `SafeguardToolTests`, `SearchPatternToolTests`, `SkeletonStableIdTests`) — `IClassFixture<SymbolGraphCatalogFixture>` durch `[Collection("SymbolGraphCatalog")]` ersetzt.
- `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` — dualer Fall: `SymbolGraphCatalogFixture`-Anteil auf `[Collection("SymbolGraphCatalog")]` umgestellt, `IClassFixture<BaselineCatalogFixture>` bleibt erhalten.
- `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs` — dito (enthält den EPIC-06-Flaky-Test, siehe Beobachtungen).
- `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs` + `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs` — `IClassFixture<McpLiveRepositoryFixture>` durch `[Collection("McpLiveRepository")]` ersetzt; in `McpLiveRepositoryTests.cs` zusätzlich 2 Doc-Kommentare ("pro Testklasse" → "pro Collection", Z. 16 und Z. 155) angepasst.
- `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs`, `SafeguardToolTests.cs`, `SearchPatternToolTests.cs` — je 4/4/6 `using`-Entfernungen vor `var state = new McpCodeGraphServer(...(_fixture.Catalog)...)` (Dispose-Fix); lokale `using var state = ...(catalog)`-Instanzen (eigener, nicht geteilter Catalog aus `CompileErrorMiniFixtureWorkspace`/`ThrowingTextLoader`-Fällen) bewusst unverändert gelassen.

## Commit

- **Code-Commit-Hash:** `6dfd588`
- **Message:**
  ```
  refactor: Fixtures SymbolGraphCatalog/McpLiveRepository teilen [flaky-and-test-performance]

  SymbolGraphCatalogFixture (18 Testklassen) und McpLiveRepositoryFixture
  (2 Testklassen) von IClassFixture<T> auf ICollectionFixture<T> mit
  [CollectionDefinition] umgestellt, analog zum step-001-Muster
  (SymbolGraphMcpCollection). Zusaetzlich das dabei identifizierte
  Dispose-Risiko behoben: [...]

  Refs: tasks/flaky-and-test-performance/step-016
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                                                                → grün, 0 Warnungen, 0 Fehler
dotnet test --filter (Gruppe A, 150 Tests)   → grün, 3/3 Wiederholungen
dotnet test --filter (Gruppe B, 14 Tests)    → grün, 3/3 Wiederholungen
dotnet test --no-build (voller Lauf, 1325 Tests) → grün, 3/3 aufeinanderfolgende Wiederholungen (Isolationscheck-Pflicht erfüllt)
dotnet run --project src/AiNetLinter -- --config rules.json --path . → OK
```

### Mess-Zahlen (Mediane aus je 3 Läufen, `dotnet test --no-build`)

| Variante | Vorher (Median) | Nachher (Median) | Δ absolut | Δ relativ |
|----------|----------------:|-----------------:|----------:|----------:|
| Gruppe A isoliert (150 Tests, 18 Klassen)   | 12,31 s  | 22,82 s | +10,51 s | **+85,4 %** |
| Gruppe B isoliert (14 Tests, 2 Klassen)     | 13,63 s  | 13,44 s | −0,19 s  | −1,4 % |
| Voller Lauf (1325 Tests)                    | 102,69 s | 97,75 s | −4,94 s  | −4,8 % |

Rohzeiten (Sekunden):

- **Vorher Gruppe A:** 12,07 / 12,31 / 12,42
- **Nachher Gruppe A:** 23,01 / 22,80 / 22,82
- **Vorher Gruppe B:** 13,63 / 13,72 / 13,58
- **Nachher Gruppe B:** 12,86 / 13,44 / 13,54
- **Vorher voll:** 102,69 / 96,18 / 103,83
- **Nachher voll:** 98,47 / 96,09 / 97,75

Alle 6 Vorher- und alle 9 Nachher-Läufe (inkl. der 3 vollen Wiederholungsläufe) waren grün,
0 Fehler, 0 übersprungen — Gruppe A: 150/150, Gruppe B: 14/14, voll: 1325/1325.

## Abweichungen vom Plan

Keine inhaltliche Abweichung — alle „Konkrete Änderungen" (A0–A18, B0–B2, 14 Dispose-Fixes,
4 Doku-Kommentar-Anpassungen) 1:1 wie im Plan umgesetzt und gegen die tatsächlichen Zeilennummern
verifiziert (deckten sich exakt mit dem Plan, keine Verschiebung durch zwischenzeitliche Änderungen).

Eine kleine Selbstkorrektur während der Arbeit: Beim ersten Versuch, den `McpLiveRepositoryTests.cs`-
Doc-Kommentar „pro Testklasse" (Z. 154, im safeguard-Testkommentar) anzupassen, hatte ich versehentlich
einen anderen, thematisch ähnlichen Kommentarblock weiter unten (Z. 167-169, „Tool-Layer-Invariante")
getroffen und dort fälschlich „pro Collection" eingefügt. Beim Gegenlesen bemerkt, den Fehltreffer
zurückgesetzt und stattdessen den tatsächlich vom Plan gemeinten Kommentar (Z. 155 nach dem [Collection]-
Zeilen-Einschub) korrigiert — im finalen Commit ist nur die geplante Textstelle geändert.

## Beobachtungen

- **Gruppe-A-Sequenzialisierung ist deutlich teurer als beim step-001-Spike.** Der Plan hat dieses
  Risiko explizit als „bewusst höher als beim step-001-Spike" markiert (143 statt 22 Tests, in-process
  statt Subprozess) — die Messung bestätigt das drastisch: +85 % isoliert, gegenüber +5,3 % beim
  step-001-Spike. 18 vormals parallel laufende Testklassen laufen jetzt seriell innerhalb einer
  Collection; die eingesparte einmalige `MSBuildWorkspace`-Ladezeit (ca. 1 Load statt 18) kompensiert
  das nicht annähernd.
- **Der volle Lauf wurde trotzdem leicht schneller (−4,8 %), nicht langsamer wie beim step-001-Spike
  (+8,1 %).** Das ist auf den ersten Blick widersprüchlich zur Gruppe-A-Verschlechterung. Plausibelste
  Erklärung: im vollen Lauf mit `parallelizeTestCollections: true` überlappen andere Collections
  (Core/, Configuration/, Output/ etc.) die jetzt serialisierte `SymbolGraphCatalog`-Collection zeitlich
  — die Gesamtlaufzeit wird vom längsten Pfad durch alle Collections bestimmt, nicht von der Summe.
  Da vorher 18 einzelne `MSBuildWorkspace`-Loads CPU/Disk-Kontention mit anderen parallel laufenden
  Testklassen erzeugt haben könnten, entlastet die Reduktion auf einen Load den restlichen Lauf leicht.
  Das ist eine Hypothese, kein Beweis (kein Profiler-Lauf, siehe „Bekannte Unschärfen").
- **Gruppe B (McpLiveRepository) bestätigt den step-001-Befund für Subprozess-Sharing nicht eindeutig**
  — hier minimal schneller statt langsamer, aber die Differenz (−0,19 s auf ~13,5 s) liegt im Bereich
  der Lauf-zu-Lauf-Varianz, die auch bei den anderen Messreihen sichtbar ist. Kein belastbarer Trend.
- **Flaky-Test-Beobachtung (EPIC-06):** `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  lief in allen 3 vollen Wiederholungsläufen nach der Umstellung grün — keine sichtbare Änderung der
  Flaky-Rate innerhalb dieser begrenzten Stichprobe (3 Läufe sind zu wenig, um eine Rate belastbar zu
  schätzen; der Test war laut CodeMap schon vorher überwiegend grün). Kein EPIC-06-Fix vorgenommen,
  wie im Plan vorgesehen.
- **Dispose-Fix wirkte wie im Plan erwartet:** kein einziger der 1325+150+14 Testläufe (vorher/nachher,
  9+3 Wiederholungen gesamt) zeigte einen durch den geteilten Catalog verursachten Folgefehler. Die
  Analyse im Plan (`McpCodeGraphServer.Dispose()` nur `_catalog?.Dispose()`, kein weiterer State) hat
  sich empirisch bestätigt.
- **Kein Anlass, EPIC-03 als „gescheitert" zu werten oder zurückzurollen** — analog zur Nutzer-Vorgabe
  im Plan („Bekannte Ausnahmen"): das gemischte Ergebnis (Gruppe A schlechter, Gruppe B neutral, voll
  leicht besser) ist eine ehrliche Beobachtung, kein Blocker. Aus reiner Performance-Sicht wäre die
  Umstellung von Gruppe A für sich genommen nicht zu empfehlen (siehe „Bekannte Unschärfen" für eine
  mögliche Folge-Überlegung); das ist aber eine Entscheidung für den Nutzer/Planer, nicht für den Coder.

## Bekannte Unschärfen

- **Nur 3 Läufe je Messreihe** (wie beim step-001-Spike) — die Lauf-zu-Lauf-Varianz insbesondere beim
  vollen Lauf (96–104 s Streubreite) ist relativ zur gemessenen Differenz (~5 s) nicht klein. Eine
  belastbarere Aussage bräuchte eine größere Stichprobe (z. B. 10 Läufe); für diesen Step reicht das
  Bild, um den Trend "gemischt, kein klarer Gewinn" zu dokumentieren, aber nicht um exakte Prozentwerte
  als stabil zu behaupten.
- **Die Hypothese zur "leichten Verbesserung im vollen Lauf trotz Gruppe-A-Verschlechterung" (Overlap
  mit parallelen Collections, reduzierte Disk/CPU-Kontention) ist nicht durch einen Profiler-Lauf
  verifiziert** — plausible Erklärung basierend auf der bekannten `parallelizeTestCollections`-Konfiguration,
  aber kein Beweis. Falls ein Folge-Step (z. B. EPIC-04 Fast-Path) hier tiefer graben will, wäre ein
  Timing-Log pro Collection ein sinnvoller nächster Schritt.
- **Ob Gruppe A in dieser Form (eine große Collection statt Hybrid-Split) langfristig sinnvoll ist,
  bleibt eine offene Frage für den Nutzer/Planer** — der Plan hat den Hybrid-Split bewusst verworfen
  („keine vergleichbar klare Trennlinie" bei 18 ähnlich intensiven Verwendern), aber die +85 %-Zahl bei
  isolierter Betrachtung ist deutlich höher als beim step-001-Spike und könnte ein Argument für eine
  spätere Revision sein, falls der volle-Lauf-Vorteil sich in weiteren Messungen nicht bestätigt.
