# Epic 7 — Betrieb, Sicherheit und Fehlerbehandlung (Epic-6-Basis erhalten)

## Primäre Einstiegspunkte

- Epic-5-Target-Routing und Assembly-Dispatch:
  - `src/AiNetLinter/Mcp/AnalysisToolCall.cs:113-201`
  - `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:44-224`
  - `src/AiNetLinter/Mcp/Registration/SymbolBodyToolRegistrations.cs:30-44`
  - `src/AiNetLinter/Mcp/Registration/FileStructureToolRegistrations.cs:95-179`
  - `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs:140-180`
  - `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:87-125`
- Epic-5-Symbolsuche und Referenznavigation:
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:21-145`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolSearch.cs:18-107`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs:16-106`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyReferenceNavigator.cs:24-180`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindSymbolTool.cs:21-96`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs:19-79`
  - `src/AiNetLinter/Mcp/Tools/CallTree/AssemblyGetCallTreeTool.cs:18-90`
- Epic-5-Body-, Struktur- und Metrikfolgen:
  - `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs:65-176`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs:18-260`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs:131-221`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/GetTypeHierarchyTool.cs:20-61`
  - `src/AiNetLinter/Mcp/Tools/DependencyGraph/DependencyGraphTool.cs:29-194`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetNamespaceTreeTool.cs:20-216`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetFileSkeletonTool.cs:24-156`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:32-382`
  - `src/AiNetLinter/Mcp/Tools/MetricsLookup/MetricsLookupTool.cs:23-133`
  - `src/AiNetLinter/Mcp/Tools/MetricsTree/MetricsTreeTool.cs:28-122`
