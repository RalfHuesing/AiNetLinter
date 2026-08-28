---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 007
corrects: null
title: "Source-Snapshot-Identität und residente Registry mit injizierbarem Ergebnis"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T18:31:00+02:00
related_to: [step-006/step-result.md, step-006/step-review.md]
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-006/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-006/step-review.md"
    - "tasks/decompiled-assembly-analysis/codemap.md"
    - "src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs"
    - "src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyRoslynWorkspaceFactory.cs"
    - "src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs"
    - "src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs"
    - "src/AiNetLinter.TestKit/TestTempDirectory.cs"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs — nur zur Bestätigung, dass die Session in diesem Step unverändert bleibt"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs und AssemblyAnalysisService.cs — nur zur Abgrenzung der späteren Session-/Tool-Anbindung"
    - "src/AiNetLinter/Mcp/McpCodeGraphServer.cs und McpCodeGraphServerOptions.cs — nur zur Prüfung der späteren ReadOnlySolutionSnapshot-Übergabe"
    - "src/AiNetLinter/Baseline/SourceFileCatalog.cs und SourceFileCatalogLoader.cs — nur falls der injizierte Workspace-Owner gegen den bestehenden Source-Katalog abgeglichen werden muss"
    - "tasks/decompiled-assembly-analysis/Konzept.md — nur die Abschnitte zu Gitea-Register, Source-Snapshot-Identität und Source-Auflösung bei unklaren Invarianten"
    - "Docs/configuration.md und Docs/agent-api.md — nur falls wider Erwarten ein bereits implementierter öffentlicher Vertrag geändert werden muss"
  out_of_scope:
    - "Vollständige Solution-/Project-Match-Auflösung über Project.AssemblyName, Projektpfadableitung, Match-Evidence oder Confidence"
    - "AssemblyAnalysisSession, AssemblyAnalysisContextFactory, AssemblyAnalysisService, AnalysisToolCall, MCP-Registrierungen sowie Stdio-/Daemon-Wiring"
    - "Gitea-Clone/Fetch, Authentifizierung, Branch-/Refresh-Logik, Netzwerk, Source-of-Truth und persistenter Source-Cache"
    - "Transitive Referenzauflösung, Referenzmatrix, gemeinsame Capability-Matrix und finale Tool-Routen"
    - "Änderungen an ExternalSourceConfiguration-Schema, appsettings.json, Docs, rules.json, task-state.md, codemap.md oder früheren Steps"
    - "Assembly.Load, Reflection-Ausführung, AssemblyLoadContext, Fremdprojekt-Restore und breite DRY-/MagicValues-/DeadCode-Sweeps"
---

# Step 007: Source-Snapshot-Identität und residente Registry mit injizierbarem Ergebnis

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — Step 005/006 haben Mapping,
  Validierung, Diagnose- und Provider-Port abgeschlossen; offen ist die
  gemeinsame Identität eines geladenen Source-Solutionsnapshots.
- **Konzept-Referenz:** `Konzept.md` „Gitea-Register und wartungsarmes
  Mapping“, „Arbeitskontext und Cache-Grenze“, „Fingerprint und Cache-Key“
  sowie Phase 3; der Step implementiert nur die Identitäts-/Registry-Grenze,
  nicht die Source-Akquisition oder die Projektzuordnung.
- **Split-Gate:** Genau zwei eng gekoppelte Verträge, drei Schichten und sieben
  Akzeptanzkriterien. Der Provider-Ergebniswert ist die notwendige
  Injektionsgrenze für die Snapshot-Registry; es entsteht kein zweiter
  Resolver-, Session- oder MCP-Vertrag.

## Aktueller Projektzustand (JIT-Kontext)

Step 006 ist genehmigt. `ExternalSourceMapping` ist ein unveränderlicher
Mappingwert mit Repository-URL, repository-relativem Solution-Pfad und
normalisierten Assembly-Namen. `IExternalSourceProvider` existiert bereits,
aber `ExternalSourceProviderResult` transportiert aktuell nur
`IsAvailable` und Diagnosen; es gibt noch keinen Source-Snapshotwert und keinen
Konsumenten außerhalb des Vertragstests und des `Unavailable`-Adapters.

