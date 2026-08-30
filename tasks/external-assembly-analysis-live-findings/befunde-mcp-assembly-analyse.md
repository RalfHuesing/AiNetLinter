# Technische Befunde & Optimierungsbedarf der AiNetLinter MCP-Tools

Dieses Dokument fasst die technischen Schwachstellen, Fehler und Performance-Probleme zusammen, die bei der praktischen Analyse realer DLLs (`Sagede.OfficeLine.CloudStorage.dll` und `Sagede.OfficeLine.Wawi.BelegEngine.dll`) aufgetreten sind.

---

## Übersicht der Befunde

| ID | Priorität | Betroffenes Tool / Komponente | Problem | Ursache & Empfohlene Behebung |
|:---|:---|:---|:---|:---|
| **BEF-01** | **P1 (Kritisch)** | `inspect_assembly`, `find_assembly_extensions` | **Token-Explosion / 643 KB Output:** Ungefilterte Ausgabe aller transitiven Referenz-Diagnosen (1.300+ Knoten). | Im Textoutput nur Root-Assembly-Diagnosen ausgeben. Transitive Diagnosen als 1-Zeilen-Metrik zusammenfassen. |
| **BEF-02** | **P1 (Kritisch)** | `get_call_tree`, `get_symbol_body`, `dependency_graph` | **Crash bei Assembly-Zielen:** `ArgumentException: The path is empty. (Parameter 'relativeTo')`. | Bei dekompilierten In-Memory-Dokumenten existiert kein physischer Solution-Root. `Path.GetRelativePath` muss für Assembly-Sessions abgesichert oder übersprungen werden. |
| **BEF-03** | **P2 (Mittel)** | `find_references` | **0 Treffer bei Compile-Errors in Decompilation:** Roslyn `SymbolFinder` scheitert an semantischem Binding. | Bei unvollständig auflösbaren Dekompilationen (hier 1.071 Errors wegen fehlender Framework-/COM-Typen) einen syntaktischen/toleranten Symbol-Finder als Fallback nutzen. |
| **BEF-04** | **P2 (Mittel)** | `get_file_skeleton` | **In-Memory-Dateien nicht gefunden:** `RESOURCE_NOT_FOUND` bei dekompilierten Dateinamen (`00004-...cs`). | Physische Dateiprüfung gegen Dateisystem schlägt fehl; muss auf `RoslynWorkspace.CurrentSolution` der Assembly-Session umgestellt werden. |
| **BEF-05** | **P2 (Mittel)** | `get_call_tree` (Identifikatoren) | **`SYMBOL_NOT_FOUND` bei Signatur mit Parametern:** `Beleg.Save(bool)` wird nicht aufgelöst, nur `Beleg.Save`. | Der Symbol-Resolver für Methodensignaturen mit Parametertypen parst Kurzformen wie `(bool)` nicht tolerant genug gegen DocCommentIds / Roslyn-Symbole. |
| **BEF-06** | **P3 (Niedrig)** | `get_class_structure` | **Kappung bei Riesenklassen (1.100 Member):** Cap bei 200 Membern erfordert wiederholte Aufrufe. | Ein `kindFilter` (z. B. `kind: "method"` oder `kind: "property"`) oder Namens-Filter direkt in `get_class_structure` würde das gezielte Erkunden großer Klassen stark beschleunigen. |
| **BEF-07** | **P3 (Niedrig)** | `metrics_tree` | **Dateigröße 0 B bei In-Memory-Dateien:** Dateisystem-Größe fehlt. | Bei dekompilierten Syntaxbäumen die Größe über `SourceText.Length` in Bytes annähern, statt 0 B auszugeben. |
| **BEF-08** | **Positiv / Valide** | `metrics_lookup`, `get_type_hierarchy`, Routing | **Exzellente Funktionalität & Robustheit:** Metriken, Schwellwertabgleiche, Typhierarchie und Unsupported-Routing funktionieren stabil. | Architekturansatz mit `targetType='assembly'` und einheitlichem Error-Handling (`ASSEMBLY_TARGET_UNSUPPORTED`) hat sich voll bewährt. |

