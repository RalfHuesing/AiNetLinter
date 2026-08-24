---
status: offen
type: konzept
project_kind: brownfield
estimated_scope: medium
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-24
open_questions: []
herkunft: Beobachtung 2026-08-23 (Ralf) — Agenten rufen find_symbol mehrfach direkt hintereinander auf. Ursprünglich Entscheidung A1 (additiv, namePattern + namePatterns). Überarbeitung 2026-08-24 (Ralf): Verwerfung von A1 zugunsten eines harten Cuts auf reine Array-Parameter über die gesamte Batch-Toolfamilie (4 Tools); Nutzer-Entscheidung „alle 4 Tools jetzt".
---

# Batch-Toolfamilie: reine Array-Parameter statt Singular/Array-Dualismus

## Ziel

Die vier Batch-Tools des MCP-Servers — `find_symbol`, `get_file_skeleton`,
`get_symbol_body`, `metrics_lookup` — bekommen ausschließlich Array-Parameter.
Die Singular-Parameter (`namePattern`, `filePath`, `symbolIdentifier`) werden
**entfernt**, nicht parallel weitergeführt. Wer genau ein Symbol/meine Datei
sucht, übergibt ein Array mit genau einem Eintrag. Ein fehlender/leerer
Array-Parameter ist ein deterministischer `INVALID_ARGUMENT`, der den
korrekten Parameternamen nennt.

Damit einher geht die Beseitigung des Antwort-Dualismus: Jedes dieser Tools
antwortet mit **exakt einem** StructuredContent-Schema, unabhängig von der
Array-Länge. Insbesondere entfällt der heute in `metrics_lookup`
dokumentierte Split (nacktes `MetricsLookupResultDto` bei Einzelsymbol vs.
`MetricsLookupBatchDto` bei Batch — Kommentar in `MetricsLookupTool.cs:89–92`)
und der für `find_symbol` geplante Doppel-Shape (`{Matches}` vs. `{Results}`).

