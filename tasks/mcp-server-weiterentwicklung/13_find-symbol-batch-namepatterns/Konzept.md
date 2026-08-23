---
status: offen
type: konzept
project_kind: brownfield
estimated_scope: small-medium
priority: P1
agent_role: .agents/Agent-Scaffolding/dev-loop/planning/orchestrator.md
rules_dir: .agents/rules
last_updated: 2026-08-23
open_questions: []
herkunft: Beobachtung 2026-08-23 (Ralf) — Agenten rufen find_symbol mehrfach direkt hintereinander auf; Entscheidung A1 (additiv, konsistent zur Toolfamilie)
---

# `find_symbol`: Batch-Parameter `namePatterns[]` ergänzen (A1, additiv)

## Ziel

`find_symbol` akzeptiert zusätzlich zum bestehenden Einzelpattern `namePattern` ein
Batch-Array `namePatterns[]`, damit ein Agent mehrere Symbol-Namen in **einem** Turn
und mit **einem** Solution-Durchlauf auflösen kann. Die Anpassung orientiert sich
vollumfänglich an den vorhandenen Batch-Tools der Familie (`get_file_skeleton`,
`get_symbol_body`, `metrics_lookup`) und ihrem gemeinsamen Helper
`McpBatchArguments.Collect` — kein neues Tool, kein neues Antwortformat jenseits
des Familienmusters.

