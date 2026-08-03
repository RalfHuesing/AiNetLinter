---
status: done
type: step-plan
task: codegraph-mcp-finish
step: 001
title: "Testsuite-Performance: ConsoleTestCollection auf begründete Mitglieder eingrenzen (F.1)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: []
---

# Step 001: Testsuite-Performance — ConsoleTestCollection-Regression beheben (F.1)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-01` aus `roadmap.md` — Testsuite-Performance (Block F,
  Unterpunkte F.1-F.6). Dieser Step deckt **ausschließlich F.1** ab, den
  laut Konzept größten Laufzeit-Hebel — F.2-F.6 bleiben für spätere Steps
  desselben Epics offen (Epic ist zu groß für einen Step).
- **Konzept-Referenz:** `Konzept.md` Muss-Haben F, Punkt 1
  ("`ConsoleTestCollection`-Regression beheben (größter Hebel)"), plus
  Abschnitt "Entdeckte Mängel/Redundanzen" → "`ConsoleTestCollection` von 8
  auf 21 Mitglieder zurückgewachsen".

## Aktueller Projektzustand (JIT-Kontext)

Verifiziert gegen den tatsächlichen Code (nicht nur gegen die
Konzept-Beschreibung):

- `src/AiNetLinter.Tests/ConsoleTestCollection.cs` definiert die Collection
  (`[CollectionDefinition(nameof(ConsoleTestCollection), DisableParallelization = true)]`),
  trägt selbst keine Mitgliederliste — Mitgliedschaft wird pro Testklasse
  per `[Collection("ConsoleTestCollection")]`-Attribut deklariert.
- **Grep bestätigt exakt 21 Testklassen** mit diesem Attribut (deckt sich
  mit Konzept-Zahl):
  `FindSymbolToolTests`, `McpDocumentationSmokeTests`,
  `McpLiveRepositoryTests`, `McpServerAllToolsE2ETests`,
  `McpServerOptionsBuilderTests`, `McpServerOptionsFactoryTests`,
  `McpTestClientParallelTests`, `McpTestClientRetryTests`,
  `McpCodeGraphServerConstructorTests`, `ProgramTests`, `AuditCommandTests`,
  `CliBatchRegressionTests`, `DocsCommandTests`,
  `McpServerCommandAmbiguityE2ETests`, `McpServerCommandErrorHandlingTests`,
  `McpServerCommandStalenessTests`, `PlaybookCheckCommandTests`,
  `SyncAgentRulesCommandTests`, `BaselineCliTests`, `CliIntegrationTests`,
  `FilterCliIntegrationTests`.
