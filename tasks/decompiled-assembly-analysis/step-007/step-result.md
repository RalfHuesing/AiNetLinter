---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 007
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T19:00:00+02:00
code_commit_hash: cbd79a51ea7c23fbb58b417504daa65722e47d09
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 007: Source-Snapshot-Identität und residente Registry

## Zusammenfassung

Die Source-Snapshot-Grenze ist als immutable Identität, Workspace-Owner,
residente In-Memory-Registry und injizierbares Provider-Ergebnis umgesetzt.
Assembly-Aliase und Consumer-Kontext beeinflussen den Snapshot-Key nicht;
Solution-/Project-Match, Session-Komposition, Netzwerk und Runtime-Laden
bleiben außerhalb dieses Steps.

## Änderungen

- `SourceSnapshotIdentity` kanonisiert Repository-URL, geladene Revision und
  repository-relativen `.sln`-/`.slnx`-Pfad und erzeugt daraus einen stabilen,
  length-prefixed Key.
- `ExternalSourceSnapshot` hält den immutable Roslyn-`Solution`-Wert und den
  zugehörigen `Workspace`-Owner privat; die Freigabe ist idempotent.
- `SourceSnapshotRegistry` dedupliziert ordinal über die vollständige
  Identität, liefert top-level `SourceSnapshotLease`-Objekte, gibt unterlegene
  Doppelgänger kontrolliert frei und räumt beim terminalen Registry-Dispose
  resident gehaltene Owner auf. Es gibt keine TTL-, LRU- oder persistente
  Cache-Infrastruktur.
- `ExternalSourceProviderResult` transportiert optional `SourceSnapshot`;
  ein Snapshot mit `IsAvailable=false` wird abgewiesen. Der Unavailable-Adapter
  bleibt snapshotlos und netzwerkfrei.
- Provider- und Registry-Vertragstests decken Kanonisierung, Alias-
  Deduplizierung, Revision-/Solution-Trennung, Lease-/Dispose-Idempotenz,
  terminales Registry-Dispose, Duplicate-Ownership und Snapshot-Transport
  einschließlich Diagnosen ab.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs`
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`
- `tasks/decompiled-assembly-analysis/step-007/step-plan.md` — Statuswechsel
- `tasks/decompiled-assembly-analysis/step-007/step-result.md`

## Commits

- **Code-/Test-Commit:** `cbd79a51ea7c23fbb58b417504daa65722e47d09`
- **Message:** `feat: Source-Snapshot-Registry einführen [decompiled-assembly-analysis]`
- **Branch:** `main`
- **Push:** nein
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.

## Tests

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceProviderContractTests|FullyQualifiedName~SourceSnapshotRegistryTests|FullyQualifiedName~SourceSnapshotIdentityTests" --no-restore` — grün, 10/10 Tests.
- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1.897/1.897 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360 Tests.
- Stress-Tests wurden nicht ausgeführt.
- AiNetLinter-MCP `get_violations` im angefassten Assembly-Scope — 0 Verstöße.

## Abweichungen vom Plan

Der bestehende `UnavailableExternalSourceProvider` musste produktiv nicht
geändert werden: Er erzeugte bereits ausschließlich ein snapshotloses
Nichtverfügbarkeitsergebnis; die Vertragstests prüfen diesen Zustand jetzt
explizit. Die im Nutzerauftrag ausdrücklich ausgeschlossenen
`codemap.md`, `task-state.md`, `roadmap.md`, früheren Steps und `tech-debt.md`
wurden nicht geändert. Die Identitäts-Tests liegen gemeinsam mit den
Registry-Tests in der geplanten Assembly-Testdatei, wobei die Identitäts-
Testklasse separat benannt ist, damit die statische Testabdeckung beide
Verträge erkennt.

## Beobachtungen

Die Registry bleibt nach der letzten Lease-Freigabe resident; nur der
explizite Registry-Shutdown gibt Snapshots frei. Bei einem Identitäts-Treffer
bleibt der zuerst registrierte Snapshot Owner, während ein anderer angebotener
Snapshot genau einmal freigegeben wird. Derselbe bereits residente Snapshot
kann erneut geleast werden, ohne sich selbst freizugeben. Der Provider-Port
transportiert den Snapshot nur; die Registry übernimmt die Ownership beim
ersten `Acquire`.

Die Pfadnormalisierung wurde nach dem MCP-Lintbefund in kohärente interne
Helfer geteilt. Danach meldete der angefasste Produktions-Scope keine
Violations. Es wurden keine separaten DRY-, Magic-Value- oder Dead-Code-
Sweeps und keine künstlichen Tech-Debt-Pakete angelegt.

## Bekannte Unschärfen

Die Identity-Factory validiert die Repository-Adresse als absolute HTTP(S)-URL
und verwendet `Uri.AbsoluteUri` für die URI-Kanonisierung; der Repository-Pfad
bleibt dabei URI-semantisch erhalten. Geladene Revisionen werden von
Rand-Whitespace bereinigt, aber nicht auf Commit-Hash-Format oder Case
validiert, weil die tatsächliche Revision vom späteren Source-Provider geliefert
wird. Eine Assembly-/Projekt-Auswahl und eine tatsächliche Source-Ladung sind
bewusst noch nicht Teil dieses Vertrags.

Die Workspace-Freigabe ist über den `ExternalSourceSnapshot`-Owner und die
idempotente `Dispose`-Grenze abgesichert; der Vertrag bietet bewusst keine
TTL-, LRU-, Refresh- oder persistente Cache-Semantik.
