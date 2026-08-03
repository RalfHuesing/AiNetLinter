---
status: done
type: step-result
task: codegraph-mcp-finish
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: e466020
status_after: done
blocker_category: n/a
---

# Result Step 001: Testsuite-Performance — ConsoleTestCollection-Regression beheben (F.1)

## Zusammenfassung

`ConsoleTestCollection` von 21 auf 5 Mitglieder reduziert (Gruppe A, je mit
XML-Doc-Begründung). Gruppe B (6 Klassen) läuft jetzt ohne Mitgliedschaft
parallel. Gruppe C (10 Subprozess-Klassen) läuft ebenfalls ohne
Zwangsserialisierung, abgesichert durch eine neue `SubprocessConcurrencyGate`
(`SemaphoreSlim`, 4 Slots) an allen Subprozess-Start-Stellen. Ein bei der
Verifikation aufgetretener Deadlock in `McpTestClient.ConnectAsync` (Gate-Slot
bis `DisposeAsync` gehalten, inkompatibel mit dem 16-fach-Parallel-Connect in
`McpTestClientParallelTests`) wurde behoben, bevor committet wurde.

## Geänderte Dateien

- `src/AiNetLinter.Tests/Cli/ProgramTests.cs`, `.../Commands/AuditCommandTests.cs`,
  `.../Commands/DocsCommandTests.cs`, `.../Commands/PlaybookCheckCommandTests.cs`,
  `.../Commands/SyncAgentRulesCommandTests.cs` — XML-Doc-Begründung für
  verbleibende `ConsoleTestCollection`-Mitgliedschaft ergänzt (Attribut selbst
  unverändert).
- `src/AiNetLinter.Tests/Mcp/McpServerOptionsBuilderTests.cs`,
  `.../McpServerOptionsFactoryTests.cs`, `.../McpCodeGraphServerConstructorTests.cs`,
  `.../McpTestClientRetryTests.cs`, `.../Tools/FindSymbolToolTests.cs`,
  `.../Commands/McpServerCommandErrorHandlingTests.cs` — `[Collection(...)]`
  entfernt (Gruppe B im Plan). `McpServerCommandErrorHandlingTests` startet
  tatsächlich einen MCP-Subprozess (`StdioClientTransport`/`McpClient.CreateAsync`)
  und bekam daher zusätzlich `SubprocessConcurrencyGate`-Absicherung statt einer
  reinen Attribut-Entfernung — siehe „Abweichungen vom Plan".
- `src/AiNetLinter.Tests/Fixtures/SubprocessConcurrencyGate.cs` (neu) — statische
  `SemaphoreSlim`-Bremse (4 Slots), `AcquireAsync()` liefert ein `IDisposable`-Lease.
- `src/AiNetLinter.Tests/Baseline/BaselineCliTests.cs`, `.../Cli/CliIntegrationTests.cs`,
  `.../Cli/FilterCliIntegrationTests.cs`, `.../Commands/CliBatchRegressionTests.cs`,
  `.../Commands/McpServerCommandAmbiguityE2ETests.cs`,
  `.../Commands/McpServerCommandStalenessTests.cs`,
  `.../Mcp/McpTestClientParallelTests.cs`, `.../Mcp/McpServerAllToolsE2ETests.cs`,
  `.../Mcp/McpDocumentationSmokeTests.cs`, `.../Mcp/McpLiveRepositoryTests.cs` —
  `[Collection(...)]` entfernt (Gruppe C). Direkte `Process.Start`-Aufrufe (erste
  4 Dateien) mit `SubprocessConcurrencyGate.AcquireAsync()` umschlossen; die
  restlichen laufen über `McpTestClient.ConnectAsync`/`SymbolGraphMcpFixture`/
  `McpLiveRepositoryFixture`, die den Gate-Zugriff zentral in
  `McpTestClient.ConnectAsync` bekommen (siehe unten) — keine Änderung an den
  Fixtures selbst nötig.
- `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` — Gate-Aufruf in `ConnectAsync`
  zentralisiert; Slot wird nur für Prozessstart + Handshake gehalten und direkt
  danach freigegeben (nicht erst bei `DisposeAsync`) — Fix für den unten
  beschriebenen Deadlock.

## Commit