- **Eigene Analyse (Grep nach `Console.SetOut`/`Console.SetError`/
  `StringWriter`/`TestLintConsole` je Datei) ergibt eine klare Dreiteilung:**

  **Gruppe A — echter Console-Capture-Bedarf (5 Klassen, bleiben Mitglied):**
  `ProgramTests`, `AuditCommandTests`, `DocsCommandTests`,
  `PlaybookCheckCommandTests`, `SyncAgentRulesCommandTests` — alle
  verwenden `Console.SetOut`/`StringWriter`/`TestLintConsole`
  (`src/AiNetLinter.Tests/Output/TestLintConsole.cs`, bestehender
  Console-Capture-Helper) zur Ausgabe-Verifikation. Genau der in
  `AiNetLinterRichtlinien.mdc` §4 beschriebene legitime Fall
  ("globale `Console.Out`/`Console.Error`-Umleitung, die sich mit
  parallel laufenden Tests gegenseitig stören würde").

  **Gruppe B — reine In-Process-Unit-Tests ohne Subprozess, ohne
  Console-Umleitung (6 Klassen, Mitgliedschaft ersatzlos entfernen):**
  `McpServerOptionsBuilderTests`, `McpServerOptionsFactoryTests`,
  `McpCodeGraphServerConstructorTests`, `McpTestClientRetryTests`,
  `FindSymbolToolTests` (nutzt `BaselineCatalogFixture`/
  `SymbolGraphCatalogFixture` — In-Process-Katalogaufbau, kein
  `Process.Start`), `McpServerCommandErrorHandlingTests` (kein
  `Process.Start`/`McpTestClient.ConnectAsync` im Grep-Treffer). Kein
  erkennbarer Grund für Zwangsserialisierung — die ursprüngliche
  8-Klassen-Baseline hatte diese Klassen nachweislich nicht enthalten,
  sie sind organisch mit der MCP-Arbeit hinzugewachsen.

  **Gruppe C — startet echte Subprozesse (`AiNetLinter.exe` via
  `Process.Start`/`ProcessStartInfo` oder `McpTestClient.ConnectAsync`,
  10 Klassen, Mitgliedschaft entfernen, aber mit begrenzender
  Nebenläufigkeits-Bremse statt Totalserialisierung):**
  `BaselineCliTests`, `CliIntegrationTests`, `FilterCliIntegrationTests`,
  `CliBatchRegressionTests` (direkter `Process.Start` auf
  `AiNetLinter.exe`), `McpServerCommandAmbiguityE2ETests`,
  `McpServerCommandStalenessTests`, `McpTestClientParallelTests`
  (startet in einem einzigen Test 16 parallele
  `McpTestClient.ConnectAsync`-Subprozesse — bewusster Stresstest für den
  Retry-Loop, siehe TD-019/Einheit 010/011), `McpServerAllToolsE2ETests`
  (`SymbolGraphMcpFixture`), `McpDocumentationSmokeTests` +
  `McpLiveRepositoryTests` (beide `McpLiveRepositoryFixture`) — Fixtures
  starten den Subprozess einmal pro Klasse (`IClassFixture`), nicht pro
  Testmethode.
- **Wichtige Abgrenzung zu F.2:** `src/AiNetLinter.Tests/Fixtures/`
  enthält noch **keinen** gemeinsamen `CliProcessRunner`-Helper (das ist
  F.2, ein separater späterer Step desselben Epics). Dieser Step darf die
  8 in Konzept F.2 gelisteten `Process.Start`-Stellen nicht vorwegnehmend
  auf einen neuen Helper umstellen — das würde den nächsten Step
  vorwegplanen. Stattdessen: eine **minimale, eigenständige** Bremse
  (`SemaphoreSlim`), die F.2 später ersetzen/aufnehmen kann, ohne dass F.1
  und F.2 sich überschneiden.
- **Bestehendes Fixture-Sharing-Muster** (`SymbolGraphCatalogFixture.cs`,
  `BaselineCatalogFixture.cs` in `src/AiNetLinter.Tests/Fixtures/`) zeigt
  bereits das Konventions-Muster für neue `ICollectionFixture`/
  gemeinsame Test-Infrastruktur in diesem Projekt — die neue Bremse folgt
  demselben Ordner/Namensschema (`Fixtures/`).
- **Regel-Bezug** (`AiNetLinterRichtlinien.mdc` §4, neu in dieser Session
  ergänzt): neue Testklassen werden standardmäßig nicht in eine
  zwangsserialisierende Collection aufgenommen; Ausnahme nur bei echtem,
  im XML-Doc-Kommentar der Klasse begründetem Bedarf. Vermutete
  Nebenläufigkeitsprobleme bei Subprozess-Tests werden **gezielt** gelöst
  (Lock/Fixture/Retry/begrenzende `SemaphoreSlim`), nicht durch
  Collection-weite Zwangsserialisierung.

## Intention

Nach diesem Step trägt `[Collection("ConsoleTestCollection")]` nur noch
die 5 Klassen mit echtem Console-Capture-Bedarf, jede mit einer
XML-Doc-Begründung an der Klasse (DoD-Anforderung aus `Konzept.md`). Die
16 übrigen Klassen laufen wieder parallel — die 6 reinen In-Process-Tests
ohne jede Einschränkung, die 10 Subprozess-Tests hinter einer neuen,
projektweit geteilten `SemaphoreSlim`-Bremse, die eine begrenzte Zahl
gleichzeitiger `AiNetLinter.exe`-Prozesse zulässt (Kompromiss zwischen
Laufzeitgewinn und der in `Konzept.md` dokumentierten
Ressourcen-Konkurrenz-Sorge, TD-019-Historie). Das ist der laut Konzept
größte Einzelhebel für den ~8-Minuten-Volllauf.

## Konkrete Änderungen

### Gruppe A: Legitime Console-Capture-Mitglieder — XML-Doc-Begründung ergänzen

Dateien (Attribut bleibt unverändert): `src/AiNetLinter.Tests/Cli/ProgramTests.cs`,
`src/AiNetLinter.Tests/Commands/AuditCommandTests.cs`,
`src/AiNetLinter.Tests/Commands/DocsCommandTests.cs`,
`src/AiNetLinter.Tests/Commands/PlaybookCheckCommandTests.cs`,
`src/AiNetLinter.Tests/Commands/SyncAgentRulesCommandTests.cs`

- **Was:** Jeder Klasse einen XML-Doc-Kommentar voranstellen, der den
  Console-Capture-Bedarf konkret benennt (z. B. "Nutzt
  `Console.SetOut`/`TestLintConsole` zur Ausgabe-Verifikation — parallel
  laufende Tests würden sich die globale Konsolenumleitung gegenseitig
  überschreiben."). Kein Textbaustein-Copy-Paste ohne Bezug zur jeweiligen
  Klasse — kurz, aber klassenspezifisch, wo relevant (z. B. welcher
  Console-Ausgabe-Pfad konkret geprüft wird).
- **Warum:** DoD-Anforderung aus `Konzept.md` ("jede verbleibende
  Mitgliedschaft mit Begründung im XML-Doc-Kommentar der Klasse") sowie
  `AiNetLinterRichtlinien.mdc` §4.

### Gruppe B: Reine In-Process-Tests — Mitgliedschaft entfernen

Dateien: `src/AiNetLinter.Tests/Mcp/McpServerOptionsBuilderTests.cs`,
`src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs`,
`src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs`,
`src/AiNetLinter.Tests/Mcp/McpTestClientRetryTests.cs`,
`src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs`,
`src/AiNetLinter.Tests/Commands/McpServerCommandErrorHandlingTests.cs`

- **Was:** `[Collection("ConsoleTestCollection")]`-Attribut entfernen.
  Vor dem Entfernen jede Klasse selbst noch einmal gegenlesen (nicht nur
  auf dieses Step-Plan-Grep verlassen) — insbesondere prüfen, ob
  statischer/geteilter Mutable State zwischen den Testmethoden existiert,
  der bei Parallelisierung *innerhalb* der Klasse Probleme machen könnte
  (unabhängig von der Collection-Frage).
- **Warum:** Kein Console-Capture, kein Subprozess-Start — keine erkennbare
  Grundlage für Zwangsserialisierung; verstößt aktuell gegen
  `AiNetLinterRichtlinien.mdc` §4.

### Gruppe C: Subprozess-Tests — Mitgliedschaft entfernen, Nebenläufigkeits-Bremse einführen

Neue Datei: `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs`

- **Was:** Statische, `sealed` Hilfsklasse mit einem projektweiten
  `SemaphoreSlim` (Startgröße konfigurierbar, Vorschlag 4 — grob an
  typischer Kernzahl orientiert, kein Anspruch auf Optimalwert) und einer
  schlanken Methode (z. B. `Task<IDisposable> AcquireAsync()`, gibt ein
  `IAsyncDisposable`/`IDisposable` zurück, das beim Dispose freigibt —
  `using`-freundlich an den Aufrufstellen). Kein Konstruktor-Overhead,
  keine `ICollectionFixture`-Registrierung nötig (statischer Zugriff genügt
  für eine reine Zähl-Bremse, anders als bei den bestehenden
  `SymbolGraphCatalogFixture`/`BaselineCatalogFixture`-Mustern, die
  teuren Setup-State teilen — hier gibt es keinen Setup-State, nur einen
  Zähler).
- **Warum:** `AiNetLinterRichtlinien.mdc` §4 verlangt bei vermuteten
  Subprozess-Nebenläufigkeitsproblemen eine gezielte, begrenzende Lösung
  (explizit `SemaphoreSlim` genannt) statt Collection-weiter
  Zwangsserialisierung.

Dateien (Attribut entfernen + Subprozess-Start(s) mit der neuen Bremse
umschließen): `src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs`,
`src/AiNetLinter.Tests/Cli/CliIntegrationTests.cs`,
`src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs`,
`src/AiNetLinter.Tests/Commands/CliBatchRegressionTests.cs`,
`src/AiNetLinter.Tests/Commands/McpServerCommandAmbiguityE2ETests.cs`,
`src/AiNetLinter.Tests/Commands/McpServerCommandStalenessTests.cs`,
`src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs`,
`src/AiNetLinter.Tests/Mcp/McpServerAllToolsE2ETests.cs`,
`src/AiNetLinter.Tests/Mcp/McpDocumentationSmokeTests.cs`,
`src/AiNetLinter.Tests/Mcp/McpLiveRepositoryTests.cs`

- **Was:** `[Collection("ConsoleTestCollection")]` entfernen. Direkte
  `Process.Start`/`ProcessStartInfo`-Aufrufe (erste 4 Dateien) sowie
  `McpTestClient.ConnectAsync`-Aufrufe (restliche Dateien, teils über die
  Fixtures `SymbolGraphMcpFixture`/`McpLiveRepositoryFixture` statt direkt
  in der Testklasse — dort ansetzen, wo der eigentliche Subprozess-Start
  passiert) mit `SubprocessConcurrencyGate.AcquireAsync()` umschließen.
  Bei `McpTestClientParallelTests` (16 parallele Connects in einem Test):
  Gate **pro einzelnem Connect-Aufruf** greifen lassen, nicht um den
  gesamten `Task.WhenAll`-Block — sonst bleibt der Stresstest-Zweck
  (gleichzeitige Connects) durch die Bremse selbst ausgehebelt; die Gate
  begrenzt dann nur, wie viele der 16 tatsächlich gleichzeitig laufen,
  nicht ob der Test parallele Last erzeugt.
  Bei `IClassFixture`-basierten Subprozess-Starts (`SymbolGraphMcpFixture`,
  `McpLiveRepositoryFixture`): Gate im Fixture-Konstruktor/`InitializeAsync`
  ansetzen, nicht in jeder einzelnen Testmethode (Subprozess startet dort
  ohnehin nur einmal pro Klasse).
- **Warum:** Entfernt die Zwangsserialisierung (Laufzeitgewinn), behält
  aber eine begrenzende Bremse gegen unkontrollierte Prozessexplosion bei
  gleichzeitigem Parallel-Lauf aller 10 Klassen (Konzept-Sorge:
  "Ressourcen-Konkurrenz bei vielen gleichzeitigen
  `AiNetLinter.exe`-Prozessen").

## Tests

- [ ] `dotnet build AiNetLinter.slnx` — 0 Warnungen/Fehler.
- [ ] `dotnet test --filter Category=Unit` — grün, als schnelle
  Zwischenverifikation nach den Gruppe-B-Änderungen.
- [ ] `dotnet test AiNetLinter.slnx --no-build` — vollständiger Volllauf,
  **mindestens zweimal hintereinander** ausführen (nicht nur einmal) —
  genau hier ist die Gefahr einer neu eingeführten, nicht-deterministischen
  Flakiness durch die entfernte Serialisierung am größten; ein einzelner
  grüner Lauf ist kein ausreichender Nachweis. Bei Rot: `TestResults/latest.trx`
  auslesen statt blind erneut zu laufen.
- [ ] Laufzeit des Volllaufs notieren (grobe Zahl, kein hartes Ziel) — als
  informelle Grundlage für den späteren F.6-Step, der die formale
  Vorher/Nachher-Dokumentation leistet. Keine eigene DoD-Pflicht dieses
  Steps.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt (Gruppe A/B/C)
- [ ] `ConsoleTestCollection` hat nur noch 5 Mitglieder, jedes mit
      XML-Doc-Begründung
- [ ] `SubprocessConcurrencyGate` existiert, wird an allen 10
      Gruppe-C-Subprozess-Start-Stellen verwendet
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün, Volllauf **zweimal**
      hintereinander grün (Flake-Check)
- [ ] Vor jedem Build/Test: offene `AiNetLinter.exe`/`testhost.exe`-Prozesse
      geprüft und bei Bedarf beendet (bekannte Datei-Sperren-Falle)
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[codegraph-mcp-finish]`)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4` (Updates & Tests —
  "Testsuite-Parallelität bewahren") — direkte Vorgabe für diesen Step:
  Zwangsserialisierung nur mit explizit begründetem Bedarf, gezielte
  Lösung (`SemaphoreSlim`) statt Collection-weiter Serialisierung bei
  vermuteten Subprozess-Nebenläufigkeitsproblemen.
- `.agents/rules/AiNetLinter.mdc` — allgemeine Kurz-Stil-Grenzwerte gelten
  auch für die neue `SubprocessConcurrencyGate`-Klasse (`sealed`,
  `#nullable enable`, ≤500 Zeilen/Datei, ≤4 Methodenparameter); Projekt-
  Override für `*.Tests` beachten (`MaxMethodLineCount` 100,
  `EnforceSealedClasses` aus — Sealed dennoch sinnvoll, da reine
  Hilfsklasse ohne Vererbungsbedarf).

## Bekannte Ausnahmen

- Keine.

## Notes

- **Nicht Scope dieses Steps:** F.2 (`CliProcessRunner`-Helper), F.3
  (`Core/`-Sub-Gliederung), F.4 (Test-Data-Builder), F.5
  (`#nullable enable`-Retrofit), F.6 (formale Laufzeitmessung
  vorher/nachher in `result.md`/`summary.md`) — bleiben als offene Teile
  von `EPIC-01` für Folge-Steps.
- **Für den nächsten Step-Modus-Aufruf relevant:** `F.2` baut
  voraussichtlich einen `CliProcessRunner`-Helper, der die hier neu
  eingeführte `SubprocessConcurrencyGate` sinnvoll aufnehmen/ersetzen
  kann (z. B. als internes Detail des Runners) — der nächste Planer-Aufruf
  sollte das beim Lesen des tatsächlichen Codes berücksichtigen, statt
  eine zweite, konkurrierende Bremse zu bauen.
- **Kategorisierung in "Aktueller Projektzustand" ist Planer-Analyse, kein
  Ersatz für eigene Prüfung:** Der Coder soll jede der 21 Klassen beim
  Anfassen selbst kurz gegenlesen (insbesondere Gruppe B — falls doch ein
  bisher übersehener geteilter State auffällt, gehört diese Klasse dann
  nicht mehr zu Gruppe B, sondern bekommt entweder eine eigene gezielte
  Lösung oder bleibt vorerst mit begründetem Kommentar in der Collection).
- **Kein Symptom-Fixing:** Falls der Volllauf nach den Änderungen einmalig
  flaky wird, nicht durch Wieder-Hinzufügen der Collection "reparieren",
  ohne die tatsächliche Ursache (welcher konkrete Shared State/welche
  Ressource kollidiert) zu benennen — das wäre die Regression, die dieser
  Step gerade beheben soll, nur unter neuem Namen.
