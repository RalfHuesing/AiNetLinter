---
status: done
type: step-plan
task: decompiled-assembly-analysis
step: 009
corrects: null
title: "Source-backed Assembly-Context mit deterministischem Decompilation-Fallback verbinden"
epic: EPIC-03
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-28T19:49:38+02:00
related_to:
  - step-008/step-result.md
  - step-008/step-review.md
  - src/AiNetLinter/Mcp/Assemblies/AssemblySourceMatchResolver.cs
context_budget:
  read_first:
    - "tasks/decompiled-assembly-analysis/step-008/step-result.md"
    - "tasks/decompiled-assembly-analysis/step-008/step-review.md"
    - "tasks/decompiled-assembly-analysis/codemap.md"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs"
    - "src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblySourceMatchResolver.cs"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyReferenceResolver.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs"
  read_on_demand:
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs — für die parallele Provenienz-/Hinweis-Ausgabe"
    - "src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs — nur zur Bestätigung, dass die reguläre Tool-Komposition noch keine Source-Auswahl übergibt"
    - "src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs und src/AiNetLinter/Mcp/AnalysisToolCall.cs — nur zur Abgrenzung des späteren Provider-/MCP-Adapters"
    - "src/AiNetLinter/Mcp/Assemblies/AssemblyFingerprint.cs — falls der source-backed Origin den statischen Binary-Fingerprint direkt benötigt"
    - "src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.cs — nur für bestehende Fallback- und Ausgabe-Konventionen"
    - "src/AiNetLinter.FastTests/Mcp/Assemblies/AssemblySourceMatchResolverTests.cs — nur für den Aufbau bereits gematchter Resolverwerte"
    - "src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs — nur für kleine Adhoc-Source-Solutions und Test-Referenzen"
    - "tasks/decompiled-assembly-analysis/tech-debt.md — Index und TD-001-Kontext; keine Pfadnormalisierung in diesem Step"
    - "tasks/decompiled-assembly-analysis/Konzept.md — Abschnitte Source-Auflösung vor der Dekompilation, Vertrauensstufen und Arbeitskontext/Cache-Grenze"
  out_of_scope:
    - "Änderungen an SourceSnapshotIdentity, ExternalSourceSnapshot, SourceSnapshotRegistry, SourceSnapshotLease, AssemblySourceMatchResolver oder ExternalSourceProviderResult"
    - "Gitea-Clone/Fetch, Authentifizierung, Branch-/Refresh-Logik, Netzwerk, lokale Source-of-Truth, Solution-Akquisition und persistenter Source-Cache"
    - "Neue AnalysisRegistry, allgemeine DI-/Plugin-Infrastruktur, AssemblyLoadContext, Assembly.Load, Reflection-Ausführung oder Fremdprojekt-Restore"
    - "Änderungen an AssemblyAnalysisSession, AssemblyDecompilationAdapter, AssemblyDecompilationCache, AssemblyReferenceResolver, transitive Referenzen oder Capability-Matrix"
    - "MCP-Registrierungen, AnalysisToolCall-/Daemon-Wiring und das automatische Ermitteln bzw. Leasen eines Source-Snapshots; der Step bietet nur die konsumierbare Factory-Grenze"
    - "Änderungen an Mapping-Schema, ExternalSourceConfiguration, appsettings.json, Docs, rules.json, task-state.md, codemap.md, tech-debt.md oder früheren Steps"
    - "TD-001, breite DRY-/MagicValues-/DeadCode-Sweeps, Binary-/PDB-/SourceLink-Versionsbeweis und alle transversalen Folge-Epics"
---

