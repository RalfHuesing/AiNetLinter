---
status: done
type: step-result
task: 11_epic-projektregistry-und-daemon
step: 012
epic: EPIC-B
step_type: single
coded_by: coder
coded_by_model: GPT-5
coded_by_model_knowledge_cutoff: 2024-06
coded_at: 2026-08-24T06:14:00+02:00
code_commit_hash: ffb60157
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 012: Direkte Daemon-Prozess-Contracts

## Zusammenfassung

Die zwei MAJOR-Findings aus dem step-011-Review sind geschlossen. Ein echter
Zwei-Prozess-Contract startet zwei `AiNetLinter.exe`-Daemonen am selben Named-
Pipe-Endpunkt und prüft deterministisch stderr, Exitcode, Erreichbarkeit des
ersten Hosts und die Lock-Freigabe durch einen erneuten Start. Ein zweiter
Prozess-Contract konsumiert den Daemon-Handshake und führt anschließend über
denselben Produktionsstream MCP-SDK-Initialize und `tools/list` aus.

## Geänderte Dateien

- `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonProcessContractHarness.cs`
  — gemeinsamer echter Prozess-/Pipe-Harness mit begrenztem Cleanup und
  bestehender `McpProcessRunner`-/Fixture-Infrastruktur.
- `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostProcessContractTests.cs`
  — Doppelstart-, stderr-/Exitcode-, Erreichbarkeits- und Lock-Freigabe-Contract.
- `src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs`
  — echter Pipe-Handshake sowie MCP-SDK-Initialize und `tools/list`.

## Commit

- **Code-Commit-Hash:** `ffb60157`
- **Message:**
  ```
  fix: Reale Daemon-Prozessverträge absichern [11_epic-projektregistry-und-daemon]
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1713 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (354 Tests, 0 Fehler)
Gezielter Prozess-Contract-Filter → grün (2 Tests, 0 Fehler)
```

## Abweichungen vom Plan

Keine fachliche Scope-Erweiterung und keine Produktionscodeänderung. Der
gemeinsame Harness serialisiert nur die beiden neuen endpointgebundenen Tests
und hält im Doppelstart-Contract eine Pipe-Verbindung bis zur Lock-Prüfung,
damit der kurze Idle-Exit auch unter Suite-Parallelität deterministisch bleibt.
Die kurze Runner-Ausführung verwendet die bestehende Prozessinfrastruktur
ohne den globalen Lifetime-Slot zu belegen; ThinClient, Connect-or-Start,
Retry-/Hänger-Schutz und externes Wiring bleiben unverändert außerhalb.

## Beobachtungen

Die vollständige FastTests-Suite hatte während der Entwicklung einmal einen
bestehenden Registry-Race-Testfluke; der isolierte Test und der anschließende
vollständige Lauf waren grün. Der finale Integrationslauf war mit 354/354
Tests grün; die suiteweiten Long-Running-/Dogfood-Warnungen sind bestehende
Testdiagnostik und keine Fehler dieses Steps.

## Bekannte Unschärfen

Der Contract verwendet bewusst den realen benutzergebundenen Named-Pipe-
Endpoint und behandelt fremde, außerhalb der Tests gestartete Daemonen als
Umgebungsstörung. Der Drift-Audit bleibt gemäß Task-Vorgabe bis zum EPIC-B-
Abschluss offen; dieser Step ist daher `done (pending audit)`.

## Review-Findings

Beide MAJOR-Findings aus `step-011/step-review.md` sind geschlossen: der
reale Zwei-Prozess-Doppelstartvertrag und der reale Host-/MCP-Pipevertrag.
