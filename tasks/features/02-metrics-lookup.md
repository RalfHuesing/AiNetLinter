# Konzept: `metrics_lookup` (One-Shot-Metriken & AI-Context-Footprint)

## 1. Problemstellung & Motivation
Wenn ein Agent (z. B. im Rahmen eines Refactoring-Tasks oder vor dem Hinzufügen von Logik) ein bestimmtes Symbol analysiert, muss er wissen:
- Wie komplex ist diese Methode / Klasse?
- Droht die Methode das Zeilen- oder Komplexitätslimit zu reißen?
- Wie hoch ist der aktuelle AI-Context-Footprint?

Bisher muss der Agent entweder die Datei ganz einlesen (Token-intensiv) oder `metrics_tree` bemühen, was für ein einzelnes Symbol umständlich ist.

## 2. Zielsetzung
Ein schnelles, leichtgewichtiges MCP-Tool `metrics_lookup`, das für ein gezieltes Symbol (Klasse, Record, Interface, Methode, Property) alle relevanten Metriken in einem einzigen Call zurückliefert.

## 3. Werkzeug-Spezifikation

* **Tool-Name:** `metrics_lookup`
* **Registrierung:** In `AnalysisToolRegistrations.cs` (oder eigene Registration)
* **Parameter:**
  * `symbol` (string, required): Format wie bei `find_references` / `get_symbol_body` (`Namespace.Klasse.Methode`, `Datei.cs:Zeile` oder `DocCommentId`).
* **Rückgabe (Text & StructuredContent):**
  * **Bei Methoden/Membern:**
    * Zeilenanzahl (LOC / StartLine / EndLine)
    * Zyklomatische Komplexität (`CyclomaticComplexity`)
    * Kognitive Komplexität (`CognitiveComplexity`)
    * Parameter-Anzahl (`ParameterCount`)
    * Schwellwert-Vergleich mit der aktiven `rules.json` (Status: OK / Warnung / Verletzung)
  * **Bei Typen (Klasse/Record/Struct):**
    * Zeilenanzahl des Typs
    * `AIContextFootprint` (berechnet über `AIContextFootprintCalculator`)
    * Anzahl Public Members / Total Members
    * Schwellwert-Vergleich mit der aktiven `rules.json`

## 4. Technische Umsetzung (Bestehende Komponenten)
* `AiNetLinter.Metrics.ComplexityCalculator`: Berechnet bereits CC und CogC auf MethodDeclarationSyntax.
* `AiNetLinter.Metrics.AIContextFootprintCalculator`: Berechnet bereits den AI-Context-Footprint.
* `FindReferencesTool.ResolveSymbolAsync` / Symbol-Resolution-Logik: Kann zur Auflösung des Identifiers wiederverwendet werden.

## 5. Akzeptanzkriterien
1. `metrics_lookup` löst Symbole per Name, File:Line und DocCommentId auf.
2. Liefert bei Methoden präzise CC, CogC, LOC und ParamCount.
3. Liefert bei Klassen den berechneten AIContextFootprint und Member-Counts.
4. StructuredContent ist ein valides JSON-Objekt mit klaren Feldtypen.
5. Unit-Tests in `AiNetLinter.FastTests` decken alle Symbol-Arten und Fehlerfälle ab.
