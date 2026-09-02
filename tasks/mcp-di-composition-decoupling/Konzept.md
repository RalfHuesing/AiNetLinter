---
status: draft
task: mcp-di-composition-decoupling
datum: 2026-09-02
bereich: src/AiNetLinter/Mcp
---

# Konzept: Gezielte Dependency Injection & Composition Root für das MCP-Subsystem

## 1. Intention & Zielsetzung

AiNetLinter nutzt im MCP-Server- und Daemon-Modus derzeit überwiegend manuelle Konstruktor-Kaskaden (*„Poor Man's DI“*) mit Default-Instanziierungen per `new`. Dadurch instanziieren zentrale Klassen (z. B. `AssemblyAnalysisRegistry`, `GetServerHealthResponseBuilder`, `InspectAssemblyTool`) ihre Subkomponenten und Host-Factories selbst.

### Das Problem
1. **Transitive AIContextFootprint-Explosion:**  
   Roslyn und der AiNetLinter-Footprint-Rechner traversieren konkrete Klassenreferenzen transitiv. Die Kopplung an zentrale Server-Hubs wie `ExternalResourceRegistry` (470 Zeilen) und `McpCodeGraphServer` (448 Zeilen) drückt aktuell mindestens **11 MCP- und Assembly-Klassen** knapp über das Limit von 2.500 Zeilen:
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
2. **Symptombehandlung in `rules.json`:**  
   Bisherige Ausnahmen wie `"AssemblyAnalysisRegistry"` in `FootprintIgnoreTypeNames` sowie **15 dateispezifische `MaxAIContextFootprint`-Overrides** in `rules.json` (für `FindSymbolTool`, `FindReferencesTool`, `GetServerHealthTool`, `GetViolationsTool`, `SymbolGraphToolRegistrations` etc.) kaschieren das Problem auf Konfigurationsebene, heilen jedoch nicht die strukturelle Kopplung.
3. **Begleitende Code-Schulden (DRY, Magic Values, Dead Code):**  
   Durch manuelle Dispatcher-Kaskaden und historische Refactorings verbleiben Duplikate bei der Parameter-Extraktion, Magic Strings bei Fehlermeldungen/Routennamen und totes Wiring (z. B. ungenutzte Retirement-Callbacks in `AssemblyAnalysisRegistryCoordinatorContext`).
4. **Erhöhter Testaufwand in `FastTests`:**  
   Um einzelne Komponenten isoliert im In-Memory-Modus zu testen, müssen komplexe reale Objektbäume aufgebaut werden, statt schlanke Fakes/Mocks injizieren zu können.

### Das Ziel
Einführung einer **gezielten, leichtgewichtigen Dependency Injection (DI)** und eines sauberen **Composition Roots** für den MCP-Server-Host auf Basis von `Microsoft.Extensions.DependencyInjection`, sodass:
- Klassen ausschließlich schlanke Interfaces über Constructor Injection empfangen und keine Subsysteme mehr per `new` erzeugen.
- Der transitive `AIContextFootprint` aller Klassen im MCP-Bereich stabil und ohne Ausnahmeregeln unter dem Limit von 2.500 Zeilen bleibt (Abschneiden der transitiven Bäume bei < 1.400 Zeilen).
- **Alle 15 dateispezifischen Overrides sowie die `AssemblyAnalysisRegistry`-Ausnahme in `rules.json` rückstandslos entfernt werden**.
- DRY-Verstöße, Magic Values und Dead Code im gesamten `Mcp/`-Scope bereinigt werden.
- Der Safeguard-Score **echte 10,00 / 10,00 aus dem Code heraus** erreicht.

---

## 2. Architektonisches Scope-Modell: Hybrider Ansatz

Um maximale Performance im CLI-Batchbetrieb (<50ms) zu garantieren und gleichzeitig saubere Entkopplung im langlebigen MCP-Server-Betrieb zu erreichen, wird ein hybrides Modell festgelegt:

```
┌────────────────────────────────────────────────────────────────────────┐
│                              AiNetLinter                               │
├───────────────────────────────────┬────────────────────────────────────┤
│     MCP-Server & Daemon-Host      │          Core Linter CLI           │
│  (Microsoft.Extensions.DI Root)   │        (Pure / Manual DI)          │
│                                   │                                    │
│  - Assembly-Analysis-Subsystem    │  - LinterEngine                    │
│  - ExternalResource-Registry      │  - Keine DI-Container              │
│  - Tool-Handlers & Dispatcher     │  - Keine Startup-Latenz (<10ms)    │
│  - Telemetrie & Health-Builder    │  - Direkte Methodenaufrufe         │
└───────────────────────────────────┴────────────────────────────────────┘
```

---

## 3. Konkrete Vorschläge & Code-Anker

### 3.1. MCP Host Composition Root (`src/AiNetLinter/Mcp/Composition/`)

* **Code-Anker:**  
  [`src/AiNetLinter/Mcp/Composition/McpServerComposition.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Composition/McpServerComposition.cs) (neu)  
  [`src/AiNetLinter/Mcp/McpServerOptionsFactory.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpServerOptionsFactory.cs)
* **Maßnahme:**
  - Bereitstellung einer Extension-Methode `AddMcpServerServices(this IServiceCollection services, McpServerOptions options)`.
  - Registrierung von Singletons hinter schlanken Interfaces:
    - `IExternalResourceRegistry` statt konkreter `ExternalResourceRegistry`
    - `ISolutionSnapshotProvider` / `IMcpServerState` statt monolithischem `McpCodeGraphServer`
    - `IAssemblyAnalysisRegistry` statt konkreter `AssemblyAnalysisRegistry`
  - Aufbau des `ServiceProvider` einmalig beim Start des MCP-Hosts (`McpCodeGraphServer` / `DaemonHost`).

---

### 3.2. Entkopplung des Assembly-Analysis-Subsystems (`src/AiNetLinter/Mcp/Assemblies/Analysis/`)

* **Code-Anker:**  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs)  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs)  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs)
* **Maßnahme:**
  1. **Einführung von Kern-Interfaces:**
     - `IAssemblyAnalysisResourceBudget`: Abstrahiert `Acquire`, `BeginOperation`, `EvictIdle`, `Health`, `Clock`.
     - `IAssemblyAnalysisEntryFactory`: Abstrahiert die asynchrone Entry-Erstellung (`CreateAsync`).
     - `IAssemblySourceProjectCoordinator`: Abstrahiert die Source-Project-Resolution und das Lease-Mapping.
  2. **Reine Constructor Injection in `AssemblyAnalysisRegistry`:**
     - `AssemblyAnalysisRegistry` instanziiert keine Sub-Factories mehr mit `new`.
     - Alle Abhängigkeiten werden über den primären Konstruktor injiziert.
  3. **Ergebnis:** Der transitive Footprint von `AssemblyAnalysisRegistry` sinkt von **4.362 auf < 1.400 Zeilen**.

