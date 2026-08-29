---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 030
corrects: step-029
title: "Cache-Reuse-Nachweise und Step-029-Result korrigieren"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T20:58:02+02:00
related_to:
  - ../step-029/step-plan.md
  - ../step-029/step-result.md
  - ../step-029/step-review.md
  - ../step-028/step-review.md
---

# Step 030: Cache-Reuse-Nachweise und Step-029-Result korrigieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — der bereits implementierte Initial-Reuse-Vertrag
  braucht einen belastbaren Publish-/Current-/Ownership-Nachweis.
- **Korrektur:** `corrects: step-029`; das Review `step-029/step-review.md`
  weist genau zwei MAJOR-Findings aus: falsche Verifikations-/Auditangaben
  und unvollständige Beobachtung des validen Cache-Hits.
- **Konzept-Referenz:** `Konzept.md` — die persistente Source-Generation ist
  Cache-eigen, ein Reuse liefert einen getrennten read-only Source-Stand und
  keinen zweiten Änderungs- oder Ownership-Kontext.

## Aktueller Projektzustand (JIT-Kontext)

Step 029 ist technisch umgesetzt, aber wegen seines Reviews `issues` nicht
freigegeben. Der Produktionscode besitzt bereits die benötigten internen
Seams: `ExternalSourceRepositoryAcquirer` akzeptiert getrennt
`cacheWriter` und `cacheReader`, `ExternalSourceRepositoryCacheReuse` liest
über `IExternalSourceRepositoryCacheReader`, und die bestehende
`LocalExternalSourceRepositoryCacheWriter` implementiert Publish und Read.
Eine Produktionsänderung ist deshalb nicht erforderlich.

Die drei validen Reuse-Tests liegen in der Partial-Class
`ExternalSourceRepositoryCacheWriterTests`:

- `Acquirer_ValidCacheHitCreatesIndependentCheckoutWithoutTransportOrPublish`
  in `ExternalSourceRepositoryCacheAcquirerTests.cs:102-137` verwendet
  aktuell dieselbe Local-Writer-Instanz zum Publish und Read und hat keine
  Current-Generationsidentität.
- `CacheReuse_ValidCurrentReturnsRequestOwnedCheckout` in derselben Datei
  (`:140-164`) prüft nur gegen den Fixture-Checkout.
- `Acquirer_ConcurrentCacheHitsCreateIndependentLeases` in derselben Datei
  (`:374-405`) prüft nur erneute Lesbarkeit, nicht die konkrete Current-
  Identität.

Der vorhandene private `RecordingCacheWriter` in
`ExternalSourceRepositoryCacheWriterTests.cs:406-425` zeichnet über
`Request` bereits jeden Publish-Aufruf auf. Die Tests können daher ohne neue
Produktionsabstraktion einen Local-Publisher, einen separaten Local-Reader
und einen Recording-Writer für den Acquirer verdrahten.

Der aktuelle, im Review festgehaltene Nachweis lautet: Fokuslauf 34
bestanden/1 Skip/35 gesamt, Fast-Gate 2060/2/2062, Integration-Gate
370/0/370; der Fokus-Skip und der zusätzliche Fast-Skip sind echte
Win32-1314-Reparse-Fälle. `get_violations` ist im
`ExternalSourceRepository`-Scope leer. Der frühere Result-Text nennt
stattdessen 89/2/91, 2056/2/2058, einen `scopeDir=src`-Audit und einen
veralteten Safeguard-Wert.

## Intention

Schließe die Nachweislücke, ohne den Cache-Reuse-Produktionsvertrag neu zu
gestalten. Die validen Reuse-Tests sollen den erfolgreichen Initial-Publish,
den separaten Reader, den nicht aufgerufenen Publish-Writer, den unveränderten
Current-Generation-Namen und den unabhängigen request-owned Checkout direkt
beobachten. Das Step-029-Result wird auf die tatsächlich ausgeführten Tests,
Skip-Namen und ausschließlich erlaubte scoped Audits berichtigt.

## Split-Gate

Das Paket bleibt ein primärer Fachvertrag: **reproduzierbarer Cache-Reuse-
und Ownership-Nachweis**. Es enthält höchstens drei unmittelbar gekoppelte
Schichten:

