# AiNetLinter: Einstieg für Agenten

Nur den zur Aufgabe passenden Vertrag laden. AiNetLinter analysiert C#/.NET-Solutions mit Roslyn und lokale verwaltete Assemblies über Metadaten/Dekompilate; Web-Regeln sind separat konfigurierbar.

| Aufgabe | Lesen |
| --- | --- |
| Tools wählen, Antworten auswerten, Symbole verfolgen | [MCP-Werkzeugwahl und Verträge](mcp/tools.md) |
| Neue MCP-Integration einrichten | [Bootstrap](mcp/mcp-bootstrap.md), danach bei Bedarf [Host-Integration](mcp/integration.md) |
| Dead-Code-Kandidaten beurteilen | [Nutzungsregeln, Gegenproben und Grenzen](mcp/dead-code.md) |
| Regeln/Schwellenwerte/Ausnahmen ändern | [Konfiguration](linter/configuration.md) |
| CLI aufrufen, Exit-Code oder Baseline interpretieren | [CLI](linter/cli.md) |
| CLI in Zielprojekt/CI integrieren | [Integration](linter/integration.md) |
| Laden, Daemon, Cache oder Logs untersuchen | [Serverbetrieb](mcp/server.md) |
| Aussagekraft einer Regel/Metrik verstehen | [Analysegrenzen](rationale.md) |

## Vor der ersten Analyse

- MCP: absoluter vorhandener `targetPath` einer konkreten `.sln`/`.slnx`/`.dll`/`.exe`. CLI hat den eigenen `--path`-/`--config`-Vertrag.
- `tools/list` ist das Schema der laufenden Version. Handoff-IDs `h:…` unverändert weitergeben.
- `retry`/`running` sind Zwischenzustände. Nur fertige Antworten auswerten; Ausgabevollständigkeit und Analyseabdeckung getrennt prüfen.
- `verify` führt keine Tests aus. Dead-Code-/Pattern-/Duplicate-Kandidaten verlangen fachliche Gegenprüfung.
- Code-Defaults, mitgelieferte JSON-Vorlage und effektive Projektausnahmen können abweichen. Konfiguration gezielt lesen.

Die CLI bettet Dokumentation beim Kompilieren ein (`--docs <name>`). Änderungen an diesen Markdown-Dateien ändern eine bereits installierte EXE nicht. Dokumentnamen: [CLI-Referenz](linter/cli.md).
