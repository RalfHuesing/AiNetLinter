---
status: open
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 011
corrects: step-010
title: "DaemonHost-Korrektur: deterministische Exklusivität, MRU-Normalisierung und echte Lifecycle-Contracts"
epic: EPIC-B
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T03:21:39+02:00
related_to:
  - step-010/step-plan.md
  - step-010/step-result.md
  - step-010/step-review.md
---

# Step 011: DaemonHost-Korrektur: deterministische Exklusivität, MRU-Normalisierung und echte Lifecycle-Contracts

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-B` aus `roadmap.md` — B.3/B.4-Lifecycle und die im selben
  großen B-Cluster erforderlichen Host-/MCP-Contracts werden nach dem
  `issues`-Review von step-010 belastbar geschlossen.
- **Korrektur:** `corrects: step-010`; ausschließlich die vier Findings aus
  `step-010/step-review.md` sind Umfang dieses Steps.
- **Konzept-Referenz:** `Konzept.md` B.2 (Single-Instance-Race und
  Verbindungsmodell), B.3 (Idle-Exit/Shutdown), B.4 (MRU-State), B.5 Schritt 2
  (Doppelstartvertrag), B.6/B.7 (In-proc-/Integrationscontracts und DoD).

## Aktueller Projektzustand (JIT-Kontext)

- `DaemonPipeTransport.CreateServerStream` erzeugt pro Accept-Zyklus eine
  `NamedPipeServerStream` mit `MaxAllowedServerInstances`. Das ist für mehrere
  Verbindungen richtig, verhindert aber keinen zweiten `DaemonHost`-Prozess.
  Der Korrekturvertrag braucht daher eine Host-weite, nach Pipe-Endpoint
  benannte, nicht dateibasierte Exklusivität vor dem ersten Pipe-Bind.
- `DaemonHost.RegisterConnection` startet den Handler, bevor `connections` und
  `connectionHandles` registriert sind. `HandleConnectionAsync` entfernt im
  `finally` beide Einträge; bei synchronem EOF kann danach ein abgeschlossener
  Handle wieder als aktiv eingetragen werden. Die Lifecycle-Registrierung muss
  vor dem Handlerstart unter `lifecycleGate` erfolgen und eine eindeutige
  Abschlussbereinigung besitzen.
- `MruStateStore.Read` toleriert leere/korrupt serialisierte Dateien, markiert
  diesen Normalisierungsbedarf aber nicht; `DisposeAsync` schreibt nur bei
  `dirty`. Außerdem werden validierte Root-Pfade vor dem Dictionary-Eintrag
  nicht durchgehend kanonisiert, sodass `Remove` eine alternative Schreibweise
  verfehlen kann. `CanonicalizeRoot`, `Read`, `Remove`, `Snapshot` und
  `DisposeAsync` bleiben die vorhandenen Anker; kein zweiter MRU-Store wird
  eingeführt.
- Die vorhandenen FastTests treffen überwiegend Test-Seams (`IsIdleExitDue`,
  `WarmupForTestAsync`, MRU-/CLI-Methoden). Für `RunAsync`, `AcceptLoopAsync`,
  `HandleConnectionAsync` und `RunMcpSessionAsync` fehlt ein direkter Vertrag.
  Bestehende Pipe-/Handshake-Typen, `ProjectRegistry`,
  `DaemonHostCommand.CreateSessionRunner` und das MCP-SDK werden erweitert bzw.
  über testbare Transport-/Session-Seams verwendet; ThinClient-Code bleibt
  unberührt.

## Intention

Nach diesem Step kann pro benutzerspezifischem Pipe-Endpoint höchstens ein
`DaemonHost` den Host-Lock halten und ein zweiter Start endet vor dem Pipe-Bind
deterministisch mit sachlicher stderr-Diagnose und Exit-Code ungleich null. Der
MRU-State wird nach tolerantem Lesen in eine kanonische, gültige Form überführt
und beim Shutdown auch ohne Touch zuverlässig atomar geschrieben. Direkte
In-proc-, Host- und MCP-Contracts beweisen zusätzlich Registrierung vor Handler-
Start, schnellen Disconnect, Handshake-vor-Session, verbindungsbezogene
Cancellation und genau einmalige Bereinigung.

## Umfang

- Nur die vier Findings aus step-010: Single-Instance-Lock, leer/korrupt
  erzwungener Shutdown-Flush, kanonische Entfernung toter MRU-Roots und
  Connection-Registration-Race samt fehlenden echten Contracts.
- Der Lock ist ein BCL-/OS-basierter, nicht dateibasierter Mutex-/Handle-Vertrag
  für den vollständigen Daemon-Lifecycle. Er wird vor dem ersten
  `CreateServerStream` erworben, bei Nichterwerb ohne unbehandelte Exception
  gemeldet und erst nach Host-, Registry- und MRU-Shutdown freigegeben.
- Tests bleiben deterministisch: kontrollierbare In-proc-Transporte/Connections
  für Interleavings, direkter Hoststart für den echten Named-Pipe-/Lock-Vertrag
  und eine MCP-SDK-Session auf der Host-Pipe ohne ThinClient.

## Nicht-Umfang

- Kein `ThinClientProxy`, kein `ThinClientLauncher`, kein Connect-or-Start,
  kein detached Spawn, kein Retry, kein Ping/Kill/Restart und keine
  `AINETLINTER_NO_DAEMON`-Verdrahtung. Der einzige vorgezogene Race-Vertrag ist
  die explizit nötige Host-weite Doppelstart-Sperre.
- Keine Änderung an MCP-Toolanzahl, `projectRoot`-Pflicht, Registry-Lease-/Load-
  Dedupe-Semantik, Handshake-Version, NDJSON-Framing oder per-connection
  `MaxAllowedServerInstances` des bereits laufenden Hosts.
- Kein Health-/Observability-Ausbau, keine Clientregistrierungs-/Hermes-/
  `.mcp.json`-Migration, kein Batch-Umbau und keine neue NuGet-Abhängigkeit.
- Keine eigenständige Dokumentations- oder Konzeptabspaltung; nur die
  fachlich nötigen Step-, Task-State- und groben Roadmap-Verweise werden im
  selben B.3–B.5-Cluster gepflegt.
- Kein vollständiger Nicht-Stress-Testlauf, kein Code-Commit des Coder-Schritts
  und kein Drift-Audit in diesem Planungs-Step; diese Ausführungspflichten
  bleiben im DoD des Umsetzungsschritts gemäß Task-State.

## Akzeptanzkriterien

- [ ] Vor dem Erzeugen der ersten Named-Pipe-Serverinstanz erwirbt der Host
      genau einen benutzerspezifischen, nach dem effektiven Pipe-Namen
      gebundenen nicht dateibasierten Lock. Zwei parallele `DaemonHost`-Starts
      für denselben Endpoint können nicht beide den Accept-Loop betreiben.
- [ ] Der Lock-Verlierer liefert eine sachliche, deterministische stderr-
      Diagnose und Exit-Code `!= 0`; es gibt weder unbehandelte Exception noch
      Ersetzen/Beenden des bereits laufenden Hosts. Der Lock bleibt bis nach
      vollständigem Shutdown gehalten und wird auch bei Cancellation/Fehlerpfad
      freigegeben.
- [ ] Die bestehende per-connection-Pipe-Konfiguration mit
      `MaxAllowedServerInstances` bleibt für den einzigen laufenden Host
      erhalten; Single-Instance und Multi-Connection sind getrennte Verträge.
- [ ] Ein schneller EOF/Disconnect während der Connection-Registrierung lässt
      keine abgeschlossene Task und keinen Handle in `connections` oder
      `connectionHandles` zurück. Die Invariante lautet danach
      `connections.Count == connectionHandles.Count == clientCount == 0`,
      `ActiveConnectionCount == 0`, und `IsIdleExitDue()` kann nach Ablauf der
      Idle-Zeit true werden.
- [ ] Eine Verbindung wird unter `lifecycleGate` vollständig registriert,
      bevor ihr Handler laufen kann; die Abschlussroutine ist idempotent und
      entfernt genau den zugehörigen Eintrag. Parallele andere Verbindungen
      bleiben davon unberührt.
- [ ] Der Handshake wird auf der echten Host-Verbindung vor jeder MCP-Session-
      Erstellung bzw. jedem MCP-Durchsatz geschrieben. Ein Disconnect cancelt
      nur die Session dieser Verbindung; Registry und andere Sessions bleiben
      verfügbar.
- [ ] Bei jeder normalen Host-Beendigung wird ein gültiges, atomar ersetztes
      MRU-Array als finaler Snapshot versucht, auch wenn der gelesene Vorgänger
      leer oder korrupt war und kein Touch stattfand. IO-Fehler werden geloggt
      und blockieren den Daemon nicht.
- [ ] Jeder validierte MRU-Eintrag wird vor Speicherung, Deduplizierung,
      Rückgabe und Entfernung kanonisiert; ungültige Einträge werden verworfen.
      Ein toter Root in alternativer Schreibweise wird beim Warmup entfernt und
      erscheint nach dem Shutdown nicht erneut im State.
- [ ] Direkte Contracts decken `DaemonHost.RunAsync`, `AcceptLoopAsync`,
      `HandleConnectionAsync` und `DaemonHostCommand.RunMcpSessionAsync` ab;
      die bisherigen Test-Seams allein gelten nicht als Nachweis.
- [ ] Der unveränderte `--mcp-server`-Stdio-Pfad sowie EPIC-A-Registry- und
      Step-009-Handshake-Verträge bleiben grün; kein ThinClient-Verhalten wird
      als implementiert vorausgesetzt.

## Konkrete Änderungen

### `src/AiNetLinter/Mcp/Daemon/DaemonHost.cs` und schlanker Lock-Helfer

- **Was:** Einen benannten, nicht dateibasierten `DaemonInstanceLock` (oder
  gleichwertigen kleinen BCL-Helper) in die Host-Komposition aufnehmen. Der
  Lock-Key wird aus dem bereits kanonischen Pipe-Endpoint inklusive
  Benutzerbindung gebildet; Acquire erfolgt vor `AcceptLoopAsync`/dem ersten
  Serverstream. `DaemonHost.RunAsync` und `DisposeAsync` sichern die
  Besitzdauer, Fehlerdiagnose und Freigabe in einem klaren Erfolgs- und
  Cleanup-Pfad. Für deterministische Tests einen test-only Lock-/Transport-Seam
  vorsehen, ohne den produktiven Named-Mutex-Vertrag zu umgehen.
- **Warum:** `MaxAllowedServerInstances` löst nur parallele Connections eines
  Hosts. Der B.5-Doppelstartvertrag benötigt zusätzlich eine prozessübergreifend
  entscheidende Exklusivität, ohne den späteren Connect-or-Start-Clientpfad
  einzuführen.

### `src/AiNetLinter/Mcp/Daemon/DaemonHost.cs` — Connection-Lifecycle

- **Was:** `RegisterConnection`, `AcceptLoopAsync`, `HandleConnectionAsync`,
  `RegisterClient`/`UnregisterClient` und die Session-Warte-/Shutdown-Pfade auf
  eine Registrierung-vor-Start-Ordnung umstellen. Ein registrierter Eintrag
  erhält eine eindeutige, idempotente Completion-Bereinigung; schnelle EOFs und
  Handler, die vor dem ersten Await abschließen, müssen denselben Cleanup-Pfad
  durchlaufen. `ActiveConnectionCount` und Idle-Exit lesen dieselbe autoritative
  Lifecycle-Wahrheit.
- **Warum:** Das beseitigt den in Finding 4 beschriebenen stale-handle-Race und
  macht Idle-Exit ohne Timing-Glück prüfbar.

### `src/AiNetLinter/Mcp/Daemon/MruStateStore.cs`

- **Was:** `Read` markiert leere/korrupt serialisierte vorhandene Dateien als
  Normalisierungsbedarf; `DisposeAsync` erzwingt unabhängig von `dirty` einen
  finalen Snapshot-Schreibversuch. `CanonicalizeRoot` wird vor jedem Eintrag in
  `entries` angewandt; `Snapshot`, `Remove` und die Warmup-Entfernung verwenden
  ausschließlich diesen kanonischen Schlüssel. Ungültige Roots werden als
  verworfene State-Daten behandelt und führen ebenfalls zur Normalisierung.
- **Warum:** MRU ist nur ein Warmstart-Hinweis, muss aber nach einem tolerierten
  Korruptionsfall nicht dauerhaft korrupt bleiben und darf tote Roots nicht in
  Alias-Schreibweisen wieder persistieren.

### `src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs` und bestehende
`DaemonPipe*`-Typen

- **Was:** Die Host-Command-Komposition so ergänzen, dass Lock-Erwerb/-Verlust
  und der tatsächliche Host-Returncode nach außen sauber abgebildet werden. Die
  bestehende Session-Fabrik bleibt die einzige MCP-Session-Erzeugung; nur die
  testbare direkte Host-/MCP-Verbindung und deren Cleanup werden explizit
  vertraglich zugänglich gemacht. `DaemonPipeTransport.CreateServerStream`
  bleibt multi-connection-fähig und erhält keinen Connect-or-Start-Code.
- **Warum:** Der CLI-Returncode muss den Lock-Vertrag beweisen, während die
  MCP-Session weiterhin exakt auf dem Step-009-Transport und der geteilten
  Registry aufsetzt.

### `src/AiNetLinter.FastTests/Mcp/Daemon/`

- **Was:** Bestehende `DaemonHostLifecycleTests`,
  `DaemonPipeTransportContractTests` und `MruStateStoreTests` um deterministische
  Contracts ergänzen; bei Bedarf einen fokussierten Test-Harness/helper in
  demselben Verzeichnis anlegen. Abdecken: Lock-Winner/Loser und Freigabe,
  Registrierung-vor-Handlerstart mit synchronem EOF, Idle-Exit nach Cleanup,
  Handshake-vor-Session, verbindungsbezogene Cancellation und genau einmaliges
  Dispose; außerdem korrupt/leer → final gültiges `[]` sowie alternativer
  Root-Alias → persistente Entfernung. In-proc-Seams dürfen nur Timing und
  Transport kontrollieren, nicht die zu beweisende Host-Reihenfolge auslassen.
- **Warum:** Die Race- und State-Verträge werden ohne Timing-Glück und ohne
  unnötige Prozesslast regressionsfest.

### `src/AiNetLinter.IntegrationTests/Mcp/Daemon/`

- **Was:** Einen kleinen direkten Host-/MCP-Contract gegen die echte EXE
  ergänzen: erster `--daemon-start` bindet, ein direkter Pipe-Client führt
  Step-009-Handshake und eine echte MCP-SDK-Session aus, schneller Disconnect
  lässt den Host nach TTL auslaufen, und ein zweiter paralleler
  `--daemon-start` erhält stderr + Nicht-Null-Exit. Teste außerdem den
  Shutdown-MRU-Flush aus korruptem/leerem Vorgänger und die Entfernung eines
  toten Roots in Alias-Schreibweise über einen isolierten Test-State-Pfad.
  Der Test verbindet sich direkt mit dem Host und ist ausdrücklich kein
  ThinClient-/Connect-or-Start-Szenario.
- **Warum:** Nur die direkte Host-/MCP-Prozessgrenze kann belegen, dass Lock,
  Pipe-Bind, Handshake, Session und CLI-Returncode zusammenpassen; der Vertrag
  bleibt auf wenige nicht-lastintensive E2E-Fälle begrenzt.

## Tests — Katalog

- [ ] `DaemonInstanceLockTests`: gleiche Endpoint-/User-Key-Konfiguration
      erlaubt genau einen Besitzer, der zweite erhält einen deterministischen
      Verlust, Freigabe nach Dispose erlaubt einen neuen Besitzer; kein
      dateibasierter State wird verwendet.
- [ ] `DaemonHostLifecycleTests`: tatsächlicher `RunAsync`-/Accept-Pfad mit
      kontrolliertem Transport; schneller EOF während/sofort nach Registrierung,
      `ActiveConnectionCount`/Dictionaries/Clientcount, Idle-Exit und genau
      einmalige Session-/Connection-Bereinigung.
- [ ] `DaemonHostMcpContractTests` (FastTests oder bestehender Daemon-Testanker):
      Handshake-Welcome kommt vor MCP-Initialize/Tool-Durchsatz; Session-Runner
      startet erst nach akzeptiertem Handshake, Disconnect cancelt nur diese
      Verbindung, geteilter Registry-Zustand bleibt resident.
- [ ] `MruStateStoreTests`: leere Datei und ungültiges JSON werden beim Read
      toleriert, `DisposeAsync` schreibt ohne Touch ein gültiges leeres Array;
      Schreibfehler werden nur diagnostiziert; kanonischer Alias wird dedupliziert
      und durch `Remove` dauerhaft entfernt.
- [ ] `DaemonHostProcessContractTests` (Integration): zwei echte Host-Starts
      auf demselben Endpoint, zweiter Start deterministisch stderr/Nicht-Null,
      erster Host bleibt erreichbar; danach Lock-Freigabe und sauberer Shutdown.
- [ ] `DaemonHostMcpProcessContractTests` (Integration): direkter
      Handshake- und MCP-SDK-Call über die Host-Pipe, schneller Disconnect,
      Idle-Exit und MRU-Flush; keine ThinClient- oder Connect-or-Start-Hilfen.
- [ ] Test-State nutzt die vorhandene zentrale Test-Temp-Infrastruktur bzw.
      injizierbare Pfade/Clock; keine OS-Temp-Ad-hoc-Pfade und keine
      `Category=Stress`-Tests.
- [ ] Umsetzungsgate gemäß `AGENTS.md`/`task-state.md`: `dotnet build`,
      `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
      `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
      genau einmal vor Step-Abschluss; Kritiker wiederholt den Vollstack nicht.

## Definition of Done

- [ ] Alle vier Findings aus `step-010/step-review.md` sind durch die oben
      genannten Produktions- und Testcontracts geschlossen; keine sonstigen
      Beobachtungen werden in den Step aufgenommen.
- [ ] Doppelstart ist über einen nicht dateibasierten, endpointgebundenen
      Lock vor dem Pipe-Bind deterministisch abgewiesen; stderr/Exitcode,
      Cleanup und per-connection-Multiplexing sind jeweils getestet.
- [ ] MRU-Shutdown schreibt nach leerem/korruptem Vorgänger einen gültigen
      atomaren Snapshot; kanonische Root-Schlüssel verhindern, dass tote Alias-
      Roots erneut persistiert werden; IO-Fehler bleiben nicht daemon-blockierend.
- [ ] Die direkten `DaemonHost`-Lifecycle-Methoden und der echte
      `RunMcpSessionAsync`-Pfad sind durch In-proc-/Host-/MCP-Contracts abgedeckt;
      der Registration-Race kann Idle-Exit nicht mehr dauerhaft blockieren.
- [ ] `--mcp-server`, EPIC-A, Step-009 und alle ausdrücklich außerhalb des
      Scopes liegenden ThinClient-Verträge bleiben unverändert.
- [ ] MCP-first-Semantikprüfung sowie betroffene Quality-Gates (`get_violations`,
      `safeguard`, bei neuen/geänderten Typen `metrics_lookup`) sind grün; kein
      Drift-Audit und kein Tech-Debt-Fix in diesem Step.
- [ ] Vollständiger Nicht-Stress-Teststack und Build sind gemäß Task-State genau
      einmal durch den Coder gelaufen; `step-011/step-result.md` ist geschrieben.
- [ ] Dieser Plan wird nach Umsetzung auf `done (pending audit)` gesetzt; der
      Coder übernimmt die Overrides `max_fix_rounds_per_step=6`,
      `soft_step_checkin_interval=80`, `max_batch_items=16` und
      `max_batch_diff_lines=80` unverändert.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` — neuer
  Lock-/Lifecycle-/MRU-Code bleibt klein, nullable, async an I/O-Grenzen und
  behandelt Cancellation/Fehler sichtbar.
