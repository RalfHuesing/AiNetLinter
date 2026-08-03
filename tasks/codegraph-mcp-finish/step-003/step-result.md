---
status: done
type: step-result
task: codegraph-mcp-finish
step: 003
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: 8cae25c
status_after: done
blocker_category: n/a
---

# Result Step 003: Core/-Testordner sub-gliedern + MaxDirectoryChildren aktivieren (F.3)

## Zusammenfassung

23 Test-Dateien aus `src/AiNetLinter.Tests/Core/` (42 Dateien) in die bereits
bestehenden Unterordner `Core/Checkers/` (20 Dateien) und `Metrics/` (3
Dateien) verschoben, jeweils mit Namespace-Anpassung. 19 Engine-/
Infrastruktur-Tests bleiben unverändert in `Core/`. `MaxDirectoryChildren`
in `rules.json` von `0` auf `30` gesetzt, `AiNetLinter.mdc` per
`--sync-agent-rules-only` neu synchronisiert. Keine Testinhalte/Assertions
geändert — reines Datei-/Namespace-Refactoring.

## Geänderte Dateien

- 20 Dateien `src/AiNetLinter.Tests/Core/*.cs` → `Core/Checkers/*.cs` — Verschiebung, Namespace `AiNetLinter.Tests.Core` → `AiNetLinter.Tests.Core.Checkers`.
- 3 Dateien `src/AiNetLinter.Tests/Core/*.cs` → `Metrics/*.cs` (`AIContextFootprintDeduplicationTests.cs`, `FileLimitGuidanceTests.cs`, `PostAnalysisChecksPathOverrideTests.cs`) — Verschiebung, Namespace → `AiNetLinter.Tests.Metrics`.
- `rules.json` — `MaxDirectoryChildren`: `0` → `30`.
- `.agents/rules/AiNetLinter.mdc` — automatisch neu synchronisiert (Tabellenzeile `MaxDirectoryChildren`).

`Docs/configuration.md` geprüft, nicht geändert (siehe „Abweichungen vom
Plan").

## Commit

- **Code-Commit-Hash:** `8cae25c`
- **Message:**
  ```
  refactor(tests): Core/-Testordner sub-gliedern und MaxDirectoryChildren aktivieren [codegraph-mcp-finish]

  23 Test-Dateien aus dem 42-Datei-Flachordner Core/ in die bestehenden
  Unterordner Core/Checkers/ (20) und Metrics/ (3) verschoben, jeweils
  mit Namespace-Anpassung gem. EnforceNamespaceDirectoryMapping. 19
  Engine-/Infrastruktur-Tests bleiben in Core/. Anschliessend
  MaxDirectoryChildren in rules.json von 0 auf 30 aktiviert und
  AiNetLinter.mdc neu synchronisiert.

  Refs: tasks/codegraph-mcp-finish/step-003
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx             → grün, 0 Warnungen
dotnet test --filter Category=Unit        → grün (100 Tests, 0 Fehler)
dotnet test AiNetLinter.slnx --no-build   → grün (1186 Tests, 0 Fehler, ~1m41s)
Manueller Sweep (>30 Einträge, ohne bin/obj/.git) → keine Treffer
```

## Abweichungen vom Plan

- Keine bei der Datei-Klassifizierung: die im Plan als „ambig, beim
  Öffnen verifizieren" markierten Fälle (`CouplingSemanticTests`,
  `SwitchDispatcherDetectorTests`, `NullCoalescingInitializerClassifierTests`,
  `ControlFlowResilienceTests`, `StaticTestSentinelExemptionTests`) wurden
  geöffnet und geprüft: keine der fünf testet eine dedizierte
  1:1-Checker-Klasse (kein `class XyzChecker`/`XyzDetector`/`XyzClassifier`
  in `src/AiNetLinter` gefunden) — alle laufen über `LinterAnalyzer.Analyze`
  mit spezifischer Config und decken Engine-/Metrik-Verhalten ab. Ergebnis
  deckt sich 1:1 mit der Plan-Einordnung (die ersten beiden nach
  `Checkers/`, die letzten drei bleiben in `Core/`) — keine Umsortierung
  nötig.
- `Docs/configuration.md` (Zeile ~107, ~226): nicht geändert. Beide
  Stellen sind Teil der generischen Beispielkonfiguration/Default-Doku
  für Nutzer des Tools ("Standard: 0 = deaktiviert"), nicht eine
  Darstellung des aktuellen Live-Werts dieses Projekts — passend zum im
  Step-Plan vorgesehenen Fallback „unverändert lassen, falls generisches
  Beispiel".
- `MaxDirectoryChildrenExemptNames` unverändert gelassen — der
  Verzeichnis-Sweep nach der Umstellung zeigte keine neuen Verstöße
  außerhalb des bereits bekannten `src/AiNetLinter/Core/Checkers` (28,
  unter dem neuen Grenzwert 30, also ohnehin kein Verstoß).

## Beobachtungen

- Vor jeder Verschiebung wurde geprüft, ob eine der 23 bewegten
  Testklassen von einer anderen Testdatei referenziert wird (Grep über
  `src/AiNetLinter.Tests`) — keine Treffer, keine zusätzlichen
  `using`-Anpassungen nötig.
- `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
  (wie in der Tech-Stack-Notiz/Step-Plan angegeben) schlägt ohne
  `--path .` und `--config rules.json` fehl (`--path ist erforderlich`
  bzw. `CONFIG_REQUIRED`), da `SyncAgentRulesOnly` in
  `LinterArgs.HasStandaloneCommand()` (`src/AiNetLinter/Cli/LinterArgs.cs:223`)
  nicht als Standalone-Command gelistet ist. Kein Bugfix in diesem Step
  (außerhalb Scope) — nur als Hinweis für künftige Steps/Doku, die diesen
  Befehl referenzieren: der korrekte Aufruf lautet
  `dotnet run --project src/AiNetLinter -- --path . --config rules.json --sync-agent-rules-only`.

## Bekannte Unschärfen

- Keine über die oben dokumentierten Punkte hinaus.

## Falls Status `blocked`

Entfällt — Status `done (pending audit)`.
