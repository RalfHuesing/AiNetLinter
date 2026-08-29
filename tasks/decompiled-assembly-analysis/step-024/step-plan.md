---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 024
corrects: null
title: "Erfolgreiches Acquirer→Snapshot-/Workspace-Wiring mit besitzgebundener Lifetime"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T13:00:00+02:00
related_to:
  - ../step-023/step-result.md
  - ../step-023/step-review.md
  - ../roadmap.md
  - ../follow-up-strategy.md
  - ../Konzept.md
  - ../tech-debt.md
---

# Step 024: Erfolgreiches Acquirer→Snapshot-/Workspace-Wiring

## Bezug und Split-Gate-Entscheidung

Step 023 ist durch Review `30b13647` genehmigt. Die Prozessbaum-,
Handle- und Cleanup-Grenze ist damit geschlossen; sie wird in diesem
Step nur als unveränderliche Voraussetzung konsumiert.

Der aktuelle Code zeigt eine eindeutige nächste Vertragslücke:

- `ExternalSourceRepositoryAcquirer.AcquireAsync` liefert bei Erfolg
  bereits einen kontrollierten `ExternalSourceCheckoutHandle` mit
  `CheckoutPath`, validiertem `SolutionPath` und geladener Revision.
- `IExternalSourceProvider.ResolveAsync` wird vom Orchestrator bereits
  konsumiert, ist produktiv aber nur durch
  `UnavailableExternalSourceProvider` besetzt.
- `ExternalSourceSnapshot` verlangt eine `Solution` und einen
  `Workspace`, besitzt aber noch keinen Checkout-Besitz.
- `SourceSnapshotIdentity.Create` und `SourceSnapshotRegistry` sind
  vorhanden; eine erfolgreiche Materialisierung kann diese Verträge
  direkt verwenden.
- Der vorhandene `SourceFileCatalogLoader` zeigt den freigegebenen
  `MSBuildWorkspace`-Registrierungs-, Diagnose- und Dispose-Pfad.

**Gewählter primärer Vertrag:**

`IExternalSourceProvider.ResolveAsync(mapping, cancellationToken)` wird
für den erfolgreichen Acquirer-Fall implementiert. Ein verfügbares
Ergebnis transportiert genau einen vollständig materialisierten,
revisionsgebundenen `ExternalSourceSnapshot`; der Snapshot hält den
Checkout bis zu seiner eigenen bzw. der Registry-Lifetime. Ein
fehlgeschlagener Acquirer oder eine fehlgeschlagene Workspace-
Materialisierung liefert kein Snapshot und bleibt ein typisiertes,
diagnostisches Provider-Ergebnis.

Der Refresh-/Cache-/atomare-Source-of-Truth-Kandidat wird bewusst
abgelehnt: Es gibt aktuell weder einen produktiven Snapshot-Erfolgspfad
noch eine externe Snapshot-Cache-Generation, auf die Refresh sicher
aufsetzen könnte. Ihn jetzt vorzuziehen würde die Abhängigkeit umkehren
und einen künstlichen Infrastruktur-Sweep erzeugen.

Das Gate ist erfüllt:

- **ein primärer Vertrag:** Provider-Erfolg als Acquirer→Snapshot-
  Übergabe;
- **drei gekoppelte Schichten:** Provider-Adapter, Snapshot-/Workspace-
  Materialisierung mit Ownership, deterministische lokale Tests;
- **acht Abnahmekriterien:** siehe unten;
- **kein Host-Wiring:** Der bestehende Orchestrator-Port wird bedient,
  `AssemblyAnalysisHostComposition` bleibt standardmäßig beim
  `UnavailableExternalSourceProvider`;
- **kein transitive Referenzen-Paket:** Referenzauflösung, weitere
  Assembly-Tools und EPIC-05-Capability bleiben unangetastet.

## Ziel

Der Step soll den bestehenden Acquirer kontrolliert in einen nutzbaren
Source-Snapshot überführen. Der geladene Commit, der konfigurierte
repository-relative Solution-Pfad und die kanonische Repository-URL
bilden dabei dieselbe `SourceSnapshotIdentity`. Der Snapshot darf erst
als verfügbar zurückgegeben werden, wenn der Solution-Workspace aus dem
kontrollierten Checkout materialisiert und sein Besitz eindeutig
übertragen ist.