- `.agents/rules/AiNetLinter.mdc#architecture` — Namespace-/Pfad-Mapping,
  keine Reflection oder dynamische Hostinstanziierung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1.Grundprinzipien` — bestehende
  Pipe-, Registry- und MCP-SDK-Strukturen wiederverwenden; kein DI-/RPC-
  Overhead; C#-Semantik MCP-first prüfen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3. Windows-Umgebung & Tool-Regeln`
  und `#4. Updates & Tests` — Named-Pipe-/Mutex-Vertrag, TestTempDirectory,
  xUnit-v3, gezielte Tests und ein vollständiger Nicht-Stress-Gate-Lauf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention` —
  keine Symptom-Fixes, Zero-Warning, MCP-Quality-Gates und kein vorgezogener
  Drift-Audit.

## Bekannte Ausnahmen

- Ein echter Zwei-Prozess-Contract ist hier nur für den direkten Host-/Lock-/
  MCP-Vertrag vorgesehen. ThinClient, Connect-or-Start und Clientmigration
  bleiben bewusst dem nächsten fachlichen B-Cluster vorbehalten.
- Der Lock-Test benötigt einen testbaren Besitz-/Transport-Seam; dieser darf
  nur die deterministische Testkoordination ermöglichen und nicht den
  produktiven Named-Mutex-/Endpoint-Key-Vertrag ersetzen.
- Falls eine Review-Forderung dem inzwischen gültigen Step-009-Handshake oder
  dem EPIC-A-Registry-Vertrag widerspricht, wird sie als Blocker bzw.
  Konzept-Entscheidung dokumentiert; die vier hier gelisteten Findings sind
  davon ausdrücklich nicht betroffen.

## Notes

- `MaxAllowedServerInstances` bleibt ein Multi-Connection-Detail des bereits
  laufenden Hosts und ist nicht der Single-Instance-Lock. Die beiden Contracts
  müssen getrennt getestet werden.
- MRU-Kanonisierung ersetzt niemals `projectRoot`-/Definitionsdatei-Wahrheit;
  sie normalisiert nur den Warmstart-Hinweis und dessen Lösch-/Persistenzpfad.
- Die neue direkte MCP-Abnahme verwendet die bestehende `McpServerOptionsFactory`
  bzw. `DaemonHostCommand.CreateSessionRunner` und eine Pipe-Verbindung; kein
  zweiter JSON-RPC-Server und keine zweite MCP-Toolregistrierung entstehen.
