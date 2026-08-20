---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: small
rules_dir: .agents/rules
last_updated: 2026-08-20T18:27:00+02:00
open_questions: []
---

# Konzept: Automatisches Prozess-Lebenszyklus-Management für den MCP-Server (Parent-Watchdog)

## Ziel (Was)

Der AiNetLinter MCP-Server (`--mcp-server`) soll seinen eigenen Lebenszyklus aktiv überwachen und sich selbstständig und sauber beenden, sobald der aufrufende Elternprozess (LLM-Agent, Language Server, IDE wie Antigravity / Cursor / VS Code / Claude Desktop) beendet, geschlossen oder gekillt wurde.

Damit wird verhindert, dass unbemerkt verwaiste (Zombie/Orphan) `AiNetLinter.exe`-Prozesse im Hintergrund weiterlaufen und wertvollen Arbeitsspeicher blockieren.

## Warum / Kontext

- **Problem unter Windows**: Windows beendet Kindprozesse nicht automatisch, wenn der Elternprozess stirbt, es sei denn, ein Job-Object mit `KILL_ON_JOB_CLOSE` wurde eingerichtet (was viele IDEs/Agenten nicht tun).
- **Stdio-Blockade**: Stdio-Pipes signalisieren bei abruptem Elternprozess-Ende oft kein EOF, sodass `server.RunAsync` unendlich auf stdin-Input wartet.
- **Speicherbedarf**: Jeder aktive Roslyn-Workspace hält Syntaxbäume und semantische Modelle im Speicher (~130–150 MB pro Instanz). Bei mehreren Agent-Sessions häufen sich verwaiste Prozesse an.
- **Zero-Config-Anforderung**: Entwickler und IDE-Konfigurationen (`.mcp.json`) sollen keine manuellen PID-Verdrahtungen vornehmen müssen; die Erkennung muss out-of-the-box funktionieren.

## Scope

### Muss-Haben

- **Automatischer Parent-Process-Watchdog (Zero-Config)**:
  - Automatische Ermittlung der Parent-PID zur Laufzeit (Win32 `NtQueryInformationProcess` / `ProcessBasicInformation` auf Windows, `/proc` / POSIX auf Linux/macOS).
  - Hintergrundüberwachung des Elternprozesses via `Process.WaitForExitAsync()` und periodischem Liveness-Check.
  - Sofortiges Auslösen des `CancellationToken`, sobald der Elternprozess nicht mehr existiert.
- **Explizite CLI-Option `--parent-pid <pid>`**:
  - Ermöglicht das optionale explizite Übergeben der zu überwachenden Prozess-ID (z. B. für Wrapper-Skripte oder Spezial-Umgebungen).
- **Saubere Konsolen-/CLI-Integration**:
  - Optionen in `CliOptionFactory`, `CliOptions`, `CliParsedArgs`, `LinterArgs`.
  - Saubere Beendigung (Graceful Exit 0, geordneter Shutdown).
- **Automatisierte Tests**:
  - Fast-Tests für Watchdog-Logik und CLI-Parsing.
  - Integrationstests für E2E-Lebenszyklus und Prozessbeendigung bei Parent-Exit.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

*Keine.*

### Non-Goals (bewusst NICHT Teil davon)

- **Kein `--idle-timeout`**: Auf Wunsch bewusst weggelassen, da der Parent-Watchdog das Problem bei Schließen der IDE/des Agenten unmittelbar und 100% zuverlässig löst.
- **Kein eigenes Supervisor-Daemon-System**: AiNetLinter bleibt ein schlankes CLI-Tool und startet keine zusätzlichen Hintergrund-Dienste.
- **Keine Modifikation von Fremd-Job-Objects**: Das Tool versucht nicht, sich fremden Job-Objects invasiv zuzuweisen, sondern überwacht den Elternprozess rein über OS-Standard-APIs.

## Zielplattformen / Technischer Rahmen

- **.NET 9 / C# 13**
- **Plattform-Support**:
  - Windows: Primär über Win32 `NtQueryInformationProcess` (Standard für Parent-PID-Lookup) und `System.Diagnostics.Process`.
  - Linux/macOS: Fallback über `/proc/{PID}/stat` (Feld 4: PPID) bzw. `getppid()`, sodass Cross-Platform-Fähigkeit ohne Windows-only-Hardcoding gewahrt bleibt.
- **Keine zusätzlichen externen NuGet-Abhängigkeiten**: Reine Nutzung von .NET BCL (`System.Diagnostics.Process`, `System.Threading.Tasks`) und Win32 P/Invoke.

## Verworfene Alternativen

