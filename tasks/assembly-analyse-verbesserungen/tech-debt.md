# Tech-Debt-Register

Noch keine actionable Findings erfasst. Befunde aus Implementierung, Review und Audit werden hier mit Schweregrad, Evidenz, Disposition und Log-Anker ergänzt.

## P2 – Vier bestehende Budget-Parallelcluster

- Schweregrad: P2
- Beschreibung: Vier DRY-Cluster im Budget-Code könnten gemeinsame Logik teilen.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs`.
- Evidenz: DRY-Audit des Epic-1-Implementierers meldet vier bestehende Budget-Parallelcluster.
- Disposition: accepted-deferred
- Nächster Schritt: Im passenden späteren Audit-/Refactoring-Scope prüfen, ob ein verhaltensneutrales Zusammenführen möglich ist; keine Scope-Erweiterung des Epic-1-Vertrags.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Implementierer – Abschluss.

## Aktive P1-Findings – Korrekturrunde 1

Die folgenden fünf Ursachen werden in der unmittelbar folgenden frischen Implementierer-/Reviewer-Runde bearbeitet. Sie sind bis zur Bestätigung des Folge-Reviews nicht als `fixed` disponiert.

Implementierer Korrekturrunde 1 meldet alle fünf als behoben; Disposition bleibt bis zum Folge-Review `fix-now`, Attempts bleiben bei 1.

### P1 – BudgetProjection/Envelope

- Schweregrad: P1
- Beschreibung: Budget-Trim und kanonische Paging-/Legacy-Felder werden nicht gemeinsam neu berechnet.
- Scope/Fundstelle: `InspectAssemblyResponseBuilder.cs`, `FindAssemblyExtensionsResponseBuilder.cs`, `AssemblyAnalysisResponseLimits.Budget.cs`.
- Evidenz: Reviewer reproduziert übersprungene oder unerreichbare Ergebnisse bei kleinem `maxResponseBytes`.
- Disposition: accepted-deferred
- Nächster Schritt: FileTree-Scanner- und Wire-Completeness für Dateien/Verzeichnisse in einem späteren gezielten Durchlauf vollständig trennen; reale Scanner-Regression mit beiden Richtungen ergänzen.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 1 / Folge-Reviewer 5 – Abschluss, Signatur `BudgetProjection/Envelope`.

### P1 – AssemblyWireBudget/Coverage

- Schweregrad: P1
- Beschreibung: Antwortbudget wird nicht als gemeinsames MCP-Wire-Budget über Text und Structured sowie alle Assembly-Routen durchgesetzt.
- Scope/Fundstelle: `AssemblyAnalysisResponse.cs`, `AssemblyAnalysisResponseLimits.Budget.cs`, Symbol-Body-/Symbolgraph-/Assembly-`get_file_tree`-Registrierungen, `InspectAssemblyFormatter.cs`.
- Evidenz: Reviewer weist getrennte Messung und redundante umfangreiche Textdaten nach.
- Disposition: fix-now (aktive Korrekturrunde)
- Nächster Schritt: zentralen, rückwärtskompatiblen Budgetpfad oder kurze Textzusammenfassung mit Structured als kanonischer Nutzlast umsetzen und messen.
- Attempts: 3
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Reviewer – Abschluss, Signatur `AssemblyWireBudget/Coverage`.

### P1 – CompositeBudget/Enrichment

- Schweregrad: P1
- Beschreibung: Composite-Budgetierung erfolgt vor dem finalen Envelope; entfernte Sektionen tragen keinen Status oder Fortsetzungshinweis.
- Scope/Fundstelle: `AssemblyAnalysisContextTool.cs`, `AnalysisToolCall.cs`.
- Evidenz: Reviewer beschreibt erneutes Überschreiten durch spätere Metadaten und transparente Sektionsverluste bei kleinem Budget.
- Disposition: fix-now (aktive Korrekturrunde)
- Nächster Schritt: finalen Envelope vor dem gemeinsamen Trim erzeugen und jeden optionalen Abschnitt mit Status/Truncation/Detailhinweis versehen; Regressionstest ergänzen.
- Attempts: 3
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Reviewer – Abschluss, Signatur `CompositeBudget/Enrichment`.

### P1 – CompositeTraversal/TopN

- Schweregrad: P1
- Beschreibung: Öffentlich akzeptierter `topN`-Parameter verändert die Caller-/Impact-Auswahl nicht.
- Scope/Fundstelle: `AssemblyAnalysisToolRegistrations.cs`, `AssemblyAnalysisContextTool.cs`.
- Evidenz: Reviewer reproduziert identische Auswahl für `topN=1` und `topN=100`.
- Disposition: fixed
- Nächster Schritt: `topN` semantisch anwenden oder aus dem Vertrag entfernen und Regressionstest ergänzen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Folge-Reviewer 1 – Abschluss, Signatur `CompositeTraversal/TopN`.

### P1 – AssemblyProvenance/BodyMode

- Schweregrad: P1
- Beschreibung: Structured Body kann bei dekompilierten Assemblies `source` melden, obwohl der äußere Analysekontext `decompiledProject` ausweist.
- Scope/Fundstelle: `SourceSymbolBodyResolver.cs`, `AssemblyAnalysisContextFactory.cs`, `GetSymbolBodyTool.cs`.
- Evidenz: Reviewer weist widersprüchliche Herkunftsangaben im Fallback-Pfad nach.
- Disposition: fixed
- Nächster Schritt: Body-Herkunft aus dem Assembly-Kontext ableiten und Assembly-Body-Regressionstest ergänzen.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-03 / Epic 1 / Folge-Reviewer 1 – Abschluss, Signatur `AssemblyProvenance/BodyMode`.

Folge-Reviewer 1 bestätigt `BudgetProjection/Envelope`, `AssemblyWireBudget/Coverage` und `CompositeBudget/Enrichment` weiterhin als P1 offen; `CompositeTraversal/TopN` und `AssemblyProvenance/BodyMode` sind `fixed`. Die drei offenen Einträge bleiben für Korrekturrunde 2 mit Attempts 1 aktiv.

Korrektur-Implementierer 2 meldet die drei offenen Ursachen behoben; Disposition bleibt bis zum Folge-Review 2 `fix-now`. Attempts werden auf 2 fortgeschrieben.

Folge-Reviewer 2 bestätigt die drei Ursachen weiterhin als P1 offen: `BudgetProjection/Envelope`, `AssemblyWireBudget/Coverage` und `CompositeBudget/Enrichment`. `CompositeTraversal/TopN` und `AssemblyProvenance/BodyMode` bleiben `fixed`. Korrekturrunde 3 wird mit Attempts 3 für die drei aktiven Einträge gestartet.

Korrektur-Implementierer 3 meldet die drei aktiven Ursachen behoben; Disposition bleibt bis zum Folge-Review 3 `fix-now`, Attempts bleiben bei 3.

Folge-Reviewer 3 bestätigt `AssemblyWireBudget/Coverage`, `CompositeBudget/Enrichment`, `CompositeTraversal/TopN` und `AssemblyProvenance/BodyMode` als `fixed`; `BudgetProjection/Envelope` bleibt wegen unbekannter Arrays und `fileTree.directories` P1 offen. Korrekturrunde 4 wird mit Attempts 4 gestartet.

Korrektur-Implementierer 4 meldet `BudgetProjection/Envelope` behoben; Disposition bleibt bis zum letzten Folge-Review 4 `fix-now`, Attempts bleiben bei 4.

Folge-Reviewer 4 bestätigt `BudgetProjection/Envelope` weiterhin als P1 offen: collection-spezifische Truncation von `fileTree.files` und `fileTree.directories` wird vermischt. Korrekturrunde 5 ist der letzte Versuch; bei erneutem Offenbleiben wird der Eintrag mit Attempts 0 als `accepted-deferred` hinten eingereiht.

Korrektur-Implementierer 5 (letzter Versuch) meldet `BudgetProjection/Envelope` behoben; Disposition bleibt bis zum Folge-Review 5 `fix-now`, Attempts bleiben bei 5.

Folge-Reviewer 5 bestätigt `BudgetProjection/Envelope` weiterhin als P1 offen. Nach fünf Versuchen wird der Eintrag gemäß Queue-Regel mit Attempts 0 als `accepted-deferred` hinten eingereiht; Epic 2 läuft unabhängig weiter.

## P2 – Zwei bestehende Magic-Value-Kandidaten

- Schweregrad: P2
- Beschreibung: Zwei bestehende Konstantenkandidaten im Epic-2-Scope wurden durch den Magic-Values-Audit gemeldet.
- Scope/Fundstelle: `src/AiNetLinter/Mcp` (konkrete Kandidaten laut Audit; keine neue sichere Änderung identifiziert).
- Evidenz: Epic-2-Implementierer meldet zwei bekannte Kandidaten nach der letzten Codeänderung.
- Disposition: accepted-deferred
- Nächster Schritt: In einem gezielten Konfigurations-/Konstanten-Review fachlich bewerten; nicht aus dem Source-/Cache-Epic heraus generalisieren.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 2 / Implementierer – Abschluss.

## Aktive P1-Findings – Epic 2 Review, Korrekturrunde 1

Die unabhängige Review hat drei voneinander unabhängige Ursachen mit P1-Schweregrad gefunden. Sie werden getrennt korrigiert und jeweils erneut reviewed.

### P1 – GitClone/StderrClassification

- Schweregrad: P1
- Beschreibung: Erfolgreiche Git-Clone-Ausgaben auf normalem `stderr` werden als Checkout-Fehler verworfen.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/ExternalSource/ProcessExecution/ExternalSourceGitProcessOutputPolicy.cs`, `GiteaGitRepositoryTransport.cs`.
- Evidenz: Erfolgreicher lokaler `git clone --no-local --no-hardlinks` mit Exit-Code 0 und `Cloning into '...'...` auf `stderr` wird abgelehnt; bestehende Tests verwenden nur leeres `stderr`.
- Disposition: fixed
- Nächster Schritt: keine; positive und negative Clone-/Status-Regressionen vorhanden.
- Attempts: 2
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 2 / Reviewer – Abschluss, Signatur `GitClone/StderrClassification`.