Der Pfad bleibt statisch: `MSBuildWorkspace` darf die Source-Solution
als Design-Time-Solution öffnen, aber der neue Code darf keine
`Assembly.Load`-, `AssemblyLoadContext`-, Reflection-Ausführung,
Build-, Restore- oder Testausführung fremder Repository-Artefakte
einführen.

## Kontextbudget

Der Coder liest zuerst höchstens die folgenden zwölf Dateien. Die Liste
ist der Split-Gate-Kontext und keine Aufforderung, das gesamte Repository
zu laden.

### `read_first` (maximal 12 Dateien)

1. `../step-023/step-result.md` — genehmigter Prozess-/Handle-Zustand.
2. `../step-023/step-review.md` — Invarianten und Review-Grenzen.
3. `../roadmap.md` — EPIC-04-Schnitt und Folgepakete.
4. `../follow-up-strategy.md` — Split-Gate und Vertragsgrenzen.
5. `../Konzept.md` — Phase-4-Zielbild und Source-of-Truth-Semantik.
6. `../tech-debt.md` — offene TD-001 bis TD-003 und erledigtes TD-005.
7. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
   — erfolgreicher Checkout- und Cleanup-Besitz.
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
   — Handle-, Revision- und Cleanup-Vertrag.
9. `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`
   — Provider-Ergebnis und Fehlerprojektion.
10. `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs`
    — Snapshot-Identity und Workspace-Lifetime.
11. `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs`
    — Lease-, Duplicate- und Registry-Dispose-Verhalten.
12. `src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs`
    — bestehender MSBuild-Registrierungs- und Workspace-Dispose-Pfad.

### `read_on_demand`

- `AssemblySourceSelectionOrchestrator.cs` und die vorhandenen
  Provider-/Host-Kompositionstests, nur für die bereits bestehende
  Aufruf- und Fallback-Kette.
- `ExternalSourceSnapshotTestFactory.cs`, damit neue Tests die
  bestehende AdhocWorkspace-Fixture wiederverwenden und keinen zweiten
  Snapshot-Builder erzeugen.
- `AssemblySourceMatchResolverTests.cs` und
  `SourceSnapshotRegistryTests.cs`, wenn Ownership- und Duplicate-
  Dispose-Regressionen ergänzt werden müssen.
- `SourceFileCatalog.cs`, die
  `SourceFileCatalogRegistrationPolicyTests.cs` und der lokale
  `SourceFileCatalogRegistrationTests`-Fixture, falls die gemeinsame
  Workspace-Erzeugung aus dem Loader herausgezogen wird.
- `GiteaGitRepositoryTransport.cs`,
  `IGiteaRepositoryTransport.cs` und die Credential-/Process-Verträge
  nur zur Bestätigung, dass keine Step-019/023-Grenze geändert wird.
- `AssemblyAnalysisSession.cs`, `AssemblyDecompilationCache.cs` und
  deren Tests nur zur Abgrenzung gegen Refresh und Decompilation-Cache.

### `out_of_scope`

- Refresh, Fetch, persistenter Repository-Cache, Cache-/Manifest-
  Integrität, Generationen, korrupte Snapshots und atomare
  Source-of-Truth-Veröffentlichung.
- Dirty-/unbuilt-Checkout-Erkennung, Restore, Build, Health-/Degraded-
  Zustände und eine neue Refresh- oder Fallback-Policy.
- Änderung der JSON-Konfiguration, Credentials, Auth-Resolver,
  HTTP-/Git-Transport-, Prozessbaum-, Handle- oder Native-Verträge.
- Änderung an `AssemblySourceSelectionOrchestrator`,
  `AssemblyAnalysisHostComposition`, MCP-Tool-Registrierung,
  transitive Referenzen und EPIC-05-Capability-Wiring.
- Assembly.Load/ALC/Reflection-Ausführung, Remote-/Gitea-/Git-Zugriffe
  in Tests und Testausführung von Quellcode aus dem Checkout.
- Globaler DRY-, MagicValues- oder DeadCode-Sweep sowie Entfernung
  bestehender Low-Confidence-Native-Interop-Felder.

## Scope und Architekturgrenze

### Schicht 1: Provider-Adapter (Produktion)

Neue interne Implementierung `GiteaExternalSourceProvider` von
`IExternalSourceProvider`. Sie erhält einen bestehenden
`ExternalSourceRepositoryAcquirer` und einen internen,
testbaren `IExternalSourceSnapshotMaterializer`-Seam.

Der Ablauf ist strikt:

