---
status: done
type: step-review
task: speedup-tests
step: 022
epic: EPIC-5
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5.6-terra
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-13
verdict: approved
tech_debt_ids: []
---

# Review Step 022: Korrektur: globales MSBuild-Loadgate und read-only Server-Ownership

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Korrektur-Step erforderlich
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung
- [x] Rules-Konformität
- [x] Logische Korrektheit
- [x] Konzept-Treue
- [x] Build/Test-Evidenz und enge Auditläufe

## Befund

### Plan-Erfüllung

`LoadedFixture` ist der einzige direkte `SourceFileCatalog.LoadAsync`-Callsite der Integration-Assembly; die beiden früheren Umgehungen verwenden den zentralen Max-2-Pfad, und der Korrekturscope bleibt auf Step-021 beschränkt.

### Rules-Konformität

Der Gate-Kern nutzt ein einzelnes `WaitAsync`/`finally`/`Release`, die Tests bleiben parallel ohne Collection-Serialisierung und die Korrektur verändert keinen Produktcode.

### Logische Korrektheit

Die vier deterministischen Gate-Nachweise belegen Maximum zwei sowie Permit-Freigabe nach Exception und Cancellation; alle drei gemeinsamen Toolkonsumenten erzeugen nichtbesitzende Snapshot-Server, deren Dispose den Fixture-Katalog nicht erreicht.

### Konzept-Treue (Ebene 4)

Die Korrektur erfüllt die beiden Step-021-Findings ohne die ausgeschlossenen Kohorten zu ziehen; TD-004 und TD-009 bleiben mit den neuen Nachweisen geschlossen, TD-010 unverändert offen.

### Build-/Test-Status

```text
Dokumentierter Build → grün (0 Warnungen, 0 Fehler)
Dokumentierter 11er Plattform-/Loadfilter → grün (11 Tests, 0 Fehler)
Audit: dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~LoadedFixtureTests" → grün (4 Tests, 0 Fehler)
Audit: dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~SymbolGraphCatalogFixtureTests|FullyQualifiedName~GetServerHealthToolTests|FullyQualifiedName~GetIndexScopeToolTests|FullyQualifiedName~SearchPatternToolTests" → grün (25 Tests, 0 Fehler)
Dokumentierter 6er Migrations-/Kategorieguard → grün (6 Tests, 0 Fehler)
Statische Callsite-/Owner-/TD-004-Checks und git diff --check → grün
```

Der eigene breitere 11er-Auditversuch wurde nach 64 Sekunden ohne Testergebnis durch die lokale Command-Grenze beendet; der isolierte neue Gatevertrag ist grün und die dokumentierte 11er-Ausführung bleibt die maßgebliche Gate-Evidenz.