### P1 – CacheGeneration/ReaderLease

- Schweregrad: P1
- Beschreibung: Zwischen dem Lesen einer Cache-Generation und ihrer Materialisierung besteht keine generationsbezogene Leser-Lease.
- Scope/Fundstelle: `ExternalSourceRepositoryCacheReuse.TryAcquire`, `ExternalSourceRepositoryCacheMaterializer.Materialize`, `ExternalSourceRepositoryCacheWriterLifecycle.cs`.
- Evidenz: Ein zweiter Daemon kann Generationen publizieren und Retention kann die von Daemon A gelesene Generation vor dem Kopieren löschen.
- Disposition: fixed
- Nächster Schritt: keine; echter Zwei-Prozess-/IPC-Interleaving-, Cancellation- und Cleanup-Nachweis vorhanden.
- Attempts: 2
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 2 / Reviewer – Abschluss, Signatur `CacheGeneration/ReaderLease`.

### P1 – SourcePolicy/ProvenancePropagation

- Schweregrad: P1
- Beschreibung: SourceMode wird nicht durch Selection, Fallback und Context geführt; Herkunft kann im Fallback beim Default `source_preferred` bleiben und im `analysis`-Envelope fehlen.
- Scope/Fundstelle: `AssemblyAnalysisFallbackEntryCreationParameters`, `AssemblyAnalysisContextFactory.cs`, `AssemblyAnalysisResponse.cs`, `AssemblyAnalysisSourceToolSupport.cs`.
- Evidenz: Bei `decompilation_allowed` und kontrolliertem Source-Fallback bleibt `AssemblyOrigin.SourcePolicy` auf `source_preferred`; der gemeinsame maschinenlesbare Envelope verwirft das Feld.
- Disposition: fixed
- Nächster Schritt: keine; Default- und `decompilation_allowed`-Fallback-Regressionen vorhanden.
- Attempts: 2
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 2 / Reviewer – Abschluss, Signatur `SourcePolicy/ProvenancePropagation`.

