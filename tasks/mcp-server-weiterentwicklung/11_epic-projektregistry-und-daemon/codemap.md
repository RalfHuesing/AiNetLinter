---
task: 11_epic-projektregistry-und-daemon
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-23T14:32:00+02:00
---

# CodeMap: 11_epic-projektregistry-und-daemon

Task-scoped Landkarte — existiert nur für diesen Task, wird mit
dem Task-Verzeichnis gelöscht, kein projektweites Artefakt. Enthält **nur**, was
für diesen Task relevant ist (Module/Dateien/Bereiche, die ein Step
tatsächlich berührt hat oder für die Planung des nächsten Steps
gebraucht wird) — kein Anspruch auf vollständige Projektabdeckung.

**Pointer-Prinzip — wie Regel-Index (`roadmap.md`) und Tech-Debt-Index
(`tech-debt.md`):** Jeder Eintrag ist Ort + **ein Satz**, was dort ist
und wozu es für diesen Task relevant ist — keine Verhaltensbeschreibung,
kein „wie funktioniert das im Detail". Wer mehr wissen muss, liest die
Datei selbst nach — das ersetzt die Map nie, sie beschleunigt nur das Finden.

**Warum das trotzdem verlässlich bleibt:** Der gesamte Loop läuft strikt
seriell — genau ein Subagent gleichzeitig
(`.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` §6). Zwischen einem
Coder-Update und dem nächsten Lesezugriff kann sich am Code strukturell
nichts geändert haben, was hier nicht auch eingetragen wurde.
**Schritt 2 im Step-Modus des Planers („tatsächlichen Projektzustand lesen",
spec §7.2) bleibt trotzdem Pflicht** — die Map sagt *wo* nachschauen,
ersetzt nie das Nachschauen selbst.

## Pflege — wer trägt wann ein

