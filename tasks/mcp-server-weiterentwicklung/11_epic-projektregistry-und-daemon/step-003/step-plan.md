---
status: open
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 003               # flach, Task-weite Sequenz — auch Korrekturen liegen hier, nie in einem Unterordner
corrects: null             # <null | step-NNN> — nur gesetzt, wenn dieser Step eine Korrektur ist
title: "MCP-Wiring auf die Projektregistry: Tool-Leases, harter Cut, Health-/Reload-/Overview-Vertrag"
epic: EPIC-A          # Bezug zum Epic in roadmap.md, dem dieser Step zuarbeitet
estimated_risk: high  # breitester Schnitt des Epics: 7 Wiring-Stellen + Komposition in RunAsync + sichtbarer Vertragswechsel für Clients
step_type: single  # single (Default) | batch — siehe ../spec.md §10.6
items: []  # nur bei step_type: batch
created_by: planer  # planer | orchestrator
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
created_at: 2026-08-23T15:58:00+02:00
related_to: ["step-001", "step-002", "step-002/step-review.md"]  # Pointer, keine Inhaltsangabe — step-002-Review enthält den verbindlichen Wiring-Hinweis (asynchroner Fault-Übergang)
---

# Step 003: MCP-Wiring auf die Projektregistry: Tool-Leases, harter Cut, Health-/Reload-/Overview-Vertrag

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-A` aus `roadmap.md` — offen sind nach step-001/-002 sämtliche
  Wiring-Anker: A.3 (harter Cut + Flags), A.4-Wiring (Lease-Lambdas, OptionsFactory,
  Command-Komposition), A.5-Rest (`PROJECT_ROOT_*`), A.7-Rest (Flags-Anbindung,
  zweistufiger Zustandsvertrag auf Dispatch-/Health-Seite), Overview-URI-Template,
  ServerInstructions-Budget, A.8-Restkatalog, A.9-Migration des eigenen Repos,
  A.x-Fach-Dokus.
- **Konzept-Referenz:** `Konzept.md` A.3, A.4 (inkl. F2/F3/F6/F8, Review 1/3/4/5/12,
  R2/A, Key-Kanonisierung), A.5, A.7 (Self-Audit 3, Review 7/8/13, R2/B,
  zweistufiger Zustandsvertrag), A.8, A.9, A.6 (Self-Service-Kanäle).

## Aktueller Projektzustand (JIT-Kontext)

Beim Ist-Lesen (2026-08-23, über AiNetLinter-MCP-Tools verifiziert) vorgefunden:

- **Alle 9 Klassen unter `src/AiNetLinter/Mcp/Projects/` existieren real:**
  `ProjectDefinition`, `ProjectDefinitionLoader`, `ProjectDefinitionLoadResult`,
  `ProjectErrorCodes`, `ProjectInstanceFactory`, `ProjectEntry`, `ProjectLease`,
  `ProjectLeaseResult`, `ProjectRegistry`. `find_references("ProjectRegistry")`
  liefert AUSSCHLIESSLICH 3 Treffer in `FastTests/Mcp/Projects/ProjectRegistryTests.cs`
  — **die Registry ist produktiv unverdrahtet**, genau wie im Auftrag angenommen.
- **F2 live bestätigt:** `McpServerOptionsFactory.Create(McpCodeGraphServer,
  IServiceProvider?)` + `BuildToolCollection(mcpState)` + `BuildResourceCollection(mcpState)`
  bäcken die eine Instanz per Closure in alle Lambdas.
- **F3 live bestätigt:** `SymbolGraphToolRegistrations.Register(tools, mcpState)`
  mit privaten `AddXxx(tools, mcpState)`-Methoden je Tool — das mechanische
  Replikationsmuster für die Lease-Lambdas.
- **`OverviewResourceRegistration`** nutzt die statische URI
  `ainetlinter://overview` (`OverviewUri`) und `Register(resources, mcpState)` —
  Ziel-URI-Template und Fehlverträge sind hier anzubauen.
- **`McpServerCommand.RunAsync`** (verifizierter Body): hält heute DIE eine
  `McpCodeGraphServer`-Instanz mit `LoadFunc = innerCt =>
  TryLoadSolutionAsync(solutionPath, innerCt, c)`; davor laufen
  `ResolveSolutionPathOrError` + `TryResolveRulesJsonPath` (F8: stirbt im
  MCP-Pfad). `ResolveConfig`/`ResolveMaxLineCount` delegieren seit step-001 auf
  `ProjectInstanceFactory.MaterializeRules` (geteilter Kern, bleibt für Batch).
