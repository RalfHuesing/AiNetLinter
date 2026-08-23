---
status: active  # active | done
task: 11_epic-projektregistry-und-daemon
derived_from: konzept.md
created_at: 2026-08-23T12:58:00+02:00
last_updated: 2026-08-23T23:58:00+02:00
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert (kein Cutoff im eigenen System-Prompt angegeben)
---

# Roadmap: 11_epic-projektregistry-und-daemon

Grober Anker, kein Detailplan — Detail-Steps entstehen erst JIT im
Step-Modus des Planers, siehe `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md`
§7.2. Diese Datei wird laufend angepasst (Epics abgehakt, ergänzt,
umformuliert oder als obsolet markiert) — kein starres Vorab-Dokument.

Grundlage ist ausschließlich `konzept.md` (status: ready); dessen
Epic-Gliederung wird 1:1 übernommen. Reihenfolge: EPIC-A vor EPIC-B —
EPIC-B setzt EPIC-A komplett abgeschlossen und grün voraus (Konzept
„Epics-Reihenfolge & Abhängigkeiten“). Beide Epics sind laut
Nutzervorgabe (Effizienz, siehe task-state.md) für **3–5 Steps je Epic**
dimensioniert; innerhalb jedes Epics gilt: Contract-Tests zuerst, dann
Implementierung, dann Migration der eigenen Konfigurationen — Doku- und
Sync-Pflichten liegen im fachlich berührenden Step, es gibt keine
Mini-Doku-Steps.

## Tech-Stack-Notiz

Aus dem Projekt abgeleitet (AGENTS.md, `.agents/rules/`, task-state.md),
einmalig hier — Coder und Kritiker bekommen sie bei jedem Aufruf mitgegeben:

- **Build-Command:** `dotnet build` — fehler- UND warnungsfrei
  (`TreatWarningsAsErrors=true` in allen Projekten; SDK 10.0.203).
- **Test-Command:** Abschluss-Gate je Step: `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  UND `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
  Schnelle Iteration während der Entwicklung gefiltert: `--filter Category=Unit`
  bzw. `Category=Component` (FastTests, <10 s). `Category=Stress` läuft nie
  automatisch; Testdiagnose über TRX in `TestResults/` (Richtlinien §3).
- **Lint-Command:** Kein externes Lint-Tool — AiNetLinter dogfooded sich selbst:
  Quality-Gates vor Commits über die MCP-Tools `get_violations`, `safeguard`,
  `metrics_lookup` (One-Shot-Metriken vor/nach Refactoring). **Nutzervorgabe:**
  drift-audit (`find_duplicates`/`find_magic_values`/`find_dead_code`)
  **einmal PRO EPIC** (nicht pro Step), vor Epic-Abschluss ausführen.
- **Code-Style-Kurzfassung** (aus `.agents/rules/AiNetLinter.mdc` /
  `AiNetLinterRichtlinien.mdc`): `sealed` für konkrete Klassen; Methoden ≤60 Zeilen;
  ab 5 Parametern Input-Record, Konstruktor-Deps ≤5, `AIContextFootprint` ≤2500
  (→ Options-Records statt langer Parameterlisten, Konzept F7); kein leeres
  `catch`; `Result<T>`-Pattern für erwartbare Fehlerfälle; keine Task-/Step-
  Artefakt-Referenzen in Kommentaren; file-scoped namespaces, `#nullable enable`;
  xUnit v3, `TestTempDirectory` statt OS-Temp, keine zwangsserialisierenden
  Test-Collections.
- **Commit-Konventionen:** Conventional Commits auf Deutsch, imperativ
  (`feat:`/`fix:`/`docs:`/`chore:`); Antwort endet mit `### Commit-Vorschlag`-Block
  (nur Commit-Text, ohne Shell-Befehl) — Richtlinien §4.
- **MCP-Nutzung:** ainetlinter-MCP-Tools (`find_symbol`, `get_file_skeleton`,
  `find_references`, `get_impact`, `get_violations`, …) statt rg/grep über
  C#-Symbole; rg/grep nur für Nicht-C#-Dateien/Stringsuche. Bei „lädt noch“-
  Antworten zuerst `get_server_health`, nicht blind wiederholen (Richtlinien §1).