---

### 3.3. Entkopplung der MCP Tool Handlers & Response Builder (`src/AiNetLinter/Mcp/Tools/`)

* **Code-Anker:**  
  [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs)  
  [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs)  
  [`src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs)  
  [`src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs)
* **Maßnahme:**
  - Tools und Response-Builder empfangen nicht mehr die monolithischen Klassen `ExternalResourceRegistry` und `McpCodeGraphServer`, sondern ausschließlich gezielte Interfaces (`IAssemblyAnalysisRegistry`, `ISolutionSnapshotProvider`, `IExternalResourceRegistry`).
  - Werkzeuge können als instanziierbare Handler `IMcpToolHandler<TArgs>` im DI-Container registriert werden.
  - Überlange Methoden wie `AssemblySymbolResolver.ResolveAsync` (62 Zeilen) werden im Zuge der Schnittstellenumstellung auf $\le 60$ Zeilen modularisiert.

---

### 3.4. Systematischer Quality-Audit: DRY, Dead Code & Magic Values

* **Maßnahme:**
  1. **Dead-Code-Bereinigung (`find_dead_code`):**
     - Bereinigung von historischem Dead-Wiring in Koordinatoren (z. B. ungenutzte `BeforeRetirementAsync`- und `RetireEntryAsync`-Delegates in `AssemblyAnalysisRegistryCoordinatorContext`).
     - Entfernung ungenutzter interner Überladungen und Hilfsstrukturen im MCP-Scope.
  2. **Magic-Values-Bereinigung (`find_magic_values`):**
     - Zentralisierung von Magic Strings (Tool-Namen, Diagnose-Codes, Header-Texte) und Magic Numbers (Timeout-Defaults, Cache-Limits, Navigations-Tiefen) in dedizierten Kontrakten/Enums/Konstanten.
  3. **DRY-Konsolidierung (`find_duplicates`):**
     - Vereinheitlichung redundanter Parameter-Validierungs- und Fehlerbehandlungsblöcke (`IsErrorPolicy`) in gemeinsamen Dispatcher-Helfern.

---

### 3.5. Vollständige Rücknahme aller `rules.json`-Ausnahmen & Overrides

