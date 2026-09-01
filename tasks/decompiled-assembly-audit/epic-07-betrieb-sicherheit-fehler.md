# Epic 7 – Betrieb, Sicherheit und Fehlerbehandlung

## Findings – Bug

### E7-BUG-01 – Assembly-Fehlerpfad redigiert Rohpfade und Rohdiagnosen nicht

- Priorität: **P1**
- Größe: **L**
- Vertrauen: **hoch**
- Disposition: **accepted-deferred**, Tech-Debt-Queue: **ja**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:32-55` – absolute und kanonische Eingabepfade werden in Validierungsfehler interpoliert.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:47-50,216-237,271-308` – `exception.Message`, absolute Kandidatenpfade und Diagnosekontext werden in Sessiondiagnosen übernommen.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:261-291` – Workspace-, Syntax- und semantische Diagnosen werden mit Rohtext weitergereicht.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/AnalysisToolCall.cs:180-184` und `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:240-246` – unerwartete Exception-Messages gelangen in harte Fehlerresultate.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisDiagnostics.cs:12-20`, `AssemblyAnalysisContextFactory.cs:384-392,458-464` – externe, Source- und Consumerdiagnosen werden ohne zentrale Redaction aggregiert.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:112-124` und `Coordinators/AssemblyAnalysisHealthSnapshotProvider.cs:48-55` – Anzeige-Normalisierung bzw. Health-Projektion ersetzen keine Geheimnis-, Pfad- oder Exception-Redaction.
- Beobachtung: Der kontrollierte Fehlervertrag setzt erwartbar `isError=false` und `recoverable=true`; der Inhalt bleibt trotzdem nicht redigiert. Neben Pfaden können Betriebssystem-, Roslyn-, Decompiler- oder Providerdetails in Fehlermessages und Diagnose-Samples gelangen.
- Redigierte MCP-Evidence: `inspect_assembly(targetType=assembly, targetPath=<absoluter redigierter synthetischer fehlender .dll-Pfad>, maxResults=1, maxMembers=1, publicOnly=true, includeReferences=false)` ergab `isError=false`, Structured-Code `INVALID_ARGUMENT`, `recoverable=true`, Content `text`; ein ausschließlich synthetisch verwendeter Pfadmarker wurde im Text/Structured Content wiedergefunden. Origin, Completeness und Truncation waren nicht anwendbar, weil kein Assembly-Payload erzeugt wurde.
- Auswirkung: Recoverable Eingabefehler und Health-Diagnosen können lokale Verzeichnisstruktur, Provider-/Compilerinterna und vom Aufrufer kontrollierte sensible Segmente offenlegen. Der `isError`-Wert bleibt dabei zwar korrekt, schützt aber nicht vor Disclosure.
- Empfehlung: Einen zentralen, typed-error-basierten Projektionseinstieg für Assembly, Roslyn, Cache, Provider und Health einführen. Roh-`Exception.Message`, Rohpfade, Diagnose-Locations und externe Identitäten dürfen nicht in `message`, `context`, Text-Samples oder Healthdiagnosen erscheinen; stattdessen bounded Codes, sichere Kategorien und generische Handlungsanweisungen. Falls ein kanonischer Zielpfad vertraglich maschinenlesbar nötig ist, ihn getrennt von Benutzertexten und mit eigener Redaction-/Berechtigungsentscheidung führen.
- Abgrenzung: Der Eingabevertrag verlangt einen absoluten `targetPath`; dieser Befund fordert nicht, den vom Nutzer übergebenen Wert aus internen Logs zu entfernen. `ExternalSourceRepositoryFailurePolicy` und `ExternalSourceUrlPolicy` redigieren Transportfehler bzw. lehnen Userinfo ab; sie decken die übrige Assembly-/Roslyn-/Health-Kette nicht ab.

