# Kritikerbefunde — dekompilierte Assembly-Analyse

Datum: 2026-08-31
Status: Prüfung auf Nutzeranweisung gestoppt; keine Produktions-Codeänderung durch die Kritiker.

## Gesamtbeurteilung

Die Cross-Assembly-Kernfunktion aus EPIC-D ist im Code vorhanden: Der positive
Multi-DLL-Test deckt Root plus zwei Referenz-DLLs für `find_symbol`,
`find_references` und `get_call_tree` ab; Herkunft, Begrenzung und partielle
Antworten sind grundsätzlich modelliert. Die Gesamtintention ist damit aber
nicht vollständig als „voll transparent genauso wie Quellcode“ abgesichert.

Es bleiben vier konkrete P1-Befunde. Zusätzlich fehlt der geforderte Live-
Nachweis gegen den tatsächlich registrierten MCP-Server: In der Shell war kein
`ainetlinter`-Befehl auffindbar; `.mcp.json` verwendet nur den source-lokalen
Fallback `dotnet run`. Ein MCP-Handshake mit `tools/list` und realen
`targetType=assembly`-Aufrufen wurde nach der Nutzerunterbrechung nicht mehr
ausgeführt. Daraus folgt kein Beweis, dass eine externe Installation veraltet
ist, aber die produktive Live-Parität ist ungeprüft und für die Abnahme offen.

Die bewusste Anpassung in `rules.json` wurde nicht verändert.

## Befunde

### P1 — ASSEMBLY-DEFAULT-EXPANSION

- **Beurteilung:** Der explizite Opt-in-Vertrag `includeReferences=false` ist
  auf Dispatcher-Ebene nicht vollständig eingehalten. Jede Assembly-Route
  ruft in `AnalysisToolCall.cs:170` zunächst `ExpandReferencesAsync()` auf,
  bevor der Handler ausgeführt wird. Damit werden Referenz-Sessions auch im
  Default-Pfad materialisiert; ihre Diagnosen fließen über
  `AssemblyAnalysisResponse.cs:18-21` in Status und Completeness ein.
- **Warum relevant:** Der Konzeptvertrag verlangt, dass der bisherige
  Standardpfad unverändert bleibt und Referenznavigation ausdrücklich
  angefordert wird. Ungewollte Expansion verändert Laufzeit, Ressourcenbedarf
  und ggf. den Antwortstatus einer normalen Einzel-Assembly-Abfrage.
- **Reproduktion:** Eine unterstützte Assembly-Route mit
  `includeReferences=false` und mindestens einer nicht auflösbaren oder
  fehlerhaften Referenz aufrufen; anschließend zeigen Session-/Expansion-
  Diagnosen eine Partialität, obwohl keine Referenznavigation angefordert war.
