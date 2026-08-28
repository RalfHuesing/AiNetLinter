---
status: planned
type: step-plan
task: decompiled-assembly-analysis
step: 013
corrects: step-012
title: "Registrierten Assembly-Host-Wiring-/Lifecycle-Vertrag absichern"
epic: EPIC-03
estimated_risk: medium
step_type: correction
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28
related_to:
  - step-012/step-plan.md
  - step-012/step-result.md
  - step-012/step-review.md
  - follow-up-strategy.md
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-012/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-012/step-review.md"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisHostComposition.cs"
    - "src/AiNetLinter/Mcp/McpServerOptionsFactory.cs"
    - "src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs"
    - "src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs"
    - "src/AiNetLinter/Commands/McpServerCommand.cs"
    - "src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs"
    - "src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblyAnalysisHostCompositionTests.cs"
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs"
    - "src/AiNetLinter.TestKit/ThinClientPipeTestDoubles.cs"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs — bestehender Wrapper-Overload und Result-Builder"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs — bestehender Wrapper-Overload und Result-Builder"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs und src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs — Provider-Port und Default-Fallback"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs und src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs — bestehende Registry-/Snapshot-Ownership"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs — vorhandene Selection-Grenze"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs — Recording-Provider-Muster"
    - "src/AiNetLinter.TestKit/AssemblyTestHelper.cs — deterministische DLL-Fixture"
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs — bestehender Snapshot-Testfixture-Builder für die direkte TD-004-Prüfung"
    - "src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs — bestehender Stdio-Assembly-Fallback und Toolinventar"
    - "src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs — bestehender echter Daemon-Handshake und Assembly-Toolpfad"
    - "src/AiNetLinter/Mcp/Daemon/DaemonHost.cs und src/AiNetLinter/Mcp/Daemon/DaemonPipeTransport.cs — Session-Abschluss, Connection-Dispose und Hostende"
  out_of_scope:
    - "AnalysisToolCall.cs, AnalysisTargetRequest, AnalysisTarget, ProjectRegistry, ProjectToolCall und DaemonRuntimeContext; Target-, Projekt- und Session-Kontext-Verträge bleiben unverändert"
    - "ExternalSourceMapping, ExternalSourceConfigurationLoader-Schema, ExternalSourceSnapshot, SourceSnapshotIdentity, SourceSnapshotRegistry.Acquire/Lease, AssemblySourceMatchResolver, AssemblySourceSelection, AssemblyAnalysisContextRequest und AssemblyAnalysisContextFactory; keine neue Source-/Snapshot-/Match-/Factory-Semantik"
    - "Gitea-Clone/Fetch, Authentifizierung, Refresh, Netzwerk, echte Provider-Akquisition, lokale Source-of-Truth, persistenter Cache und EPIC-04-Fehlersemantik"
    - "Transitive Referenzen, Capability-Matrix, weitere Tools oder MCP-Registrierungen, Health-/Kapazitäts-/TTL-/LRU-Verträge, zusätzliche Stdio-Provider-Injection und externe Testausführung"
    - "Assembly.Load, Reflection-Ausführung, AssemblyLoadContext, Runtime-Ausführung fremder Assemblies sowie Fremdprojekt-Restore"
    - "Änderungen an task-state.md, codemap.md, tech-debt.md, roadmap.md, früheren Step-Artefakten, Docs, README, rules.json und finale globale Audits"
    - "TD-001 bis TD-003 sowie breite DRY-/MagicValues-/DeadCode-Sweeps; TD-004 nur als direkt erforderliche, test-only Fixture-Wiederverwendung ohne neue Kopie"
---

# Step 013: Registrierten Assembly-Host-Wiring-/Lifecycle-Vertrag absichern

## Korrekturgrund

Step 012 hat die Produktionsverdrahtung für eine hostlebenslange
`AssemblyAnalysisHostComposition` umgesetzt und die direkten Assembly-Tools an
den vorhandenen Support-Overload angeschlossen. Der Kritiker hat jedoch zwei
gekoppelte Abnahmelücken festgestellt:

- `AssemblyAnalysisHostCompositionTests` ruft die beiden Wrapper direkt auf und
  umgeht damit `AssemblyAnalysisToolRegistrations`.
