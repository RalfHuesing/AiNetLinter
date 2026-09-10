# AiNetLinter – Funktionsübersicht

AiNetLinter ist ein statisch kompiliertes .NET-CLI-Tool zur Analyse von
C#-Solutions und zur Durchsetzung konfigurierbarer Architektur- und
Codequalitätsregeln.

## Analyse und Konfiguration

- `ainetlinter-rules.json` steuert aktive Regeln, Schwellwerte,
  Projektausnahmen und Dateifilter.
- Die CLI analysiert Solutions und Projekte über Roslyn und gibt Befunde mit
  Regel, Schweregrad, Datei und Position aus.
- CSS-, JavaScript- und Razor-Dateien können zusätzlich über das opt-in
  Web-Modul geprüft werden.

Die vollständige Konfigurationsreferenz steht in
[configuration.md](configuration.md).

## MCP-Server

- `--mcp-server` startet einen stdio-basierten MCP-Server.
- Zielgebundene MCP-Aufrufe erhalten einen absoluten Pfad zu einer
  Solution-Datei oder lokalen Assembly.
- Der lokale Daemon hält Analysezustände für mehrere MCP-Clients resident und
  verwendet pro Benutzer eine Named Pipe mit `CurrentUserOnly`.

Toolverträge, Ressourcen und Antwortgrenzen beschreibt
[agent-api.md](agent-api.md). Die Einrichtung und Nutzung ist in
[integration.md](integration.md) dokumentiert.

## Dokumentation

- [README](../README.md): Einstieg und lokale Ausführung
- [configuration.md](configuration.md): Regel- und CLI-Konfiguration
- [agent-api.md](agent-api.md): MCP- und CLI-Referenz
- [integration.md](integration.md): Integration und Arbeitsabläufe
- [rationale.md](rationale.md): Begründung der Regelprinzipien
