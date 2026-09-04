# AiNetLinter MCP-Bootstrap

Diese Anleitung wird einmalig pro Projekt benötigt. Sie wird nur bei einem
ausdrücklichen Auftrag zur AiNetLinter-Integration oder zur gezielten
Wiederherstellung einer fehlenden Projektinitialisierung gelesen.

## Ablauf

1. Ermittle den Projektroot und die zugehörige `.sln`- oder `.slnx`-Datei. Bei
   mehreren möglichen Solutions nicht raten, sondern die Auswahl klären.
2. Suche nach einer bestehenden Regeldatei. Eine vorhandene `rules.json` oder
   projektspezifische Regeldatei nicht überschreiben. Gibt es keine, erzeuge
   eine Ausgangsdatei mit `ainetlinter --docs rules-json`.
3. Lege im Projektroot `ainetlinter.project.json` an oder prüfe die vorhandene
   Datei. Sie muss auf genau diese Solution und Regeldatei zeigen:

   ```json
   {
     "solution": "src/MeinProjekt.slnx",
     "rules": "rules.json"
   }
   ```

   Beide Felder sind Pflicht; relative Pfade gelten ab dieser
   Definitionsdatei.
4. Lege die dauerhafte Regeldatei
   `AiNetLinter-McpWorkflow.mdc` im Regelverzeichnis des verwendeten Hosts ab:
   `.agents/rules` oder `.cursor/rules`. Eine vorhandene gleichnamige
   AiNetLinter-Datei gezielt aktualisieren, andere MCP-Regeln nicht
   überschreiben. Der aktuelle Inhalt ist über `ainetlinter --docs mcp-rule`
   verfügbar.
5. Falls der MCP-Server noch nicht registriert ist, verwende die
   Host-Konfiguration mit `ainetlinter` und `args: ["--mcp-server"]`.
   Für eine getrennte lokale Daemon-Instanz ergänzt du beispielsweise
   `--daemon-instance`, `beta`:

   ```json
   { "command": "ainetlinter", "args": ["--mcp-server", "--daemon-instance", "beta"] }
   ```
   Wenn unten ein dynamischer Laufzeitblock ausgegeben wird, verwende dessen
   `command` und `args`; `--path` und `--config` gehören nicht in diese
   Registrierung.

   Die Instanz-ID wird invariant in Kleinbuchstaben normalisiert; `BETA` und
   `beta` adressieren daher dieselbe Windows-Instanz.
6. Prüfe die Einrichtung mit `get_server_health` oder einem kleinen
   semantischen Tool-Aufruf gegen einen vollständigen Projekt-Target-Block.

Bei mehreren Solutions oder mehreren nicht eindeutig zuordenbaren
Regeldateien die Auswahl vor dem Schreiben klären. Nach erfolgreicher
Einrichtung ist dieser Bootstrap abgeschlossen; im normalen Arbeitskontext
reicht die dauerhafte `AiNetLinter-McpWorkflow.mdc`.

Die von einer laufenden AiNetLinter-Instanz gelieferte Fassung dieses
Bootstrap-Leitfadens ergänzt am Ende der Ausgabe einen
dynamischen Registrierungsblock. Er enthält den tatsächlichen Startpfad des
aktuellen Prozesses und ist für MCP-Hosts zu verwenden, die `ainetlinter` nicht
über `PATH` auflösen können. Die statische Vorlage bleibt mit dem
PATH-basierten Beispiel oben portabel.

## MCP-Projektvertrag

Jeder zielgebundene Tool-Aufruf erhält `targetType` (`project` oder `assembly`)
und den absoluten `targetPath`. Bei `targetType=project` muss im adressierten
Projektroot `ainetlinter.project.json` liegen. Fehlt die Datei oder ist sie
ungültig, liefert der Server `PROJECT_NOT_INITIALIZED` bzw. `RULES_INVALID` mit
einem konkreten Wiederherstellungshinweis. `tools/list` weist je Tool aus, ob
der gemeinsame read-only Projekt-/Assembly-Sessionpfad, nur der Projektpfad
oder nur der Assemblypfad unterstützt wird. Symbolgraph-, Struktur- und
Metrikabfragen verwenden für Assemblys eine dekompilierte
read-only Session; Regeln, Audits, Dateisuche, Tests und Git-Impact bleiben
projektgebunden. `get_server_health` kann ohne Target aggregieren oder gezielt
eine Projekt- bzw. Assembly-Session anzeigen. Target-Aufrufe verwenden keine
Legacy-Parameter wie `projectRoot` oder `assemblyPath`.
