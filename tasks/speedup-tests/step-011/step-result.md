---
status: done
type: step-result
task: speedup-tests
step: 011
epic: EPIC-3
step_type: single
coded_by: coder
coded_by_model: "gpt-5.6-sol High (Ersatz fuer nicht auswaehlbares gpt-5.6-luna High)"
coded_by_model_knowledge_cutoff: "nicht ausgewiesen"
coded_at: 2026-08-12T21:44:46.2491732+02:00
code_commit_hash: b720e1b
status_after: done
blocker_category: n/a
---

# Result Step 011: EPIC-3 Teil 2 — Web-Parser-Kohorte nach AiNetLinter.FastTests migrieren

## Zusammenfassung

Die fuenf Web-Parser-/Textanalyse-Testklassen wurden nach `AiNetLinter.FastTests` verschoben;
Testlogik, Assertions, Traits, Marker und Abhaengigkeiten blieben unveraendert. Das Ledger weist die
fuenf Klassen mit existierenden neuen Abdeckungsorten als `migrated` aus. Die Kohorte umfasst vor
und nach dem Move 74 erfolgreiche Testfaelle.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Web/*.cs` — fuenf Legacy-Testdateien mit neuem FastTests-Namespace.
- `tasks/speedup-tests/test-migration-ledger.md` — fuenf Web-Eintraege auf `migrated` mit Zielpfaden gesetzt.

## Commit

- **Code-Commit-Hash:** `b720e1b`
- **Message:**
  ```
  refactor(tests): migriere Web-Parser-Kohorte [speedup-tests]

  Refs: tasks/speedup-tests/step-011
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```text
dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~AiNetLinter.Tests.Web → gruen (74 Tests, 0 Fehler; Vorher-Basis)
dotnet build src/AiNetLinter.FastTests → gruen (0 Warnungen, 0 Fehler)
dotnet build src/AiNetLinter.Tests → gruen (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~AiNetLinter.FastTests.Web → gruen (74 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests → gruen (4 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~LegacyProjectBuildGateTests → gruen (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~FastTestsDependencyGuardTests → gruen (2 Tests, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~TestCategoryProfileGuardTests → gruen (1 Test, 0 Fehler)
```

## Abweichungen vom Plan

Der Orchestrierungscommit setzte `task-state.md` auf `in_progress`, liess den Status im Step-Plan
aber auf `open`; dieser wurde direkt auf `done (pending audit)` gesetzt. Zwei Razor-Quelldateien
hatten historisch kein abschliessendes Newline, und `RazorAnalyzerTests.Extended.cs` lag als
CRLF-Blob vor. Beim Move normalisierte Git diese rein formalen Dateiende-/Zeilenendenunterschiede;
`git show --ignore-space-at-eol --numstat b720e1b` weist fuer jede der fuenf Testdateien exakt eine
geanderte Inhaltszeile aus: den Namespace.

## Beobachtungen

Das angeforderte Modell GPT-5.6 Luna High war in der Agentenauswahl nicht verfuegbar; umgesetzt
wurde der Step mit GPT-5.6 Sol High.

## Bekannte Unschärfen

Keine.
