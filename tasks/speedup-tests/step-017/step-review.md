---
status: done
type: step-review
task: speedup-tests
step: 017
epic: EPIC-4
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra Medium
reviewed_by_model_knowledge_cutoff: nicht ausgewiesen
reviewed_at: 2026-08-13T00:30:00+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 017: Duplicate-Detection-Engine-Kohorte auf die In-Memory-Testplattform migrieren

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: die kuratierten `<rules_dir>`-Dateien eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Commit `b8730a7` verschiebt alle 24 historischen Vertragsmethoden in die drei vorgesehenen Component-Dateien, ergänzt ausschließlich den Local-Function-Vertrag, löscht die drei Legacy-Dateien und Commit `7f54028` führt Ledger sowie Codemap konsistent nach; der Preimage-Abgleich ergibt 27 XUnit-Fälle (7 Facts + 4 Theory-Daten, 9 Facts, 7 Facts) gegenüber 29 Fällen nach der Ergänzung.

### Rules-Konformität

Die kuratierten Stil- und Testregeln sind eingehalten: alle neuen Dateien sind nullable-fähig, die Partial-Kohorte teilt `Category=Component`, verwendet Factory und kalibrierten Helper, und enthält weder lokale Workspace-/MetadataReference-/Temp-Builder noch Collection-Serialisierung oder Task-Kommentare.

### Logische Korrektheit

Die Assertions der migrierten Verträge bleiben erhalten, virtuelle Pfade bleiben reine Roslyn-Identitäten, und der neue Local-Function-Fall verlangt vier Cluster-Mitglieder sowie beide Signaturen `ComputeOne` und `ComputeTwo`, sodass das Entfernen des produktiven `LocalFunctionStatementSyntax`-Zweigs den Test rot macht.

### Konzept-Treue (Ebene 4)

Die Umsetzung bleibt beim vorgesehenen In-Memory-Component-Schnitt der reinen `Solution`-Engine und lässt die 19 weiterhin pending Tool-Dispatch-Verträge (`DuplicateDetectionTool.ExecuteAsync`) unverändert außerhalb des Scopes.

### Build-/Test-Status

```
dotnet build → grün (5 Projekte, 0 Warnungen/Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~DuplicateDetectionEngineTests|FullyQualifiedName~RefactoringDriftEngineTests|FullyQualifiedName~DuplicateDetectionScannerTests|FullyQualifiedName~RefactoringDriftScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests" → grün (53 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (5 Tests, 0 Fehler)
```
