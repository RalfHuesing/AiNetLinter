---
task: get-file-tree
type: codemap
maintained_by: planer, coder, kritiker
last_updated: 2026-08-26T23:32:10+02:00
---

# CodeMap: get-file-tree

Task-scoped Landkarte — existiert nur für diesen Task und enthält nur die
Bestandsbereiche, die für die geplante physische MCP-Dateilandkarte relevant
sind. Die Einträge sind Pointer; Details werden im jeweiligen Step-Modus direkt
aus dem aktuellen Bestand gelesen.

## Karte

- **`src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`** — Bestehende Sammelstelle für dateistrukturorientierte MCP-Registrierungen und `projectRoot`-gebundene Tool-Lambdas.
- **`src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs`** — Gemeinsamer Root-Guard-, Registry-Lease- und Load-State-Dispatch mit separatem filesystem-only Callback-Einstieg. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs`** — Projekt-Key-Auflösung, Lease-Lifetime und residenter Serverzustand für die projektgebundene MCP-Adressierung.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/`** — File-Structure-Tools, Scanner-/Record-Muster und der boundary-sichere Root-Resolver für die spätere physische Enumeration. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/FileTreePathResolver.cs`** — Lexikalischer Resolver für den effektiven File-Tree-Root innerhalb des registrierten Projektroots. (zuletzt: step-001)
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeTool.cs`** — Dünnes Tool-Dispatch-Muster für eine dateistrukturorientierte Antwort mit Structured Content und Serverzustand.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/GetIndexScopeScanner.cs`** — Scanner-/Record-Ablage innerhalb des File-Structure-Bereichs als strukturelle Referenz.
- **`src/AiNetLinter/Mcp/Tools/FileStructure/SolutionFileWalker.cs`** — Roslyn-Document-Walker als bewusste Abgrenzungsreferenz, nicht als physischer File-Tree-Collector.
- **`src/AiNetLinter/Baseline/FileSystemWalkOptions.cs`** — Interne unveränderliche Optionenbasis für Tiefe, Standardausschlüsse und Cancellation des physischen Walks. (zuletzt: step-003)
- **`src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs`** — Zentrale physische Traversierung, Standardausschlüsse, Reparse-Point-Schutz, Deduplizierung und partielle Walk-Warnungen mit Options-Einstieg. (zuletzt: step-003)
- **`src/AiNetLinter/Baseline/TreeWalkStats.cs`** — Gemeinsames Warnungs-, Unzugänglichkeits-, Cancellation- und Skip-Metadatenmodell des physischen Walks. (zuletzt: step-003)
- **`src/AiNetLinter/Configuration/PathGlobMatcher.cs`** — Neutraler gemeinsamer Pfad-Glob-Matcher für die Datei- und Web-Filter-Einstiege. (zuletzt: step-003)
- **`src/AiNetLinter/Configuration/FileFilterEvaluator.cs`** — Bestehende Glob- und Ausschlusssemantik mit delegierter gemeinsamer Glob-Grundlage; Directory-Segmentprüfung bleibt eigener Bereich. (zuletzt: step-003)
- **`src/AiNetLinter/Configuration/Config.ValueTypes.cs`** — Bestehendes `FileFiltersConfig`-Modell als Referenz für zentrale Datei-/Verzeichnisfilter.
- **`src/AiNetLinter/Output/PathNormalizer.cs`** — Pfadseparator- und Output-Konventionen; der neue Resolver muss zusätzlich eine echte Root-Grenze gewährleisten.
- **`src/AiNetLinter/Mcp/McpToolResults.cs`** — Gemeinsamer Recoverable-/Error- und Structured-Content-Vertrag für MCP-Antworten.
- **`src/AiNetLinter/Mcp/McpTruncation.cs`** — Vorhandenes Trunkierungsprinzip als Referenz für die eigene strukturierte Completeness-Antwort.
- **`src/AiNetLinter/Mcp/Tools/McpToolRegistrationOptions.cs`** — Zentrale Annotation-Profile für read-only, idempotente und closed-world MCP-Tools.
- **`src/AiNetLinter.FastTests/Baseline/StalenessTreeWalkerTests.cs`** — Component-Tests für Ausschlüsse, Root-Deduplizierung, Warnungen, Tiefe, Cancellation und Reparse-Point-Entscheidungen. (zuletzt: step-003)
- **`src/AiNetLinter.FastTests/Configuration/PathGlobMatcherTests.cs`** — Unit-Tests für die zentrale `*`/`?`/`**`-Pfad-Globsemantik. (zuletzt: step-003)
- **`src/AiNetLinter.FastTests/Configuration/FileFilterEvaluatorTests.cs`** — Unit-Tests für case-insensitive Datei-/Verzeichnisfilter und delegierte Glob-Regressionsfälle. (zuletzt: step-003)
- **`src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs`** — Gefrorener Toolbestand, `projectRoot`-Pflicht sowie Registry-, Load-State- und Filesystem-Dispatch-Verträge. (zuletzt: step-001)
- **`src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/`** — Component-Testbereich für bestehende File-Structure-Toolverträge und spätere File-Tree-Logik.
- **`src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/FileTreePathResolverTests.cs`** — Unit-Testanker für relative Root-Auflösung und Boundary-Fälle des File-Tree-Resolvers. (zuletzt: step-001)
- **`src/AiNetLinter.IntegrationTests/Baseline/FileSystemExclusionHelpersTests.cs`** — Integrationstests für generierte Pfade, sichere Enumeration und den physischen Options-Walk mit zentralen Ausschlüssen. (zuletzt: step-003)
- **`src/AiNetLinter.IntegrationTests/Mcp/McpHandshakeToolRegistrationTests.cs`** — Echte MCP-Handshake-/`tools/list`-Registrierungsprüfung gegen eine Mini-Solution.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs`** — MCP-Wire- und projektgebundene Toolverträge einschließlich Fehler-/Loading-Zuständen.
- **`src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs`** — Stable-Dogfood-Teststrecke gegen das AiNetLinter-Repository.
- **`src/AiNetLinter.IntegrationTests/Mcp/Platform/`** — Prozess-, Read-only- und Repository-MCP-Fixtures für Integrationstests.
- **`src/AiNetLinter.TestKit/TestTempDirectory.cs`** — Zentrale, git-ignorierte Testtemp-Infrastruktur für künstliche Dateibäume und Zugriffs-/Trunkierungsfälle.
- **`Directory.Build.props`, `Directory.Packages.props`, `AiNetLinter.slnx`** — Globale .NET-10-/Nullable-/Warnings-as-errors-/Package- und Solution-Konfiguration.
- **`src/AiNetLinter/*.csproj`, `src/AiNetLinter.FastTests/*.csproj`, `src/AiNetLinter.IntegrationTests/*.csproj`, `src/AiNetLinter.TestKit/*.csproj`** — Projektabhängigkeiten, xUnit-v3-Testaufteilung und Referenzgrenzen für Produktion und Verifikation.
- **`.runsettings` und `tests/AiNetLinter.TestProject.props`** — Gemeinsame Testausgabe- und Testprojekt-Einstellungen.
- **`Docs/agent-api.md`, `Docs/integration.md`, `README.md`, `Docs/ROADMAP.md`** — Produktdokumentation für MCP-Toolreferenz, Projektintegration, Agentenorientierung und Entwicklungshistorie.