- Epic-5-Extensions und Trunkierungsprojektion:
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:108-177`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/FindAssemblyExtensionsResponseBuilder.cs:15-110`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:38-58,92-128,205-268`
  - `src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs:41-180`

- Epic-3-Hauptpfad Referenzen/Source/Diagnosen:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:16-427`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyReferenceSessionExpander.cs:13-164`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/AssemblySourceSelectionOrchestrator.cs:64-203`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/SourceSelection/AssemblySourceMatchResolver.cs:48-230`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:130-411`
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:133-164`

- Assembly-Analyse-Registrierung und Target-Routing:
  - `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:15-131`
  - `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs:11-81`
  - `src/AiNetLinter/Mcp/AnalysisToolCall.cs:113-179`
- Decompilation und Dokumentmaterialisierung:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:19-24,26-62,64-124,126-149,151-203`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs:238-410`
- Session, Generation und Snapshot-Lebenszyklus:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:61-368,432-467`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs:19-145`
- Epic-4-Lebenszeit-Hauptpfad:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:29-451`
    (`LeaseAsync:104-147`, `TryLeaseEntry:250-285`, `CreateEntry:354-378`,
    `RetireEntryAsync:380-393`, `DisposeAsync:332-350`).
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:15-470`
    (`RefreshAsync:71-83`, `RefreshCoreAsync:113-130`,
    `CreateAndInstallGenerationAsync:197-231`, `InstallGeneration:294-334`,
    `Dispose:85-105`, `ReleaseSnapshot:398-419`).
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisEntry.cs:25-261`
    (`Matches:65-74`, `TryAcquireLease:76-104`, `TryBeginRetirement:106-112`,
    `DisposeAsync:114-181`, `IsIdle:205-231`).
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisRegistryEvictionCoordinator.cs:12-146`
    (`RunCoreAsync:40-54`, `FindIdleCandidatesAsync:56-88`,
    `RetireIdleCandidatesAsync:100-124`, `TryRemoveEntryForRetirement:126-145`).
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs:98-172`
    und `src/AiNetLinter/Mcp/Assemblies/Analysis/ExternalResourceRegistry.cs:15-469`
    für Resident-, Disk-, Memory-, Parallelitäts- und Idle-TTL-Budget.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyFingerprint.cs:11-66`,
    `AssemblyDecompilationCache.cs:32-103,238-255` und
    `AssemblyCacheCleanup.cs:37-75` für Fingerprint-/Key-/Cache-Retention.
- Epic-4-Health-/Host-Sicht:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisHealthSnapshotProvider.cs:24-77`
    projiziert residenten Status, Origin, Generation und Diagnosen.
  - `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthModels.cs:39-72`,
    `GetServerHealthResponseBuilder.cs:16-125` und
    `Projection/AssemblyHealthProjection.cs:14-84` bilden Sessionlisten,
    Statuscounts und Diagnosebudgets, aber keine Lease-/Resource-Zähler.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisHostComposition.cs:177-219`
    erzeugt getrennte Session- und Source-Resource-Registries und entsorgt sie
    in definierter Reihenfolge.
- Source-backed-/Fallback-Kontext:
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:79-331`
- On-demand-Bodies:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs:18-260`

## Betroffene Dateien und Symbole

- `AssemblyIdentityDto`/`AssemblyReferenceDto` in `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:63-88`:
  - Die Assembly-Identität trägt ein starkes Identitätsmerkmal; Referenzdatensätze tragen aktuell nur Name, Version und Kultur.
- `AssemblyReferenceResolver`:
  - `VisitNode:76-98` kann am Node-Limit still enden; `VisitChild:100-129` setzt am Boundary nicht zwingend `node_limit`.
  - `FindReferencePath:183-218`, `EnumerateCandidatePaths:240-266`, `ReadMetadata:311-328` und `IdentityMatches:348-351` bilden Kandidatensuche, Metadaten und Identitätsvergleich.
- `SourceProjectReferenceGraph:23-127` ergänzt Source-Project-Referenzen bounded, aber keine beliebigen binären Probe-Wurzeln.
- `AssemblyReferenceSessionExpander:33-163` projiziert Resolved-/Missing-/Boundary-/Sessionstatus, Origin, Completeness und Diagnosen; MCP meldete am Klassensymbol einen `AIContextFootprint`-Überhang.
- `AssemblySourceMatchResolver:82-176` matcht Snapshot-/Mapping-Identität, Alias und Project-/Assembly-Namen, aber keine Binary-zu-Source-Identität.
- `AssemblySourceSelectionOrchestrator:116-189`, der konfigurierte
  `ExternalSourceProvider` und `ExternalSourceProviderResult:22-91` gatesen
  Provider, Checkout, Snapshot, Attestation, Health und Trust vor Source-backed.
- `AssemblyAnalysisRegistryEntryFactory:149-162` setzt im registrierten Assembly-Kontext `ConsumerSolution:null` und `ReceiverType:null`; `AssemblyAnalysisService.ToExtensionDto:133-164` projiziert dann `not_decidable`.
- `AssemblyAnalysisContextFactory:130-180,280-309,394-411` trennt Source-backed/Fallback, baut Source-Project-References ein und setzt Origin/Confidence/Trust/Partial.
- Epic-5-Befund E5-BUG-01: `AssemblySymbolSearch.FindMatchesAsync:18-67` sammelt Root zuerst, sortiert die Treffer aber global nach Origin-Pfad und kappt danach; bei kleinem `maxResults` können Root-Treffer unsichtbar werden.
- Epic-5-Befund E5-BUG-02: derselbe Pfad übergibt `distinct.Count > shown.Count` an `CreateSummary` als `assembliesTruncated`; Trefferlisten- und Assembly-Scope-Trunkierung werden dadurch vermischt.
- Epic-5-Befund E5-BUG-03: Referenz-IDs aus `find_symbol(includeReferences=true)` werden von `get_symbol_body` gegen die Root-Lease-Identität geprüft und sind dort nicht weiterverwendbar.
- Epic-5-Befund E5-BUG-04: `SkeletonSyntaxWalker.BuildConstructorInfo:214-220` erzeugt eine Konstruktor-DocumentationCommentId, die der aktuelle Stable-ID-Resolver beim Skeleton-zu-Body-Roundtrip nicht findet.
- Epic-5-Befund E5-BUG-05: `AssemblyAnalysisResponseLimits.ProjectResponseBudget(FindAssemblyExtensionsPayload):38-58` setzt bei Begleitlisten-Trimming `Truncated=true`, obwohl `TotalExtensions=0` und kein Extension-Element entfernt wurde.
- Epic-5-Boundary: `get_type_hierarchy`, `dependency_graph`, `get_namespace_tree`, `get_file_skeleton`, `get_class_structure`, `metrics_lookup` und `metrics_tree` dispatchen assembly-seitig auf die Root-Solution; sie besitzen aktuell kein `includeReferences`.

- `AssemblyDecompilationAdapter`
  - `CreateBodyResolver` und `CreateDecompiler` trennen Signatur-Snapshot (`decompileMemberBodies=false`) von Body-Abfrage (`true`).
  - `DecompileTypes` erzeugt `DecompiledDocument`, parst C# und entfernt compiler-generierte Nested Types/State-Machine-Attribute.
  - `ReadTopLevelTypes`, `ReadTypeTree` und `SelectTypes` bilden Metadaten-/Budgetgrenzen ab.
- `AssemblyDecompilationCache`
  - `ReadGeneration` → `ReadDocuments` → Cache-Hit-Snapshot.
  - `WriteDocuments`/`CreateManifest` persistieren generierte relative Quellen und Manifeststatus.
  - Epic-2-Befund E2-BUG-01: `ReadDocuments` rekonstruiert `TypeMetadataName` aus dem Dateinamen und verliert `MetadataToken` sowie die ursprüngliche Dokumentreferenz.
- `DecompiledDocument`, `AssemblyRoslynSnapshot`, `AssemblyOrigin`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSessionModels.cs:21-39,99-103,168-177`.
  - Dokumentfelder: `GeneratedPath`, `TypeMetadataName`, `CSharpSource`, `MetadataToken`.
  - Snapshotfelder: `Solution`, `ProjectId`, `Compilation`, `Documents`, `Origins`, `Workspace`.
  - Originfelder: `OriginKind`, `ContentHash`, `SourceSnapshotIdentity`, `SourceProjectPath`, `Trust`, `BodyAvailability`, `ContentMode`, `FallbackReason`, `SourceDiagnostics`.
- `AssemblyRoslynWorkspaceFactory.CreateAsync`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyRoslynWorkspaceFactory.cs:19-80`.
  - Erstellt `AdhocWorkspace`, Projekt, Dokumente und Metadatenreferenzen; die Dokument-Origin wird als decompiled mit Content-Hash und Snapshotpfad erfasst.
- `AssemblyAnalysisContextFactory`
  - `TryCreateSourceBackedContextAsync:130-182`, `BuildSourceBackedContext:280-310`, `CreateWorkspaceFallback:333-352`, `ApplyFallback:312-331`.
  - Source-backed setzt hohe Confidence und verifizierten Trust; nicht nutzbare Source-Selections/Workspaces erhalten einen expliziten Fallback und decompilieren.
- `AssemblyDecompiledBodyResolver`
  - `DecompileBodyAsync:54-101` erstellt pro Anfrage einen body-fähigen Decompiler und dekompiliert den enthaltenden Typ erneut.
  - `MatchesContainingType:123-147`, `MatchesMethod:159-172`, `MatchesProperty:174-184`, `MatchesEvent:186-192`, `MatchesParameters:194-232`.
  - Epic-2-Befund E2-OPT-01: bounded Body-Cache bzw. sicher wiederverwendbarer Kontext fehlt.
- `AnalysisSymbolIdentity`
  - `src/AiNetLinter/Mcp/AnalysisSymbolIdentity.cs:10-50`.
  - IDs werden an `ContentHash` und `Generation` gebunden; `Format`, `Matches` und `TryParse` bilden die Snapshotgrenze.
  - Epic-2-Befund E2-BUG-03: eine aus `get_file_skeleton` übernommene Konstruktor-ID war für `get_symbol_body` nicht direkt auflösbar, eine Positions-ID dagegen schon.
- `GetClassStructureTool`
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureTool.cs:48-111,152-182,256-287`.
  - `CollectDeclarationFilesAsync` liefert die relative Typ-Spanne als `TotalLines`; `CreateMemberEntry` liefert absolute Memberzeilen.
  - `src/AiNetLinter/Mcp/Tools/FileStructure/GetClassStructureModels.cs:10-37` enthält keine Koordinatenbasis für `TotalLines`.
  - Epic-2-Befund E2-BUG-02: Gesamtzeilen- und Memberzeilen-Semantik ist für Snapshot-/Struktur-Evidence nicht eindeutig.
