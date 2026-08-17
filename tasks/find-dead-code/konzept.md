---
status: ready
type: konzept
project_kind: brownfield
estimated_scope: medium
rules_dir: .agents/rules
last_updated: 2026-08-17
open_questions: []
---

# Konzept: `find_dead_code` — MCP-Tool für Dead-Code-Detection

## Ziel (Was)

Ein neues MCP-Tool, das in der geladenen Solution Symbole (Klassen, Records, Structs, Enums, Interfaces, Methoden, Properties, Felder, Events, Delegates) und (optional) ungenutzte Variablen/Lokale/Felder findet, auf die **innerhalb der geladenen Solution nicht referenziert wird**. 

Treffer werden mit klaren Confidence-Stufen ausgegeben:
- **`high`** (private/internal ohne Referenzen, keine Framework-/Interface-Bindung: mit extrem hoher Wahrscheinlichkeit toter Code, der sicher entfernt werden kann).
- **`low`** (public API-Surface, Framework-/DI-Kandidaten, `InternalsVisibleTo` oder Interface-Implementierungen mit externem Scope: "nicht in Solution referenziert" — erfordert User-Entscheidung).

Das Tool implementiert strenge **False-Positive-Schutzmechanismen** (Interface-Kaskadierung, EntryPoint-Schutz, Document-Scoped Bounding) und eine **hochperformante Scan-Pipeline** ($O(1)$/$O(\text{doc})$ für private Symbole statt naivem $O(N \times M)$ Workspace-Scan).

## Warum / Kontext

Drift-Loop-Coder-Aufgaben enthalten regelmäßig "entferne toten Code" — bei Refactorings, beim Aufräumen vor PRs oder nach Architektur-Migrationen. Aktuell muss ein Agent das manuell machen: einzeln `find_references` aufrufen, manuell iterieren und Heuristiken raten. Das erfordert $N$ Tool-Calls pro vermutetem Symbol. Ein dediziertes Tool, das die Solution scannt und verlässliche Treffer mit Confidence-Level liefert, ersetzt das durch **1 einzigen Call**.

**Strategischer Nachbar:** `find_magic_values` und `pattern_detect` machen verwandte Solution-weite Audits. `find_dead_code` schließt die Lücke für strukturelle Code-Hygiene.

**Marktdifferenzierung & Roslyn-Vorteil:** Roslyn bietet `SymbolFinder.FindReferencesAsync` und semantische Diagnosen (`CS0169`, `CS0414`, `IDE0051`, `IDE0052`). `find_dead_code` orchestriert diese nativ mit projektweiter Caching- und Scope-Bounding-Intelligenz.

## Scope

### Muss-Haben

1. **Tool `find_dead_code` Registrierung**:
   - Registriert in [AnalysisToolRegistrations.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs) (semantisch ein Solution-weiter Audit-Scan wie `find_magic_values` und `pattern_detect`).
2. **Klassifikation in Confidence-Stufen**:
   - **`high`**: `private` oder `internal` Member/Typen ohne Referenzen, keine Interface-Implementierung, kein Override, keine Framework-Marker.
   - **`low`**: `public` Symbole (potenzielle Public-API), `protected` Member (potenziell vererbt), `internal` bei Assemblies mit `InternalsVisibleTo`, oder Symbole mit Framework-/DI-/Serializer-Attributen.
