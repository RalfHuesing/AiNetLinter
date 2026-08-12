---
status: done
type: step-review
task: speedup-tests
step: 013
epic: EPIC-4
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra Medium
reviewed_by_model_knowledge_cutoff: nicht ausgewiesen
reviewed_at: 2026-08-12
verdict: issues
tech_debt_ids: []
---

# Review Step 013: EPIC-4 Teil 1 — Skeleton-Filterkohorte auf vorbereitete FilterMini-Solution migrieren

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich (`corrects: step-013`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: die kuratierten `<rules_dir>`-Dateien eingehalten
- [x] Logische Korrektheit: Code und Tests gegen die neuen Verträge geprüft
- [x] Konzept-Treue: Scope und Muss-Haben gegen `konzept.md` geprüft
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Die Pfad-/Solution-Seam, die deterministischen BCL-Referenzen, der negative Testprojekt-Erkennungsvertrag, das gemeinsame `RecordingLintConsole`-Double mit gezieltem IVT, die zwei Adaptertests, physische Legacy-Bereinigung sowie Ledger, CodeMap und die Schließung von TD-002/TD-005 sind umgesetzt. Die 18 Methoden sind zwar vollständig migriert, aber der Namespace-Glob-Fall hat seine negative Filterwirkung verloren (Finding 1); damit ist das explizite DoD „ohne Abschwächung“ nicht erreicht.

### Rules-Konformität

Die in den Rules-Refs kuratierten Stil- und Architekturvorgaben sind eingehalten: neue C#-Dateien verwenden `#nullable enable`, die konkreten Klassen sind `sealed`, der Solution-Kern verwendet den schmalen Request-Record und die Component-Tests teilen die immutable Assembly-Fixture ohne Collection-Serialisierung.

### Logische Korrektheit

`BuildAsync(string, ...)` lädt genau einmal und delegiert zum selben `Solution`-Kern; der Fallback für einen relativen Anzeigenpfad ist wirksam, wie der Component-Lauf mit `FilterMini.slnx` zeigt. Der BCL-Satz ist deterministisch und testframework-frei, während `AdditionalReferences` weiter projektspezifisch ergänzt werden; die Factory- und Fidelity-Verträge decken dabei die zuvor fehlende negative Erkennung von `FilterMini` ab. Der Namespace-Glob-Test ist jedoch tautologisch, weil sein Muster alle drei vorhandenen Fixture-Namespaces trifft; ein Defekt, der das Namespace-Filtering vollständig ignoriert, bliebe in diesem Fall grün.

### Konzept-Treue (Ebene 4)

Die Zielarchitektur entspricht der Testpyramide und der Trennung von In-Memory-Component-Tests und echten MSBuild-Adaptertests aus `konzept.md`; die geschwächte Filtermatrix widerspricht jedoch der zugesagten lückenlosen Übernahme nicht-trivialer Verträge und dem konzeptuellen Coverage-Audit.

### Build-/Test-Status

```
dotnet build --no-restore → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~SkeletonMapFilterTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests" → grün (26 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~SkeletonMapBuilderAdapterTests|FullyQualifiedName~FilterMiniFidelityTests|FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests" → grün (8 Tests, 0 Fehler)
```

## Findings

1. `src/AiNetLinter.FastTests/Maps/Skeleton/SkeletonMapFilterTests.cs:126-138` — [MAJOR] [Plan-Erfüllung / Logik / Konzept-Treue] `IncludeNamespaces = ["FilterMini.*"]` matcht jede Namespace-Ausgabe der einzigen Fixture (`FilterMini.Core`, `FilterMini.Utils`, `FilterMini.Tests.Core`), und die Assertions verlangen genau diese vollständige Menge. Der migrierte Vertrag testet damit nicht mehr, dass ein Namespace-Glob andere Namespaces ausschließt; ein ignorierter Include-Namespace-Filter wäre grün. **Fix:** Den Fall mit einem selektiven Glob kalibrieren (etwa `FilterMini.Tests.*`) und sowohl den erwarteten Treffer als auch die ausgeschlossenen Produktions-Namespaces assertieren; alternativ die Fixture minimal um einen passenden Unternamespace erweitern, wenn ausdrücklich ein Subnamespace-Glob-Vertrag benötigt wird. Die Fallzahl 18 beibehalten.
