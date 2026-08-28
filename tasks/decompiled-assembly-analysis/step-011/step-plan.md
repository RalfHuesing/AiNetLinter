---
status: done
type: step-plan
task: decompiled-assembly-analysis
step: 011
corrects: step-010
title: "Support-/Lease-Regressionen und Orchestrator-Testzuordnung korrigieren"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T21:15:55+02:00
related_to:
  - step-010/step-review.md
  - step-010/step-result.md
  - step-010/step-plan.md
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-010/step-review.md"
    - "tasks/decompiled-assembly-analysis/step-010/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-010/step-plan.md"
    - "tasks/decompiled-assembly-analysis/follow-up-strategy.md"
    - ".agents/rules/AiNetLinter-McpWorkflow.mdc"
    - ".agents/rules/AiNetLinter.mdc"
    - ".agents/rules/AiNetLinterRichtlinien.mdc"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs"
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelection.cs — Match-State, Selection und SourceSnapshotLease-Referenz"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs und SourceSnapshotModels.cs — Provider-/Snapshot-Ownership der Fixtures"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs — kontrollierter Cancellation-Fehlerpfad des Decompilation-Fallbacks"
    - "src/AiNetLinter.FastTests/Core/TestCoverageScannerTests.cs — zulässiges @covers-Muster"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs — bestehende Lease-/Idempotenz-Assertions"
    - "src/AiNetLinter.TestKit/TestTempDirectory.cs und src/AiNetLinter.TestKit/AssemblyTestHelper.cs — nur bei Fixture-Anpassungen"
  out_of_scope:
    - "Änderungen an AssemblySourceSelectionOrchestrator, AssemblyAnalysisContextFactory, AssemblySourceSelection, SourceSnapshotRegistry, SourceSnapshotLease, ExternalSourceSnapshot, SourceSnapshotIdentity oder ExternalSourceProviderResult"
    - "MCP-/Daemon-/Stdio-Wiring, AssemblyAnalysisToolRegistrations, AnalysisToolCall, InspectAssemblyTool, FindAssemblyExtensionsTool und Host-Ownership"
    - "Gitea, Netzwerk, Authentifizierung, Refresh, persistenter Source-Cache, Fremdprojekt-Restore und externe Testausführung"
    - "Transitive Referenzen, Capability-Matrix, Health-/TTL-/LRU-Verträge sowie vollständige Tool-Payloads"
    - "Assembly.Load, Reflection-Ausführung, AssemblyLoadContext, neue DI-/Plugin-/Registry-Infrastruktur oder neue fachliche Selection-Semantik"
    - "Änderungen an task-state.md, codemap.md, tech-debt.md, roadmap.md, Konzept.md, Docs, rules.json oder früheren Steps"
    - "TD-001, TD-002, TD-003 sowie breite DRY-, MagicValues- und DeadCode-Sweeps"
---

# Step 011: Support-/Lease-Regressionen und Orchestrator-Testzuordnung korrigieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — Korrektur der direkten Support-
  Komposition aus Step 010; die vorhandene Provider-/Registry-/Selection-Logik
  bleibt fachlich unverändert.
- **Korrigiert:** `step-010/step-review.md` mit zwei MAJOR-Findings:
  fehlende statische Zuordnung zum `AssemblySourceSelectionOrchestrator` und
  fehlende Regressionen an der tatsächlichen Support-/Lease-Grenze.
- **Konzept-Referenz:** `Konzept.md` „Source-Auflösung vor der Dekompilation",
  „Registry und Lebensdauer“, „Keine Codeausführung“ und Phase 3. Dieser Fix
  prüft nur die bereits implementierte gemeinsame Quelle/Fallback-Komposition;
  er erweitert weder Mapping noch Provider-/MCP-Semantik.
- **Split-Gate:** Genau ein primärer Support-/Lease-Testvertrag, drei Schichten
  (Testzuordnung, minimale Beobachtungssignatur, Component-Regressionen) und
  sieben Akzeptanzkriterien. Die zwölf `read_first`-Dateien sind die zentrale
  Obergrenze dieses Pakets.

## Aktueller Projektzustand (JIT-Kontext)

`AssemblySourceSelectionOrchestrator` besitzt bereits den Loader-/Provider-/
Registry-Ablauf und einen `AssemblySourceSelectionScope`, dessen
`SourceSnapshotLease` über `IDisposable` idempotent freigegeben wird.
`AssemblyAnalysisToolSupport.ExecuteAsync(parameters, orchestrator)` hält den
Scope aktuell syntaktisch über `CreateContextAsync` und `BuildResult`; der
bestehende Overload ohne Orchestrator bleibt der Legacy-Decompilation-Pfad.

