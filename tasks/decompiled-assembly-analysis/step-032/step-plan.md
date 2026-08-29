---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 032
corrects: null
title: "Validated Refresh/Fetch in neue Cache-Generation"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T22:56:09+02:00
related_to:
  - step-029/step-plan.md
  - step-031/step-review.md
---

# Step 032: Validated Refresh/Fetch in neue Cache-Generation

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` aus `roadmap.md` — Gitea bleibt die Source of Truth;
  ein fälliger, validierter Source-Cache darf nicht still als aktuell gelten.
- **Vorgänger:** `step-026` bis `step-028` haben Write-through, Manifest,
  Inventory, Generation und Current-Pointer atomar abgesichert. `step-029`
  bis `step-031` haben den strikt validierten Current-Reuse-Vertrag mit
  request-eigenem Checkout und grünen Gates abgeschlossen.
- **Konzept-Referenz:** `Konzept.md`, Abschnitte zu Refresh-Intervall,
  Source-Solution-Manifest, atomarer Generation und Decompilation als
  Fallback.
- **Review-Referenz:** `step-031/step-review.md` (`d8cff007`, approved).

## Split-Gate-Entscheidung

Der größtmögliche noch kontextstabile vertikale Vertrag ist ein einzelner
Refresh-/Fetch-Schnitt mit genau einem primären Vertrag:

> Ein strikt validierter Current-Eintrag wird anhand einer injizierten
> Refresh-Policy als aktuell oder fällig entschieden. Bei Fälligkeit wird
> der Snapshot in einen neuen request-eigenen Checkout materialisiert, über
> den bestehenden injizierten Git-Prozesspfad aktualisiert und erst nach
> vollständiger Validierung als neue Cache-Generation atomar veröffentlicht.
> Jeder Fehler lässt den alten Current-Pointer unverändert und führt nicht
> zu einer stillen Verwendung des veralteten Snapshots.

Der Vertrag hat höchstens drei gekoppelte Schichten:

1. **Staleness-/Policy-Entscheidung:** `CreatedUtc` der validierten
   Generation ist der letzte erfolgreiche Publish-/Refresh-Zeitpunkt. Eine
   intern injizierbare Policy verwendet zunächst den in `Konzept.md`
   beispielhaft gesetzten Default von 60 Minuten und eine testbare Uhr.
   `appsettings.json` und öffentliche Konfigurationsbindung bleiben außen.
2. **Fetch-/Acquirer-Pfad:** Der bestehende `IGiteaRepositoryTransport`
   erhält einen Default-Branch-Fetch auf einem neuen, besitzgebundenen
   Checkout. Credentials, Prozess-Timeout, Cancellation, Fehlerklassifikation
   und geheimnisfreie Diagnostik bleiben dieselben Invarianten wie in
   Steps 019–023.
3. **Generation-/Rollback-Integration:** Der validierte Fetch wird über den
   bestehenden Cache-Writer in eine neue Generation geschrieben. Eine für
   Refresh-Publishes verpflichtende erwartete Current-Generation verhindert,
   dass ein zwischenzeitlich aktualisierter Current-Pointer von einem älteren
   Refresh überschrieben wird; der Writer bleibt die einzige atomare
   Publish-Grenze. Für den bestehenden Clone-Pfad bleibt die Vorbedingung
   leer.

Damit bleiben die gemeinsamen Findings und Regressionen von Policy, Fetch
und Publish in einem größeren Paket. Ein Compact-Split ist nur zulässig,
wenn die tatsächliche Implementierung die Grenze überschreitet: Host-/MCP-
Wiring, öffentliche Cache-Konfiguration, Retention/GC/Invalidierung sowie
Health-/degraded-/dirty-/unbuilt-Policy sind eigenständige schwere Verträge
und werden nicht in diesen Step gezogen. Es gibt keinen Mini-Step für
einzelne Testfälle, Git-Kommandos oder Diagnosecodes.

## Aktueller Projektzustand (JIT-Kontext)

Die semantische MCP-Prüfung wurde mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` durchgeführt:

