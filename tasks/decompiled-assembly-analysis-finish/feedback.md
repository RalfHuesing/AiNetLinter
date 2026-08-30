# Feedback & Abschlussbericht: P1-Fix Assembly-Symbolidentität bei `get_type_hierarchy`

## 1. Übersicht & Kontext

- **Task**: Gezielter Follow-up-Fix für den dokumentierten P1-Blocker *Assembly-Symbolidentität bei `get_type_hierarchy`*
- **Bearbeitete Komponenten**:
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs`
  - `src/AiNetLinter/Output/PathNormalizer.cs`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/DiRegistrationHeuristics.cs`
  - `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetTypeHierarchyToolTests.cs`
  - `src/AiNetLinter.FastTests/Output/PathNormalizerTests.cs`
- **Status**: Erfolgreich implementiert, vollständig getestet und verifiziert.

---

## 2. Ursachenanalyse & Behebung

### 2.1 Problem im `get_type_hierarchy`-Pfad
In `GetTypeHierarchyTool.cs` wurde `FindReferencesTool.ResolveSymbolAsync(solution, symbolIdentifier, ct)` ohne den Parameter `assemblyIdentity` aufgerufen. Dadurch wurde `state.AssemblySymbolIdentity` (die an ContentHash und Generation gebunden ist) nicht an den `SymbolIdentifierResolver` übergeben.

**Folgen des ursprünglichen Verhaltens:**
1. Gültige verpackte Assembly-IDs (`assembly:<hash>:<gen>:T:...`) wurden abgewiesen, da der Resolver ein Projekt-Target erwartete.
2. Unverpackte Typ-IDs (`T:...`) konnten auf Assembly-Targets die Hash-/Generations-Prüfung umgehen.
3. Veraltete IDs nach Generationswechseln (A → B → A) wurden nicht als *stale* erkannt.

### 2.2 Korrektur in `GetTypeHierarchyTool.cs`
Die Übergabe von `state.AssemblySymbolIdentity` wurde ergänzt:
```csharp
var (symbol, error) = await FindReferencesTool.ResolveSymbolAsync(
    solution, symbolIdentifier, ct, state.AssemblySymbolIdentity);
```
Da der Assembly-Dispatcher über `lease.Server` eine Serverinstanz mit gesetzter `AssemblySymbolIdentity` bereitstellt und Projekt-Calls `AssemblySymbolIdentity = null` haben, funktioniert die Auflösung end-to-end für beide Target-Typen.

### 2.3 Ergänzende Härtung in `PathNormalizer.cs` & `DiRegistrationHeuristics.cs`
Bei synthetischen / In-Memory-Roslyn-Lösungen (wie sie für dekompilierte Assemblies im `AdhocWorkspace` existieren) ist `solution.FilePath` `null` bzw. leer.
- `PathNormalizer.ToRelative` wirft bei leerem/whitespace `outputRoot` keine `ArgumentException` (`Path.GetFullPath("")`) mehr, sondern liefert den Dateinamen zurück.
- `DiRegistrationHeuristics.cs` nutzt nun konsistent `PathNormalizer.ToRelative(Init.OutputRoot, Init.FilePath)`.

---

## 3. Testabdeckung

In `src/AiNetLinter.FastTests/Mcp/Tools/SymbolGraph/GetTypeHierarchyToolTests.cs` wurde der Integrationstest `ExecuteRouted_AssemblyAndProjectRoutes_ValidateAssemblySymbolIdentityAndAllowProjectSymbols` implementiert:

1. **Aktuelle Assembly-ID auf Assembly-Target**:
   - `CallGraphTraversal.GetStableSymbolId` erzeugt verpackte ID `assembly:<hash>:1:T:Probe.Service`.
   - `get_type_hierarchy` über die geroutete Dispatcher-Pipeline liefert erfolgreich `IService` und `Probe.IService`.
2. **A → B → A Generationswechsel**:
   - Nach Neukompilierung und Generationserhöhung auf Generation 3 wird die veraltete ID aus Generation 1 als `INVALID_ARGUMENT` mit dem Hinweis `aktuellen Assembly-Generation` abgewiesen.
3. **Schutz gegen Umgehung (Unwrapped ID)**:
   - Eine unverpackte ID (`T:Probe.Service`) auf dem Assembly-Target wird ebenfalls abgewiesen.
4. **Projekt-Target-Kompatibilität**:
   - Standard-Projekt-IDs (`BaseGreeting`) auf Projekt-Targets liefern unverändert die Hierarchie (`IGreeting`, `SpecialGreeting`).

In `src/AiNetLinter.FastTests/Output/PathNormalizerTests.cs` wurde ein Unit-Test für leere/whitespace `outputRoot`-Werte ergänzt.

---

## 4. Verifikationsergebnisse

- **Build**:
  ```bash
  dotnet build AiNetLinter.slnx --no-restore
  ```
  `0 Warnung(en)`, `0 Fehler`.
- **FastTests (`Category!=Stress`)**:
  ```bash
  dotnet test src/AiNetLinter.FastTests --filter "Category!=Stress"
  ```
  `2.208 bestanden`, `2 übersprungen`, `0 Fehler`.
- **Relevante IntegrationTests**:
  ```bash
  dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~McpServerAllToolsE2ETests|FullyQualifiedName~McpServerCommandContractTests|FullyQualifiedName~McpLiveRepositoryResourceTests|FullyQualifiedName~McpLiveRepositoryTests|FullyQualifiedName~McpHandshakeToolRegistrationTests"
  ```
  `81 bestanden`, `0 Fehler`.
- **Whitespace / Diff-Check**:
  ```bash
  git --no-pager diff --check
  ```
  Sauber.
- **MCP Qualitäts-Checks**:
  - `safeguard`: **10,00/10** (Threshold 8,00) — PASS, 0 Verstöße.
  - `get_violations`: **0 Verstöße** in 806 Dateien im Scope.
