---
status: done (pending audit)
type: step-plan
task: speedup-tests
step: 026
corrects: step-025
title: "Korrektur: Runtime-sauberer MCP-Vertragsschnitt und vollständiger Hostabschluss"
epic: EPIC-6
estimated_risk: high
step_type: batch
items:
  - id: item-01
    title: "MSBuild-ladenden Command-Hostvertrag aus FastTests entfernen"
    source: "TestResults/step025-fast-target.trx / step-025 item-01"
  - id: item-02
    title: "Alle 121 historischen Verträge eindeutig wiederherstellen"
    source: "HEAD-Vertragsinventur / step-025 Definition of Done"
  - id: item-03
    title: "Retry-, Framing-, Git-, Refresh- und Hostbudget-Verträge abschließen"
    source: "step-025 items 02-04 / TD-001"
  - id: item-04
    title: "Guards, Ledger, Dokumentation und Debt-Evidenz abschließen"
    source: "step-025 item-05 / TD-006"
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13T23:45:00+02:00
related_to:
  - step-025/step-plan.md
---

# Step 026: Korrektur: Runtime-sauberer MCP-Vertragsschnitt und vollständiger Hostabschluss

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-6` aus `roadmap.md` — erster Mini-MCP-Hostschnitt mit 21 historischen Klassen
  und 121 Verträgen; Step 025 bleibt wegen des Fast-Runtime-Dependency-Gates und offener
  Abschlussarbeit auf `issues`.
- **Korrigiert:** `step-025`; dies ist die erste Korrektur der Kette und liegt unter dem
  `max_fix_rounds_per_step: 6`-Budget. Der frühere Hinweis auf drei bezog sich auf drei
  Testwiederholungen, nicht auf sechs ausgeschöpfte Korrektur-Steps.
- **Konzept-Referenz:** `konzept.md` Leitplanken 1/3/5/6/7/8 und Step-025-Items 01-05.

## Aktueller Projektzustand (JIT-Kontext)

- `dotnet build` ist im uncommittierten Step-025-Stand grün. Es gibt noch keinen Step-Result,
  kein Review und keinen Commit; Integration-, Rest-, Ledger- und Dokumentationsgates sind offen.
- `TestResults/step025-fast-target.trx` enthält **49 ausgeführte und 49 bestandene Methoden**.
  Der Lauf ist trotzdem rot, weil `FastTestsRuntimeDependencyGuardFixture.Dispose` nach dem
  Testhost `Microsoft.Build.Framework`, `Microsoft.Build.Locator` und
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` geladen vorfindet. Der statische Guard ist grün,
  weil FastTests/TestKit diese Assemblies nicht direkt referenzieren; der Runtime-Guard entdeckt
  korrekt die transitive Ausführung über die Produktassembly. Beide Guards bleiben unverändert
  streng.
- `TestResults/step025-fast-partial.trx` belegt 31/31 grüne Verträge ohne Cleanup-Fehler,
  darunter alle drei `McpServerCommandLoadingStateTests`, alle neun derzeit schnellen
  `GetImpactToolTests`, Options/Registration/Constructor/Cache-Bypass. Die Assembly-Fixture
  `PreparedSolutionFixture` initialisiert nur ein leeres lazy Dictionary und materialisiert ohne
  Konsumenten keinen Workspace; der Runtime-Guard ist bei Assembly-Initialisierung sauber.
- Die verbleibende Ursache ist quellseitig eindeutig:
  `McpServerCommandTests.ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault`
  ruft trotz vorab abgebrochenem Transport-Token `McpServerCommand.RunAsync` auf. `RunAsync`
  konstruiert `McpCodeGraphServer` mit einer `LoadFunc`; dessen Konstruktor startet sie per
  `Task.Run` mit `CancellationToken.None`. Die Callchain läuft über
  `TryLoadSolutionAsync` → `SourceFileCatalog.LoadAsync` →
  `SourceFileCatalogLoader.LoadAsync` → `RegisterMSBuild`/`MSBuildWorkspace.Create` und lädt
  exakt die drei vom Cleanup gemeldeten Assemblies. Die 14 `McpCallLogTests` und neun statischen
  `McpServerCommandCallLogTests` verwenden nur Datei-/JSON-/Pfadlogik und sind nicht die Ursache.