Die vorhandene `AssemblyAnalysisToolSupportTests`-Klasse ruft den Support-
Overload für den Matched-, No-Mapping-, Unavailable- und Invalid-Loader-Fall
auf. Der Lease-Lifetime-Test und die NoMatch-/Ambiguous-Assertions rufen
jedoch weiterhin nur `ResolveAsync` direkt auf. Deshalb meldet der MCP für den
Orchestrator null statisch zugeordnete Testdateien und `StaticTestSentinel`,
obwohl die Testklasse funktional relevante Support-Aufrufe enthält.

Die gemeinsame Support-Signatur exponiert die Selection bisher nicht an den
Result-Builder. Für eine belastbare Aussage „Lease während Factory und Builder
lebendig, danach freigegeben“ ist daher ausschließlich eine optionale interne
Scope-Beobachtung am neuen Orchestrator-Overload zulässig. Sie darf keinen
Context, keine Diagnose, keinen Fallback und keine Ownership ändern.

## Intention

Die bestehende Support-Testklasse wird explizit dem Orchestrator zugeordnet und
prüft die Korrektur an der realen gemeinsamen Verbrauchergrenze. Matched,
NoMatch und Ambiguous laufen über den Support-Overload; normaler Abschluss,
Cancellation und Result-Builder-Fehler weisen die Lease-Freigabe über den
beobachteten Scope nach. Produktions-Fachmodelle und die Auswahlentscheidung
aus Step 010 bleiben unverändert.

## Kontext-Handoff

### Invarianten

- `AssemblySourceSelectionOrchestrator.ResolveAsync` bleibt unverändert:
  metadata-only Assembly-Identität, eindeutiger Mapping-Alias, bestehender
  Provider-Port, `SourceSnapshotRegistry.Acquire` und bestehender
  Match-/Selection-Vertrag werden nicht neu implementiert.
- Der Legacy-Aufruf `AssemblyAnalysisToolSupport.ExecuteAsync(parameters)`
  bleibt unverändert und führt weiterhin ohne Selector zur Decompilation.
- Der optionale Scope-Beobachter wird nur am neuen internen Support-Overload
  nach erfolgreichem `ResolveAsync` aufgerufen. Er liest den Scope, besitzt ihn
  nicht und ersetzt nicht `using`/`Dispose`.
- Der beobachtete Scope muss innerhalb des Result-Builders eine nicht verworfene
  Selection und `SourceLease.IsDisposed == false` zeigen. Nach normalem Rückweg,
  kontrolliert behandelter Cancellation und propagiertem Builder-Fehler muss
  derselbe Lease `IsDisposed == true` sein; eine zusätzliche Dispose-Prüfung
  bleibt idempotent.
- Der Test für Cancellation muss den Token erst im Provider-Callback nach der
  Rückgabe eines verfügbaren Snapshots abbrechen. So wird nachgewiesen, dass
  ein bereits erworbener Scope auch beim anschließenden Factory-/Fallback-
  Fehler freigegeben wird.
- Matched projiziert den vorhandenen Source-Only-Typ und bleibt
  `source-backed`; NoMatch und Ambiguous behalten ihren Match-State im
  transportierten Scope, werden aber von der bestehenden Factory als
  `decompiled` projiziert. Provider-/Loaderdiagnosen und bestehende
  Decompilation-Assertions bleiben erhalten.
- Snapshot-Ownership bleibt bei der Registry. Ein Snapshot darf nach dem
  Support-Aufruf resident bleiben und wird erst an der vorhandenen
  Registry-Dispose-Grenze beendet; getestet wird die Lease-Freigabe, nicht ein
  vorzeitiges Snapshot-Dispose.

### Risiken

- Ein Test, der nur `ExternalSourceSnapshot.IsDisposed` prüft, würde eine
  Lease-Leckage übersehen. Die Assertion muss auf dem vom Scope beobachteten
  `SourceSnapshotLease` liegen.
- Ein Cancellation-Test vor dem Provideraufruf würde keinen Lease erwerben und
  wäre daher kein Lifetime-Test. Die Provider-Fake-Reihenfolge muss explizit
  „Snapshot liefern, Token abbrechen“ verwenden.
- Builder-Ausnahmen dürfen weder geschluckt noch in einen neuen Fehlervertrag
  umgewandelt werden; der `using`-Scope muss den bestehenden Exceptionpfad
  zuverlässig verlassen.