1. **Result-/Audit-Korrektur:** Verifikationszahlen, konkrete Filter,
   Skip-Namen, Commit-Hash und Audit-Scope in `step-029/step-result.md` auf
   nachweisbare Ausgaben begrenzen.
2. **Recording-Reader-/Writer-/Current-Snapshot-Assertions:** Die validen
   Single- und Parallel-Hits mit separatem Reader und Recording-Writer
   ausstatten; den Current-Generation-Namen vor/nach Hit, nach Dispose und
   nach parallelen Hits vergleichen.
3. **Lokale Reuse-/Fallback-Regressionen:** Den direkten Reuse-Test auf
   denselben Ownership-/Current-Nachweis bringen und die bestehenden
   Invaliditäts-, Missing-Current-, Missing-Artifact- und
   Materialisierungs-Fallbacks im fokussierten Lauf unverändert ausführen.

Der Split wird nicht weiter geteilt: Result, Beobachtbarkeit und die lokalen
Regressionen bilden denselben Abnahmevertrag und bleiben unter drei Schichten
und acht Kriterien.

## Kontextbudget

```yaml
context_budget:
  max_initial_files: 12
  max_read_first_files: 10
  read_first:
    - tasks/decompiled-assembly-analysis/step-029/step-plan.md
    - tasks/decompiled-assembly-analysis/step-029/step-result.md
    - tasks/decompiled-assembly-analysis/step-029/step-review.md
    - tasks/decompiled-assembly-analysis/step-028/step-review.md
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReuse.cs
    - src/AiNetLinter/Mcp/Assemblies/IExternalSourceRepositoryCacheReader.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs
  read_on_demand:
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheMaterializer.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutReservation.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs
  out_of_scope:
    - src/AiNetLinter/Configuration/
    - src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs
    - src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs
    - src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs
    - src/AiNetLinter/Mcp/Assemblies/Assembly*.cs
    - src/AiNetLinter/Mcp/Daemon/
    - appsettings.json
    - Docs/
    - tasks/decompiled-assembly-analysis/roadmap.md
    - tasks/decompiled-assembly-analysis/task-state.md
    - tasks/decompiled-assembly-analysis/tech-debt.md
```

Der Initialkontext umfasst genau zehn Dateien und bleibt unter beiden
Schranken. Die drei Step-029-Artefakte liefern den Korrekturinput; die
Produktionsdateien bestätigen die vorhandene Injektion, während die beiden
Partial-Testdateien die wiederverwendbaren Fixtures und den Recording-Writer
enthalten. Reader-/Materializer-/Reservation-Details werden nur bei einer
konkreten Assertion nachgeladen.

## Konkrete Änderungen

### Datei 1: `tasks/decompiled-assembly-analysis/step-029/step-result.md`

- **Was:** `code_commit_hash` von `siehe Abschluss-Commit` auf den geprüften
  Commit `82692da054136dd39f6a37d110926bb95b5d796c` setzen.
- **Was:** Den Verifikationsabschnitt mit dem exakten Fokus-Command und den
  realen Zahlen ersetzen:

  ```powershell
  dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"
  ```

  Erwarteter, im Review bereits reproduzierter Stand: **34 bestanden,
  1 Skip, 35 gesamt, 0 Fehler**. Der konkrete Fokus-Skip ist
  `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
  wegen `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 beim Erzeugen eines echten
  Symlinks.
- **Was:** `dotnet build` mit **0 Warnungen, 0 Fehlern**, das vollständige
  Fast-Gate mit **2060 bestanden, 2 Skips, 2062 gesamt, 0 Fehlern** und das
  vollständige Integrations-Gate mit **370 bestanden, 0 Skips, 370 gesamt,
  0 Fehlern** dokumentieren. Der zusätzliche Fast-Skip ist
  `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`,
  ebenfalls Win32 1314. Stress wird ausdrücklich als nicht ausgeführt
  dokumentiert. Weichen erneute reale Läufe ab, dürfen ausschließlich deren
  Konsolenzahlen eingetragen werden; Zahlen werden nicht geschätzt.
