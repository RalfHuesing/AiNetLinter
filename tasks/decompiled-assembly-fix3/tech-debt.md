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
- Evidenz: Frische Nachweise meldeten zunächst 55/1 und 12/2; Korrekturversuch 3 ergänzte Session-, Diagnose- und Structured-Content-Regressionen sowie globale Health-Default-Anpassungen. Der Resume-Implementierer korrigierte die verbliebenen konkreten Health-/ReloadConfig-Werte und ergänzte Assembly-`find_references`-Abdeckung. Versuch 2 ergänzt nun zusätzlich den echten Assembly-`get_call_tree`-E2E-Pfad mit sechs kontrollierten fehlenden Referenzen und gemeinsame konkrete Assertions für Zähler, Sample-Limit, Truncation und Text-/Structured-Content-Gleichheit. Orchestrator-Verifikation: Assembly-FastTests 6/6, Health-/ReloadConfig-IntegrationTests 15/15, Build ohne Warnungen/Fehler. Vollgates bleiben wegen unabhängiger Altfehler rot; unabhängiger Abschlussreview steht noch aus.
- Disposition: fixed
- attempts: 2
- Nächster Schritt: Keine weitere Korrektur; die Regressionen decken den echten Assembly-`get_call_tree`- und `find_references`-Vertragsweg ab.
- Log-Anker: `execution-log.md`, „Wiederaufnahme Paket 2 Korrekturversuch 1 abgeschlossen"

### Diagnoseprojektion mit doppelter Ownership

- Schweregrad: P1
- Ursachensignatur: package2-diagnosis-projection-ownership
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyFindReferencesTool.cs`, `src/AiNetLinter/Mcp/Tools/SymbolGraph/TransitiveCallGraphFormatter.cs`
- Evidenz: Der Resume-Implementierer verlagert die Projektion in `TransitiveCallGraphFormatter.FormatResponse` und `FormatAssemblyCallTreeResponse`; `AssemblyGetCallTreeTool` projiziert nicht mehr selbst. Der unabhängige Review bestätigt, dass Symbolgraph- und Assembly-CallTree-Pfade denselben projizierten Datensatz für Text und Structured Content verwenden; ein Produktionsfehler ist nicht mehr belegt.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine weitere Produktionskorrektur. Der verbleibende E2E-Testausbau wird ausschließlich unter `package2-regression-test-contract-drift` geführt.
- Log-Anker: `execution-log.md`, „Paket 2 Abschlussreview abgeschlossen"

### Paket-2-Testverzeichnis über Strukturgrenze

- Schweregrad: P1 (nutzerpriorisiert)
- Ursachensignatur: package2-test-directory-footprint
- Scope/Fundstelle: `src/AiNetLinter.FastTests/Mcp/Assemblies`
- Evidenz: Der unabhängige Review meldete 31 direkte Einträge bei einer Regelgrenze von 30. Die drei fachlich zusammengehörigen Navigation-/Route-/Contract-Tests wurden nach `src/AiNetLinter.FastTests/Mcp/Assemblies/Navigation` mit Namespace `AiNetLinter.FastTests.Mcp.Assemblies.Navigation` verschoben; der Elternordner hat nun 29 direkte Einträge und der finale MCP-Scope meldet 0 Befunde.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine weitere Korrektur; die Strukturgrenze ist durch fachliche Verschiebung erfüllt.
- Log-Anker: `execution-log.md`, „Wiederaufnahme Paket 2 Korrekturversuch 1 Reviewer abgeschlossen"

### Paket-2-Produktionsviolations aus Zwischenstand

- Schweregrad: P1
- Ursachensignatur: package2-production-violations
- Scope/Fundstelle: `src/AiNetLinter/Mcp` – vier durch den Paket-2-Zwischenstand verursachte Befunde im Produktionsscope
- Evidenz: Der Review verifizierte vier diffbedingte Befunde. Korrekturversuch 2 refaktorierte `InspectAssemblyTool.BuildResult` auf einen effektiven Parameter; der Produktionsscope-Nachcheck meldete keine neuen Fehler. `AddGetServerHealth` und `GetServerHealthResponseBuilder.Build` liegen im Limit.
- Disposition: fixed
- attempts: 2
- Nächster Schritt: Keine weitere Korrektur; die drei bestehenden Warnungen bleiben separat zurückgestellt.
- Log-Anker: `execution-log.md`, „Paket 2 Korrekturversuch 2 Reviewer abgeschlossen"

### Paket-3-Produktionsverzeichnis über Strukturgrenze

- Schweregrad: P1 (nutzerpriorisiert)
- Ursachensignatur: package3-production-directory-namespace-organization
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis`
- Evidenz: Der unabhängige Review meldete 35 direkte Einträge bei einer Regelgrenze von 30. Body-Verantwortungen wurden nach `Analysis/Bodies` (`AiNetLinter.Mcp.Assemblies.Analysis.Bodies`) und Source-Selection nach `Analysis/SourceSelection` (`AiNetLinter.Mcp.Assemblies.Analysis.SourceSelection`) verschoben; der Elternordner hat nun 28 direkte Einträge und der finale Scope-Check meldet keinen Directory-Footprint-Befund.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine weitere Korrektur; die fachlichen Namespace-Grenzen sind durch den unabhängigen Review bestätigt.
- Log-Anker: `execution-log.md`, „Paket 3 Korrekturversuch 2 Reviewer abgeschlossen"

