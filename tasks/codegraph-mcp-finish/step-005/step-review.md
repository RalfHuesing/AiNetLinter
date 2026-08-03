---
status: done
type: step-review
task: codegraph-mcp-finish
step: 005
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-03
verdict: approved
tech_debt_ids: [TD-004]
---

# Review Step 005: Test-Data-Builder/Object-Mother konsolidieren — Rest-Cluster (F.4, Teil 2/2) + `#nullable enable` Randmitnahme (F.5)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle 19 gelisteten Dateien auf `TestHelper.CreateDefaultConfig() with {...}` umgestellt (per Grep verifiziert: keine `new Config {`-Treffer mehr außerhalb `TestHelper.cs`), alle 19 beginnen mit `#nullable enable` als erster Zeile, die zwei Sonderfälle (`MaxInheritanceDepthTests.cs`, `NamespaceDirectoryMappingTests.cs`) wie im Plan beschrieben behandelt, `roadmap.md` entsprechend aktualisiert.

### Rules-Konformität

`EnforceNullableEnable` (`AiNetLinter.mdc` Zeile 12/70) erfüllt; `AIContextFootprint`/`MaxLineCount` unauffällig (Diff macht Dateien knapper, nicht größer); keine Task-Referenzen (`step-005`/`F.4`) im Produktions- oder Testcode; Commit-Message konform (Conventional Commit, Suffix `[codegraph-mcp-finish]`).

### Logische Korrektheit

Rein syntaktische Transformation, keine Verhaltensänderung: `with`-Member wurden dort korrekt weggelassen, wo `Global`/`Metrics` bereits reiner Default war (`WpfCodeBehindTests.cs`, `NamespaceDirectoryMappingTests.cs`, `NestedTypesCheckerTests.cs`), sonst 1:1 übernommen. Volllauf bestätigt identische Testanzahl.

### Konzept-Treue (Ebene 4)

Deckt sich mit `Konzept.md` Muss-Haben F.4 (Test-Data-Builder/Object-Mother) und F.5 (Pragma nur als Randmitnahme, keine Flächenaktion — genau die 11 ohnehin angefassten Dateien wurden ergänzt, keine weiteren). Non-Goals „Keine Änderung an Testinhalten/Assertions" eingehalten.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx           → grün, 0 Warnungen
dotnet test AiNetLinter.slnx --no-build → grün (1186 Tests, 0 Fehler)
```

## Tech-Debt-Einträge aus diesem Review

- `TD-004` (siehe `tech-debt.md`) — Namenskollision `CreateDefaultConfig()` jetzt gebündelt für alle 6 betroffenen Testdateien (4 aus step-004 + 2 aus step-005), Priorität niedrig.
