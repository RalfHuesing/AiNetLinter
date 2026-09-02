---
status: ready
---

# Konzept: Konsolidierte Optimierung & Fehlerbehebung der dekompilierten Assembly-Analyse

## Ziel und Nutzen

Der AiNetLinter MCP-Server ermöglicht KI-Agenten die semantische und syntaktische Analyse von .NET-Projekten und Binär-Artefakten (`.dll`, `.exe`) über Roslyn und Decompilation (ICSharpCode). Nach mehreren Iterationen und einem umfassenden 360-Grad-Audit über alle 29 MCP-Tools wurden an verschiedenen Stellen verbliebene Bugs, Inkonsistenzen in Tool-Verträgen, Token-Budget-Verschwendungen und Dogfooding-Qualitätsverletzungen identifiziert.

Ziel dieses Konzepts ist die **vollständige, lückenlose Konsolidierung aller Findings, Tech-Debt-Einträge und Verbesserungsvorschläge** aus:
1. `tasks/decompiled-assembly-fix3` (abgeschlossene Vorarbeit, verbliebene Restschulden)
2. `tasks/decompiled-assembly-audit` (systematisches 360-Grad-Audit, Epics 1–8)
3. `tasks/decompiled-assembly-audit-antigravity` (Tool-Gruppen-Audit & Domänen-Reports)

Das resultierende Konzept bündelt alle validen Punkte in vier klare, unabhängige, testgetriebene Umsetzungspakete, bereinigt Duplikate und schafft eine verbindliche Arbeitsgrundlage für die anschließende Umsetzung durch den autonomen Orchestrator.

---

## Verifizierte Ausgangslage und Evidenz

