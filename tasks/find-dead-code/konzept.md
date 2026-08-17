---
status: draft
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-17
open_questions:
  - "Hybrid-Strategie: Compilation.GetDiagnostics() mit aktivierten CS0169/CS0414/IDE0051/IDE0052 als zusätzliche Datenquelle statt/mit eigener DataFlowAnalysis?"
  - "Strukturelle Lücken (Reflection, DI, JSON-Serializer, ASP.NET-Routing) im Output dokumentieren oder über zusätzliche Pattern-Listen versuchen abzudecken?"
  - "Test-Methoden-Whitelist: per Default aktiv (über [Fact]/[Theory]/[Test]/[TestMethod]) oder Filter-Flag?"
---

# Konzept: `find_dead_code` — MCP-Tool für Dead-Code-Detection

## Ziel (Was)

Ein neues MCP-Tool, das in der geladenen Solution Symbole (Klassen, Records, Enums, Methoden, Felder, Properties, Events) und (optional) Variablen/Lokale/const-Felder findet, auf die **innerhalb der geladenen Solution nicht referenziert wird**. Treffer werden mit klaren Confidence-Stufen ausgegeben — `high` (private/internal ohne Referenzen, mit hoher Wahrscheinlichkeit toter Code) und `low` (public, könnte von extern referenziert werden — der Nutzer entscheidet).

## Warum / Kontext

Drift-Loop-Coder-Aufgaben enthalten "entferne toten Code" regelmäßig — Refactorings, Aufräumen vor PR, Drift-Korrektur. Aktuell muss der Agent das **manuell** machen: einzeln `find_references` aufrufen, manuell iterieren, raten. Das ist N Calls pro vermutet totem Symbol, plus Heuristik-Bauchgefühl. Ein dediziertes Tool, das die Solution einmal durchscannt und Treffer mit Confidence-Level liefert, ersetzt das durch 1 Call.

**Status in der Roadmap:** In `tasks/features/05-roadmap.md` nicht eingeplant (weder MUST/SHOULD/NICE noch bewusst gestrichen in `06-nicht-umsetzen.md`) — neue Idee, die im aktuellen Recon-Stand fehlt. Strategischer Nachbar: `pattern_detect` (S2.2) macht etwas Ähnliches (gruppiert Lint-Verstöße), `find_magic_values` macht einen anderen On-Demand-Audit. Beide sind fertig → Tool-Familie ist etabliert.

**Marktdifferenzierung:** CodeGraph hat keine dedizierte Dead-Code-Detection, andere Roslyn-MCP-Server (siehe `tasks/features/03-market-research.md`) auch nicht. Roslyn liefert `SymbolFinder.FindReferencesAsync` (Symbol-Referenzen) und `SemanticModel.AnalyzeDataFlow` (Variablen/Lokale) — beides nativ nutzbar. Roslyns eingebaute Diagnostic-Analyzer IDE0051/IDE0052 (Private member 'X' is unused / Remove unread private member) sind vorhanden, aber wir exposen sie aktuell nicht (kein Treffer im Code auf diese IDs).

## Scope

### Muss-Haben

- Tool `find_dead_code` registriert in `SymbolGraphToolRegistrations` (neben `find_symbol`/`find_references`).
- Iterativer Sweep über alle deklarierten Symbole in der geladenen Solution (Klassen, Records, Structs, Enums, Interfaces, Methoden, Properties, Felder, Events, Delegates).
- Pro Symbol: `SymbolFinder.FindReferencesAsync` → 0 Treffer = potenziell tot.
- Klassifikation in zwei Stufen:
  - **`high`** (private/internal, nicht in `public` API-Surface, keine der unten gelisteten Attribute): sehr wahrscheinlich tot.
  - **`low`** (public/protected, oder internal mit `InternalsVisibleTo` auf eine externe Assembly, oder internal in einer Library-Assembly die als NuGet veröffentlicht wird): "nicht in Solution referenziert" — User-Entscheidung.
