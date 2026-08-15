---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-15
open_questions: []
---

# Konzept: AiNetLinter-Feedback Runde 1 — sechs Lücken/Verbesserungen aus `dry-refactor`

## Ziel (Was)

Sechs während des `dry-refactor`-Tasks am AiNetLinter MCP-Server und an den
Linter-Regeln beobachtete Lücken werden generisch (nicht projektspezifisch)
geschlossen — vier Lücken-Reparaturen (FB-01..FB-04) plus zwei
MCP-UX-Erweiterungen (A, B):

1. **`AvoidExcessiveMiddleMen`** meldet in xUnit-Testklassen fälschlich
   „100 % Middle Man", weil Einzeiler-Wrapper wie
   `Assert.True(GlobMatcher.IsMatch(...))` als reine Weiterleitung zählen —
   die Regel ist für Produktions-Architektur gedacht, nicht für Test-Klassen.
2. **`MaxPublicMembersPerType`** (`≤ 15` Default) wird von xUnit-Testklassen
   zwangsläufig überschritten, weil `[Fact]`/`[Theory]`-Methoden per
   Reflection entdeckt werden müssen und damit `public` sind — die Regel ist
   für öffentliche Produktions-APIs gedacht, nicht für Test-Container.
3. **`AIContextFootprint`** zählt transitiv referenzierte Zeilen naiv über
   alle Typen gleich — rein deklarative Typen (nur Attribute, keine echte
   Logik) blähen den Footprint strukturell auf, ohne dass ihre Zeilen für
   einen Agenten „Kontext" im eigentlichen Sinn sind.
4. **`find_duplicates`** liefert bei großen Resultsätzen eine ungekürzte
   Liste ohne Vorspann und kennt keine Trennung zwischen Test- und
   Produktions-Code — beides behindert den Agent-Loop, weil jede Antwort
   erst langwierig sortiert werden muss.
5. **Neues MCP-Tool `get_class_structure`**: kompakte Klassen-Übersicht
   mit Member-Liste und Zeilenbereichen — Antwort auf den
   `view_file`-mit-120-Zeilen-Workaround, den Agenten derzeit fahren,
   um Klassen-Splits vorzubereiten. Existierende Tools
   (`get_symbol_body` für Einzelsymbole, `get_file_skeleton` für ganze
   Dateien) decken diese Lücke nicht.
6. **Code-Snippet direkt in `get_violations`**: 1–2 Kontextzeilen plus
   die verletzende Zeile als Snippet in der Violation-Antwort. Spart
   die anschließende `view_file`-Runde für Einzeiler-Fixes (z. B.
   `sealed` ergänzen, Parameter entfernen).

Ergebnis: weniger False-Positives für Test- und Schema-Code, klarere
MCP-Antworten beim Duplicate-Check, kompaktere Antworten beim
Klassen-Refactoring und bei Violation-Triage. Der Linter bleibt ein
allgemeines Tool — keine SqlToAi-Spezifika landen im Code.

## Warum / Kontext

Während des `dry-refactor`-Tasks am AiNetLinter sind FB-01..FB-04 als
Beobachtungen aufgelaufen (siehe Feedback-Dokument im Task-Verlauf). Drei
davon (FB-02, FB-03, FB-01 im Kern) sind **echte Lücken, die jeder
AiNetLinter-Anwender mit xUnit-Tests bzw. mit schema-/attributlastigen
Typen** trifft — der konkrete Trigger im Feedback-Dokument war jeweils nur
ein Beispiel. Der vierte Punkt (FB-04) ist eine generelle
MCP-UX-Verbesserung, die unabhängig vom konkreten Projekt sinnvoll ist.

**Wichtige Constraint:** Der AiNetLinter ist ein **allgemeines Tool**, kein
projektspezifischer Linter. Die im Feedback-Dokument vorgeschlagene
Sonderbehandlung `JsonSerializerContext` lehnen wir daher ab — die richtige
Abstraktionsebene ist „declaration-only types" als generische Klasse
(Heuristik + bestehende `FootprintIgnore*`-Konfiguration als Fallback).

**Bezug zu bestehender Mechanik:** Für Test-Skips existiert im Linter
bereits ein etabliertes Pattern: `ctx.IsTestFile` wird in 9 anderen
Checkern als Skip-Bedingung geprüft (`BlockingTaskChecker`,
`ComplexityChecker`, `ControlFlowChecker`, `ImmutabilityChecker`,
`MinimalApiChecker`, `NamingChecker`, `PhantomDependencyChecker`,
`ScopeChecker`, `WpfSeparationChecker`). `MiddleManChecker` und
`PublicMembersChecker` haben diesen Skip schlicht nicht — die Lücke ist
eine Inkonsistenz, kein Feature-Wunsch.

