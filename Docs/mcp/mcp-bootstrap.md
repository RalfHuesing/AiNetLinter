# AiNetLinter MCP-Bootstrap

→ [MCP-Tools & Verträge](tools.md) | [MCP-Server & Daemon](server.md) | [MCP-Host-Integration](integration.md) | [README](../../README.md)

Diese Anleitung wird einmalig pro Integration gelesen. Sie beschreibt den
aktiven, dateibasierten MCP-Vertrag; sie benötigt keine Projektdefinitionsdatei
und keine Suche im Arbeitsverzeichnis.

## Ablauf

1. Der Agent MUSS zuerst das passende Target aus der für den Host sichtbaren
   Dateiliste wählen: den konkreten absoluten
   Pfad zu einer vorhandenen `.sln`- oder `.slnx`-Datei für Source-Analyse
   beziehungsweise zu einer vorhandenen `.dll`- oder `.exe`-Datei für
   Decompiled-Analyse. Bei mehreren möglichen Solutions nicht raten, sondern
   die Auswahl klären.
2. Für Source-Analyse ist ausschließlich die optionale Datei
   `ainetlinter-rules.json` direkt neben der gewählten Solution relevant. Sie
   darf bei Bedarf dort bereitgestellt werden; fehlt sie, bleibt Navigation
   möglich und die Lint-Capability ist `not_configured`. Es gibt keine Suche
   in Arbeitsverzeichnis, Elternverzeichnissen oder Repository-Defaults.
3. Lege die dauerhafte Regeldatei `AiNetLinter-McpWorkflow.mdc` im
   Regelverzeichnis des verwendeten Hosts ab: `.agents/rules` oder
   `.cursor/rules`. Eine vorhandene gleichnamige AiNetLinter-Datei gezielt
   aktualisieren, andere MCP-Regeln nicht überschreiben. Der aktuelle Inhalt
   ist über `ainetlinter --docs mcp-rule` verfügbar.
4. Falls der MCP-Server noch nicht registriert ist, verwende die
   Host-Konfiguration mit `ainetlinter` und `args: ["--mcp-server"]`.
   Für eine getrennte lokale Daemon-Instanz ergänzt du beispielsweise
   `--daemon-instance`, `beta`:

   ```json
   { "command": "ainetlinter", "args": ["--mcp-server", "--daemon-instance", "beta"] }
   ```

   Wenn unten ein dynamischer Laufzeitblock ausgegeben wird, verwende dessen
   `command` und `args`; `--path` und `--config` gehören nicht in diese
   Registrierung.

5. Verwende beim ersten zielgebundenen Tool-Aufruf ausschließlich den
   absoluten, vorhandenen Dateipfad als `targetPath`:

   ```json
   { "targetPath": "C:\\repos\\MeinProjekt\\src\\MeinProjekt.slnx" }
   ```

   Die Herkunft wird ausschließlich aus der Endung bestimmt. Ein relativer,
   fehlender, nicht unterstützter oder auf ein Verzeichnis zeigender Pfad ist
   `invalid_argument`. Zusätzliche Properties außerhalb des aktuellen
   `tools/list`-Schemas gehören nicht zum aktiven Vertrag.
6. Prüfe die Einrichtung mit `get_server_health` ohne Target (global) oder mit
   einem optionalen `targetPath`; ein kleiner zielgebundener Tool-Aufruf ist
   ebenfalls möglich.

Nach erfolgreicher Einrichtung ist dieser Bootstrap abgeschlossen; im normalen
Arbeitskontext reicht die dauerhafte `AiNetLinter-McpWorkflow.mdc`.

Die von einer laufenden AiNetLinter-Instanz gelieferte Fassung dieses
Bootstrap-Leitfadens ergänzt am Ende der Ausgabe einen dynamischen
Registrierungsblock. Er enthält den tatsächlichen Startpfad des aktuellen
Prozesses und ist für MCP-Hosts zu verwenden, die `ainetlinter` nicht über
`PATH` auflösen können. Die statische Vorlage bleibt mit dem PATH-basierten
Beispiel oben portabel.

## MCP-Zielvertrag

Jeder zielgebundene Tool-Aufruf erhält genau `targetPath`: einen absoluten,
existierenden Pfad zu einer konkreten `.sln`/`.slnx`-Datei oder `.dll`/`.exe`-
Datei. Die Dateiendung bestimmt deterministisch Source (`.sln`, `.slnx`) oder
Decompiled-Assembly (`.dll`, `.exe`). Der Server sucht keine andere Solution
und steigt nicht in ein übergeordnetes Repository auf; bei Source ist der
Analyse-Root das normalisierte Elternverzeichnis der übergebenen Solution.

Source liest ausschließlich die optionale benachbarte
`ainetlinter-rules.json`. Fehlt sie, lautet der Regelstatus `not_configured`:
Navigation bleibt `ok`, Lint wird nicht als scheinbar leerer oder sauberer Lauf
dargestellt. Eine ungültige oder nicht lesbare Datei ist ein
Konfigurationsfehler; es gibt keinen Default- oder Elternpfad-Fallback.
Assembly-Ziele haben `origin=decompiled` und Lint ist `unsupported`.
Details zum Zielvertrag siehe [MCP-Server & Daemon](server.md#6-mcp-zielvertrag-targetpath).

Die Resource-Templates verwenden den URL-kodierten Query-Parameter
`targetPath`:

```text
ainetlinter://overview{?targetPath}
ainetlinter://rules{?targetPath}
```

`get_server_health` akzeptiert global keinen Target-Block oder optional einen
`targetPath`. Unbekannte Properties werden nicht still ignoriert, sondern gegen
das aktuelle `tools/list`-Schema geprüft und als `invalid_argument` mit
Feldnamen abgelehnt.

## Antwort- und Folgeaufrufvertrag

Zielgebundene Antworten verwenden Contract v2. `navigation.status.operation`
beschreibt die Ausführung, `navigation.status.completeness` die Vollständigkeit
der Antwort; fachliche Analysequalität (etwa partielle Assembly-Diagnostics)
bleibt davon getrennt. Bei `RESPONSE_BUDGET_TOO_SMALL` enthält der Fehler
`fieldPath=$.maxResponseBytes`, `requestedBytes` und `minimumResponseBytes`.
Mit demselben Request und Snapshot ist dieser Mindestwert unmittelbar als
`maxResponseBytes` wiederholbar; ausschließlich der sichtbare Content wird in
UTF-8 gemessen.

Stabile Handoff-IDs werden aus der im Content explizit als Handoff-ID
ausgewiesenen Zeile übernommen, nie aus einem Anzeigenamen. Bei Assembly-Tools trennt
`includeReferences` die Suchbreite: `false` bleibt für eine Root-ID root-only
und öffnet für eine verifizierte Referenz-ID ausschließlich deren Owner;
`true` erlaubt die begrenzte Referenz-Closure. Die Antwort weist angeforderten
und effektiven Suchmodus aus. Ein zielgebundener Health-Call zeigt nur das
adressierte Target; Daemon- und Prozessaggregate stehen ausschließlich im
globalen Health-Call.

`get_file_tree` zählt physische Dateien, während `get_index_scope` die
Roslyn-Dokumentpopulation beschreibt. Ausschlüsse bleiben nach Ursache und
Einheit getrennt (`excludedPhysicalFileCount`, übersprungene Ausschluss-,
Reparse-Point- und unlesbare Verzeichnisse); diese Counts nicht gegenseitig
umdeuten.

---

> [AiNetLinter](https://github.com/RalfHuesing/AiNetLinter) — Quellcode, Changelog und Issues auf GitHub.