- `@covers` ist eine statische Zuordnung, keine Suppression. Nach der Änderung
  müssen `get_test_context` und `get_violations` erneut mit dem absoluten
  `projectRoot` `C:\Daten\Entwicklung\Ralf\AiNetLinter` geprüft werden.

### Relevante MCP-Symbole

- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator` und
  `M:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator.ResolveAsync`
  — Ziel von `StaticTestSentinel` und Scope-Erzeugung.
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport`,
  `M:...AssemblyAnalysisToolSupport.ExecuteAsync` und
  `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyToolExecutionParameters`
  — gemeinsame Support-Grenze und minimale Beobachtungssignatur.
- `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisContextFactory.CreateAsync`
  sowie `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisContextRequest` — Factory-
  Übergabe der Selection und kontrollierter Decompilation-Fallback.
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelection` und
  `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotLease`, insbesondere
  `SourceLease` und `IsDisposed` — Lifetime-Assertions im Test.
- `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry` und
  `M:...SourceSnapshotRegistry.Acquire` — Registry-Ownership und Deduplizierung.
- Nach der Umsetzung: `get_test_context` und `get_violations` für das
  Orchestrator-Symbol; bei C#-Impact-Fragen zuerst `get_impact` mit absolutem
  `projectRoot`, Textsuche nur ergänzend.

### Sicherer Einstiegspunkt

Zuerst die vorhandene `AssemblyAnalysisToolSupportTests`-Klasse und den
Orchestrator-Overload im aktuellen Code lesen. Dann die explizite
`// @covers AssemblySourceSelectionOrchestrator`-Zuordnung ergänzen und die
minimale optionale Scope-Beobachtung am Support-Overload einführen. Danach den
vorhandenen Matched-/Lease-Test auf den Support-Aufruf umstellen, NoMatch und
Ambiguous ebenfalls über Support ausführen und zuletzt Cancellation sowie
Builder-Fehler als getrennte deterministische Fälle ergänzen. Keine
MCP-Registrierung und keinen neuen Provider-/Registry-Einstieg öffnen.

## Konkrete Änderungen

### Schicht 1 — Statische Testzuordnung: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`

- **Was:** Eine zulässige explizite `// @covers AssemblySourceSelectionOrchestrator`
  -Zeile an der bestehenden Testdatei ergänzen. Keine Suppression, kein
  separates Testprojekt und keine künstliche Testklasse anlegen.
- **Warum:** Der MCP-Testkontext ordnet die fachlich relevanten Tests derzeit
  nur wegen des Dateinamens dem Support zu; die explizite Zuordnung macht die
  Orchestrator-Abdeckung statisch sichtbar.

### Schicht 2 — Minimale Beobachtungssignatur: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`

- **Was:** Die neue interne Überladung um einen optionalen,
  nicht besitzenden `Action<AssemblySourceSelectionScope>?`-Beobachter
  erweitern. Den Beobachter genau nach `ResolveAsync` und vor
  `AssemblyAnalysisContextFactory.CreateAsync` aufrufen; `using var source`
  und die vorhandene Factory-/Builder-Reihenfolge unverändert lassen.
- **Warum:** Der Review verlangt eine Beobachtung des tatsächlichen Leases
  während Factory und `BuildResult`. Die bisherige Builder-Signatur liefert nur
  `AssemblyContext`; ohne diese minimale interne Test-Naht ist der Lease nicht
  direkt prüfbar. Die Naht ist rein beobachtend, ändert kein Produktionsmodell,
  keine Ownership und keinen Response-Vertrag. Bestehende Aufrufer verwenden
  den optionalen Parameter nicht.

### Schicht 3 — Direkte Support-/Lease-Regressionen: `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`

- **Matched und normaler Builder-Pfad:**
  `ExecuteAsync_WithConfiguredMappingPassesMatchedSelectionToFactory` und
  `ExecuteAsync_HoldsSelectionLeaseThroughResultBuilderAndReleasesItOnce`
  müssen den Support-Overload ausführen. Der Scope-Beobachter speichert den
  Scope; der Result-Builder prüft während seiner Ausführung den nicht
  freigegebenen Lease, `source-backed`, den Source-Only-Typ und die bisherige
  Target-Identität. Nach erfolgreicher Rückkehr wird `IsDisposed` geprüft und
  eine zusätzliche Scope-Freigabe auf Idempotenz ausgeführt. Die vorhandene
  Snapshot-Deduplizierungs-Assertion bleibt erhalten.