## Scope

### Muss-Haben

Reihenfolge-Vorschlag: **FB-02 → FB-03 → FB-04 → B → A → FB-01** (klein
+ Tool-nah → groß + Architektur-nah). Details pro Punkt unten; Planer
kann die Reihenfolge anpassen, wenn ein späterer Schritt frühere
Ergebnisse voraussetzt.

- **FB-02: `AvoidExcessiveMiddleMen` für Testfiles überspringen.**
  - Datei: `src/AiNetLinter/Core/Checkers/MiddleManChecker.cs`, Methode
    `ShouldSkipClass` (Zeile ~49-68).
  - Ergänzung: `if (ctx.IsTestFile) return true;` als erste Bedingung
    nach dem Config-Flag-Check.
  - Keine neue Konfiguration nötig — Tests sind per Definition keine
    Middle-Man-Klassen im architektonischen Sinn.
- **FB-03: `MaxPublicMembersPerType` für Testfiles standardmäßig
  überspringen, mit Opt-in via neuer Konfigurations-Flag.**
  - Datei: `src/AiNetLinter/Core/Checkers/PublicMembersChecker.cs`,
    Methode `Check` (Zeile ~13-15).
  - Ergänzung: `if (ctx.IsTestFile && !ctx.Config.Metrics.MaxPublicMembersPerTypeApplyToTestFiles) return;`
  - Neue Konfigurations-Property in `src/AiNetLinter/Configuration/MetricsConfig.cs`:
    `MaxPublicMembersPerTypeApplyToTestFiles: bool = false` (Default =
    Testfiles werden übersprungen, konsistent mit `NamingChecker` und
    `ImmutabilityChecker`).
  - Eintrag in `rules.json` + `tests/Fixtures/BaselineMini/rules.json`
    für die Konfiguration.
- **FB-04: `find_duplicates` UX.**
  - Datei: `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionTool.cs`
    + `DuplicateDetectionScanner.cs`.
  - (a) Bei Resultsatz > N (z. B. > 20) eine Kurzzusammenfassung mit
    Top-Clustern + Dateipfaden voranstellen, statt nur die volle Liste
    auszugeben.
  - (b) Neuen optionalen Parameter `scopeType: "all" | "production" |
    "tests"` (Default `"all"`). Backend: `IsTestFile` aus dem
    `CheckerContext` wird über `SolutionFileWalker` / `DocumentContext`
    propagiert (existiert bereits für `LinterAnalyzer` —
    `src/AiNetLinter/Core/DocumentContext.cs:14`).
  - JSON-Schema (`McpJsonOptions` o. ä.) und Tests entsprechend
    erweitern.
- **B: Code-Snippet direkt in `get_violations`.**
  - Datei: `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` +
    `src/AiNetLinter/Output/ViolationMarkdownFormatter.cs`.
  - Neues Feld `snippet` (Liste von Source-Zeilen) im strukturierten
    Violation-JSON; Markdown-Output bekommt unter jeder Violation einen
    Snippet-Block (Code-Fence, mit Pfad:Zeile-Header).
  - **Edge-Cases:**
    - `contextLines` als Tool-Parameter (Default 2, max 5) — Snippet
      zeigt `N` Zeilen davor, die verletzende Zeile, `N` Zeilen danach.
    - Datei-Anfang/-Ende respektieren: weniger Zeilen zurückgeben, wenn
      der Kontext über den Datei-Rand hinausläuft (kein Wrap-Around,
      kein Phantom-Padding).
    - Cluster-Violations ohne spezifische Zeile (z. B.
      `EnableDuplicateCodeCheck`-Cluster, `MaxAIContextFootprint` auf
      Typ-Ebene, `Safeguard`-Score): **kein Snippet**, stattdessen
      Cluster-Summary oder Begründungstext. Snippet nur, wenn
      `ViolationDescription.SourceSpan` vorhanden.
    - Mehrere Violations auf der gleichen Zeile: Snippet wird pro
      Violation wiederholt (kein Caching auf Datei-Ebene, um
      Eindeutigkeit zu wahren — Token-Kosten sind überschaubar: 5
      Zeilen × ~80 Zeichen ≈ 400 Bytes je Violation).
    - Sehr lange Source-Zeilen (> 200 Zeichen): auf 200 Zeichen
      kürzen + `…` Suffix, damit der Output nicht durch Auto-Format-
      Zeilen gesprengt wird.
    - Opt-out via `includeSnippet: bool = true`, falls ein Aufrufer
      nur die Metrik-Liste will (z. B. ein Bulk-Triage-Skript).
  - Pattern-Reuse: bestehendes `maxResults`-Argument und das
    `McpTruncation`-Helper-Modul (`src/AiNetLinter/Mcp/McpTruncation.cs`)
    für die Truncation-Meta-Zeile wiederverwenden.
