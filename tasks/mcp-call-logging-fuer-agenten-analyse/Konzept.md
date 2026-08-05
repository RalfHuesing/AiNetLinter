---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-05T12:30:00+02:00
open_questions: []  # ready: alle Annahmen akzeptiert, Drift-loop kann starten
revision_history:
  - 2026-08-05T12:30:00+02:00: User-Entscheidung im Drift-Loop — kein Fallback-Pfad,
    bei nicht auflösbarer Solution bricht `--mcp-server` mit Fehlermeldung und Exit ≠ 0 ab.
    Betrifft Muss-Habe 2 und DoD 1.
---

# Konzept: MCP-Call-Log um Pfad-Konvention und Error-Sink erweitern

## Ziel (Was)

Zwei Erweiterungen am bestehenden `--mcp-log`-Opt-in:

1. **Pfad-Konvention**: Wenn `--mcp-log` ohne expliziten Pfad aktiviert wird, benutzt der Server automatisch `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`. Explizite Pfade (absolut oder relativ zur Solution-Wurzel) bleiben unverändert möglich.
2. **Error-Sink**: Unbehandelte Exceptions / JSON-RPC-Errors landen in derselben JSONL-Datei mit `level/error_type/error_message/stack_trace`.

Opt-in-Charakter bleibt: ohne `--mcp-log`-Flag wird nicht geloggt. Die Doku in `Docs/agent-api.md:317` suggeriert diesen Default bereits, der Code setzt ihn aktuell aber nicht um (Test `TryCreateCallLog_PathNotSet_ReturnsNull` zeigt: leerer Wert = `null` = kein Log) — diese Lücke wird hier geschlossen.

## Warum / Kontext

User-Beobachtungen aus der initialen Konzept-Skizze:
- "log relativ zur .exe ... unterverzeichnis pro projekt ... dann unterverzeichnis mit datum" → Pfad-Konvention
- "An error occurred invoking 'get_file_skeleton'" → Diagnose-Lücke

User-Korrektur: Opt-in (`--mcp-log` muss explizit gesetzt sein) ist gewollt — User behält Kontrolle. Aber die Pfad-Konvention fehlt: aktuell muss der User bei jedem Aufruf den vollen Pfad selbst konstruieren, oder das Flag wirkt gar nicht (wenn leer).

Error-Lücke: `McpCallLog` loggt nur erfolgreiche Calls (`scope.Complete(result)`). Unbehandelte Exceptions im SDK-Layer umgehen den Pfad und werden als "An error occurred invoking X" an den Client zurückgegeben, ohne dass irgendwo auf der Platte etwas persistiert wird.

## Scope

### Muss-Haben

- **Default-Pfad bei Opt-in**: Wenn `--mcp-log` ohne Wert gesetzt wird, konstruiert `McpServerCommand` automatisch `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl`. Verzeichnisse werden automatisch angelegt. Wenn `--mcp-log <pfad>` explizit gesetzt wird, gilt wie bisher die Pfad-Auflösung (absolut oder relativ zur Solution-Wurzel).
- **`<solutionName>`-Token**: Dateiname der Solution ohne Extension (`MyApp.slnx` → `MyApp`). **Kein Fallback-Pfad**: wenn keine Solution auflösbar (z. B. Server ohne gültigen Solution-Pfad gestartet), bricht `--mcp-server` mit Fehlermeldung auf stderr und Exit-Code ≠ 0 ab. Es wird keine Log-Datei angelegt, der Server startet nicht. Begründung: ein "still laufender" Server ohne zugeordnetes Log-Verzeichnis erzeugt schwer auffindbare Folgeprobleme bei der späteren Diagnose; ein harter Abbruch zwingt zur expliziten Klärung.
- **Datum lokal**: `yyyy-MM-dd` in der lokalen Zeitzone des Servers (nicht UTC), damit "heute" intuitiv ist.
- **`McpCallLog.RecordError(tool, args, exception)`**: neue Methode, schreibt JSONL-Zeile mit `level=error`, `error_type` (Exception-Typ-Name), `error_message`, `stack_trace`. Selber Lock wie `RecordEnd` (`McpCallLog.cs:29`), damit Call- und Error-Einträge in zeitlicher Reihenfolge erscheinen. Stack-Trace auf 4 KB gekappt, damit eine einzelne Exception das Log nicht aufbläht.
- **Error-Hook im MCP-Server-Lifecycle**: an der SDK-Stelle, wo aktuell JSON-RPC-Errors entstehen, wird `McpCallLog.RecordError` aufgerufen, sofern `callLog != null`. Bei `callLog == null` (Opt-in nicht aktiv) kein Overhead. Konkrete SDK-Punkte im Planer-Schritt verifizieren.
- **Tests**:
  - `McpServerCommandCallLogTests`: bestehende Tests `TryCreateCallLog_PathNotSet_ReturnsNull` und `TryCreateCallLog_WhitespacePath_ReturnsNull` testen das Verhalten, das wir aktiv ändern — sie werden ersetzt durch Tests, die den Default-Pfad-Pfad abdecken. Erweitert um: Default-Pfad-Konstruktion, exe-Name-Fallback bei fehlender Solution.
  - `McpCallLogTests`: erweitert um `RecordError`-Methode, JSONL-Schema-Validierung, Lock-Reihenfolge, Stack-Trace-Cap.
