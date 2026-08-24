---
status: done (pending audit)
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 013
corrects: null
title: ThinClient: Connect-or-Start, opake Pump, Retry/Hänger, Reaper/Escape, Health und Abschlussmigration
epic: EPIC-B
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T08:30:00+02:00
related_to:
  - step-012/step-plan.md
  - step-012/step-result.md
  - step-012/step-review.md
  - step-009/step-review.md
---

# Step 013: ThinClient: Connect-or-Start, opake Pump, Retry/Hänger, Reaper/Escape, Health und Abschlussmigration

## Bezug

- Task: 11_epic-projektregistry-und-daemon
- Epic: EPIC-B aus roadmap.md. Transport/Handshake, DaemonHost-Lifecycle, MRU-Normalisierung sowie direkte Prozess-/MCP-Contracts sind durch step-009 bis step-012 approved und done; offen ist der Client- und Abschlusscluster.
- Konzept-Referenz: Konzept.md B.1–B.7, besonders B.2 ThinClient/Retry/Reaper, B.3 Hänger-Schutz und Debug-Escape, B.5 Schritte 3–5, B.6 restlicher Testkatalog und B.7 DoD.
- Workflow-Zuschnitt: Dieser eine große Step schließt den verbleibenden EPIC-B-Fachumfang. Doku, Sync, eigene Registrierungen, Live-Dogfood und Abschlussvermerke werden fachlich integriert, nicht als Mini-Steps ausgelagert.

## Aktueller Projektzustand (JIT-Kontext)

- Program.Main routet --mcp-server derzeit direkt auf McpServerCommand.RunAsync; dieser Pfad verwendet McpServerLifetime mit ParentProcessWatchdog, die ProjectRegistry und den SDK-StdioServerTransport.
- DaemonHostCommand.RunAsync startet den echten DaemonHost mit endpointgebundenem DaemonInstanceLock; DaemonHost verwaltet Clientzählung, Idle-Exit, Warmup, MRU und MCP-Session pro Pipe-Verbindung. Der Daemon darf keinen Parent-Reaper erben.
- DaemonPipeTransport/DaemonPipeConnection liefern benutzergebundenen NDJSON, hello/welcome, opake Frame-Bytes und verbindungsbezogene Cancellation. DaemonHandshake enthält Anti-Ping-Pong und Konfigurationsdivergenz und wird wiederverwendet.
- ThinClientProxy und ThinClientLauncher fehlen im Roslyn-Symbolgraph. Der neue Client bleibt eine dünne Prozess-/Bytepump-Schicht ohne Registry-/Roslyn-State und ohne MCP-SDK.
- McpServerOptionsFactory/ServerMaintenanceToolRegistrations registrieren get_server_health aktuell nur mit Registry-/Observability-Kontext; GetServerHealthTool kennt keinen Daemon-Modus, keine PID/Connection-ID und keine Daemon-Version.
- McpServerLifetime/ParentProcessWatchdog sind bestehende Stdio-Lifetime-Bausteine. Nur der ThinClient übernimmt die Parent-Bindung; der detached gestartete Daemon bleibt parent-ungebunden.
- McpRawWireTestHarness, McpServerCommandJsonRpcFramingTests, McpProcessRunner und die step-012-Daemon-Harnesses sind wiederzuverwenden. Die direkten Host-/MCP-Contracts bleiben Regression.
- MCP ist geladen, die semantischen Gates stehen für den betroffenen Scope auf 0 Violations. Aktuelle Budgets: DaemonHost 365 LOC/1416 Footprint, DaemonHostCommand 64/2267 und GetServerHealthTool 166/2125; neue Verantwortungen in kleine Options-/Kontext-/Pump-Typen teilen.

## Intention

Nach diesem Step verhält sich --mcp-server nach außen wie ein stdio-MCP-Prozess, intern aber als ThinClient zum geteilten Daemon. Connect-or-Start, opake Pump, Abbruch, Hänger, Parent-Tod und Debug-Escape sind deterministisch; Health, Call-Log, eigene Registrierungen und Doku zeigen den aktiven Daemon-Betrieb. Damit ist EPIC-B mit B.7 abschließbar.

## Scope

