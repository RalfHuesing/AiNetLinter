# AiNetLinter — MCP-Server & Daemon-Architektur

→ [MCP-Tools & Verträge](tools.md) | [MCP-Host-Integration](integration.md) | [MCP-Bootstrap](mcp-bootstrap.md) | [Linter-CLI](../linter/cli.md) | [README](../../README.md)

---

## 1. Architektur-Übersicht

AiNetLinter stellt die Roslyn-basierte Solution- und Assembly-Analyse als **stdio-basierten MCP-Server** (Model Context Protocol) für AI-Coding-Agenten (Claude Code, Cursor, eigene Agent-Loops) bereit.

Die Architektur trennt strikt zwischen einem leichtgewichtigen Stdio-Proxy (**ThinClient**) und einem im Hintergrund laufenden, wiederverwendbaren Analyse-Dienst (**DaemonHost**):

```text
[ MCP Host: Claude / Cursor ]
            │  stdio (JSON-RPC)
            ▼
┌──────────────────────────────────────┐
│       AiNetLinter ThinClient         │  (kein Roslyn, kein MCP-SDK,
│  (Start via: ainetlinter --mcp-server)│   reines Byte-Pumping)
└──────────────────┬───────────────────┘
                   │  Named Pipe (benutzergebunden)
                   ▼
┌──────────────────────────────────────┐
│        AiNetLinter DaemonHost        │  (hält ProjectRegistry, Roslyn-Workspaces,
│  (läuft detached im Hintergrund)     │   Assembly-Cache, serielle Ladevorgänge)
└──────────────────────────────────────┘
```

---

## 2. Drei-Zustands-Lifecycle des MCP-Servers

Der Server-Handshake (`initialize` bzw. `server/discover`) antwortet **sofort**. Die Roslyn-Solution wird parallel im Hintergrund geladen, um Timeouts des MCP-Hosts zu verhindern.

| Zustand | Bedeutung | Verhalten bei Tool-Calls |
| :--- | :--- | :--- |
| `Loading` | Solution wird im Hintergrund geladen | Liefert `[INFO]: Server laedt die Solution noch. ...` (kein Fehler, `isError=false`). Agent-Loops sollten kurz warten (z. B. 2–5 s) und erneut anfragen. |
| `Loaded` | Solution erfolgreich geladen und indexiert | Alle zielgebundenen Tools liefern reguläre Ergebnisse. |
| `LoadFailed` | Solution konnte nicht geladen werden (z. B. Syntaxfehler, fehlende Projekte) | Liefert strukturierte Diagnose mit Fehlerursache und Behebungshinweis. |

---

## 3. Daemon-Transport & Named-Pipe-Vertrag

`--daemon-start` startet den internen `DaemonHost` über den `Mcp/Daemon/`-Transportvertrag. Der Host akzeptiert benutzergebundene Named Pipes, führt den Pipe-Level-Handshake aus und erstellt je Verbindung eine MCP-SDK-Session gegen eine gemeinsame `ProjectRegistry`. Der bestehende `--mcp-server`-Stdio-Vertrag bleibt nach außen unverändert; intern verbindet der ThinClient zuerst und startet den Host erst bei Bedarf. Mit `AINETLINTER_NO_DAEMON=1` kann der direkte In-Proc-Pfad für Debugging gewählt werden.

### Endpunkt-Benennung und Instanz-Isolation
- Standard-Endpunkt: `ainetlinter.analyzer.v1.<username>` mit `PipeOptions.CurrentUserOnly`.
- Mit `--daemon-instance <id>` wird der Endpunkt um `.<id>` erweitert; die ID wird vorher invariant in Kleinbuchstaben normalisiert.
- ThinClient und detached `--daemon-start` verwenden dieselbe Instanz-ID.
- MRU-State liegt ohne ID unter `%LOCALAPPDATA%\RalfHuesing\AiNetLinter\daemon-state.json`, mit ID unter `daemon-state.<id>.json`.
- Beide verwenden newline-delimited JSON-Objekte.
- Der Pipe-Level-Handshake ist von der MCP-SDK-Interpretation getrennt. Der ThinClient akzeptiert nur einen Daemon mit passendem Discovery-Fingerprint. Meldet ein bestehender Daemon einen Mismatch, wird dieser als terminaler Fehler auf `stderr` (`DISCOVERY_FINGERPRINT_MISMATCH`) mit Exit-Code 2 beendet.