Korrektur-Implementierer 1 meldet alle drei Epic-2-P1-Ursachen behoben; Disposition bleibt bis zum Folge-Review `fix-now`, Attempts bleiben bei 1. Der Implementierer weist ausdrücklich darauf hin, dass `source_preferred` im strukturierten `analysis`-Envelope aus Wire-Budget-/Kompatibilitätsgründen fehlt und nur im Header sichtbar bleibt; dieser Punkt ist im Folge-Review als mögliche Restursache zu verifizieren.

Folge-Reviewer 1 bestätigt alle drei P1-Ursachen weiterhin als offen: Clone-Policy akzeptiert unbekannte Warnungen/Hints, der Lease-Nachweis deckt kein echtes Mehrdaemon-/Cancellation-Interleaving ab, und `analysis.sourcePolicy` fehlt beim Default `source_preferred`. Korrekturrunde 2 wird mit Attempts 2 gestartet.

Korrektur-Implementierer 2 meldet alle drei Ursachen behoben; Disposition bleibt bis zum Folge-Review `fix-now`, Attempts bleiben bei 2. Der Implementierer weist als verbleibendes Risiko aus, dass Cancellation vor Beginn, nicht mitten in einer großen Kopieroperation geprüft wird; unbekannte Git-Ausgaben bleiben bewusst fail-closed.

