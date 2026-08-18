# Konzept: `get_feature_context` (Composite One-Shot-Exploration)

## 1. Problemstellung & Motivation
Wenn ein Agent ein bestehendes Feature modifizieren, eine Methode erweitern oder ein Refactoring durchführen soll, benötigt er stets dasselbe Informations-Bündel:
1. **Symbol-Definition & Struktur:** Was ist das für ein Typ/Member, wo liegt er und wie viele Zeilen umfasst er?
2. **Metriken & Budget:** Wie hoch sind Cyclomatic / Cognitive Complexity, LOC und AI-Context-Footprint? Ist noch Budget vor dem `rules.json`-Limit vorhanden?
3. **Blast-Radius & Callers:** Wer ruft diese Methode / Klasse direkt auf?
4. **Test-Abdeckung:** Welche Unit- und Integrationstests sichern diesen Code ab?
5. **Offene Mängel:** Gibt es auf dieser Datei / diesem Symbol bereits bekannte Linter-Violations?

Bisher muss der Agent hierfür **4–5 separate MCP-Tools** hintereinander aufrufen (`get_class_structure` ➔ `metrics_lookup` ➔ `find_references` ➔ `get_test_context` ➔ `get_violations`).
Das kostet **4–5 Hin- und Rückrunden (Roundtrips)** und fragmentiert den Kontext.

👉 **Die Lösung:** `get_feature_context` fasst diese 5 Dimensionen in einem **einzigen, hoch-effizienten One-Shot-Call** zusammen.

---

## 2. Warum hat dieses Feature einen enormen Hebel?
- **Workflow-Beschleunigung:** 1 Call statt 5 Calls vor Beginn jedes Refactorings oder Feature-Edits.
- **Runderes Agenten-Bild:** Der Agent sieht sofort auf einer Seite: Code-Struktur + Aufrufer + Tests + Metriken-Grenzwerte + offene Violations.
- **Zero Disk-I/O & Maximale Performance:** Da alle Sub-Systeme im residenten Roslyn-Server in-memory vorliegen, aggregiert `get_feature_context` alle 5 Sektionen in < 50ms.

---

## 3. Tool-Spezifikation

* **Tool-Name:** `get_feature_context`
* **Registrierung:** In `AnalysisToolRegistrations.cs` (oder `FileStructureToolRegistrations.cs`)
* **Parameter:**
  * `symbol` (`string`, Pflicht): Typname, Methodenname, `Datei.cs:Zeile` oder `DocCommentId` (z. B. `"AiNetLinter.Core.LinterAnalyzer"` oder `"McpCodeGraphServer.RefreshAsync"`).
  * `includeCallers` (`bool`, optional, Default `true`): Ob direkte Aufrufer (Callers) gelistet werden sollen.
  * `includeTests` (`bool`, optional, Default `true`): Ob zugehörige Testklassen und Testmethoden gelistet werden sollen.
  * `includeMetrics` (`bool`, optional, Default `true`): Ob Komplexitäts- und LOC-Metriken inkl. Schwellwert-Abgleich ermittelt werden sollen.
  * `includeViolations` (`bool`, optional, Default `true`): Ob offene Linter-Verstöße auf der Zieldatei/dem Symbol ausgegeben werden sollen.
  * `maxCallers` (`int`, optional, Default `10`, Cap `50`): Obergrenze für gelistete Aufrufer.

---

## 4. Beispiel-Ausgabe

**Aufruf:** `get_feature_context(symbol="AiNetLinter.Output.PathNormalizer.IsTestFile")`

```markdown
# Feature-Kontext: AiNetLinter.Output.PathNormalizer.IsTestFile

## 1. Symbol & Deklaration
- **Art:** Methode (public static bool)
- **Datei:** src/AiNetLinter/Output/PathNormalizer.cs:34-46 (13 Zeilen)
- **Container:** Klasse PathNormalizer (static class, 73 Zeilen, AI-Footprint: 120 Tokens)

## 2. Metriken & Budget (rules.json)
- **Cyclomatic Complexity:** 9 / Limit: 15 (Status: OK, Budget verbleibend: 6)
- **Cognitive Complexity:** 4 / Limit: 15 (Status: OK)
- **Parameter:** 1 / Limit: 5 (Status: OK)
- **Method LOC:** 13 / Limit: 42 (Status: OK)

## 3. Direkte Aufrufer (Callers, 4 Fundstellen)
- ViolationMarkdownFormatter.cs:81 — `FormatViolationTable(...)`
- ViolationMarkdownFormatter.cs:211 — `GroupViolationsByFile(...)`
- LinterAnalyzer.cs:30 — `Analyze(...)`
- McpCodeGraphServerRefresh.cs:265 — `SelectPrimaryProject(...)`

## 4. Test-Abdeckung (2 Testdateien, 8 Tests)
- `src/AiNetLinter.FastTests/Output/PathNormalizerTests.cs` (Unit, 6 Facts)
  - `IsTestFile_RecognizesFastTestsDirectory()`
  - `IsTestFile_RecognizesIntegrationTestsDirectory()`
  - `IsTestFile_RecognizesTestSuffix()`
- `src/AiNetLinter.FastTests/Output/ViolationMarkdownFormatterTests.cs` (Component, 2 Facts)

## 5. Offene Violations auf dieser Datei (0 Verstöße)
- Keine Linter-Verstöße auf `src/AiNetLinter/Output/PathNormalizer.cs`.

[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.
```

