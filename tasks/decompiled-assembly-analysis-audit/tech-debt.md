# Tech-Debt-Register

Stand: Abschlusskonsolidierung des read-only Audits `decompiled-assembly-analysis-audit`.

Die Queue enthält nur actionable Befunde mit belastbarer Evidenz. Es wurden keine Produktions-, Test-, Konfigurations- oder veröffentlichten Dokumentationsdateien geändert. Mangels eines vorhandenen dauerhaften Projekt-Backlogs bleiben die Einträge task-lokal und werden nicht stillschweigend verschoben.

## Priorität S1

### ASM-001 — Referenzexpansion trotz Root-only-Default

- **Schweregrad/Umfang:** S1 / U3; hohe Beweissicherheit.
- **Scope/Fundstelle:** `src/AiNetLinter/Mcp/AnalysisToolCall.cs:161-172`; Registrierung in `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:49,80,112`; geteilt mit Assembly-Inspection/Extension-Routing.
- **Evidenz:** Der Dispatcher ruft vor jedem Assembly-Handler unconditionally `lease.ExpandReferencesAsync` auf; die sichtbaren `includeReferences`-Defaults sind `false`; `Docs/agent-api.md:460` beschreibt Root-only als Default.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Expansion als Handler-Capability modellieren und Regressionen für `false` (keine Child-Leases, keine Referenzdiagnosen) sowie `true` (bounded Navigation) ergänzen.
- **Log-Anker:** Reviewer-/Fallback-Einträge Linse 01 in `execution-log.md`.

### CHK-001 — Cancellation vor Ownership-Bindung kann Checkout zurücklassen

- **Schweregrad/Umfang:** S1 / U2; hohe Beweissicherheit.
- **Scope/Fundstelle:** Provider-Orchestrierung zwischen `AcquireAsync`-Rückkehr und `TryGetCheckout`; gekoppelt an `ExternalSourceRepositoryAcquirer` und `ExternalSourceCheckoutHandle.Dispose`.
- **Evidenz:** Der Cancellation-Check kann vor der lokalen Handle-Bindung auslösen; der Fehlerpfad erhält dann `checkout == null` und erreicht die Ownership-Bereinigung nicht. Die spätere Cancellation-Phase ist dagegen durch gezielte Tests abgedeckt.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Deterministischen Testdouble-Test für das Rückgabe-/Bindungsfenster ergänzen und Ownership vor dem post-acquisition Check binden oder den unübertragenen Handle explizit entsorgen.
- **Log-Anker:** Nachträglicher unabhängiger Bericht Linse 04 in `execution-log.md`.

## Priorität S2

### EXTSRC-01 — Loader- und Runtime-URL-Policy divergieren

- **Schweregrad/Umfang:** S2 / U2; hohe strukturelle, mittlere reale Beweissicherheit.
- **Scope/Fundstelle:** `ExternalSourceMappingValidator.NormalizeUrl` gegenüber `ExternalSourceRepositoryUrlPolicy.TryNormalize`; erneute Prüfung in `ExternalSourceRepositoryAcquirer.TryValidateMapping`.
- **Evidenz:** Der Loader akzeptiert absolute HTTP(S)-URLs mit Userinfo, Query oder Fragment; die Runtimepolicy verwirft diese erst bei der Akquisition. Eine Mapping-Konfiguration kann daher zunächst erfolgreich laden und später sicherheits-/semantisch ungültig werden.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Eine gemeinsame Normalisierungs-/Policyquelle herstellen und Loader-Tests für Userinfo, Query, Fragment und gültige URL-Varianten ergänzen.
- **Log-Anker:** Unabhängiger Bericht `reports/02-external-source.md`, Befund `EXTSRC-01`.

### EXTSRC-02 — Kein Credential-Resolver im produktiven MCP-/Daemon-Einstieg

