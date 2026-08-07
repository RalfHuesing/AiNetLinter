---
status: done (pending audit)
type: step-plan
task: flaky-and-test-performance
step: 016
corrects: null
title: "EPIC-03 Fixture-Sharing: SymbolGraphCatalogFixture (18×) + McpLiveRepositoryFixture (2×) auf ICollectionFixture umstellen"
epic: EPIC-03
estimated_risk: medium  # Strukturelles Refactoring über 20 Testklassen/157 Fact-Methoden; Sequenzialisierungsrisiko analog step-001-Spike (dort bereits negativ), zusätzlich ein konkretes Dispose-Risiko bei geteiltem Catalog (siehe unten) — beides mit Gegenmaßnahme im Plan adressiert, aber nicht bloß additiv/mechanisch wie EPIC-02.
step_type: single  # Ein zusammenhängendes Refactoring (zwei neue CollectionDefinitions + Umstellung ihrer jeweiligen Verwender), kein Bündel unabhängiger Mini-Befunde — analog step-001, das ebenfalls 7 Dateien als ein Step behandelt hat.
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-07T18:15:00+02:00
related_to: ["step-001"]
---

# Step 016: EPIC-03 Fixture-Sharing — SymbolGraphCatalogFixture + McpLiveRepositoryFixture auf ICollectionFixture umstellen

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-03` aus `roadmap.md` — Fixture-Sharing im großen Stil umsetzen, geleitet vom Spike aus EPIC-01. Der Spike (`step-001`) hat `SymbolGraphMcpFixture` (6 Klassen, Subprozess-basiert) bereits umgestellt und dabei **keinen** Performance-Gewinn gemessen (isoliert +5,3 %, voll +8,1 % langsamer). **Nutzer-Entscheidung (explizit, 2026-08-07):** EPIC-03 wird trotz dieses negativen Spike-Befunds umgesetzt — keine erneute Nachfrage, ob es sich lohnt.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 3, §"Muss-Haben" Punkt 3 ("Reduktion der ~60-80 unabhängigen Lade-/Subprozessvorgänge — mindestens für `SymbolGraphCatalogFixture` 18× und `SymbolGraphMcpFixture` 6×"), §"Wo im Projekt" (`IClassFixture<SymbolGraphCatalogFixture>` in 18 Testklassen; `IClassFixture<McpLiveRepositoryFixture>` in 2 Testklassen).

## Aktueller Projektzustand (JIT-Kontext)

**Wichtigster Befund dieser Planung — Korrektur einer bestehenden Fehleinschätzung:** `step-001/step-plan.md` und die `codemap.md` (Abschnitt „Test-Fixtures — im Plan-Scope") dokumentierten `SymbolGraphCatalogFixture` als „nur 1× verwendet" (`McpServerCommandLoadingStateTests`) und erklärten die `konzept.md`-Prognose „18×" für „auf den heutigen Stand nicht mehr anwendbar". Das war **falsch** — der step-001-Scan hat nur `Commands/` und `Mcp/`-Root durchsucht, nicht `Mcp/Tools/` (existierte damals schon, wurde aber nicht gegrept). Eigener, vollständiger Scan (`grep -rn "IClassFixture" src/AiNetLinter.Tests/ --include=*.cs`, projektweit) ergibt:

- **`SymbolGraphCatalogFixture` — tatsächlich 18× verwendet** (deckungsgleich mit der ursprünglichen `konzept.md`-Prognose):
  - `Mcp/Tools/`: `CallGraphTraversalTests` (3 Facts), `DiRegistrationHeuristicsTests` (5), `FindReferencesToolTests` (16), `FindSymbolScannerTests` (6), `FindSymbolToolTests` (13, **dual** mit `IClassFixture<BaselineCatalogFixture>`), `GetFileSkeletonToolTests` (5), `GetHotspotsToolTests` (8), `GetImpactToolTests` (12), `GetIndexScopeToolTests` (7), `GetServerHealthToolTests` (5), `GetSymbolBodyToolTests` (6), `GetTypeHierarchyToolTests` (12), `GetViolationsToolTests` (9), `SafeguardScannerTests` (17), `SafeguardToolTests` (6), `SearchPatternToolTests` (9) — 16 Klassen, 139 Facts.
  - `Maps/Skeleton/SkeletonStableIdTests.cs` (1 Fact).
  - `Commands/McpServerCommandLoadingStateTests.cs` (3 Facts) — **das ist die Klasse mit dem EPIC-06-Flaky-Test** (`LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`, Z. 113-150). Sie liest `_fixture.Catalog` nur einmal read-only (`release.SetResult(_fixture.Catalog)`, Z. 141) — strukturell ein valider Sharing-Kandidat, siehe „Bekannte Ausnahmen" unten für die Interaktion mit EPIC-06.
  - **Total: 18 Klassen, 143 `[Fact]`-Methoden.**
- **Mutationscheck (Code-Inspektion aller 18 Verwendungsstellen):**
  - `SourceFileCatalog` (`src/AiNetLinter/Baseline/SourceFileCatalog.cs:15-34`) ist strukturell immutable: `Solution`/`HasLoadingErrors` sind `{ get; }` ohne Setter, `WithUpdatedSolution(...)` liefert eine **neue** Instanz statt zu mutieren. Roslyns `Solution`-Typ selbst ist immutable (Standard-Pattern). Direkte Mutation über `_fixture.Catalog` ist von außen nicht möglich.
  - Alle Fälle mit lokal erzeugten `File.WriteAllText`/`Directory.CreateDirectory`-Aufrufen (`DiRegistrationHeuristicsTests`, `GetIndexScopeToolTests`, `GetServerHealthToolTests`, `GetViolationsToolTests`, `SafeguardScannerTests`, `SafeguardToolTests`, `SearchPatternToolTests`) verifiziert: Diese schreiben ausschließlich in **lokal instanziierte** Workspaces (`DiRegistrationMiniFixtureWorkspace`, `SymbolGraphMiniFixtureWorkspace`, `CompileErrorMiniFixtureWorkspace`, temp-Verzeichnisse via `Path.GetTempPath()`/GUID) — **nie** in `_fixture.Workspace.RootPath`. Einziger Lesezugriff auf `_fixture.Workspace.RootPath` ist ein `Assert.Contains(...)` in `GetServerHealthToolTests.cs:51` (read-only).
  - **Konkretes Dispose-Risiko gefunden (der eigentliche Befund dieser Planung):** 14 Testmethoden über 3 Klassen (`GetViolationsToolTests.cs` Z. 43/58/70/82, `SafeguardToolTests.cs` Z. 55/81/96/111, `SearchPatternToolTests.cs` Z. 39/53/70/98/139/153) instanziieren `using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)))`. `McpCodeGraphServer.Dispose()` (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:177-191`) ruft `_catalog?.Dispose()` auf — das disposed die **übergebene** `SourceFileCatalog`-Instanz, hier direkt `_fixture.Catalog` (keine Kopie). `SourceFileCatalog.Dispose()` disposed wiederum die zugrunde liegende `MSBuildWorkspace`. Heute (jede der 3 Klassen mit eigener `IClassFixture`-Instanz) ist das folgenlos — die 4/4/6 Tests je Klasse disposen wiederholt dieselbe klasseneigene Instanz, und Roslyns `Solution`-Snapshot bleibt für Lesezugriffe auch nach `Workspace.Dispose()` funktional (empirisch bewiesen: alle 1325 Tests laufen heute grün mit exakt diesem Muster). **Sobald die Instanz jedoch über 18 Klassen/143 Tests geteilt wird, disposed die erste dieser 14 Testmethoden, die in der Collection-Ausführungsreihenfolge läuft, die für alle 18 Klassen gemeinsame `MSBuildWorkspace` — alle danach laufenden Klassen arbeiten mit einer bereits disposed Workspace weiter.** Das ist wahrscheinlich weiterhin unschädlich (siehe Begründung oben), aber ein qualitativ neues Risiko, das im Scope dieses Steps aktiv beseitigt wird (siehe „Konkrete Änderungen", Gruppe A, Punkt 3) statt nur toleriert.
- **`McpLiveRepositoryFixture` — 2× verwendet** (`McpDocumentationSmokeTests`, 4 Facts; `McpLiveRepositoryTests`, 10 Facts — 14 Facts total), beide ausschließlich lesend über `_fixture.Client.CallTool*Async(...)`. Startet einen echten Subprozess gegen `AiNetLinter.slnx` (`src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs:20-25`). Kein `_fixture.Client.DisposeAsync()`-Aufruf in den Tests selbst (nur in der Fixture eigenem `DisposeAsync()`) — kein Dispose-Risiko wie bei `SymbolGraphCatalogFixture`.
- **`BaselineMcpFixture`** (1×, `McpServerCommandTests`) und **`BaselineCatalogFixture`** (1×, `FindSymbolToolTests`, dort **dual** mit `SymbolGraphCatalogFixture`) — kein Sharing-Hebel, bleiben `IClassFixture`, unverändert (deckt sich mit der step-001-Entscheidung für `BaselineMcpFixture`).
- **`SymbolGraphMcpFixture`** (bereits `[Collection("SymbolGraphMcp")]` seit step-001, 6 Klassen) — nicht Teil dieses Steps, bleibt wie vom Spike hinterlassen (negativer Befund dokumentiert, Nutzer hat Beibehaltung/EPIC-03-Fortsetzung trotzdem angeordnet).
- **`xunit.runner.json`**: `parallelizeTestCollections: true` unverändert — muss erhalten bleiben (Rules-Ref unten).
- **Kein `tech-debt.md`-Eintrag mit `auto_fixable: ja`** berührt `Mcp/`, `Mcp/Tools/`, `Maps/Skeleton/` oder `Commands/McpServerCommandLoadingStateTests.cs` (TD-001 ist explizit „NICHT UMSETZEN", TD-002 betrifft Commit-Disziplin, TD-003–006 betreffen EOL/Nullable/BOM in `Output/`/`Configuration/`/`Core/`) — kein opportunistisches Batch-Item anzuhängen.
- **EPIC-07 (tote `ConsoleTestCollection`-Infrastruktur)** — verifiziert: referenziert in `Fixtures/SubprocessConcurrencyGate.cs` (Kommentar) und `Cli/ProgramTests.cs`, `Commands/SyncAgentRulesCommandTests.cs`, `Commands/PlaybookCheckCommandTests.cs`, `Commands/DocsCommandTests.cs`, `Commands/AuditCommandTests.cs` sowie `ConsoleTestCollection.cs` selbst — **keine Überschneidung** mit den in diesem Step berührten Dateien. Nicht opportunistisch angehängt (Orchestrator-Vorgabe: nur falls dieselben Dateien betroffen sind).

## Intention

`SymbolGraphCatalogFixture` (18 Verwender, in-process `MSBuildWorkspace`-Load) und `McpLiveRepositoryFixture` (2 Verwender, echter Subprozess gegen `AiNetLinter.slnx`) werden von `IClassFixture<T>` (je eine eigene Instanz pro Testklasse) auf `ICollectionFixture<T>` (eine geteilte Instanz pro neu definierter Collection) umgestellt — analog zum in step-001 etablierten Muster (`[CollectionDefinition]`-Marker-Klasse + `[Collection("Name")]` an den Verwendern). Zusätzlich wird das oben dokumentierte Dispose-Risiko aktiv beseitigt, indem die 14 betroffenen Testmethoden den geteilten Catalog nicht mehr disposen. Nach diesem Step ist EPIC-03 inhaltlich abgeschlossen — verbleibende Einzelverwender (`BaselineMcpFixture`, `BaselineCatalogFixture`) haben keinen Sharing-Hebel und bleiben unverändert.

## Konkrete Änderungen

### Gruppe A — `SymbolGraphCatalogFixture` (18 Klassen)

#### Datei A0 (NEU): `src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogCollection.cs`

- **Was:** Neue Datei, analog zu `SymbolGraphMcpCollection.cs` aus step-001:
  - `#nullable enable`, Namespace `AiNetLinter.Tests.Fixtures`, `using Xunit;`.
  - `[CollectionDefinition("SymbolGraphCatalog")]` auf `public sealed class SymbolGraphCatalogCollection : ICollectionFixture<SymbolGraphCatalogFixture> { }`.
  - Kurzer *Why*-Kommentar (keine `step-`/`EPIC-`-Verweise): geteilte `MSBuildWorkspace`-Instanz statt 18 unabhängiger Loads derselben Mini-Solution.
- **Warum:** xUnit-v3-Voraussetzung für `ICollectionFixture<T>`.

#### Dateien A1-A16 (Zeile mit `IClassFixture<SymbolGraphCatalogFixture>` ersetzen durch `[Collection("SymbolGraphCatalog")]`, Konstruktor unverändert)

| # | Datei | Zeile (Klassendeklaration) |
|---|---|---|
| A1 | `src/AiNetLinter.Tests/Mcp/Tools/CallGraphTraversalTests.cs` | 11 |
| A2 | `src/AiNetLinter.Tests/Mcp/Tools/DiRegistrationHeuristicsTests.cs` | 14 |
| A3 | `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` | 15 |
| A4 | `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolScannerTests.cs` | 12 |
| A5 | `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs` | 14 |
| A6 | `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` | 14 |
| A7 | `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` | 14 |
| A8 | `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` | 15 |
| A9 | `src/AiNetLinter.Tests/Mcp/Tools/GetServerHealthToolTests.cs` | 19 |
| A10 | `src/AiNetLinter.Tests/Mcp/Tools/GetSymbolBodyToolTests.cs` | 13 |
| A11 | `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` | 14 |
| A12 | `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` | 19 |
| A13 | `src/AiNetLinter.Tests/Mcp/Tools/SafeguardScannerTests.cs` | 27 |
| A14 | `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` | 30 |
| A15 | `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | 14 |
| A16 | `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonStableIdTests.cs` | 12 |

- **Was (je Datei):** `public sealed class <Name> : IClassFixture<SymbolGraphCatalogFixture>` → `[Collection("SymbolGraphCatalog")]` (auf eigener Zeile vor der Klassendeklaration) `public sealed class <Name>`. Konstruktor-Parameter (`SymbolGraphCatalogFixture fixture`) und Body unverändert — xUnit v3 injiziert die Collection-Fixture über denselben Konstruktor-Pfad.
- **Warum:** Sharing-Umstellung für die 16 einfachen Verwender.

#### Datei A17: `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs:13` — dualer Fixture-Fall

- **Was:** `public sealed class FindSymbolToolTests : IClassFixture<BaselineCatalogFixture>, IClassFixture<SymbolGraphCatalogFixture>` → `[Collection("SymbolGraphCatalog")]` (eigene Zeile) `public sealed class FindSymbolToolTests : IClassFixture<BaselineCatalogFixture>`. `BaselineCatalogFixture` bleibt `IClassFixture` (1× verwendet, kein Hebel — analog `McpServerCommandTests.cs` aus step-001, wo `BaselineMcpFixture` ebenso erhalten blieb).
- **Warum:** Gleiche Umstellung, nur mit einer zusätzlichen unveränderten Fixture in der Deklaration.

#### Datei A18: `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs:21`

- **Was:** `public sealed class McpServerCommandLoadingStateTests : IClassFixture<SymbolGraphCatalogFixture>` → `[Collection("SymbolGraphCatalog")]` `public sealed class McpServerCommandLoadingStateTests`. Konstruktor/Body unverändert.
- **Warum:** Vollständigkeit des Sharings — read-only-Verwendung von `_fixture.Catalog` (Z. 141) bestätigt. Siehe „Bekannte Ausnahmen" für die EPIC-06-Interaktion.

#### Dispose-Fix (Pflichtteil dieser Gruppe, kein optionales Aufräumen)

In den folgenden 14 Zeilen `using` vor `var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));` entfernen (→ `var state = ...` ohne `using`), damit keine der 18 Klassen mehr die geteilte `SymbolGraphCatalogFixture`-Instanz disposed:

- `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` — Zeilen 43, 58, 70, 82
- `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs` — Zeilen 55, 81, 96, 111
- `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` — Zeilen 39, 53, 70, 98, 139, 153

**Warum das sicher ist:** `McpCodeGraphServer.Dispose()` (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:177-191`) tut in diesen Testfällen ausschließlich `_catalog?.Dispose()` (kein Hintergrund-`_loadTask` vorhanden, da `Catalog` synchron über `McpCodeGraphServerOptionsFromParameters` übergeben wird) — es entfällt also keine andere Aufräumlogik. Genau dieses Muster (`var state = new McpCodeGraphServer(...(_fixture.Catalog)...)` **ohne** `using`) ist in den anderen 12 der 18 Klassen (z. B. `FindReferencesToolTests.cs`, `GetImpactToolTests.cs`, `GetTypeHierarchyToolTests.cs`) bereits der etablierte Standard für genau diesen Fixture-Zugriff — kein neues Muster, sondern Konsistenz-Herstellung.

