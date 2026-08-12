---
status: done
type: step-result
task: speedup-tests
step: 004
epic: EPIC-1
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-12
code_commit_hash: a303edb
status_after: done
blocker_category: n/a
---

# Result Step 004: Minimum Safety Envelope, Legacy-Build-Gate, InternalsVisibleTo und Gate-Switch

## Zusammenfassung

Alle sieben im Plan benannten Dateien umgesetzt: die drei fehlenden `InternalsVisibleTo`-Eintraege,
drei neue MSE-Tests (Solution-Analyse, CLI-Adapter-Exit-Code, MCP-Handshake/Toolregistrierung), der
Legacy-Build-Gate-Test und der eigentliche Gate-Switch in `AGENTS.md` plus die dazugehoerige
Konsistenzpflege in `AiNetLinterRichtlinien.mdc`. EPIC-1 ist damit inhaltlich abgeschlossen.

## Geänderte Dateien

- `src/AiNetLinter/Core/LinterEngine.cs` — zwei zusätzliche `InternalsVisibleTo`-Einträge für
  `AiNetLinter.FastTests`/`AiNetLinter.IntegrationTests`.
- `src/AiNetLinter.FastTests/Core/LinterEngineSolutionAnalysisTests.cs` (neu) — Component-Test:
  `LinterEngine.RunAsync(Solution)` gegen eine `AdhocWorkspace`-Zwei-Klassen-Solution, prüft
  Verletzungs- (`EnforceSealedClasses`) und regelkonformen Pfad in einem Test.
- `src/AiNetLinter.IntegrationTests/Cli/CliAdapterExitCodeTests.cs` (neu) — Integration-Test:
  `Program.Main(string[])` in-process gegen zwei Kopien von `tests/Fixtures/BaselineMini`.
- `src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs` (neu) —
  Integration-Test: echter `AiNetLinter.exe --mcp-server`-Subprozess gegen `BaselineMini`, eigener
  schlanker Handshake-Client.
- `src/AiNetLinter.IntegrationTests/Migration/LegacyProjectBuildGateTests.cs` (neu) —
  Integration-Test: Legacy-Build-Gate über `AiNetLinter.slnx`.
- `AGENTS.md` — Gate-Switch: normales Gate jetzt `dotnet test src/AiNetLinter.FastTests`/
  `src/AiNetLinter.IntegrationTests --filter Category!=Stress`; `AiNetLinter.Tests` als
  quarantäniert dokumentiert.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — TRX-Diagnoseregel verweist jetzt auf `AGENTS.md` als
  alleinige Quelle der Gate-Kommandos.
- `tasks/speedup-tests/codemap.md` — Einträge für die vier neuen Testdateien sowie Aktualisierung
  der bestehenden Einträge zu `LinterEngine.cs`, `AGENTS.md`, `AiNetLinterRichtlinien.mdc`.

## Commit

- **Code-Commit-Hash:** `a303edb`
- **Message:**
  ```
  feat(tests): Minimum Safety Envelope, Legacy-Build-Gate und Gate-Switch [speedup-tests]

  Ergaenzt InternalsVisibleTo fuer AiNetLinter.FastTests/AiNetLinter.IntegrationTests
  (LinterEngine.cs), damit die neuen Testziel-Assemblies den internen
  LinterEngine-Konstruktor direkt nutzen koennen. Rundet EPIC-1 mit der von
  konzept.md Leitplanke 8 geforderten Minimum Safety Envelope ab:
  LinterEngineSolutionAnalysisTests (Component, AdhocWorkspace-Solution, Analyse-
  Erfolgs- und Fehlerpfad), CliAdapterExitCodeTests (Integration, Program.Main
  in-process gegen eine kopierte BaselineMini-Fixture, Exit-Code-Kontrast
  sealed/unsealed) und McpHandshakeToolRegistrationTests (Integration, echter
  AiNetLinter.exe-Subprozess, JSON-RPC-Handshake + tools/list, eigener schlanker
  Client ohne TestKit-Extraktion).

  Ergaenzt das Legacy-Build-Gate (LegacyProjectBuildGateTests): prueft
  mechanisch, dass AiNetLinter.Tests solange Teil von AiNetLinter.slnx und auf
  der Platte vorhanden bleibt, wie das Migrationsledger noch pending-Zeilen hat.

  Schaltet das in AGENTS.md dokumentierte normale Gate von den solutionweiten
  Category-Filtern auf die drei neuen Zielprofile um (FastTests/
  IntegrationTests, je Category!=Stress als Abschluss-Gate); AiNetLinter.Tests
  bleibt baubar und Teil der Solution, laeuft aber nicht mehr standardmaessig
  mit (Quarantaene, gezielte Ausfuehrung nur ueber den engsten Ledger-Filter).
  Verweist die TRX-Diagnoseregel in AiNetLinterRichtlinien.mdc auf AGENTS.md als
  alleinige Quelle der aktuell gueltigen Gate-Kommandos.

  Refs: tasks/speedup-tests/step-004
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx (nach dotnet clean)                                          → grün, 0 Warnungen/Fehler, 5 Projekte
dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~LinterEngineSolutionAnalysisTests      → grün (1 Test)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~CliAdapterExitCodeTests          → grün (2 Tests)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~McpHandshakeToolRegistrationTests → grün (1 Test)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~LegacyProjectBuildGateTests      → grün (1 Test)
dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests → grün (4 Tests, Regression unauffällig)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress          → grün (10 Tests) — Epic-Grenze, neuer Standard-Gate-Pfad
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress   → grün (12 Tests) — Epic-Grenze, neuer Standard-Gate-Pfad
```