- **Schweregrad/Umfang:** S2 / U3; hohe strukturelle, mittlere Remote-Beweissicherheit.
- **Scope/Fundstelle:** Optionaler Resolver in `AssemblyAnalysisHostComposition.Create`; produktive Call-Sites in `McpServerCommand` und `DaemonHostCommand` übergeben keinen Resolver.
- **Evidenz:** Transport und Prompt-Isolation unterstützen einen Resolver, aber die Standard-Einstiege liefern `null`; geschützte Quellen bleiben dadurch im prompt-freien credential-losen Pfad.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Gewollte Credential-Quelle und Lebensdauer explizit festlegen, dann Resolver im produktiven Entry-Wiring und einen redigierten geschützten Source-Test nachweisen.
- **Log-Anker:** Unabhängiger Bericht `reports/02-external-source.md`, Befund `EXTSRC-02`.

### EXTSRC-03 — Konfigurierter MCP-Source-Flow fällt trotz Checkout auf Decompilation zurück

- **Schweregrad/Umfang:** S2 / U3; hohe Beweissicherheit für den Live-Befund, mittlere Beweissicherheit für die genaue Materialisierungsursache.
- **Scope/Fundstelle:** `AssemblyAnalysisRegistryEntryFactory`; `ExternalSourceSnapshotMaterializer`; `AssemblyAnalysisContextFactory`; produktive Assembly-MCP-Route.
- **Evidenz:** Der MCP-Test gegen die konfigurierte gemappte DLL erzeugte einen Repository-Checkout mit Solution und Source-Dateien. `inspect_assembly`, `find_assembly_extensions` und `get_server_health` meldeten trotzdem `origin=decompiled`, `sourcePath=none`, `snapshot=none` und `partial`. Der Assembly-Cache blieb bei `complete=false`; ein source-backed Snapshot wurde nicht bereitgestellt.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Einen reproduzierbaren Source-backed-Integrationstest mit Cache-/Solution-/Snapshot-Assertions ergänzen und den Materialisierungsfehler sicher, aber diagnostisch ausreichend projizieren. Klären, ob die externe Solution einen kontrollierten Package-/MSBuild-Restore benötigt.
- **Log-Anker:** Nachträglicher MCP-Live-Nachweis in `execution-log.md`; Befund `EXTSRC-03` in `reports/02-external-source.md`.

### ASM-002 — Nichttreffer anderer Assembly-Sessions werden als Partialdiagnose projiziert

- **Schweregrad/Umfang:** S2 / U2; hohe strukturelle Beweissicherheit.
- **Scope/Fundstelle:** `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs:30-61`; `AssemblyNavigationSupport.CreateSummary`; `FindReferencesTool.ResolveByNameAsync`.
- **Evidenz:** Nicht identitätsqualifizierte Namensauflösung fügt `SymbolNotFound` aus jeder Session ohne Treffer in die gemeinsame Diagnosemenge ein; `CreateSummary` setzt bei jeder Diagnose `completeness=partial`, auch wenn eine andere Session das Symbol erfolgreich liefert.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Erwartbare Nichtzuständigkeit von echten Session-/Referenzfehlern trennen und Regression für ein Root-only-Symbol über mehrere Sessions ergänzen.
- **Log-Anker:** Fallback-/MCP-Befund Linse 01 in `execution-log.md`; im unabhängigen Bericht wurde stattdessen die Batch-Navigation bestätigt.

### ASM-003 — `find_symbol` verliert Trunkierungsdiagnosen früherer Muster

- **Schweregrad/Umfang:** S2 / U2; hohe strukturelle Beweissicherheit.
- **Scope/Fundstelle:** `AssemblyFindSymbolTool.BuildResponseAsync:68-96`; `AssemblySymbolSearch.FindMatchesAsync:47-65`.
- **Evidenz:** Jede Musterabfrage erzeugt eine eigene Navigation, aber die Schleife behält nur die letzte. Ein früheres Muster kann bei `maxResults` begrenzt sein, ohne dass die abschließende gemeinsame Navigation diese Diagnose ausweist.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Diagnose-/Vollständigkeitsstatus über alle Muster akkumulieren oder pro Muster ausgeben und Batch-Regression ergänzen.
- **Log-Anker:** Unabhängiger Bericht `reports/01-assembly.md`, Befund `ASM-002`.