* **Code-Anker:**  
  [`rules.json`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/rules.json#L156) (Zeile 156–160 und 446–537)  
  [`Docs/configuration.md`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md)
* **Maßnahme:**
  1. **Entfernung aus `FootprintIgnoreTypeNames`:**  
     - `"AssemblyAnalysisRegistry"` wird aus `Metrics.FootprintIgnoreTypeNames` gelöscht (es verbleiben nur die unvermeidlichen Core-Linter-Hubs `LinterEngine` und `NamingChecker`).
  2. **Vollständige Löschung aller 18 dateispezifischen `PathOverrides` unter `src/AiNetLinter/Mcp/**`:**  
     - `src/AiNetLinter/Mcp/McpCodeGraphServer.cs` (bisher Override 2520)
     - `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs` (bisher Override 2830)
     - `src/AiNetLinter/Mcp/Registration/OverviewResourceRegistration.cs` (bisher Override 2650)
     - `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs` (bisher Override 2900)
     - `src/AiNetLinter/Mcp/Tools/FindReferencesTool.cs` (bisher Override 2700)
     - `src/AiNetLinter/Mcp/Tools/FindSymbolTool.cs` (bisher Override 2690)
     - `src/AiNetLinter/Mcp/Tools/GetFileSkeletonTool.cs` (bisher Override 2620)
     - `src/AiNetLinter/Mcp/Tools/GetHotspotsTool.cs` (bisher Override 2610)
     - `src/AiNetLinter/Mcp/Tools/GetIndexScopeTool.cs` (bisher Override 2620)
     - `src/AiNetLinter/Mcp/Tools/GetImpactTool.cs` (bisher Override 2650)
     - `src/AiNetLinter/Mcp/Tools/GetTypeHierarchyTool.cs` (bisher Override 2620)
     - `src/AiNetLinter/Mcp/Tools/GetViolationsTool.cs` (bisher Override 2620)
     - `src/AiNetLinter/Mcp/Tools/SearchPatternTool.cs` (bisher Override 2650)
     - `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` (bisher Override 2700)
     - `src/AiNetLinter/Mcp/Registration/SymbolBodyToolRegistrations.cs` (bisher Override 2830)
     - `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs` (bisher Override 2860)
     - `src/AiNetLinter/Mcp/Tools/GetServerHealthTool.cs` (bisher Override 2860)
     - `src/AiNetLinter/Mcp/Tools/ReloadConfigTool.cs` (bisher Override 2610)
  3. **Regel-Synchronisation:**  
     - Ausführung von `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only` zur Aktualisierung der Agentenregeln in `.agents/rules/AiNetLinter.mdc`.
  4. **Verifikation:**  
     - `get_violations` (0 Violations) und `safeguard` (**10,00 / 10,00 PASS** ohne jegliche Ausnahmeregel).

---

## 4. Muss-Kriterien & Akzeptanzkriterien

1. **Keine `rules.json`-Ausnahmen oder Tool-Overrides im MCP-Bereich:**  
   `rules.json` enthält weder `FootprintIgnoreTypeNames` für MCP-Klassen noch dateispezifische `MaxAIContextFootprint`-PathOverrides für die 18 MCP-Dateien.
2. **0 AIContextFootprint-Warnungen:**  
   Alle Klassen im MCP- und Assembly-Bereich unterschreiten den globalen Standard-Grenzwert von 2.500 Zeilen ohne lokale `#pragma` oder Disable-Kommentare.
3. **0 MaxLineCount- und MaxMethodLineCount-Fehler:**  
   Alle MCP-Produktions- und Testdateien halten $\le 500$ Datei-Zeilen und $\le 60$ Methoden-Zeilen ein.
4. **Sauberes DRY-, Dead-Code- und Magic-Value-Audit:**  
   MCP-Tools `find_dead_code`, `find_magic_values` und `find_duplicates` melden 0 relevante Befunde im `Mcp/`-Scope.
5. **100 % Regressionsfreiheit & Thread-Safety:**  
   Alle FastTests (2.370+ Tests) und IntegrationTests (380+ Tests) laufen vollständig grün durch. Locking- und Concurrency-Invarianten in `AssemblyAnalysisRegistry` bleiben erhalten.
6. **Keine Latenz-Regression im Core-Linter:**  
   Die CLI-Ausführung von `LinterEngine` bleibt containerlos und instantiiert keine unnötigen DI-Strukturen (< 10 ms).

---

## 5. Explizite Non-Goals & Scope-Grenzen

* **Kein DI in Roslyn Rules (`Rules/`):**  
  Die Roslyn-SyntaxWalker und Regel-Implementierungen bleiben 100 % zustandslos und containerlos.
* **Kein globaler Service Locator:**  
  `IServiceProvider` wird nicht durch Methodenaufrufe oder Parameter weitergereicht (`Anti-Pattern`). Auflösung erfolgt ausschließlich an den Systemgrenzen (Composition Root / Dispatcher).
* **Kein Austausch des Logging- oder CLI-Frameworks:**  
  Serilog und System.CommandLine bleiben unverändert im Einsatz.

---

## 6. Geplante Verifikation

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

## 7. Arbeitsgedächtnis (nur Draft)

- **Kontext-Anker:** Analyse vom 2026-09-02 zur Beseitigung aller 11 MCP-Footprint-Warnungen, der 15 `rules.json`-Overrides und Wiederherstellung des echten 10,00/10 Safeguard-Scores.
- **Entscheidung:** Entlastung aller MCP-Klassen durch gezielte Interface-DI (`IExternalResourceRegistry`, `ISolutionSnapshotProvider`, `IAssemblyAnalysisRegistry`) sowie systematischer DRY/DeadCode/MagicValues-Audit im `Mcp/`-Bereich.
- **Nächster Schritt:** Freigabe des Konzepts (`status: ready`) nach Abschluss des aktuellen Assembly-Tasks.