### E7-BUG-02 – Interne Creation-Cancellation wird als harter Toolfehler klassifiziert

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch**
- Disposition: **accepted-deferred**, Tech-Debt-Queue: **ja**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:189-211` – nicht vom Caller ausgelöste `OperationCanceledException` wird in `Failure(...)` ohne `isError=false` umgewandelt.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.cs:447-450` – der Default von `Failure` ist `isError=true`.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisSourceProjectLeaseCoordinator.cs:104-127` – Source-Project-Creation fängt den verbleibenden Cancellation-/Exception-Pfad als generischen Aufbaufehler.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:256-265` – Snapshot-Cancellation wird re-throwt und erreicht dadurch die Registry-Klassifikation.
- Befund: Caller-Cancellation wird in `AwaitCreationAsync` und der Assembly-Route korrekt weitergereicht. Wird dagegen der interne Creation-Token durch Retirement/Registry-Lifecycle beendet, sieht ein nicht abgebrochener Caller einen harten `ANALYSIS_FAILED`-/Aufbaufehler statt eines kontrollierten, wiederholbaren Zustands.
- MCP-Evidence: Die aktuelle MCP-Semantik erlaubt keinen Cancellation-Token als Eingabe und konnte diesen Lifecyclezweig daher nicht dynamisch provozieren. `FALSE-01` bestätigt nur den benachbarten Metadatenfehler: `isError=false`, `recoverable=true`, `WORKSPACE_DIAGNOSTIC`, kein Snapshot. Die Cancellation-Klassifikation ist statisch aus den obigen Kontrollflüssen abgeleitet.
- Auswirkung: Graceful Retirement, Capacity-Eviction oder ein interner Refresh-Abbruch erzeugen unnötige harte Toolfehler, verschlechtern Retry-/Health-Signale und können während Server-/Session-Abbaues wie eine Fehlfunktion aussehen.
- Empfehlung: Caller-Cancellation, internes Retirement und Provider-/IO-Fehler als getrennte typed states modellieren. Für kontrolliertes internes Abbrechen einen recoverable Code mit Retry-Hinweis oder einen bewusst unterdrückten Lifecycleabschluss liefern; keine Roh-Exception-Messages als öffentliche Ursache verwenden.
- Abgrenzung: Die bereits bekannte Snapshot-Commitpunkt-Lücke ist `E4-BUG-05`; `AssemblyAnalysisSession.Dispose`/`refreshGate` ist `E4-BUG-04`. Dieser neue Befund bewertet ausschließlich die daraus erreichbare Cancellation-/`isError`-Projektion.

## Findings – Optimierung

### E7-OPT-01 – Eviction-/Lifecycle-Symbol überschreitet den MCP-Footprint-Grenzwert