- **Doku**: `Docs/agent-api.md:311-341` Abschnitt "Call-Log (opt-in)" um Default-Pfad-Konvention und Error-Schema erweitern. `Docs/configuration.md:1087` Eintrag für `--mcp-log` anpassen (Default-Wert-Beschreibung).

### Bewusst out of scope (eigene Konzepte, falls Bedarf)

- **Hot-Reload-Hardening**: vermutetes Race im Lazy-Refresh (`McpCodeGraphServerRefresh.cs:181-205`) ist eigenes Tech-Debt-Thema, nicht Teil hier.
- **Serilog / Microsoft.Extensions.Logging einführen**: Architektur-Verbot (`AiNetLinterRichtlinien.mdc` §2, kein DI-Container). Direkter `StreamWriter` wie bestehend.
- **Log-Cleanup-Strategie** (Rotation, Max-Alter): nicht in dieser Iteration angedacht.
- **`startup.json` / stderr-Mirror**: nicht in dieser Iteration angedacht.
- **Opt-in zu Opt-out umbauen**: User-Kontrolle ist explizit gewollt.

## Zielplattformen / Technischer Rahmen

- **Stack**: .NET 9, bestehende `McpCallLog`-Klasse, kein neues NuGet-Paket
- **Format**: JSONL (eine Zeile pro Eintrag), identisches Schema wie bestehende Call-Einträge plus Error-Felder
- **Pfad-Basis**: `<exeDir>` via `Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)` (gleiche Methode wie `McpServerOptionsFactory.cs:72-75` für die Server-Version)
- **Zeichencodierung**: UTF-8 ohne BOM (analog `McpCallLog.cs:40`)
- **Schreibstrategie**: `FileMode.Append`, `FileShare.Read` (Live-Lesbarkeit für Analyse-Tools)
- **Thread-Safety**: bestehender `_writeLock` in `McpCallLog.cs:29` deckt auch `RecordError` ab
- **Stack-Trace-Cap**: 4 KB pro Eintrag (im Planer-Schritt final festlegen, falls Anpassung nötig)

## Wo im Projekt

Pointer-Liste für den Planer im `drift-loop`. Jede Angabe ist eine Fundstelle, kein Architektur-Anspruch.

- `src/AiNetLinter/Mcp/McpCallLog.cs` — bestehende Klasse; bekommt neue `RecordError`-Methode und ggf. erweitertes internes Locking.
- `src/AiNetLinter/Commands/McpServerCommand.cs:31-76` — `RunAsync`: Aufruf-Stelle, wo Default-Pfad-Trigger hinzukommt.
- `src/AiNetLinter/Commands/McpServerCommand.cs:85-91` — `TryCreateCallLog`: Verhalten ändert sich fundamental (leerer Wert = Default statt `null`). Die zwei bestehenden Tests, die das alte Verhalten prüfen, müssen ersetzt werden.
- `src/AiNetLinter/Commands/McpServerCommand.cs:99-104` — `ResolveMcpLogPath`: bleibt unverändert für explizite Pfad-Auflösung.
- `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:42-49` — `Create`: Error-Hook im Server-Lifecycle.
- `src/AiNetLinter/Cli/CliOptionFactory.cs:230-233` — `--mcp-log`-Definition: ggf. Description anpassen (Default-Verhalten klarstellen).
- `src/AiNetLinter.Tests/Commands/McpServerCommandCallLogTests.cs` — zwei Tests werden obsolet, werden ersetzt; neue Tests dazu.
- `src/AiNetLinter.Tests/Mcp/McpCallLogTests.cs` — erweitert um Error-Record-Tests.
- `Docs/agent-api.md:311-341` — Doku-Update Default-Pfad + Error-Schema.
- `Docs/configuration.md:1087` — Eintrag für `--mcp-log` anpassen (Default-Wert).

