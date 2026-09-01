# Tech-Debt Register: Decompiled Assembly Support

Dieses Register dokumentiert die im 360-Grad-Audit identifizierten technischen Schulden, Fehler und Verbesserungspotenziale zur nachgelagerten Bearbeitung.

| ID | Kategorie | Priorität | Größe | Kurzbeschreibung | Betroffene Datei(en) |
|---|---|:---:|:---:|---|---|
| TD-ASM-001 | Bug | P1 | S | Top-Level-Klassen werfen `InvalidOperationException` in `get_symbol_body` wegen falscher Auflösung von `symbol.ContainingType` | `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs` |
| TD-ASM-002 | Bug | P1 | M | `AssemblyDecompilationCache.Publish` löscht neu erzeugte Cache-Generationen fälschlich im `finally`-Block bei concurrent `TryRead` | `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs` |
| TD-ASM-003 | Bug | P2 | S | Irreführender Vollständigkeitshinweis (`McpSufficiencyHints`) bei `find_references` auf dekompilierten Snapshots ohne Method-Bodies | `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs`, `FindReferencesTool.cs` |
| TD-ASM-004 | Optimierung | P1 | M | Exakter Versionsvergleich in `AssemblyReferenceResolver` verhindert Framework-Assembly-Unification und erzeugt kaskadierende `version_mismatch` | `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs` |
| TD-ASM-005 | Optimierung | P2 | S | Unkontrollierter Namespace-Dump in `InspectAssemblyFormatter` verdrängt wertvolle Typ-/Member-Payloads im 8-KB-Response-Budget | `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs`, `AssemblyAnalysisResponseLimits.Budget.cs` |
| TD-ASM-006 | Missing Feature | P2 | S | `find_assembly_extensions` erzwingt immer `ExpandAssemblyReferences: true`; Parameter `includeReferences` fehlt im MCP-Tool-Schema | `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs` |
| TD-ASM-007 | Optimierung | P3 | S | `get_namespace_tree` gibt bei Assembly-Zielen irreführenden Header `# Solution Overview: Solution (1 Projekte)` aus | `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs` |
| TD-ASM-008 | Dokumentation | P3 | S | `instructions.md` suggeriert universelle `targetType='assembly'`-Unterstützung für alle Tools statt der tatsächlichen 13-Tool-Capability-Matrix | `C:\Users\Ralf\.gemini\antigravity-ide\mcp\AiNetLinter\instructions.md`, `Docs/agent-api.md` |