`AssemblyAnalysisSession` baut und besitzt derzeit ausschließlich eigene
Decompilation-Generationen. `AssemblyRoslynSnapshot` zeigt das passende Muster
für einen Roslyn-`Solution`-Wert mit explizitem `Workspace`-Besitz und
idempotenter Freigabe, darf aber in diesem Step nicht in die Source-Auflösung
verdrahtet werden. `ProjectRegistry` liefert Lock-, residenten Dictionary-,
Lease- und Disposal-Muster, ist wegen seiner `McpCodeGraphServer`- und
Projektdefinition-Abhängigkeiten aber kein direkt wiederverwendbarer
Source-Registry-Typ.

Daraus folgt ein kleiner eigener In-Memory-Registry-Vertrag: Der Schlüssel
identifiziert die vollständige Source-Solution, nicht das darin ausgewählte
Assembly-Projekt. Eine Source-Ergebnisgrenze kann künftig einen bereits
geladenen Roslyn-Snapshot injizieren; sie akquiriert weder selbst Gitea noch
entscheidet sie, welches `Project.AssemblyName` zu einer DLL gehört.

## Intention

Dieser Step definiert `Repository-URL + tatsächlich geladene Revision +
normalisierten Solution-Pfad` als kanonische Identität eines vollständigen
Source-Snapshots. Eine residente Registry dedupliziert solche Snapshots über
Leases, trennt unterschiedliche Revisionen oder Solution-Pfade und besitzt die
Workspace-Freigabe beim Shutdown.

Der bestehende Provider-Port wird so erweitert, dass ein injizierbarer
Provider optional einen bereits geladenen `Solution`-/`Workspace`-Snapshot
zurückgeben kann. `UnavailableExternalSourceProvider` bleibt ohne Source-
Ergebnis; die spätere Assembly-Match-Auflösung und Session-Komposition
konsumieren diesen Vertrag erst in Folgepaketen.

## Kontext-Handoff

### Invarianten

- `SourceSnapshotIdentity` enthält ausschließlich die kanonische Repository-
  URL, die nichtleere tatsächlich geladene Revision und den normalisierten
  repository-relativen `.sln`-/`.slnx`-Pfad. AssemblyName, Projektpfad,
  Target-Framework und Consumer-Kontext sind keine Bestandteile dieses Keys.
- Der gleiche Source-Snapshot-Key wird unabhängig von Assembly-Aliasen oder
  Consumer-Projekten genau einmal resident gehalten; eine andere Revision oder
  ein anderer Solution-Pfad erzeugt einen getrennten Eintrag.
- Eine vorhandene `ExternalSourceMapping` ist die einzige Eingangsquelle für
  Repository-URL und Solution-Pfad. Die Registry führt keine Discovery,
  Dateisuche, Solution-Ladung oder Assembly-Zuordnung aus.
- Ein Provider-Ergebnis darf einen Snapshot nur als verfügbares Source-Ergebnis
  transportieren; Diagnosen bleiben unverändert erhalten. Die Abwesenheit des
  optionalen Snapshots bleibt von der Provider-Verfügbarkeit unterscheidbar und
  wird nicht als Match behauptet.
- Die Registry übernimmt die Eigentümerschaft eines registrierten Snapshots.
  Bei einem bereits vorhandenen Key wird der neu angebotene Snapshot als
  unterlegener Doppelgänger einmal freigegeben; jede Lease-Freigabe ist
  idempotent.
- Die Source-Solution bleibt read-only: Der Vertrag exponiert für Konsumenten
  den Roslyn-`Solution`-Wert, nicht mutierende Provider- oder Workspace-
  Operationen. Registry-Dispose ist ein terminaler Shutdown und räumt alle
  residenten Workspace-Owner kontrolliert auf.
- Keine Decompilation, Assembly-Ausführung, Reflection, Netzwerk-Akquisition,
  transitive Referenzauflösung oder MCP-Dispatch-Änderung wird eingeführt.

### Risiken

- Doppelte Workspace-Freigabe oder Freigabe eines unterlegenen Snapshot-Owners
  wird durch eine zentrale, idempotente `Dispose`-Grenze und klaren
  Eigentumsübergang vermieden.
- Ein zu grober Key könnte unterschiedliche Source-Stände verschmelzen; die
  Tests müssen Revision und Solution-Pfad als trennende Identitätsbestandteile
  nachweisen und Assembly-/Consumer-Aliase ausdrücklich ausklammern.
- Eine Registry mit künstlichem TTL-/LRU-/Creation-Barrier-Framework würde den
  Gate-Rahmen überschreiten. In diesem Step bleibt sie bewusst in-memory und
  resident bis zum expliziten Dispose; Kapazität, Refresh und persistenter
  Cache gehören in spätere Source-/Gitea-Pakete.