## Regel-Index

Ein Eintrag pro Datei in `.agents/rules/` — Kurzbeschreibung, kein Volltext.
Der Step-Modus-Planer wählt daraus gezielt die zum Step passenden Dateien
(siehe SKILL.md Schritt 4a). Wird laufend gepflegt.

- `.agents/rules/AiNetLinter.mdc` — Auto-generiert aus `rules.json`: aktive
  C#-Grenzwerte und Stilregeln des eigenen Linters (MaxLineCount 500, Komplexitäts-,
  Parameter-, Kopplungs-Limits, agent-resilience- und Architektur-Regeln);
  Sync-Pflicht via `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`,
  sobald Regel- oder CLI-Texte geändert werden.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Manuell gepflegte Architektur-,
  Workflow-, Kommentar- und Verhaltensregeln: Design-Philosophie (Monolith,
  kein DI/ALC/Plugin, keine repo-spezifischen Hardcodings), Windows-/Shell-Pflichten,
  Testkategorien & TRX-Diagnose, Doku-Objektivität, Qualitätsdrift-Prävention
  (DRY/Magic Values/Dead Code, Zero-Warning), Kommentar-Sparsamkeit,
  Commit-Vorschlag-Pflicht.

## Epics

- [ ] **EPIC-A: Projektregistry (transportneutral)** — Der MCP-Server hält mehrere
      Projekte gleichzeitig vor, deterministisch adressiert pro Aufruf über
      `projectRoot`; kein Projektbezug mehr in der Client-Konfiguration. Vollständiger
      Umfang: `konzept.md` A.1–A.9. Dimensioniert für 3–5 Steps. Status: Grundlagen
      erledigt (step-001, step-002, jeweils approved); Restfachlichkeit inkl. Migration
      des eigenen Repos → step-003 umgesetzt; step-004 korrigierte Kalt-Load-,
      Erstzugriffs-Dedupe- und Overview-Lease-Verträge, hat aber zwei weitere
      Race-Fenster im Review offengelegt; die Produktionskorrektur läuft in
      step-005, die Interleaving-Testanker in step-006 und die letzten direkten
      Fehler-/Loser-Assertions in step-007. Abschluss (drift-audit,
      Live-Verifikation Overview, Meilenstein-Doku, §D.4) → nächster regulärer
      Step nach step-007.
  - [ ] A.2 Definitionsdatei `ainetlinter.project.json`: Pflichtfelder `solution` +
        `rules`, relativ zur Definitionsdatei aufgelöst (nie zum cwd), Existenzprüfung
        beider Ziele, kein Fallback/Raten (Nachbar-Fallback `TryResolveRulesJsonPath`
        stirbt im MCP-Pfad ersatzlos). **[x] erledigt → step-001** (Loader mit Pflichtfeldern,
        Anker Definitionsdatei, Existenzprüfung beider Ziele, Kein-Fallback; 11 Contract-Tests grün).
  - [ ] A.4 Neue Klassen unter `src/AiNetLinter/Mcp/Projects/` gemäß Strukturbaum
        (`ProjectDefinition`, `ProjectDefinitionLoader`, `ProjectEntry`, `ProjectLease`,
        `ProjectInstanceFactory`, `ProjectRegistry`) — schlank wegen Lint-Grenzen (F7,
        Options-Records); Config-Materialisierung als gemeinsamer Helper für Batch und
        Registry (Review 3). **[x] erledigt → step-001** (ProjectDefinition/-Loader/
        -InstanceFactory + ProjectErrorCodes) **und → step-002** (ProjectEntry/-Lease/
        -LeaseResult/-Registry inkl. Eviction, Busy-Guard/Pending-Adoption,
        FAILED-Marker; 14 Unit-Tests approved)
  - [ ] A.3 Harter Cut: `projectRoot` ausnahmslos Pflicht UND absolut (einzige Ausnahme:
        optionaler Filter bei `get_server_health`); `--path`/`--config` im MCP-Modus
        entfernt — unbekanntes Argument = harter Fehler; Batch unverändert; neue
        statische Flags `--mcp-project-ttl-minutes`, `--mcp-max-projects`
        (Decimal-Minuten, InvariantCulture, ungültiger Wert → harter Startfehler).
        **[x] umgesetzt → step-003** (MCP-Argumentgrenze und invariant-parsende
        Flags aktiv; Contract-/Integrationstests grün)
  - [ ] A.5 Fehlerverträge: `PROJECT_ROOT_REQUIRED`, `PROJECT_ROOT_INVALID`,
        `PROJECT_NOT_INITIALIZED` (mit vorgeschriebenem kopierfähigem Template-Block),
        `PROJECT_DEFINITION_INVALID`, `SOLUTION_NOT_FOUND`, `RULES_NOT_FOUND` —
        deterministisch, englisch, mit Bauanleitung; `AMBIGUOUS_SOLUTION` entfällt im
        MCP-Pfad. (loader-seitig **erledigt → step-001**: NOT_INITIALIZED/DEFINITION_INVALID/
        SOLUTION_NOT_FOUND/RULES_NOT_FOUND inkl. wörtlichem Template-Block, text-assertiert;
        ROOT_* → step-003)
  - [ ] A.4 Wiring: alle 6 Registration-Klassen + `OverviewResourceRegistration` auf
        `using var lease = _registry.Lease(projectRoot)` umstellen — Lambda MUSS async
        sein und awaiten (Review R2/A); `McpServerOptionsFactory.Create(ProjectRegistry)`;
        Key-Kanonisierung (`Path.GetFullPath`, Comparer `OrdinalIgnoreCase`);
        Load-Dedupe im bestehenden Instanzmuster (Review 1) — der Registry-Lock deckt
        nie einen Solution-Load. Grund-Wiring → step-003; belastbare Erstzugriffs-
        Dedupe und Overview-Lease → step-004; Lookup-/Reservation-Race → step-005;
        belastbarer Testanker → step-006; vollständige Loser-/Fehlerassertionen
        → step-007.
  - [ ] A.7 Eviction & RAM-Hygiene: TTL-Timer (Default 45 Min idle, 5 Min Takt) +
        maxProjects (Default 4) + LRU; InFlight-Tracking strukturell über Lease
        (Review 7); Busy-Guard + Pending-Eviction mit Adoption statt Doppel-Load
        (Reviews 8/13); FAILED-Einträge räumt der TTL-Tick sofort weg (Review R2/B).
        (Registry-seitig **erledigt → step-002**; Flags-Anbindung → step-003)
  - [ ] A.7 Zweistufiger Zustandsvertrag: Kalt-Load-Fehler → `PROJECT_LOAD_FAILED` +
        FAILED-Marker in der Registry, kein negatives Caching; inkrementeller
        Refresh-Fehler → last-good bleibt resident, `[WARN]`-Kopf, Health-Felder
        `LastGoodStateUtc`/`LastLoadError`; Heilung beim nächsten erfolgreichen Refresh.
        (FAILED-Marker-Grundlage erledigt → step-002; Wiring-/Dispatch-Korrektur
        → step-004; FAILED-Freigabe-Race → step-005; Regressionstest → step-006/
        step-007)
  - [ ] A.4 Overview-Ressource auf URI-Template
        `ainetlinter://overview?projectRoot=<url-encoded>` umstellen; Rückfallplan
        (Review 5): scheitert ein Host am Query-Parameter → Exposition als Tool
        (einzige erlaubte Ausnahme vom Tool-Freeze), Entscheidung im Task-Log. URI-
        Template in step-003 umgesetzt; Lease-/Fehlervertrags-Korrektur → step-004.
  - [ ] A.4/F6 projectRoot-/Definitionsdatei-Vertrag einmalig in `ServerInstructions.Text`,
        komprimiert ins Byte-Budget (`MaxUtf8Bytes` ≈ 2557); Limit-Erhöhung nur mit
        Begründung im Commit. **→ step-003**
  - [ ] A.8 Tests: Unit-Katalog vollständig (Key-Normalisierung, Loader/Pflichtfelder/
        keine Auto-Suche, uniforme Pflicht + Root-Validierung, Kein-Fallback,
        Self-Service-Template-Assertion, Load-Dedupe/Lock-Hygiene, Busy-Guard, Eviction
        mit injizierbarer Clock, Contract-Tests `required: projectRoot`, zweistufiger
        Zustandsvertrag, Snapshot-Semantik, Pending-Adoption, Lease-Disziplin inkl.
        async-Wiring-Nachweis, FAILED-Marker) + Integration-Katalog (Routing je Key,
        Bindungsverifikation via `get_server_health`, Lazy-Init, Staleness-Grenzen,
        Observability mit projectRoot, Reaper unverändert). (Teile erledigt →
        step-001/-002; Restkatalog step-003; belastbare Korrekturtests → step-004/
        step-005/step-006/step-007)
  - [ ] A.9 DoD: Build grün; beide TestSuites ohne Stress grün; harter Cut aktiv
        (MCP-Modus lehnt `--path`/`--config` ab, Batch unverändert); eigenes Repo
        migriert (`ainetlinter.project.json` im Root, AGENTS.md-Abschnitt
        „AiNetLinter-MCP: Initialisierung“, Repo-`.mcp.json` + Hermes config.yaml auf
        `command + --mcp-server` reduziert); `.agents/rules/AiNetLinter.mdc` via
        `--sync-agent-rules-only` synchronisiert; Wiederöffnungsvermerk in
        `90_bewusst-nicht-umsetzen/Konzept.md` §D.4. (Migration des eigenen Repos
        **→ step-003** — der harte Cut macht die eigenen Registrierungen sonst
        unbrauchbar; die Review-Korrekturen → step-004/step-005/step-006/step-007;
        drift-audit, Live-Verifikation Overview, §D.4-Vermerk → nächster
        regulärer Step)
  - [ ] A.x Doku-Sammelpflichten (im fachlich berührenden Step, keine Mini-Doku-Steps):
        `Docs/agent-api.md` (Init-Vertrag, Referenzabschnitt „ainetlinter.project.json",
        neue Fehlercodes), `Docs/configuration.md` (entfernte/neue Flags),
        `Docs/integration.md` (Registrierungsbeispiele ohne `--path`/`--config`),
        `Docs/ROADMAP.md`, `README.md`,
        `tasks/mcp-server-weiterentwicklung/00_uebersicht-und-entscheidungen.md` (Zeile 11).
        (Fach-Dokus agent-api/configuration/integration + README-Registrierungsbeispiele +
        Sync mdc → step-003; Meilensteinzeilen Docs/ROADMAP.md + 00_uebersicht →
        nächster regulärer Step)

