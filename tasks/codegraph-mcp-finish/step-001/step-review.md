---
status: done
type: step-review
task: codegraph-mcp-finish
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: approved
tech_debt_ids: [TD-001]
---

# Review Step 001: Testsuite-Performance — ConsoleTestCollection-Regression beheben (F.1)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Plan-Erfüllung: alle drei Gruppen (A/B/C) korrekt umgesetzt, inkl. der im
Plan geforderten Selbst-Prüfung jeder Klasse (führte zur korrekten
Umkategorisierung von `McpServerCommandErrorHandlingTests`). Rules-Konformität:
`AiNetLinterRichtlinien.mdc` §4 eingehalten (5 begründete Mitglieder,
gezielte `SemaphoreSlim`-Bremse statt Zwangsserialisierung),
`AiNetLinter.mdc`-Stilgrenzen bei der neuen Datei eingehalten. Logische
Korrektheit: gefundener Deadlock im Diff nachvollzogen und der Fix im
Code verifiziert (siehe unten) — eine kleinere Doku-Ungenauigkeit siehe
„Sonstige Beobachtungen". Konzept-Treue: Scope entspricht exakt F.1,
keine Vorwegnahme von F.2-F.6.

### Plan-Erfüllung

- Gruppe A (5 Klassen, XML-Doc-Begründung): erfüllt. Diff geprüft
  (`ProgramTests.cs`, `AuditCommandTests.cs`, `DocsCommandTests.cs`,
  `PlaybookCheckCommandTests.cs`, `SyncAgentRulesCommandTests.cs`) — jede
  Begründung ist klassenspezifisch (konkreter Testfall/Konsolenpfad
  benannt), kein generischer Textbaustein. `ConsoleTestCollection`-Mitgliedschaft
  per Grep verifiziert: exakt diese 5 Klassen, keine mehr, keine weniger.
- Gruppe B (6 Klassen, Attribut entfernt): erfüllt. Diff zeigt für alle 6
  Dateien nur die Entfernung der `[Collection(...)]`-Zeile. Eigene
  Prüfung auf geteilten Mutable State (Plan verlangte das explizit vor
  dem Entfernen): keine `static`-Felder in den 6 Klassen gefunden (nur
  eine zustandslose `private static string CreateTempDir()`-Hilfsmethode
  in `McpServerCommandErrorHandlingTests`) — keine erkennbare
  Parallelisierungsgefahr.
- Gruppe C (10 Klassen + Bremse): erfüllt, sogar um einen Fall erweitert
  (siehe Abweichung unten). `SubprocessConcurrencyGate` existiert
  (`src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs`),
  `SemaphoreSlim(4,4)`, `AcquireAsync()` liefert ein `IDisposable`-Lease.
  Bei den 4 direkten `Process.Start`-Dateien (`BaselineCliTests`,
  `CliIntegrationTests`, `FilterCliIntegrationTests`,
  `CliBatchRegressionTests`) wird das Lease unmittelbar vor
  `Process.Start` erworben und per `using` bis zum Ende der jeweiligen
  Testmethode gehalten — bindet den Subprozess korrekt für dessen ganze
  Laufzeit. Bei den MCP-Client-basierten Dateien läuft die Bremse
  zentral über `McpTestClient.ConnectAsync` (verifiziert: keine
  Änderung an `SymbolGraphMcpFixture`/`McpLiveRepositoryFixture`
  nötig/vorgenommen, wie im `step-result.md` behauptet).
  `McpTestClientParallelTests`: Gate greift wie gefordert pro
  einzelnem `ConnectAsync`-Aufruf, nicht um den gesamten
  `Task.WhenAll`-Block — verifiziert im Diff von `McpTestClient.cs`.

**Deadlock-Fix verifiziert (nicht nur der Beschreibung geglaubt):** Vor
dem Fix hätte `using var lease = ...` im ursprünglichen (unfertigen)
Stand vermutlich beim `McpTestClient`-Objekt selbst gelegen (bis
`DisposeAsync`). Im finalen Commit (`git show e466020` auf
`McpTestClient.cs`) liegt `using var lease = await
SubprocessConcurrencyGate.AcquireAsync(...)` **innerhalb der
`while`-Schleife**, vor dem `try`-Block, der den eigentlichen Connect
durchführt — der C#-`using`-Scope für diese lokale Deklaration endet am
Ende des umschließenden Blocks, also inklusive des frühen `return new
McpTestClient(client);` im try-Zweig: das Lease wird beim Verlassen der
Iteration (Erfolg oder Fehlschlag) freigegeben, nicht erst beim
`DisposeAsync` der zurückgegebenen Instanz. Das ist exakt der im
Commit/Result beschriebene Fix. Eigene Reproduktion:
`dotnet test --filter FullyQualifiedName~McpTestClientParallelTests`
lief isoliert grün durch (1/1, ~1 m 26 s, keine Hänger); anschließend
voller Volllauf `dotnet test AiNetLinter.slnx --no-build` grün (1186/1186,
1 m 43 s). Kein Deadlock reproduzierbar.

### Rules-Konformität

