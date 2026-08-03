---
status: done
type: step-plan
task: codegraph-mcp-finish
step: 008
title: "ILinterEngineConfig-Interface extrahieren, PathOverride-Liste auf Rest reduzieren (EPIC-03 / Muss-Haben C, TD-008 / TD-010)"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: []
---

# Step 008: `ILinterEngineConfig`-Interface extrahieren + PathOverride-Liste reduzieren

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-03` aus `roadmap.md` — struktureller Tech-Debt-Fix
  `ILinterEngineConfig` (Muss-Haben C, TD-008 / TD-010): schlankes Interface
  für `McpCodeGraphServer.Config` extrahieren, `rules.json`-`PathOverride`-
  Liste (14 Einträge) auf tatsächlich verbleibenden Bedarf reduzieren (mit
  Begründung pro Rest-Override).
- **Konzept-Referenz:** `konzept.md` „Muss-Haben C" (Zeile 263-295) +
  „Entdeckte Mängel/Redundanzen" Abschnitt „`rules.json`-PathOverride-Liste"
  (Zeile 578-587). Konzept-Vorgabe explizit: „Bewusst **vor** Block B
  eingeplant, damit B gegen den entlasteten Footprint umgesetzt wird."
- **Reihenfolge:** direkt nach EPIC-02 (step-007 ist abgeschlossen),
  vor EPIC-04/05/06/07/08. Diese Position ist im Konzept wie im
  `roadmap.md` vorgegeben und wird hier beibehalten — keine
  Reihenfolge-Anpassung.

## Aktueller Projektzustand (JIT-Kontext)

Beim Lesen des aktuellen Stands direkt vor diesem Plan vorgefunden (Stand
2026-08-03, nach step-007/fix-01 `cf3d7ac1`):

1. **`PathOverride`-Liste in `rules.json` (`rules.json:405-476`):**
   exakt **14 Einträge** mit `MaxAIContextFootprint: 2700`, alle in der
   MCP-Implementierung + eine CLI-Ausnahme:

   | Datei | Pfad-Kontext | PfadOverride-Quelle |
   |---|---|---|
   | `src/AiNetLinter/Commands/AuditCommand.cs` | CLI | vorbestehend (nicht aus 011) |
   | `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` | MCP | vorbestehend (nicht aus 011) |
   | `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs` | MCP | vorbestehend (nicht aus 011) |
   | `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` | MCP | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | MCP | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` | MCP-Tool | neu in Commit `8a663c7` |
   | `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` | MCP-Tool | neu in Commit `8a663c7` |

   → Die Konzept-Erwähnung „13 Einträge" ist veraltet; die korrekte
   Anzahl ist 14. Diese 14 Einträge sind genau das, was die Konzept-DoD
   „auf die Fälle reduziert, die der strukturelle Fix nicht lösen kann"
   meint.

2. **Tatsächliche `Config`-Nutzung in den 14 Dateien (per
   `grep "state\.Config|Config\s+\w" src/...`):**

   - **Tatsächliche `Config`-Verwendung (nur 2 Dateien):**
     - `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs:31` —
       `state.Config` an `GetViolationsScanner.BuildViolationsTextAsync`
       durchgereicht (liest `state.Config`, aber nutzt es nicht selbst).
     - `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs:56-61` —
       `Config` an `new LinterEngine(config, ...)`-Konstruktor
       übergeben.
     - `src/AiNetLinter/Commands/AuditCommand.cs:31,39,46,63,156` —
       `Config` im `AuditRunContext`-Record, via `ConfigLoader.TryLoadConfig`
       erzeugt, an `LinterEngine`-Konstruktor + `PlaybookOptions` übergeben.

   - **Nur transitiv, kein tatsächlicher `Config`-Leser (12 Dateien):**
     - 3 Registrar-Klassen (`AnalysisToolRegistrations.cs`,
       `FileStructureToolRegistrations.cs`, `SymbolGraphToolRegistrations.cs`)
     - 8 Tool-Klassen außer `GetViolationsTool`
     - `McpServerOptionsFactory.cs`

     Diese Dateien **lesen** `Config` nirgends. Sie tragen die 2700er-
     Overrides ausschließlich, weil sie den `McpCodeGraphServer`-Typ
     referenzieren und dessen öffentliche `Config`-Eigenschaft vom Typ
     `Config` den gesamten `Configuration`-Namespace in den Footprint
     zieht (verifiziert: `McpCodeGraphServer.cs:62` `public Config Config
     { get; }`, `McpCodeGraphServerOptions.cs:31` `public required Config
     Config { get; init; }`).