### Relevante MCP-Symbole

- `T:AiNetLinter.Configuration.ExternalSourceMapping` — validierter,
  unveränderlicher Eingang für URL und repository-relativen Solution-Pfad.
- `T:AiNetLinter.Mcp.Assemblies.IExternalSourceProvider` — injizierbarer
  Provider-Port, dessen Ergebnis um einen optionalen Source-Snapshot ergänzt
  wird.
- `T:AiNetLinter.Mcp.Assemblies.ExternalSourceProviderResult` — bestehender
  Diagnose-/Verfügbarkeitswert und neue Transportgrenze für das Source-Ergebnis.
- `T:AiNetLinter.Mcp.Assemblies.UnavailableExternalSourceProvider` — Default-
  Adapter, der weiterhin nur den sichtbaren Nichtverfügbarkeitszustand liefert.
- `T:AiNetLinter.Mcp.Assemblies.AssemblyRoslynSnapshot` — vorhandenes
  Workspace-Ownership-Muster als Referenz, nicht als Änderungsziel.
- `T:AiNetLinter.Mcp.Projects.ProjectRegistry` — vorhandenes Lock-/Lease-/
  Disposal-Muster als Referenz, nicht als Basistyp für die Source-Registry.

### Sicherer Einstiegspunkt

Zuerst `SourceSnapshotModels.cs` mit der kanonischen Identität und dem
read-only Roslyn-Snapshot-Owner neben den bestehenden Assembly-Modellen
anlegen. Danach `ExternalSourceProviderResult` um das optionale Ergebnis
erweitern und den `Unavailable`-Adapter explizit snapshotlos lassen. Erst dann
`SourceSnapshotRegistry.cs` als kleine Lock-/Dictionary-/Lease-Grenze mit
Ownership-Übergang implementieren und die beiden isolierten Testgruppen
ausführen. Nicht in `AssemblyAnalysisSession` oder
`AssemblyAnalysisContextFactory` einsteigen.

## Konkrete Änderungen

### Schicht 1 — Source-Snapshot-Vertrag

#### `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs` (neu)

- **Was:** Einen unveränderlichen `SourceSnapshotIdentity`-Wert mit
  `RepositoryUrl`, `LoadedRevision`, `SolutionPath` und stabilem Key definieren.
  Eine Factory aus `ExternalSourceMapping` und der tatsächlich geladenen,
  nichtleeren Revision kanonisiert URI-Schema/Host, Separatoren und
  repository-relative Solution-Segmente deterministisch. `..`-Escapes,
  absolute Solution-Pfade und leere Revisionen bleiben ungültig.
- **Was:** Einen `ExternalSourceSnapshot`-Owner nach dem Muster des bestehenden
  `AssemblyRoslynSnapshot` anlegen: Identität, read-only `Solution` und der
  zugehörige Roslyn-`Workspace`; `Dispose` gibt den Workspace genau einmal
  frei. Der Typ enthält weder ausgewähltes Projekt noch Assembly-Match-
  Metadaten.
- **Warum:** Ein vollständiger Source-Snapshot wird unabhängig von seinen
  späteren Assembly-Aliasen identifizierbar und kann mit kontrollierter
  Workspace-Lebensdauer an einen späteren Resolver übergeben werden.

#### `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`

- **Was:** `ExternalSourceProviderResult` um ein optionales
  `ExternalSourceSnapshot` ergänzen, ohne den bestehenden Diagnosewert zu
  verlieren. `IsAvailable` bleibt der Provider-Zustand; ein vorhandener
  Snapshot ist ein bereitgestelltes Source-Ergebnis, aber noch kein
  `AssemblyName`-/Projekt-Match.
- **Was:** Die Konstruktor-/Eigentumsinvarianten sichern, dass ein Snapshot
  nicht als nicht verfügbar transportiert wird, Diagnosen immutable bleiben
  und bestehende snapshotlose Test-Doubles kompatibel oder gezielt angepasst
  werden.
- **Warum:** Der spätere Source-Resolver kann einen Test- oder Gitea-Adapter
  injizieren, ohne den Resolver an Dateisystem, Netzwerk oder einen konkreten
  Workspace-Lader zu koppeln.

### Schicht 2 — Residente Snapshot-Registry

#### `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` (neu)

