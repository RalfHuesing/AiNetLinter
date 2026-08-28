---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 010
corrects: null
title: "Provider-/Registry-Selection für direkte Assembly-Tool-Unterstützung komponieren"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T20:33:36+02:00
related_to:
  - step-009/step-result.md
  - step-009/step-review.md
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-009/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-009/step-review.md"
    - "tasks/decompiled-assembly-analysis/codemap.md"
    - "src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs"
    - "src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs"
    - "src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceMatchResolver.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelection.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyReferenceResolver.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs"
  read_on_demand:
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs — bestehende Test- und Payload-Konventionen, falls der Support-Test einen konkreten Result-Builder benötigt"
    - "src/AiNetLinter.FastTests/Configuration/ExternalSourceConfigurationLoaderTests.cs — vorhandene Test-Helper für appsettings-/Mapping-Dateien"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs — Recording-Provider und Snapshot-Testdaten wiederverwenden"
    - "src/AiNetLinter.TestKit/TestTempDirectory.cs und src/AiNetLinter.TestKit/AssemblyTestHelper.cs — nur für die deterministische DLL-/Settings-Fixture"
    - "src/AiNetLinter/Mcp/AnalysisToolCall.cs, src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs, src/AiNetLinter/Mcp/McpServerOptionsFactory.cs — nur zur Bestätigung der nachfolgenden MCP-Kompositionsgrenze; nicht ändern"
    - "src/AiNetLinter/Commands/McpServerCommand.cs und src/AiNetLinter/Mcp/Daemon/DaemonHostCommand.cs — nur zur Bestätigung der späteren Registry-/Provider-Ownership; nicht ändern"
  out_of_scope:
    - "Änderungen an SourceSnapshotIdentity, ExternalSourceSnapshot, SourceSnapshotRegistry, SourceSnapshotLease, AssemblySourceMatchResolver, AssemblySourceSelection, ExternalSourceProviderResult oder AssemblyAnalysisContextRequest"
    - "Gitea-Clone/Fetch, Authentifizierung, Branch-/Refresh-Logik, Netzwerk, echte Provider-Akquisition, lokale Source-of-Truth, Solution-Akquisition und persistenter Source-Cache"
    - "MCP-Registrierungen, AnalysisToolCall, McpServerOptionsFactory, Stdio-/Daemon-Wiring, gemeinsame Provider-/Registry-Instanz in den Hostkommandos sowie Änderungen an InspectAssemblyTool/FindAssemblyExtensionsTool"
    - "Transitive Referenzen, Capability-Matrix, Health-/Kapazitäts-/TTL-/LRU-Verträge, Refresh, Binary-/PDB-/SourceLink-Versionsbeweis und externe Testausführung"
    - "Neue DI-/Plugin-/AnalysisRegistry-Infrastruktur, Assembly.Load, Reflection-Ausführung, AssemblyLoadContext oder Fremdprojekt-Restore"
    - "Änderungen an Mapping-Schema, appsettings.json, Docs, README, rules.json, task-state.md, codemap.md, tech-debt.md oder früheren Steps"
    - "TD-001, TD-002, TD-003 sowie breite DRY-/MagicValues-/DeadCode-Sweeps"
---

# Step 010: Provider-/Registry-Selection für direkte Assembly-Tool-Unterstützung komponieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — Step 009 ist genehmigt und stellt
  bereits die Factory-Projektion für ein geleastes `AssemblySourceSelection`
  sowie den statischen Decompilation-Fallback bereit. Es fehlt die kontrollierte
  Komposition, die ein direktes Assembly-Tool aus einem geladenen Mapping,
  Provider-Ergebnis und Snapshot-Lease bis zur Factory führt.
- **Konzept-Referenz:** `Konzept.md` „Gitea-Register und wartungsarmes Mapping“,
  „Source-Auflösung vor der Dekompilation“, „Arbeitskontext und Cache-Grenze“
  und Phase 3. Dieser Step nutzt ausschließlich bereits vorhandene Ports und
  Snapshot-/Match-Verträge; Gitea-Akquisition und die öffentliche MCP-Komposition
  folgen separat.
- **Split-Gate:** Ein primärer Provider-/Registry-Selection-Vertrag plus ein
  eng gekoppelter Lease-Scope, drei Schichten und sieben Akzeptanzkriterien.
  Mapping-Laden, Provider-Aufruf, Registry-Acquire, Match-/Selection-Projektion
  und der gemeinsame Support-Verbrauch bilden ein testbares Paket. Die zwei
  MCP-Assembly-Tools, ihre Registrierung und die beiden Host-Kompositionspunkte
  wären zusätzliche Verbraucher-/Ownership-Schichten und bleiben deshalb außen.

