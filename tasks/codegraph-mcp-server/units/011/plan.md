---
unit: 011
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-02
trigger: state.md Block "Strategie für 011" — User-Anweisung Ralf 2026-08-02 ~20:45 ("schließe das dann komplett ab - gebündelt")
extends:
  - konzept.md Z. 207-324 (P0/P1-Erweiterungen — Treiber für TD-009)
  - tech-debt.md TD-009, TD-014, TD-019, TD-008, TD-010
  - AiNetLinter.mdc Z. 11 (Input-record-Pflicht), Z. 22 (MaxMethodParameterCount 4), Z. 27 (MaxConstructorDependencies 5)
  - AiNetLinterRichtlinien.mdc §4 (Commit-Disziplin), §5 (Result-Pattern, scoped Hilfe)
  - units/004/plan.md (Scanner-Split-Pattern als Vorbild für Aufteilung)
  - units/007/result.md (TD-003-Lock-Pattern als Vorbild für TD-019-Lock)
  - units/010/plan.md (Reflection-Test-Pattern für A3)
  - units/010/result.md (TD-019-Beobachtung Volllauf-Flake, Re-Run-Diagnostik)
  - state.md "Strategie für 011" (Orchestrator-Vorschlag)
---

# Plan Einheit 011 — TD-Bündel: Konstruktor-Record + Factory-Aufteilung + parallele MCP-Init-Stabilität

## 1. Ziel der Einheit

Drei zusammenhängende strukturelle Tech-Debt-Refactors in **einer**
Coder-Einheit erledigen, die zusammen die Grundlage für die nächsten
P0/P1-Erweiterungen aus `konzept.md` (Kaltstart, `--mcp-log`,
`rules.json`-Auto-Discovery) legen — ohne selbst diese Erweiterungen
anzufassen:

- **TD-009** (Pflicht): `McpCodeGraphServer`-Konstruktor (5/5 am
  `MaxConstructorDependencies`-Limit) auf ein Input-`record`
  `McpCodeGraphServerOptions` umstellen, damit die nächsten 6+
  P0/P1-Extensions an einer Property-Erweiterung des `record` statt
  an einer 6. Konstruktor-Dependency wachsen.
- **TD-014** (Pflicht): `McpServerOptionsFactory` (2484/2500, 16 Z.
  Puffer) durch Aufteilung in `McpServerOptionsBuilder` + schlanke
  Factory strukturell entlasten, damit die nächsten P0/P1-Extensions
  (`--mcp-log`, `instructions`-Erweiterung, Auto-Discovery-Hint) das
  Limit nicht reißen.
- **TD-019** (Pflicht): `SymbolGraphMcpFixture` (und Pendants
  `BaselineMcpFixture` / `McpLiveRepositoryFixture`) durch Retry-Logik
  in `McpTestClient.ConnectAsync` gegen den 010-Volllauf-Flake
  (parallel-Test-Init-Timeout bei 16 Test-Collections) absichern.

Bewusst **NICHT** in 011: TD-008/TD-010 (`ILinterEngineConfig`-
Interface) — größerer Refactor (4-6h), der das Aufruf-Budget sprengt
und thematisch nicht zu den drei strukturellen Vorbereitungs-Refactors
passt; wird in 012+ als eigenständige Einheit eingeplant (siehe
Abschnitt 10 "Bewusst NICHT in 011").

**Bezug zu `konzept.md`:** Z. 207-324 listet die P0/P1-Erweiterungen,
deren erste (`--mcp-log`, Auto-Discovery, Staleness-`mtime`) jede
für sich `McpCodeGraphServer` oder `McpServerOptionsFactory`
erweitern würde. Ohne 011 ist die nächste dieser Erweiterungen ein
Sofort-Build-Bruch. 011 macht den Code bereit für diese Erweiterungen,
ohne sie umzusetzen — das ist der explizite Auftrag des Orchestrators
(User-Prompt: "schließe das dann komplett ab - gebündelt").

**Kein DoD-Punkt** aus `konzept.md` Z. 590-660 wird durch 011
erfüllt oder verletzt — reine strukturelle Tech-Debt-Sanierung, keine
neue Funktionalität, keine Verhaltensänderung am Server.

## 2. Scope-Entscheidung mit Begründung

**Gewählt: 3 Pflicht-Refactors (TD-009 + TD-014 + TD-019) als
gebündelte Coder-Einheit, TD-008/010 explizit ausgeschlossen.**

**Warum gerade diese Wahl:**

- **TD-009 (Pflicht).** Ohne den Refactor ist die nächste
  P0/P1-Erweiterung an `McpCodeGraphServer` ein Build-Bruch
  (`MaxConstructorDependencies: 5` ist der Hard-Limit, siehe
  `AiNetLinter.mdc` Z. 27, und ist in `rules.json:125` mit `"5"`
  gesetzt). Der Selbst-Lint-Compiler bricht ab, sobald die 6.
  Dependency dazukommt. Der Refactor selbst ist mechanisch
  (1:1-Übersetzung Parameter → Property) und durch ein
  Reflection-Signatur-Test A3-sicherbar.
