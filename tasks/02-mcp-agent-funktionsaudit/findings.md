# MCP-Agent-Funktionsaudit: AiNetLinter

Dieser Audit dokumentiert funktionale Mängel, Ergonomie-Brüche und Bugs des **AiNetLinter MCP-Servers** aus der Perspektive eines autonomen Coding-Agenten. Getestet wurden die Tool-Funktionen sowohl an der lokalen Quellcode-Solution als auch im Dekompilations-Modus an einer externen, komplexen Legacy-Assembly.

> [!NOTE]
> Gemäß Copyright-Vorgaben sind in diesem Dokument keinerlei proprietäre Pfade, Typnamen, Membernamen oder Codeteile der untersuchten Fremd-Assembly enthalten. Alle Beobachtungen beziehen sich rein auf das Verhalten und die Schnittstellen des AiNetLinter MCP-Servers.

Die Befunde sind strikt nach **Priorität sortiert** (Kritische Bugs → Hohe Priorität / Protokoll- & Ergonomie-Fallen → Mittlere Priorität / Heuristik- & Filterschwächen).

---

## Priorität 1: Kritische Bugs / Showstopper (Tool-Ausfälle für Agenten)

### 1.1 `ApplyWireBudget` vernichtet Textdarstellung bei Assembly-Tools (`find_symbol`, `get_class_structure`, `get_file_skeleton`, `get_assembly_context`)
- **Status**: **Behoben** in Commit `33f5e159`
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
- **Behebung**:
  - `DefaultResponseBytes` auf 32 KB und `MaxResponseBytes` auf 64 KB angehoben.
  - `ApplyWireBudget` trunkiert den Text nun schonend auf das verbleibende Text-Budget (mit Truncation-Hinweis), statt den gesamten Text blind durch einen Kürzungs-Einzeiler zu vernichten.
  - `maxResponseBytes` optional in `get_class_structure` und `get_file_skeleton` freigeschaltet.

---

