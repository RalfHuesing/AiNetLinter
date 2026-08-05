---
status: done (pending audit)
type: step-plan
task: mcp-call-logging-fuer-agenten-analyse
step: 001
title: "Default-Pfad-Konvention für --mcp-log Opt-in (harter Fehler bei fehlender Solution)"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: MiniMax-M3
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-05T12:45:00+02:00
related_to: []
---

# Step 001: Default-Pfad-Konvention für --mcp-log Opt-in (harter Fehler bei fehlender Solution)

## Bezug

- **Task:** `mcp-call-logging-fuer-agenten-analyse`
- **Epic:** `EPIC-01` aus `roadmap.md` — Default-Pfad-Konvention für `--mcp-log`
  Opt-in (`<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`).
  Per User-Entscheidung 2026-08-05: **kein** Fallback-Pfad
  (`ainetlinter-no-solution-<yyyy-MM-dd>`) bei fehlender Solution —
  stattdessen harter Abbruch mit Fehlermeldung auf stderr und Exit-Code
  ≠ 0, kein Log-Start, kein Server-Start.
- **Konzept-Referenz:** `Konzept.md` §"Muss-Haben" Punkte 1–3, §"Wie
  (grober Ansatz)" Schritte 1–2; DoD 1 wird durch diesen Step vorbereitet
  (DoD-1-Verifikation selbst ist EPIC-04). Beachte: die ursprüngliche
  Konzept-Skizze („Wie (grober Ansatz)" Schritt 1) erwähnt den
  Fallback-Pfad noch — das ist die Stelle, die durch die User-Korrektur
  2026-08-05 hinfällig geworden ist und in der `revision_history` von
  `Konzept.md` dokumentiert ist. Der Planer setzt das **korrigierte**
  Konzept um, nicht die alte Skizze.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des Quellcodes vorgefunden, was diesen Step prägt:

- `McpServerCommand.TryCreateCallLog` (`src/AiNetLinter/Commands/McpServerCommand.cs:85-91`)
  liefert heute bei `null` **und** bei Whitespace `null` zurück und beendet
  damit das Logging komplett. Die "Whitespace-zu-Default"-Umkehr ist der
  eigentliche Schnittstellen-Bruch, nicht das Hinzufügen einer neuen Methode.
- `McpServerCommand.ResolveMcpLogPath` (Z. 99-104) bleibt für Backward-Compat
  expliziter Pfade unverändert (absolut = wie angegeben; relativ = relativ
  zur Solution-Dir). Wichtig: diese Methode **erhält** den alten Vertrag
  (kein `string?`-Solution-Param, da sie explizite Pfade auflöst und keine
  Default-Pfad-Konstruktion macht).
- `McpServerCommand.ResolveSolutionPathOrError` (Z. 173-194) bricht bei nicht
  auflösbarer Solution bereits heute mit `LinterErrorFormatter`-Ausgabe auf
  stderr und `return 1` ab — `RunAsync` macht unmittelbar danach
  `if (solutionPath is null) return 1;` (Z. 34-35). **In der Praxis** ist
  `solutionPath` beim Erreichen von `TryCreateCallLog` also garantiert
  non-null. Die Failure-Pfad-Anforderung des neuen Konzepts
  (`BuildDefaultLogPath` signalisiert Failure bei null-Solution) ist
  trotzdem Pflicht, weil der Helper sonst **stillschweigend** einen
  Fallback-Pfad bauen würde, falls er je aus einem Kontext ohne
  vorherige `ResolveSolutionPathOrError`-Prüfung aufgerufen wird. Konzept:
  „ein harter Abbruch zwingt zur expliziten Klärung" — dieser Anspruch
  gilt auch für die Helper-Kontrakt-Ebene, nicht nur für `RunAsync`.
- `McpCallLog`-Konstruktor (`src/AiNetLinter/Mcp/McpCallLog.cs:33-41`) legt
  das Zielverzeichnis bereits automatisch via `Directory.CreateDirectory(dir)`
  an, nutzt `FileMode.Append` + `FileShare.Read` und UTF-8 ohne BOM. Diese
  Infrastruktur wiederverwenden — kein neues IO in `BuildDefaultLogPath`
  nötig.
- `McpCallLog` hat bereits einen privaten `_writeLock` (Z. 29) und einen
  `IAsyncDisposable`-Pfad; DoD 3 (Lock-Reihenfolge Call/Error) ist erst mit
  EPIC-02 (Error-Methode) relevant, aber die jetzige Signatur-Erweiterung
  muss die spätere Hinzunahme von `RecordError` nicht behindern.
- `McpServerCommandCallLogTests`
  (`src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs`) hat
  **6** Tests, die `TryCreateCallLog` oder `ResolveMcpLogPath` direkt
  aufrufen. Roadmap-Korrektur (extern aktualisiert): nur
  `TryCreateCallLog_WhitespacePath_ReturnsNull` testet das alte, jetzt
  ungültige Verhalten; `TryCreateCallLog_PathNotSet_ReturnsNull` (Argument
  `null`) bleibt inhaltlich korrekt (Flag nicht gesetzt → kein Log) und wird
  **nicht** ersetzt. Die zwei relativen/absoluten Pfad-Tests und die zwei
  `ResolveMcpLogPath_*`-Tests bleiben inhaltlich und bekommen nur die neue
  Signatur (zusätzliche `exeDir`-/`console`-Argumente).
- Test-Infrastruktur für `ILintConsole` ist bereits etabliert:
  `TestLintConsole` (`src/AiNetLinter.Tests/Output/TestLintConsole.cs`)
  sammelt `WriteLine`/`WriteError`-Aufrufe in zwei `List<string>`s und ist
  `internal sealed`. Für den neuen Failure-Test wird genau diese Fake
  verwendet — keine neue Test-Infrastruktur nötig.
- `LinterErrorFormatter.Format(code, message, context?, hint?)`
  (`src/AiNetLinter/Output/LinterErrorFormatter.cs:13`) ist die
  projektweite Konvention für strukturierte, maschinenlesbare Fehler.
  Existierende Fehler-Codes in `LinterErrorCodes.cs`:
  `ResourceNotFound`, `InvalidArgument` — passen für den neuen
  No-Solution-Fehler.
- `CliOptionFactory.CreateMcpLogOption` (Z. 230-233) hat aktuell die
  Description „Default: deaktiviert. Pfad-Aufloesung: absolut → wie
  angegeben; relativ → relativ zum Solution-Verzeichnis. Beispiel:
  --mcp-log ./.mcp-log/calls.log". Diese Description ist User-facing
  Doku. **Wird in EPIC-04 mit den `Docs/`-Updates gebündelt, nicht in
  diesem Step geändert** (analog zum Vorgehen im ersten Plan-Entwurf).
  Die technisch notwendige `ArgumentArity.ZeroOrOne` (sonst Parser-Fehler
  bei `--mcp-log` ohne Wert) wird hier gesetzt — das ist kein Doku-Touch,
  sondern harte Voraussetzung dafür, dass der Default-Pfad überhaupt
  erreichbar ist.
- `Program.cs:112` setzt `McpLogPath = parsed.McpLog` — keine Änderung
  nötig, der Parsing-Pfad bleibt.
- Die `Option<string?>`-Definition ohne explizite `ArgumentArity` lässt
  `--mcp-log` ohne Wert derzeit **fehlschlagen** (System.CommandLine wirft
  einen Parser-Fehler bei `OneOrMore`-Default). Für die Default-Pfad-
  Konvention muss `ArgumentArity.ZeroOrOne` gesetzt sein — das ist eine
  technische Notwendigkeit, kein neues Feature, und im Konzept
  („Muss-Haben 1") implizit vorausgesetzt.
- **Result-Pattern** im Projekt: Es gibt **keinen** formalen `Result<T>`-Typ
  im Codebase. Die in `AiNetLinterRichtlinien.mdc` §5 formulierte
  Bevorzugung wird projektweit über zwei etablierte Muster umgesetzt:
  (a) `Try*`-Methoden, die `T?` zurückgeben und `null` = „nicht aktiv /
    Fehler", und (b) Methoden mit Suffix `*OrError`, die selbst die
  Fehlermeldung via `console.WriteError(LinterErrorFormatter.Format(...))`
  ausgeben und `null` zurückgeben (siehe `ResolveSolutionPathOrError`).
  Der neue `BuildDefaultLogPath`-Helper folgt Muster (b) — passt zur
  bestehenden Konvention, vermeidet eine neue `Result<T>`-Einführung.

## Intention

`--mcp-log` ohne Wert aktiviert das Logging mit einem vorhersagbaren
Default-Pfad, ohne dass der User einen konkreten Pfad konstruieren muss;
ein explizit angegebener Pfad verhält sich exakt wie bisher. Wenn der
Default-Pfad mangels auflösbarer Solution nicht konstruierbar ist, bricht
`--mcp-server` hart ab — kein stiller Server-Start ohne zugeordnetes
Log-Verzeichnis. Damit wird die Lücke zwischen Doku
(`Docs/agent-api.md:317` suggeriert bereits Default-Verhalten) und
Code geschlossen, ohne die Diagnose-Sicherheit (EPIC-02 Error-Sink und
EPIC-03 Error-Hook setzen ein eindeutig zugeordnetes Log voraus) zu
untergraben.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Cli/CliOptionFactory.cs` (Zeile 230-233)

- **Was:** `opt.Arity = ArgumentArity.ZeroOrOne;` an der `--mcp-log`-Option
  setzen (z. B. direkt nach der Initialisierung von `opt`). Damit
  akzeptiert System.CommandLine `--mcp-log` sowohl ohne Wert (→ Whitespace-
  Sentinel im `McpServerCommand` triggert Default-Pfad) als auch mit Wert
  (Backward-Compat für explizite Pfade).
- **Warum:** Ohne diese Arity-Anpassung kann der User das Flag gar nicht
  ohne Wert aufrufen, und der ganze Default-Pfad-Pfad ist tot. Konzept
  spezifiziert die Arity nicht explizit — das ist eine technische
  Notwendigkeit, kein neues Feature. Description-Text bleibt
  unverändert (Doku-Sammel-Step EPIC-04).

### Datei 2: `src/AiNetLinter/Commands/McpServerCommand.cs` (Zeile 31-104)

- **Was:**
  1. Neuen Helper `internal static string? BuildDefaultLogPath(string? solutionPath, string exeDir, ILintConsole console)` anlegen
     (passend zu `ResolveSolutionPathOrError` als `internal static` mit
     Console-Param, testbar):
     - Wenn `string.IsNullOrWhiteSpace(solutionPath)` → `console.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.ResourceNotFound, "Keine Solution fuer --mcp-log aufloesbar; ohne sie ist kein Default-Log-Verzeichnis ableitbar.", hint: "Server aus einem Verzeichnis mit genau einer .sln/.slnx starten oder --path auf eine konkrete Solution-Datei setzen."));` und `return null;`.
     - Sonst: `var solutionName = Path.GetFileNameWithoutExtension(solutionPath);` — wenn auch das leer wäre (z. B. `".slnx"` als Pfad), denselben Fehlerpfad nehmen.
     - Datum: `DateTime.Now.ToString("yyyy-MM-dd")` (lokale Zeitzone, nicht UTC).
     - Rückgabe: `Path.Combine(exeDir, "logs", solutionName, dateStr, "calls.jsonl")`.
  2. `TryCreateCallLog` Signatur erweitern:
     - Alt: `internal static McpCallLog? TryCreateCallLog(string? mcpLogPath, string solutionPath)`
     - Neu: `internal static McpCallLog? TryCreateCallLog(string? mcpLogPath, string? solutionPath, string exeDir, ILintConsole console)`
     - Verhalten:
       - `mcpLogPath is null` → `return null;` (Opt-in nicht aktiv, **kein** Konsolen-Output, **kein** Fehler — exakt wie bisher).
       - `string.IsNullOrWhiteSpace(mcpLogPath)` → `var defaultPath = BuildDefaultLogPath(solutionPath, exeDir, console);` und `return defaultPath is null ? null : new McpCallLog(defaultPath);`. Im Fehlerfall hat `BuildDefaultLogPath` bereits die Fehlermeldung auf stderr geschrieben; `TryCreateCallLog` propagiert das Failure-Signal durch `null`-Rückgabe.
       - sonst → `return new McpCallLog(ResolveMcpLogPath(mcpLogPath, solutionPath ?? string.Empty));` (Backward-Compat für explizite Pfade).
  3. Aufruf-Stelle in `RunAsync` (Z. 62-64) anpassen:
     - Alt: `callLog = TryCreateCallLog(args.McpLogPath, solutionPath);`
     - Neu: vor `TryCreateCallLog` einmal `var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;` (statische Ermittlung, kein DI, keine Factory — passt zum etablierten Muster in `McpServerOptionsFactory.cs:72-75`), und `var wasOptedIn = args.McpLogPath is not null;` plus `callLog = TryCreateCallLog(args.McpLogPath, solutionPath, exeDir, c);`. Direkt danach: `if (wasOptedIn && callLog is null) { return 1; }` — das ist die vom Konzept geforderte „sauberer Fehler-Exit"-Umsetzung. Die Fehlermeldung wurde bereits vom Helper auf stderr geschrieben; `RunAsync` muss sie nicht duplizieren.
  4. XML-Doc-Kommentare von `TryCreateCallLog` und `BuildDefaultLogPath` an die neue Semantik anpassen: ersteres beschreibt jetzt die drei Fälle (null/leer/Pfad); der Helper dokumentiert explizit, dass er `null` + Konsolen-Fehler bei fehlender Solution liefert. `ResolveMcpLogPath` bleibt semantisch unverändert mit kurzem Hinweis, dass die Methode nur für explizite Pfade aufgerufen wird.
  5. `using System.Reflection;` am Dateianfang hinzufügen (für `Assembly.GetExecutingAssembly()`).
- **Warum:** Eine zusammenhängende Schnittstellen-Änderung. Helper als `internal static` symmetrisch zum bestehenden `ResolveMcpLogPath` (bzw. `ResolveSolutionPathOrError`), damit Tests ohne Reflektion oder `InternalsVisibleTo`-Gymnastik auskommen. `exeDir` wird einmal in `RunAsync` aufgelöst und durchgereicht — keine versteckte globale State. Das `wasOptedIn && callLog is null`-Muster unterscheidet sauber zwischen „Opt-in gar nicht aktiv" (kein Fehler) und „Opt-in aktiv, aber Helper gescheitert" (Failure-Exit) — der einzige Unterschied zwischen den beiden `null`-Rückgaben ist genau die Quelle (`args.McpLogPath is null` vs. `BuildDefaultLogPath` lieferte `null`).

### Datei 3: `src/AiNetLinter/Cli/LinterArgs.cs` (Zeile 166-170)

- **Was:** XML-Doc-Kommentar von `McpLogPath` anpassen auf:
  „Optionaler Pfad fuer das MCP-Call-Log (JSONL-Format, ein Eintrag pro
  Tool-Call). `null` = Log deaktiviert (Default). Wert = expliziter
  Pfad (absolut → wie angegeben; relativ → relativ zum
  Solution-Verzeichnis). Leerer/Whitespace-Wert = Default-Pfad unter
  `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`; erfordert
  eine auflösbare Solution, sonst Abbruch mit Exit ≠ 0." Sonst keine
  Code-Änderung.
- **Warum:** Die Property ist Teil des öffentlichen API-Vertrags
  (`LinterArgs` wird im Test-Setup verwendet) und ihre Doku soll das
  tatsächliche Verhalten widerspiegeln. Reine Kommentar-Änderung, kein
  Verhalten. Die `<exeDir>/...`-Formulierung hält die Test-Assertion
  `TryCreateCallLog_PathNotSet_ReturnsNull` (Argument `null`) weiter
  korrekt, weil `null` jetzt explizit als „Log deaktiviert" dokumentiert
  ist.

### Datei 4: `src/AiNetLinter/Mcp/McpCallLog.cs` (Zeile 22-53)

- **Was:** Kleine Begleit-Änderung: neuen `internal string LogPath { get; }`-
  Property hinzufügen (read-only), die `_logPath` zurückgibt. Damit der
  neue `TryCreateCallLog_WhitespacePath_CreatesDefaultLog`-Test den
  tatsächlich gewählten Pfad verifizieren kann, ohne Reflection.
- **Warum:** Saubere Test-Beobachtbarkeit. `MaxPublicMembersPerType = 15`
  ist kein Problem (Klasse hat aktuell ~6 öffentliche/innere Member).
  `EnforceSealedClasses` ist erfüllt (`internal sealed class`).

### Datei 5: `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs`

- **Was:**
  1. **Löschen**: `TryCreateCallLog_WhitespacePath_ReturnsNull` (Z. 31-40)
     — testet das alte, jetzt ungültige Verhalten.
  2. **Anpassen** an die neue Signatur (alle `TryCreateCallLog`-Aufrufe
     um `exeDir` und `console` ergänzen):
     - `TryCreateCallLog_PathNotSet_ReturnsNull` (Z. 20-29) — inhaltlich
       unverändert (`null` → `null`).
     - `TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir`
       (Z. 42-67) — `exeDir` dummy (z. B. `Path.Combine(Path.GetTempPath(), "exe-" + Guid.NewGuid().ToString("N"))`), `console` = `new TestLintConsole()`.
     - `TryCreateCallLog_AbsolutePath_CreatesLogFileAtGivenPath` (Z. 69-87)
       — dito.
  3. **Hinzufügen**:
     - `TryCreateCallLog_WhitespacePath_CreatesDefaultLog`: übergibt
       `"   "` an `TryCreateCallLog` mit gültiger Solution-Pfad und
       `exeDir`, erwartet nicht-`null` `McpCallLog` und verifiziert
       `log.LogPath` endet auf `Path.Combine(solutionName, <heute>, "calls.jsonl")`
       und beginnt mit `exeDir` + `"logs"`. Konsole darf keine Errors
       enthalten.
     - `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`:
       übergibt `"   "` mit `solutionPath: null`, erwartet `null` als
       Rückgabe und `TestLintConsole.Errors` enthält genau eine Zeile mit
       `[ERROR]:` und `RESOURCE_NOT_FOUND`. Pflicht-Test, der das
       Konzept-Update (kein Fallback, harter Fehler) dokumentiert und
       verhindert, dass eine zukünftige Refactor-Welle versehentlich
       wieder einen `ainetlinter-no-solution-...`-Fallback einbaut.
     - `BuildDefaultLogPath_WithSolution_IncludesSolutionName`:
       übergibt `("/repo/MyApp.slnx", "/opt/ainet", console)` und
       erwartet `Path.Combine("/opt/ainet", "logs", "MyApp", <heute>, "calls.jsonl")`.
     - `BuildDefaultLogPath_DateIsLocal`: prüft, dass der
       Datums-Component `DateTime.Now.ToString("yyyy-MM-dd")` exakt
       gleicht (nicht UTC).
- **Warum:** Sechs Tests bestätigen die neue Schnittstelle + den Helper
  isoliert; einer verifiziert das Zusammenspiel mit `McpCallLog`; einer
  dokumentiert explizit das Failure-Signal bei fehlender Solution.
  Test-Override `MaxMethodLineCount = 100` aus `AiNetLinter.mdc` erlaubt
  die längeren Setup-Blöcke.

## Tests

- [ ] `TryCreateCallLog_PathNotSet_ReturnsNull` (Signatur angepasst, inhaltlich unverändert)
- [ ] `TryCreateCallLog_RelativePath_CreatesLogFileRelativeToSolutionDir` (Signatur angepasst, inhaltlich unverändert)
- [ ] `TryCreateCallLog_AbsolutePath_CreatesLogFileAtGivenPath` (Signatur angepasst, inhaltlich unverändert)
- [ ] `TryCreateCallLog_WhitespacePath_CreatesDefaultLog` (neu — Happy Path der Default-Pfad-Konvention)
- [ ] `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull` (neu — Konzept-Update-Dokumentation: kein Fallback, harter Fehler)
- [ ] `BuildDefaultLogPath_WithSolution_IncludesSolutionName` (neu — Helper isoliert)
- [ ] `BuildDefaultLogPath_DateIsLocal` (neu — Helper isoliert)
- [ ] `ResolveMcpLogPath_AbsolutePath_ReturnsAsIs` (unverändert)
- [ ] `ResolveMcpLogPath_RelativePath_ResolvedAgainstSolutionDirectory` (unverändert)
- [ ] Bestehende `McpCallLogTests` (5 Tests in `McpCallLogTests.cs`) bleiben unverändert und grün — Konzept DoD 5
- [ ] `TryCreateCallLog_WhitespacePath_ReturnsNull` ist **gelöscht** (testete das alte, jetzt ungültige Verhalten)

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] `dotnet build` grün (Zero-Warning-Direktive aus `AiNetLinterRichtlinien.mdc` §5)
- [ ] `dotnet test --filter Category=Unit` grün (schnelle Iteration)
- [ ] `dotnet test` (Volllauf) grün — Konzept DoD 5
- [ ] Kein bestehender `McpCallLogTests`-Test verändert
- [ ] Der String `ainetlinter-no-solution` taucht in keiner geänderten
      Datei (Produktion, Test, Doku dieses Steps) auf — Suche per
      `grep -ri "ainetlinter-no-solution"` muss leer bleiben
- [ ] Conventional Commit auf aktuellem Branch mit Pflicht-Suffix
      `[mcp-call-logging-fuer-agenten-analyse]` (siehe `roadmap.md`
      §Commit-Konventionen und `spec.md` §10.3)
- [ ] `step-001/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#2-Architektur-Verbote` —
  `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` ist
  statische Ermittlung, kein DI-Container, kein `AssemblyLoadContext`;
  passt zur Verbotsliste. Muster bereits etabliert in
  `McpServerOptionsFactory.cs:72-75`.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3-Windows-Umgebung-und-Tool-Regeln`
  — Build/Test-Command `dotnet build` / `dotnet test`; Pfad-Trenner via
  `Path.Combine` (kein Hardcoding).
- `.agents/rules/AiNetLinterRichtlinien.mdc#4-Updates-und-Tests` —
  xUnit-v3-Pflicht (bestehende Tests sind v3); Commit-Vorschlag-Block
  am Ende der Coder-Antwort; **bewusst ausgenommen**:
  `Docs/ROADMAP.md` / `Docs/configuration.md` / `Docs/agent-api.md` /
  `README.md` / `rules.json` — diese Dateien sind im EPIC-04-Sammel-Step.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-Qualitaetsdrift-Praevention`
  — Zero-Warning-Direktive (kein neuer Build-Bruch), Clean-Code-
  Kommentar-Politik: keine `step-001` / `EPIC-01` / Konzept-Verweise in
  C#-Code, keine redundanten Nacherzählungen sprechender Namen, kein
  Refactoring-Historie-Kommentar.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-Qualitaetsdrift-Praevention-Result-Pattern`
  — `BuildDefaultLogPath` folgt dem im Projekt etablierten
  `*OrError`-Muster (`string?`-Rückgabe + Konsolen-Fehler via
  `LinterErrorFormatter`), nicht einem neu eingeführten `Result<T>`-Typ.
  `TryCreateCallLog` bleibt `Try*`-Methode, passt zum Pattern.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` — `sealed` für konkrete
  Klassen (bestehende `McpCallLog` ist `sealed`; `BuildDefaultLogPath`
  ist `static`); `#nullable enable` (alle Dateien haben es);
  Methoden ≤60 Zeilen (Test-Override 100); `MaxMethodParameterCount`
  ≤4 — `BuildDefaultLogPath` hat 3 (≤4 ✓), `TryCreateCallLog` bekommt 4
  (≤4 ✓, am Limit aber vertretbar, weil die 4. Position die ohnehin
  vorhandene `console`-Abhängigkeit der Datei bündelt).