## Entdeckte Mängel/Redundanzen

Während der Konzeption aktiv gefundene Funde. Jeder Fund unabhängig von der Nutzer-Entscheidung dokumentiert.

- **MCP-Call-Log existiert bereits vollständig**
  - **Gefunden**: `src/AiNetLinter/Mcp/McpCallLog.cs:22` (Klasse), `McpCallLogScope` in derselben Datei, registriert in `McpServerOptionsFactory.cs:58-70`
  - **Bezug**: User-Konzept "was wurde aufgerufen, was wurde zurückgeliefert"
  - **Vorschlag**: Statt Neubau, bestehende Klasse um `RecordError` und internes Locking erweitern
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben)

- **Loading-Antwort existiert bereits vollständig**
  - **Gefunden**: `ServerLoadState.cs:12`, `McpToolResults.Loading()` (`McpToolResults.cs:127`), alle 9 Tools prüfen `LoadState == Loading`
  - **Bezug**: User-Vermutung "wenn klar ist das das tool aktuell nicht verwendet werden kann"
  - **Entscheidung**: abgelehnt (bereits implementiert, hier dokumentiert damit dieselbe Frage nicht wieder aufkommt)

- **Lazy Refresh via mtime + Hash existiert**
  - **Gefunden**: `McpCodeGraphServerRefresh.cs:23` (dreiphasiger Sweep)
  - **Bezug**: User-Vermutung Hot-Reload-Unzulänglichkeit
  - **Entscheidung**: bewusst verschoben (eigenes Ticket)

- **JSON-RPC-Errors umgehen das Call-Log**
  - **Gefunden**: Tool-Handler returnen via `McpServerOptionsFactory`-Wrapper, aber unbehandelte Exceptions im SDK-Layer führen zu "An error occurred invoking X" ohne Persistenz
  - **Bezug**: User-Beobachtung "An error occurred invoking 'get_file_skeleton'"
  - **Vorschlag**: Error-Sink in `McpCallLog`
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben)

- **Opt-in-Charakter ist gewollt, aber Pfad-Konvention fehlt**
  - **Gefunden**: User bestätigt aktuelles `--mcp-log`-Verhalten, wünscht aber sinnvolle Default-Pfad-Struktur
  - **Bezug**: User-Anmerkung "ich will <exeDir>/logs/<solutionName>/<datum>/... haben, das ist kern der sache"
  - **Vorschlag**: Default-Pfad-Konstruktion bei leerem `--mcp-log`-Wert
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben)

- **Bestehende Tests prüfen das Verhalten, das wir ändern**
  - **Gefunden**: `McpServerCommandCallLogTests.cs` `TryCreateCallLog_PathNotSet_ReturnsNull` und `TryCreateCallLog_WhitespacePath_ReturnsNull`
  - **Bezug**: Doku in `Docs/agent-api.md:317` suggeriert bereits Default-Verhalten, das der Code nicht umsetzt
  - **Vorschlag**: Tests ersetzen durch Tests, die den Default-Pfad abdecken
  - **Entscheidung**: übernommen ins Scope (→ siehe Muss-Haben "Tests")

## Wie (grober Ansatz)

