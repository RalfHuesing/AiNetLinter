---
status: completed
type: step-result
task: ainetlinter-feedback-r1
step: "005"
title: "Teil A: Neues MCP-Tool get_class_structure"
epic: EPIC-05
coded_by: coder
coded_by_model: gemini-3.7-flash
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-15T19:30:00+02:00
related_to:
  - tasks/ainetlinter-feedback-r1/step-005/step-plan.md
---

# Step 005: Teil A — Neues MCP-Tool get_class_structure — Ergebnis

## Was wurde geändert

1. **Models (`GetClassStructureModels.cs`):**
   - `ClassStructureMemberEntry` (Kind, Name, Visibility, StartLine, EndLine, LineCount, Signature, FilePath).
   - `ClassStructurePayload` (TypeName, Kind, Files, TotalLines, MemberCount, Members).
2. **Tool (`GetClassStructureTool.cs`):**
   - Symbol-Auflösung über `FindReferencesTool.ResolveSymbolAsync`.
   - Extraktion von Typ-Kind (inkl. `record struct` / `record class`), partial declaration files und Zeilensumme.
   - Filterung und Extraktion von Membern (Kind, Name, Visibility, Start-/End-Zeile, Zeilenanzahl und Signatur).
   - Sortierung nach `sortBy` (`"lines"` [Default], `"kind"`, `"name"`).
   - Markdown-Formatierung (Header + Tabelle) und `ClassStructurePayload`.
3. **Tool-Registrierung (`FileStructureToolRegistrations.cs`):**
   - Registrierung von `get_class_structure` mit Parametern `symbol` (Pflicht) und `sortBy` (Default `"lines"`).
4. **Overview-Resource (`OverviewResourceRegistration.cs`):**
   - Tool-Summary für `get_class_structure` hinzugefügt.
5. **Tests (`GetClassStructureToolTests.cs`):**
   - 7 Tests für Argument-Validierung, Symbol-Auflösung, Typ-Header, Member-Tabelle, Partial-Classes-Kombination, Sortierung und Structured-JSON-Payload.
   - `OverviewResourceRegistrationTests` aktualisiert.

## Verifikation

- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~GetClassStructureToolTests`: 7/7 bestanden.
- `dotnet test src/AiNetLinter.FastTests --filter FullyQualifiedName~OverviewResourceRegistrationTests`: 5/5 bestanden.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`: 1342/1342 bestanden.