3. **Filter-Parameter**:
   - `accessibility` (enum: `all` | `private` | `internal` | `public` | `private_internal`, default `private_internal`): Filtert nach Deklarations-Sichtbarkeit. Default fokussiert auf direkt entfernbaren Code.
   - `confidence` (enum: `both` | `high` | `low`, default `both`): Filtert nach Vertrauensstufe.
   - `kind` (enum: `all` | `type` | `class` | `method` | `field` | `property` | `event` | `delegate`, default `all`): Symbol-Typ-Filter.
   - `scopeFilter` (string, optional): Case-insensitive `Contains` auf Projekt-Name oder solution-relativem Pfad (Wiederverwendung von `ViolationScopeFilter.MatchesScope`, [ViolationScopeFilter.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/Analysis/ViolationScopeFilter.cs)).
   - `include_tests` (bool, default `false`): Test-Pfade (`**/*Tests/**/*.cs`, `**/TestKit/**/*.cs`) aus dem Scan ausnehmen.
   - `mode` (enum: `members` | `locals` | `both`, default `members`):
     - `members`: Reiner Symbol-Graph-Referenz-Check für deklarierte Typen und Member.
     - `locals`: Zieht Compiler-Diagnosen für ungenutzte Elemente heran: `CS0169` (unused private field), `CS0414` (assigned but unused field), `IDE0051` (unused private member), `IDE0052` (unread private member). *(Hinweis: IDE0044 "Make readonly" wird explizit NICHT aufgenommen, da es kein toter Code ist).*
     - `both`: Führt beide Analysen zusammen.
   - `maxResults` (int, default `50`): Pagination via `McpTruncation.TruncateLines` und `IsTruncated: bool` im `structuredContent`.
4. **False-Positive-Schutz (Semantische Korrektheit)**:
   - **Interface- & Override-Kaskadierung (Kritisch!)**:
     - Wenn eine Methode/Property ein Interface implementiert (`ISymbol.ExplicitOrImplicitInterfaceImplementations`) oder eine Basismethode überschreibt (`IsOverride == true`): Prüfen, ob das Interface- oder Basis-Symbol Referenzen hat.
     - Hat das Interface/Basis-Symbol Referenzen, ist die Implementierung **KEIN** toter Code!
     - Hat auch das Interface 0 Referenzen, wird das Symbol entsprechend klassifiziert.
   - **Entry-Points & Top-Level-Programme**:
     - `compilation.GetEntryPoint(ct)` (z. B. `static void Main`, `<Program>$`, Top-Level Statements) wird immer gewhitelistet.
   - **Konstruktoren-Sonderbehandlung**:
     - *Private parameterlose Konstruktoren in statischen/Utility-Klassen* (`private MyUtils() {}`, wo alle anderen Member statisch sind) dienen der Verhinderung von Instanziierung und werden **immer gewhitelistet** (kein Dead Code).
     - *Implizite Standardkonstruktoren* (`IsImplicitlyDeclared == true`) werden immer gewhitelistet.
     - Explizite Konstruktoren in instanziierbaren Klassen werden bei 0 Referenzen nur dann als `high` gemeldet, wenn sie `private` sind und die Klasse nicht per DI oder Factory instanziiert wird.
   - **Compiler- & Runtime-Whitelist (immer ausgenommen)**:
     - `IsImplicitlyDeclared == true` (Compiler-generierte Backing-Fields, Record-Equality-Methoden `Equals`/`GetHashCode`/`ToString`/`<Clone>$`, State-Machines, Primary-Constructor-Captures).
     - `MethodKind.StaticConstructor` (`.cctor`) und `MethodKind.Destructor` (Finalizer).
     - `MethodKind.PropertyGet` / `Set` / `EventAdd` / `Remove` (werden über Property/Event selbst geprüft).
     - Operatoren-Overloads (`op_*`).
     - `[ModuleInitializer]`, `[DllImport]`, `[UnmanagedCallersOnly]`, `IsExtern == true`.
     - Test-Methoden mit `[Fact]`, `[Theory]`, `[Test]`, `[TestMethod]`.
     - Reflection-Marker: `[McpServerTool]`, `[McpTool]`, `[Export]`, `[Import]`, `[JSInvokable]`, `[Parameter]`, `[Inject]`.
   - **Kein Schutz durch XML-Doc**:
     - Reine Erwähnungen in XML-Kommentaren (`<see cref="...">`) schützen `private`/`internal`-Symbole **nicht** vor der Einstufung als Dead Code (alte Doku darf toten Code nicht maskieren).