- **A: Neues MCP-Tool `get_class_structure`.**
  - Datei: `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`
    (neu) + Registrierung in `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs`
    + Tool-Schema in `src/AiNetLinter/Mcp/McpJsonOptions.cs`.
  - Output: tabellarische Member-Liste mit Zeilenbereichen, analog zum
    User-Wunsch:
    ```text
    SchemaServiceTests:
    - L20-L45: ListDatabasesAsync_ShouldReturnAllowedDatabases (Fact, 25 Zeilen)
    - L47-L80: SearchDatabasesAsync_ShouldReturnMatchingDatabases (Fact, 33 Zeilen)
    ```
  - Parameter:
    - `className` (Pflicht): FQN (`Namespace.ClassName`) oder relativer
      Name (wenn eindeutig).
    - `maxMembers` (Default 50, max 200): begrenzt die Member-Liste
      konsistent mit `McpTruncation`-Mechanik. Bei Überschreitung
      Truncation-Meta-Zeile mit „weitere N Member" Hinweis.
    - `includeAttributes` (Default `false`): opt-in für
      Attribut-Listen pro Member (kostet Token, nicht jeder Agent
      braucht das).
  - **Edge-Cases:**
    - Klasse nicht gefunden → `ClassNotFound` (neuer Error-Code
      parallel zu `FileNotFound`).
    - Mehrdeutigkeit (gleicher Klassen-Name in verschiedenen
      Namespaces) → Antwort listet alle Treffer mit FQN, fordert
      Aufrufer zur Disambiguierung auf. Konsistent mit
      `FindSymbolTool`/`McpServerCommandFindSymbolTests`-Patterns.
    - `partial class` über mehrere Dateien → Tool gibt pro Part
      einen getrennten Eintrag mit `file:line`-Quelle des jeweiligen
      Members. Falls Parts in sehr vielen Dateien (> 5), Hinweis im
      Output („diese Klasse ist über N Dateien verteilt — Split in
      Erwägung ziehen").
    - `record` mit Primary Constructor → Parameter des Primary
      Constructors als eigene Zeile vor den restlichen Membern.
    - `struct`, `enum`, `record struct`, `interface`: alle vier
      unterstützen. Für `enum` sind „Member" die Werte; für
      `interface` nur die Signaturen.
    - Nested types: werden mit aufgelistet, mit
      `OuterClass.NestedClass`-FQN.
    - Sehr große Klassen (> 700 Zeilen, knapp unter `MaxLineCount`):
      Tool funktioniert, aber der Member-Scan läuft über Roslyn-Syntax
      (kein Regex), Performance ist kein Risiko.
  - Output-Format: Markdown (analog `get_file_skeleton`) plus
    `StructuredContent` (analog `get_violations` in Zeile 68 von
    `GetViolationsTool.cs`) für Tool-zu-Tool-Aufrufe.
- **FB-01: Heuristik für „declaration-only types" im
  `AIContextFootprint`.**
  - Datei: `src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs`,
    Methode `QueueMemberSymbols` (Zeile ~138-152) bzw.
    `QueueMethodSymbols` (Zeile ~154-161).
  - Kernidee: Typen, deren Member ausschließlich Attribute oder leere
    Bodies sind, werden in der transitiven Zählung markiert oder
    ausgeschlossen. Konkretisierung im Planer-Schritt.
  - Falls die Heuristik nicht greift, sind die bereits existierenden
    `MetricsConfig.FootprintIgnoreNamespacePrefixes` und
    `FootprintIgnoreTypeNames` die expliziten Fallback-Knöpfe
    (`src/AiNetLinter/Configuration/MetricsConfig.cs:101-112`).
  - **Edge-Cases:**
    - Klassen mit gemischtem Inhalt (z. B. drei Attribute + ein
      echter Konstruktor): Heuristik nur anwenden, wenn **alle**
      Member declaration-only sind. Andernfalls reguläre Zählung.
    - Partielle Klassen: jede Partial-Datei wird eigenständig
      bewertet (Members summieren sich nicht über Dateien, das wäre
      eine Verhaltens-Änderung).
    - Generische Constraints (`where T : new()` etc.) sind kein
      „Logik-Indikator" — sie bleiben für die Zählung irrelevant
      (betrifft ohnehin nur die generische Argument-Auflösung).

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

*Leer — alle vier Punkte sind oben als Muss-Haben verankert.*

### Non-Goals (bewusst NICHT Teil davon)

- **Spezialfall `JsonSerializerContext`** in `AIContextFootprint` —
  projektspezifischer Trigger, nicht generalisierbar. Die
  Heuristik „declaration-only types" deckt denselben Fall zusammen mit
  AutoMapper-`Profile`, EF-`IEntityTypeConfiguration<>`, Swagger-Schemas
  und ähnlichem mit ab.
- **„Höheres Limit für Testfiles"** als Alternative zu Skip in FB-03 —
  Skip ist sauberer, konsistent mit `NamingChecker`/`ImmutabilityChecker`,
  und verhindert, dass das Limit 15 in User-Köpfen als „weich" wahrgenommen
  wird. Opt-in via Flag bleibt möglich.
- **`scopeType`-Filter für andere MCP-Tools** (z. B. `pattern_detect`,
  `get_violations`) — FB-04 betrifft nur `find_duplicates`, dort ist der
  Bedarf im `dry-refactor` konkret aufgetreten. Andere Tools können
  später nachgezogen werden, wenn derselbe Bedarf empirisch entsteht.
- **Eigene Regelvariante `AvoidExcessiveMiddleMenInTests`** — Overkill,
  der Pattern-Reuse via `IsTestFile`-Skip ist etabliert und kürzer.
- **Snippets für Cluster-Violations** in B (z. B. `EnableDuplicateCodeCheck`,
  `MaxAIContextFootprint`-Überschreitungen) — Cluster haben keine
  einzelne verletzende Zeile. Ein Snippet wäre irreführend. Stattdessen
  Cluster-Summary bzw. Begründungstext (im Konzept bereits als
  Edge-Case dokumentiert).
- **Body-Snippets in `get_class_structure` (A)** — Tool liefert nur
  Zeilenbereiche und Signaturen, keine Methoden-Bodies. Wer Bodies
  braucht, nutzt `get_symbol_body` (einzelnes Symbol) oder
  `get_file_skeleton` (ganze Datei). Drei-Tool-Komposition statt
  Mega-Tool, jeweils token-effizient.
- **Snippets in `find_duplicates` / `safeguard` (FB-04)** — die
  Top-Cluster-Liste in FB-04(a) ist eine Summary, kein Snippet-
  Feld pro Cluster. Doppelung mit B wäre redundant; B ist die
  Snippet-Quelle, FB-04 die Listen-Verdichtung.
- **Volltext-Snippet-Output (mehr als 5 Kontextzeilen) in B** — die
  Grenze 5 Kontextzeilen ist hart, nicht konfigurierbar nach oben.
  Wer mehr Kontext braucht, soll `get_symbol_body` oder `view_file`
  nachziehen. Verhindert, dass die „Snippet"-Funktion zu einem
  heimlichen Volltext-Dump wird.

## Zielplattformen / Technischer Rahmen

- **Stack:** .NET 9 / C# 13 (siehe `Docs/configuration.md`,
  `src/AiNetLinter/AiNetLinter.csproj`).