#### Doku-Kommentar-Anpassungen (Pflichtteil, Konsistenz mit step-001-Präzedenzfall)

- `src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogFixture.cs:11+13` — „Laedt einmalig pro Testklasse ... Wird in Tool-Unit-Tests via `IClassFixture{SymbolGraphCatalogFixture}` verwendet." an die neue Verwendungsform (`[Collection("SymbolGraphCatalog")]`, geteilt pro Collection statt pro Testklasse) anpassen — analog zur step-001-Anpassung von `SymbolGraphMcpFixture.cs:13`.
- `src/AiNetLinter.Tests/Mcp/Tools/SafeguardToolTests.cs:22-23` — Doc-Kommentar „Pattern 1:1 von `GetViolationsToolTests`: `IClassFixture<SymbolGraphCatalogFixture>` ..." nennt die entfernte Nutzungsform explizit beim Namen; auf `[Collection("SymbolGraphCatalog")]` aktualisieren.

### Gruppe B — `McpLiveRepositoryFixture` (2 Klassen)

#### Datei B0 (NEU): `src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryCollection.cs`

- **Was:** Analog zu Datei A0: `[CollectionDefinition("McpLiveRepository")]` auf `public sealed class McpLiveRepositoryCollection : ICollectionFixture<McpLiveRepositoryFixture> { }`.
- **Warum:** xUnit-v3-Voraussetzung für die 2. Collection.