- [ ] **EPIC-B: Daemon-Modus (geteilter, langlebiger Analysekern)** — Die fertige
      Registry wandert in einen geteilten Prozess; Clients verbinden sich über einen
      Thin-Client-Stdio-Prozess, am Toolvertrag ändert sich nichts. Voraussetzung:
      EPIC-A komplett abgeschlossen und grün. Vollständiger Umfang: `konzept.md`
      B.1–B.7. Dimensioniert für 3–5 Steps.
  - [ ] B.2 Transport (`Mcp/Daemon/`): Named Pipe `ainetlinter.analyzer.v1.<username>`
        (+ ACL auf aktuellen User), newline-delimited JSON, je Verbindung ein async
        Read/Write-Loop; Disconnect bricht in-flight Calls DER Verbindung ab —
        Registry und Keys bleiben davon unberührt und warm.
  - [ ] B.2 Handshake: hello/welcome mit Protokoll-/Versionsvergleich; `shutdown` als
        Pipe-Level-Kommando, nur bei null weiteren Verbindungen — sonst Abbruch mit
        `VERSION_CONFLICT` (Anti-Ping-Pong); `welcome` trägt die effektive
        Daemon-Konfiguration → `[WARN]` + Observability-Ereignis bei Divergenz.
  - [ ] B.3 DaemonHost (`--daemon-start`, intern, in `--help` als `[internal]`):
        Registry + MCP-Session je Verbindung gegen die geteilte Registry; Idle-Exit
        (Default 10 Min, keine Clients + idle) graceful inkl. Dispose aller Keys +
        MRU-Persistierung; laufende Loads/Warmups verschieben den Exit; MRU-Warmup
        gebunden (max 2 parallele Loads), interaktiver Load wartet nie dahinter; tote
        MRU-Pfade verworfen UND aus dem State entfernt; Doppelstart → sauberer
        stderr-Fehler + Exit-Code ≠ 0; KEIN Parent-Reaper im Daemon.
  - [ ] B.2/B.3 ThinClient (`ThinClientProxy`/`ThinClientLauncher`): nach außen
        identisches `--mcp-server`; intern Connect-or-Start-Race (Connect first,
        detached Spawn `UseShellExecute=false`/`CreateNoWindow`, Retry-Fenster;
        Verlierer des Pipe-Greifens verbindet); opake Byte-Pump stdio⇄Pipe ohne
        SDK-Interpretation; Stdio-Purity per Contract-Test (ausschließlich
        Protokollbytes auf stdout); Pipe-Abbruch mitten im Call → GENAU EIN
        automatischer Retry (read-only = idempotent), zweiter Fail roh durchgereicht;
        Hänger-Schutz (Ping-Timeout → Kill/Restart, Call-Log-Ereignis); Reaper-Erbe
        (`--parent-pid`) gegen den Agent-Prozess; Escape `AINETLINTER_NO_DAEMON=1` →
        klassisch in-proc (Debug-Ventil, dokumentiert inkl. Hermes-env:-Block-Hinweis);
        statistische Flags werden beim Daemon-Spawn durchgereicht.
  - [ ] B.4 MruStateStore: `%LOCALAPPDATA%\RalfHuesing\AiNetLinter\daemon-state.json`,
        Array `{rootPath, lastUsedUtc}` max maxProjects, debounced schreiben (~30 s
        nach letztem Touch) + beim Shutdown, atomar (temp + `File.Move`), korrupt/leer
        = „kein Warmup" — niemals Wahrheitsquelle.
  - [ ] B.5 Health/Observability: `get_server_health` weist Modus/Verbindungen/PID/
        Uptime/Keys/Daemon-Version aus; Call-Log um `connectionId`/`mode=daemon`
        erweitert (Observability-Paket ggf. minor-bump — eigener Scope, eigener Commit).
  - [ ] B.6 Tests: Unit in-proc (Handshake-/Versionsvergleichslogik inkl.
        Anti-Ping-Pong über injizierbaren Versionsprovider, Idle-Exit-Timer mit
        injizierbarer Clock, MRU schreiben/lesen/korrupt/tote Pfade,
        Connect-or-Start-State-Machine am Mock-Pipe); Integration sparsam echter
        Zwei-Prozess-Betrieb (zwei Thin-Clients teilen Warmth, via RefreshCount belegt;
        Idle-Exit innerhalb TTL schreibt MRU; Kaltstart-Warmup beschleunigt den ersten
        Call; Hänger-Pfad via Stellvertreter-Prozess statt EXE-Injektion; Escape) —
        Versions-Mismatch NICHT als Zwei-Prozess-Test; nichts Neues in `Category=Stress`.
  - [ ] B.7 DoD: Build grün; beide TestSuites ohne Stress grün; Epic-A-Suite weiterhin
        grün (Contract unverändert); Live-Dogfood (eigene Hermes-Registrierung +
        Repo-`.mcp.json` nutzen den Daemon-Modus; `get_server_health` weist
        Modus/Verbindungen/PID/Keys aus); Wiederöffnungsvermerk in
        `90_bewusst-nicht-umsetzen/Konzept.md` §C.5.
  - [ ] B.x Doku-Sammelpflichten (im fachlich berührenden Step):
        `Docs/agent-api.md` (Transport-/Lifecycle-Abschnitt), `Docs/integration.md`
        (Abschnitt „Daemon-Modus": Verhalten, Update-Handling, Debug-Escape),
        `Docs/configuration.md` (`--mcp-daemon-idle-exit-minutes`), `Docs/ROADMAP.md`,
        `README.md` (kurzer Abschnitt zum neuen Nutzungsmodell).