- **Was:** Den `scopeDir=src`-Claim vollständig entfernen. Im
  MCP-/Auditabschnitt dürfen nur tatsächlich ausgeführte, begrenzte Aufrufe
  mit `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` und ihren realen
  Ausgaben stehen:

  - `get_violations(scopeFilter="ExternalSourceRepository")` — 0
    Violations in 24 Dateien.
  - `safeguard(scopeFilter="ExternalSourceRepository")` — der im Review
    reproduzierte Wert ist 5,79/10 mit den drei bestehenden Befunden
    außerhalb des Reuse-Codes; bei einer Wiederholung ausschließlich den
    tatsächlich ausgegebenen Wert und Scope übernehmen.
  - `find_duplicates(mode="clone", minTokens=20)` getrennt auf
    `src/AiNetLinter/Mcp/Assemblies` (`scopeType="production"`) und
    `src/AiNetLinter.FastTests/Mcp/Assemblies` (`scopeType="tests"`), nicht
    solutionweit: im Review 0 Exact-/Near-Cluster bei 350 Produktions- bzw.
    122 Testmethoden. Ein zusätzlicher
    `mode="refactoring-drift"`-Aufruf darf nur aufgenommen werden, wenn sein
    Helper und Scope konkret ausgeführt und im Result benannt werden.
  - `find_magic_values` ausschließlich im ExternalSourceRepository-
    Produktions-/Testscope: im Review 0 für die vier neuen Produktionsdateien,
    7 bestehende Werte im breiteren Produktionsscope und 34 absichtliche
    Fixture-/Fallwerte in der neuen Cache-Acquirer-Testdatei.
  - `find_dead_code(scopeFilter="ExternalSourceRepository", includeTests=true)`
    — 0 unreferenzierte Symbole bei 24 geprüften Dokumenten und 55
    Symbolen.

  Fehlt für eine Ausgabe der konkrete tatsächlich ausgeführte Scope, wird sie
  weggelassen. Es darf weder ein solutionweiter DRY-/MagicValues-/DeadCode-
  Sweep noch ein nicht ausgeführter Audit behauptet werden. Der Result-Text
  bleibt bei den bestehenden drei externen Safeguard-/Footprint-Befunden und
  legt keinen neuen Tech-Debt-Eintrag an.

### Datei 2: `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs` (Zeilen 102-164, 374-405)

- **Was:** In allen drei validen Reuse-Tests den initialen Cache mit einer
  `cachePublisher`-Instanz von `LocalExternalSourceRepositoryCacheWriter`
  über `PublishAsync(source.Request)` aufbauen und den Erfolg explizit mit
  `Assert.True(published.Succeeded)` prüfen. Für den Read eine zweite
  `LocalExternalSourceRepositoryCacheWriter`-Instanz mit demselben isolierten
  Cache-Root als `cacheReader` verwenden.
- **Was:** Vor jedem Reuse `cacheReader.TryReadCurrent(source.Key, out
  var currentBefore, out var beforeDiagnostic)` prüfen, `beforeDiagnostic`
  auf `null` prüfen und
  `currentBefore!.Manifest.GenerationName` als
  `currentGenerationBefore` snapshotten. Der direkte Test verwendet diesen
  Reader im `ExternalSourceRepositoryCacheReuse`; die beiden Acquirer-Tests
  verdrahten zusätzlich `var cacheWriter = new RecordingCacheWriter()` als
  `cacheWriter`.
- **Was:** Beim Single-Hit nach `AcquireAsync` explizit assertieren:
  `result.IsAvailable`, `LoadedRevision == Revision`,
  `transport.CallCount == 0` und `cacheWriter.Request == null`. Damit ist
  der konkrete Reuse-Aufruf ohne Transport und ohne Publish beobachtbar.
  Zusätzlich bleiben der neue Checkout-Pfad ungleich
  `published.GenerationPath`, der eigene Ownership-Marker und der exakte
  `SolutionPath` assertiert.
- **Was:** Direkt nach dem Hit den Reader erneut ausführen und
  `currentAfterHit.Manifest.GenerationName == currentGenerationBefore`
  assertieren. Nach `checkout.Dispose()` müssen der request-owned Checkout
  verschwunden, `published.GenerationPath` vorhanden und der erneut gelesene
  Current-Generation-Name weiterhin identisch sein. Keine Assertion darf
  lediglich „irgendein Current ist lesbar“ prüfen.