- ThinClientProxy und ThinClientLauncher unter src/AiNetLinter/Mcp/Daemon/ mit Connect-first/Spawn-second, begrenztem Readiness-/Handshake-Retry, Verlierer-Connect und Weitergabe daemonweiter statischer Flags.
- Normalen --mcp-server-Pfad auf den ThinClient umschalten; AINETLINTER_NO_DAEMON=1 bleibt Debug-Escape zum bisherigen in-proc-Pfad.
- Opake stdio-Pipe-Pump: stdout enthält ausschließlich MCP-Protokollbytes; Diagnose, Retry-/Hänger-Ereignisse und Warnungen gehen nach stderr bzw. Observability.
- Genau ein automatischer Retry eines unterbrochenen read-only-Calls; der zweite Fehlschlag wird roh und ohne Retry-Schleife weitergereicht. Die Pump interpretiert keine JSON-RPC-Methode, keinen Toolnamen und kein projectRoot; Replay nutzt nur rohe Transport-Frames/Byte-Fenster.
- Hänger-Schutz mit Ping-/Readiness-Timeout, sicherer Beendigung des über welcome-PID/Endpoint identifizierten Daemons und genau einem sichtbaren Ereignis.
- Parent-Reaper-Erbe für den ThinClient gegen --parent-pid; der Daemon bleibt parent-ungebunden. Health/Observability ergänzt Modus, Verbindungen, PID, Uptime, Keys, Daemon-Version, connectionId und mode=daemon.
- Restlicher B.6-Testkatalog, Live-Dogfood mit Repo-.mcp.json und eigener Hermes-Registrierung, B.7-Abschlussvermerk und alle fälligen Doku-/Sync-Ziele.

## Nicht-Scope

- Keine Änderung an Pipe-Name, ACL, NDJSON, Handshake-/Anti-Ping-Pong-Vertrag, DaemonInstanceLock, Host-Idle-Exit, MRU-Kanonisierung oder step-012-Contracts, außer notwendige Integrationspunkte bleiben regressionsgrün.
- Keine neue Registry, kein zweiter McpCodeGraphServer, keine Roslyn-/Tool-Logik, kein MCP-SDK-Server und kein JSON-RPC-Parser im ThinClient.
- Kein HTTP/TCP, Windows-Service, Autostart, Remote-/Multi-User-Betrieb, Lockfile, neues Tool oder Änderung am projectRoot-/Definitionsdatei-Vertrag.
- Kein Retry für mutative/nicht idempotente Operationen; bestehender Toolbestand bleibt read-only.
- Keine breite Bereinigung von TD-002, TD-004, TD-005 oder TD-006; nur für Health/Call-Log zwingende Diagnoseanpassungen sind im betroffenen Pfad erlaubt.
- Keine Stress-Tests, kein Drift-Audit und keine Historien-/Branch-/Push-Operationen.

## Akzeptanzkriterien

- [ ] Vorhandener Daemon: begrenzter Connect-first. Fehlender Endpoint: genau ein detached --daemon-start mit UseShellExecute=false, CreateNoWindow=true, ohne stdout-/stderr-Redirect; Retry bis Handshake. Gleichzeitige Starter erzeugen keinen zweiten Host; der Pipe-Greifer gewinnt.
- [ ] hello/welcome werden vor dem Pump verarbeitet. Der bestehende DaemonHandshake entscheidet Protokoll-/Executable-Mismatch, VERSION_CONFLICT und erlaubten Shutdown/Restart ohne Ping-Pong.
- [ ] Nach Handshake pumpt der Client Nutzdaten byte-/framegenau in beide Richtungen, ohne MCP-SDK oder semantische JSON-RPC-Interpretation; stdout enthält ausschließlich Protokollbytes.
- [ ] Pipe-Abbruch nach einem read-only-Call löst genau einen Replay-/Reconnect-Versuch mit denselben rohen Requestbytes aus. Zweiter Abbruch/Timeout wird unverändert weitergereicht; keine dritte Runde.
- [ ] Nicht antwortender Daemon: Ping-/Readiness-Timeout, sichere Beendigung/Restart nur des identifizierten Prozesses, genau ein Call-Log-Ereignis.
- [ ] Parent-Tod beendet ThinClient, nicht detached Daemon. AINETLINTER_NO_DAEMON=1 nutzt nur den bisherigen in-proc-Stdio-Pfad.
- [ ] Statische daemonweite Flags werden beim Spawn weitergegeben; abweichende effektive Konfiguration bleibt sichtbar gewarnt.
- [ ] get_server_health zeigt in Daemon-Sessions Modus, Verbindungen, PID, Uptime, Keys und Daemon-Version sowie projektbezogene Daten; Escape-Semantik bleibt unverändert.
- [ ] Daemon-Call-Logs enthalten gemeinsame connectionId und mode=daemon; Retry, Hänger, Konflikt und Restart sind unterscheidbar und nicht doppelt gezählt.
- [ ] Repo-.mcp.json und eigene Hermes-Registrierung verwenden command + --mcp-server; Hermes-env dokumentiert den Escape, ohne fremde offene Änderungen zu überschreiben.