- `AssemblyDiagnosticCodes.IsExpectedDeclarationOnlyDiagnostic`
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDiagnosticCodes.cs`.
  - Erkennt nur die erwartbaren Deklarations-only-Diagnosecodes für leere Event-Accessors bzw. Member-Bodies.
- `AssemblyReferenceNavigator:24-180` traversiert für `find_references`/`get_call_tree` nur bei explizitem `includeReferences=true` die bounded Lease-Menge; `CreateSources:134-145` ergänzt bei Referenzzielen eine auf den Root gemappte Quelle.
- `TransitiveCallGraphFormatter:41-180` projiziert Diagnostics und Call-Site-Completeness; leere dekompilierte Stub-Ergebnisse bleiben fachlich von globalen Negativaussagen zu trennen.
- `GetSymbolBodyTool:65-176` liefert für Assembly-Leases Text-only-Bodies und nutzt Stable-ID-/Positionsauflösung; `AssemblyDecompiledBodyResolver:54-101` dekodiert Bodies separat on demand und kann `unavailable` oder zeilenbegrenzte Ergebnisse liefern.
- `MetricsLookupTool:56-86` misst die bereitgestellte Roslyn-Snapshot-Solution; bei decompiled `decompiledSignatureOnly` sind die Metriken daher nicht automatisch Body-Metriken.

## Aufrufer und Abhängigkeiten

- Epic-3-Fluss: `targetType/targetPath` → Assembly-Registry/Lease → `AssemblyReferenceResolver` → lokaler/TPA- und Source-Project-Graph → `AssemblyReferenceSessionExpander` → Root-/Transitivdiagnosen und Response-Summaries.
- Source-Fluss: `AssemblySourceSelectionOrchestrator` → bestehender External-Source-Provider → Checkout-/Snapshot-/Trust-Gates → `AssemblySourceMatchResolver` → source-backed Context oder decompiled/fallback.
- Consumer-Fluss: Source-aware Dispatch-Overloads können eine Solution an `AssemblyAnalysisContextFactory` geben; der registrierte Assembly-Lease-Dispatch speist jedoch kein Consumer-Ziel, deshalb bleibt Applicability ohne Receiver `not_decidable`.
- Bounded Referenzauflösung sucht im Zielverzeichnis und in Trusted-Platform-Assemblies; `SourceProjectReferenceGraph` ergänzt nur den Projektgraphen. Fehlende/incompatible Dependencies werden als Partial-Diagnosen weitergereicht.

- `inspect_assembly` und `find_assembly_extensions`
  → `AssemblyAnalysisToolRegistrations`
  → `AnalysisToolCall`/`AssemblyAnalysisDispatcher`
  → `AssemblyAnalysisRegistry`/`AssemblyAnalysisSession`
  → `AssemblyAnalysisContextFactory`
  → Roslyn-Snapshot, Response-Builder und Origin-/Completeness-Projektion.
- `AssemblyAnalysisSession.RefreshCore`
  → `AssemblyFingerprintCalculator`
  → `AssemblyReferenceResolver`
  → `AssemblyDecompilationCache` oder `AssemblyDecompilationAdapter`
  → `AssemblyRoslynWorkspaceFactory`
  → Compilation-Prüfung und Generation-Installation.
- Epic-4-Lifecycle-Fluss: Fingerprint → Registry-Entry-/Creation-Barriere →
  `AssemblyAnalysisEntry`-Lease → Session-Snapshot-Lease; bei Hash-/Source-
  Mismatch wird ein neuer Entry installiert und der alte Entry nach Lease-Drain
  retired. Eviction prüft LRU/TTL, revalidiert den Entry unter `gate` und reicht
  Ressourcenfreigabe an `ExternalResourceRegistry` weiter.
- Epic-4-Cache-Fluss: `AssemblyDecompilationCacheKey` trennt Content-/Options-
  Identität; der Pointer wählt eine Cache-Generation; Retention begrenzt nur
  Generation-Verzeichnisse innerhalb eines einzelnen Key-Verzeichnisses.
- `get_file_skeleton`/`find_symbol` liefern Snapshot-gebundene IDs; `get_class_structure` und `get_symbol_body` lösen dieselbe Assembly-Generation über Symbol-/Positionsadressen auf.
- `AssemblyReferenceResolver` arbeitet über PE-/Metadatenreferenzen; es gibt keine Runtime-Ausführung des Zielartefakts.
- Source-backed und decompiled sind getrennte Originpfade. `GIT-01`, `LOCAL-01`, `LOCAL-02` und `LOCAL-03` wurden im Audit nur als `decompiled` beobachtet; `FALSE-01` erzeugte keinen Snapshot.
- Epic-5-Navigationsfluss: `targetType/targetPath` → `AnalysisToolCall` → Root-Lease; nur `find_symbol`, `find_references` und `get_call_tree` setzen mit `includeReferences=true` die bounded Referenzexpansion für ihre jeweilige Navigation in Gang.
- Progressive-Disclosure-Fluss: `find_symbol`/`get_file_skeleton` → Stable-ID → `get_symbol_body`; der Root-Methodenpfad ist auflösbar, der getestete Referenz-ID-Pfad und der Skeleton-Konstruktorpfad sind aktuell nicht geschlossen.
- Struktur-/Metrik-Fluss: Assembly-Lease → eine Root-Roslyn-Solution → `get_type_hierarchy`/`dependency_graph`/Namespace-/Klassen-/Skeleton-/Metrics-Scanner; Referenz-Assemblies werden in diesen Routen nicht als fachliche Knoten aggregiert.
- Extension-Fluss: `find_assembly_extensions` expandiert Referenz-Sessions für Begleitmetadaten, sucht klassische öffentliche `IMethodSymbol.IsExtensionMethod`-Treffer aber nur in `context.Assembly`; ohne Consumer-Solution bleibt `ReduceExtensionMethod`-Applicability `not_decidable`.
- Registry-Isolation: `AssemblyAnalysisHostComposition` hält Session- und
  Source-Ressourcen in getrennten `ExternalResourceRegistry`-Instanzen. Das
  isoliert Zustände, bedeutet aber zugleich getrennte Budgets statt eines
  nachgewiesenen hostweiten Gesamtbudgets.

## Relevante Tests, Konfiguration und Dokumentation

- Epic-3-relevante, read-only gesichtete Tests:
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisDispatcherCapabilityTests.cs:54-127` — Missing Reference und Node-Limit.
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs:118-140,349-425` — `not_decidable`, Extension-Consumerfilter, Resolver-Transitivität, Missing und Cycle.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblySourceMatchResolverTests.cs`, `AssemblyAnalysisContextFactoryTests.cs` und `AssemblyAnalysisRouteTests.cs` — Source-Match, Fallback, Source-Project-Expansion und Projektion.
- Relevante External-Source-Provider-/Snapshot-Tests in Fast- und IntegrationTests.
- Epic-4-relevante, read-only gesichtete Tests:
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryTests.cs:1-430`
    — Creation-Barriere, Cancellation, ABA-/Generationen, Hash-/Mtime-Refresh,
    LRU-/TTL-Eviction, Capacity und Entry-Disposition.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisRegistryFreshnessTests.cs:1-80`
    und `AssemblyAnalysisRegistryRetirementRaceTests.cs:1-80` — Source-
    Snapshot-Identität und Retirement-Revalidierung.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalResourceRegistryTests.cs:1-180`
    — Identity-Deduplizierung, Reservierungen, Capacity, Operation-Slots,
    Idle-Eviction und Dispose-Race.
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs:1-316`
    — Mtime-/Content-Refresh, Cache-Hit, Manifest, Cancellation, Last-good-
    Degraded-State, Größen- und Typbaumgrenzen.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyCacheCleanupTests.cs:1-100`
    — Retention innerhalb eines Cache-Key-Verzeichnisses.
  - `src/AiNetLinter.FastTests/Mcp/Tools/ServerMaintenance/GetServerHealthToolTests.cs:1-235`
    und `McpServerAssemblyHealthE2ETests.cs:1-164` — Health-Listen,
    Sessionlimits, Diagnosebudgets und Assembly-Health-Projektion.
- Epic-4-Kontext: `Docs/agent-api.md:453-481`, `Docs/integration.md:337-375`
  und `Docs/configuration.md:1660-1669` dokumentieren Target-/Health-/Session-
  sowie externe Resource-Limits; ein hostweiter Gesamtzähler und ein
  content-key-übergreifender Assembly-Cache-TTL sind dort nicht zugesagt.
- Die lokale Audit-Matrix wurde ausschließlich zur Label-/Pfadauflösung gelesen; konkrete externe Identitäten bleiben aus dieser Map heraus.

- Read-only gesichtet:
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs:22-306` — Source-backed, Fallback, Origin und Workspace-Failure.
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisSessionTests.cs:15-311` — Fingerprint, Generation, Cachemanifest, Partial-/Failed-/Degraded-Status und Nichtladen.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyAnalysisPathContractTests.cs:23-190` — generated-document routing, stabile Parameter-Methoden-ID und fremde Generation.
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs:20-345` — öffentliche API, Generics, Attribute, Parameter, Extensions, Referenzen und Partial-Diagnosen.
  - `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetClassStructureToolTests.cs` — Struktur-/Linien-/Member-Budgets.
- Read-only gesichtet:
  - `rules.json`, `ainetlinter.project.json`, `src/AiNetLinter/appsettings.json` und Root-`.gitignore`.
  - `.agents/rules/AiNetLinter-McpWorkflow.mdc` und `Docs/agent-api.md` für Target-, Origin-, Budget- und Progressive-Disclosure-Vertrag.
  - `temp/decompiled-assembly-audit-examples.md` nur für die lokale Fallauflösung; keine konkrete Matrixidentität ist in diese Map übernommen.
- Kein Test deckt im gesichteten Scope den direkten Skeleton-zu-Body-Roundtrip eines Konstruktors oder die vollständige Cache-Rundtrip-Gleichheit aller `DecompiledDocument`-Metadaten ab.
- Für Epic 2 sind keine Konfigurationsänderungen vorgeschlagen oder vorgenommen worden.
- Epic-5-relevante, read-only gesichtete Tests:
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs:99-185` — klassische Extension-Erkennung, Receiver-Filter und erwartetes `not_decidable` ohne Consumer.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation/AssemblyAnalysisPathContractTests.cs:23-190` — Assembly-Dokumentrouting, stabile Parameter-Methoden-ID und Generationsgrenze.
  - `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetClassStructureToolTests.cs` — Member-/Linien-/Budgetvertrag; kein direkter Konstruktor-Skeleton-zu-Body-Roundtrip.
  - `src/AiNetLinter.FastTests/Mcp/Tools/CallTree/GetCallTreeToolTests.cs` und Navigation-Vertragstests — Calltree-/Richtungs-/Trunkierungsform; kein externer dekompilierter Vollbody-Nachweis.
- Epic-5-relevante Doku: `Docs/agent-api.md:329-372,468-482,516-518,870-895` und `Docs/integration.md:477-495` beschreiben Root-only-Defaults, bounded Referenzexpansion, Progressive Disclosure, Stable IDs, Body-Modi sowie die Snapshot-Grenze.

## Invarianten, Risiken und Unsicherheiten

- Epic-3-Befunde: starke Referenzidentität fehlt im Referenzmodell; Node-Limit kann Status/Diagnose entkoppeln; Session-Expander überschreitet den gemeldeten Footprint; Metadatenkandidaten werden mehrfach gelesen; Consumer-Route, Binary-zu-Source-Attestierung und Source-/Dependency-Probe-Wurzeln fehlen.
- Provider-/Checkout-/Snapshot-Vertrag: `source-backed` nur mit verfügbarem, attestiertem Snapshot sowie Verified/Clean; Provider-/Workspacefehler und `isError=false` ohne fachlich nutzbaren Snapshot sind kein Erfolg.
- Partial-/Truncation-Vertrag: Referenz-, Session-, Root-/Transitivdiagnosen und Origin müssen ihre Begrenzung sichtbar halten; hohe Diagnosezahlen der Falllabels sind kein Vollständigkeitsnachweis.
- Keine externe Assembly wird geladen, ausgeführt oder instanziiert; die Analyse bleibt metadata-only und bounded (`MaxReferenceDepth=8`, `MaxReferenceNodes=128`).

- Metadata-only: PE-/Metadatenlesen und Roslyn-/Decompiler-Arbeit bleiben read-only; keine Assembly wird geladen, instanziiert oder ausgeführt.
- Fresh Snapshot: decompiled Dokumente werden als UTF-8-`SourceText` in einen `AdhocWorkspace` eingefügt; Referenzen werden als `MetadataReference` ergänzt; der Snapshot wird bei harten Syntax-/Workspacefehlern verworfen.
- Herkunft: `source-backed` benötigt Source-Snapshot, Projektpfad, hohe Confidence und verifizierten Trust; `decompiled` benötigt mindestens Content-Hash, Generation, Fallback-/Diagnoseinformationen und den Hinweis auf mögliche Abweichung.
- Completeness: Typ-/Member-/Referenz-/Diagnosebudgets und Assembly-Limits können Ergebnisse kürzen oder `partial` machen. Jede Assembly-MCP-Antwort im Audit wurde mit Herkunft, Snapshot/Generation, Status, Completeness, Trunkierung und Diagnosen erfasst.
- Signatur-/Body-Vertrag: `decompiledSignatureOnly` ist der Basissnapshot; Bodies sind `decompiledBodyOnDemand` und können zeilenbegrenzt/trunkiert oder unavailable sein.
- Stable IDs: `assembly:<content-hash>:<generation>:<symbol-id>` sind nur innerhalb des passenden Content-Hash-/Generation-Kontexts gültig; alte Generationen dürfen nicht als aktuelle Symbole wiederverwendet werden.
- Epic-5-Invariante: Root-vs-Referenz muss in Ergebnisreihenfolge, Origin, Assembly-Count und Trunkierungsgründen getrennt sichtbar bleiben; ein `maxResults`-Trefferlimit darf kein Assembly-Scope-Limit überschreiben.
- Epic-5-Invariante: `decompiledSignatureOnly` ist der Navigations-/Metrik-Snapshot; `decompiledBodyOnDemand` ist ein separater, optionaler Folgepfad und darf keine globale Aussage über Caller-Abwesenheit erzeugen.
- Epic-5-Risiko: `find_symbol(includeReferences=true)` kann unter kleinem Budget Root-Treffer verdrängen; Struktur-/Metriktools bleiben Root-only; Referenz- und Konstruktor-IDs sind nicht überall als Folgeinput konsumierbar.
- Epic-5-Risiko: `find_assembly_extensions` markiert Response-Budget-Trimming derzeit auf der Extension-Liste, obwohl nur Begleitlisten reduziert wurden; Consumer-Applicability bleibt im Standalone-Assembly-Dispatch bewusst `not_decidable`.
- E2-BUG-01 gefährdet Cache-Hit-Metadatenidentität; E2-BUG-02 gefährdet Zeileninterpretation; E2-BUG-03 gefährdet Konstruktor-Progressive-Disclosure; E2-OPT-01 betrifft Kosten, nicht Semantik.
- Offene Unsicherheit: die geprüfte Body-Parameterlogik behandelt `ref`, `out` und `in`, aber nicht nachweislich alle modernen Kombinationen wie `ref readonly`, `scoped`, `params` und Extension-`this`.
- Offene Unsicherheit: Wegen unvollständiger Referenzauflösung und Response-Budgets wurde keine Vollständigkeit über alle generischen Constraints, Attribute oder Member der fünf Falllabels behauptet.
- `GIT-01` ist kein source-backed Beleg: Der tatsächlich angesprochene Provider meldete `provider-unavailable`; die Source-backed-Implementierung wurde nur statisch als vorhanden verifiziert.
- Epic-4-Befunde aus dem Read-only-Audit:
  - `E4-BUG-01`: Resource-Budget dedupliziert bei Refresh nach Pfad und übernimmt
    bei geänderter Dateigröße die alten Dimensionen.
  - `E4-BUG-02`: Kein Post-Build-/Post-Read-Fingerprint vor Snapshot-Install und
    Cache-Publish; ein In-Flight-File-Change bleibt als Race offen.
  - `E4-BUG-03`: `RetireEntryAsync` verschluckt Cleanup-Fehler, obwohl Registry-
    Disposal Retirement-Tasks eigentlich aggregieren könnte.
  - `E4-BUG-04`: `AssemblyAnalysisSession.Dispose` entsorgt `refreshGate` ohne
    laufende/wartende Refreshes zu koordinieren.
  - `E4-BUG-05`: Zwischen Snapshot-Erzeugung und Cache-Publish/Install fehlt ein
    Cancellation-Commitpunkt.
  - `E4-OPT-01/02/03`: abgeschlossene Retirement-Tasks, Generation-Counter pro
    Pfad und case-sensitive Cache-Key-Bildung wachsen bzw. duplizieren sich
    ohne zusätzliche Begrenzung.
  - `E4-MF-01/02/03`: Root-/Content-Key-Cache-TTL, Lifecycle-/Resource-Health-
    Felder und ein optionaler hostweiter Budget-View fehlen.

## Verifikation

- Ausgeführte Epic-3-MCP-Abfragen nutzten das aktuelle Schema mit absolutem `targetPath`: `get_index_scope`, `get_file_tree`, `get_server_health`, `find_symbol`, `get_feature_context`, `get_symbol_body`, `get_violations`, `inspect_assembly` und `find_assembly_extensions`.
- Redigierte Origin-/Diagnoseprüfungen wurden für GIT-01, LOCAL-01, LOCAL-02, LOCAL-03 und den Epic-relevanten FALSE-01-Negativpfad ausgeführt. GIT-01 war `provider-unavailable`/decompiled/partial ohne nutzbaren Snapshot; LOCAL-Fälle waren decompiled/medium/untrusted/partial; FALSE-01 war recoverable `WORKSPACE_DIAGNOSTIC` ohne Snapshot.
- Keine Builds, Tests oder Commits ausgeführt. Nach der letzten Code-Map-Änderung wurden die gezielten redigierten MCP-Nachweise wiederholt und ausschließlich im Epic-3-Bericht-Handoff dokumentiert; danach erfolgt keine weitere Dateiänderung.
- Für Epic 4 wurden keine Builds, Tests oder Commits ausgeführt. Die finale
  Map-Verifikation erfolgt als redigierte MCP-Runde: `inspect_assembly` und
  zielgebundenes `get_server_health` für `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und
  `FALSE-01`; `GIT-01` ist für einen direkten Assembly-Target-Spotcheck nicht
  anwendbar. Erwartete Ergebnisform: die drei positiven Labels bleiben
  `decompiled`/`partial` mit sichtbarer Generation; `FALSE-01` bleibt ein
  recoverable Negativpfad ohne Snapshot. Das konkrete Ergebnis wird im
  Epic-4-Bericht festgehalten.

