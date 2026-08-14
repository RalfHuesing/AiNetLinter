# Pre-Audit Findings: find_magic_values (EPIC-1 & EPIC-2)

> **Hinweis zur Verwendung:**  
> Dieses Dokument enthält Beobachtungen, funktionale Schwachstellen und Linter-Regelverstöße aus einer statischen Code-Inspektion und einer automatisierten Analyse über den residierenden MCP-Server `ainetlinter`.  
> Die Punkte sind **keine pauschale Umsetzungs-Pflicht**, sondern dienen als fundierte Prüfgrundlage für den Coder/Kritiker zur Tiefenprüfung, ob und in welchem Umfang sie für das Konzept und die Praxistauglichkeit adressiert werden sollten.

---

## 1. `valueType` Default-Auflösung in `FindMagicValuesTool.cs`

- **Dateipfad:** `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesTool.cs` (ca. Zeile 142)
- **Code-Stelle:**
  ```csharp
  private static ValueTypeResolution ResolveValueType(string raw)
  {
      if (string.IsNullOrWhiteSpace(raw)) return new ValueTypeResolution(MagicValueValueType.String, null);
      ...
  }
  ```
- **Beobachtung & Problem:**  
  Laut Konzept (§"MCP-Tool Schnittstellen-Spezifikation") ist der Standardwert für `valueType` `"all"` (intern `null`). Die aktuelle Implementierung liefert bei leerem oder weggelassenem `valueType`-Parameter stattdessen `MagicValueValueType.String`.
- **Auswirkung:**  
  Wenn ein Client das Tool ohne explizite Angabe von `valueType` aufruft, werden magische Zahlen (`numbers`) standardmäßig ignoriert und nur Strings analysiert.
- **Konkreter Vorschlag zur Prüfung:**  
  Rückgabe bei `string.IsNullOrWhiteSpace(raw)` auf `new ValueTypeResolution(null, null)` (entspricht `"all"`) anpassen, analog zu `ResolveCategory`.

---

## 2. `nameof_candidates`: Erfassung von Deklarations-Bezeichnern in `MagicValuesStringHeuristics.cs`

- **Dateipfad:** `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesStringHeuristics.cs` (ca. Zeilen 152–156)
- **Code-Stelle:**
  ```csharp
  var identifiers = scopeRoot.DescendantNodesAndSelf()
      .OfType<IdentifierNameSyntax>()
      .Select(id => id.Identifier.ValueText)
      .Where(name => !string.IsNullOrEmpty(name) && string.Equals(name, literalText, StringComparison.Ordinal))
      .ToList();
  ```
- **Beobachtung & Problem:**  
  In Roslyn sind Deklarations-Bezeichner (`ParameterSyntax.Identifier`, `VariableDeclaratorSyntax.Identifier`, `PropertyDeclarationSyntax.Identifier` etc.) `SyntaxToken`s und **keine** `IdentifierNameSyntax`-Knoten. `IdentifierNameSyntax` tritt nur auf, wenn ein Bezeichner als Ausdruck verwendet wird.
- **Auswirkung:**  
  Der typische Fall `void M(string foo) { throw new ArgumentNullException("foo"); }`, bei dem `foo` nur deklariert ist, aber im Methodenrumpf nicht weiter als Ausdruck vorkommt, liefert in `OfType<IdentifierNameSyntax>()` keinen Treffer. `"foo"` wird daher nicht als `nameof_candidates` erkannt.
- **Konkreter Vorschlag zur Prüfung:**  
  Zusätzlich zu `IdentifierNameSyntax` auch Deklarations-Knoten im `scopeRoot` abfragen, z. B.:
  - `scopeRoot.DescendantNodesAndSelf().OfType<ParameterSyntax>().Select(p => p.Identifier.ValueText)`
  - `scopeRoot.DescendantNodesAndSelf().OfType<VariableDeclaratorSyntax>().Select(v => v.Identifier.ValueText)`
  - Ggf. Member- und Property-Deklarationen der umschließenden Klasse.

