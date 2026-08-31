# Audit-Report 04: Source Project Semantic Tools & Code Comprehension

**SubAgent:** SubAgent 4 (Source Comprehension)  
**Status:** Abgeschlossen  
**Prüfdatum:** 2026-08-31  
**Geprüfte Tools:** `get_feature_context`, `get_file_skeleton`, `get_symbol_body`, `get_class_structure`, `get_type_hierarchy`, `dependency_graph`, `get_test_context`, `get_impact`, `search_pattern`  
**Test-Ziel:** Source-Projekt `AiNetLinter` (`targetType="project"`)

---

## 1. Getestete Szenarien & Ergebnisse

### 1.1 `get_feature_context` (Composite Tool)
- **Funktion:** Ruft für ein beliebiges Symbol (z. B. `AnalysisTargetResolver`) in einem einzigen residenten Turn 5 Dimensionen ab:
  1. Deklaration (Dateipfad, Zeilenbereich, DocCommentId, Sichtbarkeit).
  2. Metriken & Budget (Type LOC, AI-Context-Footprint, Public Members inkl. Soll-/Ist-Vergleich und Restbudget).
  3. Direkte Aufrufer mit Projekt- und Zeilennachweis.
  4. Statisch zugeordnete Test-Dateien und konkrete Test-Methoden.
  5. Offene Linter-Violations auf der Datei.
- **Bewertung:** **Herausragend.** Spart einem Coding-Agenten 3-4 separate Tool-Turns und liefert auf ~400 Tokens den perfekten Arbeitskontext für anstehende Modifikationen oder Refactorings.

### 1.2 `get_file_skeleton` & `get_symbol_body`
- `get_file_skeleton` liefert einen kompakten strukturellen Überblick über eine Datei ohne Implementierungsrümpfe. Jedes Member erhält eine eindeutige `id:M:...` DocCommentId.
- `get_symbol_body` löst diese DocCommentId in einem Folgeaufruf direkt auf und liefert den exakten Quellcode des Methodenrumpfs.

### 1.3 `dependency_graph`
- Liefert semantische (Roslyn SemanticModel-basierte) eingehende und ausgehende Abhängigkeiten einer Datei oder eines Typs.
- Listet beteiligte Typen und Dateien sauber getrennt auf.

### 1.4 `get_test_context`
- Findet zu einer Produktionsklasse (z. B. `AnalysisTargetResolver`) automatisch die zugeordneten Unit- und Integrationstests (z. B. `AnalysisTargetResolverTests.cs` mit allen 6 Testmethoden).
- Liefert direkt einen kopierbaren `dotnet test`-Befehl mit passendem `--filter`.

### 1.5 `get_impact`
- Ermittelt projektübergreifend alle Aufrufstellen und betroffenen Komponenten bei Änderung eines Symbols.

### 1.6 `search_pattern`
- Zuverlässige Textsuche für C#- und Nicht-C#-Dateien mit Zeilenbezug und opt-in semantischer Anreicherung (`enrichCSharp=true`).

---

## 2. Befunde & Beobachtungen

### Befund SRC-001 (S3 / U0 / P3): Inkonsistente Primär-Parameterbenennung (`symbol` vs `symbolIdentifier`)
- **Beschreibung:** `get_feature_context` und `get_test_context` akzeptieren sowohl `symbol` als auch `symbolIdentifier`. Andere Tools (`find_references`, `get_call_tree`, `get_class_structure`, `get_type_hierarchy`, `dependency_graph`) erwarten primär `symbolIdentifier`.
- **Empfehlung:** Standardisierung der Dokumentationsempfehlung auf `symbolIdentifier` für alle semantischen Werkzeuge (unter Beibehaltung der toleranten Alias-Akzeptanz im Server).
- **Klassifizierung:** Schweregrad `S3` (Minor DX), Umfang `U0` (Lokal), Dringlichkeit `P3`.

---

## 3. Fazit SubAgent 4
Die semantischen Quellcode-Werkzeuge für C# sind hochentwickelt, präzise und extrem agentenfreundlich. Insbesondere `get_feature_context` und `get_test_context` minimieren Reibung und Kontextverlust im Agenten-Workflow drastisch.