- **Lint-Engine-Architektur:** Checker-Klassen unter
  `src/AiNetLinter/Core/Checkers/`, Konfiguration via
  `Configuration/GlobalConfig.cs` + `Configuration/MetricsConfig.cs`,
  MCP-Tools unter `src/AiNetLinter/Mcp/Tools/`.
- **Test-Aufteilung:** `AiNetLinter.FastTests` (Unit/Component) +
  `AiNetLinter.IntegrationTests` (Integration/Dogfood/Performance).
  Stress-Tests bleiben explizit außen vor.
- **Konfigurations-Doppelpflege:** Neue Konfigurations-Properties müssen in
  `rules.json` (Repo-Root), `tests/Fixtures/BaselineMini/rules.json` und in
  `src/AiNetLinter/Configuration/ConfigOverrides.cs` (für Per-Project-
  Overrides) ergänzt werden.
- **Agent-Rules-Sync:** `dotnet run --project src/AiNetLinter --
  --sync-agent-rules-only` regeneriert `.agents/rules/AiNetLinter.mdc`
  aus `rules.json` — Pflichtschritt nach jeder Konfig-Änderung (siehe
  `AGENTS.md` §3).

## Verworfene Alternativen

- **„Nur die SqlToAi-spezifischen Trigger fixen"** (z. B. via
  `FootprintIgnoreTypeNames: ["JsonSerializerContext"]`): verworfen, weil
  der AiNetLinter ein allgemeines Tool ist und derselbe Trigger bei jedem
  anderen Anwender mit schema-lastigen Typen wieder auftritt.
