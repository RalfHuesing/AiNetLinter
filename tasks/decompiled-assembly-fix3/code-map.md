## Primäre Einstiegspunkte

- Assembly-Context-Aufbau: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
- Source-Selection: `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/`
- Assembly-Registry-Fallback: `src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs`
- Body-Navigation: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/` und `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs`
- Assembly-Erweiterungen: `src/AiNetLinter/Configuration/AssemblyPathValidation.cs` ist die zentrale interne Prüfung für `.dll`/`.exe`; die vier Verbraucher sind `AnalysisTargetResolver`, `AssemblyAnalysisService`, `ExternalSourceMappingValidator` und `AssemblySourceMatchResolver`.
- Hotspots: `src/AiNetLinter/Mcp/Tools/FileStructure/GetHotspotsTool.cs`, `GetHotspotsScanner.cs` und `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs`.

## Betroffene Dateien und Symbole

- `AssemblyAnalysisContextFactory.TryGetProjectCompilationAsync` liefert neben `Compilation` und Fehlertext begrenzte typisierte Roslyn-Diagnosen. `TryCreateSourceBackedContextAsync` und `CreateSourceProjectContextAsync` führen diese Diagnosen in den Source-Diagnosemetadaten weiter; der dekompilierte Fallback übernimmt sie in `AssemblySourceFallbackMetadata` und damit in `AssemblyOrigin.SourceDiagnostics`. `workspace-failure` bleibt nur der zusätzliche Fallback-Grund.
- `AssemblyBodySyntax` liegt in `Analysis/Bodies/AssemblyBodySyntax.cs` und ist der gemeinsame Helper für `HasUnavailableMember`, Property-`HasNoBody` und Extern-Erkennung. Source- und Assembly-Bodypfad verwenden dieselbe fachliche Prüfung.
- `Analysis/Bodies/` enthält `AssemblyDecompilationSourceText`, `AssemblyDecompiledBodyResolver`, `IAssemblyBodyContext` und `AssemblyBodySyntax`.
- `Analysis/SourceSelection/` enthält `AssemblySourceSelectionOrchestrator`, `AssemblySourceProviderCoordinator`, `AssemblySourceSelectionScope`, `AssemblySourceSelection`, `IAssemblySourceResolver` und `AssemblySourceMatchResolver`. Der Orchestrator erhält eine vorprojizierte `AssemblySourceSelectionConfiguration` statt eines `ExternalSourceConfigurationLoadResult`; dadurch entfällt die direkte schwere Konfigurationsabhängigkeit.
- `SourceSymbolBodyResolver.cs` ist versioniert und verwendet den gemeinsamen Body-Helper. Der Provider-Koordinator ist über `// @covers AssemblySourceProviderCoordinator` in `AssemblyAnalysisToolSupportTests.cs` testseitig abgedeckt.
- `AssemblyAnalysisPathContractTests.cs`, `AssemblyAnalysisRouteTests.cs` und `AssemblyNavigationResponseContractTests.cs` liegen real unter `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/` mit Namespace `AiNetLinter.FastTests.Mcp.Assemblies.Navigation`.
- `AnalysisToolRegistrations` registriert `symbolIdentifier` bei `get_feature_context` und `get_test_context` primär; `symbol` bleibt als benannter Kompatibilitätsalias erhalten. Die Options-Records behalten ihre Positional-Reihenfolge für bestehende interne Aufrufer.

## Aufrufer und Abhängigkeiten