- **TD-001 im Code bestätigt:** `MaterializeRules` macht
  `ConfigLoader.TryLoadConfig(...) ?? MaterializedRules.Defaults().Config` — eine
  lesbare, aber UNGÜLTIGE rules.json lädt damit auch über
  `Create(definition, isRequired: true)` stumm mit Defaults, und `Create` setzt
  `UsedDefaultConfig: false` hart. Genau die stille Fehl-Bindung, gegen die das
  Epic gebaut ist → dieser Step löst TD-001 (siehe Änderung 7).
- **TD-003-Anker:** `ProjectErrorCodes` enthält alle sechs A.5-Codes als
  Konstanten, `ProjectRootRequired`/`ProjectRootInvalid` sind ungenutzte
  Platzhalter — der Guard kommt auf Argumentebene (A.3), nicht in den Loader.
- **`ReloadConfigTool.ExecuteAsync(state, configPath?, ct)`** löst ohne
  `configPath` heute per Nachbar-Suche neben der Solution auf
  (`ResolveTargetPath`) und antwortet bei defekter Datei bereits sauber mit
  `McpToolResults.Recoverable(CONFIG_INVALID)` — Review 4 ersetzt nur die
  Zielauflösung (Definition.RulesPath des Keys), das Recoverable-Muster bleibt.
- **`GetServerHealthTool`** hat zwei `ExecuteAsync`-Overloads (einer mit
  `IMcpObservabilityService?`) — die pro-Key-Aggregation baut darauf, braucht
  zusätzlich einen Read-Only-Zugriff auf den Registry-Stand.
- **CLI-Seite:** `LinterArgs` hat `TargetPath` (required, `IsPathMissing()`),
  `ConfigPath?`, `McpServer`-Flag und `Validate()` als zentralen Guard-Anker
  für den harten Cut; neue Flags kommen als `decimal?`/`int?`-Felder dazu.
- **`ServerInstructions.MaxUtf8Bytes = 2557`** — der Vertragsblock muss
  komprimiert hinein (Review 12), Budget-Test schreibt die Rechnung fest.
- **Anti-Loop-Check gegen `codemap.md`:** Kein Widerspruch — die Karte trägt
  genau diese `[ÄNDERN]`-Anker (OptionsFactory, McpServerCommand, Registrations,
  ServerInstructions, Cli). Die step-002-Entscheidungen (Sync-Lease, Dedupe im
  Instanzmuster, Options/Defaults in `ProjectRegistry.cs`, Tick nach
  `ParentProcessWatchdog`-Muster) werden NICHT angetastet, nur konsumiert.

**Scope-Entscheidung des Planers:** Der Fachinhalt passt in EINEN Step, weil
die Migration des eigenen Repos (`ainetlinter.project.json`, `.mcp.json`,
Hermes `config.yaml`) FUNktional an den harten Cut gebunden ist — ohne sie
wäre nach diesem Step jede eigene Dogfood-Registrierung unbrauchbar
(`--path`/`--config` → harter Fehler bzw. fehlende Definitionsdatei →
`PROJECT_NOT_INITIALIZED`). Sie gehört daher in den fachlich berührenden
Step (Nutzervorgabe 2). Übrig bleiben für step-004 (Abschluss): drift-audit
(einmal pro Epic), Live-Verifikation des Overview-URI-Templates in Hermes +
Claude Code inkl. ggf. Rückfall-Entscheidung (Review 5), Meilensteinzeilen
(`Docs/ROADMAP.md`, `00_uebersicht-und-entscheidungen.md` Zeile 11),
§D.4-Wiederöffnungsvermerk.

## Intention

Nach diesem Step ist Epic A fachlich komplett: Jeder Tool-/Resource-Aufruf ist
per absolutem `projectRoot` deterministisch an einen Lease-geschützten
Registry-Key gebunden (26 Tools + Overview), der harte Cut entfernt den
Projektbezug aus der Client-Konfiguration, Health/Reload wirken pro Key,
defekte Regeldateien scheitern deterministisch statt stumm (TD-001), und das
eigene Repo ist selbst auf dem neuen Vertrag. Step-004 ist dann reiner
Abschluss-Step (Audit, Live-Checks, Meilenstein-Doku, §D.4).