- `.agents/rules/AiNetLinter.mdc#Grenzwerte-Produktion` —
  `MaxConstructorDependencies` ≤5 (nicht betroffen, kein neuer
  Konstruktor), `AIContextFootprint` ≤2500 (nicht betroffen, kein neuer
  Typ hinzu).
- `.agents/rules/AiNetLinter.mdc#EnforceNamespaceDirectoryMapping` —
  `McpServerCommand.cs` bleibt in `src/AiNetLinter/Commands/` und
  Namespace `AiNetLinter.Commands`; `BuildDefaultLogPath` fügt sich in
  dieselbe Datei ein.
- `.agents/rules/AiNetLinter.mdc#EnforceNoSilentCatch` — kein neuer
  `catch`-Block; bestehende `TryDelete`-Helfer in den Tests sind
  explizit als best-effort kommentiert.

## Bekannte Ausnahmen

- `BuildDefaultLogPath_DateIsLocal` ist tagesrand-anfällig: wenn der
  Test knapp vor Mitternacht läuft, könnte `DateTime.Now` im Helper
  einen anderen Tag liefern als der im Test ermittelte Soll-Wert.
  Mitigation: Test verwendet `var today = DateTime.Now.ToString("yyyy-MM-dd");`
  und vergleicht den *Anteil* des Helper-Ergebnisses (letzter Pfad-Component
  vor `calls.jsonl`). Risiko akzeptabel, weil die Test-Laufzeit <1s ist
  und Mitternachts-Treffer in CI extrem unwahrscheinlich.
