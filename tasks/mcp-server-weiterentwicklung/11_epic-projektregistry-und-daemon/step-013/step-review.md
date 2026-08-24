---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 013
epic: EPIC-B
step_type: single
reviewed_by: kritiker
reviewed_by_model: stealth/ox-alpha (openrouter)
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-24T12:10:00+02:00
verdict: issues
tech_debt_ids: [TD-007]
---

# Review Step 013: ThinClient — Connect-or-Start, opake Pump, Retry/Hänger, Reaper/Escape, Health und Abschlussmigration

## Verdict

- [ ] approved
- [x] **issues** — Korrektur-Step erforderlich (`corrects: step-013`; Nummerierung durch Orchestrator). Zwei MAJOR-Findings: fehlende planierte Contract-Nachweise (F1) und unerreichbare Timeout-Diagnostik in der Pump (F2).
- [ ] blocked

## Geprüft

- [x] Plan-Erfüllung: alle Diffs beider Commits (`b9605ea5` Code/Tests, `759da1bf` Doku/Sync/Status) vollständig gelesen, nicht nur die Messages; Abgleich gegen Scope, Akzeptanzkriterien und Testkatalog des Plans
- [x] Rules-Konformität: ausschließlich die im Plan unter „Rules-Refs“ zitierten Dateien (`AiNetLinter.mdc`, `AiNetLinterRichtlinien.mdc`)
- [x] Logische Korrektheit: ThinClientProxy/ThinClientLauncher/DaemonBytePump/DaemonHost-Wiring/Health-Wiring Zeile für Zeile nachvollzogen
- [x] Konzept-Treue: Konzept.md B.1–B.7 inkl. Self-Audit-Punkte gegen die Umsetzung geprüft
- [x] Build: selbst nachgeprüft — `dotnet build` → 0 Warnungen, 0 Fehler (TreatWarningsAsErrors erfüllt)
- [x] Tests: gezielte Filterläufe als Stichprobe (kein Vollstack, keine Stress-Kategorie, kein erneuter Drift-Audit)

## Beantwortung des spezifischen Prüfauftrags (Kernfrage)

**Urteil: Die beiden Vollstack-Abweichungen sind Testisolation-/Parallelitätsprobleme bzw. Harness-Drift — keine echten Vertragsregressionen des Produktionscodes.**

Begründung aus Plan, Result, Diffs und eigenen Stichproben:

1. **Shared-Daemon-Endpunkt (IntegrationTests 352/356):** Sämtliche Zwei-Prozess-Tests teilen sich den benutzergebundenen Pipe-Endpunkt `ainetlinter.analyzer.v1.<username>`. Mit diesem Step routet `--mcp-server` erstmals über den ThinClient — dadurch connecteten/spawnten die *Bestands*-MCP-Prozesstests im Vollparallelauf echte Daemons am gemeinsamen Endpunkt (übernommene Daemons früherer Tests mit fremder Fixture-Konfiguration, Doppelstart-Fehler, Idle-Daemons, die Build-Artefakte sperren). Isoliert laufen dieselben Tests grün. Die Korrektur im selben Commit ist eine Isolationsmaßnahme, keine Assertion-Abschwächung: `McpProcessHost`, `McpHandshakeToolRegistrationTests`, `McpObservabilityE2ETests`, `McpServerCommandErrorHandlingTests` und der Raw-Wire-Harness (Default `noDaemon: true`) pinnen die Bestandstests explizit auf den dokumentierten Escape-Pfad (`AINETLINTER_NO_DAEMON=1`); bewusste Daemon-Läufe erhalten einen kurzen Idle-Exit von 0,01 min. Das ist exakt die von Richtlinien §4 geforderte „gezielte Lösung“ statt Collection-Serialisierung.
2. **Alte Versionsrepräsentation im Harness:** `DaemonProcessContractHarness.cs:82` sendete `exeVersion` noch als viersegmentige Assembly-Version, während die Produktion auf die gemeinsame Quelle `McpServerOptionsFactory.GetServerVersion()` umgestellt wurde (`ThinClientProxy.ConnectAsync`, `CurrentDaemonIdentityProvider.GetIdentity`, Health-Payload). Der Harness-Hello galt damit als Executable-Mismatch und löste `ShutdownRequested`/`VersionConflict` in Tests aus, die schlicht Welcome erwarteten. Die Korrektur richtet den Harness an dieselbe Single-Source-of-Truth aus — sie vereinheitlicht die Versionswahrheit, schwächt nichts.
3. **Eigene gezielte Stichproben (alle grün):**
   - `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~ThinClientMcpProcessContractTests` → 2/2 (normaler Connect-or-Start mit Health-Runtime-Payload + Escape-Pfad)
   - `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~DaemonHostProcessContractTests | DaemonHostMcpProcessContractTests` → 2/2 (Doppelstart/Lock-Freigabe sowie Host-Handshake/MCP-Initialize über den korrigierten Harness)
   - `ProjectRegistryTests.Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner` dreifach gezielt → 3/3 grün; passend zur Coder-Einschätzung ein timingabhängiger EPIC-A-Bestandstest unter Volllast, kein Step-013-Defekt.
