---
unit: 006
task: codegraph-mcp-server
workflow: dynamic-loop
type: plan
created_by: planer
created_at: 2026-08-01
epic: EPIC-06 (Robustheit bei Compile-/Solution-Fehlern)
extends:
  - konzept.md Z. 102-103 (EPIC-06)
  - konzept.md Z. 146-153 (Muss-Haven Fehlerbehandlung)
  - konzept.md Z. 609-611 (DoD: Compile-Fehler-Verhalten)
  - konzept.md Z. 564-567 (Server-Betrieb Schritt 3: strukturierte Fehlerantwort)
  - units/002/plan.md (M-1 Hint-Bug-Muster, A3-Methodik)
  - units/004/plan.md (E2E-Fixture-Pattern, neue Test-Datei pro Tool)
  - units/005/plan.md (P0/P1-Trunkierung-Vorbild, Re-Messung pro Einheit)
  - TD-005 (McpCodeGraphServer-Pull-in-Muster, beibehalten)
  - TD-008 (PathOverride 2700 für FindReferencesTool, unverändert)
  - TD-011 (SymbolGraphToolRegistrations Footprint knapp, Pflichtmessung)
  - TD-003 (RegisterMSBuild-Race: durch Collection-Serialisierung in 006-Tests
    umgangen, nicht strukturell gefixt — siehe Scope-Grenze)
---

# Plan Einheit 006 — Robustheit bei Compile-/Solution-Fehlern (EPIC-06)

## Ziel der Einheit

