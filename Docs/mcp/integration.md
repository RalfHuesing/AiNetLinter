# MCP-Host-Integration

[Bootstrap](mcp-bootstrap.md) · [Toolverträge](tools.md) · [Daemon und Diagnose](server.md)

## Registrierung

Für Hosts mit `mcpServers`-Konfiguration:

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "ainetlinter",
      "args": ["--mcp-server"]
    }
  }
}
```

Konfigurationsort und äußeres Dateiformat bestimmt der Host. Wenn die EXE nicht im PATH liegt, ihren absoluten Pfad einsetzen. `ainetlinter --docs mcp-bootstrap` bzw. `ainetlinter://agent-guide` hängen den tatsächlichen Laufzeitpfad an; bei Start über `dotnet` steht die AiNetLinter-DLL als erstes Argument im Laufzeitblock.

Keine `--path`-/`--config`-Argumente: Sie sind im MCP-Modus ungültig. Das Host-cwd wählt kein Projekt aus; pro Aufruf gilt der absolute `targetPath` der konkreten Solution oder Assembly.

## Erstaufruf und Resources

| Bedarf | Resource/Aufruf |
| --- | --- |
| Einrichtung einmalig | `ainetlinter://agent-guide` |
| Parameter der laufenden Version | `tools/list` |
| Zielstatus | `ainetlinter://overview?targetPath=<URL-kodierter-absoluter-Pfad>` |
| Effektive Source-Regeln | `ainetlinter://rules?targetPath=<URL-kodierter-absoluter-Solutionpfad>` |
| Serverweite Kapazität | `get_server_health` ohne Target |
| Einzelnes Target und Diagnosen | `get_server_health(targetPath, includeDiagnostics=true)` |

Der Handshake wartet nicht auf das Solution-Laden. Während des Ladens liefern betroffene Tools `operation=retry` mit `isError=false`; danach denselben Aufruf wiederholen. Globale `ServerInstructions` sind leer; Bootstrap und Tool-Schemas werden gezielt abgerufen.

## Getrennte Daemon-Instanz

```json
{"command":"ainetlinter","args":["--mcp-server","--daemon-instance","beta"]}
```

Die ID beginnt mit einem ASCII-Buchstaben, enthält danach nur ASCII-Buchstaben, Ziffern, `.`, `_`, `-` und ist höchstens 32 Zeichen lang. Normalisierung in Kleinbuchstaben: `BETA` und `beta` adressieren dieselbe Instanz. Named Pipe, Startup-Gate und MRU-State sind pro Instanz getrennt. Details: [Daemon-Transport](server.md#3-daemon-transport--named-pipe-vertrag).

## Agenten-Loop

Aufgabenspezifische Werkzeugwahl und kopierbare Beispiele stehen ausschließlich in [tools.md](tools.md). Die statische Testzuordnung von `get_test_context` und `get_impact` führt keine Tests aus. Ein Gate-Nachweis stammt aus einem abgeschlossenen `verify`; Kontext- und Advisory-Antworten ersetzen ihn nicht.
