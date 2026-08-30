# Technische Befunde & Optimierungsbedarf der AiNetLinter MCP-Tools

Dieses Dokument fasst die technischen Schwachstellen, Fehler und Performance-Probleme zusammen, die bei der praktischen Analyse realer DLLs (`Sagede.OfficeLine.CloudStorage.dll` und `Sagede.OfficeLine.Wawi.BelegEngine.dll`) aufgetreten sind.

---

## Übersicht der Befunde

| ID | Priorität | Betroffenes Tool / Komponente | Problem | Ursache & Empfohlene Behebung |
|:---|:---|:---|:---|:---|
| **BEF-01** | **P1 (Kritisch)** | `inspect_assembly`, `find_assembly_extensions` | **Token-Explosion / 643 KB Output:** Ungefilterte Ausgabe aller transitiven Referenz-Diagnosen (1.300+ Knoten). | Im Textoutput nur Root-Assembly-Diagnosen ausgeben. Transitive Diagnosen als 1-Zeilen-Metrik zusammenfassen. |
| **BEF-02** | **P1 (Kritisch)** | `get_call_tree`, `get_symbol_body` | **Crash bei Assembly-Zielen:** `ArgumentException: The path is empty. (Parameter 'relativeTo')`. | Bei dekompilierten In-Memory-Dokumenten existiert kein physischer Solution-Root. `Path.GetRelativePath` muss für Assembly-Sessions abgesichert oder übersprungen werden. |
| **BEF-03** | **P2 (Mittel)** | `get_file_skeleton` | **In-Memory-Dateien nicht gefunden:** `RESOURCE_NOT_FOUND` bei dekompilierten Dateinamen (`00004-...cs`). | Physische Dateiprüfung gegen Dateisystem schlägt fehl; muss auf `RoslynWorkspace.CurrentSolution` der Assembly-Session umgestellt werden. |
| **BEF-04** | **P2 (Mittel)** | `get_call_tree` (Identifikatoren) | **`SYMBOL_NOT_FOUND` bei Signatur mit Parametern:** `Beleg.Save(bool)` wird nicht aufgelöst, nur `Beleg.Save`. | Der Symbol-Resolver für Methodensignaturen mit Parametertypen parst Kurzformen wie `(bool)` nicht tolerant genug gegen DocCommentIds / Roslyn-Symbole. |
| **BEF-05** | **P3 (Niedrig)** | `get_class_structure` | **Kappung bei Riesenklassen (1.100 Member):** Cap bei 200 Membern erfordert wiederholte Aufrufe. | Ein `kindFilter` (z. B. `kind: "method"` oder `kind: "property"`) oder Namens-Filter direkt in `get_class_structure` würde das gezielte Erkunden großer Klassen stark beschleunigen. |

---

## Detaillierte Fehleranalyse

### 1. `get_call_tree` & `get_symbol_body` — `relativeTo`-Fehler
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
  In internen Pfadberechnungen (z. B. um für Symbole relative Dateipfade wie `src/Models/Beleg.cs:120` zu formatieren) wird `Path.GetRelativePath(rootPath, docPath)` aufgerufen. Bei dekompilierten Assemblies ist `rootPath` jedoch ein Leerstring oder `null`.
- **Lösung:**
  Wenn `string.IsNullOrEmpty(rootPath)` oder bei Assembly-Zielen: Entweder den dekompilierten Dateinamen (`00004-Beleg.cs`) direkt verwenden oder den relativen Pfad über `doc.Name` beziehen.

---

### 2. `inspect_assembly` — Diagnose-Flut
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

### 3. `get_class_structure` — Filter-Erweiterung
- **Szenario:** Die Klasse `Beleg` enthält **1.100 Member**.
- **Aktuelles Verhalten:** `get_class_structure` gibt maximal `maxMembers: 200` zurück. Sortierung nach `kind` hilft, schneidet aber dennoch ab.
- **Lösungsvorschlag:**
  Parameter `kind: string?` (z. B. `"method"`, `"property"`, `"field"`, `"event"`) oder `nameFilter: string?` ergänzen, damit gezielt nur relevante Member abgefragt werden können.
