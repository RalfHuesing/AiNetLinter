# Epic 3 — kompakte Code-Map (Epic-2-Basis erhalten)

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
- `AssemblySourceSelectionOrchestrator:116-189`, `GiteaExternalSourceProvider:26-153` und `ExternalSourceProviderResult:22-91` gatesen Provider, Checkout, Snapshot, Attestation, Health und Trust vor Source-backed.
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
- `get_file_skeleton`/`find_symbol` liefern Snapshot-gebundene IDs; `get_class_structure` und `get_symbol_body` lösen dieselbe Assembly-Generation über Symbol-/Positionsadressen auf.
- `AssemblyReferenceResolver` arbeitet über PE-/Metadatenreferenzen; es gibt keine Runtime-Ausführung des Zielartefakts.
- Source-backed und decompiled sind getrennte Originpfade. `GIT-01`, `LOCAL-01`, `LOCAL-02` und `LOCAL-03` wurden im Audit nur als `decompiled` beobachtet; `FALSE-01` erzeugte keinen Snapshot.

## Relevante Tests, Konfiguration und Dokumentation

- Epic-3-relevante, read-only gesichtete Tests:
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisDispatcherCapabilityTests.cs:54-127` — Missing Reference und Node-Limit.
  - `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs:118-140,349-425` — `not_decidable`, Extension-Consumerfilter, Resolver-Transitivität, Missing und Cycle.
  - `src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblySourceMatchResolverTests.cs`, `AssemblyAnalysisContextFactoryTests.cs` und `AssemblyAnalysisRouteTests.cs` — Source-Match, Fallback, Source-Project-Expansion und Projektion.
  - Relevante External-Source-Provider-/Snapshot-Tests in Fast- und IntegrationTests.
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

## Verifikation

- Ausgeführte Epic-3-MCP-Abfragen nutzten das aktuelle Schema mit absolutem `targetPath`: `get_index_scope`, `get_file_tree`, `get_server_health`, `find_symbol`, `get_feature_context`, `get_symbol_body`, `get_violations`, `inspect_assembly` und `find_assembly_extensions`.
- Redigierte Origin-/Diagnoseprüfungen wurden für GIT-01, LOCAL-01, LOCAL-02, LOCAL-03 und den Epic-relevanten FALSE-01-Negativpfad ausgeführt. GIT-01 war `provider-unavailable`/decompiled/partial ohne nutzbaren Snapshot; LOCAL-Fälle waren decompiled/medium/untrusted/partial; FALSE-01 war recoverable `WORKSPACE_DIAGNOSTIC` ohne Snapshot.
- Keine Builds, Tests oder Commits ausgeführt. Nach der letzten Code-Map-Änderung wurden die gezielten redigierten MCP-Nachweise wiederholt und ausschließlich im Epic-3-Bericht-Handoff dokumentiert; danach erfolgt keine weitere Dateiänderung.

- Vollständig gelesen: `AGENTS.md`, relevante `.agents/rules/*.mdc`, `Konzept.md`, `roadmap.md`, vorherige `code-map.md` und `implement/SKILL.md`.
- MCP-Projektchecks: `get_index_scope`; `get_file_tree` (Assembly-Unterbaum, Tiefe 3, 97/97, nicht gekürzt); `get_server_health` projektgebunden und aggregiert; `get_feature_context`; `get_class_structure`; `get_symbol_body`; `get_file_skeleton`.
- MCP-Assemblychecks: `inspect_assembly` und `find_assembly_extensions` für alle fünf Labels mit `targetType=assembly` und absolutem Matrixpfad; `find_symbol`, `get_class_structure`, `get_file_skeleton` und `get_symbol_body` für die positiven Fälle.
- Ergebnis: `GIT-01`/`LOCAL-01`/`LOCAL-02`/`LOCAL-03` decompiled, `partial`, ohne Source-Snapshot; `FALSE-01` recoverable `WORKSPACE_DIAGNOSTIC` ohne Snapshot. Signaturen, Attribute, Parameter, generische Signaturen und Bodies waren im bounded Umfang abfragbar.
- Read-only Text-/Testinspektionen wurden nur zur Kontext- und Abdeckungsprüfung verwendet. Es wurden keine Builds, Tests, Produktions-/Konfigurations-/Produktdokumentationsänderungen oder Commits ausgeführt.
- Nach der letzten Änderung an dieser Code-Map wurden ausschließlich redigierte Artefaktprüfungen, Pfad-/Label-Scans und gezielte MCP-Semantik-Spotchecks ausgeführt; der finale Hand-off bezieht sich auf diesen Stand.