- **Was:** Eine kleine `IDisposable`-Registry mit ordinal stabilem
  `SourceSnapshotIdentity.StableValue`-Key, kurzem Lock, residenter Map und
  `SourceSnapshotLease` implementieren. `Acquire` übernimmt einen neuen
  Snapshot oder erhöht bei gleicher Identität die Lease-Zahl des vorhandenen
  Eintrags.
- **Was:** Beim Identitäts-Treffer den neu angebotenen Snapshot kontrolliert
  freigeben und nur den residenten Snapshot ausliefern; unterschiedliche
  Revisionen oder Solution-Pfade bleiben getrennte Einträge. Lease- und
  Registry-Dispose müssen wiederholbar sein; Shutdown räumt alle residenten
  Workspace-Owner außerhalb des Locks auf.
- **Was:** Nur minimale interne Statussicht für Tests vorsehen, etwa
  `ResidentCount` und den von der Lease gelieferten Snapshot. Kein TTL, LRU,
  Kapazitätslimit, asynchroner Creation Barrier, persistenter Cache oder
  automatischer Refresh.
- **Warum:** Die Registry bildet genau die im Konzept geforderte gemeinsame
  Snapshot-Repräsentation ab, ohne den projektgebundenen `ProjectRegistry` um
  Source-, Gitea- oder Sessionsemantik zu überladen.

#### `src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs`

- **Was:** Den bestehenden Adapter nur soweit anpassen, dass sein Ergebnis
  explizit keinen Snapshot enthält; Warning-Diagnose, Mapping-Location,
  Cancellation und kein Netzwerk bleiben unverändert.
- **Warum:** Der Default bleibt bis zur späteren Gitea-/Source-Akquisition
  sichtbar nicht verfügbar und kann nicht versehentlich als source-backed
  Match verwendet werden.

### Schicht 3 — Deterministische Vertrags- und Registry-Tests

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`

- **Was:** Den Recording-Fake um ein optionales Snapshot-Ergebnis ergänzen
  und prüfen, dass Identität/Solution/Diagnosen über den Provider-Port erhalten
  bleiben. Die vorhandenen Tests für snapshotlose Verfügbarkeit,
  `Unavailable` und Cancellation bleiben bestehen.
- **Warum:** Die Injektionsgrenze wird ohne MCP-Host, Solution-Match,
  Gitea-Client oder Netzwerk direkt an ihrem öffentlichen internen Vertrag
  geprüft.

#### `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`

- **Was:** In-memory Roslyn-Workspaces verwenden und mindestens prüfen:
  gleiche Repository-URL/Revision/Solution-Identität wird nur einmal resident
  und liefert über mehrere Leases dieselbe Snapshot-Instanz; anderer Commit
  und anderer Solution-Pfad werden getrennt; doppeltes Lease-/Registry-Dispose
  bleibt ohne Nebenwirkung; der unterlegene doppelte Workspace wird nicht
  resident weiterverwendet.
- **Was:** Einen Alias-Fall mit unterschiedlicher Assembly-Liste, aber gleicher
  Snapshot-Identität abdecken, damit der Registry-Key nicht fälschlich an
  einzelne DLL-Namen gekoppelt wird. Es gibt keinen `Project.AssemblyName`-
  Match-Test.
- **Warum:** Die Tests sichern die zentrale Reuse-/Lebensdauer-Invariante mit
  kleinen Adhoc-Workspaces und ohne Datei-Temp, Assembly-Laden oder externe
  Abhängigkeiten.

## Tests

- [ ] `SourceSnapshotRegistryTests` — kanonische Identität, gleicher Key über
  Alias-Varianten und getrennte Keys für Revision/Solution-Pfad.
- [ ] `SourceSnapshotRegistryTests` — Lease-Reuse, idempotente Freigabe,
  Ownership des unterlegenen Doppels und Registry-Shutdown.
- [ ] `ExternalSourceProviderContractTests` — Snapshot-Ergebnis, Identität,
  Solution und Diagnosen werden durch den Fake-Port transportiert; der
  `Unavailable`-Adapter bleibt snapshotlos und cancellation-sicher.
- [ ] Kein Test lädt eine Assembly, führt fremden Code aus, restauriert ein
  Fremdprojekt oder greift auf Netzwerk/Gitea zu; Adhoc-Workspaces sind
  ausreichend, `TestTempDirectory` nur bei einem konkret notwendigen
  Dateisystemfall.
- [ ] Schneller gezielter Unit-Lauf für die beiden Assembly-Testklassen grün.
- [ ] Abschlussverifikation nach Implementierung: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`;
  Stress bleibt ausgeschlossen.

