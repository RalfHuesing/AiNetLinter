---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 015
corrects: null
title: "Repository-Akquisitionsvertrag mit injizierbarem Gitea-Transport und sicherer Staging-Fassade"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T00:12:40+02:00
related_to:
  - step-014/step-result.md
  - step-014/step-review.md
  - follow-up-strategy.md
  - Konzept.md
  - roadmap.md
---

# Step 015: Repository-Akquisitionsvertrag mit injizierbarem Gitea-Transport und sicherer Staging-Fassade

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Vorgänger:** Step 014 ist mit Code-Commit `3f83c5f2`,
  Dokumentations-Commit `804f00b0` und Review-Commit `0902a7b7` genehmigt.
  Die typisierte Provider-Failure-Grenze ist damit abgeschlossen.
- **Konzept-Referenz:** `Konzept.md`, Phase 4; insbesondere explizite
  Repository-Identität, kontrollierte lokale Staging-Verzeichnisse,
  deterministische Test-Doubles und statische Analyse ohne Runtime-Laden.

## Split-Gate und Entscheidung

Die nächste sinnvolle Grenze ist die **initiale Repository-Akquisition**. Sie
bleibt ein zusammenhängendes vertikales Paket, weil der injizierbare
Transportvertrag ohne die besitzende Staging-Fassade keine testbare
Sicherheitssemantik hätte und die Fassade ohne Transport-Doppel keinen
deterministischen Akquisitionspfad nachweisen könnte.

Der Schnitt erfüllt die verbindlichen Gates:

- **Primäre Fachverträge:** genau zwei eng gekoppelte Verträge: der
  `IGiteaRepositoryTransport`-Port samt Ergebniswert und die
  `ExternalSourceRepositoryAcquirer`-Fassade samt Checkout-Handle. Die
  Ergebniswerte sind Bestandteil dieser beiden Verträge, kein dritter
  Infrastruktur-Port.
- **Betroffene Schichten:** (1) Transport-Port und typisierte Ergebnisse,
  (2) sichere Akquisitions-/Staging-Fassade, (3) FastTests/TestKit-basierte
  deterministische Tests.
- **Akzeptanzkriterien:** acht, siehe unten.
- **`read_first`:** zwölf Dateien, siehe `context_budget`.

Ein weiteres Teilen wäre an dieser Stelle nicht sinnvoll: Ein reiner
Transport-Port wäre kein vertikaler Nachweis, ein eigener Mini-Step nur für
die Test-Doubles würde die Fassade künstlich abtrennen. Die konkrete
Netzwerk-/Git-Implementierung bleibt trotzdem aus dem Ausführungsumfang
dieses Steps heraus, weil dieser Step keine echte Gitea-Ausführung erlaubt
und im Repository noch kein sicher eingeführter Git-/HTTP-Adapter existiert.

## Aktueller Projektzustand (JIT-Kontext)

`IExternalSourceProvider.ResolveAsync(ExternalSourceMapping,
CancellationToken)` und `ExternalSourceProviderResult` existieren bereits.
Step 014 hat die Failure-Arten
`ProviderUnavailable`, `AuthenticationRequired`, `AccessDenied`,
`RepositoryNotFound`, `NetworkUnavailable`, `Timeout` und `InvalidResponse`
typed und diagnostisch stabil gemacht. `UnavailableExternalSourceProvider`
liefert weiterhin einen snapshot-freien Fallback.

Die semantische MCP-Untersuchung zeigt:

- `AssemblySourceSelectionOrchestrator.ResolveAsync` ruft den Provider an
  und übernimmt dessen Failure-Kind/Diagnosen; die direkte Auswirkung liegt
  in `AssemblyAnalysisToolSupport` und dessen Tests.
- `ExternalSourceMapping` enthält nur Repository-URL, Solution-Pfad und
  Assembly-Aliase. `ExternalSourceConfigurationLoader` kennt keinen
  Credential-, Staging- oder Cache-Vertrag.
- `ExternalSourceSnapshot`, `SourceSnapshotIdentity` und
  `SourceSnapshotRegistry` besitzen bereits die spätere Snapshot-/Lease-
  Semantik. Sie sollen für diesen Akquisitionsvertrag nicht vorzeitig mit
  Checkout-Lebensdauer oder Refresh gekoppelt werden.
