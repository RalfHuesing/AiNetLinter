# Feature Request: get_call_tree mit optionaler Richtung (Incoming/Outgoing)

## 1. Ziel & Kontext
Aktuell visualisiert das MCP-Tool `get_call_tree` (`GetCallTreeTool`) ausschließlich eingehende Aufrufe (**Caller-Tree**: "Wer ruft mich auf?"). 
Für tiefere Codeanalysen, Refactorings und Architekturverständnis komplexer Methoden soll ein optionaler Parameter `direction` eingeführt werden, um auch ausgehende Aufrufe (**Callee-Tree**: "Wen ruft diese Methode auf?") transitiv als hierarchischen Baum darzustellen.

## 2. Anforderungen
- **Parameter**:
  - `direction`: `"incoming"` (Default) | `"outgoing"` | `"both"` (optional)
- **Verhalten bei `direction: "incoming"`**:
  - Unverändertes bisheriges Verhalten (Traversierung über `FindReferencesTool` / Call-Sites).
- **Verhalten bei `direction: "outgoing"`**:
  - Syntax- und SemanticModel-Traversierung über die Methodenaufrufe (InvocationExpressions / MemberAccess / ObjectCreation) innerhalb des Methoden-Bodies des Zielsymbols.
  - Transitiv bis zur konfigurierten `depth` (Default 2, hard cap 5) mit `topN`-Kappung pro Ebene.
  - Berücksichtigung des 250-Knoten-Hardcaps.
- **Ausgabeformate**:
  - Unterstützung für ASCII-Baum (`format: "ascii"`) und Mermaid-Diagramm (`format: "mermaid"`).
- **Abwärtskompatibilität & IsErrorPolicy**:
  - Ungültige `direction`-Werte liefern recoverable `INVALID_ARGUMENT`.
  - Ohne `direction` bleibt das Default-Verhalten `"incoming"` 100% erhalten.

## 3. Betroffene Komponenten
- `src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeTool.cs`
- `src/AiNetLinter/Mcp/Tools/CallTree/GetCallTreeModels.cs`
- `src/AiNetLinter/Mcp/Tools/CallTree/CallGraphTraversal.cs`
- `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/`
- `Docs/agent-api.md`
