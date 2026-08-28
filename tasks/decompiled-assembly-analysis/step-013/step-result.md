---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 013
corrects: step-012
epic: EPIC-03
step_type: correction
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28
code_commit_hash: 1cd279f0ae7a683484cd21a32157a88b84313e95
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 013: Registrierten Assembly-Host-Wiring-/Lifecycle-Vertrag absichern

## Zusammenfassung

Der Step-012-Nachweis ist durch einen deterministischen Fast-Component-Contract
geschlossen. Zwei MCP-Sessions laufen über `McpClient` und
`RunMcpSessionAsync` gegen dieselbe hostlebenslange
`AssemblyAnalysisHostComposition`; die beiden direkten Assembly-Tools werden
dabei ausschließlich über die echte Tool-Collection und ihre Registrierung
aufgerufen.

Der Test liefert für beide registrierten Callbacks source-backed Ergebnisse aus
einem kontrollierten Snapshot, prüft Source-only-Typ/Extension, Filter-/Limit-
Weitergabe und Providerdiagnose und bewahrt den bestehenden Fallback durch die
unveränderten Bestandsverträge. Nach jedem Session-Ende bleiben Composition,
Registry und Snapshot resident. Erst das explizite Composition-Dispose leert
die Registry und entsorgt den Snapshot; wiederholtes Dispose und Zugriff nach
Dispose bleiben abgesichert.

## Änderungen

- `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs` —
  registrierter source-backed MCP-Callback-Vertrag mit zwei sequentiellen
  Session-Lebenszyklen, Recording-Provider, Payload-/Diagnose-/Limit-
  Assertions und Daemon-Lifecycle-Grenze.
- `src/AiNetLinter.FastTests/Fixtures/ExternalSourceSnapshotTestFactory.cs` —
  einmalige test-only Snapshot-Fixture für die Assembly-Source-Tests.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs`
  und `AssemblyAnalysisToolSupportTests.cs` — bestehende private Snapshot-
  Builder auf die gemeinsame Test-Fixture umgestellt; Source-/Fallback-
  Verträge fachlich unverändert.
- Keine Produktionsänderung erforderlich; die bestehende
  `BuildToolCollection`-/`AssemblyAnalysisToolRegistrations`-Verdrahtung hat
  den neuen End-to-End-Contract erfüllt.

## Commits

- **Code-/Test-Commit:** `1cd279f0ae7a683484cd21a32157a88b84313e95`
- **Message:** `test: Assembly-Session-Wiring absichern [decompiled-assembly-analysis]`
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.
- **Branch:** `main`
- **Push:** nein

## Tests

- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1922/1922.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360.
- Fokussierte Host-/Source-Regressionen — grün, 15/15.
- Stress-Tests wurden nicht ausgeführt.
- Die gezielte MCP-Violationsprüfung für `src/AiNetLinter.FastTests` meldete 0 Verstöße.

## Abweichungen vom Plan

- Der Produktions-Wiringpfad musste nicht geändert werden: Der neue Test
  durchläuft `RunMcpSessionAsync` → `BuildToolCollection` →
  `AssemblyAnalysisToolRegistrations.Register` und weist nach, dass die
  vorhandene Composition-Weitergabe source-backed funktioniert.
- Die bestehenden Integration-Process-Contracts wurden nicht erweitert, weil
  der geforderte injizierte Zwei-Session-Nachweis im vorhandenen in-memory
  `RunMcpSessionAsync`-Pfad vollständig und deterministisch erbracht wird; die
  unveränderten Process-Contracts behalten Handshake, Toolinventar und
  decompiled/unavailable-Fallback bei.
- `codemap.md` wurde entsprechend der expliziten Step-Vorgabe nicht geändert,
  obwohl die allgemeine Coder-Anweisung eine Codemap-Aktualisierung vorsieht.
  `task-state.md`, `tech-debt.md`, `roadmap.md`, Docs, README, rules.json und
  frühere Step-Artefakte blieben ebenfalls unverändert.

## Beobachtungen

- Der Recording-Provider erhält vier Aufrufe über dieselbe konfigurierte
  Mapping-Instanz, jeweils mit einem cancelbaren Request-Token. Die Registry
  dedupliziert den wiederholt gelieferten Snapshot auf einen residenten Eintrag.
- Die Source-Assertions prüfen sowohl Text als auch StructuredContent: Der
  source-backed Ursprung, `SourceOnly`, `SourceOnlyExtension`, die
  kontrollierte Diagnose sowie `maxMembers`/`maxResults` sind sichtbar; der
  decompiled-Ursprung und `TargetOnly` erscheinen im matched Szenario nicht.
- Es wurden keine Assembly-Loads, Reflection-Ausführung,
  AssemblyLoadContexts, Netzwerk-/Gitea-Akquisitionen, neuen Provider- oder
  Registry-APIs oder zusätzlichen Registrierungen eingeführt. TD-001 bis
  TD-004 und breite Audits bleiben unangetastet.

## Bekannte Unschärfen

- Der neue Lifecycle-Contract verwendet den bestehenden in-memory
  MCP-Session-Runner statt einen zusätzlichen Prozess-Contract. Der echte
  Daemon-/Stdio-Transport bleibt durch die vorhandenen Integrationstests
  abgedeckt; eine injizierte Source-Provider-Composition im externen Prozess
  war ausdrücklich außerhalb des Scopes.
- Der nachgelagerte Kritiker-/Drift-Audit steht noch aus.

## Auditstatus

`done (pending audit)` — der nachgelagerte Kritiker-/Drift-Audit steht noch aus.