- Vollständig gelesen: `AGENTS.md`, relevante `.agents/rules/*.mdc`, `Konzept.md`, `roadmap.md`, vorherige `code-map.md` und `implement/SKILL.md`.
- MCP-Projektchecks: `get_index_scope`; `get_file_tree` (Assembly-Unterbaum, Tiefe 3, 97/97, nicht gekürzt); `get_server_health` projektgebunden und aggregiert; `get_feature_context`; `get_class_structure`; `get_symbol_body`; `get_file_skeleton`.
- MCP-Assemblychecks: `inspect_assembly` und `find_assembly_extensions` für alle fünf Labels mit `targetType=assembly` und absolutem Matrixpfad; `find_symbol`, `get_class_structure`, `get_file_skeleton` und `get_symbol_body` für die positiven Fälle.
- Ergebnis: `GIT-01`/`LOCAL-01`/`LOCAL-02`/`LOCAL-03` decompiled, `partial`, ohne Source-Snapshot; `FALSE-01` recoverable `WORKSPACE_DIAGNOSTIC` ohne Snapshot. Signaturen, Attribute, Parameter, generische Signaturen und Bodies waren im bounded Umfang abfragbar.
- Read-only Text-/Testinspektionen wurden nur zur Kontext- und Abdeckungsprüfung verwendet. Es wurden keine Builds, Tests, Produktions-/Konfigurations-/Produktdokumentationsänderungen oder Commits ausgeführt.
- Nach der letzten Änderung an dieser Code-Map wurden ausschließlich redigierte Artefaktprüfungen, Pfad-/Label-Scans und gezielte MCP-Semantik-Spotchecks ausgeführt; der finale Hand-off bezieht sich auf diesen Stand.