## Aktueller Projektzustand (JIT-Kontext)

Step 009 ist genehmigt. `AssemblySourceSelection` verknüpft ein bereits
  aufgelöstes Match mit einer `SourceSnapshotLease`; `AssemblyAnalysisContextRequest`
  kann diese Selection bereits an `AssemblyAnalysisContextFactory` weiterreichen.
  Die Factory prüft den Match nochmals defensiv, projiziert nur die gelieferte
  `ProjectId`-Compilation und behält den Decompilation-Pfad bei allen nicht
  nutzbaren Zuständen. Sie besitzt weder Provider- noch Registry-Lifecycle.

`ExternalSourceConfigurationLoader.Load(string?)` liefert einen unveränderlichen
  `ExternalSourceConfigurationLoadResult` mit validierten Mapping-Aliasen oder
  sichtbaren Lade-/Validierungsdiagnosen. `IExternalSourceProvider` ist der
  injizierbare Async-Port; `ExternalSourceProviderResult` kann einen bereits
  geladenen `ExternalSourceSnapshot` tragen, behauptet bei `IsAvailable=false`
  aber keinen Snapshot. `SourceSnapshotRegistry.Acquire` ist der einzige
  vorhandene Eintritt für Ownership/Deduplizierung; es gibt bewusst noch keinen
  Registry-Lookup nach Key.

`AssemblySourceMatchResolver.Resolve` verwendet ausschließlich die explizite
  Mapping-Identität und `Project.AssemblyName`. Die PE-Metadaten des DLL-Ziels
  liefert `AssemblyReferenceResolver.Resolve`, ohne die DLL zu laden. Der
  gemeinsame `AssemblyAnalysisToolSupport` wird derzeit nur von
  `InspectAssemblyTool` und `FindAssemblyExtensionsTool` genutzt; beide bauen
  `AssemblyToolExecutionParameters` mit dem legacy-Fallback-Aufruf und reichen
  noch keinen Source-Selector ein. Die beiden MCP-Registrierungen rufen diesen
  Pfad über `AnalysisToolCall.ExecuteAssemblyAsync` auf, das weiterhin nur den
  kanonischen Assembly-Pfad weitergibt.

Daraus folgt: Der nächste sichere Produktionsbaustein ist ein kleiner, von
  Loader-Result, Provider und Snapshot-Registry abhängiger Orchestrator sowie
  eine explizite Support-Überladung. Die bestehende Factory wird nur über ihren
  bereits genehmigten Request-Vertrag konsumiert. Die Lease muss den gesamten
  Zeitraum von der Factory-Projektion bis zum Result-Builder überleben und erst
  danach durch den äußeren Support-Scope freigegeben werden.

## Intention

`AssemblySourceSelectionOrchestrator` wählt aus einem bereits geladenen,
  validierten Mapping anhand der statisch gelesenen Assembly-Identität die
  passende Quelle, ruft genau für dieses Mapping den injizierten Provider auf,
  registriert einen gelieferten Snapshot und erzeugt daraus die bestehende
  `AssemblySourceSelection`. Ein kleiner disposable Scope hält die zugehörige
  Lease bis nach der Factory und dem Result-Builder; Provider- und Loaderdiagnosen
  bleiben dabei sichtbar.

`AssemblyAnalysisToolSupport` erhält eine neue interne, injizierbare
  Orchestrator-Überladung. Der vorhandene Aufruf ohne Orchestrator bleibt
  unverändert und liefert weiterhin die Decompilation. Der neue direkte
  Support-Pfad reicht eine gültige Selection an die bestehende Factory und fällt
  bei fehlendem Mapping, unavailable Provider, NoMatch, Ambiguous oder nicht
  nutzbarem Source-Projekt deterministisch auf dieselbe Decompilation zurück.

## Kontext-Handoff

### Invarianten

- Der Orchestrator konsumiert den `ExternalSourceConfigurationLoadResult` aus
  `ExternalSourceConfigurationLoader.Load`; bei ungültigem Ergebnis wird kein
  Provider aufgerufen und jede Loaderdiagnose wird in den Fallback-Kontext
  übernommen. Eine leere, gültige Konfiguration ist ein normaler No-Mapping-
  Fallback ohne Provider-Aufruf.