---

## 3. Hardcodierter Typ `Number` bei duplizierten `const`-Feldern

- **Dateipfad:** `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScanner.cs` (ca. Zeile 434–437)
- **Code-Stelle:**
  ```csharp
  sink.Add(new RawMagicValue(
      entry.FilePath, entry.Line, entry.Column,
      MagicValueValueType.Number, key.Value, classification));
  ```
- **Beobachtung & Problem:**  
  In `DetectDuplicateConstFieldsAsync` wird für jedes gefundene duplizierte `const`-Feld fest `MagicValueValueType.Number` in den Sink übergeben, unabhängig vom eigentlichen Typ der Konstante.
- **Auswirkung:**  
  Duplizierte String-Konstanten (z. B. `private const string DefaultRole = "Admin";`) werden mit `valueType: "number"` klassifiziert, was die Metadaten im `StructuredContent` verfälscht und in `FormatReport` dazu führt, dass der Wert unquoted ausgegeben wird.
- **Konkreter Vorschlag zur Prüfung:**  
  Bestimmung des `ValueType` dynamisch anhand des Literals oder Typs durchführen, z. B.:
  ```csharp
  var valueType = string.Equals(key.Type, "string", StringComparison.OrdinalIgnoreCase)
      ? MagicValueValueType.String
      : MagicValueValueType.Number;
  ```

---

## 4. Trivia-Attachment bei Suppression-Kommentaren (`// ainetlinter-disable MagicValues`)