## Definition of Done

- [ ] `SourceSnapshotIdentity` unterscheidet Repository-URL, tatsächlich
  geladene Revision und Solution-Pfad deterministisch; Assembly-/Projekt-
  Auswahl ist nicht Teil des Snapshot-Keys.
- [ ] `ExternalSourceSnapshot` hält einen read-only Roslyn-Solutionwert mit
  eindeutigem Workspace-Owner und gibt diesen idempotent frei.
- [ ] `SourceSnapshotRegistry` dedupliziert identische Snapshots, trennt
  Revisionen/Pfade, liefert Lease-Reuse und räumt Ownership ohne Doppel-
  freigabe auf; TTL/LRU/Refresh bleiben bewusst außerhalb.
- [ ] `ExternalSourceProviderResult` transportiert optional den Snapshot und
  erhält `IsAvailable` sowie alle strukturierten Mappingdiagnosen; der
  `Unavailable`-Adapter liefert keinen Snapshot.
- [ ] Provider- und Registry-Tests sind deterministisch, read-only und ohne
  Netzwerk, Gitea, Solution-Match, Assembly-Ausführung oder Runtime-Laden.
- [ ] Assembly-Session, MCP-Dispatch, vollständige Solution-Auflösung,
  persistenter Source-Cache und User-Dokumentation bleiben unverändert und
  sind als Folgepakete abgegrenzt.
- [ ] Build und beide vollständigen Nicht-Stress-Testläufe sind grün; keine
  Änderungen an `task-state.md` oder früheren Steps wurden eingeführt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbole, Referenzen und Impact zuerst mit absolutem
  Projektroot über AiNetLinter-MCP prüfen; Textsuche bleibt Ergänzung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` und
  `#2 Architektur-Verbote` — immutable Records, direkte verständliche
  Registry, kein dynamisches Laden, keine Reflection, kein ALC, kein DI-
  Container und keine repo-spezifischen Annahmen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  deterministische Test-Doubles, zentrale Test-Infrastruktur und vollständige
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Result-/Fehlerzustände, Zero-Warning-Gate und keine separaten
  DRY-/MagicValues-/DeadCode-Sweeps.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md#Empfohlene
  Epic-Schnitte` — EPIC-03 wird in Mapping/Validierung und Snapshot-/Session-
  Pakete geteilt; der Step nimmt nur den nächsten Snapshot-/Registry-Kern.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md#Step-Modus`
  — tatsächlichen Code vor dem JIT-Plan lesen, ein Step pro Aufruf und
  `context_budget`/Handoff dokumentieren.

## Bekannte Ausnahmen

- Die Registry besitzt in diesem Step keinen TTL-/LRU-Mechanismus und keinen
  persistenten Disk-Cache. Das ist eine bewusste Gate-Grenze, weil ohne
  Gitea-/Refresh-Vertrag nur die in-memory Reuse- und Ownership-Semantik
  testbar ist; Kapazität und Refresh werden später entschieden.
- `Docs/configuration.md`, `Docs/agent-api.md` und `README.md` werden nicht
  geändert, weil kein neuer Benutzerparameter und noch kein wirksamer
  Source-Match-/MCP-Vertrag sichtbar wird.

## Notes

- Die Source-Snapshot-Identität gehört zur vollständigen Solution. Ein
  Repository mit mehreren Assembly-Projekten darf denselben Snapshot für
  mehrere Aliase verwenden; die spätere Match-Projektion auf ein konkretes
  `Project` bleibt ein separater Vertrag.
- Die vorhandene `AssemblyRoslynSnapshot`-Struktur ist nur das Ownership-
  Referenzmuster. `AssemblyAnalysisSession` bleibt in diesem Step unverändert
  und dekompiliert weiterhin direkt.
- Die vorhandene `ProjectRegistry` wird nicht kopiert oder umbenannt. Ihre
  Lock-/Lease-/Disposal-Invarianten dienen als Leitplanke; source-spezifische
  Registrylogik bleibt wegen abweichender Schlüssel- und Ownership-Semantik
  eigenständig und klein.
- Das AiNetLinter-MCP bestätigte vor dem Textabgleich die relevanten Symbole
  und Aufrufer für `AssemblyAnalysisSession`, `IExternalSourceProvider`,
  `AssemblyAnalysisContextFactory` und `AssemblyRoslynSnapshot` gegen das
  absolute Projektroot `C:\Daten\Entwicklung\Ralf\AiNetLinter`.