- Die Mapping-Wahl basiert auf dem metadata-only `AssemblyIdentityDto.Name`
  des Ziel-DLLs und einem case-insensitiven exakten Treffer in den bereits
  validierten Aliasen. DLL-Dateiname, Repositoryname, Projektname und Pfad sind
  keine Ersatzsignale; doppelte Alias-Mappings sind bereits ein ungültiger
  Loader-Result.
- Für ein gefundenes Mapping wird `IExternalSourceProvider.ResolveAsync` genau
  einmal mit diesem Mapping und dem CancellationToken aufgerufen. `false` oder
  ein fehlender Snapshot erzeugt keine Selection; Providerdiagnosen werden nicht
  verworfen. Der Orchestrator akquiriert keine Quelle und fängt keine Netzwerk-
  oder Gitea-Fehlersemantik vorweg.
- Ein vorhandener Provider-Snapshot wird genau über
  `SourceSnapshotRegistry.Acquire` geleast. `AssemblySourceMatchResolver.Resolve`
  und `AssemblySourceSelection.Create` bleiben die einzigen Match-/Identity-
  Grenzen. Der Orchestrator wählt kein Projekt und kopiert keine Solution.
- Der neue Scope besitzt nur die von ihm erworbene `SourceSnapshotLease` und
  gibt sie idempotent nach der Support-Ausführung frei. Die Registry bleibt
  Eigentümerin des `ExternalSourceSnapshot`; Support und Factory disposen weder
  Snapshot noch Registry.
- `AssemblyAnalysisToolSupport` verwendet den bestehenden
  `AssemblyAnalysisContextRequest` und reicht `SourceSelection` an
  `AssemblyAnalysisService.CreateContextAsync` weiter. Providerdiagnosen werden
  zusätzlich zu, nicht anstelle von, den Assembly-/Fallbackdiagnosen in den
  `AssemblyContext` übernommen.
- Der Scope bleibt bis nach `BuildResult` aktiv. Cancellation, Fehler und
  Result-Builder-Ausnahmen verlassen den Scope trotzdem über `using`; der alte
  Support-Aufruf ohne Selector bleibt ein unveränderter Decompilation-Fallback.
- Keine MCP-Registrierung, keine Daemon-/Stdio-Ownership, keine transitive
  Referenzauflösung, kein Runtime-Laden und keine Assembly-Ausführung wird in
  diesem Step eingeführt.

### Risiken

- Ein zu früh freigegebener Lease-Scope würde eine source-backed Compilation
  während der Result-Aufbereitung von ihrem Snapshot trennen. Der Scope muss
  deshalb im gemeinsamen Support-Overload um Factory-Aufruf und Builder liegen.
- Das Wiederholen der Alias-/Mappingnormalisierung neben dem Validator oder das
  direkte Projekt-Matching neben `AssemblySourceMatchResolver` würde die bereits
  genehmigten Verträge duplizieren. Der Orchestrator darf nur den validierten
  Assembly-Alias zur Mappingwahl und danach den bestehenden Resolver verwenden.
- Loaderdiagnosen, Providerdiagnosen und Assemblydiagnosen haben unterschiedliche
  Typen. Die Support-Grenze darf sie für `AssemblyContext.Diagnostics` nur mit
  einem stabilen, begrenzten Format zusammenführen und keine vorhandenen
  Assemblydiagnosen ersetzen.
- Eine Instanz darf Provider oder Registry nicht selbst besitzen oder beim
  Dispose schließen. Diese Ownership wird erst im Folgepaket an die MCP-/Daemon-
  Komposition gebunden.

### Relevante MCP-Symbole

- `T:AiNetLinter.Configuration.ExternalSourceConfigurationLoader` und
  `M:AiNetLinter.Configuration.ExternalSourceConfigurationLoader.Load(System.String)`
  — bestehender Loader für den expliziten Mapping-Result.
- `T:AiNetLinter.Configuration.ExternalSourceConfigurationLoadResult` und
  `T:AiNetLinter.Configuration.ExternalSourceMapping` — immutable Konfiguration,
  Mappings und strukturierte Diagnosen.
- `T:AiNetLinter.Mcp.Assemblies.IExternalSourceProvider`,
  `M:AiNetLinter.Mcp.Assemblies.IExternalSourceProvider.ResolveAsync` und
  `T:AiNetLinter.Mcp.Assemblies.ExternalSourceProviderResult` — injizierbarer
  Provider-Port und optionaler Snapshot-/Diagnosetransport.
