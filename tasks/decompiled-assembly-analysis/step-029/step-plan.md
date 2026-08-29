---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 029
corrects: null
title: "Cache-backed Initial Acquisition aus validierter Generation"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T19:46:48+02:00
related_to:
  - ../step-026/step-plan.md
  - ../step-028/step-plan.md
  - ../step-028/step-result.md
  - ../step-028/step-review.md
  - ../codemap.md
  - ../Konzept.md
  - ../roadmap.md
  - ../tech-debt.md
---

# Step 029 – Cache-backed Initial Acquisition aus validierter Generation

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` aus `roadmap.md` — Gitea-Source-of-Truth mit
  getrenntem Cache-, Acquisition- und Fallback-Vertrag.
- **Konzept-Referenz:** `Konzept.md` und `follow-up-strategy.md` — die
  initiale Akquisition darf eine validierte persistente Generation wieder-
  verwenden, muss aber einen neuen kontrollierten Request-Checkout besitzen;
  Refresh und Source-Policy folgen später.
- **Vorgänger:** genehmigter Step 028 mit `step-028/step-review.md` und
  `step-028/step-result.md`.

## Intention

Nach diesem Step kann der bestehende Acquirer eine valide veröffentlichte
Cache-Generation als initiale Acquisition-Quelle verwenden, ohne Transport
oder Publish erneut auszuführen. Der Snapshot-/Workspace-Pfad erhält dabei
unverändert einen frischen, request-eigenen Checkout mit kontrollierter
Lifetime; jeder Cache-Miss oder jede Invalidität fällt in den bestehenden
Clone-/Write-through-Vertrag zurück.

## Planstatus und Übergabegrund

Step 028 ist durch `step-028/step-review.md` mit Commit `d3d17fe1`
genehmigt. Der Write-through-Publish-Vertrag ist damit als belastbarer
Vorgänger abgeschlossen: credentialfreier Cache-Key, Manifest und Inventory,
isolierte Generation, bounded Read-back, atomarer `current`-Pointer,
per-Key-In-Process-Lock, deterministische Race-/Malformed-Matrix und
Testisolation bleiben unverändert.

Step 029 nimmt ausschließlich den ausdrücklich herausgelösten nächsten
Vertrag auf: Eine gültige persistente Generation darf als Acquisition-Quelle
dienen. Das Ergebnis ist trotzdem immer ein neu reservierter und
request-eigener Checkout. Die persistente Generation wird niemals direkt als
`ExternalSourceCheckoutHandle` oder Snapshot-Besitz weitergereicht.

## Gewählter Vertrag

Der Acquirer versucht nach erfolgreicher Mapping-/Solution-Pfad-Validierung
und vor jeder neuen Transport-Reservation zunächst den vorhandenen
Cache-Key zu lesen. Der Read-Port liefert nur dann einen Hit, wenn der
vorhandene `current`-Pointer, die referenzierte Generation, Manifest und
Inventory strikt und unabhängig validiert wurden. Dazu gehören die bereits
implementierten Identitätsvergleiche, bounded Reads, Datei-Längen und
-Hashes, erwarteter Solution-Anker, sichere relative Pfade und
Reparse-Prüfungen.

Bei einem Hit reserviert der Acquirer über
`ExternalSourceRepositoryCheckoutReservation.TryCreate` ein neues Verzeichnis
unter seiner bestehenden Staging-Wurzel. Nur die bereits validierte
Generation wird in dieses Verzeichnis materialisiert. Die neue
`ExternalSourceCheckoutOwnership` samt Marker bleibt die alleinige Ownership
des Request-Checkouts; daraus wird der vorhandene
`ExternalSourceCheckoutHandle` mit Solution-Pfad und `LoadedRevision` aus dem
Manifest erzeugt.

Fehlt `current`, ist Pointer/Manifest/Inventory/Inhalt ungültig oder schlägt
die kontrollierte Materialisierung fehl, wird der neue Checkout vollständig
bereinigt und der bisherige Clone-/Write-through-Pfad ausgeführt. Ein
Cache-Fehler darf keinen scheinbar erfolgreichen Checkout erzeugen. Bei
Cancellation nach einer Reservation wird nach Cleanup weiterhin Cancellation
weitergegeben; sie wird nicht in einen neuen Clone umgedeutet. Bei
Transportfehlern bleiben die bestehenden typisierten
Unavailable-/Fallback-Diagnosen und die bisherige Fehlerklassifikation
maßgeblich.

## Split-Gate

Das Paket ist genau ein primärer Vertrag mit höchstens drei gekoppelten
Schichten:

1. **Current-/Manifest-/Inventory-Read und Validierung:** Einen
   read-only-fähigen Port für die vorhandene lokale Cache-Fassade bereitstellen
   und die bestehende `ExternalSourceRepositoryCacheReader`-/`ReadSupport`-
   Validierung wiederverwenden. Kein zweiter Parser und kein eigener
   Cache-Key-/Manifest-Vertrag.
2. **Request-owned Checkout-Lease und Materialisierung:** Mit der bestehenden
   Reservation eine frische Ownership-Lease erzeugen, validierten Content
   sicher in den Checkout kopieren und die bestehende Handle-/Cleanup-Semantik
   verwenden. Die persistente Generation bleibt cache-eigen.
3. **Acquirer-Auswahl, Fallback und Tests:** Cache-first im vorhandenen
   Acquirer ausführen; Miss, Invalidität und Materialisierungsfehler in den
   unveränderten Clone-/Write-through-Pfad führen; die Tests mit bestehenden
   Transport-, Cache-, Temp- und Ownership-Fixtures ergänzen.

Nicht enthalten sind ein eigener Refresh-/Fetch-/Policy-Vertrag, eine zweite
Cache-Store-Abstraktion, eine neue Snapshot- oder Provider-Lifetime sowie ein
allgemeiner Cache-Health-Mechanismus.

## Kontextbudget

```yaml
context_budget:
  max_initial_files: 12
  max_read_first_files: 10
  read_first:
    - tasks/decompiled-assembly-analysis/step-028/step-plan.md
    - tasks/decompiled-assembly-analysis/step-028/step-result.md
    - tasks/decompiled-assembly-analysis/step-028/step-review.md
    - tasks/decompiled-assembly-analysis/codemap.md
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReadSupport.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs
  read_on_demand:
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheContract.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryPathGuard.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutReservation.cs
    - src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs
    - src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs
    - src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs
  out_of_scope:
    - src/AiNetLinter/Configuration/
    - src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs
    - src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs
    - src/AiNetLinter/Mcp/Assemblies/Assembly*.cs
    - src/AiNetLinter/Mcp/Daemon/
    - appsettings.json
    - Docs/
    - tasks/decompiled-assembly-analysis/step-030/
