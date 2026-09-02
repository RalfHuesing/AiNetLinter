# AiNetLinter

AiNetLinter ist ein Roslyn-basierter MCP-Server für C#-Solutions und lokale
.NET-Assemblies (`.dll`/`.exe`). Er stellt Coding-Agenten gezielt abgefragten,
strukturierten Kontext zu Symbolen, Abhängigkeiten, Auswirkungen, Tests,
Metriken und Regelverstößen bereit.

Die Antworten sind begrenzt und weisen ihren Vollständigkeitsstatus aus.
Agenten können dadurch weitere Informationen gezielt nachladen, statt
standardmäßig große Teile eines Repositorys als Kontext zu verwenden. Die
zugrunde liegende Analyse-Engine ist zusätzlich als CLI-Linter für
konfigurierbare Regeln, Baselines, automatische Fixes und Quality Gates
verfügbar.

## Für Entwicklungs- und Agentenworkflows

Der MCP-Server unterstützt die Analyse vor, während und nach einer Änderung:

| Situation | Beispiele | AiNetLinter liefert |
| :--- | :--- | :--- |
| Vor einer Änderung | `get_feature_context`, `get_class_structure` | Deklaration, Member-Struktur, Metriken, direkte Aufrufer, statische Testzuordnung und Regelverstöße für ein Symbol. |
| Beim Nachvollziehen von Abhängigkeiten | `find_references`, `get_call_tree`, `get_type_hierarchy`, `dependency_graph` | Aufrufstellen, Aufrufer- und Aufgerufene-Bäume, Typ-Hierarchien und semantische Abhängigkeiten. |
| Nach einer Änderung | `get_impact`, `get_test_context`, `safeguard` | Betroffene Symbole und Dateien, statisch zugeordnete Tests, diffbezogene Regelverstöße und ein Quality Gate. |
| Beim Erkunden einer fremden Assembly | `inspect_assembly`, `find_assembly_extensions` | Öffentliche API, Typen, Member und klassische Extension-Methoden aus einer lokalen `.dll` oder `.exe`. |

Die MCP-Tools geben zu begrenzten Ergebnissen und nicht auflösbaren
Abhängigkeiten ihren Vollständigkeitsstatus aus. Agenten können ihren nächsten
Schritt daran ausrichten, statt den gesamten Repository-Inhalt als Kontext zu
laden. Build und Tests werden nicht ersetzt; sie bleiben eigenständige
Prüfungen. Die semantischen MCP-Abfragen sind auf C# ausgerichtet und machen
Grenzen wie nicht auflösbare Abhängigkeiten oder gekürzte Ergebnisse sichtbar.

## Externe Assemblies statisch analysieren

`inspect_assembly` und `find_assembly_extensions` untersuchen eine lokale
`.dll` oder `.exe` statisch über Roslyn-Metadaten. Die Assembly wird dafür nicht
geladen oder ausgeführt. Ohne verfügbare Quelle erzeugt AiNetLinter eine
dekompilierte, schreibgeschützte Analyse-Session.

Für eine eingebundene Fremd-Assembly kann zusätzlich eine passende
Source-Solution aus einem konfigurierten öffentlichen Git-Repository
zugeordnet werden. Dann arbeitet die Assembly-Analyse mit einem
schreibgeschützten Source-Snapshot dieser Solution. Herkunft, Snapshot und
mögliche unvollständige Abhängigkeiten bleiben im Ergebnis sichtbar.

Details zum Verhalten, zu Filtern und zum Konfigurationsvertrag:
[External-Source-Mapping](Docs/configuration.md#expliziter-external-source-mappingvertrag)
und [MCP- und CLI-Referenz](Docs/agent-api.md).

## Schnellstart

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

### Als CLI-Linter ausführen

```powershell
ainetlinter --config rules.json --path .\src\MeinProjekt.slnx
```

Der Lauf liefert einen Markdown-Report und einen Exit-Code: `0`, wenn keine
neuen Verstöße gefunden werden, und `1`, wenn Verstöße vorliegen. Für die
schrittweise Einführung stehen Baselines bereit; einfache Roslyn-basierte
Korrekturen lassen sich mit `--fix` anwenden. Aus der Regelkonfiguration
können außerdem Agenten-Regeln synchronisiert werden.

## Dokumentation

| Dokument | Inhalt |
| :--- | :--- |
| [MCP- und CLI-Referenz](Docs/agent-api.md) | Tools, Parameter, Antworten, Fehler und Capability-Matrix. |
| [Integration](Docs/integration.md) | Einbindung in ein bestehendes Projekt, Baseline, CI und MCP-Registrierung. |
| [Konfiguration](Docs/configuration.md) | `rules.json`, Regel-IDs, Defaults und `ExternalSources`. |
| [MCP-Bootstrap](Docs/mcp-bootstrap.md) | Einmalige Einrichtung für Agenten und MCP-Hosts. |

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
