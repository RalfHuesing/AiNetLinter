# Konzept: Semantische DRY- & Drift-Erkennung via AST-Fingerprinting (`find_duplicates` Erweiterung)

## 1. Problemstellung & Motivation
Die bisherige Duplicate-Detection (`find_duplicates`) arbeitet mit zwei Modi:
1. `mode="clone"`: Token-N-Gramme (Jaccard-Ähnlichkeit) für syntaktische Klone (Typ 1–3).
2. `mode="refactoring-drift"`: Prüfung, ob ein *bereits bekannter* `helperSymbol` nicht aufgerufen wird.

👉 **Die Lücke:** **Semantische Duplikation (Typ 4 / Intended Duplication)**.
Agenten bauen für neue Features lokale Hilfsfunktionen, die denselben Zweck erfüllen wie bereits existierende Funktionen in anderen Modulen. Da Identifier-Namen, String-Literale und exakte AST-Zweige variieren, scheitert der Token-Vergleich.

### Reales Fallbeispiel (Session 2026-08-19):
Vier Methoden mit identischer Absicht (*„Mappe Roslyn-Typ/Kind auf String“*), aber unterschiedlicher Syntax:
* `GetClassStructureTool.GetTypeKindDescription(INamedTypeSymbol) -> string`
* `GetNamespaceTreeScanner.DescribeTypeKind(INamedTypeSymbol) -> string`
* `FindSymbolTool.DescribeKind(ISymbol) -> string`
* `DeadCodeFilters.GetNamedTypeKindString(TypeKind) -> string`

Da Methoden in `AiNetLinter` durch `MaxMethodLineCount ≤ 42` und `MaxCyclomaticComplexity ≤ 12` klein und flach sind, lässt sich der Kontrollfluss und die Typ-Interaktion jeder Methode in einen **kompakten mathematischen Merkmalsvektor** übersetzen.

---

## 2. Architektur-Entscheidung: Konsolidierung in `find_duplicates`

Statt ein neues Tool einzuführen (was das MCP-Tool-Budget des Agenten unnötig vergrößert), erweitern wir das existierende `find_duplicates`-Tool um einen neuen Modus:

| Modus in `find_duplicates` | Analysierte Ebene | Was wird gefunden? |
|---|---|---|
| `mode="clone"` *(Default)* | Syntaktische Token-Ebene (N-Gramme) | Copy-Paste-Code, formatgleiche Blöcke (Typ 1–3). |
| `mode="structural"` *(Neu)* | Semantische AST- & Typ-Ebene (Feature-Vektoren) | Parallele Hilfsfunktionen mit gleicher Intention & Signatur (Typ 4). |
| `mode="refactoring-drift"` | Call-Graph & Strukturvergleich | Nicht-Nutzung eines explizit vorgegebenen `helperSymbol`. |

---

## 3. Merkmals-Extraktion (Der strukturelle Feature-Vektor)

Jede Methode in der Solution wird durch einen leichten Roslyn-Syntax/Semantik-Walk in ein **Struktur-Profil** zerlegt:

### Die 4 Merkmals-Dimensionen:
1. **Signatur-Profil (Input $\rightarrow$ Output):**
   * Exakte Parameter-Typen (z. B. `[INamedTypeSymbol]`) und Rückgabetyp (z. B. `string`, `bool`).
2. **Kontrollfluss-Skelett (CFG-Shape):**
   * Geordnete Folge von Kontrollstrukturen (z. B. `[SwitchExpression, Return]` oder `[IfGuard, Loop, Return]`).
3. **Ziel-Typ-Interaktionen (Target Types):**
   * Typen, auf denen geschaltet oder deren Properties gelesen werden (z. B. `Microsoft.CodeAnalysis.TypeKind`, `Accessibility`).
4. **Operationale Charakteristik (Behavioral Markers):**
   * `HasLiterals: true` (und Anzahl-Klasse: ~5 Literale)
   * `IsPureFunction: true` (kein Instanz-State, keine Mutationen, kein I/O)
   * `ReturnsLiteralOrParameter: true`

---

## 4. Mathematische Umsetzungs-Varianten (Praxis-Vergleich)

### Variante A: Sparse Feature Cosine Similarity (Empfohlen für Phase 1)
* Jede Eigenschaft ist ein Merkmal in einer Sparse-Map: `Dictionary<string, double>`.
  * Merkmale: `In:INamedTypeSymbol: 1.0`, `Out:String: 1.0`, `Switch:TypeKind: 1.0`, `CFG:SwitchReturn: 1.0`, `Literals:High: 1.0`.