Der MCP-Server liefert für alle drei in `konzept.md` Z. 146-153 und
Z. 609-611 genannten Fehlerfälle eine **strukturierte** Antwort im
bestehenden `[ERROR]`-Format aus `Docs/agent-api.md` (Z. 147-150) statt
eines Absturzes: (a) Solution lädt gar nicht, (b) Solution lädt mit
Workspace-Diagnosen / Compile-Fehlern in einzelnen Dateien, (c)
Tool-Call trifft auf einen unterwarteten internen Fehler im Tool-Pfad.
Damit ist EPIC-06 vollständig erfüllt: DoD-Kriterien Z. 609-613 ("eine
Solution mit Compile-Fehlern in einer Datei liefert für nicht betroffene
Dateien weiterhin korrekte Antworten, für die betroffene Datei einen
Warnhinweis statt eines Absturzes" + "eine nicht ladbare Solution führt
dazu, dass der Server startet, aber jeder Tool-Call einen strukturierten
Fehler statt eines Crashs liefert") systematisch umgesetzt und durch
A3-testbare E2E-Tests gegen eine Compile-Fehler-Fixture abgesichert.

**Bewusst NICHT in 006:** P0/P1-Extensions (Kaltstart, Auto-Discovery,
Staleness-Sweep, Call-Log, Verzeichnis-Sweep, `ILintConsole`-MCP-Modus),
TD-003-Race-Condition-Fix, EPIC-07-Tests-Ausbau, EPIC-08-Doku, weitere
Scanner-Splits, `PathOverrides`-Wert-Erhöhung, Trunkierungs- oder
Miss-Hint-Änderungen (alle P0/P1 fertig in 002/003/004/005). Scope-Hart.

## Scope-Entscheidung

**Gewählt: Vollständiger EPIC-06-Audit + Compile-Fehler-Warnhinweis in
allen 9 Tools + Server-Lifecycle-Test für nicht-ladbare Solution.**
Begründung:

- (a) **Audit ist die Kern-Pflicht** aus `konzept.md` Z. 102-103 ("Audit
  aller 9 Tools auf den strukturierten `[ERROR]`-Pfad statt Absturz"). Die
  Pflicht ist explizit **alle 9 Tools** — nicht nur die 6 datei-
  spezifischen. Selbst wenn nur 1 Tool eine Lücke hat, reißt das
  DoD-Kriterium ("Server bleibt am Leben").
- (b) **Compile-Fehler-Warnhinweis ist die andere Kern-Pflicht** aus
  Z. 152-153 ("Tools liefern für nicht betroffene Bereiche weiterhin
  korrekte Antworten, für betroffene Bereiche einen Warnhinweis").
  Roslyn toleriert fehlerhaften Code (gibt Diagnostics aus, crasht
  nicht), aber für den **Endnutzer** (Agent) ist nicht erkennbar, dass
  die Antwort möglicherweise unvollständig ist. Das ist der
  Informations-Verlust, den EPIC-06 heilt.
- (c) **Server-Lifecycle ist der dritte Eckpfeiler** aus Z. 146-150
  ("Solution lädt gar nicht → jeder Tool-Call liefert eine strukturierte
  Fehlerantwort … Server bleibt am Leben"). Konzeptuell getrennt vom
  Compile-Fehler-Pfad, aber architektonisch derselbe
  `SolutionNotLoaded()`-Helper (bereits 9/9 vorhanden, dieser Pfad ist
  effektiv schon umgesetzt — fehlt nur der explizite E2E-Test dafür,
  der die DoD-Aussage stützt).

**Bewusst NICHT in 006:**

- **Keine** P0/P1-Extensions (Kaltstart entkoppeln, Auto-Discovery,
  Staleness-Sweep mit Verzeichnis-`mtime`, Call-Log, Verzeichnis-Sweep
  für neue/gelöschte Dateien, `ILintConsole`-MCP-Modus) — separate
  Folge-Einheiten.
- **Kein** TD-003-Race-Condition-Fix (eigener Punkt für EPIC-07 oder
  später). TD-003 wird in 006-Tests durch `[Collection(...)]`-
  Serialisierung **umgangen**, nicht behoben.
- **Kein** EPIC-07-Tests-Ausbau (jenseits der 006-spezifischen Tests).
- **Kein** EPIC-08-Doku (`Docs/agent-api.md`, `Docs/integration.md`,
  `Docs/ROADMAP.md`).
- **Kein** Scanner-Split für ein bestehendes Tool.
- **Keine** Trunkierungs-Änderungen (4/4 Listen-Tools fertig).
- **Keine** Miss-Hint-Änderungen (003 abgeschlossen).
- **Kein** `PathOverrides`-Wert erhöhen.
- **Kein** Tool-Set erweitern (kein `get_active_rules`, kein
  `get_symbol_body` — P2-Backlog).

## Vor-der-Planung-Checks (Kernel Teil B "Drift" / "Duplikate durch Blindheit")

### Check 1 — 9 Tools inventarisiert (Lücken-Audit, gemessen 2026-08-01)

**Befund (gelesen, alle 9 `Mcp/Tools/*Tool.cs`):**

| Tool | `SolutionNotLoaded()`-Pfad | Compile-Fehler-Pfad | Ungefangene Exceptions? |
|---|---|---|---|
| `find_symbol` (Z. 39-40) | ✓ | ✗ (kein Per-Datei-Hinweis) | Scanner-Symbolfinder-API: toleriert, keine direkten `try/catch` nötig |
| `find_references` (Z. 31-32) | ✓ | ✗ | `FindCallSitesAsync`: toleriert |
| `get_impact` (Z. 25-26) | ✓ | ✗ | `DiffImpactAnalyzer.AnalyzeAsync` + `FindCallSitesAsync`: toleriert |
| `get_type_hierarchy` (Z. 23-24) | ✓ | ✗ | `FindDerivedClassesAsync`/`FindImplementationsAsync`: toleriert |
| `get_file_skeleton` (Z. 25-26) | ✓ | ✗ | `SkeletonMapBuilder.ExtractFromDocumentAsync`: toleriert |
| `get_index_scope` (Z. 22-23) | ✓ | n/a (aggregate) | `GetIndexScopeScanner.BuildBreakdownText`: nur File-System-IO-`try/catch` |
| `get_hotspots` (Z. 24-25) | ✓ | n/a (aggregate) | `GetHotspotsScanner`: `try/catch (IOException)` an File-Read-Stellen |
| `get_violations` (Z. 27-28) | ✓ | n/a (Lint, nicht Compile) | `GetViolationsScanner` hat defensives `try/catch (Exception ex)` → `[ERROR]: ANALYSIS_FAILED` (Z. 64-71) |
| `search_pattern` (Z. 48-49) | ✓ | ✗ | `try/catch (ArgumentException)` für ungültige Regex (Z. 58-64), IO-`try/catch` im Scanner |

**Erkenntnisse:**

- **9/9 Tools haben den `SolutionNotLoaded()`-Pfad** (M-1-Bug-Muster
  aus 002 ist sauber durchgezogen). **Kein** Tool wirft eine
  unbehandelte `NullReferenceException`, wenn `_catalog` null ist.
- **0/9 Tools haben einen expliziten Compile-Fehler-Warnhinweis** —
  Roslyn toleriert fehlerhaften Code zwar, aber der Agent bekommt
  nicht mitgeteilt, **dass** die Antwort möglicherweise unvollständig
  ist. Das ist die echte EPIC-06-Lücke.
- `GetViolationsScanner` hat bereits einen defensiven
  `catch (Exception ex) when (ex is not OperationCanceledException)`-
  Pfad (Z. 64-71), der `[ERROR]: ANALYSIS_FAILED` liefert — das ist
  bereits die Struktur, die für andere Tools wiederverwendet werden
  kann.
- `SearchPatternTool` hat einen analogen defensiven Pfad
  (`try/catch (ArgumentException)` Z. 58-64 für Regex-Syntax). Andere
  Tools verlassen sich auf Roslyns Toleranz, ohne defensiven Wrapper.

**Entscheidung im Plan:**

- (a) **Audit-Ergebnis dokumentieren** in `result.md` Abschnitt
  "Audit-Befund" — als Bestätigung, dass 9/9 `SolutionNotLoaded` haben
  (kein Code-Eingriff nötig), und dass 6/9 Tools einen neuen
  Compile-Fehler-Warnhinweis bekommen (siehe nächster Punkt).
- (b) **Defensive `try/catch`-Wrapper in den 6 datei-spezifischen
  Tools** (siehe Check 2): einheitliches Muster, das
  `McpToolResults.CompilationError(message, context)` für
  unerwartete Roslyn-Exceptions wirft. Vorbild:
  `GetViolationsScanner` Z. 64-71. Hilft gegen den "seltenen"
  Edge-Case, dass Roslyn in einem fehlerhaften Code-Pfad doch einmal
  eine Exception wirft, die bisher ungefangen den Server crasht.
- (c) **Compile-Fehler-Warnhinweis** in den 6 datei-spezifischen
  Tools + 3 aggregate Tools (siehe Check 4).

### Check 2 — `McpCodeGraphServer.GetCurrentSolution`-Fehlerverhalten

**Befund (gelesen, `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:77-86`):**

```csharp
public Solution? GetCurrentSolution()
{
    lock (_lock)
    {
        if (_catalog is null) return null;
        RefreshStaleDocuments();
        return _catalog.Solution;
    }
}
```

- **Wirft keine Exception** bei nicht-geladener Solution (gibt
  `null` zurück). 9/9 Tools reagieren korrekt mit
  `McpToolResults.SolutionNotLoaded()`. ✓
- **Wirft keine Exception** bei Datei-IO-Fehlern im
  Staleness-Check (`TryApplyContentChange` Z. 162-181,
  `try/catch (IOException ex)` Z. 176-180 — `[WARN]` auf Console).
- **`IsLoaded` Property** (Z. 48): `true` iff `_catalog is not null`.
- **`SourceFileCatalog.HasLoadingErrors`** Flag (in
  `SourceFileCatalog.cs:34`): wird in
  `McpServerCommand.TryLoadSolutionAsync` Z. 147-150 **nur geloggt**
  (`"[WARN]: Solution mit Workspace-Diagnosen geladen: ..."`), nicht
  an die Tool-Schicht propagiert.

**Lücke:** `HasLoadingErrors` ist heute ein **optisches** Signal für
den Server-Log, nicht für den Tool-Call. Für EPIC-06 brauchen die
Tools Zugriff auf "gibt es in dieser Solution Compile-Fehler, und
wenn ja, in welchen Dateien" — das ist **nicht** dasselbe wie
`HasLoadingErrors` (das nur MSBuild-Workspace-Diagnosen umfasst,
nicht Roslyn-Compile-Fehler nach dem Load).

**Entscheidung im Plan:**

- **Neuer Helper `McpCompileDiagnostics`** (siehe Check 4) als
  Brücke zwischen Roslyn-`Compilation.GetDiagnostics()` und der
  Tool-Schicht. `McpCodeGraphServer` bekommt **keine** neue
  Property (`HasLoadingErrors` bleibt unverändert) — das wäre eine
  Verbreiterung des Server-Surface (gegen TD-005-Muster). Stattdessen
  greifen die Tools **on-demand** über den Helper zu.
- **Kein** Eingriff in `McpCodeGraphServer` selbst, außer einer
  minimalen Read-only-Methode `GetCurrentCompilationAsync(ct)`, die
  die aktuelle `Compilation` liefert (für `GetDiagnostics`).
  Existiert implizit über `Solution.Projects.First().GetCompilationAsync()`,
  aber `McpCompileDiagnostics` bündelt das für alle Tools an einer
  Stelle. **+5-8 Z. in `McpCodeGraphServer`** — Pflichtmessung nach
  Coder-Schritt.

### Check 3 — `LinterErrorCodes` + `McpToolResults.Error`-Format-Konsistenz

**Befund:**

- `LinterErrorCodes.cs` enthält 15 Codes, davon `WorkspaceDiagnostic`
  bereits definiert (Z. 16) — **ungenutzt** bisher. Perfekt für
  Compile-Fehler-Diagnostik. **Wiederverwenden**, nicht neu anlegen
  (sonst Duplikat, Kernel Teil B "Duplikate durch Blindheit").
- `LinterErrorFormatter.Format` liefert einheitliches Format
  `[ERROR]: <CODE>: <message>` + optional `context` + `hint`
  (gelesen, `LinterErrorFormatter.cs:9-21`).
- `McpToolResults.Error(code, message, context?, hint?)` ist die
  zentrale Builder-Methode (Z. 21-29) — 9/9 Tools nutzen sie
  konsistent.
- **Lücken im Helper-Set:** kein dedizierter Helper für
  "Compile-Fehler in dieser Datei" oder "Aggregat: N Dateien mit
  Compile-Fehlern". Bestehende Helper sind:
  - `SolutionNotLoaded()` (Z. 35-41) — einparametrisch
  - `SymbolNotFound(identifier)` (Z. 47-54)
  - `AmbiguousSymbol(identifier, candidateLines)` (Z. 61-68)
  - `InvalidArgument(message)` (Z. 74-80)
  - `FileNotFound(relativePath)` (Z. 87-94)

**Entscheidung im Plan:**

- (a) **Neuer Helper** `McpToolResults.WarningsSection(text)`: liefert
  einen normalen Text-Block (kein `IsError = true`), der **vor**
  dem eigentlichen Tool-Output eingefügt wird und einen Warnhinweis
  enthält. **Nicht** ein `[ERROR]`, weil Compile-Fehler nicht
  bedeuten, dass der Tool-Call gescheitert ist — der Output ist
  weiterhin nützlich, nur möglicherweise unvollständig.
  Format: `Hinweis: Diese Datei hat N Compile-Fehler — Ergebnis ist
  möglicherweise unvollständig. Diagnostics: ...`.
- (b) **Neuer Helper** `McpToolResults.CompilationError(message,
  context)`: liefert ein `[ERROR]: WORKSPACE_DIAGNOSTIC: ...`-
  Ergebnis (Code = `WorkspaceDiagnostic` wiederverwendet). Wird
  genutzt, wenn ein Tool wegen Compile-Fehlern gar nicht
  sinnvoll antworten kann.
- (c) **Kein** neuer LinterErrorCode — `WorkspaceDiagnostic` ist
  semantisch passend ("Diagnose aus dem Roslyn-Workspace") und
  bereits definiert.

### Check 4 — Compile-Fehler-Fixture

**Befund (gelesen, `tests/Fixtures/`):**

- `BaselineMiniFixtureWorkspace` (1 Projekt, 1 Datei) — keine
  Compile-Fehler.
- `SymbolGraphMiniFixtureWorkspace` (1 Projekt, 5 .cs-Dateien + 4
  Web-Dateien) — keine Compile-Fehler.
- `GitImpactMiniFixtureWorkspace` (1 Projekt, 2 .cs-Dateien) —
  keine Compile-Fehler.
- **Keine** Fixture mit intentionalen Compile-Fehlern.

**Erkenntnis:** Eine Compile-Fehler-Fixture muss neu erstellt werden.
Vorgeschlagenes Layout (`tests/Fixtures/CompileErrorMini/`):

```
CompileErrorMini/
  CompileErrorMini.slnx
  src/CompileErrorMini/
    CompileErrorMini.csproj
    ValidClassA.cs           (kompiliert sauber, eine Klasse + Methode)
    ValidClassB.cs           (kompiliert sauber, andere Names)
    ValidClassC.cs           (kompiliert sauber, weiterer Namespace)
    BrokenClassA.cs          (Syntax-Fehler: Klammer fehlt)
    BrokenClassB.cs          (Semantischer Fehler: undef. Typ)
    BrokenClassC.cs          (weiterer Compile-Fehler)
```

- 3 intakte + 3 kaputte Dateien (entspricht Konzept-Vorgabe "viele
  intakte Dateien, eine/einige kaputte Dateien").
- Compile-Fehler sind **deterministisch** und **lokalisiert** in
  den `Broken*`-Dateien — `find_symbol` für `ValidClassA` matcht
  nur in `ValidClassA.cs`, ohne Warnhinweis; `find_symbol` für
  einen Bezeichner in `BrokenClassA` löst Warnhinweis aus.
- `BrokenClassA.cs` mit `public class BrokenClassA { public void
  F( { } }` (offene Klammer in Methodensignatur) — Roslyn meldet
  CS1513 "} expected".
- `BrokenClassB.cs` mit `public class BrokenClassB : DoesNotExist
  { }` — CS0246.
- `BrokenClassC.cs` mit `public class BrokenClassC { private
  UndefinedType field; }` — CS0103/CS0246.

**Entscheidung im Plan:**

- **Neue Fixture** `CompileErrorMiniFixtureWorkspace.cs` in
  `src/AiNetLinter.Tests/Fixtures/` (analog
  `BaselineMiniFixtureWorkspace` Pattern, `tests/Fixtures/
  CompileErrorMini/` als Quell-Verzeichnis).
- **Neue C#-Dateien** für die Fixture: 6 .cs-Dateien, 1 .slnx,
  1 .csproj. Coder erstellt sie.
- **TD-005 / TD-011 Konsequenz:** neue Fixture-Datei trägt **nicht**
  in den `AIContextFootprint` einer einzelnen Tool-Klasse bei, weil
  Fixtures im Test-Projekt liegen, nicht in `src/AiNetLinter/`.

### Check 5 — TD-003 (`RegisterMSBuild`-Race) in 006-Tests

**Befund (gelesen, `tech-debt.md:TD-003`):**

> Führt bei parallel laufenden Testklassen, die
> `SourceFileCatalog.LoadAsync` erstmalig aufrufen, intermittierend
> zu `InvalidOperationException`. Vorschlag: `RegisterMSBuild()`
> mit statischem Lock absichern. **Vor weiteren MCP-Integrationstests
> (EPIC-07) angehen**, da die Kollisionswahrscheinlichkeit mit jeder
> weiteren parallelen Testklasse steigt.

**Kollisionswahrscheinlichkeit in 006:**

- 006 plant **mehrere neue E2E-Test-Klassen** (siehe Check 6 unten):
  voraussichtlich 4-5 neue `RunAsync_...Tests.cs` analog 004/005
  (`McpServerCommandFindReferencesTests.cs`,
  `McpServerCommandCompileErrorTests.cs`, etc.). Jede startet einen
  frischen Subprozess, jeder ruft `SourceFileCatalog.LoadAsync`
  erstmalig auf. **TD-003-Kollisionswahrscheinlichkeit steigt
  signifikant.**
- 004/005 E2E-Tests sind in `[Collection("ConsoleTestCollection")]`
  (siehe 004 `McpServerCommandFindSymbolTests.cs:20`,
  005 `McpServerCommandGetImpactTests.cs:21`,
  `McpServerCommandFindReferencesTests.cs:20`). Das serialisiert
  sie — **umgeht** die Race für diese Tests, ohne TD-003 strukturell
  zu lösen.

**Entscheidung im Plan:**

- **Alle 006-E2E-Tests** bekommen `[Collection("ConsoleTestCollection")]`
  (oder eine neue, feiner-granulare Collection, falls die bestehende
  schon zu langsam wird). Damit sind sie mit 004/005 E2E-Tests
  serialisiert, TD-003-Race ist **für 006-Tests umgangen**.
- **Kein** struktureller Fix für TD-003 in 006. Das bleibt ein
  eigenes Thema für EPIC-07 oder später (wie in `tech-debt.md`
  vorgeschlagen). TD-003-Status bleibt **offen**.
- **Dokumentation** in `result.md`: "006-Tests sind in
  `ConsoleTestCollection` serialisiert; TD-003-Race in 006-Tests
  nicht aufgetreten, Race-Risiko für künftige parallele Test-
  Klassen bleibt offen."

### Check 6 — Footprint-Lage vor 006 (TD-011-Pflicht, gemessen 2026-08-01)

**Befund (gemessen mit `dotnet run --project src/AiNetLinter --
--footprint <Class> --path .`):**

| Klasse | Z. | Limit | Puffer | TD-Status |
|---|---:|---:|---:|---|
| `FindSymbolTool` | 2491 | 2700 (PathOverride) | 209 | TD-008-Schutz |
| `FindReferencesTool` | 2522 | 2700 (PathOverride) | 178 | TD-008-Schutz |
| `GetImpactTool` | 2495 | 2500 | **5** ⚠ | TD-011-Knappheit |
| `GetTypeHierarchyTool` | ~1490 | 2500 | — | unverändert |
| `GetFileSkeletonTool` | ~1000 | 2500 | — | unverändert |
| `GetIndexScopeTool` | ~900 | 2500 | — | unverändert |
| `GetHotspotsTool` | ~990 | 2500 | — | unverändert |
| `GetViolationsTool` | ~1450 | 2500 | — | unverändert |
| `SearchPatternTool` | 2482 | 2500 | 18 | TD-010 |
| `SymbolGraphToolRegistrations` | 2494 | 2500 | **6** ⚠ | **TD-011 versschärft** |
| `McpServerOptionsFactory` | 2484 | 2500 | 16 | TD-014 |
| `McpToolResults` | ~110 | 2500 | — | unverändert |
| `McpCodeGraphServer` | 184 | 2500 | — | **+5-8 erwartet** (neue Methode) |
| `McpServerCommandTests.cs` | 426/500 | 500 | 74 | — |
| `McpCompileDiagnostics` (NEU) | n/a | 2500 | — | wird in 006 angelegt |

**006-Eingriffspunkte (Schätzung):**

- **`McpCompileDiagnostics.cs` (NEU, ~60-80 Z.):** Helper-Klasse
  für Compile-Diagnostics-Bündelung. Reine Funktion
  (`Compilation → IReadOnlyList<CompileDiagnostic>`), keine
  `McpCodeGraphServer`-Abhängigkeit, direkt unit-testbar. +
- **`McpCodeGraphServer` (+5-8 Z.):** `GetCurrentCompilationAsync
  (ct)`-Methode (oder vergleichbar). Minimaler Eingriff.
- **`McpToolResults` (+15-25 Z.):** `WarningsSection(text)` und
  ggf. `CompilationError(message, context)`-Helper.
- **6 Tools** (`find_symbol`, `find_references`, `get_impact`,
  `get_type_hierarchy`, `get_file_skeleton`, `search_pattern`):
  jeweils +3-6 Z. für Aufruf von `McpCompileDiagnostics` + ggf.
  Warnhinweis-Sektion anhängen. +20-40 Z. gesamt.
- **3 Aggregate-Tools** (`get_index_scope`, `get_hotspots`,
  `get_violations`): jeweils +2-4 Z. für Header-Zeile. +10 Z.
  gesamt.
- **Konsequenz für `GetImpactTool` (Puffer 5):** Trunkierungs-
  Pflicht-Messung **vor und nach** Coder-Schritt. Erwartung: 2495
  + 4 = 2499, **knapp aber legal**. Falls > 2500: Description-
  Kürzung (analog 004-Plan-Check 3).
- **Konsequenz für `SymbolGraphToolRegistrations` (Puffer 6):**
  006 erweitert **keine** Tool-Description (kein Trunkierungs-Text
  o. ä.) — bleibt 2494. TD-011 bleibt offen (Puffer-Schrumpfung
  wird in 006 nicht verschärft).

**Entscheidung im Plan:**

- (a) **Pflichtmessung** vor und nach allen 006-Coder-Schritten in
  `result.md` Abschnitt "Footprint" (analog 005-Result-Vorbild).
- (b) **Bei `GetImpactTool` > 2500:** Plan-Abweichung 1 (siehe
  Schritt 9 unten) — Description-Kürzung um 1-2 Sätze im MCP-
  Delegate, **kein** PathOverride.
- (c) **Bei `SymbolGraphToolRegistrations` > 2500:** kosmetische
  Description-Kürzung um einen Satz im MCP-Delegate.

## Konkretes Vorgehen (Schritt-für-Schritt für den Coder)

### Schritt 0 — Pre-Build-Check + Footprint-Baseline (gemessen)

Vor jeder Code-Änderung:

1. `dotnet build AiNetLinter.slnx` — muss grün sein.
2. `dotnet test AiNetLinter.slnx --no-build` — muss grün sein
   (Baseline-Tests, A3).
3. Footprint-Messung pro betroffener Klasse (`--footprint`-Flag),
   exakt wie 005-Schritt-0 dokumentiert. Werte in
   `result.md` Abschnitt "Footprint-Baseline" eintragen.

**Erwartetes Ergebnis:** Build grün, Tests grün (mind. 1114/1114
aus 005-Stand), Footprints wie in Check 6 dokumentiert.

### Schritt 1 — Neue Fixture `CompileErrorMiniFixtureWorkspace` anlegen

1. **Quell-Files in `tests/Fixtures/CompileErrorMini/`** erstellen
   (analog `tests/Fixtures/SymbolGraphMini/`):
   - `CompileErrorMini.slnx` (leeres XML-Root)
   - `src/CompileErrorMini/CompileErrorMini.csproj` (Sdk-Format,
     `TargetFramework=net10.0`, **keine** `<Nullable>`-Direktive
     oder `<LangVersion>` um die Compile-Fehler klar zu halten)
   - `src/CompileErrorMini/ValidClassA.cs`, `ValidClassB.cs`,
     `ValidClassC.cs` (3 intakte Klassen)
   - `src/CompileErrorMini/BrokenClassA.cs`, `BrokenClassB.cs`,
     `BrokenClassC.cs` (3 Klassen mit Compile-Fehlern wie in
     Check 4 beschrieben)
2. **Fixture-Klasse**
   `src/AiNetLinter.Tests/Fixtures/CompileErrorMiniFixtureWorkspace.cs`
   (analog `SymbolGraphMiniFixtureWorkspace.cs` — `Path.Combine(
   FindSolutionRoot(), "tests", "Fixtures", "CompileErrorMini")`,
   Temp-Kopie mit `CopyFixture`, `IDisposable` mit `Directory.Delete
   (recursive: true)`).
3. **Property `BrokenClassAPath`** etc. analog `ViolatingClassPath`
   in `BaselineMiniFixtureWorkspace.cs` (für E2E-Tests, die
   Compile-Fehler-Datei als Argument nutzen).
4. **Build-Check** der Fixture:
   `dotnet build tests/Fixtures/CompileErrorMini/src/CompileErrorMini/
   CompileErrorMini.csproj` — muss **rot** sein (Compile-Fehler
   erwartet). Alternativ: Fixture so gestalten, dass die intakten
   Klassen ein eigenes Test-Projekt bilden und die kaputten Klassen
   in einem zweiten .csproj stehen, sodass der Build **selektiv
   testbar** ist.
   - **Achtung:** Wenn der Build der gesamten Solution wegen
     Compile-Fehlern rot wird, scheitern alle anderen Tests.
   - **Pragmatische Lösung:** Fixture in einem **separaten**
     Verzeichnis `tests/Fixtures/CompileErrorMini/` mit eigenem
     `.slnx` und 2 `.csproj`-Dateien:
     - `ValidProject.csproj` mit `ValidClassA/B/C.cs` (kompiliert
       sauber)
     - `BrokenProject.csproj` mit `BrokenClassA/B/C.cs` (Compile-
       Fehler, Build scheitert)
   - Coder prüft, ob `MSBuildWorkspace` die kaputte `.slnx` mit
     `HasLoadingErrors = true` öffnet (Erwartung: ja, MSBuild
     Workspace meldet die Compile-Fehler als Workspace-Diagnosen).
   - **Falls MSBuild die kaputte Solution gar nicht lädt:** Fall-
     Plan B: die kaputten Klassen werden in **eine** `.cs`-Datei
     kompiliert, die **manuell** mit `File.WriteAllText` zur
     Laufzeit in eine **kopierte** Solution geschrieben wird (die
     Solution wird mit den 3 Valid-Klassen **kopiert**, dann wird
     `BrokenClassA.cs` als zusätzliche Datei ins Quellverzeichnis
     geschrieben). Roslyn liest die Datei **on-demand** per
     `Document.GetTextAsync()`, MSBuild-Workspace cached das
     nicht — d. h. der Test kann die Datei **nach** dem Load
     schreiben und Roslyn sieht die Compile-Fehler im aktuellen
     Compilation. **Coder entscheidet** nach Probe.

### Schritt 2 — Neuer Helper `McpCompileDiagnostics` anlegen

**Datei:** `src/AiNetLinter/Mcp/Tools/McpCompileDiagnostics.cs` (NEU)

**Inhalt:**

```csharp
internal static class McpCompileDiagnostics
{
    /// <summary>
    /// Liefert alle Roslyn-Compile-Fehler (Severity == Error) der aktuellen
    /// Solution, gruppiert nach Document-FilePath. Reine Funktion ohne
    /// McpCodeGraphServer-Abhängigkeit.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<Diagnostic>>> GetErrorsByFileAsync(
        Solution solution, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<Diagnostic>>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;
            foreach (var diagnostic in compilation.GetDiagnostics())
            {
                if (diagnostic.Severity != DiagnosticSeverity.Error) continue;
                if (diagnostic.Location.SourceTree?.FilePath is not { } path) continue;
                if (!result.TryGetValue(path, out var list))
                {
                    list = new List<Diagnostic>();
                    result[path] = list;
                }
                ((List<Diagnostic>)list).Add(diagnostic);
            }
        }
        return result;
    }

    /// <summary>
    /// Baut einen knappen Warnhinweis-Text für eine einzelne Datei
    /// (datei-spezifische Tools). Format: "Hinweis: Diese Datei hat N
    /// Compile-Fehler — Ergebnis ist möglicherweise unvollständig.
    /// Diagnostics: <Id>: <Message>; <Id>: <Message>; ...".
    /// </summary>
    internal static string FormatFileWarning(IReadOnlyList<Diagnostic> diagnostics, int maxShown = 3)
    {
        // ... (siehe Schritt 2 Detail-Block)
    }

    /// <summary>
    /// Baut eine aggregierte Header-Zeile (aggregate Tools). Format:
    /// "Hinweis: N Dateien haben Compile-Fehler (M Errors gesamt) — Details
    /// siehe `find_symbol`/`search_pattern` für die betroffenen Dateien.".
    /// </summary>
    internal static string FormatAggregateWarning(int fileCount, int totalErrors)
    {
        // ... (siehe Schritt 2 Detail-Block)
    }
}
```

**Detail-Block `FormatFileWarning` (für Coder-Implementierung):**

- Nimmt die ersten `maxShown` Diagnostics, formatiert als
  `Id: Message` (Message auf 80 Zeichen gekürzt mit `…`).
- Liefert: `"Hinweis: Diese Datei hat N Compile-Fehler — Ergebnis
  ist möglicherweise unvollständig. Diagnostics: <Id>: <Message>;
  ..."`.
- Bei 0 Diagnostics: leerer String (Aufrufer prüft `string.
  IsNullOrEmpty`).

**Detail-Block `FormatAggregateWarning` (für Coder-Implementierung):**

- Liefert: `"Hinweis: {N} Dateien haben Compile-Fehler
  ({totalErrors} Errors gesamt) — Details siehe get_file_skeleton
  für die betroffenen Dateien."`.
- Bei 0: leerer String.

**`FormatReport` Inspiration:** Format-Konsistenz mit `McpTruncation`
(knapp, einheitlich, LLM-freundlich).

### Schritt 3 — Neue Helper in `McpToolResults`

**Datei:** `src/AiNetLinter/Mcp/McpToolResults.cs`

**Hinzufügen:**

```csharp
/// <summary>
/// Kurzform fuer den Fall, dass ein Tool auf eine Datei mit
/// Compile-Fehlern stoesst — liefert einen NORMALEN Text-Block
/// (kein IsError), der VOR dem eigentlichen Tool-Output eingefuegt
/// wird. Compile-Fehler bedeuten nicht, dass der Tool-Call
/// gescheitert ist, nur dass das Ergebnis moeglicherweise
/// unvollstaendig ist.
/// </summary>
internal static CallToolResult WarningsSection(string warningText)
{
    return new CallToolResult
    {
        Content = new List<ContentBlock> { new TextContentBlock { Text = warningText } },
    };
}

/// <summary>
/// Kurzform fuer den Fall, dass ein Tool wegen Compile-Fehlern
/// gar nicht sinnvoll antworten kann (z. B. das angefragte Symbol
/// existiert nur in einer fehlerhaften Datei und Roslyn kann es
/// nicht aufloesen). Liefert ein [ERROR]: WORKSPACE_DIAGNOSTIC-
/// Ergebnis mit Code aus dem bestehenden LinterErrorCodes-
/// Konstanten-Set (wiederverwendet, nicht neu angelegt).
/// </summary>
internal static CallToolResult CompilationError(string message, string? context = null)
{
    return Error(
        LinterErrorCodes.WorkspaceDiagnostic,
        message,
        context: context,
        hint: "Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.");
}
```

**Footprint-Schätzung:** +20-25 Z. in `McpToolResults.cs` (ist
aktuell ~110 Z., Puffer riesig, kein Risiko).

### Schritt 4 — `McpCodeGraphServer` minimale Erweiterung

**Datei:** `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`

**Hinzufügen (eine Methode, +5-8 Z.):**

```csharp
/// <summary>
/// Liefert die aktuelle Compilation der resident gehaltenen Solution
/// (oder null, wenn keine Solution geladen ist). Bewusst KEIN
/// gecachter Wert — jeder Aufruf holt frisch, weil Compile-Fehler
/// sich nach Staleness-Update geaendert haben koennen.
/// </summary>
public async Task<Compilation?> GetCurrentCompilationAsync(CancellationToken ct)
{
    var solution = GetCurrentSolution();
    if (solution is null) return null;
    return await solution.Projects.FirstOrDefault()?.GetCompilationAsync(ct);
}
```

**Achtung — Designentscheidung:** `Solution.GetCompilationAsync()`
existiert nicht direkt. Compilation ist **pro Project**. Für den
Helper in Schritt 2 brauchen wir Compile-Fehler **pro Datei**,
was `project.GetCompilationAsync()` für jedes Project erfordert.
**Es gibt zwei Wege:**

- (a) **Tools rufen `McpCompileDiagnostics.GetErrorsByFileAsync
  (solution, ct)` direkt** — kein neuer `McpCodeGraphServer`-
  Eingriff nötig. Saubere Trennung.
- (b) **`McpCodeGraphServer` cached eine aggregierte
  Dictionary<string, List<Diagnostic>>-Property** — minimal
  schneller, aber Cache-Invalidierung komplex.

**Entscheidung: (a).** Tools greifen direkt auf
`McpCompileDiagnostics` zu, das `Solution` als Parameter bekommt
(so wie heute schon `GetIndexScopeScanner.BuildBreakdownText
(solution)`). **Kein** Eingriff in `McpCodeGraphServer` —
kleinstmöglicher Scope.

**Schritt 4 wird übersprungen** (gestrichen in der finalen
Plan-Version).

### Schritt 5 — Audit-Tests + Compile-Fehler-Warnhinweis in 6 datei-spezifischen Tools

Für jedes der 6 datei-spezifischen Tools (`find_symbol`,
`find_references`, `get_impact`, `get_type_hierarchy`,
`get_file_skeleton`, `search_pattern`):

1. **Unit-Test in der bestehenden `*ToolTests.cs`-Datei:**
   - Direkter Aufruf von `Tool.ExecuteAsync(mcpState, ...)`
     mit `mcpState.GetCurrentSolution()` aus der
     `CompileErrorMiniFixtureWorkspace` (kompiliert mit kaputter
     Datei).
   - Assert: Result ist **kein** Error, der Output-Text beginnt
     mit dem `WarningsSection`-Hinweis.
2. **A3-Methodik pro Test:**
   - **Vor der Änderung:** Test schreiben, der die Warnhinweis-
     Sektion assertiert.
   - **Ausführen:** Test schlägt fehl (kein Warnhinweis im
     aktuellen Output).
   - **Failure-Output wortwörtlich** in `result.md` Abschnitt
     "A3-Nachweis" dokumentieren.
   - **Code-Änderung** (siehe Schritt 6): Tool ruft
     `McpCompileDiagnostics.GetErrorsByFileAsync(solution, ct)`
     + `FormatFileWarning(diagnostics)` + fügt den Hinweis **vor**
     dem eigentlichen Output ein.
   - **Nach der Änderung:** Test grün.
3. **Konkrete Tool-Änderungen** (Pattern für jedes Tool):

```csharp
// Vor der Aenderung:
var text = await SomeScanner.SomeAsync(solution, ...);
return McpToolResults.Text(text);

// Nach der Aenderung:
var diagnostics = await McpCompileDiagnostics.GetErrorsByFileAsync(solution, ct);
var warning = McpCompileDiagnostics.FormatFileWarning(
    diagnostics.GetValueOrDefault(documentFilePath, []));

var text = await SomeScanner.SomeAsync(solution, ...);
return string.IsNullOrEmpty(warning)
    ? McpToolResults.Text(text)
    : McpToolResults.Text(warning + "\n\n" + text);
```

**Achtung — `find_symbol` und `find_references` haben KEIN
direktes `documentFilePath`-Argument**, sondern matchen **alle**
Dateien. Hier ist der Compile-Fehler-Warnhinweis nur sinnvoll
für **eindeutig-datei-bezogene Treffer** — wenn ein Treffer in
einer kaputten Datei liegt, wird der Warnhinweis **pro Treffer-
Zeile** eingefügt (Aggregat-Format), nicht global. **Pragmatische
Vereinfachung:** für diese Tools wird der **Aggregate-Warnhinweis**
verwendet ("Hinweis: N Dateien mit Compile-Fehlern in der
Solution"), nicht der datei-spezifische.

**Konsequenz für die 6 Tools:**

- `find_symbol`, `find_references`, `get_impact`,
  `get_type_hierarchy`: Aggregate-Warnhinweis (kein direkter
  Datei-Bezug im Output).
- `get_file_skeleton`: datei-spezifischer Warnhinweis (direkter
  `filePath`-Parameter).
- `search_pattern`: Aggregate-Warnhinweis (mehrere Dateien
  möglich).

**Pattern (vereinfacht, Aggregate-Variante):**

```csharp
var diagnosticsByFile = await McpCompileDiagnostics.GetErrorsByFileAsync(solution, ct);
var totalErrors = diagnosticsByFile.Values.Sum(list => list.Count);
var warning = totalErrors > 0
    ? McpCompileDiagnostics.FormatAggregateWarning(diagnosticsByFile.Count, totalErrors)
    : "";

var text = await SomeScanner.SomeAsync(solution, ...);
return string.IsNullOrEmpty(warning)
    ? McpToolResults.Text(text)
    : McpToolResults.Text(warning + "\n\n" + text);
```

### Schritt 6 — Aggregate-Warnhinweis in 3 Aggregate-Tools

Für `get_index_scope`, `get_hotspots`, `get_violations`:

1. **Header-Zeile** mit Compile-Fehler-Count.
2. **Unit-Test:** Tool-Call gegen `CompileErrorMiniFixture`,
   assertiert dass der Output die Header-Zeile enthält.
3. **`get_violations` Spezialfall:** Compile-Fehler sind **kein**
   Lint-Verstoß — bestehende Lint-Ausgabe bleibt unverändert.
   **Negativtest:** `get_violations` liefert die **gleiche**
   Anzahl Lint-Verstöße wie ohne Compile-Fehler (Compile-Fehler
   blähen den Lint-Output **nicht** auf).

### Schritt 7 — Defensive `try/catch`-Wrapper in den 6 datei-spezifischen Tools

**Pattern (für jeden der 6 Tools):**

```csharp
internal static async Task<CallToolResult> ExecuteAsync(...)
{
    var solution = state.GetCurrentSolution();
    if (solution is null) return McpToolResults.SolutionNotLoaded();

    try
    {
        // ... bisheriger Body ...
        return McpToolResults.Text(text);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        return McpToolResults.CompilationError(
            $"Unerwarteter Fehler in {ToolName}: {ex.Message}",
            context: ... );
    }
}
```

**Achtung:** Nicht alle Tools brauchen den Wrapper — z. B.
`GetViolationsScanner` hat schon einen (Z. 64-71). Wrapper nur
dort, wo Roslyn-Operationen ohnehin selten fehlschlagen, aber im
Fehlerfall ungehandelt durchschlagen würden.

**Pragmatisch:** Für 006 nur den Wrapper für die 3 Tools
hinzufügen, die noch keinen haben und am wahrscheinlichsten
Fehler werfen: `find_symbol`, `find_references`, `get_impact`.
Die anderen 3 (`get_type_hierarchy`, `get_file_skeleton`,
`search_pattern`) sind durch ihre Scanner oder durch
`SearchPatternTool`'s bestehendes `try/catch` schon abgesichert.

### Schritt 8 — Server-Lifecycle-E2E-Test

**Neue Test-Datei:** `src/AiNetLinter.Tests/Commands/
McpServerCommandErrorHandlingTests.cs` (in
`[Collection("ConsoleTestCollection")]`).

**Test-Inhalt (2-3 Tests):**

1. `RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError`:
   - Echte kaputte `*.slnx` (analog `TryLoadSolutionAsync_BrokenSlnx_
     LogsWarningWithoutThrowing` in `McpServerCommandTests.cs:110-131`).
   - MCP-Client startet Server, ruft `find_symbol` auf.
   - Assertiert: `IsError == true` und Text enthält
     `SOLUTION_NOT_LOADED`.
2. `RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection`:
   - Echte `CompileErrorMiniFixtureWorkspace`.
   - MCP-Client startet Server, ruft `get_file_skeleton` mit
     `BrokenClassA.cs` als `filePath`.
   - Assertiert: Output beginnt mit `Hinweis:`, Text enthält
     `Compile-Fehler`.

### Schritt 9 — Plan-Abweichung 1: `GetImpactTool` Footprint-Puffer 5 zu knapp

Falls nach Schritt 5-7 `GetImpactTool` > 2500 Z. reißt:

- **Maßnahme 1 (bevorzugt):** Description im
  `SymbolGraphToolRegistrations.cs` für `get_impact` um 1-2
  Wörter kürzen (z. B. "fuer ein Symbol" statt "fuer ein
  einzelnes Symbol").
- **Maßnahme 2 (Fallback):** `try/catch`-Wrapper für `get_impact`
  weglassen (Wrapper trägt +6-8 Z. ein — wenn ohne Wrapper
  ≤ 2500, ist das die Lösung). Begründung in `result.md`:
  "Defensive Wrapper für 3/3 Symbolgraph-Tools wäre overkill;
  `get_impact` delegiert vollständig an Roslyn-APIs, die
  dokumentiert exceptionsfrei arbeiten."
- **Maßnahme 3 (nicht ergriffen):** PathOverride-Erhöhung —
  explizit **nicht** in 006-Scope (A5, Vorgabe "Kein
  `PathOverrides`-Wert erhöhen").

### Schritt 10 — Finale Verifikation

1. `dotnet build AiNetLinter.slnx` — grün, 0 Warnungen.
2. `dotnet test AiNetLinter.slnx --no-build` — grün, Test-Anzahl
   dokumentiert (Erwartung: 1114 + 6-8 neue = 1120-1122).
3. `dotnet run --project src/AiNetLinter -- --footprint
   GetImpactTool --path .` — ≤ 2500.
4. `dotnet run --project src/AiNetLinter -- --footprint
   McpCodeGraphServer --path .` — falls Schritt 4 doch
   Erweiterung braucht: ≤ 2500.
5. `dotnet run --project src/AiNetLinter -- --footprint
   SymbolGraphToolRegistrations --path .` — ≤ 2500.
6. `dotnet run --project src/AiNetLinter -- --footprint
   McpCompileDiagnostics --path .` — ≤ 2500 (Erwartung: ~80 Z.).
7. **Self-Lint** via `dotnet run --project src/AiNetLinter --
   --path src/AiNetLinter` — 0 Violations.
8. **Dogfooding** (Konzept-Vorgabe): echte `AiNetLinter.slnx`
   laden (selbst kompiliert sauber), ein Tool-Call für
   `find_symbol` "Robust" — Output dokumentieren (Erwartung:
   kein Compile-Fehler-Hinweis, weil die echte Solution sauber
   kompiliert).
9. **Commit-Message** (Conventional Commits, deutsch,
   imperative Stimmung): `feat(mcp): compile-fehler-warnhinweis
   in 9 tools (EPIC-06) [codegraph-mcp-server]`.

## Erwartete Tests (mit A3-Methodik pro Test)

### T1 — `find_symbol` Aggregate-Warnhinweis (Unit)

**Datei:** `src/AiNetLinter.Tests/Mcp/Tools/FindSymbolToolTests.cs`
(oder neue E2E-Datei).

**Test:**
```csharp
[Fact]
public async Task ExecuteAsync_CompileErrorFixture_ReturnsAggregateWarningSection()
{
    using var fixture = new CompileErrorMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var server = new McpCodeGraphServer(catalog);

    var result = await FindSymbolTool.ExecuteAsync(
        server, "ValidClassA", kind: null, maxResults: 50, CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    Assert.Contains("Hinweis:", text, StringComparison.Ordinal);
    Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
}
```

**A3-Methodik:**

- **Vor Code-Änderung:** Test schreiben, ausführen, Failure
  wortwörtlich dokumentieren (Erwartung: Output beginnt **nicht**
  mit "Hinweis:").
- **Nach Code-Änderung:** Test grün.

### T2 — `find_references` Aggregate-Warnhinweis (Unit)

Analog T1, Symbol `ValidClassA.SomeMethod` (eine deklarierte
Methode in `ValidClassA.cs`).

### T3 — `get_impact` Aggregate-Warnhinweis (Unit)

Analog T1, Symbol-Branch.

### T4 — `get_type_hierarchy` Aggregate-Warnhinweis (Unit)

Analog T1, `ValidClassA` als Typ.

### T5 — `get_file_skeleton` Datei-spezifischer Warnhinweis (Unit)

**Test:**
```csharp
[Fact]
public async Task ExecuteAsync_CompileErrorFile_ReturnsFileSpecificWarning()
{
    using var fixture = new CompileErrorMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var server = new McpCodeGraphServer(catalog);

    var result = await GetFileSkeletonTool.ExecuteAsync(
        server, "src/CompileErrorMini/BrokenClassA.cs",
        CancellationToken.None);

    Assert.NotEqual(true, result.IsError);
    var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
    Assert.Contains("Diese Datei", text, StringComparison.Ordinal);
    Assert.Contains("Compile-Fehler", text, StringComparison.Ordinal);
}
```

### T6 — `search_pattern` Aggregate-Warnhinweis (Unit)

Analog T1, Pattern `"ClassA"`.

### T7 — `get_index_scope` Aggregate-Header (Unit)

**Test:**
```csharp
[Fact]
public async Task ExecuteAsync_CompileErrorFixture_HeaderReportsFileCount()
{
    using var fixture = new CompileErrorMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var server = new McpCodeGraphServer(catalog);

    var result = await GetIndexScopeTool.ExecuteAsync(server, CancellationToken.None);
    var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    Assert.Contains("Hinweis:", text, StringComparison.Ordinal);
    Assert.Matches(@"\b\d+\s+Dateien?\s+mit\s+Compile-Fehlern", text);
}
```

### T8 — `get_hotspots` Aggregate-Header (Unit)

Analog T7.

### T9 — `get_violations` Compile-Fehler sind KEIN Lint-Verstoß (Unit, Negativtest)

**Test:**
```csharp
[Fact]
public async Task ExecuteAsync_CompileErrorFixture_DoesNotIncludeCompileErrorsAsViolations()
{
    using var fixture = new CompileErrorMiniFixtureWorkspace();
    var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
    using var server = new McpCodeGraphServer(catalog);

    var result = await GetViolationsTool.ExecuteAsync(
        server, scopeFilter: null, CancellationToken.None);
    var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    // Compile-Fehler wie CS1513 duerfen NICHT in der Lint-Output-Tabelle
    // auftauchen — das ist ein bewusster Test, dass EPIC-06 die
    // Lint-Ausgabe NICHT aufblaht.
    Assert.DoesNotContain("CS1513", text, StringComparison.Ordinal);
    Assert.DoesNotContain("CS0246", text, StringComparison.Ordinal);
}
```

### T10 — E2E `RunAsync_BrokenSlnx_ToolCallReturnsSolutionNotLoadedError`

**Datei:** `src/AiNetLinter.Tests/Commands/
McpServerCommandErrorHandlingTests.cs` (NEU,
`[Collection("ConsoleTestCollection")]`).

**Test:** Echte kaputte `*.slnx` (Inhalt `<this-is-not-a-valid-slnx>`),
MCP-Client startet Server, ruft `find_symbol` auf, assertiert
`IsError == true` + `SOLUTION_NOT_LOADED` im Text.

### T11 — E2E `RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection`

Analog T10, aber mit `CompileErrorMiniFixtureWorkspace` und
`get_file_skeleton` für `BrokenClassA.cs`.

### T12 — Audit-Dokumentations-Test (oder Doku in `McpToolResults`)

**Test:** `McpToolResults.WarningsSection("Hinweis: ...")` liefert
einen normalen Text-Block, **kein** `IsError = true`. Direkter
Unit-Test in `McpToolResultsTests.cs`.

**A3-Methodik:**

- Test schreiben, Build-Check (existierende `McpToolResultsTests.cs`
  ist 1250 Bytes, siehe Check 6).
- Vor Code-Änderung: Test schlägt fehl (Methode existiert nicht).
- Nach Code-Änderung: Test grün.

**Test-Anzahl-Schätzung:** 6 Unit-Tests (T1-T6) + 2 Aggregate
(T7-T8) + 1 Negativtest (T9) + 2 E2E (T10-T11) + 1 Helper (T12) =
**12 neue Tests**.

## Footprint-Messung (TD-011-Pflicht, vor und nach 006)

**Pre-006-Baseline** (gemessen 2026-08-01, exakt zu wiederholen):

```
dotnet run --project src/AiNetLinter -- --footprint FindSymbolTool --path .
dotnet run --project src/AiNetLinter -- --footprint FindReferencesTool --path .
dotnet run --project src/AiNetLinter -- --footprint GetImpactTool --path .
dotnet run --project src/AiNetLinter -- --footprint GetTypeHierarchyTool --path .
dotnet run --project src/AiNetLinter -- --footprint GetFileSkeletonTool --path .
dotnet run --project src/AiNetLinter -- --footprint GetIndexScopeTool --path .
dotnet run --project src/AiNetLinter -- --footprint GetHotspotsTool --path .
dotnet run --project src/AiNetLinter -- --footprint GetViolationsTool --path .
dotnet run --project src/AiNetLinter -- --footprint SearchPatternTool --path .
dotnet run --project src/AiNetLinter -- --footprint SymbolGraphToolRegistrations --path .
dotnet run --project src/AiNetLinter -- --footprint McpServerOptionsFactory --path .
dotnet run --project src/AiNetLinter -- --footprint McpToolResults --path .
dotnet run --project src/AiNetLinter -- --footprint McpCodeGraphServer --path .
```

**Post-006** (nach Schritt 10): dieselben Befehle, Werte in
`result.md` Abschnitt "Footprint" eintragen. **Erwartete
Veränderungen:**

| Klasse | Vor 006 | Δ | Nach 006 (Erwartung) | Puffer nach 006 |
|---|---:|---:|---:|---:|
| `GetImpactTool` | 2495 | +4-6 | 2499-2501 | -1 bis +5 ⚠ |
| `FindReferencesTool` | 2522 | +4-6 | 2526-2528 | 172-174 |
| `FindSymbolTool` | 2491 | +4-6 | 2495-2497 | 203-205 |
| `GetTypeHierarchyTool` | ~1490 | +4-6 | ~1496 | — |
| `GetFileSkeletonTool` | ~1000 | +4-6 | ~1006 | — |
| `SearchPatternTool` | 2482 | +4-6 | 2486-2488 | 12-14 |
| `SymbolGraphToolRegistrations` | 2494 | +0 | 2494 | 6 |
| `McpServerOptionsFactory` | 2484 | +0 | 2484 | 16 |
| `McpToolResults` | ~110 | +20-25 | ~135 | — |
| `McpCodeGraphServer` | 184 | +0 | 184 | — |
| `McpCompileDiagnostics` (NEU) | n/a | n/a | ~80 | — |

**TD-Status nach 006:**

- TD-010 (`SearchPatternTool` Footprint knapp): Puffer 12-14,
  **knapp** (Puffer-Schrumpfung dokumentieren, bleibt offen).
- TD-011 (`SymbolGraphToolRegistrations` Footprint knapp):
  unverändert 2494/2500, **Puffer-Schrumpfung von 6 → 6**
  (006 ändert die Registrar-Klasse **nicht**).
- TD-014 (`McpServerOptionsFactory` Footprint knapp): unverändert,
  006 ändert diese Klasse **nicht**.

## Bezug zu Projektregeln

- **`AiNetLinter.mdc`** (alwaysApply, C#-Codequalität):
  - `MaxLineCount: 500` (alle Klassen) — Pflichtmessung in Check 6.
  - `MaxAIContextFootprint: 2500` — TD-011-Pflicht.
  - `MaxMethodParameterCount: 4` — bei `GetImpactTool`-
    Parameter-Erweiterung Schritt 9 Plan-Abweichung 1.
  - `MaxConstructorDependencies: 5` — TD-009 (kein 006-Eingriff
    erwartet, `McpCodeGraphServer`-Konstruktor bleibt 5/5).
  - `MaxMethodBodyLength` (Schritt 0 in 006: jede Tool-Methode
    bleibt klein dank `McpCompileDiagnostics`-Delegation).
- **`AiNetLinterRichtlinien.mdc`** (alwaysApply, Workflow):
  - §1 "Monolithisch & schlank bleiben" — `McpCompileDiagnostics`
    ist Helper, kein DI-Container-Ausbruch.
  - §2 "Kein DI-Container" — Helper wird statisch aufgerufen,
    nicht via DI-Container instanziiert.
  - §5 "Result-Pattern" — Compile-Fehler führen zu
    `McpToolResults.CompilationError(...)` (Error-Helper), nicht zu
    Exceptions.
- **Konzept-Vorgaben:**
  - Z. 146-153: Fehlerbehandlung ohne Absturz — Pflicht.
  - Z. 226-233: Plain-Text-Format — Compile-Fehler-Warnhinweis ist
    Plain-Text, keine JSON-Metadaten.
  - Z. 564-567: Server-Betrieb Schritt 3 — strukturierte Fehler-
    antwort im `[ERROR]`-Format.

## Annahmen und offene Fragen

### Annahmen

- **A1:** `MSBuildWorkspace` lädt eine Solution mit Compile-Fehlern
  in einzelnen Dateien und liefert eine Solution, deren
  `Project.GetCompilation().GetDiagnostics()` die Fehler enthält.
  (Standard-Roslyn-Verhalten, dokumentiert in MSBuild-Workspace-
  Spec.)
- **A2:** Die `CompileErrorMiniFixtureWorkspace` ist analog zu
  `BaselineMiniFixtureWorkspace` als Temp-Kopie realisierbar.
- **A3:** xUnit `[Collection("ConsoleTestCollection")]` serialisiert
  Tests, sodass TD-003-Race in 006-Tests **nicht** auftritt.
- **A4:** `McpCompileDiagnostics.GetErrorsByFileAsync` ist
  deterministisch für eine gegebene Solution (kein Caching, kein
  Random).
- **A5:** Aggregate-Warnhinweis (für `find_symbol` et al.) ist
  informationsreich genug, dass der Agent die Compile-Fehler-
  Dateien identifizieren und separat prüfen kann. Detail-Format
  "N Dateien / M Errors" reicht aus.
- **A6:** `dotnet test` parallel-Test-Default ist so, dass die
  006-Tests in `ConsoleTestCollection` sequenziell laufen — kein
  Race-Bedingung zwischen 006-Tests und 004/005-Tests.

### Offene Fragen (für Coder oder Folge-Planung)

- **F1:** Soll die Header-Zeile in `get_index_scope` und
  `get_hotspots` **vor** oder **nach** der Haupt-Aufschlüsselung
  erscheinen? Konzept schweigt. **Pragmatik:** vor (LLM sieht
  Warnung zuerst). Coder entscheidet, dokumentiert in `result.md`.
- **F2:** Wenn ein Tool für `BrokenClassA.cs` aufgerufen wird
  (`get_file_skeleton`), liefert Roslyn eine leere oder partielle
  Type-Liste — soll der Output dann **nur** die Warnung
  enthalten, oder die Warnung + leeres Skelett? **Pragmatik:**
  Warnung + leeres Skelett ("Keine Typen gefunden in
  'BrokenClassA.cs'") — analog bestehende `get_file_skeleton`-
  Leerbehandlung (Z. 36-38). Coder entscheidet, dokumentiert.
- **F3:** `get_violations` mit Compile-Fehlern: läuft
  `LinterEngine.RunAsync` für die kaputte Solution durch, oder
  wirft sie eine Exception? Falls Exception: bestehender
  `try/catch (Exception)` in `GetViolationsScanner` Z. 64-71 greift
  → `[ERROR]: ANALYSIS_FAILED`. Coder prüft, dokumentiert.
- **F4:** Soll `McpCompileDiagnostics` **alle** Diagnostics
  filtern (nur `Severity == Error`), oder auch `Severity ==
  Warning`? **Pragmatik:** nur Errors. Warnings sind im
  bestehenden Lint-Output bereits abgedeckt.

## Harte Scope-Grenze (wiederholt)

- **Keine** P0/P1-Extensions (Kaltstart, Auto-Discovery,
  Staleness-Sweep, Call-Log, Verzeichnis-Sweep, `ILintConsole`-
  MCP-Modus).
- **Kein** TD-003-Race-Fix (eigener Punkt für EPIC-07 oder
  später; 006 umgeht die Race durch Collection-Serialisierung).
- **Kein** EPIC-07-Tests-Ausbau.
- **Kein** EPIC-08-Doku.
- **Kein** Scanner-Split für ein bestehendes Tool.
- **Keine** Trunkierungs-Änderungen (4/4 Listen-Tools fertig).
- **Keine** Miss-Hint-Änderungen (003 abgeschlossen).
- **Kein** `PathOverrides`-Wert erhöhen.
- **Kein** Tool-Set erweitern (kein `get_active_rules`, kein
  `get_symbol_body`).
- **Kein** `McpServerOptionsFactory`-Eingriff (TD-014 bleibt
  offen).
- **Kein** `McpCodeGraphServer`-Konstruktor-Refactor (TD-009
  bleibt offen).
- **Kein** Eingriff in `konzept.md` (A7), Projektregeln (A7),
  `kernel.md` (A8), Rollen-Dateien (A8).

## Quellen-Referenz

- `konzept.md` Z. 102-103: EPIC-06 Auftrag.
- `konzept.md` Z. 146-153: Muss-Haven Fehlerbehandlung.
- `konzept.md` Z. 564-567: Server-Betrieb Schritt 3.
- `konzept.md` Z. 609-611: DoD Compile-Fehler-Verhalten.
- `konzept.md` Z. 612-613: DoD nicht-ladbare Solution.
- `Docs/agent-api.md` Z. 144-173: `[ERROR]`-Format.
- `McpCodeGraphServer.cs`: aktueller Stand, kein Eingriff in
  Schritt 4 (gestrichen).
- `McpToolResults.cs`: aktueller Stand, +20-25 Z. in Schritt 3.
- `LinterErrorCodes.cs` Z. 16: `WorkspaceDiagnostic` wiederverwendet.
- `units/002/plan.md` M-1-Muster: Hint-Bug-Defensive.
- `units/004/plan.md` E2E-Fixture-Pattern: neue Datei pro Tool.
- `units/005/plan.md` P0/P1-Vorbild: Re-Messung pro Einheit.
- `tech-debt.md:TD-003` RegisterMSBuild-Race: durch Collection-
  Serialisierung umgangen.
- `tech-debt.md:TD-008/010/011/014` Footprint-Druck: Pflichtmessung
  in 006 (kein 006-Eingriff zur Schließung).