- Priorität: **P3**
- Größe: **M**
- Vertrauen: **hoch**
- Disposition: **accepted-deferred**, Tech-Debt-Queue: **ja**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisRegistryEvictionCoordinator.cs:12-146` – zielgebundener Violation-Check meldete `2524 > 2500` AI-context footprint.
  - Transitive Betriebsabhängigkeiten im MCP-Ergebnis: `ExternalResourceRegistry`, `McpCodeGraphServer` und `SourceSnapshotIdentity`.
- Befund: Der Lifecycle-/Eviction-Koordinator liegt knapp über dem projektweiten Footprint-Limit. Bei diesem Scope ist das kein nachgewiesener Laufzeitfehler, aber ein konkretes Wartbarkeitsrisiko für Retry-, Retirement-, Capacity- und Health-Invarianten.
- MCP-Evidence: `get_violations(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, scopeFilter=src/AiNetLinter/Mcp/Assemblies/Analysis, includeSnippet=false, contextLines=1, maxResults=200)` ergab `isError=false` und zwei `AIContextFootprint`-Treffer: den Eviction-Koordinator mit `2524 > 2500` sowie den bereits aus früheren Epics bekannten Referenz-Expander mit `2513 > 2500`.
- Auswirkung: Fehlersemantik und Cleanup-Ownership sind schwerer lokal prüfbar; spätere Betriebs-/Observability-Erweiterungen erhöhen den Drift- und Regressiondruck.
- Empfehlung: Nur bei Umsetzung in einen begrenzten Lifecycle-Facade und klar getrennte Retirement-/Capacity-/Health-Kollaboratoren schneiden; die Error-/Cancellation-Invarianten jeweils an den neuen Grenzen erhalten. Keine breite Umstrukturierung im Audit.
- Abgrenzung: `AssemblyReferenceSessionExpander` wird nicht als neuer Epic-7-Befund gezählt. Es gibt keinen gemessenen Performanceverlust und keinen Anlass für eine autonome Refaktorierung im read-only Vertrag.

## Findings – Missing Feature

### E7-MF-01 – Assembly-Health weist Betriebszustände und Recoverability nicht vollständig aus

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch**
- Disposition: **accepted-deferred**, Tech-Debt-Queue: **ja**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/Coordinators/AssemblyAnalysisHealthSnapshotProvider.cs:24-75` – Snapshot unterscheidet loading/failed bzw. Sessionstatus und sammelt Diagnosen, aber keine Fehlerklasse, Recoverability, Lease- oder Resource-Daten.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/Projection/AssemblyHealthProjection.cs:14-48` und `GetServerHealthModels.cs:39-72` – maschinenlesbare Assembly-Felder beschränken sich auf Ziel-/Origin-/Status-/Generation-/Diagnose-/Completenesswerte.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthTool.cs:47-97` und `GetServerHealthResponseBuilder.cs:17-49` – Health ruft Sessions/Leases ab, projiziert aber keinen Registry-/Budgetzustand.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResourceBudget.cs:41-51,98-128` und `AssemblyAnalysisRegistry.cs:86-108` – Resource-Health/ResidentCount existieren intern, sind im Assembly-Health-Payload jedoch nicht enthalten.
- Befund: Eine positive gezielte Health-Abfrage zeigt `loadState`, `originKind`, `completeness`, `generation`, Diagnose-Summary und Truncation-/Sessionaggregate. Es fehlen mindestens `failureCode`, `recoverable`, letzter guter Zustand/letzter Fehler, `loading`/`retiring`/`disposed`-Phase, aktive Leases/Operationen, Resident-/Evictionstatus und Resource-Ist-/Limitwerte.
- MCP-Evidence: `get_server_health(targetType=assembly, targetPath=<absoluter redigierter Matrixpfad von LOCAL-03>, includeDiagnostics=true, includeSessions=true, maxDiagnostics=5, maxSessions=5)` ergab `isError=false`, `totalAssemblySessions=1`, `shownSessionCount=1`, `sessionsTruncated=false`; der Assembly-Eintrag enthielt genau die oben genannten Status-/Origin-/Generation-/Diagnose-/Completenessfelder, aber keine Fehlerklasse, Recoverability, Lease- oder Resourcefelder. `get_server_health` für `FALSE-01` ergab `isError=false`, `recoverable=true`, `WORKSPACE_DIAGNOSTIC` ohne residenten Assembly-Eintrag.
- Auswirkung: Ein Monitor kann `partial` und Diagnoseanzahl sehen, aber nicht belastbar entscheiden, ob ein Retry sinnvoll ist, ob eine Session gerade abgebaut wird, ob Capacity/IO/Cancellation ursächlich sind oder ob eine Resource-Grenze erreicht wurde. Abwesenheit eines Eintrags und kontrollierter Fehlschlag sind schwer zu unterscheiden.
- Empfehlung: Optionales, bounded `assemblyOperational`-Objekt ergänzen: typed failure code, `recoverable`, Lifecyclephase, current/last-good generation, timestamps, active leases/operations, Resident-/Evictionzähler, Resource-Ist-/Limitwerte und Retry-/IO-/Cancellation-Counter. Diagnose-Samples bleiben redigiert; externe Identitäten und Rohpfade gehören nicht in dieses Telemetrieobjekt.
- Abgrenzung: Bestehende Origin-, Trust-, Generation-, Status-, Completeness- und Diagnosefelder sind kein fehlendes Feature. Die allgemeine Lifecycle-/Resource-Health-Lücke wurde bereits als `E4-MF-02/03` notiert; dieser Befund präzisiert die fehlende Fehlerklassifikation und Assembly-spezifische Betriebsprojektion.

## Scope-Ergebnisse ohne neuen Befund

- Absolute Pfade und Dateitypen: `AnalysisTargetResolver`/`AssemblyPathValidation` verlangen ein vorhandenes, absolut kanonisiertes `.dll`-/`.exe`-Ziel. Ein vorhandener interner Nicht-Assembly-Pfad wurde als `INVALID_ARGUMENT`, `isError=false`, `recoverable=true` abgewiesen.
- Native/beschädigte/wechselnde PE-Dateien: `AssemblyReferenceResolver` verwendet `PEReader.HasMetadata` und fängt relevante IO-/Access-/Bad-Image-Pfade. `FALSE-01` blieb ohne Identität, Snapshot oder Assembly-Payload. Der wechselnde-Datei-Race ist der bestehende `E4-BUG-02`; im read-only Audit wurde keine untersuchte Datei verändert.
- Metadata-only/Nichtausführung: Read-only Textsuche im Assembly-/Analysis-Bereich fand keinen Runtime-Load-, `AssemblyLoadContext`-, `Activator`- oder Prozessstart-Aufruf. Die positiven MCP-Fälle belegten dekompilierte, begrenzte Signatur-Snapshots; daraus folgt kein Execution-Nachweis.
- Providerfehler: Die Provider-Transportprojektion liefert sichere Codes/Meldungen/Locations. `GIT-01` ist der konfigurierte Source-/Git-Fall und wurde in diesem Epic nicht als direkter Assembly-Pfad wiederholt. Kein neuer Provider-Redaction-Befund außerhalb der zentralen Assembly-/Roslyn-/Health-Kette.

## Evidence-/Scope-Abschnitt

### Scope und Redaction

Geprüft wurde ausschließlich Epic 7: absolute Pfade und Dateitypen, native/beschädigte/wechselnde PE-Ziele, metadata-only und Nichtausführung, redigierte Fehlertexte, Health/Observability, Fail-Closed, recoverable Fehler, Cancellation-/IO-/Providerfehler sowie Server-/Session-Lifecycle soweit assemblybezogen.

Die lokale Matrix wurde ausschließlich zur Auflösung der opaken Labels verwendet. In diesem Bericht erscheinen nur `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01`. Konkrete externe Assembly-Namen, Namespaces, Pfade, URLs, Hashes und dekompilierte Inhalte sind weder im Bericht noch in der Code-Map enthalten. Absolute Matrixpfade sind in MCP-Tabellen als redigierte Platzhalter bezeichnet.

### Read-only gelesene Nachweise (keine ausgeführten Änderungen)

- `C:/Daten/Entwicklung/Ralf/AiNetLinter/AGENTS.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinter.mdc`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinterRichtlinien.mdc`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinter-McpWorkflow.mdc`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-audit/Konzept.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-audit/roadmap.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-audit/code-map.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/skills/implement/SKILL.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/skills/audit/SKILL.md` – vorgeschriebener diagnostischer Abschlusscheck; wegen read-only Vertrag keine Korrektur.
- Relevante Produktionsquellen unter `src/AiNetLinter/Mcp/`, `src/AiNetLinter/Configuration/` und `src/AiNetLinter/Mcp/Assemblies/ExternalSource/` gemäß den Finding-Fundstellen.
- Die lokale Matrixdatei wurde nur gelesen, um die fünf erlaubten Labels aufzulösen; keine Matrixinhalte wurden in Bericht, Map oder Hand-off übernommen.
- Es wurden keine Builds, Tests oder Commits ausgeführt. Read-only Test-/Featureverweise aus MCP wurden nicht als Laufzeitbeleg verwendet.