- `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry` und
  `M:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry.Acquire` — zentrale
  Snapshot-Ownership- und Deduplizierungsgrenze.
- `T:AiNetLinter.Mcp.Assemblies.AssemblyReferenceResolver` und
  `M:AiNetLinter.Mcp.Assemblies.AssemblyReferenceResolver.Resolve` — statische
  Assembly-Identität für die Mappingwahl.
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceMatchResolver` und
  `M:AiNetLinter.Mcp.Assemblies.AssemblySourceMatchResolver.Resolve` — bestehende
  Project.AssemblyName-Matchentscheidung.
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelection` und
  `M:AiNetLinter.Mcp.Assemblies.AssemblySourceSelection.Create` — bestehende
  Selection-Projektion ohne Ownership-Übertragung.
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport`,
  `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisToolSupport.ExecuteAsync`
  und `M:...AssemblyAnalysisToolSupport.PrepareAsync` — gemeinsamer Support-
  Verbraucher der zwei direkten Assembly-Tools.
- `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisContextRequest` und
  `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisService.CreateContextAsync`
  — bereits vorhandener Factory-/Service-Einstieg für die Selection.

### Sicherer Einstiegspunkt

Zuerst `AssemblySourceSelectionOrchestrator.cs` mit Orchestrator und dem eng
  gekoppelten disposable Selection-Scope anlegen. Der Orchestrator soll den
  vorhandenen Loader-Result entweder direkt erhalten oder über eine kleine
  `CreateFromSettings`-Factory aus `ExternalSourceConfigurationLoader.Load`
  erzeugen; Provider und Registry bleiben injizierte, nicht besessene
  Abhängigkeiten. Danach `AssemblyAnalysisToolSupport.ExecuteAsync` um eine
  Orchestrator-Überladung erweitern und die bestehende Preparation intern so
  teilen, dass der Scope bis `BuildResult` reicht. Erst anschließend die direkte
  Support-Testklasse schreiben. Nicht in `AnalysisToolCall` oder die beiden
  Registrierungen einsteigen.

## Konkrete Änderungen

### Schicht 1 — Provider-/Registry-Selection und Lease-Scope

#### `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs` (neu)

- **Was:** Einen `AssemblySourceSelectionOrchestrator` mit injiziertem
  `ExternalSourceConfigurationLoadResult`, `IExternalSourceProvider` und
  `SourceSnapshotRegistry` definieren. Eine kleine `CreateFromSettings`-Factory
  darf ausschließlich `ExternalSourceConfigurationLoader.Load(settingsPath)`
  aufrufen und den Result unverändert übernehmen; sie führt keine MCP- oder
  Daemon-Komposition ein.
- **Was:** `ResolveAsync(string assemblyPath, CancellationToken)` zuerst über
  `AssemblyReferenceResolver.Resolve` metadata-only die Assembly-Identität
  bestimmen lassen. Bei ungültigem Loader-Result, fehlender Identität oder
  nicht gemapptem Assembly-Alias wird ein Fallback-Result ohne Provideraufruf
  erzeugt. Der Aliasvergleich verwendet die vom Validator gelieferte
  kanonische Form und bleibt case-insensitiv; keine Dateinamen-/Projektname-
  Heuristik ergänzen.
- **Was:** Für genau ein passendes Mapping den Provider aufrufen, dessen
  `ExternalSourceProviderResult.Diagnostics` übernehmen und nur bei
  `IsAvailable` plus nichtnulligem Snapshot `SourceSnapshotRegistry.Acquire`
  verwenden. Danach den bestehenden `AssemblySourceMatchResolver` mit Lease,
  Mapping und metadata-only AssemblyName aufrufen und die Rückgabe durch
  `AssemblySourceSelection.Create` projizieren. `NoMatch` und `Ambiguous` dürfen
  als Fallback-Selection transportiert werden; ein `Matched` ohne Candidate oder
  eine inkonsistente Selection bleibt ohne source-backed Auswahl.
- **Was:** Einen zweiten, eng gekoppelten `AssemblySourceSelectionScope` als
  `IDisposable` im selben Adaptermodul vorsehen. Er exponiert die optionale
  `AssemblySourceSelection` und strukturierte Loader-/Providerdiagnosen, besitzt
  die erworbene Lease und gibt sie genau einmal frei. Bei Ablehnung durch
  `AssemblySourceSelection.Create` muss auch die bereits erworbene Lease in
  diesem Scope landen und danach freigegeben werden.
- **Warum:** Die komplette Auswahlentscheidung wird vor der Factory gebündelt,
  ohne Snapshot-/Match-Verträge zu duplizieren. Der Scope verhindert, dass die
  source-backed Roslyn-Solution vor der Ausgabe oder bei einem Fehlerpfad
  vorzeitig aus dem äußeren Registry-Lifecycle fällt.

### Schicht 2 — Gemeinsamer Assembly-Tool-Support

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`