5. **Performance-Architektur ($O(\text{doc})$ Bounding)**:
   - **Document-Scoped Search für `private` Symbole**: Da `private`-Member nur in der deklarierenden Datei (bzw. bei `partial` in den Typ-Dokumenten) sichtbar sind, wird `SymbolFinder.FindReferencesAsync(symbol, solution, documents: ImmutableHashSet.Create(doc))` aufgerufen. Dies reduziert die Suchzeit für ~70 % aller Symbole um Faktor 50x–100x.
   - **Top-Down Container Pruning**: Wenn ein nicht-öffentlicher Container-Typ (`private class/struct`) 0 Referenzen hat, wird der gesamte Typ als Dead Code markiert; dessen innere Member müssen nicht mehr separat per Workspace-Scan durchleuchtet werden.
   - **Identifier Pre-Check**: Vor der Ausführung von `FindReferencesAsync` für `internal`/`public` Symbole wird geprüft, ob der Identifier-Name überhaupt als Token in anderen Dokumenten vorkommt.
6. **Structured Output & Trust-Modell**:
   - Output Schema: `{ deadSymbols: [{ id, kind, containerType, file, line, accessibility, confidence, reason, limitsApplies: string[] }], summary: { high, low, byKind, scannedSymbols }, limits: string[], recommendedNextAction: { action: "ask_user", reason: "..." } }`.
   - Header-Box mit klarem Trust-Hinweis im Text-Output.
   - Sufficiency-Hinweis via `McpSufficiencyHints.Append`.
7. **Tests**:
   - FastTests (Unit/Component): Interface-Kaskadierung, `private`-Document-Bounding, Entry-Point-Schutz, Utility-Konstruktoren, Filter-Kombinationen, Pagination.
   - IntegrationTests: Live-Dogfood-Test gegen AiNetLinter-Solution selbst.

### Non-Goals (bewusst NICHT Teil davon)

- **Transitive Inseln (Effektiv-private Erkennung)**: Eine isolierte Gruppe von `public` Klassen, die sich nur gegenseitig aufrufen, aber von außen ungenutzt sind. (Komplexer Graph-Algorithmus, separater Scope).
- **Auto-Fix / Auto-Delete**: AiNetLinter bleibt Verifikations-Gatekeeper (Read-Only). Der Coder/Agent entscheidet über das Löschen.
- **Cross-Solution / Externe NuGet-Consumer**: Das Tool bewertet ehrlich innerhalb der geladenen Solution; `public` APIs werden konsistent als `low` eingestuft.
- **Reflection String-Parsing**: Kein Parsen von `Type.GetMethod("Foo")` — stattdessen Abbildung über `limitsApplies: ["reflection"]`.
- **Multi-Language Support**: AiNetLinter ist C#-pur.

## Strukturelle Lücken & `limitsApplies`-Matrix

Das Tool kennzeichnet strukturell nicht erkennbare Muster transparent pro Treffer über das `limitsApplies`-Feld:

| Symbol-Charakteristik | `limitsApplies` Einträge | Hintergrund |
|---|---|---|
| `public` oder `protected` | `["publicApiSurface", "reflection"]` | Mögliche externe Consumer oder Reflection-Aufrufe. |
| Interface-Implementierung | `["interfaceImplementation"]` | Aufrufe laufen potenziell über Interface-Instanzen. |
| POCO / DTO Properties | `["jsonSerializer", "optionsBinding"]` | Serializer oder `IOptions<T>` setzen Properties per Reflection. |
| Controller / Minimal API Actions | `["aspNetRouting"]` | Framework routet HTTP-Endpunkte per Metadaten. |
| CQRS / MediatR / Handler | `["di", "handler"]` | Handler-Auflösung erfolgt dynamisch über den Service-Provider. |
| EF Core Entity / Configuration | `["efCoreMapping"]` | Entity-Framework bindet Navigations-Properties und Tabellen. |
| Blazor Component Parameter | `["blazor"]` | `[Parameter]` wird von der Blazor-Runtime injiziert. |
| `InternalsVisibleTo` Assembly | `["internalsVisibleTo"]` | Zugriff potenziell aus befreundeter externer Assembly. |

## False-Positive-Schutz & Trust-Modell

Da statische Dead-Code-Analysen per Definition Lücken gegenüber dynamischer Laufzeit-Magie haben, gelten drei Schutzmaßnahmen:

1. **Prominenter Header-Hinweis**: Jeder Text-Output beginnt mit einem Hinweis auf Heuristiken und verweist auf die `limits`-Liste.
2. **Per-Treffer `limitsApplies`**: Zeigt dem Agenten genau, welche Framework-Effekte für diesen konkreten Treffer zutreffen.
3. **`recommendedNextAction` Block**: Enthält immer `action: "ask_user"`, um autonome Fehl-Löschungen durch Agenten zu verhindern.