### Tatsächlich ausgeführte MCP-Abfragen vor der finalen Map-Änderung

Alle target-gebundenen Aufrufe nutzten das aktuelle Schema. `targetPath` war jeweils absolut; konkrete Matrixwerte sind hier aus Redaction-Gründen nicht wiederholt.

| ID | MCP-Tool und vollständige relevante Parameter | Redigiertes Ergebnis |
|---|---|---|
| P1 | `get_index_scope(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter)` | `isError=false`; 886 C#-Dateien im Projektscope; Scope vollständig. Origin/Completeness/Truncation: n/a. |
| P2 | `get_file_tree(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, root=src/AiNetLinter/Mcp, view=tree, treeDepth=3, maxResults=200, includeMetadata=false, includeLineCount=false)` | `isError=false`; relevante Assembly-/Analysis-/Health-Unterbäume sichtbar; Resultat wegen `maxResults=200` gegenüber dem breiteren Trefferbestand gekürzt. Origin/Completeness: n/a; Truncation: `maxResults`. |
| P3 | `find_symbol(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, namePatterns=[AssemblyFingerprintCalculator, AssemblyReferenceResolver, AssemblyAnalysisSession, AssemblyAnalysisHealthSnapshotProvider, AssemblyHealthProjection, AssemblyAnalysisResponse, AssemblyAnalysisToolSupport, AssemblyAnalysisRegistry], maxResults=50, includeReferences=false)` | `isError=false`; alle relevanten Produktionssymbole gefunden, inklusive aktueller Datei-/Startzeilen. Origin/Completeness/Truncation: n/a. |
| P4 | `get_feature_context(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, symbolIdentifier=<jeweils relevantes Produktionssymbol>, includeCallers=true, includeTests=true, includeMetrics=true, includeViolations=true, maxCallers=10, maxTests=10)` | `isError=false`; zentrale Resolver-/Session-/Context-/Reference-/Health-/Registration-Symbole kontextualisiert; keine relevanten Regelverletzungen an den abgefragten Symbolen. Origin/Completeness/Truncation: n/a. |
| P5 | `get_server_health(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, includeDiagnostics=true, includeSessions=true, maxDiagnostics=20, maxSessions=50)` | `isError=false`; Projekt geladen, keine residenten Assembly-Sessions im aggregierten Snapshot. Origin/Completeness/Truncation: n/a. |
| P6 | `get_violations(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, scopeFilter=src/AiNetLinter/Mcp/Assemblies/Analysis, includeSnippet=false, contextLines=1, maxResults=200)` | `isError=false`; zwei `AIContextFootprint`-Treffer: der Eviction-Koordinator `2524 > 2500` und der bereits bekannte Referenz-Expander `2513 > 2500`. Origin/Completeness/Truncation: n/a. |
| A1 | `find_duplicates(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, scopeDir=src/AiNetLinter/Mcp/Assemblies/Analysis, scopeType=production, mode=clone, similarityThreshold=exact, normalizeIdentifiers=false, minTokens=30, maxResults=50)` | `isError=false`; 0 exakte Clone-Cluster. Origin/Completeness/Truncation: n/a. |
| A2 | `find_dead_code(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, scopeFilter=src/AiNetLinter/Mcp/Assemblies/Analysis, accessibility=private_internal, confidence=high, includeTests=true, kind=all, mode=members, maxResults=100)` | `isError=false`; 0 hoch-konfidente private/interne Dead-Code-Treffer. Origin/Completeness/Truncation: n/a. |
| A3 | `find_magic_values(targetType=project, targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter, scopeFilter=src/AiNetLinter/Mcp/Assemblies/Analysis, valueType=all, categoryFilter=all, minOccurrences=2, includeTests=true, includeSuppressed=false, changedOnly=false, maxResults=100)` | `isError=false`; 0 Treffer im gewählten Wiederholungsfilter. Origin/Completeness/Truncation: n/a. |

