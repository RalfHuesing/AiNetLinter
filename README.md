# AiNetLinter

AiNetLinter ist ein Roslyn-basierter MCP-Server und CLI-Linter für C#-Solutions
und lokale .NET-Assemblies. Der MCP-Server stellt Coding-Agents semantischen
Codekontext, Auswirkungsanalysen, Metriken und regelbasierte Befunde bereit; die
CLI führt dieselbe Analyse als Batch-Lauf oder Quality Gate aus.

Source-Targets (`.sln`, `.slnx`) werden über Roslyn analysiert. Lokale
verwaltete Assemblies (`.dll`, `.exe`) werden für die Analyse metadata-only
dekompiliert und nicht ausgeführt.

## MCP-Server für Coding-Agents

Der Server verwendet lokalen stdio-Transport über JSON-RPC 2.0. Er lässt sich in
MCP-Hosts mit lokalem stdio-Support registrieren, etwa Cursor, Claude Code,
Codex, GitHub Copilot oder Windsurf.

| Bereich | MCP-Fähigkeiten |
| :--- | :--- |
| Symbolnavigation | Symbolsuche, Member-Struktur, Quellcode-Fenster, Referenzen, Call-Trees sowie Typ-Hierarchien und Implementierungen. |
| Änderungsanalyse | Git-Diff- und Symbol-Impact, Abhängigkeitsgraphen und statische Testzuordnung. |
| Qualitätsanalyse | Konfigurierbare Regelverstöße, Metriken, Hotspots, Duplikate, Dead-Code- und Magic-Value-Kandidaten sowie Quality Gates. |
| Assembly-Analyse | Öffentliche API, Signaturen, Extension-Methoden, Dekompilat und Metadaten lokaler `.dll`- und `.exe`-Dateien. |

### MCP-Protokollvertrag für Agenten

Jeder zielgebundene Aufruf verwendet einen absoluten `targetPath` zu einer
Solution oder Assembly. Die Dateiendung bestimmt die Route: Source oder
dekompilierte Assembly. Antworten enthalten lesbares Markdown und
`structuredContent`; Symbol-IDs, Snapshot-Metadaten, Vollständigkeitsstatus,
Trunkierung und gegebenenfalls Continuation-Tokens machen gezielte Folgeaufrufe
maschinell auswertbar. Die aktuelle Tool- und Parameterschnittstelle wird über
`tools/list` veröffentlicht.

## Schnellstart

1. Die [aktuelle Windows-x64-Release](https://github.com/RalfHuesing/AiNetLinter/releases/latest)
   herunterladen und entpacken. Das Verzeichnis mit `AiNetLinter.exe` entweder
   dem `PATH` hinzufügen oder im MCP-Host als absoluter Pfad verwenden.
2. Den Server im MCP-Host registrieren:

```json
{
  "mcpServers": {
    "ainetlinter": {
      "command": "C:\\Tools\\AiNetLinter\\AiNetLinter.exe",
      "args": ["--mcp-server"]
    }
  }
}
```

3. Der Agent verwendet bei zielgebundenen Tool-Aufrufen einen absoluten
   `targetPath`, beispielsweise `C:\\Code\\MeinProjekt\\MeinProjekt.slnx`.

Eine Source-Solution kann direkt daneben eine optionale
`ainetlinter-rules.json` enthalten. Sie aktiviert das Linting; die semantische
Navigation bleibt ohne Regeldatei verfügbar.

## CLI-Linter

Der Batch-Modus prüft eine Solution gegen eine Regelkonfiguration und gibt
Befunde mit Exit-Code aus. Er unterstützt Baselines für inkrementelle
Einführung, automatische Roslyn-basierte Fixes, Caches und CI-Quality-Gates.

```powershell
AiNetLinter.exe --config .\ainetlinter-rules.json --path .\src\MeinProjekt.slnx
```

## Dokumentation

| Dokument | Inhalt |
| :--- | :--- |
| [MCP-Bootstrap](Docs/mcp-bootstrap.md) | Einmalige Einrichtung eines Projekts für MCP-Hosts und Agents. |
| [MCP- und CLI-Referenz](Docs/agent-api.md) | Aktuelle CLI-Optionen, Toolverträge, Antworten, Fehler und Capability-Matrix. |
| [Projektintegration](Docs/integration.md) | Regeldatei, Baseline, CI und MCP-Registrierung. |
| [Konfiguration](Docs/configuration.md) | `ainetlinter-rules.json`, Regeln, Defaults, Profile und External-Source-Mapping. |
| [Rationale](Docs/rationale.md) | Grundlagen der Regelprinzipien. |

Die eingebettete Kurzreferenz ist auch über `AiNetLinter.exe --docs <name>`
verfügbar; die gültigen Namen stehen in `AiNetLinter.exe --help`.

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
