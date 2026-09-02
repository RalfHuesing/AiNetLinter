---
status: ready
task: mcp-di-composition-decoupling
datum: 2026-09-02
bereich: src/AiNetLinter/Mcp
---

# Konzept: Gezielte Dependency Injection & Composition Root für das MCP-Subsystem

## 1. Intention & Zielsetzung

AiNetLinter nutzt im MCP-Server- und Daemon-Modus derzeit überwiegend manuelle Konstruktor-Kaskaden (*„Poor Man's DI"*) mit Default-Instanziierungen per `new`. Dadurch instanziieren zentrale Klassen (z. B. `AssemblyAnalysisRegistry`, `GetServerHealthResponseBuilder`, `InspectAssemblyTool`) ihre Subkomponenten und Host-Factories selbst.

### Das Problem
1. **Transitive AIContextFootprint-Explosion:**  
   Roslyn und der AiNetLinter-Footprint-Rechner traversieren konkrete Klassenreferenzen transitiv. Die **zentrale Koppelungsstelle** ist [`AssemblyAnalysisLease.Server`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyAnalysisLease.cs#L38) (Typ `McpCodeGraphServer`, 423 Zeilen), die den kompletten `McpCodeGraphServer`-Footprint (inkl. `ExternalResourceRegistry` 455 Zeilen + `SourceSnapshotIdentity` 316 Zeilen) transitiv in **alle 11 Klassen** propagiert, die `AssemblyAnalysisLease` nutzen:
   - `AssemblyHealthProjection` (2.567)
   - `AssemblyAnalysisResponse` (2.556)
   - `AssemblySymbolResolver` (2.540)
   - `AssemblySymbolSearch` (2.528)
   - `AssemblyAnalysisToolSupport` (2.522)
   - `AssemblyGetCallTreeTool` (2.519)
   - `FindAssemblyExtensionsResponseBuilder` (2.519)
   - `FindAssemblyExtensionsBuildRequest` (2.511)
   - `InspectAssemblyResponseBuilder` (2.509)
   - `AssemblyFindSymbolTool` (2.508)
   - `InspectAssemblyBuildRequest` (2.501)
   
   Zusätzlich gibt es 1 Fehler (`MaxMethodLineCount`): `AssemblySymbolResolver.ResolveAsync` hat 62 Zeilen (Limit: 60).

2. **Symptomkaschierung war die bisherige Antwort:**  
   Historische `FootprintIgnoreTypeNames`- und `PathOverrides`-Einträge in `rules.json` wurden bereits vollständig entfernt. Damit sind die Violations jetzt **sichtbar**, aber das strukturelle Problem bleibt ungelöst.

3. **Begleitende Code-Schulden (DRY, Magic Values, Dead Code):**  
   Durch manuelle Dispatcher-Kaskaden und historische Refactorings verbleiben Duplikate bei der Parameter-Extraktion, Magic Strings bei Fehlermeldungen/Routennamen und potenziell totes Wiring.

4. **Erhöhter Testaufwand in `FastTests`:**  
   Um einzelne Komponenten isoliert im In-Memory-Modus zu testen, müssen komplexe reale Objektbäume aufgebaut werden (>249 Referenzen auf `McpCodeGraphServer` allein in den Tests), statt schlanke Fakes/Mocks injizieren zu können.

### Das Ziel
Einführung einer **gezielten, leichtgewichtigen Entkopplung** durch schlanke Interfaces im MCP-Server-Host, sodass:
- Der transitive `AIContextFootprint` aller Klassen im MCP-Bereich stabil und ohne Ausnahmeregeln unter dem Limit von 2.500 Zeilen bleibt.
- `AssemblySymbolResolver.ResolveAsync` auf ≤ 60 Zeilen modularisiert wird.
- DRY-Verstöße, Magic Values und Dead Code im gesamten `Mcp/`-Scope bereinigt werden.
- Der Safeguard-Score **echte 10,00 / 10,00 aus dem Code heraus** erreicht (aktuell 0,00/10 mit 13 Verstößen).

---

## 2. Architektonisches Scope-Modell: Hybrider Ansatz

Um maximale Performance im CLI-Batchbetrieb (<50ms) zu garantieren und gleichzeitig saubere Entkopplung im langlebigen MCP-Server-Betrieb zu erreichen, wird ein hybrides Modell festgelegt:

```
┌────────────────────────────────────────────────────────────────────────┐
│                              AiNetLinter                               │
├───────────────────────────────────┬────────────────────────────────────┤
│     MCP-Server & Daemon-Host      │          Core Linter CLI           │
│  (Interface-basierte Entkopplung) │        (Pure / Manual DI)          │
│                                   │                                    │
│  - Assembly-Analysis-Subsystem    │  - LinterEngine                    │
│  - ExternalResource-Registry      │  - Keine DI-Container              │
│  - Tool-Handlers & Dispatcher     │  - Keine Startup-Latenz (<10ms)    │
│  - Telemetrie & Health-Builder    │  - Direkte Methodenaufrufe         │
└───────────────────────────────────┴────────────────────────────────────┘
```

### Entscheidung: Interfaces statt DI-Container

Die 360°-Analyse ergibt, dass **kein `Microsoft.Extensions.DependencyInjection`-Container nötig ist**. Das Paket ist nicht referenziert, und die bestehende `AssemblyAnalysisHostComposition` / `AssemblyAnalysisHostFactory` übernimmt bereits die Composition-Root-Rolle für das Assembly-Subsystem. Die Lösung besteht ausschließlich in **schlanken Interfaces**, die die transitiven Footprint-Ketten kappen.

> [!IMPORTANT]
> **Kein NuGet-Paket `Microsoft.Extensions.DependencyInjection` wird eingeführt.**
> Die bestehende Factory-/Constructor-Injection-Architektur bleibt vollständig erhalten.
> Nur die konkreten Typen in Konstruktor-Parametern und Properties werden durch Interfaces ersetzt.

---

## 3. Root-Cause-Analyse & Koppelungsketten

### 3.1. Primäre Koppelungskette (verantwortlich für alle 11 Footprint-Warnungen)

```
AssemblyAnalysisLease.Server : McpCodeGraphServer (423 Zeilen)
    ├── verwendet von: 20+ Aufrufstellen (lease.Server.GetCurrentSolution(), 
    │                   lease.Server.AssemblySymbolIdentity, lease.Server.GetConfigSnapshot())
    └── transitiv: McpCodeGraphServer → ExternalResourceRegistry (455) + SourceSnapshotIdentity (316)
         → Footprint > 2.500 für alle Consumer von AssemblyAnalysisLease
```

**Beweis aus MCP-Evidenz:** Jede einzelne Footprint-Warnung zeigt exakt dieselben Top-3:
- `+ ExternalResourceRegistry (470)` (Datei: 455 + 7 nested class `ResourceEntry`)
- `+ McpCodeGraphServer (448)` (Datei: 423 + interne Records)
- `+ SourceSnapshotIdentity (316)`

### 3.2. Sekundäre Koppelungskette (AssemblyAnalysisRegistry-intern)

```
AssemblyAnalysisRegistry(.ctor)
    ├── new AssemblyAnalysisResourceBudget(resourceRegistry) → ExternalResourceRegistry
    ├── new AssemblyAnalysisRegistryEntryFactory(...) → ExternalResourceRegistry transitiv
    ├── new AssemblyAnalysisSourceProjectEntryFactory(...) → dito
    ├── new AssemblyAnalysisRegistryEvictionCandidates(...)
    ├── new AssemblyAnalysisRegistryEvictionCoordinator(...)
    └── new AssemblyAnalysisSourceProjectLeaseCoordinator(...)
```

Diese Kette verursacht aktuell **keine** Footprint-Warnung für `AssemblyAnalysisRegistry` selbst (liegt durch `IAssemblyAnalysisRegistry`-Interface bereits entkoppelt), aber sie erhöht den Footprint der Klassen, die `AssemblyAnalysisRegistry` **konkret** (statt über das Interface) nutzen.

---

## 4. Konkrete Maßnahmen & Code-Sketches

### 4.1. Interface `ISolutionStateProvider` — Primärer Hebel (Kappen der Hauptkette)

**Lokation:** `src/AiNetLinter/Mcp/ISolutionStateProvider.cs` (NEU)

Das Interface abstrahiert die **drei tatsächlich genutzten** Capabilities von `McpCodeGraphServer` in der Lease-Kette:

```csharp
// src/AiNetLinter/Mcp/ISolutionStateProvider.cs (NEU)
namespace AiNetLinter.Mcp;

internal interface ISolutionStateProvider
{
    Solution? GetCurrentSolution();
    AnalysisSymbolIdentity? AssemblySymbolIdentity { get; }
    ServerLoadState LoadState { get; }
}
```

**Erwarteter Footprint des Interface:** ~5 Zeilen → kein transitiver Ballast.

**Änderungen an `McpCodeGraphServer`:**
```csharp
// McpCodeGraphServer.cs — nur Ergänzung der Interface-Deklaration
internal sealed class McpCodeGraphServer : ISolutionStateProvider, IDisposable, IAsyncDisposable
{
    // Alle Member bleiben unverändert — die Methoden existieren bereits.
}
```

**Änderungen an `AssemblyAnalysisLease`:**
```csharp
// AssemblyAnalysisLease.cs — Konstruktor und Property
internal sealed class AssemblyAnalysisLease : IDisposable, IAssemblyBodyContext
{
    // VORHER: McpCodeGraphServer server
    // NACHHER:
    internal AssemblyAnalysisLease(
        AssemblyAnalysisEntry entry,
        string canonicalPath,
        ISolutionStateProvider server,  // ← Interface statt konkreter Klasse
        AssemblyContext context,
        AssemblyReferenceLeaseContext referenceContext) { ... }

    internal ISolutionStateProvider Server { get; }  // ← Typ-Änderung
}
```

**Auswirkung:** Alle 11 Footprint-Warnungen werden aufgelöst, da `ISolutionStateProvider` (~5 Zeilen) statt `McpCodeGraphServer` (423 Zeilen) den transitiven Baum dominiert.

### 4.2. Weitere Interface-Auslagerung von `McpCodeGraphServer` in Methodenparametern

**Problem:** `AssemblyAnalysisToolSupport` nimmt `McpCodeGraphServer?` direkt als Parameter:

```csharp
// VORHER (AssemblyAnalysisToolSupport.cs:48-52)
internal static async Task<AssemblyToolPreparation> PrepareAsync(
    McpCodeGraphServer? state, ...)
```

**NACHHER:**
```csharp
internal static async Task<AssemblyToolPreparation> PrepareAsync(
    ISolutionStateProvider? state, ...)
```

Ebenso für `AssemblyToolExecutionParameters.State` (Record-Definition Zeile 112) und `TryPrepareInput`.

### 4.3. Caller-Anpassungen: `lease.Server`-Zugriffe

Die ~50 Aufrufstellen von `lease.Server` bleiben **syntaktisch unverändert**, da `ISolutionStateProvider` genau die genutzten Member deklariert:
- `lease.Server.GetCurrentSolution()` → ✅ im Interface
- `lease.Server.AssemblySymbolIdentity` → ✅ im Interface
- `lease.Server.LoadState` → ✅ im Interface

Einzige Ausnahmen, die zusätzliches Interface-Surface erfordern (oder einen cast benötigen):

| Caller | Zugriff | Lösung |
|:---|:---|:---|
| `ReloadConfigTool.ExecuteAsync` | `lease.Server` als `McpCodeGraphServer` (für `ReloadConfig`) | Parameter auf `McpCodeGraphServer` belassen (nicht über Lease) |
| `OverviewResourceRegistration` | `snapshot.Server` für `DescribeSolution`/`DescribeConfig` | Dito — der ProjectLease hat eigene Server-Ref |
| `ProjectToolCall` | `lease.Server` für Load-State-Check + Reload | Dito — ProjectLease behält eigenen konkreten Typ |

> [!IMPORTANT]
> Die `ProjectLease` (in `ProjectRegistry`) ist ein anderer Lease-Typ als `AssemblyAnalysisLease` und **nicht betroffen** von den Footprint-Warnungen. Dort bleibt `McpCodeGraphServer` konkret.

### 4.4. Modularisierung `AssemblySymbolResolver.ResolveAsync` (62 → ≤ 60 Zeilen)

**Problem:** `ResolveAsync` hat 62 Codezeilen (Limit: 60), CyclomaticComplexity=10, CognitiveComplexity=12 — zu hoch für Compound-Suppression.

**Lösung:** Extract-Method-Refactoring — die referenzierende Lease-Resolution (19 Zeilen) ist bereits in `ResolveLeaseAsync` extrahiert. Der verbleibende Body enthält zwei separate Blöcke:
1. Initial-Resolution + Fehlerbehandlung (~30 Zeilen)
2. Reference-Navigation + Fallback (~32 Zeilen)

Ein `TryResolveInReferences`-Extrakt kann die Methode unter das Limit bringen:

```csharp
// Sketch: Extract-Method
private async Task<(AssemblySymbolTarget? Target, string? Diagnostic)> TryResolveInReferencesAsync(
    Solution solution,
    AssemblyAnalysisLease root,
    string identifier,
    CancellationToken cancellationToken)
{
    // Die ~20 Zeilen Reference-Navigation-Logik hierher verschieben
}
```

### 4.5. Systematischer Quality-Audit: DRY, Dead Code & Magic Values

* **Dead-Code-Bereinigung (`find_dead_code`):**
  - Bereinigung von historischem Dead-Wiring in Koordinatoren (z. B. `beforeRetirementAsync`-Callback in `AssemblyAnalysisRegistry` Konstruktor, falls ungenutzt).
  - Entfernung ungenutzter interner Überladungen und Hilfsstrukturen im MCP-Scope.
* **Magic-Values-Bereinigung (`find_magic_values`):**
  - Zentralisierung von Magic Strings (Tool-Namen, Diagnose-Codes, Header-Texte) und Magic Numbers (Timeout-Defaults, Cache-Limits, Navigations-Tiefen) in dedizierten Kontrakten/Enums/Konstanten.
* **DRY-Konsolidierung (`find_duplicates`):**
  - Vereinheitlichung redundanter Parameter-Validierungs- und Fehlerbehandlungsblöcke in gemeinsamen Dispatcher-Helfern.

### 4.6. Test-Datei-Splitting: `AssemblyAnalysisSessionTests.cs` (508 → ≤ 500 Zeilen)

**Problem:** `AssemblyAnalysisSessionTests.cs` hat 508 Zeilen (Limit: 500, Severity: error).

**Lösung:** Thematisch zusammengehörende Test-Gruppen in eigene Dateien extrahieren (z. B. `AssemblyAnalysisSessionReferenceTests.cs` oder `AssemblyAnalysisSessionLeaseTests.cs`).

---

## 5. Muss-Kriterien & Akzeptanzkriterien

1. **0 AIContextFootprint-Warnungen:**  
   Alle Klassen im MCP- und Assembly-Bereich unterschreiten den globalen Standard-Grenzwert von 2.500 Zeilen ohne lokale `#pragma` oder Disable-Kommentare.
2. **0 MaxLineCount- und MaxMethodLineCount-Fehler:**  
   Alle MCP-Produktions- und Testdateien halten ≤ 500 Datei-Zeilen und ≤ 60 Methoden-Zeilen ein (konkret: `AssemblySymbolResolver.ResolveAsync` und `AssemblyAnalysisSessionTests.cs`).
3. **Sauberes DRY-, Dead-Code- und Magic-Value-Audit:**  
   MCP-Tools `find_dead_code`, `find_magic_values` und `find_duplicates` melden 0 relevante Befunde im `Mcp/`-Scope.
4. **100 % Regressionsfreiheit & Thread-Safety:**  
   Alle FastTests (2.370+ Tests) und IntegrationTests (380+ Tests) laufen vollständig grün durch. Locking- und Concurrency-Invarianten in `AssemblyAnalysisRegistry` bleiben erhalten.
5. **Keine Latenz-Regression im Core-Linter:**  
   Die CLI-Ausführung von `LinterEngine` bleibt containerlos und instantiiert keine unnötigen DI-Strukturen (< 10 ms).
6. **Kein neues NuGet-Paket:**  
   `Microsoft.Extensions.DependencyInjection` wird nicht eingeführt.
7. **Safeguard-Score:**  
   `safeguard(minScore: 8.0)` ergibt **10,00 / 10,00 — PASS** ohne jegliche Ausnahmeregel.

---

## 6. Explizite Non-Goals & Scope-Grenzen

* **Kein DI-Container (weder `Microsoft.Extensions.DependencyInjection` noch anderer):**  
  Die bestehende Constructor/Factory-Architektur bleibt vollständig erhalten.
* **Kein DI in Roslyn Rules (`Rules/`):**  
  Die Roslyn-SyntaxWalker und Regel-Implementierungen bleiben 100 % zustandslos und containerlos.
* **Kein globaler Service Locator:**  
  `IServiceProvider` wird nicht durch Methodenaufrufe oder Parameter weitergereicht (`Anti-Pattern`). Auflösung erfolgt ausschließlich an den Systemgrenzen.
* **Kein Austausch des Logging- oder CLI-Frameworks:**  
  Serilog und System.CommandLine bleiben unverändert im Einsatz.
* **ProjectLease / ProjectRegistry bleibt unverändert:**  
  Der `ProjectEntry.Server`-Typ bleibt `McpCodeGraphServer` — dort gibt es keine Footprint-Warnung.
* **Keine `rules.json`-Änderungen:**  
  Es gibt keine `PathOverrides` oder `FootprintIgnoreTypeNames` mehr — die wurden bereits entfernt. Das Konzept erzeugt keine neuen Einträge.

---

## 7. Geplante Verifikation

| Phase / Schritt | Verifikationsbefehl | Erwartetes Ergebnis |
|:---|:---|:---|
| Build | `dotnet build` | 0 Warnungen, 0 Fehler (`TreatWarningsAsErrors`) |
| FastTests | `dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"` | 2.370+ bestanden, 0 Fehler |
| IntegrationTests | `dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress"` | 380+ bestanden, 0 Fehler |
| Dead Code Check | MCP `find_dead_code` (`scopeFilter: "src/AiNetLinter/Mcp"`) | **0 tote Symbole** |
| Magic Values Check | MCP `find_magic_values` (`scopeFilter: "src/AiNetLinter/Mcp"`) | **0 unbegründete Magic Values** |
| Duplicate Check | MCP `find_duplicates` (`similarityThreshold: "exact"`) | **0 Duplikat-Cluster** |
| Lint-Violations | MCP `get_violations` (`targetType: "project"`) | **0 Verstöße** in der gesamten Solution |
| Quality Gate | MCP `safeguard` (`minScore: 8.0`) | **10,00 / 10,00 — PASS** |

---

## 8. Risiken & Edge Cases

### 8.1. Interface-Segregation: Reicht `ISolutionStateProvider` aus?

**Risiko:** Einzelne Caller greifen über `lease.Server` auf Members zu, die nicht im Interface sind.

**Analyse:** Die grep-Suche auf `lease.Server` zeigt:
- Häufigste Zugriffe: `.GetCurrentSolution()`, `.AssemblySymbolIdentity`, `.LoadState` → alle im Interface
- Sonderfälle (`ReloadConfigTool`, `OverviewResourceRegistration`, `RulesResourceFormatter`, `ProjectResourceLease`) nutzen `ProjectLease.Server` (anderer Lease-Typ) oder erhalten `McpCodeGraphServer` direkt als Parameter → **nicht betroffen**

**Maßnahme:** Vor Implementierung einmalig alle `lease.Server.`-Zugriffe validieren und ggf. Interface erweitern.

### 8.2. Thread-Safety bei `AssemblyAnalysisLease`

**Risiko:** `AssemblyAnalysisLease` ist thread-safe durch `referenceGate` (Lock-Objekt). Die Typänderung von `McpCodeGraphServer` → `ISolutionStateProvider` ändert keine Locking-Semantik.

**Maßnahme:** Regressionstests für Concurrency-Szenarien (inkl. `AssemblyAnalysisRegistryRetirementRaceTests`).

### 8.3. Assembly-Analyse-Entry-Factory: Erhält `McpCodeGraphServer` über `AssemblyAnalysisRegistryEntryCreation`

Die Entry-Creation-Kette in `AssemblyAnalysisRegistryEntryCreation` erstellt Leases und übergibt den konkreten `McpCodeGraphServer`. Hier muss die Factory ebenfalls das Interface verwenden.

**Edge Case:** `AssemblyAnalysisRegistryEntryCreation` wird in `AssemblyAnalysisHostComposition.Create()` und diversen Tests erstellt. Die Tests konstruieren `McpCodeGraphServer` direkt — diese müssen das Interface nutzen oder die Tests einen konkreten `McpCodeGraphServer` übergeben (der das Interface implementiert).

### 8.4. Body-Resolution und `IAssemblyBodyContext`

`AssemblyAnalysisLease` implementiert `IAssemblyBodyContext` mit:
```csharp
Solution? IAssemblyBodyContext.Solution => Server.GetCurrentSolution();
AnalysisSymbolIdentity? IAssemblyBodyContext.AssemblySymbolIdentity => Server.AssemblySymbolIdentity;
```
Das funktioniert weiterhin, da `ISolutionStateProvider` genau diese Member deklariert.

### 8.5. Kein Over-Engineering: `IExternalResourceRegistry` ist nicht nötig

**Analyse:** `ExternalResourceRegistry` erscheint als Top-Contributor in den Footprint-Warnungen, aber **nicht weil sie direkt referenziert wird**, sondern transitiv über `McpCodeGraphServer`. Sobald `ISolutionStateProvider` die Kette kappt, sinkt der Footprint aller 11 Klassen um ~470+448+316 = ~1.234 Zeilen — weit unter das 2.500-Limit.

Ein separates `IExternalResourceRegistry`-Interface wäre Over-Engineering und wird **nicht** eingeführt.

