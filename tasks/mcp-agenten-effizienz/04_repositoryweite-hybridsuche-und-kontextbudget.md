---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-21
open_questions: []
---

# Repositoryweite hybride Suche mit agententauglichem Kontext

## Ziel

Das bestehende `search_pattern` soll von einer reinen Textausgabe zu einer
strukturierten, kontextbudgetierten Suchfunktion erweitert werden. Die Suche bleibt
repositoryweit und dateitypunabhängig; für C# kann sie Treffer optional mit dem bereits
geladenen Roslyn-Symbolgraphen anreichern.

„Hybrid“ bedeutet hier deterministische Kombination aus Text-/Regex-Suche und optionaler
C#-Syntax-/Semantikanalyse. Es wird kein LLM-Ranking, kein RAG und kein Semantic Kernel
eingeführt.

Das Ergebnis soll einem Agenten mit möglichst wenigen Folgeaufrufen einen präziseren,
kleineren und maschinell auswertbaren Kontext geben. `rg` bleibt ausdrücklich erlaubt und
wird weder ersetzt noch vom Agenten-Workflow ausgeschlossen.

## Priorität und erwarteter Nutzen

Die Aufgabe ist P1, weil sie direkt auf die beiden wichtigsten Ziele der Initiative einzahlt:

1. weniger übertragener Kontext durch Treffer-, Datei- und Kontextlimits,
2. bessere Folgeentscheidungen durch Zeilen-/Spaltenpositionen, Trefferbereiche,
   Dateityp, Projektbezug und — soweit sicher auflösbar — Symbolidentität.

Die Wirkung ist als Hypothese zu messen, nicht zu behaupten. Reproduzierbare Proxies sind
UTF-8-Bytes/Zeichen der MCP-Antwort, Anzahl ausgegebener Treffer und Anzahl notwendiger
Folgeaufrufe in repräsentativen Agenten-Loops. Eine exakte Tokenersparnis wird nicht
behauptet, weil der Tokenizer host- und modellabhängig ist.

## Ausgangslage und belegte Lücke

`search_pattern` kann bereits:

- alle vom Scanner erreichten Dateitypen durchsuchen,
- case-insensitive Substrings und Regex verarbeiten,
- generierte Pfade auslassen,
- deterministisch sortierte Trefferzeilen mit `maxResults` liefern.

Die Implementierung scannt jedoch die von `WebFileCatalog` gelieferten Projektverzeichnisse
und ist damit nicht automatisch identisch mit einem vollständigen Git-Repository-Scan.
Die Antwort ist eine formatierte Textzeile statt eines strukturierten Trefferobjekts. Sie enthält weder
Match-Spans und Spalten noch optionalen Kontext, Scope-Metadaten oder einen getrennten
Vollständigkeitsstatus. C#-Semantik ist heute in `find_symbol`/`find_references` getrennt;
`search_pattern` bleibt der Textfallback für Nicht-C#.

Das Problem ist somit nicht, dass `rg` zu wenig Suchreichweite hätte. Die Lücke ist die
agententaugliche Verbindung von Reichweite, Kontextbudget und vorhandener C#-Semantik.

## Scope

### Must-have: strukturiertes lexikalisches Suchresultat

- Das bestehende Tool `search_pattern` additiv erweitern; kein neues Suchtool registrieren.
- `pattern`, `isRegex` und die bisherige Textantwort für abwärtskompatible Clients erhalten.
- Zusätzlich `structuredContent` mit deterministischen Trefferobjekten liefern.
- Ein Trefferobjekt enthält mindestens:
  - solution-/scope-relativen Forward-Slash-Pfad,
  - 1-basierte Zeile und Spalte,
  - Trefferlänge bzw. mehrere Trefferbereiche innerhalb einer Zeile,
  - unveränderten Zeilentext,
  - optional angeforderte Kontextzeilen,
  - Projektname, wenn der Pfad einem geladenen Projekt zugeordnet ist.
- Für Plain-Text- und Regex-Suche dieselbe Ergebnisform verwenden. Mehrere Matches in einer
  Zeile dürfen nicht stillschweigend zu einem unklaren Treffer zusammenfallen.

### Must-have: Kontextbudget und Vollständigkeit