- `AssemblyAnalysisHostFactory` projiziert die geladene externe Konfiguration in `AssemblySourceSelectionConfiguration`, erzeugt den `IAssemblySourceProviderCoordinator` und instanziiert damit den Source-Selection-Orchestrator.
- Die betroffenen FastTests projizieren ihre `ExternalSourceConfigurationLoadResult`-Fixtures ebenfalls explizit; der entfernte `CreateFromSettings`-Pfad wird nicht mehr verwendet.
- `AssemblyAnalysisRegistryEntryFactory` übergibt die vom Context-Factory-Fallback erhaltenen Diagnosen weiter in den Registry-Fallback-Origin.
- `AssemblyAnalysisRegistry.LeaseAsync` verwendet weiterhin ausschließlich `Path.GetFullPath`. Die lokale Registry-Untersuchung reproduzierte keine Alias-/Reparse-/8.3-Doppelgeneration: das Volume-8.3-Query war ohne erhöhte Rechte nicht verfügbar, der Datei-Query unterstützt den verwendeten Parameter nicht und im Projektroot wurden keine Reparse-Punkte gefunden. Daher wurde keine riskante Windows-Handle-Kanonisierung und kein Regressionstest ohne reproduzierten Fehler eingeführt; der Befund bleibt als P2 zurückgestellt.
- `src/AiNetLinter/Mcp/Assemblies/GlobalUsings.cs` und `src/AiNetLinter.FastTests/GlobalUsings.cs` importieren die beiden fachlichen Unter-Namespaces.

## Relevante Tests, Konfiguration und Dokumentation

- Fallback-/Compilation-Regression: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs`, einschließlich eines fehlerhaften, aber weiter nutzbaren Source-Compilations-Snapshots mit typisiertem `CS0246`-Origin.
- Navigation-/Overload-/Lease-/Diagnoseverträge: `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/` sowie die bestehenden Assembly-Analysis-Tooltests.
- Paket-4-Regressionen: `ManagedAssemblyBinaryTests` prüft ein kompiliertes verwaltetes `.exe` sowie ein natives Windows-PE mit typisiertem Metadatenfehler; `GetHotspotsToolTests`, `WiringToolCollectionContractTests` und `McpServerToolBehaviorE2ETests` prüfen Parameter, Bounds, Sortierung, Schema und Wire-Payload. `AssemblyAnalysisTestSupport` hält die JSON-/Text-Helfer für beide Assembly-Testklassen gemeinsam.
- Runtime-/Doku-Verträge: `McpDocumentationSmokeTests`, `McpAgentGuideRegistrationTests`, `Docs/agent-api.md`, `Docs/integration.md` und `.agents/rules/AiNetLinter-McpWorkflow.mdc` führen `.exe`, DTOs, `analysis.bodyAvailability`/`contentMode`, Health-Targetvarianten und Progressive Disclosure zusammen.
- `FileStructureToolRegistrations` wurde beim FileTree-Default nicht geändert: Textbeschreibung und `GetFileTreeTool.DefaultMaxResults` stehen bereits beide auf 200. `Docs/configuration.md` bleibt unverändert, da keine persistente Konfiguration hinzukommt.
- `rules.json`, `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden in diesem Versuch nicht geändert. Die Produktions- und Testordner werden gegen `MaxDirectoryChildren=30` geprüft.

## Invarianten, Risiken und Unsicherheiten

- Verwertbare Source-Compilations mit Roslyn-Diagnosen bleiben `source-backed` und werden `partial`; bei nicht erzeugbarer Compilation bleibt der stabile Fallback-Grund `workspace-failure` erhalten, ergänzt um konkrete typisierte Compilation-Diagnosen.
- Source-backed, `decompiledSignatureOnly` und `decompiledBodyOnDemand` sowie Overload-/Lease-/Literalverträge bleiben unverändert. Bodies werden weiterhin nur innerhalb einer aktiven Lease dekompiliert.
- `get_hotspots` filtert ab `minLinePercentage` (Default 80, Clamp 0–100), begrenzt auf `maxResults` (Default 50, Cap 200) und sortiert die sichtbaren Einträge stabil nach Zeilenzahl absteigend, dann Pfad. StructuredContent weist Gesamt-/Anzeigezahl, effektive Parameter und Trunkierung aus.
- Verwaltete `.exe`-Assemblies werden wie `.dll` über den vorhandenen zentralen Extension-Helper zugelassen; Existenz-, Metadaten- und Sicherheitsgrenzen bleiben unverändert. Native PE-Dateien ohne .NET-Metadaten bleiben ein typisierter, hilfreicher Fehlerpfad ohne Ausführung.
- Die direkte Struktur ist nach der Verschiebung `Analysis=28` Einträge und `FastTests/Mcp/Assemblies=29` Einträge; `Bodies` und `SourceSelection` sind fachlich benannte Unterordner ohne Sammelordner.
- Kein AdhocWorkspace-Fallback für fehlerhafte externe Checkouts, keine Cachepfade in Antworten und keine Assembly-Ausführung wurden eingeführt.
- Unveränderte, scopefremde Violations können außerhalb der betroffenen Pfade bestehen; sie werden nicht durch Suppression oder Akzeptieren im Code behandelt.

