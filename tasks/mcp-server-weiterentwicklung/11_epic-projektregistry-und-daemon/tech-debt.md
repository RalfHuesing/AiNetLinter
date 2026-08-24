---
task: 11_epic-projektregistry-und-daemon
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-24T15:20:00+02:00
---

# Tech-Debt-Log: 11_epic-projektregistry-und-daemon

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem eigenen Korrektur-Step oder einem neuen Epic. Das entscheidet
grundsätzlich der Nutzer (manuell, z. B. durch Ergänzen eines Epics in
`roadmap.md` mit Verweis auf die Tech-Debt-ID).

**`auto_fixable` (`ja`/`nein`, siehe `../spec.md` §9.1) ist die einzige
Ausnahme:** rein mechanische, entscheidungsfreie Fixes ohne
Architektur-Ermessen dürfen vom Planer opportunistisch an einen ohnehin
laufenden Step angehängt werden (§10.6) — kein eigener Step, kein
eigener Sweep. Default bei Unsicherheit ist `nein`.

## Index

| ID | Bereich / Datei | Priorität | Auto-Fixable | Kurzfassung |
|---|---|---|---|---|
| TD-001 | `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` + `src/AiNetLinter/Configuration/ConfigLoader.cs` | mittel | nein | **[erledigt — step-016]** Deterministischer RULES_INVALID-Vertrag im Registry-Pfad (bestand bereits seit Epic-Wiring) um kopierfähige Bauanleitung ergänzt — kein Default-Fallback |
| TD-002 | `src/AiNetLinter/Configuration/ConfigLoader.cs` | niedrig | nein | Diagnosen von TryLoadConfig gehen hart auf Console.Error (Kanal nicht injizierbar) — Misch-Thema erst mit dem Daemon (Epic B) |
| TD-003 | `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` | niedrig | nein | **[erledigt — step-016]** Loader-Guard PROJECT_ROOT_REQUIRED mit Self-Service-Template schließt cwd-relative Restauflösung auf Load-Ebene |
| TD-004 | `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` | mittel | nein | **[erledigt — step-016, Akzeptanz]** Soft-Cap-Überlauf bei nur-busy Registern als gewollte Semantik festgenagelt (XML-Doc + Contract-Test) |
| TD-005 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` + `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` | niedrig | nein | Disposal faulted Loads über den Sync-Eviction-Pfad schreibt [WARN] auf den nicht injizierbaren Console-Kanal (verwandt TD-002) |
| TD-006 | `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTestDoubles.cs` + `src/AiNetLinter.TestKit/TestConfigFactory.cs` | niedrig | nein | Exact-Duplikat der leeren Config-Erzeugung in getrennten Test-/TestKit-Grenzen; gemeinsame Ablage braucht eine Abhängigkeitsentscheidung |
| TD-007 | `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs`, `McpRawWireTestHarness.cs` + Legacy-MCP-Prozesstests | niedrig | nein | Abdeckungsasymmetrie nach ThinClient-Umstellung: Bestandssuiten fixiert im Escape-Pfad, produktiver Daemon-Pfad nur durch wenige dedizierte Contracts gedeckt |
| TD-008 | `src/AiNetLinter.IntegrationTests/**` (benutzergebundener Pipe-Endpunkt) | mittel | nein | **[erledigt — step-016]** Suite-weites Cleanup/Gating: DaemonEndpointJanitor-Fixture räumt eigene Repo-Builds weg und skippt bei Fremdbelegung; ungeschützter Spawn-Pfad (McpServerLifetimeTests) auf Escape-Pfad gepinnt |
| TD-009 | `src/AiNetLinter/Mcp/*ToolRegistrations.cs` + `OverviewResourceRegistration.cs` (+ Prosa in `ServerInstructions`) | niedrig | nein | Jeder MCP-Toolname liegt als Literal an Registrierung UND Overview-Tabelle (plus Prosa-Nennungen); Rename-Drift Registrierung↔Tabelle ist zwar testbewacht, bleibt aber Doppel-Pflege |
| TD-010 | `Docs/agent-api.md` (Fehlertabelle) | niedrig | nein | **[erledigt — step-016]** Stale AMBIGUOUS_SOLUTION-Zeile entfernt; übrige Tabelle gegen Emittenten verifiziert |

## Einträge

### TD-001 — Defekte Regeldatei: stummer Default-Fallback im Registry-Pfad [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-23)
- **Ort:** `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs:17` (`MaterializeRules` → `ConfigLoader.TryLoadConfig`)
- **Befund:** `ConfigLoader.TryLoadConfig` gibt bei lesbarer, aber ungültiger `rules.json`
  `null` zurück (stderr-Diagnose + Rückgabewert null — der `isRequired`-Parameter betrifft
  nur leere Pfadangaben, nicht defekte Inhalte); `MaterializeRules` fängt das mit
  Defaults ab. Im Batch-Pfad ist das gepinnt korrekt. Im späteren Registry-Pfad
  (`ProjectInstanceFactory.Create`, `isRequired: true`) lädt ein Projekt mit DEFEKTER
  rules.json damit stumm mit Default-Regeln weiter — genau die stille Fehl-Bindung,
  gegen die das Epic gebaut wird. Konzept A.5 kennt dafür keinen Fehlercode
  (`RULES_NOT_FOUND` deckt nur fehlende Dateien). Der Coder hat die Lücke im
  step-result.md selbst gemeldet; Verifikation per `get_symbol_body` bestätigt sie.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 — das Batch-Verhalten
  ist Bestands-/Step-Vertrag; ein eigener Vertrag für defekte Regeldateien im Registry-Pfad
  (z. B. neuer Code oder sichtbare Markierung) ist eine Konzept-relevante
  Vertragsentscheidung, keine mechanische Korrektur.
- **Vorschlag:** Im Wiring-Step von Epic A entscheiden: entweder deterministischer
  Fehlervertrag für parse-defekte Regeldateien im Registry-Pfad (z. B. `RULES_INVALID`,
  ergänze Konzept A.5) oder mindestens sichtbare Markierung des Default-Fallbacks im
  Tool-Antwortpfad (`UsedDefaultConfig=true` auswerten). Bis dahin dokumentiert dieser
  Eintrag die bekannte Lücke.
- **Auto-Fixable:** nein — Verhaltens- und Vertragsentscheidung mit Architektur-Ermessen.
- **Status:** erledigt (step-016)  # offen | erledigt | verworfen
  Umsetzung: Der deterministische `RULES_INVALID`-Vertrag bestand im Registry-Pfad
  (`ProjectInstanceFactory.TryCreate`) bereits seit dem Epic-Wiring; step-016 ergänzte
  die fehlende kopierfähige Bauanleitung (minimales rules.json-Skelett im Fehlertext)
  samt Testabsicherung. Batch-Pfad unverändert (dokumentierter Default-Fallback).

### TD-002 — Diagnosekanal von ConfigLoader nicht injizierbar [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-23)
- **Ort:** `src/AiNetLinter/Configuration/ConfigLoader.cs` (`TryLoadConfig`, drei `Console.Error.WriteLine`-Stellen)
- **Befund:** `TryLoadConfig` schreibt Diagnosen direkt auf `Console.Error` — kein
  injizierbarer Ausgabekanal. Solange nur der Batch-Prozess läuft, unkritisch. Bedient der
  Daemon (Epic B) mehrere Projekte/Verbindungen in einem Prozess, mischen sich diese
  Meldungen untereinander und mit dem Protokoll-/Antwortpfad, ohne Zuordnung zu
  Verbindung/Key. Vom Coder im step-result.md gemeldet; per `get_symbol_body` bestätigt.
- **Warum nicht sofort gefixt:** Bestandscode außerhalb des Step-Scopes; eine Injektion
  (z. B. `ILintConsole`/Channel-Parameter durchreichen) ändert interne Signaturen und
  betrifft mehrere Call-Sites — eigenständige Entscheidung.
- **Vorschlag:** Mit dem Epic-B-Ausbau von Health/Observability prüfen, den
  Diagnosekanal zu injizieren und ins Observability-Log je Verbindung zu führen.
- **Auto-Fixable:** nein — API-/Signaturänderung mit Integrationsentscheidung.
- **Status:** offen  # offen | erledigt | verworfen

### TD-003 — Loader ohne Root-Guard: cwd-relative Restauflösung bis zum Wiring [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-23)
- **Ort:** `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` (`Load`, `projectRoot ?? string.Empty`)
- **Befund:** Bei null/leerem/Whitespace-`projectRoot` baut der Loader einen relativen
  Definitionsdatei-Pfad; die Existenzprüfung läuft dann implizit gegen den Prozess-cwd —
  formal ein Verstoß gegen die Ankerregel A.2 („nie zum cwd"). Bis zum Wiring-Step ist der
  Pfad unerreichbar (`projectRoot` wird dort Pflicht UND absolut sein:
  `PROJECT_ROOT_REQUIRED`/`PROJECT_ROOT_INVALID` auf Argumentebene); vom Coder im
  step-result.md als „Bekannte Unschärfe" gemeldet und plan-konform bewusst so belassen.
- **Warum nicht sofort gefixt:** Der Step-Plan legt die Root-Validierung ausdrücklich auf
  die Argumentebene des Wiring-Steps; ein eigener Guard im Loader wäre Doppelvalidierung
  bzw. eine Vertragsänderung in diesem Step.
- **Vorschlag:** Der Wiring-Step muss garantieren, dass kein `Load`-Aufruf mit leerem
  Root erfolgt; dort sinnvoll einen Contract-Test ergänzen, der das absichert (z. B. über
  die Argumentvalidierung vor dem ersten Registry-Zugriff).
- **Auto-Fixable:** nein — Verhaltensfrage (wo validiert wird) mit Test-Entscheidung.
- **Status:** erledigt (step-016)  # offen | erledigt | verworfen
  Umsetzung: Guard am Anfang von `ProjectDefinitionLoader.Load` — null/leer/Whitespace
  liefert sofort `Failure(PROJECT_ROOT_REQUIRED, RootRequiredTemplate())` mit wörtlichem
  Self-Service-Template (Stil wie `NotInitializedTemplate`); Theory-Test deckt
  null/""/"   " ab. Gültige absolute Roots unverändert.

### TD-004 — Soft-Cap: kein aktives Reklamieren von Überhang über MaxProjects [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-23; vom Coder im step-result.md selbst gemeldet)
- **Ort:** `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:186-207` (`EvictLeastRecentlyUsed`) und `:154-177` (`InsertResident`)
- **Befund:** Ist das Register voll und sind ALLE Entries busy (`InFlightCount > 0`), bricht die
  LRU-Eviction ergebnislos ab und der neue Entry wird trotzdem angelegt — der Bestand liegt
  kurzzeitig über `MaxProjects`. Der TTL-Tick reklamiert solchen Überhang nicht aktiv; er räumt
  nur nach Idle-TTL, FAILED oder ausgereifter PendingEviction. Überhang baut sich erst wieder
  über Idle-TTL bzw. LRU-Druck bei künftigen Inserts ab. Die implementierte Semantik folgt
  zwingend aus zwei Konzept-Regeln (Sync-Lease darf weder blockieren noch ablehnen;
  Busy-Guard verbietet Eviction laufender Calls) — das Konzept A.7 adressiert den entstehenden
  Überschuss aber nicht. Für Epic B (langlebiger Daemon, RAM-Hygiene) ist eine explizite
  Kapazitätsentscheidung nötig.
- **Warum nicht sofort gefixt:** Jede harte Kapazitätsdurchsetzung (Call blocken/rejecten/queeuen)
  ändert den Lease-Vertrag und wäre eine Konzept-Erweiterung — außerhalb des Step-Scopes.
- **Vorschlag:** Mit Epic-B-Kapazitätsplanung entscheiden: entweder dokumentiertes Soft-Cap als
  Vertrag (Überschuss nur transient) oder tick-seitige Cap-Reklamation/harte Ablehnung bei
  vollem Register. Bis dahin dokumentiert dieser Eintrag die bekannte Ecke.
- **Auto-Fixable:** nein — Vertrags- und Architekturentscheidung (Verhalten bei Volllast).
- **Status:** erledigt (Akzeptanz, Nutzerentscheid 2026-08-24; step-016)  # offen | erledigt | verworfen
  Umsetzung: KEINE Verhaltensänderung — Überlauf bei nur-busy Registern ist gewollte
  Semantik. Festgenagelt durch XML-Doc an `ProjectRegistry.EvictLeastRecentlyUsed`
  (mit Nutzerentscheid-Datum) und Contract-Test
  `ProjectRegistryCapacityContractTests.Lease_AllSlotsBusy_AllowsOverflowUntilSlotFreesThenEvictsAgain`
  (injizierbare Clock wie bestehende Eviction-Tests).

### TD-005 — [WARN] auf Console-Kanal beim Disposal faulted Loads im Sync-Eviction-Pfad [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-23; verwandt zu TD-002, vom Coder im step-result.md gemeldet)
- **Ort:** `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (`DisposeAsync`, `WriteError` bei nicht-abbrechbarem Hintergrund-Load); Auslöser: `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:45-48` (Sync-Pfad disposed verdrängte Server über `Dispose()`)
- **Befund:** Räumt der synchrone `Lease`-Pfad einen FAILED-Marker (oder einen sonstwie
  faulted Load), läuft der Bestands-Wrapper `McpCodeGraphServer.Dispose()` → `DisposeAsync()`
  und schreibt bei nicht abbrechbarem Hintergrund-Load eine `[WARN]`-Zeile über den
  Console-Kanal. Der Kanal ist nicht injizierbar (gleiches Grundthema wie TD-002, dort für
  `ConfigLoader` festgehalten); im Daemon-Betrieb (Epic B, Stdio-Purity B.3) mischen sich solche
  Zeilen ohne Key-/Verbindungsbezug unter den Protokollpfad.
- **Warum nicht sofort gefixt:** Bestandscode außerhalb des Step-Scopes; Injektion des Kanals
  betrifft Signaturen und mehrere Call-Sites — dieselbe Entscheidung wie TD-002, erst mit Epic B.
- **Vorschlag:** Zusammen mit TD-002 in Epic B lösen (injizierbarer Diagnosekanal je
  Verbindung/Key); bis dahin die mögliche `[WARN]`-Zeile beim FAILED-Räumungspfad als bekannt
  einstufen.
- **Auto-Fixable:** nein — API-/Signaturänderung mit Integrationsentscheidung.
- **Status:** offen  # offen | erledigt | verworfen

### TD-006 — Exact-Duplikat der leeren Test-Config über Testgrenzen [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-008 (einmaliger Epic-Drift-Audit vom 2026-08-24)
- **Ort:** `TrackingServerFactory.MinimalConfig()` und
  `TestConfigFactory.CreateEmpty()`; beide liefern `Config` mit `GlobalConfig`
  und `MetricsConfig`.
- **Befund:** Der tokenbasierte Audit meldete einen exact-Cluster. Die Methoden
  sind semantisch gleich, werden aber in unterschiedlichen Grenzen verwendet:
  der erste Helper ist privat im Registry-Test-Double, der zweite ist eine
  öffentliche TestKit-Fabrik mit Aufrufern in Fast- und IntegrationTests.
- **Warum nicht sofort gefixt:** Eine Konsolidierung in das TestKit würde die
  Abhängigkeitsrichtung und die Zuständigkeit des Registry-Test-Doubles ändern;
  das ist keine entscheidungsfreie Änderung im EPIC-A-Abschluss-Step.
- **Vorschlag:** Bei einer späteren Testinfrastruktur-Konsolidierung prüfen, ob
  `TrackingServerFactory` die öffentliche TestKit-Fabrik verwenden kann, ohne
  Test-Intent oder Projektabhängigkeiten zu verschieben.
- **Auto-Fixable:** nein — Testinfrastruktur-/Abhängigkeitsentscheidung.
- **Status:** offen  # offen | erledigt | verworfen

### TD-007 — Abdeckungsasymmetrie: Bestands-MCP-Suiten fixiert im Escape-Pfad [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-013 (Kritiker-Review vom 2026-08-24)
- **Ort:** `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs:61-68`
  (env-Pinning `AINETLINTER_NO_DAEMON=1`), `McpRawWireTestHarness.cs`
  (Default `noDaemon: true`) sowie die darauf aufsetzenden Bestandstests
  (`McpHandshakeToolRegistrationTests`, `McpObservabilityE2ETests`,
  `McpServerCommandErrorHandlingTests`, Framing-Tests).
- **Befund:** Mit der ThinClient-Umstellung des normalen `--mcp-server`-Pfads
  wurden die Legacy-MCP-Prozesstests bewusst und regelkonform (Richtlinien §4:
  gezielte Isolation statt Collection-Serialisierung) auf den In-proc-Escape-
  Pfad gepinnt, damit sie sich im Vollparallelauf nicht am gemeinsamen
  benutzergebundenen Pipe-Endpunkt stören. Folge: Die breite Toolverhaltens-
  regression läuft seither ausschließlich gegen den In-proc-Stack; der
  produktive Daemon-Pfad (Spawn, Handshake, Pump, Retry) wird nur von wenigen
  dedizierten Contracts abgedeckt (`ThinClientMcpProcessContractTests`,
  `DaemonHostProcessContractTests`, `DaemonHostMcpProcessContractTests`).
  Kein Defekt dieses Steps — eine dauerhafte Testdesign-Entscheidung, die
  künftige Steps bewusst fortschreiben sollten.
- **Warum nicht sofort gefixt:** Eine selektive Ausweitung der Daemon-Pfad-
  Abdeckung (weitere `noDaemon: false`-Szenarien im Raw-Wire-Harness) muss
  gegen die Endpunkt-Kollisionsgefahr im Parallellauf entworfen werden
  (eigenständige Fixtures, kurzer Idle-Exit, ggf. Suite-Marker) — eine
  Testarchitektur-Entscheidung mit Ermessen, kein mechanischer Fix und nicht
  Teil des F1/F2-Korrekturumfangs von step-013.
- **Vorschlag:** In einem späteren Step mit Testinfrastruktur-Fokus schrittweise
  kollisionsfreie Daemon-Pfad-Szenarien ergänzen (Purity-All-Zeilen-Assertion
  für `noDaemon: false`, Zwei-Clients-Varianten), ohne die Serialisierungs-
  verbote von Richtlinien §4 zu verletzen.
- **Auto-Fixable:** nein — Testdesign-/Isolationsentscheidung.
- **Status:** offen  # offen | erledigt | verworfen

### TD-008 — Überlebende Daemons am gemeinsamen Endpunkt als suite-weite Flakiness-Quelle [Priorität: mittel] [Auto-Fixable: nein]

- **Gefunden in:** step-014 (Kritiker-Review vom 2026-08-24)
- **Ort:** `src/AiNetLinter.IntegrationTests/**` — der benutzergebundene
  Pipe-Endpunkt `ainetlinter.analyzer.v1.<username>` wird suite-weit geteilt;
  neue Daemon-Läufe gate zwar über `AcquireEndpointAsync`
  (`DaemonProcessContractHarness`), aber kein Fixture räumt
  *fremde/überlebende* Daemons weg, bevor Tests am Endpunkt binden.
- **Befund:** Im Vollstack-Lauf des Coders störte ein einziger überlebender,
  detached gespawnter Daemon (Default-Idle-Exit 10 min, aus einer manuellen
  MCP-Gate-Session) vier Bestands-/Neue Tests gleichzeitig (Doppelstart-Lock,
  fremder Repo-Key in Health-Antworten, Exit 1 beim Handshake-Lauf). Auch im
  isolierten Kritiker-Nachlauf des N4-Contracts blieb der Daemon einige
  Sekunden nach dem Teardown-Kill sichtbar, bevor er sich selbst beendete —
  im parallelen Volllauf ist dieses Fenster eine reale Störquelle. Die
  Step-013-Isolation (Escape-Pinning) mildert das für die Bestandssuiten; die
  seit step-014 wachsende Zahl echter Daemon-Läufe erhöht die Exposition
  erneut. Kein Defekt dieses Steps — strukturelle Testumgebungs-Schwäche.
- **Richtung (Nutzer-Entscheid):** Suite-weites Cleanup-/Gating-Fixture
  (z. B. Endpunkt-Claim mit Fremd-Daemon-Erkennung und kontrolliertem Kill
  oder dedizierter Test-Endpunkt via Env), bevor weitere Daemon-Pfad-Tests
  dazukommen; Serialisierungsverbote von Richtlinien §4 bleiben gewahrt.
- **Auto-Fixable:** nein — Architektur- und Isolationsentscheidung.
- **Status:** erledigt (step-016)  # offen | erledigt | verworfen
  Umsetzung: `DaemonEndpointJanitor` als xUnit-v3-Assembly-Fixture — beendet vor dem
  ersten Endpunkt-Zugriff Daemon-Prozesse eigener Bauart (Bildpfad = Test-EXE bzw.
  Build-Ausgabe dieses Repos; nie blinde Namens-Matches, unlesbare Bildpfade unangetastet),
  verifiziert den Endpunkt per Client-Probe und skippt transparent mit Begründung bei
  nicht behebbarer Fremdbelegung. Choke-Point ist `AcquireEndpointAsync`; zusätzlich
  wurde der letzte ungeschützte Spawn-Pfad (`McpServerLifetimeTests`, startete Daemons
  mit Default-10-min-Idle-Exit = empirisch belegte Kontaminationsquelle in zwei
  Vollstack-Läufen) auf den Escape-Pfad gepinnt. Simulation eines hängenden Daemons
  wurde nachgewiesen: Janitor identifizierte/beendete ihn, Contract-Test lief grün.

### TD-009 — MCP-Toolnamen doppelt gepflegt: Registrierung ↔ Overview-Tabelle (+ Prosa) [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-015 (Kritiker-Review vom 2026-08-24; vom Coder im step-result.md als systemische Beobachtung gemeldet)
- **Ort:** `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` und die übrigen Tool-Registrierungsklassen sowie `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs` (`ToolSummaries`); zusätzlich Prosanennungen von Toolnamen (z. B. Workflow-Zeilen in `ServerInstructions.Text`, Doku).
- **Befund:** Jeder der ~24 MCP-Toolnamen liegt als String-Literal an mindestens zwei unabhängigen Stellen (Registrierung + Overview-Tabelle), teils auch in Prosa-Workflowtexten. Ein Rename erfordert parallele Pflege; das Drift-Audit flaggte nur `get_class_structure`, weil der Classifier ältere Namen whitelistet — das Muster ist aber codebase-weit. **Mildern:** `OverviewResourceRegistrationTests.ToolSummaries_MatchesRegisteredToolNames` (FastTests/Mcp/OverviewResourceRegistrationTests.cs:100) prüft die Mengengleichheit der Namen gegen die echte `ToolCollection` — Registrierungs↔Tabelle-Drift fällt sofort als roter FastTest auf, nicht erst zur Laufzeit. Verbleibender Rest: Wartungsaufwand je Rename und ungeschützte Prosa-Nennungen.
- **Warum nicht sofort gefixt:** Step-015 durfte nur Funde aus den drei Audit-Läufen fixen; ein Einzelfix des einen geflaggten Paares wäre Inkonsistenz (korrekt als No-op dokumentiert). Die Wahl des Mechanismus — Namenskonstanten, generierte Overview-Tabelle oder nur Prosa-Absicherung per Test — ist eine Architektur-/Testdesign-Entscheidung.
- **Vorschlag:** In einem späteren Infrastruktur-/Refactoring-Step zentrale Namensquelle oder Generierung der Overview-Tabelle umsetzen und Prosa-Toolnennungen testabsichern.
- **Auto-Fixable:** nein — Mechanismuswahl mit Architektur-Ermessen.
- **Status:** offen  # offen | erledigt | verworfen

### TD-010 — Stale Doku-Zeile `AMBIGUOUS_SOLUTION` in Docs/agent-api.md [Priorität: niedrig] [Auto-Fixable: nein]

- **Gefunden in:** step-015 (Kritiker-Review vom 2026-08-24; vom Coder im step-result.md gemeldet)
- **Ort:** `Docs/agent-api.md:834` (Fehlertabelle „Bedeutung im MCP-Kontext")
- **Befund:** Die Zeile beschreibt `AMBIGUOUS_SOLUTION` als aktiven Fehlercode („Batch-Modus: mehrere `.sln`/`.slnx` im `cwd` ohne `--path`"). Im Code existiert seit dem EPIC-A-Wiring (Commit `ccf7b33a`, dort fiel auch der assertierende E2E-Test weg) kein Emitter mehr; step-015 entfernte lediglich die verwaiste Konstante `LinterErrorCodes.AmbiguousSolution`. Das ist ein Verstoß gegen Richtlinien §1 Dokumentations-Objektivität („Nur Implementiertes dokumentieren") — aber NICHT eine unvollständige Ausführung von step-015: die Zeile war vor dem Step bereits stale, der Plan schloss Doku-Sync explizit aus, und die §4-Update-Pflicht greift weder nach Dateiliste (`agent-api.md` fehlt) noch nach Anlass (Totcode-Entfernung = keine Feature-/Konfigurationsänderung). Der zweite Halbsatz der Zeile („MCP lehnt `--path` stattdessen per Hard-Cut ab") ist korrekt und zu erhalten.
- **Warum nicht sofort gefixt:** Außerhalb des Step-Scopes; die korrekte Ersatzformulierung verlangt die Verifikation des heutigen Batch-Restverhaltens bei mehreren Solutions im cwd (nicht durch diesen Review geleistet) plus Formulierungsurteil.
- **Vorschlag:** In einem Doku-Pflicht-Step die Tabellenzeile an das tatsächliche Verhalten anpassen (Hard-Cut-Halbsatz behalten, `AMBIGUOUS_SOLUTION` entfernen bzw. als historisch kennzeichnen) und stichprobenartig die übrige Tabelle gegen `McpToolResults`/`LinterErrorCodes` prüfen.
- **Auto-Fixable:** nein — Inhaltliche Verifikation des Batch-Restverhaltens und Formulierungsurteil nötig, kein blindes Edit.
- **Status:** erledigt (step-016)  # offen | erledigt | verworfen
  Umsetzung: `AMBIGUOUS_SOLUTION`-Zeile aus der Fehlertabelle entfernt. Die übrige
  Tabelle wurde vollständig gegen die Emittenten verifiziert (Konstanten in
  `LinterErrorCodes`/`ProjectErrorCodes` + Nutzungsstellen) — kein weiterer toter
  Code gefunden.
