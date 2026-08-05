---
status: done
type: step-review
task: mcp-call-logging-fuer-agenten-analyse
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: MiniMax-M3
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-05T12:55:00+02:00
verdict: approved
tech_debt_ids: [TD-001]
---

# Review Step 001: Default-Pfad-Konvention für `--mcp-log` Opt-in (harter Fehler bei fehlender Solution)

## Verdict

- [x] **approved** — alle vier Prüfebenen ok
- [ ] **issues** — Fix-Step `step-001/fix-XX` angelegt mit Fix-Plan
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/AiNetLinterRichtlinien.mdc` und `.agents/rules/AiNetLinter.mdc` eingehalten (siehe Details)
- [x] Logische Korretheit: Code macht was er soll, Tests sind aussagekräftig (Details siehe Befund)
- [x] Konzept-Treue: passt zu `konzept.md` (Muss-Haben 1–3 umgesetzt, Non-Goal „Fallback-Pfad" respektiert, User-Korrektur 2026-08-05 sauber umgesetzt)
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün (1270/1270 Volllauf)

## Befund

### Plan-Erfüllung
Alle fünf im Plan genannten Datei-Änderungen umgesetzt (CliOptionFactory, McpServerCommand, LinterArgs, McpCallLog, McpServerCommandCallLogTests) mit den jeweils spezifizierten Inhalten. Diff-Statistik passt (5 Dateien, 160/33 Zeilen). Test-Scope wie geplant: 1 Test gelöscht (`TryCreateCallLog_WhitespacePath_ReturnsNull`), 3 Tests auf neue Signatur umgestellt, 4 Tests neu (3 für Pfad-Konstruktion/Helper + 1 für Failure-Pfad-Dokumentation der User-Korrektur 2026-08-05). XML-Doc-Semantik an allen drei betroffenen Stellen (`TryCreateCallLog`, `BuildDefaultLogPath`, `McpLogPath`, plus `ResolveMcpLogPath`-Hinweis) konsistent auf Drei-Fälle-Modell erweitert. `using System.Reflection;` korrekt ergänzt. Commit-Body mit `Refs:`-Trailer vorhanden. Konventioneller `feat:`-Commit-Header mit Pflicht-Suffix `[mcp-call-logging-fuer-agenten-analyse]` und Subject ≤72 Zeichen.

### Rules-Konformität
**`AiNetLinterRichtlinien.mdc`:** §2 Architektur-Verbote eingehalten — `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory` ist statische Ermittlung ohne DI/ALC (Muster identisch zu `McpServerOptionsFactory.cs:72-75`). §3 Windows — `Path.Combine` für Pfad-Konstruktion, keine Hardcoding, `dotnet build`/`dotnet test` verwendet. §4 Updates/Tests — xUnit-v3-Tests, Commit-Vorschlag am Ende der Antwort (nicht in step-review prüfbar, aber `Refs:`-Trailer im Commit vorhanden); Doku-Dateien (Docs/, README.md, rules.json) korrekt ausgenommen (EPIC-04). §5 Zero-Warning — `dotnet build` ergibt 0 Warnungen/0 Fehler bei `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Clean-Code-Kommentar-Politik — keine Task-/Step-/EPIC-/Konzept-Verweise in C#-Code, XML-Docs beschreiben das Was/Wie der API, nicht den Refactoring-Anlass. Result-Pattern — `BuildDefaultLogPath` folgt dem etablierten `*OrError`-Muster (string? + LinterErrorFormatter), `TryCreateCallLog` bleibt `Try*` mit `T?`-Rückgabe; **kein** neuer `Result<T>`-Typ eingeführt (passt zur Konvention im Projekt, die gar keinen `Result<T>` hat).

**`AiNetLinter.mdc`:** Kurz-Stil — `McpCallLog` ist `internal sealed class`, alle vier modifizierten Produktiv-Dateien haben `#nullable enable` in Zeile 1. `MaxMethodLineCount` (≤60 Produktion, ≤100 Tests) eingehalten: längste Produktiv-Methode `RunAsync` 49 Zeilen, längste Test-Methode `TryCreateCallLog_RelativePath_…` 26 Zeilen. `MaxMethodParameterCount` (≤4) eingehalten: `BuildDefaultLogPath` 3, `TryCreateCallLog` 4 (am Limit, aber durch Plan legitimiert). `MaxConstructorDependencies`/`MaxPublicMembersPerType` nicht verletzt (McpCallLog hat 6 sichtbare Member, weit unter 15). `EnforceNamespaceDirectoryMapping` — McpServerCommand.cs in `src/AiNetLinter/Commands/`, Namespace `AiNetLinter.Commands`. `EnforceNoSilentCatch` — keine neuen `catch`-Blöcke; das einzige `catch` in der Test-Datei (`TryDelete`) ist pre-existing. `EnforceSealedClasses` — `McpCallLog` ist `sealed`. `EnforceAsciiIdentifiers` — alle neuen Identifier (`BuildDefaultLogPath`, `LogPath`, `MakeExeDir`) sind reines ASCII; Umlaute/≠ tauchen nur in Kommentaren/XML-Docs auf.

