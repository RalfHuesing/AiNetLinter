# Epic 8 – Test- und Dokumentationsnachweis

## Befundregister

### Bug

- **E8-BUG-01 – Negativtest verankert die falsche Redaction-Erwartung**  
  **Priorität:** P1 · **Größe:** M · **Vertrauen:** hoch

  **Ist:** `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/ManagedAssemblyBinaryTests.cs:47-75`, insbesondere `InspectAssembly_NativePeFailsWithTypedMetadataDiagnostic`, erwartet im strukturierten Fehlerpayload den unveränderten Eingabepfad (`payload.Context == nativeAssemblyPath`). Der Test prüft damit genau die Pfadweitergabe, die für einen nicht verwalteten PE-Negativfall nicht als öffentlicher Vertrag gelten darf.

  **Soll:** Der Test sollte nur den typisierten, recoverable Fehler, den sicheren fachlichen Grund und das Fehlen konkreter Pfade/Exceptiontexte in Text und Structured Content absichern. Das gilt gleichermaßen für `FALSE-01` und ungültige Pfade.

  **Auswirkung:** Die bestehende Test-Suite würde eine spätere Redaction-Korrektur als Regression behandeln und schützt dadurch die bereits als **E7-BUG-01** registrierte Leckage. Das ist kein neuer technischer Ursprung des Leaks, sondern ein widersprüchlicher Test-Orakel-/Vertragsnachweis.

  **Empfehlung / spätere Verifikation:** Nach der Redaction-Klärung den Test auf sichere Negativassertionen umstellen und anschließend denselben Inhalt in Text, Structured Content und zielgebundener Health-Sicht prüfen. Keine konkrete externe Identität in Testnamen, Fehlermeldungen oder Auditmaterial aufnehmen.

  **Abgrenzung / Unsicherheit:** Der Test wurde nur gelesen, nicht ausgeführt. Die Ursache in `AssemblyAnalysisService`, `AssemblyAnalysisToolSupport`, der Assembly-Route und der Response-Anreicherung bleibt der Epic-7-Befund und wird hier nicht dupliziert.

### Optimierung

- **E8-OPT-01 – Statische Testzuordnung unterschätzt indirekte Assembly-Abdeckung**  
  **Priorität:** P2 · **Größe:** M · **Vertrauen:** hoch

  **Ist:** `get_test_context` meldet für `AssemblyAnalysisToolRegistrations`, `AssemblyAnalysisService`, `AssemblyAnalysisResponseLimits`, `AssemblyAnalysisHealthSnapshotProvider`, `AssemblyNavigationSupport`, `AssemblySymbolSearch`, `AssemblyReferenceNavigator`, `AssemblyDecompiledBodyResolver` und `AssemblyHealthProjection` keine statisch zugeordneten Testdateien (`isUntested=true`). Gleichzeitig existieren indirekte bzw. route-nahe Nachweise, unter anderem in `AssemblyAnalysisToolTests.cs:23-425`, `AssemblyAnalysisToolTests.ResponseBudget.cs:18-139`, `AssemblyAnalysisRouteTests.cs:32-316`, `McpServerAssemblyHealthE2ETests.cs:28-162` und `AssemblyAnalysisSessionTests.cs:22-315`. Explizite `@covers`-Markierungen benennen überwiegend Dispatcher, Expander, Session- und Toolklassen, nicht die genannten Registrierungs-, Projektions- und Orchestrierungshilfen.

  **MCP-Evidence:** Für jedes genannte Symbol wurde mit dem aktuellen Schema `get_test_context({targetType:"project", targetPath:"<absoluter Repo-Root>", symbolIdentifier:"<Symbol>", maxResults:100})` abgefragt; die Antworten enthielten keine Zuordnung. Gegenprobe war `get_feature_context` mit `includeTests=true`, `maxTests=50` sowie den übrigen Dimensionen aktiv; dort blieben die statischen Testlisten für die genannten Hilfen ebenfalls leer, während Dispatcher, Session, Registry und Context Factory Testzuordnungen zeigten.

  **Soll:** Der Testkontext sollte für eine Coverage-Entscheidung zwischen direkter Markierung und indirekter Route-Abdeckung unterscheiden können. Entweder werden vorhandene Tests gezielt mit `@covers` ergänzt, oder der statische Mapper dokumentiert/ermittelt die belegte Route nachvollziehbar.

  **Auswirkung:** Ein Orchestrator kann für tatsächlich indirekt belastete Codepfade fälschlich neue Tests planen oder vorhandene Nachweise übersehen. Umgekehrt darf `isUntested=true` hier nicht als Aussage „zur Laufzeit ungetestet“ gelesen werden.

  **Empfehlung / spätere Verifikation:** Testkontext und Dokumentation um die Unterscheidung `direkt`, `indirekt über Dispatcher/Route` und `nur gelesen` ergänzen. Danach die Assembly-Capability-Matrix erneut per MCP gegen Testzuordnungen und Testdateien prüfen.

  **Abgrenzung / Unsicherheit:** Dies ist kein Befund, dass alle genannten Hilfen ohne Laufzeitabdeckung sind. Der MCP-Testkontext ist statische Zuordnung; die vorhandenen indirekten Tests wurden nur gelesen und nicht ausgeführt.