- Es ist **keine statische Loaderkopplung der gemeinsamen Produktionsklasse belegt**: Der
  Loaderpfad wird nur durch `RunAsync` ausgeführt. Deshalb zunächst ausschließlich den echten
  Hostvertrag nach Integration verschieben. Nur falls ein anschließendes Pure-Command-Ursachegate
  ohne `RunAsync` dieselben Assemblies lädt, darf ein minimaler produktiver Split der bereits
  fachlich getrennten Resolution-/Config-/CallLog-Helfer vom Hostadapter geplant und umgesetzt
  werden; ohne diesen Beleg bleibt `McpServerCommand` unverändert.
- Die HEAD-Inventur hat 121 historische `[Fact]`-Verträge. Im aktuellen Zielstand sind davon 15
  nicht gleichwertig repräsentiert: zwölf aus `McpServerCommandTests`, der Textvertrag
  `ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite` und zwei
  `McpTestClientRetryOptions`-Verträge. Der neue Gate-Test ersetzt keinen historischen Retry-
  Vertrag. Die fehlende Step-025-Legacy-Baseline kann nach dem bereits erfolgten uncommittierten
  Move nicht ehrlich als Pre-Move-Lauf nachgeholt werden; als unveränderliche Ausgangsevidenz
  dienen HEAD-Quellen und ihre 121er-Inventur. Im Result muss diese Evidenzlücke ausdrücklich
  stehen, nicht als nachträgliche Baseline umetikettiert werden.

## Intention

Step 026 korrigiert den Projektgrenzfehler ursächlich: Nur garantiert MSBuild- und prozessfreie
Logik bleibt in FastTests, während echte Command-Host-, stdio-, Git-, Retry-, Framing- und
Refresh-Verträge in IntegrationTests laufen. Gleichzeitig wird der angefangene Step 025 vollständig
zu Ende geführt: alle 121 historischen Verträge erhalten eine nachvollziehbare Zielzuordnung,
Hostownership und Max-2-Lebensdauerbudget werden geschlossen, und alle engen Gates laufen ohne
Voll-, Dogfood-, Performance- oder Stressprofil.

## Konkrete Änderungen

### item-01: MSBuild-ladenden Command-Hostvertrag aus FastTests entfernen

- `McpServerCommandTests` fachlich teilen:
  - In FastTests bleiben die zehn reinen `ResolveSolutionPathOrError_*`, `ResolveMaxLineCount_*`
    und `ResolveConfig_*`-Verträge, die ausschließlich vorbereitete Werte bzw. isolierte kleine
    Dateien lesen und **nicht** `RunAsync`, `TryLoadSolutionAsync`, `SourceFileCatalog.LoadAsync`,
    `Process.Start` oder einen echten Transport aufrufen.
  - `ResolveConfig_NoExplicitConfigPath_NoRulesJsonFound_UsesDefault` wird als echter
    Command-Hostvertrag nach Integration verschoben, weil seine `[WARN]`-Assertion nur durch
    `RunAsync` erreicht wird und damit absichtlich den Hintergrund-MSBuild-Load startet.
  - Die zwölf bereits aus der Klasse entfernten Pfad-/Host-/Toolverträge werden ebenfalls in
    Integration wiederhergestellt: `TryLoadSolutionAsync_BrokenSlnx_LogsWarningWithoutThrowing`
    sowie die elf `RunAsync_ValidFixture_*`-Methoden für Toolliste, Hotspots, IndexScope,
    Violations, SearchPattern, FindSymbol, FindReferences, beide GetImpact-Zweige,
    FileSkeleton und TypeHierarchy.
