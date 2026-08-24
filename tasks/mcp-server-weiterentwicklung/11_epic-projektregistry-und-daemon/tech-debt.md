---
task: 11_epic-projektregistry-und-daemon
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-24T12:15:00+02:00
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
| TD-001 | `src/AiNetLinter/Mcp/Projects/ProjectInstanceFactory.cs` + `src/AiNetLinter/Configuration/ConfigLoader.cs` | mittel | nein | Defekte (lesbare, aber ungültige) rules.json fällt im künftigen Registry-Pfad stumm auf Defaults zurück — kein deterministischer Fehlervertrag dafür |
| TD-002 | `src/AiNetLinter/Configuration/ConfigLoader.cs` | niedrig | nein | Diagnosen von TryLoadConfig gehen hart auf Console.Error (Kanal nicht injizierbar) — Misch-Thema erst mit dem Daemon (Epic B) |
| TD-003 | `src/AiNetLinter/Mcp/Projects/ProjectDefinitionLoader.cs` | niedrig | nein | Load(null/leerer projectRoot) löst implizit cwd-relativ auf — Ankerregel formal verletzt bis der Wiring-Guard existiert |
| TD-004 | `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` | mittel | nein | Soft-Cap: bei nur-busy Register wächst der Bestand über MaxProjects; TTL-Tick reklamiert Überhang nicht aktiv — Kapazitätsentscheidung fehlt bis Epic B |
| TD-005 | `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` + `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` | niedrig | nein | Disposal faulted Loads über den Sync-Eviction-Pfad schreibt [WARN] auf den nicht injizierbaren Console-Kanal (verwandt TD-002) |
| TD-006 | `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTestDoubles.cs` + `src/AiNetLinter.TestKit/TestConfigFactory.cs` | niedrig | nein | Exact-Duplikat der leeren Config-Erzeugung in getrennten Test-/TestKit-Grenzen; gemeinsame Ablage braucht eine Abhängigkeitsentscheidung |
| TD-007 | `src/AiNetLinter.IntegrationTests/Mcp/Platform/McpProcessHost.cs`, `McpRawWireTestHarness.cs` + Legacy-MCP-Prozesstests | niedrig | nein | Abdeckungsasymmetrie nach ThinClient-Umstellung: Bestandssuiten fixiert im Escape-Pfad, produktiver Daemon-Pfad nur durch wenige dedizierte Contracts gedeckt |

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
- **Status:** offen  # offen | erledigt | verworfen

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
- **Status:** offen  # offen | erledigt | verworfen

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
- **Status:** offen  # offen | erledigt | verworfen

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