1. Mapping und Cancellation prüfen und den Acquirer aufrufen.
2. Ein nicht verfügbares Acquirer-Ergebnis unverändert über
   `ExternalSourceProviderFailureProjection.FromUnavailableAcquisition`
   projizieren. Dadurch bleiben Step-018-1314-/Reparse-Capability und
   Step-019-HTTP-/Git-Klassifikation erhalten.
3. Ein verfügbares Ergebnis defensiv auf einen nicht verworfenen
   Checkout-Handle und eine nichtleere geladene Revision prüfen.
4. Den Handle an den Materializer übergeben. Der Besitz geht nur bei
   erfolgreich zurückgegebenem Snapshot an diesen über.
5. Einen verfügbaren `ExternalSourceProviderResult` mit dem Snapshot
   und den bereits sicheren Acquirer-Diagnosen liefern.

Materialisierungsfehler werden als nicht verfügbares Ergebnis mit der
vorhandenen `RepositorySolutionInvalid`-/`InvalidResponse`-Semantik
abgebildet; die Exception-Nachricht und Workspace-Pfade werden nicht
als neue öffentliche Diagnosequelle durchgereicht. Cancellation bleibt
eine echte `OperationCanceledException`. Bei jedem Fehler vor der
Besitzübertragung ist der Checkout-Handle zu disposen.

### Schicht 2: Snapshot-/Workspace-Materialisierung und Ownership

Neue interne `ExternalSourceSnapshotMaterializer`-Implementierung. Sie
öffnet ausschließlich `checkout.SolutionPath` mit dem vorhandenen
`MSBuildWorkspace`-Design-Time-Muster und baut die Identität über
`SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision)`.

Die vorhandene MSBuild-Registrierung in `SourceFileCatalogLoader` wird
als ein interner gemeinsamer Workspace-Erzeugungspfad nutzbar gemacht;
`RegisterMSBuild` und sein bestehendes `MsBuildRegistrationLock` bleiben
zentral. Der neue Materializer kopiert weder Registrierung noch
Umgebungsbereinigung. Die vorhandenen strukturellen Registration-Tests
müssen deshalb weiter gelten.

`ExternalSourceSnapshot` erhält einen optionalen
`ExternalSourceCheckoutHandle`-Owner, damit die bisherigen
AdhocWorkspace-Tests ohne Checkout kompatibel bleiben. Der Dispose-Pfad
disposet zuerst den Roslyn-Workspace und danach, auch bei einer
Workspace-Dispose-Exception, den Checkout-Handle. Er bleibt idempotent.
Die bestehende Registry muss dafür nicht semantisch umgebaut werden:
ihre vorhandenen Duplicate- und Registry-Dispose-Pfade disposen den
Snapshot und damit dessen Owner.

`WorkspaceFailed`-Ereignisse, ein fehlgeschlagenes Öffnen oder ein
ungültiges Ergebnis dürfen keinen partiellen Snapshot als verfügbar
markieren. Workspace-Besitz wird bei jedem fehlgeschlagenen Aufbau
geschlossen; die Provider-Schicht führt anschließend das kontrollierte
Checkout-Cleanup aus. Es gibt keinen Restore-/Build-Versuch.

### Schicht 3: Deterministische lokale Regressionen

- Neue Fast-Component-Tests für den Provider mit einem lokalen
  `IGiteaRepositoryTransport`-Double, `TestTempDirectory` und der
  vorhandenen `ExternalSourceSnapshotTestFactory`.
- Ein lokaler Integrationstest für den tatsächlichen
  `MSBuildWorkspace`-Materialisierungspfad mit einer minimalen,
  vollständig lokalen Solution. Der Test verwendet weder Gitea noch
  Git noch ein Netzwerk.
- Tests für Erfolg/Identity, Acquirer-Fehlerprojektion,
  Materialisierungsfehler, Cancellation, Handle-Lifetime und
  idempotenten Cleanup. Bestehende Provider-, Registry- und
  Fallback-Tests bleiben unverändert grün.

Es werden keine MCP-Host- oder Tool-Tests verdrahtet. Ein direkter
Provider-Test ist für diesen Vertrags-Step der ausreichende vertikale
Nachweis; die Host-Auswahl ist ein späterer, eigener Integrationsschnitt.

## Konkrete Änderungsflächen

Voraussichtlich betroffene Produktionsdateien:

