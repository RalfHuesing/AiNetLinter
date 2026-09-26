# MCP-Bootstrap

Einmal pro Einrichtung lesen. Laufende Agenten beginnen bei [Werkzeugwahl und Verträgen](tools.md).

1. Vorhandene konkrete Zieldatei bestimmen: `.sln`/`.slnx` für Source, verwaltete `.dll`/`.exe` für Assembly. Jeder zielgebundene Aufruf benötigt ihren absoluten `targetPath`; Verzeichnisse und relative Pfade sind ungültig.
2. Server im Host registrieren: `command: "ainetlinter"`, `args: ["--mcp-server"]`. Host-spezifische Ablage: [Integration](integration.md). Der dynamisch angehängte Laufzeitblock liefert bei CLI-/Resource-Ausgabe den tatsächlichen Startpfad, falls PATH-Auflösung fehlt.
3. Für Source-Linting eine `ainetlinter-rules.json` direkt neben der Solution bereitstellen. Ohne Datei bleibt Navigation verfügbar; Lint ist `not_configured`. Keine Suche in Elternverzeichnissen oder im Host-cwd. Assembly-Linting ist `unsupported`.
4. Dauerhafte Workflowregel mit `ainetlinter --docs mcp-rule` abrufen und in das Regelverzeichnis des Hosts übernehmen (`.agents/rules` oder `.cursor/rules`). Vorhandene fremde Regeln erhalten.
5. `get_server_health` global abrufen; anschließend mit dem konkreten `targetPath` den Zielstatus prüfen. `tools/list` liefert die gültigen Parameter.

Registrierungsbeispiel:

```json
{"command":"ainetlinter","args":["--mcp-server"]}
```

Beispielargumente für eine zielgebundene Abfrage:

```json
{"targetPath":"C:\\repos\\MyApp\\MyApp.slnx"}
```

`--path` und `--config` sind in der MCP-Registrierung Startfehler. Für isolierte Daemons `--daemon-instance beta` ergänzen.

## Antwort- und Folgeaufrufvertrag

- Genau einen Text-Content-Block lesen; Fehler tragen `isError=true`. Status, Vollständigkeit und Handoff-IDs stehen im Content.
- `operation=retry`: Target lädt noch; kurz warten und denselben Aufruf wiederholen.
- `operation=running`: denselben Aufruf mit ausgegebenem `operationToken` fortsetzen. Erst das Endergebnis auswerten. `continuationToken` dient anschließend für Ausgabeseiten.
- `h:…` unverändert als Symboladresse übernehmen, `targetPath` beibehalten. `symbolIdentifiers` ist ein Array; `symbolIdentifier` ein Einzelwert. Nach `HANDOFF_UNKNOWN` das Symbol erneut suchen.
- Bei `RESPONSE_BUDGET_TOO_SMALL` mit dem ausgegebenen `minimumResponseBytes` als `maxResponseBytes` wiederholen.
- Leere/gekürzte/partielle Ergebnisse unterscheiden. Null Treffer bei partieller Abdeckung beweisen keine Abwesenheit.
- Für C#-Symbole semantische Tools nutzen; Text und Nicht-C# mit `search_pattern` untersuchen. Razor-Folgehinweise aus `find_references` ausführen.

[Tooldetails und Zustände](tools.md) · [Dead-Code-Grenzen](dead-code.md) · [Serverbetrieb](server.md)