Nicht Teil dieses Tasks: die Compile-Diagnostics-Cache-Frage (siehe „Blick
über den Tellerrand") und die Umstellung einzelner, bewusst
nicht-batch-fähiger Tools (siehe Non-Goals).

## Intention und Fehlverhalten heute

### Beobachtung

Agenten suchen typischerweise nicht nach genau einem Namen. Realer Loop im
eigenen Dogfooding: 3–6 `find_symbol`-Aufrufe direkt hintereinander („finde
Klasse X, Y, Z, dann Methode A"). Der MCP-Server ist dafür ausgelegt —
residente Solution, Roslyn-Symbolgraph — aber der Vertrag von `find_symbol`
erzwingt einen Call pro Name.

### Warum jeder dieser Calls teuer ist

Ein einzelner `find_symbol`-Call durchläuft zwei vollständige Solution-Pässe,
**unabhängig davon, wie eng das Pattern ist**:

1. **SymbolFinder-Sweep:** `FindSymbolScanner.FindMatchesWithEntriesAsync`
   iteriert via `SymbolFinder.FindSourceDeclarationsAsync` über alle
   Quell-Deklarationen der Solution und matcht per Substring.
2. **Compile-Diagnostics-Pass (dominanter Anteil):**
   `FindSymbolTool.BuildAggregateWarningAsync` ruft
   `McpCompileDiagnostics.GetErrorsByFileAsync`; dieser Helper iteriert jedes
   Projekt und ruft pro Projekt `compilation.GetDiagnostics()` — den
   vollständigen Compile-Diagnose-Lauf. Kein Cache; derselbe Pass läuft auch
   in `get_symbol_body` und `metrics_lookup` (`Render*Async`-Methoden rufen
   `BuildAggregateWarningAsync` jeweils selbst).

Die Folge: N sequenzielle Calls = N komplette Diagnostics-Pässe über die
gesamte Solution, nur um Warnhinweise zu bauen, die im Regelfall leer sind.

### Warum nicht `search_pattern`?

`search_pattern` ist die bewusste Fallback-Stufe für Text außerhalb des
C#-Symbolgraphs (`Docs/integration.md` „Tool-vs-`rg`-Empfehlung";
`ServerInstructions.cs`). Es nimmt nur ein `pattern` und liefert keine
Symbolsemantik (Kind, Signatur, Datei:Zeile der Deklaration).

### Kein Duplikat im Bestand (Audit)

Geprüft gegen die registrierten Tools: `find_symbol` ist der einzige
deterministische Roslyn-Namens-Locator. `get_symbol_body`/`find_references`/
`get_class_structure`/`metrics_lookup` benötigen bereits eine stabile
Symbol-ID (können also nicht lokalisieren), `get_namespace_tree` liefert
hierarchische Orientierung statt Namenssuche, `search_pattern` ist Textsuche
ohne Semantik. Erweiterung statt Ersatz — deckungsgleich mit
`90_bewusst-nicht-umsetzen/Konzept.md` (A.2: „bestehendes Tool erweitern,
keinen Alias und kein zweites Schema").

## Warum harter Cut statt additive Variante (A1 verworfen)

Der ursprüngliche Entwurf (A1) plante `namePattern` **und** `namePatterns`
mit Merge-Semantik (`McpBatchArguments.Collect`) und zwei verschiedenen
StructuredContent-Formen je nach Parameterwahl. Diese Variante wurde am
2026-08-24 verworfen. Gründe:

1. **Dualer Response-Shape ist der eigentliche Schaden.** Unter A1 hätte
   derselbe Tool-Call je nach Parameterform zwei Schemata geliefert
   (`{Matches}` einzeln, `{Results:[...]}` im Batch). Ein Aufrufer müsste die
   Form aus seiner eigenen Anfrage ableiten — fehleranfällig und gegen das
   Determinismus-Prinzip. Array-only erzwingt eine Form.
2. **Gesamtaufwand sinkt.** A1-erst, Familie-später hieße: dieselben Dateien,
   Descriptions, Doku-Abschnitte und Tests zweimal anfassen. Der Cut jetzt
   berührt jeden Ort genau einmal.
3. **Der Helper vereinfacht sich.** `Collect(single, multiple)` degeneriert
   ohne Singular-Parameter zu einem Normalize-Schritt (Trimmen, Leeres
   entfernen, Deduplizieren) — ein Helper für alle 4 Tools ohne
   Merge-Logik.
4. **Kein Funktionsverlust.** Das Array subsumiert die Einzelform vollständig;
   `["Greeter"]` kostet gegenüber `namePattern="Greeter"` nichts Substantielles.
   Die alte Evidenz-Frage („erst Nutzungsdaten aus Aufgabe 01 abwarten") ist
   damit moot.

Bekannter Preis (akzeptiert, dokumentiert unter Edge-Cases): Modelle senden
aus Mustererkennung gelegentlich den alten Singular-Parameternamen → Binding-
fehler bzw. `INVALID_ARGUMENT` → ein fehlgeschlagener Call plus Retry.
Gegenmaßnahme: Description formuliert den Array-Parameter als Standardweg
„auch für genau einen Namen"; Fehlermeldungen nennen den korrekten Namen.
Externe Konsumenten mit alten Prompts heilen durch Discovery (tools/list)
selbst — die Tool-Oberfläche wird pro Session dynamisch bezogen, nicht
kompiliert konsumiert.

## Vertrag (Zielzustand)

### Gemeinsamer Parameter-Kern (Helper)

`McpBatchArguments.Collect(string? single, string[]? multiple, …)` wird durch

```csharp
internal static List<string> Normalize(string[]? values, StringComparer? comparer = null)
```

ersetzt (gleiche Datei `McpBatchArguments.cs`): Einträge trimmen,
Null/Whitespace-Einträge verwerfen, Deduplizierung (Default
`StringComparer.Ordinal`). Alter Helper inklusive Singular-Parameter wird
komplett gelöscht — kein Shim, keine Überladung mit zwei Parametern.

Vergleicherverteilung bleibt wie heute: `get_file_skeleton` normalisiert
Pfadangaben mit `OrdinalIgnoreCase` (Windows-Pfade), die drei anderen Tools
mit `Ordinal`.

Für alle vier Tools gilt dieselbe Argument-Prüfung in dieser Reihenfolge
(jeweils innerhalb der bestehenden Tool-eigenen Reihenfolge zu Load-State,
siehe Edge-Cases):

- `Normalize(...)` liefert 0 Einträge (fehlend, `[]`, nur Whitespace) →
  `INVALID_ARGUMENT` (recoverable), Meldung
  „Pflichtparameter '<arrayParam>' fehlt oder ist leer." mit Hint
  `<arrayParam>: ["<BeispielA>"] oder <arrayParam>: ["<BeispielA>", "<BeispielB>"]`.
- `find_symbol` zusätzlich: mehr als `MaxPatternsPerCall = 10` Einträge →
  `INVALID_ARGUMENT` mit Cap-Hinweis.

### `find_symbol`

```csharp
// SymbolGraphToolRegistrations.cs — AddFindSymbol
async (string projectRoot,
       string[]? namePatterns,
       string? kind = null,
       int maxResults = 50,
       CancellationToken ct = default) => ...
```

- Parameter ist bewusst nullable mit eigener Validierung (statt
  Schema-Pflichtfeld): einheitlicher Fehlerpfad für fehlend/null/leer/
  whitespace-only, gleiche Meldung wie die Geschwister.
- **Antwortform immer batchförmig**, auch bei genau einem Pattern:

```json
{
  "Results": [
    { "NamePattern": "Greeter", "Matches": [ /* SymbolLocationEntry[] */ ] },
    { "NamePattern": "GreetingService", "Matches": [] }
  ]
}
```

  Schlüssel PascalCase konsistent zum Bestand (`Matches`-Payload /
  `SymbolLocationEntry` serialisieren ebenfalls PascalCase). Kein Top-Level-
  Array (Regression `McpToolResultsTests.Text_WithListPayload_StructuredContentIsJsonObjectNotArray`).
- Text-Antwort: Abschnitt pro Pattern mit Kopfzeile (dem angefragten
  Pattern), Trennmuster wie `MarkdownBuilder.Divider()` in
  `RenderSymbolBodiesAsync`; darunter das gewohnte
  `Datei:Zeile - Kind: Signatur`-Listing inkl. Meta-Zeilen. Auch bei Länge 1
  läuft derselbe Rendering-Pfad — kein Einzel-Branch.
- **Semantik je Pattern unverändert zur heutigen Einzelform:** Substring-Match
  case-insensitive auf Deklarationsnamen (`SymbolFilter.TypeAndMember`),
  Kind-Filter `kind` gemeinsam für alle Patterns, einmalige Vorab-Validierung
  gegen `ValidKinds`.
- **`maxResults` wirkt pro Pattern:** jedes Pattern bekommt seine eigene
  Trefferliste inklusive eigener Trunkierungs-Meta-Zeile
  (`McpTruncation.TruncateLines`). Deterministisch; ein globales Limit wäre in
  Batch-Antworten nicht interpretierbar.
- **Miss-Hint je Pattern:** 0 C#-Treffer eines Patterns → individueller
  Miss-Hint (Legacy-Textsuche via `SearchPatternLegacyFileHitScanner`,
  Dateiliste trunkiert via `TruncateFileList`) unter seinem Abschnitt — kein
  Sammel-Error, weil der Fehlschlag eines Patterns den anderen ihre gültigen
  Ergebnisse nicht nehmen darf.
- **Aggregated Warning genau einmal pro Call** (nicht je Pattern): der
  Diagnostics-Pass läuft einmal, der Hinweis steht einmal am Anfang. Genau
  das ist die Einsparung gegenüber N Einzel-Calls.
- **Cap 10:** Neue Konstante `MaxPatternsPerCall = 10` in `FindSymbolTool`.
  Deterministische Obergrenze statt stiller Kappung — ein still
  abgeschnittenes Pattern würde der Agent als „nicht gesucht" missdeuten.
  10 ist großzügig über der beobachteten Realnutzung (3–6) und begrenzt den
  Worst-Case-Anteil des je-Pattern-Sweeps samt Miss-Hint-Suche. Bewusste
  Asymmetrie zur Familie: nur `find_symbol` cappt, weil nur es pro Eintrag
  einen vollen Symbol-Sweep macht; die Geschwister bleiben caplos (separates
  Thema, kein Bestandteil hier).
- **Description** (`FindSymbolDescription`) neu, Stil der Geschwister:
  „… namePatterns: Array von Namens-Mustern (auch fuer genau einen Namen;
  Batch loest N sequentielle Calls ab, max. 10 pro Call)." Der Hinweis „auch
  für genau einen Namen" ist die bewusste Gegenmaßnahme gegen
  Gewohnheits-Singular-Calls.

### `get_file_skeleton`

- Registrierung (`Mcp/FileStructureToolRegistrations.cs`, `AddGetFileSkeleton`):
  Lambda verliert `filePath`, behält `string[]? filePaths`.
- `ExtractFilePaths(filePath, filePaths)` fällt weg; stattdessen
  `McpBatchArguments.Normalize(filePaths, StringComparer.OrdinalIgnoreCase)`.
- Antwortverhalten unverändert: rein textuelle Skeleton Map, kein
  StructuredContent heute — also auch künftig keiner (kein neuer Shape).
  Abschnitts-/Divider-Rendering bleibt.
- Description: „filePaths: Array von Dateipfaden (auch für genau eine Datei),
  relativ oder absolut."

### `get_symbol_body`

- Registrierung (`Mcp/SymbolBodyToolRegistrations.cs`, `AddGetSymbolBody`):
  Lambda verliert `symbolIdentifier`, behält `string[]? symbolIdentifiers`.
- `ExtractIdentifiers` fällt weg; stattdessen
  `McpBatchArguments.Normalize(symbolIdentifiers)` (Ordinal).
- Antwortverhalten unverändert: rein textuell (Body-Markdown, Sufficiency-
  Hints, PrependWarning), kein StructuredContent — kein neuer Shape.
- Description: „symbolIdentifiers: Array von Symbol-IDs (auch für genau ein
  Symbol) …"

### `metrics_lookup`

- Registrierung (`Mcp/AnalysisToolRegistrations.cs`, `AddMetricsLookup`):
  Lambda verliert `symbolIdentifier`, behält `string[]? symbolIdentifiers`.
- `ExtractIdentifiers` fällt weg; stattdessen `Normalize(symbolIdentifiers)`
  (Ordinal).
- **StructuredContent-Vereinheitlichung:** immer `MetricsLookupBatchDto`
  (wie im heutigen Batch-Fall), unabhängig von der Array-Länge. Der
  Nacktes-DTO-Zweig (`identifiers.Count == 1 && dtos.Count == 1 ? dtos[0] :
  …`) entfällt samt seinem erklärenden Kommentar. Interner Verbrauch
  (`FeatureContextFormatter`/`FeatureContextModels` nutzen
  `MetricsLookupResultDto` direkt, nicht den Wire-Shape) ist davon nicht
  berührt.
- Description entsprechend angepasst.

### Bewusst NICHT umgestellt (Non-Goals im Enge Sinne)

Einzel-Symbol-Analysatoren bleiben Single-Parameter-Tools und bekommen **keinen**
Array-Parameter: `find_references`, `get_call_tree`, `get_impact`,
`get_type_hierarchy`, `get_feature_context` (deren `symbolIdentifier` ist
Identifikator, nicht Batch-Eingabe), ebenso `dependency_graph` (sein
`filePath`/`symbolIdentifier` ist ein Modus-Wähler, keine Batch-Liste) und
`search_pattern` (Textsuche mit einem Muster). Die Array-only-Regel gilt
ausschließlich für die vier Batch-Tools, die heute bereits
`McpBatchArguments.Collect` bzw. deren Dual-Overloads nutzen.

## Betroffene Stellen (Implementierungsplan)

| # | Ort | Änderung |
| :-- | :--- | :--- |
| 1 | `src/AiNetLinter/Mcp/McpBatchArguments.cs` | `Collect(single, multiple, comparer)` → `Normalize(values, comparer)`; Singular-Pfad löschen |
| 2 | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | `AddFindSymbol`: Lambda auf `string[]? namePatterns`; `FindSymbolDescription` neu (Array, „auch für genau einen Namen", Cap 10) |
| 3 | `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs` | `ExecuteAsync` auf Pattern-Liste; Validierung (leer/Cap) via `Normalize`; `MaxPatternsPerCall = 10`; immer Batch-Pfad, immer `Results`-StructuredContent |
| 4 | `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolScanner.cs` | Scanner-Funktion für Pattern-Liste; pro Pattern bestehende Logik; gemeinsamer Warning-Pass bleibt im Tool |
| 5 | `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` | `AddGetFileSkeleton`: `filePath` raus; Description neu |
| 6 | `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs` | Single-Overload + `ExtractFilePaths` löschen; `Normalize(..., OrdinalIgnoreCase)` |
| 7 | `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs` | `AddGetSymbolBody`: `symbolIdentifier` raus; Description neu |
| 8 | `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` | Single-Overload + `ExtractIdentifiers` löschen; `Normalize` |
| 9 | `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | `AddMetricsLookup`: `symbolIdentifier` raus; `MetricsLookupDescription` neu |
| 10 | `src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupTool.cs` | Single-Overload + `ExtractIdentifiers` löschen; immer `MetricsLookupBatchDto`; Nacktes-DTO-Zweig samt Kommentar entfernen |
| 11 | Tests (siehe Testplan) | Bestehende Calls auf Array-Form; neue Normalize-/Batch-Tests |
| 12 | `Docs/agent-api.md`, `Docs/ROADMAP.md`, `README.md`, `Docs/integration.md`, Übersichts-Task | Doku-Sync (siehe Dokumentation) |

Explizit **kein** Touch: `McpTruncation`, `McpCompileDiagnostics`,
`SearchPattern*`, Registry/Daemon (`ProjectToolCall`, TTL/MRU), `rules.json`,
CLI-Batch-Modus, die Nicht-Batch-Tools gemäß Non-Goals.

## Testplan

Anpassung Bestand (JSON-Argumente auf Array-Form; betroffene Dateien laut
Codebestand):

- FastTests: `FindSymbolToolTests`, `McpServerCommandLoadingStateTests`.
- IntegrationTests: `McpServerCommandContractTests`, `McpServerAllToolsE2ETests`,
  `McpLiveRepositoryTests`, `McpServerCommandFindSymbolTests`,
  `McpServerCommandMissHintTests`, `McpServerCommandErrorHandlingTests`,
  `McpServerCommandStalenessTests`, `McpDocumentationSmokeTests`.
- Explizit **unverändert** bleiben Tests, die lediglich `symbolIdentifier`
  für `find_references`/`get_impact` oder `pattern` für `search_pattern`
  setzen (z. B. `McpServerCommandFindReferencesTests`,
  `McpServerCommandGetImpactTests`, `GetImpactToolIntegrationTests`,
  `SearchPatternToolTests`, `SearchPatternEvaluationTests`,
  `ChangeContextResponseModelTests`) — deren Parameter bleibt bestehen.

Neu (Unit, FastTests, InMemory-Fixture `McpInMemoryTestContext`):

- `Normalize`: Trimmen, Whitespace-Einträge fallen heraus, Deduplizierung
  Ordinal bzw. OrdinalIgnoreCase (je Vergleichervalierung), null → leere Liste.
- `find_symbol`: leer/fehlend → `INVALID_ARGUMENT` mit neuem Hint-Text;
  11 Patterns → `INVALID_ARGUMENT` mit Cap-Hinweis; 10 → OK.
- Batch-Glücksfall: 2 Patterns mit Treffern → zwei Abschnitte, korrekte
  Zuordnung; structuredContent immer `Results`-Form (auch bei Länge 1).
- Miss je Pattern: A trifft, B verfehlt → B-Abschnitt mit Miss-Hint, A mit
  Treffern; Gesamt-Call kein Error.
- Trunkierung je Pattern: eigene Meta-Zeile bei `maxResults`-Kappung.
- Warning genau einmal: Fixture mit Compile-Fehler → Warnhinweis einmal pro
  Antwort, nicht je Abschnitt.
- Duplikate `[\"Foo\", \"Foo\"]` → ein Abschnitt; `"foo"`/`"Foo"` → zwei
  Abschnitte (Ordinal-Dedup), beide liefern dieselben Treffer.

Neu (Integration):

- Batch-Call über den echten stdio-Client (2 Patterns, einer mit Treffer,
  einer Miss) → Abschnitte + Miss-Hint + Meta-Zeilen wie spezifiziert.
- `MissingArrayParameter` → recoverable `INVALID_ARGUMENT` mit Hint-Text für
  alle vier Tools.
- `metrics_lookup` mit genau einem Identifier → StructuredContent ist
  `MetricsLookupBatchDto` (Regression gegen Rückfall in den Nacktes-DTO-Zweig).

Entfallene Garantien (bewusst): Die frühere Einzelform-Regression
„textlich UND structuredContent-seitig byte-identisch zum heutigen Verhalten"
(`{ \"Matches\": ... }`) sowie der metrics_lookup-Nacktes-DTO-Vertrag werden
abgelöst — der Contract-Break ist intendiert und wird in ROADMAP/agent-api.md
benannt.

Doku-/Vertragstests: `AgentApi_DescribesCsharpOnlyToolScopeWithoutHardcodedCounts`
gruppiert nur feste Sätze — muss grün bleiben.

## Dokumentation

- `Docs/agent-api.md`: Tool-Tabelle für alle vier Tools (Input nur noch
  Arrays, `find_symbol` mit Cap 10); Structured-Output-Abschnitt: `find_symbol`
  immer `Results`, `metrics_lookup` immer `MetricsLookupBatchDto`; Beispiele
  auf Array-Form umgestellt; Breaking-Change-Hinweis zu entfernten
  Singular-Parametern.
- `Docs/integration.md`: drei relevante Fundstellen prüfen (Tool-vs-`rg`-
  Abschnitt nennt `find_symbol` generisch) und falls Beispiele betroffen sind,
  mitsynchronisieren.
- `Docs/ROADMAP.md`: neuer abgeschlossener Eintrag unter dem MCP-Epic (Datum,
  Umfang, Motivation „N sequentielle Calls vermeiden", ausdrückliche
  Kennzeichnung des Contract-Breaks inkl. entfernter Singular-Parameter).
- `README.md` Tool-Tabelle: Kurzbeschreibungen der vier Tools auf Array-Form.
- `.agents/rules/AiNetLinter.mdc` und `Docs/configuration.md`: **kein** Touch —
  keine Regelwerk-Parameter betroffen.
- `tasks/mcp-server-weiterentwicklung/00_uebersicht-und-entscheidungen.md`:
  Aufgabe 13 in der Ausführungsreihenfolge-Tabelle ergänzen/aktualisieren
  (Herkunft: Beobachtung 2026-08-23; Entscheidung 2026-08-24: Array-only für
  die Familie).

## Edge-Cases (360°-Blick)

1. **Fehlend vs. leer:** fehlender Parameter, `null`, `[]`, `[" ", ""]` →
   alles derselbe `INVALID_ARGUMENT`-Pfad (Normalize liefert leer). Kein
   stiller Erfolg mit leerem Ergebnis.
2. **Duplikat-Einträge:** `[\"Foo\", \"Foo\"]` → ein Abschnitt/eine Bearbeitung
   (Deduplizierung). Kein doppelter Output, kein Fehler.
3. **Case-Unterschiede:** `"foo"`/`"Foo"` bleiben zwei Einträge (Ordinal-Dedup
   in `find_symbol`/`get_symbol_body`/`metrics_lookup`) und liefern
   bewusst doppelten Output — Konsequenz der Anfrage, kein Bug.
   `get_file_skeleton` dedupliziert Pfade `OrdinalIgnoreCase` (Bestands-
   verhalten, Windows-Pfade).
4. **Legacy-Singular-Call eines Modells:** alter Parametername → Binding-
   Fehler bzw. `INVALID_ARGUMENT`; Fehlermeldung/Hint nennt den Array-
   Parameternamen; Discovery (tools/list) zeigt die neue Signatur. Ein
   fehlgeschlagener Call + Retry ist der akzeptierte Preis (siehe oben).
5. **Loading-Zustand:** jede Tool-Familien-Reihenfolge bleibt wie heute —
   die drei Geschwister prüfen Load-State vor den Argumenten
   (`Loading()` zuerst), `find_symbol` validiert Argumente zuerst und antwortet
   erst danach mit `Loading()`/`SOLUTION_NOT_LOADED`. Diese bestehende
   intrafamiliäre Abweichung wird nicht geglättet (Glättung würde sichtbares
   Verhalten ändern, ohne zum Ziel „Array-only" beizutragen).
6. **Solution nicht geladen:** nach bestandener bzw. gemäß Punkt 5
   gereihter Validierung → `SOLUTION_NOT_LOADED`, wie heute.
7. **Unerwartete Roslyn-Exception:** defensiver Wrapper bleibt; Kontext-
   String nennt die Eintragsliste (gekürzt), nicht nur den ersten.
8. **`kind` ungültig (`find_symbol`):** Validierung einmalig vorab für alle
   Patterns — gleiche Fehlermeldung wie heute, kein je-Pattern-Retry.
9. **Antwortgröße:** Worst Case wächst linear (Einträge × je-Eintrag-Limit);
   bei `find_symbol` durch Cap 10 begrenzt, bei den Geschwistern wie heute
   unbegrenzt (kein neues Budget-Konzept in diesem Task).
10. **Miss-Hint-Kosten im Batch:** Legacy-Textsuche läuft pro verfehltem
    Pattern — akzeptiert; der Cap begrenzt den Multiplikator.
11. **Observability:** Call-Log-Schema unverändert; `durationMs` misst nun
    den jeweiligen Batch. Für die Nutzungsanalyse (Aufgabe 01) entsteht keine
    Drift; Eintragsanzahl wird bewusst nicht geloggt.
12. **Daemon/Registry:** `projectRoot`-Lease-Handling unverändert; Batch
    erhöht die Lease-Dauer pro Call (ein statt N Calls) — entlastet TTL/MRU.
13. **Annotation-Tools (04_tool-annotations):** Read-only-Semantik bleibt;
    unabhängiger Task, hier keine Berührung.
14. **Interner Verbrauch von DTOs:** `FeatureContext*` nutzt
    `MetricsLookupResultDto` direkt — von der Wire-Änderung nicht betroffen
    (verifiziert gegen `FeatureContextFormatter.cs`/`FeatureContextModels.cs`).

## Blick über den Tellerrand (bewusst NICHT Teil dieses Tasks)

- **Compile-Warning-Pass cachen:** Der eigentliche Performance-Hebel auch für
  Einzel-Calls. Trifft mehrere Tools (`find_references`, `get_impact`,
  `get_type_hierarchy`, `search_pattern`, …). Eigener Step mit
  Staleness-Frage (Cache-Key auf Solution-Version), nicht hier vermischen.
- **Caps für die Geschwister:** `get_file_skeleton`/`get_symbol_body`/
  `metrics_lookup` haben heute keinen Eintrags-Limit; falls Nutzungsdaten
  (Aufgabe 01) Missbrauch zeigen, eigener Step.
- **Kein `maxResponseBytes` für `find_symbol`:** `search_pattern` hat es,
  weil dort freie Textmengen entstehen; `find_symbol`s Output ist
  strukturiert gekappt (maxResults × Cap) — ein Byte-Limit wäre ein zweiter,
  konkurrierender Cut.

## Definition of Done

- [ ] Alle vier Batch-Tools akzeptieren ausschließlich Array-Parameter;
      Singular-Parameter sind aus Registrierung, Tool-Implementierung und
      Descriptions entfernt (kein Shim, keine Überladung).
- [ ] `McpBatchArguments.Normalize` ersetzt `Collect`; Vergleicherverteilung
      wie oben (OrdinalIgnoreCase nur für Dateipfade).
- [ ] `find_symbol`: Cap 10 mit hartem Fehler darüber; immer
      `Results`-StructuredContent; je-Pattern-Trunkierung, je-Pattern-Miss-
      Hint, Warning genau einmal.
- [ ] `metrics_lookup`: immer `MetricsLookupBatchDto` (auch bei Länge 1),
      Regressiontest dagegen.
- [ ] Neue Unit-/Integrationtests grün; alle benannten Bestandstests auf
      Array-Form umgestellt und grün; Nicht-Batch-Tool-Tests unverändert grün.
- [ ] `dotnet build` sowie beide Nicht-Stress-Testprojekte grün.
- [ ] Doku-Sync vollständig (agent-api.md, integration.md, ROADMAP.md mit
      Breaking-Note, README.md, Übersichts-Index).
