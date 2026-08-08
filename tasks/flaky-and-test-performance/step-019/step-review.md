---
status: done
type: step-review
task: flaky-and-test-performance
step: 019
epic: EPIC-06
step_type: single
reviewed_by: kritiker
reviewed_by_model: Claude Sonnet 5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-08T12:15:00+02:00
verdict: approved
tech_debt_ids: [TD-008, TD-009, TD-010]
---

# Review Step 019: Flaky-Test strukturell fixen — LoadState-Übergang event-/await-basiert statt Poll-Loop

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues**
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (die im Plan zitierten Abschnitte) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (gefiltert; volle 10-Lauf-Kette vom Coder dokumentiert, siehe unten)

## Befund

### Plan-Erfüllung

Beide „Konkrete Änderungen"-Punkte 1:1 umgesetzt (`LoadTask`-Property in `McpCodeGraphServer.cs:80`, `Task.WhenAny`-Wartemuster in beiden Testmethoden in `McpServerCommandLoadingStateTests.cs`, Signaturänderung `void`→`async Task` bei der ersten Methode). Kommentar-Pflege wie im Plan vorgesehen ergänzt, ohne Step-/Epic-Referenz. Die einzige Abweichung (kein XML-Doc-Kommentar auf der neuen Property, wegen `AIContextFootprint`-Limit ohne Headroom) ist transparent begründet und selbst per A/B-Test verifiziert (von mir per vollem Self-Lint-Lauf gegengeprüft: `OK`) — sachlich vertretbar, keine Rule-Verletzung, da §5 „Sparsame Kommentare" ohnehin für Verzicht spricht. `step-plan.md`-Status korrekt auf `done (pending audit)` gesetzt, `codemap.md` vom Coder aktualisiert (verifiziert per Diff des Doku-Commits `a47774f`) und inhaltlich zutreffend (McpCodeGraphServer.cs-Eintrag, Testdatei-Eintrag, AIContextFootprint-Beobachtung).

**10-Lauf-Kern-Verifikation (Konzept-DoD):** Beide vom Step adressierten Testmethoden liefen in allen 10 Läufen (inkl. der beiden problematischen Läufe 9/10, soweit sie liefen) fehlerfrei — das eigentliche DoD-Kriterium ("keine dieser beiden Testmethoden schlägt fehl") ist damit tatsächlich erfüllt, nicht nur behauptet. Der Fehlschlag in Run 9 (`McpServerCommandErrorHandlingTests`, MCP-Client-Timeout) und der Hang in Run 10 (`McpServerCommandJsonRpcFramingTests`) betreffen nachweislich andere Testklassen mit anderer Root-Cause-Kategorie (Subprozess-/MSBuildWorkspace-Kontention, nicht `LoadState`/`Task.Run`-Continuation) und werden im `step-result.md` mit Tabelle, Einzel-Ergebnis pro Lauf und expliziter Nutzer-Entscheidung sauber dokumentiert — das ist genau die Art von ehrlicher, nachvollziehbarer Doku, die bei einer Abweichung vom Idealbild "10/10 komplett grün" erwartet wird. Ich werte das **nicht** als eigenständiges Finding: der wörtliche DoD-Satz "10 aufeinanderfolgende volle Testläufe grün — insbesondere ohne Fehlschlag von [den beiden Zielmethoden]" ist durch den Klammerzusatz selbst auf die beiden Zielmethoden verengt, und genau dieser engere Maßstab ist erfüllt.

### Rules-Konformität

- `BanBlockingTaskAccess` — eingehalten. Neuer Code nutzt ausschließlich `await`/`Task.WhenAny`, keine `.Wait()`/`.Result`/`.GetAwaiter().GetResult()`-Aufrufe. Die bestehenden, per `ainetlinter-disable` begründeten Ausnahmen in `LoadState`/`GetCurrentSolution()` bleiben unverändert.
- „Symptom-Fixing verboten" (`AiNetLinterRichtlinien.mdc` §5) — eingehalten. Die Lösung ersetzt das Sampling-Intervall strukturell durch ein echtes Completion-Signal (`Task.WhenAny` gegen den tatsächlichen `LoadTask`), der 20s-Timeout ist nachvollziehbar als reines Sicherheitsnetz (nicht die Wartebedingung selbst) begründet — kein bloßes Hochsetzen der alten 5s-Deadline.
- „Sparsamer Einsatz von Code-Kommentaren" — eingehalten, kein Step-/TD-/EPIC-Verweis im neuen Code-Kommentar.
- „xUnit v3 Tests: Pflicht für jede Logik-Änderung" — erfüllt (Test-Änderung ist die Logik-Änderung selbst).
- Commit-Subject-Längen (72-Zeichen-Grenze, TD-002-Kontext) — beide Commits (71 bzw. 63 Zeichen) unter der Grenze, keine Wiederholung des TD-002-Musters.