- **Was:** Beim Paralleltest dieselbe getrennte Publisher-/Reader-/Recording-
  Verdrahtung und denselben Vorher-Snapshot verwenden. Nach vier parallelen
  Acquisitions müssen alle Ergebnisse verfügbar sein, vier unterschiedliche
  Checkout-Pfade und eigene Marker besitzen, `transport.CallCount == 0` und
  `cacheWriter.Request == null` gelten. Vor und nach dem Dispose aller vier
  Handles muss der Reader denselben
  `currentGenerationBefore` liefern; die persistente Generation bleibt
  vorhanden.
- **Was:** Im direkten `CacheReuse_ValidCurrentReturnsRequestOwnedCheckout`
  den Checkout ebenfalls gegen `published.GenerationPath` und nicht nur gegen
  `source.CheckoutPath` abgrenzen sowie Current-Name, Revision, Marker und
  Current-Erhalt nach Dispose prüfen. Die Publish-Beobachtung liegt bewusst
  in den beiden Acquirer-Tests, weil nur der Acquirer einen Writer für den
  Reuse-Entscheidungspfad besitzt.
- **Warum:** Der Test beweist damit die vollständige Kette
  `Publish -> separater Reader -> Reuse ohne Publish -> gleicher Current ->
  unabhängiger Checkout -> Cleanup ohne Generationseffekt`.

### Datei 3: `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs`

- **Was:** Den vorhandenen privaten `RecordingCacheWriter` nur
  wiederverwenden. Eine Erweiterung ist nicht erforderlich; `Request` bleibt
  vor dem Hit `null` und wird nach dem Hit ebenfalls als `null` geprüft.
- **Warum:** Die bestehende Partial-Class teilt diese Test-Hilfe bereits mit
  `ExternalSourceRepositoryCacheAcquirerTests.cs`; dadurch entsteht weder ein
  zweiter Recording-Writer noch ein neuer Fixture-Aufbau.

### Datei 4: bestehende lokale Reuse-/Fallback-Tests

- **Was:** `Acquirer_InvalidCacheIdentityFallsBackToClone`,
  `Acquirer_MissingCurrentFallsBackToClone`,
  `Acquirer_MissingCacheArtifactFallsBackToClone` und
  `Acquirer_MaterializationFailureCleansLeaseAndFallsBack` im Fokuslauf
  weiter ausführen. Nur wenn die getrennte Testverdrahtung eine Konstruktor-
  oder Fixture-Anpassung erzwingt, diese lokal und ohne Semantikänderung
  vornehmen.
- **Warum:** Der Korrektur-Step ergänzt nur die fehlende Erfolgsbeobachtung;
  die bereits nachgewiesene Miss-/Invaliditäts-/Fallback-Semantik wird als
  Regression erhalten und nicht neu entworfen.

## Abnahmekriterien (maximal 8)

1. **Result-Identität:** `step-029/step-result.md` enthält den vollständigen
   geprüften Code-Commit `82692da054136dd39f6a37d110926bb95b5d796c` und keine
   Platzhalter- oder „Abschluss-Commit“-Angabe.
2. **Reproduzierbare Testzahlen:** Der exakte Fokus-Filter, Build, beide
   vollständigen Nicht-Stress-Gates, reale Bestanden-/Skip-/Gesamt-/Fehler-
   Zahlen und die beiden konkreten 1314-Skip-Testnamen sind korrekt; Stress
   ist als nicht ausgeführt markiert.
3. **Scoped Audits:** Das Result dokumentiert nur tatsächlich ausgeführte
   MCP-/DRY-/MagicValues-/DeadCode-/Safeguard-Aufrufe mit absolutem
   `projectRoot` und Cache-/Acquirer-Scope; kein solutionweiter Audit-Claim
   und kein globaler Sweep.
4. **Konkreter Publish-Vertrag:** Jeder validierte Acquirer-Hit wird aus einer
   zuvor mit erfolgreichem `PublishAsync` aufgebauten Generation über einen
   separaten Reader geprüft und verwendet zusätzlich einen
   `RecordingCacheWriter`; `Request` bleibt leer und `transport.CallCount`
   bleibt null.
5. **Current-Unveränderlichkeit:** Der konkrete
   `Manifest.GenerationName`-Wert des Current wird vor dem Reuse und nach
   Single-Hit, nach Handle-Dispose und nach parallelen Hits identisch
   assertiert.