### F-05-01 — Erfolgreiche Cache-Generationen ohne begrenzten Retention-Sweep

- **Schweregrad/Umfang:** S2 / U3; hohe statische, mittlere Langzeit-Beweissicherheit.
- **Scope/Fundstelle:** `AssemblyDecompilationCache.Publish`; `ExternalSourceRepositoryCacheWriter.PublishGeneration`; Cleanup nur für nicht veröffentlichte Generationen.
- **Evidenz:** Erfolgreiche Publikationen erzeugen neue Generation-Verzeichnisse; ein sicherer Sweep alter, nicht mehr benötigter Generationen oder ein persistent wirkendes Diskbudget wurde in den geprüften Pfaden nicht gefunden. Aktive Lease-/Rollback-Sicherheit muss bei jeder Folgearbeit erhalten bleiben.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Retention-/Grace-Policy, sichere Sweep-Reihenfolge und Regression für wiederholte erfolgreiche Refreshes definieren.
- **Log-Anker:** Unabhängiger Bericht `reports/05-cache-snapshot.md`, Befund `F-05-01`.

### F-05-02 — Prozessweite Cache-Key-Lock-Tabelle wächst monoton

- **Schweregrad/Umfang:** S2 / U3; hohe strukturelle, mittlere Größen-Beweissicherheit.
- **Scope/Fundstelle:** `ExternalSourceRepositoryCacheWriter.cs:22,330-337,441-455`; statische `ConcurrentDictionary<string, SemaphoreSlim>`.
- **Evidenz:** Neue kanonische Entry-Pfade werden via `GetOrAdd` registriert; der Dispose-/Lease-Pfad gibt nur das Semaphore frei und entfernt den Dictionary-Eintrag nicht.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Race-sichere Entfernung nach dem letzten Waiter/Publisher entwerfen und einen langlebigen Multi-Key-Test mit Reclamation-Assertion ergänzen.
- **Log-Anker:** Unabhängiger Bericht `reports/05-cache-snapshot.md`, Befund `F-05-02`.

### MCP-L6-001 — Ungültige Positionsspalte wird als Workspace-Fehler ausgegeben

- **Schweregrad/Umfang:** S2 / U2; hohe Beweissicherheit.
- **Scope/Fundstelle:** `SymbolIdentifierResolver.TryParsePosition`; `FindReferencesTool.ResolveByPositionAsync`; `McpToolResults.CompilationError`.
- **Evidenz:** Spalte `0` wird akzeptiert und führt vor `FindToken` zu `ArgumentOutOfRangeException`, die als `WORKSPACE_DIAGNOSTIC` mit `isError=true` projiziert wird, statt als recoverable `INVALID_ARGUMENT`.
- **Disposition:** `promoted-to-project-debt`; `attempts: 0`.
- **Nächster Schritt:** Gemeinsame Zeilen-/Spaltenbereichsprüfung gegen `SourceText` und negative Tests für nullgroße/überlange Koordinaten.
- **Log-Anker:** Unabhängiger Bericht `reports/06-mcp-contracts.md`, Befund `MCP-L6-001`.

### UX-001 — Registry überschreitet projektiertes AI-Context-Footprint

- **Schweregrad/Umfang:** S2 / U2; hohe MCP-Metrik-, mittlere Laufzeit-/Agentenwirkung-Beweissicherheit.
- **Scope/Fundstelle:** `AssemblyAnalysisRegistry.cs:24-499`; `get_feature_context` meldet Type LOC `648 > 500`.
- **Evidenz:** Registry bündelt viele Lebenszyklus-, Generation- und Ownership-Invarianten mit 25 Aufrufern und 18 Tests; der Metrikbefund ist durch den MCP-Kontextcheck sichtbar.
- **Disposition:** `accepted-deferred`; `attempts: 0`.
- **Nächster Schritt:** Scope-nahe Zerlegung oder schmale Interfaces prüfen, ohne Ownership-/Generation-Invarianten zu verteilen.
- **Log-Anker:** Fallback-Bericht `reports/07-agent-surface.md`, Befund `UX-001`.