---

## Detaillierte Fehleranalyse

### 1. `get_call_tree`, `get_symbol_body` & `dependency_graph` — `relativeTo`-Fehler
- **Reproduktion:**
  ```json
  {
    "targetType": "assembly",
    "targetPath": "C:\\Program Files (x86)\\Sage\\Sage 100\\9.0\\Shared\\Sagede.OfficeLine.Wawi.BelegEngine.dll",
    "symbolIdentifier": "Sagede.OfficeLine.Wawi.BelegEngine.Beleg.Save"
  }
  ```
- **Fehlermeldung:**
  ```text
  [ERROR]: WORKSPACE_DIAGNOSTIC: Unerwarteter Fehler in get_call_tree: The path is empty. (Parameter 'relativeTo')
  ```
- **Technische Ursache:**
  In internen Pfadberechnungen (z. B. um für Symbole relative Dateipfade wie `src/Models/Beleg.cs:120` zu formatieren) wird `Path.GetRelativePath(rootPath, docPath)` aufgerufen. Bei dekompilierten Assemblies ist `rootPath` jedoch ein Leerstring oder `null`. Betrifft gleichermaßen:
  - `GetSymbolBodyTool`
  - `GetCallTreeTool`
  - `DependencyGraphTool`
- **Lösung:**
  Wenn `string.IsNullOrEmpty(rootPath)` oder bei Assembly-Zielen: Entweder den dekompilierten Dateinamen (`00004-Beleg.cs`) direkt verwenden oder den relativen Pfad über `doc.Name` beziehen.

---

### 2. `find_references` — Semantisches Binding bei fehlerhaften/partiellen Dekompilationen
- **Symptom:** `find_references` meldet für `BelegData.InsertBeleg` `Keine Aufrufstellen gefunden`, obwohl `Beleg.cs` die Methode intensiv aufruft.
- **Ursache:**
  Roslyns `SymbolFinder.FindReferencesAsync` erfordert ein fehlerfreies semantisches Modell. Wenn eine dekompilierte DLL viele nicht auflösbare Typen hat (hier 1.071 Compile-Fehler durch fehlende COM- und Framework-Abhängigkeiten), kann Roslyn Methodenaufrufe den Methodensymbolen nicht zuordnen.
- **Lösung:**
  Einführung einer fehlertoleranten Referenzsuche für Assembly-Sessions (z. B. Identifier-Syntax-Matching kombiniert mit Member-Matching).

---

### 3. `inspect_assembly` — Diagnose-Flut
- **Reproduktion:** Aufruf von `inspect_assembly` auf einer DLL mit vielen Abhängigkeiten (z. B. Sage 100).
- **Auswirkung:**
  - Der Server sammelt für jede referenzierte DLL alle Roslyn- und Decompiler-Meldungen.
  - Bei 158 Referenzen summiert sich dies auf tausende Zeilen Text (z. B. 4.659 Meldungen zu `System.CodeDom` aus `System.dll`).
  - Der eigentliche API-Block (3–5 Zeilen) landet ganz am Ende nach 600 KB Diagnosetext.
- **Lösung:**
  1. `analysis.diagnostics` im MCP-Header auf maximal 3–5 wichtigste Fehler kappen.
  2. Im Textblock der Antwort:
     ```text
     Referenzen: 158 (122 aufgelöst, 24 Versionsabweichungen, 12 nicht gefunden)
     ```
     statt hunderte Zeilen Einzelfehlermeldungen zu drucken.

---

### 4. `get_class_structure` — Filter-Erweiterung
- **Szenario:** Die Klasse `Beleg` enthält **1.100 Member**.
- **Aktuelles Verhalten:** `get_class_structure` gibt maximal `maxMembers: 200` zurück. Sortierung nach `kind` hilft, schneidet aber dennoch ab.
- **Lösungsvorschlag:**
  Parameter `kind: string?` (z. B. `"method"`, `"property"`, `"field"`, `"event"`) oder `nameFilter: string?` ergänzen, damit gezielt nur relevante Member abgefragt werden können.