4. Die Result-Darstellung ist ehrlich („bewusst nicht als grüne Vollsuite behauptet“) und deckt sich mit dem Befundbild der Diffs.

## Befund

### Plan-Erfüllung

Weitgehend erfüllt: Routing (`Program.cs:45`), Escape, detached Spawn ohne stdout/stderr-Redirect, opake NDJSON-Pump mit Frame-Limit, genau-ein-Retry-Schleife (`MaximumRetries = 1`), Kill nur der per welcome-PID identifizierten Instanz, Reaper-Erbe via `McpServerLifetime.Start(args.ParentPid)` im Client, Flag-Weitergabe inkl. InvariantCulture, daemonweite Health-Felder (`mode`/`connectionId`/`connections`/`processId`/`uptimeSeconds`/`keys`/`daemonVersion`) ohne Toolvertragserweiterung, Observability-Anreicherung auf Anwendungsebene (dokumentierte Paketgrenzen-Entscheidung, kein Bump), Doku-/Sync-/Codemap-Pflichten (Stichprobe `Docs/agent-api.md`: sachlich und code-konsistent; Codemap für Programm/Factory/Daemon/Testordner aktualisiert). **Nicht erfüllt:** mehrere im Testkatalog explizit genannten Contract-Nachweise fehlen (siehe Finding F1), während die zugehörigen DoD-Zeilen als belegt angekreuzt sind.

### Rules-Konformität

Keine Verstoß gegen die referenzierten Regeln festgestellt: neue Typen durchgängig `sealed`/statisch bzw. Records; keine leeren Catches (alle melden sichtbar nach stderr oder werfen weiter); kein `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`; Namespaces mappen auf `Mcp/Daemon/`; keine Task-/Step-Artefakt-Referenzen in Kommentaren; keine zwangsserialisierende Test-Collection eingeführt (Isolation über env-Pinning = regelkonforme gezielte Lösung); neue Integrationstests nutzen die bestehenden Fixture-/Harness-Infrastrukturen statt Ad-hoc-Skripten; Zero-Warning-Build bestätigt.

### Logische Korrektheit

Die Produktionslogik ist im Reviewed-Zustand in sich konsistent: Handshake-Versionsvergleich greift auf beiden Seiten auf dieselbe Versionsquelle zu; Empfang eines `shutdown`-Frames (kontrollierter Neustart bei Solo-Mismatch) führt über IOException → Catch-all im Connect-first → Spawn-second zum konzeptgerechten Neustart; `VERSION_CONFLICT` bricht sichtbar über den generischen FATAL-Catch (`Program.cs:61-65`) mit Exit ≠ 0 ab; Kill beschränkt sich auf PID > 0 und Fremd-PID. **Gefunden:** In `DaemonBytePump.ReadFailure` ist der Timeout-Diagnose-Zweig unerreichbar (Finding F2) — der Hänger-Schutz *funktioniert* (Timeout → Kill → genau ein Replay), aber sein Ereignis verliert die Timeout-Signatur.

### Konzept-Treue (Ebene 4)

