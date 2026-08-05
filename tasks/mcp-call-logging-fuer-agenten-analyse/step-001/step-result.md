---
status: done (pending audit)
type: step-result
task: mcp-call-logging-fuer-agenten-analyse
step: 001
coded_by_model: MiniMax-M3
coded_by_model_knowledge_cutoff: 2026-01
---

# Step 001: Result

## Zusammenfassung

Default-Pfad-Konvention für `--mcp-log` umgesetzt. `--mcp-log` ohne Wert
erzeugt jetzt `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`;
bei nicht auflösbarer Solution bricht `--mcp-server` mit
`RESOURCE_NOT_FOUND` und Exit 1 ab, statt einen Fallback-Pfad zu wählen
(User-Entscheidung 2026-08-05).

## Geänderte Dateien

- `src/AiNetLinter/Cli/CliOptionFactory.cs` — `ArgumentArity.ZeroOrOne` an
  `--mcp-log`-Option gesetzt, damit das Flag ohne Wert parserfähig wird.
- `src/AiNetLinter/Commands/McpServerCommand.cs` — `using System.Reflection;`
  ergänzt; `BuildDefaultLogPath` als neuer `internal static` Helper nach
  `*OrError`-Muster (`string?`-Rückgabe + `LinterErrorFormatter`-Ausgabe);
  `TryCreateCallLog`-Signatur um `solutionPath: string?`,
  `exeDir: string`, `console: ILintConsole` erweitert (3 → 4 Parameter, am
  Limit aber vertretbar gem. Plan §Rules-Refs); `RunAsync` blockt mit
  `exeDir`-Ermittlung via `Assembly.GetExecutingAssembly()` + `wasOptedIn`-
  Diskriminator und `return 1` bei Opt-in ohne erfolgreich konstruierten
  Default-Pfad. XML-Doc-Kommentare auf die neue Drei-Fälle-Semantik
  angepasst, `ResolveMcpLogPath` mit Hinweis "nur fuer explizite Pfade"
  versehen.
- `src/AiNetLinter/Cli/LinterArgs.cs` — XML-Doc auf `McpLogPath`
  erweitert: dokumentiert jetzt die drei Fälle
  (null/leer/Pfad) und die harte Abbruchbedingung bei fehlender
  Solution. Stil an die Datei-Konvention angepasst
  (ASCII-Transliteration, `Exit ungleich 0` statt `Exit ≠ 0`).
- `src/AiNetLinter/Mcp/McpCallLog.cs` — `internal string LogPath { get; }`
  als read-only Zugriff auf den privaten `_logPath` hinzugefügt
  (Test-Beobachtbarkeit).
- `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` —
  `TryCreateCallLog_WhitespacePath_ReturnsNull` gelöscht; die drei
  bestehenden `TryCreateCallLog`-Tests auf die neue Signatur umgestellt
  (Dummy-`exeDir` + `TestLintConsole`); neu: `TryCreateCallLog_WhitespacePath_CreatesDefaultLog`,
  `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull`,
  `BuildDefaultLogPath_WithSolution_IncludesSolutionName`,
  `BuildDefaultLogPath_DateIsLocal`.

## Commits

- **Code-Commit:** `1cefdce0dadde63bdc2748b572f6c9f2ad0b93aa` —
  `feat: Default-Pfad-Konvention fuer --mcp-log Opt-in [mcp-call-logging-fuer-agenten-analyse]`
- **Doku-Commit:** siehe unten (wird im Anschluss erstellt).

## Build- und Test-Output

- `dotnet build` — 0 Warnungen, 0 Fehler, ~5 s
- `dotnet test --filter FullyQualifiedName~McpServerCommandCallLogTests` — 9/9 grün (44 ms)
- `dotnet test --filter FullyQualifiedName~McpCallLogTests` — 5/5 grün (unverändert, 95 ms)
- `dotnet test` (Volllauf) — 1270/1270 grün, 1 min 47 s
- `git grep -i "ainetlinter-no-solution" -- <geänderte Dateien>` — leer (DoD erfüllt)

## Abweichungen vom Plan

Keine inhaltlichen Abweichungen. Stilistische Mini-Anpassung:
`LinterArgs.McpLogPath` XML-Doc nutzt im Kommentar die im Rest der
Datei etablierte ASCII-Transliteration (`auflösbar` → `aufloesbar`,
`Exit ≠ 0` → `Exit ungleich 0`) statt der im Plan zitierten Umlaute/≠.
Funktional gleichwertig, semantisch identisch. `EnforceAsciiIdentifiers`
betrifft zwar nur Identifier, nicht Kommentare, aber der
Dateikonsistenz wegen habe ich es angeglichen — der Plan formuliert
seine Wörtlich-Zitate als „auf die neue XML-Doc-Semantik anpassen",
nicht als „diese exakten Buchstaben übernehmen".

