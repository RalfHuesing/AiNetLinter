---
status: done
type: step-result
task: speedup-tests
step: 024
epic: EPIC-5
step_type: batch
coded_by: coder
coded_by_model: gpt-5.6
coded_by_model_knowledge_cutoff: nicht ausgewiesen
coded_at: 2026-08-13T22:20:27+02:00
code_commit_hash: 30918b9
status_after: done
blocker_category: n/a
---

# Result Step 024: Korrektur: deterministische EPIC-5-Grenzprofile

## Zusammenfassung

Der Runtime-Guard der FastTests lebt jetzt als Assembly-Fixture und der MSBuild-Adapter ist hinter einem MSBuild-freien Catalog-Kern getrennt. Das Integration-Loadbudget verwendet einen instanzbasierten Gate-Kern; seine Verträge sind vom produktiven Gate getrennt und räumen alle gestarteten Tasks auf. Die gemeinsame Root-Auflösung schließt TD-011, während TD-008 unverändert offen bleibt.

## Geänderte Dateien

- item-01: `src/AiNetLinter/Baseline/SourceFileCatalog.cs`, `SourceFileCatalogLoader.cs` — Catalog-Kern von MSBuild-Typen getrennt und Loader hinter `LoadAsync` verschoben.
- item-01: `src/AiNetLinter.FastTests/Architecture/FastTestsRuntimeDependencyGuardFixture.cs`, `FastTestsDependencyGuardTests.cs`, `Platform/PreparedSolutionAssemblyFixture.cs`, `Baseline/SourceFileCatalogPolicyTests.cs` — assembly-weiter Runtime-Guard und enger Policy-Ladenachweis.
- item-02: `src/AiNetLinter.IntegrationTests/Platform/LoadedFixture.cs`, `LoadedFixtureTests.cs` — instanzbasierter Gate-Kern mit vollständigem Task-Cleanup und unverändertem produktivem Max-2-Pfad.
- item-02: `src/AiNetLinter.IntegrationTests/Configuration/ProjectOverrideRealSolutionTests.cs` — drei Zielprojekte nach einem einzigen echten Solution-Load geprüft.
- item-03: `src/AiNetLinter.IntegrationTests/Platform/SolutionRootLocator.cs` — gemeinsame Root-Auflösung für Fixture und Callsite-Guard.
- item-03: `tasks/speedup-tests/tech-debt.md` — TD-011 als geschlossen fortgeschrieben; TD-008 bleibt offen.

## Commit

- **Code-Commit-Hash:** `30918b9`
- **Message:**
  ```
  fix(tests): stabilisiere EPIC-5-Grenzprofile [speedup-tests]

  Refs: tasks/speedup-tests/step-024
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit.

## Build-/Test-Output

```
dotnet build → grün (0 Warnungen, 0 Fehler, 2 s)
Fast Guard-/Policy-Selbsttests → grün (9 Tests, 0 Fehler, 769 ms)
LoadedFixture-Selbsttests → grün (5 Tests, 0 Fehler, 176 ms)
MSBuild-Fixture-Selbsttests → grün (4 Tests, 0 Fehler, 983 ms)
ProjectOverrideRealSolutionTests → grün (1 Test, 0 Fehler, 3 s)
McpHandshakeToolRegistrationTests → grün (1 Test, 0 Fehler, 5 s)
Category=Unit|Category=Component → grün (778 Tests, 0 Fehler, 5 s)
Integration-Reihenfolgeausschnitt → grün (51 Tests, 0 Fehler, 30 s; keine neuen relevanten Prozesse)
Category=Integration → grün (155 Tests, 0 Fehler, 38 s; keine neuen relevanten Prozesse)
Kategorie-/Ledger-/Legacy-Guards → grün (6 Tests, 0 Fehler, 438 ms)
git diff --check → grün
```

## Abweichungen vom Plan

Keine fachliche Abweichung. Der erste Build wurde von einem bereits in der Vorher-Baseline vorhandenen `AiNetLinter.IntegrationTests.exe` (PID 20520) gesperrt; nach erneuter PID-/Commandline-Prüfung wurde nur dieser ausdrücklich autorisierte Prozess ohne direkte Kinder beendet. Der nachfolgende Build und beide finalen Profile endeten ohne manuellen Abbruch.

## Beobachtungen

Die Runner-Ausgabe markiert mehrere reale Integrationsverträge wegen des konfigurierten Drei-Sekunden-Schwellenwerts als langlaufend; beide erforderlichen Profile beendeten dennoch vollständig im vorgesehenen Zeitbudget. Fixversuche: 0 von 6, weil weder der erwartete Baseline-Nachweis noch der Fremdprozess-Lock einen Code-Fix erforderte.

## Bekannte Unschärfen

Keine. Die PID-Nachkontrollen lassen die bereits vor den Läufen vorhandenen fremden MCP-/BuildHost-Prozesse unangetastet und zeigen nach den eigenen finalen Läufen keine neuen relevanten Prozesse.