- **Was:** Eine neue interne Überladung
  `ExecuteAsync(AssemblyToolExecutionParameters, AssemblySourceSelectionOrchestrator)`
  ergänzen. Sie nutzt dieselbe absolute Pfad- und Loading-Prüfung wie der
  bestehende Overload, öffnet danach den Selection-Scope und hält ihn bis nach
  `AssemblyAnalysisService.CreateContextAsync(new AssemblyAnalysisContextRequest(...))`
  und `BuildResult`.
- **Was:** Den bestehenden Preparation-Code nur intern so faktorisieren, dass
  der Request mit `SourceSelection` an den vorhandenen Service-/Factory-Overload
  gelangen kann. Der bestehende `ExecuteAsync(parameters)` bleibt ohne Selector
  und reicht weiterhin den Request mit `null` weiter; keine neue Positions-
  parameterkette in `AssemblyToolExecutionParameters` einführen.
- **Was:** Loader-/Providerdiagnosen im Support-Adapter in stabile begrenzte
  Diagnosezeilen für `AssemblyContext.Diagnostics` überführen und mit den
  bereits gelieferten Assemblydiagnosen deduplizieren, ohne deren Status oder
  Inhalt zu ersetzen. Bei fehlender Selection läuft die Factory über ihren
  genehmigten Decompilation-Fallback.
- **Was:** Die vorhandene Preparation-/Error-Semantik, Cancellation und
  `BuildResult`-Callback-Signatur beibehalten. Der Scope muss auch bei
  Cancellation oder einer Exception des Factory-/Builder-Pfads zuverlässig
  freigegeben werden; Providerdiagnosen dürfen bei einem Fallback nicht
  verschwinden.
- **Warum:** Beide bestehenden direkten Assembly-Tools teilen bereits diesen
  Support. Eine einzelne injizierbare Verbrauchergrenze vermeidet zwei
  parallele Provider-/Lease-Kompositionen, ohne die MCP-Registrierung oder die
  Host-Ownership vorwegzunehmen.

### Schicht 3 — Deterministische direkte Support-Regressionen

#### `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs` (neu)

- **Was:** Mit `TestTempDirectory`, einer statisch emittierten kleinen Target-DLL,
  einer Adhoc-Source-Solution und einem Recording-Fake für
  `IExternalSourceProvider` den Orchestrator über einen expliziten
  `ExternalSourceConfigurationLoader.Load(settingsPath)`-Result aufbauen. Der
  Result-Builder prüft, dass ein gemapptes Assembly über das ausgewählte
  `Project.AssemblyName` source-backed wird, ein source-only Symbol vorhanden
  ist, Target-Identität erhalten bleibt und der Provider das richtige Mapping
  sowie CancellationToken erhalten hat.
- **Was:** Einen Scope-/Ownership-Test ergänzen, der während Factory und
  `BuildResult` eine lebende Selection sieht und nach dem Support-Aufruf die
  Lease idempotent freigegeben hat; die Registry- und Snapshot-Ownership endet
  erst an der bestehenden Registry-Dispose-Grenze. Ein doppelter Provider-
  Snapshot darf nicht als zweiter residenter Owner verbleiben.
- **Was:** No-Mapping und `IsAvailable=false`/Snapshot-null mit sichtbarer
  Providerdiagnose prüfen: Provider wird im No-Mapping-Fall nicht aufgerufen,
  der Fallback bleibt `decompiled`, und der bereits bestehende Assembly-
  Diagnosepfad wird nicht ersetzt. Einen ungültigen Loader-Result ohne Provider-
  Aufruf und mit sichtbarer Loaderdiagnose abdecken, sofern die Fixture ohne
  zusätzliche Infrastruktur bleibt.