3. **`LinterEngine`-Konstruktor (`src/AiNetLinter/Core/LinterEngine.cs:34`):**
   `internal LinterEngine(Config config, string? rulesJsonContent = null,
   IPerformanceProfiler? profiler = null, ILintConsole? console = null,
   LinterArgs? args = null)`. `Config` ist hier **nicht** durch ein
   Interface ersetzbar, weil die Engine an mehreren Stellen die
   Record-Semantik nutzt: `_config with { SolutionBasePath = dir }`
   (Z. 233), `_config.SolutionBasePath` (Z. 230), `_config.TestSentinel.
   TestProjectNameSuffixes` (Z. 142), `_config.FileFilters` (Z. 247),
   sowie durchgereichte `_config`-Referenzen an `ProjectConfigResolver
   .ResolveForDocument` (Z. 272) und `catalog.CollectDocumentWorkItemsAsync`
   (Z. 159). Die Engine selbst ist bereits in
   `rules.json:144-147` (`FootprintIgnoreTypeNames: ["LinterEngine",
   "NamingChecker"]`) — sie zählt also nicht in fremden Footprints, nur
   der `Config`-Parametertyp tut es.

4. **`LinterEngine`-Footprint-Ignore ist die Vorlage für die richtige
   Lösung:** Wenn `Config` ebenfalls in `FootprintIgnoreTypeNames`
   aufgenommen würde, verschwänden alle 14 Overrides strukturell ohne
   Interface-Einführung. Das ist aber **nicht** der vom Nutzer in
   `konzept.md` explizit gewünschte Weg („strukturellen Fix" mit
   Interface-Extraktion, nicht „Whitelist-Erweiterung"). Die
   Interface-Lösung ist sauberer, weil sie `Config` an der Quelle
   (Property-Typ) entkoppelt statt die Messung zu entschärfen. Die
   DoD-Vorgabe „mit Begründung pro verbleibendem Override" funktioniert
   für beide Wege, der Konzept-Text bindet aber explizit auf den
   Interface-Weg.

5. **Test-Surface-Auswirkung (Vorbereitung für den Coder):** Die
   Konstruktor-Signatur-Änderung in `McpCodeGraphServer` und
   `McpCodeGraphServerOptions` zieht **12 Testdateien** im
   `Mcp/`-Testbereich mit (per Grep ermittelt: alle Tool-Tests +
   `McpServerOptionsFactoryTests`, `McpCodeGraphServerConstructorTests`,
   `McpCodeGraphServerTests`, `FindSymbolToolTests`,
   `SearchPatternToolTests`, `GetViolationsToolTests`,
   `GetTypeHierarchyToolTests`, `GetIndexScopeToolTests`,
   `GetImpactToolTests`, `GetHotspotsToolTests`,
   `GetFileSkeletonToolTests`, `FindReferencesToolTests`).
   Konstruktive Migration sollte per `McpCodeGraphServerOptions.From`
   (1:1-Übersetzung, siehe step-007/fix-01) ohne Test-Inhalts-Änderung
   möglich sein, weil `From` weiterhin `Config` entgegennimmt und intern
   zuweist.

6. **TD-008 / TD-010 in `tech-debt.md`:** beide aktuell **nicht** im
   Index gelistet (Index reicht TD-001 bis TD-006, siehe
   `tech-debt.md:24-32`). Sie sind im Konzept als „unverändert aus dem
   alten Tech-Debt-Log, geschätzt 4-6h" referenziert, im aktuellen
   `tech-debt.md` aber nicht eingetragen. Für diesen Step irrelevant —
   EPIC-03 als Ganzes ist die vom Konzept definierte Erledigung beider
   Tech-Debt-Punkte; ein Tech-Debt-Eintrag wäre redundant, wenn der
   Refactor approved ist. Falls der Coder am Ende **keinen** der
   `PathOverride`s auflösen kann (Edge-Case), bleibt TD-008/TD-010 als
   „offen, dieser Step hat [X] nicht erreicht" zurück — dann als
   Tech-Debt-Eintrag ergänzen, nicht als neues Epic. Siehe Notes
   unten.

7. **Konzept-Konformität zur Konzeptzeile 285-292:** „Umsetzung
   (unverändert aus dem alten Tech-Debt-Log, geschätzt 4-6h)" + „interne
   interface ILinterEngineConfig, das nur die von LinterEngine/den Tools
   tatsächlich benötigten Properties exportiert" + „McpCodeGraphServer.
   Config wird vom Interface-Typ statt der konkreten Config-Klasse" +
   „Reduziert im Idealfall die 13 PathOverride-Einträge auf die
   tatsächlich verbleibenden Fälle (mit Begründung pro verbleibendem
   Override, siehe DoD)". Diese drei Vorgaben sind im Plan
   1:1 umgesetzt.

## Intention

Nach diesem Step ist `McpCodeGraphServer.Config` (und analog
`McpCodeGraphServerOptions.Config`) vom Typ eines neuen, schlanken
`internal interface ILinterEngineConfig`, das nur die Properties
exportiert, die `LinterEngine`/die MCP-Tools tatsächlich konsumieren.
Die konkrete `Config`-Klasse implementiert dieses Interface. Damit
verschwindet der `Configuration`-Namespace aus dem transitiven
Footprint jeder Tool-Klasse, die `Config` nicht aktiv liest — was
mindestens 12 der 14 `PathOverride`-Einträge in `rules.json` strukturell
überflüssig macht. Verbleibende Overrides (sofern überhaupt welche
übrigbleiben) werden mit per-Eintrag-Begründung in `rules.json`
dokumentiert, sodass die Liste nicht stillschweigend schrumpft, sondern
begründbar schrumpft.

Die Lösung folgt `konzept.md` Zeile 285-292 (Interface-Extraktion,
nicht Footprint-Ignore-Erweiterung) — auch wenn der
Ignore-Listen-Weg technisch kürzer wäre, würde er die vom Konzept
geforderte strukturelle Entkopplung nicht leisten.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Configuration/ILinterEngineConfig.cs` (NEU)

- **Was:** Neues `internal interface ILinterEngineConfig` anlegen, das
  die Properties exportiert, die der Linter und die MCP-Tools
  tatsächlich lesen. Inhalt abgeleitet aus der LinterEngine-Nutzung
  (siehe Aktueller Projektzustand Punkt 3) und der
  `Config`-Property-Nutzung in den 14 betroffenen Dateien (Punkt 2):

  ```csharp
  namespace AiNetLinter.Configuration;

  /// <summary>
  /// Lese-Sicht auf die Linter-Konfiguration, die der Linter und seine
  /// Konsumenten tatsaechlich benoetigen. Wird von <see cref="Config"/>
  /// implementiert und ermoeglicht es Aufrufern (z. B.
  /// <c>McpCodeGraphServer</c>), die Config-Eigenschaft schmal zu
  /// exposen, ohne den vollstaendigen <c>Configuration</c>-Namespace in
  /// ihren AIContextFootprint zu ziehen.
  /// </summary>
  internal interface ILinterEngineConfig
  {
      GlobalConfig Global { get; }
      MetricsConfig Metrics { get; }
      TestSentinelConfig TestSentinel { get; }
      FileFiltersConfig FileFilters { get; }
      UiSeparationConfig UiSeparation { get; }
      WebConfig Web { get; }
      IReadOnlyDictionary<string, RuleMetadataEntry> RuleMetadata { get; }
      IReadOnlyCollection<NamespaceRule> ForbiddenNamespaceDependencies { get; }
      IReadOnlyDictionary<string, ProjectOverrideEntry> ProjectOverrides { get; }
      IReadOnlyDictionary<string, ProjectOverrideEntry> PathOverrides { get; }
      string? SolutionBasePath { get; }
  }
  ```

  Property-Auswahl **konservativ-pragmatisch**: alle Properties, die
  `Config` heute hat (`Config.cs:7-38`) — bewusst nicht minimaler als die
  aktuelle Konfig-Sub-Struktur, weil `LinterEngine` mehrere
  Sub-Properties indirekt über `ProjectConfigResolver.ResolveForDocument`
  konsumiert (siehe Aktueller Projektzustand Punkt 3) und ein noch
  schmalerer Interface-Scope eine größere
  `LinterEngine`-Refactoring-Welle nach sich zöge (Konzept-Schätzung
  „4-6h" spricht für minimale Engine-Touch-Points). Der eigentliche
  Footprint-Gewinn kommt aus der **Trennung am Verbrauchsort**
  (`McpCodeGraphServer.Config` ist nicht mehr `Config`, sondern das
  Interface) — nicht aus der Interface-Breite.

- **Warum:** Erfüllt die Konzept-Vorgabe
  `konzept.md:286` („interne interface ILinterEngineConfig, das nur die
  von LinterEngine/den Tools tatsächlich benötigten Properties
  exportiert"). Bewusst nicht `public` — der MCP-Server und seine
  Scanner-Klassen sind `internal`, externe Konsumenten sollen weiterhin
  den vollen `Config`-Typ sehen, falls sie ihn je brauchen.

### Datei 2: `src/AiNetLinter/Configuration/Config.cs`

- **Was:** `Config` als Implementierer von `ILinterEngineConfig`
  deklarieren. Die Property-Signaturen sind bereits konform (alle
  Properties haben `get; init;` bzw. `get;` mit den richtigen Typen).
  Änderung am Klassenkopf:

  ```csharp
  public sealed record Config : ILinterEngineConfig
  ```

  → keine Property-Bodies ändern, nur die Interface-Deklaration
  hinzufügen. Implementierung erfolgt implizit über die existierenden
  Property-Definitionen.

- **Warum:** Ohne diese Zeile ist `McpCodeGraphServer.Config` (vom Typ
  `ILinterEngineConfig`) nicht zuweisbar mit `Config`-Instanzen.

### Datei 3: `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`

- **Was:** Property-Typ `Config` → `ILinterEngineConfig` an Zeile 62:

  ```csharp
  // vorher:
  public Config Config { get; }

  // nachher:
  public ILinterEngineConfig Config { get; }
  ```

  Konstruktor (`Z. 33-45`) und `McpCodeGraphServerOptions`-Wiring bleiben
  unverändert — `Config` ist strukturell kompatibel (Record erfüllt
  Interface), die Zuweisung `Config = options.Config;` (Z. 39) ist
  weiterhin gültig.

  Die bestehende XML-Doc an der Property
  (`Z. 56-61`) bleibt inhaltlich richtig (sie spricht über „vollständige
  Linter-Konfiguration", nicht über den konkreten Typ). **Nur** den
  Inline-Cref `<see cref="Config"/>` zu `<see cref="ILinterEngineConfig"/>`
  bzw. zum Record-`<see langword="class"/>`-Verweis prüfen, falls die
  Doku den konkreten Typ nennt — siehe Datei 4 für die Parallel-Änderung.

- **Warum:** Die zentrale Footprint-Entkopplung. 12 der 14
  PathOverride-Dateien greifen ausschließlich über
  `McpCodeGraphServer.Config` (Property-Typ) auf den
  `Configuration`-Namespace zu, ohne die Property zu lesen — die
  Interface-Verschmälerung entkoppelt sie strukturell.

### Datei 4: `src/AiNetLinter/Mcp/McpCodeGraphServerOptions.cs`

- **Was:** Property-Typ `Config` → `ILinterEngineConfig` an Zeile 31
  (analog Datei 3):

  ```csharp
  // vorher:
  public required Config Config { get; init; }

  // nachher:
  public required ILinterEngineConfig Config { get; init; }
  ```

  Die `From()`-Factory (`Z. 40-53`) **muss** `Config` weiterhin
  konstruieren können (Record-Instantiierung in Z. 51), bleibt also
  syntaktisch wie bisher — `Config` ist nach wie vor ein gültiger
  Wert für die Interface-Property. Falls die XML-Doc an der Property
  (Z. 29-30) den konkreten Typ `<see cref="Config"/>` nennt, auf
  `<see cref="ILinterEngineConfig"/>` anpassen (kein Inhalts-Drift,
  nur Typ-Verweis-Update).

- **Warum:** Sonst würde `McpCodeGraphServerOptions.Config` weiterhin
  den vollen `Config`-Typ exposen und der Property-Wechsel in
  `McpCodeGraphServer` würde den `Configuration`-Namespace immer noch
  über den Options-Pfad reinziehen.

### Datei 5: `src/AiNetLinter/Mcp/Tools/GetViolationsScanner.cs`

- **Was:** Scanner-Signatur `BuildViolationsTextAsync(..., Config
  config, ...)` (Z. 43-48) auf `ILinterEngineConfig config` umstellen.
  An der `new LinterEngine(config: config, ...)`-Stelle (Z. 56-61)
  ist das **nicht** ohne Weiteres möglich, weil `LinterEngine` den
  konkreten `Config`-Typ verlangt (Record-Semantik für `with {...}` in
  Z. 233). Daher einer der beiden Wege (Coder-Entscheidung, beide
  sind begründbar):

  - **Weg A (empfohlen, minimal-invasiv):** Scanner behält
    `ILinterEngineConfig config` als Parameter, macht intern
    `var engine = new LinterEngine(config: (Config)config, ...);` —
    ein expliziter Downcast am Call-Site. Begründung im XML-Doc der
    Methode, warum der Cast sicher ist (alle
    `ILinterEngineConfig`-Implementierer im Projekt sind `Config`,
    verifiziert per Grep). Einziger Nachteil: Warnung bei strikter
    Null-Prüfung möglich, daher `ArgumentNullException.ThrowIfNull`
    + Cast sauber in einer Zeile mit `// sichere Implementierung:
    // ILinterEngineConfig wird projektweit nur von Config
    // implementiert`-Kommentar. `GetViolationsTool.cs:31` gibt
    `state.Config` (jetzt `ILinterEngineConfig`) direkt an den
    Scanner weiter — keine weitere Änderung dort nötig.

  - **Weg B (refactor-intensiver, nur falls Coder die Downcast-
    Begründung nicht vertreten kann):** `LinterEngine`-Konstruktor auf
    `ILinterEngineConfig`-Parametertyp umstellen + `_config` intern
    auf `ILinterEngineConfig` umstellen. Erfordert, dass `Config`-
    spezifische Aufrufe (`_config with { SolutionBasePath = dir }`
    in `LinterEngine.cs:233`, `_config.SolutionBasePath != null` in
    Z. 230) auf Interface-konforme Äquivalente umgestellt werden.
    Erweitert den Step-Scope **erheblich** (mehrere Stellen in
    `LinterEngine` + `ProjectConfigResolver` + ggf. weitere
    Konsumenten) und sprengt die Konzept-Schätzung „4-6h". **Nicht
    empfohlen** für diesen Step.

  - **Weg C (Pragmatik-Alternative, falls der Coder Weg A aus
    Striktheits-Gründen ablehnt):** Den konkreten `Config`-Parametertyp
    im Scanner **beibehalten** und stattdessen den `PathOverride` für
    `GetViolationsScanner.cs` (und ggf. `GetViolationsTool.cs`, das
    liest die Property) **dokumentiert beibehalten**. Begründung
    im `rules.json`-Kommentar: Scanner-Pfad ist der einzige
    MCP-Pfad, der `Config` aktiv braucht. Diese Variante verlagert
    das Problem in die Doku, statt es strukturell zu lösen — daher
    nur als Fallback, wenn Weg A aus unerwarteten Gründen scheitert.

  **Coder wählt; der Planer präferiert Weg A** (kleinster Eingriff,
  Konzept-Vorgabe „interne interface" ist mit Downcast am Call-Site
  vereinbar, weil das Interface selbst die Sicherheitsgrenze
  definiert).

- **Warum:** Der Scanner ist der einzige MCP-Punkt, an dem `Config`
  tatsächlich genutzt wird (siehe Aktueller Projektzustand Punkt 2).
  Diese Datei **muss** mit angefasst werden, weil sonst die 2700er-
  Overrides für `GetViolationsTool.cs` + `GetViolationsScanner.cs`
  nicht strukturell auflösbar sind.

### Datei 6: `rules.json`

- **Was:** Die `PathOverrides`-Sektion (Z. 405-476) umstrukturieren.
  Ausgangspunkt: 14 Einträge. Erwartung nach dem Refactor (Schätzung
  auf Basis der Verifizierung in Aktueller Projektzustand Punkt 2):

  - **Erwartung (Weg A oder C erfolgreich):** `GetViolationsScanner.cs`
    und `GetViolationsTool.cs` bleiben ggf. mit Override (Weg A:
    Downcast am Call-Site, Scanner-Override entfällt **falls** der
    Downcast die Footprint-Spur von `Config` unterbricht — das hängt
    von der konkreten Linter-Implementierung ab und ist erst nach
    Build-/Lint-Lauf verifizierbar). **Wahrscheinlich** bleiben 0-2
    Einträge übrig.

  - **Konkrete Aktion in diesem Step:**
    1. Den `dotnet build`-Output laufen lassen und die resultierende
       Lint-Warnung-Liste (Pflicht-Verifikation, DoD) für die 14
       Dateien auswerten.
    2. Für jeden Eintrag, der nach dem Refactor **keine** Warnung
       mehr auslöst: ersatzlos entfernen.
    3. Für jeden Eintrag, der nach dem Refactor **weiterhin** eine
       Warnung auslöst: mit einem JSON-Kommentarfeld (oder einer
       Begleitnotiz, falls JSON-Kommentare nicht erlaubt sind) die
       Begründung des Verbleibs dokumentieren — pro Eintrag genau
       eine Zeile, warum dieser konkrete Override nicht strukturell
       lösbar war. Begründung muss lauten „Datei X greift aktiv auf
       Y zu, Downcast/Refaktor wäre out-of-scope dieses Steps" oder
       ähnlich konkret.

  - **Falls JSON-Kommentare nicht unterstützt werden** (Standard-
    `System.Text.Json` und `Newtonsoft.Json` lehnen Kommentare ab,
    das Linter-Parsing dieser Datei ist im Konzept nicht spezifiziert):
    Begründungen in **einer separaten Sektion** unter
    `PathOverrides` ablegen, z. B. ein neuer Schlüssel
    `"PathOverrideBegruendungen": { "src/.../Foo.cs": "..." }`.
    Konvention mit dem Coder abstimmen.

  - **Falls alle 14 Einträge strukturell auflösbar sind:** die
    gesamte `PathOverrides`-Sektion kann leer bleiben oder leerer
    Block `{}` sein — kein Pflicht-Erhalt.

- **Warum:** Erfüllt die Konzept-DoD
  (`konzept.md:646-649`): „`ILinterEngineConfig`-Refactor (C)
  umgesetzt: `rules.json` `PathOverride`-Liste ist auf die Fälle
  reduziert, die der strukturelle Fix nicht lösen kann (falls
  vorhanden, mit Begründung pro verbleibendem Override dokumentiert)."

### Datei 7: Test-Surface-Anpassungen

- **Was:** 12 Testdateien (siehe Aktueller Projektzustand Punkt 5)
  müssen kompilieren. Da `McpCodeGraphServerOptions.From(...)` weiterhin
  `Config` als Parameter akzeptiert (Factory bleibt), ist die
  wahrscheinlichste Migrationsform: keine Test-Inhalts-Änderung,
  nur Build-Verifikation. Falls Tests **direkt** auf die
  `Config`-Property der `McpCodeGraphServer`-Instanz zugreifen
  (z. B. `.Config` als `Config` typisiert), muss entweder:
  - der Test `state.Config as Config` schreiben (Downcast, gleich
    wie Scanner), oder
  - der Test das Interface nutzen (wahrscheinlich kein Test tut das
    heute — `Config` ist heute als Property-Setter über
    `McpCodeGraphServerOptions.From` versorgt, nicht direkt gelesen).

  Erwartung: **keine** Test-Inhalts-Änderung nötig, nur
  Build-Verifikation. Falls der Build wegen Property-Typ-Wechsel
  fehlschlägt: per minimal-invasivem Cast an den Aufrufstellen
  fixen (im XML-Doc der Test-Methode kurz kommentieren).

- **Warum:** Erfüllt die Zero-Warning-Direktive
  (`AiNetLinterRichtlinien.mdc` §5) — Test-Build muss ohne neue
  Warnungen grün sein, sonst ist der Refactor nicht konform.

### Optional: Begleitende `Docs/`-Aktualisierung

- **Was:** `Docs/configuration.md` (falls vorhanden) prüfen — wird
  `Config` dort als öffentliche API dokumentiert? Falls ja: klarstellen,
  dass `ILinterEngineConfig` die schmale Lese-Sicht ist. Falls nicht:
  keine Aktion.
- **Warum:** Konsistenz mit `konzept.md` Zeile 90-91 (Update-Pflicht
  bei Konfig-Änderungen). Wahrscheinlich kein Update nötig — die
  öffentliche `Config`-API bleibt unverändert, nur die interne
  Property-Typisierung ändert sich.

## Tests

- [ ] **Build grün mit 0 Warnungen** (Zero-Warning-Direktive,
      `AiNetLinterRichtlinien.mdc` §5) — `dotnet build AiNetLinter.slnx`
- [ ] **Volllauf grün** mit der gleichen Testzahl wie in
      `step-007/fix-01/step-result.md` = 1186 — `dotnet test
      AiNetLinter.slnx --no-build`. Falls die TD-005-Last-Flake
      (`McpServerCommandErrorHandlingTests` × Gate-Sättigung) wieder
      auftritt: wie in `step-007/fix-01` als **infrastructure**
      behandeln (kein Fix-Versuch, Scope-Drift vermeiden), im
      `step-result.md` unter „Bekannte Unschärfen" vermerken.
- [ ] **Footprint-Verifikation:** `dotnet build` läuft den Linter
      mit. Die Linter-Warnungen für `MaxAIContextFootprint` müssen
      für **jede** der 14 vormals override-Datei explizit
      gegengeprüft werden:
      - **Keine Warnung** + Override steht in `rules.json` → der
        Override ist toter Ballast, **muss entfernt** werden.
      - **Warnung** + Override steht in `rules.json` → der
        Override ist aktiv, mit Begründung dokumentieren (Datei 6).
      - **Warnung** + Override wurde entfernt → Build bricht ab,
        Override **muss wieder rein** (oder anderer Fix).
- [ ] **Vorhandene Unit-Tests für `McpCodeGraphServer`/`Config`-
      Interaktion** (per `grep` in Tests-Projekt ermittelt, vermutlich
      `McpCodeGraphServerConstructorTests`, `McpCodeGraphServerTests`,
      `GetViolationsToolTests`) müssen ohne Test-Inhalts-Änderung
      grün laufen — die Migration ist rein typ-, nicht
      verhaltensbezogen.
- [ ] **Optionaler Smoke-Test:** `McpLiveRepositoryTests` (Dogfooding
      gegen das eigene Repo) muss weiterhin grün laufen — das ist
      die End-zu-End-Bestätigung, dass `get_violations` (der einzige
      Tool-Pfad, der `Config` aktiv nutzt) mit dem neuen Interface-
      Typ funktioniert.

## Definition of Done

- [ ] `ILinterEngineConfig.cs` existiert mit den in Datei 1
      spezifizierten Properties
- [ ] `Config.cs` deklariert `ILinterEngineConfig`-Implementierung
- [ ] `McpCodeGraphServer.Config` ist vom Typ `ILinterEngineConfig`
- [ ] `McpCodeGraphServerOptions.Config` ist vom Typ
      `ILinterEngineConfig`
- [ ] `GetViolationsScanner.BuildViolationsTextAsync` nutzt das
      Interface (Weg A bevorzugt) oder behält `Config` (Weg C, mit
      Doku-Begründung)
- [ ] `rules.json` `PathOverrides`-Sektion auf das tatsächlich
      verbleibende Minimum reduziert, mit per-Eintrag-Begründung
      für alle verbleibenden Einträge (oder leerer Block, falls
      alle strukturell auflösbar)
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün,
      Zero-Warning-Direktive eingehalten
- [ ] Test-Command aus Tech-Stack-Notiz grün (1186 Tests oder
      begründete Abweichung bei Last-Flake)
- [ ] `McpLiveRepositoryTests` (Dogfooding) grün
- [ ] Code-Commit auf aktuellem Branch (Conventional Commit auf
      Deutsch, imperativ, Task-Suffix `[codegraph-mcp-finish]`)
- [ ] Doku-Commit auf aktuellem Branch (`step-plan.md`-Status-Update
      + `step-008/step-result.md`)
- [ ] `step-008/step-result.md` geschrieben mit:
      - Vorher-/Nachher-Zählung `PathOverride`-Einträge
      - Per-Override-Begründung für alle verbleibenden Einträge
      - Entscheidung Weg A / B / C in Datei 5 (kurz begründet)
      - Footprint-Messung (Warnungs-Output `dotnet build` für die
        14 Dateien)
- [ ] **Kein Push** in diesem Step (Orchestrator-Konvention, lokale
      Commits nur)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (Grundprinzipien:
  „Einfachheit vor Abstraktion", „Immutability & Performance") — die
  Interface-Einführung ist eine echte Abstraktion mit klarem
  Mehrwert (Footprint-Entkopplung), nicht vorzeitige Indirektion.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §2 (Architektur-Verbote:
  „Kein DI-Container", „monolithisch & schlank") — das Interface
  ist eine reine Property-Typ-Verschmälerung, **kein** DI-Container,
  **kein** Plugin-Mechanismus, fügt keine Laufzeit-Indirektion ein.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Qualitätsdrift-
  Prävention) — Zero-Warning-Direktive ist die primäre Verifikation;
  Kommentar-Regel betrifft die XML-Doc-Updates an den Properties
  (`<see cref="..."/>`-Verweise ggf. anpassen, keine TD-/Plan-
  Artefakte im Code).