- Bestehendes `maxResults` beibehalten und seine Semantik im strukturierten Vertrag klären.
- Zusätzliche, begrenzende Parameter prüfen: `maxFiles`, `contextLines` und ein begrenztes
  Antwortbudget in Zeichen oder UTF-8-Bytes.
- `completeness` ausgeben mit mindestens `scanCompleted`, Gesamt-/Sichtbarzahlen und
  getrennten Gründen wie `maxResults`, `maxFiles`, `maxResponseBytes` sowie übersprungenen
  Binär-/unlesbaren Dateien.
- Keine Cursor- oder Session-Pagination einführen. Bei Trunkierung sind Scope-Verfeinerung,
  niedrigere Kontextbreite oder ein höheres Limit der vorgesehene Folgeweg.
- Leere Ergebnisse, ungültige Regex und noch nicht geladene Solution an den bestehenden
  `isError`-/Recoverable-Vertrag anschließen.

### Must-have: kontrollierbarer Repository-Scope

- Standardmäßig den sicher ermittelten Solution-/Repository-Scope verwenden; niemals einen
  vom Agenten gelieferten Pfad ungeprüft außerhalb dieses Scopes lesen.
- Generische Include-/Exclude-Filter für Pfade oder Dateitypen vorsehen.
- Standardmäßig Build-, VCS-, temporäre, generierte, binäre und offensichtlich minifizierte
  Dateien auslassen; eine bewusste Erweiterung muss explizit und budgetiert erfolgen.
- Scope, Filter und Snapshot-/Ladezustand in der Antwort sichtbar machen.

### Must-have: optionale C#-Anreicherung ohne falsche Sicherheit

- Für Treffer in geladenen C#-Dokumenten optional Syntax-/SemanticModel-Informationen ergänzen:
  `declaration`, `symbol_reference`, `comment`, `string`, `code` oder `unknown`.
- Bei sicherer Auflösung `DocumentationCommentId` bzw. die bestehende stabile Symbol-ID und
  den Projektnamen ergänzen.
- Nicht auflösbare, mehrdeutige oder außerhalb des Roslyn-Snapshots liegende Treffer explizit
  als `not_applicable`, `ambiguous` oder `unavailable` markieren.
- Keine Behauptung einer semantischen Referenz allein aufgrund eines Texttreffers.

### Nice-to-have

- `outputMode`: nur Dateien, Trefferzeilen, Treffer mit Kontext oder gruppierte Blöcke.
- Mehrere Patterns in einem Aufruf mit expliziter OR-Semantik und getrennten Trefferbereichen.
- Git-Diff-Scope, insbesondere „nur hinzugefügte Zeilen“, sofern dies ohne neues Composite-Tool
  und ohne Remote-/Netzwerkzugriff deterministisch möglich ist.
- Ein diagnostischer Vergleich mit direkt aufgerufenem `rg` auf einer neutralen Fixture; dies
  ist kein Produktions-Dependency- oder Pflicht-Gate.

## Vorgeschlagener Antwortvertrag

Die konkrete Benennung darf den bestehenden MCP-Konventionen angepasst werden. Die Form muss
jedoch die Textantwort nicht ersetzen:

```json
{
  "matches": [
    {
      "filePath": "src/App/OrderService.cs",
      "line": 42,
      "matchRanges": [
        { "column": 18, "length": 10 }
      ],
      "lineText": "    return await PlaceAsync(order);",
      "contextBefore": [],
      "contextAfter": [],
      "projectName": "App",
      "semantic": {
        "kind": "symbol_reference",
        "resolution": "resolved",
        "symbolId": "M:App.OrderService.PlaceAsync"
      }
    }
  ],
  "completeness": {
    "scanCompleted": true,
    "matchedFileCount": 1,
    "totalMatchedLineCount": 1,
    "shownMatchedLineCount": 1,
    "truncated": false,
    "truncatedBy": [],
    "skippedBinaryFileCount": 0,
    "skippedUnreadableFileCount": 0
  }
}
```

Eine Zeile darf mehrere `matchRanges` enthalten. Damit bleibt die bestehende Zeilen-basierte
Textausgabe kompatibel, ohne im strukturierten Ergebnis einzelne Trefferbereiche zu verlieren.

## Technische Machbarkeit im bestehenden Stack

