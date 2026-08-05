---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-05T11:30:00+02:00
open_questions:
  - "Backward-Compat: --mcp-log bleibt Override (Annahme) oder wird deprecated/ersetzt?"
  - "solutionName-Token bei nicht auflösbarer Solution: Fallback 'default' (Annahme) oder exe-Name oder Fehler?"
  - "Greift Default-Logik auch in anderen Modi (Lint, Audit) oder nur MCP-Server (Annahme: nur MCP)?"
---

# Konzept: Default-MCP-Call-Logging mit Pro-Solution/Pro-Tag-Pfadstruktur

## Ziel (Was)

Der MCP-Server soll **ohne explizites Flag** ein Call-Log inklusive JSON-RPC-Errors unter einem vorhersagbaren, beschreibbaren Pfad schreiben. Default: `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`. Das bestehende `--mcp-log`-Flag bleibt als expliziter Override erhalten.

## Warum / Kontext

`--mcp-log` ist aktuell Opt-in und im Default off (`CliOptionFactory.cs:230`). In der Praxis wird der MCP-Server häufig ohne dieses Flag gestartet (z. B. global in Claude Desktop registriert), wodurch keinerlei Persistenz der Tool-Calls existiert. Diagnose von Fehlern wie *"An error occurred invoking 'get_file_skeleton'"* ist dann nicht möglich: weder der erfolgreiche Call-Pfad (`McpCallLog.RecordEnd`) noch der JSON-RPC-Error-Pfad schreiben etwas auf die Platte. Beide Pfade müssen persistiert werden, sonst bleibt die Diagnose-Lücke halb geschlossen.

## Scope

### Muss-Haben

- **Default-Pfad aktiviert**: Ohne `--mcp-log` schreibt der Server automatisch nach `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`. Verzeichnisse werden automatisch angelegt.
- **Harter Fehler bei read-only `<exeDir>`**: Beschreibbarkeitsprüfung im Pfad-Builder; bei IO-Exception Exit-Code != 0 mit klarer Meldung auf stderr inkl. Workaround-Hinweis `--mcp-log <pfad>`.
- **JSON-RPC-Errors im Log**: Jeder Tool-Call, der mit RPC-Error oder unbehandelter Exception endet, wird mit `level=error`, `error_type`, `error_message`, `stack_trace` ins selbe Log geschrieben. Damit wird die ursprüngliche Beobachtung *"An error occurred invoking 'get_file_skeleton'"* reproduzierbar.
- **`<solutionName>`-Token**: Dateiname der Solution ohne Extension (`MyApp.slnx` → `MyApp`). Fallback siehe `open_questions`.
- **Datum lokal**: `yyyy-MM-dd` in der lokalen Zeitzone des Servers (nicht UTC), damit "heute" intuitiv ist.
- **Backward-Compat `--mcp-log`**: bestehendes Flag bleibt funktional mit aktueller Pfad-Auflösung (absolut oder relativ zur Solution-Wurzel); überschreibt den neuen Default.

### Nice-to-Have (optional, spätere Iteration)

- **Log-Cleanup-Strategie**: Alte Tages-Logs nach N Tagen automatisch löschen oder Max-N-Dateien. Out of scope hier.
- **Strukturierte `startup.json`**: Pro Run mit Version, Solution-Pfad, geladener Config, Tool-Liste. Out of scope.
- **stderr-Mirror**: Komplette stderr-Ausgabe in eine Datei spiegeln. Out of scope.

### Non-Goals

- **Serilog / Microsoft.Extensions.Logging einführen**: Architektur-Verbot (kein DI-Container, `AiNetLinterRichtlinien.mdc` §2). Direkter `StreamWriter` analog zum bestehenden `McpCallLog`.
- **Hot-Reload-Hardening**: Vermutetes Race im Lazy-Refresh (`McpCodeGraphServerRefresh`) ist eigenes Tech-Debt-Thema. Das Default-Logging macht das Symptom sichtbar, behebt aber nicht die Ursache.
- **Globale Projekt-Logger für alle Modi**: Default-Logik gilt nur im MCP-Server-Modus. Lint, Audit etc. bekommen kein Auto-Log (Diagnose dort weniger akut, Konzept-Aufblähung vermieden).

## Zielplattformen / Technischer Rahmen

- **Stack**: .NET 9, bestehende `McpCallLog`-Klasse, kein neues NuGet-Paket
- **Format**: JSONL (eine Zeile pro Eintrag), einheitlich für Calls und Errors
- **Pfad-Basis**: `<exeDir>` via `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` (gleiche Methode wie `McpServerOptionsFactory.cs:72-75` für die Server-Version)
- **Zeichencodierung**: UTF-8 ohne BOM (analog `McpCallLog.cs:40`)
- **Schreibstrategie**: `FileMode.Append`, `FileShare.Read` (Live-Lesbarkeit für Analyse-Tools)
- **Thread-Safety**: bestehender `_writeLock` in `McpCallLog.cs:29` wird auch von der Error-Methode genutzt, damit Call- und Error-Einträge in zeitlicher Reihenfolge erscheinen