- Vor jeder Produktionsänderung ein kleines Pure-Command-Gate aus genau den zehn Fast-Methoden
  plus statischem und Runtime-Dependency-Guard ausführen. Ist es cleanup-grün, ist kein
  Produktionssplit erlaubt. Lädt es wider Erwarten MSBuild, im Step-Result die konkrete Methode
  und Assembly-Callchain belegen und nur dann die bereits vorhandenen reinen Helper aus
  `McpServerCommand` in einen schmalen internen Policy/Resolution-Typ extrahieren; Signaturen und
  Hostverhalten bleiben unverändert, keine Loader-Abstraktion und kein Guard-Workaround.
- `McpCallLogTests`, `McpServerCommandCallLogTests`, die drei direkten Loading-State-Verträge und
  die neun in-memory/no-repository `GetImpactToolTests` bleiben Fast. `NoGitRepository` endet in
  `GitRepositoryLocator.FindRoot` vor `Process.Start`; alle echten Git-Repo-/Diff-Pfade bleiben
  Integration.

### item-02: Alle 121 historischen Verträge eindeutig wiederherstellen

- Eine prüfbare 121er-Matrix im Step-Result und in den 21 Ledger-Evidenzfeldern führen:
  historischer vollqualifizierter Methodenname → Zielprojekt → Zielmethode → Kategorie →
  Infrastrukturgrenze. Zusätzliche Host-/Gate-Selbsttests zählen separat und dürfen keinen der
  121 Verträge ersetzen.
- Zielverteilung der drei gesplitteten Familien:
  - `McpServerCommandTests`: zehn reine Fast-Verträge; 13 Integration-Verträge (zwölf derzeit
    entfernte plus der MSBuild-ladende Warn-/RunAsync-Vertrag), zusammen weiterhin 23.
  - `GetImpactToolTests`: neun Fast-Verträge für Solution-/Symbol-/Input-/No-Repo-Logik; fünf
    Integration-Verträge für Git-Diff und echten CompileError-MSBuild-Load. Den fehlenden
    `ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite` wiederherstellen; Text-
    und StructuredContent-Vertrag nicht still zu einer schwächeren Assertion verschmelzen.
  - `McpTestClientRetryTests`: alle drei historischen Verträge erhalten — realer erschöpfter
    Connect-Retry in Integration sowie die beiden garantiert MSBuild-/prozessfreien Default-/
    Override-Wertverträge der Retry-Optionen in Fast. Die neuen Max-2-Gate-Verträge sind
    zusätzliche Infrastrukturtests.
- Damit ergibt sich für die historischen Verträge die explizite Zielsumme **66 Fast / 55
  Integration = 121**. Die 121er-Matrix muss diese Summe aus historischen Methoden herleiten;
  zusätzliche Host-/Gate-Tests werden außerhalb der Summe ausgewiesen.
- Für die übrigen 18 historischen Klassen die heutigen Methodennamen und Assertions gegen HEAD
  abgleichen. Konsolidierung ist nur bei tatsächlich identischem Vertrag zulässig und muss beide
  historischen Namen in der Matrix nennen; keine bloße Fallzahlkompensation durch neue Gate-Tests.
- Legacy-Quellen bleiben physisch entfernt, die bewusst ausgeschlossenen CLI-/Dogfood-/
  Performance-/Stress-Klassen bleiben unverändert `pending` und baubar.

### item-03: Retry-, Framing-, Git-, Refresh- und Hostbudget-Verträge abschließen

- `McpProcessHost` zum einzigen Owner von Fixture, vollständigem Max-2-Lifetime-Lease,
  Transport/Client, Loading-/Connect-Retry und Disposal machen. Bei Erfolg, Startfehler,
  Cancellation und Testfehler müssen Client/Transport, eigener Prozessbaum, ausstehende Tasks,
  Fixture und Permit in deterministischer Reihenfolge freigegeben werden. Keine globale
  Collection-Serialisierung und keine Reset-API.
