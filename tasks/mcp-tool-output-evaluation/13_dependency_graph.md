# 13 – dependency_graph

## 1. Tool-Steckbrief & Agenten-Rolle
- **Name:** `dependency_graph`
- **Agenten-Priorität:** Prio 13 (Mittel – Analyse semantischer Typ-Abhängigkeiten auf Datei- oder Symbolebene)
- **Hauptzweck:** Beantwortet die Frage „Wer hängt von Typ/Datei X ab?“ bzw. „Von wem hängt Typ/Datei X ab?“ basierend auf dem echten Roslyn-`SemanticModel` (echte Typreferenzen, keine simplen `using`-Direktiven).
- **Wichtigste Parameter:**
  - `targetPath` (Pflicht, String): Absoluter Pfad zur `.sln`/`.slnx` oder `.dll`.
  - `filePath` (String) **ODER** `symbolIdentifier` (String): Entweder ganze Datei oder konkreter Typ (gegenseitig exklusiv!).
  - `direction` (Optional, String, Default `both`): `incoming`, `outgoing`, `both`.
  - `depth` (Optional, Int, Default 1, Cap 3): Traversierungstiefe.
  - `maxResults` (Optional, Int, Default 50).

---

## 2. Test-Samples & Rohe Tool-Outputs

### Sample A: Eingehende Typ-Abhängigkeiten (`symbolIdentifier: "AiNetLinter.Models.RuleViolation"`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "symbolIdentifier": "AiNetLinter.Models.RuleViolation",
  "direction": "incoming",
  "depth": 1
}
```
- **Tool-Output (Rohdaten):**
```text
Scope: all; includeGenerated=False


Eingehende Abhaengigkeiten (wer verwendet Typen aus diesem Scope):
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Baseline\BaselineViolationFilter.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Cache\CacheEntryMapper.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Commands\AuditCommand.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Configuration\RuleMetadataRegistry.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\AnalysisState.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\Checkers\CheckerContext.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\Checkers\DuplicateCodeChecker.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\Checkers\UiFileSeparationChecker.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\DuplicateDetection\DuplicateDetectionModels.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\LinterAnalyzer.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\LinterAutoFixer.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\LinterEngine.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\PartialClassLineAggregator.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\PostAnalysisChecks.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\RuleRegistry.Severity.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Core\TestSentinelContext.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\Analysis\DiffViolationScanner.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\Analysis\GetViolationsScanner.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\Analysis\ViolationScopeFilter.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\FeatureContext\FeatureContextScanner.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\MetricsTree\MetricsTreeRoslynScanner.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\PatternDetect\PatternCatalog.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\PatternDetect\PatternDetectScanner.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\Safeguard\SafeguardModels.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\Safeguard\SafeguardScanner.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Mcp\Tools\SymbolGraph\ChangeContextResponseModels.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Models\RuleViolationFactory.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Output\ViolationMarkdownFormatter.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Output\ViolationSummaryBuilder.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Scope\ViolationScopeFilter.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Suppression\ViolationPathResolver.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Web\CssAnalyzer.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Web\JsAnalyzer.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Web\RazorAnalyzer.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Web\WebFileSeparationChecker.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Baseline\BaselineViolationFilterTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Cache\CacheEntryMapperTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Configuration\AgentFeaturesTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Configuration\RuleMetadataRegistryTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\Checkers\DuplicateCodeCheckerTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\Checkers\MaxBoolParameterCountTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\Checkers\MaxPublicMembersPerTypeTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\Checkers\MethodParameterCountAccessibilityTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\Checkers\NestedTypesCheckerTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\Checkers\UiFileSeparationCheckerTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Core\CompoundSuppressionIntegrationTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Mcp\Tools\Analysis\DiffViolationFilterTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Mcp\Tools\Analysis\GetViolationsToolTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Mcp\Tools\Safeguard\SafeguardScannerTests.cs (1 Typ: RuleViolation)
- c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter.FastTests\Mcp\Tools\Safeguard\SafeguardScoreMetadataTests.cs (1 Typ: RuleViolation)
[57 Kanten gesamt, 50 gezeigt — depth reduzieren oder maxResults erhoehen]
```

### Sample B: Ausgehende Datei-Abhängigkeiten (`filePath: "src/AiNetLinter/Core/LinterEngine.cs"`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "filePath": "src/AiNetLinter/Core/LinterEngine.cs",
  "direction": "outgoing",
  "maxResults": 15
}
```
- **Tool-Output (Rohdaten):**
```text
Scope: all; includeGenerated=False
Ausgehende Abhaengigkeiten von 'src/AiNetLinter/Core/LinterEngine.cs' (depth=1):
- src/AiNetLinter/Baseline/FileChecksumCalculator.cs (1 Typ: FileChecksumCalculator)
- src/AiNetLinter/Baseline/ProjectRestoreState.cs (1 Typ: ProjectRestoreState)
- src/AiNetLinter/Baseline/SourceFileCatalog.cs (2 Typen: CatalogDocumentWorkItem, SourceFileCatalog)
- src/AiNetLinter/Cache/AnalysisCacheEntry.cs (2 Typen: AnalysisCacheEntry, TestSignalsDto)
- src/AiNetLinter/Cache/AnalysisCacheManager.cs (1 Typ: AnalysisCacheManager)
- src/AiNetLinter/Cache/CacheEntryMapper.cs (2 Typen: BuildEntryParams, CacheEntryMapper)
- src/AiNetLinter/Configuration/Config.cs (1 Typ: Config)
- src/AiNetLinter/Configuration/FileFilterEvaluator.cs (1 Typ: FileFilterEvaluator)
- src/AiNetLinter/Configuration/ProjectConfigResolver.cs (1 Typ: ProjectConfigResolver)
- src/AiNetLinter/Core/AnalysisState.cs (1 Typ: AnalysisState)
- src/AiNetLinter/Core/Documents/DocumentContext.cs (1 Typ: DocumentContext)
- src/AiNetLinter/Core/LinterAnalyzer.cs (2 Typen: AnalyzerArgs, LinterAnalyzer)
- src/AiNetLinter/Core/PartialClassLineAggregator.cs (1 Typ: PartialClassPart)
- src/AiNetLinter/Core/PostAnalysisChecks.cs (1 Typ: PostAnalysisChecks)
- src/AiNetLinter/Core/TestCoverageIndex.cs (1 Typ: TestCoverageIndex)
[25 Kanten gesamt, 15 gezeigt — depth reduzieren oder maxResults erhoehen]
```

