# 20 – find_duplicates

## 1. Tool-Steckbrief & Agenten-Rolle
- **Name:** `find_duplicates`
- **Agenten-Priorität:** Prio 20 (Mittel – DRY-Audit & Refactoring-Vorbereitung)
- **Hauptzweck:** Sucht nach duplizierten oder strukturell ähnlichen Methodenclustern (Token-basierte Clone-Detection, Jaccard-N-Gramm, Cosine-Similarity).
- **Wichtigste Parameter:**
  - `targetPath` (Pflicht, String): Absoluter Pfad zur `.sln`/`.slnx`.
  - `mode` (Optional, String, Default `clone`): `clone`, `refactoring-drift`, `structural`.
  - `similarityThreshold` (Optional, String, Default `fuzzy`): `exact` (>=0.95), `near` (>=0.80), `fuzzy` (>=0.65).
  - `minTokens` (Optional, Int, Default 30).
  - `scopeDir` (Optional, String): Pfadfilter.
  - `maxResults` (Optional, Int, Default 20).

---

## 2. Test-Samples & Rohe Tool-Outputs

### Sample A: Duplicate-Scan über `src/AiNetLinter` (`similarityThreshold: "fuzzy"`, `maxResults: 5`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeDir": "src/AiNetLinter",
  "similarityThreshold": "fuzzy",
  "maxResults": 5
}
```
- **Tool-Output (Rohdaten nach Behebung):**
```text
5 von 19 Duplikat-Kandidatencluster(n) (2395 Methoden gescannt):
Evidenzgrenze: statische Aehnlichkeit innerhalb des angeforderten Source-Scopes; keine semantische oder Laufzeitgleichheit.
Countercheck: Reflection, DI, Generatoren, dynamic und manuelle Semantikpruefung.

## 1. exact (Score 1,00, 2 Methoden)
- AiNetLinter.Mcp.Tools.SymbolGraph.FindReferencesTool.ApplyFinalResponseBudget(ModelContextProtocol.Protocol.CallToolResult, int) (src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:148, 35 Tokens) — candidate, confidence=medium
- AiNetLinter.Mcp.Tools.SymbolGraph.GetTypeHierarchyTool.ApplyFinalResponseBudget(ModelContextProtocol.Protocol.CallToolResult, int) (src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs:96, 35 Tokens) — candidate, confidence=medium

## 2. near (Score 0,91, 2 Methoden)
- AiNetLinter.Mcp.Tools.ServerMaintenance.Projection.AssemblyHealthProjection.ToPublicEntry(AiNetLinter.Mcp.Tools.ServerMaintenance.AssemblyHealthEntry) (src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs:106, 67 Tokens) — candidate, confidence=medium
- AiNetLinter.Mcp.Tools.ServerMaintenance.Projection.AssemblyHealthProjection.ToTargetEntry(AiNetLinter.Mcp.Tools.ServerMaintenance.AssemblyHealthEntry) (src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs:125, 63 Tokens) — candidate, confidence=medium

## 3. near (Score 0,86, 2 Methoden)
- AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisResponseLimits.ProjectResponseBudget(AiNetLinter.Mcp.Tools.AssemblyAnalysis.InspectAssemblyPayload, bool, System.Func<AiNetLinter.Mcp.Tools.AssemblyAnalysis.InspectAssemblyPayload, bool>?, AiNetLinter.Mcp.Tools.AssemblyAnalysis.ResponseBudgetOptions) (src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:14, 129 Tokens) — candidate, confidence=high
- AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisResponseLimits.ProjectResponseBudget(AiNetLinter.Mcp.Tools.AssemblyAnalysis.FindAssemblyExtensionsPayload, System.Func<AiNetLinter.Mcp.Tools.AssemblyAnalysis.FindAssemblyExtensionsPayload, bool>?, AiNetLinter.Mcp.Tools.AssemblyAnalysis.ResponseBudgetOptions) (src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:39, 127 Tokens) — candidate, confidence=high