```

Die zehn `read_first`-Dateien begrenzen den Initialkontext; PathGuard,
Reservation, Snapshot-/Provider-Konsumenten und die Fixtures werden nur bei
konkreter Implementierungsfrage nachgeladen. Der Coder liest zusätzlich die
Regeln und den Plan vollständig, führt aber keinen Solution-weiten
Komplettdump durch.

## Aktueller Projektzustand (JIT-Kontext)

Der aktuelle Codebestand enthält bereits den vollständigen Write-through-
Unterbau aus Steps 026 bis 028, aber noch keinen produktiven Consumer für
`ExternalSourceRepositoryCacheReader.TryReadCurrent`. Deshalb ist der
kleinste stabile nächste Schritt ein Cache-first-Auswahlzweig am Acquirer,
nicht ein Umbau des Provider- oder Snapshot-Wirings.

### Tatsächlicher Impact und Wiederverwendung

Die MCP-Semantikanalyse wurde mit dem absoluten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` durchgeführt. Sie zeigt:

- `ExternalSourceRepositoryCacheReader.TryReadCurrent` ist produktiv nur über
  die lokalen Writer-Wrapper in
  `ExternalSourceRepositoryCacheWriter.cs` erreichbar. Der Read-Port kann
  deshalb dort extrahiert bzw. delegiert werden, ohne einen parallelen Parser
  zu schaffen.