## Epic 6 – Response-, Token- und Laufzeiteffizienz

### Relevante Implementierungspfade

- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:12-244` definiert Diagnose-, Referenz-, Session- und Samplebudgets. Die Diagnoseauswahl ist root-first/prefix-basiert; `SelectSamples` beendet die Kandidatenschleife beim ersten Byteüberlauf.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:15-323` projiziert Inspect-/Extension-Payloads durch einzelnes Entfernen von Sessions, Referenzen, Diagnosen und Ergebnislisten. Jede Probe misst Text und Structured Content separat; ein terminaler irreduzibler Fixed-Metadata-Fallback fehlt.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:19-144` reichert beide Responsekanäle an. Die interne Budgetprüfung zählt Text- und Structured Content getrennt.
- `src/AiNetLinter/Mcp/McpToolResults.cs:197-225` erzeugt für `Text<T>` ein gemeinsames Ergebnis mit Text- und Structured-Content-Kanal.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:58-95` sammelt/sortiert passende Typen und Extensions vor `Take(maxResults)`; die Memberprojektion begrenzt ebenfalls erst nach vorgelagerter Materialisierung.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:26-42,151-229` und `AssemblyAnalysisSession.cs:167-195` bauen den bounded Signature-Snapshot vor der konkreten MCP-Ausgabeauswahl.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceSessionExpander.cs:23-60,89-163` besucht Referenzkanten vor der sichtbaren Projektion; nach dem Node-Cap können weitere Boundary-Sessions/Diagnosen materialisiert werden.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:131-168` weist keine feldspezifischen Namespace-Gesamt-/Truncationwerte und keine maschinenlesbaren Bytebudgetwerte aus.

### Epic-6-Befundregister

- `E6-BUG-01` (P2/M/hoch): `MaxResponseBytes=8192` wird in `FitsResponseBudget` kanalweise statt über das vollständige CallToolResult geprüft. Redigierte große/kleine positive Abfragen lagen je Kanal unter 8192, zusammen aber zwischen 9198 und 13271 Byte. Details und alle Counts stehen in `tasks/decompiled-assembly-audit/epic-06-response-token-laufzeiteffizienz.md`.
- `E6-BUG-02` (P2/M/mittel-hoch): Nach dem Entfernen aller optionalen Listen garantiert `ProjectResponseBudget` keinen weiterhin passenden Payload; feste Pfad-/Identitäts-/Statusfelder werden nicht separat begrenzt. Der Extremfall ist statisch abgeleitet, in der Normalmatrix nicht reproduziert.
- `E6-OPT-01` (P2/L/hoch): Einzelweises Trimming serialisiert/formatierte den Payload wiederholt und misst beide Kanäle erneut.
- `E6-OPT-02` (P2/L/hoch): Query-Limits begrenzen die vorgelagerte Typ-/Extension-/Member- und Snapshotarbeit nicht proportional.
- `E6-OPT-03` (P2/M/hoch): Die Referenzarbeit übersteigt die sichtbaren Caps. Die redigierten Gesamt-Sessioncounts der positiven großen Fälle lagen bei 4039, 1482 und 1519; sichtbar blieb regelmäßig nur eine Session. Die verdeckte Referenzerweiterung selbst bleibt `E1-BUG-01`.
- `E6-OPT-04` (P3/S/mittel-hoch): Diagnose-Sample-Auswahl ist root-first und bricht beim ersten nicht passenden Sample ab; späteres kürzeres/ergänzendes Material wird nicht geprüft.
- `E6-MF-01` (P2/M/hoch): Es fehlen maschinenlesbare Ist-/Limitwerte für Text, Structured Content und die kombinierte Response sowie feldbezogene Trimursachen.
- `E6-MF-02` (P3/S/hoch): Namespace-Trimming ist nur über den allgemeinen Top-Level-Grund sichtbar; `TotalNamespaces`/`NamespacesTruncated` fehlen.

### Epic-6-Metrik-/Footprint-Nachweis

Der aktuelle zielgebundene MCP-Metriklauf meldete für die zentralen Response-Symbole: `AssemblyAnalysisResponseLimits` 498 LOC/941 Footprint, `InspectAssemblyResponseBuilder` 75/2450, `FindAssemblyExtensionsResponseBuilder` 86/2463, `AssemblyAnalysisResponse` 125/2500 und `AssemblyReferenceSessionExpander` 135/2513. Der letzte Wert ist der bereits bestehende Epic-3-Footprint-Überhang und wird in Epic 6 nicht doppelt als Befund gezählt. Die zentrale Response-/Budgetlogik liegt damit nahe an den projektweiten Grenzen und soll nicht ungezielt weiter anwachsen.

### Epic-6-Verifikation

- Read-only MCP-Projektchecks nutzten `targetType=project` und den absoluten Projektpfad: Indexscope vollständig (886 C#-Dateien), Assembly-Unterbaum 18/18 Einträge nicht gekürzt, Symbol-/Feature-/Metrikauflösung für Response-, Budget-, Service-, Decompilation- und Referenzpfade.
- Redigierte Assemblychecks nutzten `targetType=assembly` und jeweils den absoluten, nur über das Label referenzierten Matrixpfad. `inspect_assembly` wurde für `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01` mit kleinen (`includeReferences=false`, `maxResults=1`, `maxMembers=1`) und großen (`includeReferences=true`, `maxResults=1000`, `maxMembers=1000`) Limits ausgeführt. `find_assembly_extensions` wurde für die drei positiven Labels mit `maxResults=1` und `maxResults=1000` ausgeführt; `FALSE-01` war für diesen Extension-Spotcheck nicht relevant.
- Die positiven Fälle blieben redigiert `decompiled`/`partial`, `FALSE-01` ein recoverable `WORKSPACE_DIAGNOSTIC`-Negativpfad ohne Snapshot. Keine externe Identität oder dekompilierter Inhalt ist in dieser Map enthalten.
- Die dort dokumentierte Epic-6-Finalrunde wurde nach der damaligen Map-Ergänzung ausgeführt. Spätere Epic-Lieferungen ergänzen diese Karte chronologisch und führen ihre eigenen Abschluss-Spotchecks aus.

## Epic 7 – Betrieb, Sicherheit und Fehlerbehandlung

### Relevante Implementierungspfade

- Zielauflösung und Dateityp:
  - `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs:11-100` – absolute Kanonisierung, Existenz- und `.dll`/`.exe`-Prüfung sowie recoverable Argumentfehler.
  - `src/AiNetLinter/Configuration/AssemblyPathValidation.cs:12-21` – zentrale, case-insensitive Erweiterungsregel.
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:22-59` – direkter Assembly-Pfad-Guard; Fehlermeldungen übernehmen Eingabepfad bzw. kanonischen Pfad.
- Metadata-only und Fail-Closed:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:22-50,216-237` – `PEReader`/Metadatenprüfung; fehlende oder beschädigte Metadaten liefern keine Identität.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:26-61,151-158` – bounded Decompilation mit Cancellation/Deadline; kein Runtime-Load-Pfad.
  - `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs:48-98` – Pfadfehler und nicht erzeugbarer Kontext werden recoverable projiziert.
