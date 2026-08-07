---
status: done
type: step-review
task: flaky-and-test-performance
step: 016
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-07T20:15:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 016: EPIC-03 Fixture-Sharing — SymbolGraphCatalogFixture + McpLiveRepositoryFixture auf ICollectionFixture umstellen

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (zitierte Abschnitte) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md`
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (3 volle Wiederholungsläufe zusätzlich zu den 3 des Coders)

## Befund

Alle vier Ebenen unauffällig. Besonders geprüft (Auftrag verlangte gründlichere Tiefe wegen `estimated_risk: medium`): das Dispose-Risiko und die Isolations-/Nebenläufigkeits-Behauptungen wurden nicht nur gelesen, sondern über den vollständigen Commit-Diff, die Produktionscode-Kette (`McpCodeGraphServer.Dispose()` → `SourceFileCatalog.Dispose()` → `MSBuildWorkspace.Dispose()`) und drei eigene volle Testläufe unabhängig nachvollzogen — Ergebnis deckungsgleich mit Plan und `step-result.md`.

### Plan-Erfüllung

Alle „Konkreten Änderungen" 1:1 im Diff (`git show 6dfd588`) verifiziert:
- A0/B0 (neue `[CollectionDefinition]`-Dateien) angelegt, folgen exakt dem step-001-Muster (`SymbolGraphMcpCollection.cs`), Why-Kommentar ohne `step-`/`EPIC-`-Referenz.
- A1–A18 (18 Klassendeklarationen Gruppe A) + B1–B2 (2 Klassendeklarationen Gruppe B) = 20 Umstellungen, alle im Diff nachgezählt und gegen die Datei-für-Datei-Tabelle im Plan abgeglichen — vollständig, keine übersehen.
- `FindSymbolToolTests.cs` (A17, dualer Fixture-Fall): `[Collection("SymbolGraphCatalog")]` korrekt ergänzt, `IClassFixture<BaselineCatalogFixture>` korrekt erhalten, `IClassFixture<SymbolGraphCatalogFixture>`-Anteil korrekt entfernt; Konstruktor bindet weiterhin beide Fixtures — kompiliert und läuft grün (kein „Fixture already declared"-Fehler, da unterschiedliche Fixture-Typen).
- **14 Dispose-Fixes vollständig verifiziert** (Detail siehe unten, eigener Abschnitt).
- 4 Doku-Kommentar-Anpassungen (`SymbolGraphCatalogFixture.cs`, `SafeguardToolTests.cs`, `McpLiveRepositoryFixture.cs`, `McpLiveRepositoryTests.cs` — 2 Stellen) im Diff vorhanden und korrekt (nur "pro Testklasse" → "pro Collection" bzw. `IClassFixture` → `[Collection(...)]`, keine inhaltliche Übertreibung).
- CodeMap aktualisiert: neue Fixture-Dateien, Dispose-Risiko-Historie und ein eigener „EPIC-03 — Abschluss"-Abschnitt mit korrekten Zahlen (18+2=20, gemischtes Messergebnis).
- Vorher-/Nachher-Messung für Gruppe A, Gruppe B und Gesamtlauf vorhanden, je 3 Läufe, Mediane dokumentiert.
- `status` in `step-plan.md`-Frontmatter korrekt `done (pending audit)`.

**Dispose-Fix — vollständige, unabhängige Verifikation (14/14):**
Im Commit-Diff gezählt: `GetViolationsToolTests.cs` 4 Hunks (Zeilen ehem. 43/58/70/82), `SafeguardToolTests.cs` 4 Hunks (ehem. 55/81/96/111), `SearchPatternToolTests.cs` 6 Hunks (ehem. 39/53/70/98/139/153) — Summe 14, exakt wie im Plan gefordert, keine Stelle übersehen. Zusätzlich per `grep` auf dem **aktuellen** Dateistand aller drei Dateien nachvollzogen: alle verbliebenen `using var state = new McpCodeGraphServer(...)`-Vorkommen instanziieren nachweislich einen **lokalen**, nicht-geteilten Catalog (`SourceFileCatalog.LoadAsync(fixture.RootPath)` mit eigener `CompileErrorMiniFixtureWorkspace`/`SymbolGraphMiniFixtureWorkspace`, oder `new SourceFileCatalog(solution, ...)` mit `AdhocWorkspace`) — nie `_fixture.Catalog`. Die Behauptung „lokale Catalog-Instanzen bewusst unverändert gelassen" aus `step-result.md` ist damit code-seitig bestätigt, nicht nur behauptet.
Produktionscode-Kette gegengelesen (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:177-191`, `src/AiNetLinter/Baseline/SourceFileCatalog.cs:143`): `Dispose()` ruft `_catalog?.Dispose()` → `_workspace?.Dispose()` (MSBuildWorkspace) — die im Plan dokumentierte Kausalkette für das Dispose-Risiko ist korrekt, kein zusätzlicher State, der beim Fix übersehen worden wäre.

### Rules-Konformität