### 1.2 `RESOURCE_NOT_FOUND` bei relativen Pfaden in dekompilierten Assemblies (`get_file_skeleton`, `dependency_graph`)
- **Status**: **Behoben** in Commit `6e555d34`
- **Art**: Funktionaler Bug / Pfadauflösungsfehler
- **Betroffene Komponenten**: [SolutionDocumentPathResolver.cs:65-82](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs#L65-L82), [DependencyGraphTool.cs:94-98](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphTool.cs#L94-L98)
- **Beobachtung**:
  Wird der relative Dateipfad übergeben, den `get_file_tree` oder `search_assembly` für eine dekompilierte Assembly liefert (z. B. `Unterordner/Datei.cs`), quittieren `get_file_skeleton` und `dependency_graph` dies sofort mit:
  ```text
  [ERROR]: RESOURCE_NOT_FOUND: Datei 'Unterordner/Datei.cs' nicht in der Solution gefunden.
    hint: Pfad relativ zum Solution-Verzeichnis angeben (Forward- oder Backslash), 'find_symbol' zur Orientierung nutzen.
  ```
- **Ursache**:
  `SolutionDocumentPathResolver.GetSolutionDirectory(solution)` sucht nach `solution.FilePath`. Bei dekompilierten Assemblies läuft der Roslyn-Workspace jedoch als generierter Workspace ohne physische `.sln`-Datei (`solution.FilePath` ist `null` oder leer). Dadurch schlug die relative Pfadauflösung fehl.
- **Behebung**:
  `SolutionDocumentPathResolver.GetSolutionDirectory` ermittelt nun bei leerem/null `solution.FilePath` das gemeinsame Basisverzeichnis aller Dokumente in Projekten der Solution. Relative Pfade werden sowohl gegen den gemeinsamen Dokument-Root als auch gegen Dokument-Pfade robust aufgelöst.

---

### 1.3 `dependency_graph` korrumpiert absolute Pfade bei Assembly-Zielen
- **Status**: **Behoben** in Commit `6e555d34`
- **Art**: Funktionaler Bug / Datenverfälschung
- **Betroffene Komponenten**: [DependencyGraphTool.cs:149-168](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphTool.cs#L149-L168), [DependencyGraphScanner.cs:327-332](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphScanner.cs#L327-L332)
- **Beobachtung**:
  Übergibt man `dependency_graph` den absoluten Pfad einer dekompilierten Datei aus dem Assembly-Cache, meldete das Tool ausgehende und eingehende Abhängigkeiten mit verstümmelten Fantasie-Pfaden im Server-Installationsverzeichnis:
  ```text
  - C:\Daten\Tools\AiNetLinter-win-x64\ZielKlasse.cs (1 Typ: ...)
  ```
- **Ursache**:
  1. `DependencyGraphScanner.ToRelativePath` berechnete `PathNormalizer.ToRelative(solutionDir, absolutePath)`. Da `solutionDir` bei Assembly-Workspaces leer (`""`) war, schnitt der Normalizer Pfadsegmente falsch ab.
  2. Anschließend versuchte `DependencyGraphTool.ToAbsolutePath` den Pfad wieder absolut zu machen: `Path.Combine(solutionDir, path)`. Da `solutionDir` leer war, löste `Path.GetFullPath` gegen das aktuelle Arbeitsverzeichnis des laufenden Prozesses auf.
- **Behebung**:
  `DependencyGraphScanner` und `DependencyGraphTool` nutzen `SolutionDocumentPathResolver.GetSolutionDirectory` mit Fallback auf den dekompilierten Dokument-Root. Wenn `solutionDir` leer ist oder der Pfad bereits absolut ist, wird der Pfad unverändert beibehalten und nicht mehr gegen das Tool-Verzeichnis verfälscht.

---

## Priorität 2: Hohe Priorität (Ergonomie-Fallstricke & Protokoll-Inkonsistenzen)

### 2.1 Paginierungs-Falle: Widerspruch zwischen Hinweistext (`continuationToken`) und Schema (`cursor`) in `search_assembly`
- **Status**: **Behoben** in Commit `0400f617`
- **Art**: Schema-/Instruktions-Diskrepanz (Endlosschleifen-Gefahr für Agenten)
- **Betroffene Komponenten**: `search_assembly.json`, `AssemblySearchTool.cs`, `AssemblyAnalysisToolRegistrations.cs`
- **Beobachtung**:
  Trunkiert `search_assembly` das Ergebnis wegen `maxResults`, enthielt die Textausgabe folgende Handlungsanweisung:
  ```text
  Ergebnis gekürzt (maxResults); continuationToken=5; continuationToken mit derselben Suchanfrage verwenden oder maxResults erhoehen.
  ```
  Übergab der Agent `{ "continuationToken": "5" }`, wurde der Parameter ignoriert, weil der Paginierungsparameter `cursor` hieß.
- **Behebung**:
  `AssemblySearchTool` und die Registrierungen akzeptieren `continuationToken` nun als voll funktionsfähigen Alias für `cursor`. Der Hinweistext weist transparent auf beide Parameter hin (`cursor=...; continuationToken=...`).

---

### 2.2 Inkonsistenter Zielvertrag: `projectRoot` vs. `targetType` & `targetPath`
- **Status**: **Behoben** in Commit `c6a4072a`
- **Art**: Schnittstellen-Inkonsistenz & Dokumentations-Widerspruch
- **Betroffene Komponenten**: `AGENTS.md` §1, [AnalysisTargetResolver.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Targets/AnalysisTargetResolver.cs)
- **Beobachtung**:
  In `AGENTS.md` §1 wurde veraltet behauptet, dass jeder Aufruf `projectRoot` erhält. Die Tools verlangen jedoch einheitlich `targetType` und `targetPath`.
- **Behebung**:
  - `AGENTS.md` §1 bereinigt und auf den kanonischen `targetType` (`project`/`assembly`) und `targetPath`-Vertrag aktualisiert.
  - `AnalysisTargetResolver.ResolveTarget` ist nun case-insensitive und fehlertolerant gegenüber Whitespace (`targetType.Trim().ToLowerInvariant()`).
  - Fehlermeldung präzisiert: Erklärt Agenten genau, welche Werte erwartet werden (`'project'` für Solution/Root, `'assembly'` für DLL/EXE).

---

### 2.3 Latenz bei tiefen Analysen auf dekompilierten Assemblies (`get_impact`, `get_assembly_context`)
- **Status**: Dokumentiert & durch P1.1 (Budget-Rettung) massiv entschärft; Short-Circuiting für Tests auf Assemblies für zukünftiges Major-Update vorgemerkt.

---

## Priorität 3: Mittlere Priorität (Heuristiken & Filter-Möglichkeiten)

### 3.1 Diskrepanz zwischen Test-Aufrufern und statischer Test-Zuordnung in `get_feature_context`
- **Status**: Als Heuristik-Backlog vorgemerkt (aktuell durch `find_references` / Aufrufer-Auflistung abgedeckt).

---

### 3.2 Fehlende Filter nach `ruleId` und `severity` in `get_violations`
- **Status**: **Behoben** in Commit `7aca33c5`
- **Art**: Fehlende Filter-Ergonomie
- **Betroffene Komponenten**: `get_violations.json`, `GetViolationsTool.cs`, `ViolationScopeFilter.cs`, `GetViolationsScanner.cs`
- **Behebung**:
  - Optionale Filter `ruleId` (Filterung nach Regelname/-identifikator) und `minSeverity` (`info`, `warning`, `error`) im MCP-Schema und im Tool implementiert.
  - `ViolationFilterOptions` strukturiert die Filterbedingungen sauber.
  - Unbekannte Schweregrade werden fehlertolerant behandelt.

---

### 3.3 False Positives bei Namens-Heuristik in `find_magic_values` (`security_candidates`)
- **Status**: **Behoben** in Commit `28076049`
- **Art**: Heuristik-Schwäche
- **Betroffene Komponenten**: [MagicValuesClassifier.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/MagicValues/MagicValuesClassifier.cs)
- **Behebung**:
  `IsNonSecretToken`-Prüfung ergänzt. Begriffe wie `continuationToken`, `cancellationToken`, `syntaxToken`, `publicKeyToken` oder JSON-RPC-Tokens werden nicht mehr fälschlicherweise als `security_candidates` klassifiziert.

---

### 3.4 Stillschweigendes Ignorieren von `endLine` in `get_symbol_body`
- **Status**: **Behoben** in Commit `28076049`
- **Art**: Ergonomie-Falle
- **Betroffene Komponenten**: `get_symbol_body.json`, `GetSymbolBodyTool.cs`
- **Behebung**:
  - `endLine` als optionaler Parameter im Schema registriert.
  - `EffectiveMaxBodyLines = endLine - startLine + 1` rechnet die gewünschte Zeilenobergrenze intuitiv um, wenn `endLine >= startLine` übergeben wird.

---

## Zusammenfassung & Handlungsmatrix

| ID | Bereich | Problem | Schweregrad | Status |
|:---|:---|:---|:---|:---|
| **1.1** | Assembly-Tools | `ApplyWireBudget` vernichtet Textdarstellung | **Kritisch (Showstopper)** | **Behoben** (`33f5e159`) |
| **1.2** | Assembly-Tools | `RESOURCE_NOT_FOUND` bei relativen Pfaden | **Kritisch** | **Behoben** (`6e555d34`) |
| **1.3** | Assembly-Tools | Pfadkorruption in `dependency_graph` | **Kritisch** | **Behoben** (`6e555d34`) |
| **2.1** | Paginierung | `continuationToken` vs. `cursor` in `search_assembly` | **Hoch** | **Behoben** (`0400f617`) |
| **2.2** | API-Vertrag | `projectRoot` vs. `targetType`/`targetPath` | **Hoch** | **Behoben** (`c6a4072a`) |
| **2.3** | Performance | 28s Latenz bei `get_impact` auf Assembly | **Hoch** | Analysiert & mitigiert |
| **3.1** | Feature-Kontext | Test-Aufrufer fehlen im Test-Kontext | **Mittel** | Backlog |
| **3.2** | Violations | Fehlende `ruleId`- & `severity`-Filter | **Mittel** | **Behoben** (`7aca33c5`) |
| **3.3** | Magic-Values | Paginierungs-Token als KeyVault-Kandidat | **Mittel** | **Behoben** (`28076049`) |
| **3.4** | Symbol-Body | `endLine` wird ignoriert | **Mittel** | **Behoben** (`28076049`) |
