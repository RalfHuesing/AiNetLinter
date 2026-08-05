---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-05T11:38:00+02:00
open_questions:
  - "Stack-Trace-Cap: Vorschlag 4 KB pro Eintrag — finale Festlegung im Planer-Schritt ok?"
---

# Konzept: JSON-RPC-Errors ins MCP-Call-Log

## Ziel (Was)

Wenn `--mcp-log` aktiv ist, sollen nicht nur erfolgreiche Tool-Calls, sondern auch unbehandelte Exceptions / JSON-RPC-Errors in derselben JSONL-Datei landen — mit Tool, Args, Exception-Type, Message und Stack-Trace. Damit wird die im Agent-Verlauf beobachtete Meldung *"An error occurred invoking 'get_file_skeleton'"* aus dem Log reproduzierbar.

## Warum / Kontext

`McpCallLog` (`src/AiNetLinter/Mcp/McpCallLog.cs:22`) zeichnet aktuell nur erfolgreiche Tool-Calls auf — aufgerufen via `scope.Complete(result)` aus den Tool-Registrations. Unbehandelte Exceptions, die im MCP-SDK-Handler-Layer gefangen und als "An error occurred invoking X" an den Client zurückgegeben werden, **umgehen** diesen Pfad und landen nirgends persistiert. Folge: der User sieht im Agent-Verlauf einen Fehler, kann aber nicht nachvollziehen, wann/wie oft er aufgetreten ist oder welche Args ihn ausgelöst haben.

Opt-in-Charakter (`--mcp-log` muss explizit gesetzt sein) und Pfad-Auflösung (absolut oder relativ zur Solution-Wurzel) bleiben unverändert. Der User bestätigt, dass das aktuelle Verhalten so gewollt ist.

## Scope

### Muss-Haben

- **`McpCallLog.RecordError(tool, args, exception)`**: neue Methode auf `McpCallLog`, schreibt eine JSONL-Zeile mit `level=error`, `error_type` (Exception-Typ-Name), `error_message`, `stack_trace`. Selber Lock wie `RecordEnd` (`McpCallLog.cs:29`), damit Call- und Error-Einträge in zeitlicher Reihenfolge erscheinen.
- **Error-Hook im MCP-Server-Lifecycle**: an der Stelle, wo aktuell JSON-RPC-Errors entstehen (vermutlich via SDK-Middleware im `McpServer`/`StdioServerTransport`-Setup), wird `McpCallLog.RecordError` aufgerufen, sofern `callLog != null`. Konkrete SDK-Punkte werden im Planer-Schritt verifiziert.
- **Tests**: Erweiterung von `McpCallLogTests` (Error-Record-Methode, JSONL-Schema, Lock-Reihenfolge). Kein neuer Test-File nötig.

### Nice-to-Have (optional, spätere Iteration)

- **Default-Pfad aktiviert** (z. B. `<exeDir>/logs/<solutionName>/<datum>/calls.jsonl`): bewusst NICHT Teil dieses Konzepts — User bestätigt Opt-in-Verhalten.
- **Log-Cleanup-Strategie**: out of scope.
- **`startup.json` + stderr-Mirror**: out of scope.

### Non-Goals

- **Opt-in zu Opt-out umbauen**: User bestätigt aktuelles `--mcp-log`-Verhalten.
- **Hot-Reload-Hardening**: vermutetes Race im Lazy-Refresh (`McpCodeGraphServerRefresh.cs:181-205`) ist eigenes Tech-Debt-Thema. Dieses Konzept verbessert Sichtbarkeit, nicht Ursache.
- **Serilog / Microsoft.Extensions.Logging einführen**: Architektur-Verbot (`AiNetLinterRichtlinien.mdc` §2); direkter `StreamWriter` wie bestehend.

## Zielplattformen / Technischer Rahmen

- **Stack**: .NET 9, bestehende `McpCallLog`-Klasse, kein neues NuGet-Paket
- **Format**: JSONL (eine Zeile pro Eintrag), identisches Schema wie bestehende Call-Einträge plus Error-Felder
- **Zeichencodierung**: UTF-8 ohne BOM (analog `McpCallLog.cs:40`)
- **Thread-Safety**: bestehender `_writeLock` deckt auch `RecordError` ab
- **Stack-Trace-Begrenzung**: Stack-Traces können lang sein; ein Cap (Vorschlag 4 KB) verhindert, dass eine einzelne Exception das Log aufbläht. Finaler Wert im Planer-Schritt.

## Wo im Projekt

Pointer-Liste für den Planer im `drift-loop`. Jede Angabe ist eine Fundstelle, kein Architektur-Anspruch.