- `AiNetLinterRichtlinien.mdc` §4 („Testsuite-Parallelität bewahren"):
  eingehalten. `ConsoleTestCollection` hat nach dem Diff nur noch die 5
  Gruppe-A-Klassen (per Grep verifiziert), jede mit klassenspezifischer
  Begründung. Für vermutete Subprozess-Nebenläufigkeitsprobleme wurde wie
  gefordert eine gezielte `SemaphoreSlim`-Lösung statt
  Collection-Zwangsserialisierung gewählt.
- `AiNetLinter.mdc` (Kurz-Stil-Grenzwerte): `SubprocessConcurrencyGate.cs`
  hat `#nullable enable` (Zeile 1), 45 Zeilen (≤500), die äußere Klasse
  ist `static` (in C# implizit versiegelt, `sealed`-Anforderung
  gegenstandslos erfüllt), die innere `Lease`-Klasse ist explizit
  `sealed`, `AcquireAsync` hat einen Parameter (≤4). Keine Verstöße
  gefunden.

### Logische Korrektheit

Deadlock-Ursache und -Fix nachvollzogen (siehe oben, eigene
Testreproduktion). Die 4 direkten `Process.Start`-Aufrufer halten das
Lease korrekt über die gesamte Subprozess-Laufzeit; die MCP-Client-Aufrufer
(zentral über `McpTestClient.ConnectAsync`) geben das Lease dagegen
bereits direkt nach dem Handshake frei (notwendig zur Deadlock-Vermeidung
bei 16 parallelen Connects gegen 4 Slots) — die Bremse begrenzt für diese
Klassen faktisch nur noch gleichzeitige Start-/Handshake-Vorgänge, nicht
mehr die Zahl insgesamt gleichzeitig laufender Prozesse. Das ist eine
nachvollziehbare, im `step-result.md` transparent offengelegte
Kompromissentscheidung zur Deadlock-Vermeidung. Ein daraus resultierender
Doku-Fehler in `SubprocessConcurrencyGate.cs` selbst siehe „Sonstige
Beobachtungen" unten (MINOR, kein Blocker).

### Konzept-Treue (Ebene 4)

`Konzept.md` Block F.1 formuliert die Ressourcen-Konkurrenz-Sorge
ausdrücklich konditional („**Falls** die eigentliche Sorge
Ressourcen-Konkurrenz ... war: eine begrenzende, aber nicht vollständig
serialisierende Lösung ... statt Totalserialisierung") — keine harte
Vorgabe, dass die Bremse zwingend die Gesamtzahl gleichzeitig laufender
Prozesse für die volle Testlaufzeit begrenzen muss, nur dass überhaupt
eine begrenzende (nicht rein serialisierende) Lösung existiert. Das ist
erfüllt: die Bremse begrenzt nachweislich gleichzeitige Prozess-Starts/
Handshakes auf 4, keine Totalserialisierung. Scope entspricht exakt F.1
(„Notes" im Plan grenzt F.2-F.6 sauber ab, Diff bestätigt: kein
`CliProcessRunner`, keine `Core/`-Umgliederung, kein
`#nullable enable`-Retrofit außerhalb der neuen Datei angefasst). Kein
Non-Goal umgesetzt, kein Muss-Haben-Punkt aus `Konzept.md` F.1 fehlt.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx                                              → grün (0 Warnungen, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build --filter Category=Unit             → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build --filter ~McpTestClientParallelTests → grün (1 Test, 0 Fehler, ~1 m 26 s, kein Deadlock)
dotnet test AiNetLinter.slnx --no-build                                    → grün (1186 Tests, 0 Fehler, 1 m 43 s)
```
Deckt sich mit den im `step-result.md` behaupteten Werten (1186/1186,
~1 m 35–41 s über zwei Läufe).

## Sonstige Beobachtungen / MINOR / NITPICK

- `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs:9-13` (Klassen-Doc)
  und `:21-26` (`AcquireAsync`-Doc) beschreiben einen Vertrag ("Der
  Aufrufer haelt das Handle fuer die gesamte Laufzeit des zugehoerigen
  Subprozesses ... damit die Bremse tatsaechlich die Zahl gleichzeitig
  laufender Prozesse begrenzt"), der durch die tatsächliche, im selben
  Commit eingeführte Nutzung in `McpTestClient.cs` (Lease wird direkt
  nach dem Handshake freigegeben, nicht bis `DisposeAsync` gehalten)
  widerlegt wird — für 6 der 10 Gruppe-C-Klassen (alle MCP-Client-basierten)
  stimmt der dokumentierte Vertrag nicht mehr. Die inline-Begründung in
  `McpTestClient.cs:44-53` beschreibt das tatsächliche Verhalten korrekt
  und widerspricht damit direkt der Doku in `SubprocessConcurrencyGate.cs`.
  Kein Blocker (Bug-frei, Konzept-Anforderung ist trotzdem erfüllt, siehe
  Ebene 4), aber die Doku sollte in einem der nächsten Berührungen dieser
  Datei (z. B. F.2) korrigiert werden, um künftige Fehlannahmen über die
  Stärke der Bremse zu vermeiden.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — abgerissene, vorbestehende XML-Doc-Kommentare
  in drei Gruppe-B-Testklassen (`McpCodeGraphServerConstructorTests`,
  `McpServerOptionsFactoryTests`, `McpTestClientRetryTests`), außerhalb
  des Scopes dieses Steps.
