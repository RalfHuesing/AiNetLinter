---
status: done (pending audit)
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 014
epic: EPIC-B
completed_at: 2026-08-24T10:10:00+02:00
model: stealth/ox-alpha (openrouter)
model_knowledge_cutoff: nicht deklariert
code_commit: 683a3e4f
---

# Step 014 Ergebnis: Step-013-Korrektur — F1 (fünf Contract-Nachweise) und F2 (erreichbare Timeout-Diagnostik)

## Ergebnis

Beide MAJOR-Findings aus dem Step-013-Review sind geschlossen:

- **F2:** `DaemonBytePump.ReadFailure` erkennt den reinen Idle-Timeout-Fall
  (`pumpCancelled && inputTask.IsCanceled && outputTask.IsCanceled`, nachdem
  Caller-Cancel und stdin-EOF zuvor ausgeschlossen sind) **vor** dem
  Null-Zweig und liefert die unterscheidbare `TimeoutException`-Signatur
  („Die Daemon-Pipe antwortete nicht innerhalb des Hanger-Schutz-Zeitlimits.").
  Der frühere, unerreichbare Ternary-Zweig ist entfernt. Caller-Cancellation
  bleibt weiterhin unattributiert (`Failure == null`).
- **F1:** Alle fünf fehlenden Contract-Nachweise existieren und sind grün:
  1. Genau-ein Replay (AK 4): Fenster gesetzt → Rohabbruch liefert den Frame;
     Antwort resettet das Fenster (`ReplayFrame == null`); Wiederanlauf schreibt
     den Replay-Frame zuerst — Pump-Ebene (FastTests, Unit).
  2. Zweiter Rohfehler ohne dritte Runde (AK 4): Proxy-Sitzung endet mit Exit 2,
     genau zwei `[WARN]`-Ereignissen (Suffixe „genau ein read-only Replay wird
     versucht" / „kein weiterer Retry"), genau zwei Verbindungsversuchen, kein
     Spawn, und der zweite Verbindungsaufbau trägt den Replay-Frame zuerst.
  3. Hänger-Timeout (AK 5, Stellvertreterprozess): Pump-Ebene assertiert die
     F2-`TimeoutException` mit erhaltenem Replay-Frame; Proxy-Ebene belegt
     Haänger-Timeout → Kill des per Welcome-PID identifizierten Stellvertreter-
     prozesses (echter OS-Prozess), **genau ein** `[WARN]`-Ereignis mit der
     Hanger-Schutz-Signatur, danach erfolgreicher Retry bis Session-Ende.
  4. Zwei ThinClients teilen die Daemon-Registry (B.6): echter Zwei-Prozess-Lauf
     über den Raw-Wire-Harness gegen denselben Daemon (identische Welcome-PID);
     Shared-Warmth über Keys (Fixture-Key resident), identischen RefreshCount
     (kein zweiter Load/Refresh) und strikt gewachsene Instanz-Uptime.
  5. Connect-or-Start-Transitions/konkurrierende Starter am Mock-Pipe (B.6 Unit):
     Connect-first-Gewinn ohne Spawn, Spawn + Readiness-Retry bis Welcome,
     Spawn-Misserfolg ohne Readiness-Loop sowie zwei gleichzeitige Starter, die
     auf denselben Mock-Endpunkt konvergieren (gleiche Daemon-PID, zwei
     Verbindungen, je genau ein Spawn).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Daemon/DaemonBytePump.cs` — F2-Fix in `ReadFailure`.
- `src/AiNetLinter/Mcp/Daemon/ThinClientProxy.cs` — Sitzungskern als internes
  `RunSessionAsync` extrahiert; neue Records `ThinClientSessionOptions`
  (Connect-/Spawn-Delegate, Pump-Idle-Timeout, Stdio-Streams) und
  `ThinClientSessionContext`; `ThinClientConnection` auf Dateiebene internal.
  Default-Pfad verhält sich unverändert weiter wie previously.
- `src/AiNetLinter.TestKit/ThinClientPipeTestDoubles.cs` — neu: Duplex-Paare,
  `ScriptedMockPipeTransport`, `MockDaemonScript`, `DuplexStream` (gemeinsame
  Doubles beider Suiten; intern, da AiNetLinter-Interne Typen referenziert werden).
- `src/AiNetLinter.FastTests/Mcp/Daemon/ThinClientPumpContractTests.cs` — neu
  (Nachweis 1 + F2-Signatur + Caller-Cancel-Regression).
- `src/AiNetLinter.FastTests/Mcp/Daemon/ThinClientConnectOrStartTests.cs` — neu
  (Nachweis 5).
- `src/AiNetLinter.FastTests/Mcp/Daemon/ThinClientContractTests.cs` — privater
  Duplex-/Frame-Helper durch die gemeinsamen Doubles ersetzt (DuplicateCode-
  Finding des eigenen Quality-Gates).
- `src/AiNetLiner.IntegrationTests/Mcp/Platform/McpRawWireTestHarness.cs` — neue
  Methode `RunAndCollectWithDiagnosticsAsync` (stderr-Text, ExitCode, optionale
  Daemon-Idle-Exit-Minuten und LOCALAPPDATA-Isolation); alter Aufruf delegiert.
  Zusätzlich `StandInProcess` (Stellvertreterprozess) als neuer Besitzer einer
  `Process.Start`-Callsite innerhalb der guard-whitelisteden Harness-Datei.
- `src/AiNetLinter.IntegrationTests/Mcp/Daemon/ThinClientProxySessionContractTests.cs` —
  neu (Nachweise 2 und 3, getakteter Mock-Server).
- `src/AiNetLiner.IntegrationTests/Mcp/Daemon/ThinClientsSharedWarmthProcessContractTests.cs` —
  neu (Nachweis 4).

## Verifikation (Build-/Test-Output)

Der komplette Nicht-Stress-Stack wurde genau einmal vor Step-Abschluss
gestartet:

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress        → grün (1726/1726)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → 354/359 grün, 5 Fehler (Klassifikation s. u.)
```

Der timingabhängige EPIC-A-Bestandstest
`ProjectRegistryTests.Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner`
lief im diesmalen Vollparallellauf **grün** — keine Ausnahme nötig.

### Klassifikation der 5 Integrationsfehler (Coder-Schritt 4a)

- **1 echter, im Scope behobener Befund:**
  `McpProcessArchitectureGuardTests.RunnerAndProcessCallsites_StayWithinMcpOwners`
  erwartete genau 3 `Process.Start(`-Callsites; die Startstelle des
  Stellvertreterprozesses lag zunächst in der Testdatei selbst. Behoben durch
  Verlagerung nach `StandInProcess` (McpRawWireTestHarness.cs, bereits
  guard-whitelisted) — Guard unverändert, gezielt erneut grün.
- **4 Umfeld-Kontaminationen (keine Code-Defekte):**
  Vor dem Vollstack hatte ich die MCP-Quality-Gates manuell über eine
  stdio-JSON-RPC-Session gefahren. Dieser Gate-Client spawnte einen echten
  detached Daemon mit Default-Idle-Exit (10 min), der während des
  Integrationslaufs noch am benutzergebundenen Endpunkt hing und vier
  Bestands-/Neue Tests störte (Doppelstart-Lock-Fehler, fremder Repo-Key in
  der Health-Antwort, Exit 1 beim Host-Handshake-Lauf bzw. Loading-Hint statt
  LoadFailed-Vertrag). Alle AiNetLinter-Prozesse wurden terminiert; die vier
  betroffenen Tests wurden isoliert nachgefahren und sind grün:
  `TwoDaemonProcessesOnOneEndpointRejectSecondAndReleaseLock`,
  `HostPipeHandshakeThenMcpInitializeListsToolsAndExitsIdle`,
  `ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract`,
  `TwoThinClients_ConnectToSameDaemon_AndReuseWarmProjectKey` (letzterer wurde
  zusätzlich gehärtet: Health-Einträge werden per Root-Match statt `Single()`
  ausgewählt, damit ein geteilter Daemon auch fremde residente Keys halten darf).
- Gemäß Vorgabe wurde der Vollstack danach **nicht wiederholt**; stattdessen
  liegen für jeden Fehler isolierte Grün-Nachweise vor.

Gezielte Läufe während der Entwicklung (Auszug):

- FastTests `Category=Unit`: 1238/1238 · `Category=Component`: 488/488.
- Neue Tests einzeln: Pump-/ConnectOrStart-Contracts 10/10 bzw. 13/13 im
  ThinClient-Verbund; Proxy-Session-Contracts 2/2 (~0,7 s).
- Nachweis 4 solo: grün (≈1 m 40 s, Roslyn-Kaltlast + Polling dominieren).

## MCP-Quality-Gates

Der AiNetLinter-MCP-Server ist in dieser Subagent-Umgebung nicht als
eingebettetes Tool registriert; die Gates wurden daher über eine direkte
stdio-JSON-RPC-Session gegen die gebaute EXE (`--mcp-server`) gefahren:

- `get_violations` (projectRoot = Repo-Root, maxResults 200): **0 Violations in
  659 Dateien** — Produktionsscope und Testscopes gemeinsam, vollständig für
  den Scope laut Tool-Hinweis. Ein vorheriger Lauf zeigte das DuplicateCode-
  Finding der doppelten Frame-Hilfsklasse; es wurde im selben Zug konsolidiert
  (siehe ThinClientContractTests oben).
- `safeguard`: **Score 10,00/10 (Threshold 8,00) — PASS**, 0 Top-Verstöße,
  676 Klassen.
- drift-audit: nicht ausgeführt (in step-013 für EPIC-B erledigt).

## Abweichungen vom Plan

1. **Seam-Ausmaß:** Der Plan erlaubte für die Nachweise 2/3 alternativ „einen
   minimalen internen Test-Seam" bzw. „Pump-Optionen/Timeout injizierbar".
   Umgesetzt als ein Record (`ThinClientSessionOptions`) plus Extraktion von
   `RunSessionAsync`; zusätzlich zu Timeout/Transport sind Spawn-Delegate und
   Stdio-Streams injizierbar, weil sonst weder deterministischer Rohabbruch
   noch Kill-Nachweis ohne reale Prozesse möglich waren. Kein Verhalten des
   Default-Pfads geändert.
2. **Nachweis 2 in-proc statt als Prozesslauf:** Die erste Variante (echter
   Client-Prozess gegen einen impostierenden Pipe-Server, wie im Plan zuerst
   genannt) kollidiert strukturell mit parallel laufenden echten
   Daemon-Tests am selben benutzergebundenen Endpunkt (Impostor stiehlt deren
   Verbindungen — dieselbe Interferenzklasse, die step-013 zur Isolation via
   `AINETLINTER_NO_DAEMON` zwang). Nach Rücksprache mit der Plan-Alternative
   („oder über einen minimalen internen Test-Seam") läuft Nachweis 2 in-proc
   über den Seam; die Proxy-/Prozess-Ebene bleibt über Nachweis 3 (echtes
   Kill-Szenario) und Nachweis 4 (echte Zwei-Prozess-Läufe) abgedeckt.
3. **Wörtliche Review-Bedingung angepasst:** Das Review formulierte
   „inputFailure/outputFailure sind OperationCanceledException". Tatsächlich
   mappt `ObserveAsync` OCE auf `null`; das OCE-Signal steht als
   `task.IsCanceled` zur Verfügung. Die Umsetzung prüft genau diese
   IsCanceled-Bits — semantisch identisch zur gemeinten Bedingung.
4. **Nachweis 4 sequenziell statt parallel:** Zwei Clients laufen nacheinander
   gegen denselben langlebigen Daemon (Idle-Exit 5 min, Teardown-Kill per
   Welcome-PID). Warmth wird über Keys/RefreshCount/strikt wachsende
   Instanz-Uptime belegt statt über refreshCount ≥ 1 — der Zähler inkrementiert
   nur bei Reload/Inkremental-Refresh, nicht beim Initialload (Konzept nennt
   RefreshCount/Staleness-Zähler als Beleg; Uptime ist hier das schärfere,
   diskriminierende Signal).
5. **MCP-Gates via stdio-Session:** siehe Abschnitt oben — Werkzeug statt
   eingebetteter Toolaufruf, Inhaltlich identische Gates.

## Beobachtungen (für den Kritiker)

- Beim Caller-Cancel kann `WhenAny` die gecancelte Input-Task wählen; dann
  meldet die Pumpe `Completed=true` trotz Abbruch. In `RunSessionAsync` ist
  das folgenlos (beide Wege führen zu Exit 0), aber semantisch wäre
  `Completed=false` sauberer. Nur beobachtet, nicht geändert (Scope).
- `TerminateIdentifiedDaemon` wird weiterhin nach jedem unvollständigen Pump-
  Ergebnis gerufen (Review-MINOR) — unverändert, kein Handlungsbedarf.
- Die neuen Integrationstests halten den Endpunkt über `AcquireEndpointAsync`
  gegated; N2/N3b binden den Endpunkt bewusst NICHT (in-proc Mock), N4 schon.
  Ein überlebender Fremd-Daemon am Endpunkt bleibt die größte Flakiness-Quelle
  der Suite (heute empirisch belegt); ein Suite-weites Cleanup-Fixture könnte
  das strukturell lösen — Kritiker-Entscheidung.
- `McpRawWireTestHarness.RunAndCollectStdoutAsync` behielt Signatur und
  Verhalten; neue Aufrufer sollten die Diagnostics-Variante nutzen.

## Bekannte Unschärfen

- Nachweis 4 dauert ~1,5–2 min (Roslyn-Kaltlast + Health-Polling bis
  `Loaded`); er gated über `AcquireEndpointAsync` und verlängert die
  Integrations-Suite entsprechend.
- Der Stellvertreterprozess (`cmd.exe /c ping …`) ist Windows-spezifisch —
  konsistent mit der Windows-only-Vorgabe der Richtlinien §3.
- Die Vollstack-Integrationsabweichungen sind oben klassifiziert und einzeln
  isoliert nachgewiesen; eine Wiederholung des Vollstacks war gemäß
  Nutzervorgabe nicht zulässig.

## Commits

1. `683a3e4f` — fix(daemon): Haenger-Signatur und Contract-Nachweise liefern
   [11_epic-projektregistry-und-daemon] (Code + Tests).
2. Doku-/Status-Commit folgt unmittelbar (step-plan status, step-result.md,
   codemap.md).
