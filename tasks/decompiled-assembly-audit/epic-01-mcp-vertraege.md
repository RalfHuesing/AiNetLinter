# Epic 1 — Öffentliche MCP-Verträge und Discoverability

## Evidence-/Scope-Abschnitt

Dieser Bericht behandelt ausschließlich die Assembly-Unterstützung der beiden öffentlichen MCP-Werkzeuge inspect_assembly und find_assembly_extensions: Registrierung, aktuelle Tool-Schemas, Annotationen, Parameterdefaults, Capability-/Target-Abgrenzung, progressive Disclosure, Response-Metadaten und Dokumentationskonsistenz.

Die Prüfung ist eine Diagnose- und Berichtsprüfung. Produktionscode, Tests, Konfiguration und Produktdokumentation wurden nicht geändert. Es wurden keine Builds oder Tests ausgeführt. Die lokalen Prüffälle werden ausschließlich über die opaken Labels GIT-01, LOCAL-01, LOCAL-02, LOCAL-03 und FALSE-01 referenziert. Konkrete externe Assembly-Identitäten, externe Namespaces, konkrete externe Zielpfade, URLs und dekompilierte Inhalte sind aus diesem Bericht ausgeschlossen.

Als Primärnachweis wurden die aktuellen AiNetLinter-MCP-Abfragen mit targetType und absolutem targetPath verwendet. Textstellen und Dokumentationsaussagen wurden anschließend lesend über die Repository-Dateien abgeglichen. Die Zeilenangaben beziehen sich auf den bei der Prüfung gelesenen Arbeitsstand.

### Verifizierter öffentlicher Vertrag

| Werkzeug | Registrierung und Target | Aktuelle sichtbare Parameter | Response-/Capability-Signale |
|---|---|---|---|
| inspect_assembly | AssemblyAnalysisToolRegistrations.Register, Zeilen 27–87; targetType="assembly", absoluter targetPath auf .dll/.exe; read-only | namespace, typeName, memberName, publicOnly=true, maxResults aus AssemblyAnalysisService.DefaultMaxResults, exactTypeName=false, memberNames, maxMembers aus DefaultMaxMembers, includeReferences=null | Herkunft, Snapshot, Vertrauen, Generation, Status, Vollständigkeit, Fallback, Body-Verfügbarkeit, Content-Modus und Diagnosezusammenfassung; Referenz-/Diagnose-/Member-/Typ-Budgets |
| find_assembly_extensions | AssemblyAnalysisToolRegistrations.Register, Zeilen 89–131; gleicher Assembly-Target-Vertrag; read-only | receiverType, extensionName, namespace, maxResults=100; keine öffentliche includeReferences-Option | Herkunft, Snapshot, Vertrauen, Generation, Status, Vollständigkeit, Fallback, Diagnosezusammenfassung, Consumer-/Applicability-Signale und Referenzzusammenfassung |

Die gemeinsame Registrierung erfolgt einmalig über McpServerToolCollectionFactory.Create, src/AiNetLinter/Mcp/Composition/McpServerToolCollectionFactory.cs:20. AssemblyTool in src/AiNetLinter/Mcp/Tools/McpToolRegistrationOptions.cs:47-52 kennzeichnet beide Werkzeuge mit dem Read-only-Profil und ergänzt den Assembly-Target-Vertrag in der Beschreibung. Die Laufzeitvalidierung liegt in AnalysisTargetResolver und trennt project und assembly explizit.

## Findings — Bug

### E1-BUG-01 — find_assembly_extensions expandiert Referenzen ohne öffentlichen Schalter

- Priorität: P2
- Vertrauen: hoch
- Größe: M
- Betroffen: find_assembly_extensions; Registrierung in src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:89-115; gemeinsame Dispatch-Logik in AnalysisToolCall.cs.

**Beobachtet.** Die öffentliche Lambda-Signatur enthält bei find_assembly_extensions nur targetType, targetPath, die drei Filter und maxResults; includeReferences fehlt in Zeile 93–99. Der Dispatchwert wird in Zeile 113 trotzdem fest auf ExpandAssemblyReferences: true gesetzt. Damit ist die Referenzexpansion für dieses Werkzeug weder abschaltbar noch über den öffentlichen MCP-Vertrag discoverable.

