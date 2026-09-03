## Primäre Einstiegspunkte

- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs` — bestehender Einstieg in die Assembly-Dekompilation.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs` — bestehende Roslyn-Snapshot-Erzeugung.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs` — MCP-Einstieg für Assembly-Inspektion und Pfadausgabe.

## Betroffene Dateien und Symbole

- Bekannte Bereiche: `Mcp/Assemblies/Analysis`, `Mcp/Tools/AssemblyAnalysis`, `Mcp/Tools/SymbolGraph`.
- Zu verifizieren: `AssemblyDecompilationAdapter`, `AssemblyDecompilationCache`, `AssemblyRoslynWorkspaceFactory`, `IAssemblyBodyContext`, `SourceSymbolBodyResolver`, `InspectAssemblyTool` und Response-Modelle.

## Aufrufer und Abhängigkeiten

- Assembly-Registry/Session-Factories erzeugen und halten die Analyse-Snapshots.
- Assembly-Navigations- und Symbol-Body-Tools konsumieren die Sessions.
- ICSharpCode.Decompiler/`WholeProjectDecompiler` ist die vorgesehene externe Decompilation-Abhängigkeit.

## Relevante Tests, Konfiguration und Dokumentation

- FastTests: `src/AiNetLinter.FastTests/Mcp/Assemblies` und `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis`.
- IntegrationTests: `src/AiNetLinter.IntegrationTests/Mcp/Assemblies`.
- Konfiguration: `src/AiNetLinter/Configuration` und `appsettings*.json`.
- Vertrag: `tasks/decompiled-source-dump/Konzept.md` und `Docs/configuration.md`.

## Invarianten, Risiken und Unsicherheiten

- Decompilation muss atomar über Staging veröffentlicht werden.
- Unvollständige/fehlerhafte Decompilate dürfen nutzbare Teilergebnisse nicht verwerfen.
- Cache-Bereinigung ist Best-Effort bei Dateisperren.
- Referenz-DLLs werden nur als Metadaten verwendet; keine rekursive Volldekompilierung.
- Die konkrete aktuelle Symbol-/Session-Verkabelung wird im MCP-first-Implementierer-Check verifiziert.

## Verifikation

- Noch offen; jeder Rollenbericht ergänzt konkrete MCP-Abfragen, Tests und den Stand nach der letzten Codeänderung.