Folge-Reviewer 2 bestätigt alle drei Epic-2-P1-Ursachen als `fixed`; der gezielte IPC-Test startet zweimal die echte `AiNetLinter.exe` und prüft Retention, Materialisierung, Cancellation sowie Lock-/Cleanup-Freigabe. Der Implementierer-Hinweis zur Cancellation vor Beginn einer großen Kopieroperation bleibt als nicht-blockierendes Betriebsrisiko dokumentiert.

## P2 – IPC-Test-Probe im Produktionsbinary

- Schweregrad: P2
- Beschreibung: Der echte Zwei-Prozess-Lease-Nachweis nutzt einen versteckten Probe-Pfad in der Produkt-EXE und akzeptiert frei gewählte Markerpfade.
- Scope/Fundstelle: `src/AiNetLinter/Program.cs:69`, `src/AiNetLinter/Commands/ExternalSourceCacheLeaseProbeCommand.cs:16`.
- Evidenz: Folge-Reviewer 2 sieht aktuell kein konkretes Sicherheits- oder Cleanup-Fehlverhalten; ein separater Test-Worker würde den Produktionsumfang langfristig reduzieren.
- Disposition: accepted-deferred
- Nächster Schritt: Bei einer späteren Testinfrastruktur-Runde Probe aus dem Produktionsbinary herauslösen, ohne den echten Prozess-/Lease-Nachweis zu schwächen.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 2 / Folge-Reviewer 2 – Abschluss.

## Aktive P1-Findings – Epic 3 Review, Korrekturrunde 1

Die unabhängige Epic-3-Review hat drei voneinander unabhängige P1-Ursachen gefunden. Sie werden getrennt korrigiert und jeweils erneut reviewed.

### P1 – SearchBudget/EnvelopeReachability

- Schweregrad: P1
- Beschreibung: Der `assemblySearch`-Payload wird bei kleinem `maxResponseBytes` als unbekannter Container vollständig entfernt; Results, Counts und Paging fehlen statt eines nutzbaren gekürzten Envelopes.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:301,317,358`.
- Evidenz: Der Container fehlt in `IsTrimContainer` und bekannten Ergebniscontainern; Reviewer reproduziert vollständiges Entfernen bei kleinem Budget.
- Disposition: fixed
- Nächster Schritt: keine; gezielte Budget-Regression 40/40 im Implementierer-Lauf.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 3 / Reviewer – Abschluss, Signatur `SearchBudget/EnvelopeReachability`.

### P1 – SearchPaging/MaxFilesContinuation

- Schweregrad: P1
- Beschreibung: `maxFiles` kann am Ende des eingeschränkten Dateiscopes `isTruncated=true` und denselben Cursor erneut liefern; Paging macht keinen Fortschritt.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblySearchTool.cs:184`.
- Evidenz: Reviewer reproduziert wiederholbaren Token nach Ende des `maxFiles`-Scopes.
- Disposition: fixed
- Nächster Schritt: keine; terminalen Zustand und Fortschritt gezielt regressionsgetestet.
- Attempts: 1
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 3 / Reviewer – Abschluss, Signatur `SearchPaging/MaxFilesContinuation`.

### P1 – DaemonDiscovery/ExecutableVersionFingerprint

- Schweregrad: P1
- Beschreibung: Der Handshake prüft nur eine feste, hashfreie `ExecutableVersion`; ein alter Daemon kann einen neuen ThinClient akzeptieren und eine Tool-Liste ohne `search_assembly` liefern.
- Scope/Fundstelle: `src/AiNetLinter/AiNetLinter.csproj:5`, `src/AiNetLinter/Mcp/Composition/McpServerVersion.cs:17`, `src/AiNetLinter/Mcp/Daemon/DaemonHandshake.cs:75`.
- Evidenz: Discovery wird beim Daemonstart aufgebaut, die feste Projektversion bleibt trotz neuer Toolregistrierung gleich; Dokumentation nennt nur manuellen Neustart.
- Disposition: fixed
- Nächster Schritt: keine; kontrollierter `DISCOVERY_FINGERPRINT_MISMATCH`-Pfad und Legacy-Named-Pipe-Regression vorhanden.
- Attempts: 2
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 3 / Reviewer – Abschluss, Signatur `DaemonDiscovery/ExecutableVersionFingerprint`.

## P2 – Epic-3-Such- und Discovery-Nachweis