- `ReadGeneration`, `ReadInventory`, `ValidateInventory` und
  `ReadBoundedText` enthalten bereits die strikte Current-/Manifest-/Inventory-
  Prüfung. `ValidateManifestIdentity` bindet Schema, Stable-Key, URL,
  Solution-Pfad und Generation; `ValidateInventoryIdentity` bindet dieselben
  Werte unabhängig an das Inventory.
- `ExternalSourceRepositoryAcquirer.AcquireAsync` validiert Mapping und
  Solution-Pfad, reserviert danach den Checkout und führt Transport,
  Checkout-Validierung, Handle-Erzeugung und Write-through aus. Der neue
  Zweig liegt an dieser Auswahlgrenze; `GiteaExternalSourceProvider`,
  `SourceSnapshotMaterializer`, `SourceSnapshotRegistry` und ihre Owner-
  Verträge bleiben Konsumenten des unveränderten Acquisition-Ergebnisses.
- `ExternalSourceRepositoryCheckoutReservation.TryCreate`,
  `ExternalSourceRepositoryPathGuard.IsOwnedCheckout` und
  `TryDeleteOwnedCheckout` liefern die erforderliche neue Lease- und
  Cleanup-Grenze. Es wird kein persistenter Generation-Pfad als Checkout-
  Ownership missbraucht.
- `ExternalSourceRepositoryCacheStorage` besitzt bereits die sicheren Pfad-,
  Reparse-, Walk- und Copy-Primitiven. Eine neue
  `CopyValidatedContent`-/Materialisierungsroutine darf diese Primitiven
  intern nutzen, schreibt aber kein neues Inventory in den Request-Checkout.
- `ExternalSourceRepositoryCacheAcquirerTests` deckt derzeit den
  Write-through-Pfad ab; `ExternalSourceRepositoryCacheWriterTests` und
  `ExternalSourceRepositoryTestSupport.SourceFixture` besitzen die
  wiederverwendbaren Generation-, `TestTempDirectory`-, Transport- und
  Cleanup-Fixtures. Direkte Acquirer-Tests müssen einen isolierten Cache-Read
  (Hit-Double oder deterministischen Miss) erhalten, damit kein
  `AppContext.BaseDirectory`-Rest das Ergebnis beeinflusst.

## In-Scope-Dateien und konkrete Änderungen

Produktionscode:

- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceRepositoryCacheReader.cs`
  und `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`:
  Die bereits vorhandene lokale `TryReadCurrent`-Fassade hinter einen
  read-only Reader-Port stellen; `LocalExternalSourceRepositoryCacheWriter`
  implementiert diesen Port weiter ohne einen zweiten Reader. Der
  Root-/Stable-Key-Pfad und die Fehlerdiagnose bleiben an einer Stelle; die
  strikte statische Reader-Logik wird nur delegiert. Der bestehende
  Write-through-Writer bleibt für Publish zuständig und behält Locks,
  Pointer- und Rollback-Semantik.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`:
  Eine eng begrenzte Materialisierungsroutine ergänzt die vorhandenen
  Storage-Primitiven für den
  `generation/content`-Baums in einen bereits reservierten Checkout. Nur
  sichere, erwartete Manifest-Dateien werden kopiert; Marker, `current`,
  Manifest und Inventory des persistenten Cache-Eintrags werden nicht in die
  Ownership-Lease übernommen. Länge/Hash/Pfad werden gegen den Read-Result-
  Vertrag geprüft; bei Copy-/TOCTOU-/Reparse-Abweichung wird fail-closed
  abgebrochen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`: Einen
  optional injizierbaren Reader am bestehenden Konstruktor ergänzen, mit dem
  unveränderten Default-Cache-Root. Nach Mapping-Validierung den Key lesen,
  bei Hit reservieren/materialisieren/Handle erzeugen und bei Miss,
  Invalidität oder kontrolliertem Materialisierungsfehler den vorhandenen
  `AcquireReservedCheckoutAsync`-Clone-/Write-through-Zweig aufrufen. Der
  erfolgreiche Cache-Hit darf Transport und Publish nicht aufrufen.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs`
  bleibt unverändert, sofern der neue Reader-Port mit den bestehenden
  `ExternalSourceRepositoryCacheReadRequest`-/`ReadResult`-Typen auskommt;
  falls ein Typ technisch ergänzt werden muss, nur als direkte
  Reuse-Vertragsstütze. Keine neue Cache-Generation, kein zweiter Key und
  kein Health-/Policy-Status.

