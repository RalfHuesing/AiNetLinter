# Befunde aus dem Live-Test der externen Assembly-Analyse

## 1. Kontext & Testaufbau
- **Test-Datum:** 2026-08-30
- **Getestete DLL:** `C:\Program Files (x86)\Sage\Sage 100\9.0\Shared\Sagede.OfficeLine.CloudStorage.dll`
- **Modus:** Live-MCP-Aufrufe über Daemon (`1.0.154`) mit `targetType: "assembly"` und `targetPath: "..."`
- **Ziel:** Verifikation der statischen Dekompilation, Referenz-Auflösung, Typ-Extraktion und MCP-Tool-Antworten unter realen Produktionsbedingungen (ohne Quellcode-Projekt).

---

## 2. Zusammenfassung der Befunde

| ID | Schweregrad | Bereich | Status | Kurzbeschreibung |
|:---|:---|:---|:---|:---|
| **BEF-01** | **P1 (Hoch)** | Token-Budget / Output-Größe | Zu beheben | `inspect_assembly` und `find_assembly_extensions` emittieren massives Rauschen (643 KB / 1.600 Zeilen) durch ungefilterte transitive Decompiler-Diagnosen aller 1.300+ besuchten Referenzknoten. |
| **BEF-02** | **P2 (Mittel)** | `get_symbol_body` | Zu beheben | Wirft `ArgumentException: The path is empty. (Parameter 'relativeTo')` bei dekompilierten In-Memory-Syntaxbäumen. |
| **BEF-03** | **P3 (Niedrig)** | `get_file_skeleton` | Zu beheben | Findet dekompilierte In-Memory-Dateinamen (`00000-...cs`) nicht (`RESOURCE_NOT_FOUND`), da physisch auf Platte gesucht wird. |
| **BEF-04** | **Info (Positiv)** | Sicherheit & Funktionalität | Bestätigt | Statische Analyse ohne Codeausführung ist 100% sicher; `get_class_structure`, `find_symbol`, `get_namespace_tree`, `get_server_health` und das Caching funktionieren einwandfrei und kompakt. |

---

## 3. Detaillierte Befundbeschreibungen

### BEF-01: Massive Context-Bloat durch transitive Referenz-Diagnosen in `inspect_assembly`
- **Symptom:** Der Aufruf von `inspect_assembly` lieferte eine Antwort mit **643 KB** und **1.611 Zeilen** (~140.000 bis 160.000 Tokens).
- **Ursache:** 
  1. `Sagede.OfficeLine.CloudStorage.dll` besitzt 158 direkte/transitive Abhängigkeiten.
  2. Der `AssemblyReferenceSessionExpander` traversiert bis zu 128 Knoten (im Test 1.323 besuchte Kanten).
  3. Bei der Text-Generierung in `InspectAssemblyTool` / `FindAssemblyExtensionsTool` werden sämtliche gesammelten Diagnosen aller dekompilierten Kind-Sessions ungekappt in den MCP-Text ausgegeben (z. B. 4.600 Roslyn-Meldungen zu `System.CodeDom` aus `System.dll`, hunderte Meldungen aus `ADODB.dll`, `Newtonsoft.Json.dll` etc.).
  4. Die eigentliche Nutzlast (die 3 Typen der Ziel-DLL) machte lediglich **12 Zeilen** am Ende des Texts aus.
- **Auswirkung auf KI-Agenten:**
  - Verstopft fast das gesamte LLM-Kontextfenster in einem einzigen Turn.
  - Zwingt IDE/Agent-Hosts zur Auslagerung in temporäre Dateien (`The output was large and was saved to output.txt`).
  - Erschwert die maschinelle Weiterverarbeitung, da der Nutzinhalt im Diagnose-Rauschen untergeht.
- **Empfohlene Lösung:**
  - **Progressive Disclosure & Kappung:** Im Text-Payload standardmäßig nur Diagnosen der *angeforderten Root-Assembly* ausgeben.
  - **Aggregation für Referenzen:** Transitive Diagnosen nur als Metrik zusammenfassen (z. B. `158 Referenzen analysiert: 122 aufgelöst, 24 Version-Mismatch, 12 nicht auflösbar`).
  - Volle Referenzdiagnosen nur bei explizitem Parameter (z. B. `includeReferenceDiagnostics: true`) oder im strukturierten JSON-Payload vorhalten.

