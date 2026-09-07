## Primäre Einstiegspunkte

`Konzept.md` und die Markdown-Findings unter `findings/` dokumentieren den
MCP-Nutzungs-Audit. Diese Aufgabe betrifft ausschließlich die redaktionelle
Neutralisierung der darin verwendeten Probe-Targets und Beispiel-Symbole.

## Betroffene Dateien und Symbole

Alle Markdown-Dateien unter diesem Task-Verzeichnis werden auf nicht-neutrale
Produkt-, Hersteller-, Namespace-, Pfad-, Typ- und Memberbezeichnungen geprüft.
Konkrete Probe-Targets und proprietär wirkende Bezeichner werden durch
generische Platzhalter ersetzt; Aussagen, Messwerte und Befunde bleiben
inhaltlich erhalten.

## Aufrufer und Abhängigkeiten

Keine Laufzeit- oder Quellcodeabhängigkeiten. Die einzige Querverbindung sind
relative Verweise zwischen `Konzept.md`, Finding-Dateien und deren Dateinamen.

## Relevante Tests, Konfiguration und Dokumentation

Keine C#- oder Konfigurationsänderung. Verifikation erfolgt über rekursive
`rg`-Suchen, Dateinamensprüfung und Diff-Inspektion. Build- und Test-Gates sind
für diese reine Markdown-Neutralisierung nicht fachlich betroffen.

## Invarianten, Risiken und Unsicherheiten

- Keine produkt- oder herstellerspezifischen Begriffe dürfen im Task-Text oder
  in Finding-Dateinamen verbleiben.
- Keine konkreten dekompilierten Typen, Member, Namespaces oder Pfade des
  untersuchten Fremdtargets dürfen verbleiben.
- MCP-Toolnamen, AiNetLinter-Pfade und die technische Audit-Aussage bleiben
  unverändert, sofern sie keine Target-Identität preisgeben.

## Verifikation

Nach der Ersetzung erneut mit case-insensitiven Wort-/Namespace-Mustern nach
den gesperrten Hersteller- und Produktbezeichnungen sowie nach bekannten
Target-Symbolen suchen. Danach nur neutrale Treffer und die erwarteten
AiNetLinter-Eigenbezüge im Diff zulassen.
