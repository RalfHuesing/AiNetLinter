# Code-Map: decompiled-assembly-analysis-finish2

## Primäre Einstiegspunkte

- `AiNetLinter.Mcp.AssemblyAnalysisDispatcher` in
  `src/AiNetLinter/Mcp/AnalysisToolCall.cs` routet Assembly-Aufrufe getrennt
  vom Projektpfad.
- `AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisService` und
  `AssemblyAnalysisContextFactory` komponieren die Assembly-Analyse-Kontexte.
- `AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisSymbolTraversal`
  verbindet Assembly-Kontext, Roslyn-Symbolgraph und die allgemeinen
  Symbol-/Datei-/Call-Tree-Tools.
- `AiNetLinter.Mcp.Assemblies.Analysis.AssemblyAnalysisSession` liefert
  Snapshot/Generation, dekompilierte Dokumente und Assembly-Identität.
- `AssemblyAnalysisResponse` ergänzt nach dem Dispatcher ausschließlich
  Assembly-Metadaten; die Inspect-/Extensions-Payloads enthalten die einzige
  Diagnostics-Projektion im StructuredContent.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/` enthält Health-Optionen,
  Wire-Records und den Response-Builder. `includeDiagnostics` ist dort eine
  explizite Detailoption; standardmäßig werden nur Diagnosezähler und
  begrenzte Metadaten ausgegeben.

## Betroffene Dateien und Symbole

- Produktionspfad: `src/AiNetLinter/Mcp/Assemblies/Analysis/`
  (`AssemblyAnalysisSession`, `AssemblyAnalysisSessionModels`,
  `AssemblyAnalysisEntry`, `AssemblyAnalysisRegistry` und Resolver/Cache).
- Assembly-Toolpfad: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
  (`AssemblyAnalysisService`, `AssemblyAnalysisContextFactory`,
  `AssemblyAnalysisSymbolTraversal`, `AssemblyAnalysisSourceToolSupport` und
  die einzelnen Tool-Handler). `AssemblyAnalysisResponseLimits` projiziert
  Diagnostics und Referenz-/Session-Listen für die Assembly-Wire-Antworten
  gemeinsam in Text und StructuredContent; Root-/transitive Samples werden
  whitespace-normalisiert und über ein gemeinsames Diagnosebudget begrenzt.
- Gemeinsame Symbolgraph-/Dateitool-Pfade unter
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/`,
  `src/AiNetLinter/Mcp/Tools/FileStructure/`,
  `src/AiNetLinter/Mcp/Tools/CallTree/`,
  `src/AiNetLinter/Mcp/Tools/DependencyGraph/` sowie
  `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` werden nur angepasst,
  wenn der Assembly-Kontext dort nachweisbar falsch behandelt wird.
- Der dokumentbezogene Infrastruktur-Bereich liegt unter
  `src/AiNetLinter/Core/Documents/`: `SolutionDocumentPathResolver` und das
  interne `DocumentContext` verwenden dort den Namespace
  `AiNetLinter.Core.Documents`; `DiffImpactAnalyzer` bleibt unter
  `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` im Namespace `AiNetLinter.Core`.
- Für EPIC-A konkret bestätigt: `DiffImpactAnalyzer.FindDocumentByPath` delegiert
  über `SolutionDocumentPathResolver` die Dokumentauflösung für `get_file_skeleton`,
  `dependency_graph` und dateibasierte `find_references`-Pfade. `get_symbol_body`
  löst Symbole separat über den Identifier-Resolver auf; seine relative Ausgabe
  sowie die Ausgaben von `CallGraphTreeBuilder.FormatPath` und
  `DependencyGraphScanner.ToRelativePath` verwenden `PathNormalizer.ToRelative`;
  dort wird keine direkte
  `Path.GetRelativePath`-Nutzung mehr angenommen.
- `SolutionDocumentPathResolver` vergleicht zuerst sichere absolute/relative
  Pfadvarianten und erlaubt danach ausschließlich bei eindeutigem Treffer den
  reinen Dokument-Basename (`Document.Name`). Mehrdeutige Basenames liefern
  keinen stillschweigend falschen Treffer.