- **Dateipfad:** `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (ca. Zeilen 302–328)
- **Code-Stelle:**
  ```csharp
  private static bool HasDisableComment(LiteralExpressionSyntax literal)
  {
      ...
  }
  ```
- **Beobachtung & Problem:**  
  In Roslyn haften führende Zeilenkommentare (über der Zeile) am ersten Token der gesamten Anweisung/Deklaration (`const`, `var`, `string`), und Trailing-Kommentare am Zeilenende haften am Semikolon `;` des Statements – nicht am inneren `LiteralExpressionSyntax`-Knoten.
- **Auswirkung:**  
  Übliche Kommentare wie:
  ```csharp
  // ainetlinter-disable MagicValues
  const string ApiUrl = "https://api.example.com";
  ```
  oder
  ```csharp
  const string ApiUrl = "https://api.example.com"; // ainetlinter-disable MagicValues
  ```
  werden bei alleiniger Prüfung von `literal.GetLeadingTrivia()` nicht erkannt. Nur Inline-Kommentare direkt vor/nach dem Literal (`/* ainetlinter-disable MagicValues */ "https://..."`) greifen.
- **Konkreter Vorschlag zur Prüfung:**  
  Die Trivia-Prüfung um den umschließenden Statement- bzw. Deklarationsknoten erweitern (siehe auch Punkt 6.1 zur Komplexitätsreduktion).

---

## 5. Freistehende synthetische Literale bei interpolierten Strings

- **Dateipfad:** `src/AiNetLinter/Mcp/Tools/MagicValues/FindMagicValuesScannerWalker.cs` (ca. Zeilen 274–277)
- **Code-Stelle:**
  ```csharp
  var synthetic = SyntaxFactory.LiteralExpression(
      SyntaxKind.StringLiteralExpression,
      SyntaxFactory.Literal(textValue, textValue));
  ProcessLiteral(synthetic, node.GetLocation());
  ```
- **Beobachtung & Problem:**  
  Der synthetisch erzeugte `LiteralExpressionSyntax`-Knoten besitzt kein Parent (`Parent == null`), da er nicht Teil des geparsten Syntax-Baums ist.
- **Auswirkung:**  
  Alle Heuristiken oder Filter, die im AST nach oben navigieren (z. B. `ResolveSurroundingName` für `security_candidates`, `FirstAncestorOrSelf<AttributeSyntax>()`, oder `HasDisableComment`), liefern bei synthetischen String-Segmenten `null` bzw. greifen nicht.
- **Konkreter Vorschlag zur Prüfung:**  
  Prüfen, ob für statische Fragmente interpolierter Strings der übergeordnete `InterpolatedStringExpressionSyntax`-Knoten als Kontext übergeben werden sollte, um zumindest Symbol- und Unterdrückungskontext auswerten zu können.

---

## 6. Linter- & Architektur-Befunde (ermittelt via MCP-Server `ainetlinter`)

Die Analyse über die residierenden Linter-Tools (`get_violations`, `get_hotspots`, `safeguard`) hat folgende konkrete Regelverletzungen und Risiken im aktuellen Codebestand aufgezeigt:

### 6.1 `MaxCognitiveComplexity` in `MagicValuesClassifier.cs`
- **Fundstelle:** `src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs` (Zeile ~301)
- **Regelverstoß:** Die Methode `HasDisableComment` hat eine Kognitive Komplexität von **16** (erlaubt sind maximal **15**).
- **Ursache:** Verschachtelte Schleifen (`for current = literal.Parent` + `foreach trivia`) bei der Vorfahren-Prüfung.
- **Konkreter Vorschlag:** Auslagerung der Vorfahren-Schleife in eine separate Hilfsmethode (z. B. `HasAncestorDisableComment(literal, marker)`), um die Komplexität der Hauptmethode unter 15 zu senken.

### 6.2 `MaxBoolParameterCount` in `FindMagicValuesTestHelpers.cs`
- **Fundstelle:** `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesTestHelpers.cs` (Zeilen ~27 & ~51)
- **Regelverstoß:** Die Hilfsmethode `RunAsync` deklariert **3 bool-Parameter** (`includeTests`, `includeSuppressed`, `changedOnly`). Erlaubt ist laut Projektregel maximal **1 bool-Parameter**.
- **Ursache:** Bool-Parameter sind an der Aufrufstelle opak (z. B. `RunAsync(source, false, false, true)`).
- **Konkreter Vorschlag:** Nutzung des bereits bestehenden Records `ScanAsyncParams` oder eines Options-Parameters anstelle einzelner bools in den Helper-Überladungen.

### 6.3 `DetectAndBanPhantomDependencies` in `FindMagicValuesTestHelpers.cs`
- **Fundstelle:** `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesTestHelpers.cs` (Zeile 9)
- **Regelverstoß:** Der importierte Namespace `using AiNetLinter.TestKit;` kann im Kontext des Testprojekts nicht aufgelöst werden (Phantom Dependency).
- **Konkreter Vorschlag:** Unbenutztes `using` entfernen bzw. Projektreferenz prüfen.

### 6.4 Hotspot-Warnung / `MaxLineCount`-Risiko in `FindMagicValuesScannerTests.cs`
- **Fundstelle:** `src/AiNetLinter.FastTests/Mcp/Tools/FindMagicValuesScannerTests.cs`
- **Metrik:** Die Datei steht bei **486 Zeilen (97 % des 500-Zeilen-Limits)**.
- **Auswirkung:** Werden weitere Unit-Tests in diese Datei eingefügt, bricht der Build mit `MaxLineCount > 500` ab (`TreatWarningsAsErrors=true`).
- **Konkreter Vorschlag:** Alle neuen Tests für EPIC-2 konsequent in `FindMagicValuesScannerHeuristicTests.cs` oder eine neue `FindMagicValuesScannerArgTests.cs` auslagern.

### 6.5 `MaxPublicMembersPerType` in Test-Klassen
- **Fundstellen:**  
  - `FindMagicValuesScannerTests.cs` (21 öffentliche Member, erlaubt: 15)  
  - `FindMagicValuesScannerHeuristicTests.cs` (19 öffentliche Member, erlaubt: 15)
- **Konkreter Vorschlag:** Bei weiterem Anwachsen der Testsuite die Test-Klassen nach Fachbereichen (z. B. Filter-Tests vs. Heuristik-Tests) aufteilen.