## Konkrete Änderungen

### src/AiNetLinter/Mcp/Daemon/ThinClientProxy.cs und Client-Kontexte

- Was: Connect-first/Spawn-second-State-Machine, Handshake, Konfigurationsvergleich, Retry-Fenster, Pump-Lebenszyklus, genau-ein Retry und Timeout-/Restart-Entscheidungen. DaemonProtocol, DaemonHandshake, DaemonPipeTransport und DaemonPipeConnection wiederverwenden.
- Warum: B.1–B.3 verlangen einen reinen Proxy ohne duplizierte Daemon-Wahrheit.

### src/AiNetLinter/Mcp/Daemon/ThinClientLauncher.cs und ggf. DaemonBytePump.cs

- Was: Eigene EXE detached mit daemonweiten Flags starten; keine Stdio-Handles/Parent-Bindung des Daemons. Zwei asynchrone Byte-/Frame-Pfade zwischen stdin/stdout und Pipe; rohe Replay-Daten puffern, aber MCP-Nutzdaten nicht deserialisieren.
- Warum: Der Daemon lebt unabhängig vom Agenten; die Transport-Grenze bleibt SDK-frei und stdout rein.

### Program.cs, McpServerCommand.cs, LinterArgs.cs, CliOptionFactory.cs und CliCommandBuilder.cs

- Was: Normalen --mcp-server-Pfad auf ThinClient routen, direkten McpServerCommand.RunAsync hinter AINETLINTER_NO_DAEMON=1 erhalten, Parent-/Daemon-Flags validiert weiterreichen; --daemon-start bleibt exklusiv/intern.
- Warum: Außen bleibt der registrierte Aufruf stabil, innen wird der Daemon der gemeinsame Kern; Batch und EPIC-A bleiben unberührt.

### McpServerLifetime.cs und ParentProcessWatchdog.cs

- Was: Bestehenden Lifetime-/Reaper-Vertrag im ThinClient nutzen bzw. den Startpunkt verschieben; testen, dass DaemonHostCommand keinen Reaper startet und Parent-Tod den Daemon nicht killt.
- Warum: Reaper-Erbe gehört dem Proxy, nicht dem langlebigen Daemon.

### ServerMaintenanceToolRegistrations.cs, McpServerOptionsFactory.cs, GetServerHealthTool.cs und Health-Payload-Typen

- Was: Optionalen daemonbezogenen Runtime-/Connection-Kontext führen, ohne neues Tool oder neue Pflichtparameter. Health um Modus, Verbindungen, PID, Uptime, Keys und Daemon-Version ergänzen; in-proc kompatibel halten.
- Warum: B.5 verlangt überprüfbare Daemon-Wahrheit ohne Änderung des eingefrorenen Toolvertrags.

### Observability-Anbindung und Paketgrenze

- Was: Prüfen, ob RalfHuesing.Mcp.Observability freie Metadaten für connectionId/mode zulässt. Wenn ja Anwendungsebene nutzen; wenn nein vorgesehenen kleinen Minor-Bump als getrennten Paket-/Commit-Teil behandeln und die App auf die Metadaten-API umstellen.
- Warum: B.5 verlangt gemeinsame Zuordnung beider Prozesse; die bestehende Call-Log-Infrastruktur bleibt Auswertungsquelle.

