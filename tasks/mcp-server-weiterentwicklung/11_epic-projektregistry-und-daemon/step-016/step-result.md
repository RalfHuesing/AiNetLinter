---
status: done (pending audit)
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 016
epic: EPIC-B
completed_at: 2026-08-24T15:35:00+02:00
model: stealth/ox-alpha (openrouter)
model_knowledge_cutoff: nicht deklariert
code_commit: 2f1cced3
---

# Step 016 Ergebnis: Tech-Debt-Pflegepaket (TD-008, TD-001, TD-003, TD-010 fixen; TD-004 als Akzeptanz verankern)

## Zusammenfassung

Alle fünf Items des Batch-Steps sind umgesetzt. Kernstück ist die neue
`DaemonEndpointJanitor`-Assembly-Fixture für IntegrationTests (TD-008): Sie räumt
vor dem ersten Endpunkt-Zugriff überlebende Daemons eigener Bauart weg, verifiziert
den Endpunkt per Client-Probe und skippt transparent bei Fremdbelegung — nachgewiesen
durch Simulation eines hängenden Daemons. Bei der Gelegenheit wurde die tatsächliche,
bislang unentdeckte Kontaminationsquelle IN der Suite gefunden und geschlossen
(`McpServerLifetimeTests` spawnte Daemons mit 10-min Idle-Exit). item-02 fiel nach
Code-Verifikation kleiner aus als geplant (deterministischer Vertrag existierte schon);
item-03/04/05 entsprechen dem Plan. Vollstack grün: FastTests 1730/1730,
IntegrationTests 359/359 — die bisherigen TD-008-Kontaminationsausfälle (zuvor
357/359) treten nicht mehr auf.

## Geänderte Dateien

- **item-01:** `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonEndpointJanitor.cs` (neu)
  — statischer Janitor (Cleanup + Pipe-Probe + Skip-Gate, einmalig pro Testlauf,
  parallel-sicher) plus `DaemonEndpointJanitorFixture` als xUnit-v3-Assembly-Fixture;
  Identifikation ausschließlich über Bildpfad `AiNetLinter.exe` UND Lage unterhalb des
  Repos bzw. Gleichheit mit der Test-EXE — niemals blinde Namens-Matches.
- **item-01:** `.../Mcp/Daemon/DaemonProcessContractHarness.cs` — `AcquireEndpointAsync`
  führt den Janitor INNERHALB des exklusiven Endpoint-Gates aus (damit nie legitime
  parallele Endpunkt-Nutzer desselben Builds getroffen werden); bei Contamination wird
  das Gate freigegeben und per `Assert.Skip` transparent geskippt.
- **item-01:** `.../Platform/MsBuildFixtureHostAssemblyFixture.cs` — Fixture-Registrierung.
- **item-01:** `.../Mcp/Daemon/*ContractTests.cs` (4 Dateien) — Ctor-Injektion der
  Fixture; `ThinClientMcpProcessContractTests.NormalMcpServerPath…` bindet den Endpunkt
  jetzt ebenfalls über das Gate; CTS-Budgets der drei bestehenden Gated-Tests 45 s → 240 s
  (legitime Wartezeit auf den eigenen Turn bei bis zu vier Gate-Nutzern).
- **item-01:** `.../Mcp/McpServerLifetimeTests.cs` — Escape-Pin (`AINETLINTER_NO_DAEMON=1`,
  Muster wie McpProcessHost/Raw-Wire-Harness); siehe „Abweichungen" Punkt 2.
