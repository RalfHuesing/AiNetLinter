---
task: safeguard
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-06T14:50:00+02:00
---

# Tech-Debt-Log: safeguard

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**
Bewusst `hoch`/`mittel`/`niedrig` (deutsch) statt `CRITICAL`/`MAJOR`/
`MINOR`, um jede Verwechslung mit den blockierenden Findings in
`step-review.md` auszuschließen — kein Eintrag hier führt automatisch zu
einem Fix-Step oder einem neuen Epic. Das entscheidet ausschließlich der
Nutzer (manuell, z. B. durch Ergänzen eines Epics in `roadmap.md` mit
Verweis auf die Tech-Debt-ID).

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsScannerTests.cs` (fehlt) | mittel | Dedizierte Scanner-Tests für `GetViolationsScanner` existieren nicht — Logik nur indirekt über `GetViolationsToolTests` getestet. |
| TD-002 | `src/AiNetLinter/Mcp/*ToolRegistrations.cs` + `Mcp/Tools/SafeguardTool.cs` | niedrig | PathOverride-Trend im 2800-2900-Band: drei Registrierungs-Dateien + ein Tool liegen jetzt alle nahe am 2500-Standardlimit, ab 4. Tool ohne Konsolidierung (Helper-Klasse o. ä.) eng. |
| TD-003 | `src/AiNetLinter/Mcp/McpToolResults.cs` | niedrig | Strukturierter-Output-Pattern nicht generalisiert: `SafeguardTool` ist erstes Tool mit `JsonElement?`-structured-content; gemeinsamer `McpToolResults.Structured<T>`-Helper für künftige Tools sinnvoll. |

## Einträge

### TD-001 — Fehlende dedizierte Tests für `GetViolationsScanner` [Priorität: mittel]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-06)
- **Ort:** `src/AiNetLinter.Tests/Mcp/Tools/GetViolationsScannerTests.cs` (Datei existiert nicht)
- **Befund:** `GetViolationsScanner.BuildViolationsTextAsync` und `FormatReport` werden nur indirekt über `GetViolationsToolTests.ExecuteAsync_*` getestet. Es gibt keine dedizierte Scanner-Test-Datei, die die Format-Logik isoliert prüft (Scope-Filter, Severity-Bucket-Trennung in `AppendSection`, „Keine Dateien im Scope"-Sonderfall, Default-Config-Marker). Der Coder von step-001 hat das im Result dokumentiert und das neue `SafeguardScannerTests.cs` bewusst als Pattern-Vorbild etabliert — die fehlende Scanner-Test-Datei für `GetViolationsScanner` ist damit konsistent beobachtbar, aber out of scope für EPIC-01.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von EPIC-01 (SafeguardScanner). Eine rückwirkende Scanner-Test-Datei für `GetViolationsScanner` ist ein eigenständiger Test-Refactor, der die bestehenden Tool-Tests duplizieren oder restrukturieren würde. Der Planer hat in `step-plan.md` §"Bekannte Ausnahmen" explizit auf diese Beobachtung als Tech-Debt-Kandidat hingewiesen, ohne sie als Step-Auftrag zu definieren.
- **Vorschlag:** Eigenes kleines Epic „Scanner-Tests für Bestandsscanner" in `roadmap.md` ergänzen, das `GetViolationsScannerTests.cs` nach dem `SafeguardScannerTests`-Pattern (AdhocWorkspace-Helper + direkter `FormatReport`-Zugriff via `InternalsVisibleTo`) aufbaut. Falls weitere Bestandsscanner (`FindSymbolTool`/`FindReferencesTool`/etc.) ebenfalls keine dedizierten Tests haben: in derselben Welle mit-aufnehmen.
- **Status:** offen  # offen | erledigt | verworfen — Änderung ist manuell (Nutzer), kein Subagent aktualisiert dieses Feld selbst

### TD-002 — PathOverride-Threshold-Trend im 2800-2900-Band [Priorität: niedrig]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-06)
- **Ort:** `src/AiNetLinter/Mcp/AnalysisToolRegistrations.cs:408` (2870), `src/AiNetLinter/Mcp/SymbolGraphToolRegistrations.cs:438` (2900), `src/AiNetLinter/Mcp/FileStructureToolRegistrations.cs:418` (2890), `src/AiNetLinter/Mcp/ServerMaintenanceToolRegistrations.cs:498` (2860), `src/AiNetLinter/Mcp/McpServerOptionsFactory.cs:428` (2830), `src/AiNetLinter/Mcp/SymbolBodyToolRegistrations.cs:493` (2830), `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs:413` (neu, 2800) — alle in `rules.json` `PathOverrides`
- **Befund:** Mit dem dritten Tool in `AnalysisToolRegistrations` (`AddSafeguard`) und dem ersten Tool mit eigenem `PathOverride`-Eintrag (`SafeguardTool.cs` 2800) liegen jetzt sieben Registrierungs-/Tool-Dateien im 2800-2900-Band — alle nur 300-400 Einheiten über dem Standardlimit 2500. Der Planer hat in `step-002/step-plan.md` "Bekannte Ausnahmen / Entscheidungen" explizit auf das Konsolidierungs-Szenario ("sobald ein 4. Tool dazukommt") hingewiesen — diese Beobachtung ist also im Plan antizipiert, aber noch nicht im Tech-Debt-Log formalisiert.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von EPIC-02 (Tool-Layer-Teil); eine `AnalysisToolHelpers`-Konsolidierung würde alle drei bestehenden `AddXxx`-Methoden in `AnalysisToolRegistrations` mit-umstellen und wäre ein eigenständiger Refactor mit eigenem Review-Risiko. Der Coder hat den 3300-Planwert für `AnalysisToolRegistrations` nicht gebraucht (realer Footprint blieb unter 2870), was den Trend bestätigt, aber noch keinen akuten Schmerz erzeugt.
- **Vorschlag:** Bei EPIC-03 oder einem späteren Schritt, sobald ein viertes Tool in `AnalysisToolRegistrations` dazukommt, den Footprint neu messen. Wenn der reale Wert dann über 3200 steigt: `AnalysisToolHelpers`-Klasse extrahieren (Pattern: generische `AddXxx(tools, mcpState, callLog, name, description, handler)`-Methode, die den `McpServerTool.Create`-Aufruf + `callLog`-Verzweigung kapselt). Falls bis dahin nichts dazukommt: Eintrag kann bei Task-Abschluss verworfen werden, Trend löst sich nicht von selbst auf, ist aber auch nicht dringend.
- **Status:** offen

### TD-003 — Strukturierter-Output-Pattern nicht generalisiert [Priorität: niedrig]

- **Gefunden in:** step-002 (Kritiker-Review vom 2026-08-06)
- **Ort:** `src/AiNetLinter/Mcp/Tools/SafeguardTool.cs:75` (`StructuredContent = JsonSerializer.SerializeToElement(score, SerializerOptions)`)
- **Befund:** `SafeguardTool` ist das erste MCP-Tool im Projekt, das `StructuredContent` (JSON-Schema-2020-12-konformer Output) im `CallToolResult` befüllt — die anderen Tools (`get_violations`, `find_symbol`, etc.) liefern ausschließlich `TextContentBlock`. Der Planer hat in `step-002/step-plan.md` "Bekannte Ausnahmen / Entscheidungen" explizit vermerkt: "Falls eine zukünftige Tool-Erweiterung auch structured content braucht, kann ein `McpToolResults.Structured<T>(value, text)`-Helper nachgezogen werden — out of scope hier." Konzept §"Wo im Projekt"/"Nicht angefasst (bewusst)" hatte das vorher schon ausgeschlossen.
- **Warum nicht sofort gefixt:** Aktuell nur **ein** Tool, das das Pattern nutzt — eine Helper-Abstraktion mit nur einem Aufrufer wäre Over-Engineering und würde `McpToolResults.cs` ohne klaren Mehrwert aufblähen. Solange structured content ein Einzelfall bleibt, ist die Inline-Serialisierung im Tool-Wrapper die ehrlichere Form.
- **Vorschlag:** Sobald ein zweites Tool structured content braucht (naheliegende Kandidaten: `get_violations` für eine maschinenlesbare Violations-Liste, `find_references` für symbol-strukturierte Call-Sites), `McpToolResults.Structured<T>(T value, string text, JsonSerializerOptions? options = null)`-Helper analog zu `McpToolResults.Text(string)` extrahieren. Die `SerializerOptions` (CamelCase, WhenWritingNull) wandern in den Helper als Default. `SafeguardTool` wird dann zum 1-Zeilen-Aufruf.
- **Status:** offen
