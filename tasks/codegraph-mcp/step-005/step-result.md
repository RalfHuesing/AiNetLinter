---
status: done
type: step-result
task: codegraph-mcp
step: 005
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T14:40:00Z
code_commit_hash: 4e472ba
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 005: get_impact Tool (Git-Diff- und Symbol-Impact ueber DiffImpactAnalyzer.AnalyzeAsync)

## Zusammenfassung

Alle neun im Plan beschriebenen Dateien umgesetzt: `get_impact` ist ein
duenner Dispatch mit zwei gegenseitig exklusiven Eingabe-Modi — `gitRef`
(optional, leer = uncommittete Aenderungen, delegiert an
`DiffImpactAnalyzer.AnalyzeAsync` mit **immer** `verbose: false`) oder
`symbolIdentifier` (delegiert an `FindReferencesTool.ResolveSymbolAsync`
+ `DiffImpactAnalyzer.FindCallSitesAsync`). Neuer Fehlercode
`INVALID_ARGUMENT` fuer den Fall, dass beide Parameter gesetzt sind.
Neue `GitImpactMini`-Fixture mit echtem, lokal initialisiertem
Git-Repository (erster Test-Fixture-Typ dieses Task fuer den bisher
ungetesteten Git-Diff-Zweig von `AnalyzeAsync`). Registrierung als
drittes Tool in `McpServerOptionsFactory`. Bestehender E2E-Tool-Zaehl-Test
auf drei Tools angepasst.