- Den historischen echten Retry-Fehlerpfad mit einer schmal injizierbaren test-infrastrukturellen
  Connect-Attempt-Funktion oder gleichwertiger Beobachtung wiederherstellen: exakt konfigurierte
  Versuche/Backoffwerte, abschließende `InvalidOperationException`, keine echte Sleep-Annahme.
  Loading-Retry aus `ErrorHandling` in den gemeinsamen Host ziehen; Optionsverträge erhalten.
- `ReadOnlyMcpHostFixture` bleibt lazy, thread-sicher und ausschließlich read-only. Selbsttests
  müssen Einmalmaterialisierung, parallele Objektidentität/Toolcalls, normales Disposal und
  Permit-Rückgabe nach Initialisierungsfehler belegen. Mutierende Git-/Refresh-/Error-/Framing-
  Verträge verwenden frische exklusive Hosts und isolierte Workspaces.
- Die fünf Git-/CompileError-`GetImpact`-Verträge, Git-Impact-E2E und Staleness/Refresh auf
  besitzende Fixture-Hosts bringen. `GitImpactMiniFixtureWorkspace` muss Commit, uncommitted
  Mutation und Windows-ReadOnly-Cleanup abdecken; jeder gestartete Git-/MCP-Prozess wird awaited.
- Die drei Raw-stdio-Framing-Verträge vollständig erhalten. Writer, stdout und stderr parallel
  drainen, bounded graceful shutdown versuchen und nur den eigenen Prozessbaum als Fallback
  beenden. TD-001 nicht durch höhere Pauschaltimeouts, weniger Assertions oder serielle
  Collections maskieren; denselben Framing-Filter mehrfach zusammen mit begrenzten exklusiven
  Mini-MCP-Filtern ausführen und JSON-RPC-Reinheit belegen.
- `McpProcessLifetimeGate` mit Kapazität 2 vollständig testen: zwei aktive Leases, wartender
  dritter, Freigabe nach Disposal, Startfehler, Cancellation und idempotentes Dispose. Der lazy
  read-only Host belegt höchstens einen Slot; parallel lebt höchstens ein exklusiver MCP-Host.
  Runnerwerte bleiben `parallelizeAssembly: false`, `parallelizeTestCollections: true`,
  `maxParallelThreads: 4`.

### item-04: Guards, Ledger, Dokumentation und Debt-Evidenz abschließen

- Statischen und Runtime-Fast-Dependency-Guard unverändert streng halten; keine Allowlist,
  Filterausnahme, spätere Guard-Initialisierung, eigener Testhost oder Assembly-Unload-Maske.
  Den Kategorie-Trait-Inspector xUnit-frei im TestKit fertigstellen und beide Kategorieguards
  darauf belassen; Kategorien müssen projektkonform sein (Fast `Unit`/`Component`, Integration
  `Integration`). Falsch gesetzte Methoden-Traits entfernen statt den Klassentrait zu verdoppeln.
- Einen Integration-Runner-/Process-Callsiteguard fertigstellen: Runnerwerte exakt prüfen,
  echte `StdioClientTransport`-/`Process.Start`-Callsites dieser Kohorte nur in besitzenden Hosts,
  keine `SymbolGraphMcp`-Collection, Max-2-Lifetimebudget und alle Prozessklassen ausschließlich
  in IntegrationTests.
- Alle 21 Ledgerzeilen auf `migrated`/`consolidated` setzen, beide Zielorte gesplitteter Klassen
  nennen und Fall-/Risiko-/Erfolgs-/Negativ-/Fehler-/Evidenznotiz gegen die 121er-Matrix pflegen.
  Ledgerguard muss exakt 53 `pending` melden; Legacy-Deklarationen der 21 Klassen fehlen, bewusst
  ausgeschlossene Reste bleiben vorhanden.
