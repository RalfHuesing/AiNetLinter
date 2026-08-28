---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 015
corrects: null
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-29
code_commit_hash: 3bd71a73156ea0b7c5f9560edc7b38610f2afdcc
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 015: Repository-Akquisitionsvertrag mit injizierbarem Gitea-Transport und sicherer Staging-Fassade

## Zusammenfassung

Der Step führt einen schmalen, injizierbaren `IGiteaRepositoryTransport` mit
typisiertem Akquisitionsergebnis ein und verwendet dafür die bestehende
`ExternalSourceProviderFailureKind`-Semantik. `ExternalSourceRepositoryAcquirer`
validiert Mapping und Pfade, erzeugt einen eindeutigen Staging-Child, prüft
Checkout, Solution-Datei und Revision nach dem Transport und gibt einen
besitzenden `ExternalSourceCheckoutHandle` zurück. Fehler und Cancellation
bereinigen den eigenen temporären Checkout; Snapshot-, Workspace-, Host-,
Refresh-, Cache-, Git-/HTTP- und Netzwerklogik wurden nicht eingeführt.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` (neu) — definiert den schmalen Default-Branch-Clone-Port und sein typisiertes Ergebnis mit Failure-Kind und Diagnosen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` (neu) — kapselt Mapping-/Pfadvalidierung, kontrolliertes Staging, Post-Clone-Verifikation und Cleanup.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs` (neu) — stellt den besitzenden Checkout-Handle und das typisierte Fassadenergebnis bereit.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs` (neu) — prüft Child-Pfade und Reparse-Punkte und löscht nur sicher eigenen Besitz.
- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` — ergänzt zentrale Diagnosecodes für Staging, Checkout, Solution, Transport und Cleanup.
- `src/AiNetLinter/Configuration/ExternalSourcePathRules.cs` (neu), `ExternalSourceMappingValidator.cs`, `SourceSnapshotModels.cs` — zentralisiert die gemeinsame Laufwerksqualifikationsregel nach dem Drift-Audit.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs` (neu) — prüft Erfolg, alle sieben Provider-Failure-Gruppen, Transportfehler, Cancellation, Credentialschutz, Pfadgrenzen, fehlende Solution, Ownership und Reparse-Entscheidung mit TestKit-Leases und Transport-Double.

## Commit

- **Code-Commit-Hash:** `3bd71a73156ea0b7c5f9560edc7b38610f2afdcc`
- **Message:**
  ```
  feat(mcp): Akquisition kapseln [decompiled-assembly-analysis]

  Kapsle den injizierbaren Gitea-Transport hinter einem typisierten Vertrag.

  Sichere Staging-Pfade, Checkout-Ownership, Solution-Prüfung und Bereinigung ab.

  Decke Fehler, Cancellation, Pfadschutz und Reparse-Entscheidungen deterministisch ab.

  Refs: tasks/decompiled-assembly-analysis/step-015
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit nach diesem Ergebnis.

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~ExternalSourceRepositoryAcquirerTests → grün (20 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1957 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (360 Tests, 0 Fehler)
```

Stress-Tests wurden nicht ausgeführt. Die Tests verwenden ausschließlich
`TestTempDirectory`, `IsolatedFixtureLease` und ein lokales Transport-Double;
es wurden weder Netzwerk, Gitea, Git-Prozess noch externe Hosts/Restore-Quellen
verwendet. Die neue Implementierung enthält weder `Assembly.Load` noch
`AssemblyLoadContext`, Reflection oder Decompilation-Ausführung.

## Abweichungen vom Plan

- Die im Auftrag genannte Datei `.agents/Agent-Scaffolding/dev-loop/drift-loop/coder.md` existiert im Repository nicht; verwendet wurde die vorhandene kanonische Coder-Skill-Datei `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/coder/SKILL.md`.
- Nach dem Drift-Audit wurde die bereits vorhandene `IsDriveQualified`-Logik zentralisiert, damit der neue Akquisitionspfad keinen dritten Exact-Clone einführt.
- `task-state.md`, `roadmap.md`, `codemap.md` und `tech-debt.md` wurden nicht geändert; ihre Pflege bleibt wie beauftragt beim Orchestrator.

## Beobachtungen

- Der solution-weite Drift-Audit fand nach der Zentralisierung keinen neuen Exact-Clone im Akquisitionspfad; der verbleibende Exact-Cluster betrifft bestehende Assembly-Analysis-Tools. Der strukturell ähnliche Provider-/Transport-Ergebnis-Konstruktor ist durch die zwei getrennten Vertragsgrenzen begründet.
- `TD-001` bis `TD-003` bleiben unverändert; ein unabhängiger DRY-, MagicValues- oder DeadCode-Sweep wurde nicht begonnen. Der Magic-Value-Audit meldet nur den bereits als Konstante gefassten Checkout-Präfix und die interne Konstruktor-Guard-Meldung.

## Bekannte Unschärfen

- Ein echter Git-/HTTP-/Gitea-Transport, Credential-Binding, Refresh/Fetch, Cache, Snapshot-/Workspace-Erzeugung und Host-Wiring bleiben Folgepakete. Der injizierte Transport muss den übergebenen, noch nicht existierenden Zielpfad verwenden und eine nichtleere geladene Revision liefern.
- Reparse-/Symlink-Schutz wird vor und nach dem Transport über Pfad-/Attributprüfungen sowie eine reparse-sichere rekursive Bereinigung umgesetzt. Ein tatsächlich angelegter Symlink wurde für die deterministischen, plattformneutralen Tests nicht erzeugt; die reine Attributentscheidung ist separat getestet und sollte vom Kritiker gegen die Zielplattform geprüft werden.
- Der Besitzschutz ist best-effort und nicht als OS-Handle- oder atomare Race-Free-Garantie modelliert; das ist bewusst außerhalb der initialen Fassade und der späteren produktiven Adapterentscheidung.

## Auditstatus

`done (pending audit)` — der nachgelagerte Kritiker-/Drift-Audit steht noch aus.