- `AssemblyRoslynWorkspaceFactory.CreateProjectInfo` konstruiert den
  synthetischen Projektpfad aus dem ersten generierten Dokument und ist damit
  die relevante Fallback-Stelle für ein Dokument ohne physisches Verzeichnis.
- Implementiert: `FindDocumentByPath` delegiert die sichere Auflösung (CWD,
  Solution-Verzeichnis, absolute Formen und eindeutiger Basename) an den neuen
  Resolver; `GetFileSkeletonTool`,
  `DependencyGraphTool`, `FindReferencesTool` und der Dependency-Scanner
  nutzen den logischen Eingabepfad. `CallGraphTreeBuilder`,
  `GetSymbolBodyTool` und `DependencyGraphScanner` verwenden für Ausgaben
  `PathNormalizer.ToRelative`.
- MCP-Kontextbefund: Die fünf Einstiegstypen sind im aktuellen Solution-Index
  vorhanden; alle abgefragten Dateien meldeten 0 Violations. `AssemblyAnalysisSession`
  ist mit 394 Codezeilen/44 Membern bereits nahe am Footprint-Budget, daher
  keine breite Zerlegung ohne konkreten EPIC-A-Bedarf.
- EPIC-B-Kontext per MCP bestätigt: `InspectAssemblyTool` (202 Codezeilen),
  `FindAssemblyExtensionsTool` (163), `GetServerHealthTool` (104),
  `GetServerHealthResponseBuilder` (291) und `AssemblyAnalysisDiagnostics`
  sind die relevanten Einstiegspunkte. `AssemblyAnalysisResponseLimits` (226
  physische Zeilen, 207 LOC laut aktuellem MCP) ist der gemeinsame Projektor
  für Diagnostics und Textausgabe. Vor der
  Änderung verwendeten die Handler
  `Take(100)` für aggregierte Diagnostics, begrenzten Referenzlisten nicht und
  Health gab Assembly-Diagnostics standardmäßig vollständig aus. EPIC-B führt
  dafür `AssemblyAnalysisResponseLimits` ein: 20 Diagnostics standardmäßig,
  maximal 50, je Meldung 256 Zeichen, insgesamt 4 KiB; Referenzen und
  Referenz-Sessions werden jeweils auf 32 Einträge und Session-Diagnostics auf
  3 Samples begrenzt. `get_server_health` bleibt standardmäßig kompakt und
  akzeptiert für Detail-Samples `includeDiagnostics`/`maxDiagnostics`.

## Aufrufer und Abhängigkeiten

- Dispatcher/Host → `AssemblyAnalysisService` →
  `AssemblyAnalysisToolSupport`/`AssemblyAnalysisContextFactory` →
  `AssemblyContext` mit dekompilierten Roslyn-Dokumenten.
- `AssemblyAnalysisContextFactory` nutzt Source-Snapshot-Leases nur bei
  passender Source-Zuordnung; ansonsten den dekompilierten Fallback.
- `AssemblyAnalysisSymbolTraversal` wird von Factory und Service für
  Symbol-/Call-Tree-Navigation verwendet; die gemeinsame Datei-/Projekttool-
  Schicht muss `DocumentId`, `SyntaxTree` und `relativeTo` ohne physische
  Dateibasis tolerieren.
- `LinterEngine` verwendet `AiNetLinter.Core.Documents.DocumentContext` an den
  drei verifizierten bisherigen Erzeugungs-/Verwendungsstellen; die
  Namespace-Anpassung ändert nur die interne Dateiorganisation.
- `AssemblyAnalysisSession` hängt an Decompilation-Cache, Snapshot-Lease und
  `AssemblyReferenceResolver`; Projekt- und Assembly-Sessions bleiben getrennt.

## Relevante Tests, Konfiguration und Dokumentation

- Bestehende FastTests: `src/AiNetLinter.FastTests/Mcp/Assemblies/` und
  `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`, insbesondere
  `AssemblyAnalysisSessionTests`, `AssemblyAnalysisContextFactoryTests`,
  `AssemblyAnalysisToolSupportTests` und
  `AssemblyAnalysisDispatcherCapabilityTests`.
- Zu ergänzende EPIC-A-Regressionen bleiben in den bestehenden Assembly-
  Testdateien bzw. einer eng benannten Assembly-Tool-Testdatei; keine neuen
  Task-/Step-Artefakte.