- `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`
  ist der erste Test in dieser Datei, der die `ILintConsole`-Fake
  verwendet. Die exakte Fehlermeldung wird per `Assert.Contains` auf
  `[ERROR]:` und `RESOURCE_NOT_FOUND` geprüft (nicht auf den vollen
  Wortlaut), damit ein zukünftiger Text-Tweak den Test nicht bricht —
  die Failure-Signalisierung als solche bleibt das eigentliche
  Prüfkriterium.
- **Defense in depth**: In `RunAsync` ist der `wasOptedIn && callLog is null`-
  Check in der Praxis unerreichbar, weil `ResolveSolutionPathOrError` (Z. 34-35)
  bereits bei null-Solution mit `return 1` abbricht, bevor `TryCreateCallLog`
  überhaupt aufgerufen wird. Der Check bleibt trotzdem drin, weil er die
  Helper-Kontrakt-Ebene absichert (verhindert, dass eine zukünftige
  Aufruf-Reihenfolge-Änderung den Fehlerpfad verschluckt) und weil er
  die explizite Konzept-Anforderung „harter Fehler-Exit" auf der
  Entry-Point-Ebene sichtbar macht.
- `CliOptionFactory.CreateMcpLogOption` (Z. 230-233) Description-Text wird
  **nicht** in diesem Step aktualisiert. Inkonsistenz zur Roadmap EPIC-01,
  aber konsistent mit dem im Brief gegebenen Scope (Doku-Sammel-Step
  EPIC-04). Falls der Coder die Description hier anpasst, ist das ok,
  aber nicht erforderlich.