### Sample C: Parameter-Konflikt (`filePath` UND `symbolIdentifier` angegeben)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "filePath": "src/AiNetLinter/Core/LinterEngine.cs",
  "symbolIdentifier": "RuleViolation"
}
```
- **Tool-Output (Rohdaten):**
```text
[ERROR]: INVALID_ARGUMENT: filePath und symbolIdentifier sind gegenseitig exklusiv — genau einen angeben, nie beide oder keins.
  hint:    Entweder filePath ODER symbolIdentifier angeben, nie beide.
```

---

## 3. Effizienz- & SNR-Bewertung (gemäß AiNetLinter-Richtlinien)
- **Token-Footprint:**
  - Sample A (50 Abhängigkeiten): ~6.1 KB (~1.400 Token).
  - Sample B (15 Abhängigkeiten): ~1.2 KB (~280 Token).
- **Signal-to-Noise Ratio (SNR):** Sehr hoch. Jede Zeile nennt den Dateipfad und die exakte Anzahl sowie Namen der referenzierten Typen.
- **Trunkierungs-Hinweis:** Gibt genau an, wie viele Kanten existieren und wie der Call angepasst werden kann (`[25 Kanten gesamt, 15 gezeigt — depth reduzieren oder maxResults erhoehen]`).
- **Content-Only-Hard-Cut:** Konform.

---

## 4. Composability & Chaining-Validierung (Input/Output-Passgenauigkeit)
- **WICHTIGER BEFUND ZUR PFAD-KONSISTENZ (Asymmetrie):**
  - Bei `symbolIdentifier`-Abfragen (Sample A) gibt das Tool **vollständige absolute Windows-Pfade** mit Backslashes aus:
    `c:\Daten\Entwicklung\Ralf\AiNetLinter\src\AiNetLinter\Baseline\BaselineViolationFilter.cs`
  - Bei `filePath`-Abfragen (Sample B) gibt das Tool **relative Unix-Pfade** mit Forward-Slashes aus:
    `src/AiNetLinter/Baseline/FileChecksumCalculator.cs`
  - **Wirkung auf Agenten:** Die Pfad-Inkonsistenz erfordert Normalisierungs-Logik, wenn ein Agent Pfade zwischen `get_file_skeleton` und `dependency_graph` austauscht.
- **Chaining-Passgenauigkeit:**
  | Ausgabefeld | Folge-Tool | Parameter | Status | Anmerkung |
  |---|---|---|---|---|
  | Relativer Pfad | `get_file_skeleton` | `filePaths` | ✅ Direkt | Aus Sample B direkt übergebbar |
  | Absoluter Pfad | `get_file_skeleton` | `filePaths` | ✅ Direkt | `get_file_skeleton` akzeptiert auch absolute Pfade |
  | Typname in Klammern | `find_symbol` | `pattern` | ✅ Direkt | z. B. `FileChecksumCalculator` |

---

## 5. Fazit & Architekturentscheidung (Offener Punkt)
- **Prädikat:** **Sehr gut**
- **Stärken:**
  1. Ermittelt echte semantische Kopplungen, nicht nur oberflächliche Using-Deklarationen.
  2. Robuste gegenseitige Ausschluss-Prüfung der Parameter.
- **Architektonische Weichenstellung (Zur Abstimmung):**
  - **Befund:** Bei `symbolIdentifier`-Abfragen werden absolute Windows-Pfade ausgegeben, bei `filePath`-Abfragen relative Pfade.
  - **Überlegung & Nutzer-Feedback:**
    - Ein reiner Wechsel auf relative Pfade greift zu kurz, da der Server auch Assemblies dekompiliert. Bei dekompilierten Quelltexten liegen die Dateien oft in isolierten Cache-/Snapshot-Verzeichnissen.
    - Damit Agenten dekompilierten Quellcode auch mit Built-in-Tools (Dateieditoren, Ripgrep, CLI) untersuchen können, muss der absolute Speicherort für den Agenten auffindbar bleiben.
    - **Mögliche architektonische Lösungen:**
      1. **Einmalige Basis-Pfadangabe im Header:** Absolutes Arbeits-/Dekompilierungs-Verzeichnis einmalig oben im Header nennen (`Basisverzeichnis: c:\...`), in den Fundlisten einheitlich schlanke relative Pfade nutzen.
      2. **Kontext-spezifisch:** Bei Solution-Code relative Pfade, bei extern dekompilierten Assemblies absolute Pfade beibehalten.
  - **Entscheidung:** Erfordert eine übergreifende Abstimmung zur Pfad-Philosophie (insb. für Decompiler-Workflows). Keine übereilte Codeänderung.
