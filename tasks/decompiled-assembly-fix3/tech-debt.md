# Tech-Debt-Queue

## Initial übernommene, zurückgestellte Befunde

### Auditbefunde aus dem freigegebenen Konzept

- Schweregrad: P2/P3
- Ursachensignatur: audit-zusatzbefunde-ohne-sichere-scope-korrektur
- Scope/Fundstelle: `src/AiNetLinter/Mcp` – Near-/Fuzzy-Duplikate, Low-Confidence-Dead-Code-Kandidaten und Magic-Value-Kandidaten aus dem Konzept-Audit
- Evidenz: Das Konzept nennt acht Duplikatcluster, 37 ausschließlich Low-Confidence-Dead-Code-Kandidaten und 245 Magic-Value-Kandidaten; ohne zusätzliche Fachentscheidung ist keine sichere Änderung belegt.
- Disposition: accepted-deferred
- Nächster Schritt: Im Abschluss-Audit nur scope-nahe, eindeutig verhaltensneutrale Befunde prüfen; übrige Kandidaten nicht in den aktuellen Produktumfang ziehen.
- Log-Anker: `Konzept.md`, Abschnitt „Audit-Zusatzbefunde"

### Neue Parameterzahl-Warnungen nach Paket 1

- Schweregrad: P2 (Review-Bestätigung ausstehend)
- Ursachensignatur: erweiterte-mcp-verträge-ohne-parameterobjekt
- Scope/Fundstelle: geänderte MCP-Methoden in `src/AiNetLinter/Mcp`, insbesondere `McpToolResults` und Assembly-Analyse-Verträge
- Evidenz: Der Abschlussreview bestätigte vier aktive `MaxMethodParameterCount`-Verstöße in `McpToolResults.cs` an Zeilen 48, 67, 78 und 220 (`Error`, `Recoverable`, `BuildResult`, `CompilationError`). Sie entstanden durch die typisierte Payload-Erweiterung und wurden als Produktions-P1 eingestuft.
- Disposition: fix-now
- attempts: 0
- Nächster Schritt: Scope-nahen Parametervertrag/Parameterobjekt-Fix implementieren und Produktionsscope mit `get_violations` erneut prüfen.
- Log-Anker: `execution-log.md`, „Paket 1 Abschlussreview abgeschlossen"

### Fehlende globale Antwortbudget-Projektion

- Schweregrad: P1
- Ursachensignatur: assembly-response-budget-projection-missing-after-compactor-removal
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs`, `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs`, `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs`
- Evidenz: Der erste Reviewer stellte fehlende globale Budgetierung fest. Korrekturversuch 2 bezieht nun `AssemblyAnalysisResponse.Enrich` ein und behandelt Singleton-Übergrößen. Korrekturversuch 3 ergänzt isolierte Dispatcher-/Enrichment- und Singleton-Regressionstests; 29/29 gezielte Tests und der scoped `get_violations`-Nachcheck meldeten 0 Verstöße. Der Abschluss-Review und die Gesamtgates stehen noch aus.
- Disposition: fixed
- attempts: 3
- Nächster Schritt: Keine weitere Korrektur; die explizite Budget-Testpflicht ist durch Dispatcher-/Singleton-Regressionen abgedeckt.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 3 Reviewer abgeschlossen"

### Projektionstyp übersteigt Zeilenlimit

- Schweregrad: P2
- Ursachensignatur: response-projection-file-footprint
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs`
- Evidenz: Der erste `get_violations`-Check meldete 543 statt maximal 500 Zeilen; Korrekturversuch 2 reduzierte die Produktionsdatei auf 268 Zeilen und der aktuelle scoped Nachcheck meldet 0 Verstöße.
- Disposition: fixed
- Nächster Schritt: Keine weitere Korrektur; Testdatei-Limits werden separat unter `response-projection-structural-rule-drift` verfolgt.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 2 abgeschlossen"

### Duplizierte Diagnose-Entfernung

