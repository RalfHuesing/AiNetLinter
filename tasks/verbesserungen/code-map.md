## Primäre Einstiegspunkte

Assembly-MCP-Verträge und die vorhandenen Analyse-/Sessionmodelle.

## Betroffene Dateien und Symbole

Keine Produktionsdatei geändert. Das Verbesserungsdokument betrifft
`inspect_assembly`, Symbolnavigation, Referenzsessions, Provenienz und
zukünftige Persistenzanalyse.

## Aufrufer und Abhängigkeiten

Die Vorschläge beziehen sich auf die bestehende Assembly-Dispatcher-,
Session-, Decompilation- und Navigation-Schicht sowie deren MCP-Verträge.

## Relevante Tests, Konfiguration und Dokumentation

Relevante Dokumentation: `Docs/agent-api.md`, `Docs/integration.md` und
`Docs/ROADMAP.md`. Vorhandene Assembly-Contract- und Navigationstests sind
bei einer späteren Umsetzung zu erweitern.

## Invarianten, Risiken und Unsicherheiten

- Ziel-Assemblies bleiben read-only und werden nicht ausgeführt.
- Root-Scope und Referenzexpansion müssen getrennt bleiben.
- Dekompilierte Inhalte dürfen nicht als Originalquelle dargestellt werden.
- Fehlende Referenzen können Call Trees und Persistenzbefunde begrenzen.
- Dieses Dokument enthält keine untersuchungsspezifischen Namen, Pfade oder
  Quelltextausschnitte.

## Verifikation

Keine Builds oder Tests ausgeführt, entsprechend der Nutzeranweisung.
Dokument und Commit werden vor dem Commit auf die verlangte Neutralität und
den tatsächlichen Diff geprüft.
