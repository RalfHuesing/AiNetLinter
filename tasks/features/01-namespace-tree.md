# Konzept: `get_namespace_tree` (Hierarchische Code-Exploration & Progressive Disclosure)

## 1. Problemstellung & Motivation
Wenn ein Agent eine Codebase erkundet (zu Beginn einer Session oder bei unbekannten Projekten), steht er vor einem Dilemma:
1. `find_symbol` durchsucht die Solution flach und verlangt einen bekannten Namens-Substring (`namePattern`). Sucht man nach allgemeinen Begriffen wie `"Tool"`, `"Service"` oder `"Controller"`, erhält man dutzende Treffer aus allen Projekten und Tests durcheinandergewürfelt.
2. `metrics_tree` zeigt zwar einen Baum, basiert jedoch auf der **physischen Verzeichnisstruktur** und Zeilenzahlen, nicht auf der **logischen C#-Namespace- und Typ-Architektur**.
3. `get_file_skeleton` und `get_class_structure` setzen bereits voraus, dass man die exakte Datei oder den Typnamen kennt.

👉 **Es fehlt die semantische Zoom-Stufe:** Ein strukturierter Drilldown entlang der logischen C#-Hierarchie: **Projekte ➔ Namespaces ➔ Typen**.

## 2. Warum hat dieses Feature den höchsten Hebel (Token-Save & Effizienz)?
- **Progressive Disclosure:** Der Agent muss nicht blind raten oder 50 Treffer filtern. Er zoomt gezielt Ebene für Ebene tiefer.
- **Enorme Token-Ersparnis:** Statt bei jedem Session-Start hunderte Zeilen flache Symbol-Listen zu scannen (~2.000–4.000 Tokens), genügen kompakte hierarchische Abfragen mit ~100–300 Tokens.
- **Workflow-Beschleunigung:** Der Agent findet in 1–2 gezielten Schritten die zuständigen Klassen für ein Feature, ohne Test-Klassen und Hilfsklassen mühsam aussortieren zu müssen.

---

## 3. Tool-Spezifikation

* **Tool-Name:** `get_namespace_tree`
* **Registrierung:** In `FileStructureToolRegistrations.cs`
* **Parameter:**
  * `project` (string, optional, Default `null`): Projektname oder Substring (z. B. `"AiNetLinter"`, `"Core"`). Ohne Angabe werden alle Projekte der Solution gelistet.
  * `namespacePrefix` (string, optional, Default `null`): Namespace-Filter/Startpunkt (z. B. `"AiNetLinter.Mcp"`).
  * `depth` (int, optional, Default `1`, Cap `3`): Wie viele Namespace-Ebenen ab dem Startpunkt traversiert werden.
  * `includeTypes` (bool, optional, Default `true`): Ob die Typen (Klassen, Interfaces etc.) im Ziel-Namespace angezeigt werden oder nur Sub-Namespaces.
  * `kind` (string, optional, Default `"all"`): Filter nach Typ-Art (`class`, `interface`, `record`, `enum`, `all`).
  * `maxResults` (int, optional, Default `50`, Cap `200`): Obergrenze für angezeigte Einträge inkl. Truncation-Meta-Zeile.


---

## 4. Beispiel-Workflows & Ausgaben

### Stufe 1: Solution-Überblick (Welche Projekte gibt es?)
**Aufruf:** `get_namespace_tree()`
```text
# Solution Overview: AiNetLinter.slnx (4 Projekte)

- AiNetLinter (Typ: Exe, 8 Namespaces, 52 Typen)
- AiNetLinter.FastTests (Typ: Test, 14 Namespaces, 78 Typen)
- AiNetLinter.IntegrationTests (Typ: Test, 6 Namespaces, 31 Typen)
- AiNetLinter.TestKit (Typ: Lib, 3 Namespaces, 12 Typen)

Tipp: Nutze get_namespace_tree(project="AiNetLinter") fuer die Namespaces eines Projekts.
```

### Stufe 2: Projekt-Drilldown (Welche Namespaces hat das Projekt?)
**Aufruf:** `get_namespace_tree(project="AiNetLinter", includeTypes=false)`
```text
# Namespaces in Projekt 'AiNetLinter':

- AiNetLinter (3 Typen)
- AiNetLinter.Commands (14 Typen)
- AiNetLinter.Configuration (6 Typen)
- AiNetLinter.Core (18 Typen)
- AiNetLinter.Mcp (4 Typen)
  - AiNetLinter.Mcp.Tools (22 Typen)
- AiNetLinter.Metrics (4 Typen)

Tipp: Nutze get_namespace_tree(project="AiNetLinter", namespacePrefix="AiNetLinter.Mcp.Tools") fuer die Typen.
```

### Stufe 3: Namespace-Inhalt (Welche Typen liegen hier?)
**Aufruf:** `get_namespace_tree(project="AiNetLinter", namespacePrefix="AiNetLinter.Mcp.Tools", kind="class")`
```text
# Typen in Namespace 'AiNetLinter.Mcp.Tools' (Projekt: AiNetLinter):

- FindSymbolTool (Klasse) — src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolTool.cs:25
- GetCallTreeTool (Klasse) — src/AiNetLinter/Mcp/Tools/SymbolGraph/GetCallTreeTool.cs:17
- GetClassStructureTool (Klasse) — src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:36
- GetViolationsTool (Klasse) — src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs:23
- SafeguardTool (Klasse) — src/AiNetLinter/Mcp/Tools/Analysis/SafeguardTool.cs:30
... (18 von 18 Typen gezeigt)

[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.
```

---

## 5. Technische Umsetzung (Roslyn-APIs)

* **Projekt-Ebene:** Zugriff über `Solution.Projects`. Test-Projekte werden über Heuristik/Referenzen annotiert.
* **Namespace- & Typ-Ebene:** 
  * Traversierung über `Project.GetCompilationAsync() -> Compilation.GlobalNamespace`.
  * `INamespaceSymbol.GetNamespaceMembers()` für Sub-Namespaces.
  * `INamespaceSymbol.GetTypeMembers()` für Typen (`INamedTypeSymbol`).
* **Performance:** Da die Roslyn-Compilation im residenten Server bereits vorliegt, dauert die Abfrage < 20ms (0 Festplatten-I/O).

---

## 6. Akzeptanzkriterien

1. `get_namespace_tree` ohne Parameter listet alle Projekte mit Typ- und Namespace-Zahlen.
2. `get_namespace_tree` mit `project` schränkt auf das Zielprojekt ein.
3. `get_namespace_tree` mit `namespacePrefix` filtert hierarchisch ab diesem Namespace.
4. `includeTypes=false` gibt nur die Namespace-Hierarchie zurück (maximal token-sparend).
5. `kind`-Filter filtert zuverlässig nach `class`, `interface`, `record`, `enum` oder `all`.
6. `maxResults` trunkiert sauber mit Truncation-Meta-Zeile und Sufficiency-Hinweis bei Vollständigkeit.
7. `StructuredContent` liefert ein valides JSON-Objekt (`{ project, namespacePrefix, namespaces: [...] }`).
8. 15+ Unit-Tests in `AiNetLinter.FastTests` belegen alle Hierarchie-Ebenen, Filter und Fehlerfälle.
