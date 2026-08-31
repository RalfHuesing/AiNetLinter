---
status: draft
task: mcp-di-composition-decoupling
datum: 2026-08-31
bereich: src/AiNetLinter/Mcp
---

# Konzept: Gezielte Dependency Injection & Composition Root für das MCP-Subsystem

## 1. Intention & Zielsetzung

AiNetLinter nutzt im MCP-Server- und Daemon-Modus derzeit überwiegend manuelle Konstruktor-Kaskaden (*„Poor Man's DI“*) mit Default-Instanziierungen per `new`. Dadurch instanziieren zentrale Klassen (z. B. `AssemblyAnalysisRegistry`, `GetServerHealthResponseBuilder`, `InspectAssemblyTool`) ihre Subkomponenten und Factories selbst.

### Das Problem
1. **Transitive AIContextFootprint-Explosion:**  
   Roslyn und der AiNetLinter-Footprint-Rechner traversieren konkrete Klassenreferenzen transitiv. Wenn eine Registry ihre 4–5 Sub-Factories und Resource-Budgets direkt per `new` instanziiert, wird der gesamte Sourcecode dieser Subsysteme dem Footprint der Registry zugerechnet (z. B. 4.362 Zeilen bei `AssemblyAnalysisRegistry` vs. Limit 2.500).
2. **Symptombehandlung in `rules.json`:**  
   Bisherige Ausnahmen wie `"AssemblyAnalysisRegistry"` in `FootprintIgnoreTypeNames` lösen das Problem auf Konfigurationsebene, heilen jedoch nicht die strukturelle Kopplung im Code.
3. **Erhöhter Testaufwand in `FastTests`:**  
   Um einzelne Komponenten isoliert im In-Memory-Modus zu testen, müssen komplexe reale Objektbäume aufgebaut werden, statt schlanke Fakes/Mocks injizieren zu können.

### Das Ziel
Einführung einer **gezielten, leichtgewichtigen Dependency Injection (DI)** und eines sauberen **Composition Roots** für den MCP-Server-Host auf Basis von `Microsoft.Extensions.DependencyInjection`, sodass:
- Klassen ausschließlich schlanke Interfaces über Constructor Injection empfangen und keine Subsysteme mehr per `new` erzeugen.
- Der transitive `AIContextFootprint` aller Klassen im MCP-Bereich stabil und ohne Ausnahmeregeln unter dem Limit von 2.500 Zeilen bleibt.
- Die Ausnahmeregel in `rules.json` für `AssemblyAnalysisRegistry` rückstandslos entfernt werden kann und der Safeguard-Score **echte 10,00 / 10,00 aus dem Code heraus** erreicht.

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
│  - ExternalSource-Registry        │  - Keine DI-Container              │
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
  - Registrierung der Singletons (`ExternalResourceRegistry`, `SourceSnapshotRegistry`, `AssemblyAnalysisRegistry`, `TimeProvider`).
  - Aufbau des `ServiceProvider` einmalig beim Start des MCP-Hosts (`McpCodeGraphServer` / `DaemonHost`).

---

### 3.2. Entkopplung des Assembly-Analysis-Subsystems (`src/AiNetLinter/Mcp/Assemblies/Analysis/`)

* **Code-Anker:**  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs)  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs)  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs)  
  [`src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisSourceProjectEntryFactory.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisSourceProjectEntryFactory.cs)
* **Maßnahme:**
  1. **Einführung von Kern-Interfaces:**
     - `IAssemblyAnalysisResourceBudget`: Abstrahiert `Acquire`, `BeginOperation`, `EvictIdle`, `Health`, `Clock`.
     - `IAssemblyAnalysisEntryFactory`: Abstrahiert die asynchrone Entry-Erstellung (`CreateAsync`).
     - `IAssemblySourceProjectCoordinator`: Abstrahiert die Source-Project-Resolution und das Lease-Mapping.
  2. **Reine Constructor Injection in `AssemblyAnalysisRegistry`:**
     - `AssemblyAnalysisRegistry` instanziiert keine Sub-Factories mehr mit `new`.
     - Alle Abhängigkeiten (`IAssemblyAnalysisResourceBudget`, `IAssemblyAnalysisEntryFactory`, `IAssemblySourceProjectCoordinator`) werden über den primären Konstruktor injiziert.
  3. **Ergebnis:** Der transitive Footprint von `AssemblyAnalysisRegistry` sinkt von **4.362 auf < 1.400 Zeilen**.

---

### 3.3. Entkopplung der MCP Tool Handlers (`src/AiNetLinter/Mcp/Tools/`)

* **Code-Anker:**  
  [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs)  
  [`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs)  
  [`src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs)
* **Maßnahme:**
  - Tools empfangen nicht mehr die monolithische Klasse `McpCodeGraphServer? state`, sondern gezielte Schnittstellen (z. B. `IAssemblyAnalysisRegistry`, `ISolutionSnapshotProvider`).
  - Werkzeuge können als instanziierbare Handler `IMcpToolHandler<TArgs>` im DI-Container registriert werden.

---

### 3.4. Rücknahme der `rules.json`-Ausnahme & Safeguard-Verifikation

* **Code-Anker:**  
  [`rules.json`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/rules.json#L156)  
  [`Docs/configuration.md`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md)
* **Maßnahme:**
  - Entfernung von `"AssemblyAnalysisRegistry"` aus `Metrics.FootprintIgnoreTypeNames`.
  - Ausführung von `dotnet run --project src/AiNetLinter -- --sync-agent-rules-only`.
  - Verifikation via `get_violations` (0 Violations) und `safeguard` (**10,00 / 10,00 PASS**).

---

## 4. Muss-Kriterien & Akzeptanzkriterien

1. **Keine `rules.json`-Ausnahme für `AssemblyAnalysisRegistry`:**  
   `rules.json` enthält unter `FootprintIgnoreTypeNames` ausschließlich die historischen Core-Linter-Klassen (`LinterEngine`, `NamingChecker`).
2. **0 AIContextFootprint-Warnungen:**  
   Alle Klassen im MCP- und Assembly-Bereich unterschreiten den Grenzwert von 2.500 Zeilen ohne lokale `#pragma` oder Disable-Kommentare.
3. **100 % Regressionsfreiheit & Thread-Safety:**  
   Alle bestehenden FastTests (2.275 Tests) und IntegrationTests (377 Tests) laufen vollständig grün durch. Locking- und Concurrency-Invarianten in `AssemblyAnalysisRegistry` bleiben erhalten.
4. **Keine Latenz-Regression im Core-Linter:**  
   Die CLI-Ausführung von `LinterEngine` bleibt containerlos und instantiiert keine unnötigen DI-Strukturen.

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
| FastTests | `dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"` | 2.273+ bestanden, 0 Fehler |
| IntegrationTests | `dotnet test src/AiNetLinter.IntegrationTests --filter "Category!=Stress"` | 377+ bestanden, 0 Fehler |
| Lint-Violations | MCP `get_violations` (`targetType: "project"`) | **0 Verstöße** in der gesamten Solution |
| Duplicate Check | MCP `find_duplicates` (`similarityThreshold: "exact"`) | **0 Duplikat-Cluster** |
| Quality Gate | MCP `safeguard` (`minScore: 8.0`) | **10,00 / 10,00 — PASS** |

---

## 7. Arbeitsgedächtnis (nur Draft)

- **Kontext-Anker:** Diskussion vom 2026-08-31 zur Beseitigung der letzten Footprint-Schulden und sauberen Safeguard-10/10-Wiederherstellung.
- **Entscheidung:** Entlastung von `AssemblyAnalysisRegistry` durch gezielte Constructor Injection von Interfaces statt temporärer `rules.json`-Symptombekämpfung.
- **Nächster Schritt:** Vorbereitung des Orchestrator-Epics nach Freigabe des Konzepts.