- **„Eigene Regel `AvoidExcessiveMiddleMenInProduction`"**: verworfen,
  weil die existierende Regel bereits korrekt formuliert ist, nur der
  Skip-Pfad fehlt. Pattern-Reuse über `IsTestFile` ist kürzer und
  konsistenter.
- **„Test-Klassen per `MaxPublicMembersPerTypeInTestFiles: 50` lockern"**
  statt Skip: verworfen, weil Skip-Default (mit Opt-in) dem Stil der
  anderen Test-Skip-Checker entspricht und keinen „weichen Grenzwert"
  etabliert.
- **„`find_duplicates` in zwei separate Tools `find_duplicates_prod` /
  `find_duplicates_tests` aufspalten"**: verworfen, weil ein
  `scopeType`-Parameter dasselbe in einem Tool löst, weniger
  Tool-Registry-Lärm verursacht und konsistent mit anderen Parametern
  (z. B. `mode`, `minTokens`) ist.

## Wo im Projekt

- `src/AiNetLinter/Core/Checkers/MiddleManChecker.cs:13-68` —
  `Check` + `ShouldSkipClass`; betroffen für FB-02.
- `src/AiNetLinter/Core/Checkers/PublicMembersChecker.cs:13-44` —
  `Check`; betroffen für FB-03.
- `src/AiNetLinter/Configuration/MetricsConfig.cs:209-219` —
  `MaxPublicMembersPerType*` Properties; betroffen für FB-03.
- `src/AiNetLinter/Metrics/AIContextFootprintCalculator.cs:138-161` —
  `QueueMemberSymbols`/`QueueMethodSymbols`; betroffen für FB-01.
- `src/AIContextFootprint`-betroffene Aufrufer:
  - `src/AiNetLinter/Metrics/StateChecker.cs` und/oder
    `src/AiNetLinter/Core/Checkers/StateChecker.cs` (suche nach
    `AIContextFootprintCalculator.Calculate*`).
  - Linter-Regel-ID: `MaxAIContextFootprint` (siehe
    `src/AiNetLinter/Core/LinterRuleIds.cs`,
    `src/AiNetLinter/Core/RuleRegistry.cs`).
- `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionTool.cs` —
  Einstiegspunkt für `find_duplicates`; betroffen für FB-04.
- `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionScanner.cs` —
  Scan-Logik; Ort für `scopeType`-Filter und Summary-Header.
- `src/AiNetLinter/Mcp/McpJsonOptions.cs` und ggf.
  `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionModels.cs` —
  Schema-Erweiterung für `scopeType`.
- `rules.json` (Repo-Root) +
  `tests/Fixtures/BaselineMini/rules.json` — Konfigurations-Default-
  Pflege für `MaxPublicMembersPerTypeApplyToTestFiles` und ggf.
  Fußnoten in `MetricsConfig`-Properties.
- `src/AiNetLinter/Configuration/ConfigOverrides.cs` — Per-Project-
  Override-Slots, falls die neue Property überschreibbar sein soll.
- `src/AiNetLinter/Docs/configuration.md` + `Docs/ROADMAP.md` — Doku
  nach Konfig-Änderung (siehe `AGENTS.md` §3).

## Entdeckte Mängel/Redundanzen