- `ExternalSourceRepositoryAcquirer` liegt in
  `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`.
  Die Datei hat 477 Zeilen, davon 431 Codezeilen, und orchestriert bereits
  Cache-Reuse, Clone, Ownership, Validation und Write-through. Sie darf
  durch den Refresh-Hook nicht über die MaxLineCount-Grenze wachsen.
- `ExternalSourceRepositoryCacheReader` validiert Current-Pointer,
  Generation, Manifest, Inventory und Inhalt fail-closed. Das Manifest
  enthält bereits `CreatedUtc`; ein neues Schema-Feld ist für diesen Step
  nicht erforderlich.
- `ExternalSourceRepositoryCacheMaterializer` erzeugt aus einer validierten
  Generation einen frischen Checkout. `CacheStorage` kopiert dabei auch den
  bestehenden Git-Arbeitsbaum nach den vorhandenen Pfad- und Reparse-Regeln;
  der persistente Generation-Pfad wird nicht als Request-Handle verwendet.
- `LocalExternalSourceRepositoryCacheWriter` besitzt die bestehende
  per-Key-Sperre, Staging-Generation, Read-back-Prüfung, Rollback und
  atomare Current-Pointer-Veröffentlichung. Die Datei ist mit 399 Zeilen
  nahe, aber noch unter der 500-Zeilen-Grenze; eine kleine Precondition darf
  keinen neuen Cache-Unterbau erzwingen.
- `IGiteaRepositoryTransport` bietet derzeit nur
  `CloneDefaultBranchAsync`. `GiteaGitRepositoryTransport` kapselt bereits
  Credential-Auflösung, isolierte Git-Umgebung, Prozess-Executor,
  Timeout/Cancellation, HEAD-Prüfung, Reparse-Schutz und typisierte Fehler.
  Der neue Fetch muss diese Pfade wiederverwenden.
- Die einzige produktive Transport-Implementierung hat semantisch
  eingehende Verwendungen im Acquirer und Provider; die zwei relevanten
  Test-Doubles liegen in
  `ExternalSourceRepositoryAcquirerTestTransport.cs` und
  `ExternalSourceRepositoryCancellationTests.cs`. Der Acquirer hat 22
  statisch zugeordnete bestehende Tests.
- Der aktuelle scoped MCP-Audit fand 0 Produktions-Duplikatcluster bei 307
  gescannten Methoden, 0 High-Confidence-Dead-Code-Kandidaten und die
  bereits bekannten sieben Magic-Value-Kandidaten. Das ist Bestandsbefund,
  keine Freigabe für einen globalen Cleanup.

`AssemblyDecompilationCache` wird nur als Referenzmuster betrachtet. Es wird
keine gemeinsame Cache-Basisklasse und kein zweiter Reader/Parser erfunden.

## Intention

Nach diesem Step entscheidet der Acquirer deterministisch zwischen einem
frischen validierten Current-Reuse und einem fälligen Refresh. Ein fälliger
Refresh verändert nie die persistente alte Generation; er veröffentlicht nur
nach erfolgreichem Fetch, Checkout-Check und vollständigem Write-through eine
neue Generation. Bei Fehlern bleibt der alte Pointer lesbar, wird aber nicht
als aktuelles Ergebnis ausgegeben; die bestehende statische Decompilation
bleibt der sichtbare Fallback.

## Gewählter Refresh-/Policy-Vertrag

### Entscheidung

- Die Entscheidung startet nur bei einem strikt lesbaren Current-Pointer und
  einem vollständig validierten `CacheReadResult`. Missing, malformed,
  inkonsistent oder inhaltlich beschädigt bleibt ein Cache-Miss und folgt dem
  unveränderten Clone-/Write-through-Pfad.
