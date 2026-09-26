# CLI in Zielprojekte integrieren

[CLI-Vertrag](cli.md) · [Konfiguration](configuration.md) · [MCP-Einrichtung](../mcp/mcp-bootstrap.md)

## Voraussetzungen und Dateien

- .NET 10 SDK und wiederhergestellte Abhängigkeiten der Ziel-Solution (`obj/project.assets.json`). Fehlende/veraltete Restore-Daten erscheinen als `PROJECT_NOT_RESTORED`; der Linter führt keinen automatischen Restore aus.
- Das vollständige Publish-Verzeichnis bereitstellen, einschließlich der MSBuild-Host-Verzeichnisse `BuildHost-netcore/` und `BuildHost-net472/` neben der EXE.
- Regeldatei versionieren. CLI: explizites `--config`. MCP: ausschließlich `ainetlinter-rules.json` direkt neben der adressierten Solution.

## Konfiguration und CI-Aufruf

Vorlage einmalig anlegen; die Umleitung überschreibt eine vorhandene Datei:

```sh
ainetlinter --docs ainetlinter-rules-json > ainetlinter-rules.json
ainetlinter --config ainetlinter-rules.json --path ./MyApp.slnx
```

In CI den Exit-Code und stdout/stderr sichern. Jeder Nichtnull-Code schlägt fehl; `1` kann Verstöße oder einen erwarteten Fehler anzeigen. Für ein vollständiges Gate den obigen Audit ohne Baseline verwenden. CLI-Laden kann die Konfiguration durch Schema-Synchronisation zurückschreiben; Änderungen am Regelbestand nach dem Lauf prüfen.

Ein Prozessaufruf aus xUnit/NUnit/MSTest ist eine Integration über die CLI-Grenze. Dabei absolute Pfade verwenden, stdout/stderr parallel lesen und den Exit-Code auswerten. Eine zusätzliche Test-Wrapper-Implementierung ist für den CI-Aufruf nicht erforderlich.

## Migration

Optionaler Baseline-Modus und Bulk-Suppression: [CLI-Workflows](cli.md). Die Baseline speichert Datei-Checksummen und aktualisiert sich auch bei verbleibenden Verstößen. Sie beweist daher weder Verstoßfreiheit des Altbestands noch eine Verbesserung gegenüber dem vorherigen Lauf.

`--fix`, `--add-disable-all` und `--remove-disable-all` verändern Quelldateien. Suppressions gelten dateiweit; genaue Syntax steht in der [Konfiguration](configuration.md#suppressions).

## Generierte Dateien

`cache/`, `measurements/` und Logs neben der Toolinstallation nicht versionieren. Regeldatei und eine bewusst eingesetzte Baseline gehören in die Versionskontrolle. Ausführliche Pfade und Schalter: [Cache und Profiling](configuration.md#cache-und-profiling).

## Implementierungsbelege

[AuditCommand](../../src/AiNetLinter/Commands/AuditCommand.cs), [ConfigLoader](../../src/AiNetLinter/Configuration/ConfigLoader.cs), [ConfigSyncer](../../src/AiNetLinter/Configuration/ConfigSyncer.cs), [ProjectRestoreState](../../src/AiNetLinter/Baseline/ProjectRestoreState.cs).