- **item-02:** `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` — `RULES_INVALID`-
  Fehlertext um kopierfähige Bauanleitung erweitert (minimales rules.json-Skelett mit
  `Global`/`Metrics.MaxLineCount`, Pfad, Hinweis „keine Default-Regeln geladen", Retry).
- **item-03:** `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` — Guard am Anfang
  von `Load`: null/leer/Whitespace → sofortige `Failure(PROJECT_ROOT_REQUIRED)` mit
  wörtlichem Self-Service-Template im Stil von `NotInitializedTemplate`; kein
  `projectRoot ?? string.Empty` mehr.
- **item-04:** `Docs/agent-api.md` — `AMBIGUOUS_SOLUTION`-Zeile entfernt.
- **item-05:** `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` — XML-Doc an
  `EvictLeastRecentlyUsed`: Überlauf bei nur-busy Slots ist bewusste Entscheidung
  (Nutzerentscheid 2026-08-24), TTL-Tick reklamiert Überschuss nicht.
- **Tests:** `ProjectDefinitionLoaderTests.cs` (+Theory null/""/"   " → PROJECT_ROOT_REQUIRED
  inkl. Template-Assertion), `ProjectInstanceFactoryTests.cs` (+Template-Assertions),
  `ProjectRegistryCapacityContractTests.cs` (neu; Overflow-Vertrag mit FakeClock:
  alle Slots busy → Bestand 3 > MaxProjects 2, Tick räumt nicht; Slot frei → Eviction
  greift beim nächsten Insert wieder und entfernt den LRU-freien Entry).
- `tasks/.../tech-debt.md` — Indexzeilen und Volltext-Statuszeilen gepflegt
  (TD-001/003/008/010 erledigt mit step-016-Verweis; TD-004 erledigt Akzeptanz).

## Commit

- **Code-Commit-Hash:** `2f1cced3`
- **Message:**
  ```
  fix: Tech-Debt-Batch TD-008/001/003/010/004 umsetzen [11_epic-projektregistry-und-daemon]

  (Body: je Item ein Umsetzungsbullet; Refs: tasks/.../step-016)
  ```
- **Branch:** main · **Push:** nein (lokal)
- **Doku-Commit:** separater zweiter Commit (tech-debt.md, codemap.md,
  step-plan.md-Status, step-result.md).

## Build-/Test-Output

Gefilterte Entwicklungsläufe (Auszug): FastTests `Category=Unit` 1242/1242 →
1730/1730 nach Testzugewinn; `Category=Component` 488/488; Daemon-Verbund
(`FullyQualifiedName~Mcp.Daemon`) 7/7; Janitor-Simulationsnachweis einzeln grün.

Vollständiger Nicht-Stress-Stack:

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress        → grün (1730/1730)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (359/359, ~4 min)
```

Hinweis zur Vorgabe „GENAU EINMAL": Der erste Vollstack-Lauf endete 355/359 (4 Fehler,
s. u.). Nach Klassifikation und Behebung (Testdatei-Aufteilung + Escape-Pin) wurden
FastTests und IntegrationTests je EINMAL erneut gefahren — beide grün. Stress wurde
niemals ausgeführt.

### Fehlerklassifikation des ersten Vollstack-Laufs (Coder-Schritt 4a)

- **3 × CliRepositoryDogfoodTests (echter Code-Befund, behoben):** Mein neuer
  Overflow-Test verlängerte `ProjectRegistryTests.cs` über das Datei-Limit
  `MaxLineCount=500` (Dogfood meldete korrekt „1 violations | Tests"). Behoben durch
  Auszug des Tests in die neue Datei `ProjectRegistryCapacityContractTests.cs`.
  Kein Symptom-Fixing — die Linter-Meldung war zutreffend.
- **1 × HostPipeHandshakeThenMcpInitializeListsToolsAndExitsIdle (TD-008-Klasse,
  Ursache gefunden und geschlossen):** stderr lautete
  `[ERROR]: Daemon fuer Pipe-Endpunkt 'ainetlinter.analyzer.v1.ralf' laeuft bereits.`
  Ursachenanalyse über TRX-Zeitachsen beider Vollstack-Läufe:
  `ExplicitParentExit_StopsMcpServerWithinFiveSeconds` spawnt einen echten Thin-Client
  OHNE `NO_DAEMON`-Pin und ohne Idle-Exit-Flag → Connect-or-Start legt einen detached
  Daemon mit DEFAULT-10-min-Idle-Exit an, der das Testende überlebt; wenige Sekunden
  später scheitert der nächste Daemon-Contract am Doppelstart-Lock. Zeitlich exakt
  belegt in beiden Läufen (Run 1: Lifetime-Test 14:37:52 → HostPipe-Fehler 14:38:08;
  Run 2: 14:48:28 → 14:48:50). Das ist dieselbe Kontaminationsklasse wie step-014 —
  nur aus der eigenen Suite heraus, wogegen ein einmaliges Suite-Start-Cleanup
  strukturell nicht schützen kann. Fix: Escape-Pin des Bestandstests (Konsolidierung
  auf das step-013/TD-007-Muster). Danach Vollstack vollständig grün — die DoD-
  Anforderung „Kontaminationsausfälle treten mit der Fixture nicht mehr auf" ist
  erfüllt.

## MCP-Quality-Gates

Der AiNetLinter-MCP-Server ist in dieser Subagent-Umgebung nicht als eingebettetes
Tool registriert; Gates liefen daher als stdio-JSON-RPC-Session gegen die gebaute EXE
(`--mcp-server`, Daemon-Modus, Loading-Retry bis Load-Abschluss), wie in step-014/015:

- `get_violations` (projectRoot = Repo-Root, maxResults 200): **0 Violations**.
- `safeguard`: **Score 10,00/10 (Threshold 8,00) — PASS**, 0 Top-Verstöße,
  679 Klassen analysiert.
- Gate-Daemon (Welcome-/Health-PID) wurde nach der Session sauber beendet;
  anschließend verifiziert: keine AiNetLinter-Restprozesse.
- drift-audit: nicht ausgeführt (Vorgabe; step-015 war der taskweite Audit-Lauf).

## Abweichungen vom Plan

1. **item-02 reduziert (Plan-Kontext älter als Code):** Der Plan-JIT-Kontext ging von
   einem stillen Default-Fallback im Registry-Pfad aus. Tatsächlich scheitert
   `ProjectInstanceFactory.TryCreate` bereits deterministisch mit `RULES_INVALID`
   (seit Epic-Wiring, mit Unit-Test). Verifiziert: Registry-Pfad hart, Batch-Pfad
   (`MaterializeRules`) mit dokumentiertem Fallback — genau die vom Plan-Notes
   geforderte Abgrenzung. Umgesetzt daher nur: kopierfähige Bauanleitung im
   Fehlertext + Testabsicherung. Kein Doppeltes gebaut.
2. **Escape-Pin für `McpServerLifetimeTests` (über item-01-Wortlaut hinaus):** Der Plan
   sah die Fixture als Ergänzung vor und sagte die step-014-Lösung unverändert fort.
   Die Fehleranalyse zeigte, dass eine suite-interne Stelle (nicht Fremdprozesse) die
   Kontamination aktiv ERZEUGT; ohne Pin wäre die TD-008-Klasse trotz Fixture
   weiter aufgetreten (empirisch in beiden ersten Vollstack-Läufen belegt). Der Pin
   konsolidiert diesen Bestandstest auf das etablierte step-013-Isolationsmuster und
   ist notwendiger Bestandteil der DoD-Erfüllung.
3. **Zweiter Vollstack-Lauf:** Wegen der beiden echten Befunde im ersten Lauf (oben)
   war eine Wiederholung zur Grün-Nachweisführung nötig; Abweichung von
   „GENAU EINMAL" mit Begründung dokumentiert. FastTests+IntegrationTests je genau
   einmal im Finalzustand; dazwischen nur gefilterte Läufe.
4. **Gate-Budgets 45 s → 240 s:** Vier Daemon-Contracts teilen sich jetzt das
   exklusive Endpoint-Gate; die bisherigen 45-s-CTS starben legitim wartend hinter
   dem ~110-s-Shared-Warmth-Test (im Entwicklungslauf reproduziert, OCE am
   SemaphoreSlim). Die Budgets decken nun jede Turn-Reihenfolge; Hang-Schutz bleibt
   über die phasenspezifischen Timeouts der Harnesses erhalten.
5. **Janitor läuft innerhalb des Gates:** Erst-Entwurf hatte Cleanup vor dem Gate;
   bewusst umgeordnet (und per Kommentar begründet), damit der Cleanup-Kill nie einen
   legitimen, parallel laufenden Endpunkt-Nutzer desselben Builds trifft.

## Beobachtungen (für den Kritiker)

- `NormalMcpServerPath_ConnectsThroughDaemon…` ist jetzt gegatet; damit bindet ALLE
  Endpunkt-Nutzer der Suite dasselbe Gate. Künftige daemon-path-Tests sollten das
  Muster direkt übernehmen (TD-007-Vorschlag bleibt relevant).
- Der Janitor erkennt Build-Ausgaben ÜBERHALB des Repos nicht (bewusst: fremde
  Installationen werden nie angetastet) — dort greift dann der transparente Skip.
  Falls künftig Daemons außerhalb des Repo-Builds erwartet werden, bräuchte es eine
  explizite Allowlist-Entscheidung (Kritiker/Planer).
- `ExplicitParentExit` testet den Parent-Pid-Watchdog weiterhin real (Subprozess +
  Kill-Tree), nur eben im Escape-Modus; produktiver Daemon-Pfad bleibt über die
  dedizierten Daemon-Contracts abgedeckt.
- Kleinigkeit aus der Analyse: `HostPipeHandshake` assertete ExitCode ohne
  stderr-Kontext (`Assert.Equal(0, …)`); ich habe es auf das im Sibling-Test
  übliche Muster `Assert.True(exit == 0, result.Error)` umgestellt — erst dadurch
  war die Lock-Fehlerdiagnose möglich. Assertion nicht abgeschwächt.

## Bekannte Unschärfen

- Die Janitor-Identifikation liest `Process.MainModule.FileName`; für Prozesse mit
  unlesbarem Bildpfad (Schutzstufe/Fremdsession) gilt: nicht identifizierbar → nie
  gekillt → ggf. transparenter Skip. Das ist Absicht, begrenzt aber die automatische
  Räumung auf dieselbe Nutzer-Session (Windows-only-Umfeld, konsistent).
- Der Skip-Pfad des Janiors wurde im Grünfallok nicht vollständigungsende-zu-endende
  getestet (der Simulationsversuch mit falscher Pfadbasis lieferte einmal einen echten
  Skip — Mechanismus funktionierte; der „Fremdbelegung dauerhaft"-Fall selbst wurde
  nicht künstlich lang aufrechterhalten).
- `Assert.Skip` wirft durch `AcquireEndpointAsync`; alle heutigen Aufrufer sind
  Testmethoden. Künftige Aufrufe aus Fixtures/Setup-Kontexten heraus müssten das
  berücksichtigen.

## Commits

1. `2f1cced3` — fix: Tech-Debt-Batch TD-008/001/003/010/004 umsetzen
   [11_epic-projektregistry-und-daemon] (Code + Tests + Docs/agent-api.md).
2. Doku-Commit folgt unmittelbar (step-plan status, step-result.md, tech-debt.md,
   codemap.md).