- `WiringContractTests` prüft nur Toolinventar und Schema; die echten Stdio-/Daemon-
  Prozessregressionen verwenden den Default-Provider und beweisen daher nur den
  decompiled-/unavailable-Fallback.
- `DaemonHostMcpContractTests` beendet genau eine EOF-Session und beobachtet weder
  die Wiederverwendung derselben Composition/Registry noch die Dispose-Grenze
  zwischen Sessionende und Daemonende.

Die Korrektur bündelt beide Befunde in einem primären Vertrag: **registrierte
Assembly-MCP-Callbacks verwenden dieselbe hostlebenslange Composition über zwei
Daemon-Sessions; Sessionende gibt die Source-Registry nicht frei, erst das
Daemonende beendet sie.** Der Nachweis erfolgt über eine echte MCP-Client-/Server-
Verbindung im bestehenden in-memory Hostpfad. Direkte Aufrufe von
`InspectAssemblyTool.ExecuteAsync` oder `FindAssemblyExtensionsTool.ExecuteAsync`
zählen für diesen Vertrag ausdrücklich nicht.

## Split-Gate

- **Primärer Vertrag:** genau ein Host-Wiring-/Lifecycle-Testvertrag für die zwei
  direkten Assembly-Registrierungen und ihre gemeinsame Daemon-Composition.
- **Schichten:** (1) deterministische Test-Composition mit Recording-Provider und
  vorhandener Source-Snapshot-Fixture, (2) registrierter Callback und zwei
  in-memory MCP-Session-Lebenszyklen, (3) bestehende Stdio-/Daemon-Prozess-
  Parität und Abschluss-Gates.
- **Akzeptanzkriterien:** acht, siehe unten.
- **`read_first`:** zwölf zentrale Dateien, siehe `context_budget`.
- **Vertikaler Schnitt:** Die Korrektur prüft nur den bereits implementierten
  Consumerpfad. Source-Auswahl, Snapshot-Identität, Lease-Semantik und Target-
  Dispatch werden konsumiert, nicht neu definiert.

## Kontext-Handoff

### Invarianten

- `AssemblyAnalysisHostComposition` besitzt genau eine
  `SourceSnapshotRegistry` und einen `AssemblySourceSelectionOrchestrator`.
  `McpServerOptionsFactory.BuildToolCollection` erhält diese Instanz explizit;
  `AssemblyAnalysisToolRegistrations.Register` ist der einzige direkte Adapter
  für `inspect_assembly` und `find_assembly_extensions`.
- Der Test des source-backed Pfads muss die registrierte MCP-Tool-Collection
  durchlaufen: `DaemonHostCommand.RunMcpSessionAsync` baut die Collection,
  `McpClient.CallToolAsync` ruft beide Toolnamen auf. Wrapper-Direktaufrufe,
  private Delegate-Aufrufe oder ein isolierter Support-Aufruf sind für diesen
  Nachweis unzureichend.
- Der Recording-Provider liefert für ein gematchtes Assembly-Target einen
  vorhandenen `ExternalSourceSnapshot` mit passendem `AssemblyName`, einem
  Source-only-Typ bzw. einer Source-only-Extension und einer kontrollierten
  Providerdiagnose. Das Fixture bleibt read-only, netzwerkfrei und benutzt
  `AssemblyTestHelper`/`TestTempDirectory` bzw. die bestehende zentrale
  Snapshot-Testhilfe.
- Die beiden Sessions benutzen dieselbe `AssemblyAnalysisHostComposition` und
  dieselbe `SourceSnapshotRegistry`. Nach dem Ende der ersten und der zweiten
  Session bleiben `ResidentCount` und Snapshot lebendig; erst ein explizites
  Dispose am simulierten Daemonende leert die Registry und beendet den Snapshot.
  Das Dispose muss idempotent bleiben.
- Die vorhandene Source-/Match-/Lease-/Result-Semantik bleibt maßgeblich:
  `source-backed`, Source-only-Symbol, Filter-/Limit-Weitergabe und
  Providerdiagnose werden beobachtet; es werden keine neue Selection, kein
  zweiter Result-Builder und keine neue Factory eingeführt.
- Stdio bleibt bei einer Composition pro Serverlauf; der echte Stdio-Contract
  darf weiterhin den netzwerkfreien `decompiled`-/`ProviderUnavailable`-
  Fallback und das unveränderte Toolinventar prüfen. Dieser Prozesspfad ist
  kein Ersatz für den injizierten source-backed Nachweis.