- Fingerprint, IO und wechselnde Dateien:
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyFingerprint.cs:11-28,30-49` – Hashbildung über einmaligen Read und Übernahme des davor gelesenen `FileInfo`-Zustands; IO-/Access-Fehler tragen `exception.Message` weiter.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:113-229` – Fingerprint vor Referenzauflösung/Decompilation/Snapshot/Cache-Publish; kein neuer Post-Read-Commitpunkt.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:104-146,189-247,434-450` – Retry bei wechselnder Identität, interne Creation-Cancellation und Default-`isError`-Semantik.
- Fehlerprojektion und Redaction:
  - `src/AiNetLinter/Mcp/AnalysisToolCall.cs:139-185` – unerwartete Assembly-Route-Exceptions werden als `CompilationError` mit Rohtext zurückgegeben.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisContextFactory.cs:384-392,458-464` – Source-, Roslyn- und Referenzdiagnosen werden direkt zusammengeführt.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisDiagnostics.cs:10-20` – externe Diagnosen werden ohne Redaction in die Anzeige formatiert.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:32-124` – Enrichment/Origin- und Source-Diagnose-Samples; `NormalizeForDisplay` ist keine Geheimnis-/Pfadredaktion.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisHealthSnapshotProvider.cs:24-75` – Health nimmt Fault-Exception-Messages direkt als Diagnose auf.
- Provider- und Lifecycle-Sicherheit:
  - `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/ExternalSourceRepositoryFailurePolicy.cs:120-161` – Provider-Transportdiagnosen werden auf Code, sichere Meldung und `$repository` projiziert.
  - `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs:382-410` – URLs mit Userinfo, Query oder Fragment werden abgewiesen.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisSourceProjectLeaseCoordinator.cs:46-68,104-127` – Source-Project-Creation und Cancellation-/Exception-Projektion.
  - `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs:41-51,98-165` – Resource-Health ist intern vorhanden, wird aber nicht in Assembly-Health eingebunden.
  - `src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs:12-90`, `GetServerHealthModels.cs:35-72` und `GetServerHealthTool.cs:47-97` – begrenzte Health-Projektion ohne Fehlerklasse, Lease-/Retirement- oder Resource-Zähler.

