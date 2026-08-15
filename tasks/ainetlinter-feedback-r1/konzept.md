---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-15
open_questions: []
---

# Konzept: AiNetLinter-Feedback Runde 1 — vier Lücken/Verbesserungen aus `dry-refactor`

## Ziel (Was)

Vier während des `dry-refactor`-Tasks am AiNetLinter MCP-Server und an den
Linter-Regeln beobachtete Lücken werden generisch (nicht projektspezifisch)
geschlossen:

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

Ergebnis: weniger False-Positives für Test- und Schema-Code, klarere
MCP-Antworten beim Duplicate-Check. Der Linter bleibt ein allgemeines Tool
— keine SqlToAi-Spezifika landen im Code.

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

## Wie (grober Ansatz)

Die Umsetzung wird in **vier logisch trennbaren Steps** (einer pro
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
- **FB-01** (1–2 Commits): Heuristik im
  `AIContextFootprintCalculator.QueueMemberSymbols` einbauen — Idee:
  Member ohne `Body`/`ExpressionBody` und ohne nicht-Attribute-Dekoration
  werden markiert oder beim Aufsummieren mit reduzierter Gewichtung
  gezählt. Konkretisierung mit Verweis auf existierende
  `AIContextFootprintDeduplicationTests` (für Regressions-Schutz).
- **FB-04** (1–2 Commits): `scopeType`-Parameter in
  `DuplicateDetectionInput`/`DuplicateDetectionScanner`, plus
  Summary-Header in `DuplicateDetectionTool.BuildResponse` (oder
  einem dedizierten Response-Builder). JSON-Schema + FastTests +
  Integration-Tests anpassen.

Reihenfolge-Vorschlag: **FB-02 → FB-03 → FB-04 → FB-01** (klein nach
groß). Alle vier Commits auf Deutsch, Conventional-Commits-Stil, autonom
in den Loop gepusht — keine erzwungenen Pausen dazwischen, sofern die
Tests grün bleiben.

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
- **Tests (alle müssen grün sein):**
  - `dotnet build` (mit `TreatWarningsAsErrors = true`).
  - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
    — neuer MiddleMan-Test, neue PublicMembers-Tests
    (Skip + Opt-in), neue AIContextFootprint-Tests (Heuristik),
    neue DuplicateDetection-Tests (`scopeType` + Summary).
  - `dotnet test src/AiNetLinter.IntegrationTests --filter
    Category!=Stress` — keine Regression in bestehenden
    End-to-End-Szenarien.
- **Doku & Konfig-Sync:**
  - `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`
    ausgeführt und committed.
  - `Docs/configuration.md` aktualisiert (neue Property, neuer
    `scopeType`-Parameter, ggf. Heuristik-Hinweis).
  - `Docs/ROADMAP.md` aktualisiert, falls einer der vier Punkte dort
    als Meilenstein geführt wird (prüft der Planer).
- **Commits:** Conventional Commits auf Deutsch, imperativ. Pro
  Muss-Haven-Punkt mindestens ein Commit, autonom in den
  `drift-loop` eingespielt. Push erfolgt durch den Loop bzw. durch den
  Nutzer am Ende der Runde.
- **Keine projektspezifischen Hardcodings:** Kein `JsonSerializerContext`,
  keine `SqlCharScanner`-Sonderlogik, keine `Mcp*`-Spezialfälle
  außerhalb der `find_duplicates`-Tool-Logik.

## Offene Punkte

*Leer — alle vier FB-Punkte sind oben in Muss-Haben verankert, der Nutzer
hat den Empfehlungen aus dem Beurteilungs-Turn zugestimmt.*