### Missing Feature

- **E8-MF-01 – Es fehlt ein öffentlicher Assembly-Capability-Regressionstest über die gesamte Matrix**  
  **Priorität:** P1 · **Größe:** L · **Vertrauen:** hoch

  **Ist:** `Docs/agent-api.md:335-383` dokumentiert elf assembly-fähige Folgewerkzeuge sowie `inspect_assembly`, `find_assembly_extensions` und `get_server_health`. `WiringToolCollectionContractTests.cs:66-116` prüft dafür vor allem Beschreibungsmarker. `McpServerAssemblyHealthE2ETests.cs:28-67,123-140` ruft den öffentlichen MCP-Kanal für die eigene Produkt-Assembly nur bei `inspect_assembly`/Extensions auf und prüft ein begrenztes Schema; die Navigationstests in `AssemblyAnalysisRouteTests.cs:32-316` und `AssemblyAnalysisPathContractTests.cs:32-208` rufen dagegen interne Route-/Toolpfade mit synthetischen Assemblies auf.

  **Soll:** Ein table-driven Nachweis muss den öffentlichen MCP-Client und die dokumentierte Capability-Matrix verbinden: Root-only-Default, explizites `includeReferences`, Herkunft/Status/Completeness, Diagnose- und Truncation-Projektion, Body-/Struktur-/Metrik-Folgeabrufe, `.dll`- und `.exe`-Fälle sowie den recoverable Nicht-.NET-Negativfall.

  **Auswirkung:** Registrierungs- und Dispatcher-Tests können grün sein, obwohl Schema, öffentliche Route und typed/textual response bei einem realen Assembly-Ziel auseinanderlaufen. Die bereits registrierten Semantikbefunde aus Epics 1–6 (unter anderem Referenzexpansion, Navigation-IDs und Response-Budget) erhalten dadurch keinen durchgehenden Regressionsschutz.

  **Empfehlung / spätere Verifikation:** Einen separaten Integrationstest mit `McpTestClient` und ausschließlich den Labels `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03`, `FALSE-01` als Testfall-IDs vorsehen; konkrete externe Identitäten bleiben aus Quelltext, Bericht und Hand-off heraus. Ergänzend eine synthetische Matrix für alle öffentlichen Assembly-Routen verwenden und je Fall Text/Structured Content, `isError`, `analysis`, `navigation`, `completeness`, Redaction und Nichtausführung prüfen.

  **Abgrenzung / Unsicherheit:** Die Matrix darf wegen externer Verfügbarkeit nicht Teil dieses Read-only-Laufs oder eine fest verdrahtete Produktvoraussetzung werden. Die vorhandenen direkten Route-Tests bleiben wertvoll; dieser Befund betrifft deren fehlende öffentliche, matrixweite Klammer, nicht die einzelnen bereits registrierten technischen Ursachen.