- **Was:** Einen `Ambiguous`-Match und einen vorhandenen Snapshot ohne nutzbare
  Compilation als Fallback prüfen. Kein Test darf ein Projekt nach Name oder
  Dateipfad auswählen; alle Tests bleiben ohne Netzwerk, Gitea, Fremdprojekt-
  Restore, Assembly.Load, Reflection-Ausführung und AssemblyLoadContext.
- **Warum:** Die Tests verifizieren die neue vertikale Composition an der
  gemeinsamen Support-Grenze und halten die zwei höheren Toolpfade bis zum
  separaten MCP-Wiring-Paket unverändert.

## Tests

- [ ] `ExecuteAsync_WithConfiguredMappingPassesMatchedSelectionToFactory` —
  Loader-Result, Assembly-Identität, Provider-Mapping, Source-Compilation,
  source-only Symbol, Target-Identität und Source-Origin.
- [ ] `ExecuteAsync_HoldsSelectionLeaseThroughResultBuilderAndReleasesItOnce` —
  Selection bleibt während Factory/Builder gültig; Lease-Freigabe erfolgt
  nachher idempotent und Snapshot-Ownership bleibt bei der Registry.
- [ ] `ExecuteAsync_WithoutMappingSkipsProviderAndUsesDecompilationFallback` —
  kein Provideraufruf, `decompiled` bleibt unverändert.
- [ ] `ExecuteAsync_UnavailableProviderPreservesDiagnosticsAndFallsBack` —
  Providerdiagnose bleibt sichtbar; kein source-backed Context.
- [ ] `ExecuteAsync_InvalidConfigurationOrUnusableMatchFallsBackDeterministically` —
  Loaderdiagnose, Ambiguous bzw. fehlende Compilation erzeugen keinen beliebigen
  Source-Treffer.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolSupportTests"`
  läuft als deterministischer Component-Slice ohne Netzwerk, Fremdprojekt-
  Restore, Runtime-Laden oder Codeausführung.
- [ ] Abschlussverifikation: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.

## Definition of Done

- [ ] Ein injizierbarer Orchestrator konsumiert den validierten Loader-Result,
  wählt das Mapping nur über die statisch gelesene Assembly-Identität und ruft
  den Provider nur für einen eindeutigen Alias auf.
- [ ] Ein Provider-Snapshot wird über `SourceSnapshotRegistry.Acquire` geleast,
  durch den bestehenden Match-/Selection-Vertrag projiziert und bei Ablehnung
  oder Fehler nicht geleakt.
- [ ] Der disposable Selection-Scope hält die Lease bis nach Factory und
  `BuildResult`; weder Support noch Factory übernehmen Snapshot-/Registry-
  Ownership und jede Scope-Freigabe ist idempotent.
- [ ] Der neue Support-Overload reicht `AssemblySourceSelection` an die
  bestehende `AssemblyAnalysisContextFactory` und behält den alten nullbaren
  Fallback-Overload ohne Verhaltensänderung.
- [ ] Loader-/Providerdiagnosen bleiben neben Assemblydiagnosen sichtbar;
  No-Mapping, unavailable, NoMatch, Ambiguous und nicht nutzbare Compilation
  führen deterministisch zur vorhandenen Decompilation.
- [ ] Direkte Support-Regressionen sichern source-backed und Fallback inklusive
  Lease-Lifetime ohne Netzwerk, Runtime-Laden, Reflection, Gitea oder
  Fremdprojekt-Restore.
- [ ] MCP-Registrierung, Daemon-/Stdio-Wiring, Provider-/Registry-Ownership im
  Host und alle transversalen Folge-Epics bleiben unverändert; keine Änderung an
  `task-state.md`, `codemap.md`, früheren Steps oder TD-001/TD-002/TD-003.
- [ ] Build und beide vollständigen Nicht-Stress-Testläufe sind grün; ein
  reguläres `step-result.md` wird vom Coder erstellt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbol-, Referenz- und Impact-Fragen zuerst mit dem
  AiNetLinter-MCP und absolutem Projektroot prüfen; Textsuche bleibt Ergänzung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` und
  `#2 Architektur-Verbote` — immutable Werte, direkte kleine Composition,
  read-only Source-Snapshots, kein DI-/Plugin-Overhead und kein dynamisches
  Runtime-Laden, keine Reflection und kein AssemblyLoadContext.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrale `TestTempDirectory`, deterministische Test-Doubles und vollständige
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Result-/Fehlerzustände sichtbar halten, keine Diagnoseverluste und DRY-,
  MagicValues- oder DeadCode-Funde nur bei direktem, sicherem Bezug integrieren.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md#Split-Gate vor dem Coder`
  — höchstens zwei eng gekoppelte Verträge, drei Schichten, acht Kriterien und
  zwölf `read_first`-Dateien; MCP-/Daemon-Wiring wird separat geschnitten.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md#Step-Modus`
  — tatsächlichen Code und letzte Resultate/Reviews abgleichen, genau einen
  Step planen sowie Context-Budget und Handoff dokumentieren.

