# Gruppe E: Cross-Tool-Konsistenz & Navigation — Befundbericht

## Geprüfte Dimensionen
- Status- und Vollständigkeitssignale (`completeness`, `result.available`)
- Navigation-Block (`operationStatus`, `result`, `next`)
- Semantische Konsistenz über Tools hinweg
- Namenskonventionen

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