# Step 009: Source-backed Assembly-Context mit deterministischem Decompilation-Fallback verbinden

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-03` aus `roadmap.md` — Step 008 ist genehmigt und liefert
  die deterministische Zuordnung eines expliziten Assembly-Alias zu einem
  `Project.AssemblyName`; die Zuordnung wird bislang noch von keinem
  Assembly-Analyseverbraucher verwendet.
- **Konzept-Referenz:** `Konzept.md` „Source-Auflösung vor der Dekompilation",
  „Arbeitskontext und Cache-Grenze“, „Vertrauensstufen“ und Phase 3. Dieser
  Step verbindet nur ein bereits gematchtes, geleastes Source-Projekt mit dem
  bestehenden Assembly-Context und lässt bei jedem nicht nutzbaren Match die
  statische Decompilation bestehen.
- **Split-Gate:** Ein primärer Source-/Fallback-Vertrag, drei Schichten und
  acht Akzeptanzkriterien. Die Factory erhält einen bereits vorbereiteten
  Source-Selection-Wert; Provider-Akquisition, Registry-Lookup und MCP-
  Dispatch sind keine versteckten Bestandteile dieses Steps.

## Aktueller Projektzustand (JIT-Kontext)

Step 008 ist genehmigt. `AssemblySourceMatchResolver.Resolve` liefert einen
immutable `ExternalSourceMatchResult` mit `Matched`, `NoMatch` oder
`Ambiguous`, `ProjectId`, Snapshot-Identität, Kandidaten und Evidence. Der
Resolver besitzt weder einen Snapshot noch eine Lease; die Registry liefert
den `SourceSnapshotLease`, dessen `ExternalSourceSnapshot` die read-only
Roslyn-`Solution` und den Workspace-Owner hält.

`AssemblyAnalysisContextFactory.CreateAsync` erzeugt derzeit unabhängig von
jedem Source-Ergebnis mit `await using var session` eine neue
`AssemblyAnalysisSession`, ruft `RefreshAsync` auf und projiziert ausschließlich
`generation.Snapshot.Compilation` in `AssemblyContext`. Die Session besitzt
die aktuelle und alte Decompilation-Generation, Cache-/Referenzdiagnosen und
deren Workspace-Freigabe. Dieser Pfad ist der unveränderte Fallback und darf
nicht um Source- oder Providersemantik erweitert werden.

`AssemblyReferenceResolver` und `AssemblyFingerprintCalculator` lesen
Assembly-Identität, Referenzen und Binary-Hash metadata-only. Damit kann der
source-backed Pfad die Identität des analysierten DLL-Targets beibehalten,
ohne die Assembly zu laden. `AssemblyContext.Origin` ist aktuell ausschließlich
auf `decompiled` zugeschnitten; die beiden Assembly-Textformatter geben den
Dekompilationshinweis derzeit für jedes vorhandene Origin aus.

Der reguläre MCP-Dispatch in `AssemblyAnalysisToolRegistrations` übergibt
aktuell keine externe Source-Auswahl an `AssemblyAnalysisToolSupport` und ruft
die spezialisierten Tools mit `state: null` auf. Das ist eine bewusste nächste
Adaptergrenze: Dieser Step nimmt einen bereits gematchten Wert an, verdrahtet
aber weder Provider noch Registry-Lookup in die Toolregistrierung.

`tech-debt.md` enthält nur `TD-001` zur doppelten privaten
Drive-Path-Prüfung zwischen Mapping-Validator und Snapshot-Identität. Der
geplante Step normalisiert keinen solchen Pfad und berührt daher weder den
Befund noch seine Vertragsgrenzen.

## Intention

Ein kleiner interner `AssemblySourceSelection`-Wert verbindet das Ergebnis
des genehmigten Resolvers mit der zugehörigen `SourceSnapshotLease`. Die
Factory verwendet ausschließlich dann das `Project` aus dieser read-only
Solution, wenn der Wert einen konsistenten `Matched`-Kandidaten enthält und
dessen Compilation verfügbar ist. Bei `NoMatch`, `Ambiguous`, fehlender
Source-Auswahl (einschließlich unavailable) oder nicht verwendbarem
Source-Projekt wird der bestehende `AssemblyAnalysisSession`-Pfad ausgeführt.

Der source-backed `AssemblyContext` erhält die Compilation des ausgewählten
Source-Projekts, die statisch aus der DLL gelesene Target-Identität und die
Source-Snapshot-/Projekt-Provenienz. Er erhält keine Decompilation-Generation.
Die Factory gibt die vom Aufrufer übergebene Lease niemals frei; Snapshot-
Ownership und terminale Registry-Freigabe bleiben bei den bestehenden Typen
beziehungsweise beim äußeren Adapter.

## Kontext-Handoff

### Invarianten

- `AssemblySourceSelection` ist bereits für genau dieses Assembly-Ziel
  erzeugt; die Factory führt kein zweites Alias-Matching und keine
  `Project.Name`-/Dateinamen-Heuristik ein.
- Source-backed ist nur ein konsistenter Zustand aus `Matched`, nichtnulligem
  `MatchedCandidate`, identischer Snapshot-Identität und einem noch
  verfügbaren `Project` unter der gelieferten `ProjectId`. Alle anderen
  Zustände führen deterministisch zur Decompilation.
- Der source-backed Pfad liest ausschließlich
  `selection.SourceLease.Snapshot.Solution.GetProject(...)` und ruft die
  Compilation ab. Er verändert die Solution nicht und erzeugt keinen neuen
  Workspace-/Snapshot-Owner.
- Die Lease bleibt Eigentum des äußeren Aufrufers. `AssemblyAnalysisContextFactory`,
  `AssemblyAnalysisService` und die Fallback-Session dürfen die Lease nicht
  disposen; die Registry bleibt alleinige Eigentümerin der residenten
  Snapshot-Workspace-Freigabe.
- `AssemblyAnalysisSession` und ihr Cache-/Generation-/Decompilation-Pfad
  bleiben unverändert. Ein source-backed Treffer darf weder Decompilation
  noch einen neuen Decompilation-Cache-Eintrag auslösen.
- Target-Identität, Referenzliste und Binary-Hash stammen auch bei
  source-backed aus dem statischen Assembly-Pfad. Die Source-Compilation ist
  die Symbolquelle, aber kein Ersatz für die DLL-Identität.
- `consumerSolution` bleibt ausschließlich der Consumer-Kontext für die
  Receiver-Auflösung; die externe Source-Solution wird nicht als Consumer-
  Projekt oder zweites Änderungsziel verwendet.
- `OriginKind=source-backed` weist Source-Snapshot, Revision, Solution-Pfad
  und Source-Projekt aus. Der standardisierte Decompilation-Hinweis erscheint
  nur bei `OriginKind=decompiled`; bestehende Decompilationsergebnisse bleiben
  semantisch und textuell unverändert.

### Relevante MCP-Symbole

- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisContextFactory`
  und `M:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisContextFactory.CreateAsync`
  — aktuelle Verbrauchergrenze, die heute immer die Decompilation-Session
  erzeugt.