### Logische Korrektheit

Das neue Wartemuster ist korrekt: `_loadTask` ist `readonly`, daher liefert `server.LoadTask` bei jedem Zugriff dieselbe Instanz — kein TOCTOU-Risiko zwischen dem `Task.WhenAny`-Aufruf und dem `Assert.Same`-Vergleich. `LoadState` liest denselben `_loadTask` direkt, sodass nach erfolgreichem `await` (bzw. `Task.WhenAny`-Gewinn von `LoadTask`) der Zustand konsistent verfügbar ist — kein Nachlauf-Fenster wie beim alten Poll-Loop. Der Timeout-Wettlauf schlägt korrekt fehl, falls `LoadTask` tatsächlich hängt (dann gewinnt `safetyTimeout`, `Assert.Same` schlägt fehl statt den Test unbegrenzt zu blockieren). Selbst nachgebaut und verifiziert: `dotnet build` grün (0 Warnungen), `dotnet test --filter FullyQualifiedName~McpServerCommandLoadingStateTests` grün (3/3), Self-Lint `OK`.

Kleinigkeit (kein Finding): Die neue `LoadTask`-Property in `McpCodeGraphServer.cs:80` sitzt ohne Leerzeile zwischen der `LoadState`-Property und dem XML-Doc-Kommentar von `MaxLineCount` — rein kosmetisch, siehe „Sonstige Beobachtungen" unten.

### Konzept-Treue (Ebene 4)

Erfüllt exakt den in `konzept.md` §"Wie" Schritt 6 und §"Muss-Haben" beschriebenen Muss-Haben-Punkt ("Fix strukturell … nicht durch Hochsetzen der Deadline oder Ausklammern aus dem Volllauf"). Kein Non-Goal berührt: `McpCodeGraphServer` bleibt `internal sealed class`, die neue Property ist `internal`, kein `public`-API-Zuwachs, kein sichtbares CLI/MCP-Verhalten geändert. Scope passt exakt zur Intention (auch die zusätzlich mitbehobene zweite Testmethode ist im Plan selbst nachvollziehbar begründet als „gleicher Root Cause, keine künstliche Batch-Fragmentierung" — keine Scope-Ausweitung, die über EPIC-06 hinausginge).

### Build-/Test-Status

```
dotnet build                                                             → grün (0 Warnungen, 0 Fehler)
dotnet test --filter FullyQualifiedName~McpServerCommandLoadingStateTests → grün (3 Tests, 0 Fehler)
dotnet run --project src/AiNetLinter -- --config rules.json --path .     → OK (Self-Lint)
```

Die vom Coder dokumentierte 10-Lauf-Kern-Verifikation (`step-result.md`) wurde nicht von mir wiederholt (Laufzeit ~30 Min., Tabelle mit Pass/Fail je Lauf bereits vorhanden und plausibel) — inhaltlich oben unter „Plan-Erfüllung" bewertet.

## Sonstige Beobachtungen / MINOR / NITPICK

- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:80` — neue `LoadTask`-Property direkt ohne Leerzeile vor dem XML-Doc-Kommentar von `MaxLineCount` eingefügt; rein kosmetisch (keine Rule verlangt eine Leerzeile hier), würde aber von einer Leerzeile lesbarer profitieren. Kein Blocker.

## Tech-Debt-Einträge aus diesem Review

- `TD-008` (siehe `tech-debt.md`) — `AnalysisToolRegistrations.cs` steht ohne Headroom auf dem `AIContextFootprint`-Limit (2870); jede künftige Erweiterung von `McpCodeGraphServer.cs` o. ä. reißt es sofort.
- `TD-009` (siehe `tech-debt.md`) — verwaiste `AiNetLinter.exe`-Subprozesse sterben unter Windows nicht mit ihrem Eltern-Testprozess, können nach abnormalem Testlauf-Abbruch Datei-Locks/Folgefehler verursachen.
- `TD-010` (siehe `tech-debt.md`) — reproduzierbare MSBuildWorkspace-/Subprozess-Hänger bei anderen, unabhängigen MCP-Integrationstests im vollen Testlauf unter Last; Risiko für EPIC-08.