* **Ähnlichkeitsberechnung:**
  $$\text{Cosine}(A, B) = \frac{A \cdot B}{\|A\| \|B\|}$$
* **Vorteil:** Extrem transparent, intuitiv debuggbar, keine False-Positives bei unterschiedlichen Rückgabetypen.
* **Performance:** Für 2.500 Methoden in < 150 ms im RAM berechenbar.

### Variante B: SimHash / Locality-Sensitive Hashing (Alternative für sehr große Repositories)
* Merkmale werden über 64-Bit-Hashes gewichtet und zu einem 64-Bit-Fingerprint komprimiert.
* Methoden mit ähnlicher Struktur haben eine **Hamming-Distanz $\le 3$ Bits**.
* **Vorteil:** Suchzeit $O(N \log N)$ über sortierte Hash-Listen (unter 10 ms).
* **Nachteil:** Schwellenwerte sind etwas schwerer zu kalibrieren als direkte Prozentwerte (z. B. 0.85).

---

## 5. Werkzeug-Spezifikation (`find_duplicates` mit `mode="structural"`)

### Parameter
* `mode` (string, optional): `"clone"` (Default), `"structural"`, `"refactoring-drift"`.
* `similarityThreshold` (string, optional, Default `"near"`):
  * `exact` ($\ge 0.95$): Fast identische Signatur, identischer Kontrollfluss & Ziel-Typ.
  * `near` ($\ge 0.80$): Gleiche Absicht, leichte Variation in Parametern oder Rückgabetyp.
* `scopeFilter` (string, optional): Projektname oder Pfad-Substring.
* `scopeType` (string, optional, Default `"all"`): `"all"`, `"production"`, `"tests"`.
* `minTokens` / `minStatements` (int, optional, Default 3 Statements): Filtert 1-Zeiler-Getter/Forwarder heraus.
* `maxResults` (int, optional, Default 20).

### Output-Format
```text
3 semantische Struktur-Cluster gefunden (2.741 Methoden gescannt):

## 1. near (Score 0,93, 3 Methoden)
Profil: [INamedTypeSymbol/ISymbol] -> string | SwitchExpression(TypeKind) | 4-6 String-Literale
- GetTypeKindDescription(INamedTypeSymbol) (src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:144)
- DescribeTypeKind(INamedTypeSymbol) (src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeScanner.cs:411)
- DescribeKind(ISymbol) (src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs:129)
Empfehlung: Prüfen, ob diese Methoden in einem zentralen 'SymbolKindClassifier' konsolidiert werden können.

## 2. exact (Score 1,00, 2 Methoden)
Profil: [Accessibility] -> string | SwitchExpression(Accessibility)
- ResolveVisibility(Accessibility) (src/AiNetLinter/Mcp/Tools/FileStructure/SymbolVisibilityResolver.cs:12)
- GetAccessibilityString(Accessibility) (src/AiNetLinter/Mcp/Tools/DeadCode/DeadCodeFilters.cs:90)
Empfehlung: 'GetAccessibilityString' auf 'SymbolVisibilityResolver.ResolveVisibility' umstellen.
```

---

## 6. Warum das in unserer Codebase besonders gut funktioniert

1. **Flache Methoden:** Durch `MaxMethodLineCount ≤ 42` haben Methoden fast keine verschachtelten „Wand-aus-Code“-Effekte, die Feature-Vektoren verrauschen würden.
2. **Roslyn SemanticModel bereits im RAM:** Wir müssen keine ASTs neu parsen. Typ-Informationen liegen resident vor.
3. **Zwei-Stufen-Prinzip (Filter ➔ Agent):**
   * Der Algorithmus liefert keine unumstößlichen Fehler, sondern **hochwertige Verdachtsfälle** (Signal-to-Noise Ratio > 80%).
   * Der Agent (oder Entwickler) im Drift-Audit liest nur die Top 3 Cluster und entscheidet in Sekunden.

---

## 7. Umsetzungs-Schritte
1. **`MethodStructuralProfileExtractor.cs` in `Core/DuplicateDetection/`:**
   * Extrahiert Parameter-, Rückgabetyp-, Switch- und Kontrollfluss-Merkmale pro `MethodDeclarationSyntax`.
2. **`StructuralSimilarityEngine.cs`:**
   * Berechnet Cosine-Similarity über normalisierte Sparse-Vektoren.
3. **Integration in `DuplicateDetectionEngine.cs`:**
   * Verzweigung nach `mode == "structural"`.
4. **FastTests:**
   * Unit-Tests mit synthetischen Zwillings-Methoden (gleiche Signatur & Switch, unterschiedliche Namen/Strings).
