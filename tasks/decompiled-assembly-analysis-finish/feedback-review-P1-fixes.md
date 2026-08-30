# Review-Bericht: Unabhängige Prüfung der P1-Fixes

## 1. Übersicht & Review-Urteil

- **Status:** `approved`
- **Geprüfter Scope:**
  1. Cancellation-Propagation in `AssemblyAnalysisSession` und `AssemblyAnalysisRegistry`
  2. `AssemblySymbolIdentity` im `get_type_hierarchy`-Resolverpfad
- **Datum:** 2026-08-30
- **Ergebnis:** Beide P1-Befunde sind vollständig, regelkonform und ohne Regressionen behoben. Alle Prüfkriterien und Invarianten sind erfüllt.

---

## 2. Detaillierte Prüfung der P1-Fixes

### 2.1 Cancellation-Propagation in `AssemblyAnalysisSession` & `AssemblyAnalysisRegistry`

| Prüfpunkt | Befund | Bewertung |
|:---|:---|:---|
| **Echte `OperationCanceledException`-Weitergabe** | In `AssemblyAnalysisSession.CreateSnapshotAsync` wird bei Abbruch der erzeugte `AssemblyRoslynSnapshot` via `snapshot?.Dispose()` bereinigt und `throw;` ausgeführt. In `AssemblyRoslynWorkspaceFactory.CreateAsync` schützt ein `try / catch { workspace.Dispose(); throw; }` vor verwaisten Workspaces. `AssemblyDecompilationAdapter.DecompileAsync` unterscheidet Caller-Cancellation (Re-throw) von internen Deadlines. | **Erfüllt** |
| **Korrektes shared-Creation-Verhalten** | In `AssemblyAnalysisRegistry.TryLeaseCurrentAsync` fängt `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` die Caller-Cancellation ab und wirft sofort weiter. Die gemeinsame Hintergrund-Creation läuft für andere parallele Waiter ungestört weiter; der Entry wird nicht vorzeitig entfernt. | **Erfüllt** |
| **Fail-Closed bei echtem Creation-Abbruch** | Bricht die Creation selbst ab (z. B. Registry-Dispose oder internes Creation-Token), fängt der nachgelagerte Catch-Block die Exception, entfernt den fehlerhaften Eintrag via `RemoveFailedEntry` und liefert ein kontrolliertes Fehlermodell. `ObserveCreation` sichert dies asynchron via `ContinueWith` ab. | **Erfüllt** |
| **Keine Lease-/Registry-Leaks** | Alle Ressourcenpfade (`CreateEntryAsync`, `DisposeAsync`, Snapshot-Leases mit Ref-Counting und `leasesDrained`) sind gegen Leaks geschützt. | **Erfüllt** |

### 2.2 `AssemblySymbolIdentity` im `get_type_hierarchy`-Resolverpfad

| Prüfpunkt | Befund | Bewertung |
|:---|:---|:---|
| **Durchreichung der Identität** | In `GetTypeHierarchyTool.ExecuteAsync` wird `state.AssemblySymbolIdentity` nun explizit an `FindReferencesTool.ResolveSymbolAsync(solution, symbolIdentifier, ct, state.AssemblySymbolIdentity)` übergeben. | **Erfüllt** |
| **Aktuelle und Stale Assembly-IDs** | `SymbolIdentifierResolver.TryResolveByStableIdAsync` validiert `expectedAssemblyIdentity.Matches(...)`. Passende verpackte IDs (`assembly:<hash>:<gen>:T:...`) werden aufgelöst; veraltete IDs werden als `INVALID_ARGUMENT` mit Stale-Hinweis abgewiesen. | **Erfüllt** |
| **Schutz gegen Umgehung (Unwrapped IDs)** | Wird auf einem Assembly-Ziel eine unverpackte ID (`T:...`) übergeben, wird diese ebenfalls abgewiesen (`StaleAssemblyId`), sodass die Generations-/Hash-Prüfung nicht umgangen werden kann. | **Erfüllt** |
| **A→B→A-Generationsmonotonie** | Die monoton steigende Generationsvergabe (`nextGenerations`) stellt sicher, dass ein A→B→A-Zyklus (Generation 1 → 2 → 3) IDs aus Generation 1 zuverlässig als veraltet erkennt. | **Erfüllt** |
| **Unveränderte Projekt-IDs** | Für Projekt-Ziele (`state.AssemblySymbolIdentity = null`) bleibt das Verhalten vollständig abwärtskompatibel und unverändert. | **Erfüllt** |
| **Härtung bei In-Memory-Lösungen** | `PathNormalizer.ToRelative` und `DiRegistrationHeuristics` fangen leere `outputRoot`-Pfade bei synthetischen Roslyn-Lösungen sicher ab. | **Erfüllt** |
| **Echter Tool-/Route-Test** | In `GetTypeHierarchyToolTests.ExecuteRouted_AssemblyAndProjectRoutes_ValidateAssemblySymbolIdentityAndAllowProjectSymbols` werden alle 4 Szenarien über die reale Dispatcher-Pipeline `AnalysisToolCall.ExecuteRouted` end-to-end getestet. | **Erfüllt** |

---

## 3. Verifikationsergebnisse & Messungen

1. **Build:**
   - `dotnet build`: `0 Warnung(en)`, `0 Fehler` (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`).
2. **FastTests (`Category!=Stress`):**
   - `dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"`: **2.208 bestanden, 0 Fehler, 2 übersprungen** (Reparse Privilege Tests).
3. **IntegrationTests:**
   - Gezielte E2E-/Contract-/Tool-Tests (`McpServerAllToolsE2ETests`, `McpServerCommandContractTests`, `McpLiveRepositoryTests`, etc.): **81 bestanden, 0 Fehler**.
4. **Semantische MCP-Qualitätsprüfung:**
   - `safeguard`: Score **10,00/10** (Threshold 8,00) — **PASS**, 871 Klassen analysiert, 0 Verstöße.
   - `get_violations`: **0 Verstöße** in 806 Dateien im Scope.
5. **Sicherheits- & Architekturvorgaben:**
   - Keine untersuchte Assembly wurde geladen, reflektiert oder ausgeführt.
   - Keine AIContextFootprint-Regelverstöße oder Qualitätsdrift.