- Schweregrad: P2
- Beschreibung: Cursor sind noch nicht an Query/Root/Generation gebunden; der Assembly-Header kann absolute Pfade/Repository-URLs enthalten; `ServerInstructions` dokumentiert nicht alle vier Assembly-only-Tools; E2E deckt nicht alle Suchmodi und Fehlerfälle ab.
- Scope/Fundstelle: `AssemblySearchTool.cs`, `AssemblyAnalysisResponse.cs`, `ServerInstructions.cs`, `McpServerAssemblyHealthE2ETests.cs`.
- Evidenz: Folge-Reviewer 1 nennt konkrete Lücken bei `external_calls`, fehlendem Root, Budget, `maxFiles`, ungültigen Cursorn und Source-backed Suche; kein konkreter Secret-Leak nachgewiesen.
- Disposition: accepted-deferred
- Nächster Schritt: Nach Freigabe der P1-Korrekturen Cursor-Bindung, logische Diagnose-IDs, Capability-Doku und ergänzende E2E-Fälle in einem fokussierten Nachlauf bewerten.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 3 / Reviewer – Abschluss.

Korrektur-Implementierer 1 meldet alle drei Epic-3-P1-Ursachen behoben; Disposition bleibt bis zum Folge-Review `fix-now`, Attempts bleiben bei 1. Die bestehende `AIContextFootprint`-Warnung in `DaemonHost` (2530 > 2500) bleibt außerhalb der P1-Ursachen und wird als nicht-blockierendes Tech Debt weitergeführt.

Korrektur-Implementierer 2 meldet `DaemonDiscovery/ExecutableVersionFingerprint` behoben; Disposition bleibt bis zum letzten Folge-Review `fix-now`, Attempts bleiben bei 2. Der kontrollierte terminale `DISCOVERY_FINGERPRINT_MISMATCH`-Pfad und der reale Legacy-Named-Pipe-Test werden durch die Folge-Review abschließend verifiziert.

Folge-Reviewer 2 bestätigt `DaemonDiscovery/ExecutableVersionFingerprint` als `fixed`: Legacy-Welcome ohne Fingerprint endet kontrolliert mit `DISCOVERY_FINGERPRINT_MISMATCH`, Exit 2, genau einem Connect-Versuch und freigegebener Pipe. Disposition wird auf `fixed` gesetzt, Attempts bleiben bei 2.

## P2 – Duplicate Helper im Legacy-Handshake-Test

- Schweregrad: P2
- Beschreibung: `CompleteAsync` ist im neuen Legacy-Handshake-Test und bereits in `ThinClientProxySessionContractTests` vorhanden.
- Scope/Fundstelle: `src/AiNetLinter.IntegrationTests/Mcp/Daemon/ThinClientDiscoveryContractTests.cs:101` und bestehender Session-Test.
- Evidenz: Folge-Reviewer 2 meldet einen DuplicateCode-Kandidaten; keine Produktionsduplikation und keine P1-Relevanz.
- Disposition: accepted-deferred
- Nächster Schritt: In einem fokussierten Test-DRY-Nachlauf prüfen, ob gemeinsamer Helper ohne Testsemantik-Drift sinnvoll ist.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 3 / Folge-Reviewer 2 – Abschluss.

Folge-Reviewer 1 bestätigt `SearchBudget/EnvelopeReachability` und `SearchPaging/MaxFilesContinuation` als `fixed`. `DaemonDiscovery/ExecutableVersionFingerprint` bleibt P1 offen, weil ein fingerprintloses Welcome des alten Daemons im generischen Retrypfad bis zum Readiness-Timeout läuft. Korrekturrunde 2 wird mit Attempts 2 ausschließlich für diese Ursache gestartet.

## P2 – AIContextFootprint im DaemonHost

- Schweregrad: P2
- Beschreibung: Bestehender MCP-Kontextumfang von `DaemonHost` liegt mit 2530 über dem projektspezifischen Zielwert 2500.
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Daemon/DaemonHost`.
- Evidenz: Epic-3-Korrektur-Implementierer meldet die Warnung; sie betrifft nicht Search-Envelope, Paging oder Discovery-Fingerprint und wurde deshalb nicht durch Scope-Erweiterung verändert.
- Disposition: accepted-deferred
- Nächster Schritt: In einem separaten Kontextbudget-/Refactoring-Scope bewerten.
- Attempts: 0
- Log-Anker: `execution-log.md`, Run 2026-09-04 / Epic 3 / Korrektur-Implementierer 1 – Abschluss.