Alle Befunde wurden am aktuellen Codebestand von `AiNetLinter` (C#/.NET 9 / Roslyn) sowie über MCP-Live-Abfragen gegen verschiedene Prüffall-Typen (Source-backed, dekompilierte DLLs, Managed EXE, Native PE Negativfall) verifiziert:

1. **Top-Level Klassen-Crash in `get_symbol_body` (P1 Bug):**
   - *Evidenz:* `AssemblyDecompiledBodyResolver.cs` Zeile 72 ruft `symbol.ContainingType` auf. Bei Top-Level-Typen ist dies `null`, was zu `ToReflectionTypeName(null) == ""` und einer `InvalidOperationException` in `DecompileTypeAsString` führt.
2. **Concurrency-Bug im Decompilation-Cache (P1 Bug):**
   - *Evidenz:* In `AssemblyDecompilationCache.Publish` (Zeile 78–102) wird bei erfolgreichem `TryRead` vorzeitig `return PublishResult(true, generationDirectory)` aufgerufen, während `isPublished` noch `false` ist. Im `finally`-Block wird `generationDirectory` daraufhin sofort physisch von der Festplatte gelöscht.
3. **Framework-Assembly Unification fehlt (P1 Bug):**
   - *Evidenz:* `AssemblyReferenceResolver.IdentityMatches` vergleicht Versions-Strings exakt (`Ordinal`). Assemblies mit Abhängigkeiten auf z. B. `mscorlib 1.0.3300.0` oder ältere Framework-Versionen scheitern auf modernen Systemen kaskadierend mit `version_mismatch` und erzeugen falsche `CS0246`-Fehler.
4. **Skeleton-DocCommentId vs Semantische ID Mismatch (P1 Bug):**
   - *Evidenz:* `GetFileSkeletonTool` generiert rein syntaktische DocCommentIds. In fehlertoleranten Snapshots mit unaufgelösten Typen erzeugt Roslyn semantische IDs mit `~`/`?`. `SymbolIdentifierResolver` scheitert beim exakten Match -> `SYMBOL_NOT_FOUND`.
5. **Projekt-Health im Daemon-Proxy-Modus defekt (P1 Bug):**
   - *Evidenz:* `GetServerHealthTool.ExecuteAsync` fragt die lokale Instanz-Registry ab. Im Daemon-Client-Proxy antwortet der Call mit `PROJECT_NOT_INITIALIZED`, obwohl das Projekt im Hintergrund-Daemon geladen ist.
6. **Irreführender Vollständigkeitshinweis in `find_references` (P2 Bug):**
   - *Evidenz:* Bei `contentMode=decompiledSignatureOnly` findet Roslyn naturgemäß 0 Aufrufstellen in Rümpfen. `TransitiveCallGraphFormatter` hängt fälschlich an: `[HINWEIS]: Diese Daten sind vollstaendig fuer den angefragten Scope — kein zusaetzliches Read/Grep noetig.` (False Negative mit irreführend hoher Konfidenz).
7. **Namespace-Dumping verdrängt Typen im 8-KB-Budget (P2 Optimierung):**
   - *Evidenz:* `InspectAssemblyFormatter` listet alle Namespaces ungekürzt auf. Bei großen Assemblies (z. B. 64 Namespaces) belegt dies ~2.5 KB, woraufhin die Budget-Projektion Member und Typen vollständig auf 0 kürzt.
8. **Fehlende Schema-Parameter & Assembly-Routen (P2 Missing Features):**
   - `find_assembly_extensions` erzwingt fest `ExpandAssemblyReferences: true` ohne `includeReferences`-Parameter im Schema.
   - `get_impact` unterstützt für `symbolIdentifier` nur `ProjectCall` und wirft bei Assembly-Zielen pauschal `ASSEMBLY_TARGET_UNSUPPORTED`.
   - `instructions.md` behauptet universelle `targetType='assembly'`-Unterstützung für alle Tools, obwohl nur 13 von 27 Tools dies unterstützen.
9. **Dogfooding `AIContextFootprint`-Verstöße (P2 Qualität):**
   - 5 Klassen überschreiten leicht das Zeilenlimit von 2500 Zeilen transitiv (`AssemblyAnalysisRegistryEvictionCoordinator`, `AssemblyReferenceSessionExpander`, `AssemblyNavigationSupport`, `AssemblyReferenceNavigator`).

---

## Betroffene Bereiche und Komponenten

- **Decompilation & Body-Auflösung:** `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`, `AssemblyDecompilationAdapter.cs`, `AssemblyRoslynWorkspaceFactory.cs`.
- **Cache & Lebenszyklus:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationCache.cs`, `AssemblyCacheCleanup.cs`, `AssemblyAnalysisRegistry.cs`, `AssemblyAnalysisSession.cs`.
- **Referenzauflösung & Binding:** `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs`, `AssemblyDiagnosticCodes.cs`.
- **Symbol-Identifikation & Navigation:** `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs`, `AssemblyFindReferencesTool.cs`, `TransitiveCallGraphFormatter.cs`, `AssemblySymbolSearch.cs`, `FindSymbolTool.cs`.
- **MCP-Tool-Registrierungen & Schemas:** `AssemblyAnalysisToolRegistrations.cs`, `SymbolGraphToolRegistrations.cs`, `FileStructureToolRegistrations.cs`, `ServerMaintenanceToolRegistrations.cs`, `AnalysisToolCall.cs`, `ServerInstructions.cs`.
- **Formatierung & Response-Budgets:** `InspectAssemblyFormatter.cs`, `AssemblyAnalysisResponseLimits.cs`, `AssemblyAnalysisResponseLimits.Budget.cs`, `GetNamespaceTreeTool.cs`, `GetFileSkeletonTool.cs`.
- **Dokumentation & Test-Suite:** `Docs/agent-api.md`, `Docs/configuration.md`, `Docs/integration.md`, `README.md`, `instructions.md`, `FastTests`, `IntegrationTests`.

---

## Zielvertrag und Muss-Kriterien

1. **Stabile Body-Dekomposition:** `get_symbol_body` muss für alle unterstützten C#-Symbole (Top-Level-Klassen, Structs, Enums, Interfaces, Records, Methoden, Konstruktoren, Property-Getter/Setter und Indexer) deterministisch den dekompilierten Rumpf liefern oder einen typisierten `unavailable`-Status mit sauberem Grund zurückgeben; Ausnahmen wie `InvalidOperationException` sind unzulässig.
2. **Race- und Lösch-freier Cache:** `AssemblyDecompilationCache.Publish` darf niemals ein existierendes oder als erfolgreich gemeldetes Cache-Verzeichnis im `finally`-Block löschen.
3. **Robuste Framework-Unification:** `AssemblyReferenceResolver` muss bekannte Framework- und Core-Assemblies (z. B. `mscorlib`, `System.*`, `Microsoft.*`, `WindowsBase*`) mit flexibler Versionsunification auflösen, um unnötige `version_mismatch`-Warnungen und Folgefehler zu vermeiden.
4. **Konsistente Symbol-IDs zwischen Skeleton und Folge-Tools:** Eine von `get_file_skeleton` ausgegebene Symbol-ID muss von `get_symbol_body` und `find_references` auch bei dekompilierten oder fehlertoleranten Snapshots fehlertolerant aufgelöst werden können.
5. **Proxy-transparenter Health-Status:** `get_server_health` mit `targetType='project'` muss im Daemon-Proxy-Modus den Status des im Daemon geladenen Projekts zurückgeben, nicht die leere lokale Client-Registry abfragen.
6. **Wahrheitsgetreue Vollständigkeit:** Bei dekompilierten Snapshots ohne Methodenrümpfe (`decompiledSignatureOnly`) darf `find_references` keinen irreführenden Sufficiency-Vollständigkeitshinweis erzeugen.
7. **Informationsdichte unter Response-Limits:** `InspectAssemblyFormatter` muss Namespaces bei Überschreiten einer Grenze (z. B. >10) kompakt zusammenfassen, damit Typen- und Methodensignaturen im 8-KB-Budget erhalten bleiben.
8. **Konsistente Tool-Schemas und Ergonomie:**
   - `find_assembly_extensions` erhält den Parameter `includeReferences` (Default `false`).
   - `get_impact` unterstützt `targetType='assembly'` für `symbolIdentifier`.
   - `get_file_skeleton` akzeptiert `filePath` (String) als Alias/Fallback für `filePaths` (Array).
   - `metrics_tree` setzt standardmäßig `mode = "code_size"`.
   - `get_namespace_tree` gibt bei Assembly-Targets einen passenden `# Assembly Overview`-Header aus.
   - `instructions.md` und `ServerInstructions.cs` beschreiben die tatsächliche 13-Tool-Capability-Matrix wahrheitsgemäß.
9. **Dogfooding-Konformität:** Alle produktiven Klassen halten die Regel `AIContextFootprint <= 2500 Zeilen` ein; der Linter- und Safeguard-Lauf über die eigene Solution ist sauber.

---

## Nicht-Ziele und Scope-Grenzen

- Keine Ausführung, kein Starten von Prozessen und kein dynamisches Laden von Drittanbieter-Assemblies via `AssemblyLoadContext` oder Reflection.
- Keine universelle Unterstützung aller 27 MCP-Tools für Assembly-Targets (Linter-, Git- und Solution-Audit-Tools wie `get_violations`, `safeguard` bleiben reine Projekt-Tools).
- Kein globaler unbeschränkter GAC-Scan über das gesamte Dateisystem ohne konfigurierte Pfadgrenzen.
- Keine Entfernung von `isError=false` bei recoverable Nutzereingabefehlern (Einhaltung der verbindlichen `Mcp/IsErrorPolicy.md`).
- Keine Änderungen an historischen Task-Verzeichnissen; alle Dokumentationen und Nachweise verbleiben in diesem Ziel-Task.

---

## Betriebs-, Sicherheits- und Lebenszeitmodell

- **Metadata-Only Invariante:** Fremde Assemblies werden ausschließlich über Roslyn-Metadaten und den ICSharpCode-Decompiler analysiert.
- **Fail-Closed Redaction:** Interne Dateipfade, Stack-Traces oder geheime Strings werden in Fehlermeldungen und Diagnosen redigiert; native PE-Dateien (`FALSE-01`) werden deterministisch mit recoverablem Fehler abgewiesen.
- **Lebenszyklus & Ressourcen:** Assembly-Sessions unterliegen einer Lease-Bindung und einer Idle-TTL. Transitive Referenz-Sessions dürfen den Speicher nicht unbegrenzt belasten und werden nach Ablauf von Leases priorisiert bereinigt.

---

## Geplante Umsetzungspakete

### Paket 1: Kritische Korrektheits-Bugs & Stabilität (P1 Core Fixes)
**Intention:** Behebung aller harten Laufzeitfehler, Abstürze und Datenverluste in der Decompilation-, Cache- und Resolver-Pipeline.

1. **Top-Level-Typ & Accessor Support in `AssemblyDecompiledBodyResolver`:**
   - Unterstützung für `INamedTypeSymbol` (Klassen, Structs, Enums, Interfaces) direkt via `ToReflectionTypeName(symbol)` ohne `ContainingType`.
   - Erweiterung von `MatchesMember` für `PropertyDeclarationSyntax` / `AccessorDeclarationSyntax` bei Getter/Setter-Methoden.
   - Unit-Tests für Klassen-, Interface- und Property-Body-Dekomposition in `ManagedAssemblyBinaryTests.cs` und `AssemblyAnalysisToolSupportTests.cs`.
2. **Cache-Publish Concurrency & Lösch-Bug:**
   - In `AssemblyDecompilationCache.Publish` den `finally`-Block so absichern, dass `DeleteDirectory` nur aufgerufen wird, wenn weder das Publishing erfolgreich war noch ein bereits publiziertes Verzeichnis zurückgegeben wird.
   - Gezielter Concurrency-Test mit parallelen Threads auf denselben Cache-Key.
3. **Framework-Assembly Unification in `AssemblyReferenceResolver`:**
   - Implementierung von Versions-Toleranz und Unification für Standard-Framework-Bibliotheken (`mscorlib`, `System.*`, `Microsoft.*`, `WindowsBase*`).
   - Verhindern kaskadierender `version_mismatch`-Warnungen bei abwärtskompatiblen Systembibliotheken.
4. **DocCommentId-Auflösung zwischen Skeleton und Resolver:**
   - In `SymbolIdentifierResolver.TryResolveByStableIdAsync` bei Assembly-Targets einen Syntax-/Namen-basierten Fallback ergänzen, falls Roslyns semantische ID wegen unaufgelöster Typen abweicht (`~`/`?`-Formatierung).
5. **Daemon-Proxy Health-Routing:**
   - In `ServerMaintenanceToolRegistrations.cs` und `GetServerHealthTool.cs` sicherstellen, dass bei `targetType='project'` im Daemon-Modus der Projektstatus über den Daemon-Kontext abgefragt wird.

### Paket 2: Tool-Verträge, Schemas & Entwicklerergonomie (P2 Contracts & DX)
**Intention:** Konsolidierung der MCP-Schnittstellen, Schemas, Parameter und Ausgabetexte über alle Werkzeuge.

1. **`find_assembly_extensions` Parameter `includeReferences`:**
   - Ergänzung von `bool includeReferences = false` im Tool-Schema, DTO und Dispatcher; Beseitigung der festen `ExpandAssemblyReferences: true`-Kopplung.
2. **`get_impact` für Assembly-Targets:**
   - Bereitstellung eines `AssemblySessionCall`-Zweigs in `SymbolGraphToolRegistrations.cs` für `get_impact(symbolIdentifier="...")`.
3. **Parameter-Toleranz & Default-Werte:**
   - `get_file_skeleton`: Unterstützung von `filePath` (String) neben `filePaths` (Array).
   - `metrics_tree`: Default `mode = "code_size"` im Schema und Registrar.
4. **Formatierung & UI-Präzisierung:**
   - `get_namespace_tree`: Header `# Assembly Overview: <Name>` bei Assembly-Zielen.
   - `AssemblyFindReferencesTool`: Unterdrückung des Sufficiency-Vollständigkeitshinweises bei `decompiledSignatureOnly`; stattdessen Ausgabe eines erklärenden Hinweises.
   - `McpToolResults`: Deterministischer Hinweis bei nativen PE-Dateien (kein sinnloser Retry-Hint).
   - Beseitigung falscher Trunkierungs-Flags (`assembliesTruncated` vs `resultsTruncated`).
5. **Dokumentations- und Instruktionsabgleich:**
   - `instructions.md` und `ServerInstructions.cs` auf die tatsächliche 13-Tool-Capability-Matrix anpassen.
   - `README.md` und `Docs/configuration.md` bzgl. `.exe`-Targets und `memberNames`-Parametern aktualisieren.

### Paket 3: Token-Budget, Response-Limits & Dogfooding (P2 Efficiency & Quality)
**Intention:** Optimierung der Antwortgrößen, Vermeidung unnötiger Arbeit und Einhaltung eigener Architektur-Regeln.

1. **Namespace-Trimming in `InspectAssemblyFormatter`:**
   - Begrenzung der ausgegebenen Namespaces auf maximal 10 mit Zusammenfassung (`Top 10 Namespaces und X weitere`), um Typen und Member im 8-KB-Budget nicht zu verdrängen.
2. **Roslyn Compilation Error-Reduktion bei Signature-Only:**
   - Optimierung der dekompilierten Stub-Erzeugung (z. B. leere Rümpfe `{ throw null!; }`), um hunderte synthetische `CS0501`-Fehlerobjekte im Roslyn-Workspace zu vermeiden.
3. **Behebung der 5 `AIContextFootprint`-Linter-Verstöße:**
   - Einführung schlanker Interfaces oder Aufteilung von Abhängigkeiten in:
     - `AssemblyAnalysisRegistryEvictionCoordinator.cs`
     - `AssemblyReferenceSessionExpander.cs`
     - `AssemblyNavigationSupport.cs`
     - `AssemblyReferenceNavigator.cs`
   - Absicherung, dass alle 4 Klassen unter 2500 Zeilen transitiven Footprint fallen.
4. **Referenz-Session Lebenszeit & Memory-Footprint:**
   - Kürzere TTL oder aggressiveres Eviction-Handling für temporäre Referenz-Sessions nach Abschluss des übergeordneten Tool-Calls.

### Paket 4: Test-Matrix, Regressionen & Nachweise (P1/P2 Verification)
**Intention:** Absicherung aller Verträge durch automatisierte Fast- und Integration-Tests.

1. **Erweiterung der FastTests:**
   - Direkte Unit-Tests für `AssemblyDecompiledBodyResolver` (Klassen, Structs, Enums, Accessors).
   - Concurrency- und Publish-Race-Tests für `AssemblyDecompilationCache`.
   - Versionsunification-Tests für `AssemblyReferenceResolver`.
   - Tool-Ergonomie-Tests (`metrics_tree` Default, `get_file_skeleton` String-Parameter, `get_namespace_tree` Assembly-Header).
2. **Erweiterung der IntegrationTests:**
   - E2E-Matrix für `get_impact` auf Assembly-Targets.
   - Daemon-Proxy Test für `get_server_health(targetType='project')`.
   - Response-Budget-Tests für große Assemblies (Verifikation, dass Typen/Member nicht durch Namespaces verdrängt werden).
3. **Abschluss-Verifikation:**
   - Voller grüner Durchlauf von `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
   - Warnungs- und fehlerfreier Build (`dotnet build`).
   - Sauberer `get_violations`- und `safeguard`-Durchlauf über die eigene Solution.

---

## Vollständige Finding-Konsolidierungsmatrix (Index aller Quellen)

Um sicherzustellen, dass **kein einziger Befund** aus den drei Quellverzeichnissen verloren geht, ordnet die folgende Matrix jeden identifizierten Befund zu:

| Original Finding-ID | Quell-Dokument / Verzeichnis | Kurzbeschreibung | Einstufung & Disposition | Konsolidiertes Zielpaket |
|---|---|---|---|---|
| **TD-ASM-001** / FINDING-SG-01 / FINDING-EPIC02-01 | `audit-antigravity/audit-symbol-graph-tools.md`, `epic-02` | `get_symbol_body` Crash bei Top-Level-Klassen (`ContainingType == null`) | Bug (P1) – zu beheben | **Paket 1** |
| **TD-ASM-002** / FINDING-EPIC04-01 | `audit-antigravity/epic-04-session-cache-lifetime.md` | `AssemblyDecompilationCache.Publish` löscht Generation im `finally`-Block | Bug (P1) – zu beheben | **Paket 1** |
| **TD-ASM-003** / FINDING-SG-02 / FINDING-EPIC05-01 | `audit-antigravity/audit-symbol-graph-tools.md`, `epic-05` | Falscher Sufficiency-Vollständigkeitshinweis bei `find_references` auf Stubs | Bug (P2) – zu beheben | **Paket 2** |
| **TD-ASM-004** / FINDING-EPIC03-01 | `audit-antigravity/epic-03-references-source-selection-diagnostics.md` | Exakter Versionsvergleich verhindert Framework-Unification in `AssemblyReferenceResolver` | Bug (P1) – zu beheben | **Paket 1** |
| **TD-ASM-005** / FINDING-EPIC06-01 | `audit-antigravity/epic-06-response-token-runtime-efficiency.md` | Unkontrollierter Namespace-Dump in `InspectAssemblyFormatter` verdrängt Typen | Optimierung (P2) – zu beheben | **Paket 3** |
| **TD-ASM-006** / FINDING-EPIC01-02 / 04 / E1-BUG-01 | `audit-antigravity/epic-01`, `audit/tech-debt.md` | `find_assembly_extensions` fehlt `includeReferences` Parameter im Schema | Missing Feature (P2) – zu beheben | **Paket 2** |
| **TD-ASM-007** / FINDING-FS-02 / FINDING-EPIC05-02 | `audit-antigravity/audit-file-structure-tools.md`, `epic-05` | `get_namespace_tree` gibt irreführenden `# Solution Overview`-Header aus | UI/Bug (P3) – zu beheben | **Paket 2** |
| **TD-ASM-008** / FINDING-EPIC01-01 | `audit-antigravity/epic-01-mcp-contracts-and-discoverability.md` | `instructions.md` suggeriert universellen Assembly-Support statt 13-Tool-Matrix | Doku/Bug (P2) – zu beheben | **Paket 2** |
| **TD-FS-001** / FINDING-FS-01 / E2-BUG-03 | `audit-antigravity/audit-file-structure-tools.md`, `audit/tech-debt.md` | `get_file_skeleton` DocCommentIds in fehlertoleranten Snapshots nicht auflösbar | Bug (P1) – zu beheben | **Paket 1** |
| **TD-FS-002** / FINDING-FS-03 | `audit-antigravity/audit-file-structure-tools.md` | Parameter `filePath` (String) in `get_file_skeleton` abfangen | Optimierung (P2) – zu beheben | **Paket 2** |
| **TD-CTX-001** / FINDING-CTX-01 | `audit-antigravity/audit-context-testing-metrics-tools.md` | `get_server_health` mit `targetType='project'` scheitert im Daemon-Proxy | Bug (P1) – zu beheben | **Paket 1** |
| **TD-CTX-002** / FINDING-CTX-02 | `audit-antigravity/audit-context-testing-metrics-tools.md` | `metrics_tree` fehlt Default-Wert für `mode` (`code_size`) | Optimierung (P3) – zu beheben | **Paket 2** |
| **TD-SG-001** / FINDING-SG-04 / FINDING-EPIC05-04 | `audit-antigravity/audit-symbol-graph-tools.md`, `epic-05` | `get_impact` unterstützt keine Assembly-Targets für `symbolIdentifier` | Missing Feature (P2) – zu beheben | **Paket 2** |
| **TD-QL-001** / FINDING-QL-01 / E3-OPT-01 / E7-OPT-01 | `audit-antigravity/audit-quality-lint-audit-tools.md`, `audit/tech-debt.md` | 5 `AIContextFootprint`-Warnungen in Assembly-Coordinators und Navigators | Dogfooding (P2) – zu beheben | **Paket 3** |
| **FINDING-EPIC02-02** | `audit-antigravity/epic-02-decompilation-and-semantic-snapshot.md` | `AssemblyDecompiledBodyResolver` scheitert bei Property-Accessor-Methoden | Bug (P2) – zu beheben | **Paket 1** |
| **FINDING-EPIC02-03** | `audit-antigravity/epic-02-decompilation-and-semantic-snapshot.md` | Synthetische Roslyn-Compilation generiert hunderte `CS0501`-Fehler | Optimierung (P2) – zu beheben | **Paket 3** |
| **FINDING-EPIC02-04** | `audit-antigravity/epic-02-decompilation-and-semantic-snapshot.md` | XML-Dokumentation aus Begleitdateien (`ShowXmlDocumentation = false`) | Missing Feature (P3) – evaluieren | **Paket 3 / Später** |
| **FINDING-EPIC03-02** | `audit-antigravity/epic-03-references-source-selection-diagnostics.md` | Konfigurationspfade in Source-Mapping-Diagnosen ausweisen | Optimierung (P2) – zu beheben | **Paket 2** |
| **FINDING-EPIC03-03** / E3-MISSING-03 | `audit-antigravity/epic-03`, `audit/tech-debt.md` | Fehlende GAC- und Reference-Assembly-Suchpfade | Missing Feature (P2) – evaluieren | **Paket 1 / Später** |
| **FINDING-EPIC04-02** / E6-OPT-03 | `audit-antigravity/epic-04`, `audit/tech-debt.md` | Hohe Speicherlast durch permanente Referenz-Sessions im Daemon | Optimierung (P2) – zu beheben | **Paket 3** |
| **FINDING-EPIC04-03** | `audit-antigravity/epic-04-session-cache-lifetime.md` | Manuelles Invalidieren von Assembly-Sessions im Daemon | Missing Feature (P3) – evaluieren | **Paket 2 / Später** |
| **FINDING-EPIC05-03** | `audit-antigravity/epic-05-navigation-and-query-correctness.md` | `find_symbol` ohne `includeReferences` gibt bei 0 Treffern keinen Tipp | Optimierung (P2) – zu beheben | **Paket 2** |
| **FINDING-EPIC06-02** | `audit-antigravity/epic-06-response-token-runtime-efficiency.md` | Hoher Token-Footprint durch parallele Markdown- & JSON-Payloads | Optimierung (P2) – evaluieren | **Paket 3** |
| **FINDING-EPIC06-03** | `audit-antigravity/epic-06-response-token-runtime-efficiency.md` | Fehlender `compact`-Modus für schnelle Typen-Übersicht | Missing Feature (P3) – evaluieren | **Paket 3** |
| **FINDING-EPIC07-01** / E8-BUG-01 | `audit-antigravity/epic-07`, `audit/tech-debt.md` | Irreführender Retry-Hint bei deterministisch nicht-.NET-Dateien | Optimierung (P2) – zu beheben | **Paket 2** |
| **FINDING-EPIC07-02** | `audit-antigravity/epic-07-operations-security-error-handling.md` | Dedizierte Diagnose für C++/CLI Mixed-Mode Assemblies | Missing Feature (P3) – evaluieren | **Paket 2 / Später** |
| **FINDING-EPIC08-01** | `audit-antigravity/epic-08-test-and-documentation-evidence.md` | Fehlender Testfall für `get_symbol_body` auf Typ-Ebene | Testlücke (P2) – zu beheben | **Paket 4** |
| **FINDING-EPIC08-02** | `audit-antigravity/epic-08-test-and-documentation-evidence.md` | Fehlende Concurrency-Stress-Tests für Cache-Publishing | Testlücke (P2) – zu beheben | **Paket 4** |
| **FINDING-EPIC08-03** | `audit-antigravity/epic-08-test-and-documentation-evidence.md` | `memberNames`-Parameter in `Docs/configuration.md` nicht synchron | Doku (P3) – zu beheben | **Paket 2** |
| **FINDING-QL-02** | `audit-antigravity/audit-quality-lint-audit-tools.md` | `find_magic_values` scannt standardmäßig Test-Assertions | Optimierung (P3) – evaluieren | **Paket 2 / Später** |
| **E1-BUG-02** | `audit/tech-debt.md` | Abweichendes dokumentiertes Response-Budget | Doku/Bug (P2) – zu beheben | **Paket 2** |
| **E1-BUG-03** | `audit/tech-debt.md` | README nennt nur DLL für Assembly-Ziele | Doku (P3) – zu beheben | **Paket 2** |
| **E1-OPT-01** | `audit/tech-debt.md` | Dynamischer Default für `includeReferences` in `inspect_assembly` | Doku/Schema (P2) – zu beheben | **Paket 2** |
| **E1-MISSING-01** | `audit/tech-debt.md` | Maschinenlesbare Assembly-Capability im Schema | Schema (P2) – evaluieren | **Paket 2** |
| **E2-BUG-01** / E8-MF-03 | `audit/tech-debt.md` | Cache-Roundtrip verliert Dokument-Metadaten | Bug/Test (P1) – zu beheben | **Paket 1 / 4** |
| **E2-BUG-02** | `audit/tech-debt.md` | Uneindeutige Zeilenbasis in `get_class_structure` | Doku/Bug (P2) – zu beheben | **Paket 2** |
| **E2-OPT-01** | `audit/tech-debt.md` | Wiederholte On-demand-Body-Dekomposition | Optimierung (P2) – evaluieren | **Paket 3** |
| **E3-BUG-02** | `audit/tech-debt.md` | Referenzknotenlimit projiziert Zustand uneinheitlich | Bug (P1) – zu beheben | **Paket 1** |
| **E3-OPT-02** | `audit/tech-debt.md` | Kandidaten im Reference-Resolver mehrfach gelesen | Optimierung (P2) – evaluieren | **Paket 3** |
| **E3-MISSING-01** / E5-MF-03 | `audit/tech-debt.md` | Consumer-Kontext für Extension-Prüfung | Missing Feature (P1) – zurückgestellt | **Accepted Deferred** |
| **E3-MISSING-02** / E8-MF-02 | `audit/tech-debt.md` | Binary-zu-Source-Identität nicht attestiert | Missing Feature (P1) – zurückgestellt | **Accepted Deferred** |
| **E4-BUG-01..05** / E8-MF-04 | `audit/tech-debt.md` | Lebenszeit-, Cancellation- & Retirement-Grenzfälle | Robustheit (P1/P2) – absichern | **Paket 1 / 4** |
| **E4-OPT-01..03** | `audit/tech-debt.md` | Registry Retirement-Tasks & Pfad-Casing | Optimierung (P2) – zu beheben | **Paket 1** |
| **E4-MF-01..03** | `audit/tech-debt.md` | Root-Cleanup & Lifecycle-Health-Metriken | Missing Feature (P2) – evaluieren | **Paket 3 / Später** |
| **E5-BUG-01** | `audit/tech-debt.md` | Root-Treffer durch globale Referenzsortierung verdrängt | Bug (P1) – zu beheben | **Paket 1** |
| **E5-BUG-02** | `audit/tech-debt.md` | Trefferlisten-Kappung wird als Assembly-Kappung markiert | Bug (P2) – zu beheben | **Paket 2** |
| **E5-BUG-03** | `audit/tech-debt.md` | Referenz-Stable-ID für Body-Folgeabfrage nicht auflösbar | Bug (P1) – zu beheben | **Paket 1** |
| **E5-BUG-05** | `audit/tech-debt.md` | Response-Budgettrimming als Extension-Trunkierung markiert | Bug (P2) – zu beheben | **Paket 2** |
| **E5-MF-01** | `audit/tech-debt.md` | Referenzsicht für Struktur- und Metriktools | Missing Feature (P2) – zurückgestellt | **Accepted Deferred** |
| **E5-MF-02** | `audit/tech-debt.md` | Signatur-only Basis in Calltree/Metrics explizit projizieren | Optimierung (P2) – zu beheben | **Paket 2** |
| **E6-BUG-01** | `audit/tech-debt.md` | Response-Budget prüft Kanäle getrennt statt gesamt | Bug (P2) – zu beheben | **Paket 3** |
| **E6-BUG-02** | `audit/tech-debt.md` | Irreduzible feste Metadaten können Budget überschreiten | Bug (P2) – zu beheben | **Paket 3** |
| **E6-OPT-01..02** | `audit/tech-debt.md` | Einzelweises Trimming & Query-Limits | Optimierung (P2) – zu beheben | **Paket 3** |
| **E6-OPT-04** | `audit/tech-debt.md` | Diagnose-Samples nicht byteeffizient repräsentativ | Optimierung (P3) – zu beheben | **Paket 3** |
| **E6-MF-01..02** | `audit/tech-debt.md` | Budgettelemetrie & Namespace-Trimming sichtbar | Missing Feature (P2/P3) – zu beheben | **Paket 3** |
| **E7-BUG-01** | `audit/tech-debt.md` | Assembly-Fehlerpfad redigiert Rohpfade nicht | Bug (P1) – zu beheben | **Paket 2** |
| **E7-BUG-02** | `audit/tech-debt.md` | Interne Creation-Cancellation als harter Fehler klassifiziert | Bug (P2) – zu beheben | **Paket 1** |
| **E7-MF-01** | `audit/tech-debt.md` | Assembly-Health weist Lifecycle unvollständig aus | Missing Feature (P2) – evaluieren | **Paket 2** |
| **E8-OPT-01** | `audit/tech-debt.md` | Statische Testzuordnung unterschätzt indirekte Abdeckung | Optimierung (P2) – evaluieren | **Paket 4** |
| **E8-MF-01** | `audit/tech-debt.md` | Öffentlicher Assembly-Capability-Regressionstest fehlt | Testlücke (P1) – zu beheben | **Paket 4** |
| **AUD-N01..N04** | `audit/tech-debt.md` | DRY-Prüfhelfer, Marker & Fallback-Fehlermeldungen | Refactoring (P3) – zu beheben | **Paket 1 / 3** |
| **fix3 Restschulden** | `fix3/tech-debt.md` | Overload-Randtypen (`ref readonly`, etc.), Take(20) Sample-Prio | Project Debt (P2) – dokumentiert | **Paket 1 / 3** |

---

## Test- und Verifikationsvertrag für die spätere Umsetzung

Für jedes Paket ist eine eigenständige, automatisierte Verifikation erforderlich:

1. **Paket 1 (Core Fixes):**
   - Unit-Tests für `AssemblyDecompiledBodyResolver`: Top-Level Klassen (`INamedTypeSymbol`), Structs, Enums, Interfaces, Property Getters/Setters.
   - Concurrency-Test für `AssemblyDecompilationCache.Publish` mit synchronen und asynchronen parallelen Lese-/Schreibzugriffen.
   - Version-Unification Test für `AssemblyReferenceResolver` mit simulierten `mscorlib 1.0.3300.0`- und `System.Runtime 4.0.0.0`-Kandidaten.
   - Daemon-Proxy Health-Test für `get_server_health(targetType='project')`.
2. **Paket 2 (Contracts & DX):**
   - Schema- und Dispatcher-Tests für `find_assembly_extensions(includeReferences=false/true)`.
   - Tool-Tests für `get_impact` mit `targetType='assembly'` und `symbolIdentifier`.
   - Ergonomie-Tests für `get_file_skeleton(filePath="...")`, `metrics_tree(mode=null)`, `get_namespace_tree` Assembly-Header.
   - Verifikation der Tool-Instruktionen (`instructions.md`) gegen alle 27 Tools.
3. **Paket 3 (Efficiency & Quality):**
   - Budget-Projektionstest mit einer Assembly mit >50 Namespaces: Verifikation, dass Typen- und Member-Listen nicht auf 0 gekürzt werden.
   - Linter-Prüfung (`dotnet run --project src/AiNetLinter -- src/AiNetLinter` bzw. `get_violations`): 0 Verstöße gegen `AIContextFootprint` in den Assembly-Modulen.
4. **Paket 4 (Gesamtabschluss):**
   - `dotnet build` (warnungs- und fehlerfrei, `TreatWarningsAsErrors=true`).
   - `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` (100% grün).
   - `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` (100% grün).
   - `safeguard`-Score über das gesamte Repository `>= 8.0/10` (Grün).

---

## Offene Punkte und Freigabestatus

Das Konzept ist **vollständig freigegeben (`status: ready`)** und für die Übergabe an den autonomen Orchestrator bereit.

Bestätigte Fachentscheidungen:
1. **Paket-Reihenfolge:** Die 4-Pakete-Struktur (Paket 1: Core Fixes → Paket 2: Contracts & DX → Paket 3: Efficiency & Quality → Paket 4: Test & Verification) wird als verbindlicher Ausführungsrahmen festgelegt.
2. **Framework-Unification:** Die Versions-Toleranz in `AssemblyReferenceResolver` ist gezielt auf bekannte System-Präfixe (`mscorlib`, `System`, `Microsoft`, `WindowsBase`) beschränkt, um kaskadierende Typauflösungsfehler zu verhindern und gleichzeitig Mismatches bei Drittanbieter-DLLs sicher zu melden.
3. **Freigabe:** Der Draft-Bereich wurde bereinigt; der Übergabevertrag für den Orchestrator ist erfüllt.