Nicht Teil dieses Tasks: die Umstellung der gesamten Toolfamilie auf reine Arrays
(diskutiert, bewusst verschoben — separater Task, sobald Nutzungsdaten vorliegen)
sowie das Caching des Compile-Warning-Passes (siehe „Blick über den Tellerrand").

## Intention und Fehlverhalten heute

### Beobachtung

Agenten suchen typischerweise nicht nach genau einem Namen. Realer Loop im eigenen
Dogfooding: 3–6 `find_symbol`-Aufrufe direkt hintereinander („finde Klasse X, Y, Z,
dann Methode A"). Der MCP-Server ist dafür ausgelegt — residente Solution, Roslyn-
Symbolgraph — aber der Vertrag von `find_symbol` erzwingt einen Call pro Name:

```csharp
// SymbolGraphToolRegistrations.cs:47
async (string projectRoot, string? namePattern = null, string? kind = null,
       int maxResults = 50, CancellationToken ct = default) => ...
```

### Warum jeder dieser Calls teuer ist (Fehl-Verhalten im weiteren Sinne)

Ein einzelner `find_symbol`-Call durchläuft zwei vollständige Solution-Pässe,
**unabhängig davon, wie eng das Pattern ist**:

1. **SymbolFinder-Sweep:** `FindSymbolScanner.FindMatchesWithEntriesAsync`
   (`FindSymbolScanner.cs:65`) iteriert via `SymbolFinder.FindSourceDeclarationsAsync`
   über alle Quell-Deklarationen der Solution und matcht per Substring.
2. **Compile-Diagnostics-Pass (der dominante Anteil):**
   `FindSymbolTool.BuildAggregateWarningAsync` (`FindSymbolTool.cs:80`) ruft
   `McpCompileDiagnostics.GetErrorsByFileAsync` (`McpCompileDiagnostics.cs:27–45`).
   Diese Helper-Methode iteriert **jedes Projekt** und ruft pro Projekt
   `compilation.GetDiagnostics()` — den **vollständigen Compile-Diagnose-Lauf** — an.
   Es gibt keinerlei Cache; der eigene Doc-Kommentar hält fest: „jeder Aufruf
   potentiell den vollen Compile-Zyklus".

Die Folge: N sequenzielle `find_symbol`-Calls = N komplette Diagnostics-Pässe über
die gesamte Solution, nur um einen Warnhinweis zu bauen, der im Regelfall leer ist.
Der Batch-Wunsch der Agenten ist also berechtigt — nur der Vertrag gibt es nicht her.

### Warum nicht `search_pattern`?

`search_pattern` ist die bewusste Fallback-Stufe für Text außerhalb des C#-Symbolgraphs
(`Docs/integration.md` „Tool-vs-`rg`-Empfehlung"; `ServerInstructions.cs`). Es nimmt
ebenfalls nur ein `pattern` und liefert keine Symbolsemantik (Kind, Signatur,
Datei:Zeile der Deklaration). Ein Ausweichen dorthin wäre kein Ersatz.

### Kein Duplikat im Bestand (Audit)

Geprüft gegen alle 26 registrierten Tools: `find_symbol` ist der **einzige
deterministische Roslyn-Namens-Locator**. `get_symbol_body`/`find_references`/
`get_class_structure`/`metrics_lookup` benötigen bereits eine stabile Symbol-ID
(sie können also nicht lokalisieren), `get_namespace_tree` liefert hierarchische
Orientierung statt Namenssuche, `search_pattern` ist Textsuche ohne Semantik.
Löschung/Ersetzung von `find_symbol` scheidet aus; stattdessen Erweiterung —
deckungsgleich mit der Festlegung in `90_bewusst-nicht-umsetzen/Konzept.md`
(A.2: „bestehendes Tool erweitern, keinen Alias und kein zweites Schema").

## Vertrag (Zielzustand)

### Parameter

```csharp
// SymbolGraphToolRegistrations.cs — AddFindSymbol
tools.Add(McpServerTool.Create(
    async (string projectRoot,
           string? namePattern = null,
           string[]? namePatterns = null,
           string? kind = null,
           int maxResults = 50,
           CancellationToken ct = default) => ...
```

- **`namePattern` bleibt unverändert bestehen** (Entscheidung A1, additiv). Bestehende
  Clients, Prompts und Tests laufen weiter — identisch zur Schnittlage von
  `get_file_skeleton` (`filePath`/`filePaths`, `FileStructureToolRegistrations.cs:119–122`)
  und `get_symbol_body` (`symbolIdentifier`/`symbolIdentifiers`,
  `SymbolBodyToolRegistrations.cs:35`).
- `namePatterns[]` ist optional, leere Strings im Array werden ignoriert,
  Duplikate werden dedupliziert — exakt die Semantik von
  `McpBatchArguments.Collect(single, multiple, comparer)` (`McpBatchArguments.cs:16`),
  die die drei Batch-Tools der Familie bereits teilen.
- **Beide gesetzt → Merge, kein Fehler.** Wichtiges Audit-Ergebnis: Meine ursprüngliche
  Idee „beide gesetzt = harter Fehler" widerspricht der etablierten Familien-Semantik.
  Alle drei bestehenden Verwendungen von `McpBatchArguments.Collect`
  (`GetFileSkeletonTool.cs:125`, `GetSymbolBodyTool.cs:140`, `MetricsLookupTool.cs:130`)
  mergen Einzelwert + Array dedupliziert, statt XOR zu erzwingen. `find_symbol`
  schließt sich dem an (Konsistenz vor Schönheit; die Dualismus-Frage stellt die
  ganze Familie später einmal).
  Konsequenz konkret: `namePattern="Foo"` + `namePatterns=["Bar"]` sucht nach
  `["Foo", "Bar"]`. Das ist deterministisch, dokumentierbar und deckt den Fall
  „Agent wiederholt den letzten Einzelsuchbegriff und hängt neue an" sinnvoll ab.
  Deduplizierung mit `StringComparer.Ordinal` — Substring-Matching ist
  case-insensitive, aber die Identität zweier Patterns wird ordnungsgemäß zeichenweise
  entschieden (kein stillschweigendes Zusammenwerfen von `"foo"`/`"Foo"`.
- **Weder noch → harter Fehler wie heute.** `INVALID_ARGUMENT` (recoverable),
  Meldung analog zu `get_file_skeleton`:
  „Pflichtparameter 'namePattern' oder 'namePatterns' fehlt oder ist leer." mit Hint
  `namePattern: "Greeter" oder namePatterns: ["Greeter", "GreetService"]`.
  Keine Auto-Vereinigung, kein Default-Pattern, kein Rumraten.

### Batch-Umfang (Cap)

Neue Konstante `MaxPatternsPerCall = 10` in `FindSymbolTool`. Mehr als 10 Patterns →
`INVALID_ARGUMENT` mit Hinweis auf den Cap. Begründung:

- Deterministische Obergrenze statt stiller Kappung — ein still abgeschnittenes
  Pattern würde der Agent als „nicht gesucht" missdeuten.
- 10 ist großzügig über der beobachteten Realnutzung (3–6) und begrenzt den
  Worst-Case-Anteil des Diagnostics-Passes (der ohnehin einmal pro Call läuft).
- Familien-Kompatibel: `get_symbol_body` kappt derzeit nicht (Risiko dort separat);
  wir führen den Cap hier neu ein, weil `find_symbol` pro Pattern einen vollen
  Symbol-Sweep macht. Abweichung wird in der Description benannt.

### Semantik je Pattern (unverändert zur Einzelform)

Pro Pattern gilt exakt das heutige Einzelverhalten, inklusive aller Kanten:

- Substring-Match case-insensitive auf Deklarationsnamen
  (`SymbolFilter.TypeAndMember`), Kind-Filter `kind` (gemeinsam für alle Patterns,
  Validierung gegen `ValidKinds` weiterhin einmalig vorab).
- **`maxResults` wirkt pro Pattern** (Entscheidung vom Nutzer gebilligt): Jedes
  Pattern bekommt seine eigene Trefferliste inklusive eigener Trunkierungs-Meta-Zeile
  (`McpTruncation.TruncateLines`). Vorhersehbar und deterministisch; ein globales
  Limit über alle Patterns wäre bei Batch-Antworten nicht mehr interpretierbar.
- **Miss-Hint je Pattern:** Liefert ein Pattern 0 C#-Treffer, erscheint sein
  individueller Miss-Hint (Legacy-Textsuche via `SearchPatternLegacyFileHitScanner`,
  Dateiliste trunkiert via `TruncateFileList`) unter seinem Abschnitt — kein
  Sammel-Error für den ganzen Batch, weil ein Fehlschlag eines Patterns den anderen
  Patterns ihre gültigen Ergebnisse nicht nehmen darf.
- **Aggregated Warning genau einmal pro Call** (nicht je Pattern): Der
  Compile-Diagnostics-Pass läuft einmal, der Warnhinweis steht einmal am Anfang der
  Antwort. Genau das ist die Einsparung gegenüber N Einzel-Calls.

### Antwortformat

Text-Antwort: Abschnitte pro Pattern, getrennt mit dem familieneigenen Trennmuster
(vgl. `MarkdownBuilder.Divider()` in `GetSymbolBodyTool.RenderSymbolBodiesAsync`);
je Abschnitt eine Kopfzeile mit dem angefragten Pattern, darunter das gewohnte
`Datei:Zeile - Kind: Signatur`-Listing inkl. Meta-Zeilen.

StructuredContent (JSON-Objekt, nie Top-Level-Array — Regression
`McpToolResultsTests.Text_WithListPayload_StructuredContentIsJsonObjectNotArray`):

```json
{
  "Results": [
    { "NamePattern": "Greeter", "Matches": [ /* SymbolLocationEntry[] */ ] },
    { "NamePattern": "GreetingService", "Matches": [] }
  ]
}
```

- **Einzelform (nur `namePattern`):** `structuredContent` bleibt **exakt** wie heute
  `{ "Matches": [...] }` — kein Breaking Change, keine neuen Felder.
- **Batchform (`namePatterns` gesetzt, auch Merge-Fall):** `structuredContent`
  wechselt für diesen Call zu `{ "Results": [{ NamePattern, Matches }] }`.
  Ein Merge-Call (`namePattern`+`namePatterns`) ist definitionsgemäß Batch.
- **Casing konsistent zum Bestand:** Alle Schlüssel PascalCase (`Results`,
  `NamePattern`, `Matches`), identisch zum heutigen `Matches`-Payload
  (`SymbolLocationEntry` serialisiert ebenfalls PascalCase) — keine zweite
  Naming-Konvention einführen.
- Die Entries je Pattern sind auf `maxResults` gekappt, konsistent zur
  Text-Trunkierung (heute schon so, `FindSymbolScanner.cs:83`).

### Description / Discovery

`FindSymbolDescription` wird erweitert (Stil der Geschwister-Descriptions):
„… namePattern (einzeln) ODER namePatterns (Array fuer Batch in 1 Turn, max. 10):
mehrere Symbole mit EINEM Aufruf statt N sequentiellen Calls."

`ServerInstructions.Text` bleibt unberührt: Das Byte-Budget beträgt 2.557 Bytes und
ist knapp kalkuliert; die Start-Sequenz „find_symbol -> find_references/get_impact"
bleibt gültig. Der Batch-Hinweis gehört auf die Tool-Ebene (tools/list), nicht in
den globalen Instructions-Text — konsistent zum Kommentar in `ServerInstructions.cs`.

## Betroffene Stellen (Implementierungsplan)

| # | Ort | Änderung |
| :-- | :--- | :--- |
| 1 | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | Lambda um `string[]? namePatterns = null` erweitern; Dispatch an Tool |
| 2 | `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs` | `ExecuteAsync` sammelt Patterns via `McpBatchArguments.Collect`, validiert (leer → INVALID_ARGUMENT; >Cap → INVALID_ARGUMENT), Single-Pfad unverändert lassen, Batch-Pfad neu |
| 3 | `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolScanner.cs` | Scanner-Funktion für Pattern-Liste ergänzen; pro Pattern bestehende Logik nutzen; gemeinsamer Warning-Pass bleibt im Tool |
| 4 | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | `FindSymbolDescription` erweitern |
| 5 | Tests (siehe unten) | Unit + Integration |

Explizit **kein** Touch: `McpTruncation`, `McpCompileDiagnostics`, `SearchPattern*`,
Registry/Daemon (`ProjectToolCall`, TTL/MRU), `rules.json`, CLI-Batch-Modus.

## Testplan

Unit (FastTests, Category Unit/Component, InMemory-Fixture `McpInMemoryTestContext`):

- Collect-Semantik: Merge von `namePattern` + `namePatterns`, Deduplizierung
  (Ordinal), Leersätze im Array fallen heraus.
- Weder noch → `INVALID_ARGUMENT`; nur Whitespace/leeres Array → ebenso.
- Cap: 11 Patterns → `INVALID_ARGUMENT` mit Cap-Hinweis; 10 → OK.
- Batch-Glücksfall: 2 Patterns mit Treffern → zwei Abschnitte, korrekte Zuordnung;
  structuredContent-Form `Results` mit je-Pattern-Entries.
- Miss je Pattern: Pattern A trifft, Pattern B verfehlt (0 C#-Treffer) → B-Abschnitt
  enthält Miss-Hint, A-Abschnitt enthält Treffer; Gesamt-Call kein Error.
- Trunkierung je Pattern: pro Abschnitt eigene Meta-Zeile bei `maxResults`-Kappung.
- Warning genau einmal: Fixture mit Compile-Fehler (`CompileErrorMiniFixture`-Muster)
  → Warnhinweis einmal pro Antwort, nicht je Abschnitt.
- Einzelform-Regression: Nur `namePattern` → Text und structuredContent byte-identisch
  zum heutigen Verhalten (`{ "Matches": ... }`).

Integration (IntegrationTests):

- Bestehende Tests bleiben unverändert grün (Vertrag Garantie): 
  `McpServerCommandContractTests.RunAsync_ValidFixture_FindSymbolReturnsMatch`,
  `McpServerCommandFindSymbolTests` (Truncation), `McpServerCommandMissHintTests`,
  `McpServerAllToolsE2ETests` (Kind-Filter, Zero-Results, MissingNamePattern),
  `McpDocumentationSmokeTests.FindSymbol_*`, `McpLiveRepositoryTests.LiveDogfood_FindSymbol`.
- Neu: Batch-Call über den echten stdio-Client (2 Patterns, einer mit Treffer, einer
  Miss) → Abschnitte + Miss-Hint + Meta-Zeilen wie spezifiziert.
- Neu: `MissingBothParameters` → recoverable INVALID_ARGUMENT mit neuem Hint-Text.

Doku-/Vertragstests: `AgentApi_DescribesCsharpOnlyToolScopeWithoutHardcodedCounts`
gruppiert nur feste Sätze — kein Update nötig, muss aber grün bleiben.

## Dokumentation

- `Docs/agent-api.md`:
  - Tool-Tabelle Zeile `find_symbol` (Zeile 251): Input um
    `namePatterns?` (Batch-Array, max. 10) erweitern; Merge-Semantik benennen.
  - Structured-Output-Abschnitt (~Zeile 280): Batchform `Results` dokumentieren,
    Einzelform unverändert.
  - Beispiel-Request (~Zeile 551): unverändert lassen (Einzelform bleibt gültig).
- `Docs/ROADMAP.md`: neuer abgeschlossener Eintrag unter dem MCP-Epic (Datum,
  Umfang, Motivation „N sequenzielle Calls vermeiden").
- `README.md` Tool-Tabelle: Kurzbeschreibung um „(einzelne Namen oder Batch-Array)"
  ergänzen — eine Zeile, kein Umbau.
- `.agents/rules/AiNetLinter.mdc` und `Docs/configuration.md`: **kein** Touch —
  `find_symbol` ist kein Regelwerk-Parameter und taucht dort nicht auf.
- `tasks/mcp-server-weiterentwicklung/00_uebersicht-und-entscheidungen.md`:
  Aufgabe 13 in der Ausführungsreihenfolge-Tabelle ergänzen (Herkunft:
  Beobachtung 2026-08-23).

## Edge-Cases (360°-Blick)

1. **Leeres Array vs. Array mit Leerstrings:** `[ ]` → INVALID_ARGUMENT;
   `[" ", ""]` → ebenfalls (Collect liefert leer). Kein stiller Erfolg mit
   leerem Ergebnis.
2. **Duplikat-Patterns:** `["Foo", "Foo"]` → ein Abschnitt (Deduplizierung).
   Kein doppeltes Result, kein Fehler.
3. **Merge-Fall Deduplizierung:** `namePattern="Foo"` + `namePatterns=["Foo","Bar"]`
   → `["Foo","Bar"]` (Collect dedupliziert cross-parameter).
4. **Case-Unterschiede im Pattern:** `"foo"`/`"Foo"` sind zwei Abschnitte
   (Ordinal-Dedup), beide matchen dieselben Symbole — doppelter Output ist
   dann gewollte Konsequenz der Anfrage, nicht ein Bug.
5. **Loading-Zustand:** Reihenfolge bewusst **wie heute** lassen — `find_symbol`
   validiert die Argumente zuerst (`FindSymbolTool.cs:54`) und prüft erst danach
   LoadState/Solution (`:72–74`); `get_file_skeleton` macht es umgekehrt. Diese
   bestehende Familienabweichung wird hier nicht geglättet, weil eine Umstellung
   das Einzelform-Verhalten ändern würde (fehlendes Pattern während des Ladens
   antwortete statt `INVALID_ARGUMENT` plötzlich mit `Loading`). Batch folgt der
   find_symbol-eigenen Reihenfolge: invalide Batch-Args → sofort
   `INVALID_ARGUMENT`; valide Args während Solution-Load → `McpToolResults.Loading()`.
6. **Solution nicht geladen:** nach bestandener Argument-Validierung →
   `SOLUTION_NOT_LOADED`, wie heute (Validierung läuft in `find_symbol` zuerst,
   siehe Punkt 5).
7. **Unerwartete Roslyn-Exception:** defensiver Wrapper bleibt; Kontext-String im
   CompilationError nennt die Pattern-Liste (gekürzt), nicht nur das erste.
8. **`kind` ungültig:** Validierung einmalig vorab für alle Patterns — gleiche
   Fehlermeldung wie heute, kein je-Pattern-Retry.
9. **Sehr breite Patterns im Batch:** jedes Pattern trunkiert auf `maxResults`
   (Default 50); Worst Case Antwortgröße wächst linear mit Pattern-Anzahl ×
   maxResults — durch Cap 10 begrenzt. `maxResponseBytes` gibt es (bewusst) nicht
   bei `find_symbol`; wer klein antworten will, senkt `maxResults`.
10. **Miss-Hint-Kosten im Batch:** Legacy-Textsuche läuft pro gemisstem Pattern —
    akzeptiert, denn sie ist die dokumentierte Fallback-Evidenz; der Cap begrenzt
    den Multiplikator.
11. **Observability:** Call-Log schreibt toolName `find_symbol` — Batch ändert
    nichts am Record-Schema; `durationMs` misst nun den Batch. Für die
    Nutzungsanalyse (Aufgabe 01) entsteht keine Drift, da die Pattern-Anzahl
    nicht geloggt wird — bewusst keine Schema-Änderung am Call-Log.
12. **Daemon/Registry:** `projectRoot`-Lease-Handling unverändert; Batch erhöht
    die Lease-Dauer pro Call (ein statt N Calls) — entlastet TTL/MRU eher, als
    dass es sie belastet.
13. **Annotation-Tools (04_tool-annotations):** Read-only-Semantik bleibt;
    falls Aufgabe 04 noch nicht umgesetzt ist, ändert der Batch nichts an den
    fehlenden Annotations — unabhängiger Task.

## Blick über den Tellerrand (bewusst NICHT Teil dieses Tasks)

- **Compile-Warning-Pass cachen** (Option B aus der Diskussion): Der eigentliche
  Performance-Hebel auch für Einzell-Calls. Trifft laut EPIC-06 mehrere Tools
  (find_references, get_impact, get_type_hierarchy, search_pattern, …). Eigener
  Step mit Staleness-Frage (Cache-Key auf Solution-Version), nicht hier vermischen.
- **Familienweite Aufräumung des Singular/Array-Dualismus** („A2 für alle"):
  Sobald Aufgabe 01 (Nutzungsevidenz) vorliegt, prüfen, ob alle Batch-Tools auf
  reine Arrays umstellen. Bis dahin A1-Konsistenz.
- **Kein `maxResponseBytes` für find_symbol**: search_pattern hat es, weil dort
  freie Textmengen entstehen; find_symbols Output ist strukturiert gekappt
  (maxResults × Cap) — ein Byte-Limit wäre ein zweiter, konkurrierender Cut.

## Definition of Done

- [ ] `namePatterns[]` akzeptiert; Merge + Ordinal-Dedup via `McpBatchArguments`;
      Cap 10 mit hartem Fehler darüber.
- [ ] Einzelform textlich UND structuredContent-seitig unverändert (Regressiontest).
- [ ] Batchform: Abschnitte je Pattern, je-Pattern-Trunkierung, je-Pattern-Miss-Hint,
      Warning genau einmal, `Results`-StructuredContent.
- [ ] Description erwähnt Batch + Cap; ServerInstructions unverändert (Byte-Budget
      weiterhin ≤ 2557).
- [ ] Neue Unit-/Integrationtests grün; alle oben genannten Bestandstests unverändert grün.
- [ ] `dotnet build` sowie beide Nicht-Stress-Testprojekte grün.
- [ ] Doku-Sync vollständig (agent-api.md, ROADMAP.md, README.md, Übersichts-Index).