- **E8-MF-02 – Der source-backed Vertrag ist nur über Fakes/Komponenten, nicht über den öffentlichen konfigurierten Pfad belegt**  
  **Priorität:** P1 · **Größe:** M · **Vertrauen:** hoch

  **Ist:** `AssemblyAnalysisContextFactoryTests.cs:22-260` und `AssemblyAnalysisToolSupportTests.cs:24-450` prüfen Matched-, Fallback-, Provider- und Lease-Zustände mit Test-Snapshots bzw. Fake-Providern. `AssemblyAnalysisHostCompositionTests.cs:21-68` prüft die Hostverdrahtung mit einem absichtlich nicht verfügbaren Provider. Ein öffentlicher MCP-Call über die konfigurierte source-backed Auswahl mit `GIT-01` ist in den gelesenen Fast-/Integration-Tests nicht nachgewiesen; `McpServerAssemblyHealthE2ETests.cs` verwendet für seine Assembly-Calls die eigene Produkt-Assembly.

  **Soll:** Der dokumentierte Vertrag aus `Docs/agent-api.md:324-332,443-482` und `Docs/integration.md:325-330` muss mindestens einmal über die reale öffentliche Route nachweisen, dass Source- und Decompilation-Fallback, `origin`, `trust`, `contentMode`, Status und Diagnosen konsistent projiziert werden.

  **Auswirkung:** Die Komponenten können korrekte Fakes verarbeiten, ohne dass Provider-Konfiguration, Host-Komposition, Session-Auswahl und MCP-Response gemeinsam belegt sind. Die fehlende Attestation/Probe-Root-Semantik bleibt dabei **E3-MISSING-02** bzw. **E3-MISSING-03**; sie wird nicht als neuer Implementierungsbefund ausgegeben.

  **Empfehlung / spätere Verifikation:** Für `GIT-01` einen redigierten öffentlichen Call mit konfiguriertem Mapping, einem Source-Erfolg und einem kontrollierten Fallback ausführen. Nur Label, Origin-Klasse, Status, Completeness und sichere Diagnoseform dokumentieren; keine URL, keinen Pfad, Hash oder dekompilierten Inhalt übernehmen.

  **Abgrenzung / Unsicherheit:** Die vorherigen Epics enthalten bereits redigierte MCP-Nachweise für `GIT-01`; sie belegen das aktuelle Verhalten, ersetzen aber keinen dauerhaft wiederholbaren Test im Repository. Dieser Befund ist die fehlende Testklammer.

- **E8-MF-03 – Der Cache-Test prüft Dateien und Manifest, aber nicht den semantischen Dokument-Roundtrip**  
  **Priorität:** P1 · **Größe:** M · **Vertrauen:** hoch

  **Ist:** `AssemblyAnalysisSessionTests.cs:75-107` prüft Manifest-Schlüssel, Schema-/Statusmarker, die neue Session und die Existenz der generierten Dateien. Fresh-vs-Cache-Gleichheit von `DecompiledDocument`-Metadaten wie Typ-Metadatenname, Token, Generated Path und C#-Source wird nicht verglichen; ebenso fehlt der anschließende stabile-ID-/Body-Nachweis auf dem restaurierten Snapshot.

  **Soll:** Ein Cache-Roundtrip-Test muss die semantisch relevanten Dokumentfelder, Origin/Generation und die anschließende Navigation gegen den Fresh-Snapshot vergleichen. Ein veraltetes oder unvollständiges Cache-Manifest muss weiterhin sichtbar invalidiert werden.

  **Auswirkung:** Der bestehende Test kann trotz verlorener Dokumentmetadaten grün bleiben. Das würde die bereits in **E2-BUG-01** registrierte technische Ursache nicht zuverlässig als Regression erfassen und kann spätere Body-/Navigation-Probleme erst außerhalb des Cache-Tests sichtbar machen.

  **Empfehlung / spätere Verifikation:** Nach der technischen E2-Korrektur einen identischen Fresh-vs-Cache-Snapshotvergleich einschließlich Body-Folgeabruf und Generation durchführen; bei absichtlich inkompatiblem Manifest zusätzlich den erwarteten sicheren Neuaufbau prüfen.

  **Abgrenzung / Unsicherheit:** Keine Cache-Datei wurde verändert und kein Test ausgeführt. Der Befund ist ausschließlich die fehlende Assertion; **E2-BUG-01** bleibt die registrierte Ursache.