6. **Request-Ownership:** Jeder Hit erzeugt einen Checkout-Pfad, der von
   `published.GenerationPath` verschieden ist, trägt seinen eigenen Marker,
   liefert den erwarteten Solution-Pfad und entfernt nur den eigenen Checkout
   beim Dispose; die persistente Generation bleibt vorhanden und lesbar.
7. **Fallback-Regression:** Die bestehenden lokalen Invaliditäts-, Missing-,
   Materialisierungs- und Cancellation-Tests bestehen unverändert im
   fokussierten Lauf; kein Produktionsvertrag wird erweitert oder abgeschwächt.
8. **Scope-/Arbeitsbaum-Disziplin:** Keine Produktionsänderung, kein Refresh,
   Fetch, Policy, Config, Retention/GC, Invalidierung, Health/Dirty,
   Host-/MCP-/Provider-/Snapshot-/Registry-Redesign, Transport-/Native-/
   EPIC-05-Arbeit oder neue Test-Temp-/Netzwerkstruktur; der Plan-/Status-
   Commit lässt den Arbeitsbaum sauber zurück.

## Tests

- [ ] `Acquirer_ValidCacheHitCreatesIndependentCheckoutWithoutTransportOrPublish`:
  erfolgreicher Initial-Publish, separater Reader, Recording-Writer,
  `Request == null`, `CallCount == 0`, Current-Name vor/nach Hit und nach
  Dispose, unabhängiger Checkout.
- [ ] `CacheReuse_ValidCurrentReturnsRequestOwnedCheckout`: separater Reader,
  konkreter Current-Name, Generation-/Checkout-Trennung und Current-Erhalt
  nach Dispose.
- [ ] `Acquirer_ConcurrentCacheHitsCreateIndependentLeases`: vier unabhängige
  request-owned Checkouts, kein Publish/Transport und identischer Current-Name
  vor/nach parallelen Hits und Cleanup.
- [ ] Bestehende lokale Regressionen
  `Acquirer_InvalidCacheIdentityFallsBackToClone`,
  `Acquirer_MissingCurrentFallsBackToClone`,
  `Acquirer_MissingCacheArtifactFallsBackToClone`,
  `Acquirer_MaterializationFailureCleansLeaseAndFallsBack` und
  `Acquirer_CacheHitCancellationRethrowsWithoutClone`.
- [ ] Exakter Fokuslauf:
  `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"`.
- [ ] Abschlussprüfung: `dotnet build`,
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.
- [ ] Kein Stress-Lauf und keine Remote-/Git-Netzwerkzugriffe; bei echter
  Reparse-Erzeugung bleibt ausschließlich der vorhandene 1314-Skip-Vertrag
  zulässig.

## MCP-/DRY-/MagicValues-/DeadCode-Plan