- `AnalysisToolCall.ExecuteAssemblyAsync`, `targetType`/`targetPath`,
  Projekt-Dispatch, `DaemonRuntimeContext`, Toolinventar und alle nicht direkt
  betroffenen Registrierungen bleiben unverändert.

### Relevante MCP-Symbole

- `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisHostComposition` — Host-Owner
  für Provider, Registry und Orchestrator sowie die Dispose-Grenze.
- `M:AiNetLinter.Mcp.McpServerOptionsFactory.BuildToolCollection` — explizite
  Composition-Durchleitung in die Tool-Collection.
- `M:AiNetLinter.Mcp.Registration.AssemblyAnalysisToolRegistrations.Register` —
  registrierter Adapter für beide direkten Assembly-Tools.
- `M:AiNetLinter.Mcp.Daemon.DaemonHostCommand.RunMcpSessionAsync` und
  `M:AiNetLinter.Mcp.Daemon.DaemonHostCommand.CreateSessionRunner` — Session-
  Verbraucher und Host-Capture der Composition.
- `M:AiNetLinter.Commands.McpServerCommand.RunAsync` — Stdio-Composition und
  Server-Lifetime.
- `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport.ExecuteAsync`
  mit `AssemblySourceSelectionOrchestrator` — bestehende Source-/Fallback-
  Verbrauchsgrenze.
- `M:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry.Acquire` und
  `M:AiNetLinter.Mcp.Assemblies.IExternalSourceProvider.ResolveAsync` —
  unveränderte Snapshot-Ownership und Provider-Beobachtung.

### Sicherer Einstiegspunkt

Mit `DaemonHostMcpContractTests.cs` und
`ThinClientPipeTestDoubles.CreateDuplexPair()` beginnen. Einen deterministischen
in-memory MCP-Server pro Session über `RunMcpSessionAsync` starten, den Client
initialisieren und ausschließlich über `CallToolAsync` die beiden registrierten
Assembly-Tools aufrufen. Danach denselben Ablauf ein zweites Mal mit derselben
Composition wiederholen. Erst wenn beide Aufrufe source-backed und die
Resident-/Dispose-Aussagen trennscharf sind, die bestehende echte Daemon-
Prozessregression um den zweiten Connection-Zyklus ergänzen bzw. unverändert
bestätigen.

## Umsetzung

### Schicht 1 — Deterministische Test-Composition und Fixture-Wiederverwendung

#### `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs`

- Den vorhandenen Test um einen kontrollierten Recording-Provider und ein
  gematchtes Adhoc-Source-Snapshot ergänzen. Das Assembly-Fixture enthält einen
  bewusst nicht im Snapshot vorhandenen Typ (`TargetOnly`); die Source-Solution
  enthält stattdessen einen Source-only-Typ und eine gezielt filterbare
  Extension-Methode. Eine Providerdiagnose wird absichtlich mitgeliefert.
- Den bestehenden privaten Snapshot-Aufbau nicht erneut kopieren. Falls die
  vorhandene `CreateSnapshot`-Hilfe wegen ihrer privaten Sichtbarkeit nicht
  wiederverwendbar ist, den bereits in Support-/Factory-Tests duplizierten
  test-only Builder einmal in eine zentrale TestKit-/FastTests-Hilfe überführen
  und die betroffenen Tests auf diese eine Hilfe umstellen. Das ist die einzige
  zulässige direkte TD-004-Integration; `tech-debt.md` bleibt unverändert.
- Die Composition mit Mapping-Settings und dem Recording-Provider erzeugen.
  Keine Produktions-Provider-Injection, kein Netzwerk und keine Änderung am
  External-Source-Konfigurationsschema einführen.

### Schicht 2 — Registrierter Callback und zwei Daemon-Session-Lifecycles

#### `src/AiNetLinter.FastTests/Mcp/Daemon/DaemonHostMcpContractTests.cs`

- Einen kleinen in-memory MCP-Session-Helper auf Basis des vorhandenen
  `ThinClientPipeTestDoubles.CreateDuplexPair()` verwenden. Die Daemon-Seite
  läuft über `DaemonHostCommand.RunMcpSessionAsync(connection, registry,
  composition)`; die Client-Seite verwendet den vorhandenen MCP-Client und
  `StreamClientTransport`. Die Session endet kontrolliert durch Client-/Stream-
  Dispose bzw. EOF.