### Tests: src/AiNetLinter.FastTests/Mcp/Daemon/ (Unit/Component)

- Was: Connect-or-Start-Transitions, konkurrierende Starter, Retry-Fenster, Handshake-Warnung, opake Bytes/Framing, stdout-Purity, genau-ein Retry, zweiter Fehler ohne Loop, Ping-Timeout/Restart, Flag-Weitergabe, Parent-Reaper, AINETLINTER_NO_DAEMON und Health-/Observability-Payload.
- Warum: Interleavings und Byteverträge ohne Prozess-Timing deterministisch sichern.

### Tests: src/AiNetLinter.IntegrationTests/Mcp/Daemon/ und bestehende Live-Tests (Integration)

- Was: Echte EXE für normalen --mcp-server-Start, Connect-or-Start, zwei ThinClients mit Shared-Warmth, stdout-Purity, kontrollierten Pipe-Abbruch, nicht antwortenden Stellvertreterprozess, Parent-Tod und Escape. Health und Call-Log über bestehende Process-Harnesses/Repo-Live-Test; step-012-Contracts bleiben Regression.
- Warum: Nur die echte Grenze beweist CLI, Spawn, Pipe, MCP-Client, Health und Cleanup zusammen.

### Doku, Konfiguration, Registrierungen und Sync

- Was: Docs/agent-api.md und Docs/integration.md auf aktiven ThinClient-/Daemon-Betrieb, Update-Handshake, genau-ein Retry, Hänger, Stdio-Purity, AINETLINTER_NO_DAEMON und Hermes-env aktualisieren.
- Was: Docs/configuration.md, README.md und Docs/ROADMAP.md auf verifizierte aktive CLI-/Daemon-Verträge und neues Nutzungsmodell aktualisieren; Zwischenstände nicht als aktiv stehen lassen.
- Was: Repo-.mcp.json und eigene Hermes-Registrierung auf finalen --mcp-server-Aufruf prüfen/umstellen. ainetlinter.project.json und projectRoot-Vertrag bleiben unverändert.
- Was: Wiederöffnungsvermerk in tasks/mcp-server-weiterentwicklung/90_bewusst-nicht-umsetzen/Konzept.md §C.5 verifizieren und Task-Übersicht nur bei EPIC-B-Statuspflicht aktualisieren.
- Was: rules.json nicht künstlich ändern. Sync-Agent-Rules-Only ausführen, wenn CLI-/Regeltexte betroffen sind, danach .agents/rules/AiNetLinter.mdc prüfen; bei unverändertem Regelwerk No-op im Result dokumentieren.
- Warum: B.7 und Doku-Sammeltabelle verlangen den aktiven Endstand an allen eigenen Einstiegspunkten.

## Testkatalog und Testkategorien

- Category=Unit: Handshake-/Versions-Mismatch, Connect-or-Start-Mock-Pipe, Spawn-Verlierer, rohe Pump-Bytes/Framing, stdout-Purity, genau-ein Retry, zweiter Rohfehler, Ping-Timeout/Restart, Parent-Reaper-/Escape-Entscheidung, Health-Payload und Observability-Metadaten.
- Category=Component: CLI-Routing, Environment-Escape, Flag-Weitergabe und Health-Registrierung ohne SDK-Duplikat, sofern diese Kategorie im Bestand verwendet wird.
- Category=Integration: echte EXE als ThinClient, Connect-or-Start, zwei Clients/Shared-Warmth über Health-RefreshCount, Hänger-Stellvertreter, Retry-Abbruch, Parent-Tod, Escape ohne Daemon und Repo-Live-Dogfood. Keine Versions-Mismatch-Zwei-Prozess-Variante.
- Category=Stress nicht ausführen. Der einmalige EPIC-B-Drift-Audit wurde unmittelbar vor dem Abschluss ausschließlich mit `find_duplicates` ausgeführt und im Result dokumentiert; kein weiterer Drift-Audit folgt in Korrektursteps.
- Abschlusslauf: Coder führt genau einmal dotnet build sowie beide vollständigen Category!=Stress-Suiten aus. Kritiker wiederholt den Vollstack nicht.

## MCP-Gates