- In `src/AiNetLinter/Mcp/Assemblies` gibt es keinen produktiven
  Gitea-, `HttpClient`-, LibGit- oder Clone-/Fetch-Adapter. Die Textsuche mit
  `rg` fand nur URL-Beispiele und bestehende Assembly-/Decompilation-Refresh-
  Logik, nicht aber einen Repository-Akquisitionspfad.
- `TestTempDirectory` und `IsolatedFixtureLease` liefern bereits die
  zentrale, aufgeräumte Test-Temp-Infrastruktur. Ad-hoc-Temp-Pfade oder
  echte externe Repositories wären eine Regression der bestehenden
  Testregeln.

## Intention und Scope

Der Step definiert und implementiert einen internen Akquisitionsvertrag für
einen **neuen Checkout**:

1. `IGiteaRepositoryTransport` beschreibt einen injizierbaren Port für das
   Klonen des Default-Branch-Zustands in ein vom Aufrufer vorgegebenes,
   isoliertes Ziel. Der Port liefert einen geladenen Revisionswert oder die
   bereits vorhandenen typisierten Provider-Failure-Arten mit stabilen
   Diagnosen. Eine konkrete Netzwerk-/Git-Ausführung ist nicht Bestandteil
   des Steps; deterministische Doubles simulieren Erfolg, Teilfehler,
   Auth-/Transportfehler und Cancellation.
2. `ExternalSourceRepositoryAcquirer` besitzt die Staging-Wurzel, validiert
   Mapping und Zielpfad, erzeugt einen eindeutigen untergeordneten
   Checkout-Pfad und delegiert genau eine initiale Akquisition an den
   injizierten Transport. Der zurückgegebene Checkout-Handle enthält
   Checkout-Wurzel, verifizierten Solution-Pfad und geladene Revision und
   räumt ausschließlich seinen eigenen Staging-Besitz auf.
3. Die Fassade prüft nach erfolgreichem Transportlauf, dass der Checkout
   innerhalb der erlaubten Staging-Wurzel liegt und die konfigurierte
   Solution innerhalb dieses Checkouts auflösbar ist. Fehler und Cancellation
   lassen keinen halbfertigen, weiterverwendbaren Checkout zurück.
4. Die Tests verwenden ausschließlich `TestTempDirectory`,
   `IsolatedFixtureLease` und einen kontrollierten Transport-Doppel. Sie
   prüfen den Erfolgspfad, typisierte Fehler, Cancellation, Pfadgrenzen,
   Cleanup und die Nichtverwendung von Runtime-Assembly-Laden.

Die neue Fassade wird in diesem Step **noch nicht** an
`IExternalSourceProvider`, `AssemblySourceSelectionOrchestrator` oder
`AssemblyAnalysisHostComposition` verdrahtet. Die Provider-Integration muss
später zusätzlich einen `MSBuildWorkspace`-/`ExternalSourceSnapshot`-Bau,
Lease-Lifetime und Source-of-Truth-Entscheidungen tragen und ist deshalb
kein versteckter Teil der Akquisition.

## Konkrete Änderungen

### 1. Transportvertrag und Ergebnismodell

- Neue interne, schmale Schnittstelle unter
  `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs` (oder der
  vom bestehenden Namespace vorgegebenen gleichwertigen Datei).
- Der Vertrag nimmt das bestehende `ExternalSourceMapping`, einen bereits
  von der Fassade geprüften Zielpfad und `CancellationToken` entgegen. Der
  initiale Vorgang ist ein Clone des Default-Branch-Zustands; Fetch in einen
  vorhandenen Checkout wird nicht modelliert.
- Das Ergebnis enthält nur Akquisitionsdaten (Erfolg, geladene Revision,
  Failure-Kind und Diagnosen). Es erzeugt weder ein
  `ExternalSourceSnapshot` noch eine persistente Cache-Generation.
- Vorhandene `ExternalSourceProviderFailureKind`-Werte werden wiederverwendet;
  neue inline Fehler-Strings, Credentials oder parallele Fehler-Enums sind
  zu vermeiden. Falls Staging-spezifische Diagnosen nötig sind, werden ihre
  Codes an einer zentralen Stelle des neuen Vertrags definiert.

### 2. Sichere Akquisitions- und Staging-Fassade