- **`IsTestFile`-Skip-Pattern bereits etabliert, aber unvollständig
  ausgerollt**
  - **Gefunden:** 9 Checker prüfen `ctx.IsTestFile` als Skip-Bedingung
    (`BlockingTaskChecker.cs:19,61`, `ComplexityChecker.cs:256`,
    `ControlFlowChecker.cs:16`, `ImmutabilityChecker.cs:16`,
    `MinimalApiChecker.cs:14`, `NamingChecker.cs:43,60,85`,
    `PhantomDependencyChecker.cs:15,35`, `ScopeChecker.cs:30,40`,
    `WpfSeparationChecker.cs:16`). `MiddleManChecker` und
    `PublicMembersChecker` sind die einzigen produktiven Checker mit
    klarer Test-Skip-Lücke.
  - **Bezug:** `AGENTS.md` §1 (Architektur-Konsistenz), implizit
    dokumentiert in `.agents/rules/AiNetLinterRichtlinien.mdc` (kein
    festgeschriebenes „alle Checker skippen Tests"-Gesetz, aber
    Konsistenz mit den 9 existierenden Skip-Stellen).
  - **Vorschlag:** Statt eine neue Skip-Mechanik zu erfinden, exakt
    das `if (ctx.IsTestFile) ... return;`-Pattern wiederverwenden.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben FB-02 +
    FB-03).
- **`FootprintIgnoreNamespacePrefixes` / `FootprintIgnoreTypeNames` in
  `MetricsConfig` sind der etablierte Erweiterungspunkt**
  - **Gefunden:** `src/AiNetLinter/Configuration/MetricsConfig.cs:101-112`
    listet genau diese beiden Konfigurations-Hebel mit
    Anwendungs-Beispielen (u. a. „Drittanbieter-Quellcode").
  - **Bezug:** `Docs/configuration.md` (Konfigurations-Doku).
  - **Vorschlag:** FB-01 primär als Heuristik (deckt 90 % der Fälle
    ohne Konfig-Aufwand). Die bestehenden Ignore-Properties bleiben
    der explizite Fallback für edge cases — keine Doppel-Mechanik
    einführen.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben FB-01,
    Heuristik zuerst).
- **`scopeType`-Parameter etabliert ein neues Filter-Konzept für
  MCP-Tools**
  - **Gefunden:** Andere MCP-Tools kennen vergleichbare Filter (z. B.
    `minTokens`, `maxResults`, `mode`, `similarityThreshold` in
    `DuplicateDetectionTool.cs:40-49`). Ein expliziter
    Production/Tests-Filter existiert nirgends.
  - **Bezug:** Keine festgeschriebene Regel, aber
    `Docs/agent-api.md` enthält die MCP-Tool-Referenz.
  - **Vorschlag:** Nur in `find_duplicates` einführen, nicht alle Tools
    gleichzeitig umstellen — siehe Non-Goals. Falls sich der Bedarf
    später verallgemeinert, kann das Pattern kopiert werden.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben FB-04).
- **`maxResults` + `McpTruncation`-Mechanik ist etabliertes Pattern für
  Output-Begrenzung in MCP-Tools**
  - **Gefunden:** `get_violations` (Default 50, `GetViolationsScanner.DefaultMaxResults`),
    `search_pattern` (Default 50), `pattern_detect` (Default 20),
    `find_magic_values` (Default 50), `find_duplicates` (Default 20) —
    alle nutzen `maxResults` als Parameter und klammern sich an
    `src/AiNetLinter/Mcp/McpTruncation.cs` (zentrale
    Truncation-Meta-Zeile).
  - **Bezug:** Konsistenz mit existierenden Tool-API-Konventionen;
    vermeidet Tool-Drift, bei dem jeder MCP-Call anders aussieht.
  - **Vorschlag:** Für A (`get_class_structure`) `maxMembers` (Default
    50) nach demselben Muster; für B (`get_violations`-Snippet) die
    Truncation-Logik aus `McpTruncation` wiederverwenden statt neue
    erfinden.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben A + B
    Edge-Cases).
- **Strukturierter Output (`StructuredContent`) ist etabliertes Pattern
  für Tool-zu-Tool-Aufrufe**
  - **Gefunden:** `GetViolationsTool.cs:68` liefert Text + strukturiertes
    `{ Violations = result.Violations! }`. Andere Tools
    (`find_magic_values`, `search_pattern`) folgen demselben Muster
    laut `IsErrorPolicy.md` und Tool-Tests.
  - **Bezug:** MCP-Schema-Konvention für `structuredContent`.
  - **Vorschlag:** `get_class_structure` (A) liefert sowohl Markdown
    (human-readable) als auch `StructuredContent` (machine-readable
    mit `{ ClassName, Members: [...] }`), damit andere Tools/Agents
    ohne Markdown-Parsing arbeiten können.
  - **Entscheidung:** übernommen ins Scope (→ siehe Muss-Haben A
    Output-Format-Spezifikation).

