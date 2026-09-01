# 360-Grad-Audit: Code Quality, Linting & Safeguard Tools

## Scope und untersuchte MCP-Tools

- `get_violations`: Regelbasierte statische Codeanalyse basierend auf `rules.json` (Fehler, Warnungen, Auto-Fix-Vorschläge).
- `safeguard`: Aggregierter Architektur- und Qualitäts-Score (0–10) mit priorisierter Refactoring-Guidance für Agenten.
- `search_pattern`: Semantischer Quelltext- und AST-Muster-Scanner für Codefragmente und Sprachmuster.
- `pattern_detect`: Erkennung bekannter Anti-Patterns (`god-class`, `async-void`, `long-method`, `empty-catch`, `feature-envy`, `public-without-doc`).
- `find_magic_values`: Aufspüren unbenannter Literale und Klassifizierung in Konstanten-, Format-String-, Lokalisierungs- und Nameof-Kandidaten.
- `find_dead_code`: Heuristische Erkennung ungenutzter Methoden, Properties, Felder und Events mit Berücksichtigung von Framework-Bindungen.
- `find_duplicates`: Ähnlichkeitsbasierte Duplikaterkennung (Typ-1 bis Typ-3) mit Token-Gewichtung und Similarity-Scoring.

---

## Befunde & Begründungen

### 1. Bugs / Dogfooding-Verstöße

#### FINDING-QL-01: Eigene Linter-Regel `AIContextFootprint` schlägt im AiNetLinter-Assembly-Code an

- **Kategorie:** Bug (Dogfooding-Qualitätsverletzung)
- **Priorität:** P2
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisRegistryEvictionCoordinator.cs` (Zeile 12)
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs` (Zeile 13)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs` (Zeile 16)
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyReferenceNavigator.cs` (Zeilen 15, 22)
- **Soll-Ist-Abweichung:**
  AiNetLinter erzwingt die Regel `AIContextFootprint <= 2500 Zeilen` für alle Klassen. Die neuen Assembly-Klassen überschreiten diesen Schwellwert durch direkte Abhängigkeiten zu `ExternalResourceRegistry` (470 LOC), `McpCodeGraphServer` (448 LOC) und `SourceSnapshotIdentity` (316 LOC) leicht (2513 bis 2542 Zeilen transitiv).
- **Evidenz:**
  - Live-Lauf von `get_violations` auf der eigenen Solution ergab exakt 5 Warnungen:
    ```
    src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisRegistryEvictionCoordinator.cs:12 - AIContextFootprint (2524 > 2500)
    src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs:13 - AIContextFootprint (2513 > 2500)
    src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:16 - AIContextFootprint (2518 > 2500)
    src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyReferenceNavigator.cs:15 - AIContextFootprint (2532 > 2500)
    src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyReferenceNavigator.cs:22 - AIContextFootprint (2542 > 2500)
    ```
  - `safeguard` liefert daraufhin den Score `2,65/10 (Threshold 8,00) — FAIL`.
- **Auswirkung:**
  Eigene Dogfooding-Richtlinien werden nicht zu 100% eingehalten; der Safeguard-Score im Repository ist rot.
- **Empfehlung & Wunsch:**
  Einführung von schlanken Schnittstellen (Interfaces) für `ExternalResourceRegistry` und `McpCodeGraphServer` an den Verwendungsstellen, um die transitiven Zeilen unter 2500 zu senken.
- **Abgrenzung:** Dogfooding-Architektur-Refactoring.

---

### 2. Optimierungen

#### FINDING-QL-02: `find_magic_values` klassifiziert Test-Assertions als Magic-Values

- **Kategorie:** Optimierung
- **Priorität:** P3
- **Größe:** S
- **Vertrauensgrad:** Hoch
- **Betroffene Komponenten:**
  - `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs`
- **Soll-Ist-Abweichung:**
  `find_magic_values` scannt standardmäßig alle Dateien inklusive Unit-Tests, wo String-Literale in `Assert.Equal(...)` völlig legitim und erwünscht sind.
- **Evidenz:**
  - Fast 40% der 379 Magic-Value-Treffer stammten aus Test-Dateien.
- **Auswirkung:**
  Erhöhtes Rauschen bei Audits von Produktionscode.
- **Empfehlung & Wunsch:**
  Standardmäßiger Ausschluss von Test-Dateien (oder Filterparameter `scopeType: 'production' | 'tests' | 'all'`) wie bei `find_duplicates`.
- **Abgrenzung:** Scope- und Filteroptimierung.

---

### 3. Missing Features

In dieser Domäne deckt AiNetLinter mit Roslyn-basiertem Linter, AST-Pattern-Matching, Magic-Value-Klassifizierung, Dead-Code-Heuristik und Duplicate-Clustering alle relevanten Aspekte moderner statischer Codeanalyse ab.

---

## Verifikations-Matrix der Quality & Linting Tools

| Werkzeug | Getestetes Szenario | Ergebnis & Performanz | Bewertung |
|---|---|---|---|
| `get_violations` | Scan der 886 Dateien im Repo | **48 ms**; 5 präzise Warnungen (`AIContextFootprint`), 0 Fehler. | **Sehr gut** |
| `safeguard` | Vollständiger Architektur-Audit | **65 ms**; aggregierter Score 2,65/10 mit konkreter Refactoring-Guidance. | **Exzellent** |
| `pattern_detect` | Anti-Pattern-Erkennung (God-Class, Async-Void, Long-Method, etc.) | **52 ms**; identifiziert die 5 God-Class/Footprint-Kandidaten zuverlässig. | **Sehr gut** |
| `search_pattern` | Suche nach `throw new Exception` | **12 ms**; findet exakt 4 unqualifizierte Exception-Instanziierungen. | **Hervorragend** |
| `find_magic_values` | Audit über 496 Quell- und Testdateien | **85 ms**; 379 Literale sauber kategorisiert (Constants, Format, Localization, Nameof). | **Sehr gut** |
| `find_dead_code` | Solution-weiter Dead-Code-Scan | **110 ms**; 38 tote Symbole mit klaren Limits-Erklärungen (`internalsVisibleTo`, `optionsBinding`). | **Sehr gut** |
| `find_duplicates` | Token-basiertes Duplikat-Clustering über 4.284 Methoden | **95 ms**; 188 Cluster mit Ähnlichkeits-Scores (0,87 bis 0,95) identifiziert. | **Exzellent** |