- `CreatedUtc` bedeutet für diesen Schritt „zuletzt erfolgreich
  veröffentlichte Source-Generation“. Die Policy ist fällig, sobald
  `now >= CreatedUtc + RefreshInterval`; eine zukünftige oder nicht sauber
  als UTC behandelbare Zeit wird aus Fail-closed-Gründen nicht als Freibrief
  für stilles Reuse verwendet.
- Der produktive Default beträgt zunächst 60 Minuten als benannte interne
  Policy-Konstante. `TimeProvider` oder eine gleichwertige injizierbare Uhr
  macht Grenzfälle deterministisch testbar. Es gibt keine Änderung an
  `ExternalSourceConfiguration`, `appsettings.json` oder Host-Bindings.
- Ein frischer Current wird ausschließlich über den vorhandenen
  `ExternalSourceRepositoryCacheReuse`-Pfad materialisiert. Dieser Vertrag
  bleibt unverändert und führt nicht zum Transport.

### Fälliger Refresh

- Der Refresh materialisiert die alte Generation in einen neuen reservierten
  und besitzgebundenen Request-Checkout. Die persistente Generation bleibt
  unverändert und wird nie direkt von Git beschrieben.
- Der Transport führt genau einen begrenzten Default-Branch-Fetch auf diesem
  Checkout aus und liefert danach den verifizierten aktuellen HEAD-Commit.
  Die konkrete Git-Argumentfolge muss die vorhandene Single-Branch-/Remote-
  HEAD-Semantik von `GiteaGitRepositoryTransport` beibehalten; sie darf
  keinen neuen konfigurierbaren Branch einführen.
- Der Acquirer validiert Ownership, Reparse-Sicherheit, Solution-Pfad,
  Checkout-Inhalt und die gültige Revision vor dem Publish. Der Publish-
  Request trägt die beobachtete alte Generation als verpflichtende erwartete
  Current-Generation.
- Der vorhandene Writer erzeugt daraus eine neue eindeutige Generation mit
  aktualisiertem Manifest/Inventory, liest sie vollständig zurück und schaltet
  den Current-Pointer atomar um. Alte Leases und die alte Generation bleiben
  bis zu einem späteren Retention-Vertrag erhalten.

### Fehler- und Konkurrenzsemantik

- Fetch-, Validation-, Cancellation- oder Publish-Fehler beenden den
  Refresh bounded. Der neue Request-Checkout und eine unvollständige
  Staging-Generation werden über die bestehende Ownership-/Writer-Cleanup-
  Grenze bereinigt; der alte Current-Pointer bleibt unverändert.
- Ein fälliger Refresh wird nicht in einen Clone-Retry umgedeutet. Der
  Acquirer gibt einen typisierten, geheimnisfreien Provider-Fehler zurück,
  damit die statische Decompilation übernimmt. Der alte Snapshot bleibt für
  Diagnose erhalten, wird aber nicht still als aktuell verwendet.
- Wenn die erwartete Generation beim Publish nicht mehr Current ist, darf
  der ältere Refresh nicht überschreiben. Der Pfad verwirft seinen Checkout,
  liest den neuen Current erneut und verwendet ihn nur, wenn er vollständig
  validiert und nach derselben Policy aktuell ist; es gibt keinen zweiten
  Remote-Fetch-Versuch in diesem Aufruf.
- Die bestehende Clone-Semantik bleibt fail-open bezüglich eines Cache-
  Publish-Fehlers. Nur der neue fällige Refresh ist fail-closed, weil seine
  erfolgreiche Aktualisierung sonst nicht als neue Source-of-Truth belegt
  wäre.

## Konkrete Änderungen

### Schicht 1: Policy und Acquirer-Entscheidung

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshPolicy.cs`
  (neu): kleine interne Policy mit benanntem 60-Minuten-Default,
  injizierbarer Uhr und klarer Fälligkeitsgrenze. Keine appsettings- oder
  Host-Komposition.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs`
  (neu): Orchestrator für Current-Lookup, Policy-Entscheidung,
  Materialisierung, Fetch, Validation, Publish und Cleanup. Er verwendet
  `CacheReader`, `CacheReuse`, `CacheMaterializer`, `CacheWriter` und die
  vorhandene Failure-/Ownership-Semantik; er implementiert keinen zweiten
  Cache-Reader.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
  (bestehender Hook): Refresh/Re-use vor dem bisherigen Clone-Pfad
  aufrufen. Der Acquirer bleibt unter der Zeilen-/Methodengrenze; die
  Refresh-Orchestrierung wird nicht in seine bereits große Klasse kopiert.

