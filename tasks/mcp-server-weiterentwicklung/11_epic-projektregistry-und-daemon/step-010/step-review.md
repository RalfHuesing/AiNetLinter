---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 010
epic: EPIC-B
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-24T03:13:53+02:00
verdict: issues
resolved_by:
  - step-011
  - step-012
tech_debt_ids: []
---

# Review Step 010: DaemonHost-Lifecycle, Idle-Exit und MRU-Warmup

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: Host-, Adapter-, MRU-, Warmup- und CLI-Bausteine sind vorhanden; die geplanten Host-/MCP-/Doppelstart-Contracts fehlen jedoch bzw. belegen die Kernpfade nicht.
- [x] Rules-Konformität: MCP `get_violations` meldet 0 Verstöße; `safeguard` steht für `src/AiNetLinter/Mcp/Daemon` und `src/AiNetLinter/Cli` jeweils bei 10,00/10; die gemeldeten Metriken liegen innerhalb der Grenzwerte.
- [x] Logische Korrektheit: Shared Registry, Session-Runner, Handshake-Reihenfolge, Idle-Clock und Warmup-Semaphore sind im Code erkennbar, aber Doppelstart, Shutdown-Persistenz und ein Connection-Registration-Race brechen den Vertrag.
- [x] Konzept-Treue: ThinClient, Connect-or-Start, Stdio-Pump, Retry/Hänger-Schutz und externe Hermes-Verdrahtung wurden nicht vorweggenommen; B.3-Doppelstart und B.4-State-Vertrag sind aber nicht erfüllt.
- [x] Build: laut `step-result.md` grün mit 0 Warnungen und 0 Fehlern; gemäß Nutzer-Override nicht wiederholt.
- [x] Tests: laut `step-result.md` grün (`1705/1705` FastTests, `352/352` IntegrationTests, jeweils ohne Stress); gemäß Nutzer-Override nicht wiederholt. Eine gezielte Doppelstartprobe mit der aktuellen Debug-Ausgabe ließ zwei Host-Prozesse gleichzeitig laufen, ohne stderr-Fehler.

## Befund

### Plan-Erfüllung

Der interne Routing-Pfad, der DaemonHost, der shared `DaemonRegistryAdapter`, die per-connection MCP-Session, Idle-Zustandslogik, Warmup-Begrenzung und der MRU-Store sind implementiert. Die im Plan unter `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` vorgesehenen echten Host-/Pipe-/MCP-Contracts wurden nicht hinzugefügt; die vorhandenen Tests rufen nur Test-Seams für `IsIdleExitDue`, `WarmupForTestAsync`, MRU-Methoden und CLI-Parsing auf. `DaemonHost.RunAsync`, `AcceptLoopAsync`, `HandleConnectionAsync` und `DaemonHostCommand.RunMcpSessionAsync` sind damit nicht gegen den vorgesehenen Verbindungspfad abgenommen.

### Rules-Konformität

Die MCP-Gates für die betroffenen Produktionsscopes sind grün. Es gibt keinen Rules- oder Metrik-Fund, der das Verdict trägt.

### Logische Korrektheit

`DaemonHostCommand.RunMcpSessionAsync` baut je Verbindung einen SDK-Server auf demselben `DaemonPipeConnection.Stream` und verwendet dabei die gemeinsame `ProjectRegistry`; `HandleConnectionAsync` führt den Step-009-Handshake vor dem Session-Runner aus und bindet die Cancellation an die Verbindung. `ProjectRegistry.ActiveLoadCount`, `activeWarmups` und die injizierbare `TimeProvider` schützen den Idle-Exit grundsätzlich. Die konkrete Pipe-Bindung, der Shutdown-State und die Verbindungsregistrierung enthalten jedoch die in den Findings beschriebenen Vertragsbrüche.

### Konzept-Treue (Ebene 4)

Der Scope bleibt gegenüber den ausdrücklich späteren ThinClient-/Hermes-Themen abgegrenzt. Die Muss-Haves „Doppelstart → sachlicher stderr-Fehler + Exit-Code ungleich null“, persistentes Leeren/Normalisieren eines korrupten MRU-Vorgängers beim Shutdown, tote-Root-Entfernung und belastbare Host-/MCP-Contracts aus B.3/B.4/B.6 sind in diesem Ergebnis nicht erfüllt.

### Build-/Test-Status

```text
dotnet build → laut step-result.md grün (0 Warnungen, 0 Fehler; nicht wiederholt)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → laut step-result.md grün (1705/1705; nicht wiederholt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → laut step-result.md grün (352/352; nicht wiederholt)
Stress-Tests → nicht ausgeführt
Drift-Audit → gemäß Step-Scope nicht ausgeführt
Gezielte Doppelstartprobe → zwei Debug-Host-Prozesse gleichzeitig aktiv; zweiter Start ohne stderr-Fehler und ohne Nicht-Null-Exit
```

