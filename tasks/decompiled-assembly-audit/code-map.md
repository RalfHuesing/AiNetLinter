# Epic 4 — kompakte Code-Map (Epic-3-Basis erhalten)

## Primäre Einstiegspunkte

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