- `T:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisSession` und
  `M:AiNetLinter.Mcp.Assemblies.AssemblyAnalysisSession.RefreshAsync` —
  unveränderter statischer Fallback mit Generation/Cache-Ownership.
- `T:AiNetLinter.Mcp.Assemblies.AssemblySourceMatchResolver` und
  `T:AiNetLinter.Mcp.Assemblies.ExternalSourceMatchResult` — genehmigtes
  Match-Ergebnis, das nur konsumiert und nicht neu berechnet wird.
- `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry`,
  `M:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry.Acquire` und
  `T:AiNetLinter.Mcp.Assemblies.SourceSnapshotLease` — Snapshot-Ownership
  und Lease-Lebensdauer, die beim äußeren Adapter verbleiben.
- `T:AiNetLinter.Mcp.Assemblies.ExternalSourceSnapshot` — read-only
  Solution-Wert und Workspace-Owner des bereits geladenen Source-Snapshots.
- `M:AiNetLinter.Mcp.Assemblies.AssemblyReferenceResolver.Resolve` und
  `T:AiNetLinter.Mcp.Assemblies.AssemblyFingerprintCalculator` — metadata-only
  Target-Identität, Referenzen und Hash ohne Runtime-Laden.
- `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyContext` sowie
  `T:AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyAnalysisService` —
  gemeinsamer Result-/Servicepfad für source-backed und decompiled Symbole.

### Sicherer Einstiegspunkt