- **Code-Commit-Hash:** `e466020`
- **Message:**
  ```
  test: ConsoleTestCollection auf begruendete Mitglieder eingrenzen [codegraph-mcp-finish]

  Reduziert ConsoleTestCollection von 21 auf 5 Mitglieder (echter
  Console.Out/Error-Capture-Bedarf, jeweils XML-Doc-begruendet). 6 reine
  In-Process-Unit-Tests laufen jetzt parallel. Fuer die 10
  Subprozess-Tests ersetzt eine neue, statische SubprocessConcurrencyGate
  (SemaphoreSlim, 4 Slots) die Zwangsserialisierung.

  McpTestClient.ConnectAsync hielt den Gate-Slot urspruenglich bis zum
  DisposeAsync des Clients offen - bei McpTestClientParallelTests (16
  gleichzeitige Connects gegen ein 4-Slot-Gate) fuehrte das zu einem
  Deadlock, da alle erfolgreichen Connects ihre Slots erst nach
  Abschluss aller 16 Tasks freigegeben haetten. Slot wird jetzt nur
  fuer Prozessstart+Handshake gehalten und sofort danach freigegeben.

  Refs: tasks/codegraph-mcp-finish/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx                → grün (0 Warnungen, 0 Fehler)
dotnet test --filter Category=Unit           → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build (1/2) → grün (1186 Tests, 0 Fehler, 1 m 41 s)
dotnet test AiNetLinter.slnx --no-build (2/2) → grün (1186 Tests, 0 Fehler, 1 m 35 s)
```

Laufzeit-Notiz (informell, für späteren F.6-Step): Volllauf jetzt bei ca.
1 m 35–41 s statt der im Konzept dokumentierten ~8 Minuten Baseline — deutlicher
Effekt der aufgehobenen Zwangsserialisierung.

## Abweichungen vom Plan

- **`McpServerCommandErrorHandlingTests` war im Plan als Gruppe B kategorisiert**
  ("kein `Process.Start`/`McpTestClient.ConnectAsync` im Grep-Treffer"), startet
  beim eigenen Gegenlesen aber tatsächlich einen echten MCP-Subprozess via
  `StdioClientTransport`/`McpClient.CreateAsync` mit `Command = exePath` auf
  `AiNetLinter.exe` — der Plan-Grep hat das übersehen, weil weder der Literal-String
  `Process.Start` noch `McpTestClient.ConnectAsync` im Code vorkommt (die
  Transport-Abstraktion startet den Prozess intern). Behandelt wie Gruppe C:
  Attribut entfernt, beide Testmethoden mit `SubprocessConcurrencyGate.AcquireAsync()`
  um den Client-Verbindungsaufbau abgesichert. Das entspricht explizit der
  Anweisung im Step-Plan, jede der 21 Klassen selbst gegenzulesen statt der
  Planer-Kategorisierung blind zu vertrauen.
- **Deadlock-Fix in `McpTestClient.ConnectAsync` (nicht im Plan vorgesehen, aber
  zur Umsetzung des Plans zwingend nötig):** Der ursprüngliche Implementierungs-
  entwurf (in einem bereits vorgefundenen, unfertigen Arbeitsstand — siehe
  „Beobachtungen") hielt den Gate-Slot vom Verbindungsaufbau bis zum
  `DisposeAsync()` des `McpTestClient`. Das ist für Tests mit 1–2 sequenziellen
  Connects unproblematisch, führte aber bei `McpTestClientParallelTests`
  (16 parallele `ConnectAsync`-Aufrufe gegen ein 4-Slot-Gate, `Task.WhenAll`,
  erst danach `DisposeAsync` für alle 16 in einer Schleife) zu einem echten
  Deadlock: die ersten 4 Connects belegen alle Slots dauerhaft (Freigabe erst
  nach `Task.WhenAll`), die restlichen 12 warten auf einen Slot, der nie frei
  wird, weil dessen Freigabe selbst von der Fertigstellung aller 16 abhängt.
  Reproduziert beim ersten Volllauf-Versuch (Prozess nach ca. 10 Minuten ohne
  jede CPU-Aktivität und ohne laufende `AiNetLinter.exe`-Kindprozesse manuell
  beendet). Fix: Slot wird jetzt nur für Prozessstart + MCP-Handshake gehalten
  und direkt danach freigegeben (`using var lease` statt Übergabe an die
  `McpTestClient`-Instanz) — entspricht der im Step-Plan explizit für diesen
  Test geforderten Semantik „Gate pro einzelnem Connect-Aufruf greifen lassen,
  nicht um den gesamten Task.WhenAll-Block", nur dass die Freigabe-Seite (nicht
  nur die Acquire-Seite) korrigiert werden musste, um den Stresstest-Zweck ohne
  Deadlock zu erhalten.

