---
status: done
type: step-review
task: codegraph-mcp
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: claude-sonnet-5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-07-31T14:30:00Z
verdict: approved
tech_debt_ids: [TD-001, TD-002]
---

# Review Step 001: CLI-Einstiegspunkt --mcp-server + minimaler stdio-MCP-Server

## Verdict

- [x] **approved** — alle vier Prüfebenen ok

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `.agents/rules/AiNetLinterRichtlinien.mdc` §1/§2/§5, `.agents/rules/AiNetLinter.mdc` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, Tests aussagekräftig
- [x] Konzept-Treue: passt zur (bewusst reduzierten) Teil-Abdeckung von EPIC-01 aus `konzept.md`
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün

## Befund

Alle acht geplanten Dateien wie im Plan beschrieben geändert/angelegt, Tests vorhanden und grün. Kein `IServiceCollection`/Generic-Host im neuen Code, keine stdout-Verschmutzung im MCP-Pfad, kein leeres `catch`. Die drei im Step-Result dokumentierten Abweichungen/Unschärfen sind alle sachlich begründet und unkritisch (siehe unten).

### Plan-Erfüllung

Alle 8 "Konkrete Änderungen" (Dateien 1-8) verifiziert per Diff: NuGet-Paket, `CreateMcpServerOption`, `CliOptions`/`CliParsedArgs`-Felder, `CliCommandBuilder`-Verdrahtung, `LinterArgs.McpServer` + `HasStandaloneCommand()`-Erweiterung, `LinterErrorCodes.AmbiguousSolution`, `McpServerCommand.cs` (neu, `RunAsync`/`ResolveSolutionPathOrError`/`TryLoadSolutionAsync`), `Program.cs`-Fast-Path vor dem `# Run:`-Header — alles erfüllt. Tests (`McpServerCommandTests.cs`, 6 Fälle) decken Mehrdeutigkeits-Abbruch, kein Solution gefunden, cwd-Default, Einzelkandidat, kaputte `.slnx` und einen echten Subprozess-E2E-Test ab — deckt alle im Plan geforderten Testfälle ab.

### Rules-Konformität

`AiNetLinterRichtlinien.mdc` §2 ("kein DI-Container"): eingehalten — `McpServer.Create(transport, serverOptions)` + `StdioServerTransport(serverOptions)` sind Low-Level-API-Aufrufe ohne `IServiceCollection`/`Host.CreateEmptyApplicationBuilder`; die im Plan als Risiko benannte transitive DI-Infrastruktur des SDK-Pakets wird nirgends im eigenen Code instanziiert. `AiNetLinter.mdc`: `#nullable enable` vorhanden, Methoden kurz (`RunAsync` 12 Zeilen, `ResolveSolutionPathOrError` ~20 Zeilen), kein leeres `catch` (`TryLoadSolutionAsync` fängt `Exception` außer `OperationCanceledException`, loggt via `console.WriteError`), max. 3 Parameter bei `RunAsync`, kein `bool`-Parameter außer dem etablierten Muster. `ILintConsole.WriteError` verifiziert (`Output/LinterConsole.cs:14`) — schreibt auf `Console.Error`, damit ist die im Plan geforderte stdout-Sauberkeit für den gesamten Pfad **vor** Server-Start tatsächlich gegeben, auch ohne direkten `Console.Error.WriteLine`-Aufruf wie im Plan-Snippet vorgeschlagen.

### Logische Korrektheit

`Program.cs`-Fast-Path liegt korrekt vor dem `# Run:`-Header-Print und vor der übrigen Kommando-Kette. Mehrdeutigkeits-/Nicht-gefunden-Fälle sauber über `switch`-Expression auf Kandidatenzahl abgebildet. `TryLoadSolutionAsync` verwendet zusätzlich `catalog.HasLoadingErrors` für einen Warnhinweis bei geladener, aber fehlerbehafteter Solution — eine sinnvolle Ergänzung über den Plan hinaus, konsistent mit der Konzept-Intention "Ladefehler crasht nicht". Der End-to-End-Test startet tatsächlich einen echten Subprozess und verbindet per SDK-Client — eine belastbare Ersatzverifikation für den nicht durchführbaren manuellen Test.

### Konzept-Treue (Ebene 4)

Deckt exakt den im Plan explizit abgegrenzten Teil von EPIC-01 ab (Flag, Command, Paket, Mehrdeutigkeits-Abbruch, startender leerer Server) und lässt den ebenfalls im Plan explizit ausgeklammerten Rest (resident/zustandsvoller Server, Staleness-Cache, EPIC-02) unangetastet — kein Scope-Creep, kein Non-Goal aus `konzept.md` umgesetzt (kein Editier-Tool, kein DI-Container, kein Plugin-System).

### Build-/Test-Status

```
dotnet build AiNetLinter.slnx → grün (0 Warnungen, 0 Fehler)
dotnet test AiNetLinter.slnx  → grün (1021 Tests, 0 Fehler)
```

## Sonstige Beobachtungen / MINOR / NITPICK

- `ResourceNotFound` wird für zwei fachlich unterschiedliche Fälle wiederverwendet ("Pfad existiert nicht" vs. "kein `.sln`/`.slnx` im Verzeichnis") — Plan überließ diese Detailwahl bewusst dem Coder, beide Fälle liefern trotzdem eine strukturierte, unterscheidbare Fehlermeldung (unterschiedlicher `message`-Text). Kosmetisch, kein Fix nötig.
- `ServerInfo.Version` liefert zur Laufzeit `1.0.78.0` (vierte Komponente `.0`) statt exakt `1.0.78` wie in `AiNetLinter.csproj` — rein informatives `initialize`-Metadatenfeld, kein funktionaler Vertrag darauf.

## Tech-Debt-Einträge aus diesem Review

- `TD-001` (siehe `tech-debt.md`) — Ungenutzte transitive `Microsoft.Extensions.AI.Abstractions`-Abhängigkeit über das `ModelContextProtocol`-Paket, relevant für spätere Footprint-Tools (EPIC-04).
- `TD-002` (siehe `tech-debt.md`) — Subprozess-basierter E2E-Test ohne Fixture-Pool, relevant falls EPIC-07 weitere MCP-Subprozess-Tests ergänzt.