Mit dem kleinen `AssemblySourceSelection`-Vertrag neben dem bestehenden
Source-Matchcode beginnen. Danach nur die Provenienzfelder von `AssemblyOrigin`
ergänzen und die Quelle-oder-Fallback-Projektion in
`AssemblyAnalysisContextFactory.CreateAsync` einbauen; die bisherige
Session-Erzeugung als separaten, unveränderten Rückfallpfad erhalten.
Anschließend die Factory-Regressionen mit einer kleinen Adhoc-Solution zuerst
für `Matched`, dann für `NoMatch`/`Ambiguous` und null (unavailable/no source)
schreiben. Erst zum Schluss die zwei Textformatter auf den neuen Origin-Typ
begrenzen. Keine Änderung an Registry, Provider, Session, MCP-Registrierung
oder Solution-Akquisition beginnen.

## Konkrete Änderungen

### Schicht 1 — Source-Selection- und Provenienzvertrag

#### `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelection.cs` (neu)

- **Was:** Einen immutable `AssemblySourceSelection`-Wert aus
  `SourceSnapshotLease` und `ExternalSourceMatchResult` definieren. Er prüft
  beim Erzeugen die Identitätsübereinstimmung von Match und Lease; eine
  source-backed Auswahl setzt `Matched` und einen nichtnulligen Kandidaten
  voraus. `NoMatch` und `Ambiguous` dürfen als bereits geprüfte Fallback-
  Ergebnisse transportiert werden; unavailable bzw. kein Source-Ergebnis
  wird als `null` an die Factory gegeben.
- **Was:** Keine `IDisposable`-Weiterleitung auf dem neuen Wert einführen.
  Die Lease bleibt beim äußeren Aufrufer und wird nicht implizit an die
  Factory oder den `AssemblyContext` übertragen.
- **Was:** Im selben kleinen Adaptervertrag ein
  `AssemblyAnalysisContextRequest`-Parameter-Record für Assembly-Pfad,
  Consumer-Solution, Receiver-Typ, optionale Source-Selection und
  Cancellation vorsehen. Der bestehende Vier-Argument-Einstieg der Factory
  bleibt als Fallback-kompatible Weiterleitung erhalten; ein fünfter
  Positionsparameter wird nicht eingeführt.
- **Warum:** Der Resolver liefert bewusst Resultat und Registry-Lease
  getrennt. Eine kleine Konsistenzgrenze verhindert, dass die Factory einen
  Kandidaten aus einem fremden Snapshot als Source-Projekt verwendet, ohne
  Snapshot- oder Providerlogik zu duplizieren.

#### `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs`

- **Was:** `AssemblyOrigin` um optionale Source-Snapshot-Identität und
  Source-Projektpfad ergänzen und eine zentrale `IsDecompiled`-Abfrage
  anbieten. Der bestehende positional decompiled-Aufruf bleibt mit seinen
  bisherigen Werten gültig; source-backed verwendet einen leeren
  `GeneratedDocumentPath`, einen statischen Target-Hash und die gewählte
  Source-Projekt-/Snapshot-Provenienz.
- **Warum:** Die gemeinsamen Assembly-Resultmodelle sollen die Herkunft
  maschinenlesbar unterscheiden, ohne eine zweite Tool-spezifische Origin-
  Struktur oder einen falschen generierten Pfad zu erfinden.