Die aktuelle Dokumentation behauptet in Docs/agent-api.md:320-348 und Docs/agent-api.md:477-482, dass Referenzexpansion explizit über includeReferences=true angefordert wird. Die Werkzeugtabelle in Docs/agent-api.md:355-360 listet für find_assembly_extensions keinen solchen Parameter.

**MCP-Nachweis.** find_assembly_extensions wurde mit targetType="assembly", absolutem targetPath des opaken Prüffalls LOCAL-03, maxResults=5 und ohne Filter/Referenzparameter aufgerufen. Die Antwort zeigte Referenz- und Session-Zusammenfassungen sowie eine partielle, wegen maxResults und responseBudget gekürzte Antwort. Das belegt die Expansion auf dem öffentlichen Default-Pfad; es belegt nicht, dass jede einzelne Referenz vollständig materialisiert wurde.

**Soll-Ist.** Soll ist entweder ein expliziter, im Schema sichtbarer includeReferences-Parameter mit dokumentiertem Default oder eine werkzeugspezifische Dokumentation, die die unvermeidbare Expansion klar als Vertrag ausweist. Ist ist ein verstecktes, immer aktiviertes Verhalten bei gleichzeitigem Progressive-Disclosure-Vertrag für explizite Expansion.

**Auswirkung.** Ungeplante Referenz- und Session-Arbeit erhöht Latenz und Budgetdruck und kann bei kleinen maxResults-Werten zu einer Antwort führen, deren Nutzdaten wegen des globalen Budgets stark gekürzt sind. Clients können die Kosten nicht durch einen MCP-Parameter kontrollieren und können aus der Dokumentation keinen stabilen Default ableiten.

**Empfehlung.** Einen öffentlichen includeReferences-Parameter mit false als Root-Default ergänzen und die bestehende Dispatch-Entscheidung darauf umstellen. Falls die fachliche Extension-Suche zwingend über Referenzen laufen muss, sollte das als bewusstes capability-spezifisches Verhalten im Schema und in der Dokumentation bezeichnet und durch einen separaten, expliziten Referenzmodus ergänzt werden.

**Abgrenzung und Unsicherheit.** Die Findings bewerten den öffentlichen Vertrag, nicht die interne Notwendigkeit der Referenzsuche. Ob die Extension-Suche fachlich ohne Referenzexpansion vollständig ist, wurde in Epic 1 nicht untersucht. Das inspect_assembly-Verhalten ist hiervon getrennt und wird als Optimierung erfasst.

### E1-BUG-02 — Dokumentiertes Response-Budget weicht vom implementierten Budget ab

- Priorität: P2
- Vertrauen: hoch
- Größe: S
- Betroffen: beide Assembly-Werkzeuge; Limits in src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponseLimits.cs:12-19; Dokumentation in Docs/agent-api.md:397-435 und Docs/configuration.md:32-35.

**Beobachtet.** Der aktuelle Code setzt AssemblyAnalysisResponseLimits.MaxResponseBytes auf 8 * 1024. Die Dokumentation beschreibt an den genannten Stellen weiterhin ein hartes Response-Budget von 4 KiB. Die Budgetprüfung in AssemblyAnalysisResponseLimits.Budget.cs:120-128 bewertet sowohl Text- als auch Structured-Content nach Anreicherung und trimmt bei Bedarf weiter.

**MCP-Nachweis.** Die Aufrufe von inspect_assembly und find_assembly_extensions mit targetType="assembly", absolutem matrixaufgelöstem targetPath, kleinen maxResults-/maxMembers-Werten und includeReferences=false bzw. ohne diesen Parameter lieferten sichtbare truncated-/completeness-Signale. Die Extension-Antwort meldete zusätzlich responseBudget als Kürzungsursache. Die Laufzeitantworten sind damit budgetbewusst, lösen aber die dokumentierte Größenabweichung nicht auf.

**Soll-Ist.** Soll ist ein einziger konsistenter Budgetwert in Code, Structured-Metadaten und Dokumentation. Ist sind 8 KiB im Code gegenüber 4 KiB in den öffentlichen Dokumenten.

**Auswirkung.** Integratoren können Puffer, Transportannahmen und Trunkierungsgrenzen falsch dimensionieren. Die Abweichung erschwert außerdem die Interpretation von completeness und truncated.