### Paket-2-Magic-Value-Kandidaten

- Schweregrad: P2
- Ursachensignatur: package2-magic-value-candidates
- Scope/Fundstelle: geänderte Paket-2-Produktionsbereiche
- Evidenz: Der Implementierer meldete 11 Magic-Value-Hinweise; konkrete gemeinsame fachliche Werte und sichere Zentralisierung sind noch nicht belegt.
- Disposition: accepted-deferred
- Nächster Schritt: Abschluss-Audit prüft nur scope-nahe, eindeutig gemeinsame Werte; keine pauschale Zentralisierung.
- Log-Anker: `execution-log.md`, „Paket 2 Implementierer abgeschlossen"

### Abschlussaudit-Magic-Value-Kandidaten

- Schweregrad: P2
- Ursachensignatur: audit-mcp-magic-value-candidates
- Scope/Fundstelle: `src/AiNetLinter/Mcp` – 249 Treffer in 241 eindeutigen Einträgen über 292 Dateien
- Evidenz: Der einmalige Abschlussaudit meldete überwiegend einmalige Diagnosecodes, Fehlermeldungen, Identifier und bestehende Konstantenkandidaten. Die Werte sind ohne zusätzliche Fachentscheidung nicht sicher gemeinsam zu zentralisieren; `changedOnly=true` war wegen sauberem Working Tree leer.
- Disposition: accepted-deferred
- Nächster Schritt: Kein solutionweiter Cleanup in diesem Task; nur bei einem späteren, fachlich abgegrenzten Paket erneut prüfen.
- Log-Anker: `execution-log.md`, „Einmaliger Abschlussaudit ausgeführt"

### Paket-3-Strukturgrenzen in Source-/Body-Navigation

- Schweregrad: P1
- Ursachensignatur: package3-structural-rule-drift
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblySourceSelectionOrchestrator.cs` und direkt betroffene Konstruktor-/AIContext-Schnittstellen
- Evidenz: Dateigrößen-, Methodenlängen- und Komplexitätsbefunde sind beseitigt. Der Orchestrator-AIContext-Footprint sank 2607 → 2396, das `HasNoBody`-Duplikat wurde durch `AssemblyBodySyntax` zentralisiert, und der fehlende `StaticTestSentinel` ist durch direkte `@covers`-Abdeckung behoben. Produktions- und Teststruktur liegen mit 28 bzw. 29 direkten Einträgen unter der Grenze; der finale Scope-Check meldet nur bestehende, nicht diffbetroffene AIContext-Hinweise.
- Disposition: fixed
- attempts: 3
- Nächster Schritt: Keine weitere Korrektur; bestehende AIContext-Hinweise außerhalb des Diffs bleiben zurückgestellt und werden nur im Abschlussaudit auf Regression geprüft.
- Log-Anker: `execution-log.md`, „Paket 3 Implementierer abgeschlossen"

### Paket-3-Fallback-Diagnosepropagation

- Schweregrad: P1
- Ursachensignatur: package3-fallback-diagnostic-propagation
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`, `src/AiNetLinter/Mcp/Assemblies/Analysis/Factories/AssemblyAnalysisRegistryEntryFactory.cs`
- Evidenz: Die konkrete Compilation-Diagnose wird nun von `TryGetProjectCompilationAsync` über `AssemblySourceFallbackMetadata` bis in `AssemblyOrigin.SourceDiagnostics` getragen; `workspace-failure` bleibt als Zusatzdiagnose erhalten. Eine gezielte `CS0246`-Regression und 35 fokussierte FastTests/17 fokussierte IntegrationTests sind grün.
- Disposition: fixed
- attempts: 2
- Nächster Schritt: Keine weitere Korrektur; der vollständige Fallback-Pfad ist durch den unabhängigen Review bestätigt.
- Log-Anker: `execution-log.md`, „Paket 3 Review abgeschlossen"

