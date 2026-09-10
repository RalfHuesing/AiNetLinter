# Gruppe C: Symbol-Chaining & Composite-Tools — Befundbericht

## Geprüfte Tools
- `find_symbol`
- `get_symbol_body`
- `get_class_structure`
- `get_file_skeleton`
- `find_references`
- `get_call_tree`
- `get_impact`
- `get_type_hierarchy`
- `find_implementations`
- `get_feature_context`
- `get_test_context`
- `get_assembly_context`

---

### [Major] Befund F-03: `get_feature_context` wiederholt Trimming-Hinweis mehrfach und liefert leere Deklarationsdaten
- **Tool(s)**: `get_feature_context`
- **Quellcode**:
  - [McpUtf8BudgetTrimmer.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Wire/McpUtf8BudgetTrimmer.cs)
  - [McpToolResults.CompositeBudget.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Wire/McpToolResults.CompositeBudget.cs)
  - [GetFeatureContextTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/GetFeatureContextTool.cs)
- **Evidenz**:
  Aufruf von `get_feature_context(symbolIdentifier="AnalysisTargetResolver", targetPath="AiNetLinter.slnx")`:
  Response-Ausschnitt:
  ```text
  # Composite-Antwort (Wire-Budget): AiNetLinter.Mcp.AnalysisTargetResolver
  - **Abschnitt declaration:** Status: complete
  - **Counts:** 0 sichtbare Treffer von 0.
  - **Abschnitt metrics:** Status: complete
  - **Counts:** 0 sichtbare Treffer von 0.
  - **Abschnitt impact:** Status: truncated
  - **Counts:** 6 von 34 statischen Referenzen/Call-Sites zurückgegeben.
  - **Nächster sicherer Schritt (impact):** Abschnitt impact: maxCallers erhöhen und die statischen Call-Sites erneut abfragen. Wire-Budget für Abschnitt impact erreicht; Detailabfrage gezielt wiederholen. Wire-Budget für Abschnitt impact erreicht; Detailabfrage gezielt wiederholen. Wire-Budget für Abschnitt impact erreicht; Detailabfrage gezielt wiederholen. Wire-Budget für Abschnitt impact erreicht; Detailabfrage gezielt wiederholen.
  ...
  ```
- **Problem**:
  1. **Text-Loop / Duplikation**: Der Satz `"Wire-Budget für Abschnitt impact erreicht; Detailabfrage gezielt wiederholen."` wird viermal hintereinander konkateniert. Das erzeugt unnötigen Token-Müll im Kontextfenster des Agenten.
  2. **Fehlende Typ-Deklarationsdaten**: Bei Klassen (wie `AnalysisTargetResolver`) meldet der Abschnitt declaration `Status: complete` und `Counts: 0 sichtbare Treffer von 0`. Ein Agent erfährt weder Signaturen noch Methoden der Klasse aus diesem Composite-Call, obwohl es das primäre "One-Shot-Exploration"-Tool vor Refactorings sein soll.
- **Reproduktion**:
  `get_feature_context(symbolIdentifier="AnalysisTargetResolver", targetPath="<slnx>")` aufrufen.
- **Empfehlung**:
  1. In [McpToolResults.CompositeBudget.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Wire/McpToolResults.CompositeBudget.cs) sicherstellen, dass Truncation-Warnungen dedupliziert werden (`Distinct()` vor dem Anhängen).
  2. Typdeklarationen (z. B. Klassenkopf, Vererbung, primäre Member) in die `declaration`-Projektion von `get_feature_context` aufnehmen, nicht nur Methoden-Symbole.

---