## Beobachtungen

- **Vorgefundener, unfertiger Arbeitsstand:** Bei Übernahme des Steps lag im
  Arbeitsverzeichnis bereits ein größtenteils fertiger, aber unfertig und
  **nicht committeter** Implementierungsstand vor (Gruppe B/C korrekt
  umgesetzt inkl. neuer `SubprocessConcurrencyGate`-Datei, aber ohne die
  Gruppe-A-XML-Docs und mit dem oben beschriebenen Deadlock-Bug). Ursprung
  unklar (vermutlich ein vorheriger, unterbrochener Coder-Lauf). Übernommen,
  gegengelesen, vervollständigt und den Deadlock behoben, statt neu zu
  implementieren — das Ergebnis ist inhaltlich identisch mit dem, was aus
  dem Plan heraus entstanden wäre, nur der Entstehungsweg war ungewöhnlich.
  Für den Kritiker relevant: der komplette Diff wurde von mir Zeile für Zeile
  gegen den Plan geprüft, nicht blind übernommen.
- **Vorbestehende, inhaltlich lückenhafte XML-Doc-Kommentare** in mehreren
  Gruppe-B-Klassen (`McpCodeGraphServerConstructorTests`,
  `McpServerOptionsFactoryTests`, `McpTestClientRetryTests`) — Sätze brechen
  mitten im Wort/Satz ab (z. B. „Eingefuehrt mit `MaxConstructorDependencies: 5`-
  Limit lag." oder „...siehe Plan-Abweichung im `result.md` von."). Diese
  Lücken existierten bereits vor diesem Step (nicht durch die hier gemachten
  Änderungen verursacht) und liegen außerhalb des Scopes (nur das
  `[Collection(...)]`-Attribut sollte hier angefasst werden) — nicht
  korrigiert, um keine Scope-Erweiterung vorzunehmen. Möglicher
  Tech-Debt-Kandidat für den Kritiker.
- **`McpServerCommandGetImpactTests`** (nicht Teil der 21 im Plan gelisteten
  Klassen) nutzt ebenfalls `McpTestClient.ConnectAsync` und war beim ersten
  (deadlockenden) Testlauf-Versuch mit unter den blockierten Tests — nach dem
  Fix lief sie im Volllauf unauffällig grün mit durch. Kein Hinweis auf
  eigenständiges Problem, nur als Beobachtung, dass der Deadlock-Radius über
  die 21 Plan-Klassen hinausreichte (jede Klasse, die `McpTestClient.ConnectAsync`
  nutzt, wäre betroffen gewesen).

## Bekannte Unschärfen

- Die `SubprocessConcurrencyGate`-Kapazität (4 Slots) ist wie im Plan
  vorgeschlagen ein grober, nicht optimierter Wert. Nach dem Deadlock-Fix
  begrenzt sie nur noch die Zahl gleichzeitiger Prozess-Start-/Handshake-Vorgänge,
  nicht mehr die Zahl insgesamt gleichzeitig verbundener/laufender Clients —
  das ist eine bewusste Abweichung von der ursprünglich im Code-Kommentar
  dokumentierten (aber deadlock-anfälligen) Absicht „begrenzt die Zahl
  gleichzeitig laufender Prozesse". Ob dieser schwächere Schutz für die im
  Konzept dokumentierte Ressourcen-Konkurrenz-Sorge (TD-019) ausreicht, sollte
  der Kritiker im Kontext von F.2 (`CliProcessRunner`) nochmal einordnen — zwei
  Volllauf-Wiederholungen liefen hier grün, das ist aber kein Beweis für
  Deadlock-Freiheit unter allen Lastszenarien.
- Kein Hinweis auf sonstige Flakiness in den zwei Volllauf-Wiederholungen
  (1186/1186 beide Male), aber zwei Läufe sind ein begrenztes Stichproben-Budget
  für ein Nebenläufigkeits-Problem.