#### Datei B1: `src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs:17`

- **Was:** `public sealed class McpDocumentationSmokeTests : IClassFixture<McpLiveRepositoryFixture>` → `[Collection("McpLiveRepository")]` `public sealed class McpDocumentationSmokeTests`. Konstruktor/Body unverändert.
- **Warum:** Sharing-Umstellung, erster Verwender.

#### Datei B2: `src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs:19`

- **Was:** `public sealed class McpLiveRepositoryTests : IClassFixture<McpLiveRepositoryFixture>` → `[Collection("McpLiveRepository")]` `public sealed class McpLiveRepositoryTests`. Konstruktor/Body unverändert. Zusätzlich Doc-Kommentare Z. 16 ("... pro Testklasse.") und Z. 154 ("... pro Testklasse, startet einmal ...") auf "pro Collection" anpassen, um die in step-001 dokumentierte NITPICK (stale "pro Testklasse"-Formulierung in `McpServerAllToolsE2ETests.cs`) hier nicht zu wiederholen.
- **Warum:** Sharing-Umstellung, zweiter und letzter Verwender.

#### Doku-Kommentar-Anpassung

- `src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs:12-13` — „Startet einmalig pro Testklasse den MCP-Server-Prozess ... Wird in Read-Only Integrationstests via `IClassFixture{McpLiveRepositoryFixture}` verwendet." an `[Collection("McpLiveRepository")]` anpassen.