- `src/AiNetLinter/Mcp/McpCallLog.cs` — bestehende Klasse; bekommt neue `RecordError`-Methode und ggf. erweitertes JSON-Schema.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:42-49` — `Create`: hier wird der Error-Recorder in den Server-Lifecycle eingehängt. Konkrete SDK-Stelle im Planer-Schritt verifizieren.
- `src/AiNetLinter/Commands/McpServerCommand.cs:31-76` — `RunAsync`: bestehender `callLog`-Parameter wird durchgereicht; keine Änderung an Aufruf-Seite nötig.
- `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` — bestehende Tests, erweitert um Error-Record-Tests.
- Keine Änderungen an CLI, Doku-Defaults oder Pfad-Auflösung.

## Entdeckte Mängel/Redundanzen

Während der Konzeption aktiv gefundene Funde. Jeder Fund unabhängig von der Nutzer-Entscheidung dokumentiert.

- **MCP-Call-Log existiert bereits vollständig**
  - **Gefunden**: `src/AiNetLinter/Mcp/McpCallLog.cs:22`, registriert in `McpServerOptionsFactory.cs:58-70`
  - **Bezug**: User-Konzept "was wurde aufgerufen, was wurde zurückgeliefert"
  - **Vorschlag**: Statt Neubau, bestehende Klasse um Error-Sink erweitern
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben)

- **Loading-Antwort existiert bereits vollständig**
  - **Gefunden**: `ServerLoadState.cs:12`, `McpToolResults.Loading()` (`McpToolResults.cs:127`), alle 9 Tools prüfen `LoadState == Loading`
  - **Bezug**: User-Vermutung "wenn klar ist das das tool aktuell nicht verwendet werden kann"
  - **Entscheidung**: abgelehnt (bereits implementiert, hier dokumentiert damit dieselbe Frage nicht wieder aufkommt)

- **Lazy Refresh via mtime + Hash existiert**
  - **Gefunden**: `McpCodeGraphServerRefresh.cs:23`
  - **Bezug**: User-Vermutung Hot-Reload-Unzulänglichkeit
  - **Entscheidung**: bewusst verschoben (eigenes Ticket)

- **JSON-RPC-Errors umgehen das Call-Log**
  - **Gefunden**: Tool-Handler returnen via `McpServerOptionsFactory`-Wrapper, aber unbehandelte Exceptions im SDK-Layer führen zu "An error occurred invoking X" ohne Persistenz
  - **Bezug**: User-Beobachtung "An error occurred invoking 'get_file_skeleton'"
  - **Vorschlag**: Error-Sink in `McpCallLog`
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben)

- **Opt-in-Charakter und Pfad-Auflösung sind gewollt**
  - **Gefunden**: User bestätigt aktuelles `--mcp-log`-Verhalten
  - **Entscheidung**: bewusst NICHT ändern

## Wie (grober Ansatz)

1. **`McpCallLog.RecordError`** implementieren: neue Methode, kapselt JSONL-Zeile mit `level=error`, `error_type`, `error_message`, `stack_trace`. Stack-Trace auf max. 4 KB gekappt. Lock wie in `RecordEnd`.
2. **Error-Hook in `McpServerOptionsFactory`**: an der SDK-Stelle, wo aktuell JSON-RPC-Errors entstehen (zu verifizieren via MCP-SDK-Inspektion), wird `callLog?.RecordError(tool, args, ex)` aufgerufen. Bei `callLog == null` (Opt-in nicht aktiv) kein Overhead.
3. **Tests**: Erweiterung von `McpCallLogTests` um:
   - `RecordError` schreibt Zeile mit allen 4 Error-Feldern
   - `RecordError` mit langem Stack-Trace kappt bei 4 KB
   - Lock-Reihenfolge: `RecordEnd` und `RecordError` serialisieren
4. **Doku-Update**: `Docs/agent-api.md:311-341` — Abschnitt "Call-Log" um Error-Schema erweitern. Keine Änderung an `integration.md` oder `configuration.md` nötig.

## Definition of Done / Erfolgskriterien

- **DoD 1**: Mit aktivem `--mcp-log <pfad>` löst eine künstlich ausgelöste Exception in einem Tool-Handler (z. B. via Test-Harness) eine zusätzliche JSONL-Zeile mit `level=error`, `error_type`, `error_message`, `stack_trace` in derselben Datei aus.
- **DoD 2**: Die Reihenfolge der Einträge in der Log-Datei entspricht der zeitlichen Abfolge (Call- und Error-Einträge serialisiert via `_writeLock`).
- **DoD 3**: Ohne `--mcp-log` (Opt-in nicht aktiv) wird keine Datei erzeugt, keine Exception geworfen, kein File-I/O ausgeführt (Fast-Path beibehalten).
- **DoD 4**: Bestehende `McpCallLogTests` und `McpServerCommandCallLogTests` bleiben unverändert grün. Neue Tests grün.
- **DoD 5**: `dotnet test` (Volllauf) grün, keine neuen Compiler-Warnungen (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **DoD 6**: `Docs/agent-api.md` Abschnitt "Call-Log" um Error-Schema erweitert.
- **DoD 7**: `konzept.md`-Status auf `ready` nach expliziter User-Bestätigung.

## Offene Punkte

- **Lösung für Hot-Reload-Race**: eigenes Konzept unter `tasks/mcp-hot-reload-hardening/`, nicht Teil hier.
- **Stack-Trace-Cap-Wert**: 4 KB als Vorschlag; finale Festlegung im Planer-Schritt.
- **SDK-spezifische Error-Hook-Punkte**: werden im Planer-Schritt verifiziert (welcher SDK-Call fängt die unbehandelte Exception?).
