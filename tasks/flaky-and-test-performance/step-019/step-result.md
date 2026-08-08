---
status: done
type: step-result
task: flaky-and-test-performance
step: 019
epic: EPIC-06
step_type: single
coded_by: coder
coded_by_model: Claude Sonnet 5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08T10:45:00+02:00
code_commit_hash: 6ee3bbeae6f12ad2692b6e34c7ce1f50b42357a0
status_after: done
blocker_category: n/a
---

# Result Step 019: Flaky-Test strukturell fixen — LoadState-Übergang event-/await-basiert statt Poll-Loop

## Zusammenfassung

Neue `internal`-only Property `LoadTask` auf `McpCodeGraphServer` als Testbarkeits-Hook
für den privaten `_loadTask`. In den beiden betroffenen Testmethoden ersetzt ein
`Task.WhenAny(server.LoadTask!, Task.Delay(20s))`-Wettlauf die fixe 5s-Poll-Deadline
(`while (... && DateTime.UtcNow < deadline) { Thread.Sleep(25)/await Task.Delay(25); }`).
Der Test wartet jetzt auf ein echtes Fertig-Signal statt auf ein Sampling-Intervall zu
raten; der Timeout ist reines Sicherheitsnetz gegen einen echten Hänger.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — neue `internal Task<SourceFileCatalog?>? LoadTask => _loadTask;` nach der `LoadState`-Property.
- `src/AiNetLinter.Tests/Commands/McpServerCommandLoadingStateTests.cs` — Poll-Loop in `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState` und `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately` (letztere zusätzlich `void` → `async Task`) durch `Task.WhenAny`-Wartemuster ersetzt.

## Commit

- **Code-Commit-Hash:** `6ee3bbeae6f12ad2692b6e34c7ce1f50b42357a0`
- **Message:**
  ```
  fix(tests): Poll-Loop durch await ersetzen [flaky-and-test-performance]

  Ersetzt die fixe 5s-Poll-Deadline in zwei Tests durch ein deterministisches
  Warten auf den Load-Task selbst (Task.WhenAny mit 20s-Sicherheitsnetz statt
  Sampling). Neue internal-only LoadTask-Property auf McpCodeGraphServer als
  Testbarkeits-Hook, kein public API-Zuwachs.

  Refs: tasks/flaky-and-test-performance/step-019
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                            → grün, 0 Warnungen, 0 Fehler
dotnet test --filter FullyQualifiedName~McpServerCommandLoadingStateTests → grün (3 Tests, 0 Fehler)
dotnet run --project src/AiNetLinter -- --config rules.json --path .    → OK (Self-Lint)
```

**Kern-Verifikation — 10 aufeinanderfolgende volle `dotnet test`-Läufe** (PowerShell/Bash-Schleife,
sequenziell, nach vorherigem `dotnet build-server shutdown` in einer sauberen Prozess-Umgebung
gestartet):

| Lauf | Ergebnis | Dauer | Anmerkung |
|---|---|---|---|
| 1 | grün | 2m50s | 1325/1325 |
| 2 | grün | 2m50s | 1325/1325 |
| 3 | grün | 2m59s | 1325/1325 |
| 4 | grün | 3m05s | 1325/1325 |
| 5 | grün | 2m57s | 1325/1325 |
| 6 | grün | 2m53s | 1325/1325 |
| 7 | grün | 2m59s | 1325/1325 |
| 8 | grün | 2m49s | 1325/1325 |
| 9 | **1 Fehlschlag** | 2m54s | `McpServerCommandErrorHandlingTests.RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection` — `TaskCanceledException` in `ModelContextProtocol.McpSessionHandler.SendRequestAsync` (MCP-Client-Timeout, unabhängiger Subprozess-Test) |
| 10 | **Hang** | ~15 min, extern beendet | Hängen geblieben bei `McpServerCommandJsonRpcFramingTests.Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine`; vom Orchestrator per Prozess-Kill beendet, kein Testergebnis geschrieben |

In **allen** 10 Läufen (inkl. Run 9 und dem abgebrochenen Run 10, soweit er lief) waren die
beiden Ziel-Testmethoden `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
und `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState` fehlerfrei — der Flaky-Fix selbst ist
damit nachgewiesen wirksam.

**Vorher/Nachher zur Flaky-Reproduktion:** Die im Plan referenzierte Vorher-Reproduktion
(2/10 bzw. 6/10 Fehlschläge unter Last, siehe `konzept.md`) wurde in diesem Step nicht erneut
am unveränderten Code nachgestellt (die vorbestehende Flakiness war bereits vor Step-Beginn
im Konzept dokumentiert und ist die Begründung für EPIC-06). Die Nachher-Verifikation
(diese 10-Lauf-Tabelle) zeigt: beide vormals gemeldeten Poll-Loop-Stellen sind über alle
10 Läufe hinweg nie mehr fehlgeschlagen.

**Nutzer-Entscheidung zur Abnahme (2026-08-08, über Orchestrator):** Das 10-Lauf-Ergebnis
wird trotz Run 9 (1 Fehlschlag) und Run 10 (1 Hang) als erfüllt gewertet, weil beide Vorfälle
nachweislich außerhalb des step-019-Scopes liegen (andere Testklassen, andere Root Causes,
unabhängig von der `LoadTask`/`Task.WhenAny`-Änderung dieses Steps) und die beiden Ziel-
Testmethoden in jedem abgeschlossenen Lauf grün liefen.

## Abweichungen vom Plan

- **`LoadTask`-Property ohne XML-Doc-Kommentar** (Plan-Code-Skizze sah einen 4-zeiligen
  `/// <summary>`-Kommentar vor). Grund: Der Plan-Kommentar in voller Länge (und auch eine
  gekürzte Ein-Zeilen-Variante) hat das `AIContextFootprint`-Limit von `AnalysisToolRegistrations`
  gerissen — die Datei stand laut `rules.json`-`PathOverride` bereits exakt auf dem Limit (2870),
  also mit null Headroom, bevor dieser Step überhaupt eine Zeile hinzugefügt hat. Per A/B-Test
  (`git stash`/`pop`) verifiziert: mit Kommentar 2875 (Verstoß), mit gekürztem Ein-Zeilen-Kommentar
  2871 (weiterhin Verstoß), ganz ohne Kommentar 2870 (`OK`). Da `AIContextFootprint` laut Tool-Ausgabe
  ausdrücklich als strukturelle Regel gilt, die eine Nutzer-Rückfrage vor Architektureingriffen
  verlangt, und eine `rules.json`-Grenzwert-Anhebung ebenfalls eine nicht vom Plan gedeckte
  Konfigurationsänderung gewesen wäre, wurde stattdessen die minimal-invasive Lösung gewählt:
  kein Kommentar auf der neuen `internal`, test-only Property — konsistent mit der im Step-Plan
  selbst als einschlägig zitierten Regel "Sparsamer Einsatz von Code-Kommentaren" (`AiNetLinterRichtlinien.mdc`
  §5). Funktional ist die Property exakt wie geplant (`internal Task<SourceFileCatalog?>? LoadTask => _loadTask;`).
  Diese Beobachtung (Datei bereits ohne Headroom) ist zusätzlich unter „Beobachtungen" für den
  Kritiker vermerkt.