- **Evidenz:** `src/AiNetLinter/Mcp/AnalysisToolCall.cs:167-172`,
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:17-33`.
- **Konfidenz:** hoch.
- **Umfangsschätzung:** klein bis mittel — Dispatcher-/Route-Vertrag und
  Regressionen für `false` versus `true`; keine neue Resolver-Architektur nötig.
- **Empfehlung:** Expansion nur im expliziten Assembly-Navigationspfad
  ausführen oder den Dispatcher abhängig vom normalisierten Opt-in-Argument
  verzweigen; danach Default- und Opt-in-Fälle mit fehlerhafter Referenz
  testen.

### P1 — ASSEMBLY-NAME-RESOLUTION-FALSE-PARTIAL

- **Beurteilung:** Eine qualifizierte Suche nach einem Typ, der ausschließlich
  in einer Referenz-DLL existiert, kann als `partial` markiert werden, obwohl
  der Typ gefunden wurde. `AssemblySymbolResolver` versucht die Suche über
  alle Leases; erwartete `SymbolNotFound`-Ergebnisse anderer Leases werden als
  Diagnosen gesammelt. `AssemblyNavigationSupport.CreateSummary` leitet aus
  jedem solchen Diagnoseeintrag `completeness=partial` ab.
- **Warum relevant:** Ein erfolgreicher Cross-Assembly-Treffer wird dadurch
  semantisch wie ein unvollständiges Analyseergebnis dargestellt. Das ist für
  Agenten irreführend und verletzt den Vertrag „Partialität nur bei echter
  Einschränkung/Fehlerdiagnose“.
- **Reproduktion:** Mit `includeReferences=true` einen Assembly-identifizierten
  oder qualifizierten Typnamen suchen, der nur in einer der geladenen
  Referenz-Sessions definiert ist; die übrigen Leases liefern erwartbar keinen
  Treffer und machen die Gesamtsummary trotzdem partial.
- **Evidenz:**
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblySymbolResolver.cs:30-55`,
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyNavigationSupport.cs:41-55`.
- **Konfidenz:** hoch.
- **Umfangsschätzung:** klein — erwartete „nicht gefunden“-Ergebnisse von
  echten Lease-/Decompilerfehlern trennen und eine gezielte Regression ergänzen.
- **Empfehlung:** Bei einer Mehr-Lease-Suche normale Nichttreffer nicht als
  globale Diagnose werten; echte Lade-, Parse- oder Auflösungsfehler weiterhin
  als Partialität erhalten.

### P1 — DIAGNOSTICS-SAMPLE-BUDGET / GLOBAL-WIRE-BUDGET

- **Beurteilung:** Das 4-KiB-Limit wird in `ProjectDiagnostics` für die
  gemeinsame Auswahl einer Sample-Liste angewandt, nicht für den vollständigen
  serialisierten MCP-Payload. Dieselben Samples erscheinen anschließend
  mehrfach: im Payload-Feld `diagnostics`, in
  `diagnosticsSummary.samples`, in `root`/`transitive.samples` und bei
  Health-Antworten zusätzlich je Assembly. Die interne Begrenzung ist deshalb
  kein globales Wire-Budget.
- **Warum relevant:** Das Konzept fordert begrenzte Diagnostics mit einem
  festgelegten Payload-Budget. Mehrfache Serialisierung kann die erwartete
  Antwortgröße deutlich überschreiten und widerspricht dem belastbaren
  externen Vertrag.
- **Reproduktion:** Bis zu 15 maximal lange Diagnostics erzeugen und eine
  Assembly-Inspect- bzw. Health-Antwort serialisieren. Die Sample-Auswahl wird
  zwar je Liste auf 4 KiB begrenzt, die mehrfach ausgegebenen Listen summieren
  sich jedoch bereits vor JSON-Overhead auf deutlich mehr als 4 KiB.
- **Evidenz:** `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:12-18, 32-52, 204-230`,
  `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs:73-100`,
  `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:119-134`,
  `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:15-53`;
  bereits als `TD-EPIC-B-005`/`TD-EPIC-B-010` mit ausgeschöpften drei
  Korrekturversuchen dokumentiert.
- **Konfidenz:** hoch.
- **Umfangsschätzung:** mittel — kanonische Wire-Sample-Liste oder globales
  Budget über den fertig serialisierten Payload festlegen, anschließend
  StructuredContent- und Health-Regressionen nachziehen.
- **Empfehlung:** Keine weitere blinde Korrekturschleife. Zuerst die
  Wire-Form festlegen; danach Budgetberechnung und Tests auf genau diese Form
  ausrichten.

### P1 — MCP-LIVE-BINARY-NOT-VERIFIED

- **Beurteilung:** Die für die eigentliche Nutzerintention maßgebliche
  produktive MCP-Live-Strecke wurde nicht verifiziert. `Get-Command
  ainetlinter` fand keinen installierten Befehl. Die lokale Registrierung in
  `.mcp.json` startet dagegen `dotnet run --project
  src/AiNetLinter/AiNetLinter.csproj -- --mcp-server` und beweist keine
  Übereinstimmung mit einer extern registrierten/stale DLL oder einem bereits
  laufenden MCP-Prozess.
- **Warum relevant:** „DLL bzw. DLL + Git-Repo kann mit den MCP-Server-
  Funktionen transparent analysiert werden“ ist eine Laufzeit-/Deployment-
  Zusage, nicht nur eine Quellcode- oder Unit-Test-Zusage. Ohne Handshake,
  `tools/list`, Schema-Prüfung und echte Assembly-Aufrufe gegen den
  registrierten Server ist die Parität nicht belegt.
- **Evidenz:** `C:\Daten\Entwicklung\Ralf\AiNetLinter\.mcp.json:1-8`;
  Shell-Prüfung vom 2026-08-31: `ainetlinter: NOT_FOUND`.
- **Konfidenz:** hoch für den fehlenden Live-Nachweis und die lokale
  Registrierung; mittel für die Annahme, dass eine externe Installation
  tatsächlich veraltet ist (nicht live geprüft).
- **Umfangsschätzung:** mittel — registrierten Startpfad/Deployment-Artefakt
  eindeutig aktualisieren, realen MCP-Prozess starten und Handshake,
  Tool-Schemas, `includeReferences` sowie DLL- und DLL+Git-Repo-Szenarien
  end-to-end prüfen.
- **Empfehlung:** Bei der Fortsetzung zuerst den tatsächlichen MCP-Server
  aktualisieren bzw. seinen Startpfad klären; danach einen reproduzierbaren
  Live-Test als Abnahmekriterium ergänzen. Bis dahin keinen vollständigen
  Transparenz-Claim abgeben.

### P2 — EXISTING-HEALTH-FOOTPRINT

- **Beurteilung:** Der Abschlussaudit meldet weiterhin den bestehenden
  `AIContextFootprint`-Warnbefund `2502 > 2500` in
  `GetServerHealthResponseBuilder`. Er betrifft nicht die Kernnavigation,
  verhindert aber die Aussage „global 0 Violations“.
- **Evidenz:** `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs:17`;
  `TD-EPIC-E-001` in `tech-debt.md`.
- **Konfidenz:** hoch.
- **Umfangsschätzung:** klein — sichere Aufteilung/Footprint-Reduktion und
  erneuter MCP-Violations-Check.
- **Disposition:** akzeptiert-zurückgestellt; nicht Teil einer Änderung dieses
  Stop-/Findings-Commits.

## Abdeckung und offene Nachweise

- Die positive Cross-Assembly-Regression ist vorhanden und belegt den
  Kernpfad für drei Symbolgraph-Routen.
- Die Konzept-Checkliste führt das globale Payload-Budget weiterhin als
  offen; das passt zum P1-Befund oben.
- Die volle Tool-Parität für alle Assembly-/Projektwerkzeuge wurde in diesem
  gestoppten Review nicht erneut als Matrix gegen den Live-Server geprüft.
  Nicht erneut belegte Punkte sind daher Nachweis-Lücken, nicht automatisch
  zusätzliche Codebefunde.
- Kritiker 1 lieferte einen terminalen Bericht mit den oben übernommenen
  Befunden. Kritiker 2 wurde auf Nutzeranweisung vor einem terminalen Bericht
  beendet; ihm werden keine ungeprüften Befunde zugeschrieben.
- Es wurden keine fremden Tasks, keine Nutzeränderungen und kein
  Produktionscode angefasst. Die vorhandenen Änderungen in
  `appsettings.json`, `scripts/deploy-local.ps1`,
  `src/AiNetLinter/AiNetLinter.csproj` und `external-sources.json` blieben
  unberührt.