- Filter-Parameter `include_public: bool` (default `false`): wenn `false`, werden nur `high`-Treffer geliefert; wenn `true`, zusätzlich `low`-Treffer.
- Filter-Parameter `scope` (analog `get_violations`): Subset der Solution.
- Filter-Parameter `kind`: einschränken auf `class | method | field | property | event | all` (default `all`).
- Filter-Parameter `mode`: `members` (default) | `locals` | `both` (siehe Nice-to-Have für `locals`).
- Structured Output (JSON Schema 2020-12): `{ deadSymbols: [{ id, kind, containerType, file, line, accessibility, confidence, reason, exemptReason? }], summary: { high, low, byKind, scannedSymbols, exemptCount }, limits: string[] }`. `limits` listet die strukturellen Lücken auf (siehe "Strukturelle Lücken" unten), damit der Agent weiß, was das Tool NICHT erkennen kann.
- Sufficiency-Hinweis: "Diese Daten sind vollstaendig fuer die geladene Solution" (analog `find_references`).
- **Compiler-/Reflection-Whitelist** (immer ausgenommen, nie als tot markiert — ohne diese ist das Tool im Eigengebrauch unbrauchbar, weil massenhaft compiler-generated Symbole als tot gemeldet würden):
  - `IsImplicitlyDeclared == true` (fängt ab: Auto-Property-Backing-Fields, Record-Equality-Methoden (`Equals`/`GetHashCode`/`ToString`/`PrintMembers`/`Deconstruct`/`<Clone>$`), Lambda-Display-Klassen, Iterator-State-Machines (`<MethodName>d__*`), Async-State-Machines, Primary-Constructor-Capture-Felder, `init`-only/Required-Helpers).
  - `MethodKind.StaticConstructor` (`.cctor`) — wird implizit aufgerufen.
  - `MethodKind.Destructor` (Finalizer `~Foo()`) — wird vom GC aufgerufen.
  - `MethodKind.PropertyGet` / `MethodKind.PropertySet` / `MethodKind.EventAdd` / `MethodKind.EventRemove` / `MethodKind.EventRaise` (Property/Event-Accessor-Symbole — direkter Property-Zugriff ruft sie auf).
  - Operator-Overloads (`op_*`) — Compiler synthetisiert die Aufrufe; Roslyn erkennt das meist, aber defensiv whitelisten.
  - Attribute mit `[Conditional(...)]` (analog Konzept-V1).
  - Methoden/Properties mit `[ModuleInitializer]` (impliziter Runtime-Aufruf).
  - Methoden mit `[DllImport]` / `IsExtern == true` (PInvoke).
  - Test-Methoden mit `[Fact]` / `[Theory]` / `[Test]` / `[TestMethod]` (Test-Runner-Aufruf per Reflection).
  - Reflection-Marker-Attribute (für MCP-Server / MEF / ähnliche Plugin-Systeme): `[McpServerTool]`, `[McpTool]`, `[Export]`, `[Import]`, `[Plugin]`-artige. **Bewusst erweiterbar** über Whitelist-Konstante, weil das projektspezifisch ist.
  - `[InternalsVisibleTo]`-Assembly-Whitelist: Wenn eine Solution-Assembly `InternalsVisibleTo` auf eine Test-Assembly hat, werden `internal`-Symbole, die nur von der Test-Assembly referenziert werden, **nicht** als `high` markiert, sondern als `low` mit `exemptReason: "InternalsVisibleTo"`. Konkret relevant für AiNetLinter selbst: `LinterEngine.cs:18-20` deklariert `InternalsVisibleTo` auf `AiNetLinter.FastTests`/`IntegrationTests`/`TestKit` — wenn wir das Tool darauf selbst anwenden, würden sonst dutzende `internal`-Helfer fälschlich als tot gemeldet.
  - Konstruktoren (Roslyn unterscheidet `IMethodSymbol.MethodKind == Constructor` — bewusst nicht als tot werten, auch wenn keine direkten Aufrufer; `new Foo()` ist die übliche Nutzung, aber `Activator.CreateInstance(typeof(Foo))` und DI-Container umgehen das — siehe Lücken).
  - Symbole, die in XML-Doc-Kommentaren referenziert werden (`<see cref="...">`).