- Integrationstestpfade unter
  `src/AiNetLinter.IntegrationTests/Mcp/` werden für den gezielten Epic-Lauf
  geprüft, aber nur bei tatsächlich notwendigem End-to-End-Vertragsnachweis
  geändert.
- EPIC-B-Regressionen liegen in
  `AssemblyAnalysisDispatcherCapabilityTests` (Root-/transitive Diagnostics,
  Status-/Text-/Structured-Konsistenz, Referenzgrenzen und 4-KiB-Budget),
  `GetServerHealthToolTests` (kompakter Default und begrenzte Detail-Samples)
  sowie `McpServerAssemblyHealthE2ETests` (Schema-/Routing-Vertrag).
- `README.md`/`Docs/*` werden nur bei einer sichtbaren Vertragsänderung
  aktualisiert; keine Assembly-DLL wird ausgeführt oder verändert.

## Invarianten, Risiken und Unsicherheiten

- Projekt- und Assembly-Sessions bleiben getrennt; externe DLLs werden nicht
  ausgeführt oder verändert.
- EPIC-A umfasst keine Cross-Assembly-Erweiterung aus EPIC-D.
- EPIC-B projiziert nur Antwortdaten: Die bestehende Assembly-/Projekt-
  Trennung, Lease-/Snapshot-Ownership und Referenzexpansion bleiben
  unverändert; die gezielte Assembly-Health-Route expandiert Referenzen nur
  bei `includeDiagnostics=true`, während die aggregierte Health-Sicht aus
  Registry-Snapshots ohne transitive Expansion arbeitet.
- `ProjectDiagnostics` dedupliziert normalisierte Root-/transitive Diagnostics
  mit Root-Vorrang, dedupliziert zusätzlich nach der finalen
  `NormalizeForDisplay`-Kürzung, baut Root-/transitive-/Aggregate-Summaries aus
  derselben Auswahl und begrenzt diese Auswahl intern gemeinsam auf 4 KiB;
  `WithoutSamples` leert Samples und setzt Aggregate-/Root-/Transitive-
  `ShownCount` gemeinsam auf 0. Die Inspect-/Extensions-Payloads serialisieren
  die Auswahl jedoch weiterhin mehrfach (`diagnostics`,
  `diagnosticsSummary.samples` und Root-/Transitive-Samples); im Health-Payload
  gilt die Projektion zudem je Assembly. `AssemblyAnalysisResponse.Enrich`
  ergänzt unter `analysis` nur Metadaten und serialisiert dort keine
  Diagnostics-Projektion erneut.
- `complete` mit mindestens einer Root- oder transitiven Diagnose wird in den
  Inspect-/Extensions-Payloads, im `AssemblyAnalysisResponse` und in beiden
  Health-Umwandlungen über `ResolveEffectiveStatus` als `partial` ausgegeben.
  `ProjectAssemblyEntry` verwendet diese effektiven Werte auch für die
  kompakte und detaillierte Health-Projektion.
- Die vier EPIC-B-Formatter-Komplexitätsbefunde sind durch kleine, verhaltens-
  neutrale Struktur-/Append-Helfer beseitigt: `FormatText` delegiert Header,
  Extension-/Kontext- und gemeinsame Diagnostics-Ausgabe; `AppendAssemblySection`
  delegiert Header-, Source- und Diagnostics-Ausgabe.
- Leere physische Pfade dürfen in dekompilierten In-Memory-Dokumenten nicht zu
  `Path.GetFullPath`-/`relativeTo`-Fehlern in `get_call_tree`,
  `get_symbol_body` oder `dependency_graph` führen.
- Stable Symbol IDs und Assembly-Identität müssen aus demselben Snapshotvertrag
  in Datei-, Symbol-, Hierarchie- und Körperantworten stammen; fremde IDs sind
  fachliche Eingabefehler, keine interne Ausnahme.
- Die Dateiadressierung generierter Dokumente ist durch die Varianten- und
  eindeutige Basename-Auflösung zentral behoben; der Workspace-Fallback deckt
  zusätzlich bare generierte Dateinamen ohne physisches Verzeichnis ab.
