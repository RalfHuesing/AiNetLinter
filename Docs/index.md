# AiNetLinter: Einstieg für Agenten

Nur den zur Aufgabe passenden Vertrag laden. AiNetLinter analysiert C#/.NET-Solutions mit Roslyn und lokale verwaltete Assemblies über Metadaten/Dekompilate; Web-Regeln sind separat konfigurierbar.

Dieser Einstieg ist mit `ainetlinter --docs index` oder `ainetlinter -d index` abrufbar. Folgeaufrufe geben jeweils ein Dokument auf stdout aus; ein Target ist dafür nicht erforderlich.

| Aufgabe | Lesen | CLI-Aufruf |
| --- | --- | --- |
| Tools wählen, Antworten auswerten, Symbole verfolgen | [MCP-Werkzeugwahl und Verträge](mcp/tools.md) | `ainetlinter --docs mcp-tools` |
| Neue MCP-Integration einrichten | [Bootstrap](mcp/mcp-bootstrap.md) | `ainetlinter --docs mcp-bootstrap` |
| MCP-Host registrieren | [Host-Integration](mcp/integration.md) | `ainetlinter --docs mcp-integration` |
| Dead-Code-Kandidaten beurteilen | [Nutzungsregeln, Gegenproben und Grenzen](mcp/dead-code.md) | `ainetlinter --docs mcp-dead-code` |
| Regeln/Schwellenwerte/Ausnahmen ändern | [Konfiguration](linter/configuration.md) | `ainetlinter --docs configuration` |
| CLI aufrufen, Exit-Code oder Baseline interpretieren | [CLI](linter/cli.md) | `ainetlinter --docs cli` |
| CLI in Zielprojekt/CI integrieren | [Integration](linter/integration.md) | `ainetlinter --docs integration` |
| Laden, Daemon, Cache oder Logs untersuchen | [Serverbetrieb](mcp/server.md) | `ainetlinter --docs mcp-server` |
| Aussagekraft einer Regel/Metrik verstehen | [Analysegrenzen](rationale.md) | `ainetlinter --docs rationale` |

## Vor der ersten Analyse

- MCP: absoluter vorhandener `targetPath` einer konkreten `.sln`/`.slnx`/`.dll`/`.exe`. CLI hat den eigenen `--path`-/`--config`-Vertrag.
- `tools/list` ist das Schema der laufenden Version. Handoff-IDs `h:…` unverändert weitergeben.
- `retry`/`running` sind Zwischenzustände. Nur fertige Antworten auswerten; Ausgabevollständigkeit und Analyseabdeckung getrennt prüfen.
- `verify` führt keine Tests aus. Dead-Code-/Pattern-/Duplicate-Kandidaten verlangen fachliche Gegenprüfung.
- Code-Defaults, mitgelieferte JSON-Vorlage und effektive Projektausnahmen können abweichen. Konfiguration gezielt lesen.

Die CLI bettet Dokumentation beim Kompilieren ein (`--docs <name>`). Änderungen an diesen Markdown-Dateien ändern eine bereits installierte EXE nicht. Dokumentnamen: [CLI-Referenz](linter/cli.md).