## Wie (grober Ansatz)

Die Umsetzung wird in **sechs logisch trennbaren Steps** (einer pro
Muss-Haven-Punkt) geplant — Details (Step-Reihenfolge, Commit-Schnitte)
verantwortet der Planer im `drift-loop`. Grob-Skizze je Punkt:

- **FB-02** (1 Commit): in `MiddleManChecker.ShouldSkipClass` nach der
  `AvoidExcessiveMiddleMen`-Flag-Prüfung ein
  `if (ctx.IsTestFile) return true;` einfügen. FastTests anpassen:
  ein neuer Test in `src/AiNetLinter.FastTests/Core/Checkers/MiddleManCheckerTests.cs`,
  der eine Test-Datei mit Forwardern synthetisiert und zeigt, dass keine
  Violation gemeldet wird.
- **FB-03** (1–2 Commits): in `PublicMembersChecker.Check` ganz am
  Anfang den `IsTestFile`-Skip mit Opt-in-Flag einfügen. Property in
  `MetricsConfig` ergänzen. Konfig in `rules.json` +
  `tests/Fixtures/BaselineMini/rules.json` ergänzen. FastTests in
  `MaxPublicMembersPerTypeTests.cs` anpassen. Agent-Rules-Sync
  ausführen. `Docs/configuration.md` aktualisieren.
- **FB-04** (1–2 Commits): `scopeType`-Parameter in
  `DuplicateDetectionInput`/`DuplicateDetectionScanner`, plus
  Summary-Header in `DuplicateDetectionTool.BuildResponse` (oder
  einem dedizierten Response-Builder). JSON-Schema + FastTests +
  Integration-Tests anpassen.
- **B** (1–2 Commits): Snippet-Implementierung in
  `GetViolationsScanner.cs` (Snippet-Resolution aus
  `SemanticModel.SyntaxTree.GetText().Lines`, gesteuert über
  `contextLines` + `includeSnippet`-Parameter aus dem Tool-Schema).
  Markdown-Format-Anpassung in `Output/ViolationMarkdownFormatter.cs`
  (Code-Fence-Block mit Pfad:Zeile-Header, max 200 Zeichen pro Zeile,
  Kontext vor/nach der verletzenden Zeile). Schema-Erweiterung in
  `McpJsonOptions.cs` + Tool-Registrierung in
  `AnalysisToolRegistrations.cs`. FastTests in
  `GetViolationsToolTests.cs`/`GetViolationsScannerTests.cs`
  (Edge-Cases: Datei-Anfang, mehrere Violations/Zeile, Cluster ohne
  Zeile). Integration-Tests in
  `IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs`.
- **A** (2–3 Commits): neues Tool
  `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs`.
  Scanner-Logik auf Roslyn-SyntaxTree (`SyntaxNode.DescendantNodes()`
  gefiltert auf `MemberDeclarationSyntax`-Kinder, mit
  `GetLocation().GetLineSpan()` für Zeilenbereiche). Output-Builder
  liefert Markdown + `StructuredContent` analog
  `GetViolationsTool.cs:68`. Tool-Registrierung in
  `FileStructureToolRegistrations.cs` (Lambda-Header mit
  `maxMembers = 50` Default + `includeAttributes = false` Default,
  konsistent mit `AnalysisToolRegistrations.cs:65`-Pattern).
  FastTests in `GetClassStructureToolTests.cs` (alle Edge-Cases:
  nicht gefunden, mehrdeutig, partial, record, struct, enum, nested,
  sehr große Klasse > 100 Member mit Truncation-Meta-Zeile).
  Integration-Tests für End-to-End-Aufruf.
- **FB-01** (1–2 Commits): Heuristik im
  `AIContextFootprintCalculator.QueueMemberSymbols` einbauen — Idee:
  Member ohne `Body`/`ExpressionBody` und ohne nicht-Attribute-Dekoration
  werden markiert oder beim Aufsummieren mit reduzierter Gewichtung
  gezählt. Konkretisierung mit Verweis auf existierende
  `AIContextFootprintDeduplicationTests` (für Regressions-Schchutz).

Reihenfolge-Vorschlag: **FB-02 → FB-03 → FB-04 → B → A → FB-01** (klein
+ Tool-nah → groß + Architektur-nah). Alle sechs Commits auf Deutsch,
Conventional-Commits-Stil, autonom in den Loop gepusht — keine
erzwungenen Pausen dazwischen, sofern die Tests grün bleiben.