### [Major] Befund F-04: `get_impact` liefert bei Einzelsymbolen exakt identischen Output wie `find_references`
- **Tool(s)**: `get_impact`, `find_references`
- **Quellcode**:
  - [GetImpactTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/GetImpactTool.cs)
  - [SymbolGraphToolRegistrations.cs:43](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs#L43)
- **Evidenz**:
  Aufruf 1: `find_references(symbolIdentifier="AnalysisTargetResolver", maxResults=5)`
  ```text
  src/AiNetLinter.FastTests/Mcp/AnalysisTargetResolverTests.cs:23 - Aufruf von 'AnalysisTargetResolver' in Projekt 'AiNetLinter.FastTests' [handoff=true; id=`source:...`]
  ...
  [34 Treffer gesamt, 5 gezeigt — Pattern verfeinern oder maxResults erhöhen]
  ```
  Aufruf 2: `get_impact(symbolIdentifier="AnalysisTargetResolver", maxResults=5)`
  ```text
  src/AiNetLinter.FastTests/Mcp/AnalysisTargetResolverTests.cs:23 - Aufruf von 'AnalysisTargetResolver' in Projekt 'AiNetLinter.FastTests' [handoff=true; id=`source:...`]
  ...
  [34 Treffer gesamt, 5 gezeigt — Pattern verfeinern oder maxResults erhöhen]
  ```
- **Problem**:
  Ein Agent erwartet bei `get_impact` eine semantische Auswirkungsanalyse (welche downstream-Projekte sind betroffen, welche Tests brechen potenziell, Risikoeinstufung). Wenn `symbolIdentifier` übergeben wird, delegiert `get_impact` jedoch einfach an denselben Call-Site-Visitor wie `find_references`. Dies führt zu Verwirrung ("Warum gibt es zwei Tools mit unterschiedlichem Namen, die identischen Output liefern?").
- **Reproduktion**:
  Beide Tools mit identischem `symbolIdentifier` und `maxResults` aufrufen.
- **Empfehlung**:
  In der Tool-Description von `get_impact` klarstellen, dass der Einzelsymbol-Modus die Call-Site-Ebene darstellt, oder `get_impact` im Einzelsymbol-Modus mit Aggregatdaten anreichern (z. B. Anzahl betroffener Downstream-Projekte und direkt zugeordneter Testmethoden).

---

### [Minor] Befund F-11: `get_assembly_context` druckt Paging-Token im Markdown-Text nicht ab
- **Tool(s)**: `get_assembly_context`
- **Quellcode**:
  - [AssemblyAnalysisContextTool.cs](file:///c:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextTool.cs)
- **Evidenz**:
  Aufruf von `get_assembly_context(maxResults=3, targetPath="<LOCAL-01>")`:
  ```text
  Assembly-Kontext: 3 von 48
  Scope: root; Vollständigkeit: partial
  Antwort gekürzt; continuationToken für die Fortsetzung verwenden.

  ## Navigation
  - completeness: `truncated`
  - next: `request_detail` — Scope oder Detaillevel verfeinern und die Antwort gezielt wiederholen.
  ```
- **Problem**:
  Im Text steht der Satz `"Antwort gekürzt; continuationToken für die Fortsetzung verwenden."`, aber der konkrete Token-Wert (wie `v1.5.5E...` in `search_assembly`) wird im sichtbaren Markdown-Text gar nicht angegeben! Ein Agent, der nur den Textblock liest, weiß nicht, wie der Token lautet.
- **Reproduktion**:
  `get_assembly_context` mit niedrigem `maxResults` aufrufen.
- **Empfehlung**:
  Den generierten `continuationToken` wie bei `search_assembly` explizit in den Text aufnehmen: `continuationToken=<token>`.

---

### [Positiv / Best Practice] Highlights in Gruppe C
1. **Stabile Symbol-IDs**: Sowohl Source (`source:Qzpc...`) als auch Assembly (`assembly:Qzpc...`) nutzen robuste, basierte IDs, die den TargetPath und SHA256-Fingerprint einbetten.
2. **Skeleton Map**: `get_file_skeleton` liefert extrem kompakte Signaturen mit Symbol-IDs direkt in C#-Codekommentaren (`/* id:... */`). Ein Agent kann die Struktur einer 1000-Zeilen-Datei erfassen und gezielt einzelne Methoden mit `get_symbol_body` nachladen.
3. **Paging & Windowing**: `get_symbol_body` unterstützt `startLine` und `endLine`, wodurch auch gigantische generierte Methoden häppchenweise ohne Kontextüberlauf gelesen werden können.