**Empfehlung.** Eine autoritative Konstante bzw. generierte Schema-/Dokument-Quelle festlegen und Docs/agent-api.md sowie Docs/configuration.md an den tatsächlichen Vertrag angleichen; alternativ den Code bewusst auf den dokumentierten Wert zurückführen. Die gewählte Grenze muss in der Response weiterhin maschinenlesbar erkennbar bleiben.

**Abgrenzung und Unsicherheit.** Es wurde nicht bewertet, ob das größere Budget beabsichtigt ist oder durch historische Dokumentationsdrift entstand. Es wurden keine Tests ausgeführt, daher ist dies ein statischer und MCP-beobachteter Vertragsbefund.

### E1-BUG-03 — README beschreibt die Assembly-Fähigkeit unvollständig

- Priorität: P3
- Vertrauen: hoch
- Größe: S
- Betroffen: Assembly-Discoverability in README.md:19,28-42.

**Beobachtet.** Die öffentliche README-Einstiegserklärung beschreibt die Fremdassembly-Nutzung nur über .dll, während Registrierung und McpToolRegistrationOptions sowohl .dll als auch .exe als zulässige Assembly-Targets ausweisen. Docs/agent-api.md:308-318 und Docs/agent-api.md:443-475 bestätigen denselben .dll/.exe-Vertrag.

**Soll-Ist.** Soll ist eine einheitliche Aussage über beide unterstützten Dateiendungen und den metadata-only/read-only Charakter. Ist ist eine verkürzte README-Discoverability, die gültige .exe-Targets nicht sichtbar macht.

**Auswirkung.** Nutzer und nachgelagerte Agenten können .exe-Ziele als nicht unterstützt annehmen oder die Capability-Entscheidung unnötig auf .dll begrenzen.

**Empfehlung.** Die README-Aussage auf die beiden im öffentlichen Assembly-Target-Vertrag genannten Dateiendungen und den metadata-only-Modus präzisieren.

**Abgrenzung und Unsicherheit.** Der Befund betrifft nur Discoverability; die Laufzeitannahme und das Resolver-Verhalten wurden separat über MCP geprüft.

Keine weiteren belastbaren Bugs innerhalb des Epic-1-Scope wurden aus den verfügbaren Nachweisen abgeleitet. Annotationen, read-only-Profil, targetType-/targetPath-Pflicht, recoverable Invalid-Argument-Responses und die sichtbaren Response-Metadaten waren konsistent.

## Findings — Optimierung

### E1-OPT-01 — Ungefilterter inspect_assembly-Default expandiert Referenzen

- Priorität: P2
- Vertrauen: hoch
- Größe: M
- Betroffen: inspect_assembly; Dispatchentscheidung in src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:61-64; Progressive Disclosure in Docs/agent-api.md:477-482.

**Beobachtet.** includeReferences ist nullable und wird für inspect_assembly auf true gesetzt, wenn kein Typ- oder Memberfilter angegeben ist. Ein ungefilterter MCP-Aufruf mit targetType="assembly", absolutem targetPath des opaken Prüffalls LOCAL-01, maxResults=1, maxMembers=1 und ohne explizites includeReferences zeigte Referenz- und Session-Metadaten. Ein anschließender Typfilter-Aufruf mit ansonsten kleinem Budget blieb dagegen im Root-Kontext.

**Soll-Ist.** Der allgemeine Progressive-Disclosure-Text empfiehlt kleine, gefilterte Root-Abfragen und explizite Referenzexpansion. Ist ist ein kontextabhängiger Default, der bei der breitesten Abfrage automatisch expandiert; der Default ist zwar im Code und teilweise in der Dokumentation erkennbar, aber nicht als klarer Schemawert discoverable.

**Auswirkung.** Die breiteste, zugleich naheliegende Einstiegsabfrage kann unnötige Referenzsessions eröffnen und den Response-Budgetdruck erhöhen, obwohl nur ein Ergebnis und ein Member angefordert wurden.

**Empfehlung.** Für Progressive Disclosure includeReferences=false als stabilen Root-Default erwägen und die Expansion ausschließlich explizit anfordern. Falls der kontextabhängige Default beibehalten wird, sollte die Beschreibung die genaue Entscheidungstabelle und den Kosten-/Trunkierungseffekt sichtbar machen.