Alle C#-Semantikabfragen erhalten exakt
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` wird ausschließlich
für Text-/Dateisuche eingesetzt. Vor der Änderung prüft der Coder per MCP
`get_feature_context` beziehungsweise `get_symbol_body` für
`ExternalSourceRepositoryAcquirer`, `ExternalSourceRepositoryCacheReuse`,
`IExternalSourceRepositoryCacheReader`, den vorhandenen Recording-Writer und
die drei betroffenen Testmethoden. `get_test_context` und
`find_references`/`get_impact` bleiben auf Acquirer, Reader und Tests
begrenzt. Nach der Änderung werden nur die direkten Auswirkungen erneut
geprüft.

Die Audit-Aufrufe sind begrenzt und reproduzierbar:

- `get_violations(scopeFilter="ExternalSourceRepository")` für den
  produktiven/testbezogenen betroffenen Bereich.
- `safeguard(scopeFilter="ExternalSourceRepository")`; bestehende
  Directory-/Footprint-Warnungen außerhalb des Reuse-Codes werden nur als
  bestehender scoped Kontext dokumentiert.
- `find_duplicates(mode="clone", minTokens=20, scopeType="production")`
  mit `scopeDir="src/AiNetLinter/Mcp/Assemblies"` sowie getrennt
  `scopeType="tests"` mit
  `scopeDir="src/AiNetLinter.FastTests/Mcp/Assemblies"`. Keine
  `scopeDir="src"`- oder Solution-Variante.
- `find_magic_values` mit `scopeFilter` auf die genannten
  `ExternalSourceRepository`-Produktions- und Testpfade sowie
  `includeTests` passend zum Scope.
- `find_dead_code(scopeFilter="ExternalSourceRepository",
  includeTests=true, mode="members")`; Low-Confidence-Kandidaten werden
  nicht gelöscht.

Nur ein unmittelbar durch die neuen Testassertions verursachter DRY-,
MagicValues- oder DeadCode-Befund darf als in-scope betrachtet werden. Es
werden keine bestehenden Tech-Debt-Einträge TD-001 bis TD-003 bearbeitet und
kein globaler Sweep gestartet. Nicht ausgeführte oder nicht exakt
scope-belegbare Audits erscheinen nicht im Step-029-Result.

## Risiken und Gegenmaßnahmen

- **Falsche Reader-/Writer-Kopplung:** Initial-Publish mit einem isolierten
  Local-Publisher ausführen, danach eine zweite Local-Reader-Instanz und
  einen separaten Recording-Writer am Acquirer verwenden.
- **Publish-Beobachtung bleibt wirkungslos:** `RecordingCacheWriter.Request`
  unmittelbar nach dem Hit und nach dem Cleanup auf `null` assertieren;
  `transport.CallCount` zusätzlich auf `0` assertieren.
- **Current wird nur indirekt geprüft:** Den konkreten
  `Manifest.GenerationName`-String einmal vor dem Reuse speichern und an
  jedem geforderten Lebenszeitpunkt exakt vergleichen.
- **Persistente Generation wird versehentlich als Request-Checkout
  behandelt:** `checkout.CheckoutPath != published.GenerationPath`, eigener
  Marker, Dispose des Checkouts und Existenz/Read-back der Generation
  explizit prüfen.
- **Result driftet erneut:** Zahlen nur aus dem ausgeführten Command
  übernehmen; jeden Audit mit Tool, Scope und `projectRoot` benennen; keinen
  solutionweiten Sammelclaim formulieren.
- **Scope-Drift:** Keine Produktionsänderung und keine Cache-/Refresh-/Policy-
  Entscheidung aus dem Testnachweis ableiten. Die vorhandene Konstruktor-
  Injektion ist ausreichend; eine neue Produktions-Seam ist nur bei einem
  tatsächlich nachgewiesenen Compilerzwang zulässig und muss im Result
  begründet werden.
- **Host-Capability:** Die zwei bekannten echten Reparse-Fälle bleiben
  wegen Win32 1314 transparent übersprungen; kein Fake-Reparse und keine
  abgeschwächte Assertion.

## DoD für den Coder

- [ ] `step-029/step-result.md` ist mit Commit-Hash, realen
      Fokus-/Fast-/Integration-Zahlen, konkreten Skip-Namen und scoped
      Audit-Ausgaben korrigiert.
- [ ] Der Result-Text enthält keinen `scopeDir=src`- oder sonstigen
      solutionweiten Audit-Claim und keine nicht ausgeführte Prüfung.
- [ ] Die drei validen Reuse-Tests verwenden die getrennte Reader-/Publisher-
      Fixture; die Acquirer-Hits verwenden zusätzlich den vorhandenen
      `RecordingCacheWriter`.
- [ ] Erfolgreicher Publish wird explizit geprüft; `Request == null`,
      `CallCount == 0` und Current-Generation-Identität sind nach Hit,
      Dispose und Parallelfall assertionsfähig.
- [ ] Der request-owned Checkout bleibt vom persistenten
      `published.GenerationPath` getrennt und wird unabhängig bereinigt.
- [ ] Die lokalen Fallback-/Cancellation-Regressionen und die vollständigen
      Nicht-Stress-Gates sind grün; Stress ist nicht ausgeführt.
- [ ] `step-030/step-result.md` wird geschrieben und der Step-Plan nach
      Abschluss auf `done (pending audit)` gesetzt. `task-state.md` und
      `roadmap.md` werden vom Coder nicht geändert.
- [ ] Coder erstellt die vorgesehenen lokalen Code-/Doku-Commits ohne Push;
      der Orchestrator führt danach den Kritikerlauf aus.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#architecture` und `#test-coverage` — keine
  Runtime-/Assembly-Ausführung und direkte Testabdeckung der Vertragsgrenze.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln`
  und `#4 Updates & Tests` — PowerShell, zentrale Test-Temp-Verzeichnisse,
  Fokus-/Nicht-Stress-Gates und transparenter 1314-Skip.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  scoped DRY-/MagicValues-/DeadCode-Prüfung ohne globalen Sweep.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Semantik zuerst über MCP mit absolutem
  `projectRoot`, Text nur über `rg`.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/skills/planer/SKILL.md#Fix-Modus`
  — ausschließlich Findings korrigieren, `roadmap.md` im Fix-Modus nicht
  anfassen.

