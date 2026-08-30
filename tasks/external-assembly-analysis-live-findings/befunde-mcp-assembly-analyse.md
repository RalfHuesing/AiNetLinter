# Technische Befunde & Optimierungsbedarf der AiNetLinter MCP-Tools

Dieses Dokument fasst die technischen Schwachstellen, Fehler und Performance-Probleme zusammen, die bei der praktischen Analyse realer DLLs (`Sagede.OfficeLine.CloudStorage.dll`, `Sagede.OfficeLine.Wawi.BelegEngine.dll`, `Sagede.OfficeLine.Wawi.LagerEngine.dll`) aufgetreten sind.

---

## Übersicht der Befunde

| ID | Priorität | Betroffenes Tool / Komponente | Problem | Ursache & Empfohlene Behebung |
|:---|:---|:---|:---|:---|
| **BEF-01** | **P0 (Blocker)** | `ExternalResourceRegistry` / `AssemblyAnalysisRegistry` | **Totalausfall durch Ressourcen-Erschöpfung:** `ANALYSIS_FAILED: Das externe Ressourcenlimit ist ausgeschöpft (32 Einträge)`. | Grenze ist hardcodiert in `ExternalResourceRegistryDefaults.MaxResidentResources = 32`. **Lösung:** Wert konfigurierbar machen (`--mcp-max-assemblies`, Settings) + echtes LRU-Verfahren (`EvictLeastRecentlyUsed`) statt hartem Abbruch. |
| **BEF-02** | **P1 (Kritisch)** | `inspect_assembly`, `find_assembly_extensions` | **Token-Explosion / 643 KB Output:** Ungefilterte Ausgabe aller transitiven Referenz-Diagnosen (1.300+ Knoten). | Im Textoutput nur Root-Assembly-Diagnosen ausgeben. Transitive Diagnosen als 1-Zeilen-Metrik zusammenfassen. |
| **BEF-03** | **P1 (Kritisch)** | `get_call_tree`, `get_symbol_body`, `dependency_graph` | **Crash bei Assembly-Zielen:** `ArgumentException: The path is empty. (Parameter 'relativeTo')`. | Bei dekompilierten In-Memory-Dokumenten existiert kein physischer Solution-Root. `Path.GetRelativePath` muss für Assembly-Sessions abgesichert oder übersprungen werden. |
| **BEF-04** | **P2 (Feature)** | `find_symbol`, `get_call_tree` | **Fehlende Cross-Assembly Typauflösung:** Keine Möglichkeit abzufragen, in welcher referenzierten DLL ein externer Typ (z. B. `LagerJob`) definiert ist. | Parameter `includeReferences: bool` für `find_symbol` bzw. transitives Call-Tracing über Assembly-Grenzen hinweg. |
| **BEF-05** | **P2 (Mittel)** | `find_references` | **0 Treffer bei Compile-Errors in Decompilation:** Roslyn `SymbolFinder` scheitert an semantischem Binding. | Bei unvollständig auflösbaren Dekompilationen (hier 1.071 Errors wegen fehlender Framework-/COM-Typen) einen syntaktischen/toleranten Symbol-Finder als Fallback nutzen. |
| **BEF-06** | **P2 (Mittel)** | `get_server_health` | **Healthcheck Output-Bloat (289 KB):** Ungefilterter Dump aller Decompiler-Diagnosen aller 32 residenten Sessions. | Im Health-Check pro Session nur Metadaten und Diagnose-Anzahlen ausweisen, nicht den vollen Text aller Compiler-Meldungen. |
| **BEF-07** | **P2 (Mittel)** | `get_file_skeleton` | **In-Memory-Dateien nicht gefunden:** `RESOURCE_NOT_FOUND` bei dekompilierten Dateinamen (`00004-...cs`). | Physische Dateiprüfung gegen Dateisystem schlägt fehl; muss auf `RoslynWorkspace.CurrentSolution` der Assembly-Session umgestellt werden. |
| **BEF-08** | **P2 (Mittel)** | `get_call_tree` (Identifikatoren) | **`SYMBOL_NOT_FOUND` bei Signatur mit Parametern:** `Beleg.Save(bool)` wird nicht aufgelöst, nur `Beleg.Save`. | Der Symbol-Resolver für Methodensignaturen mit Parametertypen parst Kurzformen wie `(bool)` nicht tolerant genug gegen DocCommentIds / Roslyn-Symbole. |
| **BEF-09** | **P3 (Niedrig)** | `get_class_structure` | **Kappung bei Riesenklassen (1.100 Member):** Cap bei 200 Membern erfordert wiederholte Aufrufe. | Ein `kindFilter` (z. B. `kind: "method"` oder `kind: "property"`) oder Namens-Filter direkt in `get_class_structure` würde das gezielte Erkunden großer Klassen stark beschleunigen. |
| **BEF-10** | **P3 (Niedrig)** | `metrics_tree` | **Dateigröße 0 B bei In-Memory-Dateien:** Dateisystem-Größe fehlt. | Bei dekompilierten Syntaxbäumen die Größe über `SourceText.Length` in Bytes annähern, statt 0 B auszugeben. |
| **BEF-11** | **Positiv / Valide** | `metrics_lookup`, `get_type_hierarchy`, Routing | **Exzellente Funktionalität & Robustheit:** Metriken, Schwellwertabgleiche, Typhierarchie und Unsupported-Routing funktionieren stabil. | Architekturansatz mit `targetType='assembly'` und einheitlichem Error-Handling (`ASSEMBLY_TARGET_UNSUPPORTED`) hat sich voll bewährt. |