- **Planer, Roadmap-Modus (einmalig):** befüllt die Map initial aus dem
  Grobüberblick über den Bestandscode (unten, Status „initial").
- **Coder (jeder Step):** ergänzt/aktualisiert Einträge für tatsächlich
  angelegte oder geänderte Module, **vor** dem Doku-Commit
  (`../skills/coder/SKILL.md` Schritt 6a) — Status-Vermerke
  „(zuletzt: step-NNN)" fortschreiben.
- **Planer, Step-Modus (jeder Step):** liest die Map vor dem Planen,
  ergänzt neue Bereiche, die er beim Lesen des Ist-Zustands entdeckt.
  Zusätzlich Grundlage für den Anti-Loop-Check (siehe unten).
- **Kritiker:** prüft stichprobenartig, ob die Map dem tatsächlichen Diff
  entspricht — schreibt selbst nur bei offensichtlicher Lücke/Fehler nach.

## Anti-Loop-Nutzen

Bevor der Planer im Step-Modus einen neuen Step plant, gleicht er sein
Vorhaben gegen die hier verzeichneten, bereits getroffenen Entscheidungen
ab. Widerspricht der neue Plan erkennbar einem hier festgehaltenen,
bereits umgesetzten Stand: entweder im neuen Step-Plan explizit als
Erweiterung begründen, oder den alten Eintrag hier als „obsolet —
<Grund>" markieren (nicht löschen) — nie stillschweigend widersprechen.

## Karte

Initialbefüllung aus dem Grobüberblick (Roadmap-Modus, 2026-08-23; alle
Einträge bis auf weiteres „(zuletzt: initial)"):

### Produktionscode — direkt betroffen

- **`src/AiNetLinter/Program.cs`** — Einstieg: CLI-Parsing → `LinterArgs` →
  Routing (Batch vs. Standalone-Commands); hier erhält der MCP-Zweig später
  das `--daemon-start`-Routing (Konzept-Strukturbaum: ÄNDERN). (zuletzt: initial)
- **`src/AiNetLinter/Cli/`** (`CliOptions.cs`, `CliOptionFactory.cs`,
  `CliCommandBuilder.cs`, `LinterArgs.cs`) — System.CommandLine-Argumentparsing;
  Anker für den harten Cut (`--path`/`--config` im MCP-Zweig als harter Fehler
  ablehnen) und die neuen statischen Flags `--mcp-project-ttl-minutes`,
  `--mcp-max-projects`, `--mcp-daemon-idle-exit-minutes`. (zuletzt: initial)
- **`src/AiNetLinter/Commands/McpServerCommand.cs`** — statischer MCP-Einstieg
  (`RunAsync` hält heute DIE eine `McpCodeGraphServer`-Instanz); `ResolveConfig`/
  `ResolveMaxLineCount` delegieren seit step-001 auf `ProjectInstanceFactory.MaterializeRules`
  (geteilter Kern mit dem Registry-Pfad), während Pfadauflösung inkl.
  `TryResolveRulesJsonPath` und Solution-Auto-Suche batch-seitig hier bleiben (F8);
  hier landet ProjectRegistry + Eviction-Timer. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`** — instanzbasierter Analysekern
  (F1, kein statisches Mutable-State): Options-Record-Konstruktor, Config-Hot-Swap
  (`ReloadConfig`), `ReloadSolutionAsync`, `DisposeAsync` mit LoadTask-Abbruch,
  Health-Zähler (`Uptime`/`RefreshCount`/`LastStalenessStats`) — wird je Projektkey
  instanziiert, nicht umgebaut. `ServerLoadState` (`Mcp/ServerLoadState.cs`: Loading/Loaded/
  LoadFailed) und die `_loadTask`-Adoption sind der Zustands-/Dedupe-Anker, den ProjectRegistry
  liest (Review 1: Dedupe im Instanzmuster, nie in der Registry). (zuletzt: initial)
- **`src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs`** — ausgelagerte
  Staleness-/Refresh-Helper (`Run`/`CacheInitialFileState`/`SweepForNewFiles`),
  wegen der Klassengrenzen aus dem Kern extrahiert (F7); Ansatzpunkt für den
  zweistufigen Zustandsvertrag (last-good + `[WARN]`). (zuletzt: initial)
- **`src/AiNetLinter/Mcp/ServerStalenessStats.cs`** — Staleness-Zähler einer
  Serverinstanz; geht in die pro-Key-Health-Aggregation von Epic A ein.
  (zuletzt: initial)
- **`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`** — bäckt heute die eine
  Serverinstanz per Closure in alle Tool-Lambdas und die Resource-Collection (F2);
  wird auf `Create(ProjectRegistry registry)` umgestellt. (zuletzt: initial)
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — zentraler Result-Builder für Tool-/Resource-
  Antworten (`Error(code, message, context, hint)`, `Recoverable`, `Loading`); Anker für die
  A.5-Fehlerverträge des Epics A (Codes künftig zentral in `Mcp/Projects/ProjectErrorCodes`).
  Verifiziert per Skeleton 2026-08-23. (zuletzt: initial)
- **`src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`** — Options-Record (`required
  Catalog/Console/Config`, `LoadFunc`) mit bestehender `From(Parameters)`-Fabrik;
  Materialisierungsziel von `ProjectInstanceFactory` (Review 3) — kein zweites Options-Muster
  erfinden. `LoadFunc: Func<CancellationToken, Task<SourceFileCatalog?>>?` ist der Hintergrund-
  Load-Hook des Registry-MISS-Pfads; produktiv setzt ihn bisher nur `McpServerCommand.RunAsync`
  (wrapt `TryLoadSolutionAsync`). Verifiziert per Skeleton/find_references 2026-08-23. (zuletzt: initial)
- **`src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs`** — fluenter Builder rund um
  `McpServerOptions` (Name/Version/Instructions/Tool-/ResourceCollections).
  (zuletzt: initial)
- **`src/AiNetLinter/Mcp/ServerInstructions.cs`** — Single-Source-of-Truth des
  initialize-Handshake-Texts (F6) mit hartem Budget `MaxUtf8Bytes = 2557`;
  erhält den komprimierten projectRoot-/Definitionsdatei-Vertragsblock.
  (zuletzt: initial)
- **`src/AiNetLinter/Mcp/*ToolRegistrations.cs` + `OverviewResourceRegistration.cs`**
  (`SymbolGraph…`, `Analysis…`, `FileStructure…`, `SymbolBody…`,
  `DuplicateDetection…`, `ServerMaintenance…`) — die 7 Wiring-Stellen (F3):
  Delegate-Closures `(args…) => XxxTool.ExecuteAsync(mcpState, …)`; werden
  mechanisch auf `async (string projectRoot, …) { using var lease =
  _registry.Lease(projectRoot); return await …(lease.Server, …); }` umgestellt.
  (zuletzt: initial)
- **`src/AiNetLinter/Mcp/Tools/**`** — Tool-Implementierungen mit statischer
  `ExecuteAsync(mcpState, …)`-Signatur; erster Parameter wird zum Lease-Server
  (26× mechanische Anpassung, `projectRoot` erscheint automatisch required im
  JSON-Schema). (zuletzt: initial)
- **`src/AiNetLinter/Mcp/Lifetime/`** (`McpServerLifetime.cs`,
  `ParentProcessWatchdog`) — bestehender `--parent-pid`-Reaper; bleibt für
  Thin-Client/Batch erhalten, wird im Daemon bewusst NICHT genutzt (B.2/B.3).
  (zuletzt: initial)
- **`src/AiNetLinter/Configuration/ConfigLoader.cs`** — rules.json-Ladepipeline,
  heute von `McpServerCommand` aufgerufen; wird als gemeinsame Materialisierung in
  `ProjectInstanceFactory` gezogen (geteilt Batch + Registry, Review 3).
  (zuletzt: initial)
- **`src/AiNetLinter/Mcp/Projects/`** — seit step-001 angelegt: `ProjectDefinition`
  (Record, absolut + existenzgeprüft via Loader), `ProjectDefinitionLoader`
  (Pflichtfelder, Anker Definitionsdatei, kein Fallback, Fehlerverträge mit
  Template-Text), `ProjectDefinitionLoadResult` (flacher Result-Record),
  `ProjectErrorCodes` (alle sechs A.5-Codes; `PROJECT_ROOT_*` erst im Wiring aktiv)
  und `ProjectInstanceFactory` (`MaterializeRules` = geteilter Config-Kern Batch +
  Registry, `Create(Definition)` → Options via `From(...)`). `ProjectEntry`,
  `ProjectLease`, `ProjectRegistry` folgen in späteren Steps. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Daemon/`** — existiert noch NICHT (Epic B); verbindliche
  Zielstruktur steht im Konzept-Strukturbaum. (zuletzt: initial)

### Tests

- **`src/AiNetLinter.FastTests/Fixtures/McpInMemoryTestContext.cs`** — TestKit baut
  In-Memory-MCP-Server per Options-Record ohne Prozessstart (F4); Basis für die
  neuen Registry-/Daemon-Unit-Tests. (zuletzt: initial)
- **`src/AiNetLinter.FastTests/Mcp/**`** — bestehende Unit-/Component-Tests zu
  Command/Factory/Registrations/Overview (u. a. `McpServerOptionsFactoryTests`,
  `SymbolGraphToolRegistrationsTests`, `OverviewResourceRegistrationTests`,
  LoadingState-/CacheBypass-/CallLog-Tests); seit step-001 dazu
  `FastTests/Mcp/Projects/` mit `ProjectDefinitionLoaderTests` (Ankerregel,
  Fehlerverträge inkl. Template-Text-Assertion, Kein-Fallback) und
  `ProjectInstanceFactoryTests` (Materialisierung + Gleichheit mit der
  Batch-Pipeline). Contract-Tests (`projectRoot` required) und
  `FastTests/Mcp/Daemon/` kommen später dazu. (zuletzt: step-001)
- **`src/AiNetLinter.IntegrationTests/Mcp/**`** — Subprozess-/JSON-RPC-Integrationstests
  (u. a. `McpHandshakeToolRegistrationTests`, `McpServerCommandContractTests`,
  Lifetime-/Staleness-/Framing-/E2E-Tests); die sparsamen Daemon-Zwei-Prozess-E2E
  (B.6) ergänzen diesen Bereich. (zuletzt: initial)
- **`src/AiNetLinter.TestKit/**`** — zentrale Test-Infrastruktur; Pflicht
  `TestTempDirectory` statt OS-Temp (Richtlinien §4) gilt auch für die
  Definitionsdatei-Fixtures. (zuletzt: initial)

### Doku-/Sync-Ziele (Konzept-Doku-Tabelle)

- **`Docs/agent-api.md`** · **`Docs/configuration.md`** · **`Docs/integration.md`** ·
  **`Docs/ROADMAP.md`** · **`README.md`** — Doku-Sammelpflicht-Ziele: Init-Vertrag +
  Definitionsdatei-Referenz, CLI-Flagänderungen, Registrierungsbeispiele/Daemon-Abschnitt,
  Meilensteine, neues Nutzungsmodell; **`AGENTS.md`** (Repo-Root) erhält den Abschnitt
  „AiNetLinter-MCP: Initialisierung“. (zuletzt: initial)
- **`.mcp.json` (Repo-Root) + Hermes `config.yaml`** — eigene MCP-Registrierungen;
  Epic-A-DoD reduziert sie auf `command + --mcp-server`, Epic-B-DoD prüft sie live
  auf Daemon-Modus. (zuletzt: initial)
- **`.agents/rules/AiNetLinter.mdc`** — auto-generiertes Sync-Target
  (`dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`), sobald
  Regel-/CLI-Texte betroffen sind. (zuletzt: initial)
- **`AiNetLinter.slnx` / `rules.json` (Repo-Root)** — Solution + Regelwerk dieses
  Repos; das eigene `ainetlinter.project.json` (Epic-A-Migration) zeigt genau darauf.
  (zuletzt: initial)
- **`90_bewusst-nicht-umsetzen/Konzept.md`** — Entscheidungsregister; erhält die
  Wiederöffnungsvermerke §D.4 (Epic A) und §C.5 (Epic B). (zuletzt: initial)
