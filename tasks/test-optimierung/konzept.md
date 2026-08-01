---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: large
rules_dir: .agents/rules
last_updated: 2026-08-01
open_questions:
  - "#nullable enable in den 63 Dateien ohne Datei-Pragma: bei dieser Gelegenheit überall nachrüsten, oder nur in ohnehin bearbeiteten Dateien mitnehmen (Empfehlung: Randmitnahme, siehe Entdeckte Mängel)?"
  - "MaxDirectoryChildren-Regel projektweit aktivieren (aktuell global deaktiviert): eigene Grundsatzentscheidung außerhalb dieses Tasks, oder soll dieser Task das gleich mit anstoßen?"
---

# Konzept: Test-Infrastruktur-Refactoring (AiNetLinter.Tests)

## Ziel (Was)

Die Testsuite von AiNetLinter (`src/AiNetLinter.Tests`, 137 Dateien,
~1097 Tests) soll strukturell und infrastrukturell überarbeitet werden:
schnellere, gezielt filterbare Testläufe für den agentischen
Entwicklungszyklus (`dynamic-loop`), plus Behebung konkret gefundener
DRY- und Struktur-Mängel in der Testsuite selbst. Die fachliche
Testabsicht (was getestet wird, welche Assertions gelten) bleibt dabei
unverändert — es geht um Boilerplate, Organisation und Ausführung, nicht
um Testinhalte.

## Warum / Kontext

Ein Volllauf dauert auf schwächerer Hardware (Notebook) 7-8 Minuten. Der
`dynamic-loop`-Workflow (siehe
`.agents/Agent-Scaffolding/dev-loop/dynamic-loop/`) lässt den Coder pro
Einheit einen Volllauf fahren (belegt in `tasks/codegraph-mcp-server/
state.md`: "Volllauf 1097/1097 grün" nach jeder Einheit/Fix-Runde) — bei
einem Deckel von z. B. 40 Aufrufen und mehreren Fix-Runden macht das
ganze Aufgaben auf schwacher Hardware unpraktikabel langsam.