1. **Default-Pfad-Builder** in `McpServerCommand`: neue private Methode `BuildDefaultLogPath(solutionPath, exeDir)`, die `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` konstruiert. Fallback `ainetlinter-no-solution-<yyyy-MM-dd>` bei fehlender Solution. Lokales Datum.
2. **`TryCreateCallLog` erweitern** ODER neue Methode `TryCreateCallLogOrDefault`: bei `mcpLogPath` null/leer/whitespace wird `BuildDefaultLogPath` aufgerufen. Andernfalls wie bisher `ResolveMcpLogPath`. Konkrete Form (Methode ersetzen oder neue hinzufügen) entscheidet der Planer; Verhalten muss sein: leerer Wert = Default-Pfad, nicht `null`.
3. **`McpCallLog.RecordError`** implementieren: JSONL-Zeile mit `level/error_type/error_message/stack_trace`. Stack-Trace-Cap 4 KB. Selber Lock wie `RecordEnd`.
4. **Error-Hook in `McpServerOptionsFactory`**: SDK-Stelle, wo aktuell JSON-RPC-Errors entstehen (zu verifizieren via MCP-SDK-Inspektion), ruft `callLog?.RecordError(tool, args, ex)`. Bei `callLog == null` (Opt-in nicht aktiv) kein Overhead, kein `if`-Check im Hot-Path.
5. **Tests**:
   - `McpServerCommandCallLogTests`: zwei Tests ersetzen (Default-Pfad statt `null`), neue Tests für Default-Pfad-Konstruktion, exe-Name-Fallback.
   - `McpCallLogTests`: `RecordError`-Methode, JSONL-Schema, Stack-Trace-Cap, Lock-Reihenfolge.
6. **Doku-Update**: `Docs/agent-api.md:311-341` Abschnitt "Call-Log (opt-in)" — Default-Pfad dokumentieren, Error-Schema-Beispiel ergänzen. `Docs/configuration.md:1087` — Default-Wert im CLI-Option-Eintrag.

## Definition of Done / Erfolgskriterien

- **DoD 1**: `ainetlinter --mcp-server` ohne `--mcp-log`-Flag erzeugt KEINE Log-Datei. `ainetlinter --mcp-server --mcp-log` (ohne Wert) erzeugt nach Server-Lauf eine Datei `<exeDir>/logs/<solutionName>/<yyyy-MM-dd>/calls.jsonl` mit ≥1 Eintrag pro Tool-Call. `ainetlinter --mcp-server --mcp-log <pfad>` benutzt den expliziten Pfad (Backward-Compat). `ainetlinter --mcp-server --mcp-log` ohne auflösbare Solution bricht mit Fehlermeldung ab, Exit-Code ≠ 0, keine Log-Datei.
- **DoD 2**: Mit aktivem `--mcp-log` löst eine künstlich ausgelöste Exception in einem Tool-Handler (z. B. via Test-Harness simuliertes Hot-Reload-Race in `get_file_skeleton`) eine zusätzliche JSONL-Zeile mit `level=error`, `error_type`, `error_message`, `stack_trace` in derselben Datei aus. Aus dem Agent-Verlauf "An error occurred invoking 'get_file_skeleton'" lässt sich im Log die zugehörigen Args + Stack-Trace nachschlagen.
- **DoD 3**: Reihenfolge der Einträge folgt zeitlicher Abfolge (Lock serialisiert).
- **DoD 4**: Stack-Trace-Cap funktioniert (4 KB); eine Exception mit 100 KB Stack-Trace erzeugt einen 4 KB langen `stack_trace`-Eintrag, nicht mehr.
- **DoD 5**: Bestehende `McpCallLogTests` (4 Call-Tests) bleiben unverändert grün. `McpServerCommandCallLogTests` werden angepasst (2 obsolet → ersetzt) und erweitert. Neue Tests grün. `dotnet test` (Volllauf) grün. Keine neuen Compiler-Warnungen (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
- **DoD 6**: `Docs/agent-api.md:311-341` und `Docs/configuration.md:1087` sind aktualisiert und beschreiben Default-Pfad-Konvention + Error-Schema konsistent.
- **DoD 7**: `konzept.md`-Status auf `ready` nach expliziter User-Bestätigung.

## Offene Punkte

Bewusst offen, weil Folge-Themen mit eigenem Konzept-Charakter:

- **Lösung für Hot-Reload-Race**: vermutetes Race in `McpCodeGraphServerRefresh.cs:181-205` (nicht-atomarer Read+Hash+Replace). Eigenes Konzept unter `tasks/mcp-hot-reload-hardening/`, nicht Teil hier. Das Default-Logging macht das Symptom sichtbar, behebt aber nicht die Ursache.
- **SDK-spezifische Error-Hook-Punkte**: konkrete SDK-Stelle wird im Planer-Schritt verifiziert (welcher SDK-Call fängt die unbehandelte Exception ab?).