### MCP-WIRE-001 — Internes Diagnosebudget ist nicht als globales Wire-Budget nachgewiesen

- **Schweregrad/Umfang:** S2 / U3; mittlere Beweissicherheit, da die statische Mehrfachprojektion belegt, die vollständige Serialisierung aber nicht gemessen wurde.
- **Scope/Fundstelle:** `AssemblyAnalysisResponseLimits`; `InspectAssemblyTool` und `ProjectReferenceSessions`; `Docs/configuration.md:35`.
- **Evidenz:** Samples werden unter einem internen Cap ausgewählt, erscheinen aber top-level, in Summary-Feldern und pro Referenzsession. Ein vorhandener Test misst nur top-level Samples, nicht die vollständige serialisierte Antwort.
- **Disposition:** `accepted-deferred`; `attempts: 0`; nicht als bestätigter globaler Wire-Vertragsbruch klassifiziert.
- **Nächster Schritt:** Maximalfixture über JSON-Serialization messen; erst danach deduplizieren, global budgetieren oder die Dokumentation präzisieren.
- **Log-Anker:** Fallback-Befund Linse 06 sowie Querverweis in `reports/07-agent-surface.md`; unabhängige Linse 06 führt den Punkt als Abdeckungsgrenze.

### F-05-03 — Root-Byte-only-Reuse und mögliche Source-/Dependency-Refreshes

- **Schweregrad/Umfang:** bedingtes S2 / U3; mittlere Beweissicherheit.
- **Scope/Fundstelle:** `AssemblyAnalysisSession.TryReuseCurrent` und Source-/Dependency-Auswahlpfad.
- **Evidenz:** Identischer Root-SHA erlaubt Reuse; eine source-backed Liveprobe und ein direkter Test für reine Source-/Dependency-Änderung fehlen. Ob dies dem beabsichtigten Refresh-Vertrag widerspricht, ist nicht vollständig festgelegt.
- **Disposition:** `accepted-deferred`; `attempts: 0`; ausdrücklich kein bestätigter Defekt.
- **Nächster Schritt:** Refresh-Identität im Vertrag entscheiden und einen deterministischen Dependency-/Source-Invalidationstest ergänzen.
- **Log-Anker:** Unabhängiger Bericht `reports/05-cache-snapshot.md`, Befund `F-05-03`.

## Nicht in die Queue übernommen

- Der unabhängige Probehinweis „falsches Assembly-Root-Routing“ mit einem referenzierten Basistyp wurde nach Orchestrator-Abgleich als `rejected/not-applicable` verworfen; er bleibt im Linse-01-Report dokumentiert.
- `GIT`-Befunde, geschützte-Remote-Abdeckung, Reparse-Capability-Skips sowie die niedrigen Dead-Code-/Magic-Value-/Clone-Kandidaten sind Coverage-Grenzen oder unbestätigte Prüfhinweise, keine Tech-Debt-Einträge. Der öffentliche Live-Checkout ist dagegen in `EXTSRC-03` als bestätigter Delivery-Fallback aufgenommen.

## Priorität S3

### MCP-L6-002 — Health-Response-Builder überschreitet Agenten-Kontextgrenze

- **Schweregrad/Umfang:** S3 / U2; hohe MCP-Metrik-Beweissicherheit.
- **Scope/Fundstelle:** `GetServerHealthResponseBuilder.cs:17`; transitive Footprint-Meldung `2502 > 2500`, aktive Grenze in `rules.json:154`.
- **Evidenz:** `get_violations` meldet den eindeutigen `AIContextFootprint`-Befund; keine unmittelbare Laufzeitstörung nachgewiesen.
- **Disposition:** `accepted-deferred`; `attempts: 0`.
- **Nächster Schritt:** Health-Projektion in kleinere Verantwortungsgrenzen schneiden oder schmale Abhängigkeiten einführen; danach `get_violations` erneut ausführen.
- **Log-Anker:** Unabhängiger Bericht `reports/06-mcp-contracts.md`, Befund `MCP-L6-002`.