## Code-Skizze (optional)

```csharp
// In McpServerCommand.cs
using System.Reflection; // neu

internal static string? BuildDefaultLogPath(string? solutionPath, string exeDir, ILintConsole console)
{
    var solutionName = string.IsNullOrWhiteSpace(solutionPath)
        ? null
        : Path.GetFileNameWithoutExtension(solutionPath);

    if (string.IsNullOrWhiteSpace(solutionName))
    {
        console.WriteError(LinterErrorFormatter.Format(
            LinterErrorCodes.ResourceNotFound,
            "Keine Solution fuer --mcp-log aufloesbar; ohne sie ist kein Default-Log-Verzeichnis ableitbar.",
            hint: "Server aus einem Verzeichnis mit genau einer .sln/.slnx starten oder --path auf eine konkrete Solution-Datei setzen."));
        return null;
    }

    var dateStr = DateTime.Now.ToString("yyyy-MM-dd");
    return Path.Combine(exeDir, "logs", solutionName, dateStr, "calls.jsonl");
}

internal static McpCallLog? TryCreateCallLog(string? mcpLogPath, string? solutionPath, string exeDir, ILintConsole console)
{
    if (mcpLogPath is null) return null;
    if (string.IsNullOrWhiteSpace(mcpLogPath))
    {
        var defaultPath = BuildDefaultLogPath(solutionPath, exeDir, console);
        return defaultPath is null ? null : new McpCallLog(defaultPath);
    }
    return new McpCallLog(ResolveMcpLogPath(mcpLogPath, solutionPath ?? string.Empty));
}

// In RunAsync (Z. 62-64, neu)
var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
var wasOptedIn = args.McpLogPath is not null;
McpCallLog? callLog = null;
try
{
    callLog = TryCreateCallLog(args.McpLogPath, solutionPath, exeDir, c);
    if (wasOptedIn && callLog is null)
    {
        return 1; // Fehlermeldung wurde bereits vom Helper auf stderr geschrieben.
    }

    var serverOptions = McpServerOptionsFactory.Create(mcpState, callLog);
    // ... (Rest wie bisher)
}
```

