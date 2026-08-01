---
unit: 003
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
epic: EPIC-05 (Scope-Kommunikation Teil a + Miss-Hint Teil b in find_symbol)
extends:
  - konzept.md Z. 154-166 (Scope-Kommunikation: Tool-description + initialize.instructions)
  - konzept.md Z. 167-174 (Miss-Hint in find_symbol bei 0 C#-Treffern)
  - konzept.md Z. 540-553 (Tool-Set-Tabelle — find_symbol-Eintrag schärfen)
  - konzept.md Z. 604-606 (DoD: "Name nur in .js/.razor/.xaml → explizite Miss-Hint-Meldung")
  - units/002/plan.md (exportierte GetFilesWithHits-API in SearchPatternScanner)
  - units/002/fix-01/review.md (TD-011-Pflicht-Check bei Symbolgraph-Tool-Erweiterung)
---

# Plan Einheit 003 — EPIC-05 Scope-Kommunikation + Miss-Hint in `find_symbol`

## Ziel der Einheit

EPIC-05 abschließen für `find_symbol`: bei null C#-Treffern einen
**Miss-Hint** liefern, der Textvorkommen in nicht abgedeckten
Dateitypen (`.js`/`.razor`/`.xaml`/`.html`/`.css`) meldet, statt
einer stillen Leermenge — und die **Scope-Kommunikation** zentral
vervollständigen, indem der `initialize`-Handshake den
`ServerInstructions`-Text trägt, der die C#-only-Grenze einmal
server-weit benennt. Die Tool-`description` von `find_symbol` selbst
wird **minimal erweitert** (Miss-Hint-Verhalten erwähnt); die
Grenz-Formulierung "Deckt nur .cs-Dateien ab" steht dort bereits seit
Einheit 002-Folge-Commits (`SymbolGraphToolRegistrations.cs:31-32`)
und ist nicht zu wiederholen. Bezug: `konzept.md` Z. 98-101 (EPIC-05
Definition), Z. 604-606 (DoD-Kriterium), Z. 161-164 (initialize-
instructions-Feld).

## Scope-Entscheidung

**Gewählt: EPIC-05 vollständig für `find_symbol` (Scope-Kommunikation
+ Miss-Hint).** Trunkierung in `find_symbol`/`find_references`/
`get_impact` (P0/P1 aus `konzept.md` Z. 215-225) bleibt **separaten
Folge-Einheiten** vorbehalten (004/005/006 oder eine konsolidierte
Einheit, die der nächste Planer schneidet — siehe
`units/002/fix-01/review.md` Z. 213-217).

**Begründung der Wahl:**

1. **EPIC-05 ist die stärker gebundene Wahl** — `konzept.md` Z. 604-606
   nennt den Miss-Hint explizit als DoD-Kriterium ("Eine Anfrage nach
   einem Namen, der nur in einer `.js`/`.razor`/`.xaml`-Datei
   vorkommt, liefert die explizite Miss-Hint-Meldung statt einer
   stillen Leermenge"), die Trunkierung dagegen nur als
   Erweiterungs-Element ohne expliziten Tool-Bezug im DoD-Abschnitt.
2. **API ist bereits gebaut** — `SearchPatternScanner.GetFilesWithHits`
   (`SearchPatternScanner.cs:88-112`) wurde in 002 **explizit für
   EPIC-05 / 003 exportiert** (siehe XMLDoc Z. 22-26: *"Zusatz-API
   `GetFilesWithHits` ist der importierbare Mechanismus fuer EPIC-05 /
   Einheit 003 (Miss-Hint in `find_symbol`)"*). Die Schnittstelle ist
   exakt passend: `Solution solution, string pattern, bool isRegex`
   → `IReadOnlyList<string>` (solution-relative Forward-Slash-Pfade,
   sortiert). Keine API-Erweiterung nötig.
3. **SymbolgraphMini-Fixture ist bereits vorbereitet** — die Fixture
   enthält bereits `wwwroot/site.js`, `wwwroot/Component.razor`,
   `wwwroot/index.html`, `wwwroot/Page.xaml`, `wwwroot/styles.css`
   (siehe `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/`).
   Der Coder muss nur **eine** dieser Dateien um einen Identifier
   erweitern, der in keiner `.cs`-Datei der Fixture vorkommt (siehe
   Schritt 0 unten).
4. **`ServerInstructions` ist im SDK verfügbar und ungenutzt** — die
   `McpServerOptionsFactory.cs:25-36` setzt aktuell nur `ServerInfo`
   und `ToolCollection`. Das SDK (`ModelContextProtocol` 2.0.0,
   `McpServerOptions`) hat eine `ServerInstructions`-Property
   (`String`, get/set) — verifiziert per Reflection-Probe gegen die
   SDK-DLL am 2026-08-01 (siehe Vor-der-Planung-Check 3). Konzept
   Z. 161-164 erwähnt das Feld namentlich, der Coder muss also
   **keine** SDK-Recherche durchführen.

**Bewusst NICHT in 003:**

- **Trunkierung in `find_symbol`/`find_references`/`get_impact`** —
  wäre eine eigene Einheit (mind. 3 Touchpoints, je 1 A3-Nachweis
  pro existierendem Test, je 1 Footprint-Re-Messung). `McpTruncation.cs`
  ist in 002 für `search_pattern` bereits angeschlossen.
- **`SymbolGraphToolRegistrations`-Description-Update für andere
  Tools** — `find_references`/`get_impact`/`get_type_hierarchy` haben
  ihre Scope-Aussage bereits explizit in der Description
  (`SymbolGraphToolRegistrations.cs:40-44, 52-55, 64-67`). Die
  "Beschreibung pro Tool benennt die Grenze"-Anforderung ist für
  diese drei Tools **bereits erfüllt** (auch wenn das im Konzept
  Z. 154-166 nicht explizit hervorgehoben ist). Nur `find_symbol`
  bekommt den additiven Miss-Hint-Hinweis, weil dort 003 das
  Verhalten ändert.
- **Miss-Hint in `find_references`/`get_impact`** — beide arbeiten
  auf einem **bereits gefundenen** Symbol (Symbol-Identifikator
  statt Name-Pattern), der Miss-Hint-Sinn (Pattern existiert
  irgendwo, aber nicht als C#-Symbol) passt dort konzeptuell nicht.
  Konzept Z. 167-174 nennt nur `find_symbol`.
- **EPIC-06 (Robustheit), EPIC-07 (Tests-Ausbau), EPIC-08 (Doku)** —
  separate Einheiten, kein 003-Scope.

## Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### Check 1 — Existierender Miss-Hint-Pfad in `find_symbol`

**Befund (gelesen):**

- `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` existiert (101 Z.).
  **Es gibt keine separate `FindSymbolScanner.cs`** — die gesamte
  Logik lebt im Tool (anders als bei `search_pattern` und
  `get_violations`, die TD-005-Muster-konform einen separaten
  Scanner haben).
- `FindSymbolTool.FindMatchesAsync` Z. 41-60: aktueller Leermenge-
  Pfad ist Z. 51-55:
  ```csharp
  if (filtered.Count == 0)
  {
      var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
      return $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
  }
  ```
- Symbol-Trefferformat ist `"{relativePath}:{line} - {kindLabel}: {symbolDisplayString}"`
  (Z. 89), also reines Klartext.
- Die `find_symbol`-Beschreibung in `SymbolGraphToolRegistrations.cs:31-32`
  ist **bereits explizit**: "Deckt nur .cs-Dateien ab, keine
  .js/.razor/.xaml/.html/.css-Dateien." — der Orchestrator-Hinweis
  im Prompt ("benennt die Grenze noch nicht so explizit") trifft
  also für die Tool-Description **nicht** zu. Was fehlt, ist die
  Erwähnung des **Miss-Hint-Verhaltens** in der Tool-Description
  selbst (analog 002 für `search_pattern`, das den
  Fallback-Charakter explizit benennt) — das ist ein additiver
  Hint, kein Ersatz.

**Entscheidung im Plan:**

- **Der Miss-Hint-Pfad gehört in `FindSymbolTool.cs`** — **nicht**
  in einen neuen `FindSymbolScanner.cs`. Begründung (Kernel "Drift"):
  Der bestehende Code hat die gesamte `find_symbol`-Logik im Tool;
  das Aufteilen wäre eine **strukturelle Änderung** ohne funktionalen
  Mehrwert für 003 und würde das Review-Aufkommen für eine reine
  Code-Umzugs-Aktion künstlich aufblähen (A5: "fertig ist fertig").
  Der TD-005-Hinweis ("dünner Dispatch + separate Scanner-Datei")
  ist als **Prävention** für **neue** Tools gedacht, nicht als
  nachträgliche Pflicht für bestehende Tools ohne AIContextFootprint-
  Not (siehe `tech-debt.md` Z. 75: *"etabliertes Gegenmuster …
  funktioniert konsequent, wenn von Anfang an angewendet, nicht
  erst reaktiv"*). `FindSymbolTool` ist aktuell 2518 Z. mit
  PathOverride 2700 (Puffer 182) — keine Not. **Falls** ein
  Folge-Schritt (Trunkierung 004) das Tool weiter aufbläht und in
  PathOverride-Nähe rückt, ist der Scanner-Split dann TD-005-konform
  zu prüfen — **nicht** in 003.
- **Neuer Code:** in `FindMatchesAsync` direkt nach dem
  `filtered.Count == 0`-Block (Z. 51-55): Aufruf von
  `SearchPatternScanner.GetFilesWithHits(solution, namePattern,
  isRegex: false)` und **bei nicht-leerer Trefferliste** Anhängen
  einer Hint-Zeile (Plain-Text, ein oder zwei Zeilen — siehe
  Schritt 2 unten für exakten Wortlaut). Bei leerer Trefferliste:
  Verhalten unverändert (nur "Keine Treffer …").

### Check 2 — `GetFilesWithHits`-API in `SearchPatternScanner`

**Befund (gelesen, `SearchPatternScanner.cs:88-112`):**

```csharp
internal static IReadOnlyList<string> GetFilesWithHits(
    Solution solution,
    string pattern,
    bool isRegex)
```

Liefert `IReadOnlyList<string>` solution-relative Pfade
(Forward-Slashes, `SortedSet<string>`-sortiert, deterministisch).
Iteriert über `WebFileCatalog.GetProjectDirectories(solution)` →
`SafeEnumerateFiles` → `IsGeneratedPath`-Filter → `FileMatches` (=
Substring `OrdinalIgnoreCase` bzw. kompilierte Regex) — analog
`SearchAndFormat`. Wirft `ArgumentException` bei ungültiger Regex
(`isRegex == true`).

**Bewertung für 003-Miss-Hint-Pfad:**

- **Signatur passt** für `find_symbol`-Miss-Hint **ohne Erweiterung**:
  `Solution` ist in `FindMatchesAsync` bereits verfügbar (Parameter
  Z. 42), `namePattern` ist der Such-Name (String-Parameter),
  `isRegex: false` ist der semantisch korrekte Default für
  `find_symbol` (Substring-Match, identisch zu `SymbolFinder`-
  Predicate Z. 46).
- **Rückgabewert ist genau richtig:** nur Pfade, keine Textstellen.
  Der Hint braucht keine Zeilennummern, keine Inhalte — der Agent
  soll **wissen, dass es woanders Treffer gibt**, und dann selbst
  mit `search_pattern` weitersuchen (siehe Konzept Z. 169-170).
- **Edge-Case: `isRegex: false` ist OK**, weil `find_symbol`-Pattern
  semantisch ein Substring ist (siehe `FindSymbolTool.cs:46` —
  `name => name.Contains(namePattern, OrdinalIgnoreCase)`). Wenn
  `find_symbol` jemals Regex-Syntax zuließe, müsste man hier
  nachsteuern — gehört in den zukünftigen Schritt, nicht in 003.
- **Performance:** Miss-Hint-Pfad läuft nur bei 0 C#-Treffern
  (Edge-Case), nicht im Hot-Path. Scan-Aufwand bei
  SymbolGraphMini-Fixture (~7 Dateien + 5 Web-Dateien) ist < 50 ms.
  Bei Last-Fixture-Größe (500/5000 Dateien) wäre der zweite
  Scan redundant zur C#-Symbol-Suche — vertretbar für 003 (Miss-
  Hint ist nicht Hot-Path), in EPIC-08-Last-Fixture-Messung zu
  beobachten.

**Entscheidung im Plan:** API **unverändert** übernehmen, kein
Signatur-Touch.

### Check 3 — `initialize`-`ServerInstructions`-Feld im SDK

**Befund (verifiziert per Reflection-Probe gegen
`ModelContextProtocol.dll` 2.0.0 am 2026-08-01):**

```
McpServerOptions:
  Implementation         ServerInfo
  ServerCapabilities     Capabilities
  String                 ProtocolVersion
  TimeSpan               InitializationTimeout
  String                 ServerInstructions  ← verfügbar
  Boolean                ScopeRequests
  Implementation         KnownClientInfo
  ClientCapabilities     KnownClientCapabilities
  McpServerHandlers      Handlers
  McpServerFilters       Filters
  McpServerPrimitiveCollection`1  ToolCollection
  McpServerResourceCollection   ResourceCollection
  McpServerPrimitiveCollection`1  PromptCollection
  Int32                  MaxSamplingOutputTokens
  IList`1                RequestHandlers
```

- Property heißt `ServerInstructions` (nicht `Instructions` wie im
  Konzept Z. 161-164 verkürzt) — vom SDK-Setter als
  `String`-Property freigegeben. Wird vom Server in der
  `initialize`-Antwort als `instructions`-Feld im Server-Info-
  Block durchgereicht (semantisch identisch zu dem, was das
  Konzept meint — Konzept-Wortlaut ist verkürzt, der Coder nutzt
  den exakten SDK-Namen `ServerInstructions`).
- **Aktuell nicht gesetzt** — `McpServerOptionsFactory.cs:25-36`
  belegt nur `ServerInfo` (Name + Version) und `ToolCollection`.
  Die `ServerInstructions`-Property bleibt auf `null`/`default`.

**Entscheidung im Plan:** `ServerInstructions` als
`private const string` in `McpServerOptionsFactory.cs` definieren
und in `McpServerOptions`-Initializer zuweisen. Wortlaut:
zentraler Hint, der die C#-only-Grenze **einmal** benennt und
gleichzeitig auf `search_pattern` als Fallback verweist — exakter
Text in Schritt 3.

### Check 4 — Footprint-Situation (TD-011 Pflicht-Check)

**Vorab gemessen** (`--footprint <Klasse> --path .` heute
12:34-12:35, Stand `bd9e6fd` + Working-Tree clean):

| Klasse | transitive Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindSymbolTool` | **2518** | 2700 (PathOverride) | 182 |
| `SymbolGraphToolRegistrations` | **2487** | 2500 | 13 |
| `McpServerOptionsFactory` | **2470** | 2500 | 30 |
| `SearchPatternTool` (ref) | 2485 | 2500 | 15 |

(volle Tabelle im Anhang.)

**TD-011-Trigger-Bewertung:**

- 003 ändert `SymbolGraphToolRegistrations` (Description-Erweiterung
  für `find_symbol` um den Miss-Hint-Hinweis) → Registrar-Footprint
  steigt um ~1-2 Z. Wrap-Zuwachs. Aktueller Puffer 13 Z. ist
  ausreichend, aber **knapp**.
- 003 ändert `FindSymbolTool` selbst (Miss-Hint-Codeblock ~6-12 Z.,
  je nach Aufbau) → Tool-Footprint steigt ebenfalls. PathOverride
  2700 hat 182 Z. Puffer, das reicht locker.
- 003 ändert `McpServerOptionsFactory` (`ServerInstructions` const
  + Setter-Zeile) → +2-4 Z. auf 2470, Puffer bleibt >25 Z.

**Entscheidung im Plan:**

- **Beschreibungserweiterung in `find_symbol` minimal halten:**
  genau **ein zusätzlicher Satz** ("Bei 0 Treffern wird auf
  Textvorkommen in Nicht-C#-Dateien hingewiesen."), kein Ausschmücken.
  Schätzwert: +1-2 Z. auf 2487 → 2488-2489, Puffer 11-12 Z. nach
  003. Bleibt unter 2500, kein 5. Registrar nötig.
- **Pflicht-Re-Messung nach 003:** Coder misst `FindSymbolTool`,
  `SymbolGraphToolRegistrations` und `McpServerOptionsFactory` nach
  den Edits und dokumentiert die Zahlen im `result.md` (analog
  002-Pflicht-Messung). Falls eine Klasse über ihr Limit reißt:
  Coder entscheidet zwischen (a) kosmetischer Description-Kürzung
  in `find_symbol`, (b) PathOverride-Erhöhung **nur wenn wirklich
  nötig** (TD-008-Präzedenz, aber explizit als Anti-Muster
  dokumentiert), oder (c) `FindSymbolTool`-Aufteilung in
  `FindSymbolTool` + `FindSymbolScanner` (TD-005-Muster, hier
  gerechtfertigt weil es eh gerade angerührt wird).
- **TD-008-Verschärfung bewusst NICHT in 003:** Die
  `Config`-Property-Erweiterung an `McpCodeGraphServer` (ein
  zukünftiger P0/P1-Schritt aus `konzept.md` Z. 257-264 für
  `rules.json`-Auto-Discovery) ist außerhalb 003; **TD-008 wird
  nicht durch 003 verschärft**.

### Check 5 — Tests-Fixture-Erweiterung

**Befund:** `tests/Fixtures/SymbolGraphMini/` enthält aktuell in
`src/SymbolGraphMini/wwwroot/`:

- `site.js` (3 Z.): `console.log("SymbolGraphMini fixture: site.js");`
- `Component.razor` (3 Z.): `<h3>SymbolGraphMini fixture component</h3>`
- `index.html`, `Page.xaml`, `styles.css` — analog minimal

**Aktueller Stand der C#-Symbole in der Fixture** (laut
`Caller.cs`/`Greeter.cs`/`Hierarchy.cs`/`OtherCaller.cs`/
`ViolationTrigger.cs`): `Greeter`, `GreeterFactory`,
`HierarchyNode`, `HierarchyRoot`, `Caller`, `OtherCaller`,
`ViolationTrigger`, `IHierarchyNode` — diese dürfen **nicht** als
Miss-Hint-Test-Name gewählt werden.

**Entscheidung im Plan (Empfehlung — Coder darf abweichen, wenn
Begründung):**

- **Erweiterung in `site.js`** (eine Datei, eine Zeile): Funktions-
  Definition `function userService() { return "ok"; }` hinzufügen.
  Begründung: `userService` kollidiert nach menschlichem
  Sprachgefühl mit `.cs`-Klassennamen-Pattern (z. B. `UserService`-
  Klassen), was den realen Anwendungsfall (Agent sucht einen
  Service-Namen und findet ihn in JS statt in C#) **exakt**
  abbildet. **Alternative**, falls Coder minimaler will: nur
  `var userServiceMarker = 1;` (eine Zeile, gleicher Effekt). Coder
  entscheidet, dokumentiert in `result.md`.
- **Eindeutigkeits-Check vor Commit:** Coder verifiziert per
  `rg "userService" tests/Fixtures/SymbolGraphMini/` dass der Name
  in **keiner** `.cs`-Datei der Fixture vorkommt (sonst Test
  wirkungslos — A3-Fehlschlag-Nachweis funktioniert nicht). Wenn
  der Name doch in einer `.cs`-Datei auftaucht, anderen Namen
  wählen (z. B. `notificationWidget`, `pageEventHandler`).
- **Fixture-Datei wird modifiziert, nicht neu angelegt** — der
  Datei-Scan in `SymbolGraphMiniFixtureWorkspace.cs:33-45`
  kopiert alle bestehenden Dateien 1:1, eine neue Datei würde
  ohne Scan-Code-Änderung **nicht** in der Test-Fixture landen.
  Daher: bestehende Datei erweitern, nicht neue anlegen.
- **Diese Fixture-Änderung ist Teil des Code-Commits** (A1: ein
  Coder, ein Commit) — sie ist kein "Doku-Touch", sondern
  Test-Voraussetzung.

## Betroffene Dateien / Module

### Neu zu erstellen

| Datei | Zweck | Geschätzte Größe |
|---|---|---|
| (keine) | — | — |

003 braucht **keine** neue Datei. Die Erweiterung lebt in
bestehenden Dateien — bewusst minimal-invasiv.

### Zu modifizieren

| Datei | Änderung |
|---|---|
| `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | `FindMatchesAsync` Z. 51-55 erweitern: bei `filtered.Count == 0` `SearchPatternScanner.GetFilesWithHits(solution, namePattern, isRegex: false)` aufrufen, bei nicht-leerer Liste Hint-Zeile(n) anhängen. `using AiNetLinter.Mcp.Tools;` (für `SearchPatternScanner`) ist **bereits** im Namespace — `SearchPatternScanner` lebt in `AiNetLinter.Mcp.Tools` (`SearchPatternScanner.cs:11`), also reicht ein zusätzlicher `using AiNetLinter.Mcp.Tools;` ist nicht nötig (gleicher Namespace). |
| `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | Description von `find_symbol` (Z. 31-32) minimal erweitern: +1 Satz "Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien hingewiesen." Andere Tools unverändert. |
| `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | `private const string ServerInstructions = "..."` + `ServerInstructions = ServerInstructions` im `McpServerOptions`-Initializer. |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | +3 neue Tests (siehe unten) + 1 modifizierter Test (Fixture-Wechsel). |
| `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/site.js` | +1 Zeile mit `userService` (oder Wahl des Coders, Eindeutigkeits-Check dokumentiert). |
| `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` | +1 neuer Test `McpServerOptions_HasServerInstructions_WithScopeHint` (siehe unten). Bestehende Tests unverändert. |

**Nicht modifiziert** (bewusst, gegen Drift-Anfälligkeit):

- `src/AiNetLinter/Mcp/Tools/SearchPatternScanner.cs` — `GetFilesWithHits`-
  API passt, kein Touch.
- `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` —
  Trunkierungs-Einbau in andere Tools ist Folge-Einheit, nicht 003.
- `src/AiNetLinter/Mcp/McpTruncation.cs` — nicht beteiligt.
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` — keine neue
  Dependency nötig, TD-009-Risiko (Konstruktor 5/5) bleibt stabil.
- `rules.json` — keine PathOverride-Änderung erwartet (siehe
  Check 4).
- `konzept.md`, `tech-debt.md`, `state.md`, Projektregeln, `Docs/**` —
  A7 (nur lesen).

## Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 0 — Fixture-Erweiterung `site.js`

Datei `tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/site.js`
am Ende (oder als eigene Zeile) ergänzen um **eine** Zeile mit
einem Identifier, der in **keiner** `.cs`-Datei der Fixture
vorkommt. Empfehlung:

```javascript
function userService() { return "ok"; }
```

**Pflicht-Verifikation vor Schritt 1:**

```bash
rg "userService" tests/Fixtures/SymbolGraphMini/ --type cs
```

Darf **nichts** liefern. Falls doch: anderen Namen wählen
(`notificationWidget`, `pageEventHandler`, `e2eMarker` — Coder
entscheidet). Verifikation im `result.md` wortwörtlich
dokumentieren (A3-Voraussetzung: Test muss nachweislich fehlschlagen
können, das geht nur wenn `userService` wirklich nicht in `.cs`
existiert).

### Schritt 1 — `FindSymbolTool.cs` Miss-Hint-Erweiterung

In `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` den
`FindMatchesAsync`-Body (Z. 41-60) so erweitern, dass bei
`filtered.Count == 0` (Z. 51) **vor** der `return`-Zeile Z. 54
ein Miss-Hint angehängt wird. Konkret:

```csharp
if (filtered.Count == 0)
{
    var kindSuffix = kind is null ? "" : $" (Kind-Filter: {kind})";
    var baseText = $"Keine Treffer fuer '{namePattern}'{kindSuffix}";
    var missHits = SearchPatternScanner.GetFilesWithHits(
        solution, namePattern, isRegex: false);
    if (missHits.Count == 0)
    {
        return baseText;
    }
    // Miss-Hint: nennt nur die Dateipfade, keine Inhalte (Konzept Z. 169-174).
    // Forward-Slash-Pfade sind konsistent mit SearchPatternScanner.
    var fileList = string.Join(", ", missHits);
    return $"{baseText}\nHinweis: kein C#-Symbol, aber Textfund in {fileList} " +
        $"(nicht Teil des Symbolgraphs — fuer Inhalte search_pattern nutzen).";
}
```

**Constraints:**

- **Reihenfolge-Edge-Case:** wenn `namePattern` leer ist (was
  `find_symbol` aktuell nicht aktiv filtert — `name.Contains("")`
  matcht alles), würde `GetFilesWithHits` für `""` potentiell alle
  Dateien liefern. **Bewusst NICHT** in 003 abgesichert — wenn
  `find_symbol` mit `""` aufgerufen wird, ist das eh ein
  Agenten-Missbrauch, der in EPIC-06 (Robustheit) oder einer
  Input-Validierungs-Einheit (analog `search_pattern` EmptyPattern-
  Fix) zu adressieren ist. TD-005-Muster-Konsistenz: gleiche
  Argumentation wie 002 für `search_pattern` Empty-Pattern.
- **Plain-Text-Format:** keine JSON-Strukturierung, keine
  Trunkierung (max 5-10 Dateien in der Praxis, also kein Hot-Path-
  Risiko). Konsistent mit dem bestehenden `find_symbol`-Output-
  Format (Plain-Text, `\n`-separiert).
- **Keine `McpCodeGraphServer`-Zusatzabhängigkeit:** Der Scanner-
  Aufruf nimmt `solution` (aus dem `FindMatchesAsync`-Parameter)
  entgegen, nicht den Server. `FindSymbolTool` bleibt ein
  statisches Tool mit `McpCodeGraphServer` nur im `ExecuteAsync`-
  Pfad (Z. 27-35).
- **Namespace:** `SearchPatternScanner` lebt in
  `AiNetLinter.Mcp.Tools` (`SearchPatternScanner.cs:11`), `FindSymbolTool`
  ebenfalls (`FindSymbolTool.cs:14`). Kein zusätzlicher `using`
  nötig.
- **Method-Line-Count:** `FindMatchesAsync` wächst von ~20 Z. auf
  ~30 Z. — Puffer zu `MaxMethodLineCount: 60` weiterhin groß.
- **Zusätzliche Cyclomatic-Komplexität:** +2 (das `if (missHits.
  Count == 0)` und die zusätzliche Stringverkettung) — unter
  `MaxCyclomaticComplexity: 12`.

### Schritt 2 — `SymbolGraphToolRegistrations.cs` Description-Erweiterung

In `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` Z. 31-32
(`find_symbol`-Block) die Description minimal erweitern. **Genau
ein zusätzlicher Satz**, kein Ausschmücken:

```csharp
Description = "Sucht C#-Symbole (Klassen, Methoden, Properties, Interfaces) per " +
    "Substring im Namen. Deckt nur .cs-Dateien ab, keine .js/.razor/.xaml/.html/.css-Dateien. " +
    "Bei 0 Treffern wird auf Textvorkommen in Nicht-C#-Dateien hingewiesen.",
```

**Constraints:**

- Andere Tools (`find_references`, `get_impact`, `get_type_hierarchy`)
  **unverändert** — ihre DoD-Description ist bereits explizit, kein
  003-Scope.
- **Wortlaut-Kopplung** zum Hint in Schritt 1: Der Description-Satz
  nennt das Verhalten abstrakt ("wird auf Textvorkommen
  hingewiesen"), der Hint in Schritt 1 nennt es konkret
  ("Hinweis: kein C#-Symbol, aber Textfund in …"). Beide
  Wortlaute sind **bewusst unterschiedlich** — die Description
  ist Agenten-Marketing (kurz, was passiert), der Hint ist
  Tool-Output (vollständig, wie es aussieht). Test darf **nicht**
  den Description-Wortlaut prüfen (über das MCP-Protocol als
  Wire-Format umständlich), nur den Hint-Wortlaut.

### Schritt 3 — `McpServerOptionsFactory.cs` `ServerInstructions`

In `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` zwei
Änderungen:

**(3a)** Neue `private const string` direkt unter
`private const string ServerName` (Z. 18):

```csharp
private const string ServerName = "ainetlinter";

// Zentraler Scope-Hint fuer den initialize-Handshake (EPIC-05 / 003).
// Wird via ModelContextProtocol-SDK-Property McpServerOptions.ServerInstructions
// an den Server-Info-Block der initialize-Antwort durchgereicht. Nennt die
// C#-only-Grenze einmal server-weit, damit der Agent sie nicht pro Tool-
// Description zusammensuchen muss. Verweist auf search_pattern als Fallback
// fuer Namen in Nicht-C#-Dateien (.js, .razor, .xaml, .html, .css).
private const string ServerInstructions =
    "Symbolgraph-Tools (find_symbol, find_references, get_impact, get_type_hierarchy, " +
    "get_file_skeleton, get_violations) arbeiten ausschliesslich auf C#/.cs-Quellcode. " +
    "Fuer Namen, die nur in .js, .razor, .cshtml, .xaml, .html oder .css vorkommen, " +
    "ist search_pattern der passende Fallback. Struktur-Tools ohne C#-Beschraenkung: " +
    "get_index_scope, get_hotspots.";
```

**(3b)** Im `McpServerOptions`-Initializer (Z. 27-35) `ServerInstructions`
zuweisen:

```csharp
return new McpServerOptions
{
    ServerInfo = new Implementation
    {
        Name = ServerName,
        Version = GetServerVersion(),
    },
    ServerInstructions = ServerInstructions,
    ToolCollection = BuildToolCollection(mcpState),
};
```

**Constraints:**

- **Wortlaut** ist die **kanonische Formulierung** der
  Scope-Grenze. Wenn der Coder sie ändern will, MUSS er das
  begründen und im `result.md` dokumentieren — der Wortlaut ist
  der zentrale Vertragspunkt mit dem Agenten und sollte nicht
  pro Einheit anders klingen.
- **Plain-Text, deutsche Umlaute als ue/oe/ae** — konsistent mit
  dem bestehenden Code-Stil (siehe `McpServerOptionsFactory.cs:18`
  `ainetlinter` ohne Umlaut, aber die Tool-Descriptions in
  `SymbolGraphToolRegistrations.cs:31-67` nutzen deutsche Texte
  mit Umlaut-Ersetzung).
- **Tool-Liste explizit:** die Aufzählung nennt **alle 6**
  C#-only-Tools (`find_symbol`, `find_references`, `get_impact`,
  `get_type_hierarchy`, `get_file_skeleton`, `get_violations`) +
  `search_pattern` als Fallback + `get_index_scope` +
  `get_hotspots` als gegenbeispielliche Tools ohne C#-Beschränkung.
  Diese Vollständigkeit ist genau der zentrale Vorteil ggü. der
  Tool-Description (jede einzeln).
- **Kein Bezug zu internen Klassen/Symbolen** — der Hint landet
  im Wire-Format und ist für externe Agenten lesbar.

### Schritt 4 — `McpServerOptionsFactory`-Footprint nach 003 messen

Nach Schritt 3 misst der Coder die Footprints neu:

```bash
dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
dotnet run --project src/AiNetLinter -- --footprint FindSymbolTool --path .
```

Erwartung (geschätzt, Vor-Planung-Werte + ~2-4 Z.):

- `McpServerOptionsFactory`: 2470 → 2472-2474 (Puffer 26-28) ✓
- `SymbolGraphToolRegistrations`: 2487 → 2488-2489 (Puffer 11-12) ✓
- `FindSymbolTool`: 2518 → 2524-2530 (PathOverride 2700, Puffer 170-176) ✓

Falls eine Klasse reißt: siehe Check 4 — Coder entscheidet
zwischen kosmetischer Kürzung, PathOverride oder Scanner-Split,
dokumentiert im `result.md`.

### Schritt 5 — Tests in `FindSymbolToolTests.cs`

Vier Änderungen in
`src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs`:

**(5a)** Bestehender Test `FindMatchesAsync_NoMatch_ReturnsNoResultsText`
(Z. 49-58): **Fixture-Wechsel** von `BaselineMiniFixtureWorkspace` zu
`SymbolGraphMiniFixtureWorkspace`. Grund: der bestehende Test
verwendet eine reine C#-Fixture, in der `DoesNotExistXyz` (zufällig
gewählter Nicht-Name) garantiert nirgendwo existiert. Mit
SymbolGraphMini kann der Test auch ohne Miss-Hint grün bleiben
(wenn `DoesNotExistXyz` zufällig in `userService`+XYZ kollidiert,
was nicht der Fall ist — aber zur **Sicherheit** explizit
SymbolGraphMini wählen, damit der Test auch ohne Miss-Hint-Code
definitiv grün ist). **Achtung:** der Test ist ein
**Regressions-Test** für den **Fall-back-Pfad** (kein Non-C#-Hit).
Sein Passen nach 003 ist genau der Beweis, dass die Erweiterung
additiv wirkt.

**(5b)** Neuer Test `FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList`:

```csharp
[Fact]
public async Task FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList()
{
    using var fixture = new SymbolGraphMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);

    var result = await FindSymbolTool.FindMatchesAsync(
        catalog.Solution, "userService", kind: null, CancellationToken.None);

    // C#-Leermenge-Bestaetigung.
    Assert.Contains("Keine Treffer fuer 'userService'", result);
    // Miss-Hint-Markierung.
    Assert.Contains("Hinweis: kein C#-Symbol, aber Textfund", result);
    // Pfad-Liste enthaelt die Fixture-Datei.
    Assert.Contains("site.js", result);
    // Fallback-Verweis: search_pattern ist der naechste Schritt.
    Assert.Contains("search_pattern", result);
}
```

**A3-Fehlschlag-Nachweis (PFLICHT, A3):**

1. Erstlauf mit geändertem `FindSymbolTool.cs` + neuem Test:
   **grün** (Fix-Code + Test passen zusammen).
2. **A3-Auslöser:** `FindSymbolTool.cs` Z. 51-62 (Miss-Hint-
   Block) **auskommentieren** oder die `if (missHits.Count == 0)`
   return-Zeile durch `return baseText;` ersetzen — semantisch:
   kein Miss-Hint mehr.
3. **A3-Lauf:** nur dieser Test → **rot** mit Failure-Meldung
   `Not found: "Hinweis: kein C#-Symbol, aber Textfund"`.
4. **A3-Rückgängig:** Fix wieder einkommentieren.
5. **Volllauf:** 1097+x/1097+x grün.
6. Build: 0 Warnungen, 0 Fehler.

**(5c)** Neuer Test
`FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText`:
regression-für-den-Backward-Pfad. Name "DoesNotExistXyzBlub123"
(nicht in `.cs`, nicht in `.js`/`.razor`/etc. der Fixture).
Erwartung: `Assert.Contains("Keine Treffer fuer '…'")` ✓,
`Assert.DoesNotContain("Hinweis: kein C#-Symbol", …)`. A3-Methodik
analog (Test rot wenn Hint-Code fälschlich **immer** feuert, auch
bei leerer Non-C#-Liste).

**(5d)** Neuer Test
`FindMatchesAsync_KindFilterMissHit_StillFires`:
derselbe Name (`userService`), aber mit `kind: "class"`. Erwartung:
Miss-Hint feuert trotzdem (Kind-Filter ändert nichts an der
Non-C#-Suche). A3-Methodik: A3-Lauf mit auskommentiertem
Miss-Hint → rot, mit Fix → grün. Schärft ab, dass der Hint-Pfad
**nicht** durch den `FilterByKind`-Schritt bedingt ist.

### Schritt 6 — Test in `McpServerCommandTests.cs` für `ServerInstructions`

**(6a)** Neuer Test
`McpServerOptionsFactory_Create_ServerInstructionsContainsScopeHint`
in `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs`:

```csharp
[Fact]
public void McpServerOptionsFactory_Create_ServerInstructionsContainsScopeHint()
{
    var state = new McpCodeGraphServer(null);
    var options = McpServerOptionsFactory.Create(state);

    Assert.False(string.IsNullOrEmpty(options.ServerInstructions));
    Assert.Contains(".cs", options.ServerInstructions);
    Assert.Contains("search_pattern", options.ServerInstructions);
    Assert.Contains(".js", options.ServerInstructions);
    Assert.Contains(".xaml", options.ServerInstructions);
}
```

`McpServerOptionsFactory` ist `internal static` — für die
Test-Projekt-Sichtbarkeit sorgt die bestehende
`[assembly: InternalsVisibleTo("AiNetLinter.Tests")]`-Direktive
(`src/AiNetLinter/Core/LinterEngine.cs:18`).

**A3-Fehlschlag-Nachweis:**

1. Erstlauf mit `McpServerOptionsFactory.Create` + `ServerInstructions`-
   Zuweisung + Test: **grün**.
2. **A3-Auslöser:** in `McpServerOptionsFactory.cs` die Zeile
   `ServerInstructions = ServerInstructions,` (oder den ganzen
   const-Block) **entfernen**.
3. **A3-Lauf:** Test rot — `Assert.False(string.IsNullOrEmpty(...))`
   failt (ServerInstructions ist null/leer).
4. **A3-Rückgängig:** Zeile wieder hinzufügen.
5. Volllauf grün, Build sauber.

### Schritt 7 — Build und Tests

```bash
dotnet build AiNetLinter.slnx
dotnet test AiNetLinter.slnx --no-build
```

Erwartung:

- Build: 0 Warnungen, 0 Fehler (analog 002-Result).
- Tests: 1097 + 4 neue = 1101/1101 grün, 0 übersprungen, 0
  fehlgeschlagen.
- Falls ein Test rot: **nicht** den Test abschwächen, sondern
  den Fix-Code in `FindSymbolTool.cs`/`McpServerOptionsFactory.cs`
  korrigieren (Konzept-Regel "Symptom-Fixing verboten", siehe
  `AGENTS.md` Z. 35-37).

### Schritt 8 — Dogfooding gegen die eigene `AiNetLinter.slnx`

Analog Konzept Z. 193-204 / 625-627 ("Dogfooding pro Tool-Step
gegen die eigene `AiNetLinter.slnx`"): manueller ad-hoc-Lauf
von `find_symbol` mit einem Namen, der nur in einer
Nicht-`.cs`-Datei vorkommt — z. B. **die Datei `README.md`
enthält keinen Roslyn-Symbol-Treffer, aber enthält erwähnte
Namen wie `McpServerOptionsFactory` (in C# vorhanden — also
kein guter Kandidat)**. Bessere Wahl: ein Name, der im
`.editorconfig` oder in `Docs/**/*.md` vorkommt aber nicht in
`.cs` — z. B. einen Methodennamen, der nur in Doku-Beispielen
auftaucht, oder ein JS-Name in einer eventuellen
`wwwroot/*.js`-Datei der `AiNetLinter.slnx`. Wenn die
AiNetLinter.slnx **keine** Nicht-C#-Dateien mit
symbol-artigen Namen hat, ist das ehrlich im `result.md` zu
vermerken und der Schritt als "nicht anwendbar" zu markieren
(kein Pseudo-Test). **Mindestens aber:** manueller
Server-Start (`ainetlinter --mcp-server --path <AiNetLinter.slnx>`)
+ `initialize`-Aufruf + manuelles Inspizieren der
`ServerInstructions`-Antwort + ein `find_symbol`-Call gegen
einen garantiert nicht existierenden Namen (z. B.
`DoesNotExistXyzAinetlinter`) mit Bestätigung der
"Keine Treffer"-Antwort ohne Hint-Zeile (da in
`AiNetLinter.slnx` keine Nicht-C#-Dateien mit dem Namen).

### Schritt 9 — Conventional Commit

```bash
git add src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs \
        src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs \
        src/AiNetLinter/Mcp/McpServerOptionsFactory.cs \
        src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs \
        src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs \
        tests/Fixtures/SymbolGraphMini/src/SymbolGraphMini/wwwroot/site.js
git commit -m "feat(mcp): find_symbol miss-hint + initialize instructions [codegraph-mcp-server]"
```

**Constraints:**

- **Gezielter `git add`** (A4) — **kein** `-A`/`.`, **kein** Push.
- Conventional-Format: `feat(mcp):` (neue Funktionalität, kein
  Bugfix), deutscher Imperativ, Task-Suffix
  `[codegraph-mcp-server]` analog 002 (Commit `28e6e58`).
- **Reihenfolge der Dateien im Commit:** unwichtig, aber
  `site.js` (Fixture) als Letzte oder in einer separaten Zeile,
  damit `git show --stat` sie klar von Produktiv-Code trennt.

## Erwartete Tests (mit A3-Methodik pro neuem Test)

| # | Test | Pfad | A3-Schritte |
|---|---|---|---|
| 1 | `FindMatchesAsync_NoMatch_ReturnsNoResultsText` (modifiziert) | `FindSymbolToolTests.cs` Z. 49-58 | Fixture-Wechsel zu `SymbolGraphMini`. Regressions-Test: bleibt grün, wenn Hint-Pfad korrekt nur bei Non-C#-Hits feuert. A3 implizit (Test bestand schon vor 003). |
| 2 | `FindMatchesAsync_NoCsMatchButNonCsHit_ReturnsMissHintWithFileList` | `FindSymbolToolTests.cs` neu | A3: Erstlauf grün → Miss-Hint-Block in `FindSymbolTool.cs` auskommentieren → rot (`Not found: "Hinweis: kein C#-Symbol"`) → rückgängig → grün. |
| 3 | `FindMatchesAsync_NoCsMatchAndNoNonCsHit_ReturnsPlainNoMatchText` | `FindSymbolToolTests.cs` neu | A3: Test grün mit Fix → Hinweis-Zeile fälschlich **immer** anhängen (z. B. das `if (missHits.Count == 0)` entfernen) → rot (`Unexpected: "Hinweis: kein C#-Symbol"`) → rückgängig → grün. |
| 4 | `FindMatchesAsync_KindFilterMissHit_StillFires` | `FindSymbolToolTests.cs` neu | A3: Test grün → Hint-Block entfernen → rot → rückgängig → grün. |
| 5 | `McpServerOptionsFactory_Create_ServerInstructionsContainsScopeHint` | `McpServerCommandTests.cs` neu | A3: Test grün → `ServerInstructions = ServerInstructions,` Zeile entfernen → rot (`Assert.False(string.IsNullOrEmpty…)` failt) → rückgängig → grün. |

**5 Tests** in 003 (1 modifiziert + 4 neu). Vor 003: 1097 Tests,
nach 003: **1101** Tests, alle grün.

## Footprint-Messung (TD-011 Pflicht)

**Vorab (heute 12:34-12:35, Stand `bd9e6fd`):**

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `FindSymbolTool` | 2518 | 2700 (PathOverride) | 182 |
| `SymbolGraphToolRegistrations` | 2487 | 2500 | 13 |
| `McpServerOptionsFactory` | 2470 | 2500 | 30 |
| `SearchPatternTool` (ref) | 2485 | 2500 | 15 |

**Erwartung nach 003** (geschätzt):

| Klasse | Z. (geschätzt) | Limit | Puffer |
|---|---:|---:|---:|
| `FindSymbolTool` | 2524-2530 | 2700 | 170-176 |
| `SymbolGraphToolRegistrations` | 2488-2489 | 2500 | 11-12 |
| `McpServerOptionsFactory` | 2472-2474 | 2500 | 26-28 |

**Pflicht-Re-Messung im `result.md`** durch den Coder. Falls ein
Wert über dem Limit landet (unwahrscheinlich): siehe Check 4.

**Volle Tabelle** aller 12 Tool-Klassen plus Scanner im Anhang
"Footprint-Tabelle" unten, wie in 002-Pflicht.

## Bezug zu Projektregeln (Datei + Kurzgrund pro Datei)

| Regel | Berührung in 003 | Kurzgrund |
|---|---|---|
| `AiNetLinter.mdc` (C#-Codequalität) | `EnforceNullableEnable`, `MaxLineCount` ≤ 500, `MaxMethodLineCount` ≤ 60, `EnforceSealedClasses`, `MaxMethodParameterCount` ≤ 4, `MaxCyclomaticComplexity` ≤ 12, `EnforceNamespaceDirectoryMapping` | Standard-Check; `FindSymbolTool` und `McpServerOptionsFactory` wachsen moderat, bleiben unter allen Limits. Footprint-Limit ist die einzige substantive Sorge (TD-011). |
| `AiNetLinterRichtlinien.mdc` §1 (Einfachheit vor Abstraktion) | Keine neue Datei, kein DI-Container, keine Helper-Klasse. | Bewusst minimal-invasiv: 003 erweitert **3** bestehende Klassen + 1 Test-Datei, schafft **0** neue Strukturen. |
| `AiNetLinterRichtlinien.mdc` §2 (Kein DI) | `McpServerOptionsFactory.Create(state)` weiterhin direkter Methodenaufruf, kein DI-Container. | Unverändert. |
| `AiNetLinterRichtlinien.mdc` §5 (Result-Pattern) | Miss-Hint-Pfad in `FindSymbolTool.FindMatchesAsync` ist reine String-Formatierung, keine Exception. `SearchPatternScanner.GetFilesWithHits` würde bei ungültiger Regex `ArgumentException` werfen — aber 003 ruft nur mit `isRegex: false` auf, der Pfad ist nicht betroffen. | Result-Pattern bleibt sauber. |
| `AiNetLinterRichtlinien.mdc` §5 (Zero-Warning-Direktive) | Build wird mit `TreatWarningsAsErrors=true` laufen, daher 0 Warnungen Pflicht. | Standard. |
| `AiNetLinterRichtlinien.mdc` §3 (Konventionelle Commits) | Commit-Format `feat(mcp):` mit Task-Suffix. | Siehe Schritt 9. |

**Nicht angetastete Regeln** (kein 003-Berührungspunkt):
`AiNetLinterRichtlinien.mdc` §4, §6, §7.

## Annahmen und offene Fragen, die der Coder klären soll

### Annahmen (vom Planer getroffen, im `result.md` zu bestätigen)

- **A1:** Der Name `userService` (oder Coder-Wahl) kommt in
  **keiner** `.cs`-Datei der SymbolGraphMini-Fixture vor. Verifiziert
  per `rg "userService" tests/Fixtures/SymbolGraphMini/ --type cs`
  vor Schritt 1.
- **A2:** Die `ServerInstructions`-Property wird vom
  `ModelContextProtocol`-SDK 2.0.0 tatsächlich an die
  `initialize`-Antwort durchgereicht. Verifiziert per
  Reflection-Probe (Planer hat's getan: Property existiert als
  `String get; set;`). **Coder verifiziert zusätzlich** durch
  Dogfooding-Schritt 8 (manueller `initialize`-Aufruf).
- **A3:** `McpServerOptionsFactory` ist via
  `[assembly: InternalsVisibleTo("AiNetLinter.Tests")]`
  (`src/AiNetLinter/Core/LinterEngine.cs:18`) für die Tests
  sichtbar. **Verifiziert** durch Existenz des Tests in Schritt 6.
- **A4:** `SymbolGraphMiniFixtureWorkspace` kopiert **alle**
  Dateien aus `tests/Fixtures/SymbolGraphMini/` — die
  `site.js`-Änderung in Schritt 0 wird also vom Test gesehen.
  **Verifiziert** durch `SymbolGraphMiniFixtureWorkspace.cs:33-45`
  (`EnumerateFiles(sourceRoot, "*", AllDirectories)`).

### Offene Fragen (Coder klärt, dokumentiert im `result.md`)

- **F1:** Welcher konkrete Nicht-C#-Identifier-Namen wird in
  `site.js` (oder Coder-Wahl) eingefügt? Empfehlung `userService`,
  aber Coder darf abweichen (mit Begründung). **Pflicht:**
  Eindeutigkeits-Check per `rg` (siehe Schritt 0) dokumentiert.
- **F2:** Falls die Footprint-Re-Messung eine Klasse über das
  Limit treibt: welche der drei Strategien
  (kosmetische Description-Kürzung / PathOverride / Scanner-Split)
  wählt der Coder? **Empfehlung** Planer: kosmetische
  Description-Kürzung (geringster Risiko, kein
  Anti-Pattern-Folge-Effekt). **Dokumentationspflicht im
  `result.md`**, welche Strategie mit welcher Begründung.
- **F3:** Falls der Coder die `ServerInstructions`-Wortlaut-
  Formulierung aus Schritt 3 ändern will: warum? Coder kann vom
  Wortlaut abweichen, **muss** aber die Tool-Liste vollständig
  halten (alle 6 C#-only + Fallback + 2 gegenbeispielliche).

## Harte Scope-Grenze (was NICHT in 003 ist)

- **Keine** Trunkierungs-Erweiterung in `find_symbol`/
  `find_references`/`get_impact` (`McpTruncation`-Einbau) — das
  ist 004+ (siehe `units/002/fix-01/review.md` Z. 213-217).
- **Keine** Erweiterung an `find_references`/`get_impact`/
  `get_type_hierarchy` Descriptions — bereits explizit, kein
  003-Scope.
- **Keine** Miss-Hint-Erweiterung in `find_references`/
  `get_impact` — diese arbeiten auf bereits-gefundenen Symbolen,
  Miss-Hint konzeptuell nicht passend (`konzept.md` Z. 167-174
  nennt nur `find_symbol`).
- **Keine** `SearchPatternTool`-Änderungen (Trunkierung schon
  in 002 angeschlossen).
- **Kein** Eingriff in `McpCodeGraphServer` (TD-009-Konstruktor
  bleibt bei 5/5, keine neue Dependency).
- **Keine** `McpTruncation`-Änderungen.
- **Keine** `rules.json`-Änderungen (kein neuer PathOverride
  erwartet; siehe Check 4).
- **Keine** EPIC-06-Robustheit, EPIC-07-Tests-Ausbau,
  EPIC-08-Doku — separate Einheiten.
- **Keine** P0/P1-Erweiterungen über 003-Scope hinaus (kein
  `--mcp-log`, kein "lädt noch"-Zustand, keine
  `rules.json`-Auto-Discovery, keine Staleness-Sweep-mtime-
  Optimierung, keine Verzeichnis-Sweep für neue/gelöschte
  `.cs`-Dateien — alle aus `konzept.md` Z. 207-324 separat).
- **Keine** `konzept.md`-, `tech-debt.md`-, `state.md`-, Projektregeln-
  Edits (A7).

## Anhang — Footprint-Tabelle aller Tool-Klassen (vor 003)

| Klasse | Z. | Limit | Puffer |
|---|---:|---:|---:|
| `McpServerOptionsFactory` | 2470 | 2500 | 30 |
| `McpCodeGraphServer` | 2416 | 2500 | 84 |
| `McpToolResults` | 107 | 2500 | — |
| `McpTruncation` | ~40 | 2500 | — |
| `SymbolGraphToolRegistrations` | 2487 | 2500 | **13** |
| `FileStructureToolRegistrations` | 2480 | 2500 | 20 |
| `AnalysisToolRegistrations` | ~2474 (geschätzt nach 002 + `search_pattern`) | 2500 | ~26 |
| `FindSymbolTool` | 2518 | 2700 (PathOverride) | 182 |
| `FindReferencesTool` | 2519 | 2700 (PathOverride) | 181 |
| `GetImpactTool` | ~2440 | 2500 | ~60 |
| `GetTypeHierarchyTool` | ~2440 | 2500 | ~60 |
| `GetFileSkeletonTool` | ~2440 | 2500 | ~60 |
| `GetIndexScopeTool` | 2445 | 2500 | 55 |
| `GetHotspotsTool` | 2447 | 2500 | 53 |
| `GetViolationsTool` | 2451 | 2500 | 49 |
| `SearchPatternTool` | 2485 | 2500 | 15 |
| `FindSymbolScanner` | n/a (existiert nicht) | — | — |
| `SearchPatternScanner` | ~177 (geschätzt nach 002) | 2500 | — |
| `GetViolationsScanner` | 1834 | 2500 | — |
| `GetIndexScopeScanner` | ~120 | 2500 | — |
| `GetHotspotsScanner` | ~120 | 2500 | — |
| `GetFileSkeletonScanner` | ~120 | 2500 | — |
| `GetTypeHierarchyScanner` | ~120 | 2500 | — |
| `GetImpactScanner` | ~120 | 2500 | — |

(geschatzte Werte vor 003-Commit, exakt im `result.md` zu messen.)