## Bekannte Ausnahmen

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
  und `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`
  dürfen ausschließlich wegen echtem Win32 `ERROR_PRIVILEGE_NOT_HELD` /
  `1314` übersprungen werden.
- Stress-Tests werden nicht ausgeführt.
- Bestehende Safeguard-/Directory-/Footprint-Befunde und TD-001 bis TD-003
  bleiben außerhalb dieses Korrektur-Scopes.

## Exakter Coder-Hand-off

> Starte einen neuen Coder-Agenten für
> `decompiled-assembly-analysis`; verwende keinen bestehenden Agenten und
> keinen bestehenden Agenten-Kontext. Lies zuerst die zehn Dateien aus
> `context_budget.read_first`, den vollständigen Step-029-Review und die
> relevanten Rules. Verwende für jede C#-Semantikabfrage exakt
> `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` und `rg` ausschließlich
> für Text. Ändere keine Produktionsdatei, solange die bereits vorhandenen
> `cacheWriter`-/`cacheReader`-Seams ausreichen.
>
> Korrigiere `step-029/step-result.md`: trage den geprüften Hash
> `82692da054136dd39f6a37d110926bb95b5d796c` ein, verwende den exakten
> Fokus-Filter und die tatsächlich ausgeführten Zahlen 34/1/35, 2060/2/2062
> und 370/0/370 beziehungsweise neue reale Ausgaben. Nenne die beiden
> konkreten 1314-Skip-Testnamen und entferne jeden `scopeDir=src`- bzw.
> solutionweiten Audit-Claim. Dokumentiere ausschließlich ausgeführte,
> scoped MCP-/DRY-/MagicValues-/DeadCode-/Safeguard-Aufrufe.
>
> Verstärke die drei validen Reuse-Tests in
> `ExternalSourceRepositoryCacheAcquirerTests.cs`: publiziere zunächst mit
> einem isolierten Local-Publisher und prüfe `PublishAsync(...).Succeeded`,
> lies Current danach über eine separate Local-Reader-Instanz und snapshotte
> `Manifest.GenerationName`. Die beiden Acquirer-Tests erhalten zusätzlich
> einen vorhandenen `RecordingCacheWriter` als `cacheWriter`; nach einem
> validen Hit müssen `RecordingCacheWriter.Request == null` und
> `transport.CallCount == 0` gelten. Prüfe den identischen Current-Namen vor
> dem Hit, nach dem Hit, nach Dispose und im Paralleltest nach allen vier
> Hits/Disposes. Prüfe außerdem, dass jeder request-owned Checkout vom
> `published.GenerationPath` verschieden ist, seinen eigenen Marker trägt,
> unabhängig verschwindet und die persistente Generation lesbar bleibt.
> Der direkte `ExternalSourceRepositoryCacheReuse`-Test verwendet den
> separaten Reader und prüft denselben Current-/Ownership-Vertrag; er braucht
> keinen Writer-Parameter, weil die Publish-Beobachtung am Acquirer liegt.
>
> Führe danach exakt den Fokuslauf, `dotnet build` und beide vollständigen
> Nicht-Stress-Gates aus. Führe die im Plan genannten scoped MCP-/Audit-
> Aufrufe aus und schreibe `step-030/step-result.md`; halte Fallback-,
> Cancellation-, 1314-, Temp- und Ownership-Semantik unverändert. Kein
> Refresh, Fetch, Policy, Config, Retention/GC, Invalidierung, Health,
> Host-/MCP-/Provider-/Snapshot-/Registry-Redesign, Transport-/Native-/
> EPIC-05-Thema oder globaler Sweep. Kein Push.