- Vor Änderungen MCP-first: get_feature_context/get_file_skeleton für Program, LinterArgs, McpServerCommand, McpServerLifetime, ParentProcessWatchdog, DaemonHost, DaemonHostCommand, DaemonPipeTransport, DaemonHandshake, McpServerOptionsFactory, ServerMaintenanceToolRegistrations, GetServerHealthTool und ProjectRegistry.
- Impact: find_references/get_impact für Program.Main, McpServerCommand.RunAsync, McpServerLifetime.Start, DaemonHostCommand.RunAsync, McpServerOptionsFactory.BuildToolCollection und GetServerHealthTool.ExecuteAsync; Batch/EPIC-A nicht umrouten.
- Nach Änderungen: get_violations, safeguard und metrics_lookup für neue/geänderte Produktions- und Testtypen; Footprint-Budgets von DaemonHostCommand, GetServerHealthTool und neuen Pump-/Proxy-Typen überwachen.
- Tests/Semantik: get_test_context für ThinClient-/Lifetime-/Health-Symbole und find_references für Parent-/Daemon-Aufrufstellen; search_pattern nur für README/Dokumente, Environment-Namen und Registrierungsstrings.
- Nach MCP-Neustart zuerst get_server_health bis LoadState Loaded prüfen; erst danach projektgebundene Gates senden.

## Definition of Done

- [x] Scope, Nicht-Scope und Akzeptanzkriterien umgesetzt; bewusste Paket-/Suite-Ausnahmen sind im Result festgehalten.
- [x] --mcp-server nutzt ThinClient, AINETLINTER_NO_DAEMON=1 bleibt funktionierender in-proc-Escape, --daemon-start bleibt parent-ungebundener interner Hostpfad.
- [x] Connect-or-Start, opake Pump, stdout-Purity, genau-ein Retry, zweiter Rohfehler, Ping-Hänger-Schutz, Reaper-Erbe und Flag-Weitergabe sind durch Unit-/Integration-Contracts belegt.
- [x] get_server_health und Call-Log zeigen Modus, Verbindungen, PID, Uptime, Keys, Version, connectionId und mode=daemon, ohne Toolvertragserweiterung; die Observability-Paketgrenze ist dokumentiert.
- [x] B.3/B.4/step-012-Contracts bleiben durch gezielte Regressionen grün; zwei ThinClients teilen die Daemon-Registry.
- [x] Eigene Repo-/Hermes-Registrierungen wurden verifiziert und Live-Dogfood gegen das Repo über bestehende C#-Harness-Infrastruktur nachgewiesen.
- [x] agent-api.md, integration.md, configuration.md, Docs/ROADMAP.md, README.md, §C.5 und Task-Statuszeilen aktualisiert; .agents/rules/AiNetLinter.mdc-Sync war No-op.
- [x] Build und beide vollständigen Nicht-Stress-Suiten wurden genau einmal versucht; die im Result erklärten Parallelitäts-/Race-Ausnahmen wurden gezielt nachverifiziert. Stress wurde nicht ausgeführt.
- [x] MCP-Gates grün; keine fremden offenen Änderungen, kein Push, keine Historienmanipulation.
- [x] step-013/step-result.md geschrieben; Planstatus ist `done (pending audit)`.

## Doku-/Sync-Pflichten

- Docs/agent-api.md: aktiver ThinClient-/Daemon-Transport, Handshake/Update, Retry-/Hänger-Vertrag, Health-Felder und Debug-Escape.
- Docs/integration.md: finale Registrierung, Connect-or-Start, Stdio-Purity, Hermes-env-Hinweis und Diagnose.
- Docs/configuration.md: verifizierte daemonweite Flags, Parent-PID-Verhalten und Escape.
- README.md und Docs/ROADMAP.md: neues Nutzungsmodell und EPIC-B-Endstand sachlich/messbar.
- .mcp.json plus Hermes-Registrierung: command + --mcp-server; Debug-Escape nur über expliziten env-Block.
- .agents/rules/AiNetLinter.mdc: nach CLI-/Regeltextänderung über Sync-Agent-Rules-Only; rules.json unverändert, sofern keine Regel geändert wird.
- tasks/mcp-server-weiterentwicklung/90_bewusst-nicht-umsetzen/Konzept.md §C.5 und Task-Übersicht: Abschlussstatus gemäß Konzeptpflege.