- `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs`
  (neu): Acquirer→Provider-Erfolg, Fehlerprojektion und Ownership-
  Übergabe.
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceSnapshotMaterializer.cs`
  (neu): interner Test-/Materialisierungs-Seam, kein öffentlicher
  MCP-Vertrag.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs`
  (neu): MSBuildWorkspace-Load und Snapshot-Erzeugung.
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs`: optionaler
  Checkout-Owner und fail-safe Dispose-Reihenfolge.
- `src/AiNetLinter/Baseline/SourceFileCatalogLoader.cs`: nur die
  gemeinsame interne Workspace-Erzeugung extrahieren bzw. freigeben;
  keine Änderung der bisherigen Catalog-Semantik.

Voraussichtlich betroffene Testdateien:

- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs`
  (neu).
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs`
  (neu, lokal und netzwerkfrei).

`Docs/configuration.md` ist nicht zu ändern, solange weder CLI noch
JSON-Schema noch Credential-Konfiguration geändert werden. Interne
XML-Dokumentation für den neuen Ownership-Seam genügt. Eine Änderung
an der Roadmap erfolgt ausschließlich als präzise Step-024-Folge-
abgrenzung außerhalb der Coder-Produktionsänderung.

## Abnahmekriterien

1. Ein erfolgreicher Acquirer erzeugt über den Provider ein verfügbares
   `ExternalSourceProviderResult` mit genau einem nicht verworfenen
   `ExternalSourceSnapshot`; der bestehende Orchestrator-Vertrag bleibt
   unverändert.
2. Die Snapshot-Identity enthält exakt die kanonische Mapping-URL, die
   vom Acquirer geladene Revision und den konfigurierten
   repository-relativen Solution-Pfad; es gibt kein erneutes HEAD-
   Lesen oder alternatives Pfad-Resolving im Provider.
3. Die Materialisierung öffnet ausschließlich die validierte
   `checkout.SolutionPath` über den zentralen Design-Time-
   `MSBuildWorkspace`-Pfad und verwendet keine Assembly.Load-, ALC- oder
   Reflection-Ausführung sowie keinen Restore/Build.
4. Der Checkout bleibt bis zum Snapshot-/Registry-Dispose verfügbar;
   danach wird er genau einmal, idempotent und nach dem Workspace
   geschlossen. Ein Fehler vor der Besitzübertragung räumt den Handle
   ebenfalls auf.
5. Acquirer-Fehler, insbesondere Step-018-1314-/Reparse-
   Capability-Nichtverfügbarkeit, erreichen den vorhandenen typisierten
   Provider-Fallback ohne globale Repository-Sperre, neue Secrets oder
   neue Fehlerklassifikation.
6. Workspace-Fehler, ungültige Materialisierung und Cancellation liefern
   keinen verfügbaren partiellen Snapshot; Cancellation wird weiter-
   gereicht und alle bereits erworbenen lokalen Ressourcen werden
   begrenzt bereinigt.
7. Die neuen Fast-/Integration-Tests sind vollständig lokal:
   kein Remote-, Gitea-, Git- oder Netzwerkzugriff; die Test-Fixtures
   verwenden `TestTempDirectory` und keinen ad-hoc OS-Temp-Pfad.
8. Build, beide vollständigen Nicht-Stress-Testgates, die relevanten
   MCP-Violation-/Impact-Prüfungen und der scoped DRY-/MagicValues-/
   DeadCode-Check bleiben grün; es gibt keine Änderung an
   Prozessbaum-/Handle-Native-Sequenz oder Provider-Host-Wiring.

## Teststrategie

Während der Implementierung:

```text
dotnet test src/AiNetLinter.FastTests --filter Category=Component
dotnet test src/AiNetLinter.IntegrationTests --filter Category=Integration
```

Die Filter sind bei Bedarf auf die neuen Testklassen zu verengen. Ein
Test-Double darf nur den bereits injizierbaren Transport ersetzen; kein
Test startet `git`, kontaktiert Gitea oder nutzt Netzwerk. Der lokale
MSBuild-Test darf Roslyn/BuildHost für die Solution-Materialisierung
nutzen, aber nicht kompilieren, restaurieren oder Repository-Code
ausführen.

Vor Step-Abschluss sind zwingend auszuführen:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress-Tests bleiben ausgeschlossen. Privilegierte 1314-/Reparse-
Fälle dürfen nur entsprechend ihrer bestehenden Host-Capability
transparent als Skip erscheinen; normale lokale Repository-Fälle
dürfen dadurch nicht global blockiert werden.

## MCP-, DRY-, MagicValues- und DeadCode-Plan

Alle semantischen Abfragen verwenden den absoluten
`projectRoot`-Wert `C:/Daten/Entwicklung/Ralf/AiNetLinter`. `rg` bleibt
auf Dateinamen, Dokumenttext und Bannwortsuche begrenzt.

### MCP-Semantik

- Vor jeder Produktionsänderung `get_feature_context`/
  `get_symbol_body` für Provider, Acquirer, Snapshot, Registry und
  Workspace-Loader verwenden.
- Nach der Änderung `find_references` und `get_impact` für
  `IExternalSourceProvider.ResolveAsync`, den neuen Materializer und
  `ExternalSourceSnapshot.Dispose` prüfen. Der Impact darf nur die
  direkte Provider-/Snapshot-Kette erklären; keine transitive
  Referenzauflösung hinzufügen.
- `safeguard` bzw. `get_violations` auf die geänderten
  `Mcp/Assemblies`-/`Baseline`-Scopes anwenden. Neue Typen müssen die
  bestehenden Limits für Methodengröße, Parameter und Komplexität
  einhalten.
- Mit `search_pattern` gezielt nach `Assembly.Load`,
  `AssemblyLoadContext`, Reflection-Aufrufen, `dotnet restore`,
  `dotnet build`, `git` und Netzwerk-Clients in den neuen Dateien
  suchen; C#-Symbole selbst bleiben MCP-Semantik.

### DRY-Audit und Tech-Debt-Disposition

Der vorgezogene `find_duplicates`-Audit über `src` mit `minTokens=20`
fand keinen package-lokalen Exact-Clone. Der einzige relevante near-
Cluster liegt zwischen `ExternalSourceSnapshotTestFactory` und dem
privaten `AssemblySourceMatchResolverTests.CreateSnapshot`. Neue Tests
verwenden die vorhandene Fixture und führen keinen dritten Builder ein.
Der strukturelle Treffer zwischen
`GiteaGitRepositoryTransport.Failure` und
`ExternalSourceRepositoryTransportResult.Success` sind unterschiedliche
typed Result-Konstruktionen; sie werden nicht künstlich vereinheitlicht.
Unbeteiligte Exact-Cluster, insbesondere die beiden Assembly-Tool-
`ExecuteAsync`-Methoden, bleiben außerhalb dieses Vertrags-Scope und
werden nicht in Step 024 gezogen.

Die offenen IDs werden wie folgt berücksichtigt:

- `TD-001`: keine neue Pfadnormalisierung und kein zweiter Drive-Path-
  Validator; vorhandene `SourceSnapshotIdentity` verwenden.
- `TD-002` und `TD-003`: kein Origin-Vertrag in diesem Step, daher keine
  Änderung und keine vorgezogene Entfernung.
- `TD-005`: erledigt; URL-Policy, Transportklassifikation,
  Credentials und Prozesslebenszyklus bleiben unverändert.

### Magic Values

Der scoped `find_magic_values`-Audit für `SourceSnapshot` meldet nur die
bereits vorhandenen sechs langen Validierungs-/Exception-Texte und
keine neuen Config-/Constant-Kandidaten. Neue Provider-Diagnosen
verwenden bestehende `ExternalSourceConfigurationDiagnosticCodes`; neue
Timeouts, Statuscodes, Pfadtrenner oder Wire-Strings werden nicht
inline eingeführt. Falls die gemeinsame Workspace-Funktion einen neuen
stabilen Identifier benötigt, wird er lokal als benannte Konstante
begründet und nicht über einen globalen Constants-Sweep verschoben.

### Dead Code

Der scoped `find_dead_code`-Audit für die Snapshot-Dateien fand keinen
unreferenzierten Code. Der breitere Assemblies-Scan meldet ausschließlich
Low-Confidence-Native-Interop-Felder und andere dynamisch gebundene
Kandidaten; sie werden nicht gelöscht. Der neue Provider und
Materializer müssen durch jeweils einen Produktionsaufruf und lokale
Tests statisch nachvollziehbar referenziert sein.

## Invarianten und Risiken

- Die repository-spezifische 1314-/Reparse-Regel aus Step 018 bleibt
  eine Capability-Nichtverfügbarkeit für genau dieses Repository; es
  gibt keinen globalen Lockout.
- Step-019-HTTP-/Git-Fehlerklassifikation und Credential-Sanitizing
  laufen ausschließlich im Acquirer-/Transportpfad weiter.
- Die native Sequenz `CREATE_SUSPENDED → AssignProcessToJobObject →
  ResumeThread` und `KILL_ON_JOB_CLOSE` wird weder gelesen noch
  erweitert. Die Step-023-Handle-/Prozessgrenze ist nur Regression.
- Roslyn kann beim Workspace-Load BuildHost-/Dateihandles halten.
  Gegenmaßnahme: zentrale Registration, klarer Ownership-Transfer,
  Workspace vor Checkout und Dispose auch im Exception-Pfad.
- Eine scheinbar geladene, aber diagnostisch fehlgeschlagene Solution
  darf nicht als source-backed gelten. Gegenmaßnahme: kein Snapshot bei
  Load-Failure oder partieller Materialisierung; Decompilation-Fallback
  bleibt der bestehende Orchestrator-Pfad.
- Der Host verwendet nach diesem Step weiterhin den Unavailable-Provider.
  Das ist eine bewusste Integrationsgrenze und kein halbfertiges
  MCP-Wiring. Ein späterer Step muss den Provider separat in die
  Host-Komposition einführen.
- Neue Snapshot-Owner können die bestehende Registry-Deduplizierung
  sichtbar machen. Gegenmaßnahme: Duplicate-Dispose und Registry-
  Lifetime lokal testen, ohne Registry-Semantik neu zu erfinden.

## Definition of Done

- Die acht Abnahmekriterien sind nachweisbar erfüllt.
- Produktions- und Teständerungen bleiben auf die drei gekoppelten
  Schichten begrenzt; `Docs/configuration.md` bleibt wegen unveränderter
  Konfiguration unangetastet.
- MCP-Semantik, scoped `find_duplicates`, `find_magic_values` und
  `find_dead_code` sind dokumentiert und ohne offenen package-lokalen
  Exact-Clone oder High-Confidence-Dead-Code-Fund.
- Build und beide Nicht-Stress-Gates sind grün.
- Der Coder liefert einen Step-Result mit geänderten Dateien,
  Testbefunden, verbleibenden Skips und Ownership-/Fallback-Nachweis.

## Folgeaktion

Nach genehmigtem Step 024 ist der nächste eigene EPIC-04-Schnitt
Refresh/Fetch mit persistentem Repository-Cache, Cache-/Manifest-
Integrität und atomarer Source-of-Truth-Veröffentlichung. Dirty/unbuilt-
Checkout-Health und produktives Host-Wiring werden erst dort bzw. in
den ausdrücklich dafür geplanten Folgepaketen entschieden. Bei einem
Review-Fund bleibt eine Korrektur auf diesen Provider-/Snapshot-
Vertrag begrenzt.

## Exakte Coder-Hand-off-Anweisung

Lies zuerst die zwölf `read_first`-Dateien. Implementiere danach nur
den Vertrag `IExternalSourceProvider.ResolveAsync` als direkten
Acquirer→Snapshot-Adapter, die Snapshot-/Workspace-Materialisierung
und deren Owner-Lifetime sowie die genannten lokalen Tests.

Verwende den vorhandenen Acquirer unverändert, projiziere dessen
Fehler über `ExternalSourceProviderFailureProjection`, extrahiere den
MSBuildWorkspace-Erzeugungspfad ohne eine zweite Registrierung und
übertrage den Checkout-Besitz erst nach erfolgreicher Snapshot-
Materialisierung. Halte `AssemblyAnalysisHostComposition`,
`AssemblySourceSelectionOrchestrator`, Refresh/Cache/Manifest,
Credentials, Git-Prozess-/Native-Code und alle Remote-Tests außerhalb
der Änderung.

Führe nach den lokalen Tests die MCP-/DRY-/MagicValues-/DeadCode-
Prüfungen und anschließend `dotnet build` sowie beide vollständigen
Nicht-Stress-Gates aus. Erstelle keinen Commit und keinen Push im
Coder-Schritt; liefere ausschließlich den Result-/Statusnachweis an
den Orchestrator zurück.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc`
- `.agents/rules/AiNetLinterRichtlinien.mdc`
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`
- `.agents/Agent-Scaffolding/AGENTS.md`
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md`
- `.agents/skills/drift-audit/SKILL.md`

## Notes

Dieser Plan ist ein einzelnes vertikales EPIC-04-Paket. Er enthält keine
Produktionsimplementierung und keine Kritikerarbeit. Die Roadmap-
Markierung benennt lediglich die aktivierte Folgegrenze für Step 024.
