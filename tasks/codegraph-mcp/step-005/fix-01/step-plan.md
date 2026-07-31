---
status: open
type: step-plan
task: codegraph-mcp
step: 005/fix-01
title: "Fix: RunGitDiff haengt im echten stdio-MCP-Serverprozess (get_impact Git-Ref-Zweig)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-07-31T15:30:00Z
related_to: [tasks/codegraph-mcp/step-005/step-review.md]
---

# Step 005/fix-01: Fix: RunGitDiff haengt im echten stdio-MCP-Serverprozess (get_impact Git-Ref-Zweig)

## Bezug

- **Task:** `codegraph-mcp`
- **Epic:** `EPIC-03` aus `roadmap.md` — dieser Fix behebt ein CRITICAL-Finding
  aus dem Review von step-005 (`get_impact`), das die Konzept-Treue des
  bereits umgesetzten Tools verletzt (Git-Ref-Modus antwortet im einzigen
  realen Produktions-Aufrufkontext nicht). Kein neues Epic, kein neuer
  Tool-Scope — reiner Bugfix am bestehenden step-005-Umfang.
- **Konzept-Referenz:** `tasks/codegraph-mcp/step-005/step-review.md`,
  Finding 1 (Abschnitt „Findings", CRITICAL/Ebene 4). Das ist der
  **alleinige** Scope dieses Fix-Steps — `step-plan.md`/`step-result.md`
  von step-005 selbst dienen nur als Kontext, nicht als zusaetzlicher Scope.

## Aktueller Projektzustand (JIT-Kontext)

- `DiffImpactAnalyzer.RunGitDiff` (`src/AiNetLinter/Core/DiffImpactAnalyzer.cs:78-98`)
  startet `git diff -U0 [gitSinceRef] -- *.cs` per `Process.Start` mit
  `RedirectStandardOutput = true`, `RedirectStandardError = true`,
  **ohne** `RedirectStandardInput`, und liest danach synchron
  `process.StandardOutput.ReadToEnd()` gefolgt von `process.WaitForExit()`.
  Das ist exakt das Muster, das die offizielle .NET-Doku zu
  `Process.StandardOutput`/`Process.StandardError` als Deadlock-Risiko
  benennt, wenn *beide* Streams umgeleitet, aber nicht *beide* gelesen
  werden, bevor auf Prozessende gewartet wird — hier wird nur `stdout`
  gelesen, `stderr` nie.
- Der Kritiker hat den Hang selbst reproduziert (echter
  `--mcp-server`-Stdio-Prozess, `get_impact` mit und ohne `gitRef`,
  `TIMED OUT after 30s`) und einen einfachen stderr-voll-Test
  ausgeschlossen (leerer `stderr` bei einem normalen `git diff`), ohne die
  Ursache abschliessend zu beweisen. Zwei Faktoren bleiben als plausible,
  einander nicht ausschliessende Erklaerungen stehen:
  1. **Fehlende `RedirectStandardInput = true`:** Der aeussere
     `--mcp-server`-Prozess hat seine eigene `stdin` an die JSON-RPC-Pipe
     gebunden. Startet er selbst (ueber `Process.Start` mit
     `UseShellExecute = false`) einen Kindprozess, ohne dessen `stdin`
     explizit umzuleiten, kann der Kindprozess je nach Handle-Vererbung
     unter Windows die (an die Pipe gebundene) `stdin` des Elternprozesses
     erben. `git diff` selbst liest zwar normalerweise nicht von `stdin`,
     aber ein offenes, an eine blockierende Pipe gebundenes Handle kann in
     Kombination mit dem synchronen Lese-Muster unten zu einem Hang
     fuehren, der sich nicht ueber die einfache "stderr-Puffer voll"-Erklaerung
     zeigt.
  2. **Synchrones `ReadToEnd()` + `WaitForExit()`:** unabhaengig von (1) ist
     dies bereits fuer sich genommen die von Microsoft dokumentierte
     Deadlock-Klasse (siehe unten, Rules-Refs/Notes) und sollte in jedem
     Fall auf das empfohlene asynchrone Lese-Muster umgestellt werden — das
     ist die robuste Lösung unabhaengig davon, ob (1) allein schon reicht.
  Der Kritiker verlangt ausdruecklich **beide** Anpassungen zusammen, nicht
  nur eine isolierte Teilursache zu beheben, da die exakte Ursache nicht
  bewiesen ist (siehe Finding-Text).
- **Analoges Muster in Testcode:** `GitImpactMiniFixtureWorkspace.RunGit`
  (`src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs:72-97`)
  dupliziert dasselbe fragile Grundmuster (`RedirectStandardOutput`/
  `RedirectStandardError` ohne `RedirectStandardInput`, `stdout` wird nie
  gelesen, `stderr` nur im Fehlerfall). Haengt aktuell nicht (kleine,
  kurzlebige `git init`/`config`/`add`/`commit`-Aufrufe mit wenig Ausgabe),
  ist aber dieselbe Fund-Kategorie und wird vom Kritiker im Finding-Text
  explizit als Teil des Fix-Scopes genannt ("ggf. den analogen
  `RunGit`-Helper"). Wird hier aus Konsistenzgruenden mitgehaertet — kein
  neuer Scope, sondern dieselbe Korrektur an derselben Fund-Klasse.
- **`AnalyzeAsync`/`ParseGitDiffHunks`/restliche Analyselogik bleiben
  unveraendert** — der Plan von step-005 hatte das bereits ausgeschlossen,
  und das Finding beschraenkt den Fix explizit auf den
  Prozessstart-Mechanismus. Die Rueckgabesemantik von `RunGitDiff`
  (`string?`, `null` bei Fehlschlag, sonst Rohausgabe von `git diff`) bleibt
  identisch, damit `AnalyzeAsync` unangetastet bleibt.
- **Kein bestehender Subprozess-E2E-Test deckt den Git-Ref-Zweig ab:**
  `McpServerCommandTests.cs` hat aktuell drei Subprozess-Tests
  (`RunAsync_ValidFixture_ServerRespondsWithThreeTools`,
  `RunAsync_ValidFixture_FindSymbolReturnsMatch`,
  `RunAsync_ValidFixture_FindReferencesReturnsCallSite`) — keiner ruft
  `get_impact` auf. Genau das war die Luecke, die den Bug bis zum
  Dogfooding unentdeckt liess (siehe step-review.md, Konzept-Treue-Absatz).
  Dieser Fix-Step muss diese Luecke schliessen, sonst waere der Fix nicht
  gegen die tatsaechliche Bug-Klasse abgesichert (siehe Kritiker-Abnahmekriterium).
- `GitImpactMiniFixtureWorkspace` hat aktuell nur **einen** Commit (Ausgangszustand)
  plus eine Methode fuer eine unkommittete Aenderung
  (`ChangeCalculatorAddBodyWithoutCommitting`). Fuer einen Subprozess-Test
  mit explizitem `gitRef: "HEAD~1"` (wie im Dogfooding-Aufruf 2 aus
  step-005) braucht es einen **zweiten** Commit, sonst existiert `HEAD~1`
  gar nicht sinnvoll auswertbar. Das Fixture muss dafuer minimal erweitert
  werden (neue Methode, kein neues Fixture-Verzeichnis).

## Intention

`RunGitDiff` so anpassen, dass der `git`-Subprozessaufruf zuverlässig
terminiert, unabhängig davon, ob er in einem stdio-gebundenen
Elternprozess läuft — durch (a) `RedirectStandardInput = true` (bricht die
Handle-Vererbungskette zur äußeren JSON-RPC-Pipe) und (b) asynchrones
Lesen von `StandardOutput`/`StandardError` statt der aktuellen
Reihenfolge `ReadToEnd()` → `WaitForExit()` (Standard-.NET-Empfehlung
gegen die dokumentierte Deadlock-Klasse bei doppelt umgeleiteten Streams).
Der analoge `RunGit`-Helfer im Test-Fixture erhält dieselbe Härtung.
Anschließend: ein neuer Subprozess-E2E-Test (echte `AiNetLinter.exe`,
`StdioClientTransport`, analog `McpServerCommandTests`), der `get_impact`
sowohl mit explizitem `gitRef: "HEAD~1"` als auch ganz ohne `gitRef`
(uncommittete Änderungen) aufruft — das ist der einzige Testtyp, der die
Bug-Klasse überhaupt abdecken kann (In-Process-Tests taten das nachweislich
nicht). Kein neuer In-Process-Unit-Test allein als „Beweis" — nur
zusätzlich zur Absicherung der reinen Rückgabewerte, falls hilfreich, aber
nicht als Ersatz für den Subprozess-Test.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` (`RunGitDiff`, Zeile 78-98)

- **Was:**
  - `RedirectStandardInput = true` im `ProcessStartInfo` ergänzen; direkt
    nach `Process.Start` `process.StandardInput.Close()` aufrufen (sendet
    sofort EOF an den Kindprozess, git wartet dadurch nie auf `stdin` und
    das Pipe-Handle bleibt nicht offen an die äußere stdio-Bindung
    gekoppelt).
  - Die synchrone `process.StandardOutput.ReadToEnd()` +
    `process.WaitForExit()`-Reihenfolge durch asynchrones Lesen ersetzen:
    `OutputDataReceived`/`ErrorDataReceived`-Handler registrieren, die in
    je einen `StringBuilder` schreiben, `BeginOutputReadLine()` +
    `BeginErrorReadLine()` aufrufen, danach `process.WaitForExit()` (ohne
    Timeout-Parameter, wie bisher — Timeout-Handling ist nicht Teil dieses
    Findings).
  - Rückgabewert bleibt `process.ExitCode == 0 ? <gesammelter stdout-Text>
    : null` — identische Signatur (`string?`) und identische
    Fehlerfall-Semantik wie bisher, damit `AnalyzeAsync` unverändert
    bleibt.
  - **Wichtig für die Umsetzung:** `StringBuilder.AppendLine(e.Data)` würde
    auf Windows `\r\n` statt des von `git diff` gelieferten `\n` einfügen
    und `ParseGitDiffHunks`s zeilenbasiertes Parsing (Split auf `'\n'`,
    `StartsWith`-Prüfungen) potenziell mit zusätzlichen `\r`-Resten
    versehen. Stattdessen `stdoutBuilder.Append(e.Data).Append('\n')`
    verwenden (oder gleichwertig), um das bisherige `\n`-getrennte Format
    zu erhalten, das `ParseGitDiffHunks` erwartet.
- **Warum:** behebt beide vom Kritiker benannten, sich nicht
  ausschließenden Ursachenkandidaten für den Hang im echten
  stdio-Serverprozess, ohne die öffentliche Signatur oder das
  Rückgabeverhalten von `RunGitDiff` zu ändern — reiner
  Prozessstart-Mechanik-Fix, keine Analyselogik-Änderung.

### Datei 2: `src/AiNetLinter.Tests/Fixtures/GitImpactMiniFixtureWorkspace.cs` (`RunGit`, Zeile 72-97)

- **Was:** Dieselbe Härtung wie in Datei 1 anwenden: `RedirectStandardInput
  = true` + `process.StandardInput.Close()` nach `Process.Start`;
  `StandardOutput`/`StandardError` beide asynchron lesen (auch den bisher
  nie gelesenen `stdout` dieses Helfers), `WaitForExit()` danach. Der
  bestehende Fehlerpfad (`ExitCode != 0` → `InvalidOperationException` mit
  `stderr`-Text) bleibt erhalten, liest aber aus dem gesammelten
  `StringBuilder` statt aus `process.StandardError.ReadToEnd()`.
- **Warum:** identische Fund-Klasse wie Datei 1 (vom Kritiker im
  Finding-Text explizit mitgenannt), aktuell zwar nicht hängend, aber
  dieselbe latente Deadlock-Anfälligkeit bei größeren `git`-Ausgaben in
  künftigen Tests, die dieses Fixture wiederverwenden (siehe
  `step-plan.md` step-005, Datei 7 — "wiederverwendbar für künftige
  Tests"). Konsistenz-Fix, kein neuer Scope.
- Erweiterung um eine neue Methode `CommitCalculatorAddBodyChange()`
  (Name exemplarisch): ändert `Calculator.Add` (Signatur- oder Body-Zeile,
  analog `ChangeCalculatorAddBodyWithoutCommitting`) und committet die
  Änderung sofort (`git add -A` + `git commit -m "..."`). Damit existiert
  nach Aufruf ein zweiter Commit, sodass `HEAD~1` im neuen Subprozess-Test
  (Datei 3) einen echten, auswertbaren Diff liefert. Ergänzt die
  bestehende `ChangeCalculatorAddBodyWithoutCommitting`-Methode, ersetzt
  sie nicht — beide Testfälle (explizites `gitRef`, Default/uncommitted)
  bleiben über je eine eigene Methode klar getrennt abrufbar.

### Datei 3: `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`

- **Was:** Zwei neue Subprozess-E2E-Tests nach dem bestehenden Muster von
  `RunAsync_ValidFixture_FindReferencesReturnsCallSite` ergänzen (echte
  `AiNetLinter.exe`, `StdioClientTransport`, `McpClient.CreateAsync`,
  30s-`CancellationTokenSource` wie die bestehenden drei Tests):
  1. `RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite` —
     `GitImpactMiniFixtureWorkspace` verwenden, `CommitCalculatorAddBodyChange()`
     aufrufen (zweiter Commit), dann `get_impact` mit
     `["gitRef"] = "HEAD~1"` aufrufen. Assertion: `IsError` nicht `true`,
     Antworttext enthält `CalculatorCaller.cs` (Aufrufstelle von
     `Calculator.Add`) — **und implizit**, dass der Aufruf innerhalb der
     30s-Frist überhaupt zurückkehrt (kein Timeout/`OperationCanceledException`).
  2. `RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite` —
     dasselbe Fixture, `ChangeCalculatorAddBodyWithoutCommitting()`
     aufrufen (keine Commit), `get_impact` **ohne** `gitRef`-Parameter
     (bzw. mit `gitRef = null`) aufrufen. Gleiche Assertions.
  Beide Tests decken genau die zwei in Finding 1 genannten Szenarien ab
  (explizites `gitRef` und weggelassenes/Default-`gitRef`).
- **Warum:** Das ist der eigentliche Abnahmetest für dieses Finding — der
  Kritiker hat ausdrücklich verlangt, dass der Fix gegen den echten
  Stdio-Serverprozess verifiziert wird, nicht nur gegen einen
  In-Process-Unit-Test (der die Bug-Klasse nachweislich nicht abdeckt,
  siehe `GetImpactToolTests.cs`, die trotz identischer Analyselogik nie
  gehängt haben). TD-002 (Kosten weiterer Subprozess-Tests, aus step-004)
  wird hier bewusst in Kauf genommen — die Alternative (kein
  Subprozess-Test) würde den Fix nicht wirklich absichern, exakt das
  Problem, das dieses Finding beschreibt.

## Tests

- [ ] `RunAsync_ValidFixture_GetImpactWithGitRefReturnsCallSite` (neu, Datei 3) —
      muss **vor** dem Fix (auf dem aktuellen `main`-Stand) nachweislich
      hängen/timeout und **nach** dem Fix grün sein; das vor/nach-Verhalten
      im `step-result.md` explizit dokumentieren (nicht nur "Test ist grün").
- [ ] `RunAsync_ValidFixture_GetImpactWithoutGitRefUncommittedReturnsCallSite` (neu, Datei 3) —
      gleiche Vor/Nach-Erwartung wie oben, für den Default-Git-Ref-Fall.
- [ ] Bestehende Tests bleiben grün: `GetImpactToolTests.cs` (In-Process,
      unverändert), `DiffImpactAnalyzerTests.cs` (`ParseGitDiffHunks`,
      unverändert, prüft weiterhin, dass das `\n`-getrennte Format nach
      der Umstellung auf asynchrones Lesen identisch bleibt), die drei
      bestehenden `McpServerCommandTests`-Subprozess-Tests.
- [ ] CLI-Regressionscheck (kein automatisierter Test, aber Teil der
      Definition of Done unten): `dotnet run --project src/AiNetLinter --
      --path . --impact HEAD~1 -v` liefert weiterhin ein korrektes
      Ergebnis (wie im step-005-Dogfooding bereits mit 4,7s bestätigt) —
      stellt sicher, dass der CLI-Pfad (`ImpactCommand`), der `RunGitDiff`
      ebenfalls nutzt, durch die Umstellung nicht regressiert.

## Definition of Done

- [ ] Beide „Konkrete Änderungen" (Datei 1 + 2) umgesetzt, Datei 3 (zwei
      neue Subprozess-Tests) umgesetzt
- [ ] Vor dem Fix: mindestens einer der beiden neuen Subprozess-Tests
      (oder ein äquivalenter Ad-hoc-Nachweis) reproduziert den Hang auf dem
      unveränderten Code — dokumentiert im `step-result.md`, damit der Fix
      nachweislich etwas behebt und nicht nur einen bereits grünen Test
      hinzufügt.
- [ ] Nach dem Fix: beide neuen Subprozess-Tests grün, insbesondere ohne
      Timeout/`OperationCanceledException` innerhalb der 30s-Frist
- [ ] Build-Command aus Tech-Stack-Notiz (`dotnet build AiNetLinter.slnx`) grün, 0 Warnungen
- [ ] Test-Command aus Tech-Stack-Notiz (`dotnet test AiNetLinter.slnx`) grün (volle Suite, nicht nur die neuen Tests)
- [ ] Selbst-Lint (`ainetlinter --config rules.json --path ./src/`) 0 Violations
- [ ] CLI-Regressionscheck aus „Tests" oben manuell ausgeführt und im
      `step-result.md` mit Ergebnis dokumentiert
- [ ] **Erneutes Dogfooding (Abnahmekriterium des Kritikers):** Aufruf 2 aus
      step-005 (`get_impact({ gitRef: "HEAD~1" })` gegen die echte
      `AiNetLinter.slnx` über echten `ainetlinter --mcp-server`-Subprozess,
      identisches Aufrufmuster wie im step-005-Dogfooding) erneut
      durchführen und im `step-result.md` dieses Fix-Steps mit Ergebnis
      dokumentieren — **das ist die eigentliche Abnahme für dieses
      Finding**, zusätzlich zu den automatisierten Tests, nicht optional.
- [ ] Commit auf aktuellem Branch (Conventional Commit, Englisch,
      `[codegraph-mcp]`-Suffix, siehe `roadmap.md` Tech-Stack-Notiz;
      `fix:`-Typ, kein `feat:`, da reiner Bugfix)
- [ ] `step-005/fix-01/step-result.md` geschrieben
- [ ] `status` in diesem `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Zero-Warning-Direktive;
  die Umstellung auf `OutputDataReceived`/`ErrorDataReceived` darf keine
  neuen Compiler-Warnungen (z. B. durch Lambda-Closures über
  `StringBuilder`) einführen.
- `.agents/rules/AiNetLinter.mdc#Grenzwerte (Produktion)` — Methodenlänge
  (`RunGitDiff`/`RunGit` bleiben nach der Umstellung voraussichtlich unter
  dem Zeilenlimit; falls nicht, kleine private Hilfsmethode statt neuer
  Abstraktionsebene, analog dem in step-005 bereits verwendeten
  Ausweichmuster).

## Bekannte Ausnahmen

- Keine.

## Code-Skizze (optional)

```csharp
private static string? RunGitDiff(string repoRoot, string? gitSinceRef)
{
    var args = string.IsNullOrEmpty(gitSinceRef) ? "diff -U0 -- *.cs" : $"diff -U0 {gitSinceRef} -- *.cs";
    var startInfo = new ProcessStartInfo
    {
        FileName = GitCommand,
        Arguments = args,
        WorkingDirectory = repoRoot,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    using var process = Process.Start(startInfo);
    if (process == null) return null;

    process.StandardInput.Close();

    var stdout = new StringBuilder();
    var stderr = new StringBuilder();
    process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.Append(e.Data).Append('\n'); };
    process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.Append(e.Data).Append('\n'); };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();

    process.WaitForExit();

    return process.ExitCode == 0 ? stdout.ToString() : null;
}
```

## Notes

- **Warum beide Massnahmen zusammen und nicht nur eine:** die exakte
  Ursache ist laut Kritiker nicht abschliessend bewiesen (der minimale
  Repro-Versuch aus step-005 konnte die Handle-Vererbungs-Hypothese allein
  nicht bestaetigen). Nur die synchrone Lese-Reihenfolge zu fixen und
  `RedirectStandardInput` wegzulassen (oder umgekehrt) wuerde das Risiko
  eingehen, nur eine von zwei moeglichen Teilursachen zu beheben und den
  Hang unter leicht veraenderten Bedingungen (z. B. groesserer Diff, andere
  Windows-Version) wieder auftreten zu lassen. Beide Aenderungen sind
  fuer sich genommen Standard-.NET-Empfehlungen ohne Nachteil — es gibt
  keinen Grund, nur eine davon umzusetzen.
- **Warum der Subprozess-Test kein „Nice-to-have" ist:** Der Kritiker hat
  explizit demonstriert, dass sechs gruene In-Process-Tests
  (`GetImpactToolTests.cs`) den Hang nicht aufdecken. Ein Fix-Step, der nur
  einen weiteren In-Process-Test ergaenzt, waere gegen genau die
  Bug-Klasse, die dieses Finding beschreibt, blind. Der neue
  Subprozess-Test **muss** vor dem Fix nachweislich haengen/timeout, sonst
  ist nicht belegt, dass er die richtige Sache prueft.
- **`HEAD~1` im Fixture:** Das bestehende `GitImpactMiniFixtureWorkspace`
  hatte bisher nur einen Commit, weil step-005 nur den
  "uncommittete Aenderungen"-Fall testete (siehe `GetImpactToolTests`,
  Testfall `ExecuteAsync_NoGitRefUncommittedChange_ReturnsChangedMethodCallSite`).
  Die neue `CommitCalculatorAddBodyChange()`-Methode ist eine reine
  Ergaenzung (zweiter Commit) — sie aendert nichts an bestehenden Tests,
  die weiterhin nur den ersten Commit + `ChangeCalculatorAddBodyWithoutCommitting()`
  nutzen.
- **Nicht anfassen:** `AnalyzeAsync`, `ParseGitDiffHunks`,
  `GetChangedSymbolsFromHunksAsync`, `FindAllCallSitesAsync`,
  `GetImpactTool.cs` selbst (Dispatch-Logik von step-005) — dieser Fix ist
  ausschliesslich Prozessstart-Mechanik in `RunGitDiff` (+ analogem
  Test-Helfer) und ein neuer Verifikationstest. Auch `LogGitWarning`
  (Console.WriteLine-Problem bei `verbose`) ist **nicht** Teil dieses
  Findings — das war bereits in step-005 bewusst durch `verbose: false`
  in `GetImpactTool` abgesichert und ist ein anderes Thema.