Kein Non-Goal verletzt: kein SDK/JSON-RPC-Parser im ThinClient (JSON nur für den bestehenden Pipe-Handshake), keine zweite Registry, kein HTTP/TCP/Service, Batch unberührt, MRU/Idle-Exit/step-012-Verträge unverändert übernommen. Muss-Haben aus B.5/B.7 (Health-Felder, Dogfood, §C.5, Registrierungen) ist belegt. Ein Muss-Haben aus **B.6** (Testkatalog) ist teilweise nicht umgesetzt und fällt mit Finding F1 zusammen (Race-Logik am Mock-Pipe, Hänger-Stellvertreter, zwei ThinClients mit Shared-Warmth). Ein konzeptinterner Spannungspunkt („Call-Log-Ereignis“ beim SDK-freien Client) ist unten als Entscheidungsbedarf dokumentiert, nicht als Rückbau-Forderung.

### Build-/Test-Status

```
dotnet build                                                                                       → grün (0 Warnungen, 0 Fehler)
dotnet test ...IntegrationTests --filter FullyQualifiedName~ThinClientMcpProcessContractTests      → grün (2/2)
dotnet test ...IntegrationTests --filter ~DaemonHostProcessContractTests | ~DaemonHostMcpProcess…   → grün (2/2)
dotnet test ...FastTests --filter ~Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner → grün (3× je 1/1)
```

Vollstack gemäß Nutzervorgabe nicht wiederholt; Stress nie ausgeführt; Drift-Audit nicht erneut ausgeführt.

## Findings

1. `src/AiNetLinter.FastTests/Mcp/Daemon/` + `src/AiNetLinter.IntegrationTests/Mcp/Daemon/` (fehlende Dateien) — **[MAJOR]** [Plan-Erfüllung / Logische Korrektheit] Der Plan-Testkatalog und die angekreuzten DoD-Zeilen fordern Contract-Nachweise, die im gesamten Testbestand fehlen (Belegsuche über beide Suiten: `ReplayFrame|DaemonBytePump|ThinClientProxy|PumpIdle|ReplayWindow` trifft ausschließlich `ThinClientContractTests.BytePump_ForwardsOpaqueFramesWithoutJsonInterpretation`, das keinen Replay-/Retry-Pfad berührt):
   - **Genau-ein Replay** nach Rohframe-Abschluss ohne Antwort (AK 4): Fenster gesetzt → Antwort löscht das Fenster (`Take() == null`); erneuter Lauf schreibt die ReplayFrame zuerst. Auf `DaemonBytePump`-Ebene ohne Seams unit-testbar (Streams injizieren, `DaemonPumpOptions(…, ReplayFrame)`).
   - **Zweiter Rohfehler ohne dritte Runde** (AK 4): zweiter Abbruch → `Completed=false`, Exit ≠ 0, kein Loop; Proxy-Seite über Integration mit kontrolliertem Pipe-Abbruch oder über einen minimalen internen Test-Seam am Retry-Fenster absichern.
   - **Ping-/Hänger-Timeout → TerminateIdentifiedDaemon + genau ein Ereignis** (AK 5, Plan-Ausnahme „Stellvertreterprozess“): kein Test existiert. Deterministischer Kern auf Pump-Ebene (winziges `PumpIdleTimeout`, stummer Stream → Timeout-Signatur nach F2-Fix assertieren); die Kill-/Restart-Entscheidung des Proxys braucht dafür einen kleinen testbaren Seam (z. B. Pump-Optionen/Timeout injizierbar) oder einen engen Integrationslauf — Architekturmehrheit liegt beim Korrektur-Step, kein Rückbau bestehender Verträge.
   - **Zwei ThinClients teilen die Daemon-Registry** (DoD-Zeile „…zwei ThinClients teilen die Daemon-Registry“, B.6 Shared-Warmth über RefreshCount/Keys): kein Test existiert; als enger Zwei-Prozess-Lauf über den Raw-Wire-Harness (`noDaemon: false`, kurzer Idle-Exit, gemeinsame Fixture) ergänzen.
   - **Connect-or-Start-Transitions/konkurrierende Starter am Mock-Pipe** (B.6 Unit): kein dedizierter Unit-Test; nur indirekt über den kalten Integrationslauf abgedeckt.
   **Fix:** Korrektur-Step legt genau diese fünf Nachweise an (Pump-Level-Contracts sind seamfrei sofort möglich; Proxy-Level über Integration oder minimalen Seam). Bis dahin ist die DoD-Aussage „durch Unit-/Integration-Contracts belegt“ für genau-ein Retry, zweiter Rohfehler, Ping-Hänger-Schutz und Shared-Warmth nicht haltbar.