- **NoMatch und Ambiguous:**
  `ExecuteAsync_InvalidConfigurationOrUnusableMatchFallsBackDeterministically`
  soll für beide Fälle den Support-Overload verwenden, jeweils den Scope samt
  Match-State beobachten und im Builder den bestehenden `decompiled`-Fallback
  mit Target-Typ ohne Source-Only-Typ prüfen. Nach jedem Aufruf muss der
  zugehörige Lease freigegeben sein; kein Projekt darf über Namen oder Pfad
  ausgewählt werden.
- **Cancellation nach Lease-Erwerb:** Eine neue Regression mit dem
  vorhandenen `RecordingProvider` lässt den Provider einen verfügbaren Snapshot
  liefern und bricht danach den übergebenen `CancellationTokenSource` ab. Der
  Support-Aufruf muss den bestehenden kontrollierten Assembly-Fehler liefern;
  der beobachtete Scope muss trotz Factory-/Fallback-Abbruch freigegeben sein.
  Provider-Aufrufzahl, Mapping und Token-Weitergabe bleiben assertiert.
- **Result-Builder-Fehler:** Eine neue Matched-Regression lässt den Builder
  nach der Assertion eines lebendigen Leases eine eindeutige
  `InvalidOperationException` werfen. Die Ausnahme bleibt sichtbar und wird
  nicht in einen neuen Result-Vertrag umgewandelt; nach `Assert.ThrowsAsync`
  muss der beobachtete Lease freigegeben und die Registry weiterhin Eigentümer
  des residenten Snapshots sein.
- **Bestehende Regressionen bewahren:** No-Mapping, unavailable Provider,
  ungültiger Loader-Result, Providerdiagnosen, deduplizierter Snapshot und der
  direkte Provider-Cancellation-Test bleiben mit ihren bestehenden Aussagen
  erhalten. Test-Fixtures bleiben `TestTempDirectory`-/Adhoc-Roslyn-basiert,
  netzwerkfrei und ohne Runtime-Laden.

## Tests

- [ ] `ExecuteAsync_WithConfiguredMappingPassesMatchedSelectionToFactory` prüft
  weiter Source-Selection, Origin, Target-Identität, Source-Only-Symbol,
  Provider-Mapping und CancellationToken über den Support-Overload.
- [ ] `ExecuteAsync_HoldsSelectionLeaseThroughResultBuilderAndReleasesItOnce`
  prüft den Lease während Factory/Builder sowie normale und idempotente
  Freigabe; Snapshot-Ownership bleibt bei der Registry.
- [ ] `ExecuteAsync_InvalidConfigurationOrUnusableMatchFallsBackDeterministically`
  führt NoMatch und Ambiguous beide über Support aus und prüft den
  Decompilation-Fallback samt Lease-Freigabe.
- [ ] Neue Support-Regression für Cancellation nach Provider-Snapshot-Erwerb
  weist kontrollierten Fehler und Lease-Freigabe nach.
- [ ] Neue Support-Regression für einen fehlerhaften Result-Builder weist
  sichtbare Ausnahme und Lease-Freigabe nach.
- [ ] Bestehende No-Mapping-, Unavailable-, Loaderdiagnose- und direkte
  Provider-Cancellation-Assertions bleiben grün.
- [ ] Vor und nach der Änderung: MCP-Semantik mit absolutem
  `projectRoot=C:\Daten\Entwicklung\Ralf\AiNetLinter` über
  `get_test_context`/`get_violations` prüfen; danach fokussiert
  `dotnet test src/AiNetLinter.FastTests --filter
  "FullyQualifiedName~AssemblyAnalysisToolSupportTests" --no-restore` sowie
  die vollständigen Nicht-Stress-Gates ausführen.

## Definition of Done

- [ ] `get_test_context` findet die bestehende Support-Testdatei über die
  explizite `@covers`-Zuordnung für `AssemblySourceSelectionOrchestrator`;
  `get_violations` meldet für das Orchestrator-Symbol keinen
  `StaticTestSentinel` mehr.
- [ ] Matched, NoMatch und Ambiguous werden an der gemeinsamen
  `AssemblyAnalysisToolSupport.ExecuteAsync`-Grenze ausgeführt; der bestehende
  Source-/Fallback-Vertrag bleibt unverändert.
