# Konzept: `find_scattered_switches` / `AvoidScatteredEnumSwitches` (Erkennung semantischer Typ-4-Duplikation)

## 1. Problemstellung & Motivation
Klassische Code-Clone-Detection (wie Token-N-Gramme in `find_duplicates`) erkennt rein syntaktische Duplikate (Typ 1 bis 3). Sie versagt jedoch bei **semantischer Duplikation (Typ 4 / Konzeptioneller Drift)**:
* Verschiedene Agenten oder Entwickler implementieren unabhängig voneinander Hilfsfunktionen, die denselben Enumerations- oder Klassentyp transformieren.
* Da die Methoden unterschiedliche Namen, leicht abweichende Rückgabewerte (z. B. `"class"` vs. `"Klasse"`, `"record class"` vs. `"record"`) oder unterschiedliche Formatierungsstile nutzen, unterscheiden sich AST und Token-Sequenzen stark.

### Reales Fallbeispiel (Session 2026-08-19, Task 01-namespace-tree):
In der Codebase entstanden 4 unabhängige Methoden für dasselbe logische Mapping von Roslyn-`TypeKind` auf Strings:
1. `GetClassStructureTool.GetTypeKindDescription(INamedTypeSymbol)`
2. `GetNamespaceTreeScanner.DescribeTypeKind(INamedTypeSymbol)`
3. `FindSymbolTool.DescribeKind(ISymbol)`
4. `DeadCodeFilters.GetNamedTypeKindString(TypeKind)`

Zusätzlich entstanden 2 unabhängige Methoden für `Accessibility`:
1. `SymbolVisibilityResolver.ResolveVisibility(ISymbol)`
2. `DeadCodeFilters.GetAccessibilityString(Accessibility)`

👉 **Muster:** Wenn an mehreren Stellen im Code ein `switch` oder `switch expression` über denselben Enum- oder Interface-Typ ausgeführt wird, ist das ein fast 100%iger Indikator für fehlende Kapselung und DRY-Drift.

---

## 2. Zielsetzung
Automatisierte Erkennung von verstreuten Fallunterscheidungen über denselben Ziel-Typen mittels Roslyn-Syntax- und Semantik-Analyse.

Das Feature dient zwei Zwecken:
1. **MCP-Tool / Audit-Modus (`find_scattered_switches`):** On-Demand-Audit zur Identifikation von Kandidaten für zentrale Resolver/Classifier.
2. **Roslyn-Linter-Regel (`AvoidScatteredEnumSwitches`):** Architekturelles Quality-Gate, das verhindert, dass neue lokale Switch-Kaskaden über bekannte Domain-Enums entstehen.

---

## 3. Methodischer Ansatz (Roslyn-Semantik)

1. **Syntax-Erkennung:**
   * Sammeln aller `SwitchExpressionSyntax` und `SwitchStatementSyntax` in allen `.cs`-Dateien der Solution.
2. **Semantische Typauflösung via `SemanticModel`:**
   * Ermitteln des exakten `ITypeSymbol` des Switch-Governors (`switch (governor)`).
   * Filter auf Enum-Typen (`TypeKind.Enum`), Flags-Enums und versiegelte Typ-Hierarchien (z. B. `Microsoft.CodeAnalysis.TypeKind`, `Accessibility`, `SymbolKind`, eigene Domain-Enums).
3. **Clustering nach Ziel-Typ:**
   * Gruppierung aller Fundstellen nach `ITypeSymbol.ToDisplayString()`.
   * Schwellenwert: Wenn mehr als $N$ (Standard: 2) Methoden auf denselben Typen switchen, wird ein Cluster gemeldet.
4. **Filterung & Whitelist:**
   * Trivial-Switches (z. B. Standard-Enums wie `System.DayOfWeek`, `System.IO.FileMode`) oder Switches innerhalb derselben deklarierenden Klasse können ignoriert werden.
   * Ausschluss von Test-Dateien (sofern nicht explizit aktiviert).

---

## 4. Werkzeug-Spezifikation (MCP-Tool)

* **Tool-Name:** `find_scattered_switches` (oder Modus in `find_duplicates(mode="scattered-switches")`)
* **Parameter:**
  * `scopeFilter` (string, optional): Projektname oder Pfad-Substring.
  * `minOccurrences` (int, optional, Default 2): Mindestanzahl von Methoden, die auf denselben Typen switchen müssen.
  * `includeTests` (bool, optional, Default false): Ob Switches in Test-Dateien einbezogen werden.
  * `maxResults` (int, optional, Default 20): Maximale Anzahl von Clustern.
* **Output-Beispiel:**
```text
3 verstreute Switch-Cluster gefunden:

1. Microsoft.CodeAnalysis.TypeKind (4 Vorkommen in 4 Dateien)
   - src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:144 (GetTypeKindDescription)
   - src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:411 (DescribeTypeKind)
   - src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs:129 (DescribeKind)
   - src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeFilters.cs:77 (GetNamedTypeKindString)
   Empfehlung: Zentralisieren in einen gemeinsamen 'SymbolKindClassifier' / 'TypeKindFormatter'.

2. Microsoft.CodeAnalysis.Accessibility (2 Vorkommen in 2 Dateien)
   - src/AiNetLinter/Mcp/Tools/FileStructure/SymbolVisibilityResolver.cs:12 (ResolveVisibility)
   - src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeFilters.cs:90 (GetAccessibilityString)
   Empfehlung: Zentralisieren in 'SymbolVisibilityResolver'.
```

---

## 5. Linter-Regel-Spezifikation (`AvoidScatteredEnumSwitches`)

* **Regel-ID:** `AvoidScatteredEnumSwitches`
* **Kategorie:** Architecture / DRY
* **Severity:** `warning`
* **Konfiguration in `rules.json`:**
```json
{
  "AvoidScatteredEnumSwitches": {
    "Enabled": true,
    "MaxAllowedSwitchesPerType": 2,
    "ExcludedTypes": [
      "System.DayOfWeek",
      "System.IO.FileAccess"
    ],
    "ExcludedProjects": [
      "*.Tests",
      "*.IntegrationTests"
    ]
  }
}
```

---

## 6. Akzeptanzkriterien
1. Erkennt alle verstreuten `switch`-Ausdrücke und `switch`-Statements auf demselben Enum- oder Typ-Symbol über mehrere Klassen hinweg.
2. Unterscheidet sauber zwischen Domain-/Framework-Typen und trivialen Standard-Library-Typen.
3. Funktioniert rein resident auf dem Roslyn-`Compilation`-Zustand ohne Re-Parsing.
4. Bietet sowohl ein MCP-Audit-Tool als auch eine konfigurierbare Linter-Regel.