- `tech-debt.md` TD-006 erst nach grünen Kategorieguards als geschlossen markieren. TD-001 erst
  nach dem wiederholten Framing-Lastgate mit dokumentierter TRX-/PID-Evidenz schließen; andernfalls
  offen lassen und Step 026 nicht als erledigt melden. TD-008/TD-010 bleiben offen.
- `tasks/speedup-tests/codemap.md`, `roadmap.md`, `step-025/step-plan.md`/Step-Result und
  `step-026/step-result.md` auf den realen Schnitt aktualisieren. Öffentliche Produktdokumente nur
  ändern, falls der endgültige Produktionscode tatsächlich öffentliches CLI-/MCP-Verhalten
  ändert; reine Testprojektverschiebungen nicht als Feature dokumentieren. Die fehlende Pre-Move-
  Legacy-Baseline ausdrücklich als nicht erhobene Evidenz festhalten; keine nachträgliche
  Zielmessung als Vorhermessung ausgeben.
- Genau einen kohärenten deutschen Conventional Commit mit `[speedup-tests]` erst nach allen
  Gates erstellen; kein Amend, Rebase oder Push.

## Tests

- [ ] **Ursachegate A — Pure Command:** zehn reine Fast-`McpServerCommand`-Methoden +
  `FastTestsDependencyGuardTests` im selben Testhost, eigene TRX; kein Assembly-Cleanup-Fehler.
- [ ] **Ursachegate B — CallLog:** 14 `McpCallLogTests` + neun
  `McpServerCommandCallLogTests` + beide Dependency-Guards im selben Testhost; cleanup-grün.
- [ ] **Ursachegate C — direkter Hostzustand:** drei Loading-State- und neun schnelle
  `GetImpactToolTests` + Runtime-Guard; cleanup-grün. Das vorhandene 31/31-Artefakt ist Diagnose,
  der finale Lauf wird nach dem Schnitt neu erzeugt.
- [ ] **Vollständiges Fast-Zielgate:** alle Fast-Zielklassen der 21er-Kohorte, Kategorieguard,
  statischer und Runtime-Dependency-Guard in **einem** Testhost; alle Methoden grün und kein
  Assembly-Cleanup-Fehler.
- [ ] **Vertragszählung:** maschinenlesbarer Vergleich HEAD-Inventur ↔ Zielinventur belegt alle
  21 Klassen und exakt 121 historische Methodenzuordnungen; zusätzliche Hosttests separat.
- [ ] **Read-only-Integration-Ziel:** Host-Selbsttests, Handshake, AllTools, FindReferences,
  FindSymbol, Symbol-Impact, MissHint und die wiederhergestellten read-only Command-Smokes in
  einem Testhost; genau eine lazy Hostinstanz, keine Collection-Serialisierung.
- [ ] **Exclusive-Integration-Ziel:** Ambiguity, ErrorHandling, echter Connect-Retry, fünf
  Git-/CompileError-GetImpact-Verträge, Git-E2E, Staleness und Framing; Max-2-Telemetrie und
  bounded Hangdiagnose, eigene TRX.
- [ ] **Framing-Wiederholung:** drei Framing-Methoden mehrfach mit begrenztem exklusivem
  Mini-MCP-Hintergrundfilter; alle stdout-Zeilen JSON-RPC, keine TD-001-Signatur.
- [ ] **Guards:** beide Kategorieguards, Fast static/runtime dependency guards,
  Runner-/Process-Callsiteguard, `TestMigrationLedgerConsistencyTests` und
  `LegacyProjectBuildGateTests`; 53 pending.
