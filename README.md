# AiNetLinter — .NET-Linter und MCP-Server für C#-Agentenworkflows

AiNetLinter analysiert C#-Code in `.sln`- und `.slnx`-Solutions mit Roslyn
gegen die in `rules.json` konfigurierte Regelmenge. Es kann als CLI-Batch-Tool
oder als [MCP](https://modelcontextprotocol.io)-Server betrieben werden; beide
Modi verwenden dieselbe Analyse-Engine.

| Modus | Schnittstelle | Aufgabe |
| :--- | :--- | :--- |
| CLI-Batch-Modus | `ainetlinter --config … --path …` | Führt einen Lint-Lauf aus, schreibt einen Markdown-Report nach stdout und setzt einen Exit-Code. |
| MCP-Server-Modus | `ainetlinter --mcp-server` | Stellt die Analyse als einzeln abfragbare Tools für einen laufenden Coding-Agenten bereit. |

Compilerfehler und Laufzeitverhalten sind nicht Gegenstand der Analyse. Build
und Tests bleiben eigenständige Prüfungen.

---

## Ablauf im agentischen Entwicklungsworkflow

Die MCP-Tools stellen dem Agenten Informationen zu einzelnen Symbolen,
Zusammenhängen und dem aktuellen Regelstand bereit.

| Arbeitsphase | Abfragen | Gelieferte Informationen |
| :--- | :--- | :--- |
| Orientierung vor einem Edit | `get_feature_context`, `find_symbol`, `get_file_skeleton`, `get_symbol_body` | Deklarationen, Member-Struktur, Metriken, direkte Aufrufer, statische Test-Zuordnung und aktuelle Regelverstöße. |
| Abhängigkeiten untersuchen | `find_references`, `get_call_tree`, `get_type_hierarchy`, `dependency_graph` | Aufrufstellen, Aufrufer-/Aufgerufene-Bäume, Vererbung und semantische Typreferenzen. |
| Änderung und Tests einordnen | `get_impact`, `get_test_context` | Betroffene Call-Sites; für Diff-Kontext auch geänderte Symbole, statische Test-Zuordnungen und ausführbare Testfilter. |
| Nach einem Edit prüfen | `get_violations`, `metrics_lookup`, `safeguard` | Aktuelle Verstöße für einen Scope, Symbolmetriken mit Schwellwert-Abgleich sowie Score und Pass/Fail gegen einen konfigurierten Grenzwert. |
| Repository-Audit | `metrics_tree`, `pattern_detect`, `find_duplicates`, `find_dead_code`, `find_magic_values` | Aggregierte Strukturmetriken und Kandidaten für konfigurierbare Pattern, Duplikate, unreferenzierten Code und Magic Values. |

Für Textmuster, Konfiguration und Nicht-C#-Dateien stehen `search_pattern` und
bei lokaler Datei-/Zeilenarbeit auch `rg` zur Verfügung. Symbol-, Referenz- und
Impact-Fragen werden über die Roslyn-basierten MCP-Tools beantwortet.

---

## CLI-Batch-Modus

```powershell
ainetlinter --config rules.json --path .\src\MeinProjekt.slnx
```

Der Lauf liefert Exit-Code `0` ohne neue Verstöße und `1`, wenn Verstöße
gefunden wurden. Weitere CLI-Funktionen sind unter anderem:

- `--fix --check` und `--fix` für die Vorschau und Anwendung einfacher Roslyn-basierter Fixes;
- `--create-baseline` und `--baseline` für inkrementelle Einführung in bestehenden Code;
- `--impact` für eine semantische Diff-Impact-Analyse;
- `--playbook` und `--sync-agent-rules` für aus dem Repository bzw. der Konfiguration erzeugte Agenten-Kontexte;
- `--list-rules`, `--describe-rule` und `--docs` zur Discovery ohne Lint-Lauf.

Parameter, Exit-Codes und vollständige Workflows: [Docs/agent-api.md](Docs/agent-api.md).

---

## MCP-Server-Modus

Registrierung in einem MCP-Host:

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

Jeder projektbezogene Tool-Aufruf erhält einen absoluten `projectRoot`. Im
Projektroot liegt die Definition der Solution und Regeldatei:

```json
{
  "solution": "src/MeinProjekt.slnx",
  "rules": "rules.json"
}
```

Diese Datei heißt `ainetlinter.project.json`; ihre Pfade werden relativ zu ihr
aufgelöst. Im MCP-Modus gehören `--path` und `--config` nicht in die
Registrierung. `ainetlinter://agent-guide` stellt den Agentenleitfaden bereit;
`tools/list` liefert die aktuellen Tool- und Parameterschemas.

Registrierung, Projektvertrag und Tool-vs.-Textsuche: [Docs/integration.md](Docs/integration.md#mcp-server-registrieren).

---

## Konfigurierbare Analysebereiche

`rules.json` definiert Regeln und Grenzwerte. Dazu gehören unter anderem
Komplexitäts- und Strukturmetriken, der AI-Context-Footprint, projekt- und
pfadspezifische Overrides, Baselines sowie Suppressions. Die Web-Analyse für
CSS, JavaScript und Razor wird über `Web.IsEnabled` aktiviert.

Die vollständige Konfigurationsreferenz enthält alle Regel-IDs, Felder und
Standardwerte: [Docs/configuration.md](Docs/configuration.md).

---

## Dokumentation

Die Dokumente sind in die Binary eingebettet und können ohne Netzzugriff über
`ainetlinter --docs <name>` ausgegeben werden.

| Dokument | Inhalt |
| :--- | :--- |
| [Docs/agent-api.md](Docs/agent-api.md) | Alle CLI-Flags, MCP-Tools, Parameter, Output- und Fehlerverträge. |
| [Docs/integration.md](Docs/integration.md) | Integration in ein bestehendes Projekt, Baseline und MCP-Registrierung. |
| [Docs/configuration.md](Docs/configuration.md) | `rules.json`-Schema, Regeln, Defaults und Deployment-Hinweise. |
| [Docs/rationale.md](Docs/rationale.md) | Design-Entscheidungen und Quellen zur Regelauswahl. |
| [Docs/ROADMAP.md](Docs/ROADMAP.md) | Entwicklungshistorie und abgeschlossene Vorhaben. |

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