Tests:

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`:
  Cache-Hit ohne Transport/Publish, Revision-/Solution-Weitergabe,
  unabhängige Invalidität als Miss, Materialisierungsfehler mit Cleanup,
  persistente Generation nach Handle-Dispose und Fallback in den bestehenden
  Clone-/Write-through-Pfad.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs`
  und bei Bedarf `ExternalSourceRepositoryCacheWriterTests.cs`: bestehende
  `SourceFixture`, Temp-Root, Writer-/Reader-Doubles und
  `TestTempDirectory` so ergänzen, dass Reader und Writer denselben
  testisolierten Root teilen; keine neue konkurrierende Fixture-Hierarchie.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`
  und `ExternalSourceRepositoryCancellationTests.cs`: vorhandene
  Acquirer-Konstruktionen gegen den neuen Reader-Port explizit auf
  deterministischen Miss bzw. denselben isolierten Temp-Root setzen. Die
  bestehenden HTTP-/Git-/Credential-/Cancellation-/1314-/Reparse-Assertions
  bleiben fachlich unverändert.

Eine neue Integrationstest- oder Host-Wiring-Datei ist nur zulässig, wenn der
Compiler für den bestehenden Konstruktorvertrag sie zwingend macht; fachlich
ist der Nachweis in den vorhandenen FastTests ausreichend.

## Architekturgrenze und Nicht-Scope

Innerhalb des Steps gilt folgende Ownership-Kette:

```text
persistent cache generation
        │  strict read + independent validation
        ▼
cache reuse materializer
        │  fresh CheckoutReservation / new ownership marker
        ▼
request-owned ExternalSourceCheckoutHandle
        │
        ├── existing Snapshot-/Workspace ownership
        └── Dispose => only request checkout cleanup