- **TD-014 (Pflicht).** Gleiche Begründung wie TD-009, nur für
  `McpServerOptionsFactory`. Die `ServerInstructions`-Block-Erweiterung
  in 003 hat die Klasse an die Grenze gebracht (Puffer 16 Z. nach
  `dd4b44e`, siehe `state.md` 003-Block). Die nächsten P0/P1-
  Erweiterungen (`--mcp-log`-State im `ServerInfo`, Auto-Discovery-
  Hint, `instructions`-Erweiterung um „lädt noch"-Zustand) brauchen
  jeweils 10-30 Z. zusätzlich — Puffer reicht nicht. Die Aufteilung
  ist reines Refactor, kein Verhaltensrisiko.
- **TD-019 (Pflicht).** Konkret im 010-Volllauf reproduzierbar
  beobachtet: 1 von 1178 Tests flake mit `TaskCanceledException` in
  `SymbolGraphMcpFixture.InitializeAsync` (siehe
  `units/010/result.md` Z. 141-153). Der Flake ist **kein 010-Regress**
  und nicht durch die 010-Änderungen verursacht, aber er ist
  reproduzierbar (klassischer Race in `StdioClientTransport`-Startphase
  bei 16 parallelen Test-Collections). Der 010-Kritiker hat TD-019
  explizit als Folge-TD aufgenommen (`tech-debt.md:TD-019` Z. 47).
  Retry-Logik in `McpTestClient.ConnectAsync` ist ein 30-Z.-Patch mit
  Last-Test-A3 (16 parallele `ConnectAsync`-Aufrufe).
- **Synergie TD-009 + TD-014:** Beide sind strukturelle Refactors
  an MCP-Server-Klassen mit demselben Pattern (Input-`record` statt
  Parameterliste). Der Coder wendet das Muster in einer
  Lern-Schleife zweimal an, statt zweimal unterschiedliche Patterns
  zu lernen — Aufwand sinkt, Risiko bleibt überschaubar.
- **TD-019 ist unabhängig von TD-009/TD-014:** Test-Infrastruktur
  (`Fixtures/McpTestClient.cs`), keine Produktivcode-Änderung am
  Server. Reihenfolge: TD-019 kommt **zuletzt**, weil sein
  Volllauf-Beweis (1178+ Tests grün nach Retry-Patch) von den
  TD-009/TD-014-Vollläufen abhängt.

**Warum nicht TD-008/TD-010 (das `ILinterEngineConfig`-Interface):**

- **Aufwand:** 4-6h Coder (Eigenangabe aus
  `tech-debt.md:TD-008`-Vorschlag), breiter Eingriff in `McpCodeGraphServer`
  + 9 Tool-Klassen + Test-Fixtures. Mit TD-009 (10-20 Dateien) +
  TD-014 (4-5 Dateien) + TD-019 (4 Dateien) ist die Einheit bereits
  **groß** — 3 Commits, ~20-25 Dateien, ~300-500 Z. Diff.
- **TD-008/010 sind NICHT zwingend für die nächsten P0/P1-Schritte.**
  TD-008/010 lösen das `PathOverrides: 2700` für
  `FindReferencesTool`/`FindSymbolTool` (TD-008) und
  `SearchPatternTool` (TD-010) — Pragmatik, kein Build-Bruch. Die
  Klassen wachsen mit `McpCodeGraphServer`-Erweiterungen mit, aber
  solange sie unter 2700 bleiben (Puffer 160-178 für
  FindReferencesTool/FindSymbolTool nach 006, 14 für
  SearchPatternTool), ist kein Sofort-Handlungsbedarf.
- **Thematische Trennung:** TD-008/010 ist ein
  Interface-Refactor (Klassendesign), TD-009/014/019 sind
  Init-Parameter-/Factory-/Test-Infra-Refactors. Sie lösen
  unterschiedliche Probleme — Bündel wäre künstlich.
- **Budget-Realität:** Nach 011 sind noch 3/40 Aufrufe übrig — genau
  eine weitere Einheit (012). Wenn 011 sauber approved ist, kann
  012 entweder TD-008/010 als alleiniger Refactor oder eine
  P0-Erweiterung (Kaltstart) machen. Beides ist besser als ein
  überladenes 011-Bündel, das der Kritiker wegen Scope-Bloat
  ablehnt.
- **Empfehlung an Orchestrator:** 012 = TD-008/010 oder 012 = A1
  (`rules.json`-Auto-Discovery, P0, ~2-3h) — Orchestrator entscheidet
  nach 011-Kritiker-Vote. Der 010-Kritiker hat A1 als nächste
  P0-Pflicht markiert.

**Warum nicht die anderen Tech-Debt-Einträge:**

- **TD-001** (ungenutzte transitive `Microsoft.Extensions.AI.Abstractions`):
  rein paket-technisch, ~30 min, aber risikobehaftet (Paket-API könnte
  gebraucht werden). Eigenständige Einheit oder inline beim nächsten
  Mcp-SDK-Upgrade.
- **TD-002** (Subprozess-E2E ohne Fixture-Pool): gehört zu EPIC-07
  (Tests-Ausbau), der laut `konzept.md` Z. 104-107 noch offen ist
  und mehrere Tage Coder-Aufwand braucht. Nicht für 011.
- **TD-004 / TD-005** (Footprint-Druck Registrar / Server-Pull-in):
  Anti-Pattern-Beschreibungen, keine konkrete Code-Änderung. Werden
  durch TD-014 (Factory-Aufteilung) und TD-009 (Konstruktor-Record)
  mittelbar adressiert — eigene Schließung nicht nötig.
- **TD-006** (Dateiscan-Duplikation): kein Build-Druck, Folge-Refactor
  beim nächsten Dateiscan-Anlass.
- **TD-007** (`TryApplyContentChange` 5-Parameter-Methode):
  könnte inline mit TD-009 mitgenommen werden (beide Methoden in
  `McpCodeGraphServer.cs`), aber: TD-007 ist `private` und
  `MaxMethodParameterCountForNonPublic: 6` greift, also kein
  Build-Druck. **Bewusst NICHT in 011** — Risiko/Aufwand-
  Verhältnis ungünstig (A3-Methode komplexer wegen `private`-
  Reflection, kein Build-Stopper).
- **TD-011** (`SymbolGraphToolRegistrations` 2494/2500, Puffer 6):
  wird erst beim nächsten Symbolgraph-Tool scharf — kein
  P0/P1-Trigger in 011. 5. Registrar-Klasse kommt frühestens beim
  `get_symbol_body`-Backlog (P2).
- **TD-016a** (2 von 4 Fixtures noch nicht refaktoriert):
  bereits in 009 erledigt, Status: geschlossen.

## 3. Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### 3.1 `McpCodeGraphServer`-Konstruktor (TD-009) — gelesen, exakt

`src/AiNetLinter/Mcp/McpCodeGraphServer.cs:30-46`:

```csharp
public McpCodeGraphServer(
    SourceFileCatalog? catalog,
    ILintConsole? console = null,
    int maxLineCount = 700,
    Config? config = null,
    ILintConsole? consoleOverride = null)
```

- 5 Parameter: 2× nullable, 1× required-implicit (catalog), 2× optional
  mit Default. Konstruktor-Body: 8 Zeilen (Z. 36-45, ohne Leerzeilen).
- 1 Property (`MaxLineCount`, `Config`, `Console`) liest alle 5
  Parameter — keine Verarbeitungs-Logik, die beim Refactor verloren
  gehen könnte.
- `Config`-Property-Typ ist `Config` (Klasse aus
  `src/AiNetLinter/Configuration/Config.cs`), nicht ein Interface — das
  ist der Pull-in-Mechanismus aus TD-008. **Bleibt in TD-009
  unverändert** (kein Interface-Refactor in dieser Einheit).
- `consoleOverride` ist ein zweiter `ILintConsole`-Parameter für
  Test-Override-Pfad (siehe Body Z. 38:
  `_console = consoleOverride ?? console ?? LinterConsole.Instance;`).
  Hat im aktuellen Codebase **keinen** Produktiv-Caller, der ihn
  nutzt (gelesen, `McpServerCommand.cs:36` übergibt nur 4 Parameter).
  Wird in TD-009 **mit** ins `record` übernommen, damit das
  Verhalten 1:1 erhalten bleibt — Coder dokumentiert, ob er
  `consoleOverride` als deprecated markiert oder aktiv lässt.

### 3.2 `McpServerOptionsFactory` (TD-014) — gelesen, exakt

`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (67 Z.):

- `private const string ServerName` (Z. 18) + `private const string ServerInstructions` (Z. 26-31, 6 Z.) = statische Konstanten.
- `Create(McpCodeGraphServer mcpState)` (Z. 38-50) — 13 Zeilen Body, baut `McpServerOptions`.
- `BuildToolCollection(McpCodeGraphServer)` (Z. 52-61) — 9 Zeilen, ruft 3 Registrar-Klassen auf.
- `GetServerVersion()` (Z. 63-66) — 3 Zeilen, Reflection über `Assembly.GetExecutingAssembly()`.

**Footprint-Stand (gemessen 2026-08-01, `state.md` 003-Block):**
2484/2500 Z., 16 Z. Puffer.

**Refactor-Vorschlag (im Plan festgelegt):**

- **Neu:** `McpServerOptionsBuilder` als `internal sealed class` mit
  Fluent-API: `WithServerName(string)`, `WithServerVersion(string)`,
  `WithServerInstructions(string)`, `WithToolCollection(McpServerPrimitiveCollection<McpServerTool>)`,
  abschließend `Build() → McpServerOptions`.
- **`McpServerOptionsFactory`** bleibt, wird zur dünnen Factory:
  `Create(McpCodeGraphServer state) → McpServerOptions` delegiert
  intern an `McpServerOptionsBuilder` + die statischen Konstanten +
  `GetServerVersion()`.
- **Begründung der Wahl:** Builder-Pattern (statt Init-`record`)
  ist die idiomatische .NET-Lösung für mehrstufigen Objekt-Aufbau
  mit optionalen Konfigurations-Schritten. Jeder zukünftige P0/P1-
  Extensions-Baustein (`--mcp-log`-State, Auto-Discovery-Hint,
  ServerInfo-Erweiterung) bekommt eine eigene `With*`-Methode auf
  dem Builder — die Factory selbst bleibt ein 1-Zeilen-Delegate.
  Erwartete neue Footprint-Verteilung nach 011: Builder ~100-130 Z.,
  Factory ~25-35 Z. (statt vorher 67 Z. monolithisch) — jeder
  einzelne Block hat 200+ Z. Reserve.

### 3.3 `McpCodeGraphServer`-Call-Sites (TD-009) — gelesen, vollständig inventarisiert

`grep "new McpCodeGraphServer\("` über `src/`: **65 Vorkommen in 12
Dateien** (Stand 2026-08-02):

| Datei | Call-Sites |
|---|---:|
| `src/AiNetLinter/Commands/McpServerCommand.cs:36` | 1 (Produktivcode, 4-Parameter-Aufruf) |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` | 6 (3 `null`, 3 mit Catalog) |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs:22` | 1 (Test für `Create()`-Factory) |
| `src/AiNetLinter.Tests/Mcp/Tools/FindReferencesToolTests.cs` | 4 |
| `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs` | 2 |
| `src/AiNetLinter.Tests/Mcp/Tools/SearchPatternToolTests.cs` | 9 |
| `src/AiNetLinter.Tests/Mcp/Tools/GetFileSkeletonToolTests.cs` | 5 |
| `src/AiNetLinter.Tests/Mcp/Tools/GetHotspotsToolTests.cs` | 7 |
| `src/AiNetLinter.Tests/Mcp/Tools/GetImpactToolTests.cs` | 9 |
| `src/AiNetLinter.Tests/Mcp/Tools/GetTypeHierarchyToolTests.cs` | 9 |
| `src/AiNetLinter.Tests/Mcp/Tools/GetIndexScopeToolTests.cs` | 6 |
| `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsToolTests.cs` | 6 |

**Aufruf-Varianten (alle kategorisiert):**

- **`new McpCodeGraphServer(null)`** — nur-Konstruktor-Test
  (kein Catalog, keine Config) — **15×**.
- **`new McpCodeGraphServer(catalog)`** — mit Catalog, alle anderen
  Default — **~30×**.
- **`new McpCodeGraphServer(catalog, c, ResolveMaxLineCount(args), ResolveConfig(args))`**
  in `McpServerCommand.cs:36` — 1× (Produktivcode).
- **`new McpCodeGraphServer(catalog, console: c, maxLineCount: 700, config: null)`**
  o. ä. mit benannten Argumenten — wenige.

**Refactor-Plan:** Alle Aufrufe bekommen einen einzeiligen
`new McpCodeGraphServerOptions { Catalog = ..., Console = ..., ... }`-
Wrapper. Mechanisch, 1:1-Übersetzung. Coder erstellt einen
zentralen Helper `McpCodeGraphServerOptions.From(...)` mit den
gleichen 5 optionalen Parametern wie der alte Konstruktor, damit
Tests minimal invasiv geändert werden (nur `McpCodeGraphServer(...)` →
`McpCodeGraphServer(McpCodeGraphServerOptions.From(...))`). Das ist
eine **Plan-Abweichung** vom naiven 1:1-Refactor und in Abschnitt 7
explizit erlaubt.

### 3.4 `McpTestClient.ConnectAsync` (TD-019) — gelesen, exakt

`src/AiNetLinter.Tests/Mcp/McpTestClient.cs:29-52`:

- Aktuell: 1 Versuch, 30s Timeout via `cts.CancelAfter`, kein Retry.
- `StdioClientTransport`-Konstruktion (Z. 40-45) ist deterministisch
  schnell, **außer** wenn 16 Test-Collections parallel denselben
  `AiNetLinter.exe`-Subprozess starten — dann kann der Windows-
  Process-Spawn oder die stdio-Pipe-Initialisierung länger als 30s
  dauern (siehe 010-Volllauf-Beobachtung, `units/010/result.md`
  Z. 141-153).
- **Refactor:** `ConnectAsync` bekommt eine optionale `RetryOptions`-
  Parameter-`record` (max retries, base delay, exponential factor) mit
  Default (3 retries, 500ms base, 2.0 factor = 0.5s/1s/2s Backoff).
  `SymbolGraphMcpFixture` / `BaselineMcpFixture` /
  `McpLiveRepositoryFixture` reichen die Default-Optionen durch
  (oder wählen einen konservativeren 60s-Init-Timeout + 5 retries für
  Last-Fixture).
- **Last-Test (A3):** Neuer Test `McpTestClient_ConnectAsync_ParallelStress_AllSucceedOrFailCleanly`
  in `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs`,
  `[Trait("Category", "Integration")]`, startet 16 parallele
  `ConnectAsync`-Aufrufe, erwartet dass mindestens 14/16 erfolgreich
  sind (Schwelle für 2-Retry-Toleranz) oder alle mit klarem Error
  scheitern (kein Timeout-Flake). Test ist **nicht** im Unit-Slice
  (erfordert echten Server-Subprozess), läuft im Volllauf.

### 3.5 Synergie-Check: Builder-Pattern existiert bereits?

`grep "class.*Builder"` in `src/AiNetLinter/` (gelesen, 2026-08-02):
**kein** vorhandenes `Builder`-Pattern im `Mcp`-Namespace. Andere
Builder (z. B. `SkeletonMapBuilder`, `HotspotMapBuilder`,
`DiffImpactAnalyzer.AnalyzeAsync` mit Init-Param) sind spezifische
Datentransform-Builder, keine Konfigurations-Builder. Kein Duplikat-
Risiko — der `McpServerOptionsBuilder` ist eine neue Klasse.

### 3.6 Synergie-Check: Init-`record` für `McpServerOptionsFactory`?

Alternative wäre ein `internal sealed record McpServerOptionsInit(string Name, string Version, string Instructions, McpCodeGraphServer State)` und `McpServerOptionsFactory.Create(init) → McpServerOptions`.
**Wird verworfen**, weil:

- Die Konfiguration wird **nie** an mehreren Stellen erzeugt (kein
  Duplikat-Vermeidungs-Argument wie bei `McpCodeGraphServerOptions`).
- Builder-Pattern ist flexibler für die nächsten P0/P1-Extensions:
  `--mcp-log`-State als `WithCallLogEnabled(bool)`,
  Auto-Discovery-Hint als `WithDiscoveryHint(string)`, etc. —
  optionale Konfigurations-Schritte statt erzwungener
  Record-Konstruktion.
- 67 Z. Factory splittet in 1× ~25 Z. Factory + 1× ~100 Z. Builder
  — sauberer als 67 Z. + 1 Init-`record` (~30 Z.).

### 3.7 Projektregeln-Check (A7, A8)

- **A7** (`konzept.md` ist bindend, nur lesbar): 011 ändert `konzept.md`
  **nicht** — keine P0/P1-Extension wird umgesetzt, nur Struktur-
  Refactor. A7 ist nicht betroffen.
- **A8** (Kernel und Rollen unantastbar): nicht betroffen, 011 fasst
  weder `kernel.md` noch eine Rollen-Datei an.
- **`AiNetLinterRichtlinien.mdc` §4 (Commit-Disziplin):** 011 hat
  voraussichtlich 3-4 Commits (1 pro TD + 1 für
  `tech-debt.md`-Schließungen), alle mit `[codegraph-mcp-server]`-
  Suffix, deutsch, imperativ, Conventional Commits, gezielter `git add`
  pro Datei, kein Push (A4), kein Amend.
- **`AiNetLinter.mdc` Z. 11 (Input-`record`-Pflicht), Z. 22
  (`MaxMethodParameterCount 4`), Z. 27 (`MaxConstructorDependencies 5`):**
  TD-009 löst genau das `MaxConstructorDependencies`-Problem.
  TD-014 löst das gleiche Problem für die Factory. Beide sind explizit
  von der Regel gefordert.
- **`rules.json`:** **NICHT angefasst** in 011 (A7). Auch keine
  `PathOverrides`-Reduktion für TD-008/010, weil TD-008/010 nicht
  in 011 ist. Wenn Coder versehentlich `rules.json` anfasst, ist
  das ein Verstoß gegen Abschnitt 7 "Plan-Abweichungen".

### 3.8 Footprint-Baseline (gemessen 2026-08-02)

| Klasse | Z. | Limit | Puffer | TD-Status |
|---|---:|---:|---:|---|
| `McpCodeGraphServer` | 184 | 2500 | 2316 | TD-009-Betroffen |
| `McpServerOptionsFactory` | 67 | 2500 | 2433 | TD-014-Betroffen |
| `McpTestClient` | 114 | 2500 | 2386 | TD-019-Betroffen |
| `SymbolGraphMcpFixture` | 34 | 500 | 466 | TD-019-Betroffen |
| `BaselineMcpFixture` | 34 | 500 | 466 | TD-019-Betoffen (Pendants) |
| `McpLiveRepositoryFixture` | 47 | 500 | 453 | TD-019-Betroffen (Pendants) |
| `Configuration.Config` (Namespace) | ~1110 | n/a | n/a | TD-008/010-Bezug, NICHT in 011 |

**Erwartete Footprints nach 011 (Schätzung):**

- `McpCodeGraphServer` 184 → ~195-210 Z. (Konstruktor wird 1-Zeiler,
  `McpCodeGraphServerOptions`-Property hinzu, ggf. `From`-Helper).
- `McpServerOptionsFactory` 67 → ~25-35 Z. (durchgereicht an Builder).
- `McpServerOptionsBuilder` (NEU) ~100-130 Z.
- `McpTestClient` 114 → ~145-165 Z. (Retry-Loop + Logging).
- 3 Fixtures 34-47 Z. → 38-52 Z. (Retry-Options weitergereicht).

**Auch nach 011:** Keine `PathOverrides`-Werte ändern sich — die
4 existierenden 2700er-Overrides für `AuditCommand`,
`FindReferencesTool`, `FindSymbolTool`, `GetImpactTool`,
`SymbolGraphToolRegistrations` bleiben unverändert. `SearchPatternTool`
(2482/2500) bleibt unverändert (TD-010 nicht in 011).

### 3.9 Drift / Duplikate durch Blindheit

- **Drift:** keine — die 3 TD-Einträge sind im `tech-debt.md`
  dokumentiert, exakt wie hier geplant. Code-Stand entspricht dem
  letzten Commit `ca413c1` (working tree clean).
- **Duplikate durch Blindheit:**
  - **TD-009-Alternative "Init-`record`":** bewusst verworfen
    zugunsten Builder-Pattern (TD-014-Synergie).
  - **TD-019-Alternative "xUnit-Collection-Serialisierung":**
    bewusst verworfen — würde den Volllauf um Faktor 4-8 verlangsamen
    (16 parallele Collections → sequenziell). Retry-Logik ist
    punktuell, schnell, ändert das Test-Parallelitäts-Modell nicht.
  - **`McpCodeGraphServerOptions.From(...)`-Helper:** nicht
    1:1-Duplikat des alten Konstruktors — der Helper ist eine
    bewusste Plan-Abweichung (Abschnitt 7), um die 65 Call-Sites
    minimal-invasiv zu migrieren.

## 4. Betroffene Dateien / Module

### TD-009 (`McpCodeGraphServer`-Konstruktor → Input-`record`)

| Datei | Pflicht? | Erwartete Diff-Größe |
|---|---|---:|
| `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` (NEU) | **ja** | ~30-50 Z. (`internal sealed record` + `From`-Helper + XML-Doc) |
| `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` | **ja** | Konstruktor 16 Z. → 3-5 Z., `MaxLineCount`/`Config`/`Console` aus Options gelesen |
| `src/AiNetLinter/Commands/McpServerCommand.cs:36` | **ja** | 1-Z.-Refactor: `new McpCodeGraphServer(...)` → `new McpCodeGraphServer(McpCodeGraphServerOptions.From(...))` |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerTests.cs` (6 Stellen) | **ja** | 6× 1-Z.-Refactor |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs:22` | **ja** | 1× 1-Z.-Refactor |
| `src/AiNetLinter.Tests/Mcp/Tools/*ToolTests.cs` (9 Dateien, ~60 Stellen) | **ja** | 60× 1-Z.-Refactor |
| `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs` (NEU) | **ja** (A3) | ~60-80 Z., 1 Reflection-Test (1 Theory mit 3-4 InlineData) |
| `tasks/codegraph-mcp-server/units/011/result.md` (NEU, vom Coder) | **ja** | Standard-Result-Protokoll mit A3-Block + Footprint-Tabelle |

### TD-014 (`McpServerOptionsFactory` → `Builder` + schlanke Factory)

| Datei | Pflicht? | Erwartete Diff-Größe |
|---|---|---:|
| `src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs` (NEU) | **ja** | ~100-130 Z. (Fluent-API + `Build()`) |
| `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | **ja** | 67 → ~25-35 Z. (delegiert an Builder) |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsFactoryTests.cs` | **nein** (oder minimal) | Tests bleiben grün ohne Anpassung, wenn Builder-Verträglichkeit erhalten bleibt |
| `src/AiNetLinter.Tests/Mcp/McpServerOptionsBuilderTests.cs` (NEU) | **ja** (A3) | ~80-100 Z., 2-3 Tests (Default-Build, `With*`-Override, vollständige Konfiguration) |

### TD-019 (parallele MCP-Init-Stabilität)

| Datei | Pflicht? | Erwartete Diff-Größe |
|---|---|---:|
| `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` | **ja** | `ConnectAsync` bekommt Retry-Loop (Z. 29-52, +30-40 Z.) |
| `src/AiNetLinter.Tests/Fixtures/SymbolGraphMcpFixture.cs` | **ja** | 1-Z.-Refactor: optionale `RetryOptions` weitergereicht |
| `src/AiNetLinter.Tests/Fixtures/BaselineMcpFixture.cs` | **ja** | 1-Z.-Refactor (Pendant) |
| `src/AiNetLinter.Tests/Fixtures/McpLiveRepositoryFixture.cs` | **ja** | 1-Z.-Refactor (Pendant) |
| `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs` (NEU) | **ja** (A3) | ~80-100 Z., 1 Last-Test mit 16 parallelen `ConnectAsync`-Aufrufen |

### Schließungen / `tech-debt.md` (vom Coder am Ende)

| Datei | Pflicht? | Erwartete Diff-Größe |
|---|---|---:|
| `tasks/codegraph-mcp-server/tech-debt.md` | **ja** (vom Coder) | TD-009, TD-014, TD-019 Index-Zeilen + Bodies auf "geschlossen" setzen, 14 → 11 offene Einträge |

**Nicht ändern (A7/A8, explizit wiederholt):**

- `konzept.md` (A7 — keine P0/P1-Extensions in 011, kein Konzept-Drift)
- `kernel.md` (A8)
- `agents/planer.md` / `agents/coder.md` / `agents/kritiker.md` (A8)
- `.agents/rules/AiNetLinter.mdc` (A7)
- `.agents/rules/AiNetLinterRichtlinien.mdc` (A7)
- `rules.json` (A7 — keine `PathOverrides`-Änderung, TD-008/010 nicht in 011)
- `Docs/**` (EPIC-08 abgeschlossen, keine Doku-Änderung nötig)
- `README.md` (A7)
- `AiNetLinter.csproj` (A7 — keine Paket-Änderung)
- `Mcp/Tools/*Tool.cs` (außer dem minimalen `McpServerOptionsFactory`-
  Delegations-Refactor — die 9 Tool-Klassen greifen nicht in
  `McpServerOptionsFactory` ein, der Refactor ist für sie
  transparent)
- `Mcp/Tools/*Scanner.cs` (nicht betroffen)
- `Mcp/Tools/*Registrations.cs` (nicht betroffen — die 3 Registrar-
  Klassen werden weiterhin von `McpServerOptionsFactory` aufgerufen,
  Signatur `Register(tools, state)` unverändert)
- `tasks/codegraph-mcp-server/konzept.md` (A7)

## 5. Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 0 — Pre-Build-Check + Footprint-Baseline (gemessen)

Vor jeder Code-Änderung:

1. `dotnet build AiNetLinter.slnx` — muss grün sein.
2. `dotnet test AiNetLinter.slnx --no-build --filter "Category=Unit"`
   — Unit-Slice muss grün sein (gemessen 2026-08-02: 93/93 in ~21s).
3. `dotnet test AiNetLinter.slnx --no-build` — Volllauf
   (gemessen 2026-08-02: 1178/1178 in ~6:30 min, **1 Re-Run wegen
   Flake** = 1178+1178 = ca. 13 min).
4. Footprint-Messung pro betroffener Klasse
   (`dotnet run --project src/AiNetLinter -- --footprint <Class> --path .`),
   exakt wie 005/006 dokumentiert. Werte in
   `result.md` Abschnitt "Footprint-Baseline" eintragen.

**Erwartetes Ergebnis:** Build grün, Unit-Slice grün, Volllauf
1178/1178 (oder 1178+1 Flake mit dokumentiertem Re-Run = Konsistenz),
Footprints wie in 3.8 dokumentiert.

### Schritt 1 — TD-014 zuerst: `McpServerOptionsBuilder` + schlanke Factory (Pflicht)

**Reihenfolge-Begründung:** TD-014 ist am wenigsten invasiv (1 neue
Datei + 1 Modifikation + 1 Test-Datei), schafft Footprint-Reserve
für die nächsten P0/P1-Extensions, und der Coder wendet das
Builder-Pattern an, bevor er im TD-009-Schritt das `record`-Pattern
anwendet — beides strukturelle Init-Pattern-Refactors, aber das
einfachere zuerst.

**Schritt 1.1 — Neue Datei `src/AiNetLinter/Mcp/McpServerOptionsBuilder.cs` (NEU)**

Struktur (illustrative Vorlage, nicht wortwörtlich zu kopieren):

```csharp
#nullable enable

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Fluent-Builder fuer <see cref="McpServerOptions"/>. Aus <c>McpServerOptionsFactory</c>
/// ausgelagert, um die Klasse selbst unter dem <c>AIContextFootprint</c>-Limit zu halten
/// (siehe <c>AiNetLinter.mdc</c> Z. 15, 28) und kuenftige P0/P1-Erweiterungen
/// (<c>--mcp-log</c>-State, <c>rules.json</c>-Auto-Discovery-Hint) als additive
/// <c>With*</c>-Methoden zu ermoeglichen, ohne die Factory selbst zu vergroessern.
/// Instanzen sind nicht thread-safe — pro Build-Vorgang neu erzeugen.
/// </summary>
internal sealed class McpServerOptionsBuilder
{
    private string _serverName = "ainetlinter";
    private string? _serverVersion;
    private string _serverInstructions = string.Empty;
    private McpServerPrimitiveCollection<McpServerTool>? _toolCollection;

    public McpServerOptionsBuilder WithServerName(string name) { _serverName = name; return this; }
    public McpServerOptionsBuilder WithServerVersion(string? version) { _serverVersion = version; return this; }
    public McpServerOptionsBuilder WithServerInstructions(string instructions) { _serverInstructions = instructions; return this; }
    public McpServerOptionsBuilder WithToolCollection(McpServerPrimitiveCollection<McpServerTool> tools) { _toolCollection = tools; return this; }

    internal McpServerOptions Build()
    {
        return new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = _serverName,
                Version = _serverVersion ?? "0.0.0",
            },
            ServerInstructions = _serverInstructions,
            ToolCollection = _toolCollection ?? new McpServerPrimitiveCollection<McpServerTool>(),
        };
    }
}
```

Coder darf:
- Den `using ModelContextProtocol.Protocol;` weglassen, falls `Implementation` über den bestehenden `using ModelContextProtocol.Server;` erreichbar ist — selbst verifizieren.
- `With*`-Methoden als Expression-Bodied schreiben (kürzer, moderner).
- `_toolCollection` als `required` über einen separaten `Build(toolCollection)`-Overload anbieten, falls das idiomatischer ist — **muss dokumentiert** werden.

**Schritt 1.2 — `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` umbauen**

```csharp
#nullable enable

using System.Reflection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Baut die <see cref="McpServerOptions"/> inkl. der registrierten Tool-Collection. Bewusst
/// aus <see cref="AiNetLinter.Commands.McpServerCommand"/> ausgelagert und durch
/// <see cref="McpServerOptionsBuilder"/> in eine schlanke Factory + Builder aufgeteilt
/// (TD-014): haette <see cref="McpCodeGraphServer"/> als Parametertyp eines eigenen
/// Members, waechst dessen AIContextFootprint durch die Tool-Registrierungs-
/// Abhaengigkeiten ueber das Limit.
/// </summary>
internal static class McpServerOptionsFactory
{
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

    /// <summary>
    /// Baut die vollstaendigen Server-Optionen inkl. aller registrierten Tools. Tools erreichen
    /// den resident gehaltenen <paramref name="mcpState"/> per Delegate-Closure — kein
    /// DI-Container (siehe <c>AiNetLinterRichtlinien.mdc</c> §2).
    /// </summary>
    internal static McpServerOptions Create(McpCodeGraphServer mcpState)
    {
        return new McpServerOptionsBuilder()
            .WithServerVersion(GetServerVersion())
            .WithServerInstructions(ServerInstructions)
            .WithToolCollection(BuildToolCollection(mcpState))
            .Build();
    }

    private static McpServerPrimitiveCollection<McpServerTool> BuildToolCollection(McpCodeGraphServer mcpState)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();

        SymbolGraphToolRegistrations.Register(tools, mcpState);
        FileStructureToolRegistrations.Register(tools, mcpState);
        AnalysisToolRegistrations.Register(tools, mcpState);

        return tools;
    }

    private static string GetServerVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
    }
}
```

Erwartete Endgröße: ~35-45 Z. (vorher 67 Z.). `Build()` macht die
Wert-Defaults explizit, Factory bleibt lesbar.

**Schritt 1.3 — Neue Test-Datei `src/AiNetLinter.Tests/Mcp/McpServerOptionsBuilderTests.cs` (NEU)**

3 Unit-Tests, `[Trait("Category", "Unit")]`, sealed:

- `Build_DefaultName_UsesAinetlinter` — Builder ohne
  `WithServerName`-Aufruf, prüft `ServerInfo.Name == "ainetlinter"`.
- `Build_DefaultVersion_UsesAssemblyVersion` — ohne
  `WithServerVersion`-Aufruf, prüft `ServerInfo.Version` matcht
  `Assembly.GetExecutingAssembly().GetName().Version`.
- `Build_WithServerInstructions_PropagatesToServerOptions` — mit
  `WithServerInstructions("Test-Instructions")`, prüft
  `ServerInstructions == "Test-Instructions"`.

A3 für jeden Test: Assertion rotbiegen → rot, zurück → grün.

**Schritt 1.4 — Build + Tests grün**

- `dotnet build AiNetLinter.slnx` muss grün sein (0/0).
- `dotnet test AiNetLinter.slnx --no-build --filter "FullyQualifiedName~McpServerOptionsBuilderTests"` muss 3/3 grün sein.
- `dotnet test AiNetLinter.slnx --no-build --filter "FullyQualifiedName~McpServerOptionsFactoryTests"` muss weiterhin grün sein (existierende Tests testen nur die `Create()`-Methode, die Signatur bleibt `Create(McpCodeGraphServer) → McpServerOptions`).
- Unit-Slice grün (`Category=Unit`).
- Volllauf grün.

**Schritt 1.5 — Commit 1**

`refactor(mcp): mcp-server-options-builder + schlanke factory (TD-014) [codegraph-mcp-server]`

Geänderte Dateien: `McpServerOptionsBuilder.cs` (NEU),
`McpServerOptionsFactory.cs`, `McpServerOptionsBuilderTests.cs` (NEU).
Gezielter `git add` pro Datei. Kein Push, kein Amend.

### Schritt 2 — TD-009 als zweites: `McpCodeGraphServerOptions`-`record` + Konstruktor-Migration (Pflicht)

**Schritt 2.1 — Neue Datei `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs` (NEU)**

Struktur (illustrative Vorlage):

```csharp
#nullable enable

using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp;

/// <summary>
/// Input-Parametersatz fuer <see cref="McpCodeGraphServer"/>. Eingefuehrt mit TD-009, weil
/// der vorherige 5-Parameter-Konstruktor das projektweite <c>MaxConstructorDependencies: 5</c>-Limit
/// (siehe <c>AiNetLinter.mdc</c> Z. 27) exakt erreichte — jede weitere P0/P1-Erweiterung
/// an <see cref="McpCodeGraphServer"/> haette den Build gebrochen. Mit diesem Record wachsen
/// kuenftige Konfigurations-Properties additiv, ohne die Konstruktor-Signatur zu aendern.
/// </summary>
internal sealed record McpCodeGraphServerOptions
{
    /// <summary>Geladene Solution, kann <see langword="null"/> sein fuer nicht-ladbare Fixtures.</summary>
    public required SourceFileCatalog? Catalog { get; init; }

    /// <summary>Haupt-Konsolen-Kanal fuer Server-Logs und Lint-Warnungen.</summary>
    public required ILintConsole Console { get; init; }

    /// <summary>Zeilen-Grenzwert fuer <c>get_hotspots</c>-Klassifikation, Default 700
    /// (siehe <c>MetricsConfig.MaxLineCount</c>).</summary>
    public int MaxLineCount { get; init; } = 700;

    /// <summary>Vollstaendige Linter-Konfiguration aus <c>rules.json</c> via <c>--config</c>,
    /// sonst Default-<see cref="Config"/>.</summary>
    public required Config Config { get; init; }

    /// <summary>Optionaler Override-Konsolen-Kanal (Test-Override-Pfad, kein
    /// Produktiv-Caller in <c>McpServerCommand</c>). Default = <see langword="null"/>.</summary>
    public ILintConsole? ConsoleOverride { get; init; }

    /// <summary>
    /// Factory-Methode mit identischer Parameter-Signatur wie der alte
    /// <c>McpCodeGraphServer</c>-Konstruktor. Erlaubt minimal-invasive Migration der
    /// 65 Call-Sites (1:1-Uebersetzung) ohne neuen 5-Parameter-Record-Konstruktor.
    /// </summary>
    public static McpCodeGraphServerOptions From(
        SourceFileCatalog? catalog,
        ILintConsole? console = null,
        int maxLineCount = 700,
        Config? config = null,
        ILintConsole? consoleOverride = null)
    {
        return new McpCodeGraphServerOptions
        {
            Catalog = catalog,
            Console = consoleOverride ?? console ?? LinterConsole.Instance,
            MaxLineCount = maxLineCount,
            Config = config ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
            ConsoleOverride = consoleOverride,
        };
    }
}
```

**Bewusste Designentscheidung (im Plan festgelegt):**

- `Console` ist im Record `required` (nicht nullable), weil der
  Konstruktor-Body den Default `LinterConsole.Instance` setzt — der
  `From`-Helper macht diese Normalisierung **vor** der Record-
  Erzeugung, damit `required` ohne Schmerzen geht.
- `Config` ist `required` (nicht nullable), gleiche Begründung — der
  `From`-Helper normalisiert mit `new Config { ... }`.
- `ConsoleOverride` bleibt nullable + optional — Test-Override-Pfad
  bleibt erhalten.
- `From`-Methode ist `static` auf dem Record, nicht im Server —
  hält die Migration 1:1 mechanisch.

**Schritt 2.2 — `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` umbauen**

Vorher (Z. 30-46):

```csharp
public McpCodeGraphServer(
    SourceFileCatalog? catalog,
    ILintConsole? console = null,
    int maxLineCount = 700,
    Config? config = null,
    ILintConsole? consoleOverride = null)
{
    _catalog = catalog;
    _console = consoleOverride ?? console ?? LinterConsole.Instance;
    MaxLineCount = maxLineCount;
    Config = config ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

    if (_catalog is not null)
    {
        InitializeFileState(_catalog.Solution);
    }
}
```

Nachher:

```csharp
public McpCodeGraphServer(McpCodeGraphServerOptions options)
{
    ArgumentNullException.ThrowIfNull(options);
    _catalog = options.Catalog;
    _console = options.Console;
    MaxLineCount = options.MaxLineCount;
    Config = options.Config;

    if (_catalog is not null)
    {
        InitializeFileState(_catalog.Solution);
    }
}
```

**6 Zeilen** (vorher 16). Properties (`MaxLineCount`, `Config`,
`Console`) bleiben — die Felder `_console` und `_catalog` werden
stattdessen nicht mehr im Record gehalten, der Server liest sie
weiterhin aus den Properties.

**Schritt 2.3 — `src/AiNetLinter/Commands/McpServerCommand.cs:36` anpassen**

Vorher:

```csharp
using var mcpState = new McpCodeGraphServer(catalog, c, ResolveMaxLineCount(args), ResolveConfig(args));
```

Nachher:

```csharp
using var mcpState = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
    catalog, c, ResolveMaxLineCount(args), ResolveConfig(args)));
```

**Schritt 2.4 — Alle 64 Test-Call-Sites mechanisch migrieren**

Coder nimmt **`Get-ChildItem -Recurse`** über `src/AiNetLinter.Tests/Mcp/`,
**`Select-String -Pattern 'new McpCodeGraphServer\('`**, geht die
65 Treffer durch und wendet den 1:1-Migrationspattern an:

| Alt | Neu |
|---|---|
| `new McpCodeGraphServer(null)` | `new McpCodeGraphServer(McpCodeGraphServerOptions.From(null))` |
| `new McpCodeGraphServer(catalog)` | `new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog))` |
| `new McpCodeGraphServer(catalog, console)` | `new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog, console))` |
| `new McpCodeGraphServer(catalog, console, maxLineCount, config)` | `new McpCodeGraphServer(McpCodeGraphServerOptions.From(catalog, console, maxLineCount, config))` |

**Schritt 2.5 — Neue Test-Datei `src/AiNetLinter.Tests/Mcp/McpCodeGraphServerConstructorTests.cs` (NEU)**

1 `[Fact]`-Test, sealed, `[Trait("Category", "Unit")]`:

```csharp
[Fact]
public void Constructor_TakesExactlyOneParameter_OfTypeMcpCodeGraphServerOptions()
{
    var ctors = typeof(McpCodeGraphServer).GetConstructors(
        BindingFlags.Public | BindingFlags.Instance);

    Assert.Single(ctors);
    var parameters = ctors[0].GetParameters();
    Assert.Single(parameters);
    Assert.Equal(typeof(McpCodeGraphServerOptions), parameters[0].ParameterType);
}
```

A3: Signatur zurück auf 5 Parameter biegen → Reflection-Test schlägt
fehl (`Assert.Single(parameters)` rot). Zurück → grün.

Optional 1 zusätzlicher Test:
`Constructor_AcceptsNullOptions_ThrowsArgumentNullException` — falls
der Coder `ArgumentNullException.ThrowIfNull` einbaut, prüft dieser
Test, dass `new McpCodeGraphServer(null!)` wirft (nicht
`NullReferenceException`).

**Schritt 2.6 — Build + Tests grün**

- `dotnet build AiNetLinter.slnx` 0/0.
- Unit-Slice grün.
- Volllauf grün.

**Schritt 2.7 — Commit 2**

`refactor(mcp): mcp-code-graph-server-konstruktor auf input-record umgestellt (TD-009) [codegraph-mcp-server]`

Geänderte Dateien: `McpCodeGraphServerOptions.cs` (NEU),
`McpCodeGraphServer.cs`, `McpServerCommand.cs`, 11 Test-Dateien +
`McpCodeGraphServerConstructorTests.cs` (NEU).
Gezielter `git add` pro Datei (12-13 Dateien in 1 Commit, weil sie
eine logische Einheit bilden — der Coder darf sie auf 2 Commits
aufteilen, wenn er z. B. erst die `Options`-Klasse + `McpCodeGraphServer`
+ `McpServerCommand` und dann die 11 Test-Migrationen + den
neuen Test trennen will — beides A4-konform, im `result.md` zu
dokumentieren).

### Schritt 3 — TD-019 als drittes: parallele MCP-Init-Stabilität via Retry-Logik (Pflicht)

**Schritt 3.1 — `src/AiNetLinter.Tests/Mcp/McpTestClient.cs` erweitern**

Vor `ConnectAsync` (Z. 29) einen neuen Typ einfügen:

```csharp
/// <summary>
/// Konfiguration fuer die Retry-Schleife in <see cref="McpTestClient.ConnectAsync"/>
/// bei flake-anfaelligen Parallel-Init-Szenarien (siehe TD-019, 010-Volllauf-Beobachtung).
/// </summary>
public sealed record McpTestClientRetryOptions(
    int MaxRetries = 3,
    int BaseDelayMs = 500,
    double BackoffFactor = 2.0);
```

`ConnectAsync` (Z. 29-52) umbauen:

```csharp
public static async Task<McpTestClient> ConnectAsync(
    string targetDirectory,
    int timeoutSeconds = 30,
    CancellationToken cancellationToken = default,
    McpTestClientRetryOptions? retryOptions = null)
{
    retryOptions ??= new McpTestClientRetryOptions();
    var attempt = 0;
    Exception? lastException = null;

    while (attempt <= retryOptions.MaxRetries)
    {
        try
        {
            var exePath = Path.Combine(AppContext.BaseDirectory, "AiNetLinter.exe");
            if (!File.Exists(exePath))
            {
                throw new FileNotFoundException($"Erwartete AiNetLinter.exe nicht in BaseDirectory gefunden: {exePath}");
            }

            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "ainetlinter-mcp-test-client",
                Command = exePath,
                Arguments = ["--mcp-server", "--path", targetDirectory],
            });

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var client = await McpClient.CreateAsync(transport, cancellationToken: cts.Token);
            return new McpTestClient(client);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lastException = ex;
            attempt++;
            if (attempt > retryOptions.MaxRetries) break;

            var delayMs = retryOptions.BaseDelayMs * Math.Pow(retryOptions.BackoffFactor, attempt - 1);
            await Task.Delay(TimeSpan.FromMilliseconds(delayMs), cancellationToken);
        }
    }

    throw new InvalidOperationException(
        $"MCP-Client-Connect scheiterte nach {retryOptions.MaxRetries + 1} Versuchen gegen '{targetDirectory}'.",
        lastException);
}
```

**Bewusste Designentscheidung:**

- `OperationCanceledException` wird nur dann nicht gefangen, wenn der
  **äußere** Caller-Cancellation-Token gecancelt wurde (User-Abbruch).
  Andernfalls (Timeout via `cts.CancelAfter`) ist es ein flake und
  wird retryt — genau das ist der 010-Volllauf-Flake-Mechanismus.
- Default `MaxRetries = 3` + `BaseDelayMs = 500` + `BackoffFactor = 2.0`
  = 0.5s/1s/2s Backoff = max ~3.5s zusätzliche Wartezeit pro
  `ConnectAsync`-Aufruf im Worst-Case. Bei 16 parallelen
  Test-Collections = 16 × 3.5s = 56s zusätzlich im Worst-Case, im
  Median < 1s pro Aufruf. **Vertretbar** im Vergleich zu 6+ min
  Volllauf.
- `lastException` wird als `InnerException` weitergegeben, damit
  der ursprüngliche Fehler diagnostizierbar bleibt.

**Schritt 3.2 — 3 Fixtures anpassen**

`SymbolGraphMcpFixture.cs:23`, `BaselineMcpFixture.cs:23`,
`McpLiveRepositoryFixture.cs:23` — die `ConnectAsync`-Aufrufe
bekommen einen optionalen 4. Parameter:

```csharp
Client = await McpTestClient.ConnectAsync(Workspace.RootPath, timeoutSeconds: 60,
    retryOptions: new McpTestClientRetryOptions(MaxRetries: 5, BaseDelayMs: 1000, BackoffFactor: 2.0));
```

(Konservativere Retry-Strategie für die Fixtures, weil sie pro
Test-Klasse einmal initialisiert werden und ein Flake hier den
**gesamten** Test-Klassen-Fail bedeutet — 5 Retries mit 1s/2s/4s/8s/16s
= max 31s zusätzliche Wartezeit, im Median < 5s.)

**Schritt 3.3 — Neue Test-Datei `src/AiNetLinter.Tests/Mcp/McpTestClientParallelTests.cs` (NEU)**

1 Integration-Test, sealed, `[Trait("Category", "Integration")]`:

```csharp
[Fact]
public async Task ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly()
{
    using var fixture = new BaselineMiniFixtureWorkspace();
    var tasks = Enumerable.Range(0, 16)
        .Select(_ => McpTestClient.ConnectAsync(fixture.RootPath, timeoutSeconds: 30,
            retryOptions: new McpTestClientRetryOptions(MaxRetries: 2)))
        .ToArray();

    var clients = await Task.WhenAll(tasks);

    // Erwartung: alle 16 erfolgreich (Retry faengt den 010-Flake ab),
    // oder saemtliche Clients ordnungsgemaess disposed.
    Assert.Equal(16, clients.Length);
    foreach (var client in clients)
    {
        await client.DisposeAsync();
    }
}
```

A3: Ohne Retry-Logik (Commit 3 weggelassen) wirft der Test mit
`TaskCanceledException` (genau das 010-Flake-Symptom). Mit
Retry-Logik grün. Coder dokumentiert den wortwörtlichen Failure-
Output im `result.md`.

**Schritt 3.4 — Build + Tests grün**

- `dotnet build AiNetLinter.slnx` 0/0.
- Unit-Slice grün (TD-019 ändert keine Unit-Tests, nur den
  `McpTestClient` + 3 Fixtures).
- Volllauf grün (Last-Test läuft im Volllauf, ist nicht im
  Unit-Slice).
- Optional: 2x Volllauf fahren, um die TD-019-Wirkung zu
  validieren (beide Läufe 1179/1179 grün, kein Flake).

**Schritt 3.5 — Commit 3**

`test(mcp): retry-logik in mcp-test-client gegen parallel-init-flake (TD-019) [codegraph-mcp-server]`

Geänderte Dateien: `McpTestClient.cs`, 3 Fixtures,
`McpTestClientParallelTests.cs` (NEU).
Gezielter `git add` pro Datei.

### Schritt 4 — `tech-debt.md`-Schließungen (vom Coder)

Eine separate Commit am Ende, um die 3 TD-Einträge sauber zu
schließen:

`chore(debt): TD-009 + TD-014 + TD-019 geschlossen durch 011 [codegraph-mcp-server]`

Geänderte Datei: `tech-debt.md` (3 Bodies + 3 Index-Zeilen
aktualisiert, jeweils Commit-Hashes + 011-Verweis).

Aktualisierungen:
- **TD-009** — Status: **geschlossen durch 011** (Commits aus
  Schritt 2.7). Body: kurze Zusammenfassung des Refactors +
  Reserve-Hinweis.
- **TD-014** — Status: **geschlossen durch 011** (Commits aus
  Schritt 1.5). Body: Builder-Pattern + neue Footprint-Verteilung
  dokumentieren.
- **TD-019** — Status: **geschlossen durch 011** (Commits aus
  Schritt 3.5). Body: Retry-Strategie + Last-Test-Verweis.
- **Index-Tabelle**: 3 Zeilen Status-Update.

**Stand nach 011:** 14 → 11 offene TD-Einträge (TD-009, TD-014,
TD-019 geschlossen). Verbleibend: TD-001, TD-002, TD-004, TD-005,
TD-006, TD-007, TD-008, TD-010, TD-011.

### Schritt 5 — Finaler Build + Volllauf (AGENTS.md §2 Pflicht)

- `dotnet build AiNetLinter.slnx` — 0/0.
- `dotnet test AiNetLinter.slnx --no-build --filter "Category=Unit"`
  — Unit-Slice grün (Erwartung: 93+3 (Builder) +1 (Constructor) = 97
  Unit-Tests, sofern keine Tests entfernt).
- `dotnet test AiNetLinter.slnx --no-build` — Volllauf grün
  (Erwartung: 1178 + 1 Last-Test = 1179/1179, in ~6:30-7:00 min).
- **Optional 2. Volllauf**, um TD-019-Wirkung zu bestätigen (kein
  zweiter Flake).

### Schritt 6 — `result.md` schreiben

Standard-Result-Protokoll mit:
- Summary (3 TD-Schließungen + neue Klassen + A3 für ~5 Tests).
- "Was geändert wurde" (3 Commits, ~12-14 Dateien).
- "Commit-Hashes" (1 pro Schritt + 1 für `tech-debt.md`).
- "A3-Nachweis pro TD" (3 Tabellen, ~3 Tests pro TD mit wortwörtlichem
  Failure-Output).
- "Build- und Test-Ergebnis" (Finaler Build, Unit-Slice, Volllauf).
- "Footprint-Tabelle" (vor/nach 011 für die 3 betroffenen Klassen +
  neue Klassen).
- "Plan-Abweichungen" (alle in 7 erlaubten Abweichungen).
- "Commit-Disziplin (A4-Checkliste)".
- "Nächste Aktion des Orchestrators" → Kritiker-Aufruf für 011.

### Schritt 7 — Commit 4 (`chore(task):` Result-Protokoll)

`chore(task): unit 011 result, TD-009 + TD-014 + TD-019 geschlossen [codegraph-mcp-server]`

Geänderte Datei: `units/011/result.md` (NEU).

## 6. Erwartete Tests (A3-Methodik pro TD)

### TD-014 (`McpServerOptionsBuilder`) — 3 Unit-Tests, A3 echt

| Test | A3-Methode | Erwarteter Failure-Output |
|---|---|---|
| `Build_DefaultName_UsesAinetlinter` | Assertion `Assert.Equal("ainetlinter", ...)` → `Assert.Equal("XYZ-rotbiegen", ...)` | `Assert.Equal() Failure: Expected "XYZ-rotbiegen" ... Actual "ainetlinter"` |
| `Build_DefaultVersion_UsesAssemblyVersion` | Assertion `Assert.Equal(asmVersion, ...)` → `Assert.Equal("XYZ", ...)` | `Assert.Equal() Failure` |
| `Build_WithServerInstructions_PropagatesToServerOptions` | `WithServerInstructions("Test")` → `WithServerInstructions("XYZ")` | `Assert.Equal() Failure` |

A3 echt gefahren, Failure-Output im `result.md` wortwörtlich
dokumentiert.

### TD-009 (`McpCodeGraphServer`-Konstruktor) — 1-2 Unit-Tests, A3 echt

| Test | A3-Methode | Erwarteter Failure-Output |
|---|---|---|
| `Constructor_TakesExactlyOneParameter_OfTypeMcpCodeGraphServerOptions` | Konstruktor auf 5-Parameter-Signatur zurückbiegen (manuell oder via `ReplaceAll`) | `Assert.Single(parameters) Failure: Expected 1, Actual 5` |
| `Constructor_AcceptsNullOptions_ThrowsArgumentNullException` (optional) | `ArgumentNullException.ThrowIfNull(options)` auskommentieren | `Assert.Throws<ArgumentNullException> Failure: Expected typeof(System.ArgumentNullException), got typeof(System.NullReferenceException)` |

A3 echt gefahren, Failure-Output im `result.md` wortwörtlich
dokumentiert.

### TD-019 (Retry-Logik) — 1 Integration-Test, A3 echt

| Test | A3-Methode | Erwarteter Failure-Output |
|---|---|---|
| `ConnectAsync_SixteenParallelCalls_AllSucceedOrFailCleanly` | Retry-Loop aus `ConnectAsync` entfernen (try/catch ohne retry), Test fahren | `TaskCanceledException : A task was canceled.` (genau das 010-Flake-Symptom). Mit Retry-Loop grün. |

A3 echt gefahren, Failure-Output im `result.md` wortwörtlich
dokumentiert. Test läuft **nicht** im Unit-Slice, sondern im
Volllauf — Coder muss den 16-parallel-Last-Test explizit fahren
und im `result.md` festhalten.

## 7. Plan-Abweichungen, die explizit erlaubt sind

1. **`McpCodeGraphServerOptions.From(...)`-Helper als
   minimal-invasive Migration** (statt 1:1 `new McpCodeGraphServerOptions
   { Catalog = ..., Console = ..., ... }` an allen 65 Call-Sites).
   Begründung: 65 Call-Sites mechanisch 1:1 zu migrieren ist
   fehleranfällig (Property-Namen, Default-Werte für `MaxLineCount`).
   Der `From`-Helper hat **identische** Parameter-Signatur wie der
   alte Konstruktor (gleiche 5 Parameter, gleiche Defaults), die
   Migration ist damit **wirklich** 1:1. Der Helper ist `static` auf
   dem Record und damit 0 Overhead.

2. **TD-009 + TD-014 in 2 Commits statt 1** (statt ein
   Mega-Refactor-Commit). Begründung: erleichtert die Kritiker-
   Review (Diff pro Commit überschaubar), A4 erlaubt mehrere
   Commits pro Einheit. Commit-Trennung: Schritt 1.5 (TD-014) +
   Schritt 2.7 (TD-009) + Schritt 3.5 (TD-019) + Schritt 4
   (`tech-debt.md`) + Schritt 7 (`result.md`) = 5 Commits.

3. **`ConsoleOverride` bleibt im Record erhalten** (statt als
   deprecated zu markieren oder zu entfernen). Begründung: keine
   Verhaltensänderung, der Parameter hat aktuell keinen
   Produktiv-Caller aber könnte in einem zukünftigen Test-Anlass
   wieder gebraucht werden. Aufräumen wäre eine separate, eigene
   Entscheidung.

4. **`McpServerOptionsBuilder` als `internal sealed class` mit
   public `With*`-Methoden** (statt `internal sealed class` mit
   `internal With*`-Methoden). Begründung: Konsistenz mit dem
   SDK-Pattern (`McpServerOptions` ist public, der Builder ist die
   naheliegende public-Erweiterung). Test ruft `new
   McpServerOptionsBuilder().With...().Build()` auf — public-API
   ist komfortabler.

5. **Retry-Strategie mit exponenziellem Backoff** (statt linearer
   Backoff oder sofortiger Retry). Begründung: 010-Flake ist
   Process-Spawn-Last, kein wiederholbarer Sofort-Retry-Fehler —
   etwas Verzögerung entzerrt parallele Spawns. 0.5s/1s/2s ist
   Standard-Praxis.

6. **`McpTestClientRetryOptions` als public `sealed record`** (statt
   als interner Helper). Begründung: Tests aus
   `McpTestClientParallelTests` und die 3 Fixtures müssen die
   Retry-Optionen explizit setzen können, `public record` ist die
   einfachste API.

7. **3-Fixture-Update mit `timeoutSeconds: 60` + 5 Retries** (statt
   nur Default-3-Retries). Begründung: pro Fixture-Init scheitert
   der **gesamte** Test-Klassen-Fail bei einem Flake — defensiver
   Retry-Plan ist angemessen, der Mehraufwand im Worst-Case (31s
   pro Fixture × ~3 Fixtures parallel = 31s) ist im
   6:30-min-Volllauf vernachlässigbar.

8. **Coder darf `ConsoleOverride` im `McpCodeGraphServerOptions.From`
   entfernen, wenn der Coder in seiner A3-Analyse feststellt, dass
   der Parameter im gesamten Test-Codebase ungenutzt ist** (kein
   Call-Site übergibt ihn). Begründung: weniger toter Code, A3
   bestätigt Verhalten. **Vorab-Pflicht:** `grep "consoleOverride"
   src/AiNetLinter.Tests/` durchführen und im `result.md`
   dokumentieren.

## 8. Bezug zu Projektregeln

| Regel | Datei + Zeile | TD-Bezug |
|---|---|---|
| `sealed` für konkrete Klassen | `AiNetLinter.mdc` Z. 10 | `McpCodeGraphServer` ist bereits `sealed` (bleibt), `McpCodeGraphServerOptions` ist `sealed record` (neu), `McpServerOptionsBuilder` ist `sealed class` (neu), `McpTestClientRetryOptions` ist `sealed record` (neu) — alle 4 Verstöße würden gegen Z. 10 verstoßen. |
| Input-`record` ab 5 Method-Parametern | `AiNetLinter.mdc` Z. 11 | TD-009 löst exakt diesen Fall für `McpCodeGraphServer` (5/5 Constructor-Deps). Vorlage: `TryApplyContentChange` (TD-007) wartet noch auf seinen eigenen Refactor. |
| `#nullable enable` am Dateianfang | `AiNetLinter.mdc` Z. 12 + `AiNetLinterRichtlinien.mdc` §`EnforceNullableEnable` | Alle 4 neuen Dateien + alle modifizierten Dateien müssen `#nullable enable` am Zeile 1 haben. Pflicht. |
| `MaxConstructorDependencies: 5` | `AiNetLinter.mdc` Z. 27 + `rules.json:125` | TD-009 ist **direkt** durch dieses Limit getrieben — aktueller Konstruktor exakt am Limit, nächste Dependency bricht den Build. Nach 011 hat der `McpCodeGraphServer`-Konstruktor **1** Parameter vom Typ `McpCodeGraphServerOptions` → `MaxConstructorDependencies` ist erfüllt mit **maximaler** Reserve. |
| `AIContextFootprint: 2500` | `AiNetLinter.mdc` Z. 15, 28 | TD-014 löst die `McpServerOptionsFactory`-Knappheit. Erwartete neue Verteilung: Builder ~100-130 Z. + Factory ~25-35 Z. — beide weit unter 2500, der Footprint-Druck ist strukturell entkoppelt. |
| `MaxLineCount: 500` (Klassen-File) | `AiNetLinter.mdc` Z. 20 | Alle 4 neuen Dateien (Builder, Options, RetryOptions, ParallelTests) bleiben unter 500 Z. (`McpServerOptionsBuilder` erwartet ~100-130 Z., die anderen < 60 Z.). |
| `MaxMethodParameterCount: 4` | `AiNetLinter.mdc` Z. 22 | `McpCodeGraphServerOptions.From(...)` hat 5 Parameter — **Achtung**: das ist **kein** Konstruktor, sondern eine `static`-Factory-Methode, und `MaxMethodParameterCount: 4` gilt nur für Konstruktoren + Methoden-Signaturen, **nicht** für Factory-Helper. Coder verifiziert per `rg "MaxMethodParameterCount" rules.json` die genaue Scope-Definition. **Falls** die Regel auch Factory-Helper trifft, muss `From` auf einen Init-`record` umgestellt werden (z. B. `McpCodeGraphServerOptionsInit { ... }`) — dann ist die Migration 1:1 nicht mehr ganz so mechanisch, aber strukturell sauber. **Risiko siehe Abschnitt 10.** |
| `MaxDirectoryDepth: 4`, `EnforceNamespaceDirectoryMapping` | `AiNetLinter.mdc` Z. 29, 58 | 4 neue Dateien landen in existierenden Namespaces (`AiNetLinter.Mcp` + `AiNetLinter.Tests.Mcp`), keine neuen Namespaces, keine Directory-Tiefe-Änderung. ✓ |
| `EnforceSealedClasses` | `AiNetLinter.mdc` Z. 65 | Alle 4 neuen Klassen sind `sealed`. ✓ |
| Conventional Commits deutsch imperativ | `AiNetLinterRichtlinien.mdc` §4 | 5 Commits in Schritten 1.5/2.7/3.5/4/7, alle mit `[codegraph-mcp-server]`-Suffix, deutsch, imperativ. |
| Kein `dynamic`, kein `out` außerhalb `Try*` | `AiNetLinter.mdc` Z. 13-14 + 79 | Werden nicht eingeführt. ✓ |
| `EnforceResultPatternOverExceptions` (deaktiviert) | `AiNetLinter.mdc` Z. 78 | TD-019 wirft `InvalidOperationException` als finalen Fallback nach erschöpften Retries — kein Result-Pattern, aber Regel ist explizit deaktiviert ("Linter erzwingt nicht, trotzdem anstreben"). `InvalidOperationException` ist hier angemessen, weil der Retry-Loop-Caller keine Handlungsalternative hat. |

## 9. Tech-Debt-Aktionen (Schließungen pro TD)

| TD | Status vor 011 | Status nach 011 | Schließungs-Commit |
|---|---|---|---|
| **TD-009** | offen (5/5 Konstruktor-Deps am Limit) | **geschlossen** — `McpCodeGraphServer(McpCodeGraphServerOptions)` mit 1 Parameter, ~6+ Properties Reserve | Schritt 2.7 |
| **TD-014** | offen (2484/2500 Z., Puffer 16) | **geschlossen** — `McpServerOptionsBuilder` (100-130 Z.) + `McpServerOptionsFactory` (25-35 Z.), beide weit unter 2500 | Schritt 1.5 |
| **TD-019** | offen (paralleler MCP-Init-Flake) | **geschlossen** — Retry-Logik in `McpTestClient.ConnectAsync` + Last-Test | Schritt 3.5 |
| TD-008 | offen (Pragmatik `PathOverrides: 2700`) | unverändert offen — gehört zu 012 | — |
| TD-010 | offen (`SearchPatternTool` 2482/2500) | unverändert offen — gehört zu 012 (mit TD-008 gebündelt als `ILinterEngineConfig`-Refactor) | — |
| TD-001, TD-002, TD-004, TD-005, TD-006, TD-007, TD-011 | offen | unverändert offen | — |

**Index-Tabelle in `tech-debt.md` (Schritt 4):** 3 Status-Updates.

**Stand nach 011:** 11 offene TD-Einträge (von 14 vor 011).

## 10. Risiken + Bewusst-NICHT-in-011-Liste

### Risiken

| Risiko | Wahrscheinlichkeit | Impact | Mitigation |
|---|---|---|---|
| `MaxMethodParameterCount: 4` greift auch für `static`-Factory-Methoden (TD-009 `From(...)`) | niedrig | mittel (5-Parameter-`From` müsste auf Init-`record` umgestellt werden) | Coder verifiziert Scope der Regel in `rules.json` **vor** Schritt 2.1; bei Trifft-Fall: Init-`record` statt `From`-Methode (Aufwand +1 Commit, +1 Test) |
| Footprint-Wachstum in `McpCodeGraphServer` über 2500 | sehr niedrig (184 → ~210 Z., Puffer 2290) | gering | Pflichtmessung nach Schritt 2.7 |
| Volllauf-Flake trotz Retry-Logik (TD-019 unzureichend) | niedrig | mittel (Test-Fail → Kritiker-`issues`) | 2. Volllauf als Konsistenz-Beweis (Schritt 5 Optional) |
| `McpTestClient`-Aufrufer-API-Bruch durch neuen 4. Parameter | sehr niedrig (Parameter ist **optional** mit Default) | sehr gering | Alle bestehenden Aufrufer kompilieren ohne Änderung weiter — `retryOptions = null` als Default, gleiche Semantik |
| `Configuration.Config`-Property auf `McpCodeGraphServerOptions` zieht weiterhin ~1110 Z. in Tool-Klassen (TD-008/010-Restproblem) | hoch (unverändert) | gering (durch `PathOverrides: 2700` weiterhin aufgefangen) | Bewusst NICHT in 011 — gehört zu 012; die nächsten P0/P1-Extensions brauchen aber zunehmend mehr `McpCodeGraphServer`-Eigenschaften, das Restproblem verschärft sich |
| `Console`-Required-Property in `McpCodeGraphServerOptions` bricht bestehende Tests, die `Console` weglassen wollten | sehr niedrig (Plan-Abweichung 1 löst es über `From`) | sehr gering | `From` normalisiert mit `consoleOverride ?? console ?? LinterConsole.Instance` |
| Re-Run nach TD-019 zeigt, dass Retry-Logik den 010-Flake nicht reproduzierbar behebt (Last-Test grün, aber Volllauf weiter flake) | niedrig | mittel (TD-019 nicht geschlossen) | Coder dokumentiert das ehrlich im `result.md`, TD-019 bleibt offen, Folge-TD-Eintrag mit "Retry half nicht" |

### Bewusst NICHT in 011 (10 Punkte)

1. **TD-008 + TD-010 (`ILinterEngineConfig`-Interface)** — größerer
   Refactor (4-6h), thematisch nicht passend zu TD-009/014/019.
   Gehört zu 012 als eigenständige Einheit.
2. **TD-007 (`TryApplyContentChange` 5-Parameter-Methode)** —
   `private`, `MaxMethodParameterCountForNonPublic: 6` greift, kein
   Build-Druck. Eigenständige Inline-Mitnahme oder Folge-Refactor.
3. **TD-001, TD-002, TD-004, TD-005, TD-006, TD-011** — wie in
   Abschnitt 2 "Warum nicht" begründet.
4. **Keine P0/P1-Erweiterungen** (`--mcp-log`, Auto-Discovery,
   Staleness-`mtime`, Kaltstart, `ILintConsole` für MCP, Last-Fixture).
   011 bereitet **nur** strukturell vor.
5. **Kein Konzept-Drift-Fix** — `konzept.md` bleibt unverändert
   (A7). Die P0/P1-Erweiterungen werden in zukünftigen Einheiten
   kommen und dann Konzept-Aktualisierungen mitnehmen.
6. **Kein `rules.json`-Edit** — keine `PathOverrides`-Wert-Änderung,
   keine Schema-Änderung, keine neuen Regeln (A7).
7. **Keine Doku-Änderungen** — `Docs/**` bleibt unverändert (EPIC-08
   abgeschlossen, keine Doku-Lücke durch 011).
8. **Kein Push, kein Amend, kein Force-Push** (A4).
9. **Kein Edit an `kernel.md` / `agents/*.md` / `.agents/rules/**`**
   (A7, A8).
10. **Keine 4. oder 5. TD-Bündelung** — 3 Pflicht + 1 Optional
    (TD-008/010) ist das absolute Maximum, hier sogar nur 3 Pflicht
    ohne Optional (TD-008/010 explizit ausgeschlossen). 011 ist
    fokussiert, nicht überladen.

## 11. Reihenfolge der TD-Refactors mit Begründung

**Reihenfolge: TD-014 → TD-009 → TD-019 → `tech-debt.md` → `result.md`**

Begründung pro Schritt:

1. **TD-014 zuerst** — am wenigsten invasiv (1 NEU + 1 Modifikation
   + 1 Test-NEU = 3 Dateien, ~150-200 Z. Diff), schafft Footprint-
   Reserve in `McpServerOptionsFactory` für die nächsten P0/P1-
   Erweiterungen, der Coder wendet das Builder-Pattern an, bevor er
   im TD-009-Schritt das `record`-Pattern anwendet. Wenn TD-014
   Probleme macht (z. B. unerwartete API-Brüche), sind sie auf 3
   Dateien begrenzt und der Coder kann gegensteuern, **bevor** die
   65-Call-Site-Migration in TD-009 läuft.

2. **TD-009 als zweites** — der größte Brocken (1 NEU + 1
   Modifikation + 1 Modifikation in `McpServerCommand` + 11
   Test-Datei-Migrationen + 1 Test-NEU = 14 Dateien, ~150-200 Z.
   Diff, ~65 1-Z.-Refactors an Call-Sites). Wenn dieser Schritt
   Probleme macht (z. B. vergessene Call-Site, falscher Default),
   ist der Coder mitten im Hotspot und kann sofort gegensteuern.
   Der `From`-Helper minimiert das Migrations-Risiko.

3. **TD-019 als drittes** — Test-Infrastruktur-Änderung mit dem
   niedrigsten Risiko (Retry-Logik in einer Helper-Methode + 3
   Fixtures + 1 Last-Test = 5 Dateien, ~150-200 Z. Diff). Kommt
   **nach** TD-009/TD-014, weil sein Volllauf-Beweis (1179/1179
   grün, optional 2. Volllauf) von der vollen Test-Suite abhängt —
   wenn TD-009/TD-014 das Test-Set verändert haben, muss der
   Retry-Beweis auf dem **finalen** Test-Set laufen.

4. **`tech-debt.md` als viertes** — rein dokumentarisch, fasst die
   3 TD-Schließungen zusammen. Kommt nach allen 3 Code-Refactors,
   damit die Commit-Hashes in `tech-debt.md` auf die finalen Commits
   zeigen.

5. **`result.md` als fünftes** — Standard-Result-Protokoll, kommt
   nach allen Commits, dokumentiert den finalen Stand.

**Commit-Reihenfolge (A4-konform, alle lokal, kein Push):**

1. `refactor(mcp): mcp-server-options-builder + schlanke factory (TD-014) ...`
   — Schritt 1.5
2. `refactor(mcp): mcp-code-graph-server-konstruktor auf input-record umgestellt (TD-009) ...`
   — Schritt 2.7 (oder 2 Commits, einer für Record + Server + Command,
   einer für 11 Test-Migrationen + ConstructorTest — siehe
   Plan-Abweichung 2)
3. `test(mcp): retry-logik in mcp-test-client gegen parallel-init-flake (TD-019) ...`
   — Schritt 3.5
4. `chore(debt): TD-009 + TD-014 + TD-019 geschlossen durch 011 ...`
   — Schritt 4
5. `chore(task): unit 011 result, TD-009 + TD-014 + TD-019 geschlossen ...`
   — Schritt 7

**Pflicht nach jedem Schritt:** Unit-Slice grün, danach
Schritt-5-Tests grün. Bei Rot: kein nächster Schritt, Kritiker
muss entscheiden (Issues-Verdict wahrscheinlich).

## 12. Aufruf-Budget-Bilanz

| Größe | Default | Stand vor 011 (state.md) | Nach 011 (Planer + Coder + Kritiker) | Verbleibend nach 011 |
|---|---:|---:|---:|---:|
| `max_aufrufe` | 40 | 34 | 37 | **3** |
| `max_fix_pro_einheit` | 3 | 0 (in 006) | 0 (011 hat keine Fix-Runde geplant) | 3 |
| `max_fix_gesamt` | 12 | 1 (002/fix-01) | 1 | 11 |

**Was die 3 verbleibenden Aufrufe nach 011 bedeuten:**

- **Genau 1 weitere Einheit (012)** ODER
- **Task-Abschluss mit `summary.md`** ohne weitere Coder-Einheit.

**Empfehlung an Orchestrator (zur Information, nicht Teil des Plans):**

- **Option A: 012 = TD-008/010 (`ILinterEngineConfig`-Refactor)**
  → schließt das `PathOverrides: 2700`-Pragmatik-Problem strukturell,
  befreit `FindReferencesTool`/`FindSymbolTool`/`SearchPatternTool` von
  der Override-Abhängigkeit, 4-6h Coder-Aufwand. Nach 012: 9 offene
  TD-Einträge, dann `summary.md` möglich.
- **Option B: 012 = A1 (`rules.json`-Auto-Discovery, P0, ~2-3h)**
  → die kleinste P0-Pflicht, vom 010-Kritiker als nächste sinnvolle
  Einheit empfohlen. Nach 012: 11 offene TD-Einträge, dann
  `summary.md` möglich.
- **Option C: 012 = `summary.md` schreiben + Push** → kein
  weiterer Code-Refactor, Task wird mit den 11 verbleibenden TD
  dokumentiert abgeschlossen (Tech-Debt-Liste verbleibt im
  `tech-debt.md` für eine spätere Task-Wiederaufnahme).

**Orchestrator entscheidet** nach 011-Kritiker-Vote. Der Planer
für 011 hat keine Präferenz — 011 ist fokussiert auf die 3 TD-
Schließungen, nicht auf die Strategie danach.

---

## 13. Zusammenfassung in einem Satz

Drei zusammenhängende strukturelle Tech-Debt-Refactors (TD-009
Konstruktor-`record`, TD-014 Factory-Builder-Aufteilung, TD-019
parallele Init-Stabilität) in einer Coder-Einheit, 5 Commits
lokal, 11-12 Dateien + 4 neue Dateien, ~500-700 Z. Diff, 5-6
neue Tests mit A3-Beweis, am Ende 3 TD-Schließungen und 1179+
Tests grün — danach entweder eine weitere Einheit (TD-008/010
oder A1) oder Task-Abschluss.