### Logische Korretheit
Die Drei-Fälle-Semantik ist sauber implementiert: `null` → `null` ohne Konsolen-Output, `IsNullOrWhiteSpace` → `BuildDefaultLogPath` (mit Failure-Signalisierung über stderr + `return null`), expliziter Pfad → `ResolveMcpLogPath`. `BuildDefaultLogPath` schreibt den Fehler via `LinterErrorFormatter.Format(LinterErrorCodes.ResourceNotFound, …, hint: …)` auf stderr — passt 1:1 zum etablierten Muster in `ResolveSolutionPathOrError` (`McpServerCommand.cs:222-225` und 247-251). Der `wasOptedIn`-Diskriminator in `RunAsync` unterscheidet korrekt die zwei `null`-Rückgaben: Plan-Doku bezeichnet ihn selbst als „in der Praxis unerreichbar" wegen vorherigem `ResolveSolutionPathOrError`-Guard, aber er ist die explizit gewollte Helper-Kontrakt-Absicherung — kein Symptom-Fixing, sondern semantisch tragend.

**Adversariell geprüfte Edge-Cases:**
- `solutionPath = ".slnx"` (nur Extension) → `Path.GetFileNameWithoutExtension(".slnx") = ""` → `IsNullOrWhiteSpace(solutionName)` triggert `console.WriteError(...)` und `return null`. Korrekt.
- `solutionPath = "/abs/path/MyApp.slnx"` (absolut) → `Path.GetFileNameWithoutExtension` extrahiert nur den Dateinamen-Anteil „MyApp". Korrekt.
- `solutionPath = null` → direkter `IsNullOrWhiteSpace`-Fail-Branch ohne Reflection-Zugriff. Korrekt.
- `exeDir = "C:\\Program Files\\ainet"` (Backslashes) → `Path.Combine` handhabt das plattformkonform. Korrekt.
- `args.McpLogPath = ""` (leerer String, nicht Whitespace) → `IsNullOrWhiteSpace("") = true` → Default-Pfad. Korrekt (Whitespace-Test deckt das mit `"   "` ab, semantisch identisch).
- `McpCallLog`-Konstruktor-Aufruf innerhalb des `try`-Blocks in `RunAsync` (Z. 67) — bei IO-Fehler (Disk voll, read-only) wird die Exception sauber durch den `finally`-Block propagiert (Dispose-Pfad) und nicht verschluckt.

Tests sind aussagekräftig: `TryCreateCallLog_WhitespacePath_CreatesDefaultLog` verifiziert die volle Pfad-Konstruktion (`Path.Combine(exeDir, "logs", "Only", today, "calls.jsonl")`), `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull` prüft die Fehler-Signalisierung mit `Assert.Contains("[ERROR]:")` und `Assert.Contains("RESOURCE_NOT_FOUND")` (text-tolerant gegen Tweak), `BuildDefaultLogPath_DateIsLocal` verifiziert `DateTime.Now`-Format (nicht UTC). Der `MakeExeDir`-Helper erzeugt eindeutige Pfade via `Guid.NewGuid().ToString("N")` — keine Parallelitäts-Kollisionen. `TryDelete` ist best-effort mit `catch`-Block, konsistent mit dem Pre-Existing-Pattern der anderen 4 Tests.

### Konzept-Treue (Ebene 4)
**Muss-Habe 1 (Default-Pfad):** Umgesetzt — `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` korrekt konstruiert. Verzeichnisse werden automatisch via `McpCallLog`-Konstruktor (`McpCallLog.cs:36-37`) via `Directory.CreateDirectory(dir)` angelegt — keine zusätzliche IO nötig.