**Abgrenzung und Unsicherheit.** Dies ist als Optimierung und nicht als Bug eingeordnet, weil die aktuelle Dokumentation einen kontextabhängigen Default erwähnt und die Antwort ihre Referenz-/Vollständigkeitssignale ausweist. Eine fachliche Vollständigkeitsanforderung für ungefilterte Assembly-Inspektion wurde in diesem Epic nicht festgelegt.

Keine weiteren belastbaren Optimierungen mit eigener öffentlicher Vertragswirkung wurden von den vorliegenden Nachweisen getrennt; insbesondere wurde die korrekte Annotation nicht als Optimierung umgedeutet.

## Findings — Missing Feature

### E1-MISSING-01 — Maschinenlesbare Assembly-Capability im Tool-Schema fehlt

- Priorität: P2
- Vertrauen: mittel
- Größe: S
- Betroffen: AssemblyTool in src/AiNetLinter/Mcp/Tools/McpToolRegistrationOptions.cs:47-52; beide Registrierungen in src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:27-131.

**Beobachtet.** Die aktuellen callable MCP-Schemas exponieren targetType und targetPath als Strings. AssemblyTool ergänzt den Assembly-Vertrag als Beschreibungstext und das Read-only-Annotierungsprofil, aber keinen maschinenlesbaren Enumwert für targetType="assembly" und keine separate Schemaeinschränkung für den absoluten .dll/.exe-Targetpfad. Die Laufzeit prüft die Capability anschließend in AnalysisTargetResolver; project-Targets werden recoverable abgewiesen und ein ungültiger Assemblypfad liefert INVALID_ARGUMENT.

**MCP-Nachweis.** Für beide Werkzeuge wurden die aktuellen Tool-Metadaten über die verfügbare Tooloberfläche geprüft: targetType:string und targetPath:string sind Pflichtfelder, während die Assembly-Einschränkung in Beschreibungstext und Laufzeitfehlern liegt. Ein direkter, unverfälschter tools/list-MCP-Aufruf war in der verfügbaren Tooloberfläche nicht als separates Callable verfügbar; deshalb bleibt die Roh-JSON-Schemaform ein mittleres Unsicherheitsmoment.

**Soll-Ist.** Soll ist, dass Discovery-Clients die Assembly-Capability vor einem Call erkennen und targetType maschinenlesbar auf assembly einschränken oder mindestens priorisieren können. Ist ist eine nachgelagerte Stringvalidierung mit prosebasierter Capability-Hilfe.

**Auswirkung.** Generische MCP-Clients können vor dem Aufruf keine verlässliche Target- und Capability-Auswahl aus dem Schema ableiten und müssen einen recoverable Fehler als Discovery-Schritt einplanen.

**Empfehlung.** Die öffentliche Schemaerzeugung um einen Assembly-Capability-Hinweis ergänzen, vorzugsweise mit einem expliziten targetType-Enumwert assembly und klarer Beschreibung für den absoluten .dll/.exe-Pfad. Die Existenzprüfung und metadata-only-Sicherheitsgrenze bleiben sinnvollerweise Laufzeitvalidierung.

**Abgrenzung und Unsicherheit.** Das Finding verlangt keine Änderung des gemeinsamen project-Vertrags und keine Änderung an Runtime-Sicherheitschecks. Die fehlende Roh-tools/list-Sicht verhindert eine höhere Sicherheit für die exakte JSON-Schema-Ausprägung; die callable Signatur und die Registrierungsimplementierung sind jedoch direkt belegt.

Keine weiteren belastbaren Missing Features im Epic-1-Scope wurden aus den Nachweisen abgeleitet. Response-Metadaten für Herkunft, Trust, Status, Completeness, Fallback und Trunkierung sind bereits vorhanden.

## Positiv verifiziert / Disposition ohne Finding