## 4. near (Score 0,85, 2 Methoden)
- AiNetLinter.Mcp.Tools.TypeResolution.ResolveTypeOriginTool.FindWithArity(Microsoft.CodeAnalysis.Compilation, string) (src/AiNetLinter/Mcp/Tools/TypeResolution/ResolveTypeOriginTool.cs:187, 66 Tokens) — candidate, confidence=medium
- AiNetLinter.Mcp.Tools.TypeResolution.ResolveTypeOriginTool.FindWithArity(Microsoft.CodeAnalysis.IAssemblySymbol, string) (src/AiNetLinter/Mcp/Tools/TypeResolution/ResolveTypeOriginTool.cs:196, 66 Tokens) — candidate, confidence=medium

## 5. near (Score 0,84, 2 Methoden)
- AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisResponseLimits.TryRemoveLastReferenceSession(ref AiNetLinter.Mcp.Tools.AssemblyAnalysis.InspectAssemblyPayload, bool) (src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:177, 88 Tokens) — candidate, confidence=high
- AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisResponseLimits.TryRemoveLastReferenceSession(ref AiNetLinter.Mcp.Tools.AssemblyAnalysis.FindAssemblyExtensionsPayload, bool) (src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:255, 91 Tokens) — candidate, confidence=high
[19 Cluster gesamt, 5 gezeigt — maxResults erhoehen oder scopeDir eingrenzen]
```

### Sample B: Gescopter Lauf mit 0 Duplikaten (`scopeDir: "src/AiNetLinter/Diagnostics"`)
- **Aufruf-Parameter:**
```json
{
  "targetPath": "c:\\Daten\\Entwicklung\\Ralf\\AiNetLinter\\AiNetLinter.slnx",
  "scopeDir": "src/AiNetLinter/Diagnostics"
}
```
- **Tool-Output (Rohdaten):**
```text
Keine Duplikat-Cluster bzw. Kandidatencluster im angeforderten Scope gefunden (13 Methoden gescannt); kein globaler Clean-Claim.
```

---

## 3. Effizienz- & SNR-Bewertung (gemäß AiNetLinter-Richtlinien)
- **Token-Footprint:**
  - Sample A (5 Cluster mit 10 Methoden): ~3.2 KB (~760 Token) statt ehemals ~5.5 KB (~1.300 Token).
  - Sample B (0 Duplikate): 134 Bytes (~32 Token).
- **SNR-Bewertung:**
  - **Behoben:** Die vormalige redundante Wiederholung von `Evidenzgrenze:` und `Countercheck:` unter jeder einzelnen Methode wurde architektonisch bereinigt und wird nun einmalig im Header ausgegeben.
  - Dadurch wurden pro Cluster-Mitglied 2 repetitive Zeilen eliminiert (**Token-Einsparung: ~42%**).
- **Content-Only-Hard-Cut:** Konform.

---

## 4. Composability & Chaining-Validierung (Input/Output-Passgenauigkeit)
- **Erzeugte Ausgabefelder:**
  - Jede Methode nennt den Dateipfad mit Startzeile in Klammern: `(src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:148, 35 Tokens)`.
  - Der Pfad `src/...cs:148` kann direkt per RegEx extrahiert und im Batch an `get_symbol_body` übergeben werden.
- **Chaining-Matrix:**
  | Ausgabefeld | Ziel-Tool | Ziel-Parameter | Zero-Transformation? | Status |
  |---|---|---|---|---|
  | `Pfad:Zeile` | `get_symbol_body` | `symbolIdentifiers` | ⚠️ In Klammern eingebettet | Pfad aus `(Pfad:Zeile, X Tokens)` extrahierbar |
  | `Pfad:Zeile` | Editor | Quellcode-Vergleich | ⚠️ Extraktion nötig | Zeile für Diffing vorhanden |

---

## 5. Fazit & Status nach Behebung
- **Prädikat:** **Sehr gut (bereinigt)**
- **Durchgeführte Optimierung:**
  - `Evidenzgrenze` und `Countercheck` wurden in `DuplicateDetectionTool.cs` und `RefactoringDriftResponseBuilder.cs` in den Header verlagert (inkl. Unit-Tests zur Sicherstellung).
- **Offener Punkt (für künftige Abstimmung):**
  - Ob die Pfadausgabe künftig außerhalb der Klammern formatiert werden soll (z. B. `— src/File.cs:148 (35 Tokens)`), ist als separates Feature abzustimmen.