- Neue interne Fassade unter
  `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` mit
  einem kleinen Checkout-Handle/Ergebnis als Teil desselben Fachvertrags.
- Vor dem Transportlauf: Mapping- und Solution-Pfad validieren, die
  Staging-Wurzel absolut und kanonisch bestimmen, einen eindeutigen Child-
  Pfad erzeugen und nachweisbar innerhalb der Staging-Wurzel halten.
- `..`, absolute Ausweichpfade, Pfadtrennzeichen-/Normalisierungsumgehungen,
  Reparse-/Symlink-Ausbrüche und ein Ziel außerhalb der Staging-Wurzel
  werden abgewiesen. Der Transport erhält keinen bereits existierenden
  Arbeitsbaum zur stillen Wiederverwendung.
- Nach dem Transport: Checkout-Wurzel und Solution-Datei erneut innerhalb
  der Staging-Wurzel verifizieren. Bei Fehler, Cancellation oder ungültigem
  Ergebnis den eigenen temporären Checkout best-effort bereinigen, die
  Cancellation aber unverändert weiterreichen und keine fremden Verzeichnisse
  löschen.
- Keine Credential-Konfiguration, kein Secret-Handling in Diagnosen, keine
  Cache-/Manifest-Datei, keine atomare Source-of-Truth-Veröffentlichung und
  kein Snapshot-/Workspace-Bau.

### 3. Deterministische Verifikation

- Neue fokussierte FastTests für die Fassade und den injizierten Transport-
  Doppel; die Tests werden als `Unit`/`Component` eingeordnet, nicht als
  Stress- oder Netzwerk-Tests.
- Fixture-Inhalte werden über `TestTempDirectory` bzw.
  `IsolatedFixtureLease` bereitgestellt. Ein Test-Doppel darf lokal in das
  von der Fassade übergebene Ziel schreiben, darf aber keinen Prozess, Git-
  Client, HTTP-Server oder externen Host starten.
- Die Tests müssen sowohl positive als auch negative Zustände explizit
  prüfen: Erfolg mit Revision und Solution-Pfad, jede relevante typisierte
  Provider-Failure-Gruppe, Cancellation, Clone-Fehler mit Cleanup sowie
  Pfad-/Reparse-Schutz.
- Die vorhandenen EPIC-03-Provider-/Orchestrator-Tests bleiben unverändert
  lauffähig; die neue Fassade wird nicht über einen Seiteneffekt in die
  Host-Komposition eingeschleust.

### Sichere Tech-Debt-Integration

`TD-001` bis `TD-003` werden nicht künstlich in diesen Step gezogen: Sie sind
nicht als `auto_fixable` markiert und betreffen entweder die spätere
Pfadidentitäts- oder Origin-Vertragsgrenze. Sicher in-scope ist ausschließlich
die Vermeidung neuer Drift: gemeinsame Pfadgrenzen, Staging-Konstanten,
Diagnosecodes und Test-Setup dürfen nicht mehrfach inline erfunden werden.
Vorhandene `PathNormalizer`- oder TestKit-Helfer werden auf Wiederverwendung
geprüft; eine neue kleine Hilfsmethode ist nur zulässig, wenn sie die
Akquisitionsgrenze tatsächlich teilt. Kein unabhängiger DRY-, MagicValues-
oder DeadCode-Sweep.

## Kontextbudget

### `read_first` (maximal 12 Dateien)

1. `tasks/decompiled-assembly-analysis/step-014/step-result.md` — genehmigter
   Ist-Vertrag der Failure-Grenze.
2. `tasks/decompiled-assembly-analysis/step-014/step-review.md` —
   Review-Grenzen und offene Risiken.
3. `tasks/decompiled-assembly-analysis/follow-up-strategy.md` — verbindliche
   Split-Gates und EPIC-04-Folgegrenzen.
4. `tasks/decompiled-assembly-analysis/Konzept.md` — Phase 4 und
   Test-/Sicherheitsleitplanken.
5. `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs` — bestehender
   Provider-Port, Result und Failure-Enum.
6. `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs` —
   unveränderter Mapping-Vertrag.
7. `src/AiNetLinter/Configuration/ExternalSourceConfigurationLoader.cs` —
   Nachweis, dass kein Credential-/Staging-Config-Vertrag vorgezogen wird.
8. `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs` —
   bestehende Provider-Aufruf- und Fallback-Grenze.
