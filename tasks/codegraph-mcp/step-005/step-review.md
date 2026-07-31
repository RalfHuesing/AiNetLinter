---
status: done
type: step-review
task: codegraph-mcp
step: 005
epic: EPIC-03
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T15:10:00Z
verdict: issues
tech_debt_ids: []
---

# Review Step 005: get_impact Tool (Git-Diff- und Symbol-Impact ueber DiffImpactAnalyzer.AnalyzeAsync)

## Verdict

- [ ] **approved**
- [x] **issues** — Fix-Step `step-005/fix-01` anzulegen
- [ ] **blocked**

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` (referenzierte Dateien) eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [ ] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben) — **nein, siehe Finding 1**
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

### Plan-Erfüllung

Alle neun im Plan genannten Dateien wie beschrieben umgesetzt: `GetImpactTool.cs` (Dispatch, zwei
Zweige, `INVALID_ARGUMENT`-Validierung), `LinterErrorCodes.InvalidArgument`,
`McpToolResults.InvalidArgument`, dritter `tools.Add(...)`-Eintrag in
`McpServerOptionsFactory` mit korrekter Description (C#-only-Scope, Exklusivität benannt),
`GitImpactMini`-Fixture + `GitImpactMiniFixtureWorkspace` (echtes lokales Git-Repo,
`Directory.Delete`-Workaround für read-only `.git`-Objekte unter Windows dokumentiert),
sechs `GetImpactToolTests`, `McpServerCommandTests` auf drei Tools angepasst. Alle sechs
Plan-Testfälle vorhanden und grün. Der neu vorgeschriebene DoD-Punkt „Dogfooding" wurde
ausgeführt und in einem eigenen Abschnitt in `step-result.md` dokumentiert — inhaltlich korrekt
im Sinne des Plans (zwei Aufrufe wie gefordert, plus zusätzliche Eingrenzungsversuche
transparent als „außerhalb der geforderten zwei Aufrufe" markiert).

### Rules-Konformität

`GetImpactTool.ExecuteAsync` hat wie im Plan vorgesehen genau vier Parameter
(`MaxMethodParameterCount` = 4, kein Verstoß). `#nullable enable` in allen neuen Dateien
vorhanden. Selbst-Lint (`ainetlinter --config rules.json --path .`) selbst nachgeprüft: `OK`,
0 Violations. `AIContextFootprint` für `GetImpactTool` (2458/2500) und
`McpServerOptionsFactory` (2469/2500) beide unter dem Limit, wie vom Coder berichtet und vom
Kritiker per eigenem Build/Lint-Lauf bestätigt — beide Werte sind aber inzwischen sehr knapp
(TD-004/TD-005 entsprechend fortgeschrieben, siehe unten). Kein DI-Container eingeführt,
Closure-basierte Registrierung konsistent zu den ersten beiden Tools
(`AiNetLinterRichtlinien.mdc#2`). Result-Pattern statt Exception für `INVALID_ARGUMENT`
konsistent zur Zero-Warning-/Result-Präferenz (`AiNetLinterRichtlinien.mdc#5`).

### Logische Korrektheit

Der Symbol-direkt-Zweig delegiert sauber an `FindReferencesTool.ResolveSymbolAsync` +
`DiffImpactAnalyzer.FindCallSitesAsync`, keine Doppel-Logik. Die
`gitRef`/`symbolIdentifier`-Exklusivitätsprüfung ist korrekt (beide gesetzt → Fehler, keines
gesetzt → Git-Ref-Zweig mit uncommitted-Default, genau eines gesetzt → jeweiliger Zweig).
`GitImpactMiniFixtureWorkspace` ist ein sauberes, wiederverwendbares Test-Fixture-Muster
(lokale `git config`, kein globaler State, read-only-Cleanup für `.git`-Objekte). Die sechs
automatisierten Tests sind aussagekräftig für das, was sie prüfen (In-Process-Aufruf von
`GetImpactTool.ExecuteAsync`).

