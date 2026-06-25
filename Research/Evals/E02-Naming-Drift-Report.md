# E02 — Naming & Vocabulary Drift Audit Report

**Datum:** 2026-06-25  
**Projekt:** AiNetLinter  
**Auditor:** Antigravity (AI Coding Assistant)  

---

## 1. Spezifikation & Domain-Vokabular (Referenz)

Aus der Projekt-Dokumentation ([README.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/README.md), [rationale.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/rationale.md), [configuration.md](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md)) extrahiertes Kernvokabular (10–20 Begriffe):

### Kernkonzepte (Substantive)
1. **Linter / Linter-Engine**: Das Hauptwerkzeug, das die Code-Analyse ausführt.
2. **Regel (Rule / Linter-Rule)**: Eine einzelne, konfigurierbare Qualitätsprüfung (z. B. `EnforceSealedClasses`).
3. **Verstoß (Violation)**: Ein gefundener Regelverstoß in einer Quellcodedatei.
4. **Baseline (Ratchet)**: Einfrieren bestehender Verstöße, um inkrementelle Änderungen zu prüfen (Ratchet-Prinzip).
5. **Unterdrückung (Suppression)**: Das selektive Deaktivieren einer Regel (inline per Code-Kommentar oder dateiweit).
6. **Auto-Fixer**: Werkzeug zur automatischen Behebung trivialer Verstöße via Syntaxbaum-Transformation.
7. **Playbook**: Ein automatisch generierter Leitfaden zur KI-Kontext-Adaption für ein Repository.
8. **Footprint (Context Footprint)**: Transitive Codezeilen-Anzahl zur Messung der Kopplungsdichte.
9. **Impact-Analyse (Auswirkungsanalyse)**: Analyse betroffener Aufrufstellen bei Signaturänderungen.
10. **Test-Sentinel**: Präsenzwächter zur Prüfung, ob Testabdeckung für komplexe Klassen existiert.
11. **Vertical Slices (Namespace-Abhängigkeiten)**: Trennung von Feature-Namespaces zur Reduzierung von Kopplung.
12. **Debt (Tech-Debt)**: Statistischer Bericht über verbleibende Verstöße und Qualitätsfortschritt.
13. **Cache**: Zwischenspeicher zur Vermeidung wiederholter Analysen.
14. **Profiler / Zeitmessung**: Leistungsmessung der Linter-Phasen.

### Kernoperationen (Verben)
1. **Linten (Lint)**: Ausführen der Qualitätsprüfung.
2. **Prüfen / Analysieren (Check / Analyze)**: Durchlaufen des Syntaxbaums.
3. **Beheben / Fixen (Fix)**: Automatisches Ändern des Codes.
4. **Unterdrücken (Suppress)**: Ignorieren eines Verstoßes.
5. **Synchronisieren (Sync)**: Abgleichen von Regeln in Cursor/Claude-Formate.
6. **Profilieren (Profile)**: Messung der Ausführungszeiten.

---

## 2. Code-Identifiers (Extrahiert)

Zentrale Typen, extrahiert aus dem Produktionscode (`src/AiNetLinter/`):

