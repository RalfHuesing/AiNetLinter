# Analyse: `codegraph_explore` vs. AiNetLinter Flow-Tools

**Repo:** `C:/Daten/Entwicklung/Ralf/AiNetLinter`
**Stand:** 2026-08
**Vergleichs-Repos:** `C:/Daten/Entwicklung/GitHub/codegraph` (TypeScript), AiNetLinter (C#/Roslyn)
**Charakter:** Detail-Vergleich, Lücken-Analyse, konkreter `trace_flow`-Vorschlag. **Keine Implementierung.**

---

## 1. Executive Summary

`codegraph_explore` ist **kein "ein besseres find_references"** — es ist ein kategorisch anderes Werkzeug. Die existierenden AiNetLinter-Tools (`find_references`, `get_impact`, `find_symbol`, `get_type_hierarchy`) sind allesamt **Eintpunkt-Werkzeuge**: ein Seed-Symbol rein, eine flache Liste raus. `codegraph_explore` ist ein **Mehrpunkt-Flow-Tracer**: ein Beutel Symbol-Namen rein, ein nummerierter Call-Path plus Source-Bodies plus Blast-Radius in einem einzigen Response raus.

**Drei zentrale Fähigkeiten fehlen AiNetLinter komplett:**

1. **Multi-Symbol-Flow-Tracing** — kein Tool nimmt eine Liste von Symbol-Namen und berechnet den längsten Call-Chain *zwischen* ihnen. Heute muss der Agent selbst N-mal `find_references` aufrufen und die Ergebnisse gedanklich zusammensetzen.
2. **Synthetisierte Kanten** — `codegraph_explore` markiert Kanten mit `provenance:'heuristic'` (Plugin-Dispatch, JSX-Children, React-Re-Render, etc.) und überbrückt damit Lücken, die im statischen Graphen fehlen. AiNetLinter hat nur den DI-Heuristik-Hinweis in `get_type_hierarchy`.
3. **Sufficiency-Doctrine** — CodeGraph sagt dem Agent aktiv *"do NOT Read these files — the source above is already complete and verbatim"*. AiNetLinter-Tools geben dem Agent nie diesen Stopp-Hinweis; nichts hindert ihn daran, parallel `Read` zu feuern.

**Empfehlung:** `trace_flow` für AiNetLinter — **Ja, mit Vorbehalt** (siehe §7). Aufwand: **1–2 Wochen** für eine erste Version, die das Killer-Feature (Multi-Symbol-Flow + Source-Bodies + Blast-Radius) liefert. Volle Synthesized-Edges-Unterstützung (Reflection, `dynamic`, C#-spezifische Dynamic-Dispatch-Heuristiken) wäre ein **eigenes Epic** (~1 Monat).

---

## 2. Phase 1 — `codegraph_explore` im Detail

### 2.1 Werkzeug-Vertrag

```typescript
// src/mcp/tools.ts:1104-1124
{
  name: 'codegraph_explore',
  description: 'PRIMARY TOOL — call FIRST for almost any question OR before an edit ...',
  inputSchema: {
    type: 'object',
    properties: {
      query: {
        type: 'string',
        description: 'Symbol names, file names, or short code terms ... ' +
                     'A natural-language question works too — no prior codegraph_search needed.',
      },
      maxFiles: { type: 'number', default: 12 },
      projectPath: projectPathProperty,
    },
    required: ['query'],
  },
}
```

**Input-Charakteristika:**
- `query` ist ein **Beutel** — Symbol-Namen *und/oder* Datei-Namen *und/oder* natürliche Sprache. Keine strukturierte Schema-Vorgabe.
- `maxFiles` limitiert die mitgelieferten Source-Bodies (Default 12).
- Kein `depth`-Parameter — Tiefe wird intern adaptiv gewählt (MAX_HOPS=7).

### 2.2 Output-Struktur (aus Tests & Code zusammengesetzt)

```markdown
**Flow (call path among the symbols you queried)**

1. PmsProductController::getList (Controllers/PmsProductController.cs:42)
   ↓ calls
2. PmsProductService.list (Services/PmsProductService.cs:18)
   ↓ heuristic: implements
3. PmsProductServiceImpl.list (Impl/PmsProductServiceImpl.cs:23)

**Dynamic-dispatch links among your symbols**
(synthesized — the indirect hops grep/Read would reconstruct; the @file:line is the wiring site)
- mutateElement → renderScene [heuristic: JSX child]

**Blast radius**
- `getList` — 3 callers (Controllers/PmsProductController.cs:42, AdminController.cs:67, Test.cs:91)
- `list` — 5 callers, 2 covering tests (PmsProductServiceTests.cs:12, IntegrationTests.cs:34)

Found 7 symbols across 3 files.

**`src/Services/PmsProductService.cs`** —
```ts
18  List<Product> list(Query q) {
19    return db.Find(q).ToList();
20  }
```

**`src/Impl/PmsProductServiceImpl.cs`** —
```ts
23  public List<Product> list(Query q) {
24    return _repo.Where(q.Spec).ToList();
25  }
```

> Complete source for 3 files is included above — do NOT re-read them. ...
```

**Vier Sektionen** (alle optional, je nach Query-Charakter):
1. **Flow** — nummerierte Kette der gefundenen Symbole, Edge-Typen zwischen Hops (`calls`, `heuristic: implements`, `heuristic: JSX child`, etc.).
2. **Dynamic-dispatch links** — synthetisierte Kanten, die der statische Graph nicht hergibt.
3. **Blast radius** — Abhängigkeiten + Test-Coverage, locations-only.
4. **Source bodies** — verbatim, line-numbered, gruppiert pro Datei (cat-n-Stil, editierbar).

### 2.3 `buildFlowFromNamedSymbols` — Kern-Algorithmus

Code: `src/mcp/tools.ts:2440–2706` (~270 Zeilen, die kritischste Funktion im gesamten MCP-Server).

**Schritt 1: Token-Extraktion** (Zeilen 2452–2457)
- Splittet Query auf Whitespace/Comma/Klammern, strippt nur echte File-Extensions, behält qualifizierte Namen (`Class.method`, `Class::method`).
- Wirft alles <3 Zeichen weg, cappt auf 16 Tokens.

**Schritt 2: Symbol-Resolution mit Co-Naming-Disambiguation** (Zeilen 2489–2526)
- Für jeden Token: alle Definitionen finden, filtern auf `CALLABLE` (method/function/component/constructor).
- **Spezifischer Name** (≤3 Treffer): behalte alle.
- **Polymorpher Name** (>3 Treffer): behalte nur Kandidaten, deren Container (vorletztes Segment des qualifiedName) auch im Token-Pool ist. Beispiel: Query `getList PmsProductController` → von 47 `getList`-Definitionen bleiben nur die aus `PmsProductController`-Containern übrig.

**Schritt 3: BFS mit Anker-Constraint** (Zeilen 2569–2599)
- Pro Seed-Symbol: BFS vorwärts durch den Call-Graph, **inkl. synthetisierter Kanten**.
- Akzeptiert nur Sinks, die auch in `named` sind (Anker-Symmetrie).
- **MAX_HOPS = 7**, **MAX_BRIDGE = 1** (max. ein aufeinanderfolgender Unnamed-Hop).
- Wählt den längsten gefundenen Pfad (`best`).

**Schritt 4: Grenz-Erkennung** (Zeilen 2611–2700)
- **Dynamic-Boundary-Scan**: wenn der Flow nicht voll verbindet, werden die Bodies der nicht-verbundenen Symbole nach dynamischen Dispatch-Sites gescannt (`getattr`, computed member calls, Event-Bus-Emits).
- **Polymorphic-Boundary-Announcement**: Token mit ≥8 gleichnamigen Definitionen, die nicht im Flow landen, werden als Interface/Registry-Dispatch angekündigt ("es gibt N Implementierungen, das ist der Supertyp, picke eine").
- **Synth-Links** (≤6): zusätzliche heuristische Kanten an Named-Symbolen, die nicht im Haupt-Flow sind.

**Schritt 5: Source-Assemblierung** (Zeilen 3877ff.)
- Pro Datei: Window um die Spine-Knoten (nutzt `spineCallSites`, um Oversize-Methoden auf den Call-Site zu kappen).
- Skeleton-Mode für Off-Spine-Polymorphic-Siblings (OkHttp's `: Interceptor`-Klassen).
- Adapters für `cat -n`-Format (`<n>\t<line>`), Markdown-Heading-Ersatz (`**`filepath`** —`).

### 2.4 Sufficiency-Doctrine (aus `server-instructions.ts:20–73`)

Der Server-Instructions-Text sagt dem Agent explizit:
- *"ONE call usually answers the whole question."*
- *"treat the source it returns as already Read."*
- *"Trust codegraph's results — don't re-verify them with grep."*
- *"Don't reconstruct a flow by hand — name the endpoints in one codegraph_explore and it surfaces the path between them."*

Die Tool-Outputs selbst wiederholen das mit `>`-Hinweisblöcken:
- `> Complete source for N files is included above — do NOT re-read them.`
- `> do NOT Read these files.`
- `> (output truncated to budget; the source above is complete and verbatim — treat it as already Read. ...`

Das ist **kein** CodeGraph-spezifischer Marketing-Spruch — das ist der eigentliche Grund, warum 88% weniger Tool-Calls gemessen werden. Der Output stoppt den Agent.

### 2.5 Test-Landschaft (Auszug)

- `__tests__/explore-blast-radius.test.ts` — Blast-Radius inkl. transitiver Test-Coverage
- `__tests__/explore-corroboration-ranking.test.ts` — Multi-Term-Ranking über mehrere Reposchichten
- `__tests__/explore-cross-call-dedup.test.ts` — Cross-File-Dedup
- `__tests__/explore-synth-constant-endpoints.test.ts` — RTK-Thunk→Thunk (`constant`→`constant`) Synth-Kanten
- `__tests__/explore-allocation-e2e.test.ts` — adaptive Output-Budgets
- `__tests__/explore-output-budget.test.ts`, `explore-proportional-allocation.test.ts`, `explore-result-count.test.ts` — Budget-Mechanik

→ **8 dedizierte Test-Dateien** nur für `codegraph_explore` + Subtests. Das ist kein "klassisches" Werkzeug; das ist ein primäres User-Facing-Feature mit eigener QA-Disziplin.

---

## 3. Phase 2 — AiNetLinter Flow-Tools im Detail

### 3.1 `find_references` (`Mcp/Tools/FindReferencesTool.cs`, 137 Zeilen)

**Vertrag** (`SymbolGraphToolRegistrations.cs:71–86`):
```csharp
async (string symbolIdentifier, int maxResults = 50, int depth = 1, CancellationToken ct = default)
```

| Parameter | Default | Hard-Cap | Wirkung |
| :--- | :---: | :---: | :--- |
| `symbolIdentifier` | — | — | DocumentationCommentId, Datei:Zeile:Spalte, oder qualifizierter Name |
| `maxResults` | 50 | — | Trunkiert die ausgegebene Liste |
| `depth` | 1 | 3 (`CallGraphTraversal.MaxRecursionDepth`) | Transitive Aufrufer; >1 löst über `SymbolFinder.FindReferencesAsync` |

**Output-Format** (aus `CallGraphTraversal.cs:122–131`):
```
src/Auth/LoginService.cs:42 - transitiver Aufrufer
src/Auth/Controllers/LoginController.cs:18 - transitiver Aufrufer
[3 Treffer gesamt (depth=3, hard-cap 200), 50 gezeigt — depth reduzieren oder maxResults erhoehen]
```

**Was es kann:**
- Direkte Aufrufstellen (depth=1) via `DiffImpactAnalyzer.FindCallSitesAsync` (Roslyn-nativ).
- Transitive Aufrufer (depth 2–3) via iterativem BFS auf `SymbolFinder.FindReferencesAsync`.
- Symbol-Resolution mit drei Eingabeformaten (DocumentationCommentId / Datei:Zeile:Spalte / qualifizierter Name).
- Accessor-Normalisierung (Property-`get`/`set` → Owner).
- Stabil gegen Overloads via DocumentationCommentId.

**Was es NICHT kann:**
- Bag-of-Symbols als Input (immer genau ein Seed).
- Callee-Traversal (nur Caller, also rückwärts).
- Edge-Typ-Markierung (kein "calls" / "override" / "implements" Label).
- Source-Body im Output (nur Locations).
- Synthetisierte Kanten / Dynamic-Boundary-Erkennung.
- Blast-Radius (kein Coverage-Hinweis).
- Polymorphic-Dispatch-Erkennung.

### 3.2 `get_impact` (`Mcp/Tools/GetImpactTool.cs`, 105 Zeilen)

**Vertrag** (`SymbolGraphToolRegistrations.cs:103–119`):
```csharp
async (string? gitRef = null, string? symbolIdentifier = null,
       int maxResults = 50, int depth = 1, CancellationToken ct = default)
```

**Zwei exklusive Branches:**
1. **Symbol-Branch** (`ExecuteSymbolBranchAsync`, Zeilen 44–72): delegiert 1:1 an `FindReferencesTool`-Logik. `depth` wirkt.
2. **Git-Branch** (`ExecuteGitRefBranchAsync`, Zeilen 74–103): nutzt `DiffImpactAnalyzer.AnalyzeAsync(solution, targetPath, gitRef)`. `depth` wird ignoriert (Symboltiefe nicht definiert für Git-Diff). Default (beide Params null) = uncommitted Changes.

**Output-Format:**
- Symbol-Branch: identisch zu `find_references`.
- Git-Branch: `DiffImpactAnalyzer`-Output (Liste geänderter Aufrufstellen).

**Was es zusätzlich zu `find_references` kann:**
- **Git-Diff-Integration** — komplett einzigartig in der MCP-Welt. `codegraph_explore` hat das nicht.
- Unterscheidung "uncommitted vs. spezifischer Ref" via Parameter-Logik.

**Was es nicht kann:** identisch zu `find_references` + keine Multi-Symbol-Flows.

### 3.3 `find_symbol` (`Mcp/Tools/FindSymbolTool.cs`, 126 Zeilen)

**Vertrag:**
```csharp
async (string namePattern, string? kind = null, int maxResults = 50, CancellationToken ct = default)
```

| Parameter | Default | Wirkung |
| :--- | :---: | :--- |
| `namePattern` | — | Substring-Match auf Symbolnamen |
| `kind` | null | Filter: `class`/`klasse`/`interface`/`method`/`methode`/`property` |
| `maxResults` | 50 | Trunkiert |

**Output-Format** (`FindSymbolToolTests.cs` + `Mcp/FormatSymbolLocations`):
```
src/Auth/LoginService.cs:18 - Klasse: Auth.LoginService
src/Auth/Controllers/LoginController.cs:42 - Methode: Auth.Controllers.LoginController.Login
[3 Treffer gesamt, 50 gezeigt — Pattern verfeinern oder maxResults erhoehen]
```

**Was es kann:** Symbol-Suche via Substring + Kind-Filter, Format-Wiederverwendung in `find_references`-Ambiguitäts-Fehler.

**Was es nicht kann:** semantische Flow-Fragen, irgendetwas mit Call-Sites.

### 3.4 `get_symbol_body` (`Mcp/Tools/GetSymbolBodyTool.cs`, 100 Zeilen)

**Vertrag:**
```csharp
async (string identifier, int maxBodyLines = 80, CancellationToken ct = default)
```

**Output:** Markdown-Block mit Body, hart gekappt bei `maxBodyLines` (Default 80) mit Ellipse-Indikator. Stabile DocumentationCommentId im Output → Edit-Loop-tauglich.

**Mapping zu CodeGraph:** Das ist `codegraph_node` (Mode 2 — "ONE SYMBOL you can name"). Aber: `codegraph_node` liefert **Body + Caller/Callee-Trail in einem Call** und pinned Overloads per `file`/`line`-Parameter. `get_symbol_body` liefert nur den Body. Kein Trail.

### 3.5 `McpTruncation` (`Mcp/McpTruncation.cs`, 63 Zeilen)

Universeller Trunkierungs-Helper für Listen-Tools. Zwei Methoden:
- `TruncateLines(...)` — Top-N + Meta-Zeile `[N Treffer gesamt, M gezeigt]`.
- `TruncateFileList(...)` — Top-N + Meta-Zeile `[N Dateien, M gezeigt]`.

**Bewusst simpel** — keine Flow-Logik, keine semantische Aggregation. Das passt zu den existierenden Tools, ist aber **die größte Hürde** für `trace_flow`, weil der Token-Save eines Flow-Tools genau aus *intelligenter Aggregation* kommt (z. B. eine oversize-Methode auf den Call-Site fenstern, nicht den ganzen 962-Zeilen-Body).

### 3.6 Aktueller Workflow (was der Agent heute tun muss)

Frage: *"Wie kommt der Login-Flow vom Controller zur DB?"*

```
1. find_symbol("LoginController")           → 1 Hit
2. find_references("LoginController.Login") → 8 Hits
3. find_symbol("LoginService")              → 1 Hit
4. find_references("LoginService.Authenticate") → 5 Hits
5. find_symbol("IUserRepository")           → 1 Hit
6. find_references("IUserRepository.FindById") → 12 Hits
7. get_symbol_body("LoginController.Login") → ~40 Zeilen
8. get_symbol_body("LoginService.Authenticate") → ~30 Zeilen
9. get_symbol_body("IUserRepository.FindById") → ~25 Zeilen
```

**9 Tool-Calls**, ~120 Zeilen Source + 25 Locations. Der Agent muss die 9 Antworten selbst zur Flow-Kette zusammensetzen. `codegraph_explore` würde das in **1 Call** liefern.

---

## 4. Phase 3 — Detaillierter Vergleich

| Aspekt | `codegraph_explore` | `find_references` | `get_impact` | Lücke? |
| :--- | :--- | :--- | :--- | :--- |
| **Input-Format** | Single-String-Query: Symbol-Bag / Datei-Namen / NL-Frage (intern tokenisiert) | Ein einzelner `symbolIdentifier` (DocId / file:line:col / qual. Name) | `gitRef` ODER `symbolIdentifier` (exklusiv) | **JA** — keine Multi-Symbol-Eingabe in AiNetLinter |
| **Output-Format** | Multi-Sektion-Markdown: Flow + Dynamic-Dispatch + Blast-Radius + Source-Bodies | Flache Liste `pfad:zeile - transitiver Aufrufer` | Identisch zu `find_references` im Symbol-Branch; Diff-Liste im Git-Branch | **JA** — keine Flow-Struktur, keine Source-Bodies |
| **Edge-Typ-Markierung** | Ja — `↓ calls`, `↓ heuristic: implements`, `↓ heuristic: JSX child` etc. pro Hop | Nein — nur Location | Nein | **JA** — kein Edge-Label |
| **Depth-Handling** | MAX_HOPS=7, MAX_BRIDGE=1 (Anker-Symmetrie: nur Named-Sinks), dynamische Tiefe | Hard-Cap depth=3, harte BFS-Wanderung, harte MAX_NODES=200 | Identisch zu `find_references` im Symbol-Branch | **TEILWEISE** — Cap okay, aber Anker-Constraint fehlt |
| **Overload-Handling** | Type-aware Co-Naming: behält Definitionen, deren Container im Token-Pool ist. Spezifischer Name (≤3) vs. polymorph (≥8) | DocumentationCommentId disambiguiert Overloads semantisch (Roslyn-nativ) | Wie `find_references` | **NEIN** — Roslyn ist hier strikter als Co-Naming |
| **Synthesized Edges** | Ja — Plugin-Dispatch, JSX-Children, React-Re-Render, Constant-Endpoints, `implements`-Edges | Nein | Nein | **JA** — nur DI-Heuristik in `get_type_hierarchy` |
| **Polymorphic-Boundary-Ankündigung** | Ja — "es gibt N Implementierungen, pick eine" bei großen Same-Name-Familien | Nein | Nein | **JA** — `get_type_hierarchy` listet, sagt aber nicht "das ist ein Dispatch" |
| **Dynamic-Boundary-Scan** | Ja — Source-Body-Scan auf `getattr`, computed member calls, Reflection-Lookups | Nein | Nein | **JA** — Roslyn hat hier mehr Informationen (Attribute, `dynamic` Keyword) |
| **Blast-Radius / Coverage-Hinweis** | Ja — "X callers, Y covering tests" (Test-Datei-Suche via Caller-Chain bis 3 Hops) | Nein — nur Caller-Liste | Nein (Symbol-Branch); Diff-Liste (Git-Branch) | **JA** — kein Coverage-Konzept |
| **Performance-Target** | 1 Call für kleine Repos, 3–5 für große; adaptive Budgets; Proportional-Allocation per File | Unklar — nicht in Tests explizit gepinnt. Roslyn `FindReferencesAsync` ist auf einer Solution schnell (ms-Bereich), aber depth=3 mit 200-Nodes-Cap ist teurer | Unklar — `DiffImpactAnalyzer.AnalyzeAsync` mit `git status` / `git diff` ist I/O-bound | **UNKLAR** — keine dokumentierten SLOs |
| **Token-Optimierung** | Adaptive Budgets: per-File-Char-Cap, Proportional-Allocation, Oversize-Methoden auf Call-Site gefenstert, "Already sent"-Pointer, Inline-Cap (25 KB) mit Offload | Trunkierung Top-N + Meta-Zeile | Wie `find_references` | **JA** — keine aggregierte Trunkierung, kein Window-Cap |
| **Sufficiency-Doctrine (Stopp-Hinweis)** | Ja — `> do NOT Read these files`, `> treat as already Read` | Nein — kein Hinweis | Nein | **JA** — kein Agent-Stopp |
| **NL-Query** | Ja — "how does X work", "the flow from A to B" | Nein | Nein | **JA** — alle AiNetLinter-Tools erwarten strukturierte Identifier |
| **Language-Scope** | 33 Sprachen (TS, JS, Python, Go, Rust, C#, Java, etc.) | Nur C# (Roslyn-Symbolgraph) | Nur C# (Git-Diff über .cs-Dateien) | **UMGEKEHRT** — AiNetLinter ist C#-pur, das ist eine *Stärke* |

### 4.1 Beobachtungen

**AiNetLinter-Vorteile, die die Tabelle nicht zeigt:**
- **Git-Diff-Integration** (`get_impact` ohne Params) ist einzigartig — CodeGraph hat das nicht.
- **Roslyn semantische Präzision** ist höher als CodeGraph's Name-String-Matching. `find_references` löst Overloads *strukturell* auf, CodeGraph's Co-Naming ist Heuristik.
- **Stabile DocumentationCommentId** überlebt Zeilenverschiebungen; CodeGraph muss jedes Mal per `file:line` pinnen.
- **C#-Tiefe** — Generics, Constraints, Extension Methods, `partial`, Lambda-Captures, `dynamic`, Attribute, Nullable-Annotations — Roslyn kennt das strukturell, CodeGraph muss raten.

**CodeGraph-Vorteile (klar dokumentiert):**
- **Multi-Symbol-Flow** ist die eine Sache, die alles andere in den Schatten stellt.
- **Token-Ökonomie** ist das zweite Killer-Feature — 88% weniger Tool-Calls laut interner Messung.
- **Anti-Pattern-Anweisungen** im `server-instructions.ts` sind ein Feature, kein Nebeneffekt.

---

## 5. Phase 4 — Lücken-Analyse

### 5.1 Was `codegraph_explore` kann, AiNetLinter NICHT

| Use-Case | codegraph_explore | AiNetLinter heute | Machbar mit Roslyn? |
| :--- | :---: | :---: | :---: |
| "Wie kommt der Login vom Controller zur DB?" — 1 Call | ✅ | ❌ (9 Calls, manuelle Aggregation) | ✅ — Roslyn `FindReferencesAsync` + `FindImplementationsAsync` + neuer BFS |
| "Zeig mir PmsProductController + PmsProductService + PmsProductServiceImpl + den Flow dazwischen" | ✅ (Flow-Sektion) | ❌ | ✅ |
| "Wo wird X per Reflection aufgerufen? Suche im Body nach dynamischem Dispatch" | ✅ (Dynamic-Boundary-Scan) | ❌ | ⚠️ — teilweise (Attribute, `dynamic`, `MethodInfo.Invoke`, `Activator.CreateInstance`) — Reflection ohne Attribut nicht statisch machbar |
| "Welche Tests decken X transitiv ab?" | ✅ (Blast-Radius mit Coverage-Hinweis) | ❌ | ⚠️ — Test-Dateien sind Namens-konventioniert (`*.Tests`, `*.Test`); brauche Heuristik + `FindReferencesAsync`-Caller-Chain |
| "Überspringe die Calls, die nicht in meinem Symbol-Bag sind" | ✅ (Anker-Constraint) | ❌ | ✅ — trivial im BFS |
| "Diese Datei ist 962 Zeilen — zeig mir nur den Call-Site, nicht den ganzen Body" | ✅ (Spine-Call-Site-Window) | ❌ | ✅ — `SyntaxTree.GetText().GetSubText(span)` |
| "Verbatim Source aller beteiligten Dateien in einem Call" | ✅ | ❌ (braucht N `get_symbol_body`) | ✅ — Bodies existieren in Roslyn |
| "Sag dem Agent: nicht nochmal lesen" | ✅ (Sufficiency-Hinweis) | ❌ | ✅ — trivial, fehlt nur |

### 5.2 Was AiNetLinter kann, `codegraph_explore` NICHT

| Use-Case | AiNetLinter | codegraph_explore | Differenz |
| :--- | :---: | :---: | :--- |
| "Welche Aufrufstellen ändern sich, wenn ich Commit X reverted?" | ✅ (`get_impact` mit `gitRef`) | ❌ | Stärke: Roslyn + Git-Integration |
| "Was bricht, wenn ich uncommittete Änderungen committe?" | ✅ (`get_impact` ohne Params) | ❌ | Stärke: `DiffImpactAnalyzer` |
| "Wer erbt von / implementiert X mit allen generischen Spezialisierungen?" | ✅ (`get_type_hierarchy`, Roslyn-aware) | ⚠️ (nur `implements`-Edges, keine Generic-Resolution) | Stärke: Roslyn kennt Constraint-Resolution |
| "Welche DI-Container-Registrierung hält diesen Service?" | ✅ (DI-Heuristik in `get_type_hierarchy`) | ❌ | Stärke: Regex auf `AddScoped<...>` etc. |
| "Property-`get` und `set` als ein Symbol behandeln" | ✅ (`NormalizeToOwningMember`) | ❌ (man muss File+Line pinnen) | Stärke: Accessor-Normalisierung |

### 5.3 Was mit Roslyn *nicht* statisch machbar ist

- **Reflection ohne Marker**: `typeof(T).GetMethod("Foo").Invoke(...)` — Roslyn sieht das `typeof`, aber das `GetMethod("Foo")` ist ein String-Lookup. Nicht statisch auflösbar.
- **`dynamic` Keyword**: `dynamic d = ...; d.Foo();` — Roslyn kennt den Typ nicht, kann keine Kante ziehen. (Wohl aber: Heuristik über `IDynamicMetaObjectProvider` / `[DynamicDependency]`-Attribute.)
- **C#-spezifische Synth-Kanten-Marker**: `ActivatorUtilities.CreateInstance`, `IServiceProvider.GetService<T>`, `MediatR` `IRequestHandler<T>`, `IOptions<T>`-Binding — Roslyn hat keine generische "ist DI-Dispatch"-Heuristik. Müsste man selbst bauen (oder per `get_type_hierarchy`-Heuristik erweitern).
- **PLINQ / `Task.Run` / `ThreadPool.QueueUserWorkItem`**: Continuations sind Roslyn nicht bekannt.
- **Source-Generators** (`partial` mit Generator-Output): Roslyn sieht den generierten Code nur, wenn `RunAnalyzers` läuft — die Symbol-Auflösung kennt ihn, aber `SyntaxTree` ist zur Compile-Zeit oft leer.

---

## 6. Phase 5 — Konkreter Vorschlag: `trace_flow` für AiNetLinter

### 6.1 Input-Schema

```json
{
  "name": "trace_flow",
  "description": "Multi-Symbol-Flow-Tracer. Nimmt 2-16 Symbol-Namen, baut den längsten Call-Chain zwischen ihnen, liefert nummerierte Flow-Sektion + Blast-Radius + verbatim Source-Bodies. Ersetzt N find_references + get_symbol_body Calls durch EINEN.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "symbols": {
        "type": "array",
        "items": { "type": "string" },
        "minItems": 2,
        "maxItems": 16,
        "description": "2-16 Symbol-Namen (qualifiziert oder teil-qualifiziert). Reihenfolge irrelevant. Co-Naming disambiguiert Overloads (siehe docs/agent-api.md)."
      },
      "maxDepth": {
        "type": "number",
        "default": 7,
        "minimum": 1,
        "maximum": 10,
        "description": "Maximale BFS-Tiefe. Default 7, aus codegraph_explore übernommen."
      },
      "includeBodies": {
        "type": "boolean",
        "default": true,
        "description": "Wenn true: Source-Bodies der Flow-Symbole anhängen. Bei false: nur Flow + Blast-Radius."
      },
      "maxBodyLines": {
        "type": "number",
        "default": 50,
        "description": "Per-Symbol-Body-Cap. Bei langen Methoden wird der Body auf das Fenster um den Call-Site reduziert."
      },
      "maxFiles": {
        "type": "number",
        "default": 6,
        "description": "Max. Anzahl Source-Sections. Adaptive Verkleinerung wenn Output > Hard-Cap."
      },
      "includeBlastRadius": {
        "type": "boolean",
        "default": true,
        "description": "Wenn true: am Ende 'Blast radius'-Sektion (Dependents + transitive Test-Coverage)."
      }
    },
    "required": ["symbols"]
  }
}
```

**Bewusste Abweichungen vom CodeGraph-Schema:**
- **Kein `query`-String**, sondern `symbols`-Array. AiNetLinter's Roslyn-Symbolauflösung ist strukturell, nicht heuristisch — ein NL-Query wäre Lügen-Marketing.
- **Mehr Body-Kontrolle** (`includeBodies`, `maxBodyLines`, `maxFiles`) weil AiNetLinter's Zielgruppe technische Roslyn-User sind.
- **Kein `projectPath`** — AiNetLinter hat per Design *eine* geladene Solution pro Server-Instanz.

### 6.2 Output-Schema (Beispiel)

````markdown
**Flow (call path among the symbols you queried)**

1. `PmsProductController::getList` (Controllers/PmsProductController.cs:42)
   ↓ calls
2. `PmsProductService.list` (Services/PmsProductService.cs:18)
   ↓ calls
3. `PmsProductServiceImpl.list` (Impl/PmsProductServiceImpl.cs:23)
   ↓ calls
4. `_repo.FindById` (Impl/PmsProductRepository.cs:15)

**Dynamic-dispatch boundaries (Roslyn-Äquivalent — keine statische Kante)**
- `PmsProductServiceImpl.list` (Impl/PmsProductServiceImpl.cs:25) — DI-Resolve-Site: `services.GetRequiredService<IRepository>()`. Kandidaten: `PmsProductRepository` (DI-Register: Program.cs:9).
- `PmsProductController::getList` (Controllers/PmsProductController.cs:48) — `[FromServices] IMediator`. Kandidaten: MediatR-Handler-Liste (siehe get_type_hierarchy).

**Blast radius**
- `PmsProductController::getList` — 3 callers; tests covering: PmsProductControllerTests.cs:18, IntegrationTests.cs:34
- `PmsProductService.list` — 5 callers; tests covering: PmsProductServiceTests.cs:12

Found 4 symbols across 3 files.

---

**`src/Services/PmsProductService.cs`** —
```csharp
18  public List<Product> list(Query q)
19  {
20      return _repo.Where(q.Spec).ToList();
21  }
```

**`src/Impl/PmsProductServiceImpl.cs`** —
```csharp
23  public List<Product> list(Query q)
24  {
25      var repo = services.GetRequiredService<IRepository>();
26      return repo.FindById(q.Id);
27  }
```
````

**Format-Unterschiede zu codegraph_explore:**
- Backticks für Symbol-Namen (C#-Konvention).
- File-Refs als `pfad:zeile` ohne `**`-Bold-Wrapping (AiNetLinter nutzt `pfad:zeile - kind: signatur`-Vokabular überall).
- Markdown-Code-Block mit `csharp`-Tag.

### 6.3 Algorithmus-Skizze (Prosa)

**Phase A: Resolution & Disambiguation** (1-2 s, warm Roslyn-Workspace)
1. Für jeden `symbols[i]`:
   - Versuche `SymbolIdentifierResolver.TryResolveByStableIdAsync` (DocumentationCommentId).
   - Sonst: Parse `Datei:Zeile:Spalte` falls Format matcht.
   - Sonst: `SymbolFinder.FindSourceDeclarationsAsync(namePattern, SymbolFilter.TypeAndMember)` + `EndsWith(identifier)`-Filter.
2. **Co-Naming-Filter** (für mehrdeutige Namen):
   - Wenn ein Token `>3` Definitionen hat, behalte nur Kandidaten, deren Container-Klasse (letztes Segment vor `::`/`.`) im Token-Pool ist.
3. Normalisiere Accessor-Symbole via `NormalizeToOwningMember`.
4. Cap auf 16 Seeds.

**Phase B: Forward-BFS mit Anker-Constraint** (0.5-2 s, abhängig von depth)
1. Initialisiere leere `parent: Map<ISymbol, {prev, edge, node}>`.
2. Enqueue alle Seeds (level 0).
3. Solange Queue nicht leer und `parent.size < 200`:
   - Dequeue (current, level).
   - Wenn `level >= maxDepth`: continue.
   - Hole Callees via:
     - `SymbolFinder.FindReferencesAsync(current).Locations` (nur Method-Decl-Locations herausfiltern)
     - Plus: `current.FindImplementationForInterfaceMember()` für Interface-Implementations.
     - Plus: `current.OverriddenMethod` für `override`.
     - Plus: Constructor-Chains.
   - Pro Callee: wenn nicht geseen → enqueue mit `level+1`.
4. Backtrack vom tiefsten Symbol, das auch in `named` ist (= Anker) zur längsten Kette.

**Phase C: Blast-Radius** (0.5-1 s)
- Für jedes Anchor-Symbol: `SymbolFinder.FindReferencesAsync` bis Tiefe 3.
- Filtere `*.Tests.cs` / `*.Test.cs`-Dateien → Coverage-Hinweis.
- Aggregiere per Datei, Top-N.

**Phase D: Body Assembly** (0.5-1 s)
- Gruppiere Anchor-Symbole per `ContainingFile`.
- Pro Datei: berechne Union der `[startLine, endLine]`-Spans.
- Wenn Union > `maxBodyLines` und Symbol ist Oversize: window auf `[callSite - 5, callSite + 30]`.
- Rendere als `**pfad** —\n```csharp\nbody\n````.

**Phase E: Dynamic-Boundary-Scan** (1-2 s, I/O-bound durch File-Reads)
- Für jedes nicht-verbundene Anchor-Symbol: lies Source-File, scanne nach:
  - `Activator.CreateInstance(`, `IServiceProvider.GetService`, `services.GetRequiredService<` (DI-Dispatch)
  - `MethodInfo.Invoke`, `GetMethod(`, `typeof(T).` (Reflection)
  - `dynamic `, `IDynamicMetaObjectProvider` (dynamic dispatch)
  - `Task.Run(`, `ThreadPool.QueueUserWorkItem(`, `Parallel.Invoke(` (Async-Decoupling)
  - `[FromServices]`, `[FromKeyedServices]` (ASP.NET)
- Pro Site: sammle Kandidaten via `GetTypeByMetadataName`, `FindImplementations` etc.

**Phase F: Truncation & Sufficiency-Notice**
- Wenn Total > Hard-Cap: prioritisiere Spine > Named > Blast-Radius > Bodies.
- Hänge an: `> Source for N files included above — do NOT re-read them. Run another trace_flow for area not covered.`

### 6.4 Roslyn nativ vs. selbst synthetisieren

| Datenpunkt | Roslyn nativ | Selbst synthetisieren |
| :--- | :---: | :---: |
| Symbol-Resolution | ✅ `SymbolFinder.FindSourceDeclarationsAsync`, `DocumentationCommentId.CreateDeclarationId` | — |
| Direkte Call-Sites | ✅ `SymbolFinder.FindReferencesAsync` | — |
| Interface-Implementations | ✅ `ISymbol.FindImplementationForInterfaceMember`, `ITypeSymbol.AllInterfaces` | — |
| Override-Chain | ✅ `IMethodSymbol.OverriddenMethod`, `OriginalDefinition` | — |
| DI-Registrierungen | ❌ | ✅ Regex auf `.cs` (existiert bereits in `DiRegistrationHeuristics`) |
| Reflection-Aufrufe | ❌ (Roslyn sieht nur `typeof`, nicht `GetMethod("X")`) | ⚠️ Pattern-Matching auf AST (`InvocationExpressionSyntax`) |
| `dynamic` Aufrufe | ⚠️ (`DynamicType` ist bekannt, aber Member-Lookup nicht) | ⚠️ Heuristik: Suche `dynamic`-Variablen + folgende `.Member()`-Aufrufe |
| Body-Extraction | ✅ `SyntaxNode.ToFullString()`, `SyntaxTree.GetText().GetSubText(span)` | — |
| Body-Windowing | ✅ `SyntaxNode.Span.Start`, `GetLineSpan`, `TextSpan` | — |
| Test-Coverage | ❌ | ⚠️ Namens-Heuristik (`*.Tests/*.Test/*Test.cs/*Tests.cs`) + Caller-Chain |
| Edge-Type-Labeling | ✅ `SymbolKind`, `MethodKind` | ⚠️ Heuristik: `virtual + !sealed` → `virtual call`; `interface member + impl` → `implements` |
| Cross-File-Dedup | ✅ `SymbolEqualityComparer.Default` | — |
| Anker-Constraint | ❌ | ✅ trivial im BFS |
| Output-Budget-Allokation | ❌ | ✅ Proportional-Split pro Datei, Oversize-Methoden auf Call-Site |

→ **~60% Roslyn-nativ, ~40% selbst synthetisieren.** Der selbst-zu-synthetisierende Anteil ist überwiegend Pattern-Matching auf AST-Strings — gut testbar, gut in `Scan`-Klassen isolierbar.

### 6.5 Aufwand-Schätzung

| Phase | Aufwand | Risiko |
| :--- | :---: | :--- |
| **MVP** — `symbols`-Bag + BFS + Co-Naming + Source-Bodies + Blast-Radius | **1 Woche** | Niedrig (Bausteine existieren: `FindReferencesTool`, `CallGraphTraversal`, `GetSymbolBodyTool`) |
| **+ Dynamic-Boundary-Scan** (DI/Reflection/dynamic Pattern-Matching) | **+3-4 Tage** | Mittel (Heuristik-Tuning, False-Positives) |
| **+ Adaptive Output-Budget** (per-File-Char-Cap, Oversize-Window) | **+2-3 Tage** | Niedrig (analog zu CodeGraph, gut testbar) |
| **+ Sufficiency-Doctrine-Hinweise** (Output-Boilerplate) | **+0.5 Tage** | Trivial |
| **+ Test-Suite** (Unit + Integration + Live-Repo) | **+3-4 Tage** | Mittel (mind. 20 Tests: BFS-Korrektheit, Co-Naming, Truncation, DI-Scan, Edge-Cases) |
| **Gesamt-MVP** | **~2 Wochen** | — |
| Volle Reflection-Unterstützung (ohne Attribute-Marker) | +2 Wochen | Hoch (grundsätzliche Grenzen) |
| Volle synthetisierte Kanten für ASP.NET Core / MediatR / IOptions | +1 Woche | Mittel |
| **Gesamt-Production-Ready** | **~5-6 Wochen** | — |

**Realistische Schätzung für eine erste Auslieferung:** **1–2 Wochen** für das Killer-Feature (Multi-Symbol-Flow + Bodies + Blast-Radius ohne Dynamic-Boundary-Scan). Dynamic-Boundary-Scan + Reflection-Heuristiken als separates Epic.

### 6.6 Token-Save-Schätzung

**Szenario:** "Wie kommt der Login vom Controller zur DB?" (siehe §3.6)

| Metrik | Heute (9 Calls) | Mit `trace_flow` (1 Call) | Save |
| :--- | ---: | ---: | ---: |
| Tool-Calls | 9 | 1 | **-89%** |
| JSON-RPC-Overhead | 9 × ~80 B Headers | 1 × ~80 B | -89% |
| Locations-Text | ~25 Zeilen `pfad:zeile` | ~12 Zeilen (Flow-Liste, kompakter) | -52% |
| Source-Bodies | ~95 Zeilen, duplikationsfrei | ~70 Zeilen (Window-Cap, keine Redundanz) | -26% |
| Total-Output | ~120 Zeilen + 9 Tool-Result-Wrapper | ~80 Zeilen + 1 Wrapper | **~-50%** |
| Latenz | 9 × Round-Trip (~5-50 ms je) | 1 × BFS (~50-200 ms) | -60% |

Konservativ: **40-60% Token-Save + 60-80% Latenz-Save** für typische Flow-Fragen. Bei großen Repos mit vielen Beteiligten Dateien steigt der Vorteil.

---

## 7. Phase 6 — Sekundäre Patterns

### 7.1 `codegraph_node` als Secondary Tool

**`codegraph_node` Vertrag** (`tools.ts:1063-1102`):
- **Mode 1 — File-Read-Ersatz:** `file=<pfad>` ohne `symbol` → liest Datei mit Line-Numbers + Dependents-Hinweis.
- **Mode 2 — One-Symbol:** `symbol=<name>` → Body + Caller/Callee-Trail in einem Call. Overloads: alle Bodies, mit `file`/`line` Pinning.

**AiNetLinter-Äquivalent:** `get_symbol_body` deckt Mode 2 *teilweise* ab (nur Body, kein Trail), und kein Tool deckt Mode 1 ab (File-Read mit Line-Numbers). `find_symbol` liefert Location-Liste, aber nicht den Source.

**Vorschlag:** `get_symbol_body` zu einem `get_node`-Tool aufwerten, das beides kann:
- Mit nur `file`-Param: Datei-Source wie `Read` + 1-Zeilen-Dependents-Hinweis.
- Mit `identifier`-Param: wie heute.
- Mit `identifier` + `includeCallers=true` + `includeCallees=true`: Body + Trail in einem Call (entspricht `codegraph_node` Mode 2).

**Aufwand:** 2-3 Tage. Der Trail ist eine Mini-Variante von `find_references` depth=1 + `GetMembers()`.

### 7.2 Sufficiency-Principle für AiNetLinter

CodeGraph's Anti-Pattern-Liste (`server-instructions.ts:57-66`) ist **nicht** auf ein bestimmtes Tool gemünzt — sie ist eine **Verhaltensregel für den Agenten**. AiNetLinter hat heute keine analoge `server-instructions.ts` (siehe `McpCodeGraphServer.cs:InitializeAsync` — der Server schickt vermutlich nichts, oder nur einen kurzen C#-Hinweis).

**Vorschlag:** Eine `src/AiNetLinter/Mcp/ServerInstructions.cs` einführen, die dem `initialize`-Response folgendes mitgibt:
- *"10 Tools, alle C#-pur (Roslyn). Suche vor dem Edit mit `find_symbol` oder `trace_flow`."*
- *"Wenn `trace_flow` Source liefert: nicht nochmal lesen."*
- *"Git-Diff-Impact via `get_impact` (kein Parameter = uncommitted Changes)."*
- *"Fallback für .js/.razor/.xaml/.html/.css: `search_pattern`."*

**Aufwand:** 0.5 Tage (reiner Text, keine Logik).

### 7.3 "Next Tool Hint" — soll der MCP-Server vorschlagen?

CodeGraph's dynamische Tool-Description (`tools.ts:1370-1400`) ändert den Description-Text von `codegraph_explore` abhängig von Repo-Größe (z. B. "1500 files — expect 3-5 explore calls"). Das ist eine **proaktive Lern-Hilfe**.

Für AiNetLinter denkbar:
- Wenn `get_impact` 0 Treffer liefert → Hint: "Keine Aufrufstellen — Symbol evtl. ungenutzt. Versuche `get_type_hierarchy` für Type-Beziehungen."
- Wenn `find_references` mit `depth=1` 0 Treffer liefert → Hint: "Keine direkten Aufrufer. Versuche `depth=3` für transitive oder `get_type_hierarchy` für Vererbungs-Hierarchie."
- Wenn `find_symbol` 0 Treffer in .cs liefert → Hint: "Symbol in Nicht-C#-Datei? Versuche `search_pattern`."

**Aufwand:** 1-2 Tage (in jeden Tool-Output einen optionalen Hint-Block einbauen, gesteuert von einem zentralen `McpHintProvider`).

---

## 8. Konkrete Empfehlung

### 8.1 Bewertung

| Kriterium | Bewertung |
| :--- | :--- |
| Killer-Feature-Potenzial | **Sehr hoch** — `trace_flow` ist der einzige Weg, die 9-Call-Sequenz in 1 Call zu kollabieren |
| Machbarkeit mit Roslyn | **Hoch** — ~60% Roslyn-nativ, der Rest gut testbar |
| Risiko (False-Positives in Heuristiken) | **Mittel** — Dynamic-Boundary-Scan braucht Tuning |
| Aufwand bis zu erstem Mehrwert | **1-2 Wochen** (ohne Dynamic-Boundary-Scan) |
| Token-Save für End-User | **40-60%** (konservativ) |
| Differenzierung von CodeGraph | **Git-Diff-Integration + Roslyn-Präzision** bleiben erhalten |

### 8.2 Empfehlung

# **Ja — `trace_flow` für AiNetLinter bauen, mit Vorbehalt.**

**Begründung:**

- Die **Multi-Symbol-Flow-Lücke** ist die einzige kategorische Lücke im aktuellen Tool-Set. Alles andere (Dynamic-Boundary-Scan, Coverage-Hinweise, Sufficiency-Doctrine) ist nice-to-have.
- **Aufwand ist gering** im Verhältnis zum Nutzen: 1–2 Wochen MVP mit ~60% Roslyn-nativ. Die Building Blocks existieren (`FindReferencesTool.ResolveSymbolAsync`, `CallGraphTraversal`, `GetSymbolBodyTool`, `DiRegistrationHeuristics`, `McpTruncation`).
- **AiNetLinter behält seine Stärken** — Git-Diff-Integration, Roslyn-Präzision, C#-Tiefe. `trace_flow` *addiert* einen kategorial neuen Use-Case, ohne bestehende zu kannibalisieren.

**Vorbehalt (was die Empfehlung zu "Ja-aber" macht):**

- **Dynamic-Boundary-Scan + Reflection-Heuristiken** sind nicht statisch-deterministisch. Bevor `trace_flow` als "Production Ready" gilt, muss der MVP **ohne** synthetisierte Kanten ausgeliefert werden, mit klarer Doku: "Dynamic dispatch is not exhaustively traced — verify manually for reflection-heavy code paths."
- **Output-Budget-Tuning** ist nicht trivial — die adaptive Allocation per Datei ist eine eigene Forschungsfrage. Für MVP reicht statischer `maxFiles`-Cap.
- **NL-Query** sollte *nicht* implementiert werden. AiNetLinter's Symbolauflösung ist strukturell, nicht heuristisch — ein NL-Front-End wäre Marketing-Lüge.

**Priorisierter Umsetzungsplan:**

1. **Sprint 1 (1 Woche):** `trace_flow` MVP — `symbols`-Bag + Forward-BFS + Co-Naming + Body-Assembly + Trunkierung. ~15 Unit-Tests, 1 Integration-Test.
2. **Sprint 2 (3-4 Tage):** Blast-Radius + Test-Coverage-Heuristik.
3. **Sprint 3 (2-3 Tage):** `get_symbol_body` → `get_node` aufwerten (File-Read + Trail).
4. **Sprint 4 (0.5-1 Tag):** `server-instructions.cs` mit Sufficiency-Doctrine.
5. **Sprint 5 (separates Epic, ~2 Wochen):** Dynamic-Boundary-Scan + Reflection-Heuristik + Adaptive-Budget.

**Erfolgs-Messung:**
- `dotnet test` grün (Pflicht).
- 3 Test-Fixtures (kleines / mittleres / großes C#-Repo) mit manuell erstellten Ground-Truth-Flows: Recall ≥ 90%.
- Token-Save-Vergleich: vorher (9 Calls) vs. nachher (1 Call), Ziel ≥ 40%.

---

## Anhang A — Referenzierte Quellen

| Datei | Zeilen | Was |
| :--- | :---: | :--- |
| `C:/Daten/Entwicklung/GitHub/codegraph/src/mcp/tools.ts` | 1104-1124 | `codegraph_explore` Input-Schema |
| `C:/Daten/Entwicklung/GitHub/codegraph/src/mcp/tools.ts` | 2440-2706 | `buildFlowFromNamedSymbols` |
| `C:/Daten/Entwicklung/GitHub/codegraph/src/mcp/tools.ts` | 2719-2848 | `buildDynamicBoundaries` + `buildPolymorphicBoundaries` |
| `C:/Daten/Entwicklung/GitHub/codegraph/src/mcp/server-instructions.ts` | 1-106 | Server-Instructions / Sufficiency-Doctrine |
| `C:/Daten/Entwicklung/GitHub/codegraph/__tests__/explore-blast-radius.test.ts` | 1-115 | Blast-Radius-Tests |
| `C:/Daten/Entwicklung/GitHub/codegraph/__tests__/explore-synth-constant-endpoints.test.ts` | 1-87 | Synth-Edges-Tests (RTK) |
| `C:/Daten/Entwicklung/GitHub/codegraph/__tests__/explore-allocation-e2e.test.ts` | 1-160+ | Adaptive-Budget-Tests |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` | 1-157 | Tool-Implementierung |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` | 1-116 | Tool-Implementierung |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` | 1-138 | Tool-Implementierung |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` | 1-100 | Tool-Implementierung |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/CallGraphTraversal.cs` | 1-132 | BFS-Implementierung (depth 1-3) |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpTruncation.cs` | 1-69 | Trunkierungs-Helper |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs` | 1-162 | Tool-Registrierungen + Beschreibungen |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md` | 240-414 | Tool-Übersicht + E.1/E.2/E.3 Epics |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/features/01-codegraph-recon.md` | — | Vorherige Recon (Schwächen-Analyse) |
| `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/features/02-ainetlinter-mcp-current.md` | — | Vorherige AiNetLinter-MCP-IST-Analyse |