## Konkrete Änderungen

### Änderung 1: `src/AiNetLinter/Commands/McpServerCommand.cs`

- **Was:** `RunAsync` hält `ProjectRegistry` (IAsyncDisposable via `await using`,
  Options aus den neuen Flags, BCL-`TimeProvider`) statt einer
  `McpCodeGraphServer`-Instanz. Factory-Delegat je Key: Definition laden
  (Loader), `ProjectInstanceFactory.TryCreate(definition)` — bei
  Konfig-Fehler Lease-Failure mit `RULES_INVALID`, sonst
  `new McpCodeGraphServer(options)` mit `LoadFunc = innerCt =>
  TryLoadSolutionAsync(definition.SolutionPath, innerCt, c)` (Komposition
  Review 1: Dedupe im Instanzmuster, Lock-Hygiene bleibt). Observability- und
  Transport-Setup bleiben strukturell bestehen;
  `McpServerOptionsFactory.Create(registry)` ersetzt die Instanz-Bäckerei.
- **Was (F8):** Im MCP-Zweig entfallen `ResolveSolutionPathOrError` und
  `TryResolveRulesJsonPath` samt Nachbar-Warnung ersatzlos. Vor dem Löschen
  der (privaten) Helfer `find_references` prüfen — Batch-Nutzung bleibt
  unberührt (Non-Goal); `ResolveConfig`/`ResolveMaxLineCount` bleiben
  (Batch-Pfad, geteilter `MaterializeRules`-Kern).
- **Warum:** Konzept A.4/F2/F8; hier sitzt die Komposition, die step-002
  bewusst offen gelassen hat.

### Änderung 2: `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`

- **Was:** `Create(McpCodeGraphServer mcpState)` →
  `Create(ProjectRegistry registry)`; `BuildToolCollection`/`BuildResourceCollection`
  reichen die Registry an die Registrations weiter statt der Serverinstanz.
- **Warum:** F2 — die „Globalität" steckt ausschließlich hier.

### Änderung 3: Sechs Registration-Klassen — `SymbolGraph/FileStructure/Analysis/SymbolBody/DuplicateDetection/ServerMaintenanceToolRegistrations.cs`

- **Was:** `Register(..., McpCodeGraphServer mcpState)` →
  `Register(..., ProjectRegistry registry)`; JEDES Tool-Lambda wird
  async mit nicht-defaultetem `string projectRoot` als ERSTEM Parameter und
  strukturellem Lease (Muster unten, Review R2/A — await ist Pflicht,
  nacktes `return` dispost das Lease vor Task-Abschluss). Die
  Tool-Implementierungen unter `Mcp/Tools/**` behalten ihre
  `ExecuteAsync(server, …)`-Signatur — sie empfangen `lease.Server`; nur
  Sonderfälle (Änderung 6) fassen die Registry selbst an.
- **Was (ServerMaintenance):** `get_server_health` ist die EINZIGE Ausnahme
  von der Pflicht (A.3): optionaler `projectRoot`-Filter — angegeben → nur
  dieser Key (mit `PROJECT_ROOT_REQUIRED`/`_INVALID`-Guard), fehlend →
  Aggregation über ALLE Keys (Read-Only-Snapshot der Registry, pro Key
  Root/Solution/Rules/LastUsedUtc/LoadState/RefreshCount/Staleness/Uptime,
  F5-Werte). `reload_config` wird zum ganz normalen Pflicht-Tool (Änderung 6).
  Parameterbudget (max. 4) ggf. über kleines Input-Record lösen (Präzedenz
  `McpCodeGraphServerOptionsFromParameters`).
- **Warum:** Konzept A.4-Wiring, Review R2/A, A.3-Ausnahmeregelung.

### Änderung 4: `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`

- **Was:** Statische URI → Resource-Template
  `ainetlinter://overview?projectRoot=<url-encoded>`; der Handler validiert
  den Query-Parameter mit denselben Guards/Fehlerverträgen wie die Tools
  (`PROJECT_ROOT_REQUIRED`/`_INVALID`, Loader-Fehler) und rendert die Overview
  des adressierten Keys. SDK-Template-Matching per In-Memory-/Integrationstest
  verifizieren; die LIVE-Host-Prüfung (Hermes/Claude Code) und die ggf.
  Rückfall-Entscheidung (Exposition als Tool — einzige erlaubte
  Freeze-Ausnahme) sind bewusst step-004 zugeordnet (Review 5, Entscheidung
  im Task-Log).
