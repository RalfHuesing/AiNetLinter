---
status: draft
task: decompiled-assembly-test
datum: 2026-08-31
bereich: src/AiNetLinter/Mcp
---

# Konzept: Test- und Basiskorrekturen für MCP-Assembly- und Tool-Filter

## Ziel und Nutzen

Dieses Test- und Teilkonzept isoliert drei eigenständige, risikoarme Korrekturen aus der Assembly- und Werkzeug-Analyse, um Filtertreue, Formatunterstützung und Parameterverträge des MCP-Servers gezielt zu verifizieren:
1. Korrekte syntaktische Filterung nach `receiverType` in `find_assembly_extensions`.
2. Unterstützung verwalteter .NET-Executables (`.exe`) neben `.dll` in allen Assembly-Pfadvalidierern.
3. Korrekte Auswertung von `treeDepth` und kompakte Ausgabe im Summary-Modus von `get_file_tree`.

## Verifizierte Evidenz

| Befundbereich | Live-/Code-Evidenz | Disposition |
| --- | --- | --- |
| Receiver-Filter | `find_assembly_extensions` auf `San.OfficeLine.Core.dll` ergab ohne Filter und mit `receiverType="Receiver_404"` jeweils 188 Treffer; der Filter auf den ersten Methodenparameter wurde nicht angewendet. | P1 – beheben |
| Assembly-Formate | Validierer (`AnalysisTargetResolver`, `AssemblyAnalysisService`, `ExternalSourceMappingValidator`, `AssemblySourceMatchResolver`) prüfen starr auf `.dll`, schließen jedoch verwaltete `.exe`-Dateien mit IL/CLI-Metadaten aus. | P2 – beheben |
| `get_file_tree` | `treeDepth` wird im Scanner nicht ausgewertet; `summary` ignoriert Verzeichnisbeschränkungen und liefert auch bei `maxResults=20` hunderte Verzeichniseinträge. | P1 – beheben |

## Betroffene Bereiche

- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisService.cs`
- `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Validation/ExternalSourceMappingValidator.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Resolution/AssemblySourceMatchResolver.cs`
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeScanner.cs`
- `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileTreeTool.cs`
- `src/AiNetLinter/Mcp/Tools/FileStructure/FileTreeAccumulator.cs`

## Zielvertrag und Muss-Kriterien

1. `receiverType` schränkt `find_assembly_extensions` unabhängig von einer Consumer-Projektauflösung syntaktisch auf den ersten Extension-Parameter (`this`-Parameter) ein.
2. Verwaltete `.exe`-Dateien werden neben `.dll` als gleichwertige Assembly-Ziele akzeptiert.
3. `get_file_tree.treeDepth` wird zuverlässig ausgewertet (inkl. `0` für Root-Ebene); `summary` bleibt kompakt und aggregiert statt Verzeichnisse unbegrenzt aufzulisten.

## Nicht-Ziele und Scope-Grenzen

- Keine Decompiler-Body-Generierung oder On-demand Body-Abrufe in diesem Scope.
- Keine Änderung am `ExternalSourceSnapshotMaterializer` oder Git-Checkout.
- Keine Restrukturierung des globalen Server-Health-Payloads.

## Umsetzungspakete

### Task 1: Receiver-Filter reparieren (`find_assembly_extensions`)
- `AssemblyExtensionSearchOptions` in `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisModels.cs` um `ReceiverType` erweitern.
- `FindAssemblyExtensionsTool.BuildResult` übergibt `arguments.ReceiverType`.
- In `AssemblyAnalysisService.FindExtensions` nach Namespace und Name einen `MatchesReceiverType`-Filter auf `pair.Method.Parameters[0].Type` anwenden.
- Die Normalisierung entfernt nur Darstellungspräfixe wie `global::`. Ein unqualifizierter Suchwert wird gegen `ITypeSymbol.Name`, ein qualifizierter gegen den C#-Fehlerformatnamen verglichen; kein unsicheres `EndsWith` und keine case-insensitive Semantik.

### Task 2: `.exe` als verwaltetes Assembly-Ziel unterstützen
- Eine gemeinsame, interne Prüffunktion für erlaubte Assembly-Erweiterungen (`.dll`, `.exe`) zentral bereitstellen; redundante `.dll`-Einzelfilter ersetzen.
- Betroffen sind `AnalysisTargetResolver`, `AssemblyAnalysisService`, `ExternalSourceMappingValidator` und `AssemblySourceMatchResolver`.
- Zulässig sind `.dll` und `.exe`; Existenz- und Metadatenprüfung (CLI/PE-Header) bleiben unverändert. Fehlermeldungen weisen auf beide unterstützten Erweiterungen hin.

### Task 3: `get_file_tree`-Parameter und Summary-Vertrag korrigieren
- In `GetFileTreeScanner.Scan` die effektive Tiefe über `input.MaxDepth ?? input.TreeDepth` ermitteln; der Wert `0` bedeutet Root-Ebene und nicht „unbegrenzt“. Bei gleichzeitiger Angabe hat `maxDepth` Vorrang.
- `FileTreeAccumulator.Build` trennt vollständige Aggregation von ausgegebenen Directory-Entries.
- Im Modus `summary` werden nur Summary-Zahlen und begrenzte Top-Level-Aggregate ausgegeben; `maxResults` begrenzt auch sichtbare Verzeichnisse zuverlässig.

## Test- und Verifikationsvertrag

- **Receiver-Filter Tests**: `AssemblyAnalysisToolTests.cs` – Receiver ohne Treffer vs. qualifizierter/unqualifizierter Treffer.
- **Assembly-Erweiterung Tests**: Unit-Tests mit `.dll` und `.exe` Pfaden sowie Validierungsfehlern für nicht unterstützte Dateiendungen.
- **FileTree Tests**: `GetFileTreeScannerTests.cs` – `treeDepth=0/1/2`, Vorrang von `maxDepth`, Trunkierung im Summary-Modus.
- **Abschluss-Gates**:
  ```bash
  dotnet build
  dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  ```