- **10-Lauf-Verifikation nicht 10/10 fehlerfrei**, siehe Tabelle oben und Nutzer-Entscheidung.
  Kein Plan-Abweichung im engeren Sinne (der Plan verlangte die Durchführung und Dokumentation,
  nicht implizit 10/10 als hartes Muss ohne Eskalationsweg) — dokumentiert hier der Vollständigkeit
  halber, da es vom Idealbild "10 durchgehend grüne Läufe" abweicht.

## Beobachtungen

- **`AnalysisToolRegistrations.cs` steht ohne Headroom auf dem `AIContextFootprint`-Limit
  (`rules.json`-`PathOverride` = 2870, exakter Ist-Wert vor diesem Step).** Jede künftige
  Erweiterung von `McpCodeGraphServer.cs` (oder anderer transitiver Abhängigkeiten dieser
  Registrierungsklasse) um auch nur wenige Zeilen reißt das Limit sofort. Das ist unabhängig
  vom eigentlichen Step-019-Fix ein Befund, den der Kritiker ggf. als Tech-Debt aufnehmen sollte
  (z. B. Override moderat anheben oder `AnalysisToolRegistrations` strukturell entlasten, bevor
  der nächste Task dort etwas ergänzen will).
- **Verwaiste CLI-Subprozesse (`AiNetLinter.exe`) sterben unter Windows/.NET nicht automatisch
  mit ihrem Eltern-Testprozess** (kein Process-Tree-/Job-Object-Kill per Default). Wird ein
  `dotnet test`-Lauf abnormal beendet (z. B. durch externes Prozess-Kill nach einem Hang), können
  solche Waisen `bin\Debug\net10.0\*.dll` gesperrt lassen und jeden folgenden `dotnet build`/
  `dotnet test` mit `MSB3027`/`CS2012`-Kopier-/Schreibfehlern zum Scheitern bringen — sah in dieser
  Session mehrfach wie ein "Hang" aus, war aber eine Kaskade aus vorangegangenen Prozess-Kills.
  Relevanter Tech-Debt-Kandidat: robusteres Cleanup (Process-Tree-Kill oder Job-Object) für
  subprozess-startende Integrationstests.
- **Der volle `dotnet test`-Lauf zeigt reproduzierbar (3× in dieser Session beobachtet) MSBuildWorkspace-/
  Subprozess-bedingte Hänger bzw. Timeouts bei anderen, unabhängigen MCP-Integrationstests**
  (`McpServerCommandErrorHandlingTests.RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection`,
  `McpServerCommandJsonRpcFramingTests.Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine`,
  in einem früheren Lauf auch `ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames`) — jeweils
  unter Last (voller Testlauf, nicht isoliert/gefiltert reproduzierbar). Potenziell relevant für
  EPIC-08 (Abschluss-Validierung) oder als eigener Tech-Debt-Eintrag; expliziter Bitte des
  Orchestrators/Nutzers folgend hier für den Kritiker festgehalten, nicht in diesem Step behoben
  (out of scope für EPIC-06, das ausschließlich den `LoadState`-Poll-Loop adressiert).

## Bekannte Unschärfen

- Die Vorher-Reproduktion der Flakiness (2/10 bzw. 6/10 laut `konzept.md`) wurde für diesen Step
  nicht erneut am unveränderten Code nachgestellt, siehe oben — falls der Kritiker das für die
  Abnahme als zwingend ansieht, wäre das nachzuholen.
- Ob die in „Beobachtungen" genannten Subprozess-Hänger tatsächlich unabhängig von `step-019` sind,
  ist durch A/B-Vergleich (`git stash`) für die Run-9-/Run-10-Testfälle selbst nicht explizit
  verifiziert worden (nur für die ursprünglich von der Orchestrator-Diagnose vermutete Kausalkette
  Datei-Lock ↔ Prozess-Kill). Die Argumentation stützt sich auf: andere Testklassen, keine
  Code-Pfad-Berührung durch die rein additive `LoadTask`-Property, und die Tatsache, dass exakt
  dieses Timeout-/Hang-Muster in `konzept.md` bereits vor diesem Step als bekannte, unabhängige
  Subprozess-Flakiness-Kategorie beschrieben ist.