### Schicht 2: Injizierter Fetch-/Transport-Pfad

- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`: eine
  asynchrone `FetchDefaultBranchAsync`-Operation mit derselben Mapping-,
  Destination- und Cancellation-Grenze wie Clone ergänzen.
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`: Fetch
  über den bestehenden `ExternalSourceGitProcessExecutor`, die bestehende
  Credential-/Environment-Erzeugung, Timeout-/Cancellation- und
  Diagnoseprojektion implementieren. Keine Credentials auf der Kommandozeile,
  kein Assembly-Laden, keine neue Netzwerkabstraktion außerhalb des Ports.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs`
  und `ExternalSourceRepositoryCacheWriter.cs`: den für Refresh-Publishes
  erforderlichen `ExpectedCurrentGeneration`-Precondition-Wert und einen
  eindeutig typisierten „Current inzwischen geändert“-Publish-Befund
  ergänzen. `null` erhält die bisherige Clone-Semantik.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs`
  und die Transport-Double-Stelle in
  `ExternalSourceRepositoryCancellationTests.cs`: Fetch-Aufzeichnung,
  Cancellation und Fehlerpfade ergänzen, ohne Remote- oder Git-Netzwerk.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`:
  Fetch-Argumente, Default-Branch-Semantik, Credential-Isolation,
  Timeout/Cancellation, HEAD-Validierung und geheime Diagnosewerte
  deterministisch über den bestehenden Recording-Executor prüfen.

### Schicht 3: Generation, Rollback und vertikale Regressionen

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshTests.cs`
  (neu): die End-to-End-Komponententests für Fresh/Expired, erfolgreichen
  Fetch, neue Generation, neuen Request-Handle, Pointer-Unverändertheit bei
  Fehlern, Cleanup, Current-Race-Precondition und bounded Cancellation.
- Bestehende Cache-Test-Support-Dateien dürfen nur um gemeinsam benötigte
  Fixture-/Policy-/Transport-Double-Helfer ergänzt werden. Die vorhandenen
  `TestTempDirectory`-, Reader-/Writer- und Ownership-Helfer werden wieder-
  verwendet; keine ad-hoc Temp-Pfade und keine Produktions-Basisklasse.

Nicht zu ändern sind die Assembly-Decompilation, die Registry-/Snapshot-
Lifetime, die Provider-/Host-Komposition, die öffentliche Konfiguration,
Retention/GC/Invalidierung sowie die vorhandene Clone-/Current-Reuse-Logik,
außer dem minimalen Acquirer-Hook und der Writer-Precondition.

## Architekturgrenze

```text
valid Current
    ├─ Policy: aktuell ──> bestehender Cache-Reuse, neuer Request-Handle
    ├─ Policy: fällig ──> Materialize alte Generation
    │                     -> Fetch Default Branch in neuem Checkout
    │                     -> Validate -> Writer: neue Generation + Current
    │                     -> neuer Request-Handle
    │                Fehler -> Cleanup, alter Current bleibt, Decomp-Fallback
    └─ Miss/invalid ──> bestehender Clone-/Write-through-Pfad
```

Die Grenze endet am validierten `ExternalSourceRepositoryAcquisitionResult`.
Sie behauptet weder Dirty-/Unbuilt-Erkennung noch Health-/degraded-State,
Host-Retry oder Retention. Die Generation ist die einzige persistente
Source-of-Truth; ein Request-Checkout bleibt request-eigen.

## Abnahmekriterien

1. Ein gültiger Current unterhalb des injizierten Intervalls verwendet den
   bestehenden Reuse-Pfad; Fetch und Publish werden nicht aufgerufen.