### Mess- und Validierungs-Logik (im Coder-Schritt, kein Datei-Output)

Analog zu step-001, getrennt für beide Gruppen plus Gesamtlauf:

1. **Vorher-Messung** (vor allen Änderungen, je 3 Läufe, Median notieren):
   - Isoliert Gruppe A: `dotnet test --filter "FullyQualifiedName~Mcp.Tools|FullyQualifiedName~SkeletonStableId|FullyQualifiedName~McpServerCommandLoadingState" --no-build` (deckt alle 18 Klassen ab — leicht großzügiger Filter über den `Mcp.Tools`-Namespace, da alle 16 dortigen Klassen ohnehin Gruppe A sind).
   - Isoliert Gruppe B: `dotnet test --filter "FullyQualifiedName~McpDocumentationSmokeTests|FullyQualifiedName~McpLiveRepositoryTests" --no-build`.
   - Voller Lauf: `dotnet test --no-build`.
2. **Nachher-Messung:** exakt dieselben drei Messungen nach den Änderungen.
3. **Isolationscheck:** Grüner Lauf = kein Isolationsbruch. **Zusätzlich zum einmaligen Nachher-Lauf: 3 aufeinanderfolgende volle `dotnet test`-Läufe grün**, gezielt wegen des oben dokumentierten Dispose-Risikos (ein einmalig grüner Lauf beweist nicht, dass keine Ausführungsreihenfolge-Abhängigkeit übersehen wurde — insbesondere weil xUnit v3 die Reihenfolge der Klassen innerhalb einer Collection nicht offiziell garantiert).
4. **Self-Lint:** `dotnet run --project src/AiNetLinter -- --config rules.json --path .` (TD-001-konformer Ersatz für das nicht existierende `--self-lint`, wie in step-001 etabliert) — muss `OK` bleiben.

