# Code-Map: 01-agent-ergonomie-und-assembly-analyse

## Primäre Einstiegspunkte
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs`: Regex-Filter für `data_access` und `fileFilter`-Evaluierung.
- `src/AiNetLinter/Mcp/Tools/CodeGraph/GetSymbolBodyTool.cs`: Rumpfextraktion und Zeilenbegrenzung.
- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs`: Pfadausgabe für dekompilierte Assembly-Treffer.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`: Text- und Structured-Content-Projektion.

## Betroffene Dateien und Symbole
- `AssemblySearchTool.cs`: `MatchDataAccess`, `CompileFileFilter`, `DataAccessRegex`.
- `GetSymbolBodyTool.cs`: `GetSymbolBodyRequest`, `GetSymbolBodyResultDto`, `ExecuteAsync`.
- `AssemblyFindSymbolTool.cs` / `FindSymbolTool.cs`: Relative Pfaderzeugung für Assembly-Dateien.
- `AssemblyAnalysisResponse.cs`: Text-Projektion ohne komplettes Trimming.

## Aufrufer und Abhängigkeiten
- `AssemblyAnalysisToolRegistrations.cs`: Tool-Registrierung für `search_assembly`.
- `SymbolGraphToolRegistrations.cs`: Tool-Registrierungen für `find_symbol`, `get_symbol_body`.
- `AiNetLinter.FastTests`: FastTests für Tools und Formatter.

## Relevante Tests, Konfiguration und Dokumentation
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblySearchToolTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/CodeGraph/GetSymbolBodyToolTests.cs`
- `Docs/agent-api.md`

## Invarianten, Risiken und Unsicherheiten
- Kein Remote-Git oder Hashing.
- Regex-Timeouts (100 ms) einhalten.
- Bestehende Tool-Signaturen abwärtskompatibel halten (optionale Parameter mit Defaults).

## Verifikation
- Gezielte Unit-Tests je Paket
- Abschließende FastTests (`Category!=Stress`) und IntegrationTests (`Category!=Stress`)
- `dotnet build` (0 Warnungen, 0 Fehler)