Die geplante Dogfooding-Ad-hoc-Pruefung (siehe DoD) hat einen
signifikanten, bislang unbekannten Defekt aufgedeckt — siehe „Dogfooding"
unten. Kein Code wurde deswegen geaendert (ausserhalb des Scopes, siehe
dort).

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` (neu) — `ExecuteAsync`/`ExecuteSymbolBranchAsync`/`ExecuteGitRefBranchAsync`.
- `src/AiNetLinter/Output/LinterErrorCodes.cs` — `InvalidArgument` ergaenzt.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — `InvalidArgument(message)` ergaenzt.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` — dritter `tools.Add(...)`-Aufruf fuer `get_impact`.
- `tests/Fixtures/GitImpactMini/` (neu) — `.slnx`, `.csproj`, `Calculator.cs`, `CalculatorCaller.cs`.
- `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` (neu) — Temp-Kopie + lokales `git init`/Commit ueber `Process`, `ChangeCalculatorAddBodyWithoutCommitting()` fuer den uncommitted-Testfall.
- `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` (neu) — sechs Tests gemaess Plan-Testliste.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` — Test umbenannt zu `RunAsync_ValidFixture_ServerRespondsWithThreeTools`, Assertion auf drei Tools inkl. `get_impact` erweitert.

## Commit

- **Code-Commit-Hash:** `4e472ba`
- **Message:**
  ```
  feat(mcp): add get_impact tool for git-diff and symbol call-site impact [codegraph-mcp]

  Adds get_impact as a thin dispatch over the existing DiffImpactAnalyzer.AnalyzeAsync
  (git-ref branch, always verbose:false to keep stdout safe for the stdio transport)
  and FindReferencesTool.ResolveSymbolAsync + DiffImpactAnalyzer.FindCallSitesAsync
  (symbol-direct branch). Introduces INVALID_ARGUMENT for the mutually exclusive
  gitRef/symbolIdentifier inputs, and a new GitImpactMini fixture + fixture workspace
  with a real local git repo to cover the previously untested git-diff branch.

  Refs: tasks/codegraph-mcp/step-005
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → gruen, 0 Warnungen
dotnet test AiNetLinter.slnx  → gruen (1049 Tests, 0 Fehler)
ainetlinter --config rules.json --path . -v → OK, 0 Violations
--footprint GetImpactTool           → 2458 (Limit 2500)
--footprint McpServerOptionsFactory → 2469 (Limit 2500)
```

## Dogfooding

Ausgefuehrt wie im DoD gefordert: gebautes `AiNetLinter.exe` per
`StdioClientTransport` (identisches Verbindungsmuster wie
`McpServerCommandTests`) als `--mcp-server --path
C:\Daten\Entwicklung\Ralf\AiNetLinter` gestartet (echtes Repo-Root, kein
Fixture) und `get_impact` per MCP-Client zweimal aufgerufen. Client-Code
lag in einem Scratch-Projekt (`ModelContextProtocol`-Client-Package,
nicht Teil des Repos, nicht committet).

**Aufruf 1 — Symbol-direkt-Modus:**
`get_impact({ symbolIdentifier: "DiffImpactAnalyzer.FindCallSitesAsync" })`
gegen die echte `AiNetLinter.slnx`.
Ergebnis: `IsError: false`, fuenf Aufrufstellen korrekt gefunden (u. a.
`GetImpactTool.cs:16`, `FindReferencesTool.cs:18/36`,
`DiffImpactAnalyzer.cs:212`, `GetImpactTool.cs:51`) — deckt sich exakt mit
den tatsaechlichen Aufrufstellen dieser Methode im Repo nach diesem Step.
Kein Auffaelligkeit, funktioniert wie erwartet.

**Aufruf 2 — Git-Ref-Modus mit `HEAD~1`:**
`get_impact({ gitRef: "HEAD~1" })` gegen dieselbe echte Solution.

**Auffaelligkeit (signifikant):** Der Aufruf hat **nie geantwortet** —
der MCP-Client-Request lief in einen selbst gesetzten 5-Minuten-Timeout
und wurde dann clientseitig abgebrochen (`TaskCanceledException`). Ein
zweiter Test mit `gitRef` komplett weggelassen (Default-Zweig
"uncommittete Aenderungen") zeigte denselben Effekt (Timeout nach 30s,
separat getestet).

Zur Eingrenzung zusaetzlich (ausserhalb der geforderten zwei
Dogfooding-Aufrufe, aber zur Ursachenklaerung):
- **CLI-Pfad identischer Logik ist schnell:** `dotnet run --project
  src/AiNetLinter -- --path . --impact HEAD~1 -v` (nutzt exakt dieselbe
  `DiffImpactAnalyzer.AnalyzeAsync`) lieferte in 4,7 s ein korrektes
  Ergebnis (fand die Aufrufstelle von `McpToolResults.InvalidArgument` in
  `GetImpactTool.cs:32`). Die Analyse-Logik selbst ist also nicht das
  Problem.
- **Symbol-direkt-Modus (kein Git-Subprozess) via MCP ist schnell:**
  Aufruf 1 oben antwortete sofort.
- **Minimaler Repro-Versuch (Parent-Prozess mit umgeleiteten
  stdin/stdout/stderr spawnt Kind-Prozess, der wiederum `git diff`
  spawnt) hat den Hang NICHT reproduziert** (38 ms, normale Rueckgabe) —
  die einfache Windows-Handle-Vererbungs-Hypothese (verschachtelte
  `Process.Start`-Umleitung ohne `RedirectStandardInput` beim inneren
  `git`-Aufruf dupliziert Handles des aeusseren stdio-Servers in den
  Git-Kindprozess) konnte damit **nicht bestaetigt** werden, bleibt aber
  die plausibelste Arbeitshypothese, da der einzige Unterschied zwischen
  "funktioniert" (CLI, Symbol-Modus) und "haengt" (Git-Ref-Modus via
  echtem MCP-Stdio-Transport) exakt der Codepfad ist, der innerhalb des
  laufenden `--mcp-server`-Prozesses (dessen eigene stdin/stdout an die
  JSON-RPC-Pipe gebunden sind) einen `git`-Subprozess per `Process.Start`
  startet (`DiffImpactAnalyzer.RunGitDiff`).
- Die exakte Ursache ist **nicht abschliessend verifiziert** — es ist ein
  reproduzierbarer, aber nicht vollstaendig erklaerter Hang, spezifisch
  fuer: Git-Ref-Zweig + echter stdio-MCP-Serverprozess (nicht In-Process-
  Testaufruf, nicht CLI).

**Einordnung:** Dieser Fund betrifft ausschliesslich
`DiffImpactAnalyzer.RunGitDiff`/`AnalyzeAsync` (Core, nicht Teil dieses
Steps' Aenderungsumfangs — der Plan untersagt explizit Aenderungen an
`AnalyzeAsync` in diesem Step) und wurde **nicht behoben** — siehe
„Beobachtungen" fuer die Weitergabe an den Kritiker. Die automatisierten
Tests dieses Steps (`GetImpactToolTests`, inkl. der beiden
Git-Ref-Zweig-Tests gegen `GitImpactMiniFixtureWorkspace`) rufen
`GetImpactTool.ExecuteAsync` direkt in-process auf (wie
`FindReferencesToolTests` es fuer `find_references` bereits tut) — dort
tritt der Hang nicht auf, da kein echter stdio-gebundener
MCP-Serverprozess involviert ist. Der bestehende E2E-Subprozess-Test
(`McpServerCommandTests`) ruft `get_impact` nicht auf (siehe TD-002,
Plan-Entscheidung "kein neuer Subprozess-E2E-Test fuer get_impact") — der
Fund waere durch die automatisierte Suite dieses Steps **nicht**
aufgefallen und wurde ausschliesslich durch die neu geforderte
Dogfooding-Pflicht entdeckt.

## Abweichungen vom Plan

Keine. Alle neun Dateien wie im Plan skizziert umgesetzt, Footprint bei
beiden im Plan explizit genannten Klassen (`GetImpactTool`,
`McpServerOptionsFactory`) unter dem Limit ohne Ausweichmuster noetig.

## Beobachtungen

- **Wichtig fuer den Kritiker (moeglicher Tech-Debt-Kandidat, hohe
  Prioritaet):** Der Git-Ref-Zweig von `get_impact` (und damit
  `DiffImpactAnalyzer.AnalyzeAsync`/`RunGitDiff` allgemein) scheint zu
  haengen, sobald er innerhalb eines echten `--mcp-server`-Prozesses
  (stdio-Transport, stdin/stdout an die JSON-RPC-Pipe gebunden) einen
  `git`-Subprozess startet — reproduzierbar sowohl mit explizitem
  `gitRef: "HEAD~1"` als auch mit weggelassenem `gitRef`
  (uncommittete-Aenderungen-Default). Siehe „Dogfooding" oben fuer
  Details und Eingrenzungsversuche. Das bedeutet: `get_impact`s
  Git-Ref-Modus funktioniert vermutlich in der Praxis (echter MCP-Client,
  z. B. ein Agent-Tool-Aufruf) nicht, obwohl alle automatisierten Tests
  gruen sind — die Tests decken diesen Pfad nur In-Process bzw. per CLI
  ab, nie durch den tatsaechlichen stdio-Serverprozess. Ich habe dies
  **nicht** behoben (ausserhalb des Scopes dieses Steps, `AnalyzeAsync`
  soll unveraendert bleiben) und keinen eigenen Tech-Debt-Eintrag
  angelegt — das bleibt dem Kritiker vorbehalten.
- Kein weiterer Nachbau/keine Duplikation: beide Zweige nutzen
  ausschliesslich bestehenden Code, `GetImpactTool.cs` ist 76 Zeilen kurz.

## Bekannte Unschärfen

- Wie im Plan unter „Bekannte Ausnahmen" dokumentiert: Der Git-Ref-Zweig
  deckt nur den bereits in `AnalyzeAsync` implementierten Umfang ab
  (nur `public`/`internal`/`protected`-Methoden/Konstruktoren). Keine
  Aenderung in diesem Step.
- Die in „Dogfooding" beschriebene Hang-Ursache ist eine plausible, aber
  nicht endgueltig bewiesene Hypothese (Windows-Prozess-Handle-Vererbung
  bei verschachtelter Stdio-Umleitung) — ein minimaler Repro-Versuch mit
  derselben Grundstruktur hat sie nicht reproduziert. Die tatsaechliche
  Ursache muesste mit tieferem Tooling (z. B. Process Explorer/Handle-
  Inspektion des haengenden `AiNetLinter.exe`- bzw. `git.exe`-Prozesses)
  weiter eingegrenzt werden.