- In jeder der zwei Sessions `inspect_assembly` und
  `find_assembly_extensions` über ihren MCP-Namen aufrufen. Der Test muss damit
  zwingend `BuildToolCollection` und `AssemblyAnalysisToolRegistrations.Register`
  durchlaufen; die direkten Wrapper-Overloads dürfen nicht aufgerufen werden.
- Für `inspect_assembly` `typeName`/exakte Suche und ein knappes
  `maxMembers`-/`maxResults`-Limit setzen. Für
  `find_assembly_extensions` die Source-only-Extension und ein knappes Limit
  filtern. Beide Antworten müssen `source-backed`, die Source-only-Ausgabe und
  die kontrollierte Providerdiagnose tragen; der decompiled-Fallback darf im
  matched Szenario nicht erscheinen.
- Nach Session 1 und Session 2 `composition.IsDisposed == false`, den
  gemeinsamen `ResidentCount` und die Lebendigkeit des kanonischen Snapshots
  assertieren. Der Recording-Provider muss die wiederholte Nutzung desselben
  Mappings/Source-Kontexts und die unveränderte Cancellation-/Argumentweitergabe
  sichtbar machen; keine fragile Zeitmessung verwenden.
- Erst nach beiden Session-Lebenszyklen `composition.Dispose()` als
  Daemon-Lebensende ausführen und danach `ResidentCount == 0`, Snapshot-Dispose
  sowie die bestehende idempotente Dispose-/Orchestrator-Sperre prüfen. Ein
  Session-Dispose darf nicht die Composition oder ihre Registry beenden.

#### Bestehende direkte Tests

- `AssemblyAnalysisHostCompositionTests.cs` behält seine direkten
  Composition-/Fallback-/Ownership-Aussagen; sie werden nicht als Nachweis des
  registrierten source-backed Callbacks umetikettiert.
- `WiringContractTests.cs` behält Toolinventar, Schema und Annotationen. Nur
  falls der neue Contract dort architektonisch besser an die vorhandene
  Collection-Regression passt, darf eine kleine Verknüpfung ergänzt werden;
  kein zweiter source-backed Result-Builder und keine parallele Fixture.

### Schicht 3 — Stdio-/Daemon-Parität und Verifikation

#### `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs` und
`src/AiNetLinter.IntegrationTests/Mcp/Daemon/DaemonHostMcpProcessContractTests.cs`

- Den vorhandenen Stdio-Contract für `inspect_assembly` und
  `find_assembly_extensions` als Fallback-/Inventarregression erhalten. Er darf
  weiterhin den produktiven `UnavailableExternalSourceProvider` sehen und
  muss nicht künstlich source-backed gemacht werden.
- Den Daemon-Prozess-Contract, falls dafür erforderlich, um einen zweiten
  sequentiellen Connection-/MCP-Session-Zyklus erweitern: jeweils Handshake,
  Tool-Liste und mindestens die direkten Assembly-Aufrufe, anschließend
  kontrolliertes Idle-Exit. Dieser Test belegt Transport-/Host-Parität; die
  injizierte Composition-/Registry-Identity bleibt der Fast-Contract aus
  Schicht 2.
- Keine neue Prozessfixture, kein Gitea-/Netzwerkzugriff und kein Runtime-Laden
  der untersuchten Assembly einführen. Bestehende Retry-/Timeout-Helfer bleiben
  begrenzt und werden nicht durch Sleeps oder unbounded Retries ersetzt.

## Akzeptanzkriterien

- [ ] Ein deterministischer Fast-Component-Test ruft beide direkten Assembly-
  Tools über eine echte MCP-Client-/Server-Session und die von
  `RunMcpSessionAsync` gebaute `McpServerOptionsFactory`-Tool-Collection auf;
  kein direkter Wrapper- oder Support-Aufruf ersetzt den Registration-Callback.
- [ ] Der registrierte source-backed Pfad liefert für das gematchte Fixture
  `source-backed`, einen Source-only-Typ bzw. eine Source-only-Extension, die
  kontrollierte Providerdiagnose sowie die erwartete Filter-/Limit-Weitergabe;
  der decompiled-Fallback wird in diesem Szenario nicht fälschlich verwendet.