- **E8-MF-04 – Kritische negative Lebenszeit- und Health-Verträge haben keine zusammenhängende Regression-Matrix**  
  **Priorität:** P1 · **Größe:** L · **Vertrauen:** hoch

  **Ist:** `AssemblyAnalysisSessionTests.cs:22-315` deckt normale Fingerprint-/Generation-, Cancellation-, Größenlimit-, Missing-Reference- und Last-good-Szenarien ab. Registry-, Retirement- und Cleanup-Tests decken Teilpfade ab; `McpServerAssemblyHealthE2ETests.cs:70-120,143-162` prüft für Health im Wesentlichen Erfolgsdaten, Sessionzählung und begrenzte Textmarker. Nicht als zusammenhängender öffentlicher Nachweis belegt sind unter anderem Änderung während Read/Decompilation vor Publish, Dispose-vs-Refresh, Fehler bei Retirement-Cleanup, interne Creation-Cancellation, Health für Fehler/Degraded/Retirement und maschinenlesbare Resource-/Lease-/Operationstelemetrie.

  **Soll:** Für jeden kritischen Lebenszeitübergang müssen alter Snapshot, neuer Generationstand, Publish-Entscheidung, Cancellationsemantik, Cleanup und Health-Projektion gemeinsam prüfbar sein.

  **Auswirkung:** Die bestehenden Tests können einzelne Bausteine bestätigen, ohne die in Epics 4 und 7 registrierten Übergangsrisiken als öffentlich sichtbaren Zustand zu sichern. Besonders **E4-BUG-01** bis **E4-BUG-05**, **E4-MF-02/03**, **E7-BUG-02** und **E7-MF-01** bleiben dadurch ohne gebündelten späteren Nachweis.

  **Empfehlung / spätere Verifikation:** Eine gezielte, fehlerinjizierende Fast-/Integration-Matrix für die genannten Übergänge ergänzen. Health muss Status, Recoverability/Fehlerklasse, Last-good-/Retirement-Zustand sowie Resource-/Lease-/Operationwerte zeigen; die Prüfung muss Text und Structured Content und die Redaction einschließen.

  **Abgrenzung / Unsicherheit:** Die bestehenden Einzeltests wurden nicht erneut bewertet oder ausgeführt. Es wird keine neue technische Ursache behauptet; die bereits registrierten Befunde werden nur mit ihrer fehlenden Verifikationsabdeckung verbunden.

## Evidence und Scope

### Tatsächlich per MCP abgefragt

- Der Server wurde mit `get_server_health({targetType:"project", targetPath:"<absoluter Repo-Root>", includeSessions:true, includeDiagnostics:true, maxSessions:10, maxDiagnostics:10})` geprüft; der Projekt-Key war geladen, es waren keine Assembly-Sessions resident.
- Für die zentralen Einstiegspunkte `AssemblyAnalysisToolRegistrations`, `AssemblyAnalysisDispatcher`, `AssemblyAnalysisSession`, `AssemblyAnalysisRegistry`, `AssemblyAnalysisResponseLimits`, `AssemblyAnalysisHealthSnapshotProvider`, `AssemblyAnalysisService` und `AssemblyAnalysisContextFactory` wurde `get_feature_context` mit `targetType="project"`, absolutem Repo-Root, `includeCallers=true`, `includeTests=true`, `includeMetrics=true`, `includeViolations=true`, `maxCallers=50`, `maxTests=50` verwendet. Die Antworten lokalisierten aktuelle Deklarationen, Caller, Metriken und statische Testzuordnungen; insbesondere wurden 0 Zuordnungen für Registrations, Service, Response Limits und Health Snapshot Provider sowie vorhandene Zuordnungen für Dispatcher, Session, Registry und Context Factory sichtbar.
- Ergänzend wurde `get_test_context` mit aktuellem Schema, `targetType="project"`, absolutem Repo-Root, `symbolIdentifier` und `maxResults=100` für die in **E8-OPT-01** genannten Orchestrierungs-, Response-, Navigation-, Body- und Health-Symbole abgefragt. Assembly-Ziele wurden für diesen project-only Vertrag nicht verwendet.
- Die abschließende MCP-Runde nach der letzten Code-Map-Änderung ist weiter unten separat festgehalten. Assembly-Ziele wurden dort ausschließlich über redigierte Labels adressiert.

### Nur gelesen