```

Der Cache-Reader kennt das Cache-Format, aber keinen Transport oder Snapshot.
Die Materialisierung kennt nur validierten Cache-Content und die bestehende
Checkout-Ownership. Der Acquirer entscheidet zwischen Cache-Quelle und
bestehendem Clone; Provider, Snapshot, Registry und Host sehen weiterhin nur
das bestehende Acquisition-Ergebnis.

Ausgeschlossen bleiben ausdrücklich:

- Refresh, Fetch, Refresh-Intervall, Staleness-Policy, neue Generationen und
  die Semantik einer fälligen fehlgeschlagenen Aktualisierung;
- Cache-Konfiguration, CacheRoot-User-Optionen, Retention, GC,
  Invalidierung, Health, degraded/dirty/unbuilt und Telemetrie;
- Provider-/Snapshot-/Registry-Neudesign, Host-/MCP-Wiring und EPIC-05;
- Änderungen an HTTP-/Git-/Credential-/Process-/Native-Semantik, am
  repository-spezifischen 1314-/Reparse-Fallback oder an statischer
  Decompilation. Insbesondere kein `Assembly.Load`, kein ALC- und kein
  Reflection-Ausführungspfad;
- globale DRY-, MagicValues- oder DeadCode-Sweeps sowie nicht direkt
  erforderliche Änderungen an `TD-001` bis `TD-003`.

## Abnahmekriterien (maximal 8)

1. **Strict cache hit:** Ein Hit wird nur für einen validierten `current`-
   Pointer und eine Generation akzeptiert, deren Manifest und unabhängig
   gelesenes Inventory Key, Schema, URL, Solution-Pfad und Generation binden
   und deren Content vollständig gegen erwartete Pfade, Größen und Hashes
   einschließlich Solution-Anker geprüft ist. Fehlende, beschädigte,
   zusätzliche, zu große, doppelte, unbekannte oder unsichere Daten sind kein
   Hit.
2. **Neue Request-Lease:** Jeder Cache-Hit erzeugt unter der bestehenden
   Acquirer-Staging-Wurzel einen neuen reservierten Checkout mit eigenem
   Ownership-Marker. Die persistente Generation und ihre Cache-Artefakte
   erhalten keinen Request-Besitz.
3. **Unveränderte Handle-/Snapshot-Lifetime:** Der Hit liefert den bestehenden
   `ExternalSourceRepositoryAcquisitionResult.Success`- und
   `ExternalSourceCheckoutHandle`-Vertrag mit Manifest-Revision und sicherem
   Solution-Pfad. Handle-/Snapshot-Dispose löscht nur die neue Lease; die
   persistente Generation bleibt lesbar.
4. **Cache-first ohne Doppelarbeit:** Bei gültigem Hit werden Transport und
   Cache-Publish nicht aufgerufen. `current` und die Generation werden nicht
   durch den Reuse-Schritt verändert.
5. **Fail-closed Fallback:** Bei Miss, jeder Read-Invalidität, Copy-/Hash-/Path-
   Abweichung oder Reparse-/Ownership-Fehler wird eine angefangene Lease
   bereinigt und der bestehende Clone-/Write-through-Pfad ausgeführt. Ein
   echter Cancellation-Fall wird bereinigt weitergeworfen.
6. **Bestehende Fehlersemantik:** Wenn der Fallback scheitert, bleiben
   bestehende typed Unavailable-/Fallback-Diagnosen sowie HTTP-, Git-,
   Credential-, Process-, Native-, 1314- und Reparse-Semantik unverändert.
7. **Deterministische Isolation:** Hit-, Miss-, Invaliditäts-, Cleanup- und
   Fallback-Tests verwenden vorhandene Recording-Doubles und testisolierte
   Temp-/Cache-Roots, greifen nicht remote oder per Git-Netzwerk zu und
   hinterlassen keine Marker, Prozesse oder Test-Generationen.
8. **Verifikation und Audit:** `dotnet build`, beide vollständigen
   Nicht-Stress-Test-Gates und die scoped MCP-/DRY-/MagicValues-/DeadCode-
   Nachweise sind dokumentiert; die bekannten echten Win32-1314-Skips bleiben
   transparent und Stress wird nicht automatisch ausgeführt.

## Teststrategie

Die bestehenden FastTests werden zuerst gezielt erweitert:

- validierte Generation publizieren, Reader-Hit herstellen und mit einem
  Recording-Transport beweisen, dass kein Clone läuft;
- prüfen, dass neuer Checkout und persistenter Generation-Pfad verschieden
  sind, der neue Marker gültig ist, der Handle die Manifest-Revision trägt,
  Dispose den Checkout entfernt und die Generation danach erneut lesbar ist;
- je ein bounded Negativfall für fehlenden/defekten Pointer, Manifest,
  Inventory oder Content aus der vorhandenen Read-back-Infrastruktur als
  Cache-Miss mit anschließendem Clone-/Write-through-Nachweis;
- Copy-Fehler, Lösungspfad-/Hash-Abweichung und Cancellation mit
  Ownership-/Leak-Assertions;
- bestehende Transport-, Credential-, HTTP-, Git-, Reparse- und
  Cancellation-Tests mit explizitem Reader-Miss weiterführen.

Der Coder führt nach der fokussierten Iteration in dieser Reihenfolge aus:

```powershell
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress-Tests werden nicht ausgeführt. Echte Reparse-/Symlink-Fälle dürfen
bei Win32 1314 nur nach dem bestehenden repository-spezifischen Skip-Vertrag
transparent übersprungen werden.

## MCP-, DRY-, MagicValues- und DeadCode-Disposition

Vor und nach der Implementierung sind semantische MCP-Abfragen mit dem
absoluten `projectRoot` Pflicht: `get_feature_context`/
`get_symbol_body` für Reader, ReadSupport, Writer, Storage, Reservation,
PathGuard und Acquirer; `find_references`/`get_impact` für Reader-Port,
Writer, Acquirer, Checkout-Handle und Reservation; `get_test_context` für
`AcquireAsync`; danach `get_violations` und `safeguard` im betroffenen Scope.