- [ ] Der gleiche Test führt mindestens zwei abgeschlossene MCP-Sessions mit
  derselben `AssemblyAnalysisHostComposition` und demselben
  `SourceSnapshotRegistry` aus; beide Sessions rufen beide direkten Assembly-
  Tools erfolgreich über die Registrierung auf.
- [ ] Nach dem Ende jeder Session bleiben Composition, kanonischer Snapshot und
  gemeinsamer `ResidentCount` lebendig; der Provider-/Snapshot-Nachweis zeigt
  keine per Session erzeugte Registry und keine vorzeitige Freigabe.
- [ ] Erst das explizite Daemon-Lebensende beendet die hosteigene Registry und
  den Snapshot; ein mehrfaches Composition-Dispose bleibt idempotent und neue
  Selection-Zugriffe werden weiterhin kontrolliert abgewiesen.
- [ ] Die bestehenden Stdio-/Daemon-Prozessverträge behalten Handshake,
  29er-Toolinventar, Target-Schema und den netzwerkfreien decompiled- bzw.
  unavailable-Fallback; `AnalysisToolCall`, Projektpfade und weitere
  Registrierungen bleiben unverändert.
- [ ] Es werden keine neue Source-/Snapshot-/Match-/Factory-Fachsemantik,
  keine Assembly.Load-/Reflection-/AssemblyLoadContext-Route und keine
  Gitea-/Netzwerk-/transitive-/Capability-Erweiterung eingeführt; TD-001 bis
  TD-003 bleiben offen und TD-004 wird höchstens direkt als einmalige
  Test-Fixture-Wiederverwendung berührt.
- [ ] `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  sind grün. Stressläufe und finale globale Audits bleiben außerhalb dieses
  Korrekturpakets.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbol-, Referenz- und Impact-Fragen zuerst mit dem
  AiNetLinter-MCP und absolutem
  `projectRoot=C:\Daten\Entwicklung\Ralf\AiNetLinter`; `rg` nur für exakte
  Text-/Pfadsuche.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — kein
  DI-/Plugin-/Service-Locator-Overhead, kein Runtime-Laden, keine Reflection-
  Ausführung und kein `AssemblyLoadContext`.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrale TestTempDirectory-/TestKit-Fixtures, deterministische Doubles,
  bewahrte Testparallelität und vollständige Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  bestehende Source-/Fallback-/Lease-Assertions nicht abschwächen; kein
  künstlicher DRY-/MagicValues-/DeadCode-Sweep.
- `follow-up-strategy.md#Split-Gate vor dem Coder` — ein primärer Vertrag,
  höchstens drei Schichten, höchstens acht Kriterien und höchstens zwölf
  `read_first`-Dateien.

## Handoff

Der Coder und der Kritiker starten mit diesem Plan, dem Step-012-Result, dem
Step-012-Review und den zwölf `read_first`-Dateien. Für jede zusätzliche
C#-Semantik-, Referenz- oder Impact-Frage ist zuerst das AiNetLinter-MCP mit
`projectRoot=C:\Daten\Entwicklung\Ralf\AiNetLinter` zu verwenden; `rg` bleibt
auf Textsuche begrenzt. Keine Assembly.Load-, Reflection-Ausführung oder
AssemblyLoadContext-Verwendung.

Der sichere Implementierungseinstieg ist der bestehende Fast-Daemon-Contract:
`ThinClientPipeTestDoubles.CreateDuplexPair()` →
`DaemonHostCommand.RunMcpSessionAsync` →
`McpServerOptionsFactory.BuildToolCollection` →
`AssemblyAnalysisToolRegistrations.Register` → MCP-Client-
`CallToolAsync("inspect_assembly"/"find_assembly_extensions")`. Diesen Ablauf
zweimal mit derselben Composition ausführen, zwischen den Sessions
`ResidentCount`/Snapshot-Lebendigkeit prüfen und erst danach Composition-
Dispose als Hostende auslösen. Die vorhandenen Support-/Wrapper-Tests bleiben
Ergänzung, nicht Ersatz für diesen Nachweis.

Nach erfolgreicher Implementierung schreibt der Coder nur das Step-013-Result;
`task-state.md`, `codemap.md`, `tech-debt.md`, `roadmap.md`, frühere Step-
Artefakte und Produktionsdateien außerhalb des direkt notwendigen Host-/Wrapper-
Consumers bleiben unverändert.