- **Warum:** MCP-Resources nehmen keine Tool-Argumente; mehrere Projekte sind
  sonst nicht adressierbar (Konzept Final-Pass).

### Änderung 5: `src/AiNetLinter/Cli/LinterArgs.cs` + `CliOptionFactory.cs` + `CliCommandBuilder.cs` (+ Routing in `Program.cs`, falls dort geprüft wird)

- **Was (neue Flags):** `--mcp-project-ttl-minutes` (decimal, InvariantCulture,
  z. B. `0.05` ≈ 3 s) und `--mcp-max-projects` (int) als statische,
  projektagnostische Flags; ungültiger Wert → harter Startfehler; fließen in
  `ProjectRegistryOptions` (Defaults 45/4 aus step-002 bleiben wirksam, wenn
  Flags fehlen).
- **Was (harter Cut):** Mit `McpServer == true` sind `--path` (TargetPath
  gesetzt) und `--config` (ConfigPath gesetzt) harte Fehler — Prüfung in
  `Validate()` (bzw. dem Routing, ein Ort genügt), mit deterministischer
  deutschsprachiger Fehlermeldung und Exit ≠ 0. Batch-Verhalten bleibt
  byte-identisch (bestehende Batch-Tests laufen unverändert weiter).
- **Warum:** Konzept A.3 — Deprecationsschichten sind globales Non-Goal.

### Änderung 6: `src/AiNetLinter/Mcp/Tools/ServerMaintenance/ReloadConfigTool.cs` + `GetServerHealthTool.cs`/`GetServerHealthModels.cs`

- **Was (reload_config, Review 4):** Ohne `configPath` wird der `rules`-Pfad
  AUS DER Definitionsdatei des adressierten Keys neu eingelesen (kein
  Nachbar-Suchlauf mehr — `ResolveTargetPath`-Zweig stirbt im MCP-Pfad); mit
  `configPath` weiterhin Hot-Swap-Override für diesen einen Key. Das
  bestehende Recoverable-Muster (`CONFIG_NOT_FOUND`/`CONFIG_INVALID`,
  aktive Config bleibt) bleibt unverändert. Zugang zur Definition über den
  Lease (kleine additive Erweiterung an `ProjectLease`, nur falls nicht
  bereits vorhanden — vorab per Skeleton prüfen).
- **Was (get_server_health):** Aggregationsmodell für den ungefilterten Fall
  (pro Key ein Abschnitt + prozessweiter Observability-Teil); gefilterter Fall
  delegiert auf den einen Key. Neue Health-Felder des zweistufigen Vertrags
  (Änderung 8) aufnehmen.
- **Warum:** Konzept A.4 (reload je Key), A.5/A.7 (Health-Felder), Review 4.

### Änderung 7: `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` + `ProjectErrorCodes.cs` — TD-001 lösen

- **Was:** Neuer Code `RulesInvalid = "RULES_INVALID"` in `ProjectErrorCodes`
  (bewusste Erweiterung von Konzept A.5 um einen siebten Code — Begründung:
  A.5 deckt nur fehlende Dateien ab; die tech-debt TD-001 delegiert die
  Vertragsentscheidung ausdrücklich an diesen Wiring-Step, und ein stummer
  Default-Fallback im nun produktiven Registry-Pfad wäre die stille
  Fehl-Bindung, gegen die das Epic gebaut ist). `ProjectInstanceFactory`
  bekommt einen fehlersignalisierenden Pfad für den Registry-Zweig (z. B.
  `TryCreate(ProjectDefinition)` → Result-Record analog
  `ProjectDefinitionLoadResult`: Options oder ErrorCode+ErrorMessage), der
  bei lesbarer, aber ungültiger rules.json `RULES_INVALID` mit Pfad und
  Bauanleitung liefert, statt `Defaults()` einzusetzen. `MaterializeRules`
  und das Batch-Verhalten bleiben NICHT angefasst (Non-Goal).