Der DRY-Audit bleibt auf Cache-/Acquirer-Produktions- und Testcode begrenzt
und verwendet das projektgebundene `find_duplicates`. Wiederverwendet werden
die bestehende strikte Reader-/ReadSupport-Logik, CacheStorage-
Pfad-/Copy-Primitiven, `PathGuard`, `CheckoutReservation`, Handle-Cleanup,
`SourceFixture`, `TestTempDirectory` sowie Recording-Transport und -Writer.
Ein neuer Helper ist nur erlaubt, wenn er die Materialisierungsgrenze
zentralisiert; kein zweites Key-/Manifest-/Path-Parsing.

`find_magic_values` prüft den geänderten Scope. Cache-Schema, Limits,
Directory-Namen und Fehlercodes werden aus vorhandenen Contract-Konstanten
bezogen; Refresh-/Policy-Werte werden nicht eingeführt. `find_dead_code`
prüft, dass Reader-Port und Materialisierer produktiv vom Acquirer und in
Tests genutzt werden; unsichere Low-Confidence-Kandidaten werden nicht
gelöscht. `TD-001` bis `TD-003` bleiben unverändert, weil sie keinen direkten
Reuse-Vertragsbefund darstellen.

## Risiken und Gegenmaßnahmen

- **Generation als Checkout weitergereicht:** strikt verbieten; nur eine neue
  `CheckoutReservation` darf den Handle erzeugen, und ein Test verifiziert
  unterschiedliche Pfade sowie Generationserhalt nach Dispose.
- **Veralteter oder teilweiser Hit:** Current, Manifest, Inventory und Content
  weiterhin unabhängig und fail-closed lesen; keine stale-/refresh-Policy
  hineininterpretieren.
- **Änderung während der Materialisierung:** erwartete Manifest-Dateien,
  bounded Länge/Hash, sichere Pfade und abschließende Checkout-Prüfung
  verwenden; Abweichung bereinigt die Lease und fällt auf Clone zurück.
- **Fallback verliert Ownership oder Typisierung:** Reservation und
  `ExternalSourceCheckoutHandle.Dispose` wiederverwenden; Cancellation nicht
  verschlucken; Transportfehler über den bestehenden Acquirer-/Providerpfad
  klassifizieren.
- **Testleck durch Default-Cache:** Reader und Writer in neuen Tests mit
  demselben `TestTempDirectory`-Root verdrahten oder einen expliziten
  deterministischen Miss verwenden; keinen AppContext-Cache als Testdoppel.
- **Scope-Drift zu Refresh/Health/Config:** keine Policy aus dem Hit ableiten;
  alle Staleness-, GC-, Config-, Host- und Providerfragen als Folgepakete
  dokumentieren.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#architecture` und `#test-coverage` —
  Architekturgrenzen, statische Analyse und reproduzierbare Tests einhalten.
- `.agents/rules/AiNetLinter.mdc#general` — sichere, bounded und
  nachvollziehbare Implementierung ohne Ausführungs-/Ladepfade.
- `.agents/rules/AiNetLinterRichtlinien.mdc#2 Architektur-Verbote` — keine
  Assembly-Ausführung, kein neues Netzwerk- oder Process-Verhalten.
- `.agents/rules/AiNetLinterRichtlinien.mdc#3 Windows-Umgebung & Tool-Regeln`
  und `#4 Updates & Tests` — Path-/Reparse-/1314-Grenze, MCP, Build und
  Nicht-Stress-Gates.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` —
  scoped DRY-/MagicValues-/DeadCode-Prüfung und Wiederverwendung.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` und
  `#Werkzeugwahl` — C#-Semantik über MCP mit absolutem `projectRoot`, Text
  nur über `rg`.

## Bekannte Ausnahmen

- Die echten Windows-Reparse-/Symlink-Tests können bei fehlendem Privileg
  weiterhin mit dem bestehenden Win32-1314-Vertrag transparent übersprungen
  werden; daraus darf kein allgemeiner Cache-Fallback abgeleitet werden.