- Vollständig gelesen: `AGENTS.md`, relevante `.agents/rules/*.mdc`, `tasks/decompiled-assembly-audit/Konzept.md`, `roadmap.md`, `code-map.md`, `.agents/skills/implement/SKILL.md`, die Epic-1-bis-Epic-7-Berichte sowie die einschlägigen Fast-/Integration-Test-, Implementierungs- und Dokumentationsdateien.
- Ergänzend per `rg` und Dateilesen geprüft: Toolregistrierung, öffentliche Capability-Matrix, MCP-E2E-Tests, direkte Dispatcher-/Route-Tests, Session-/Cache-/Provider-/Health-Tests, `Docs/agent-api.md`, `Docs/integration.md`, `Docs/configuration.md` und `README.md`.
- Keine Builds, keine Tests, keine Testfilter und keine produktiven MCP-Änderungen ausgeführt. Die lokale Prüffall-Datei wurde nur für redigierte MCP-Zielauflösung verwendet; ihre Identitäten und Inhalte erscheinen nicht in diesem Bericht.

## Vertragsabdeckung und konzeptspezifische Prüfung

| Kritischer Vertrag | Aktueller Nachweis | Bewertung / spätere Verifikation |
|---|---|---|
| Metadata-only, keine Runtime-Ausführung, verwaltete `.dll`/`.exe`, Nicht-.NET-Negativfall | Direkte Dispatch-Tests in `AssemblyAnalysisToolTests.cs:23-96` und `ManagedAssemblyBinaryTests.cs:21-75`; öffentlicher Integrationstest nur für die Produkt-Assembly | Bausteine vorhanden, öffentliche Matrix fehlt (**E8-MF-01**). `FALSE-01` später über öffentlichen MCP-Client mit sicherem, recoverable Structured Content wiederholen. |
| Source-backed versus Decompiled, Origin/Trust/ContentMode | Fake-/Component-Nachweise in Context Factory, Tool Support und Host Composition; redigierter `GIT-01`-MCP-Nachweis aus Epic 3 | Öffentliche konfigurierte Klammer fehlt (**E8-MF-02**); **E3-MISSING-02/03** bleiben technische Abgrenzungen. |
| Root-only-Default und explizite Referenznavigation | Direkte Dispatcher-/Route-Tests in `AssemblyAnalysisToolTests.cs:225-274` und `AssemblyAnalysisRouteTests.cs:32-316`; frühere MCP-Spotchecks | Semantik-/Diagnosebefunde aus Epics 1, 3, 5 und 6 nicht erneut registrieren; matrixweiter öffentlicher Regressionstest fehlt (**E8-MF-01**). |
| Stable IDs, Skeleton/Body, Overloads und Cache-Roundtrip | `AssemblyAnalysisPathContractTests.cs:32-208` deckt Methoden/Overloads und einige Folgewerkzeuge ab; Cache-Test prüft Manifest/Dateien | Konstruktor-ID bleibt **E2-BUG-03/E5-BUG-04**; semantischer Cache-Roundtrip fehlt (**E8-MF-03**). |
| Responsebudget, Text/Structured-Parität, Diagnose-/Listen-Truncation | Direkte Responsebudget-Tests in `AssemblyAnalysisToolTests.ResponseBudget.cs:18-139` und Dispatcher-Tests; reale große Payloads in Epic 6 | **E6-BUG-01/02**, **E6-MF-01/02** und zugehörige Optimierungen nicht duplizieren; spätere öffentliche Matrix muss kombinierte Budgets und Parität als Regression sichern. |
| Navigation, Struktur, Metriken und Health | Direkte Route-/Path-Tests für einen Teil der Folgewerkzeuge; Health-E2E für Erfolg, Sessioncount und begrenzte Schemafelder | Keine vollständige öffentliche Assembly-Matrix (**E8-MF-01**); Health-/Lebenszeitlücken **E8-MF-04** und **E7-MF-01**. |
| Redaction von Pfaden, Exceptions, Diagnosen und URLs | Aktuelle Implementierung/Tests gelesen; der Negativtest erwartet noch Rohkontext | Test-Orakel widerspricht dem Sicherheitsziel (**E8-BUG-01**); technische Ursache **E7-BUG-01**. |
| Statische Testzuordnung / MCP-Discoverability | `get_feature_context`/`get_test_context` für Kernhilfen sowie `@covers`-Kommentare gelesen | Indirekte Abdeckung wird nicht ausgewiesen (**E8-OPT-01**). |

Die konzeptspezifischen Redaktionsanforderungen wurden eingehalten: Im Bericht stehen nur die Prüffall-Labels `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01`; es werden keine konkreten externen Assembly-Namen, Namespaces, externen Pfade, URLs, Hashes oder dekompilierten Inhalte wiedergegeben. Die internen Repository-, Code-, Test- und Dokumentationsstellen bleiben als Nachweis konkret benannt.