- §4 „Testsuite-Parallelität bewahren": eingehalten — Sequenzialisierung ist auf die neuen Collections begrenzt, `parallelizeTestCollections: true` unverändert, Trade-off im `step-result.md` explizit als Beobachtung dokumentiert (nicht nur behauptet, sondern mit Zahlen belegt).
- §4 „MCP & Dogfood Testing": eingehalten — `McpLiveRepositoryFixture`/`McpTestClient` bleiben unverändert die Testinfrastruktur, nur die Instanziierungsform ändert sich.
- §5 „Sparsame Kommentare": eingehalten — beide neuen `[CollectionDefinition]`-Dateien tragen nur einen knappen Why-Kommentar, keine `step-`/`EPIC-`-Verweise im Code (verifiziert im Diff).
- §5 „Symptom-Fixing verboten": eingehalten — kein Test abgeschwächt, übersprungen oder mit `Skip` versehen; der Dispose-Fix behebt eine strukturelle Ursache, keine Test-Anpassung zur Grün-Erzwingung.
- §5 „Zero-Warning": eingehalten, selbst nachgeprüft (`dotnet build` → 0 Warnungen, 0 Fehler).

### Logische Korrektheit

Die Dispose-Fix-Logik ist korrekt und vollständig (siehe Detail-Abschnitt oben). Die `[Collection]`/`IClassFixture`-Kombination in `FindSymbolToolTests.cs` ist syntaktisch und semantisch korrekt (xUnit v3 injiziert beide Fixture-Typen über denselben Konstruktor). `SymbolGraphCatalogFixture`/`McpLiveRepositoryFixture` selbst implementieren `IAsyncLifetime` unverändert (nur Doc-Kommentare angepasst) — mit `ICollectionFixture<T>` ruft xUnit `InitializeAsync`/`DisposeAsync` genau einmal pro Collection-Lauf auf, das Sharing-Verhalten ist damit korrekt etabliert.

**Isolationscheck — eigene Gegenprobe (Kernauftrag dieses Reviews):** Da xUnit v3 die Ausführungsreihenfolge innerhalb einer Collection nicht garantiert, wurden zusätzlich zu den 3 Läufen des Coders **3 eigene, unabhängige volle `dotnet test`-Läufe** ausgeführt (nicht nur behauptete Wiederholung des Coder-Ergebnisses). Alle 3 grün, 1325/1325, 0 Fehler, 0 übersprungen — Laufzeiten 97s/102s/96s, konsistent mit den vom Coder berichteten Nachher-Werten (98,47/96,09/97,75s). Kein Isolationsbruch beobachtet, auch nicht bei potenziell anderer Klassen-Ausführungsreihenfolge als beim Coder-Lauf.

### Konzept-Treue (Ebene 4)

Deckt sich mit `konzept.md` §"Muss-Haben" Punkt 3 (Fixture-Sharing für `SymbolGraphCatalogFixture` 18×) — strukturell vollständig umgesetzt. Kein Non-Goal verletzt (sichtbares CLI-/MCP-Verhalten unverändert, reiner Testinfrastruktur-Umbau). Das gemischte Messergebnis (Gruppe A isoliert deutlich langsamer, Gesamtlauf leicht schneller) ist laut `step-plan.md` §"Bekannte Ausnahmen" explizit kein Abbruchkriterium (Nutzer-Vorgabe vom 2026-08-07) — die Dokumentation in `step-result.md` stellt das Ergebnis ehrlich dar, ohne Beschönigung: die +85 %-Verschlechterung wird nicht kleingeredet, die Hypothese zur Gesamtlauf-Verbesserung wird explizit als unbewiesen markiert ("Hypothese, kein Beweis"). Scope entspricht dem Plan, keine Erweiterung/Verkleinerung erkennbar.

### Build-/Test-Status

```
dotnet build                                                              → grün, 0 Warnungen, 0 Fehler
dotnet test --no-build (voller Lauf, Wiederholung 1/3)                    → grün, 1325 Tests, 0 Fehler, 97s
dotnet test --no-build (voller Lauf, Wiederholung 2/3)                    → grün, 1325 Tests, 0 Fehler, 102s
dotnet test --no-build (voller Lauf, Wiederholung 3/3)                    → grün, 1325 Tests, 0 Fehler, 96s
dotnet run --project src/AiNetLinter -- --config rules.json --path .     → OK
```

## Antworten auf die im Auftrag gestellten Fragen

- **Alle 14 Dispose-Fixes unabhängig verifiziert:** ja (Diff-Zählung 4+4+6=14 gegen aktuellen Dateistand gegengeprüft, inkl. Bestätigung, dass die verbleibenden `using`-Stellen ausschließlich lokale, nicht-geteilte Catalogs betreffen).
- **Alle 20 Klassenumstellungen unabhängig verifiziert:** ja (18 Gruppe A inkl. Sonderfall `FindSymbolToolTests` + 2 Gruppe B, alle im Diff gegen die Plan-Tabelle abgeglichen; Build + 3 eigene volle Testläufe grün bestätigen die syntaktische/semantische Korrektheit).