- **Nur auf stdin-EOF verlassen**: Verworfen, da auf Windows bei verwaisten Pipe-Handles oder abruptem Kill des Elternprozesses kein EOF auf `stdin` signalisiert wird und der Prozess für immer hängt.
- **Ausschließlich `--parent-pid` ohne Auto-Discovery**: Verworfen, da dies manuelle Konfiguration in allen MCP-Clients (`.mcp.json`, `mcp_config.json`) erfordern würde. Zero-Config über Auto-Discovery ist der Goldstandard.
- **Inaktivitäts-Timeout (`--idle-timeout`)**: Bewusst verworfen, da der Parent-Watchdog bereits alle verwaisten Prozesse unmittelbar beim Schließen der Sitzung abfängt und ein Idle-Timeout legitime längere Denkpausen während einer offenen IDE-Sitzung stören könnte.

## Wo im Projekt

- **CLI-Optionen & Parsing**:
  - [CliOptionFactory.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliOptionFactory.cs): Definition der Option `--parent-pid`.
  - [CliOptions.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliOptions.cs): Aufnahme der Option in die Options-Klasse.
  - [CliParsedArgs.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/CliParsedArgs.cs): Parsing des CLI-Arguments.
  - [LinterArgs.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Cli/LinterArgs.cs): Bereitstellung von `ParentPid`.
- **MCP Server Command & Lifecycle**:
  - [McpServerCommand.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Commands/McpServerCommand.cs): Starten und Verknüpfen des Parent-Watchdogs mit dem Server-CancellationToken.
  - [Program.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Program.cs): Weiterleiten der Tokens / Argumente.
- **Neues Lifecycle-/Watchdog-Modul**:
  - `src/AiNetLinter/Mcp/Lifetime/ParentProcessDetector.cs`: Ermittlung der Parent-PID (Windows `NtQueryInformationProcess` / Linux `/proc`).
  - `src/AiNetLinter/Mcp/Lifetime/ParentProcessWatchdog.cs`: Asynchrone Überwachung des Elternprozess-Exits.
- **Tests**:
  - `src/AiNetLinter.FastTests/Mcp/ParentProcessDetectorTests.cs`
  - `src/AiNetLinter.IntegrationTests/Mcp/McpServerLifetimeTests.cs`

## Entdeckte Mängel/Redundanzen

- **Verwaiste Hintergrundprozesse bei IDE-Neustarts**:
  - **Gefunden:** Mehrere laufende `AiNetLinter.exe`-Instanzen im Task-Manager nach Schließen/Neustarten des Agenten/Language-Servers.
  - **Bezug:** Fehlende Prozesslebenszyklus-Überwachung in [`McpServerCommand.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Commands/McpServerCommand.cs).
  - **Vorschlag:** Automatischer Parent-Watchdog, der bei Parent-Exit sofort den CancellationToken abbricht.
  - **Entscheidung:** Übernommen ins Scope (→ siehe Muss-Haben).

## Wie (grober Ansatz)

1. **Parent-Detection**: Beim Aufruf von `McpServerCommand.RunAsync` wird die Ziel-PID bestimmt: Entweder explizit aus `args.ParentPid` oder automatisch via `ParentProcessDetector.TryGetParentProcessId()`.
2. **Watchdog-Aktivierung**:
   - Wenn eine gültige Parent-PID gefunden wird, wird `ParentProcessWatchdog.Start(parentPid, watchdogCts)` gestartet.
   - Der Watchdog öffnet ein Handle auf den Prozess (`Process.GetProcessById(parentPid)`) und registriert `parentProcess.WaitForExitAsync(ct)` sowie einen 2-Sekunden-Fallback-Poll.
3. **Verknüpfung**:
   - Das resultierende `CancellationToken` aus dem Watchdog wird via `CancellationTokenSource.CreateLinkedTokenSource` mit dem Root-`ct` verknüpft und an `server.RunAsync(linkedToken)` übergeben.
   - Beim Auslösen beendet sich der MCP-Server sauber mit Exit-Code 0.

## Definition of Done / Erfolgskriterien

1. **Parent-Exit-Verifikation**: Wird der Elternprozess (z. B. Subprozess-Test oder IDE) beendet, terminiert `AiNetLinter.exe` innerhalb von maximal 2–3 Sekunden eigenständig.
2. **Zero-Regression**: Alle bestehenden Fast- und Integration-Tests (`dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `src/AiNetLinter.IntegrationTests --filter Category!=Stress`) laufen weiterhin zu 100% grün.
3. **Dokumentation**: Aktualisierung von `Docs/configuration.md` und `Docs/ROADMAP.md` bezüglich der neuen Option `--parent-pid`.

## Offene Punkte

*Keine.*
