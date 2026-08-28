---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 006
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T18:10:52+02:00
code_commit_hash: c9d71c35f1542676241400b99aca4148e562b91a
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 006: Mapping-Diagnosevertrag und direkte JSON-Regressionen korrigieren

## Befunde

Die drei Review-Befunde aus Step 005 wurden reproduziert und behoben: Die
Diagnosefabrik war in drei Konfigurationsklassen dupliziert, die Property-
Duplikatprüfung lief doppelt, und ein doppeltes vorhandenes Pflichtfeld konnte
zusätzlich als fehlend erscheinen. Direkte Loader-Regressionen für doppelte
Properties, fehlende `repositories`, leere/Whitespace-Assembly-Namen und
defektes `appsettings.json` sind ergänzt.

## Änderungen

- `ExternalSourceConfigurationDiagnostic.CreateError` ist die gemeinsame
  interne Fehlerfabrik für Code, Nachricht, Severity und Fundstelle.
- `ExternalSourceJsonValidation` führt je JSON-Objekt einen Property-Scan mit
  `Missing`, `Unique` und `Duplicate` durch und erzeugt pro doppeltem Namen
  genau eine `DuplicateField`-Diagnose.
- Loader und Validator konsumieren den zentralen Status; `RequiredFieldMissing`
  entsteht nur noch beim Status `Missing`. Pfadauflösung, Mapping-Schema,
  Assembly-Normalisierung und Provider-/Session-Grenzen blieben unverändert.
- `ExternalSourceConfigurationLoaderTests` deckt die vier direkten
  Regressionseingaben deterministisch über `TestTempDirectory` ab.

## Geänderte Dateien

- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
- `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs`
- `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs`
- `src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs`
- `tasks/decompiled-assembly-analysis/step-006/step-plan.md` — Status folgt im
  separaten Doku-Commit.

## Commit

- **Code-Commit:** `c9d71c35f1542676241400b99aca4148e562b91a`
- **Message:** `fix: Mappingdiagnosen zentralisieren [decompiled-assembly-analysis]`
- **Branch:** `main`
- **Push:** nein
- **Doku-Commit:** separater zweiter Commit nach diesem Result und der
  Statusaktualisierung.

## Tests

- `dotnet build --no-restore` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceConfigurationLoaderTests" --no-restore` — grün, 19/19 Tests.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore` — grün, 1.890/1.890 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore` — grün, 360/360 Tests.
- Stress-Tests wurden nicht ausgeführt.
- AiNetLinter-MCP `get_violations` im Konfigurations-Scope — 0 Verstöße.

## Abweichungen vom Plan

Die vorhandene Query-/Diagnoselogik wurde zu einem technischen, unveränderlichen
Objektvalidierungsergebnis mit Property-Status konsolidiert, damit der geforderte
Einzelscan tatsächlich eingehalten wird. Das ist eine interne Umsetzung des
geplanten JSON-Helpers und ändert keinen External-Source-Fachvertrag. Die im
Auftrag ausdrücklich ausgeschlossene `codemap.md` sowie `task-state.md`,
`roadmap.md`, Step-005-Dateien und `tech-debt.md` wurden nicht geändert.

## Beobachtungen

Die Diagnose-Fundstellen und bestehenden Fehlercodes bleiben stabil. Doppelte
Properties werden ordinal und unabhängig von ihrer Position erkannt; ein
mehrfach wiederholter Name erzeugt weiterhin nur eine Duplicate-Diagnose. Die
Korrektur enthält keine Assembly-Ausführung, Reflection-, Netzwerk-, Snapshot-,
Session-, MCP- oder Provider-Änderung.

## Bekannte Unschärfen

Der Step-Plan verweist auf ein separates Template unter
`tasks/decompiled-assembly-analysis/templates/step-result.md`; diese Datei ist
im Repository nicht vorhanden. Das Result orientiert sich deshalb am
vorhandenen Step-005-Resultformat. Der Doku-Commit-Hash kann erst nach dem
Commit von Result und Status genannt werden.