- **Warum:** tech-debt.md TD-001 (Vorschlag „deterministischer Fehlervertrag
  im Registry-Pfad"), A.2 Kein-Fallback-Geist, A.5-Determinismus.

### Änderung 8: `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs` + `McpCodeGraphServer.cs` + `ServerStalenessStats.cs` (+ `McpToolResults.cs` als Ansatzpunkt)

- **Was (zweistufiger Zustandsvertrag, Wiring-/Dispatch-Seite):**
  Inkrementeller Refresh-Fehler → letzter guter Stand bleibt resident,
  Analyse läuft weiter; Antworten auf diesem Key tragen bis zur erfolgreichen
  Aktualisierung einen `[WARN]`-Kopf; Health-Felder `LastGoodStateUtc` +
  `LastLoadError` je Instanz füllen; erfolgreicher Refresh heilt. Kalt-Load-
  Seite: Dispatch antwortet `PROJECT_LOAD_FAILED` mit Ursprungsmeldung +
  Restore-Hint, solange der FAILED-Marker steht (Marker-Mechanik existiert
  seit step-002; hier nur die Antwortverkettung Loading→Retry→FAILED
  sicherstellen — Code-String je Bestand verifizieren, nicht neu erfinden).
  Syntaxfehler in einzelnen .cs-Dateien bleiben KEIN Load-Fehler (Konzept).
- **Warum:** Konzept A.7 „Solution-Zustand: zweistufiger Fehlervertrag", A.8
  Testkatalog; Codemap markiert `McpCodeGraphServerRefresh.cs` als Ansatzpunkt.

### Änderung 9: `src/AiNetLinter/Mcp/ServerInstructions.cs`

- **Was:** Einmaliger, KOMPRIMIERTER Vertragsblock (F6, Review 12):
  `projectRoot` ausnahmslos Pflicht und absolut (einzige Ausnahme
  `get_server_health`-Filter), Definitionsdatei `ainetlinter.project.json`
  im Projektroot mit Pflichtfeldern `solution`+`rules`, relativ zur Datei
  aufgelöst. UTF8-Bytelänge bleibt ≤ `MaxUtf8Bytes` (2557) — Budget-Rechnung
  als Test fixieren; Limit-Erhöhung nur mit Begründung im Commit.
- **Warum:** A.4/F6 — Single-Source-of-Truth statt 26 Description-Duplikate;
  Self-Service-Kanal 2 (A.6).

### Änderung 10: Eigene Repo-Migration (A.9-Teil, an den harten Cut gebunden)

- **Was:** `ainetlinter.project.json` im Repo-Root anlegen (`solution` +
  `rules`, relativ zur Datei — zeigt auf `AiNetLinter.slnx`/`rules.json`);
  Repo-`.mcp.json` und die eigene Hermes-Registrierung (`config.yaml`) auf
  `command + --mcp-server` (+ statische, projektagnostische Flags falls
  gewünscht) reduzieren; AGENTS.md erhält den Abschnitt
  „AiNetLinter-MCP: Initialisierung" (Definitionsdatei-Vertrag,
  kopierfähiges Template aus A.5). Der Hermes-config.yaml-Eingriff wirkt erst
  beim nächsten Server-Neustart des Hosts — deshalb VOR dem Abschluss-Gate
  erledigen (siehe Notes).
- **Warum:** A.9-DoD; ohne Migration wäre das eigene Dogfooding nach dem
  harten Cut sofort gebrochen (Doku-/Sync-Pflicht im berührenden Step).

### Änderung 11: Doku + Sync (A.x, fachlich berührende Teile)