### Epic-7-Befundregister

- `E7-BUG-01` (P1/L/hoch): Rohpfade, Roh-Exception-Messages und externe/Compilerdiagnosen erreichen Assembly-Responses und Health. Das wird durch einen redigierten ungültigen MCP-Pfadmarker reproduziert; die bestehenden Transport-Provider-Policies sind davon abgegrenzt.
- `E7-BUG-02` (P2/M/hoch): Kontrollierte interne Creation-/Source-Project-Cancellation fällt über den Default `Failure(..., isError=true)` in einen harten Toolfehler, obwohl der Aufrufer nicht abgebrochen haben muss. Caller-Cancellation wird korrekt weitergereicht; der Befund betrifft den internen Lifecyclepfad.
- `E7-OPT-01` (P3/M/hoch): `AssemblyAnalysisRegistryEvictionCoordinator` überschreitet laut zielgebundenem MCP-Violation-Check das AI-context-Footprint-Limit; der bereits bekannte `AssemblyReferenceSessionExpander`-Überhang wird nicht doppelt gezählt.
- `E7-MF-01` (P2/M/hoch): `get_server_health` weist Assembly-Fehlerklasse/Recoverability, Last-good-/Retirement-Zustand und Resource-/Lease-/Operationstelemetrie nicht maschinenlesbar aus; vorhandene Status-, Origin-, Generation- und Diagnosefelder bleiben erhalten.

