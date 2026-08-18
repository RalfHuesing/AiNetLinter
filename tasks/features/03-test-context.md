# Konzept: `get_test_context` (Test-Coverage-Awareness)

## 1. Problemstellung & Motivation
Wenn ein Coding-Agent Code refaktorieren oder ein Feature anpassen soll, muss er wissen:
- Welche Tests sichern diese Klasse oder Methode aktuell ab?
- Wo liegen die Test-Dateien für diesen Produktions-Code?
- Welche Test-Kategorien (Unit, Component, Integration) sind vorhanden?

Bisher muss der Agent Test-Dateien per Dateinamensmuster oder `find_references` manuell zusammensuchen.

## 2. Zielsetzung
Ein MCP-Tool `get_test_context`, das auf Basis der im AiNetLinter-Core bereits vorhandenen Komponenten (`TestCoverageResolver`, `TestCoverageCollector`, `TestCoverageIndex`) sofort alle relevanten Test-Dateien und Test-Methoden für ein gegebenes Produktions-Symbol auflistet.

## 3. Technische Basis im Projekt
Im AiNetLinter-Code existieren bereits:
- `AiNetLinter.Core.TestCoverageResolver`: Löst Test-Referenzen über `typeof(...)`-Ausdrücke, Namenskonventionen und XML-Doc-Kommentare (`/// covers ...`) auf.
- `AiNetLinter.Core.TestCoverageIndex`: Schneller In-Memory-Index über Test-Projekte und Zuordnungen.
- `AiNetLinter.Core.TestCoverageCollector`: Durchsucht Test-Klassen nach Test-Attributen (`[Fact]`, `[Theory]`, `[Test]`, etc.).

## 4. Werkzeug-Spezifikation

* **Tool-Name:** `get_test_context`
* **Registrierung:** In `AnalysisToolRegistrations.cs` (oder `FileStructureToolRegistrations.cs`)
* **Parameter:**
  * `symbol` (string, required): Klassenname, Methodenname oder Pfad (`Namespace.Klasse` oder `Datei.cs:Zeile`).
  * `maxResults` (int, optional, Default 30): Maximale Anzahl Test-Referenzen.
* **Rückgabe (Text & StructuredContent):**
  * Liste der abdeckenden Test-Dateien mit Test-Methoden.
  * Auflösungsgrund (z. B. `Direct Typeof-Reference`, `Naming Convention Match`, `Explicit Covers Comment`).
  * Hinweis, ob überhaupt Tests gefunden wurden (oder ob die Klasse ungetestet erscheint).

## 5. Akzeptanzkriterien
1. Nutzt die bestehende `TestCoverageResolver`-Logik ohne Duplikation.
2. Findet Unit- und Integrationstests anhand von `typeof()`, Namensübereinstimmungen und Attributen.
3. Liefert `StructuredContent` als sauberes JSON-Objekt (`{ targetSymbol, testFiles: [...], totalTestsCount }`).
4. Unit-Tests in `AiNetLinter.FastTests` belegen die korrekte Zuordnung.