- Die Registrierung wird über McpServerToolCollectionFactory.Create genau einmal an die Sammlung angebunden; find_references auf AssemblyAnalysisToolRegistrations.Register lieferte den Factory-Aufrufer ohne Trunkierung.
- Beide Werkzeuge sind im aktuellen Read-only-Annotierungsprofil sichtbar: read-only true, keine Schreibfähigkeit, keine Netzwerkanforderung und keine Ausführung des Zielcodes.
- AnalysisTargetResolver verlangt targetType und targetPath, akzeptiert für Assembly-Targets nur absolute vorhandene .dll/.exe-Pfade und trennt project von assembly.
- inspect_assembly und find_assembly_extensions liefern strukturierte und textuelle Response-Signale für origin, snapshot, confidence, trust, generation, sessionStatus, completeness, fallbackReason, bodyAvailability, contentMode, Diagnosen und Trunkierung. Die Analysis-Metadaten werden in src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:17-151 budgetbewusst ergänzt.
- Die drei verwalteten lokalen Prüffälle lieferten recoverable, partielle bzw. dekompilierte, signaturorientierte Antworten mit sichtbaren Diagnosen und Vertrauenssignalen; FALSE-01 lieferte einen recoverable Workspace-Diagnostic-Pfad ohne Ausführung. Die konkreten Inhalte bleiben absichtlich außerhalb dieses Berichts.

## Tatsächliche MCP-Evidenzabfragen

Alle projektgebundenen Abfragen verwendeten als Scope projectRoot=C:\Daten\Entwicklung\Ralf\AiNetLinter und ein absolutes targetPath, soweit das jeweilige MCP-Schema ein Target verlangte. Die folgenden Assembly-Targets sind nur über ihr opakes Label und den Typ targetType="assembly" bezeichnet; die absoluten Matrixpfade werden nicht reproduziert.