9. `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs` — spätere
   Snapshot-Identität und klare Abgrenzung zum Checkout-Handle.
10. `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` — bestehende
    Lease-/Lifetime-Semantik, die nicht dupliziert werden darf.
11. `src/AiNetLinter.TestKit/TestTempDirectory.cs` — zentrale sichere Temp-
    und Cleanup-Regeln für Tests.
12. `src/AiNetLinter.TestKit/IsolatedFixtureLease.cs` — vorhandene Fixture-
    Kopie und Besitzgrenze.

### `read_on_demand`

- `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisHostComposition.cs`,
  `AssemblyAnalysisToolSupport.cs` und die direkten Support-Tests nur für
  einen gezielten Wiring-/Regression-Check; sie dürfen nicht zum Anlass für
  Provider-Integration in diesem Step werden.
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotIdentity.cs`,
  `AssemblySourceMatchResolver.cs`, `AssemblyAnalysisSession.cs` und
  `ExternalSourceSnapshot`-Verbraucher, falls der Checkout-Handle mit
  späterer Snapshot-Erzeugung abgegrenzt werden muss.
- `src/AiNetLinter/Mcp/Assemblies/PathNormalizer.cs` und
  `FileSystemExclusionHelpers.cs`, falls die MCP-Prüfung eine sichere
  Wiederverwendung für Pfadkanonisierung oder Reparse-Schutz nahelegt.
- Bestehende FastTests für Provider-/Orchestrator-Verträge zur Regression;
  keine breitere Testordner-Lektüre ohne konkreten Symbolbezug.
- Projektdateien nur dann, wenn für ein vorhandenes Paket eine tatsächliche
  Abhängigkeit geprüft werden muss. Kein neues Git-/HTTP-Paket ohne separate
  Architekturentscheidung.

### `out_of_scope`

- konkrete `HttpClient`-/LibGit-/Prozess-Implementierung, echte
  Netzwerk-/Gitea-Aufrufe, externe Credentials und Secret-Speicherung;
- `ExternalSourceConfiguration`-/Mapping-JSON-Erweiterung für Credentials,
  Staging-Root, Default-Branch oder Cache;
- Fetch, Refresh, Retry-Policy, Branchwechsel und Wiederverwendung eines
  bestehenden Checkouts;
- persistenter Repository-Cache, Cache-Key-/Manifest-Integrität,
  Generation/Pointer und atomare Veröffentlichung;
- Erstellung von `ExternalSourceSnapshot`, `MSBuildWorkspace`,
  `SourceSnapshotRegistry`-Eintrag oder Assembly-Projekt-Matching aus dem
  akquirierten Checkout;
- Änderungen an `AssemblySourceSelectionOrchestrator`,
  `AssemblyAnalysisHostComposition`, `task-state.md`, `codemap.md` oder
  `tech-debt.md`;
- `Assembly.Load`, `AssemblyLoadContext`, Reflection-Ausführung,
  Decompilation-Ausführung, automatische externe Tests oder Stress-Tests.

## Invarianten

- Die bestehende explizite Mapping- und Provider-Grenze bleibt kompatibel;
  ein fehlender/noch nicht verdrahteter Akquirer erhält weiterhin den
  snapshot-freien Provider-Fallback.
- Jeder Akquisitionsversuch besitzt genau einen von der Fassade erzeugten,
  eindeutigen Checkout-Besitz und darf nur innerhalb der konfigurierten
  Staging-Wurzel schreiben oder löschen.
- Ein erfolgreicher Handle verweist auf eine existierende Solution innerhalb
  seines Checkouts und trägt eine nichtleere geladene Revision; ein
  fehlgeschlagener oder abgebrochener Versuch liefert keinen verwendbaren
  Handle.
- Cancellation bleibt Cancellation. Sie wird nicht als normaler
  Providerfehler maskiert und nicht still verschluckt.
- Diagnosen enthalten keine Credentials und bleiben auf die vorhandenen
  typisierten Failure-Arten bzw. zentralen Staging-Codes begrenzt.
- Der Step liest/produziert nur statische Dateien; weder Assembly-Code noch
  Reflection wird geladen oder ausgeführt.
- Snapshot-, Cache- und atomare Veröffentlichungsidentitäten bleiben
  getrennte spätere Verträge.

## Akzeptanzkriterien

1. Ein injizierbarer `IGiteaRepositoryTransport`-Port und sein
   Akquisitionsergebnis sind definiert; der Port kann Erfolg, geladene
   Revision, typisierte Provider-Failure-Arten und Cancellation ausdrücken.
2. Die Akquisitionsfassade nimmt ausschließlich den bestehenden Mapping-
   Vertrag plus eine kontrollierte Staging-Wurzel entgegen und legt keinen
   neuen öffentlichen Mapping-/Credential-Vertrag an.
3. Jeder erfolgreiche Clone liegt in einem eindeutigen Child der
   Staging-Wurzel; absolute Pfade, Traversal, Normalisierungsumgehungen,
   Reparse-/Symlink-Ausbrüche und vorhandene Arbeitsbäume werden abgewiesen.
4. Ein Erfolg liefert einen besitzenden Checkout-Handle mit verifiziertem
   Solution-Pfad und geladener Revision; der Handle bereinigt ausschließlich
   seinen eigenen Staging-Besitz.
5. Transportfehler, ungültige Clone-Ergebnisse und Cancellation führen zu
   sichtbaren, typisierten Ergebnissen bzw. echter Cancellation und lassen
   keinen wiederverwendbaren halbfertigen Checkout zurück.
6. Deterministische FastTests decken Erfolg, relevante Auth-/Transport-
   Fehlergruppen, Cancellation, Cleanup und Pfadgrenzen über vorhandene
   TestKit-Temp-/Fixture-Leases ab; kein Test führt Netzwerk, Git-Prozess,
   Gitea oder externes Restore aus.
7. Die EPIC-03-Provider-/Orchestrator-Regressionen bleiben grün; es gibt in
   diesem Step keine Snapshot-/Workspace-Erzeugung, Host-Verdrahtung,
   Refresh-, Cache- oder atomare Veröffentlichungslogik.
8. Neue Diagnose-/Pfadkonstanten und gemeinsam genutztes Test-Setup sind
   zentralisiert; kein sicherer, unmittelbar betroffener DRY-/MagicValues-/
   DeadCode-Befund bleibt im neu eingeführten Akquisitionspfad liegen.

## Tests und Verifikation

Während der Implementierung:

```powershell
dotnet test src/AiNetLinter.FastTests --filter Category=Unit
dotnet test src/AiNetLinter.FastTests --filter Category=Component
```

Vor der Übergabe:

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Keine Stress-Tests und kein Testlauf gegen Netzwerk, Gitea, Git oder externe
Restore-Quellen. Der Coder dokumentiert Test-/Build-Ergebnis und die
geprüfte Nichtverwendung von `Assembly.Load`/Reflection im Step-Result.

## Definition of Done

- Die zwei eng gekoppelten Vertragsanteile und die sichere Fassade sind
  implementiert, ohne den Step über die oben genannten drei Schichten zu
  erweitern.
- Erfolgs-, Fehler-, Cancellation-, Pfadschutz- und Cleanup-Verhalten sind
  mit deterministischen Doubles und dem zentralen TestKit verifiziert.
- Der bestehende EPIC-03-Fallback und alle vorgeschriebenen Nicht-Stress-
  Tests bleiben grün; `dotnet build` ist warnungsfrei.
- Es gibt keine echte Netzwerk-/Gitea-Ausführung, kein Assembly-Laden und
  keine Reflection; Refresh/Cache/atomare Veröffentlichung sind als
  Folgepakete abgegrenzt.
- Der Coder hinterlässt `task-state.md`, `codemap.md` und `tech-debt.md`
  unverändert; der Orchestrator übernimmt deren Status-/Indexpflege.

## Risiken und Gegenmaßnahmen

- **Pfad- oder Reparse-Escape:** Kanonische Root-/Child-Prüfung vor dem
  Transport, erneute Prüfung nach dem Clone, explizite negative Tests und
  Cleanup nur über den besitzenden Handle.
- **Halbfertige Clone-Artefakte:** transportbedingte Fehler und Cancellation
  führen zu keinem Erfolgshandle; der Fassade gehört die Bereinigung des
  eigens angelegten Ziels.
- **Fehlersemantik driftet vom Step 014 weg:** vorhandenes
  `ExternalSourceProviderFailureKind` wiederverwenden und neue Codes zentral
  halten; keine parallele Enum-Hierarchie.
- **Unbeabsichtigte Cache-/Snapshot-Vorwegnahme:** Ergebnis bleibt auf
  Checkout-Pfad, Solution-Pfad und Revision begrenzt; keine Registry,
  Generation, Manifest- oder Workspace-Erzeugung.
- **Testdrift oder externe Seiteneffekte:** TestKit-Temp/Fixture-Leases und
  ein deterministischer Transport-Doppel erzwingen lokale, reproduzierbare
  Tests; Netzwerk-/Git-Clients bleiben unreferenziert.
- **Überdehnung durch Credentials/Default-Branch-Konfiguration:** Der Port
  darf den Default-Branch-Zustand als fachliche Operation ausdrücken, aber
  keine Secrets oder neue Konfigurationsfelder einführen. Credential-Binding
  und produktiver Branch-/Transport-Adapter sind Folgepakete.

## Handoff an den Coder

Sicherer Einstiegspunkt ist die neue, isolierte Vertragsgrenze in
`src/AiNetLinter/Mcp/Assemblies`: zuerst Ergebnismodell und
`IGiteaRepositoryTransport`, danach `ExternalSourceRepositoryAcquirer`, dann
die fokussierten FastTests mit `TestTempDirectory`/`IsolatedFixtureLease`.

Vor dem Editieren die zwölf `read_first`-Dateien prüfen und für alle
C#-Symbole, Referenzen und Auswirkungen erneut AiNetLinter-MCP verwenden;
`rg` bleibt auf Textdateien und nicht-semantische Suchen beschränkt. Der
Coder darf die Host-/Orchestrator-Dateien nur für Regression prüfen, nicht
für Wiring ändern. Bei einem fehlenden sicheren Reparse-/Cleanup-Nachweis
ist der Step anzuhalten und die Grenzentscheidung im Result zu melden,
nicht durch Cache- oder Snapshot-Logik zu umgehen.

Übergabebericht muss enthalten: geänderte Code-/Testdateien, Vertrag und
Besitzmodell des Checkout-Handles, ausgeführte Befehle mit Ergebnissen,
Nachweis ohne Netzwerk/Gitea und ohne Assembly.Load/Reflection sowie offene
Folgegrenzen für Credential-Binding, produktiven Transport, Refresh/Cache und
atomare Veröffentlichung. `task-state.md`, `codemap.md` und `tech-debt.md`
bleiben dem Orchestrator vorbehalten.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — kurze fokussierte C#-Methoden,
  nullable/Result-Verträge, keine Reflection oder Runtime-Assembly-
  Ausführung.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Architektur ohne neue
  Plugin-/DI-Infrastruktur, sichere Temp-/Testregeln, DRY/MagicValues/
  DeadCode-Prävention und deutscher Commit-Stil.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — AiNetLinter-MCP für
  C#-Symbole/Referenzen/Impact, `rg` ausschließlich für Text und keine
  Assembly-Ladeoperationen.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/spec.md` — JIT-Kontext,
  Split-Gates, Besitz der Planungsartefakte und Folgepaket-Regeln.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` —
  sequenzieller Coder-/Review-Handoff und Zustandsübergabe.

## Bekannte Ausnahmen

- Der Planner aktualisiert absichtlich weder `task-state.md`, `codemap.md`
  noch `tech-debt.md`; das ist trotz der üblichen Step-Abschlussroutine eine
  ausdrückliche Task-Vorgabe.
- Der Step-Plan nimmt keinen konkreten Netzwerk-/Git-Client vorweg. Das ist
  keine offene Implementierung innerhalb dieses Steps, sondern die bewusste
  Sicherheitsgrenze für deterministische, netzwerkfreie Akquisitionstests.

## Notes

Die folgenden EPIC-04-Pakete bleiben ausdrücklich getrennt: produktive
Gitea-/Credential-Bindung und echte Clone-/Fetch-Semantik; danach
Refresh-/Cache-/Manifest- und atomare Source-of-Truth-Veröffentlichung;
anschließend Snapshot-/Workspace-Materialisierung sowie dirty/unbuilt-
und Fallback-/Health-Regeln. Diese Reihenfolge hält den nächsten Coder-
Kontext unter dem Compact-Risiko und bewahrt die bereits genehmigte
Provider-Failure-Grenze.