**Aber:** genau hier liegt die entscheidende Lücke, die die Dogfooding-Prüfung aufgedeckt hat —
siehe Finding 1 unten. Die automatisierten Tests rufen `ExecuteAsync` direkt in-process auf und
verifizieren damit nur, dass `DiffImpactAnalyzer.AnalyzeAsync`/`RunGitDiff` als .NET-Methode
korrekt arbeitet, nicht dass der Git-Ref-Zweig über den tatsächlichen Produktionspfad (echter
`--mcp-server`-Subprozess mit stdio-gebundenen Handles) überhaupt terminiert.

### Konzept-Treue (Ebene 4)

**Verletzt.** `konzept.md`s Tool-Tabelle definiert `get_impact` explizit mit zwei Eingabe-Modi
("Git-Ref (optional) oder Symbol direkt"), beide als gleichwertiger Teil des Muss-Habens. Der
Git-Ref-Modus (inkl. seines Defaults „keine Angabe = uncommittete Änderungen", ebenfalls Teil
der Konzept-Tabellenzeile) funktioniert nachweislich nicht, sobald er über einen echten
stdio-gebundenen MCP-Serverprozess aufgerufen wird — er hängt unbegrenzt (kein Fehler, kein
Timeout serverseitig, einfach keine Antwort). Das ist der einzige real relevante
Aufrufkontext für `get_impact` in Produktion (ein Agent verbindet sich immer über echten
stdio-Transport, nie in-process). Siehe Finding 1 für Details und eigene Reproduktion.

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün, 0 Warnungen
dotnet test AiNetLinter.slnx  → grün (1049 Tests, 0 Fehler)
Selbst-Lint (ainetlinter --config rules.json --path .) → OK, 0 Violations
```

## Findings

1. `src/AiNetLinter/Core/DiffImpactAnalyzer.cs:78` (`RunGitDiff`) — **[CRITICAL]** **[Konzept-Treue
   / Ebene 4, zusätzlich Ebene 3 Logik]** Der Git-Ref-Zweig von `get_impact` (und damit jeder
   Aufrufer von `DiffImpactAnalyzer.AnalyzeAsync`, der über den echten stdio-MCP-Serverprozess
   läuft) hängt unbegrenzt, statt zu antworten — sowohl mit explizitem `gitRef` (z. B. `HEAD~1`)
   als auch mit weggelassenem `gitRef` (Default „uncommittete Änderungen").

   **Eigene Reproduktion (nicht nur Coder-Bericht übernommen):** gebautes
   `AiNetLinter.exe` über `StdioClientTransport` (identisches Aufrufmuster wie
   `McpServerCommandTests`/Coder-Dogfooding) gegen die reale `AiNetLinter.slnx` gestartet,
   `get_impact` ohne Parameter aufgerufen (Default-Git-Ref-Zweig, uncommittete Änderungen) →
   `TIMED OUT after 30s`, kein Ergebnis, kein Fehler. Reproduziert auf Anhieb (1 von 3
   Budget-Versuchen verbraucht), exakt wie in `step-result.md` „Dogfooding" beschrieben.
   Zusätzlich per direktem `git diff -U0 <ref1> <ref2> -- *.cs` gegen den Commit-Diff dieses
   Steps geprüft: `stderr` ist dabei leer (0 Bytes) — die einfache „stderr-Pipe-Puffer läuft voll,
   niemand liest sie" Erklärung für den bekannten .NET-Prozess-Deadlock (`RunGitDiff` setzt
   `RedirectStandardError = true`, liest aber nie `process.StandardError`, nur
   `process.StandardOutput.ReadToEnd()` gefolgt von `WaitForExit()`) ist für einen normalen Diff
   dieser Größe allein nicht ausreichend — der Hang ist wahrscheinlich tatsächlich an die
   fehlende `RedirectStandardInput = true` beim inneren `git`-Prozess gekoppelt (Handle-Vererbung
   der äußeren, an die JSON-RPC-Pipe gebundenen stdin), wie vom Coder vermutet, oder eine
   Kombination beider Faktoren unter bestimmten stderr-Ausgaben (z. B. Git-Warnungen, die im
   einfachen Diff-Test nicht auftraten). Die exakte Ursache bleibt entsprechend dem Coder-Bericht
   unbewiesen — die **Reproduzierbarkeit des Hangs selbst** ist damit aber verifiziert, nicht nur
   behauptet.

   **Einordnung Finding vs. Tech-Debt (explizite Begründung):** Der Step-Plan schränkt
   ausdrücklich ein, dass `AnalyzeAsync`/`RunGitDiff` in diesem Step unverändert bleiben sollen
   ("Bekannte Ausnahmen", "Notes"). Trotzdem ist das hier ein Finding und kein Tech-Debt-Eintrag:
   `konzept.md`s Muss-Haben-Zeile für `get_impact` verlangt beide Eingabe-Modi als Kern-Leistung
   dieses Tools; ein Modus, der in dem einzigen realen Nutzungskontext (echter MCP-Client über
   stdio) niemals antwortet, ist keine graduelle Qualitätsabweichung, sondern eine **komplett
   verfehlte Kern-Anforderung** — genau das explizite `CRITICAL`-Kriterium aus dem
   Severity-Gating ("Kern-Anforderung komplett verfehlt"), nicht "Architektur-/Anti-Pattern
   außerhalb des Scopes" (das wäre der Tech-Debt-Maßstab). Die neue Dogfooding-Pflicht aus
   `konzept.md` existiert exakt deswegen — um solche Lücken zu finden, bevor sie sich als
   Tech-Debt verstecken, statt sie erst am Task-Ende beim globalen Review zu entdecken. Ein
   Tech-Debt-Eintrag würde bedeuten: „get_impact bleibt in Produktion kaputt, wird aber nicht
   blockierend behandelt" — das widerspricht dem Zweck der Dogfooding-Pflicht.

   **Fix:** Neuer Fix-Step `step-005/fix-01`, der **gezielt** `DiffImpactAnalyzer.RunGitDiff`
   (und ggf. den analogen `RunGit`-Helper in
   `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs`, der dasselbe fragile Muster
   dupliziert, aktuell aber wegen kleiner Ausgaben nicht hängt) so anpasst, dass der
   `git`-Subprozessaufruf auch innerhalb eines stdio-gebundenen Elternprozesses zuverlässig
   terminiert — z. B. `RedirectStandardInput = true` ergänzen (verhindert Handle-Vererbung der
   äußeren stdio-Pipe) und/oder `StandardOutput`/`StandardError` beide asynchron lesen statt nur
   synchron `StandardOutput.ReadToEnd()` vor `WaitForExit()` (Standard-.NET-Empfehlung gegen
   genau diese Deadlock-Klasse). Fix-Scope bewusst eng: nur der Prozessstart-Mechanismus in
   `RunGitDiff`, keine Änderung an `AnalyzeAsync`s Analyselogik/Parsing/Rückgabeformat. Nach dem
   Fix: Dogfooding-Aufruf 2 aus diesem Step (`gitRef: "HEAD~1"` gegen die echte
   `AiNetLinter.slnx` über echten `--mcp-server`-Subprozess) muss erfolgreich und in
   angemessener Zeit antworten, bevor der Fix-Step als erledigt gilt — das ist der eigentliche
   Abnahme-Test für dieses Finding, kein neuer In-Process-Unit-Test (der den Fehler ja gerade
   nicht aufdeckt).

## Tech-Debt-Einträge aus diesem Review

Keine neuen IDs — TD-004 und TD-005 wurden mit den in diesem Step gemessenen
`AIContextFootprint`-Werten fortgeschrieben (Update-Einträge, siehe `tech-debt.md`), da beide
Werte (2458/2500, 2469/2500) inzwischen sehr knapp am Limit liegen und für die verbleibenden
zwei EPIC-03-Tools relevant werden.