## Beobachtungen

Diese Beobachtungen sind NICHT Teil des Step-Scopes, könnten aber
für spätere Schritte relevant sein (sie gehören in den Tech-Debt-
Review des Kritikers, nicht in diesen Step):

- **`Bekannte Ausnahmen` im Plan, Punkt "Defense in depth":** der
  `wasOptedIn && callLog is null`-Check in `RunAsync` ist in der
  Praxis unerreichbar, weil `ResolveSolutionPathOrError` weiter oben
  bereits bei null-Solution mit `return 1` abbricht. Der Plan
  dokumentiert das ausdrücklich als bewusste Designentscheidung
  (Helper-Kontrakt-Absicherung), nicht als Lücken-Fix. Es bleibt also
  so, kein Eingriff nötig. Erwähne ich hier nur, damit es im Review
  nicht als vermeintlich redundanter Code auffällt.
- **`LinterArgs.McpLogPath`:** die Property wird in den Tests nie
  direkt ausgelesen (immer nur via `args.McpLogPath` an
  `McpServerCommand.TryCreateCallLog` weitergereicht). Der XML-Doc-
  Change ist trotzdem sinnvoll, weil `LinterArgs` als public
  API-Vertrag gilt. Kein Code-Change, kein Test-Change nötig.
- **`McpServerCommandCallLogTests` cleanup-Helper:** `TryDelete` ist
  best-effort, was bei parallelen CI-Läufen auf derselben Temp-Pfad-
  Struktur in seltenen Fällen zu "directory not empty"-Fehlern beim
  Cleanup führen kann. Im Test-Code schon vorher so gehandhabt (auch
  die anderen 4 `McpCallLogTests` nutzen dasselbe Muster), daher kein
  Step-spezifisches Issue.
- **`McpCallLog.LogPath` als `internal`:** ist test-only. Sollte in
  EPIC-04 mit den übrigen Public-API-Diskussionen (Docs-Update) ggf.
  nochmal angeschaut werden — für reine Test-Beobachtbarkeit ist
  `internal` korrekt, aber bei einer Erweiterung der
  `McpCallLog`-API in EPIC-02/03 könnte die Sichtbarkeit neu zu
  bewerten sein. Nicht in diesem Step entscheiden.
- **Description-Text von `--mcp-log`:** der Plan hält fest, dass die
  Description in `CliOptionFactory` bewusst NICHT in diesem Step
  angefasst wird (Doku-Sammel-Step EPIC-04). Inhaltlich inkonsistent
  zur neuen Semantik (Description redet noch von expliziten Pfaden
  als einzigem Anwendungsfall), aber konsistent mit dem Scope.
- **`Tasks/mcp-call-logging-fuer-agenten-analyse/roadmap.md` Zeile 61**
  hat eine zweite, widersprüchliche Roadmap-Notiz zum Test-Scope
  (Roadmap sagt "ersetzt/erweitert die zwei betroffenen Tests",
  Schritt-Plan stellt korrekt klar: nur 1 LÖSCHT, 4 NEU, 3 ANGEPASST).
  Step-Plan hat die korrekte Lesart — Roadmap-Notiz ist Out-of-Scope
  für diesen Step (Doku-Sammel-Step).

## Bekannte Unschärfen

- `BuildDefaultLogPath_DateIsLocal` und
  `TryCreateCallLog_WhitespacePath_CreatesDefaultLog` sind
  tagesrand-anfällig: wenn die Tests knapp vor Mitternacht laufen,
  könnte `DateTime.Now` im Helper einen anderen Tag liefern als im
  Test (Plan §Bekannte Ausnahmen). Mitigation: `today` wird im
  Test mit `DateTime.Now` ermittelt, beide Aufrufe liegen in der
  gleichen Code-Zeile. Risiko akzeptabel wie im Plan dokumentiert.
- Die Hilfsmethode `MakeExeDir` in `McpServerCommandCallLogTests`
  erzeugt für jeden Test einen frischen Pfad unter `Path.GetTempPath()`
  (via `Guid.NewGuid().ToString("N")`), der **nicht** aufgeräumt wird.
  Andere Tests in der Datei machen das genauso (siehe
  `mcp-log-rel-...`/`mcp-log-abs-...`-Pattern) — der
  Verzeichnisname enthält `mcp-log-exe-` als Marker, falls jemand
  einmal aufräumen muss. Kein DoD-Verstoß, nur eine Beobachtung.

## Modell-Info

- `coded_by_model`: MiniMax-M3
- `coded_by_model_knowledge_cutoff`: 2026-01