2. Ein gültiger, fälliger Current wird nicht wiederverwendet, sondern in
   einen neuen besitzbaren Checkout materialisiert; der alte Generation-Pfad
   bleibt unverändert.
3. Ein erfolgreicher Fetch nutzt ausschließlich den bestehenden sicheren
   Git-Prozess-/Credential-/Timeout-/Cancellation-Pfad, aktualisiert den
   Default-Branch und liefert eine validierte Revision ohne Geheimnisse.
4. Nach erfolgreicher Validierung entsteht eine neue Generation mit
   konsistentem Manifest/Inventory und atomarem Current-Pointer; das Ergebnis
   verweist auf den neuen request-eigenen Checkout.
5. Bei Fetch-, Validation-, Cancellation- oder allgemeinem Publish-Fehler
   bleiben alter Pointer und alte Generation unverändert; der neue Checkout
   wird bereinigt und der fällige Pfad liefert einen typisierten Fehler statt
   stale Current oder unbounded Clone-Retry.
6. Ein konkurrierender Current-Wechsel kann von einem älteren Refresh nicht
   überschrieben werden; der Pfad revalidiert höchstens den bereits neuen
   Current und führt keinen zweiten Remote-Fetch aus.
7. Bestehende Clone-, Current-Reuse-, Ownership-, Snapshot-/Registry-,
   1314-/Reparse-, statische-Decompilation- und Git-Invarianten bleiben
   regressionsfrei; es gibt keine Assembly.Load-/ALC-/Reflection-Ausführung
   und keine Remotezugriffe in Tests.
8. Der fokussierte Test-/MCP-Nachweis ist reproduzierbar, alle relevanten
   Dateien bleiben unter den Regelgrenzen, und die Abschluss-Gates aus
   `AGENTS.md` sind grün; neue DRY-, MagicValues- oder DeadCode-Befunde
   werden innerhalb dieses Pakets geklärt oder begründet.

## Teststrategie

- Policy-Grenztests mit kontrollierter Uhr: frisch, exakt fällig, zukünftig
  datiert und invalid/missing Current.
- Refresh-Orchestrator: erfolgreicher Fetch, neuer Generation-/Pointer-
  Name, neue Revision, alter Generation-Inhalt, Ownership und Handle-
  Cleanup.
- Failure-Matrix: Fetch-Fehler je bestehender typisierter Klasse,
  ungültige Revision/Solution/Reparse, Writer-Fehler, Current-Changed,
  Cancellation und Cleanup-Fehler. Assertions prüfen alten Pointer,
  sichtbaren Fehler und Decompilation-Fallback-Signal.
- Transport: nur `RecordingGitExecutor`/injizierte Doubles; keine
  Remote-, Gitea-, Git-Netzwerk- oder echte Credential-Zugriffe. Bestehende
  1314-/Reparse-Fallback-Tests bleiben unverändert und werden mitgeführt.
- Nach der Implementierung führt der Coder zuerst fokussierte FastTests für
  Refresh, Acquirer, Cache-Reuse und Gitea-Transport aus, danach zwingend:

  ```powershell
  dotnet build
  dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  ```

  Stress bleibt ausgeschlossen. Der Planer führt in diesem Planungsturn
  keine Tests und keinen Build aus.

## Context Budget

```yaml
max_initial_files: 12
max_read_first_files: 10
read_first:
  - tasks/decompiled-assembly-analysis/step-031/step-review.md
  - tasks/decompiled-assembly-analysis/step-029/step-plan.md
  - tasks/decompiled-assembly-analysis/codemap.md
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReuse.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheMaterializer.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs
  - src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs
  - src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs
read_on_demand:
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReadSupport.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutReservation.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessContracts.cs
  - src/AiNetLinter/Mcp/Assemblies/ExternalSourceGitProcessExecutor.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs
  - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCancellationTests.cs
out_of_scope:
  - src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs
  - appsettings.json und Host-/MCP-Komposition
  - AssemblyDecompilationCache und EPIC-03-Assembly-Tools
  - Snapshot-/Workspace-/Registry-Lifetime
  - Dirty/unbuilt, Health/degraded und Capability-Matrix
  - CacheRoot-/Refresh-Konfigurationsschema
  - Retention, GC, Invalidierung und Telemetrie
  - Remote-/Gitea-/Git-Netzwerkzugriffe in Tests
```