---

### BEF-02: Pfad-Fehler in `get_symbol_body` bei dekompilierten DLLs
- **Symptom:** `get_symbol_body` für `Sagede.OfficeLine.CloudStorage.CloudStorageDropbox` schlägt fehl mit:
  ```text
  [ERROR]: WORKSPACE_DIAGNOSTIC: Unerwarteter Fehler in get_symbol_body: The path is empty. (Parameter 'relativeTo')
    context: Sagede.OfficeLine.CloudStorage.CloudStorageDropbox
  ```
- **Ursache:**
  - `GetSymbolBodyTool` setzt intern voraus, dass jedes Quelltext-Dokument einen relativen Pfad zum Projektroot (`RootPath`) besitzt (`Path.GetRelativePath(rootPath, docPath)`).
  - Dekompilierte Syntax-Trees aus `AssemblyDecompilationAdapter` liegen als synthetische In-Memory-Dateien vor (oder haben keinen gültigen relativen Solution-Root), wodurch `relativeTo` als leer übergeben wird.
- **Empfohlene Lösung:**
  - In `GetSymbolBodyTool` prüfen, ob `lease.Server` eine Assembly-Session ist bzw. ob ein In-Memory-Dokument vorliegt.
  - Den Quelltext direkt aus dem `SyntaxNode` / `SourceText` des semantischen Modells extrahieren, ohne relative Pfadtransformationen gegen das Dateisystem zu erzwingen.

---

### BEF-03: `get_file_skeleton` löst synthetische Decompiler-Dateinamen nicht auf
- **Symptom:** `get_file_skeleton(filePaths: ["00000-Sagede_OfficeLine_CloudStorage_CloudStorageDropbox.cs"])` meldet:
  ```text
  [ERROR]: RESOURCE_NOT_FOUND: Datei '00000-...' nicht in der Solution gefunden.
  ```
- **Ursache:**
  - `GetFileSkeletonTool` sucht nach physischen Dateien relativ zum Projektroot auf der Festplatte.
  - Die dekompilierten Dateien existieren jedoch primär im In-Memory Roslyn-Workspace der Assembly-Session (bzw. im Cache-Ordner).
- **Empfohlene Lösung:**
  - In der Assembly-Route von `GetFileSkeletonTool` die Dokumente über `RoslynWorkspace.CurrentSolution.Projects.SelectMany(p => p.Documents)` auflösen, statt die Existenz auf der Festplatte vorauszusetzen.

---

### BEF-04: Erfolgreich verifizierte Funktionen (Positiv-Befunde)
Folgende Kernkomponenten haben im Live-Test gegen Sage 100 DLLs hervorragend funktioniert:

1. **Sicherheit & Isolation (Zero-Execution):**
   - Die externe DLL wurde weder geladen (`Assembly.Load`) noch ausgeführt. Die Dekompilation und Roslyn-Workspace-Erstellung lief rein isoliert ab.
2. **Klassenstruktur-Extraktion (`get_class_structure`):**
   - Extrahiert öffentliche und private Member (Felder, Properties, Konstruktoren mit Parametertypen) tabellarisch, präzise und extrem token-sparend (< 1 KB).
3. **Semantische Suche (`find_symbol` & `get_namespace_tree`):**
   - Findet Klassen, Enums und Member sofort im dekompilierten Modell.
4. **Resiliente Referenzauflösung:**
   - Fehlende oder inkompatible Abhängigkeiten (.NET Framework vs. .NET 10 Runtime) führen zu keinem Crash, sondern werden sauber in den Session-Status als `partial` übernommen.
5. **Caching & Sharding (`cache\assembly`):**
   - Atomare Generationen und Pfad-Sharding funktionieren stabil. Folgeaufrufe profitieren von der persistenten Vorhaltung.
6. **Server-Health (`get_server_health`):**
   - Weist residente Assembly-Sessions mit SHA-256-Hash, Generation und Status korrekt aus.