- Schweregrad: P2
- Ursachensignatur: duplicate-diagnostic-removal-overloads
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs` – `TryRemoveLastDiagnostic`-Überladungen
- Evidenz: Der erste `get_violations`-Check meldete ein Duplikatcluster; Korrekturversuch 2 entfernte das exakte `TryRemoveLastDiagnostic`-Duplikat und der aktuelle scoped Nachcheck meldet 0 Verstöße.
- Disposition: fixed
- Nächster Schritt: Keine weitere Korrektur.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 2 abgeschlossen"

### Assembly-Extensions Footprint-Grenze

- Schweregrad: P2
- Ursachensignatur: assembly-extension-aicontext-footprint
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`
- Evidenz: Der erste `get_violations`-Check meldete AIContext-Footprint 2503 statt 2500; Korrekturversuch 2 brachte den Footprint unter das Limit und der aktuelle scoped Nachcheck meldet 0 Verstöße.
- Disposition: fixed
- Nächster Schritt: Keine weitere Korrektur.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 2 abgeschlossen"

### Aktive Produktionsregelverstöße der Budgetkorrektur

- Schweregrad: P1
- Ursachensignatur: response-projection-structural-rule-drift
- Scope/Fundstelle: `AssemblyAnalysisResponseLimits.cs`, `FindAssemblyExtensionsTool.cs`
- Evidenz: Der Review meldete 543 statt maximal 500 Zeilen, ein exaktes `TryRemoveLastDiagnostic`-Duplikat und AIContext-Footprint 2503 statt 2500. Korrekturversuch 2 bereinigte den Produktionsscope. Korrekturversuch 3 teilte die Testdateien in 492/448 Zeilen plus Budget-Partial-Dateien auf; der erweiterte Testscope meldet 0 Verstöße in 134 Dateien.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine weitere Korrektur; Abschluss-Review und Audit prüfen nur auf Regression.
- Log-Anker: `execution-log.md`, „Paket 1 Strukturkorrekturversuch 1 abgeschlossen"


### Bestehender AIContext-Footprint-Hinweis

- Schweregrad: P2
- Ursachensignatur: bestehender-aicontext-footprint
- Scope/Fundstelle: `src/AiNetLinter/Mcp` – bestehender AIContext-Footprint-Hinweis außerhalb der Paket-1-Fachursache
- Evidenz: Im frischen `get_violations`-Check weiterhin als eine der sieben Warnungen gemeldet; keine neue Ursache durch Paket 1 belegt.
- Disposition: accepted-deferred
- Nächster Schritt: Nur bei direkter Betroffenheit eines späteren Pakets erneut bewerten; kein solutionweiter Cleanup in diesem Task.
- Log-Anker: `execution-log.md`, „Paket 1 Implementierer abgeschlossen"

### Nicht klassifizierte vollständige Gate-Fehler

- Schweregrad: P1 (Klassifikation ausstehend)
- Ursachensignatur: full-gate-failures-unclassified
- Scope/Fundstelle: `src/AiNetLinter.FastTests` und `src/AiNetLinter.IntegrationTests` – Fehler aus dem vollständigen Lauf des Strukturkorrekturversuchs
- Evidenz: Der Implementierer meldete 2324 bestanden, 5 fehlgeschlagen, 2 übersprungen in FastTests sowie 376 bestanden, 1 fehlgeschlagen in IntegrationTests, ohne Testnamen oder Fehlerausgaben. Eine unabhängige Klassifikation gegen den aktuellen Diff ist erforderlich.
- Disposition: rejected/not-applicable
- Nächster Schritt: Die auftragsbezogenen vier Payload-Fehler und der Produktions-Parametervertrag sind als eigene P1-Einträge erfasst; Altbestand und Infrastrukturfehler werden nicht in den Paketumfang gezogen. Der Integrationstest bleibt bis zu einem frischen Lauf als Verifikationsrisiko protokolliert.
- Log-Anker: `execution-log.md`, „Paket 1 Abschlussreview abgeschlossen"

### Veraltete Fehlerpayload-Assertions

- Schweregrad: P1
- Ursachensignatur: typed-error-payload-contract-test-drift
- Scope/Fundstelle: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisConfigurationFailureTests.cs:71,124`; `src/AiNetLinter.FastTests/Mcp/Tools/Safeguard/SafeguardToolTests.cs:44,199`
- Evidenz: Vier Assertions erwarten `StructuredContent == null`, obwohl `McpErrorPayload` gemäß freigegebenem Paket-1-Vertrag korrekt geliefert wird.
- Disposition: fix-now
- attempts: 0
- Nächster Schritt: Assertions auf typisierte Fehlerfelder umstellen und betroffene FastTests erneut ausführen.
- Log-Anker: `execution-log.md`, „Paket 1 Abschlussreview abgeschlossen"