### Schicht 2 — Factory-Projektion und Herkunftsausgabe

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs`

- **Was:** Den neuen `AssemblyAnalysisContextRequest` an einer zusätzlichen
  internen Factory-Überladung annehmen und den bestehenden Vier-Argument-
  Einstieg unverändert auf diese Anfrage mit null Source-Selection
  weiterleiten. Bei konsistentem `Matched` zuerst den `ProjectId` in der
  geleasten read-only Solution auflösen und dessen Compilation abrufen. Für
  diesen Pfad die statische
  `AssemblyReferenceResolver`-/Fingerprint-Information des DLL-Targets
  übernehmen und daraus einen source-backed `AssemblyContext` mit
  `Generation=0`, Source-Origin und sichtbaren Referenzdiagnosen bilden.
- **Was:** Wenn Selection null ist, der Match `NoMatch`/`Ambiguous` lautet,
  die Snapshot-Identität/Lease nicht verwendbar ist, das `ProjectId` fehlt
  oder die Compilation nicht verfügbar ist, ohne Exception in den heute
  bestehenden `AssemblyAnalysisSession`-Aufbau verzweigen. Der Fallback
  verwendet dieselbe Session-, Cache-, Status- und Diagnosepipeline wie
  bisher; ein Fallback-Grund darf als deterministische, begrenzte Diagnose
  ergänzt werden, darf aber die bestehenden Decompilationdiagnosen nicht
  ersetzen.
- **Was:** `FindConsumerReceiverAsync` unverändert auf `consumerSolution`
  anwenden. Die externe Source-Solution wird nicht nach Consumer-Typen
  durchsucht und nicht als zweites Projektziel behandelt. Die Factory gibt
  die übergebene Lease in keinem Erfolgs-, Fallback-, Cancellation- oder
  Fehlerpfad frei.
- **Warum:** Die Quelle des Assembly-Targets und der Consumer-Kontext müssen
  getrennt bleiben. Ein source-backed Match soll die vorhandene Source-
  Compilation verwenden, während no-match, ambiguous, unavailable und
  beschädigte/fehlende Source-Projektionen zuverlässig auf die bereits
  bewährte statische Decompilation zurückfallen.

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs`

- **Was:** Eine dünne Request-Weiterleitung zur Factory ergänzen; der
  bestehende Vier-Argument-Aufruf bleibt kompatibel und nutzt null. Die
  Request-Überladung bündelt den neuen Source-Selection-Input statt die
  bestehende Service-Signatur um weitere Positionsparameter zu verbreitern.
- **Warum:** Der spätere Adapter kann den Factory-Vertrag ohne erneute
  Service-Signatur umgehen. Der Service erhält keine Provider-, Registry-
  oder MCP-Abhängigkeit.

#### `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyTool.cs` und
`src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/FindAssemblyExtensionsTool.cs`

- **Was:** Die Herkunftsausgabe auf `AssemblyOrigin.IsDecompiled` stützen.
  Source-backed Ergebnisse zeigen Source-Projektpfad und Snapshot-Provenienz
  ohne Decompilation-Hinweis; dekompilierte Ergebnisse behalten exakt den
  bisherigen Herkunftstext und den Hinweis auf mögliche Abweichungen.
- **Warum:** Ein später über den gleichen Context laufendes Tool darf die
  gematchte Originalquelle nicht als dekompiliert ausgeben. Es wird keine
  neue MCP-Registrierung und kein Provider-Aufruf ergänzt.

### Schicht 3 — Direkte Factory-/Ownership-Regressionen

#### `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactoryTests.cs` (neu)

- **Was:** Mit `TestTempDirectory`, einer kleinen statisch emittierten
  Target-DLL, `AdhocWorkspace`, `SourceSnapshotRegistry` und dem vorhandenen
  Resolveraufbau testen, dass ein `Matched`-Selectionwert die Compilation
  des ausgewählten Source-Projekts (einschließlich eines source-only
  Symbols) verwendet, die Target-Identität bewahrt und `source-backed` mit
  hoher Confidence sowie Snapshot-/Projektprovenienz meldet.
- **Was:** Einen `NoMatch`- und einen `Ambiguous`-Selectionwert jeweils
  gegen dieselbe DLL prüfen. Beide müssen die dekompilierte Target-Compilation
  liefern, kein beliebiges Source-Projekt auswählen und den bestehenden
  `decompiled`-Origin behalten. Ein null-Selection-Fall repräsentiert den
  unavailable-/nicht verfügbaren Source-Pfad und muss denselben Fallback
  verwenden.
- **Was:** Den Lease-/Registry-Besitz prüfen: Nach source-backed Erfolg und
  nach Fallback bleibt der Snapshot bis zur kontrollierten Registry-Freigabe
  nicht disposed; die Factory gibt weder Lease noch Snapshot frei. Die
  Registry-Dispose bleibt idempotent und beendet den Snapshot-Owner erst an
  ihrer bestehenden terminalen Grenze.