- [ ] **Prozessleck-Gate:** vor/nach jedem finalen Integration-Filter PID/ParentPID/Commandline
  für eigenen `dotnet test`, `testhost`, `AiNetLinter.exe --mcp-server` und BuildHosts erfassen;
  keine neue zugehörige Prozesskette oder Temp-Fixture bleibt zurück, fremde PIDs unangetastet.
- [ ] `dotnet build` über alle fünf Solution-Projekte, 0 Warnungen/Fehler.
- [ ] `git --no-pager diff --check`.
- [ ] **Nicht ausführen:** kein voller Fast-/Integration-`Category!=Stress`-Lauf, kein Legacy-/
  Solution-Volltest, kein Dogfood-, Performance- oder Stressprofil; insbesondere nicht
  `McpTestClientParallelTests` und nicht der 20-fache MSBuild-Paralleltest.

## Definition of Done

- [ ] Der belegt MSBuild-ladende `RunAsync`-Warnvertrag liegt in Integration; alle verbleibenden
  Fast-Verträge schließen ohne Runtime-Dependency-Cleanup-Fehler ab. Guard nicht abgeschwächt.
- [ ] Alle 121 historischen Verträge sind namentlich und semantisch zugeordnet: Command 10 Fast /
  13 Integration, GetImpact 9 Fast / 5 Integration, Retry drei erhalten; zusätzliche Gate-Tests
  ersetzen keinen historischen Vertrag. Gesamtsumme: 66 Fast / 55 Integration.
- [ ] Retry, Framing, Git, Refresh, read-only Host, vollständiges Max-2-Prozessbudget und Runner-
  Guard erfüllen Ownership-, Cancellation-, Fehler- und Disposal-Verträge ohne Prozessleck.
- [ ] Ledger meldet 53 pending; TD-006 ist evidenzbasiert geschlossen, TD-001 nur bei bestandenem
  Wiederholungsgate; fehlende Pre-Move-Baseline ist ehrlich dokumentiert.
- [ ] Alle oben genannten engen Gates, Build und `git diff --check` sind grün; keine verbotenen
  Voll-/Dogfood-/Performance-/Stressläufe wurden gestartet.
- [ ] Ein Commit, `step-025/step-result.md` und `step-026/step-result.md` sind geschrieben;
  Step 026 steht auf `done (pending audit)`, Step 025 bleibt als via Step 026 korrigiertes
  `issues`-Glied nachvollziehbar.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — Nullable-, Methoden- und
  Testprojektgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln` — PowerShell,
  getrennte TRX-Dateien und Diagnose aus bestehenden Artefakten.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — gezielte Host-/Semaphore-
  Isolation statt globaler Serialisierung; MCP-Nachweise in C#.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — Ursache statt
  Symptomfix, keine Guard-/Assertion-Abschwächung, keine dauerhaften Step-Kommentare im Code.

## Bekannte Ausnahmen

- Eine echte Pre-Move-Laufbaseline für die 21 Legacy-Klassen und die separate Framing-Baseline
  wurden vor dem uncommittierten Move nicht erzeugt. HEAD liefert die statische 121er-
  Vertragsinventur; Laufzeitvergleiche beginnen ehrlich erst mit den vorhandenen Step-025-TRX.
- TD-008 und TD-010 bleiben außerhalb dieses Korrekturscopes offen. Dogfood, Performance, Stress
  und allgemeine CLI-Self-Repo-Verträge bleiben spätere EPIC-6-Schnitte.

## Notes

- Die Runtime-Diagnose ist kein Anlass, `PreparedSolutionFixture`, den Runtime-Guard oder die
  ganze Produktionsklasse umzubauen. Erst das Pure-Command-Gate darf einen minimalen Split
  auslösen; andernfalls genügt die korrekte Testprojektgrenze.
- Ein grüner Methoden-Zähler bei rotem Assembly-Cleanup ist ein roter Lauf. TRX-Outcome,
  `RunInfo` und Cleanup-Ausgabe müssen gemeinsam ausgewertet werden.