- Parametermethoden werden weiterhin über Roslyn-Dokumentations-IDs aufgelöst;
  EPIC-A-Tests müssen die Signatur `Save(bool)` und die Assembly-ID-Kohärenz
  über Skeleton/Body schützen.
- MCP-/Test-Reproduktion: Der Route-Test meldete mit `document.Name` bei einem
  generierten `source/...`-Pfad zunächst `RESOURCE_NOT_FOUND`; der neue Resolver
  löst den eindeutigen Basename jetzt auf. `AdhocWorkspace` lässt
  `Solution.FilePath` erwartungsgemäß `null`; der synthetische `Project.FilePath`
  wird aus dem Assembly-Verzeichnis beziehungsweise dem generierten Dokument-
  Verzeichnis gebildet und ist der relevante Fallback.
- Die Stable-ID-Regression war zusätzlich eine Format-Assertion: der bestehende
  Skeleton-Vertrag rendert `id:<stable-id>` ohne Leerzeichen, während Bodies
  `id: \`<stable-id>\`` ausgeben. Die IDs selbst sind über Skeleton/Body und
  `AssemblySymbolIdentity(ContentHash, Generation)` kohärent.
- MCP-Kontext der konkreten Änderungsstellen: `FindDocumentByPath` (7
  Aufrufer, 9 statisch zugeordnete Tests in 2 Dateien), `FormatPath` (3 interne Aufrufer,
  keine statische Testzuordnung), `ToRelativePath` (6 interne Aufrufer, 15
  Scanner-Tests) und `CreateProjectInfo` (1 Aufrufer, 13 Session-Tests);
  `FormatPath`, `ToRelativePath` und `CreateProjectInfo` meldeten 0 Violations;
  `src/AiNetLinter/Core/DiffImpactAnalyzer.cs` hatte im Zwischenstand einen `MaxLineCount`-Befund,
  der durch die Extraktion in `SolutionDocumentPathResolver.cs` nicht mehr auf
  dem Diff-Hotspot liegt.
- Nach der Korrektur erneut ausgeführt: `find_symbol` für
  `SolutionDocumentPathResolver`, `get_feature_context` für
  `SolutionDocumentPathResolver.Find` (vollständig, 0 Datei-/Symbolviolations),
  `find_references` für `DiffImpactAnalyzer.FindDocumentByPath` (7 Call-Sites,
  vollständig), `get_impact` mit `detailLevel=change-context` (3 geänderte
  Symbole, 7 Call-Sites, 4 Testzuordnungen, 0 Violations) und
  `dependency_graph` für den absoluten `src/AiNetLinter/Core/Documents/SolutionDocumentPathResolver.cs`-Pfad (beide
  Richtungen, vollständig; Resolver als ausgehende Abhängigkeit sichtbar).
- Abschluss-Audit im betroffenen Produktionsscope `src/AiNetLinter`:
  `find_duplicates` fand 10 begrenzte Clone-Cluster; der einzige exakte
  Assembly-Cluster betrifft `InspectAssemblyTool`/`FindAssemblyExtensionsTool`
  und liegt außerhalb dieser EPIC-A-Pfadkorrektur. Die übrigen Near-/Fuzzy-
  Kandidaten wurden wegen unterschiedlicher Verantwortung oder fehlender
  sicherer scope-naher Korrektur deferred. `find_dead_code` fand 0 High-/Low-
  Confidence-Kandidaten. `find_magic_values` markierte bestehende
  Wire-/Trust-Strings (`source-backed`, `verified-clean`) und Standardpuffer-
  größen; keine sichere, verhaltensneutrale EPIC-A-Zentralisierung wurde
  vorgenommen.

## Verifikation

- MCP-first ausgeführt mit `find_symbol` auf `AssemblyAnalysis`,
  `AssemblySession`, `Decompiled`, `AssemblyAnalysisRegistry` sowie
  `get_feature_context` auf `AssemblyAnalysisSession`,
  `AssemblyAnalysisContextFactory`, `AssemblyAnalysisToolSupport`,
  `AssemblyAnalysisDispatcher` und `AssemblyAnalysisSymbolTraversal`;
  jeweils `targetType=project`, absoluter Projektroot, Caller-/Test-/Metrik- /
  Violations-Kontext aktiv.