### Lebensdauer und Idle-Exit
- Der Host beendet sich nach standardmäßig 10 Minuten (`--mcp-daemon-idle-exit-minutes`) ohne aktive Verbindungen, Loads oder Warmups.
- Ein debounced MRU-State hält bis zu 4 zuletzt verwendete Projektroots; beim Start werden höchstens 2 davon parallel vorgewärmt.
- Der ThinClient verwendet genau einen Replay-/Reconnect-Versuch für den read-only Wire; der zweite Rohfehler wird ohne Endlosschleife beendet.
- Konkurrierende Detached-Starts werden pro Benutzer serialisiert und vor dem Spawn nochmals gegen den bestehenden Daemon geprüft.
- `welcome` liefert die identifizierte Daemon-PID und `connectionId` für Diagnose und sichere Beendigung.

---

## 4. Parent-Prozess-Überwachung (Watchdog)

Ohne weitere Argumente ermittelt der ThinClient unter Windows die PID des aufrufenden MCP-Hosts über `NtQueryInformationProcess` und beendet sich sauber, sobald dieser Prozess endet. Auf anderen Betriebssystemen ist keine automatische Parent-PID-Ermittlung implementiert.

Für Wrapper-Skripte oder Plattformen ohne automatische Erkennung kann die Ziel-PID explizit übergeben werden:

```bash
ainetlinter --mcp-server --parent-pid 1234
```

Sobald die überwachte PID nicht mehr erreichbar ist, wird der Shutdown ausgelöst.

---

## 5. stdout-Schutz (strukturelle JSON-RPC-Absicherung)