---

## Detaillierte Fehleranalyse

### 1. Blocker: Ressourcenlimit-Erschöpfung in `ExternalResourceRegistry`
- **Fundstelle im Code:**
  [`ExternalResourceRegistryDefaults.MaxResidentResources = 32`](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/ExternalResourceRegistry.cs#L15)
  in `ExternalResourceRegistry.cs`.
- **Aktueller Zustand:**
  Der Wert 32 ist **fest im C#-Code hardcodiert** und wird beim Initialisieren von `AssemblyAnalysisHostComposition` ohne Konfigurationsoptionen instanziiert.
- **Problem:**
  Es gibt nur eine zeitbasierte 45-Minuten-Idle-TTL (`EvictExpiredNoLock`). Bei `TryAcquire` wird bei 32 Einträgen sofort `CapacityExceeded` ausgelöst, statt eine ungenutzte Session per LRU zu verdrängen.
- **Empfohlene Lösung:**
  1. **Konfigurierbarkeit:** CLI-Option `--mcp-max-assemblies <n>` (analog zu `--mcp-max-projects 4`) und Support in Settings/`rules.json`.
  2. **Automatisches LRU-Eviction (`EvictLeastRecentlyUsed`):** Wenn das Limit erreicht ist, automatisch die am längsten ungenutzte Session verdrängen.
  3. **Sub-Scoping:** Transitive Referenzen aus einer Root-Analyse als Child-Ressourcen führen.

---

### 2. Crash bei `get_call_tree`, `get_symbol_body` & `dependency_graph` — `relativeTo`-Fehler
- **Reproduktion:** Aufruf auf beliebiges Symbol einer externen DLL.
- **Fehlermeldung:**
  ```text
  [ERROR]: WORKSPACE_DIAGNOSTIC: Unerwarteter Fehler in get_call_tree: The path is empty. (Parameter 'relativeTo')
  ```
- **Technische Ursache:**
  In internen Pfadberechnungen wird `Path.GetRelativePath(rootPath, docPath)` aufgerufen. Bei dekompilierten Assemblies ist `rootPath` jedoch ein Leerstring oder `null`.
- **Lösung:**
  Wenn `string.IsNullOrEmpty(rootPath)` oder bei Assembly-Zielen: Entweder den dekompilierten Dateinamen (`00004-Beleg.cs`) direkt verwenden oder den relativen Pfad über `doc.Name` beziehen.

---

### 3. Diagnose-Flut in `inspect_assembly` (643 KB) und `get_server_health` (289 KB)
- **Auswirkung:**
  Tausende Zeilen Decompiler- und Compiler-Diagnosen fluten den MCP-Textoutput (z. B. 4.600 Meldungen zu `System.CodeDom` aus `System.dll`). Ein Healthcheck verbraucht ~70.000 Tokens.
- **Lösung:**
  Transitive Diagnosen im Textoutput strikt aggregieren (`158 Referenzen analysiert, 12 nicht auflösbar`). Details nur im strukturierten JSON oder auf explizite Anforderung.

---

### 4. Fehlende Cross-Assembly Navigation & Call-Tracing
- **Szenario:** `BelegEngine.dll` ruft `_lagerJob.Execute()` auf. Der Typ `LagerJob` liegt in einer referenzierten DLL (`LagerEngine.dll` / `Engine.dll`).
- **Aktuelles Defizit:**
  1. Kein Tool kann beantworten, welche referenzierte Assembly den Typ `LagerJob` deklariert.
  2. Kein transitiver Call-Tree über DLL-Grenzen hinweg.
- **Lösungsvorschlag:**
  Option `searchReferencedAssemblies: true` in `find_symbol` und Cross-Assembly Call-Tracing in `get_call_tree`.