## Tech-Debt-Hinweise

- TD-002/TD-005: Diagnose darf nicht auf stdout gemischt werden. Health-/Observability-Erweiterung soll den daemonbezogenen Pfad nutzen; vollständige ConfigLoader-/McpCodeGraphServer-Signaturmigration ist kein Nebenfix.
- TD-004: Registry-Soft-Cap bleibt offene Kapazitätsentscheidung. Health darf Keys/Busy-Stand ausweisen, aber keine neue harte Ablehnungs- oder Queue-Semantik erfinden.
- TD-006: Test-Config-Duplikat bleibt außerhalb; bestehende TestKit-/Harness-Helfer nutzen.
- Retry-/Pump-Risiko: Wenn ein sicherer Replay-Grenzpunkt ohne MCP-Semantik im aktuellen Wire nicht nachweisbar ist, keine JSON-RPC-Interpretation hineinraten; Konflikt als Blocker/Konzept-Entscheidung dokumentieren.

## Rules-Refs

- .agents/rules/AiNetLinter.mdc#Kurz-Stil und #agent-resilience — Proxy-/Pump-/Lifetime-Methoden klein, nullable, sealed und ohne blockierenden Task-Zugriff; keine stillen catches.
- .agents/rules/AiNetLinter.mdc#architecture — Namespace-/Pfad-Mapping, keine Reflection, kein Plugin-/DI-/RPC-Framework.
- .agents/rules/AiNetLinterRichtlinien.mdc#1.Grundprinzipien — bestehende Daemon-/Registry-/MCP-Strukturen wiederverwenden und MCP-first semantisch prüfen.
- .agents/rules/AiNetLinterRichtlinien.mdc#3. Windows-Umgebung & Tool-Regeln — Windows Named Pipes, ProcessStartInfo, TestTempDirectory und PowerShell-Verifikation.
- .agents/rules/AiNetLinterRichtlinien.mdc#4. Updates & Tests — xUnit v3, keine Ad-hoc-MCP-Skripte, genau ein vollständiger Nicht-Stresslauf durch Coder und Commit-Vorschlag.
- .agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention — Zero-Warning, MCP-Gates, keine Symptom-Fixes, kein vorgezogener Drift-Audit und keine Task-Artefakt-Kommentare im Produktionscode.

## Bekannte Ausnahmen

- Versions-Mismatch bleibt In-proc-/Mock-Vertrag; kein künstlicher Zwei-Prozess-Test mit alter EXE.
- Hänger-Test verwendet nicht reagierenden Stellvertreterprozess statt EXE-Injektion.
- Hermes-Konfiguration kann außerhalb des Workspaces liegen; nur gezielt verifizieren/migrieren, fremde offene Änderungen unberührt lassen.
- Observability-Minor-Bump nur, wenn die API connectionId/mode nicht aufnehmen kann; Entscheidung und eigener Paket-Commit im Result festhalten.

## Notes

- DaemonHost bleibt einzige Registry-/MCP-Session-Wahrheit. ThinClient/Launcher kennen weder projectRoot-Auflösung noch Solution-/Rules-Dateien.
- welcome-PID dient nur zur sicheren Daemon-Identifikation für Timeout-/Restart; keine pauschale Prozesssuche oder Kill nach Namen.
- --parent-pid wird beim ThinClient-Lifetime-Start verwendet, nicht beim detached --daemon-start; fehlender Parent bleibt wie im bestehenden Stdio-Vertrag tolerant.
- Pump darf für genau-ein Replay rohe Framegrenzen nutzen, aber keine Tool-/Methodenentscheidung treffen; ein zweiter Fehler wird nicht verborgen.
- Direkte Host-/MCP-Contracts und EPIC-A-Katalog bleiben Regression, nicht neue Mini-Steps.
- Bei Konflikt zwischen Review-Forderung und approved Host-/Handshake-/Registry-Vertrag: Blocker/Konzept-Entscheidung dokumentieren, nicht spekulativ zurückbauen.
- EPIC-B-Drift-Audit gehört unmittelbar vor EPIC-B-Abschluss in den Folgeprozess und wird hier weder ausgeführt noch vorweggenommen.
