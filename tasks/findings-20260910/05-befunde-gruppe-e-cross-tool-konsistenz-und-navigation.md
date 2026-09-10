# Gruppe E: Cross-Tool-Konsistenz & Navigation — Befundbericht

## Geprüfte Dimensionen
- Status- und Vollständigkeitssignale (`completeness`, `result.available`)
- Navigation-Block (`operationStatus`, `result`, `next`)
- Semantische Konsistenz über Tools hinweg
- Namenskonventionen

---

### [Major] Befund F-01: `get_violations` projiziert 0 Verstöße als Fehler/Leermenge (`available=false`, `completeness=empty`, `next: refine_scope`)
- **Tool(s)**: `get_violations`
- **Quellcode**:
  - [GetViolationsTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/Analysis/GetViolationsTool.cs)
  - [McpNavigationProjection.Completeness.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/McpNavigationProjection.Completeness.cs)
- **Evidenz**:
  Aufruf von `get_violations` auf der sauberen Solution `AiNetLinter.slnx`:
  ```text
  Lint-Violations: 0 Verstoesse in 869 Dateien im Scope

  Keine Lint-Violations.

  [HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.

  ## Navigation
  - targetPath: `c:\Daten\Entwicklung\Ralf\AiNetLinter\AiNetLinter.slnx`
  - origin: `source`
  - snapshot: `153C17F3...` (kind: `source-files`, fresh: `true`)
  - operationStatus: `ok`
  - result: available=`false`
  - completeness: `empty`
  - next: `refine_scope` — Keine Treffer im vollständig geprüften Scope; Suchmuster oder Scope verfeinern und erneut suchen.
  ```
- **Problem**:
  Für einen Coding-Agenten ist das Ergebnis "0 Verstöße" der ideale Zielzustand (Clean Code).
  Die Navigation projiziert diesen Zustand jedoch wie eine fehlgeschlagene Suche nach einem Symbol:
  - `result: available=false` (suggeriert dem Agenten: Daten nicht verfügbar oder Abfrage fehlgeschlagen)
  - `completeness: empty` (suggeriert: Leere Menge, nichts gefunden)
  - `next: refine_scope` ("Keine Treffer... Suchmuster oder Scope verfeinern und erneut suchen.")
  Ein autonomer Agent gerät hier in eine Endlosschleife oder Frustration, weil er versucht, den Scope zu verfeinern, um "endlich Violations zu finden", statt die Aufgabe als erfolgreich gelöst abzuschließen!
- **Reproduktion**:
  `get_violations(targetPath="AiNetLinter.slnx")` auf einer fehlerfreien Solution aufrufen.
- **Empfehlung**:
  In [McpNavigationProjection.Completeness.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/McpNavigationProjection.Completeness.cs): Wenn ein Linter-Lauf ohne Fehler 0 Verstöße findet, ist das Ergebnis vollständig (`completeness: complete`), verfügbar (`available: true`) und erfordert keinen Folgeschritt (`next: none`).

---

### [Major] Befund F-02: `pattern_detect` setzt Gesamtergebnis auf `not_configured` (`available=false`), wenn nur eine Unterregel inaktiv ist
- **Tool(s)**: `pattern_detect`
- **Quellcode**:
  - [PatternDetectTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/PatternDetect/PatternDetectTool.cs)
  - [PatternDetectScanner.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/PatternDetect/PatternDetectScanner.cs)
- **Evidenz**:
  Aufruf von `pattern_detect(targetPath="AiNetLinter.slnx")`:
  5 von 6 Patterns (`god-class`, `async-void`, `long-method`, `empty-catch`, `feature-envy`) wurden erfolgreich gescannt (jeweils Status `empty, confidence=high`).
  Genau 1 Pattern (`public-without-doc`) war in `ainetlinter-rules.json` deaktiviert (`EnforceXmlDocumentation: false`).
  Navigation des Gesamtergebnisses:
  ```text
  Vollstaendigkeitsstatus: nicht konfiguriert
  ...
  ## Navigation
  - operationStatus: `ok`
  - result: available=`false`
  - completeness: `not_configured`
  - next: `request_detail` — Die Entscheidbarkeit des angeforderten Scopes ist begrenzt; Konfiguration oder Scope prüfen und den Aufruf gezielt wiederholen.
  ```
- **Problem**:
  Obwohl 83 % der Patterns erfolgreich geprüft wurden und wertvolle Erkenntnisse lieferten, entwertet das Tool die gesamte Antwort auf `available: false` und `completeness: not_configured`.
  Für einen Agenten wirkt der gesamte Call wie fehlgeschlagen, obwohl die primären Architekturregeln (God-Classes, async void etc.) alle bestanden wurden.
- **Reproduktion**:
  `pattern_detect(targetPath="AiNetLinter.slnx")` mit Default-Konfiguration ausführen.
- **Empfehlung**:
  Die Gesamt-Completeness sollte `partial` oder `complete` sein (mit Vermerk in den Metadaten, dass einzelne Unter-Patterns deaktiviert sind). Nur wenn *alle* angeforderten Patterns unkonfiguriert sind, sollte die Gesamtantwort `not_configured` sein.

---

### [Minor] Befund F-10: `resolve_type_origin` deklariert dekompilierten Typ als "Projekt-Quellcode"
- **Tool(s)**: `resolve_type_origin`
- **Quellcode**:
  - [ResolveTypeOriginTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/TypeResolution/ResolveTypeOriginTool.cs)
- **Evidenz**:
  Aufruf auf Assembly `LOCAL-01`:
  ```text
  [ASSEMBLY] targetPath=C:\Program Files (x86)\Sage\Sage 100\9.0\Shared\Sagede.OfficeLine.Pps.Fertigungsauftrag.dll; origin=decompiled...

  # Typ-Herkunft: `AenderungsIndexSaveOfficeLineException`
  - **Vollqualifizierter Name**: `Sagede.OfficeLine.Pps.Fertigungsauftrag.AenderungsIndexSaveOfficeLineException`
  - **Symbol-Art**: `class`
  - **Assembly**: `Sagede.OfficeLine.Pps.Fertigungsauftrag`
  - **Dateipfad**: `C:\Program Files (x86)\Sage\Sage 100\9.0\Shared\Sagede.OfficeLine.Pps.Fertigungsauftrag.dll`
  - **Herkunft**: Projekt-Quellcode

  ## Navigation
  - origin: `decompiled`
  ```
- **Problem**:
  In der maschinenlesbaren Navigation steht korrekt `origin: decompiled`.
  Im generierten Markdown-Text steht jedoch: `**Herkunft**: Projekt-Quellcode`.
  Dies rührt daher, dass Roslyn die dekompilierte Datei im Adhoc-Workspace als SyntaxTree geladen hat und der Code-Zweig `origin` mit Source verwechselt. Ein Agent, der den Markdown-Text liest, könnte glauben, dass es sich um eine Quellcodedatei im eigenen Projekt handelt.
- **Reproduktion**:
  `resolve_type_origin(typeName="...", targetPath="<dll>")` aufrufen.
- **Empfehlung**:
  Im Text-Renderer bei `origin == decompiled` den Text auf `Dekompilierte Assembly` oder `Assembly-Metadaten` setzen.
