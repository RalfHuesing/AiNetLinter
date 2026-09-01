## Primäre Einstiegspunkte

- Assembly-Context-Aufbau: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
- Source-Selection: `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/`
- Assembly-Registry-Fallback: `src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs`
- Body-Navigation: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/` und `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`

## Betroffene Dateien und Symbole

- `AssemblyAnalysisContextFactory.TryGetProjectCompilationAsync` liefert neben `Compilation` und Fehlertext begrenzte typisierte Roslyn-Diagnosen. `TryCreateSourceBackedContextAsync` und `CreateSourceProjectContextAsync` führen diese Diagnosen in den Source-Diagnosemetadaten weiter; der dekompilierte Fallback übernimmt sie in `AssemblySourceFallbackMetadata` und damit in `AssemblyOrigin.SourceDiagnostics`. `workspace-failure` bleibt nur der zusätzliche Fallback-Grund.
- `AssemblyBodySyntax` liegt in `Analysis/Bodies/AssemblyBodySyntax.cs` und ist der gemeinsame Helper für `HasUnavailableMember`, Property-`HasNoBody` und Extern-Erkennung. Source- und Assembly-Bodypfad verwenden dieselbe fachliche Prüfung.
- `Analysis/Bodies/` enthält `AssemblyDecompilationSourceText`, `AssemblyDecompiledBodyResolver`, `IAssemblyBodyContext` und `AssemblyBodySyntax`.
- `Analysis/SourceSelection/` enthält `AssemblySourceSelectionOrchestrator`, `AssemblySourceProviderCoordinator`, `AssemblySourceSelectionScope`, `AssemblySourceSelection`, `IAssemblySourceResolver` und `AssemblySourceMatchResolver`. Der Orchestrator erhält eine vorprojizierte `AssemblySourceSelectionConfiguration` statt eines `ExternalSourceConfigurationLoadResult`; dadurch entfällt die direkte schwere Konfigurationsabhängigkeit.
- `SourceSymbolBodyResolver.cs` ist versioniert und verwendet den gemeinsamen Body-Helper. Der Provider-Koordinator ist über `// @covers AssemblySourceProviderCoordinator` in `AssemblyAnalysisToolSupportTests.cs` testseitig abgedeckt.
- `AssemblyAnalysisPathContractTests.cs`, `AssemblyAnalysisRouteTests.cs` und `AssemblyNavigationResponseContractTests.cs` liegen real unter `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/` mit Namespace `AiNetLinter.FastTests.Mcp.Assemblies.Navigation`.

## Aufrufer und Abhängigkeiten

- `AssemblyAnalysisHostFactory` projiziert die geladene externe Konfiguration in `AssemblySourceSelectionConfiguration`, erzeugt den `IAssemblySourceProviderCoordinator` und instanziiert damit den Source-Selection-Orchestrator.
- Die betroffenen FastTests projizieren ihre `ExternalSourceConfigurationLoadResult`-Fixtures ebenfalls explizit; der entfernte `CreateFromSettings`-Pfad wird nicht mehr verwendet.
- `AssemblyAnalysisRegistryEntryFactory` übergibt die vom Context-Factory-Fallback erhaltenen Diagnosen weiter in den Registry-Fallback-Origin.
- `src/AiNetLinter/Mcp/Assemblies/GlobalUsings.cs` und `src/AiNetLinter.FastTests/GlobalUsings.cs` importieren die beiden fachlichen Unter-Namespaces.

## Relevante Tests, Konfiguration und Dokumentation

- Fallback-/Compilation-Regression: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs`, einschließlich eines fehlerhaften, aber weiter nutzbaren Source-Compilations-Snapshots mit typisiertem `CS0246`-Origin.
- Navigation-/Overload-/Lease-/Diagnoseverträge: `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/` sowie die bestehenden Assembly-Analysis-Tooltests.
- `rules.json`, `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden in diesem Versuch nicht geändert. Die Produktions- und Testordner werden gegen `MaxDirectoryChildren=30` geprüft.

## Invarianten, Risiken und Unsicherheiten

- Verwertbare Source-Compilations mit Roslyn-Diagnosen bleiben `source-backed` und werden `partial`; bei nicht erzeugbarer Compilation bleibt der stabile Fallback-Grund `workspace-failure` erhalten, ergänzt um konkrete typisierte Compilation-Diagnosen.
- Source-backed, `decompiledSignatureOnly` und `decompiledBodyOnDemand` sowie Overload-/Lease-/Literalverträge bleiben unverändert. Bodies werden weiterhin nur innerhalb einer aktiven Lease dekompiliert.
- Die direkte Struktur ist nach der Verschiebung `Analysis=28` Einträge und `FastTests/Mcp/Assemblies=29` Einträge; `Bodies` und `SourceSelection` sind fachlich benannte Unterordner ohne Sammelordner.
- Kein AdhocWorkspace-Fallback für fehlerhafte externe Checkouts, keine Cachepfade in Antworten und keine Assembly-Ausführung wurden eingeführt.
- Unveränderte, scopefremde Violations können außerhalb der betroffenen Pfade bestehen; sie werden nicht durch Suppression oder Akzeptieren im Code behandelt.

## Verifikation

- MCP-first-Kontext: `get_feature_context` für Context-Factory, Source-Selection-Orchestrator, Provider-Koordinator, Decompiled-Body-Resolver und `SourceSymbolBodyResolver`; Baseline-`get_violations` für Produktions- und Testpfad.
- Nach der letzten Codeänderung: fokussierte FastTests 35/35, fokussierte IntegrationTests 17/17 und `dotnet build --no-restore` mit 0 Warnungen/0 Fehlern.
- Abschluss-Audit: `find_duplicates` ohne Cluster, `find_magic_values` ohne Treffer; `find_dead_code` meldet nur die zwei bestehenden LOW-Heuristiken `AssemblyAnalysisRegistry.ResourceHealth` und `AssemblyOrigin.Kind`, keine HIGH-Funde.
- Finales `get_violations`: `src/AiNetLinter.FastTests/Mcp/Assemblies` 0; `src/AiNetLinter/Mcp/Assemblies/Analysis` 2 bestehende AIContext-Warnungen (Registry-Eviction 2513, Reference-Expander 2502); `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis` 2 bestehende AIContext-Warnungen (FindAssemblyExtensionsTool 2554, InspectAssemblyTool 2566). Die bearbeiteten P1s und die Factory-MaxLineCount sind nicht mehr enthalten.
- Vollständige Nicht-Stress-Gates: FastTests 2341 bestanden, 2 übersprungen, 1 bestehender `McpAgentGuideRegistrationTests`-Fehler; IntegrationTests 376 bestanden, 2 bestehende Live-/Dogfood-Fehler. `git diff --check` meldet keine Diff-Fehler.
- `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden nicht geändert; alle Code- und Verschiebungsänderungen bleiben uncommitted.