### Paket-3-Body-Symbolauflösung bei Overloads

- Schweregrad: P1
- Ursachensignatur: package3-body-symbol-resolution-ambiguity
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs`, `FindMember`/`ResolveBodyAsync`
- Evidenz: Die ursprüngliche Zuordnung nach Name, Parameteranzahl und Generic-Arity wurde auf vollständige Symbolidentität/Parametertypen und deterministische Kandidatenauswahl umgestellt. Gleichartige Overloads werden durch fokussierte Regressionen unterschieden; der aktuelle Struktur-Refactor erhält diesen Vertrag.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Randtypen der textuellen Parametertypauflösung bleiben als P2-Folgearbeit dokumentiert; keine Korrekturschleife in diesem Task.
- Log-Anker: `execution-log.md`, „Paket 3 Review abgeschlossen"

### Paket-3-Literalregression unvollständig

- Schweregrad: P2
- Ursachensignatur: package3-literal-regression-coverage
- Scope/Fundstelle: `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetClassStructureToolTests.cs`, `ExecuteAsync_ConstantFields_FormatsInvariantLiteralValues`
- Evidenz: Die zentrale Literalformatierung deckt jetzt positive und negative Zahlen, `null`, String, Char und Bool ab; der gezielte Literaltest ist grün.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine weitere Korrektur; die ergänzte Literalabdeckung ist durch den unabhängigen Review bestätigt.
- Log-Anker: `execution-log.md`, „Paket 3 Review abgeschlossen"

### Paket-3-Overload-Randtypabdeckung

- Schweregrad: P2
- Ursachensignatur: package3-overload-identity-edge-cases
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`
- Evidenz: Die Auflösung ist für die getesteten `int`-/`string`-Overloads deterministisch und fail-closed. Sonderfälle wie `ref readonly`, komplexe Nullable-/Alias-/Function-Pointer-Typen und nicht direkt unterstützte Methodenkinds sind nicht explizit regressionstestiert.
- Disposition: promoted-to-project-debt
- Nächster Schritt: In einem separat abgegrenzten Body-Identity-Paket ergänzen; keine Scope-Erweiterung dieses Tasks.
- Log-Anker: `execution-log.md`, „Paket 3 Abschlussreview abgeschlossen"

### Paket-4-Assembly-Extension-Unterstützung

- Schweregrad: P1
- Ursachensignatur: package4-managed-executable-support
- Scope/Fundstelle: `src/AiNetLinter/Configuration/AssemblyPathValidation.cs` und Assembly-Ziel-/Source-Mapping-Verbraucher
- Evidenz: Der direkte Supportpfad liefert für native PE nun einen typisierten und hilfreichen `Recoverable`-Payload mit `IsError=false` und `Recoverable=true`. Die öffentliche Registry-/Lease-Route mappt denselben Fall jedoch noch auf `AnalysisFailed`/`isError=true`; der Routenvertrag ist daher nicht geschlossen.
- Disposition: fix-now
- attempts: 2
- Nächster Schritt: Den typisierten Native-PE-Recoverable-Vertrag durch `AssemblyAnalysisRegistry`/`AnalysisToolCall` bis zur registrierten MCP-Route propagieren und einen öffentlichen Routentest ergänzen.
- Log-Anker: `execution-log.md`, „Paket 4 Implementierer abgeschlossen"

### Paket-4-Hotspots-Parameter

- Schweregrad: P2
- Ursachensignatur: package4-hotspots-parameter-contract
- Scope/Fundstelle: `GetHotspotsTool`, `GetHotspotsScanner` und `FileStructureToolRegistrations`
- Evidenz: `maxResults` (Default 50, Cap 200) und `minLinePercentage` (Default 80, 0–100) werden typisiert normalisiert; Ergebniszählung, Trunkierung und deterministische Sortierung sind regressionstestiert.
- Disposition: review-pending
- attempts: 0
- Nächster Schritt: Unabhängigen Paket-4-Abschlussreview abwarten.
- Log-Anker: `execution-log.md`, „Paket 4 Implementierer abgeschlossen"

### Paket-4-SymbolIdentifier- und Dokumentationsvertrag

- Schweregrad: P2
- Ursachensignatur: package4-symbolidentifier-documentation-drift
- Scope/Fundstelle: Feature-/TestContext-Registrierungen, `Docs/agent-api.md`, `Docs/integration.md` und `.agents/rules/AiNetLinter-McpWorkflow.mdc`
- Evidenz: `symbolIdentifier` ist als primäre Benennung dokumentiert und registriert; `symbol` bleibt kompatibler Alias. Assembly-Detailflags, `bodyAvailability`, `.exe`-Support, Health-Ziele und Progressive Disclosure sind dokumentiert und per Smoke-/Vertragstests abgeglichen.
- Disposition: review-pending
- attempts: 0
- Nächster Schritt: Unabhängigen Paket-4-Review abwarten.
- Log-Anker: `execution-log.md`, „Paket 4 Implementierer abgeschlossen"

