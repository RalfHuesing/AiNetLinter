---
status: vorschlag / entscheidung noetig
type: konsistenz-agenten-ergonomie
priority: P2
last_updated: 2026-08-21
verified_against: src/AiNetLinter/Mcp/ServerInstructions.cs, SymbolGraphToolRegistrations.cs
---

# 03 — Wire-Texte: ASCII-transliteriertes Deutsch ist worst-of-both

## Befund

Alle Texte, die den Server verlassen (Wire-Ebene), sind Deutsch, aber ohne Umlaute:
"geaendert", "Loesung", "fuer", "ueber". Beispiele: `ServerInstructions.Text`,
alle Tool-Descriptions in `*ToolRegistrations.cs`, Antworttexte der Scanner
(`"[INFO]: Server laedt die Solution noch."`). Dagegen verwenden Docs, Task-Dateien und
teils sogar Code-Kommentare korrekte Umlaute ("gelöschte Dateien raus" steht wörtlich so
im XML-Doc von `McpCodeGraphServerRefresh.cs`).

Es gibt damit drei Ebenen mit drei unterschiedlichen Sprachformen:

| Ebene | Form |
|---|---|
| Docs / tasks | Deutsch mit Umlauten |
| Code-Kommentare | gemischt (teils Umlaute, teils Transliteration) |
| Wire-Texte (Instructions, Descriptions, Antworten) | konsequent ASCII-transliteriert |

## Analyse

Die Transliteration ist die schlechteste der drei möglichen Welten:

1. **Kein messbarer Byte-Vorteil gegenüber echtem Deutsch:** Nur Umlaut-Wörter wären
   betroffen (ä = 2 UTF-8-Bytes); der Gewinn ist marginal, während "oe/ue/ae"-Schreibungen
   ebenfalls 2 Bytes belegen.
2. **Nicht-Wörter:** "geaendert", "Loesung", "aufgeloest" sind keine gültigen deutschen
   Tokens. Sprachmodelle haben solche Formen selten im Training; Tokenizer können sie
   ungünstiger zerlegen als sowohl echtes Deutsch als auch Englisch. (Beweis schuldig
   bleibt wie in eurer Methodik gefordert — aber die *Richtung* des Zweifels geht gegen
   die Transliteration.)
3. **Ökosystem-Norm Englisch:** MCP-Server im Wildern schreiben Descriptions und
   Instructions fast ausnahmslos auf Englisch; Host-Modelle sind darauf am stärksten
   kalibriert. Ein international nutzbares Analysewerkzeug mit deutschsprachigen
   Tool-Descriptions ist eine unnötige Zugangs-Hürde.
4. **Inkonsistenz kostet Aufmerksamkeit:** Ein Agent, der Docs (Umlaut-Deutsch) und
   Wire-Texte (Transliteration) liest, sieht dieselben Begriffe in zwei Formen
   ("gelöscht" vs. "geloescht") — suboptimal für exaktes Zitieren und Suchen.

## Entscheidungsvorschlag (drei Optionen)

| Option | Beschreibung | Bewertung |
|---|---|---|
| A Status quo | Transliteration beibehalten | Konsistent auf Wire-Ebene, aber Option 1+2 bleiben ungenutzt |
| B Echtes Deutsch | Umlaute auf Wire-Ebene zulassen | Konsistenz mit Docs; löst aber Punkt 3 nicht |
| C Englisch für Wire-Texte | Instructions, Descriptions, Antworttexte auf Englisch; Docs bleiben Deutsch | Ökosystem-Norm; größter Diff, aber rein textuell, keine Vertragsänderung |

Empfehlung: **C**, phasenweise —

1. Phase 1: `ServerInstructions.Text` + Tool-Descriptions (kleiner, geschlossener Surface,
   Raw-Wire-Probes existieren bereits für Byte-Messung vorher/nachher).
2. Phase 2: Antworttexte der Scanner (`[INFO]/[ERROR]`-Texte). Fehler-*Codes*
   (SYMBOL_NOT_FOUND etc.) sind bereits Englisch — Phase 2 macht die Antwort konsistent.
3. Messung gemäß Projektmethodik: UTF-8-Bytes von `tools/list` und `server/discover`
   vor/nachher (keine Token-Schätzungen).

## Abhängigkeiten / Gegenchecks

- Tests, die deutsche Wire-Texte asserten (z. B. Loading-Text, Overview-Tests), müssen
  mitziehen — reiner Textumbau, kein Vertragsumbau.
- `.agents/rules/AiNetLinter.mdc` (generiert) enthält ebenfalls deutsche Beschreibungen;
  Sync-Lauf danach nicht vergessen.