## Tests

- **Keine neuen Tests** — der Umbau wird durch die bereits existierenden 143 (Gruppe A) + 14 (Gruppe B) = 157 Tests der umgestellten Klassen validiert, plus den vollen 1325-Test-Lauf als Isolationscheck.
- Besonderes Augenmerk (nicht nur "grün", sondern gezielt prüfen):
  - Die 14 Tests in `GetViolationsToolTests`, `SafeguardToolTests`, `SearchPatternToolTests` (Dispose-Fix) — laufen unabhängig von ihrer Position innerhalb der Collection korrekt.
  - `McpServerCommandLoadingStateTests.LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (der EPIC-06-Flaky-Test) — läuft im Rahmen der 3 vollen Wiederholungsläufe grün; **kein EPIC-06-Fix in diesem Step**, nur Beobachtung, ob sich die Flaky-Rate durch die geänderte Nebenläufigkeit sichtbar verändert (Dokumentation in `step-result.md`, keine Handlungspflicht).

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt: 2 neue Dateien (A0, B0), 18 Klassendeklarationen umgestellt (A1-A18), 14 `using`-Entfernungen (Dispose-Fix), 4 Doku-Kommentar-Anpassungen (SymbolGraphCatalogFixture.cs, SafeguardToolTests.cs, McpLiveRepositoryFixture.cs, McpLiveRepositoryTests.cs).
- [ ] `dotnet build` grün, 0 Warnungen (Zero-Warning-Direktive).
- [ ] `dotnet test` (voller Lauf): **3 aufeinanderfolgende grüne Läufe** (nicht nur 1) — wegen des dokumentierten Dispose-Risikos.
- [ ] Vorher-/Nachher-Messungen für Gruppe A (isoliert), Gruppe B (isoliert) und Gesamtlauf dokumentiert (je 3 Läufe, Median) — Ergebnis ehrlich dokumentieren, auch falls erneut kein Performance-Gewinn (wie beim step-001-Spike) — das ist explizit erwartbar und kein Abbruchkriterium.
- [ ] `dotnet run --project src/AiNetLinter -- --config rules.json --path .` → `OK`.
- [ ] `step-016/step-result.md` geschrieben mit Mess-Zahlen, Beobachtungen zur Isolation (insbesondere zum Dispose-Fix und zur Collection-internen Ausführungsreihenfolge), Flaky-Test-Beobachtung (siehe „Tests").
- [ ] CodeMap (`codemap.md`) um die neuen Dateien/Umstellungen ergänzt (Coder-Pflicht vor Doku-Commit, siehe `spec.md` §5).
- [ ] `status` in `step-plan.md` von `open` → `in_progress` → `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 ("Testsuite-Parallelität bewahren") — zentral: `[Collection("...")]` serialisiert nur die Tests *innerhalb* dieser einen Collection (hier: 143 bzw. 14 Tests), andere Collections laufen weiterhin parallel via `parallelizeTestCollections: true`. Muss im `step-result.md` wie bei step-001 explizit als Trade-off dokumentiert werden (Einsparung an Lade-Vorgängen vs. Sequenzialisierungskosten).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (MCP & Dogfood Testing) — bestätigt `McpLiveRepositoryFixture`/`McpTestClient` als die vorgesehene Infrastruktur für Live-Verifikation; die Umstellung ändert nur die Instanziierungsform, nicht den Testmechanismus.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Sparsame Kommentare) — neue `[CollectionDefinition]`-Klassen (A0, B0) nur mit knappem *Why*-Kommentar, keine `step-016`/`EPIC-03`-Verweise im Code.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Symptom-Fixing verboten) — falls einer der 3 vollen Wiederholungsläufe rot wird (z. B. durch das Dispose-Risiko oder einen bislang unentdeckten Isolationsbruch): **nicht** den betroffenen Test abschwächen oder überspringen. Ursache ermitteln und dokumentieren; ggf. Rückroll auf `IClassFixture` für die betroffene(n) Klasse(n) im selben Step, mit Begründung im `step-result.md` (analog zur "Bekannte Ausnahmen"-Klausel aus step-001).
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Zero-Warning) — alle Änderungen warnungsfrei.