## Verworfene Alternativen

- **Fallback auf `%LOCALAPPDATA%`**: User-Entscheidung "harter Fehler". Stille Fallbacks verlagern das Diagnose-Problem an einen anderen Ort.
- **Eine einzige `calls.log` ohne Tages-Ordner**: derselbe Schmerz wie das heutige Setup, nur an anderer Stelle.
- **Errors in separater Datei**: Korrelation Calls ↔ Errors über zwei Dateien ist Mehraufwand; eine Datei pro Tag hält es überschaubar.
- **Globale ILogger-Fassade (Serilog / Microsoft.Extensions.Logging)**: Architektur-Verbot; direkter `StreamWriter` ist konsistent mit bestehendem Code.

## Wo im Projekt

Pointer-Liste für den Planer im `drift-loop`. Jede Angabe ist eine Fundstelle, kein Architektur-Anspruch.

- `src/AiNetLinter/Mcp/McpCallLog.cs` — bestehende Implementierung; wird um Error-Sink erweitert, nicht ersetzt.
- `src/AiNetLinter/Commands/McpServerCommand.cs:31-76` — `RunAsync`: Default-Logik-Hook wird vor `McpServerOptionsFactory.Create` eingefügt.
- `src/AiNetLinter/Commands/McpServerCommand.cs:85-91` — `TryCreateCallLog`: bestehende Fabrik, bleibt für `--mcp-log`-Override.
- `src/AiNetLinter/Commands/McpServerCommand.cs:99-104` — `ResolveMcpLogPath`: bestehende Pfad-Auflösung, unverändert.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:42-49` — `Create`: hier wird der Error-Recorder in den Server-Lifecycle eingehängt.
- `src/AiNetLinter/Cli/CliOptionFactory.cs:230-233` — `--mcp-log`-Definition, unverändert.
- `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` — bestehende Tests, unverändert (Backward-Compat-Absicherung).
- `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` — bestehende Tests, erweitert um Error-Record-Test.
- Neue Test-Datei: `src/AiNetLinter.Tests/Commands/McpServerCommandDefaultLogTests.cs` — Tests für Default-Pfad-Konstruktion, read-only-Fail, solutionName-Token-Edge-Cases.

## Entdeckte Mängel/Redundanzen

Während der Konzeption aktiv gefundene Funde. Jeder Fund unabhängig von der Nutzer-Entscheidung dokumentiert.

- **MCP-Call-Log existiert bereits vollständig**
  - **Gefunden**: `src/AiNetLinter/Mcp/McpCallLog.cs:22` (Klasse), `McpCallLogScope` in derselben Datei, registriert in `McpServerOptionsFactory.cs:58-70`
  - **Bezug**: User-Konzept "was wurde aufgerufen, was wurde zurückgeliefert"
  - **Vorschlag**: Statt Neubau, bestehende Klasse um Error-Sink erweitern
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben "JSON-RPC-Errors im Log")

- **Loading-Antwort existiert bereits vollständig**
  - **Gefunden**: `ServerLoadState.cs:12`, `McpToolResults.Loading()` (`McpToolResults.cs:127`), alle 9 Tools prüfen `LoadState == Loading` (`GetFileSkeletonTool.cs:25`, `FindSymbolTool.cs:71`, `GetHotspotsTool.cs:24`, …)
  - **Bezug**: User-Vermutung "wenn klar ist das das tool aktuell nicht verwendet werden kann … könnte man ja eine sinnvolle info zurückgeben"
  - **Vorschlag**: kein Code-Change nötig
  - **Entscheidung**: abgelehnt (bereits implementiert, hier dokumentiert damit dieselbe Frage nicht wieder aufkommt)

- **Lazy Refresh via mtime + Hash existiert**
  - **Gefunden**: `src/AiNetLinter/Mcp/McpCodeGraphServerRefresh.cs:23` (dreiphasiger Sweep)
  - **Bezug**: User-Vermutung Hot-Reload-Unzulänglichkeit
  - **Vorschlag**: kein Code-Change im Logging-Konzept
  - **Entscheidung**: bewusst verschoben (eigenes Ticket)

- **Drei fragmentierte Diagnose-Kanäle**
  - **Gefunden**: `McpCallLog` (Tool-Calls) + `Console.Error`/`WriteError` (Server-Warnungen) + JSON-RPC-Errors (kein persistenter Pfad)
  - **Bezug**: User-Beobachtung "kann ich aktuell überhaupt nicht diagnostizieren"
  - **Vorschlag**: Error-Sink schließt den dritten Kanal
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben "JSON-RPC-Errors im Log")

- **Hot-Reload-Race möglich, aber nicht primärer Diagnose-Blocker**
  - **Gefunden**: `McpCodeGraphServerRefresh.cs:181-205` (`TryRefreshDocument`), nicht-atomarer Read+Hash+Replace
  - **Bezug**: User-Beobachtung "An error occurred invoking 'get_file_skeleton'"
  - **Vorschlag**: atomarer Replace unter Lock; optional FileSystemWatcher
  - **Entscheidung**: bewusst verschoben (eigenes Konzept); dieses Konzept verbessert Sichtbarkeit, nicht Ursache

## Wie (grober Ansatz)

1. **Default-Pfad-Builder** in `McpServerCommand`: neue private Methode `BuildDefaultLogPath(solutionPath, exeDir)`, die `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` konstruiert und Beschreibbarkeit prüft. Bei IO-Exception: stderr-Meldung + Exit.
2. **Default-Logik-Hook** in `McpServerCommand.RunAsync`: vor `TryCreateCallLog(args.McpLogPath, ...)` prüfen, ob Flag gesetzt; wenn nicht, `BuildDefaultLogPath` aufrufen. Ein Code-Pfad, ein Lock, eine Datei.
3. **Error-Sink** in `McpCallLog`: neue Methode `RecordError(tool, args, exception)`, schreibt JSONL-Zeile mit `level=error`, `error_type`, `error_message`, `stack_trace`. Selber Lock wie `RecordEnd`.
4. **Error-Hook in MCP-Server-Lifecycle**: an der Stelle, wo aktuell JSON-RPC-Errors entstehen (vermutlich via SDK-Middleware im `McpServer`/`StdioServerTransport`-Setup), wird `McpCallLog.RecordError` aufgerufen. Konkrete SDK-Punkte werden im Planer-Schritt verifiziert.
5. **Tests**: neue Unit-Tests in `McpServerCommandDefaultLogTests.cs` (Default-Pfad-Konstruktion, read-only-Fail, solutionName-Token) und Erweiterung von `McpCallLogTests` (Error-Record-Method). Integration-Tests via bestehendem `McpLiveRepositoryFixture` mit `--mcp-log <tempdir>` für Test-Isolation.
6. **Doku-Update**: `Docs/agent-api.md:311-341`, `Docs/integration.md:246`, `Docs/configuration.md:1087` — alle drei Stellen dokumentieren den neuen Default und die Backward-Compat-Garantie für `--mcp-log`.

## Definition of Done / Erfolgskriterien

- **DoD 1**: `ainetlinter --mcp-server` ohne `--mcp-log` erzeugt nach Server-Lauf eine Datei `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` mit ≥1 Eintrag pro Tool-Call.
- **DoD 2**: Eine unbehandelte Exception in einem Tool-Handler (z. B. simuliertes Hot-Reload-Race in `get_file_skeleton`) erzeugt im selben Log eine Zeile mit `level=error` und Stack-Trace. Aus dem Agent-Verlauf "An error occurred invoking 'get_file_skeleton'" lässt sich im Log die zugehörigen Args + Stack-Trace nachschlagen.
- **DoD 3**: Server in read-only-`<exeDir>` bricht ab mit Exit != 0 und stderr-Meldung `[ERROR]: MCP-Log-Verzeichnis nicht beschreibbar: <pfad>. Bitte --mcp-log <pfad> als Workaround setzen.`
- **DoD 4**: `--mcp-log <pfad>` verhält sich exakt wie vor diesem Konzept (Backward-Compat); bestehende `McpServerCommandCallLogTests` bleiben unverändert grün.
- **DoD 5**: `dotnet test` (Volllauf) ist grün, keine neuen Compiler-Warnungen (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **DoD 6**: `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` sind aktualisiert und beschreiben den neuen Default konsistent.
- **DoD 7**: `konzept.md`-Status auf `ready` nach expliziter User-Bestätigung (gem. `orchestrator.md` Schritt 6).

## Offene Punkte

Bewusst offen, weil sie die Konzept-Form nicht betreffen, aber die Implementierung beeinflussen:

- **Lösung für Hot-Reload-Race**: eigenes Konzept unter `tasks/mcp-hot-reload-hardening/`, nicht Teil hier.
- **Log-Cleanup-Strategie**: eigenes Konzept bei Bedarf.
- **`startup.json` + stderr-Mirror**: eigenes Konzept, nice-to-have.
- **Andere Modi (Lint, Audit)**: out of scope.