**Muss-Habe 2 (Kein Fallback, harter Abbruch):** User-Korrektur 2026-08-05 sauber umgesetzt. `BuildDefaultLogPath` liefert `null` + `RESOURCE_NOT_FOUND` auf stderr, wenn `solutionPath` null/leer ist oder `Path.GetFileNameWithoutExtension` einen leeren String zurückgibt. `RunAsync` macht bei `wasOptedIn && callLog is null` `return 1` (kein Server-Start, keine Log-Datei). Der Test `TryCreateCallLog_WhitespacePathNoSolution_WritesErrorAndReturnsNull` ist die maschinenlesbare Dokumentation dieser Entscheidung — schlägt fehl, falls jemand je wieder einen `ainetlinter-no-solution-…`-Fallback einbaut. `git grep "ainetlinter-no-solution"` über alle 5 geänderten Dateien liefert keinen Treffer.

**Muss-Habe 3 (Datum lokal):** `DateTime.Now.ToString("yyyy-MM-dd")` (lokale Zeitzone, nicht UTC) — korrekt. Test `BuildDefaultLogPath_DateIsLocal` verifiziert das.

**Out-of-Scope (korrekt zurückgestellt):** Muss-Habe 4 (`RecordError`), Muss-Habe 5 (Error-Hook), Doku (`Docs/agent-api.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`), DoD-1-7-Verifikation — alle korrekt in EPIC-02/03/04 delegiert. Description-Text von `--mcp-log` in `CliOptionFactory.cs:232` ist semantisch jetzt irreführend („Default: deaktiviert"), aber Plan dokumentiert das explizit als EPIC-04-Sache.

**Non-Goals:** Kein `AssemblyLoadContext`, kein DI-Container, keine Plugin-Architektur, keine Drittanbieter-Logger, keine Log-Rotation, keine Opt-out-Umkehr — alle respektiert.

### Build-/Test-Status

```
dotnet build                                              → grün (0 Warnungen, 0 Fehler, ~3 s)
dotnet test --filter FullyQualifiedName~McpServerCommandCallLogTests → grün (9/9, 41 ms)
dotnet test --filter FullyQualifiedName~McpCallLogTests              → grün (5/5, 84 ms)
dotnet test --filter Category=Unit                                    → grün (128/128, 18 s)
dotnet test (Volllauf)                                                → grün (1270/1270, 1 min 54 s)
git grep -i "ainetlinter-no-solution" -- <geänderte Dateien>          → leer (DoD erfüllt)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **Bekannte Mini-Abweichung vom Plan (vom Coder transparent gemacht):** `LinterArgs.McpLogPath` XML-Doc nutzt `Exit ungleich 0` statt `Exit ≠ 0` und `aufloesbar` statt `auflösbar`. Begründung: ASCII-Transliteration ist das etablierte Muster in dieser Datei (`Ausfuehrung`, `Deaktivierungskommentare`, `ausgefuehrt`, `Loesungs-…` etc. — durchgehend). `EnforceAsciiIdentifiers` betrifft zwar nur Identifier, nicht Kommentare, aber die Datei-Konvention ist konsistent ASCII. Plan formuliert XML-Doc-Wörtlich als „auf die neue Semantik anpassen" (nicht „diese exakten Buchstaben übernehmen"). Funktional und semantisch gleichwertig. **Kein Finding** — bewusster Stilgriff, im Einklang mit Datei-Konvention.
- **Test-Reihenfolge:** Tests sind alphabetisch/geordnet. `TryCreateCallLog_PathNotSet_ReturnsNull` enthält jetzt zusätzlich `Assert.Empty(console.Errors)` — gute Beobachtbarkeits-Verbesserung, kein Nachteil.
- **Bekannte Tech-Debt-Beobachtung des Coders** (bereits in `step-result.md` §Beobachtungen dokumentiert): `McpCallLog.LogPath` als `internal` — bewusste Test-Beobachtbarkeit, korrekt für step-001; bei EPIC-02/03-API-Erweiterung ggf. neu zu bewerten. Wird hier nicht als Tech-Debt aufgenommen, da bewusste Designentscheidung mit klarem Re-Eval-Pfad in EPIC-04.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — `tasks/mcp-call-logging-fuer-agenten-analyse/roadmap.md:61` widersprüchliche Test-Scope-Notiz („ersetzt/erweitert die zwei betroffenen Tests") widerspricht der im step-001-Plan korrigierten Lesart (1 LÖSCHT, 4 NEU, 3 ANGEPASST); wurde bereits im step-001-Plan richtiggestellt, aber die Roadmap-Notiz nicht mitgezogen.
