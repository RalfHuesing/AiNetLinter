---
status: done
type: step-result
task: codegraph-mcp
step: 001
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-07-31T12:00:00Z
code_commit_hash: 3ae6230
status_after: done
blocker_category: n/a
---

# Result Step 001: CLI-Einstiegspunkt --mcp-server + minimaler stdio-MCP-Server

## Zusammenfassung

`--mcp-server` ist als neues Bool-Flag durch die komplette CLI-Options-Pipeline
verdrahtet und löst in `Program.cs` einen schnellen Pfad **vor** dem
`# Run: ...`-stdout-Header aus. `Commands/McpServerCommand.cs` (neu) löst die
Ziel-Solution auf (Datei/Verzeichnis/cwd-Default), bricht bei mehreren
`.sln`/`.slnx`-Kandidaten mit `[ERROR]: AMBIGUOUS_SOLUTION` ab, lädt die
Solution best-effort (Ladefehler → `[WARN]` auf `Console.Error`, kein Absturz)
und startet danach einen stdio-MCP-Server über die Low-Level-API des
`ModelContextProtocol`-SDK (`StdioServerTransport` + `McpServer.Create`, kein
`IServiceCollection`/Generic-Host) mit leerem Tool-Set. Alle 6 neuen Tests
grün, inkl. eines echten End-to-End-Tests, der die gebaute `AiNetLinter.exe`
als Subprozess startet und per `ModelContextProtocol.Client` (`tools/list`)
verbindet.

## Geänderte Dateien

- `src/AiNetLinter/AiNetLinter.csproj` — `PackageReference Include="ModelContextProtocol" Version="2.0.0"` ergänzt (via `dotnet add package` aufgelöst).
- `src/AiNetLinter/Cli/CliOptionFactory.cs` — `CreateMcpServerOption()` (Bool-Flag `--mcp-server`, kein Alias).
- `src/AiNetLinter/Cli/CliOptions.cs` — `McpServer`-Feld an `CliOptions` und `CliParsedArgs` angehängt.
- `src/AiNetLinter/Cli/CliCommandBuilder.cs` — `Build()`/`CreateOptions()`/`Parse()` um `McpServer` erweitert.
- `src/AiNetLinter/Cli/LinterArgs.cs` — `McpServer`-Property, `HasStandaloneCommand()` um `|| McpServer` erweitert (verhindert `--path`-Pflicht auch falls `Validate()` je aufgerufen würde).
- `src/AiNetLinter/Output/LinterErrorCodes.cs` — neue Konstante `AmbiguousSolution = "AMBIGUOUS_SOLUTION"`.
- `src/AiNetLinter/Commands/McpServerCommand.cs` (neu) — `RunAsync`, `ResolveSolutionPathOrError` (internal, reine Funktion, kein I/O jenseits `Directory`/`File`), `TryLoadSolutionAsync` (internal, für Tests exponiert), Server-Start über SDK-Low-Level-API.
- `src/AiNetLinter/Program.cs` — `ToLinterArgs` um `McpServer` erweitert; in `Main` neuer Fast-Path `if (linterArgs.McpServer) return await McpServerCommand.RunAsync(...)` **vor** dem stdout-Header-Print.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (neu) — 6 Tests: Mehrdeutigkeits-Abbruch (2 `.slnx`), kein Solution gefunden, `--path` fehlt → cwd, einzelner Kandidat, kaputte `.slnx` (kein Crash, `[WARN]` geloggt), End-to-End über echten Subprozess + `ModelContextProtocol.Client` (`tools/list` liefert leeres Array).

## Commit

- **Code-Commit-Hash:** `3ae6230`
- **Message:**
  ```
  feat: add --mcp-server CLI entry point with minimal stdio MCP server [codegraph-mcp]

  Adds ModelContextProtocol NuGet package, new --mcp-server flag wired
  through the CLI options pipeline, and Commands/McpServerCommand.cs.
  Resolves the target solution via --path (file, directory auto-search,
  or cwd default), aborts with a structured [ERROR] AMBIGUOUS_SOLUTION
  when multiple .sln/.slnx candidates exist, loads the solution
  best-effort (load failure only logs a warning, no crash), and starts
  a stdio MCP server (low-level SDK API, no DI container/Generic Host)
  with an empty tool set. Program.cs dispatches to the new fast path
  before the stdout "# Run:" header to keep stdout clean for the
  JSON-RPC transport.

  Refs: tasks/codegraph-mcp/step-001
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx → grün (0 Warnung(en), 0 Fehler)
dotnet test AiNetLinter.slnx  → grün (1021 Tests, 0 Fehler, davon 6 neu in McpServerCommandTests.cs)
```

## Abweichungen vom Plan