Ein Read-only-Audit (eigene Prüfung + Sub-Agent, siehe „Entdeckte
Mängel/Redundanzen") hat zusätzlich zur reinen Laufzeitfrage mehrere
Architektur-/DRY-Mängel in der Testsuite gefunden, die der Nutzer
ausdrücklich mit beheben lassen möchte („die tests sollen auch
ordentlich sein"). Größter konkreter Laufzeit-Hebel: an ~50+ Stellen
werden Fixture-Workspaces (`BaselineMini`/`SymbolGraphMini`/
`GitImpactMini`, inkl. `git init`/`commit`-Subprozessen bei GitImpact)
pro Testmethode neu instanziiert, obwohl `IClassFixture`/
`ICollectionFixture` im gesamten Projekt **nicht ein einziges Mal**
genutzt wird.

**Wichtiger Nebeneffekt, kein Bestandteil dieses Konzepts:** Der Nutzer
hat zusätzlich ein Problem im `dynamic-loop`-Coder beobachtet (Bash-
Tool-Truncation bei großem `dotnet test`-Output führt zu unnötigen
Zweit-Läufen ohne Filter). Das ist ein reiner Tooling-/Prompt-Fix in
`.agents/Agent-Scaffolding/dev-loop/dynamic-loop/agents/coder.md`
(fehlende Konvention: `--logger trx` + gedrosselte Konsole statt
Vollausgabe), kein AiNetLinter-Testcode-Thema — wird separat behandelt,
nicht in diesem Konzept.

## Scope

### Muss-Haben

1. **Fixture-Sharing** für `BaselineMiniFixtureWorkspace`,
   `SymbolGraphMiniFixtureWorkspace`, `GitImpactMiniFixtureWorkspace`
   via `IClassFixture`/`ICollectionFixture` statt Instanziierung pro
   Testmethode (größter Laufzeithebel).
2. **`[Trait("Category", "Unit"|"Integration")]`-Kategorisierung** aller
   Testklassen — Basis: die im Audit enumerierte Integration-Liste
   (Prozess-Spawns, echter MCP-Server-Roundtrip, Multi-Datei-I/O gegen
   kopierte Fixture-Bäume).
3. **`xunit.runner.json`** mit expliziter Parallelisierungs-Konfiguration
   (bisher: keine Datei vorhanden, xUnit-Defaults greifen unkonfiguriert).
4. **`ConsoleTestCollection`-Zwangsserialisierung eingrenzen**: von den
   22 betroffenen Klassen nur die zwangsserialisieren, die tatsächlich
   Console-I/O umleiten/prüfen.
5. **Gemeinsamer Temp-Dir-Helper** (`IDisposable`, auf
   `Directory.CreateTempSubdirectory` basierend) als Ersatz für die 33
   Stellen mit handgerolltem `Path.GetTempPath()` + manuellem
   Try/Finally.
6. **`ILintConsole`-Testdouble konsolidieren**: `Output/
   TestLintConsole.cs` und `Maps/TestLintConsole.cs` (gleicher Name,
   inkompatible API) auf eine gemeinsame Variante zusammenführen.
7. **Root-Testdateien einsortieren**: die 23 lose im
   `AiNetLinter.Tests`-Wurzelverzeichnis liegenden Testdateien in
   Namespace-Ordner verschieben, passend zur Produktionsstruktur
   (`Core/`, `Configuration/`, `Metrics/`, `Core/Checkers/` etc.).
8. **`Commands/McpServerCommandTests.cs` splitten** (513 Zeilen, einzige
   Datei über der 500-Zeilen-Praxisgrenze) entlang MCP-Tool-Grenzen.
9. **`dynamic-loop`-Scaffolding anpassen**
   (`agents/coder.md`/`agents/kritiker.md`): Iterationsrunden fahren nur
   noch die gefilterte `Category=Unit`-Teilmenge, Volllauf ausschließlich
   in Baseline (Phase 1) und Abschluss (Phase 3). Diese Einheit steht am
   Anfang der Umsetzung, damit der restliche Task selbst schon davon
   profitiert.

**Timing-Constraint (siehe „Verworfene Alternativen"):** Punkte 1
(soweit `Mcp/Tools/*` betroffen) und 8 berühren Dateien, an denen
`tasks/codegraph-mcp-server` (`dynamic-loop`) aktuell aktiv arbeitet.
Diese Teile werden geplant, aber **erst nach dessen Abschluss**
umgesetzt (siehe „Wo im Projekt").

### Nice-to-Have (optional, spätere Iteration)

- `Core/`-Ordner (37 Dateien, größter Flachordner) nach Regel-Kategorie
  sub-gliedern, analog zur Kategorisierung in `.agents/rules/
  AiNetLinter.mdc` (agent-resilience/architecture/general/
  test-coverage).
- Test-Data-Builder/Object-Mother für `Config`/`GlobalConfig`/
  `CheckerContext` statt ad-hoc-Konstruktion pro Test.
- Gemeinsamer `CliProcessRunner`-Helper für die verstreuten
  `Process.Start`/`ProcessStartInfo`-Stellen (`Baseline/*`, `Cli/*`,
  `Suppression/DisableAllCliTests.cs`, `Fixtures/
  GitImpactMiniFixtureWorkspace.cs`).
- `#nullable enable`-Datei-Pragma in den 63 Dateien nachrüsten, die es
  aktuell nicht haben (funktional irrelevant, da Projekt-weites
  `<Nullable>enable</Nullable>` ohnehin greift — reine Regelkonsistenz).

### Non-Goals (bewusst NICHT Teil davon)

- **Keine Änderung an Testinhalten/Assertions.** Reines Boilerplate-/
  Struktur-Refactoring — was getestet wird und mit welchem erwarteten
  Ergebnis bleibt exakt gleich.
- **Kein Test-Framework-Wechsel.** xUnit v3 bleibt, `IClassFixture`/
  `ICollectionFixture`/`xunit.runner.json` sind native xUnit-Mechanismen.
- **Keine neue Testabdeckung.** Kein Task zum Schließen von
  Coverage-Lücken — das ist ein anderes Thema als Infrastruktur.
- **Der `dotnet test`-Output-Truncation-Fix im Coder-Prompt** (siehe
  „Warum/Kontext") ist explizit **nicht** Teil dieses Konzepts.
- **Keine Änderung an `.agents/rules/*.mdc` oder `rules.json` selbst** —
  außer der offenen Frage zu `MaxDirectoryChildren` (siehe Frontmatter
  `open_questions`), die eine bewusste separate Entscheidung ist.

## Zielplattformen / Technischer Rahmen

.NET 10, xUnit v3 (`xunit.v3.core`/`xunit.v3.assert`, bereits im
Einsatz laut `src/AiNetLinter.Tests/AiNetLinter.Tests.csproj`) — keine
Stack-Änderung. Umsetzung ausschließlich mit Bordmitteln von xUnit v3
(`[Trait]`, `IClassFixture<T>`, `ICollectionFixture<T>`,
`xunit.runner.json`), keine zusätzlichen Testframework-Pakete.

## Verworfene Alternativen

- **Drei-stufige Kategorisierung (`Unit`/`Slow`/`Integration`)** statt
  zwei-stufig: verworfen, weil das Audit keine Fälle gefunden hat, die
  „langsam, aber nicht Integration" sind — jede identifizierte
  langsame Klasse spawnt entweder einen Prozess, einen echten
  MCP-Server oder kopiert Multi-Datei-Fixtures. Zusätzliche Stufe wäre
  Komplexität ohne erkennbaren Nutzen.
- **Sofortige Umsetzung parallel zum Hintergrund-Agenten**, nur
  `Mcp/**` aussparen: verworfen zugunsten von „Konzept jetzt, Umsetzung
  erst nach Abschluss von `tasks/codegraph-mcp-server`" — Nutzer-
  Entscheidung, geringeres Konfliktrisiko wichtiger als der zeitliche
  Vorsprung durch Parallelarbeit.
- **Komplette Neuschreibung/Testframework-Wechsel**: nie ernsthaft
  erwogen. Bestehende Konventionen (Methodennamen konsistent im Schema
  `Subject_Scenario_Expected`, sealed-Klassen bereits durchgängig
  freiwillig genutzt) sind bereits gut — Fokus liegt auf Infrastruktur
  und Organisation, nicht auf einem Neuaufbau.

## Wo im Projekt

- `src/AiNetLinter.Tests/` (Root, 23 lose Testdateien, z. B.
  `AutoFixerTests.cs`, `ArchitectureTests.cs`, `LinterEngineTests.cs`,
  `DiffImpactAnalyzerTests.cs`) — Kandidaten für Umzug in
  Namespace-Ordner passend zur Produktionsstruktur.
  `TestHelper.cs`/`ConsoleTestCollection.cs` bleiben dort (echte
  projektweite Infrastruktur, keine Testfälle).
- `src/AiNetLinter.Tests/Core/` (37 Dateien) — größter Flachordner,
  Kandidat für Sub-Gliederung (Nice-to-have).
- `src/AiNetLinter.Tests/Fixtures/{Baseline,SymbolGraph,GitImpact}
  MiniFixtureWorkspace.cs` — Ziel für Fixture-Sharing; enthalten
  jeweils fast identische `FindSolutionRoot()`/`CopyFixture()`/
  `IsGeneratedPath()`.
- `src/AiNetLinter.Tests/Output/TestLintConsole.cs` und
  `src/AiNetLinter.Tests/Maps/TestLintConsole.cs` — Konsolidierungsziel
  (Muss-Haben 6).
- `src/AiNetLinter.Tests/ConsoleTestCollection.cs` + die 22 Klassen mit
  `[Collection("ConsoleTestCollection")]` — Prüfziel für
  Serialisierungs-Eingrenzung.
- `src/AiNetLinter.Tests/Commands/McpServerCommandTests.cs` (513
  Zeilen) — Split-Ziel, **gesperrt bis `tasks/codegraph-mcp-server`
  abgeschlossen ist**.
- `src/AiNetLinter.Tests/Mcp/Tools/*.cs` (9 Dateien) und
  `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` —
  Fixture-Sharing-Ziel, **ebenfalls gesperrt bis
  `tasks/codegraph-mcp-server` abgeschlossen ist**.
- `src/AiNetLinter.Tests/AiNetLinter.Tests.csproj` — Ziel für
  `xunit.runner.json`-Einbindung.
- `.agents/Agent-Scaffolding/dev-loop/dynamic-loop/agents/coder.md`,
  `agents/kritiker.md` — Scaffolding-Anpassung für gefilterte
  Iterationsrunden (Muss-Haben 9).
- 33 Dateien mit handgerolltem Temp-Dir-Handling (u. a.
  `AutoFixerTests.cs`, `Baseline/BaselineCliTests.cs`,
  `Baseline/BaselineReaderWriterTests.cs`,
  `Cache/AnalysisCacheManagerTests.cs`,
  `Commands/McpServerCommandTests.cs`,
  `Commands/SyncAgentRulesCommandTests.cs`,
  `Configuration/ConfigSyncerTests.cs`, mehrere `Suppression/*`- und
  `Maps/*`-Dateien) — Ziel für Muss-Haben 5, vollständige Liste liegt
  im Audit-Ergebnis dieser Konzeptions-Session vor und wird vom Planer
  bei Bedarf neu erhoben (Pointer-Prinzip: Stand kann sich verschoben
  haben).

## Entdeckte Mängel/Redundanzen

- **`ILintConsole`-Doppelung**
  - **Gefunden:** `Output/TestLintConsole.cs` (List&lt;string&gt;-
    basiert) vs. `Maps/TestLintConsole.cs` (StringBuilder-basiert) —
    gleicher Klassenname, gleiche Schnittstelle, inkompatible API, in
    zwei verschiedenen Namespaces.
  - **Bezug:** kein einzelner `rules_dir`-Regelverstoß, aber
    offensichtliches, unstrittiges Duplikat.
  - **Vorschlag:** eine gemeinsame Variante, z. B. in einem eigenen
    Testdouble-Namespace.
  - **Entscheidung:** übernommen ins Scope (Muss-Haben 6).

- **Fixture-Workspace-Triplikation**
  - **Gefunden:** `Fixtures/BaselineMiniFixtureWorkspace.cs`,
    `SymbolGraphMiniFixtureWorkspace.cs`,
    `GitImpactMiniFixtureWorkspace.cs` mit nahezu identischen privaten
    `FindSolutionRoot()`/`CopyFixture()`/`IsGeneratedPath()`-Methoden.
  - **Bezug:** kein direkter `rules_dir`-Treffer, aber offensichtliche
    Code-Duplikation (~40 Zeilen je Datei quasi identisch).
  - **Vorschlag:** gemeinsame Basis/Helper extrahieren, im selben Zug
    auf `IClassFixture`/`ICollectionFixture` umstellen (siehe
    Muss-Haben 1).
  - **Entscheidung:** übernommen ins Scope (Muss-Haben 1).

- **Root-Testdateien ohne Namespace-Ordner**
  - **Gefunden:** 23 Dateien direkt in `src/AiNetLinter.Tests/` mit
    flachem Namespace `AiNetLinter.Tests`, während Geschwisterordner
    (`Cli/`, `Commands/`, `Core/`, `Configuration/`, `Mcp/Tools/` etc.)
    konsequent Unterordner mit passendem
    `AiNetLinter.Tests.<Ordner>`-Namespace nutzen. Stichprobe zeigt:
    die betroffenen Root-Dateien testen Produktionstypen aus
    `AiNetLinter.Core`, `AiNetLinter.Configuration`,
    `AiNetLinter.Metrics`, `AiNetLinter.Core.Checkers`.
  - **Bezug:** Geist von `EnforceNamespaceDirectoryMapping`
    (`.agents/rules/AiNetLinter.mdc`, Sektion „architecture") — auch
    wenn `*.Tests` laut `rules.json`-`ProjectOverrides` aktuell nicht
    von dieser Regel erfasst wird.
  - **Vorschlag:** Root-Dateien in passende Namespace-Ordner
    verschieben.
  - **Entscheidung:** übernommen ins Scope (Muss-Haben 7).

- **`#nullable enable`-Inkonsistenz**
  - **Gefunden:** 63 von 132 Testdateien haben keine
    Datei-Pragma-Zeile `#nullable enable`, verlassen sich auf das
    Projekt-weite `<Nullable>enable</Nullable>` in
    `AiNetLinter.Tests.csproj:6`.
  - **Bezug:** `EnforceNullableEnable`
    (`.agents/rules/AiNetLinter.mdc`, Sektion „general") verlangt die
    Datei-Pragma explizit.
  - **Vorschlag:** entweder nachrüsten oder bewusst als
    Projekt-Ausnahme akzeptieren.
  - **Entscheidung:** **offen** — siehe `open_questions` im
    Frontmatter, funktional irrelevant, daher keine dedizierte
    Flächenaktion vorgeschlagen, sondern Randmitnahme in ohnehin
    bearbeiteten Dateien als Default-Empfehlung.

- **`MaxDirectoryChildren` global deaktiviert, `Core/` am ehesten
  betroffen**
  - **Gefunden:** `rules.json` → `Metrics.MaxDirectoryChildren = 0`
    (deaktiviert); `Core/` mit 37 Dateien wäre der erste Ordner, der
    bei Aktivierung anschlägt.
  - **Bezug:** eigene, aktuell inaktive Projektregel.
  - **Vorschlag:** `Core/` nach Regel-Kategorie sub-gliedern (siehe
    Nice-to-Have) und danach optional die Regel aktivieren.
  - **Entscheidung:** **offen** — siehe `open_questions`, da
    Regel-Aktivierung eine projektweite Grundsatzentscheidung ist, die
    über den Scope dieses Tasks hinausgeht.

## Wie (grober Ansatz)

Empfohlene Reihenfolge (Detailplanung macht der Planer im gewählten
Loop):

1. **Scaffolding zuerst** (Muss-Haben 9): `dynamic-loop`-Coder/Kritiker
   auf gefilterte Iterationsläufe umstellen — diese Einheit steht am
   Anfang, damit der Rest des Tasks selbst schon von kürzeren
   Testläufen profitiert.
2. **Risikoarme, dateibewegungsfreie Hebel**: `xunit.runner.json`
   (Muss-Haben 3), `ConsoleTestCollection`-Eingrenzung (Muss-Haben 4),
   `[Trait]`-Kategorisierung (Muss-Haben 2) — keine strukturellen
   Verschiebungen, schnellste messbare Wirkung.
3. **Fixture-Sharing außerhalb `Mcp/**`** (Muss-Haben 1, Teilmenge):
   `BaselineMiniFixtureWorkspace`/`SymbolGraphMiniFixtureWorkspace`
   dort umstellen, wo sie nicht von `Mcp/Tools/*` verwendet werden.
4. **Temp-Dir-Helper extrahieren** (Muss-Haben 5) und schrittweise in
   den 33 identifizierten Dateien einsetzen.
5. **`TestLintConsole` konsolidieren** (Muss-Haben 6).
6. **Root-Dateien einsortieren** (Muss-Haben 7) — mechanisch, viele
   Dateien, aber geringes Risiko pro Datei.
7. **Nice-to-Haves**, falls nach den Muss-Haben noch Kapazität ist.
8. **Nach Abschluss von `tasks/codegraph-mcp-server`**: verbleibender
   Teil von Muss-Haben 1 (`Mcp/Tools/*`-Fixture-Sharing) + Muss-Haben 8
   (`McpServerCommandTests.cs`-Split) als Nachfolge-Einheiten.

Empfehlung zur Umsetzung: über `dynamic-loop`
(`.agents/Agent-Scaffolding/dev-loop/dynamic-loop/`), analog zu
`tasks/codegraph-mcp-server` — die Einheiten-Struktur mit
Aufrufe-Deckel passt gut zu vielen kleinen, mechanischen Schritten.
Finale Wahl (`dynamic-loop` vs. `drift-loop`) trifft der Nutzer beim
Start der Umsetzung.

## Definition of Done / Erfolgskriterien

- Alle bisherigen ~1097 Tests weiterhin grün, keine Assertion inhaltlich
  verändert (nur Ort/Verpackung/Konstruktion).
- `xunit.runner.json` vorhanden und wirksam — nachgewiesen durch
  Laufzeitmessung vor/nach in `summary.md`.
- Jede Testklasse trägt `[Trait("Category", "Unit")]` oder
  `[Trait("Category", "Integration")]`; `dotnet test --filter
  Category=Unit` läuft durch und lässt die als Integration markierten
  Klassen erwartungsgemäß aus.
- `ConsoleTestCollection` nur noch an Klassen mit nachweisbarem
  Console-Capture-Bedarf, jeweils dokumentiert warum.
- Kein doppeltes `ILintConsole`-Testdouble mehr (ein Typ, ein
  Namespace, alle bisherigen Verwender umgestellt).
- Alle `*MiniFixtureWorkspace`-Nutzungen außerhalb `Mcp/**` (und nach
  Freigabe auch innerhalb) laufen über `IClassFixture`/
  `ICollectionFixture`, keine Instanziierung mehr pro Testmethode wo
  Wiederverwendung sinnvoll ist.
- Kein handgerollter Temp-Dir-Try/Finally mehr an den identifizierten
  Stellen — einheitlicher Helper im Einsatz.
- 0 lose Testdateien mehr im `AiNetLinter.Tests`-Root außer
  `TestHelper.cs`/`ConsoleTestCollection.cs`.
- Namespace-Verzeichnis-Zuordnung für alle verschobenen Dateien
  konsistent zur Produktionsstruktur.
- `dynamic-loop`-`coder.md`/`kritiker.md` fahren in der
  Iterationsschleife nur noch den gefilterten Lauf, Volllauf nur in
  Baseline/Abschluss — sichtbar in der `state.md`-Protokollierung des
  nächsten Tasks, der dieses Scaffolding nutzt.
- Laufzeitmessung vorher/nachher dokumentiert (Zielrichtung, keine
  harte Prozentzahl vorgegeben).
- Betroffene Doku aktualisiert, falls Testkonventionen dort erwähnt
  sind (`.agents/rules/AiNetLinterRichtlinien.mdc` §4 Update-Pflicht).

## Offene Punkte

Siehe `open_questions` im Frontmatter (`#nullable enable`-Nachrüstung,
`MaxDirectoryChildren`-Aktivierung) — beide bewusst als „später klären"
markiert, blockieren `status: ready` nicht, da funktional/strategisch
unabhängig vom Kern dieses Konzepts.
