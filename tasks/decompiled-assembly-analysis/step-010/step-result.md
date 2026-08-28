---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 010
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28
code_commit_hash: 28b7b76d4f9025495a6f1089954e14b42a9e0ca2
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 010: Provider-/Registry-Selection für direkte Assembly-Tools

## Zusammenfassung

Der direkte Assembly-Support kann jetzt eine vorbereitete Source-Auswahl über
einen injizierten Provider und die bestehende Snapshot-Registry komponieren.
Die Mapping-Wahl basiert ausschließlich auf der metadata-only Assembly-Identity;
unverfügbare, nicht gematchte und mehrdeutige Quellen bleiben Fallback-Zustände.
Ein disposable Selection-Scope hält die erworbene Lease bis nach Factory und
Result-Builder und gibt sie idempotent frei. Der bisherige Support-Aufruf ohne
Orchestrator bleibt im Decompilation-Pfad.

## Änderungen

- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs` —
  injizierter Loader-Result-/Provider-/Registry-Orchestrator mit
  `CreateFromSettings`, metadata-only Assembly-Identity-Auswahl,
  Providerdiagnosen, bestehendem Match-/Selection-Vertrag und
  `AssemblySourceSelectionScope` für die Lease-Lifetime.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs` —
  interne Orchestrator-Überladung mit gemeinsamer Pfad-/Loading-Prüfung,
  Selection-Request an die bestehende Factory und begrenzter, deduplizierter
  Loader-/Providerdiagnose im `AssemblyContext`; der Legacy-Overload bleibt
  ohne Orchestrator.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs` —
  netzwerkfreie Component-Regressionen für Mapping, Cancellation,
  Snapshot-Deduplizierung, Lease-Lifecycle, matched/no-match/ambiguous,
  unavailable und invaliden Loader-Fallback.

## Commits

- **Code-/Test-Commit:** `28b7b76d4f9025495a6f1089954e14b42a9e0ca2`
- **Message:** `feat: Selection-Orchestrator bauen [decompiled-assembly-analysis]`
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.
- **Branch:** `main`
- **Push:** nein

## Tests

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolSupportTests" --no-restore` — grün, 6/6 Tests.
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysis" --no-restore` — grün, 30/30 Tests.
- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1917/1917 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360 Tests, Dauer 2 m 27 s.
- Semantischer MCP-Impact meldete 0 Violations für den geänderten Support-Scope;
  der Assembly-Scope meldete vor Commit einen nicht aktualisierten
  `StaticTestSentinel`, weil die neue Testdatei zu diesem Zeitpunkt noch nicht
  im Index war.
- Stress-Tests wurden nicht ausgeführt.

## Tech-Debt und Auditbefunde

- Der eng begrenzte DRY-Audit fand keine neuen Duplikat-Cluster.
- Der Magic-Value-Audit meldete ausschließlich bereits bestehende Treffer im
  größeren Assembly-Paket; im neuen Composition-/Lease-Code entstand kein
  sicherer direkter Konsolidierungsfund.
- Der Dead-Code-Audit meldete `CreateFromSettings` als Low-Confidence-Kandidaten,
  da die spätere MCP-/Host-Komposition noch nicht angeschlossen ist. Die
  Factory bleibt wegen des Step-Vertrags und des vorgesehenen Folge-Wirings
  erhalten.
- `TD-001`, `TD-002` und `TD-003` sowie breite Audits wurden nicht geändert.

## Abweichungen vom Plan

- `tasks/decompiled-assembly-analysis/codemap.md` blieb unverändert, weil der
  ausdrückliche Step-Auftrag diese Datei ausschließt; die generelle
  Coder-Skill-Vorgabe zur CodeMap wurde diesem engeren Auftrag untergeordnet.
- Es wurden keine MCP-Registrierungen, Host-/Daemon-Pfade,
  `InspectAssemblyTool`, `FindAssemblyExtensionsTool`, Provider-Akquisition,
  Netzwerkzugriffe oder Fremdprojekt-Restores angefasst.
- Der Orchestrator behandelt mehr als ein case-insensitiv passendes Mapping als
  Fallback ohne Provideraufruf. Der bestehende Validator- und Resolver-Vertrag
  wurde dafür nicht dupliziert oder verändert.

## Beobachtungen

Der Orchestrator übergibt einen verfügbaren Snapshot ausschließlich an
`SourceSnapshotRegistry.Acquire`. Bei Registry-/Resolver-Fehlern wird eine
bereits erworbene Lease freigegeben; bei Ablehnung durch
`AssemblySourceSelection.Create` übernimmt der Scope die Lease trotzdem.
Registry- und Snapshot-Ownership verbleiben außerhalb des Scopes bei der
bestehenden Registry.

Loader- und Providerdiagnosen bleiben strukturiert im Scope und werden am
Support-Rand als höchstens 100 stabile, deduplizierte Diagnosezeilen neben den
Assemblydiagnosen in den Context aufgenommen. Ein `Matched`-Selectionwert mit
unbrauchbarer Compilation fällt weiterhin über die vorhandene Factory auf
Decompilation zurück.

## Bekannte Unschärfen

Die neue Orchestrator-Überladung wird in diesem Step nur durch direkte Tests
verwendet. Die spätere gemeinsame Provider-/Registry-Instanz und die MCP-
Registrierung sind absichtlich nicht angeschlossen; dadurch bleibt der
bestehende öffentliche Assembly-Toolpfad unverändert decompiled.

Der Scope exponiert `NoMatch` und `Ambiguous` als transportierte Selection,
während die bestehende Factory diese Zustände bewusst nicht source-backed
projiziert. Eine weitergehende Match-Evidence-Ausgabe im MCP-Payload ist nicht
Teil dieses Composition-Steps.