- **Was:** `Docs/agent-api.md` (Init-Vertrag mit `projectRoot`, Referenzabschnitt
  „ainetlinter.project.json" mit Feldtabelle/Ankerregel/Beispielen, neue
  Fehlercodes inkl. `RULES_INVALID` und `PROJECT_ROOT_*`, Overview-URI-Template);
  `Docs/configuration.md` (`--path`/`--config` nur noch Batch, neue Flags);
  `Docs/integration.md` (Registrierungsbeispiele ohne `--path`/`--config`);
  README.md nur dort anfassen, wo Registrierungsbeispiele den alten Vertrag
  zeigen (Doku-Objektivität: nur Implementiertes dokumentieren, gegen den Code
  verifizieren). Danach `.agents/rules/AiNetLinter.mdc` via
  `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
  synchronisieren (CLI-Texte haben sich geändert). NICHT in diesem Step:
  `Docs/ROADMAP.md`-Meilenstein, `00_uebersicht…` Zeile 11, §D.4 → step-004.
- **Warum:** Nutzervorgabe 2 (Doku im berührenden Step, keine Mini-Doku-Steps).

### Änderung 12: Kleine additive Registry-Erweiterungen — NUR falls vom Wiring benötigt

- **Was:** Vorab per Skeleton prüfen; anlegen nur, was tatsächlich gebraucht
  wird: Read-Only-Snapshot der Keys für die Health-Aggregation (z. B. interne
  Methode, die pro Key Root/Definition/LastUsedUtc/LoadState ohne Lease-Seiteneffekt
  liest) und RootPath/Definition-Passthrough am `ProjectLease` für
  reload_config. Bestehende Strukturen erweitern, keine zweite Zustandsquelle
  erfinden (Anti-Loop); `BanPublicNestedTypes` beachten (Präzedenz:
  Options/Defaults in derselben Datei).
- **Warum:** step-002 hat bewusst genau diese Schnittstellen offen gelassen;
  der Wiring-Step konsumiert, statt umzubauen.

## Tests

Entwicklung gefiltert (`Category=Unit` bzw. `Category=Component`, <10 s);
der komplette Nicht-Stress-Stack läuft EINMALIG als Abschluss-Gate
(siehe DoD). Neue Tests (xUnit v3, `TestTempDirectory`, keine
zwangsserialisierenden Collections, kein `.Wait()`/`.Result` — Präzedenz
`ManualResetEventSlim` aus step-002):

- [ ] Contract-Test `tools/list` (In-Memory-Server über OptionsFactory mit Registry): jedes Analyse-Tool führt `projectRoot` in `required`; `get_server_health` führt es als OPTIONALEN Parameter; das Observability-Paket-Tool (`report_observability_feedback`) bleibt vertraglich unberührt — die Toolmenge je Kategorie im Test exakt einschließen (26er-Bestand eingefroren)
- [ ] Uniforme Pflicht + Defense-in-Depth: fehlender `projectRoot` am Resolver → `PROJECT_ROOT_REQUIRED` bei beliebigem Registry-Stand (SDK-Schema-Validierung ist der Normalfall, der Code ist Rückfallebene — Konzept-Hinweis zur Erreichbarkeit beachten: Schema prüfen, nicht SDK-Pfad)
- [ ] Root-Validierung (Self-Audit 3): relativer `projectRoot` → `PROJECT_ROOT_INVALID`; `get_server_health` ohne Filter liefert alle Keys, mit Filter genau einen
- [ ] reload_config (Review 4): ohne `configPath` wird `Definition.RulesPath` des Keys neu gelesen; mit `configPath` Hot-Swap-Override; Nachbar-Suche greift NIE mehr; defekte Datei → Recoverable mit aktiver Config bleibend (bestehendes Muster)
- [ ] TD-001: lesbare, aber ungültige rules.json → `RULES_INVALID` mit Pfadangabe, KEIN Default-Load, kein Registry-Eintrag mit Default-Config; Batch-Pfad unverändert (bestehende Batch-Tests decken das ab)
- [ ] Lease-Lifetime (Review R2/A): mit async/await-Wiring bleibt `InFlightCount` während des GESAMTEN Tool-Calls > 0 (verzögerter Test-Task) und fällt erst nach Abschluss auf 0 — das nacktes-return-Muster würde diesen Test NICHT bestehen
- [ ] ServerInstructions-Budget: UTF8-Bytelänge ≤ `MaxUtf8Bytes`; Block enthält projectRoot-/Definitionsdatei-Vertrag (Text-Assertion, komprimiert)
- [ ] Zweistufiger Zustandsvertrag: inkrementeller Refresh-Fehler → last-good bleibt resident, `[WARN]`-Kopf gesetzt, `LastGoodStateUtc`/`LastLoadError` gefüllt; erfolgreicher Refresh heilt; Kalt-Load-Fehler → `PROJECT_LOAD_FAILED`-Antwortmuster über das Wiring (Loading→Retry→FAILED), FAILED-Marker-Verkettung mit der step-002-Registry
- [ ] Overview: Resource-Template-Auflösung mit URL-kodiertem absolutem Pfad liest den richtigen Key; fehlender/relativer Parameter → dieselben Fehlerverträge wie bei Tools
- [ ] Flag-Parsing (A.7): decimal/InvariantCulture (`0.05` ≈ 3 s), Defaults 45 Min/4 wirksam bei fehlenden Flags, ungültiger Wert → harter Startfehler
- [ ] Harter Cut: `--mcp-server` + `--path` → Fehler, `--mcp-server` + `--config` → Fehler; Batch-Kombinationen unverändert
- [ ] Health-Aggregation: zwei Keys → zwei korrekte pro-Key-Abschnitte inkl. F5-Werte; Observability-Teil unverändert vorhanden

Integration (Category=Integration, nur im Abschluss-Gate):

- [ ] Zwei Keys routen korrekt; Bindungsverifikation über `get_server_health` (pro-Key-Zustände); Key-Äquivalenz `C:/repos/x` ≡ `C:\repos\x` endet im selben Key
- [ ] Lazy-Init: erster Call gegen neuen Key messbar länger, zweiter sofort
- [ ] Observability: Call-Log enthält projectRoot/Key; Reaper (`--parent-pid`) unverändert
- [ ] Harter Cut im Subprozess: `--mcp-server --path …` startet nicht (Exit ≠ 0, deterministische Fehlerausgabe)
- [ ] Eigenes Repo migriert: Serverstart ohne `--path` lädt dieses Repo via `ainetlinter.project.json` (Muster `McpLiveRepositoryTests`)

## Definition of Done

- [ ] Alle „Konkreten Änderungen" umgesetzt
- [ ] Build-Command aus Tech-Stack-Notiz (`dotnet build`) grün — fehler- UND warnungsfrei (`TreatWarningsAsErrors`)
- [ ] Abschluss-Gate EINMALIG vor dem letzten Commit:
      `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
      UND `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
      (Iteration davor ausschließlich gefiltert; TRX-Diagnose bei Fehlern, keine
      blinden Wiederholungsläufe)
- [ ] Quality-Gates vor JEDEM Commit (AiNetLinter-MCP-Tools, nicht grep):
      `get_violations` (Scope der geänderten Bereiche, z. B. `Mcp`/`Projects`/`Cli`)
      → 0 Verstöße; `safeguard` (Scope `src/AiNetLinter`) → PASS; `metrics_lookup`
      vor/nach für umgebaute Methoden (`RunAsync`!, Lambdas, Health-Aggregation)
      — Grenzwerte laut `.agents/rules/AiNetLinter.mdc`
- [ ] Commits auf `main`, Conventional Commits auf Deutsch, imperativ; Antwort
      endet mit `### Commit-Vorschlag`-Block (Richtlinien §4); Code-Commit und
      separater Doku-Commit wie in step-001/-002