- **Infrastruktur & CLI**: `CliOptions`, `CliParsedArgs`, `LinterArgs`, `CliCommandBuilder`, `CliOptionFactory`, `Program`, `LinterLogger`, `ILintConsole`, `ConsoleLintConsole`.
- **Kerneinheiten**: `LinterEngine`, `LinterAnalyzer`, `AnalyzerArgs`, `LinterConfig`, `LinterConfigLoader`, `LinterConfigNormalizer`, `LinterConfigSyncer`, `RuleRegistry`, `RuleMetadataEntry`, `RuleMetadataRegistry`.
- **Regelprüfungen (Rules & Checkers)**: `AsyncVoidChecker`, `BlockingTaskChecker`, `BoolParameterChecker`, `ComplexityChecker`, `ControlFlowChecker`, `DynamicTypeChecker`, `ImmutabilityChecker`, `InheritanceDepthChecker`, `LinqChainLengthChecker`, `MinimalApiChecker`, `NamespaceCouplingChecker`, `NestedTypesChecker`, `PhantomDependencyChecker`, `PublicMembersChecker`, `ScopeChecker`, `SealedClassChecker`, `StateChecker`, `ValueObjectChecker`, `WebFileSeparationChecker`, `WpfSeparationChecker`, `NamingChecker`, `TestAttributeDetector`, `DisableAllDetector`, `GeneratedCodeDetector`, `SwitchDispatcherDetector`.
- **Verstöße & Baseline**: `RuleViolation`, `RuleViolationCount`, `FileViolationCount`, `BaselineFile`, `BaselineViolationFilter`, `BaselineComparer`, `BaselineReader`, `BaselineWriter`, `BaselineComparisonResult`.
- **Unterdrückung**: `SuppressionEntry`, `SuppressionCommentParser`, `SuppressionEvaluator`, `SuppressionScanner`, `SuppressionSourceFileResolver`, `DisableAllCommentInjector`, `DisableAllCommentRemover`.
- **Auto-Fixer**: `LinterAutoFixer`, `FixOptions`, `FixContext`.
- **Zusatzfeatures**:
  - **Playbook**: `RepoPlaybookGenerator`, `PlaybookOptions`, `PlaybookStats`, `PlaybookBuildContext`.
  - **Sentinel**: `TestSentinelConfig`, `TestSentinelContext`, `TestProjectDetector`, `TestCoverageIndex`, `TestCoverageCollector`, `TestCoverageResolver`.
  - **Footprint**: `AIContextFootprintCalculator`, `FootprintCommand`.
  - **Impact**: `DiffImpactAnalyzer`, `CliImpactOptions`, `ImpactCommand`.
  - **Debt**: `DebtReportBuilder`, `DebtReportCommand`.
  - **Profiler**: `PerformanceProfiler`, `ProfilerContext`, `ProfilerJsonReport`, `ProfilerSummary`, `PhaseDurationSnapshot`.
  - **Dateiverwaltung**: `SourceFileCatalog`, `WebFileCatalog`, `WebFileEntry`, `SourceFileEntry`.

---

## 3. Analyse & Vergleich (Naming-Drift)

### A. Synonyme (höchste Priorität)

| Konzept | Dokumentierter Begriff | Gefundene Code-Identifiers | Bewertung & Risiko |
| :--- | :--- | :--- | :--- |
| **Muster- / Regelprüfung** | Regel-Prüfung / Check | `*Checker`<br>`*Detector`<br>`*Collector`<br>`*Analyzer`<br>`*Scanner` | **Mittel:** Die Klasse zur Durchführung der eigentlichen Prüfungen heißt meist `*Checker` (z. B. `SealedClassChecker`). Jedoch weichen einige ab (`TestAttributeDetector`, `DisableAllDetector`, `ClassInfoCollector`, `SuppressionScanner`, `LinterAnalyzer`). Dies führt bei KIs zu Unsicherheit, welches Suffix für neue Regeln zu wählen ist. |
| **Unterdrückung** | Suppression / Unterdrückung | `Suppression`<br>`DisableAll` | **Gering:** `DisableAll` bezieht sich spezifisch auf den Spezialfall `ainetlinter-disable all` und seine CLI-Kommandos. Dennoch könnte eine klarere begriffliche Einordnung von "DisableAll" als "Bulk-Suppression" oder "Global-Suppression" im Code nützlich sein. |
| **Auswirkungsanalyse** | Impact / Auswirkungsanalyse | `Impact`<br>`DiffImpact` | **Gering:** CLI verwendet `--impact` (`CliImpactOptions`), die interne Klasse `DiffImpactAnalyzer`. |

### B. Aufgeblähte Namen

Folgende Bezeichner akkumulieren unnötig viele Segmente oder verwenden redundante Wörter:

1. **`SuppressionSourceFileResolver`** (4 Segmente):
   - *Warum verdächtig:* Das Wort "Source" ist redundant, da der Linter primär Source-Files analysiert. `SuppressionFileResolver` ist kürzer und präziser.
2. **`AIContextFootprintCalculator`** (4 Segmente):
   - *Warum verdächtig:* Das Präfix "AIContext" bläht den Namen auf. `FootprintCalculator` oder `ContextFootprintCalculator` ist schlanker.
3. **`ViolatingFilePathResolver`** (4 Segmente):
   - *Warum verdächtig:* `ViolationPathResolver` drückt dasselbe mit 3 Segmenten aus.
