# MCP-Agent-Funktionsaudit: AiNetLinter

Dieser Audit dokumentiert funktionale Mängel, Ergonomie-Brüche und Bugs des **AiNetLinter MCP-Servers** aus der Perspektive eines autonomen Coding-Agenten. Getestet wurden die Tool-Funktionen sowohl an der lokalen Quellcode-Solution als auch im Dekompilations-Modus an einer externen, komplexen Legacy-Assembly.

> [!NOTE]
> Gemäß Copyright-Vorgaben sind in diesem Dokument keinerlei proprietäre Pfade, Typnamen, Membernamen oder Codeteile der untersuchten Fremd-Assembly enthalten. Alle Beobachtungen beziehen sich rein auf das Verhalten und die Schnittstellen des AiNetLinter MCP-Servers.

Die Befunde sind strikt nach **Priorität sortiert** (Kritische Bugs → Hohe Priorität / Protokoll- & Ergonomie-Fallen → Mittlere Priorität / Heuristik- & Filterschwächen).

---

## Priorität 1: Kritische Bugs / Showstopper (Tool-Ausfälle für Agenten)

### 1.1 `ApplyWireBudget` vernichtet Textdarstellung bei Assembly-Tools (`find_symbol`, `get_class_structure`, `get_file_skeleton`, `get_assembly_context`)
- **Art**: Schwerer Architektur- & Ergonomie-Bug (Verlust der primären LLM-Nutzlast)
- **Betroffene Komponenten**: [AssemblyAnalysisResponse.cs:111-137](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs#L111-L137), [AssemblyAnalysisResponseLimits.cs:19-22](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs#L19-L22)
- **Beobachtung**:
  Sobald ein Aufruf von `find_symbol`, `get_class_structure`, `get_file_skeleton` oder `get_assembly_context` auf einer dekompilierten Assembly ausgeführt wird, bei der mehr als eine Handvoll Symbole/Member vorliegen, erhält der Agent als Textantwort ausschließlich:
  ```text
  [ASSEMBLY] StructuredContent ist die kanonische Nutzlast; die Textdarstellung wurde wegen des gemeinsamen Wire-Budgets gekürzt.
  ```
  Der Agent sieht im Textfeld (`content[0].text`) absolut **0 nutzbare Daten**.
- **Ursache**:
  1. `DefaultResponseBytes` ist in `AssemblyAnalysisResponseLimits` auf extrem knappe **16 KB** festgesetzt.
  2. `ApplyWireBudget` misst `Measure(withBudget).TotalBytes` als Summe aus `TextBytes + StructuredBytes`.
  3. Da das begleitende JSON-Objekt (`StructuredContent`) bei größeren Klassen oder Treffermengen bereits 10–25 KB wiegt, wird das Gesamtbudget sofort überschritten.
  4. Anstatt die JSON-Nutzlast zu stutzen oder zu paginieren, ersetzt `ReplaceText` in Zeile 113–116 den gesamten für Menschen und LLM-Agenten lesbaren Markdown-Text durch den Kürzungs-Einzeiler.
- **Erschwerender Faktor**:
  Tools wie `get_class_structure` und `get_file_skeleton` bieten im MCP-Schema nicht einmal einen Parameter `maxResponseBytes` an. Ein Agent kann sich somit nicht einmal durch explizite Budgeterhöhung behelfen.
- **Empfohlene Behebung**:
  - Der für das LLM primär sichtbare Markdown-Text darf niemals vollständig durch einen Einzeiler ersetzt werden. Wenn ein Wire-Budget greift, muss primär der `StructuredContent` gestutzt werden oder das Default-Budget auf einen für MCP realistischen Wert (z. B. 64–128 KB) angehoben werden.

---

### 1.2 `RESOURCE_NOT_FOUND` bei relativen Pfaden in dekompilierten Assemblies (`get_file_skeleton`, `dependency_graph`)
- **Art**: Funktionaler Bug / Pfadauflösungsfehler
- **Betroffene Komponenten**: [SolutionDocumentPathResolver.cs:65-82](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs#L65-L82), [DependencyGraphTool.cs:94-98](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphTool.cs#L94-L98)
- **Beobachtung**:
  Wird der relative Dateipfad übergeben, den `get_file_tree` oder `search_assembly` für eine dekompilierte Assembly liefert (z. B. `Unterordner/Datei.cs`), quittieren `get_file_skeleton` und `dependency_graph` dies sofort mit:
  ```text
  [ERROR]: RESOURCE_NOT_FOUND: Datei 'Unterordner/Datei.cs' nicht in der Solution gefunden.
    hint: Pfad relativ zum Solution-Verzeichnis angeben (Forward- oder Backslash), 'find_symbol' zur Orientierung nutzen.
  ```
- **Ursache**:
  `SolutionDocumentPathResolver.GetSolutionDirectory(solution)` sucht nach `solution.FilePath`. Bei dekompilierten Assemblies läuft der Roslyn-Workspace jedoch als generierter Workspace ohne physische `.sln`-Datei (`solution.FilePath` ist `null` oder leer). Dadurch schlägt die relative Pfadauflösung ausnahmslos fehl.
- **Empfohlene Behebung**:
  Bei Assembly-Zielen muss `SolutionDocumentPathResolver` den dekompilierten `SourceRoot` bzw. das dekompilierte Projektverzeichnis als Basisverzeichnis für relative Pfade heranziehen.

---

### 1.3 `dependency_graph` korrumpiert absolute Pfade bei Assembly-Zielen
- **Art**: Funktionaler Bug / Datenverfälschung
- **Betroffene Komponenten**: [DependencyGraphTool.cs:149-168](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphTool.cs#L149-L168), [DependencyGraphScanner.cs:327-332](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphScanner.cs#L327-L332)
- **Beobachtung**:
  Übergibt man `dependency_graph` den absoluten Pfad einer dekompilierten Datei aus dem Assembly-Cache, meldet das Tool ausgehende und eingehende Abhängigkeiten mit verstümmelten Fantasie-Pfaden im Server-Installationsverzeichnis:
  ```text
  - C:\Daten\Tools\AiNetLinter-win-x64\ZielKlasse.cs (1 Typ: ...)
  ```
- **Ursache**:
  1. `DependencyGraphScanner.ToRelativePath` berechnet `PathNormalizer.ToRelative(solutionDir, absolutePath)`. Da `solutionDir` bei Assembly-Workspaces leer (`""`) ist, schneidet der Normalizer Pfadsegmente falsch ab.
  2. Anschließend versucht `DependencyGraphTool.ToAbsolutePath` den Pfad wieder absolut zu machen: `Path.Combine(solutionDir, path)`. Da `solutionDir` leer ist, löst `Path.GetFullPath` gegen das aktuelle Arbeitsverzeichnis des laufenden Prozesses (`C:\Daten\Tools\AiNetLinter-win-x64\`) auf.
- **Empfohlene Behebung**:
  In Assembly-Sitzungen muss der kanonische Cache-Pfad der Dekompilation konsistent als Root für Relativierungen und Re-Absolutierungen verwendet werden.

---

## Priorität 2: Hohe Priorität (Ergonomie-Fallstricke & Protokoll-Inkonsistenzen)

### 2.1 Paginierungs-Falle: Widerspruch zwischen Hinweistext (`continuationToken`) und Schema (`cursor`) in `search_assembly`
- **Art**: Schema-/Instruktions-Diskrepanz (Endlosschleifen-Gefahr für Agenten)
- **Betroffene Komponenten**: `search_assembly.json`, `AssemblySearchTool.cs`
- **Beobachtung**:
  Trunkiert `search_assembly` das Ergebnis wegen `maxResults`, enthält die Textausgabe folgende Handlungsanweisung:
  ```text
  Ergebnis gekürzt (maxResults); continuationToken=5; continuationToken mit derselben Suchanfrage verwenden oder maxResults erhoehen.
  ```
  Folgt der Agent dieser Anweisung und übergibt `{ "continuationToken": "5" }`, wird der Parameter stillschweigend ignoriert, da der Paginierungsparameter im MCP-Schema `cursor` heißt! Der Server liefert daraufhin erneut Seite 1 (Treffer 1–5). Ein Agent gerät in eine Endlosschleife.
- **Empfohlene Behebung**:
  Der Hinweistext muss entweder explizit `cursor=...` instruieren oder das Tool muss im Argument-Parser `continuationToken` als Synonym für `cursor` akzeptieren.

---

### 2.2 Inkonsistenter Zielvertrag: `projectRoot` vs. `targetType` & `targetPath`
- **Art**: Schnittstellen-Inkonsistenz & Dokumentations-Widerspruch
- **Betroffene Komponenten**: `AGENTS.md` §1 vs. Werkzeug-Schemata (`find_dead_code`, `find_magic_values`, `safeguard`, `pattern_detect`, etc.)
- **Beobachtung**:
  In `AGENTS.md` §1 wird vorgegeben:
  > *„Jeder projektgebundene Tool-Aufruf erhält zusätzlich den absoluten Parameter `projectRoot`; der einzige optionale Filter ist `get_server_health`.“*
  
  Tatsächlich verlangen Tools wie `get_feature_context` und `get_violations` `projectRoot`, während `find_dead_code`, `find_magic_values`, `safeguard`, `pattern_detect`, `get_hotspots`, `get_file_tree` und `dependency_graph` den Aufruf mit `An error occurred invoking ...` verweigern, wenn man ihnen `projectRoot` übergibt! Sie fordern stattdessen zwingend `targetType` und `targetPath`.
- **Folge**:
  Ein Agent, der sich an die dokumentierte Konvention hält, scheitert bei der Hälfte der Werkzeuge.
- **Empfohlene Behebung**:
  - Vereinheitlichung der Parameter: Der MCP-Server sollte tolerant sein und `projectRoot` intern automatisch auf `targetType="project"` und `targetPath=projectRoot` mappen, falls `targetPath` nicht explizit gesetzt ist.
  - Dokumentation in `AGENTS.md` präzisieren.

---

### 2.3 Extreme Latenz bei tiefen Analysen auf dekompilierten Assemblies (`get_impact`, `get_assembly_context`)
- **Art**: Performance- & Ergonomie-Schwäche
- **Beobachtung**:
  - `get_impact` benötigte auf der Assembly **28 Sekunden** für die Auflösung von lediglich zwei direkten Aufrufern einer Methode (während `find_references` denselben Sachverhalt in unter 2 Sekunden lieferte).
  - `get_assembly_context` benötigte ohne Einzelsymbol **24 Sekunden**, nur um dann den Wire-Budget-Kürzungs-Einzeiler auszugeben.
- **Ursache**:
  `get_impact` versucht standardmäßig im dekompilierten Gesamtgraph transitive Testbeziehungen und Diff-Kontexte aufzubauen, obwohl bei einer Fremd-Assembly weder Tests noch Git-Kontexte existieren.
- **Empfohlene Behebung**:
  Bei `targetType="assembly"` sollten irrelevante Teilanalysen (wie Git-Diffs, Test-Matching) sofort kurzgeschlossen werden.

---

## Priorität 3: Mittlere Priorität (Heuristiken & Filter-Möglichkeiten)

### 3.1 Diskrepanz zwischen Test-Aufrufern und statischer Test-Zuordnung in `get_feature_context`
- **Art**: Heuristik-Inkonsistenz
- **Betroffene Komponenten**: [GetFeatureContextScanner.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/GetFeatureContextScanner.cs)
- **Beobachtung**:
  In Abschnitt 3 („Direkte Aufrufer“) listet `get_feature_context` mehrere Fundstellen in Testdateien (z. B. `AssemblyAnalysisToolTests.SearchRegression.cs`) korrekt auf.
  In Abschnitt 4 („Test-Kontext“) steht unmittelbar darunter jedoch:
  ```text
  0 Testdateien, 0 Tests — Keine Tests statisch zugeordnet
  ```
- **Ursache**:
  Der Scanner stützt sich in Abschnitt 4 ausschließlich auf Namenskonventionen (`{Typ}Tests.cs`), ignoriert aber die in Abschnitt 3 bereits nachgewiesenen echten Aufrufer aus vorhandenen Testmethoden.
- **Empfohlene Behebung**:
  Wenn semantische Aufrufer aus Test-Klassen bereits identifiziert wurden, sollten diese automatisch in den Test-Kontext übernommen werden.

---

### 3.2 Fehlende Filter nach `ruleId` und `severity` in `get_violations`
- **Art**: Fehlende Filter-Ergonomie
- **Betroffene Komponenten**: `get_violations.json`, `GetViolationsTool.cs`
- **Beobachtung**:
  `get_violations` bietet aktuell lediglich einen textuellen `scopeFilter` (Projekt- oder Pfadsubstrat).
- **Problem**:
  In realen Projekten mit hunderten Verstößen kann ein Agent nicht gezielt nach einer bestimmten Regel fragen (z. B. nur `DuplicateCode` oder nur `AvoidExcessiveMiddleMen`) oder nach Schweregrad filtern (z. B. nur `error` zur Vorbereitung eines Merges). Der Agent muss stattdessen die gesamte Liste abrufen und lokal filtern.
- **Empfohlene Behebung**:
  Ergänzung optionaler Parameter `ruleId` (String) und `minSeverity` (`error`, `warning`, `info`).

---

### 3.3 False Positives bei Namens-Heuristik in `find_magic_values` (`security_candidates`)
- **Art**: Heuristik-Schwäche
- **Betroffene Komponenten**: [MagicValuesClassifier.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs)
- **Beobachtung**:
  Ganz normale Protokoll- und API-Bezeichner wie `"continuationToken"` werden von `find_magic_values` als `security_candidates` eingestuft mit der Empfehlung:
  ```text
  Empfehlung: In Secret-Store/KeyVault auslagern
  ```
- **Ursache**:
  Die Heuristik reagiert blind auf den Substring `"token"`, ohne zu differenzieren, ob es sich um Paginierungs-/Cancel-Tokens oder kryptographische Geheimnisse handelt.
- **Empfohlene Behebung**:
  Ausschluss bekannter Nicht-Geheimnis-Begriffe wie `continuationToken`, `cancellationToken`, `jwtToken` (wenn Parametername), etc. aus dem KeyVault-Vorschlag.

---

### 3.4 Stillschweigendes Ignorieren von `endLine` in `get_symbol_body`
- **Art**: Ergonomie-Falle
- **Betroffene Komponenten**: `get_symbol_body.json`, `GetSymbolBodyTool.cs`
- **Beobachtung**:
  Übergibt ein Agent intuitiv `startLine: 1` und `endLine: 30`, ignoriert das Tool `endLine` kommentarlos und gibt die Default-Menge von 80 Zeilen aus (`maxBodyLines = 80`).
- **Ursache**:
  Das Schema kennt nur `startLine` und `maxBodyLines`.
- **Empfohlene Behebung**:
  Entweder `endLine` ins Schema aufnehmen und intern auf `maxBodyLines = endLine - startLine + 1` umrechnen, oder dem Agenten bei unbekannten Argumenten einen Validierungshinweis geben.

---

## Zusammenfassung & Handlungsmatrix

| ID | Bereich | Problem | Schweregrad | Schnelle Maßnahme |
|:---|:---|:---|:---|:---|
| **1.1** | Assembly-Tools | `ApplyWireBudget` vernichtet Textdarstellung | **Kritisch (Showstopper)** | Text nicht verwerfen; StructuredContent beschneiden / Budget erhöhen |
| **1.2** | Assembly-Tools | `RESOURCE_NOT_FOUND` bei relativen Pfaden | **Kritisch** | `SolutionDocumentPathResolver` um dekompilierten SourceRoot erweitern |
| **1.3** | Assembly-Tools | Pfadkorruption in `dependency_graph` | **Kritisch** | Decompilation-Cache-Root als Basisverzeichnis statt leerem `solutionDir` |
| **2.1** | Paginierung | `continuationToken` vs. `cursor` in `search_assembly` | **Hoch** | Alias-Support für `continuationToken` im Tool-Argument |
| **2.2** | API-Vertrag | `projectRoot` vs. `targetType`/`targetPath` | **Hoch** | Fallback-Mapping `projectRoot` → `targetPath` mit `targetType='project'` |
| **2.3** | Performance | 28s Latenz bei `get_impact` auf Assembly | **Hoch** | Short-Circuit für Tests/Git bei Assembly-Zielen |
| **3.1** | Feature-Kontext | Test-Aufrufer fehlen im Test-Kontext | **Mittel** | Semantische Test-Aufrufer in Test-Summary übernehmen |
| **3.2** | Violations | Fehlende `ruleId`- & `severity`-Filter | **Mittel** | Filterparameter in `get_violations` ergänzen |
| **3.3** | Magic-Values | Paginierungs-Token als KeyVault-Kandidat | **Mittel** | Regex-Ausschluss für Paginierungsbegriffe |
| **3.4** | Symbol-Body | `endLine` wird ignoriert | **Mittel** | `endLine` im Schema unterstützen oder umrechnen |