Das Budget umfasst zehn gezielte Erstdateien und höchstens zwei zusätzliche
Dateien im Initialkontext. Die übrigen Dateien werden nur bei einem konkreten
Symbolpfad oder Testfehler nachgeladen; ein vollständiger Solution-Dump ist
nicht zulässig.

## MCP-/DRY-/MagicValues-/DeadCode-Disposition

- Die Planung nutzte `dependency_graph`, `get_feature_context`,
  `get_test_context`, `find_duplicates`, `find_magic_values` und
  `find_dead_code` mit absolutem `projectRoot`. `rg` bleibt auf Textsuche
  beschränkt. Semantisch relevant sind die Acquirer-/Transport-/Writer-
  Kanten und die zwei Transport-Doubles.
- Nach dem Code-Change führt der Coder `safeguard`/Violations sowie scoped
  `find_duplicates`, `find_magic_values` und `find_dead_code` nur über die
  betroffenen Produktions-/Testdateien aus. Keine globale Audit-Ausweitung.
- DRY-Disposition: vorhandene Cache-Reader-/Read-back-, Materializer-,
  Writer-, Ownership-, Failure- und Git-Environment-Helfer nutzen. Eine
  neue Refresh-Orchestrierung ist zulässig; eine künstliche gemeinsame
  Cache-Basisklasse oder parallele JSON-/Path-Validierung ist unzulässig.
- Magic-Values-Disposition: Policy-Intervall, neue Fetch-/Publish-
  Diagnosecodes und Git-Argumentkonstanten zentral und benannt halten.
  Die sieben bestehenden Kandidaten aus dem scoped Audit werden nur bei
  direktem Bezug zum neuen Vertrag bearbeitet; `TD-001` bis `TD-003` bleiben
  unverändert, weil kein direkter Impact nachgewiesen ist.
- Dead-Code-Disposition: kein unreferenzierter Port oder Policy-Overload;
  neue Typen müssen durch Acquirer und Tests genutzt werden. High-Confidence-
  Funde werden nicht außerhalb dieses Vertrags gelöscht.

## Risiken und Gegenmaßnahmen

- **Stale-Bypass:** `CreatedUtc` wird nur nach vollständigem Reader-Check
  bewertet; zukünftige/fehlerhafte Zeitwerte führen nicht zu stillem Reuse.
- **Mutation einer alten Generation:** Fetch läuft nur auf einem neuen,
  ownership-geschützten Materialisierungs-Checkout.
- **Pointer-Race:** Writer-Lock plus erwartete Current-Generation verhindert
  das Überschreiben eines neueren Pointers; Current-Changed wird bounded
  behandelt.
- **Teil-Publish:** bestehende Staging-, Read-back-, Pointer- und Rollback-
  Mechanik bleibt die einzige Publish-Implementierung.
- **Credential-/Prozessleck:** Fetch teilt die vorhandene Environment-
  Redaction, den Executor und die typisierte Fehlerprojektion.
- **Kontext-/Regeldrift:** Refresh bleibt in eigener Klasse; Acquirer- und
  Writer-Dateigrenzen sowie acht Kriterien werden vor dem Abschluss geprüft.
- **Falsche Folgeausweitung:** Config, Retention/GC, Health/degraded,
  Dirty/unbuilt und Host/MCP werden als eigene Folgepakete dokumentiert,
  nicht implizit mitimplementiert.

## Definition of Done

- [ ] Alle drei gekoppelten Schichten und die acht Abnahmekriterien sind
      umgesetzt oder mit konkretem Codebefund begründet.