- **Was:** Cancellation, fehlende Assembly oder nicht auflösbares
  Source-Projekt nur dann als Fallback-/Fehlerpfad abdecken, wenn der Test
  ohne zusätzliche Akquisitions- oder Provider-Infrastruktur deterministisch
  aufgebaut werden kann. Kein Test lädt die Target-DLL in den Prozess.
- **Warum:** Die zentrale Entscheidung wird direkt am Factory-Vertrag
  geprüft. Provider, Gitea, MCP-Host und transitive Referenzen bleiben aus
  dem Testkontext heraus, während die bestehende Decompilation als
  sicherer Rückfall regressionsgesichert bleibt.

## Tests

- [ ] `CreateAsync_UsesMatchedReadOnlySourceProjectWithoutDecompilation` —
  Source-Compilation, source-only Symbol, Target-Identität,
  `source-backed`-Origin sowie Snapshot-/Projektprovenienz.
- [ ] `CreateAsync_NoMatchAndAmbiguousUseExistingDecompilationFallback` —
  beide Resolverzustände wählen kein Source-Projekt und behalten den
  bestehenden `decompiled`-Origin.
- [ ] `CreateAsync_WithoutSourceSelectionRepresentsUnavailableFallback` —
  fehlendes/unavailable Source-Ergebnis führt ohne Provider-Aufruf zur
  unveränderten statischen Decompilation.
- [ ] `CreateAsync_DoesNotReleaseSourceLeaseOrSnapshotOwnership` — Lease,
  Snapshot und Registry werden erst durch den äußeren, idempotenten
  Registry-/Lease-Lifecycle freigegeben.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisContextFactoryTests"`
  läuft deterministisch ohne Netzwerk, Fremdprojekt-Restore,
  Assembly.Load, Reflection-Ausführung oder AssemblyLoadContext.
- [ ] Bestehende Assembly-Tool- und Session-Regressionen bleiben grün; die
  bestehende Decompilation, Cache-Generation und Partial-/Degraded-Semantik
  werden nicht abgeschwächt.
- [ ] Abschlussverifikation: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.

## Definition of Done

- [ ] Ein kleiner immutable Source-Selection-Vertrag verbindet ein bereits
  geprüftes Match mit der passenden Registry-Lease, ohne Ownership an die
  Factory zu verschieben.
- [ ] Nur ein konsistenter `Matched`-Kandidat wird als read-only
  Source-Compilation verwendet; Snapshot-Identität und `ProjectId` bleiben
  die einzigen Auflösungsanker.
- [ ] `NoMatch`, `Ambiguous`, unavailable/null sowie fehlendes oder nicht
  kompilierbares Source-Projekt fallen deterministisch auf die bestehende
  statische Decompilation zurück; kein Zustand wählt einen beliebigen
  Kandidaten.
- [ ] Source-backed und decompiled `AssemblyContext`-Werte bewahren die
  Target-Identität; Origin, Snapshot-/Projektpfad und der Decompilation-
  Hinweis werden nicht verwechselt.
- [ ] Die Factory und der Service geben Source-Leases nicht frei; die
  Registry bleibt Eigentümerin der Snapshot-Workspace-Freigabe und wird nur
  an ihrer bestehenden terminalen Dispose-Grenze wirksam.
- [ ] Consumer-Auflösung bleibt auf `consumerSolution` begrenzt; es entsteht
  keine versteckte Cross-Target- oder transitive Referenzsemantik.
- [ ] Session, Provider-Akquisition, Registry-Lookup, MCP-Registrierung,
  Gitea/Netzwerk, Assembly-Ausführung und allgemeine DI-/Plugin-Infrastruktur
  bleiben unverändert außerhalb dieses Steps.
- [ ] Build und beide vollständigen Nicht-Stress-Testläufe sind grün; keine
  Änderungen an `task-state.md`, `codemap.md`, früheren Steps oder `TD-001`
  wurden eingeführt.

## Rules-Refs

- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Symbol-, Referenz- und Impact-Fragen zuerst mit
  absolutem Projektroot über AiNetLinter-MCP prüfen; Textsuche bleibt
  Ergänzung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1 Grundprinzipien` und
  `#2 Architektur-Verbote` — immutable Werte, read-only Source-Snapshots,
  keine Runtime-Ladung, keine Reflection/ALC, keine neue DI-/Plugin-
  Infrastruktur und direkte kleine Lösung.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3,
  zentrale Test-Temp-Infrastruktur, deterministische Test-Doubles und
  vollständige Nicht-Stress-Gates.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#agent-resilience` —
  kurze Factory-Methoden, bei Bedarf Parameter-Record, nullable enable,
  keine stillen Catch-Pfade und keine blockierenden Task-Zugriffe.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  Source/Fallback-Logik direkt bündeln, Magic Values nicht duplizieren und
  DRY-/DeadCode-Befunde nur im betroffenen Vertrag opportunistisch anfassen.
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md#Split-Gate vor dem Coder`
  — ein vertikales, testbares Paket mit höchstens drei Schichten und acht
  Akzeptanzkriterien; Provider-/MCP-Komposition bleibt ein Folgepaket.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md#Step-Modus`
  — Ist-Code vor JIT-Planung lesen, genau einen Step planen und
  `context_budget`/Handoff dokumentieren.

## Bekannte Ausnahmen

- Dieser Step verbindet nur einen bereits gematchten und geleasten
  Source-Kontext. Er lädt keine Source-Solution, ruft keinen Provider auf und
  löst keinen Registry-Key selbst auf. Das reguläre MCP-Assembly-Target bleibt
  bis zum Folgepaket beim bisherigen Decompilation-Pfad.
- Ein source-backed Context verwendet `Generation=0`, weil er keine
  Decompilation-Generation besitzt. Eine spätere residente gemeinsame
  Session-/MCP-Komposition muss dafür einen eigenen Lifecycle-Vertrag
  definieren und ist nicht Teil dieses Steps.
- Der Step beweist keine Binary-/PDB-/SourceLink-Versionstreue. Der statische
  Target-Hash und die Source-Snapshot-Identität werden nur sichtbar gehalten;
  der Versions-/Refresh-Vertrag folgt in EPIC-04.
- `TD-001` wird nicht angefasst: Die geplante Factory-Projektion berührt keine
  der beiden privaten Drive-Path-Prüfungen und würde keine sichere gemeinsame
  Ablage eröffnen.

## Notes

- Die null-Selection-Repräsentation für unavailable ist absichtlich eine
  Eingangsgrenze, keine neue Provider-Semantik. Ein späterer Adapter muss
  Providerdiagnosen separat sichtbar weiterreichen und die Lease bis zum
  Ende der Ergebnisverarbeitung halten.
- `ExternalSourceMatchResult` bleibt der fachliche Matchvertrag aus Step 008;
  `AssemblySourceSelection` verknüpft ihn lediglich mit dem vom äußeren
  Aufrufer kontrollierten Lease und darf keine neue Projektwahl vornehmen.
- `AssemblyAnalysisSession` bleibt der einzige Fallback-Builder. Eine
  source-backed Compilation wird direkt aus dem geleasten Roslyn-Project
  gelesen und nicht in einen zweiten Adhoc-Workspace kopiert.
- Die vorhandenen Formatter teilen derzeit den Decompilation-Hinweis. Die
  Anpassung auf `IsDecompiled` ist die kleinste notwendige Provenienzkorrektur
  und keine allgemeine Tool-Komposition.
- DRY-, MagicValues- und DeadCode-Funde bleiben Tech-Debt. Im geplanten
  Source-/Fallback-Code darf nur eine unmittelbar betroffene, architektonisch
  sinnvolle Bereinigung mitlaufen; kein Audit- oder TD-001-Sweep.
- Der Coder erstellt nach der Umsetzung das reguläre `step-result.md` und den
  deutschen Implementierungscommit mit dem Suffix
  `[decompiled-assembly-analysis]`; dieser Plan enthält keine Provider-/MCP-
  Folgeplanung.