- Ergänzend mit absolutem Projektroot: `find_symbol` für
  `FindDocumentByPath`, `FormatPath`, `ToRelativePath` und `CreateProjectInfo`
  sowie `get_feature_context` für alle vier exakten Methodensymbole mit
  Callers/Tests/Metriken/Violations. Alle Ergebnisse waren vollständig;
  `get_feature_context` mit vollständigen Signaturtexten war erwartungsgemäß
  `SYMBOL_NOT_FOUND` und wurde auf die qualifizierten Methodennamen korrigiert.
- Durchgeführt: `find_duplicates`, `find_dead_code`, `find_magic_values` für
  den geänderten Projektbereich sowie anschließend als letzter codebezogener
  Check `get_violations` mit `targetType=project`, absolutem Projektroot und
  geändertem Scope.
- Durchgeführt: EPIC-spezifische xUnit-v3-Testfilter; solutionweite Gates
  bleiben dem Orchestrator vorbehalten.
- Nach den letzten Codeänderungen verifiziert: die neue Assembly-Pfad-/ID-Regression
  (`AssemblyAnalysisPathContractTests`, 2/2) sowie Assembly-, Call-Tree-,
  Dependency-Graph-, Stable-ID-, Symbol-Body- und File-Skeleton-Komponenten-
  tests (jeweils grün; vollständige Befunde im Hand-off). Der letzte
  `get_violations`-Check im Produktionsscope `src/AiNetLinter/Core` meldete
  0 Violations; der zuvor offene `MaxDirectoryChildren`-Befund ist durch die
  Verlagerung nach `Core/Documents` behoben. Danach wurde kein Code mehr
  geändert.
- EPIC-B-MCP-Kontext: `find_symbol` und `get_feature_context` wurden für die
  drei Handler, den Health-Builder, die Response-Modelle, `AssemblyAnalysisResponse`
  und die Registrierungsstelle mit `targetType=project` sowie dem absoluten
  Projektroot ausgeführt; `get_symbol_body` prüfte die relevanten Handler und
  Payload-Records. Ein initialer falscher Namespace für
  `AssemblyAnalysisResponse` lieferte `SYMBOL_NOT_FOUND` und wurde über
  `find_symbol` korrigiert. Ein späterer MCP-Lauf traf während des
  Solution-Loadings auf `[INFO]`, ein Health-Entry-Kontext lief in den
  300-s-Timeout; lokale Build-/Testverifikation blieb davon unabhängig grün.
- EPIC-B-Audit im Scope `src/AiNetLinter/Mcp`: `find_duplicates` (8 Cluster;
  der exakte Inspect-/Extensions-Adapter ist bewusst ausgenommen; die zuvor
  neu entstandene `AppendDiagnostics`-Ähnlichkeit wurde durch den gemeinsamen
  Helper entfernt), `find_dead_code` (40 Low-Confidence-Kandidaten, 0 High;
  bestehende Infrastruktur-/Interop-/DI-/Serializer-Risiken) und die
  eingegrenzten `find_magic_values`-Läufe (AssemblyAnalysis: 2 bestehende
  Status-Identifier, ServerMaintenance: 4 bestehende Format-/Namenwerte)
  wurden triagiert. Keine sichere scope-nahe Korrektur wurde daraus abgeleitet.
- EPIC-B-lokale Verifikation: `dotnet build AiNetLinter.slnx --no-restore`
  sowie die exakten Filter für
  `AssemblyAnalysisToolTests|AssemblyAnalysisDispatcherCapabilityTests`
  (21/21) und
  `GetServerHealthToolTests|McpServerAssemblyHealthE2ETests` (9/9) waren
  seriell erfolgreich. Der vorherige parallele Wiederholungslauf erzeugte
  lediglich temporäre DLL-Sperren und wurde nicht als fachlicher Befund gewertet.
- EPIC-B-Runde-1-Vollverifikation: Der Solution-Build blieb bei 0 Warnungen und
  0 Fehlern. `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
  meldete 2236 grüne Tests, 2 Skips und 1 bestehenden, isoliert reproduzierbar
  nicht bestätigten `ProjectRegistry`-Fehler; der Einzeltest lief anschließend
  1/1 grün. `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  meldete 371/373 grüne Tests; die 2 Fehler betreffen bestehende
  MCP-Instruktions-/Registrierungsvertragstexte (`sortBy` und `ambiguous`) und
  nicht den EPIC-B-Diff.