- [ ] Der Lease ist während Factory und `BuildResult` beobachtbar lebendig und
  nach normalem Rückweg, Cancellation und Builder-Fehler freigegeben;
  Snapshot- und Registry-Ownership werden nicht vorzeitig übernommen.
- [ ] Der optionale Beobachter ist die einzige produktive Änderung: interne,
  nicht besitzende Test-Naht ohne neue Fachmodelle, Diagnosesemantik,
  Fallbackregeln oder öffentliche API.
- [ ] Loader-/Providerdiagnosen, Provider-Token, Deduplizierung, No-Mapping,
  unavailable, Invalid-Loader und direkte Provider-Cancellation bleiben durch
  die vorhandenen Regressionen gesichert.
- [ ] Keine verbotene Codeausführung oder dynamische Assembly-Ladung und keine
  Änderung an MCP-/Daemon-/Host-Wiring, Gitea, Netzwerk, transitive
  Referenzen, Capability-Matrix oder TD-001/TD-002/TD-003.
- [ ] `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  sind grün; der Coder schreibt `step-011/step-result.md` und committet die
  Korrektur separat.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbole, Referenzen, Impact und Violations zuerst über
  AiNetLinter-MCP mit absolutem `projectRoot`; `rg` bleibt Textsuche.
- `.agents/rules/AiNetLinter.mdc#test-coverage` — komplexe Typen benötigen
  statische Testklasse, `typeof(T)` oder `// @covers T`; eine Suppression ist
  nicht zulässig.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine
  Runtime-Ladung, kein `AssemblyLoadContext`, keine Reflection-Ausführung,
  keine neue DI-/Plugin-Schicht und keine repo-spezifische Semantik.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrale `TestTempDirectory`, deterministische Test-Doubles und vollständige
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  keine abgeschwächten Assertions oder Diagnoseverluste; direkte DRY-,
  MagicValues- und DeadCode-Funde bleiben außerhalb dieses Fix-Scope.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md#Split-Gate vor dem Coder`
  — höchstens ein primärer Vertrag, drei Schichten, acht Kriterien und zwölf
  `read_first`-Dateien.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md#6.2.1 Korrektur-Steps`
  — flacher Fix-Step mit `corrects: step-010`; die Roadmap wird nicht geändert.

## Bekannte Ausnahmen

- Die vorhandene Support-Testklasse wird wegen der Nähe zur gemeinsamen
  Verbrauchergrenze per explizitem `@covers` auch dem Orchestrator zugeordnet;
  das ist eine erlaubte statische Zuordnung und keine Regelunterdrückung.
- Der Testbeobachter ist eine minimale interne Signaturkorrektur, weil die
  bestehende `BuildResult`-Signatur den Lease nicht sichtbar macht. Er darf
  ausschließlich beobachten; falls eine gleichwertige testinterne Lösung ohne
  Produktionssignatur gefunden wird, ist diese zu bevorzugen.
- `TD-001`, `TD-002` und `TD-003` sowie allgemeine Audit-Funde bleiben bewusst
  Tech-Debt und werden in diesem Korrekturpaket nicht künstlich bearbeitet.

## Code-Skizze (optional)

```csharp
internal static Task<CallToolResult> ExecuteAsync(
    AssemblyToolExecutionParameters parameters,
    AssemblySourceSelectionOrchestrator orchestrator,
    Action<AssemblySourceSelectionScope>? observeScope = null)
{
    // Nach ResolveAsync beobachten; using, Factory und BuildResult bleiben gleich.
}
```

## Notes

- Kein Produktionscode des Orchestrators, der Factory, der Registry oder des
  Selection-Modells ändern. Die Reviewbefunde werden an der gemeinsamen
  Support-Grenze und im Testvertrag geschlossen.
- Keine direkte `ResolveAsync`-Assertion darf als Ersatz für einen Matched-,
  NoMatch- oder Ambiguous-Support-Aufruf verbleiben; direkte Orchestrator-Tests
  sind nur zusätzlich für Provider-Cancellation/Ownership zulässig.
- Lease- und Snapshot-Lifetime nicht verwechseln: Lease-Freigabe ist nach dem
  Support-Aufruf zu prüfen, Snapshot-Dispose erst an der vorhandenen Registry-
  Grenze. Keine privaten Felder per Reflection inspizieren.
- Vor jeder C#-Semantik-/Impact-Prüfung `get_feature_context`,
  `get_test_context`, `get_violations` oder `get_impact` des AiNetLinter-MCP mit
  dem absoluten Projektroot verwenden. Es werden keine Assemblys geladen oder
  ausgeführt.