## Bekannte Ausnahmen

- **`BaselineMcpFixture` (1× verwendet) und `BaselineCatalogFixture` (1× verwendet, dual mit `SymbolGraphCatalogFixture` in `FindSymbolToolTests`) werden NICHT umgestellt.** Begründung: kein Sharing-Hebel (1 Instanz → 1 Instanz ändert nichts), analog zur step-001-Entscheidung.
- **`SymbolGraphMcpFixture` (bereits seit step-001 `[Collection("SymbolGraphMcp")]`, 6 Klassen) wird in diesem Step NICHT angefasst.** Der Spike-Befund (kein Performance-Gewinn) bleibt unverändert dokumentiert; Nutzer hat explizit angeordnet, EPIC-03 trotzdem für die *neuen* Kandidaten (`SymbolGraphCatalogFixture`, `McpLiveRepositoryFixture`) umzusetzen, nicht den Spike-Code zurückzurollen oder erneut zu bewerten.
- **`McpServerCommandLoadingStateTests` (EPIC-06-Flaky-Test-Klasse) wird in die `SymbolGraphCatalog`-Collection aufgenommen**, obwohl sie der Ziel-Test für einen noch offenen, separaten Epic (EPIC-06, struktureller Poll-Loop-Fix) ist. Begründung: strukturell ein valider Sharing-Kandidat (read-only-Zugriff auf `_fixture.Catalog` verifiziert), kein technischer Grund zum Ausschluss. Mögliche Wechselwirkung: Die geänderte Nebenläufigkeit innerhalb der Collection könnte die Flaky-Rate des Tests verändern (in beide Richtungen) — das wird im `step-result.md` beobachtet und dokumentiert (siehe „Tests"), ist aber **kein** EPIC-06-Fix und **kein** Blocker für diesen Step. EPIC-06 bleibt ein eigener, späterer Step mit dem strukturellen Fix (Event-/`TaskCompletionSource`-basiertes Warten statt Poll-Loop).
- **Erwartbares Ergebnis „kein Performance-Gewinn" ist explizit kein Abbruchkriterium.** Der Nutzer hat trotz des negativen step-001-Spike-Befunds angeordnet, EPIC-03 umzusetzen — eine erneute negative Messung ist ein gültiges, zu dokumentierendes Ergebnis, kein Grund, den Step als "issues" zu werten oder zurückzurollen.
- **Falls einer der 3 Wiederholungsläufe rot wird** (Dispose-Risiko oder unentdeckte Isolationsabhängigkeit): siehe Rules-Ref „Symptom-Fixing verboten" oben — Ursache dokumentieren, ggf. selektiver Rückroll der betroffenen Klasse(n) auf `IClassFixture` im selben Step (mit Begründung), kein stilles Weglassen der Wiederholungsläufe aus dem `step-result.md`.

## Code-Skizze (optional)

```csharp
// src/AiNetLinter.Tests/Fixtures/SymbolGraphCatalogCollection.cs
#nullable enable

using Xunit;

namespace AiNetLinter.Tests.Fixtures;

// Eine geteilte SymbolGraphCatalogFixture-Instanz pro Collection; reduziert 18
// unabhaengige MSBuildWorkspace-Loads derselben Mini-Solution auf einen.
[CollectionDefinition("SymbolGraphCatalog")]
public sealed class SymbolGraphCatalogCollection : ICollectionFixture<SymbolGraphCatalogFixture>
{
}
```

```csharp
// src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs (Dispose-Fix, vorher/nachher)
// vorher:
using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));
// nachher:
var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));
```

## Notes

- **Reihenfolge im Coder-Schritt:** Empfohlen A0 + B0 (neue Collection-Definitionen) zuerst anlegen und `dotnet build` prüfen, dann Gruppe A (18 Klassendeklarationen + 14 Dispose-Fixes + Doku-Kommentare) und Gruppe B (2 Klassendeklarationen + Doku-Kommentare) je in sich abschließen, dann Gesamtbuild/-test. Ein Commit für den gesamten Step ist ausreichend (analog step-001, step-011..015), da alle Änderungen fachlich zusammengehören (Nutzer-Vorgabe aus `nachfragen.md`: "ruhig mehrere Dateien/Klassen in einem Step umbauen, wenn sie fachlich zusammengehören (z. B. EPIC-03 Fixture-Sharing)").
- **Größenordnung des Sequenzialisierungsrisikos bewusst höher als beim step-001-Spike:** Der Spike sequenzialisierte 22 Tests (6 Klassen) und maß bereits eine Verschlechterung. Diese Gruppe-A-Umstellung sequenzialisiert 143 Tests (18 Klassen) — ein deutlich größerer Eingriff mit ungewissem, potenziell noch stärker negativem Ausgang, weil hier in-process `MSBuildWorkspace`-Loads statt Subprocess-Starts geteilt werden (anderes Kostenprofil: ob die geteilte `MSBuildWorkspace`-Instanz tatsächlich signifikant Zeit spart, ist nicht durch den Spike abgedeckt — der hat nur Subprozess-Fixtures gemessen). Das ist explizit Teil dessen, was in diesem Step gemessen und ehrlich dokumentiert werden soll, nicht vorab zu entscheiden.
- **Warum kein Hybrid-Split (mehrere kleinere Collections statt einer großen) für Gruppe A:** Die step-001-Empfehlung „Variante B" schlug für `SymbolGraphMcpFixture` einen Hybrid-Split vor (nur die intensiven Verwender bündeln, Rest bleibt `IClassFixture`). Für `SymbolGraphCatalogFixture` gibt es keine vergleichbar klare Trennlinie (fast alle 18 Klassen sind ähnlich intensive, gleichwertige Verwender ohne einen dominanten Ausreißer wie `McpServerCommandTests` mit 18 von 22 Tests) — ein Hybrid-Split wäre hier eine spekulative Zusatzkomplexität ohne klare Kandidaten-Trennung. Die Messung in diesem Step liefert die Datenbasis, falls ein Folge-Step (durch den Nutzer angestoßen) doch einen Hybrid-Split für sinnvoll hält.
- **`FindSymbolToolTests.cs` Sonderfall:** Mehrere Attribute (`[Collection(...)]` + `: IClassFixture<BaselineCatalogFixture>`) sind kombinierbar — bei versehentlicher Kombination von `[Collection(...)]` und `IClassFixture<SymbolGraphCatalogFixture>` auf derselben Klasse gibt xUnit v3 einen Kompilierfehler ("Fixture already declared"); beim Edit sorgfältig nur den `SymbolGraphCatalogFixture`-Teil der Interface-Liste entfernen, `BaselineCatalogFixture`-Teil erhalten.
- **Bestehendes Muster wiederverwendet, nicht dupliziert:** Die `[CollectionDefinition]`/`ICollectionFixture<T>`-Mechanik ist bereits aus step-001 (`SymbolGraphMcpCollection.cs`) bekannt — A0/B0 folgen exakt demselben Muster, keine neue Infrastruktur nötig.