## Bekannte Ausnahmen

- Die echte Gitea-/Netzwerk-Akquisition ist nicht Teil dieses Steps. Der
  Provider wird nur über einen injizierten Fake bzw. den bestehenden
  `UnavailableExternalSourceProvider` beobachtet; die Host-Komposition und
  Provider-Fehlersemantik folgen in einem eigenständigen Paket.
- Die beiden MCP-Registrierungen und die beiden Hostpfade bleiben absichtlich
  beim bisherigen Aufruf. Der neue Support-Overload ist die konsumierbare,
  testbare Composition-Grenze für das Folgepaket; dadurch wird keine
  unvollständige globale Registry-Ownership vorweggenommen.
- `TD-001`, `TD-002` und `TD-003` werden nicht angefasst: Dieser Step ändert
  weder Drive-Path-Normalisierung noch `AssemblyOrigin` und eröffnet keine
  sichere gemeinsame Ablage oder Origin-Modellbereinigung.

## Code-Skizze (optional)

```csharp
using var source = await orchestrator.ResolveAsync(fullPath, ct);
var (context, error) = await AssemblyAnalysisService.CreateContextAsync(
    new AssemblyAnalysisContextRequest(
        fullPath,
        state?.GetCurrentSolution(),
        receiverType,
        source.Selection,
        ct));

// source bleibt bis nach BuildResult lebendig; danach gibt using die Lease frei.
```

## Notes

- Der Orchestrator darf keinen neuen Registry-Key-Lookup erfinden. Der Provider-
  Snapshot wird mit `SourceSnapshotRegistry.Acquire` registriert; die bestehende
  Registry-Deduplizierung entscheidet über den residenten Owner.
- Ein Mapping-Result aus dem Loader ist bereits validiert und normalisiert. Die
  Composition darf die Assembly-Identität defensiv trimmen, soll aber keine zweite
  Validator- oder Matchresolver-Implementierung daneben stellen.
- `AssemblyReferenceResolver.Resolve` ist für die Identitätswahl metadata-only;
  die Factory darf später nochmals denselben bestehenden Resolver für Target-
  Referenzen und Fingerprint verwenden. Eine Assembly wird zu keinem Zeitpunkt
  geladen oder ausgeführt.
- Ein `NoMatch`-/`Ambiguous`-Selectionwert kann für den Fallback weitergereicht
  werden, solange der Scope die Lease bis zur vollständigen Support-Antwort hält.
  `AssemblySourceSelection.Create` bleibt für die Konsistenzprüfung zuständig.
- Providerdiagnosen werden am Support-Rand in `AssemblyContext.Diagnostics`
  sichtbar gemacht, weil der genehmigte Factory-Request bewusst keine neue
  Diagnose-Property besitzt. Dafür werden keine Snapshot-, Match-, Origin- oder
  Fallback-Verträge geändert.
- Die vorhandene `AssemblyAnalysisToolSupport`-Gemeinsamkeit ist der Grund für
  genau einen Adapter: Eine parallele Implementierung in Inspect- und
  Extension-Tool würde den nächsten DRY-Drift erzeugen. Die zwei Toolpfade
  werden erst in der separaten MCP-Komposition an diesen Overload angeschlossen.
- DRY-, MagicValues- und DeadCode-Funde bleiben Tech-Debt. Nur ein unmittelbar
  im neuen Adapter entstehender, sicherer Duplikationsfund darf ohne weitere
  Vertragsgrenze opportunistisch bereinigt werden; TD-001/TD-002/TD-003 bleiben
  bewusst außerhalb.
- Der Coder erstellt nach der Umsetzung das reguläre `step-result.md` und den
  deutschen Implementierungscommit mit dem Task-Suffix; dieser Plan enthält
  keine Vorabplanung des MCP-/Daemon-Folgepakets.