### Tatsächlich ausgeführte redigierte Assembly-/Health-Abfragen vor der finalen Map-Änderung

| MCP-Tool | Vollständige relevante Parameter | Redigiertes Ergebnis |
|---|---|---|
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von LOCAL-01/LOCAL-02/LOCAL-03>`, `maxResults=5`, `maxMembers=20`, `publicOnly=true`, `includeReferences=false` | Für alle drei: `isError=false`; `origin=decompiled`, `status=partial`, `completeness=partial`, `confidence=medium`, `trust=untrusted`, `generation=1`, `truncated=true`, `truncatedBy=[maxResults,responseBudget]`; kein Source-Snapshot. `recoverable`: n/a. |
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von FALSE-01>`, `maxResults=3`, `maxMembers=10`, `publicOnly=true`, `includeReferences=false` | `isError=false`; `code=WORKSPACE_DIAGNOSTIC`, `recoverable=true`; kein Origin, keine Completeness, kein Snapshot, kein Assembly-Payload, keine Truncation. |
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter synthetischer fehlender .dll-Pfad>`, `maxResults=3`, `maxMembers=10`, `publicOnly=true`, `includeReferences=false` | `isError=false`; `code=INVALID_ARGUMENT`, `recoverable=true`; kein Origin, keine Completeness, keine Truncation. |
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter interner vorhandener Nicht-Assembly-Pfad>`, `maxResults=3`, `maxMembers=10`, `publicOnly=true`, `includeReferences=false` | `isError=false`; `code=INVALID_ARGUMENT`, `recoverable=true`; Erweiterungsfehler, kein Origin/Snapshot, keine Truncation. |
| `inspect_assembly` | `targetType=project`, `targetPath=C:/Daten/Entwicklung/Ralf/AiNetLinter`, `maxResults=3`, `maxMembers=10`, `publicOnly=true`, `includeReferences=false` | `isError=false`; `code=INVALID_ARGUMENT`, `recoverable=true`; korrekt abgewiesenes Projektziel, kein Origin/Snapshot, keine Truncation. |
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter synthetischer fehlender .dll-Pfad mit Marker>`, `maxResults=1`, `maxMembers=1`, `publicOnly=true`, `includeReferences=false` | `isError=false`, `recoverable=true`, `code=INVALID_ARGUMENT`; der redigierte Marker wurde im Fehlerresultat wiedergefunden. Kein Origin/Snapshot/Truncation. |
| `get_server_health` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von LOCAL-03>`, `includeDiagnostics=true`, `includeSessions=true`, `maxDiagnostics=5`, `maxSessions=5` | `isError=false`; `totalAssemblySessions=1`, `shownSessionCount=1`, `sessionsTruncated=false`; Eintrag `loadState=partial`, `originKind=decompiled`, `completeness=partial`, `generation=1`; keine Resource-/Lease-/Failure-Class-Felder. |
| `get_server_health` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von FALSE-01>`, `includeDiagnostics=true`, `includeSessions=true`, `maxDiagnostics=5`, `maxSessions=5` | `isError=false`; `code=WORKSPACE_DIAGNOSTIC`, `recoverable=true`; kein residenter Assembly-Eintrag. |

## Offene Unsicherheiten

- Die MCP-Schnittstelle bietet keinen Eingabeparameter, um Cancellation oder einen kontrollierten Registry-Retirement-Zeitpunkt zu injizieren. `E7-BUG-02` ist deshalb statisch, nicht dynamisch reproduziert.
- Wegen des read-only Vertrags wurde kein Ziel verändert. Die positive Wiederholungsrunde bestätigt Stabilität der beobachteten Generation, beweist aber nicht, dass der bestehende In-Flight-Fingerprint-Race behoben ist.
- Die aktuellen direkten Assembly-Live-Fälle lieferten keinen source-backed Snapshot. `GIT-01` wurde in diesem Epic nicht als direkter Assembly-Pfad wiederholt; die Source-backed-Fehler-/Health-Projektion konnte daher nur statisch bewertet werden.
- Health-Abfragen wurden mit kleinen Diagnose-/Sessionlimits ausgeführt. Fehlende Felder sind Schema-/Projektionsbefunde; sie sind nicht mit dem Fehlen interner Registry-Zustände gleichzusetzen.

## Audit-Grenzen und Verifikation

- Keine Produktionscode-, Test-, Konfigurations- oder Produktdokumentationsänderung.
- Keine Builds, Tests oder Commits.
- Die einzige neue Datei ist dieser Epic-7-Bericht; die einzige weitere Änderung ist die Epic-7-Ergänzung in `code-map.md`.
- Der diagnostische Audit-Skill wurde ausgeführt: exakte Duplikate, hoch-konfidenter privater/interner Dead Code und wiederholte Magic Values ergaben im begrenzten Assembly-Analyse-Scope keine Treffer. Der Footprint-Violation-Treffer ist oben als Optimierung aufgenommen; es wurde nichts automatisch behoben.

## Finale Spotchecks nach letzter Code-Map-Änderung

Nach der letzten Änderung an `code-map.md` wurden die positiven und negativen Fälle erneut mit dem aktuellen MCP-Schema, `targetType` und absolutem, hier redigiertem `targetPath` abgefragt. Danach erfolgte keine weitere Code-Map-Änderung.

| MCP-Tool | Vollständige relevante Parameter | Redigiertes Ergebnis |
|---|---|---|
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von LOCAL-01/LOCAL-02/LOCAL-03>`, `maxResults=5`, `maxMembers=20`, `publicOnly=true`, `includeReferences=false` | Alle drei blieben `isError=false`, `origin=decompiled`, `status=partial`, `completeness=partial`, `confidence=medium`, `trust=untrusted`, `generation=1`, `truncated=true`, `truncatedBy=[maxResults,responseBudget]`; kein Source-Snapshot. `recoverable`: n/a. |
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von FALSE-01>`, `maxResults=3`, `maxMembers=10`, `publicOnly=true`, `includeReferences=false` | `isError=false`; `code=WORKSPACE_DIAGNOSTIC`, `recoverable=true`; kein Origin, keine Completeness, kein Snapshot, keine Truncation. |
| `inspect_assembly` | `targetType=assembly`, `targetPath=<absoluter redigierter synthetischer fehlender .dll-Pfad>`, `maxResults=1`, `maxMembers=1`, `publicOnly=true`, `includeReferences=false` | `isError=false`; `code=INVALID_ARGUMENT`, `recoverable=true`; kein Origin, keine Completeness, keine Truncation. Der synthetische Marker blieb im Fehlerresultat nachweisbar. |
| `get_server_health` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von LOCAL-03>`, `includeDiagnostics=true`, `includeSessions=true`, `maxDiagnostics=5`, `maxSessions=5` | `isError=false`; eine Session, `loadState=partial`, `origin=decompiled`, `completeness=partial`, `generation=1`, `sessionsTruncated=false`; keine Resource-/Lease-/Failure-Class-Felder. |
| `get_server_health` | `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad von FALSE-01>`, `includeDiagnostics=true`, `includeSessions=true`, `maxDiagnostics=5`, `maxSessions=5` | `isError=false`; `code=WORKSPACE_DIAGNOSTIC`, `recoverable=true`; kein residenter Assembly-Eintrag. |

Damit sind die abschließenden positiven, negativen und Health-Spotchecks nach der letzten zulässigen Map-Änderung dokumentiert. Externe Assembly-Identitäten wurden auch in dieser Runde nicht übernommen.