**Token-Budget-Garantie:** kein Tool in dieser Runde darf eine
unbegrenzte Liste zurückgeben. `maxResults`/`maxMembers` ist Pflicht,
`McpTruncation`-Meta-Zeile ist Pflicht, Snippets sind pro Violation
gedeckelt (max 5 Kontextzeilen × 200 Zeichen/Zeile ≈ 1 KB/Violation).
Zielwert: eine typische `get_violations`-Antwort bleibt unter
~50 KB auch bei 50 Treffern.

## Definition of Done / Erfolgskriterien

- **Code:**
  - `MiddleManChecker`, `PublicMembersChecker`,
    `AIContextFootprintCalculator`, `DuplicateDetectionTool`/
    `DuplicateDetectionScanner` sind gemäß „Wie (grober Ansatz)" geändert.
  - `MetricsConfig.MaxPublicMembersPerTypeApplyToTestFiles` existiert
    mit Default `false` und ist in `ConfigOverrides` als Per-Project-
    Override-Slot spiegelbar.
  - `rules.json` und `tests/Fixtures/BaselineMini/rules.json` sind
    konsistent ergänzt.
  - Neues Tool `get_class_structure` mit Markdown-Output +
    `StructuredContent` ist implementiert, registriert und
    dokumentiert.
  - `get_violations` liefert Snippet-Feld pro Violation mit Zeile
    davor/danach (per `contextLines` konfigurierbar, Default 2).
- **Tests (alle müssen grün sein):**
  - `dotnet build` (mit `TreatWarningsAsErrors = true`).
  - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
    — neue MiddleMan-Tests, neue PublicMembers-Tests
    (Skip + Opt-in), neue AIContextFootprint-Tests (Heuristik),
    neue DuplicateDetection-Tests (`scopeType` + Summary), neue
    `get_class_structure`-Tests (alle Edge-Cases aus „Wie"), neue
    `get_violations`-Snippet-Tests (Datei-Anfang, mehrere
    Violations/Zeile, Cluster ohne Zeile, lange Zeilen, Opt-out).
  - `dotnet test src/AiNetLinter.IntegrationTests --filter
    Category!=Stress` — keine Regression in bestehenden
    End-to-End-Szenarien; `McpServerAllToolsE2ETests` und
    `McpServerCommandContractTests` müssen die neuen Tool-Signaturen
    und Schema-Änderungen mitmachen.
- **Doku & Konfig-Sync:**
  - `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
    ausgeführt und committed.
  - `Docs/configuration.md` aktualisiert (neue Property, neuer
    `scopeType`-Parameter, neue Tool-Beschreibung, ggf.
    Heuristik-Hinweis).
  - `Docs/agent-api.md` aktualisiert mit Signatur + Parametern von
    `get_class_structure` und dem neuen Snippet-Feld in
    `get_violations`.
  - `Docs/ROADMAP.md` aktualisiert, falls einer der sechs Punkte dort
    als Meilenstein geführt wird (prüft der Planer).
- **Token-Budget (harter Test):**
  - Smoke-Test-Skript im IntegrationTest-Setup (oder als
    `McpToolResults`-Test): ein Aufruf mit Worst-Case-Args
    (`maxResults = 100`, `contextLines = 5`, `maxMembers = 200`) darf
    die Antwort-Bytes nicht über ein definiertes Limit
    (~50 KB) treiben. Schwellwert-Verletzung ist Test-Fail.
- **Commits:** Conventional Commits auf Deutsch, imperativ. Pro
  Muss-Haven-Punkt mindestens ein Commit, autonom in den
  `drift-loop` eingespielt. Push erfolgt durch den Loop bzw. durch den
  Nutzer am Ende der Runde.
- **Keine projektspezifischen Hardcodings:** Kein `JsonSerializerContext`,
  keine `SqlCharScanner`-Sonderlogik, keine `Mcp*`-Spezialfälle
  außerhalb der `find_duplicates`-Tool-Logik. Snippet-Code und
  Class-Structure-Tool arbeiten rein über Roslyn-SyntaxTree, keine
  String-Magie.

## Offene Punkte

*Leer — alle vier FB-Punkte sind oben in Muss-Haben verankert, der Nutzer
hat den Empfehlungen aus dem Beurteilungs-Turn zugestimmt.*