- `.agents/rules/AiNetLinter.mdc` (auto-generiert) Zeile 28
  (`MaxAIContextFootprint: 2500`) und Zeile 142-147
  (`FootprintIgnoreTypeNames`) — die existierende
  `LinterEngine`-Whitelist ist die Vorlage für die Argumentation,
  warum `Config` **nicht** einfach zusätzlich gewhitelisted wird
  (siehe Aktueller Projektzustand Punkt 4).

## Bekannte Ausnahmen

- **TD-005 (Last-Flake in `McpServerCommandErrorHandlingTests`):**
  kann unter Volllauf-Last weiterhin 1-2 Failures am
  `SubprocessConcurrencyGate.AcquireAsync`-Timeout produzieren —
  ist in `tech-debt.md` dokumentiert, nicht Scope dieses Steps
  (siehe `step-007/fix-01/step-result.md` Klassifikation
  „infrastructure"). Falls der Build/Volllauf dadurch nicht grün
  wird: wie in `step-007/fix-01` mit dreimaligem Re-Run
  klassifizieren, nicht eigenhändig fixen.
- **Build-Linter kann restriktiver sein als erwartet:** falls
  nach dem Refactor **mehr** Warnungen auftreten als vorher
  (z. B. weil der Interface-`Config`-Downcast in
  `GetViolationsScanner.cs` eine zusätzliche
  `ILinterEngineConfig`-Reichweite in den Footprint zieht), muss
  der Coder Weg C wählen und die verbleibenden Overrides
  dokumentieren. **Kein Scope-Drift** in einen
  `LinterEngine`-Refactor hinein (das wäre Weg B).
- **`McpLiveRepositoryTests` (Dogfooding):** läuft als
  Integration-Test, kann länger dauern (~mehrere Minuten). Falls
  der Volllauf dadurch auf > 5 min kommt: vor `dotnet test … --no-build`
  prüfen, ob `Get-Process AiNetLinter,testhost` offene
  Kind-Prozesse hinterlassen hat (Konzept-Warnung), ggf. vorher
  beenden.

## Code-Skizze (optional)

Verdeutlichung der Scanner-Migration (Weg A):

```csharp
// vorher (GetViolationsScanner.cs:43-61):
internal static async Task<string> BuildViolationsTextAsync(
    Solution solution,
    Config config,
    ILintConsole console,
    string? scopeFilter,
    CancellationToken ct)
{
    // ...
    var engine = new LinterEngine(
        config: config,
        // ...
    );
    // ...
}

// nachher (Weg A):
internal static async Task<string> BuildViolationsTextAsync(
    Solution solution,
    ILinterEngineConfig config,
    ILintConsole console,
    string? scopeFilter,
    CancellationToken ct)
{
    // sichere Implementierung: ILinterEngineConfig wird projektweit
    // ausschliesslich von Config implementiert (verifiziert per
    // Grep), der Downcast ist daher nicht spekulativ.
    var concreteConfig = (Config)config;

    // ...
    var engine = new LinterEngine(
        config: concreteConfig,
        // ...
    );
    // ...
}
```

## Notes

- **Schritt-Größe:** EPIC-03 ist als Ganzes in einem Step abbildbar,
  weil die Konzept-Vorgabe „4-6h" gut zu den hier spezifizierten
  5-6 Datei-Touch-Points passt. Falls der Coder beim Bauen merkt,
  dass der Refactor wegen unerwarteter Touch-Points (z. B. weitere
  `Config`-Konsumenten, die beim Grep nicht aufgefallen sind)
  größer wird: **frühzeitig** stoppen, Status auf
  `done (partial)` setzen, im `step-result.md` dokumentieren, das
  Epic im `roadmap.md` als „teilweise umgesetzt → step-009
  erforderlich" markieren — kein Scope-Drift in diesem Step. Der
  Loop-Guard fängt das in der nächsten Planer-Runde auf.

- **TD-008 / TD-010 Sichtbarkeit:** sind im aktuellen
  `tech-debt.md`-Index nicht eingetragen. Falls dieser Step
  approved wird, sind sie inhaltlich erledigt. Der Coder braucht
  **keine** Tech-Debt-Einträge dafür anzulegen (Konzept-DoD
  „alle in D gelisteten Tech-Debt-Einträge sind entweder
  geschlossen oder bewusst mit Begründung zurückgestellt"
  bezieht sich auf EPIC-07, nicht hier). Falls der Coder den
  Refactor nur teilweise schafft: TD-008/TD-010 als neue
  Tech-Debt-Einträge im aktuellen `tech-debt.md` ergänzen, **nicht
  ohne Nutzer-Entscheidung** als Epic in `roadmap.md` (das
  verstößt gegen `spec.md` §8.3/§9).

- **Nicht als Epic-Erweiterung interpretieren:** falls beim Bauen
  auffällt, dass die 14 Overrides auf z. B. 6 statt 0-2 schrumpfen
  und die restlichen 6 unerwartet sind: das ist **kein** Anlass
  für ein neues Epic „Rest-PathOverrides-Hunt". Der Planer im
  nächsten Schritt-Modus-Aufruf sieht den `step-result.md`-Stand
  und entscheidet selbst, ob ein Folge-Step nötig ist.

- **Commit-Strategie:** Zwei lokale Commits in dieser Reihenfolge
  (gemäß `spec.md` §10.3):
  1. **Code-Commit** (Coder) — die eigentlichen Refactor-Änderungen
     in den 5-6 Dateien + `rules.json`-Bereinigung. Conventional
     Commit auf Deutsch, imperativ, mit Task-Suffix
     `[codegraph-mcp-finish]`. Beispiel:
     `refactor(mcp): ilinterengineconfig-interface einfuehren und pathoverrides reduzieren [codegraph-mcp-finish]`.
  2. **Doku-Commit** (Coder) — Status-Update in diesem
     `step-plan.md` (von `in_progress` auf `done (pending audit)`)
     + `step-008/step-result.md`. Conventional Commit, Beispiel:
     `docs(task): step-008 abgeschlossen [codegraph-mcp-finish]`.

- **Push:** keiner. Der Nutzer pusht selbst, gemäß
  `spec.md` §10.3 und Orchestrator-Konvention für diesen Task.