---

## 5. Technische Architektur & Composite-Muster

Das Tool implementiert das **Composite-Muster** über bestehende interne Engines und Resolver (kein Redundant-Code):

```text
                           ┌────────────────────────────────────────┐
                           │      get_feature_context (Facade)      │
                           └──────────────────┬─────────────────────┘
                                              │
         ┌──────────────────┬─────────────────┼──────────────────┬──────────────────┐
         ▼                  ▼                 ▼                  ▼                  ▼
┌──────────────────┐ ┌─────────────┐ ┌──────────────────┐ ┌─────────────┐ ┌──────────────────┐
│  SymbolResolver  │ │ MetricsCore │ │ ReferenceFinder  │ │ TestCoverage│ │  LinterAnalyzer  │
│ (FindReferences) │ │(Complexity/ │ │ (Callers/Direct) │ │  Resolver   │ │  (Violations)    │
│                  │ │  Footprint) │ │                  │ │             │ │                  │
└──────────────────┘ └─────────────┘ └──────────────────┘ └─────────────┘ └──────────────────┘
```

1. **Symbol-Auflösung:** Über die bewährte `FindReferencesTool.ResolveSymbolAsync`-Logik.
2. **Metriken:** Direkter Aufruf von `ComplexityCalculator` und `AIContextFootprintCalculator` (Shared Core von `metrics_lookup`).
3. **Callers:** Direkte Aufrufer-Sammlung über `FindReferencesCore` (ohne transitive Tiefe, nur direkte Inbound-Calls).
4. **Tests:** Direkte Zuordnung über `TestCoverageResolver` / `TestCoverageIndex` (Shared Core von `get_test_context`).
5. **Violations:** Datei-Analyse über den residenten `LinterEngine` / `LinterAnalyzer`.
6. **StructuredContent:** Liefert ein valides `FeatureContextPayload`-Objekt mit allen 5 Teil-Records für maschinelle Auswertung.

---

## 6. Abhängigkeiten & Umsetzungs-Reihenfolge

`get_feature_context` baut logisch auf den Core-Engines der anderen Werkzeuge auf:
- **Voraussetzung 1:** `04-test-context.md` (`TestCoverageResolver` als saubere Core-Komponente stabilisiert).
- **Voraussetzung 2:** `02-metrics-lookup.md` (`ComplexityCalculator` / `AIContextFootprintCalculator` Schnittstellen vereinheitlicht).

👉 **Reihenfolge:** Umsetzung direkt im Anschluss an `04-test-context` und `02-metrics-lookup`.

---

## 7. Akzeptanzkriterien

1. `get_feature_context` löst Typen und Methoden per Name, `Datei.cs:Zeile` und `DocCommentId` auf.
2. Liefert alle 5 Sektionen (Deklaration, Metriken mit Schwellwert-Check, Callers, Tests, Violations) in einem Call.
3. Jeder Teilbereich ist über boolesche Flags (`includeCallers`, `includeTests`, `includeMetrics`, `includeViolations`) zu- oder abwählbar.
4. `StructuredContent` liefert ein valides, typisiertes JSON-Objekt (`FeatureContextPayload`).
5. Bei unbekanntem Symbol wird ein sauberes `McpToolResults.Recoverable(SYMBOL_NOT_FOUND, ...)` mit Orientierungs-Hinweis geliefert.
6. 15+ Unit-Tests in `AiNetLinter.FastTests` belegen alle Einzelfelder, Filter-Flags und Fehlerfälle.
7. Vollständige Dokumentation in `Docs/configuration.md`, `Docs/integration.md` und `Docs/ROADMAP.md`.