### Epic-7-Invarianten und Abgrenzungen

- Assembly-Ziele bleiben metadata-only: `PEReader`, `MetadataReference` und Decompiler-Resolver werden verwendet; im geprüften Assembly-/Analysis-Bereich gibt es keinen `Assembly.Load`, `AssemblyLoadContext`, `Activator.CreateInstance` oder `Process.Start`-Treffer. Externe Provider-Prozesse sind davon getrennt und verarbeiten Source-Checkout, nicht das Ziel als Runtime-Assembly.
- Absolute Zielpfade und `.dll`/`.exe`-Filter funktionieren als Eingangsvertrag. Native/beschädigte/nicht verwaltete Ziele werden vor einem Snapshot beendet; `FALSE-01` blieb recoverable ohne `analysis`/Snapshot. Das ist kein neuer Fail-Closed-Befund.
- Der Race zwischen erstem Fingerprint und späterem Snapshot/Cache-Publish bleibt `E4-BUG-02`; Epic 7 bestätigt ihn als relevante Wechseldatei-Unsicherheit, dupliziert ihn aber nicht als neues Finding.
- Die sichere Provider-Transportprojektion und URL-Policy bleiben bestehen. Der neue Redaction-Befund betrifft die nicht zentralisierte Assembly-/Roslyn-/Health-Exception- und Diagnosekette.

### Epic-7-Verifikation

- Alle zielgebundenen MCP-Abfragen nutzten das aktuelle Schema mit `targetType` und absolutem `targetPath`; `get_server_health` wurde sowohl zielgebunden als auch aggregiert abgefragt.
- Read-only Gegenproben: `LOCAL-01`, `LOCAL-02` und `LOCAL-03` lieferten `isError=false`, `origin=decompiled`, `status=partial`, `completeness=partial`, `confidence=medium`, `trust=untrusted`, `generation=1` und sichtbare Truncation. `FALSE-01` lieferte `isError=false`, `recoverable=true`, `WORKSPACE_DIAGNOSTIC`, ohne Origin, Completeness, Snapshot oder Assembly-Payload. `GIT-01` ist der konfigurierte Source-/Git-Fall und wurde in diesem Epic nicht als direkter Assembly-Pfad wiederholt.
- Die abschließenden positiven/negativen Spotchecks nach der letzten Änderung an dieser Code-Map werden im Epic-7-Bericht vollständig und redigiert dokumentiert; danach erfolgte keine weitere Map-Änderung.