1. get_feature_context auf AssemblyAnalysisToolRegistrations, mit includeCallers=true, includeTests=true, includeMetrics=true, includeViolations=true, maxCallers=50, maxTests=50: interner Typ und ein Factory-Aufrufer, keine Violations, keine statische Testzuordnung.
2. get_symbol_body auf den Typ AssemblyAnalysisToolRegistrations, maxBodyLines=200: vollständige Registrierung beider Werkzeuge, Defaults und Dispatchwerte erhalten.
3. Parallele get_feature_context-Abfragen auf McpServerToolCollectionFactory, McpToolRegistrationOptions, InspectAssemblyTool, FindAssemblyExtensionsTool, AssemblyAnalysisService, AssemblyAnalysisResponseLimits, InspectAssemblyResponseBuilder und FindAssemblyExtensionsResponseBuilder, jeweils mit allen vier Include-Flags und maxCallers=20, maxTests=20: relevante Dateien, Aufrufer/Testzuordnungen und keine Violations.
4. Batch-get_symbol_body auf der AssemblyTool-Methode und der Factory, maxBodyLines=160: Factory erhalten; der zunächst verwendete nichtkanonische Methoden-Deskriptor war nicht auflösbar.
5. find_symbol mit namePatterns=["AssemblyTool","ReadOnly","ToolAnnotation"], includeReferences=false, maxResults=50: Annotation- und Registrierungs-Symbole gefunden.
6. Batch-get_symbol_body auf der AssemblyTool-Methode und einem dateizeilenbezogenen Kandidaten, maxBodyLines=120: Methode aufgelöst; der dateizeilenbezogene Kandidat blieb wegen Überladung mehrdeutig.
7. get_symbol_body auf McpToolRegistrationOptions, maxBodyLines=140: Target-Verträge, Read-only-Werte und Assembly-Tool-Erzeugung vollständig erhalten.
8. Batch-get_symbol_body auf InspectAssemblyPayload, FindAssemblyExtensionsPayload und AssemblyAnalysisResponseLimits, maxBodyLines=420: Response-Felder, Defaults, Referenz-/Diagnose-/Budget-Grenzen und Trimmreihenfolge erhalten.
9. get_feature_context auf AssemblyAnalysisResponse, alle Include-Flags, maxCallers=30: sechs Aufrufer, keine Violations.
10. get_symbol_body auf AssemblyAnalysisResponse, maxBodyLines=220: vollständige Enrichment-, Textheader- und Structured-Metadata-Logik.
11. Batch-get_symbol_body auf beiden Assembly-Tooltypen und den Dispatch-/Response-Builders, maxBodyLines=260: Toolrouting und Responseprojektion erhalten.
12. get_feature_context auf AssemblyAnalysisToolSupport, alle Include-Flags, maxCallers=50, maxTests=50: Inputvalidierung, Aufrufer und Testzuordnungen.
13. get_symbol_body auf AssemblyAnalysisToolSupport, maxBodyLines=140: Input-, Workspace- und Leasemodell.
14. get_feature_context auf AnalysisToolCall, alle Include-Flags, maxCallers=50, maxTests=50: Routing-Aufrufer.
15. get_symbol_body auf AnalysisToolCall, maxBodyLines=280: Routing- und Enrichment-Pfad; die Mehrtyp-Ausgabe war auf den angeforderten statischen Ausschnitt begrenzt.
16. get_feature_context auf AnalysisTargetResolver, alle Include-Flags, maxCallers=40, maxTests=40: Resolverdatei, Aufrufer und Tests.
17. get_symbol_body auf AnalysisTargetResolver, maxBodyLines=150: vollständige Target- und Dateiendungsvalidierung.
18. find_references auf AssemblyAnalysisToolRegistrations.Register, includeReferences=false, maxResults=50, depth=1: ein Factory-Aufrufer, vollständig und nicht trunkierter Befund.
19. get_server_health ohne Target, mit includeSessions=true, includeDiagnostics=true, maxSessions=10, maxDiagnostics=10: Daemon gesund; Assembly-Sessions und begrenzte Diagnosen sichtbar.
20. Parallele inspect_assembly-Aufrufe für LOCAL-01, LOCAL-02, LOCAL-03 und FALSE-01, jeweils targetType="assembly", absoluter matrixaufgelöster targetPath, maxResults=5, maxMembers=5, publicOnly=true, includeReferences=false: drei recoverable partielle dekompilierte Antworten mit sichtbaren Metadaten; ein recoverable Workspace-Diagnostic ohne .NET-Metadaten.
21. Parallele find_assembly_extensions-Aufrufe für LOCAL-01, LOCAL-03 und FALSE-01, jeweils targetType="assembly", absoluter matrixaufgelöster targetPath, maxResults=5, ohne Filter: partielle/gekürzte Antwort mit not_decidable-Applicability bei fehlendem Consumer-Kontext für die verwalteten Fälle; recoverable Diagnostic für FALSE-01.
22. Ungefiltertes inspect_assembly für LOCAL-01, targetType="assembly", absoluter matrixaufgelöster targetPath, maxResults=1, maxMembers=1, publicOnly=true, includeReferences nicht gesetzt: Referenzexpansion im Default-Kontext sichtbar.
23. Typgefiltertes inspect_assembly für LOCAL-01 mit denselben Grenzen, zusätzlich typeName gesetzt und includeReferences nicht gesetzt: Root-Kontext ohne sichtbare Referenzdetails; Antwort partial/truncated.
24. inspect_assembly und find_assembly_extensions mit targetType="assembly" und absolutem, nicht vorhandenem targetPath: recoverable INVALID_ARGUMENT mit targetPath-Hinweis.
25. inspect_assembly und find_assembly_extensions mit targetType="project" und absolutem Projektziel: recoverable INVALID_ARGUMENT, Assembly-only-Capability explizit ausgewiesen.

Die MCP-Antworten wurden nicht als vollständige Assembly- oder Referenzinventare interpretiert. maxResults-, maxMembers-, Diagnose-, Referenz- und Session-Budgets sowie completeness/truncated wurden bei der Bewertung berücksichtigt.

## Nur gelesene Nachweise und offene Dispositionen

Gelesen wurden die Registrierungs-, Options-, Resolver-, Dispatch-, Response- und Budgetdateien, die Assembly-relevanten Fast-/Integration-Testbereiche sowie Docs/agent-api.md, Docs/configuration.md, Docs/mcp-bootstrap.md und README.md. Die lokale Prüffall-Matrix wurde nur zur Zuordnung der opaken Labels und zur Ausführung absoluter Target-Abfragen verwendet; ihre Identitäten und Inhalte sind nicht Teil dieses Berichts.

Offen bleiben die fachliche Entscheidung, ob Extension-Suche zwingend Referenzexpansion benötigt, die gewünschte autoritative Response-Budgetgröße und die exakte Roh-JSON-Schemaform von tools/list. Diese Punkte sind als Dispositionen markiert und wurden nicht durch Code-, Test-, Konfigurations- oder Dokumentationsänderungen vorweggenommen.