## Notes

- **Wiederverwendung statt Neubau**: Der `McpCallLog`-Konstruktor legt das
  Zielverzeichnis bereits via `Directory.CreateDirectory(dir)` an
  (Z. 36-37). Das genügt für die Default-Pfad-Tiefe
  (`<exeDir>/logs/<name>/<datum>/`) — kein zusätzlicher `mkdir`-Schritt
  in `BuildDefaultLogPath`.
- **Failure-Signalisierung ohne neuen `Result<T>`-Typ**: Das Projekt
  hat **keinen** formalen `Result<T>`-Typ. Die in
  `AiNetLinterRichtlinien.mdc` §5 erwähnte Bevorzugung wird projektweit
  über (a) `Try*`-Methoden mit `T?`-Rückgabe und (b) `*OrError`-Methoden
  mit Konsolen-Fehler-Ausgabe umgesetzt. `BuildDefaultLogPath` folgt
  Muster (b), `TryCreateCallLog` bleibt Muster (a). Konsistent mit
  `ResolveSolutionPathOrError` (Z. 173-194).
- **Lock-Reihenfolge DoD 3**: Wird erst in EPIC-02 (Error-Methode)
  relevant. Der `McpCallLog`-Konstruktor + der `RecordEnd`-Lock bleiben
  in diesem Step unverändert; `RecordError` wird denselben `_writeLock`
  verwenden, aber das ist Out-of-Scope hier.