- [ ] `step-003/step-result.md` geschrieben (inkl. Abweichungen, Quality-Gate- und
      Gate-Nachweisen, Beobachtungen für Kritiker/Tech-Debt)
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt
- [ ] Bewusst NICHT in diesem Step: drift-audit (`find_duplicates`/
      `find_magic_values`/`find_dead_code` — kommt einmal pro Epic im Abschluss-Step
      004), Live-Host-Verifikation des Overview-Templates + ggf. Rückfall-Umbau
      (step-004), `Docs/ROADMAP.md`/`00_uebersicht…`-Meilensteinzeilen (step-004),
      §D.4-Vermerk (step-004), sämtliche `Mcp/Daemon/`-Dateien (Epic B)

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — Grenzwerte für den Umbau: `MaxMethodParameterCount`
  4 (projectRoot + Registry-Zugriff ggf. über Input-Record), `MaxMethodLineCount` 60,
  `AIContextFootprint` 2500 (RunAsync im Blick), `BanBlockingTaskAccess` (auch im
  Testprojekt — step-002-Beobachtung), `AllowCancellationShutdownCatch` für
  Shutdown-Pfade, `EnforceSealedClasses`/`#nullable`/ASCII
- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 — MCP-Dogfooding-Pflicht
  (eigene Tools statt grep; bei „lädt noch" zuerst `get_server_health`),
  Doku-Objektivität (nur Implementiertes, gegen Code verifizieren)
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 — `TestTempDirectory`-Pflicht,
  keine zwangsserialisierenden Collections, Update-Pflicht für
  Docs/configuration/README + rules.json-Abhängigkeiten, Commit-Vorschlag-Pflicht
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 — Zero-Warning, Result-Pattern,
  Kommentar-Sparsamkeit OHNE Task-/Step-Artefakt-Referenzen (auch nicht
  „TD-001" im Code — Rationale ID-frei formulieren)

## Bekannte Ausnahmen

- Keine flaky Tests bekannt. Bewusst hingenommen (dokumentiert, kein
  Test-Assert darauf): Das Räumen eines faulted Loads über den Sync-Eviction-Pfad
  kann eine `[WARN]`-Zeile auf dem nicht injizierbaren Console-Kanal produzieren
  (tech-debt TD-005, Lösung erst mit Epic B) — Tests dürfen davon nicht abhängig
  sein.

## Code-Skizze (optional)

```csharp
// Registrations-Lambda (26x identisch, Review R2/A: async + await PFLICHT):
async (string projectRoot, string? namePattern = null, CancellationToken ct = default) =>
{
    using var lease = _registry.Lease(projectRoot);
    return await FindSymbolTool.ExecuteAsync(lease.Server, namePattern, ct);
}

// Komposition in RunAsync (Review 1: Dedupe im Instanzmuster, Lock deckt nie einen Load):
var created = ProjectInstanceFactory.TryCreate(definition);   // RULES_INVALID statt Defaults (TD-001)
if (!created.Succeeded) return ProjectLeaseResult.Failure(created.ErrorCode, created.ErrorMessage);
var options = created.Options with { LoadFunc = innerCt => TryLoadSolutionAsync(definition.SolutionPath, innerCt, c) };
return ProjectLeaseResult.Success(new McpCodeGraphServer(options));
```

## Notes

- **Wiring-Hinweis aus step-002-Review (verbindlich):** Der Fault-Übergang von
  `_loadTask` ist ASYNCHRON — direkt nach `Lease` liefert `LoadState` auch beim
  baldigen Scheitern noch `Loading`. KEINE unmittelbare `LoadFailed`-Prüfung
  nach dem Lease erwarten; das bestehende Antwortmuster Loading → Retry →
  `PROJECT_LOAD_FAILED` tragen lassen.
- **Sync-Eviction-Latenz:** Verdrängt der Sync-Pfad einen `Loading`-Entry,
  blockiert er bis zum Load-Wind-down (step-002-Review, totes Risiko, aber
  Latenz-Puls). `LoadFunc` muss den CancellationToken zügig honorieren —
  `TryLoadSolutionAsync` tut das bereits; nicht neu erfinden.
- **Dogfood-Fenster vermeiden:** Nach dem Rebuild startet der Host den
  ainetlinter-Server ggf. neu. Deshalb Änderung 10 (eigene Migration) VOR dem
  Abschluss-Gate und vor dem letzten Commit abschließen; bis dahin können die
  MCP-Gates gegen die ALT-Registrierung laufen (alter Prozess, alte Binary).
  Falls der Server zwischendurch mit neuem Code startet, ist das erwartete
  Bild: ohne Definitionsdatei → `PROJECT_NOT_INITIALIZED` mit Template —
  genau der neue Vertrag.
- **Toolbestand eingefroren:** 26 Tools, keine neuen/removal; das
  Observability-Paket-Tool bleibt unberührt (kein `projectRoot`) — der
  Contract-Test grenzt die Mengen exakt ein, damit kein stiller Drift entsteht.
- **Kein DI-Container** (Richtlinien §2): Registry als plain Instanz durch die
  Factory/Registrations reichen, kein ServiceCollection-Ausbau.
- **Arbeitsweise (Nutzervorgaben):** AiNetLinter-MCP-Tools
  (`find_symbol`, `get_file_skeleton`, `find_references`, `get_impact`,
  `get_feature_context`, `metrics_lookup`) statt rg/grep über C#-Symbole;
  rg/grep nur für Nicht-C#-Dateien. Bei „lädt noch" zuerst `get_server_health`,
  nicht blind wiederholen. Coder entwickelt gefiltert, kompletter
  Nicht-Stress-Stack EINMAL als Abschluss-Gate (oben im DoD verankert).
- **Bestehende Muster bewusst wiederverwendet statt dupliziert:** Tick-Loop/
  CTS-Muster (`ParentProcessWatchdog`) aus step-002 für die Timer-Anbindung;
  Result-Record-Präzedenz `ProjectDefinitionLoadResult` für `TryCreate`;
  Recoverable-Muster (`McpToolResults`) für reload_config; Options-Record-
  Präzedenz (`McpCodeGraphServerOptionsFromParameters`) gegen Parameterlimit-
  Überschreitungen; `McpInMemoryTestContext` (F4) für die Contract-Tests.