- Historischer EPIC-B-`get_violations`-Check vor Runde 1 mit absolutem
  Projektroot und den Scopes `src/AiNetLinter/Mcp`,
  `src/AiNetLinter.FastTests/Mcp/Assemblies` und
  `src/AiNetLinter.IntegrationTests/Mcp/Tools`: Tests jeweils 0; Produktion
  5 Befunde. Davon sind vier neue aktive Produktions-Regelbefunde in
  `FindAssemblyExtensionsTool.FormatText` (kognitive 21, zyklomatisch 15)
  und `GetServerHealthResponseBuilder.AppendAssemblySection` (kognitive 18,
  zyklomatisch 15), die nach dem vorgeschriebenen letzten Check nicht mehr
  korrigiert wurden und im Review nicht als harmlose Deferred-Befunde gelten.
  Der fünfte Befund ist der bestehende
  `AssemblyAnalysisRegistry`-AIContextFootprint (3594 > 2500), außerhalb des
  EPIC-B-Response-Scopes; er bleibt als `promoted-to-project-debt` für den
  Orchestrator sichtbar. Der Korrekturbericht nach `95d29373` weist für die
  Produktion nur diesen bestehenden Registry-Footprint und für Fast-/
  Integration-Testscopes jeweils 0 Violations aus; die vier Formatterbefunde
  sind per aktuellem `metrics_lookup` ebenfalls 0. Die fokussierten
  Korrekturtests waren Fast 21/21 und Integration 9/9. Die globale
  Sample-Projektion war zu diesem Zeitpunkt wegen der erneuten
  `analysis`-Serialisierung und der Präfix-Kollisionen noch nicht abgeschlossen.
- EPIC-B-Korrekturrunde 2: `AssemblyAnalysisResponse.Enrich` serialisiert keine
  Diagnostics-/Summary-Felder mehr unter `analysis`; die Payload-Projektion
  bleibt dort die einzige Diagnostics-Quelle. `ProjectDiagnostics` dedupliziert
  sichtbare Samples nach der 256-Zeichen-Kürzung mit Root-Vorrang und bewahrt
  dabei die Root-/Transitive-Counts. Regressionen in
  `AssemblyAnalysisDispatcherCapabilityTests` prüfen die fehlenden
  `analysis`-Diagnostics, das Budget der jeweiligen Sample-Liste und lange
  Präfixkollisionen; eine Messung der vollständig serialisierten
  StructuredContent-Diagnostics fehlt. Der fokussierte FastTest-Lauf ist mit 22/22, der
  fokussierte Health-/Assembly-Integration-Lauf mit 9/9 bestanden. Der
  vollständige Build ist mit 0 Warnungen und 0 Fehlern grün; die vollständigen
  Nicht-Stress-Läufe meldeten FastTests 2238/2240 (2 Skips) und
  IntegrationTests 371/373. Die zwei Integrationsfehler sind die bekannten,
  nicht kausalen Beschreibungstext-Verträge `ambiguous` und `sortBy` außerhalb
  dieses Scopes. Die drei MCP-Abschlussaudits sind auf dem finalen Working Tree
  erneut gelaufen:
  Produktions-DRY 8 bestehende Cluster (der exakte Adapter bleibt bewusst
  außerhalb des Scopes), Produktions-Dead-Code 40 Low/0 High ohne sicheren
  neuen Fund und 240 Magic-Value-Kandidaten; im konkreten geänderten
  `AssemblyAnalysis`-Produktionspfad 0 Dead-Code-/Magic-Value-Funde. Im
  geänderten Testfile wurden 0 Dead-Code-Funde und 16 bewusst testbezogene
  Magic-Value-Einträge (25 Vorkommen) triagiert; die drei Test-Clonecluster
  liegen in bestehenden External-Source-Tests. Der Korrekturcommit-Impact
  gegenüber `45c9200f` meldete 3 geänderte Dateien, 10 geänderte Symbole,
  25 Call-Sites, 5 Testzuordnungen und 0 Diff-Violations; der unabhängige
  Gesamt-Impact gegenüber `b0ebc8b4` umfasst 12 geänderte Dateien, 68
  geänderte Symbole und 0 Diff-Violations. Die Formatter-Metriken bleiben
  unauffällig:
  `FormatText` kognitiv/zyklomatisch 0/1, `AppendAssemblySection` 0/1.
  Keine Audit-Korrektur war sicher, scope-nah und verhaltensneutral.
  Der letzte codebezogene `get_violations`-Nachweis meldete im Produktionsscope
  ausschließlich den bestehenden `AssemblyAnalysisRegistry`-Footprint; die
  FastTests- und IntegrationTests-Scopes meldeten jeweils 0 Violations.