Kein solutionweiter Legacy-Lauf durchgeführt (bewusst laut Plan-Testliste — `AiNetLinter.Tests` ist
laut diesem Step nicht mehr Teil des Standardpfads, Zustand bereits durch step-002 dokumentiert).

## Abweichungen vom Plan

- **Datei 3 (CLI-Adapter-Test):** Der Plan nennt als Beispiel „`tests/Fixtures/BaselineMini` oder
  `CompileErrorMini`". Verifiziert vor der Implementierung, dass `AiNetLinter`s **Default**-Config
  (kein `--config`) sehr strikt ist (`EnforceSealedClasses`, `EnforceNamespaceDirectoryMapping`,
  `EnforceNullableEnable`, `EnforceNoSilentCatch` u. a. alle standardmäßig `true`) — ein garantiert
  „sauberer" Lauf ohne eigene Config wäre gegen jede vorhandene Mini-Fixture unsicher gewesen
  (unklar, wie viele Regeln zusätzlich zur eigentlich zu testenden Sealed-Regel greifen). Deshalb:
  `BaselineMini` **kopiert** (wie im Plan verlangt — Projektstruktur, `.slnx`, `.csproj`,
  `ViolatingClass.cs` unverändert übernommen), aber die kopierte `rules.json` durch eine minimale,
  vollständig kontrollierte Konfiguration ersetzt (nur `EnforceSealedClasses` aktiv, alle anderen
  Global-Flags explizit deaktiviert, Metrics großzügig) — Grün/Rot-Kontrast entsteht ausschließlich
  über sealed/unsealed `ViolatingClass.cs`, nicht über ungeprüfte Nebenwirkungen der
  Original-Fixture-Regeln. Vor dem Schreiben des finalen Tests mit `dotnet test` verifiziert (nicht
  nur angenommen), dass beide Fälle tatsächlich den erwarteten Exit-Code liefern.
- Sonst keine Abweichungen — alle sieben Dateien 1:1 wie im Plan beschrieben umgesetzt.

## Beobachtungen

- **Vor dem finalen Commit auf orphaned Prozesse geprüft** (Nutzer-Feedback
  `feedback-subagent-git-destructive.md`): nach dem MCP-Handshake-Testlauf lief ein
  `AiNetLinter.exe`-Prozess (PID 15128) — Command-Line-Prüfung per `wmic` zeigte, dass dieser aus
  einem völlig anderen Repository (`C:\Daten\Entwicklung\Ralf\SqlToAi`) stammt und nichts mit diesem
  Step zu tun hat. Kein Aufräumbedarf durch diesen Step.
- **`dotnet clean` erzeugte 2 MSB3061-Warnungen** (gesperrte `BuildHost-netcore`-DLLs unter
  `AiNetLinter.Tests/bin`), verursacht durch einen fremden, bereits laufenden `dotnet`-Prozess
  (nicht durch diesen Step gestartet). Der eigentliche `dotnet build`-Lauf danach war sauber (0
  Warnungen/0 Fehler) — die Clean-Warnungen sind kein Bestandteil des in der Definition-of-Done
  geforderten Build-Nachweises.
- Keine sonstigen Beobachtungen außerhalb des Scopes.

## Bekannte Unschärfen

- **`McpHandshakeToolRegistrationTests` prüft nur zwei repräsentative Tool-Namen**
  (`find_symbol`, `get_violations`), nicht die vollständige Werkzeugliste wie das Legacy-Pendant
  `McpServerCommandTests.RunAsync_ValidFixture_ServerRespondsWithEighteenTools` (exakte Anzahl 18).
  Bewusst so gewählt, damit der MSE-Test nicht bei jeder künftigen Tool-Ergänzung/-Umbenennung
  unnötig anschlägt — der Plan verlangt nur „dass die erwarteten Tools registriert sind", keine
  Vollständigkeitsprüfung. Der Kritiker sollte bewerten, ob das für den MSE-Zweck ausreicht.
- **`LegacyProjectBuildGateTests` hat einen impliziten Bypass, sobald `pending = 0` wird**
  (Test kehrt dann früh zurück, siehe Kommentar im Code) — das ist explizit im Plan so vorgesehen
  („solange `test-migration-ledger.md` noch pending-Zeilen enthält"), aber bedeutet, dass der Guard
  ab dem Moment, in dem die letzte Kohorte migriert ist, stillschweigend wirkungslos wird, bis ihn
  jemand bewusst entfernt oder anpasst — kein Fehler dieses Steps, aber relevant für EPIC-3+.