- **Test-Beobachtbarkeit**: Die neue `McpCallLog.LogPath`-Property ist
  klein (eine Zeile) und macht den
  `TryCreateCallLog_WhitespacePath_CreatesDefaultLog`-Test lesbar.
  Alternative Reflection-Variante wäre möglich, aber Reflection in
  Tests verletzt das Clean-Code-Prinzip.
- **`exeDir`-Ermittlung**: `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory`
  ist robust für die üblichen .NET-Host-Szenarien (apphost,
  `dotnet`-Runner). `Assembly.Location` kann in Single-File-Deployment
  anders aussehen, aber `McpServerOptionsFactory` (Z. 72-75) nutzt
  bereits dieselbe `Assembly.GetExecutingAssembly()`-Quelle für
  `GetServerVersion`, also ist das Muster im Projekt etabliert.
- **Test-Console**: `TestLintConsole` (`src/AiNetLinter.Tests/Output/TestLintConsole.cs`)
  ist `internal sealed` und sammelt `WriteError`-Aufrufe in
  `Errors: List<string>`. Im Test-Projekt bereits sichtbar (gleiche
  Solution). Keine neue Test-Infrastruktur nötig.
- **Strikte Trennung zu EPIC-02/03/04**: Bewusst werden weder
  `McpCallLog.RecordError` (EPIC-02) noch
  `McpServerOptionsBuilder`/Tool-Registrierungen (EPIC-03) noch
  Doku-Dateien (EPIC-04) angefasst. Das hält den Diff klein (DoD 5:
  keine breite Test-Änderung) und macht den Step-Review unkompliziert.
- **Konzept-Update-Konsequenz sichtbar gemacht**: Der
  `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`-Test
  ist nicht nur eine Code-Abdeckungs-Übung — er ist die
  **maschinenlesbare Dokumentation** der User-Entscheidung 2026-08-05
  („kein Fallback, harter Fehler"). Wenn in einem späteren Refactor
  jemand versehentlich wieder einen `ainetlinter-no-solution-...`-
  Fallback einbaut, schlägt dieser Test fehl und der Fehler wird im
  Review sofort sichtbar.
- **Begründung der `wasOptedIn`-Variable in `RunAsync`**: Ohne sie
  könnten wir nicht zwischen „Opt-in gar nicht aktiv" (`null`-Pfad
  → `null` zurück) und „Opt-in aktiv, aber Helper gescheitert"
  (`"   "`-Pfad → `null` zurück) unterscheiden. Beide Fälle liefern
  `null` aus `TryCreateCallLog`, aber nur der zweite ist ein Fehler.
  `args.McpLogPath is not null` ist der verlässliche Diskriminator.