- Tests: 5+ Unit-Tests (verschiedene Confidence-Stufen, Filter-Kombinationen, Edge-Case-Whitelist), 1 Integration-Test auf Live-Repo (AiNetLinter-Repo selbst — dort gibt's garantiert echte Treffer, plus dokumentierte `InternalsVisibleTo`-Treffer als `low`).

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

- **Variablen-Modus** (DataFlowAnalysis pro Methode): findet ungenutzte lokale Variablen, ungenutzte Parameter (außer in `out`/`ref`/Discard-Pattern), ungenutzte `const`-Felder. Aufwendiger, weil pro Methode einmal SemanticModel.AnalyzeDataFlow → in großen Methoden spürbar. Eigener Parameter `mode: "members" | "locals" | "both"` (default `members`).
- **Hybrid-Strategie mit Roslyn-Compiler-Warnings:** Statt (oder zusätzlich zu) eigener `DataFlowAnalysis` aktiviert der Scanner `Compilation.GetDiagnostics()` mit `SpecificDiagnosticOptions` für:
  - `CS0169` "The private field 'X' is never used"
  - `CS0414` "The field 'X' is assigned but its value is never used"
  - `IDE0051` "Private member 'X' is unused" (Roslyn-IDE-Warnung, oft default off)
  - `IDE0052` "Remove unread private member 'X'"
  - `IDE0044` "Make field readonly" (verwandt, oft gleiche Wurzel)

  Diese Warnungen sind Roslyns eigene, kampferprobte Implementierung — exakt die Edge-Cases, die wir selbst schwer abdecken (Lambda-Discards, Field-Readonly-Mismatch, generierte Display-Classes), sind dort schon berücksichtigt. **Vorteil:** weniger eigener Code, Roslyn-Updates bringen Verbesserungen kostenlos. **Nachteil:** Compiler-Version-abhängig, ggf. unterschiedlich pro Solution aktiv/inaktiv (Default-Warning-Levels), weniger Kontrolle über Output-Format. Implementierungs-Skizze: `CSharpCompilationOptions.WithSpecificDiagnosticOptions(...)` mit `ReportDiagnostic.Hidden` für die fünf IDs, dann `compilation.GetDiagnostics()` filtern und in den Output integrieren.

- **Effektiv-private Heuristik:** ein `public` Symbol, das nur von anderen `public`-Symbolen in der eigenen Assembly referenziert wird, die ihrerseits ungenutzt sind → rekursive "transitive tote Inseln" erkennen. Erweitert das um `internal`-Symbole, die nur von anderen ungenutzten erreicht werden.
- **Grouped Output nach Datei** (analog `pattern_detect` Summary): für Audit-Workflow "welche Dateien würden durch Aufräumen am stärksten schrumpfen".
- **`--dead-code-only`**-Option für `dotnet run` (CLI): gleiche Logik wie MCP-Tool, aber als Datei-Audit-Output — sinnvoll für CI-Integration.

### Nice-to-Have (Zwischenspeicher — vor `status: ready` aufgelöst)

- **Variablen-Modus** (DataFlowAnalysis pro Methode): findet ungenutzte lokale Variablen, ungenutzte Parameter (außer in `out`/`ref`/Discard-Pattern), ungenutzte `const`-Felder. Aufwendiger, weil pro Methode einmal SemanticModel.AnalyzeDataFlow → in großen Methoden spürbar. Eigener Parameter `mode: "members" | "locals" | "both"` (default `members`).
- **Effektiv-private Heuristik:** ein `public` Symbol, das nur von anderen `public`-Symbolen in der eigenen Assembly referenziert wird, die ihrerseits ungenutzt sind → rekursive "transitive tote Inseln" erkennen. Erweitert das um `internal`-Symbole, die nur von anderen ungenutzten erreicht werden.
- **Grouped Output nach Datei** (analog `pattern_detect` Summary): für Audit-Workflow "welche Dateien würden durch Aufräumen am stärksten schrumpfen".
- **`--dead-code-only`**-Option für `dotnet run` (CLI): gleiche Logik wie MCP-Tool, aber als Datei-Audit-Output — sinnvoll für CI-Integration.

### Non-Goals (bewusst NICHT Teil davon)

- **Auto-Fix / Auto-Delete** (auch via `preview_refactor`): explizit ausgeschlossen — wäre Mutation auf der Platte. AiNetLinter bleibt Verifikations-Gatekeeper, der Coder/Agent entscheidet. Begründung: gleiches Argument wie die Streichung von `preview_refactor` in `06-nicht-umsetzen.md` §3 (read-only-Architektur).
- **Cross-Solution / NuGet-Consumer-Analyse:** wir können nicht wissen, ob eine `public`-Methode aus einer anderen Solution aufgerufen wird. Wir markieren das ehrlich als `low` und überlassen die Entscheidung dem User. Eine echte Analyse würde NuGet-Referenz-Graph erfordern — eigenständiges Vorhaben (siehe M2 `dependency_graph`, abgeschlossen in der Roadmap, aber noch nicht für Consumer-Use-Cases erweitert).
- **Source-Generator-Output-Tracking:** Generierter Code kann statische Methoden/Properties aus Source-Assemblies referenzieren, die Roslyn als ungenutzt darstellt. Würde zu vielen False-Positives führen. Erkennung: `Symbol.IsInSource == false` oder `IPropertySymbol.GetMethod?.DeclaringSyntaxReferences` zeigt auf generierte Dateien → als `low` whitelisten, nicht beheben.
- **Reflection-Aufrufer:** `Type.GetMethod("Foo")` oder `Activator.CreateInstance(typeof(T))` können nicht strukturell erkannt werden. Wäre `provenance: heuristic`-Arbeit analog CodeGraph — wir liefern `provenance: roslyn-symbolic` (siehe `00-master-overview.md` §4.3), das schließt Reflection aus.
- **Multi-Sprachen-Totcode** (VB.NET, F#): AiNetLinter ist C#-pur (siehe `06-nicht-umsetzen.md` §8).

## Strukturelle Lücken — was das Tool NICHT erkennen kann

Diese Patterns sind **nicht durch strukturelle Roslyn-Statische-Analyse** erkennbar (Roslyn sieht die Aufrufe nicht, weil sie zur Laufzeit per Reflection oder Framework-Magic entstehen). Das Tool meldet für betroffene Symbole ggf. fälschlich `high`. Das ist **kein Bug, sondern eine fundamentale Grenze** — würde sich nur durch eine dynamische Analyse (Profiling, Coverage-Daten) lösen lassen, die AiNetLinter bewusst nicht macht. Die Liste wird im Tool-Output unter `limits[]` angezeigt, damit der Agent weiß, was er manuell validieren muss.

| Pattern | Warum nicht erkennbar | Beispiel | Mitigation |
|---------|----------------------|----------|------------|
| `Type.GetMethod("Foo")` / `MethodInfo.Invoke` | String-basierte Reflection, Roslyn sieht nur den String | `typeof(Foo).GetMethod("Bar")?.Invoke(...)` | manuell validieren, ggf. `[InternalsVisibleTo]`-Hinweis |
| `Activator.CreateInstance(typeof(T))` | Type-Symbol wird zwar referenziert, aber der "wird instanziiert"-Pfad fehlt | `Activator.CreateInstance(typeof(MyClass))` | siehe oben |
| DI-Container-Registrierung | Container ruft Konstruktoren/Methoden per Reflection auf | `services.AddTransient<IFoo, Foo>()` | siehe oben |
| JSON-Serializer (System.Text.Json, Newtonsoft) | Serializer liest public Properties/Fields per Reflection | `JsonSerializer.Serialize<Foo>(foo)` | public properties gelten ggf. fälschlich als `low` |
| ASP.NET MVC/WebAPI Controller-Routing | Routing liest Controller-Methoden per Reflection | `[HttpGet("foo")] public IActionResult Foo()` | siehe oben, ggf. `[McpServerTool]`-Analogon für `[Http*]`-Attribute als Hinweis |
| Minimal-API Handler | Lambda wird registriert, Roslyn erkennt den Lambda-Body | `app.MapGet("/foo", (FooService s) => ...)` | der Lambda-Body selbst ist sichtbar, der Handler-Pfad nicht |
| Blazor Component-Methoden | Blazor ruft `[Parameter]` und `[JSInvokable]` per Reflection | `[Parameter] public string Foo { get; set; }` | ggf. Attribute als Exempt-Marker |
| xUnit/NUnit/MSTest Test-Runner | ruft `[Fact]`/`[Test]`-Methoden per Reflection | (wird durch Whitelist abgedeckt, siehe Muss-Haben) | OK |
| MCP-Server-Tool-Dispatch | Server holt `[McpServerTool]`-Methoden per Reflection | (wird durch Whitelist abgedeckt) | OK |
| Source-Generator-Output | Generierter Code referenziert Symbole aus Source-Assembly | `[GeneratedRegex]`-Helper | schwer erkennbar — IsInSource/IsImplicitlyDeclared hilft teilweise |
| `dynamic x; x.Foo()` | Dynamic Dispatch, statisch nicht auflösbar | `dynamic obj; obj.Foo();` | nicht erkennbar, aber selten |
| `MethodInfo.CreateDelegate` | Delegate-Erzeugung per Reflection | `typeof(Foo).GetMethod("Bar").CreateDelegate(...)` | wie Reflection, nicht erkennbar |
| `[InternalsVisibleTo]` über Solution-Grenzen | Wir sehen nur References innerhalb der geladenen Solution; Aufrufer in externen Assemblies (NuGet-Consumer, andere nicht-geladene Solutions) sind unsichtbar | Library-Assembly mit `public`-API wird von externem Service genutzt | als `low` markiert, User manuell validiert |
| COM / WinRT / PInvoke | extern, nicht in Solution | `[DllImport("kernel32.dll")]` | OK (Whitelist) |
| Module-Initializer | Runtime ruft Methode einmal auf | `[ModuleInitializer] static void Init() {...}` | OK (Whitelist) |

Diese Liste wird im Tool-Output **explizit** ausgeben (nicht stillschweigend weglassen), damit ein Agent entscheiden kann, ob er `find_dead_code`-Treffer vertraut oder ob er manuell prüft.

## Zielplattformen / Technischer Rahmen

- **MCP-Server, stdio** — bestehender Server (`src/AiNetLinter/Mcp/McpCodeGraphServer.cs`), neues Tool parallel zu `find_references` in `SymbolGraphToolRegistrations`. Begründung: thematisch verwandt (Symbolgraph-Abfragen), User-Workflow passt zu "ich vermute X ist tot → find_dead_code zeigt alle Kandidaten".
- **Roslyn `SymbolFinder.FindReferencesAsync`** für Symbol-Referenz-Checks — bestehende API, bereits in `find_references` genutzt.
- **Roslyn `SemanticModel.AnalyzeDataFlow`** (optional, für `mode=locals`) — neue API-Nutzung, semantischer Schritt pro Methode (Performance-Implikation siehe Nice-to-Have).
- **C#-only** — wie alle anderen MCP-Tools, dokumentiert in `ServerInstructions.cs`.
- **Structured Output** — Pflicht, wie bei `pattern_detect` (S2.2 Akzeptanzkriterien).
- **Naming-Konvention:** `find_*` (wie `find_symbol`, `find_references`, `find_duplicates`, `find_magic_values`). Vorschlag: `find_dead_code` — passt zum bestehenden Naming, "dead code" ist etablierter C#-Begriff (Roslyn IDE0051/0052), LLMs erkennen ihn sicher.

## Verworfene Alternativen

- **Nur als Linter-Rule** (`DeadCodeChecker`): Verworfen, weil das Pattern dem User-Facing-Charakter nicht gerecht wird. Linter-Violations werden vom Agent typischerweise in großen Listen mit anderen Verstößen vermischt; ein dediziertes Tool mit Confidence-Levels und Filter ist klarer. Mögliche Brücke: Linter-Rule könnte das Tool aufrufen und in `get_violations` aggregieren — Nice-to-Have.
- **Externes Tool (z. B. `JetBrains.Annotations`, `Resharper`-CLI):** Verworfen, weil AiNetLinter bewusst eigenständig bleibt und Roslyn nativ nutzt. Keine zusätzlichen Tool-Abhängigkeiten für eine Fähigkeit, die Roslyn selbst anbietet.
- **Embeddings / Fuzzy-Suche:** Verworfen, siehe `06-nicht-umsetzen.md` §10 (semantische Suche widerspricht der Roslyn-präzisen Positionierung).
- **Zwei separate Tools** (`find_unused_symbols` + `find_unused_locals`): Verworfen für jetzt — ein Tool mit `mode`-Parameter ist kompakter, konsistent mit `metrics_tree` (verschiedene Modi in einem Tool). Falls sich zeigt, dass die Variablen-Analyse fundamental andere UX braucht (z. B. weil DataFlowAnalysis andere Filter braucht), Split nachholen.
- **Git-History-aware (wer hat das Symbol zuletzt benutzt?):** Verworfen — siehe `06-nicht-umsetzen.md` §9 (kein Git-Wrapper ohne echten Symbolbezug). Wenn überhaupt, wäre das ein eigener `find_dead_code`-Modus mit `consider_git: bool` — sehr aufwendig, kein belegter Bedarf.

## Wo im Projekt

**Pattern-Reuse-Check (Schritt 3a, bereits durchgeführt):**

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs` — nutzt `SymbolFinder.FindReferencesAsync` (über `DiffImpactAnalyzer.FindCallSiteEntriesAsync`). Kernlogik wiederverwendbar, aber `find_references` ist 1 Symbol → N Aufrufer; wir brauchen das Inverse: N Symbole → 0 Aufrufer. Iterations-Wrapper, nicht Replacement.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs` — Symbol-Identifikator-Resolution (`SymbolIdentifierResolver`). Wird im neuen Tool NICHT gebraucht (wir iterieren über die ganze Solution), aber `McpSufficiencyHints`/`McpTruncation`/`McpToolResults` werden wiederverwendet.
- `src/AiNetLinter/Mcp/Tools/PatternDetect/PatternCatalog.cs` + `PatternDetectScanner.cs` — Pattern-Catalog-Pattern (statische Pattern-Definition) + Scanner-Pattern (Solution-weiter Sweep). Architektur-Vorbild für `find_dead_code`: `FindDeadCodeScanner.cs` als reine Funktion, `FindDeadCodeTool.cs` als dünner Wrapper.
- `src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsScanner.cs` + `ViolationScopeFilter.cs` — `scope`-Parameter-Handling analog übernehmen.
- `src/AiNetLinter/Mcp/McpSufficiencyHints.cs` — Sufficiency-Hinweis wiederverwenden, gleiche Textbaustein-Pattern wie `find_references`.
- `src/AiNetLinter/Mcp/McpToolResults.cs` — `McpToolResults.Recoverable()` / `.Text()` / `.SolutionNotLoaded()` / `.CompilationError()` — alle vorhanden, keine Neuerfindung.
- `src/AiNetLinter/Mcp/ServerInstructions.cs` — muss ergänzt werden (neuer Tool-Eintrag in der Tool-Liste, neuer Eintrag in der C#-only-Sektion).
- `src/AiNetLinter/Mcp/IsErrorPolicy.md` — kein neuer `isError: true`-Fall nötig (Empty-Result ist recoverable wie bei `find_references`).

**Mängel-Check:** Im Bestand kein Verstoß gegen `.agents/rules/**` gefunden im scope dieses Konzepts. Konzept folgt den Stil-Konventionen (record-Parameter, sealed, max. 60 Zeilen Methode).

## Entdeckte Mängel/Redundanzen

- **`find_references` mit 0 Treffern = Vorstufe von Dead-Code-Check**
  - **Gefunden:** `FindReferencesTool.cs:75-77` — "Keine Aufrufstellen gefunden fuer 'X'" ist bereits ein vollständiges, definitives Ergebnis.
  - **Bezug:** kein `rules_dir`-Verstoß; strukturelle Redundanz — beide Tools rufen `SymbolFinder.FindReferencesAsync`, aber in entgegengesetzter Richtung.
  - **Vorschlag:** Behalten — User kann für Einzelfälle weiter `find_references` nutzen, das neue Tool ist der "Scan-alle"-Aufruf. Kein Refactoring der bestehenden `FindReferencesScanner.cs` nötig; `find_dead_code` ruft intern dieselbe API, aggregiert über alle Symbole.
  - **Entscheidung:** bewusst beibehalten (siehe Nice-to-Have Brücke zur Linter-Rule).

- **Filter-Logik für compiler-generated Members existiert bereits fragmentiert**
  - **Gefunden:** `AIContextFootprintCalculator.cs:108-110` filtert mit `MethodKind.Ordinary + !IsImplicitlyDeclared + Record-Spezial-Members-Whitelist`; `GetClassStructureTool.cs:222-231` filtert Accessor-`MethodKind`-Werte.
  - **Bezug:** kein `rules_dir`-Verstoß; Pattern-Reuse statt Neuerfindung.
  - **Vorschlag:** Diese Filter-Logik in `FindDeadCodeScanner` wiederverwenden (gleiche Filter-Semantik). Wenn sich zeigt, dass die Filter konzeptionell identisch bleiben, kann man später eine gemeinsame `IsEffectivelyUserAuthored(ISymbol)`-Helfermethode extrahieren — vorerst Pattern-Reuse per Copy mit klarer Begründung im Scanner-Header.
  - **Entscheidung:** übernommen ins Scope (Muss-Haben Whitelist nutzt diese Logik).

- **`InternalsVisibleTo` auf Test-Assemblies — direkter Eigengebrauch-Effekt**
  - **Gefunden:** `LinterEngine.cs:18-20` deklariert `InternalsVisibleTo("AiNetLinter.FastTests"/"IntegrationTests"/"TestKit")`.
  - **Bezug:** kein `rules_dir`-Verstoß; **wäre ein falscher `high`-Treffer-Generator** ohne explizite Whitelist-Behandlung.
  - **Vorschlag:** Muss-Haben-Whitelist erkennt `InternalsVisibleTo`-deklarierende Assemblies, markiert `internal`-Symbole, die nur von diesen referenziert werden, als `low` mit `exemptReason: "InternalsVisibleTo"`. So wird das Tool im Eigengebrauch sofort nutzbar, ohne Dutzende False-Positives.
  - **Entscheidung:** übernommen ins Scope (Muss-Haben Whitelist-Item).

- **Roslyn-Compiler-Diagnostic-IDs für ungenutzte Elemente werden nicht genutzt**
  - **Gefunden:** Kein Treffer auf `CS0169`/`CS0414`/`IDE0051`/`IDE0052`/`IDE0044` im Code — diese existieren in Roslyn nativ, werden aber nirgends in der Pipeline aktiviert oder ausgewertet.
  - **Bezug:** kein `rules_dir`-Verstoß; verpasste Chance, eine kampferprobte Datenquelle zu nutzen.
  - **Vorschlag:** Hybrid-Strategie (siehe Nice-to-Have) aktiviert diese Warnungen on-demand im Scanner und integriert sie in den Output.
  - **Entscheidung:** Nice-to-Have, nicht Muss — reine `SymbolFinder`-Variante funktioniert auch ohne.

- **Reflection-Marker-Attribute sind projektspezifisch**
  - **Gefunden:** Kein zentrales Whitelist-Pattern für Reflection-Attribute im Bestand; jede Datei handhabt das ad-hoc.
  - **Bezug:** kein `rules_dir`-Verstoß; Pattern-Reuse-Opportunity.
  - **Vorschlag:** Initiale Whitelist-Liste im Scanner-Code mit den gängigen Attributen (`[McpServerTool]`, `[McpTool]`, `[Export]`, `[Import]`, `[Fact]`, `[Theory]`, `[Test]`, `[TestMethod]`, `[Conditional]`, `[ModuleInitializer]`, `[DllImport]`, `[JSInvokable]`, `[Parameter]`, `[Inject]`) — als Konstante, leicht erweiterbar. Keine `rules.json`-Anbindung in v1 (würde gegen das "monolithisch & schlank"-Architekturprinzip verstoßen, siehe `06-nicht-umsetzen.md` §8).
  - **Entscheidung:** übernommen ins Scope (Muss-Haben Whitelist nutzt die Konstante).

## Wie (grob)

1. `FindDeadCodeScanner` iteriert via `solution.Projects` → `INamedTypeSymbol.GetMembers()` (mit `DeclaredAccessibility`-Filter und `Kind`-Filter) → für jedes Symbol `SymbolFinder.FindReferencesAsync(symbol, solution)`.
2. Pro Symbol: bei 0 Treffern Klassifikation nach `Accessibility` + Attributen → `high` oder `low`.
3. `FindDeadCodeTool.ExecuteAsync` orchestriert: Solution-Load-Check, Filter-Param-Normalisierung, Scanner-Aufruf, Result-Aggregation, Structured-Output-Build, Sufficiency-Hinweis anhängen.
4. Edge-Case-Whitelist in `FindDeadCodeScanner` als kleine Helfermethode `IsExempt(ISymbol)` (Main, Constructor, Conditional, etc.).
5. Tests: 5+ Unit-Tests mit In-Memory-Workspaces (verschiedene Symbol-Kinds, Accessibilities, Attribute-Kombinationen), 1 Integration-Test gegen AiNetLinter-Solution selbst.

## Definition of Done / Erfolgskriterien

- Tool `find_dead_code` ist in `SymbolGraphToolRegistrations` registriert und im `tools/list`-Output sichtbar.
- Tool-Eintrag in `ServerInstructions.cs` ergänzt (Tool-Liste + C#-only-Sektion).
- Structured Output vorhanden, JSON Schema validiert.
- `include_public`-Filter funktioniert wie spezifiziert.
- Edge-Case-Whitelist schützt vor False-Positives (5+ dokumentierte Exempt-Cases).
- 5+ Unit-Tests in `AiNetLinter.FastTests` (Unit/Component), alle grün.
- 1 Integration-Test in `AiNetLinter.IntegrationTests`: `LiveDogfood_FindDeadCode_ReturnsResults` auf AiNetLinter-Repo selbst, stabil unter wiederholten Läufen, findet mindestens 3 echte Treffer in der eigenen Codebase.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` beide grün.
- `dotnet build` warnungsfrei (`TreatWarningsAsErrors = true`).
- Doku in `Docs/agent-api.md#mcp-server-modus` mit Beispiel-Workflow ("vermutlich tote private Methode X → find_dead_code liefert die ganze Liste → User entscheidet pro Treffer").
- Drift-Audit-Skill (`find_duplicates`) gibt keine Hinweise auf Code-Duplikation zwischen Scanner und Pattern-Catalog-Pattern (wir bauen **kein** Duplikat).

## Offene Punkte

(Vorerst leer — Klärung in Fragerunde 1.)
