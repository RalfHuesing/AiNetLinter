# Audit-Report 07: Dokumentation, Agent-Guide & Workflow-Regel-Alignment

**SubAgent:** SubAgent 7 (Documentation & Governance)  
**Status:** Abgeschlossen  
**Prüfdatum:** 2026-08-31  
**Geprüfte Artefakte:** `Docs/agent-api.md`, `Docs/mcp-bootstrap.md`, `Docs/integration.md`, `.agents/rules/AiNetLinter-McpWorkflow.mdc`, `instructions.md`, Tool-Schemas in `mcp/AiNetLinter/`

---

## 1. Getestete Szenarien & Konsistenzprüfung

### 1.1 `ainetlinter://agent-guide` & `Docs/mcp-bootstrap.md`
- **Abgleich:** Der integrierte Bootstrap-Guide (`ainetlinter://agent-guide`) stimmt vollständig mit der statischen Dokumentation überein.
- **Laufzeit-Pfad:** Der dynamische Laufzeitblock am Ende von `agent-guide` löst den tatsächlichen Pfad der `AiNetLinter.exe` zur Laufzeit korrekt auf.
- **Definitionsdatei:** Das Template für `ainetlinter.project.json` (`solution` und `rules` als Pflichtfelder) ist überall einheitlich beschrieben.

### 1.2 MCP-Workflow-Regeln (`.agents/rules/AiNetLinter-McpWorkflow.mdc`)
- Verbindliche Prioritäten für semantische C#-Werkzeuge sind klar und unmissverständlich definiert.
- Die Leitlinie zur Token-Schonung bei `get_file_tree` (`view="summary"` statt `view="files"` auf Root-Ebene) verhindert reproduzierbar Kontext-Überläufe.
- Trennung zwischen zielgebundenen Tools (`targetType`, `targetPath`) und ungebundenen Tools (`get_server_health`, `report_observability_feedback`) ist exakt dargelegt.

---

## 2. Befunde & Dokumentations-Drift

### Befund DOC-001 (S2 / U0 / P2): Veraltetes JSON-RPC Beispiel in `Docs/agent-api.md`
- **Beschreibung:** In `Docs/agent-api.md` (Zeilen 731–742) zeigt das Beispiel für einen `tools/call` an `find_symbol` nur die Argumente `namePatterns` und `maxResults`. Die Pflichtparameter `targetType` und `targetPath` fehlen im Beispiel-Payload.
- **Auswirkung:** Übernimmt ein LLM-Agent dieses Beispiel wörtlich, schlägt der Aufruf mit `INVALID_ARGUMENT: Der Parameter 'targetType' ist erforderlich` fehl.
- **Empfehlung:** Ergänzung des Beispiels um `"targetType": "project"` und `"targetPath": "/pfad/zum/projektroot"`.
- **Klassifizierung:** Schweregrad `S2` (Mittel/Dokumentationsfehler), Umfang `U0` (Lokal), Dringlichkeit `P2`.

### Befund DOC-002 (S3 / U0 / P3): Diskrepanz beim `maxResults`-Default in `get_file_tree`
- **Beschreibung:** In `get_file_tree.json` ist im JSON-Schema `"default": 200` hinterlegt, während im Freitext der Beschreibung `"(Default 100, Maximum 2000)"` dokumentiert ist.
- **Empfehlung:** Angleichung von Schema-Default und Textbeschreibung auf einen einheitlichen Wert (z. B. 100 oder 200).
- **Klassifizierung:** Schweregrad `S3` (Minor), Umfang `U0` (Lokal), Dringlichkeit `P3`.

---

## 3. Fazit SubAgent 7
Die Dokumentation und Regelwerke sind weitgehend synchron und von außergewöhnlich hoher Qualität für agentische Workflows. Die Behebung von DOC-001 stellt sicher, dass auch Agenten ohne Vorwissen sofort fehlerfreie JSON-RPC Payloads formulieren können.