## Verifikation

- MCP-first-Kontext: `get_file_tree` Summary sowie `get_feature_context` für `AnalysisTargetResolver`, `AssemblyAnalysisService`, `ExternalSourceMappingValidator`, `AssemblySourceMatchResolver`, `GetHotspotsTool`, `GetHotspotsScanner`, `AnalysisToolRegistrations`, `AssemblyAnalysisContextFactory`, `AssemblyReferenceResolver`, `AssemblyAnalysisRegistry`, `AssemblyAnalysisRegistryEntryFactory`, `GetFeatureContextTool` und `GetTestContextTool`; `find_symbol` für unbekannte Registrierungssymbole; Baseline-`get_violations` für Produktions- und Testpfade.
- Nach der letzten Codeänderung wurden fokussierte FastTests/IntegrationTests, gezielte `get_violations`, `dotnet build --no-restore` und `git diff --check` ausgeführt; anschließend liefen die vollständigen Nicht-Stress-Gates und der Audit-Nachcheck.
- Abschluss-Audit: `find_duplicates` meldete 10 bestehende near-/fuzzy-Kandidaten, ohne eindeutigen Klon im Paket-4-Code; `find_magic_values` meldete 8 eindeutige bestehende Einträge (16 Vorkommen), ohne sicheren tasknahen Korrekturbedarf. `find_dead_code` meldete 37 LOW-Heuristiken und keine HIGH-Funde; die Kandidaten betreffen vor allem dynamische/Interop-Verträge und wurden nicht entfernt.
- Audit-Nachcheck: `find_duplicates` findet im geänderten FileStructure-Produktionsscope 0 und im Assembly-Testscope 0 Cluster; `find_magic_values` findet dort nur den bestehenden `PrimaryCtor-Param`-Literal in `GetClassStructureTool`; `find_dead_code` findet im FileStructure-Scope 0.
- Nach der letzten Codeänderung: gezielte Assembly-/Hotspot-/Wiring-/Agent-Guide-FastTests 119 bestanden, 1 bestehender Agent-Guide-Zeilenumbruchfehler; `get_violations` meldet für FileStructure-Produktion und Assembly-Tests jeweils 0. Der frühere stale Snapshot ist damit durch eine aktualisierte MCP-Abfrage ersetzt.
- Finales projektweites `get_violations`: 10 bestehende Warnungen, davon 8 `AIContextFootprint` in Assembly-/Health-/SymbolGraph-Komponenten und 2 `MaxMethodParameterCount` in `FindSymbolScanner`; keine Fehler und keine Paket-4-Datei betroffen.
- Vollständige Nicht-Stress-Gates nach der Teststruktur-Bereinigung: FastTests 2346 bestanden, 2 übersprungen, 1 bestehender `McpAgentGuideRegistrationTests`-Fehler; IntegrationTests 377 bestanden, 2 Fehler im Vollparallel-/Live-Lauf. Die isolierte Nachprüfung besteht den Whole-Solution-Dogfood-Test 1/1, der Safeguard-Live-Test scheitert reproduzierbar mit Score 0 statt mindestens 5. `dotnet build --no-restore` meldete 0 Warnungen/0 Fehler; `git diff --check` meldet keine Diff-Fehler.
- `roadmap.md`, `execution-log.md` und `tech-debt.md` wurden nicht geändert; alle Code- und Verschiebungsänderungen bleiben uncommitted.
