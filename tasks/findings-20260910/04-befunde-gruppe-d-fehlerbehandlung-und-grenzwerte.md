# Gruppe D: Fehlerbehandlung & Grenzwerte — Befundbericht

## Geprüfte Szenarien
1. Fehlendes Pflichtfeld (`targetPath` weglassen)
2. Nicht-existenter `targetPath`
3. Falscher Argumenttyp (String statt Array bei `namePatterns`)
4. Ungültiger Enum-Wert (`kind="unknownKind"`)
5. Leerer String für Pflichtfeld (`targetPath=""`)
6. `targetPath` zeigt auf ein Verzeichnis statt auf eine Datei
7. Grenzwerte: `maxResults=0` oder negative Limits
8. Unbekannte/unerwartete Argumente (`foo="bar"`)
9. Nicht verwaltete Binärdatei (`FALSE-01`)

---

### [Positiv / Best Practice] Exzellente Vorab-Validierung
Die Validierung im AiNetLinter MCP-Server gehört zu den robustesten Implementierungen im MCP-Ökosystem:
1. **JSON-Feldpfade**: Alle Fehler nennen den genauen Feldpfad (`fieldPath: $.targetPath`, `fieldPath: $.namePatterns`, `fieldPath: $.maxResults`), was Agenten die automatische Korrektur enorm erleichtert.
2. **Keine Stacktraces**: Kein einziger Fehlerfall leakt interne Exceptions oder Stacktraces. Alle Fehler werden kontrolliert als `McpToolResults.InvalidArgument` oder domänenspezifische Fehlercodes zurückgegeben.
3. **Schutz vor Phantom-Parametern**: Übergebene Parameter, die nicht im Schema existieren, werden sofort mit `Unbekanntes Argument: <name>` und `fieldPath: $.<name>` abgewiesen ([TargetPathToolRegistrationOptions.cs:66](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/TargetPathToolRegistrationOptions.cs#L66)).
