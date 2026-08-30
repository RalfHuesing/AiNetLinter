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

## Betroffene Dateien und Symbole

- Produktionspfad: `src/AiNetLinter/Mcp/Assemblies/Analysis/`
  (`AssemblyAnalysisSession`, `AssemblyAnalysisSessionModels`,
  `AssemblyAnalysisEntry`, `AssemblyAnalysisRegistry` und Resolver/Cache).
- Assembly-Toolpfad: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`
  (`AssemblyAnalysisService`, `AssemblyAnalysisContextFactory`,
  `AssemblyAnalysisSymbolTraversal`, `AssemblyAnalysisSourceToolSupport` und
  die einzelnen Tool-Handler).
- Gemeinsame Symbolgraph-/Dateitool-Pfade unter
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/`,
  `src/AiNetLinter/Mcp/Tools/FileStructure/`,
  `src/AiNetLinter/Mcp/Tools/CallTree/`,
  `src/AiNetLinter/Mcp/Tools/DependencyGraph/` sowie
  `src/AiNetLinter/Mcp/Tools/GetSymbolBodyTool.cs` werden nur angepasst,
  wenn der Assembly-Kontext dort nachweisbar falsch behandelt wird.
- Für EPIC-A konkret bestätigt: `DiffImpactAnalyzer.FindDocumentByPath` ist
  über `SolutionDocumentPathResolver` die gemeinsame Dokumentauflösung für `get_file_skeleton`,
  `get_symbol_body`, Dependency- und Referenzpfade. Die relative Ausgabe-
  normalisierung in `CallGraphTreeBuilder.FormatPath` und
  `DependencyGraphScanner.ToRelativePath` erfolgt über
  `PathNormalizer.ToRelative`; dort wird keine direkte
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
- `README.md`/`Docs/*` werden nur bei einer sichtbaren Vertragsänderung
  aktualisiert; keine Assembly-DLL wird ausgeführt oder verändert.

## Invarianten, Risiken und Unsicherheiten

- Projekt- und Assembly-Sessions bleiben getrennt; externe DLLs werden nicht
  ausgeführt oder verändert.
- EPIC-A umfasst keine Cross-Assembly-Erweiterung aus EPIC-D.
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
  Aufrufer, 7 statisch zugeordnete Tests), `FormatPath` (3 interne Aufrufer,
  keine statische Testzuordnung), `ToRelativePath` (6 interne Aufrufer, 15
  Scanner-Tests) und `CreateProjectInfo` (1 Aufrufer, 13 Session-Tests);
  `FormatPath`, `ToRelativePath` und `CreateProjectInfo` meldeten 0 Violations;
  `DiffImpactAnalyzer.cs` hatte im Zwischenstand einen `MaxLineCount`-Befund,
  der durch die Extraktion in `SolutionDocumentPathResolver.cs` nicht mehr auf
  dem Diff-Hotspot liegt.
- Nach der Korrektur erneut ausgeführt: `find_symbol` für
  `SolutionDocumentPathResolver`, `get_feature_context` für
  `SolutionDocumentPathResolver.Find` (vollständig, 0 Datei-/Symbolviolations),
  `find_references` für `DiffImpactAnalyzer.FindDocumentByPath` (7 Call-Sites,
  vollständig), `get_impact` mit `detailLevel=change-context` (3 geänderte
  Symbole, 7 Call-Sites, 4 Testzuordnungen, 0 Violations) und
  `dependency_graph` für den absoluten `DiffImpactAnalyzer.cs`-Pfad (beide
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
  tests (jeweils grün; vollständige Befunde im Hand-off). Der abschließende
  `get_violations`-Check meldete genau einen strukturellen
  `MaxDirectoryChildren`-Befund (31 statt 30) im Produktionsscope; dieser ist
  als `accepted-deferred`-Tech-Debt dokumentiert. Danach wurde kein Code mehr
  geändert.
