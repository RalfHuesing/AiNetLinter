---
status: done
type: step-result
task: speedup-tests
step: 013
epic: EPIC-4
step_type: single
coded_by: coder
coded_by_model: gpt-5.6-terra High
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-12
code_commit_hash: 8edee78
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 013: EPIC-4 Teil 1 — Skeleton-Filterkohorte auf vorbereitete FilterMini-Solution migrieren

## Zusammenfassung

Die Skeleton-Filtermatrix läuft jetzt als 18-fällige Component-Kohorte gegen den vorbereiteten
`FilterMini`-Snapshot; die zwei echten Pfadverträge liegen als Integrationstests vor. Der
`SkeletonMapBuilder` trennt den einmaligen Pfad-Load vom objektbasierten `Solution`-Kern, und die
Testplattform verwendet einen deterministischen, testframework-freien BCL-Referenzsatz.

## Geänderte Dateien

- `src/AiNetLinter/Maps/Skeleton/SkeletonMapBuilder.cs` — Pfadadapter delegiert an den internen Solution-Kern mit Parameter-Record.
- `src/AiNetLinter/Core/LinterEngine.cs` — gewährt TestKit Zugriff auf die unveränderte interne Console-Schnittstelle.
- `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs` — baut gecachte Core-Referenzen deterministisch aus BCL-Assemblies.
- `src/AiNetLinter.TestKit/RecordingLintConsole.cs` und `Properties/AssemblyInfo.cs` (neu) — gemeinsames Console-Testdouble für Fast-/IntegrationTests.
- `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs` (neu) — 18 migrierte Component-Filterverträge gegen `FilterMini`.
- `src/AiNetLinter.IntegrationTests/Maps/Skeleton/SkeletonMapBuilderAdapterTests.cs` (neu) — zwei Pfad-/MSBuild-Adapterverträge gegen isoliertes `FilterMini`.
- `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs` und `src/AiNetLinter.IntegrationTests/Platform/FilterMiniFidelityTests.cs` — negative In-Memory-Testprojekterkennung abgesichert.
- `src/AiNetLinter.Tests/Cli/FilterCliIntegrationTests.cs` und `src/AiNetLinter.Tests/Maps/Skeleton/SkeletonMapBuilderTests.cs` — nach vollständiger Übernahme entfernt.
- `tasks/speedup-tests/{test-migration-ledger,tech-debt,codemap}.md` — zwei Migrationen, TD-002/TD-005 und aktuelle Zielorte nachgeführt.

## Commit

- **Code-Commit-Hash:** `8edee78`
- **Message:**
  ```
  feat(skeleton): migriere Filterkohorte auf FilterMini [speedup-tests]
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~FilterCliIntegrationTests|FullyQualifiedName~SkeletonMapBuilderTests" → grün (20 Tests, 0 Fehler)
dotnet build → grün (5 Projekte, 0 Warnungen/Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~SkeletonMapFilterTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests" → grün (26 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~SkeletonMapBuilderAdapterTests|FullyQualifiedName~FilterMiniFidelityTests|FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (8 Tests, 0 Fehler)
```

## Abweichungen vom Plan

- Der Plan hatte beim Coder-Aufruf noch `status: open` statt `in_progress`; nur der erlaubte Statuswert wurde auf `done (pending audit)` gesetzt.
- Der erste enge FastTests-Lauf deckte einen echten Kernpfadfall auf: ein relativer Anzeigenpfad erzeugte einen leeren Solution-Ordner. Der Kern fällt dafür auf das aktuelle Verzeichnis zurück; der Folge-Lauf war grün.

## Beobachtungen

- Keine außerhalb des Step-Scopes.

## Bekannte Unschärfen

- Die ausgewählten BCL-Assemblies decken die aktuelle FilterMini- und Factory-Kohorte ab; neue In-Memory-Szenarien mit weiteren Framework-APIs müssen ihre Referenzen bewusst über `AdditionalReferences` liefern.