- Stress-Tests werden gemäß Projektregel nicht automatisch ausgeführt.
- Bestehende solutionweite DRY-/Safeguard-/Tech-Debt-Befunde außerhalb des
  Cache-/Acquirer-Scopes werden nur dokumentiert, nicht opportunistisch
  global bereinigt.

## DoD für den späteren Coder

- [ ] Split-Gate und alle acht Kriterien sind erfüllt oder ein konkreter,
      reproduzierbarer Blocker ist im `step-result.md` festgehalten.
- [ ] Der Read-Port nutzt die vorhandene strict Reader-/ReadSupport-Kette;
      kein paralleler Cache-Parser, Key oder Manifest-Vertrag ist entstanden.
- [ ] Jeder Reuse-Erfolg besitzt eine neue, kontrolliert materialisierte
      Request-Lease; persistenter Cache und Request-Handle sind lifetime-
      und cleanup-seitig getrennt.
- [ ] Miss, Invalidität, Copy-Fehler und Cancellation sind mit bounded
      Cleanup nachgewiesen; Clone-/Write-through und typed Fallback bleiben
      unverändert.
- [ ] Testisolation, keine Remote-/Git-Netzwerkzugriffe und 1314-/Reparse-
      Invarianten sind nachgewiesen.
- [ ] MCP-Impact, Violations, scoped DRY-/MagicValues-/DeadCode-Disposition,
      Build und beide Nicht-Stress-Gates sind im Result dokumentiert.
- [ ] `tasks/decompiled-assembly-analysis/step-029/step-result.md` ist
      erstellt; `task-state.md` wird erst nach Coder-/Kritiker-Durchlauf auf
      den tatsächlichen Status gesetzt.

## Exakter Coder-Hand-off

> Neuer Coder-Agent für `decompiled-assembly-analysis`, kein bestehendes
> Agentenprofil oder vorherigen Agenten wiederverwenden. Lies zuerst die zehn
> `read_first`-Dateien dieses Plans sowie die drei Regeldateien und die
> einschlägige Dev-Loop-Orchestrierung; lade PathGuard, Reservation und
> Fixtures nur bei Bedarf nach. Verwende für jede C#-Semantikabfrage
> `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` und `rg` ausschließlich
> für Text. Ändere nur den In-Scope-Code und die zugehörigen Tests.
>
> Implementiere genau den cache-backed Initial-Acquisition-Vertrag: exponiere
> die vorhandene lokale Writer-Read-Fassade hinter
> `IExternalSourceRepositoryCacheReader` in einer kleinen read-only
> Schnittstelle, delegiere strikt an `ExternalSourceRepositoryCacheReader` und
> `ExternalSourceRepositoryCacheReadSupport`, reserviere bei Hit über
> `ExternalSourceRepositoryCheckoutReservation`, materialisiere in den neuen
> Checkout und erzeuge daraus den vorhandenen Handle mit Manifest-Revision.
> Übergib niemals `GenerationPath` direkt an Handle, Snapshot oder Workspace.
> Bei Miss, Invalidität, Copy-/Path-/Hash-Fehler räume die neue Lease auf und
> rufe den bestehenden Clone-/Write-through-Pfad auf; bei Cancellation räume
> auf und rethrow. Ändere weder Transport-, Credential-, Process-, Native-,
> HTTP-, Git-, 1314-/Reparse-, Snapshot- noch Registry-Semantik.
>
> Ergänze zuerst die vorhandenen Cache-Acquirer-Tests und Test-Support-Fixtures
> für Hit, Miss, Invalidität, Materialisierungsfehler, Cleanup und Fallback;
> injiziere für alle betroffenen Tests einen isolierten Reader bzw. einen
> deterministischen Miss. Führe anschließend die fokussierten Tests, Build,
> beide Nicht-Stress-Gates und die scoped MCP-/Audit-Abfragen aus. Erzeuge
> danach `step-029/step-result.md` mit Dateien, Kriterien, Verifikation,
> Leaks, MCP-Befunden und offenen Risiken. Kein Refresh, Fetch, Config,
> Retention/GC, Health/degraded/dirty/unbuilt, Host-/MCP-Wiring,
> Provider-/Snapshot-/Registry-Redesign, EPIC-05 oder globaler Sweep.