## Wo im Projekt

- **Scanner-Kern**: `src/AiNetLinter/Mcp/Tools/Analysis/FindDeadCodeScanner.cs` (reine statische Scan-Funktion).
- **Tool-Wrapper**: `src/AiNetLinter/Mcp/Tools/Analysis/FindDeadCodeTool.cs`.
- **Registrierung**: `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs` (unter den Solution-weiten Audits).
- **Instructions**: `src/AiNetLinter/Mcp/ServerInstructions.cs` (Dokumentation des neuen Tools).
- **Tests**:
  - `src/AiNetLinter.FastTests/Mcp/FindDeadCodeScannerTests.cs` (Unit/Component-Tests).
  - `src/AiNetLinter.IntegrationTests/McpLiveRepositoryTests.cs` (Dogfood-Test gegen AiNetLinter).

## Wie (Ablauf & Algorithmus)

1. **Initialisierung & Pre-Filter**:
   - Solution validieren. Bei `include_tests == false` Test-Projekte aus der Dokument-Menge herausfiltern.
   - EntryPoint der Compilation ermitteln und in Whitelist aufnehmen.
2. **Deklarations-Sweep mit Scope-Bounding**:
   - Für jedes Dokument der Ziel-Projekte: deklarierte Typen und Member ermitteln.
   - Whitelist-Check (`IsImplicitlyDeclared`, Compiler-Generics, Marker-Attribute, Utility-Konstruktoren, EntryPoint).
   - *Top-Down Pruning*: Wenn Container-Typ privat & ungenutzt, Member kaskadierend erfassen ohne Einzelscans.
3. **Referenz-Prüfung**:
   - Für `private` Symbole: `SymbolFinder.FindReferencesAsync` beschränkt auf Deklarations-Dokument(e).
   - Für `internal`/`public` Symbole: Schneller Token-Pre-Check, danach `SymbolFinder.FindReferencesAsync` über Solution.
   - Bei Interface-Implementierungen / Overrides: Referenzen des Interface-/Basis-Symbols gegenprüfen.
4. **Locals-Diagnosen (wenn `mode != members`)**:
   - Diagnostics für `CS0169`, `CS0414`, `IDE0051`, `IDE0052` sammeln und als Treffer integrieren.
5. **Klassifikation & Output-Generierung**:
   - Zuordnung `high` vs. `low`, Ermittlung von `limitsApplies`.
   - Result-Aggregation, Pagination via `McpTruncation`, Anfügen von Sufficiency-Hinweis und `recommendedNextAction`.

## Definition of Done

- Tool `find_dead_code` ist in `AnalysisToolRegistrations` registriert und über MCP aufrufbar.
- Parameterset (`accessibility`, `confidence`, `kind`, `scopeFilter`, `include_tests`, `mode`, `maxResults`) funktioniert vollständig.
- Keine False-Positives bei:
  - Interface-Implementierungen mit aktiven Interface-Aufrufen.
  - Überschriebenen Methoden (`override`) mit Basis-Aufrufen.
  - Entry-Points (`Program.cs`, `Main`).
  - Privaten Utility-Konstruktoren (`private MyUtils() {}`).
  - Compiler-generierten Members (Records, Auto-Props, Lambdas).
- Performance: Scan über AiNetLinter-Solution läuft in unter 3 Sekunden durch Scope Bounding.
- Structured Output validiert mit `deadSymbols[]`, `summary`, `limits[]`, `limitsApplies[]`, `recommendedNextAction`.
- FastTests und IntegrationTests laufen fehlerfrei durch (`dotnet test --filter Category!=Stress`).
- Zero-Warning-Build (`TreatWarningsAsErrors = true`).

## Offene Punkte

(Bewusst leer — `open_questions` im Frontmatter ist aufgelöst; alle Nice-to-Have-Punkte sind entweder in Muss-Haben hochgestuft oder in Non-Goals mit Begründung verschoben. Keine Restbestände.)