- [ ] Der fällige Refresh ist bei Fetch-/Publish-Fehlern fail-closed; alter
      Current-Pointer und alte Generation bleiben nachweisbar unverändert.
- [ ] `dotnet build` ist warnungsfrei grün.
- [ ] Beide vollständigen Nicht-Stress-Testläufe aus `AGENTS.md` sind grün;
      Stress ist nicht ausgeführt.
- [ ] Scoped MCP-Safeguard sowie DRY-/MagicValues-/DeadCode-Audits sind
      ausgeführt und im Step-Result wahrheitsgemäß dokumentiert.
- [ ] Keine Änderung an öffentlicher Cache-Konfiguration, Host/MCP-Wiring,
      Retention/GC, Health/degraded/dirty/unbuilt oder statischer
      Decompilation wurde eingeschmuggelt.
- [ ] `step-032/step-result.md` und ein genehmigter Review sind geschrieben;
      der Planstatus wechselt erst danach auf `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc`
- `.agents/rules/AiNetLinterRichtlinien.mdc`
- `.agents/rules/AiNetLinter-McpWorkflow.mdc`
- `.agents/Agent-Scaffolding/AGENTS.md`
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md`
- `tasks/decompiled-assembly-analysis/follow-up-strategy.md`

## Bekannte Ausnahmen

- Die zwei bereits dokumentierten FastTest-Skips für echte Reparse-/Symlink-
  Nachweise unter Win32-Fehler 1314 bleiben repository-spezifisch und dürfen
  nicht in eine globale Capability-Sperre umgewandelt werden.
- Die Abschluss-Gates bleiben trotz der Planerrolle Pflicht für den Coder;
  dieser Plan führt sie nicht vorweg aus.

## Exakter Coder-Hand-off

Starte einen **neuen Coder-Agenten**; verwende keinen bestehenden Agenten
wieder und beginne keine Kritikerarbeit. Lies zuerst die drei Regeldateien,
`AGENTS.md`, diesen Step-Plan, `step-031/step-review.md`,
`step-029/step-plan.md` und die zehn `read_first`-Dateien. Verwende für jede
C#-Semantik das AiNetLinter-MCP mit dem absoluten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; nutze `rg` ausschließlich
für Text.

Implementiere genau den oben definierten einen Refresh-/Fetch-Vertrag:
Policy-Entscheidung auf `CreatedUtc`, neuer materialisierter Checkout,
injizierter Default-Branch-Fetch mit den Steps-019–023-Invarianten,
vollständige Validation und atomare neue Generation mit Current-Precondition.
Bei fälligem Fehler darfst du weder den alten Current still zurückgeben noch
unbounded clonen; gib den typisierten Fehler für den bestehenden statischen
Decompilation-Fallback zurück. Bewahre Clone, validated Current-Reuse,
Snapshot-/Registry-Ownership, 1314-/Reparse-Regeln sowie alle
HTTP/Git/Credentials/Process-/Native-Invarianten. Nutze vorhandene Reader-,
Materializer-, Writer-, Failure- und TestKit-Helfer. Erzeuge keine gemeinsame
Cache-Basisklasse.

Halte die Änderung auf die im Scope genannten Produktions- und Testdateien
begrenzt. Nicht anfassen: öffentliche Cache-Konfiguration, appsettings,
Host/MCP-Wiring, Dirty/unbuilt, Health/degraded, Retention/GC/Invalidierung,
Telemetry, EPIC-05 und Assembly-Decompilation. Teste ausschließlich mit
lokalen `TestTempDirectory`-Fixtures und injizierten Doubles; kein Remote,
Gitea- oder Git-Netzwerk. Führe danach die fokussierten Tests, `dotnet build`
und beide vollständigen Nicht-Stress-Gates aus. Liefere `step-result.md` mit
realen Zahlen, Skips, MCP-Audits, offenen Risiken und dem exakten Commit;
pushe nichts und übergib erst dann an einen neuen Kritiker.
