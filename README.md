# AiNetLinter — .NET-Linter und MCP-Server für agentische Entwicklungsworkflows

`AiNetLinter` ist ein .NET 10 CLI-Tool, das C#-Code per Roslyn-Syntaxanalyse gegen konfigurierbare Qualitätsregeln prüft. Die Regeln sind auf den agentischen Entwicklungsworkflow mit AI-Tools wie Cursor, Claude Code oder GitHub Copilot ausgelegt — mit dem Ziel, die Fehlerrate autonomer Agenten beim Bearbeiten von C#-Code zu senken. Die wissenschaftlichen Grundlagen der Regelauswahl sind in der [Design-Rationale](Docs/rationale.md) dokumentiert.

Der Fokus unterscheidet sich damit von allgemeiner Code-Qualität und menschlicher Lesbarkeit, die etablierte C#-Analyzer bereits abdecken: Die Regeln zielen gezielt darauf, was ein LLM beim autonomen Editieren zuverlässig erfassen und korrekt manipulieren kann — z. B. Kontextfenster-Budget pro Klasse (`AIContextFootprint`), Verwechslungsgefahr bei überladenen Methoden oder State-Management-Fehler bei autoregressiver Codegenerierung.

Das Tool läuft in zwei unabhängigen Modi:

| Modus | Was es tut |
| :--- | :--- |
| **CLI-Batch-Modus** | Ein Lint-Lauf gegen eine Solution: Markdown-Report auf stdout, CI-tauglicher Exit-Code, optionaler Auto-Fixer für triviale Verstöße. |
| **MCP-Server-Modus** (`--mcp-server`) | Stdio-basierter [MCP](https://modelcontextprotocol.io)-Server, der dieselbe Roslyn-basierte Solution-Analyse als einzeln abfragbare Tools (Symbolsuche, Referenzen, One-Shot Feature-Kontext, Impact-Analyse, Lint-Status, Namespace-Baum u. a.) direkt in einen laufenden AI-Coding-Agenten einbindet, statt nur einen fertigen Report auszugeben. |

Beide Modi teilen sich dieselbe Analyse-Engine und dieselbe `rules.json`-Konfiguration.

---

## Wann einsetzen?

AiNetLinter ist **kein Ersatz für Compiler oder Tests** — es setzt dort an, wo Build und Tests bereits grün sind:

```
dotnet build  ✓
dotnet test   ✓
ainetlinter   ← hier
```

Der Linter prüft keine Syntaxfehler oder Laufzeitverhalten, sondern Designqualität: Komplexität, KI-taugliche Codestruktur, Architektur-Constraints.

---

## CLI-Batch-Modus

### Schnellstart

```bash
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx
```

Der Linter gibt einen Markdown-Report auf stdout aus und beendet sich mit Exit-Code `0` (keine neuen Verstöße) oder `1` (Verstöße gefunden — CI-tauglich).

MCP-Call-Logs können unabhängig vom Server und ohne Solution-Load ausgewertet werden:

```bash
ainetlinter --analyze-mcp-log "%LOCALAPPDATA%/RalfHuesing/McpObservability/ainetlinter" --format text
ainetlinter --analyze-mcp-log "./.mcp-log/**/*.jsonl" --format json
```

Das Kommando durchsucht Verzeichnisse und Globs rekursiv, schließt Feedback-Logs aus und berichtet Tool-Nutzung, Fehler, Loading-Retry-Bursts, Antwortvollständigkeit sowie prozess-/dateibasierte Sequenzen.

### Agentische Integration

Die eingebauten Discovery-Commands ermöglichen einem KI-Agenten, das Tool explorativ zu verstehen und eigenständig in ein Projekt zu integrieren — ohne Vorab-Konfiguration durch den Entwickler.

```bash
# Tool erkunden (kein --path nötig):
ainetlinter --list-rules
ainetlinter --describe-rule EnforceSealedClasses
ainetlinter --docs configuration

# Lint-Lauf:
ainetlinter --config rules.json --path ./src/MeinProjekt.slnx

# Auto-Fix für triviale Verstöße (sealed, nullable, PascalCase):
ainetlinter --config rules.json --path ./src/ --fix --check   # Dry-Run: zeigt Anzahl, schreibt nichts
ainetlinter --config rules.json --path ./src/ --fix
```

**Typischer Einstieg:** `AiNetLinter` in ein eigenes Verzeichnis außerhalb des Projekts installieren (z. B. `C:\Tools\AiNetLinter\`). Das Tool bringt mehrere Dateien mit, lässt sich so von mehreren Projekten gleichzeitig nutzen, und Updates sind an einer einzigen Stelle erledigt. Den Pfad zur Exe einem Agenten im Projektkontext übergeben — dieser exploriert das Tool über die Discovery-Commands und integriert es eigenständig, z. B. als Schritt in einem Test- oder CI-Skript.

Vollständige Agent-API-Referenz (alle Flags, Workflows, Error-Format): [Docs/agent-api.md](Docs/agent-api.md). Schritt-für-Schritt-Integrationsanleitung (Verzeichnisstruktur, Baseline, Agent-Regeln-Sync): [Docs/integration.md](Docs/integration.md).

---

## MCP-Server-Modus

```bash
ainetlinter --mcp-server                              # sucht .sln/.slnx im aktuellen Verzeichnis
ainetlinter --mcp-server --path ./src/MeinProjekt.slnx # explizite Ziel-Solution
ainetlinter --mcp-server --parent-pid 1234             # optionale explizite Parent-PID
```

Der Server lädt die Solution einmal beim Start über `MSBuildWorkspace` und hält sie über die Prozesslaufzeit resident — Tool-Calls arbeiten gegen den geladenen Zustand statt gegen wiederholte Disk-Scans, und werden bei Dateiänderungen inkrementell aktualisiert (Datei-`mtime` + SHA-256-Hash-Vergleich, kein Komplett-Reload).

Für Legacy-MCP liefert `initialize` die globale Server-Anleitung. MCP `2026-07-28` verwendet dafür `server/discover` mit Protokollversion, Client-Info und Client-Capabilities unter `params._meta`; Folge-Requests wie `tools/list` führen diese Metadaten weiter. Der globale Instructions-Text verweist auf `tools/list` und `ainetlinter://overview`, statt die Toolliste zu duplizieren.

Im MCP-Modus überwacht der Server automatisch den aufrufenden Host-Prozess und beendet sich bei dessen Ende sauber. Mit `--parent-pid <pid>` kann die zu überwachende Prozess-ID für Wrapper-Skripte oder Spezialumgebungen explizit gesetzt werden.

| Tool | Zweck |
| :--- | :--- |
| `get_feature_context` | Composite One-Shot-Exploration vor Edits/Refactorings: bündelt Deklaration, Metriken, direkte Aufrufer, statische Test-Zuordnung und Linter-Violations |
| `get_test_context` | Statische Test-Zuordnung für ein C#-Symbol: ermittelt zugeordnete Testdateien, Testklassen, Testmethoden, Kategorien und direkt ausführbare `dotnet test` Filterbefehle |
| `get_namespace_tree` | Hierarchischer Namespace- und Typ-Baum (3 Zoom-Stufen: Solution-Overview, Namespaces, Typ-Liste mit Datei/Zeile/Sichtbarkeit) |
| `find_symbol` | Klassen/Methoden/Properties/Interfaces per Namensmuster finden |
| `find_references` | Aufrufstellen eines Symbols (optional transitiv über `depth`), mit strukturierten Treffern und Vollständigkeitsmetadaten |
| `get_impact` | Betroffene Call-Sites für uncommittete Änderungen oder ein Symbol; der Symbol-Branch liefert dieselbe transitive Struktur wie `find_references` |
| `get_type_hierarchy` | Basisklassen, Interfaces, abgeleitete Typen, heuristische DI-Registrierungen |
| `get_call_tree` | Aufrufer- oder Aufgerufene-Baum eines Symbols (Eltern-Kind-Struktur, ASCII oder Mermaid), Richtung `incoming`/`outgoing`/`both`, transitiv über `depth` |
| `dependency_graph` | Datei-/Typ-Abhängigkeiten (echte SemanticModel-Typreferenzen statt `using`-Direktiven), ein-/ausgehend, transitiv |
| `get_file_skeleton` | Struktur-Skelett einer oder mehrerer C#-Dateien (Batch-Support in 1 Turn) |
| `get_class_structure` | Tabellarische Member- und Zeilen-Übersicht eines Typs (Kind, Name, Visibility, Start-/End-Zeile, Signatur); `maxMembers` (Default 50, Cap 200) + `sortBy` (`lines`/`kind`/`name`); bei `record`-Typen werden Primary-Constructor-Parameter als eigene Zeilen ausgegeben |
| `get_symbol_body` | Source-Body eines oder mehrerer C#-Symbole (Batch-Support in 1 Turn) per stabiler ID oder Name |
| `get_index_scope` | Dateityp-Aufschlüsselung der geladenen Solution |
| `get_hotspots` | Dateien nahe oder über dem `MaxLineCount`-Limit |
| `metrics_lookup` | Punktgenaue Metriken (LOC, Komplexität, Parameter, AI-Footprint) und Schwellwert-Abgleich für ein oder mehrere Symbole (Batch-Support in 1 Turn) |
| `metrics_tree` | ASCII-Baum mit aggregierten Werten pro Verzeichnisknoten (Code-Größe, Kommentaranteil, Lint-Verstöße, Komplexität), Ebene für Ebene explorierbar |
| `get_violations` | Aktuelle Lint-Verstöße für einen Scope |
| `pattern_detect` | Lint-Verstöße nach Pattern-Kategorie gruppiert (God-Class, async-void, lange Methoden, Public-API ohne Doc, leere Catch-Blöcke, Feature-Envy) statt flacher Datei-Liste |
| `find_magic_values` | On-Demand-Audit nach Magic Values (URLs, Pfade, Timeouts, Format-Strings, Schwellenwerte, HTTP-Statuscodes) in C#-Quellcode |
| `find_dead_code` | On-Demand-Audit nach totem/unreferenziertem C#-Code (Methoden, Typen, Properties) mit Confidence-Stufen |
| `find_duplicates` | Token-basierte Duplikat-Suche (Clone-Detection, Jaccard-N-Gram) und Refactoring-Drift-Erkennung (Helper wird strukturell nachgebaut statt aufgerufen) |
| `safeguard` | Deterministischer 0–10-Qualitätsscore inkl. Pass/Fail gegen einen Schwellenwert |
| `search_pattern` | Text-/Regex-Suche über alle Dateitypen (Fallback für Nicht-C#-Treffer); `enrichCSharp=true` ordnet sichtbare Treffer geladener C#-Dokumente optional ein |
| `report_observability_feedback` | Strukturierte Bug-Reports, False-Positives oder Feature-Wünsche von Agenten an das System melden |
| `reload_config` | `rules.json` zur Laufzeit neu einlesen, ohne Server-Neustart |
| `get_server_health` | LoadState, geladene Solution/Config, Uptime, aktuelle Call-Log-Aggregate |

Registrierung im MCP-Host (Claude Code, Cursor, eigene Agent-Loops):

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

Vollständige Tool-Referenz (Parameter, Trunkierung, Error-Codes, Call-Log): [Docs/agent-api.md#mcp-server-modus](Docs/agent-api.md#mcp-server-modus). Registrierungs-Anleitung inkl. Mehrdeutigkeits-Behandlung und Tool-vs-`rg`-Empfehlung: [Docs/integration.md#mcp-server-registrieren](Docs/integration.md#mcp-server-registrieren).

`search_pattern` behält den Legacy-Text und liefert zusätzlich ein strukturiertes Ergebnis mit
solution-relativen Pfaden, Positionen, Match-Bereichen, Kontext und Vollständigkeitsstatus.
`enrichCSharp` ist standardmäßig `false`; bei `true` ergänzt die Suche für sichtbare Treffer
geladener C#-Dokumente ein `semantic`-Objekt. Kommentare und Strings werden als nicht anwendbar
gekennzeichnet, während mehrdeutige oder außerhalb des Roslyn-Snapshots liegende Auflösungen als
`ambiguous` bzw. `unavailable` sichtbar bleiben.

---

## Ausgewählte Regeln und Features

| Regel/Feature | Zweck |
| :--- | :--- |
| **Baseline / Ratchet** (`--baseline`) | Friert bestehende Verstöße per SHA-256 ein — nur geänderte Dateien werden geprüft. Ermöglicht den Einsatz in Legacy-Projekten mit bestehenden Verstößen, ohne diese vorher beheben zu müssen. |
| **AI-Context-Footprint** (`MaxAIContextFootprint`) | Misst die transitiven Codezeilen, die ein KI-Modell für eine Klasse laden müsste. Direkte Metrik für Kontextbudget-Verbrauch im agentischen Workflow. |
| **Phantom-Dependency-Ban** (`DetectAndBanPhantomDependencies`) | Verbietet nicht auflösbare Namespaces und Reflection-Lade-APIs. |
| **Komplexitätsgrenzen** (`MaxCyclomaticComplexity`, `MaxCognitiveComplexity`) | McCabe- und SonarSource-Kognitiv-Komplexitätsmetriken pro Methode. |
| **Project Overrides** (`ProjectOverrides`) | Projektscharfe Regelanpassungen (z. B. `*.Tests` mit anderen Limits) statt einer einzigen Konfiguration für alle Projekte. |
| **Compound-Suppressions** (`CompoundSuppressions`) | Kontextabhängige Regelunterdrückung inkl. `SeverityOverride: "warning"` — Verstöße in konfigurierten Szenarien können auf Warning herabgestuft werden, ohne den Build zu blockieren. |
| **LINQ-Kettenlänge** (`MaxLinqChainLength`) | Begrenzt die Anzahl verketteter LINQ-Methoden pro Ausdruckskette. Konfigurierbare Whitelist für Builder-Ketten. |
| **Globales Scope-Filtering** (`--project`, `--namespace`) | Eingrenzung der Analyse auf bestimmte Projekte oder C#-Namespaces (inkl. Wildcard-Unterstützung und Ausschluss-Shortcut für Test-Projekte). |
| **Suppression-Bypass** (`--ignore-suppressions`) | Umgeht Code-Unterdrückungen (`disable all` und inline `disable [Rule]`) dynamisch beim Linter-Lauf für konfigurierte Sprachklassen (`all`, `cs`/`c#`, `razor`, `js`, `css`). |
| **Web-Asset-Linting** (CSS, JS, Razor) | Analyse für CSS (ExCSS), JS (Esprima) und Razor: Dateigrößen-Limits, ES6-Modul-Pflicht, Verbot globaler `window`-Zuweisungen, HTML-Verschachtelungstiefe, Control-Flow-Blöcke, Komponenten-Parameter, Ternaries in HTML-Attributen. Opt-in über `Web.IsEnabled = true`. |

Vollständige, aktuelle Regel-Liste: `ainetlinter --list-rules`. Vollständige Konfigurationsreferenz: [Docs/configuration.md](Docs/configuration.md).

---

## Dokumentation

Alle Dokumente sind in die Binary eingebettet und ohne Netzzugriff per `ainetlinter --docs <name>` abrufbar (z. B. `ainetlinter --docs agent-api`).

| Dokument | Inhalt |
| :--- | :--- |
| [Docs/agent-api.md](Docs/agent-api.md) | Agent-API: alle CLI-Flags, Workflows, Error-Format, Discovery-Commands, MCP-Tool-Referenz |
| [Docs/configuration.md](Docs/configuration.md) | Vollständige Konfigurationsreferenz (`rules.json`-Schema, alle Regeln und Defaults) |
| [Docs/integration.md](Docs/integration.md) | Schritt-für-Schritt-Integration in ein bestehendes Projekt, MCP-Server-Registrierung |
| [Docs/rationale.md](Docs/rationale.md) | Design-Entscheidungen & wissenschaftliche Grundlagen |
| [Docs/ROADMAP.md](Docs/ROADMAP.md) | Entwicklungshistorie nach Epics |

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