- **Tatsächliche SDK-API weicht in Details vom Skizzen-Snippet im Plan ab**
  (Plan-Notes hatten das explizit als möglich angekündigt): Es gibt kein
  `McpServerFactory.Create(...)`, sondern eine statische Methode
  `McpServer.Create(ITransport, McpServerOptions, ILoggerFactory? = null, IServiceProvider? = null)`
  auf der Klasse `ModelContextProtocol.Server.McpServer` selbst (Rückgabetyp
  `McpServer`, implementiert `IAsyncDisposable`, `RunAsync(CancellationToken)`
  wie erwartet). `StdioServerTransport` hat den erwarteten Konstruktor
  `(McpServerOptions, ILoggerFactory? = null)`. Die grundsätzliche Linie
  "kein `IServiceCollection`/Generic-Host-Builder" ist eingehalten — die
  gesamte DI-Infrastruktur (`Microsoft.Extensions.DependencyInjection.*`,
  `AddMcpServer()`, `WithStdioServerTransport()`) liegt ausschließlich im
  Haupt-`ModelContextProtocol`-Paket (2.0.0) und wurde nicht angefasst; alle
  genutzten Typen (`McpServer`, `McpServerOptions`, `StdioServerTransport`,
  `Implementation`, `ServerCapabilities`) stammen aus dem transitiv
  referenzierten `ModelContextProtocol.Core` (2.0.0).
- **`McpServerOptions.ToolCollection` wird explizit gesetzt** (leere
  `McpServerPrimitiveCollection<McpServerTool>()`), statt es auf dem
  SDK-Default `null` zu belassen — verifiziert per Reflection, dass sowohl
  `ToolCollection` als auch `Capabilities` standardmäßig `null` sind. Ohne
  explizite (leere) `ToolCollection` bestand das Risiko, dass der Server die
  `tools`-Capability gar nicht meldet und `tools/list` fehlschlägt statt ein
  leeres Array zu liefern — das würde der Intention "meldet über `tools/list`
  ein (noch) leeres Tool-Array" widersprechen. Der End-to-End-Test bestätigt
  das erwartete Verhalten (leeres Array, kein Fehler).
- **`TryLoadSolutionAsync` ist `internal` statt `private`** (einzige
  Abweichung von der Methodensichtbarkeit im Code-Skizze-Snippet), damit der
  "kaputte `.slnx` crasht nicht"-Testfall direkt gegen die Methode statt
  gegen einen vollständigen (endlos laufenden) Server-Start getestet werden
  kann — konsistent mit der im Plan selbst geforderten Testbarkeits-Linie für
  `ResolveSolutionPathOrError`.
- Ansonsten 1:1 wie geplant umgesetzt.

## Beobachtungen

- `ModelContextProtocol` 2.0.0 zieht `Microsoft.Extensions.AI.Abstractions`
  10.8.3 als transitive Abhängigkeit mit (für `SampleAsync`/`IChatClient`
  o.ä. auf `McpServer`, hier ungenutzt). Kein Rules-Verstoß, aber
  Erwähnenswert für spätere Footprint-Betrachtungen (EPIC-04
  `get_hotspots`/Footprint-Tools laufen ggf. gegen diese zusätzliche
  Abhängigkeit).
- Der End-to-End-Test (`RunAsync_ValidFixture_ServerRespondsWithEmptyToolList`)
  startet einen echten Subprozess (`AiNetLinter.exe`) und dauert dadurch
  spürbar länger als die übrigen Unit-Tests (Prozessstart + MSBuildLocator-
  Registrierung + Solution-Load der Mini-Fixture). Bei den 1021 Gesamttests
  fällt das nicht ins Gewicht, aber falls EPIC-07 (Tests) weitere
  Subprozess-basierte MCP-Integrationstests ergänzt, lohnt sich ggf. ein
  gemeinsamer Fixture-Prozess-Pool statt eines Subprozesses pro Testfall.

## Bekannte Unschärfen

- Der DoD-Punkt "`ainetlinter --mcp-server --path <Solution>` lässt sich
  manuell starten und mit einem MCP-Client verbinden" wurde **nicht manuell**
  (kein interaktiver MCP-Client zur Hand), sondern ausschließlich über den
  automatisierten End-to-End-Test verifiziert (echter Subprozess + echter
  `ModelContextProtocol.Client`, kein In-Memory-Mock) — wie im Plan als
  Ersatz vorgesehen.
- `ResourceNotFound` (bestehender Fehlercode) wird sowohl für "Pfad existiert
  nicht" als auch für "kein `.sln`/`.slnx` im Verzeichnis gefunden" genutzt;
  der Plan überließ die Detailwahl explizit dem Coder ("neuer Code z. B.
  `AMBIGUOUS_SOLUTION`... Planer überlässt die Detailwahl dem Coder" nur für
  den 0-Treffer-Fall). Falls der Kritiker einen eigenen Code für "0 Treffer"
  vs. "Pfad existiert nicht" für sinnvoller hält, wäre das eine kleine
  Folgeänderung.
- Servertitel/-version: `ServerInfo.Version` wird zur Laufzeit aus
  `Assembly.GetExecutingAssembly().GetName().Version` gelesen (nicht als
  String-Literal aus der `.csproj` dupliziert) — liefert `1.0.78.0` (mit
  vierter Komponente `.0`) statt exakt `1.0.78` wie in `AiNetLinter.csproj`.
  Funktional unkritisch (reines Metadatenfeld im `initialize`-Response), aber
  falls ein exaktes String-Match auf `1.0.78` erwartet wird, wäre das ein
  Punkt für den Kritiker.

## Falls Status `blocked`

Nicht zutreffend — Status `done (pending audit)`.
