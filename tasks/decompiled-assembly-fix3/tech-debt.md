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
- Evidenz: Der Abschlussreview bestätigte vier aktive `MaxMethodParameterCount`-Verstöße in `McpToolResults.cs`; Korrekturversuch 5 führte `McpErrorParameters` ein und der Produktionsscope-Nachcheck meldete keine Verstöße an den geänderten Symbolen. Unabhängige Review-Bestätigung steht noch aus.
- Disposition: fixed
- attempts: 2
- Nächster Schritt: Keine weitere Korrektur; die zwei scopefremden `FindSymbolScanner`-Warnungen bleiben separat `accepted-deferred`.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 5 Reviewer abgeschlossen"
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 5 abgeschlossen"

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
- Evidenz: Vier Assertions erwarteten `StructuredContent == null`; Korrekturversuch 5 prüft nun typisierte `McpErrorPayload`-Felder und der gezielte Lauf meldete 11/11 Tests grün. Unabhängige Review-Bestätigung steht noch aus.
- Disposition: fixed
- attempts: 2
- Nächster Schritt: Keine weitere Korrektur.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 5 Reviewer abgeschlossen"

### Bestehende FindSymbolScanner-Warnungen

- Schweregrad: P2
- Ursachensignatur: existing-find-symbol-scanner-warnings
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindSymbolScanner.cs`
- Evidenz: Der frische Produktionsscope-Check meldete zwei bestehende Warnungen; sie betreffen keine geänderten Symbole dieses Korrekturversuchs.
- Disposition: accepted-deferred
- Nächster Schritt: Nur bei einem späteren direkten Scope-Bezug prüfen; keine Ausweitung von Paket 1.
- Log-Anker: `execution-log.md`, „Paket 1 Korrekturversuch 5 abgeschlossen"

### Unvollständiger ReloadConfig-Erfolgspayload

- Schweregrad: P1
- Ursachensignatur: reload-config-structured-payload-missing
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/ReloadConfigTool.cs` und zugehörige Registrierung/Tests
- Evidenz: Der Paket-2-Review stellte das fehlende DTO fest. Korrekturversuch 1 ergänzte `ReloadConfigTool`/`ReloadConfigModels` und liefert Text plus Payload; die verbleibende Testabdeckung wird unter `package2-regression-test-contract-drift` verfolgt.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine separate Produktionskorrektur; strukturierte ReloadConfig-Tests im Regressionstest-Befund ergänzen.
- Log-Anker: `execution-log.md`, „Paket 2 Korrekturversuch 1 Reviewer abgeschlossen"

### Paket-2-Testvertrag nach Progressive Disclosure

- Schweregrad: P1
- Ursachensignatur: package2-regression-test-contract-drift
- Scope/Fundstelle: Assembly-Inspektions- und Health-/Reload-Tests
- Evidenz: Frische Nachweise meldeten zunächst 55/1 und 12/2; Korrekturversuch 3 ergänzte die fehlenden Session-, Diagnose- und Structured-Content-Regressionen sowie die beiden globalen Health-Default-Anpassungen. Die Orchestrator-Nachverifikation steht nun bei 61/61 FastTests und 18/19 fokussierten IntegrationTests; eine Textassertion erwartet noch den nicht mehr ausgegebenen Detailblock `Diagnosen: 0 von 4`. Vollständige Nicht-Stress-Gates und unabhängige Review-Bestätigung stehen aus.
- Disposition: fix-now
- attempts: 2
- Nächster Schritt: Health-Assertions sowie Diagnose-/Session-/Structured-Content-Regressionen ergänzen.
- Log-Anker: `execution-log.md`, „Paket 2 Korrekturversuch 3 abgeschlossen"

### Paket-2-Produktionsviolations aus Zwischenstand

- Schweregrad: P1
- Ursachensignatur: package2-production-violations
- Scope/Fundstelle: `src/AiNetLinter/Mcp` – vier durch den Paket-2-Zwischenstand verursachte Befunde im Produktionsscope
- Evidenz: Der Review verifizierte vier diffbedingte Befunde. Korrekturversuch 2 refaktorierte `InspectAssemblyTool.BuildResult` auf einen effektiven Parameter; der Produktionsscope-Nachcheck meldete keine neuen Fehler. `AddGetServerHealth` und `GetServerHealthResponseBuilder.Build` liegen im Limit.
- Disposition: fixed
- attempts: 2
- Nächster Schritt: Keine weitere Korrektur; die drei bestehenden Warnungen bleiben separat zurückgestellt.
- Log-Anker: `execution-log.md`, „Paket 2 Korrekturversuch 2 Reviewer abgeschlossen"

### Paket-2-Magic-Value-Kandidaten

- Schweregrad: P2
- Ursachensignatur: package2-magic-value-candidates
- Scope/Fundstelle: geänderte Paket-2-Produktionsbereiche
- Evidenz: Der Implementierer meldete 11 Magic-Value-Hinweise; konkrete gemeinsame fachliche Werte und sichere Zentralisierung sind noch nicht belegt.
- Disposition: accepted-deferred
- Nächster Schritt: Abschluss-Audit prüft nur scope-nahe, eindeutig gemeinsame Werte; keine pauschale Zentralisierung.
- Log-Anker: `execution-log.md`, „Paket 2 Implementierer abgeschlossen"