Der registrierte `ainetlinter`-Prozess nutzt `stdout` **ausschließlich** für JSON-RPC-Nachrichten:
- Der ThinClient verarbeitet nur den Pipe-Level-Handshake und pumpt danach alle MCP-Bytes opak durch; weder MCP-SDK noch JSON-RPC-Parser werden im ThinClient geladen.
- Status-, Retry- und Hängerdiagnosen gehen ausschließlich auf `stderr` oder in die System-Logdatei.
- Dadurch wird verhindert, dass versehentliche Konsolenausgaben den JSON-RPC-Datenstrom des MCP-Clients beschädigen („Transport closed").

---

## 6. MCP-Zielvertrag (`targetPath`)

Jeder zielgebundene Analyse-, Wartungs- oder Audit-Aufruf erhält genau einen absoluten, vorhandenen `targetPath` zu einer konkreten `.sln`/`.slnx`-, `.dll`- oder `.exe`-Datei. Die Endung bestimmt Source oder Decompiled-Assembly.

- **Pfad-Validierung:** Relative, fehlende, nicht unterstützte oder auf ein Verzeichnis zeigende Pfade liefern `invalid_argument` mit Feldpfad und nächstem Schritt.
- **Single-Session:** Jede zielgebundene Toolantwort enthält im einzigen sichtbaren Content die Navigation mit `operation`, `completeness`, Target- und Snapshot-Hinweisen sowie gegebenenfalls einem nächsten Schritt.
- **Regel-Herkunft bei Source:** Für Source ist die konkrete Solution bindend: Der Analyse-Root ist ihr normalisiertes Elternverzeichnis. Regeln werden ausschließlich aus der optionalen Datei `ainetlinter-rules.json` direkt neben der Solution gelesen. Fehlt sie, bleibt Navigation möglich und Lint erhält den Status `not_configured`. Ungültige oder nicht lesbare Regeln sind ein Konfigurationsfehler. Es gibt keinen Fallback auf Arbeitsverzeichnis oder Elternordner.
- **Decompiled-Assembly-Ziele:** Verwaltete `.dll` oder `.exe` werden metadata-only analysiert und nicht ausgeführt. `WholeProjectDecompiler` materialisiert beim Laden eager einen Projekt-Snapshot, den `AssemblyRoslynWorkspaceFactory` als echte Roslyn-Dokumente in den Workspace lädt.

---

## 7. Compile-Diagnostics & Staleness-Invalidierung

### Compile-Diagnostics
Diagnostics werden während des Ladens ermittelt und im Cache gehalten. Sie werden bei Tool-Aufrufen ausgewiesen, um Agenten auf bestehende Build-Probleme hinzuweisen, bevor semantische Analysen interpretiert werden.

### Staleness-Invalidierung
Wird eine Quellcodedatei zwischen Tool-Aufrufen verändert:
- Erkennt der Server die Dateiänderung über Dateisystem-Beobachtung / Hashes.
- Aktualisiert den Roslyn-Syntaxbaum im Speicher atomar.
- Nachfolgende Tool-Aufrufe spiegeln unmittelbar den neuen Stand wider.
- Schlägt ein Refresh fehl, liefert der Server den vorherigen Stand mit dem Hinweis `degraded=true`, `freshness="stale"`, `degradedReason="refresh-failed"`.

---

## 8. System-Logging (`appsettings.json`)

AiNetLinter schreibt ein gemeinsames prozessinternes System-Logging (Serilog, Datei-Sink):
Es protokolliert Prozess- und Verbindungs-Lifecycle — Prozessstart mit Rolle/PID/Version/Argumente, CLI-Parsefehler, Daemon-Connect-or-Start, Handshake-Ergebnisse inklusive Ablehnungen (Versionskonflikt, Protokollversion), MCP-Session-Enden und -Ausnahmen, Parent-Watchdog-Abbrüche, Pipe-Pump-Enden mit Ursache, Idle-Exit und Exit-Codes.

### Konfigurationsdatei

Die Datei `appsettings.json` liegt neben der ausführbaren Datei:

```json
{
  "Logging": {
    "MinimumLevel": "Debug",
    "Directory": "logs",
    "RetainedFileCount": 14,
    "McpCallLogging": true
  },
  "AssemblyAnalysis": {
    "CacheRoot": "cache/asm",
    "DecompilationTimeoutSeconds": 180,
    "ResponseBudgetBytes": 16384
  }
}
```

| Schlüssel | Default | Bedeutung |
|:----------|:--------|:----------|
| `MinimumLevel` | `Debug` | `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` |
| `Directory` | `logs` | Log-Verzeichnis, relativ zur EXE (absoluter Pfad erlaubt) |
| `RetainedFileCount` | `14` | Anzahl behaltener Tagesdateien (1–365) |
| `McpCallLogging` | `true` | Genau ein Serilog-Event je abgeschlossenem MCP-Tool-Call; ohne Argumente/Response-Payloads |

### MCP-Tool-Call-Logging
Bei aktiviertem `Logging:McpCallLogging` schreibt der Server nach jedem abgeschlossenen Tool-Call genau ein Event mit `ToolName`, `DurationMs` und `IsError`. Bei `IsError=true` kommt `ErrorCode` hinzu; im Daemon-Modus wird zusätzlich `ConnectionId` protokolliert. Es werden weder Argumente noch Response-Payloads mitgeschrieben.

### Eager Assembly-Decompilation (`AssemblyAnalysis`)
- `CacheRoot`: Persistent gespeicherte Dekompilate (Default: `cache/asm`).
- `DecompilationTimeoutSeconds`: Timeout für die Dekompilierung ganzer Assemblies (Default: 180 s).
- `ResponseBudgetBytes`: Standardbudget serialisierter Assembly-Antworten (1–32768 Bytes).
- Im MCP-/Daemon-Modus können die Settings über CLI-Overrides (`--mcp-external-max-disk-bytes`, `--mcp-external-max-memory-bytes`, `--mcp-external-max-parallel-operations`, `--mcp-external-max-resident-resources`, `--mcp-external-idle-ttl-minutes`) angepasst werden.

### Log-Format und Ablage
Täglich rollende Dateien `ainetlinter-<yyyyMMdd>.log` im konfigurierten Verzeichnis:
```text
2026-08-24 20:31:37.233 +02:00 [INF] [thin-client] System-Logging initialisiert (Level=Debug, ...)
2026-08-24 20:31:38.499 +02:00 [INF] [daemon] Daemon: Handshake fuer Verbindung 1 abgeschlossen (Status="Accepted", ClientPid=13336, AktiveVerbindungen=1)
```
Das Feld hinter dem Level ist die Prozessrolle: `cli`, `thin-client` oder `daemon`.
