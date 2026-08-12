---
status: done
type: step-result
task: speedup-tests
step: 012
epic: EPIC-3
step_type: single
coded_by: coder
coded_by_model: "gpt-5.6-sol High (Ersatz fuer nicht auswaehlbares gpt-5.6-luna High)"
coded_by_model_knowledge_cutoff: "nicht ausgewiesen"
coded_at: 2026-08-12T22:02:05.6646251+02:00
code_commit_hash: eb645b8
status_after: done
blocker_category: n/a
---

# Result Step 012: EPIC-3 Teil 3 — Renderer-Kohorte nach AiNetLinter.FastTests migrieren und Unit-Profil verifizieren

## Zusammenfassung

Die zwei Renderer-Testklassen wurden nach `AiNetLinter.FastTests` verschoben und um je einen
rekursiven Top-N-pro-Ebene-Vertragsfall ergaenzt. Alle acht Bestandsfaelle blieben erhalten; die
Zielkohorte umfasst zehn erfolgreiche Tests. Ledger und CodeMap weisen die realen Zielpfade aus.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Mcp/Tools/CallTreeMermaidRendererTests.cs` — migrierte vier Bestandsfaelle plus rekursiver Top-N-/Overflow-Kantenvertrag.
- `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTreeRendererTests.cs` — migrierte vier Bestandsfaelle plus rekursiver Sortier-, Top-N- und Einrueckungsvertrag.
- `src/AiNetLinter.Tests/Mcp/Tools/*RendererTests.cs` — beide Legacy-Testdateien physisch entfernt.
- `tasks/speedup-tests/test-migration-ledger.md` — beide Renderer-Eintraege auf `migrated` mit existierenden Zielpfaden gesetzt.
- `tasks/speedup-tests/codemap.md` — FastTests-Zielpointer ergaenzt und Legacy-Planungspointer als obsolet markiert.

## Commit

- **Code-Commit-Hash:** `eb645b8`
- **Message:**
  ```
  refactor(tests): migriere Renderer-Kohorte [speedup-tests]

  Refs: tasks/speedup-tests/step-012
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```text
dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~CallTreeMermaidRendererTests|FullyQualifiedName~MetricsTreeRendererTests" → gruen (8 Tests, 0 Fehler; Vorher-Basis)
dotnet build src/AiNetLinter.FastTests → gruen (0 Warnungen, 0 Fehler)
dotnet build src/AiNetLinter.Tests → gruen (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~CallTreeMermaidRendererTests|FullyQualifiedName~MetricsTreeRendererTests" → gruen (10 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests → gruen (4 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~LegacyProjectBuildGateTests → gruen (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter Category=Unit → gruen (326 Tests, 0 Fehler; EPIC-3-Grenzgate)
```

## Abweichungen vom Plan

Der Orchestrierungscommit setzte `task-state.md` auf `in_progress`, liess den Status im Step-Plan
aber auf `open`; dieser wurde direkt auf `done (pending audit)` gesetzt. `last_updated` im Ledger
stand bereits auf dem aktuellen Datum 2026-08-12 und blieb deshalb textuell unveraendert.

## Beobachtungen

Das angeforderte Modell GPT-5.6 Luna High war in der Agentenauswahl nicht verfuegbar; umgesetzt
wurde der Step mit GPT-5.6 Sol High. Die neuen Tests bestaetigten den gelesenen Produktvertrag;
Produkt- und Projektdateien mussten nicht geaendert werden.

## Bekannte Unschärfen

Keine.