2. `src/AiNetLinter/Mcp/Daemon/DaemonBytePump.cs:146` (gegenüber `:149-150`) — **[MAJOR]** [Logische Korrektheit] Der `TimeoutException`-Zweig ist unerreichbar: Bei Erreichen des Pump-Idle-Limits canceln beide Pump-Tasks über denselben linked Token gemeinsam, sodass Zeile 146 (`pumpCancelled && inputTask.IsCanceled && outputTask.IsCanceled → return null`) zuerst greift; der Zweig, der „Die Daemon-Pipe antwortete nicht innerhalb des Hanger-Schutz-Zeitlimits.“ baut (Zeilen 149-150), kann nie erreicht werden. Folge: Das Hänger-Ereignis erscheint über `ThinClientProxy.ReportPumpFailure` als „unbekannter Pipe-Fehler“ — Akzeptanzkriterium „Retry, Hänger, Konflikt und Restart sind unterscheidbar“ ist auf dem Diagnosekanal verletzt. **Fix:** Den reinen Idle-Timeout-Fall vor dem Null-Zweig erkennen (`linked.IsCancellationRequested && !callerToken.IsCancellationRequested && inputFailure/outputFailure sind OperationCanceledException` → TimeoutException liefern); anschließend im neuen Hänger-Contract (F1) auf diese Signatur assertieren.

## Konzept-Entscheidungsbedarf (kein Blocker, Nutzerentscheid)

Akzeptanzkriterium 5 fordert für den Hänger-Fall „genau ein Call-Log-Ereignis“ (Konzept B.3: „Ereignis ins Call-Log“). Der ThinClient ist laut Konzept/Plan bewusst SDK-frei und hat keinen Observability-Sink; die Umsetzung meldet Retry/Hänger/Kill daher ausschließlich als stderr-[WARN]. Beide Forderungen stehen in Spannung. Ohne Rückbau entscheidbar, indem die Unterscheidbarkeit (AK 6) über signaturhaltige stderr-Ereignisse (F2-Fix) und die korrekte Attribuierung der Daemon-Call-Logs (connectionId/mode, bereits umgesetzt) erklärt wird; soll das Ereignis zusätzlich physisch im Observability-Call-Log landen, wäre ein Konzeptnachtrag (z. B. dateibasiertes Client-Ereignisprotokoll oder Akzeptanz der stderr-Lösung) nötig. Entscheidung beim Nutzer; der Korrektur-Step für F1/F2 ist davon unabhängig möglich.

## Sonstige Beobachtungen / MINOR / NITPICK

- Stdout-Purity im Daemon-Pfad ist nur implizit belegt: der strenge All-Zeilen-JSON-Contract (`McpServerCommandJsonRpcFramingTests.HandshakeAndSingleToolCall_AllStdoutLinesAreValidJsonRpcFrames`) läuft seit der Isolationsumstellung gegen den In-proc-Pfad; der neue Daemon-Lauf asserted nur `FindResponse(id=2)`. Empfehlung: dieselbe All-Zeilen-Assertion zusätzlich für einen `noDaemon: false`-Lauf — im F1-Korrektur-Step mitzulegen. Ebenso behauptet `codemap.md` („Raw-Wire-Harness deckt … stdout-Purity ab“) mehr, als dediziert geprüft wird.
- `VERSION_CONFLICT` erreicht den Agenten über den generischen `[FATAL ERROR]`-Catch (`Program.cs:61-65`) statt als gezielter Hinweis; sichtbar und Exit ≠ 0, aber nicht selbsterklärend. Kann opportunistisch mit F2 geschärft werden (`ThinClientVersionConflictException` gezielt fangen).
- `TerminateIdentifiedDaemon` wird nach *jedem* unvollständigen Pump-Ergebnis gerufen, nicht nur nach Hänger — praktisch harmlos (bei totem Daemon No-op; Named Pipes am lokalen Host trennen nicht bei gesundem Server), erwähnt zur Dokumentation, kein Handlungsbedarf.

## Tech-Debt-Einträge aus diesem Review

- `TD-007` (siehe `tech-debt.md`) — Abdeckungsasymmetrie: Legacy-MCP-Integrationssuiten laufen fixiert im Escape-Pfad; der produktive Daemon-Pfad wird nur von wenigen dedizierten Contracts abgedeckt.
