# Audit-Bericht: Epic 08 — Test- und Dokumentationsnachweis

## Scope und Evidenz

### Untersuchte Komponenten und Verträge

- **FastTests (Read-only Analyse):**
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/` (`AssemblyAnalysisSessionTests.cs`, `AssemblyAnalysisToolSupportTests.cs`, `AssemblyAnalysisToolTests.cs`, `ManagedAssemblyBinaryTests.cs`, `AssemblyAnalysisFilterTests.cs`, `AssemblyAnalysisConfigurationFailureTests.cs`).
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/` (`AssemblyAnalysisRegistryTests.cs`, `AssemblyAnalysisRegistryRetirementRaceTests.cs`, `AssemblyAnalysisRegistryFreshnessTests.cs`, `AssemblyAnalysisHostCompositionTests.cs`, `Navigation/AssemblyAnalysisRouteTests.cs`).
- **IntegrationTests (Read-only Analyse):**
  - `src/AiNetLinter.IntegrationTests/Mcp/Tools/McpServerAssemblyHealthE2ETests.cs`.
  - `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs`.
- **Dokumentationsdateien (Read-only Analyse):**
  - `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md`, `Docs/ROADMAP.md`, `README.md`.

---

## Befunde

### 1. Bugs

#### FINDING-EPIC08-01: Fehlender Testfall für `get_symbol_body` auf Typ-Ebene in der Testsuite

- **Kategorie:** Bug
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/ManagedAssemblyBinaryTests.cs`
- **Soll-Ist-Abweichung:**
  In der Testsuite existieren zahlreiche Tests für `get_symbol_body` auf Methodenebene, aber kein einziger Testfall, der `get_symbol_body` für ein `INamedTypeSymbol` (Klasse, Struct, Interface) einer dekompilierten Assembly abfragt.
  Aus diesem Grund blieb der in Epic 02 aufgedeckte Fehler `FINDING-EPIC02-01` (`InvalidOperationException` wegen `symbol.ContainingType == null`) unbemerkt und konnte trotz grüner Testsuite im Produktivcode verbleiben.
- **Evidenz:**
  - Analyse aller `GetSymbolBody`-Testmethoden in `AssemblyAnalysisToolSupportTests.cs`: Alle Tests übergeben Methodennamen (z. B. `TestMethod`, `Add`) oder DocCommentIds mit `M:...`, kein Test verwendet `T:...`.
- **Auswirkung:**
  Regressionen und Laufzeitfehler bei Typ-Symbolen werden von den automatisierten Gates nicht abgefangen.
- **Empfehlung:**
  Ergänzung eines Unit-Tests in `ManagedAssemblyBinaryTests.cs`, der `get_symbol_body` für einen Klassennamen aufruft und auf den dekompilierten Klassentext asserted.
- **Abgrenzung:** Testlücke mit direkter Bug-Kausalität.

---

### 2. Optimierungen

#### FINDING-EPIC08-02: Fehlende Concurrency-Stress-Tests für `AssemblyDecompilationCache.Publish`

- **Kategorie:** Optimierung
- **Priorität:** P2
- **Größe:** M
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryRetirementRaceTests.cs`
- **Soll-Ist-Abweichung:**
  Die bestehenden Race-Tests prüfen vor allem das Aufräumen von Sessions und Leases in der Registry (`RetirementRace`), testen jedoch nicht das parallele Schreiben und Ersetzen von Cache-Generationen in `AssemblyDecompilationCache.Publish`. Dadurch blieb der in Epic 04 aufgedeckte Bug `FINDING-EPIC04-01` (Löschen neuer Generationen bei gleichzeitigem `TryRead`) ungetestet.
- **Evidenz:**
  - `AssemblyAnalysisRegistryRetirementRaceTests.cs` mockt Teile des Caches bzw. verwendet separate Test-Ordner pro Session.
- **Auswirkung:**
  Concurrency-Glitches beim Cache-Publishing werden erst im Mehrbenutzer-/Mehr-Session-Betrieb sichtbar.
- **Empfehlung:**
  Hinzufügen eines gezielten Multi-Thread-Tests für `AssemblyDecompilationCache.Publish` mit identischem CacheKey.
- **Abgrenzung:** Testinfrastruktur- und Verifikations-Optimierung.

---

### 3. Missing Features

#### FINDING-EPIC08-03: `memberNames`-Parameter in `Docs/configuration.md` nicht dokumentiert

- **Kategorie:** Missing Feature
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `Docs/configuration.md` (Abschnitt 12)
  - `Docs/agent-api.md`
- **Soll-Ist-Abweichung:**
  In `Docs/agent-api.md` wird der neuere Parameter `memberNames?` (Array für exakte OR-Auswahl) korrekt beschrieben. In `Docs/configuration.md` fehlt dieser Parameter im erläuternden Fließtext und in den CLI-/Tool-Beispielen.
- **Evidenz:**
  - Vergleich von `Docs/agent-api.md` Zeile 359 mit `Docs/configuration.md` Zeile 35.
- **Auswirkung:**
  Entwickler, die `Docs/configuration.md` als Referenz nutzen, übersehen die Möglichkeit zur Mehrfach-Member-Filterung.
- **Empfehlung:**
  Synchronisation der Dokumentation in `Docs/configuration.md`.
- **Abgrenzung:** Dokumentations-Vollständigkeit.

---

## Offene Unsicherheiten

1. **Integrationstest-Laufzeiten:** Bei neuen E2E-Assembly-Tests muss darauf geachtet werden, dass FastTests in-memory bleiben und nicht durch Dateisystem-I/O verlangsamt werden.