| Teil | Machbarkeit | Einschätzung |
|---|---|---|
| Strukturierte Treffer und Formatter | hoch | Bestehenden Scanner in Ergebnisobjekt und separaten Formatter teilen; analog zu Aufgabe 03. |
| Scope-/Exclude-Filter | hoch bis mittel | Mit vorhandenen Pfad-/Exclusion-Helfern realisierbar; Repository-Root und verlinkte Dateien müssen generisch bestimmt werden. |
| Kontext- und Antwortlimits | hoch | Deterministische Caps und UTF-8-Byte-Messung benötigen keinen neuen Stack. |
| C#-Anreicherung | mittel bis hoch | Roslyn `Document`, `SyntaxTree` und `SemanticModel` sind vorhanden; nur sichere Teilmengen dürfen als semantisch gelten. |
| Direkter `rg`-Backend-Aufruf | technisch möglich, aber nicht erforderlich | `ProcessStartInfo` wäre verfügbar, erzeugt aber Abhängigkeit, Prozess-/Encoding-/Cancellation- und Architekturtestaufwand. Kein harter `rg`-Zwang empfohlen. |
| Exakte Tokenoptimierung | nicht direkt messbar | Nur Zeichen, UTF-8-Bytes, Ergebnisgröße und Folgeaufrufe sind reproduzierbare Proxies. |
| RAG/Embeddings/LLM-Ranking | nicht erforderlich und ausgeschlossen | Der Nutzen entsteht aus Struktur, Scope und Roslyn-Auflösung, nicht aus probabilistischem Ranking. |

Empfohlener Start ist eine Erweiterung des bestehenden verwalteten Scanners mit Streaming-
Enumeration und strukturiertem Ergebnis. Ein optionaler `rg`-Backend-Vergleich kann später
zeigen, ob die Performance auf großen Repositories den zusätzlichen Prozessvertrag rechtfertigt.
Die Kernfunktion darf nicht von einer installierten `rg`-Binary abhängen.

## Risiken und Edge Cases

| Risiko/Edge Case | Auswirkung | Gegenmaßnahme |
|---|---|---|
| Große Repositories | Antwort- und Scanzeit explodieren | frühe Limits, Streaming, Excludes, deterministische Truncation und Messung; kein Komplettdump als Default |
| Mehrere Matches pro Zeile | Trefferzahl und Legacy-Text können auseinanderlaufen | Zeilenkompatibilität beibehalten, Bereiche separat modellieren, Zählbegriffe explizit benennen |
| Kommentare und Strings | False Positives bei vermeintlichen Symboltreffern | Syntaxkategorie ausweisen; semantische Auflösung nur bei sicherem Roslyn-Treffer |
| Regex mit teurem Backtracking | CPU-Hänger | Regex-Länge/Optionen begrenzen, Timeout verwenden, Timeout recoverable melden |
| Binärdateien, Encoding, BOM | unlesbare oder sinnlose Treffer | Encoding-Erkennung, Binärheuristik, Zähler für übersprungene Dateien |
| CRLF/LF und Unicode | falsche Spaltenpositionen | Zeilen 1-basiert und Spaltenkonvention dokumentieren; für C# an Roslyn-Positionen ausrichten |
| Linked Documents und doppelte Pfade | doppelte Treffer | physische Pfad-/Dokumentidentität deduplizieren und Herkunft transparent machen |
| `bin`, `obj`, `.git`, `temp`, generierte Dateien | Rauschen und unnötige Bytes | zentrale Exclusion-Policy wiederverwenden; Override nur explizit erlauben |
| Disk-Datei vs. geladener Roslyn-Snapshot | Text und Semantik können auseinanderfallen | Snapshot-/Load-Metadaten ausgeben; `unavailable` statt falscher Semantik |
| Pfadgrenzen und Reparse Points | unerlaubtes Lesen oder Endlosschleifen | normalisierte Root-Prüfung und sichere Enumeration; keine Shell-Stringverkettung |
| Abbruch während paralleler Suche | unvollständige, aber wie vollständig wirkende Antwort | Cancellation als eigener Completeness-/Recoverable-Zustand ausweisen |
| Tool- und Output-Schema-Wachstum | größerer Discovery-/Host-Kontext | additive, schlanke Struktur; kein pauschaler Output-Schema-Rollout ohne Messung |