### Paket-4-Health-Dokumentationsvertrag

- Schweregrad: P1
- Ursachensignatur: package4-health-documentation-drift
- Scope/Fundstelle: `Docs/agent-api.md`, `Docs/integration.md`, `ServerMaintenanceToolRegistrations` und `GetServerHealthResponseBuilder`
- Evidenz: Die Runtime aggregiert den parameterlosen Health-Call standardmäßig und unterstützt `includeSessions`/`maxSessions`; die Dokumentation beschreibt nun Aggregate, zielgebundene Details und die beiden Steuerparameter synchron zur Runtime. Smoke-/Integrationstests sind grün.
- Disposition: fixed
- attempts: 1
- Nächster Schritt: Keine weitere Korrektur; Aggregate-/Detailvertrag mit `includeSessions`/`maxSessions` ist synchronisiert und getestet.
- Log-Anker: `execution-log.md`, „Paket 4 Reviewer abgeschlossen"

### Paket-4-Registry-Alias-Kanonisierung

- Schweregrad: P2
- Ursachensignatur: package4-registry-alias-canonicalization
- Scope/Fundstelle: `AssemblyAnalysisRegistry.LeaseAsync` / Pfadidentität
- Evidenz: Alias-/Reparse-/8.3-Doppelgeneration war in der Untersuchung nicht reproduzierbar. Eine Windows-spezifische Kanonisierung würde ohne Regression ein Risiko für Pfadsemantik und Lebenszeitvertrag einführen.
- Disposition: accepted-deferred
- Nächster Schritt: Nur bei reproduzierbarem Alias-Test als eigenes Paket wieder aufnehmen.
- Log-Anker: `execution-log.md`, „Paket 4 Implementierer abgeschlossen"

### Paket-4-Test- und Betriebsnachschärfungen

- Schweregrad: P2
- Ursachensignatur: package4-follow-up-verification
- Scope/Fundstelle: `GetTestContextTool`, `ManagedAssemblyBinaryTests`, `GetHotspotsToolTests` und aktiver MCP-Daemon
- Evidenz: Die TestContext-Fehlerhilfe nennt noch `symbol`; der managed-`.exe`-Test beweist Nichtausführung nur indirekt; die Hotspot-Sortierungsassertion ist teilweise selbstreferenziell. Der aktive MCP-Daemon kann gegenüber dem lokalen Source-Stand veraltet sein und benötigt vor Deployment einen Neustart/Aktualisierung.
- Disposition: accepted-deferred
- Nächster Schritt: In einer separaten Verifikations-/Release-Schleife nachschärfen; keine P1-Blockade der aktuellen Lieferung.
- Log-Anker: `execution-log.md`, „Paket 4 Reviewer abgeschlossen"

### Paket-3-Diagnose-Sample-Priorisierung

- Schweregrad: P2
- Ursachensignatur: package3-fallback-diagnostic-sample-priority
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`
- Evidenz: Nach dem Zusammenführen werden Diagnosen mit `Take(20)` begrenzt. Bei mindestens 20 Snapshot-Diagnosen können `CompilationFailed` oder `WorkspaceDiagnostic` aus den Samples fallen; `fallbackReason` bleibt unabhängig davon erhalten.
- Disposition: promoted-to-project-debt
- Nächster Schritt: In einem separaten Diagnose-Projektionspaket die Priorisierung systematisch absichern; keine Änderung nach dem freigegebenen Review.
- Log-Anker: `execution-log.md`, „Paket 3 Abschlussreview abgeschlossen"

### Paket-3-Resolver-Direktabdeckung

- Schweregrad: P2
- Ursachensignatur: package3-resolver-direct-test-coverage
- Scope/Fundstelle: `src/AiNetLinter/Mcp/Assemblies/Analysis/Bodies/AssemblyDecompiledBodyResolver.cs`
- Evidenz: Der Resolver wird über Navigationstests indirekt ausgeführt; eine eigene direkte Testdatei ist nicht erforderlich für die aktuelle Regelprüfung, da keine aktive `StaticTestSentinel`-Violation besteht.
- Disposition: accepted-deferred
- Nächster Schritt: Nur bei einer späteren Body-Navigationserweiterung direkte Resolver-Tests ergänzen.
- Log-Anker: `execution-log.md`, „Paket 3 Abschlussreview abgeschlossen"