4. **`DisableAllCommentInjector` / `DisableAllCommentRemover`** (4 Segmente):
   - *Warum verdächtig:* Das Wort "Comment" ist redundant. `DisableAllInjector` und `DisableAllRemover` sind selbsterklärend.
5. **`UiSeparationConfigOverride`** (4 Segmente):
   - *Warum verdächtig:* Kann zu `UiSeparationOverride` verkürzt werden.

### C. Verwaiste Spec-Begriffe

Zentrale Begriffe aus der Dokumentation, die im Code nicht als Identifier existieren:

1. **Ratchet / Ratchet-Modus**:
   - *Spec:* Wird als fundamentales Konzept ("Baseline-Ratchet", "Ratchet-Prinzip") beschrieben.
   - *Code:* Der Begriff "Ratchet" kommt im Code-Vokabular überhaupt nicht vor. Das Konzept wird rein über `Baseline` (z. B. `BaselineViolationFilter`, `CliBaselineOptions`) abgebildet.
2. **Vertical Slices**:
   - *Spec:* Die Dokumentation beschreibt "Namespace-Abhängigkeitsprüfung (Vertical Slices)".
   - *Code:* Im Code wird das Konzept als `NamespaceCouplingChecker` and `ForbiddenNamespaceDependencies` bezeichnet. Der Begriff "Slice" existiert im Code nicht.

### D. Fremde Begriffe

Code-Begriffe, die in der Dokumentation nicht oder anders vorkommen:

1. **Sentinel**:
   - *Code:* `TestSentinelConfig`, `TestSentinelContext`.
   - *Spec:* Der Begriff "Sentinel" wird meist nur als "Static Test Sentinel" oder "Test-Präsenzwächter" erklärt. Auf sich allein gestellt ist "Sentinel" ohne Dokumentations-Kontext abstrakt.
2. **Catalog**:
   - *Code:* `SourceFileCatalog`, `WebFileCatalog`.
   - *Spec:* Die Dokumentation spricht von "Dateiliste", "Pfad" oder "Workspace". "Catalog" taucht dort nicht auf.

---

## 4. Urteil & Empfehlungen

### Urteil (Naming-Drift): 2.5 / 5 (Mittel)

> [!NOTE]
> Der Naming-Drift ist moderat. Die Code-Bezeichner sind strukturell konsistent und gut lesbar. Der Drift entsteht vor allem durch die Verwendung unterschiedlicher Suffixe für ähnliche Aufgaben (`Checker` vs. `Detector` vs. `Scanner`) sowie durch marketing- bzw. konzeptstarke Begriffe in der Doku (*Ratchet*, *Vertical Slices*), die im Code rein technisch übersetzt wurden (*Baseline*, *NamespaceCoupling*).

### Konkrete Handlungsempfehlungen

1. **Kanonische Suffixe festlegen (Glossar-Verankerung):**
   - Für Klassen, die syntaktische oder semantische Regeln prüfen, wird das Suffix `*Checker` als Standard definiert (z. B. Umbenennung von `TestAttributeDetector` in `TestAttributeChecker` oder Einordnung in `ScopeChecker`).
   - Das Suffix `*Detector` wird reserviert für Klassen, die lediglich Dateitypen oder statische Zustände identifizieren (z. B. `GeneratedCodeDetector`, `TestProjectDetector`), aber selbst keine Linter-Violations erzeugen.
   
2. **Glossar in `AGENTS.md` / `CLAUDE.md` verankern:**
   - Ein kurzes Begriffs-Mapping eintragen, um zukünftigen KI-Agenten die begriffliche Brücke zu bauen:
     - *Ratchet-Prinzip* -> Implementiert als `Baseline`
     - *Vertical Slices* -> Implementiert als `NamespaceCoupling` / `ForbiddenNamespaceDependencies`
     
3. **Refactoring-Kandidaten zur Namensentlastung:**
   - `SuppressionSourceFileResolver` -> `SuppressionFileResolver`
   - `ViolatingFilePathResolver` -> `ViolationPathResolver`
   - `DisableAllCommentInjector` -> `DisableAllInjector`
   - `DisableAllCommentRemover` -> `DisableAllRemover`
   - `AIContextFootprintCalculator` -> `ContextFootprintCalculator` (oder Anpassung an `FootprintCommand`)