## Bewusste Abgrenzung zu `rg`/`grep`

Der Agent darf für schnelle, ad-hoc Textsuche weiterhin direkt `rg` verwenden. Dieses Feature
hat einen anderen Zweck:

- `rg` liefert maximale rohe Suchreichweite und Geschwindigkeit.
- `search_pattern` liefert eine MCP-kompatible, begrenzte und strukturierte Antwort.
- Roslyn kann C#-Treffer optional semantisch einordnen.
- Der MCP kann Ergebnisvollständigkeit, Snapshot, Scope und nächste sinnvolle Analyseebene
  maschinenlesbar angeben.

Nicht Teil der Aufgabe sind vollständige `rg`-Kompatibilität, PCRE2-Parität, Shell-Optionen,
eine eigene grep-Syntax oder ein Verbot des direkten Agentenaufrufs von `rg`.

## Umsetzungsschritte

1. Aktuellen `SearchPatternScanner`-Output messen und ein neutrales mehrsprachiges Fixture mit
   C#, JSON, Markdown und mindestens einem generierten/ausgeschlossenen Pfad anlegen.
2. Scanmodell und Formatter trennen; bestehende Textausgabe als Kompatibilitätspfad erhalten.
3. Matchbereiche, Scope-/Dateifilter, Kontextlimits und `completeness` implementieren.
4. StructuredContent per Raw-Wire- und direktem Tooltest absichern; keine Cursor- oder Session-
   Zustände einführen.
5. C#-Semantic-Enrichment separat und opt-in implementieren, mit expliziten unresolved-/ambiguous-
   Fällen und neutralen Multi-Project-Fixtures.
6. UTF-8-Bytes, Treffer-/Dateizahlen, Scanzeit und erforderliche Folgeaufrufe vor/nachher messen.
7. Erst nach dem Messpunkt entscheiden, ob ein optionaler `rg`-Backend-Prototyp sinnvoll ist.

## Tests und Messungen

- Plain-Text- und Regex-Suche über C#, JSON, Markdown und weitere nicht-C#-Dateien.
- Mehrere Trefferbereiche in einer Zeile, 1-basierte Positionen und stabile Sortierung.
- `maxResults`, `maxFiles`, `contextLines` und Antwortbudget mit getrennten Truncation-Gründen.
- Leere Treffer, ungültige Regex, Regex-Timeout, unlesbare Datei und Cancellation.
- Ausschluss von Build-, VCS-, temporären, generierten und binären Dateien.
- Pfad-Scope, Include-/Exclude-Filter, verlinkte Dateien und Forward-Slash-Ausgabe.
- Wiederholte Aufrufe liefern byte-identische strukturierte Reihenfolge.
- C#-Treffer in Deklaration, Code, Kommentar und String; auflösbare und mehrdeutige Symbole.
- Raw-Wire-Test bestätigt, dass `structuredContent` ein JSON-Objekt bleibt und Legacy-Text weiter
  vorhanden ist.
- Benchmark vergleicht UTF-8-Bytes und Folgeaufrufe auf einem neutralen Fixture; keine
  modellabhängige Tokenzahl und kein verpflichtender Test gegen eine externe `rg`-Installation.

## Definition of Done

- `search_pattern` durchsucht den vereinbarten generischen Scope über alle unterstützten
  Dateitypen und bleibt für den Agenten optional neben direktem `rg` nutzbar.
- Strukturierte Treffer, Matchbereiche, optionale Kontextzeilen und Vollständigkeitsstatus sind
  deterministisch und textkompatibel ausgegeben.
- C#-Semantic-Enrichment behauptet keine Auflösung, wenn Roslyn sie nicht sicher liefern kann.
- Antwortgröße und Folgeaufrufe sind mit reproduzierbaren Proxies evaluiert; keine unbelegte
  Tokenersparnis wird dokumentiert.
- Kein RAG, keine Embeddings, kein Semantic Kernel, kein LLM-Ranking, kein Cursor-/Session-State
  und kein mutierendes Refactoring-Feature wurde eingeführt.
- `README.md`, `Docs/agent-api.md`, `Docs/integration.md`, Overview-Resource und Toolbeschreibung
  sind widerspruchsfrei aktualisiert.
- `dotnet build` sowie beide Nicht-Stress-Testprojekte sind grün.