## Abschlussrunde nach letzter Code-Map-Änderung

Nach der letzten Änderung an `tasks/decompiled-assembly-audit/code-map.md` wurden keine weiteren Dateien außer diesem Bericht ergänzt. Danach erfolgte eine gezielte, read-only MCP-Runde:

- `get_test_context` erneut mit `targetType="project"`, absolutem Repo-Root, `symbolIdentifier` und `maxResults=100` für `AssemblyAnalysisToolRegistrations`, `AssemblyAnalysisService`, `AssemblyAnalysisResponseLimits`, `AssemblyAnalysisHealthSnapshotProvider`, `AssemblyNavigationSupport`, `AssemblySymbolSearch`, `AssemblyReferenceNavigator`, `AssemblyDecompiledBodyResolver` und `AssemblyHealthProjection`: bei allen neun Symbolen weiterhin `isUntested=true`/keine statische Testdatei; das bestätigt **E8-OPT-01** gegen den finalen Map-Stand.
- `get_feature_context` erneut für `AssemblyAnalysisService`, `AssemblyAnalysisToolRegistrations` und `AssemblyAnalysisResponseLimits` mit `targetType="project"`, absolutem Repo-Root, allen fünf Dimensionen sowie `maxCallers=50`, `maxTests=50`: aktuelle Deklarationen und Consumer blieben sichtbar; direkte statische Testzuordnung für Service/Registrierung/Response-Limits blieb leer.
- `inspect_assembly` für `LOCAL-03` mit `targetType="assembly"`, absolutem, nur labelreferenziertem Matrixpfad, `includeReferences=false`, `publicOnly=true`, `maxResults=1`, `maxMembers=1`: `isError=false`, `completeness=partial`, `truncated=true`, `sessionStatus=partial`, `totalTypes=380`, `shownCount=1`, `analysis.contentMode=decompiledSignatureOnly`, `bodyAvailability=onDemand`, `confidence=medium`, `trust=untrusted`. Der parallele `find_assembly_extensions`-Spotcheck mit `maxResults=1` blieb ebenfalls `isError=false`, `partial`, `truncated`, `decompiledSignatureOnly` und `onDemand`; `get_server_health` mit `includeSessions=true`, `includeDiagnostics=true`, `maxSessions=1`, `maxDiagnostics=5` blieb `isError=false` und zeigte eine Assembly-Sicht.
- `inspect_assembly` und `get_server_health` für `FALSE-01` mit `targetType="assembly"` und absolutem, nur labelreferenziertem Matrixpfad, begrenzten Detailwerten: beide blieben `isError=false`, `recoverable=true`, `code=WORKSPACE_DIAGNOSTIC`; ein analysierbarer Assembly-Snapshot wurde nicht ausgewiesen. Konkrete Fehleridentitäten wurden nicht übernommen.

Diese letzte Runde wurde nach der Map-Änderung ausgeführt; anschließend wurde nur noch dieser Bericht vervollständigt. Sie ersetzt keine Testausführung.

## Hand-off

- **Geändert:** ausschließlich `tasks/decompiled-assembly-audit/epic-08-test-dokumentationsnachweis.md` und `tasks/decompiled-assembly-audit/code-map.md`.
- **Nicht geändert:** Produktionscode, Tests, Konfiguration, Produktdokumentation, Roadmap, externe Prüffall-Datei und Git-Historie.
- **Entscheidung:** E8 registriert einen widersprüchlichen Redaction-Testoracle-Befund, eine statische Traceability-Optimierung und vier eigenständige Nachweislücken; technische Ursachen aus Epics 1–7 sind nur referenziert.
- **Verifikation:** Dateien, aktuelle Code-/Doku-Stellen und priorisierte MCP-Abfragen wurden read-only geprüft; nach der Map-Änderung wurden die gezielten Testkontext-, Featurekontext-, `inspect_assembly`- und Health-Abfragen wiederholt. Keine Builds oder Tests ausgeführt.
- **Offene nächste Schritte:** Erst technische Korrekturen aus den früheren Epics priorisieren, danach die beschriebenen öffentlichen Matrix-, Source-, Cache-, Lebenszeit- und Redaction-Regressionen ergänzen und ausführen. Die Labels bleiben die einzige zulässige Referenz auf externe Prüffälle.