## Findings

1. `src/AiNetLinter/Mcp/Daemon/DaemonPipeTransport.cs:38-43` — **[MAJOR] [Konzept/Logik]** `CreateServerStream` erzeugt Named Pipes mit `NamedPipeServerStream.MaxAllowedServerInstances`. Zusammen mit `DaemonHost.AcceptLoopAsync` existiert keine prozessweite Single-Instance-Sperre; eine zweite `--daemon-start`-Instanz kann deshalb ebenfalls eine Serverinstanz unter demselben Pipe-Namen eröffnen. Die gezielte Probe ließ beide Hosts gleichzeitig laufen, statt den zweiten Start deterministisch mit stderr-Fehler und Exit-Code ungleich null abzulehnen. **Fix:** Eine daemonweite, nicht dateibasierte Single-Instance-Sperre für den Pipe-Namen beim Hoststart erwerben, bei Nichterwerb eine sachliche stderr-Diagnose und Exit-Code `!= 0` liefern und die Sperre erst nach vollständigem Shutdown freigeben; die per-connection `MaxAllowedServerInstances` für den bereits laufenden Host darf dabei erhalten bleiben.

2. `src/AiNetLinter/Mcp/Daemon/MruStateStore.cs:57-91, 180-203` — **[MAJOR] [Konzept/Logik]** `Read` behandelt leere/korrupt serialisierte Dateien nur als `[]`, setzt aber weder `dirty` noch einen Flush-Bedarf. `DisposeAsync` schreibt ausschließlich bei `dirty == true`; startet der Host mit leerem/korruptem Vorgänger und ohne erfolgreichen Touch, bleibt die kaputte Datei beim Shutdown bestehen. Das widerspricht dem Step-Vertrag, den MRU-State auch aus diesem Vorgängerzustand heraus beim Shutdown zu persistieren. **Fix:** Lesen als tolerierte Normalisierung markieren und beim Shutdown mindestens ein atomar geschriebenes leeres, gültiges Array erzwingen; Schreibfehler müssen dabei weiterhin geloggt werden, ohne den Daemon zu blockieren.

3. `src/AiNetLinter/Mcp/Daemon/MruStateStore.cs:57-91, 242-246` sowie `src/AiNetLinter/Mcp/Daemon/DaemonHost.cs:274-317` — **[MAJOR] [Logik]** Eingelesene Roots werden in `Read` ungekanonisiert in `entries` übernommen, während `Remove`/`Touch` über `CanonicalizeRoot` mit `Path.GetFullPath(...).TrimEnd(...)` arbeiten. Ein gültiger, aber anders geschriebener State-Eintrag wie `C:\repo\.` oder `C:\repo\` kann beim fehlgeschlagenen Warmup nicht aus `entries` entfernt werden; `WarmupCandidateAsync` ruft zwar `Remove` auf, der rohe Schlüssel bleibt jedoch bestehen und wird beim nächsten Shutdown erneut persistiert. **Fix:** Jeden validierten Eintrag vor Grouping, Speicherung und Rückgabe kanonisieren, ungültige Ergebnisse verwerfen und die deduplizierte kanonische Form als alleinigen MRU-Schlüssel verwenden; dafür einen Contract mit einem toten Root in alternativer Schreibweise ergänzen.

4. `src/AiNetLinter/Mcp/Daemon/DaemonHost.cs:163-173, 175-221` und `src/AiNetLinter.FastTests/Mcp/Daemon/` — **[MAJOR] [Logik/Plan]** `RegisterConnection` startet `HandleConnectionAsync` vor dem Eintragen von Task und Handle in `connections`/`connectionHandles`. Bei einem synchron beendeten Read (z. B. schneller Disconnect/EOF) kann der Handler im `finally` bereits entfernen, bevor die Dictionaries befüllt werden; danach bleibt ein abgeschlossener Handle als scheinbar aktive Verbindung zurück und `IsIdleExitDue` blockiert den Idle-Exit trotz `clientCount == 0`. Für `RunAsync`, `AcceptLoopAsync` und `HandleConnectionAsync` existiert kein direkter Lifecycle-Contract; die vorhandenen Tests benutzen nur Test-Seams und prüfen weder Handshake/Session noch diesen Race. **Fix:** Die Verbindung unter dem Lifecycle-Lock registrieren, bevor der Handler gestartet wird, oder den Abschluss-Race so synchronisieren, dass ein bereits beendeter Handler seinen Eintrag zuverlässig wieder entfernt; ergänzend einen in-proc Contract für schnellen Disconnect, Clientzählung, Handshake-vor-Session, Session-Cancellation und genau einmaliges Registry-/MCP-Dispose hinzufügen.
