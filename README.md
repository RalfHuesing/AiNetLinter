# AiNetLinter

AiNetLinter ist ein Roslyn-basierter Linter und MCP-Server für C#-Solutions.
Er prüft Code gegen konfigurierbare Regeln und stellt Coding-Agenten
semantische Informationen über Code, Abhängigkeiten und Änderungen bereit.

Im CLI-Modus schreibt AiNetLinter einen Markdown-Report und einen passenden
Exit-Code. Im MCP-Modus stellt es dieselbe Analyse-Engine als gezielt
abfragbare Werkzeuge bereit. Build und Tests werden nicht ersetzt; sie bleiben
eigenständige Prüfungen.

## Für Entwicklungs- und Agentenworkflows

| Situation | AiNetLinter liefert |
| :--- | :--- |
| Vor einer Änderung | Deklarationen, Member-Struktur, Metriken, Aufrufer, zugeordnete Tests und offene Regelverstöße für ein Symbol. |
| Beim Nachvollziehen von Abhängigkeiten | Referenzen, Aufrufer- und Aufgerufene-Bäume, Typ-Hierarchien und semantische Dateiabhängigkeiten. |
| Nach einer Änderung | Git-Diff-bezogene Auswirkungen, statisch zugeordnete Tests, Regelverstöße und ein Quality-Gate. |
| Beim Erkunden einer fremden Assembly | Öffentliche API, Typen, Member und klassische Extension-Methoden aus `.dll` oder `.exe` – ohne die Assembly zu laden oder auszuführen. |

Die MCP-Tools geben zu begrenzten Ergebnissen und nicht auflösbaren
Abhängigkeiten ihren Vollständigkeitsstatus aus. Agenten können ihren nächsten
Schritt daran ausrichten, statt den gesamten Repository-Inhalt als Kontext zu
laden.

### Externe Assemblies mit Quellkontext

`inspect_assembly` und `find_assembly_extensions` untersuchen eine lokale
`.dll` oder `.exe` statisch über Roslyn. Ohne verfügbare Quelle erzeugt AiNetLinter dafür
eine dekompilierte, schreibgeschützte Analyse-Session.

Bei `inspect_assembly` ist `memberNames` eine case-insensitive exakte OR-Auswahl;
`memberName` bleibt eine Teiltextsuche. Referenz-Assemblies werden bei
`find_assembly_extensions` nur mit `includeReferences=true` einbezogen (Default: `false`).

Für eine eingebundene Fremd-Assembly kann zusätzlich eine passende Source-Solution
aus einem konfigurierten öffentlichen Git-Repository zugeordnet werden. Dann
arbeitet die Assembly-Analyse mit einem schreibgeschützten Source-Snapshot
dieser Solution. Symbolsuche, Struktur-, Referenz- und Metrikabfragen beziehen
sich damit auf den Quellkontext; Herkunft, Snapshot und mögliche unvollständige
Abhängigkeiten bleiben im Ergebnis sichtbar.

Der Abschnitt `ExternalSources` in `appsettings.json` verweist auf die
Mapping-Datei und begrenzt die Ressourcen. Ohne gültige Quellzuordnung bleibt
die Dekompilierung der sichere Fallback. Details und der Konfigurationsvertrag:
[External-Source-Mapping](Docs/configuration.md#expliziter-external-source-mappingvertrag).

## Schnellstart

### Als CLI ausführen

```powershell
ainetlinter --config rules.json --path .\src\MeinProjekt.slnx
```

Der Lauf liefert Exit-Code `0`, wenn keine neuen Verstöße gefunden werden, und
`1`, wenn Verstöße vorliegen. Für die schrittweise Einführung stehen Baselines
bereit; einfache Roslyn-basierte Korrekturen lassen sich mit `--fix` anwenden.
Aus der Regelkonfiguration können außerdem Agenten-Regeln synchronisiert werden.

### Als MCP-Server registrieren

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

Im Projektroot liegt die Definition der zu analysierenden Solution und
Regeldatei:

```json
{
  "solution": "src/MeinProjekt.slnx",
  "rules": "rules.json"
}
```

Speichere diese Datei als `ainetlinter.project.json`. Zielgebundene MCP-Aufrufe
verwenden anschließend `targetType` (`project` oder `assembly`) und einen
absoluten `targetPath`. Für den Projektstart stellt
`ainetlinter://agent-guide` den Bootstrap bereit; `tools/list` beschreibt die
aktuell registrierten Tools und ihre Parameter.

## Dokumentation

| Dokument | Inhalt |
| :--- | :--- |
| [MCP- und CLI-Referenz](Docs/agent-api.md) | Tools, Parameter, Antworten, Fehler und Capability-Matrix. |
| [Integration](Docs/integration.md) | Einbindung in ein bestehendes Projekt, Baseline, CI und MCP-Registrierung. |
| [Konfiguration](Docs/configuration.md) | `rules.json`, Regel-IDs, Defaults und `ExternalSources`. |
| [MCP-Bootstrap](Docs/mcp-bootstrap.md) | Einmalige Einrichtung für Agenten und MCP-Hosts. |

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
