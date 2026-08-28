---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 012
corrects: null
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28
code_commit_hash: db386bc4
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 012: Gemeinsame Host-Komposition für Assembly-MCP-Tools

## Zusammenfassung

Die direkten Assembly-MCP-Registrierungen verwenden im expliziten Hostpfad
jetzt eine gemeinsame `AssemblyAnalysisHostComposition`. Die Komposition lädt
die External-Source-Konfiguration einmal, hält Provider, Snapshot-Registry und
Selection-Orchestrator zusammen und gibt die Registry beim Hostende idempotent
frei. Stdio besitzt eine Komposition pro Serverlauf; der Daemon capturt eine
Komposition für seine gesamte Lebensdauer und reicht sie an jede Session weiter.

Der kanonische `AnalysisToolCall.ExecuteAssemblyAsync`-Dispatch blieb
unverändert. `inspect_assembly` und `find_assembly_extensions` delegieren über
die bestehenden Tool-Wrapper an die vorhandene Orchestrator-Überladung; der
Legacy-Aufruf ohne Composition bleibt der Decompilation-Fallback.

## Änderungen

- `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisHostComposition.cs` — neuer
  hostlebenslanger Owner für Loader-Result, Provider, Registry und Orchestrator
  mit kontrolliertem, idempotentem Dispose.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` und
  `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` —
  explizite Composition-Durchleitung ausschließlich für die beiden direkten
  Assembly-Registrierungen; Projekt- und Fremdtool-Registrierungen bleiben
  unverändert.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs` und
  `FindAssemblyExtensionsTool.cs` — dünne Orchestrator-Overloads bei gemeinsamem
  Parameter-/Result-Builder.
- `src/AiNetLinter/Commands/McpServerCommand.cs` und
  `src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs` — Stdio-/Daemon-Lifetime
  und Session-Weitergabe der Composition.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisHostCompositionTests.cs`,
  `WiringContractTests.cs` und `DaemonHostMcpContractTests.cs` — Ownership-,
  Dispose-, Fallback-, Toolinventar- und Session-Regressionen.
- `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs` und
  `Mcp/Daemon/DaemonHostMcpProcessContractTests.cs` — beide direkten Assembly-
  Tools über Stdio und Daemon gegen echte MCP-Hosts geprüft.

## Commits

- **Code-/Test-Commit:** `db386bc4`
- **Message:** `feat: Assembly-Host verdrahten [decompiled-assembly-analysis]`
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.
- **Branch:** `main`
- **Push:** nein

## Tests

- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1921/1921.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360, Dauer 2 m 16 s.
- Fokussierte Host-Regressionen — grün, 16 FastTests und 2 IntegrationTests.
- Stress-Tests wurden nicht ausgeführt.

## Abweichungen vom Plan

- `tasks/decompiled-assembly-analysis/codemap.md` wurde nicht geändert, weil
  der Nutzer sie für diesen Step ausdrücklich als unveränderlich vorgegeben
  hat. Die Änderung ist in diesem Result vollständig referenziert.
- Die produktiven Hosts injizieren weiterhin keinen echten externen Provider;
  ohne Provider-Injection wird der bestehende `UnavailableExternalSourceProvider`
  verwendet. Die Integrationstests prüfen deshalb den direkten Host-Fallback;
  Source-backed-/Lease-Semantik bleibt durch die vorhandenen Support-Tests
  abgedeckt.

## Beobachtungen

- Die Composition wird nicht in einer Registration-Lambda erzeugt. Die Lambdas
  greifen bei jedem direkten Assembly-Aufruf auf die bereits hostlebenslange
  Composition zu; dadurch bleiben Registry-Ownership und Snapshot-Deduplizierung
  an der Hostgrenze.
- Die Composition besitzt ausschließlich die von ihr erzeugte
  `SourceSnapshotRegistry`; der injizierte Provider erhält keinen künstlichen
  Dispose-Vertrag.
- Der bestehende `DaemonHostCommand`-AIContext-Footprint bleibt als vorher
  vorhandene Linter-Warnung bestehen und wurde außerhalb dieses schmalen Steps
  nicht umgebaut.
- TD-001 bis TD-004, `AnalysisToolCall`, Projekt-Targets, Toolinventar und
  Task-/Roadmap-/CodeMap-/Tech-Debt-Dateien wurden nicht fachlich verändert.

## Bekannte Unschärfen

- Die gezielte netzwerkfreie Adapterprüfung verwendet den bestehenden
  `UnavailableExternalSourceProvider`; ein source-backed Ergebnis wird weiterhin
  in den genehmigten Support-Regressionen direkt geprüft, nicht über eine echte
  externe Provider-Akquisition im Hostprozess.
- Die nachgelagerte Kritikerprüfung und der Drift-Audit stehen noch aus.

## Auditstatus

`done (pending audit)` — der nachgelagerte Kritiker-/Drift-Audit steht noch aus.