## EPIC-C — Ressourcen, Konfiguration und Lebensdauer

- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` erweitert den
  bestehenden `ExternalSources`-Settings-Vertrag um ein validiertes gemeinsames
  Optionsmodell für Disk, Memory, Parallelität, residenten Bestand und Idle-TTL.
  `ExternalSourceResourceOptionsLoader` liest dieselben fünf Felder fail-closed
  aus dem vorhandenen JSON-Abschnitt; Defaultwerte bleiben 512 MiB, 512 MiB, 4,
  32 und 45 Minuten.
- `AssemblyAnalysisHostComposition` erzeugt aus Settings und optionalen
  `ExternalResourceRegistryOverrides` zwei getrennte, gleich konfigurierte
  `ExternalResourceRegistry`-Instanzen: eine für Assembly-Sessions und eine für
  Source-Snapshots. Die fünf MCP-/Daemon-CLI-Overrides werden über
  `CliOptions`/`LinterArgs`, den ThinClient und beide Startpfade bis dorthin
  weitergereicht; die Projektregistry bleibt getrennt.
- `ExternalResourceRegistry` bildet Capacity, LRU/TTL, aktive Leases,
  Parallelitäts-Slots und in-flight Materialisierungsreservierungen unter einer
  Sperre ab. Reservierungen deduplizieren Identitäten auch gegenüber residenten
  Einträgen; `PromoteReservation` überführt sie genau einmal in eine Resident-
  Lease. `SourceSnapshotRegistry` koordiniert vor einer Materialisierung die
  Source-Eviction mit dieser Reservation unter derselben Sperrreihenfolge.
  Aktive Snapshot-Leases bleiben dadurch vor Eviction/Dispose geschützt. Die
  Lease-Typen in `AssemblyAnalysisRegistryEntryCreation` stellen idempotente
  Consumer-, Owner-, Reservation- und Operation-Leases bereit.
- `ExternalSourceSnapshotMaterializer` reserviert die konservativ aus dem
  vollständigen Checkout geschätzten Disk-/Memorykosten vor dem Workspace-Load,
  hält die Reservation bis zur Source-Registrierung am Snapshot und überführt sie
  dort atomar in die Resident-Lease; Fehler und Cancellation rollen sie zurück.
  `ExternalSourceRepositoryCacheMaterializer` bereinigt einen frisch reservierten
  Checkout bei Materialisierungsfehlern rollback-sicher und bewahrt die
  ursprüngliche Exception.
- `AssemblyAnalysisEntry` erhält die Registry-Zeitquelle für deterministische
  TTL-Tests; Factory-Fehler und Entry-Dispose entfernen Owner-Leases aus dem
  Ressourcenregister. Bei Assembly-Capacity-Druck retired
  `AssemblyAnalysisRegistry` idle Entries in LRU-Reihenfolge und wartet das
  Retirement vor dem nächsten Acquire; aktive Leases werden nicht angetastet.
  Die Retirement-Transition revalidiert jetzt unter Registry- und Entry-Lock
  `leaseCount == 0` und setzt atomar `closing`, bevor der Creation-Eintrag
  entfernt wird; dadurch kann eine Lease nach der Kandidatenprüfung den Entry
  nicht mehr unbemerkt zur Retirement-Dispose freigeben.
- `AssemblySourceProviderCreation` trennt Producer-CTS von wartenden Consumer-
  `WaitAsync`-Tokens, räumt abgelehnte Snapshots auf und bietet mit
  `AssemblySourceSelectionOrchestrator.DisposeAsync` einen deterministischen
  Join des Producer-Tasks. `Complete()` erfolgt vor der Entfernung aus dem
  Join-Set; `AssemblyAnalysisHostComposition` wartet diesen Join vor Source-
  Registry- und Ressourcen-Dispose ab.
- Der Pipe-Handshake trägt die fünf effektiven External-Limits als optionale
  Felder. Der ThinClient vergleicht explizite Overrides auch beim Connect zu
  einem bestehenden Daemon; alte Partner ohne diese Felder bleiben kompatibel.
- Regressionen liegen in `ExternalResourceRegistryTests`,
  `SourceSnapshotRegistryTests`, `AssemblyAnalysisRegistryTests`,
  `AssemblyAnalysisHostCompositionTests`,
  `AssemblyAnalysisToolSupportCreationBarrierTests` (einschließlich der
  deterministischen Retirement-/Creation-Join-Races), den CLI-/ThinClient-
  Vertragstests und `ExternalSourceSnapshotMaterializerTests`.
- Für diesen Korrekturlauf werden nach der letzten Codeänderung erneut die
  MCP-Audits `find_duplicates`, `find_dead_code` und `find_magic_values` sowie
  ein produktiver `get_violations`-Nachweis ausgeführt. Das ausgeschöpfte
  EPIC-B-Finding `DIAGNOSTICS-SAMPLE-BUDGET` bleibt außerhalb des Scopes.

## EPIC-C-Verifikation

 - Round 2 schließt `TD-EPIC-C-002` mit einer atomaren Zustandsübernahme:
   `TryRemoveEntryForRetirement` hält den Registry-Lock, revalidiert den
   Kandidaten und ruft `AssemblyAnalysisEntry.TryBeginRetirement` unter dem
   Entry-Lock auf. Nur ein Entry mit `leaseCount == 0` kann auf `closing`
   wechseln; eine danach eintreffende Analyse-Lease wird damit nicht mehr auf
   den gerade retired werdenden Entry angewendet. Der deterministische
   Kandidat-zu-Lease-Race-Test liegt in
   `AssemblyAnalysisRegistryRetirementRaceTests`.
 - Round 2 schließt `TD-EPIC-C-008` / den Rest von `TD-EPIC-C-005` mit dem
   umgekehrten Producer-Join: `RunProviderCreationAsync` ruft
   `creation.Complete()` vor der Entfernung aus dem Join-Set auf. Der
   deterministische Orchestrator-Dispose-Race-Test hält genau dieses
   Zwischenfenster offen und prüft, dass
   `AssemblySourceSelectionOrchestrator.DisposeAsync` auf den Producer wartet.
 - Fokussierte Verifikation nach der letzten Codeänderung: betroffene
   FastTests 57/57 und `ExternalSourceSnapshotMaterializerTests` /
   `ThinClientProxySessionContractTests` 7/7.
 - `dotnet build --no-restore`: 0 Warnungen, 0 Fehler.
 - Vollständige Nicht-Stress-Gates: FastTests 2265 bestanden, 2 vorgesehene
   Skips, 0 Fehler; IntegrationTests 373/375 bestanden, 0 Skips, 2 bekannte
   MCP-Registrierungs-/Beschreibungstext-Verträge (`ambiguous` und `sortBy`).
   CLI-Dogfood und Live-Safeguard sind nach der Strukturkorrektur grün.
 - Round-2-Audits: `find_duplicates` meldet 1 bestehendes Near-Clone-Cluster
   in `AssemblyReferenceResolver`; `find_dead_code` meldet 3 Low-/0
   High-Confidence-Kandidaten (darunter ein duplizierter Heuristik-Treffer);
   `find_magic_values` meldet 0 Treffer. Kein Befund ist ein sicherer,
   scope-naher EPIC-C-Refactor.
 - Das ausgeschöpfte EPIC-B-Finding `DIAGNOSTICS-SAMPLE-BUDGET` wurde nicht
   wiedereröffnet. `TD-EPIC-C-006` und `TD-EPIC-C-007` bleiben
   accepted-deferred. Stress wurde nicht ausgeführt.
