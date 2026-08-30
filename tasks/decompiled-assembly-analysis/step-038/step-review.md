---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 038
epic: EPIC-04
step_type: correction
corrects: 037
reviewed_by: kritiker
reviewed_by_model: gpt-5
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-30T08:14:34+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 038: Checkout-Trust-Attestation bis Materialisierung und Publish

## Verdict

- [ ] **approved**
- [x] **issues** — ein zusammenhängendes Trust-/Materialisierungs-Korrekturpaket ist erforderlich
- [ ] **blocked**

Step 038 ist nicht freigabefähig. Die Test-Fassade ist jetzt korrekt explizit,
und die bestehende Datei-Lock-Lifetime schützt bereits vorhandene Dateien in
den positiven Publish-/Workspace-Fällen. Die primäre Sicherheitszusage ist
jedoch weiterhin nicht erfüllt: Der Lease bindet weder den vollständigen
Checkout-Namespace noch alle Eintrittspfade vor der Erzeugung des Handles.
Zusätzlich gibt es eine fail-closed-Lücke im Statusparser, einen
Cancellation-/InvalidData-Leak bei partieller Lease-Akquisition und eine
weiterhin abgeschwächte Dirty-Propagation.

Alle Befunde gehören in ein größeres Korrekturpaket an derselben
Checkout-Trust-/Materialisierungsgrenze. Es ist kein Audit-only- oder
Assertion-Mini-Step.

## Geprüft

- [x] `.agents/rules/*` vollständig, besonders `AiNetLinter-McpWorkflow.mdc`
  und `AiNetLinterRichtlinien.mdc`
- [x] `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md`,
  `initial-prompt.md`, `follow-up-strategy.md`, `roadmap.md`,
  `task-state.md`, `tech-debt.md`, Step-038-Plan/Result/Codemap und
  Step-037-Review `078c3e15`
- [x] Codercommit `170b446c6038952dbf2790fe030c5ac2051832ff` sowie
  Dokumentationscommit `c1efb9ed730bb4c986711ac56472c23d9fcbcb08`
- [x] projektgebundenes AiNetLinter-MCP mit absolutem
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`: `get_feature_context`,
  `find_symbol`/`get_symbol_body`, `find_references`/`get_impact`,
  `get_test_context`, `get_violations` und `safeguard`
- [x] Statusparser, Git-Statusargumente, Ownership-/Attestation-Vertrag,
  Cache-Copy/Manifest/Inventory/Readback/Pointer, Workspace-Open,
  Snapshot-Lifetime, Acquirer/Refresh/Reuse/Provider/Selection/Tool sowie
  alle sichtbaren Cancellation-/Cleanup-Pfade
- [x] scoped DRY-Audit gemäß `drift-audit`: Clone exact/near und Structural;
  Magic-Values und High-Confidence-Dead-Code
- [x] Build, fokussierte Parser-/Materializer-Regressionen, beide vollständigen
  Nicht-Stress-Gates, Prozess-/Temp-Isolation und der bekannte Safeguard-
  Korridor

## Befunde

### MAJOR-001 — Lone-CR-Status wird als Clean akzeptiert

In `ExternalSourceRepositoryCheckoutStatus.AssessStatus` (Zeilen 74–107)
wird nur an `\n` gesplittet. `TryNormalizeStatusRecord` (Zeilen 110–123)
entfernt danach jedes `\r`, das am Recordende steht, ohne zu prüfen, ob es
tatsächlich Teil eines `\r\n`-Zeilentrenners war.

Reproduzierbare Eingabe: erfolgreicher `git status --porcelain=v1
--untracked-files=all --ignored=all`-Prozess, leeres stderr und
`stdout = "?? .ainetlinter-owner\r"`. Daraus wird ein gültiger Owner-Record
und anschließend `null`/Clean. Ein einzelnes `\r` ist aber keine vollständige
CRLF-Framierung; unerwartete oder abgeschnittene Statusausgabe muss
`Unverified` ergeben. Die vorhandene Matrix deckt leere, führende und innere
Records sowie CRLF ab, aber nicht diesen Lone-CR-Fall.

Auswirkung: Ein beschädigter/unvollständig gerahmter Status kann die
Clean-Grenze passieren. Die Korrektur muss die Zeilenframing-Grammatik
explizit validieren und eine Regression für genau diese Eingabe ergänzen,
ohne den erlaubten leeren erfolgreichen Output oder den exakten Owner-Marker
abzuschwächen.

### MAJOR-002 — Materialisierungs-Lease schützt nur den zum Acquire-Zeitpunkt bekannten Dateisatz

`ExternalSourceCheckoutMaterializationLease.TryAcquire` (Zeilen 20–61)
ruft `WalkFiles` auf und öffnet für jede bereits vorhandene Datei einen
`FileShare.Read`-Handle. `WalkFiles`/`WalkDirectory` in
`ExternalSourceRepositoryCacheStorage` (Zeilen 44–84) enumeriert jedes
Verzeichnis jedoch nur einmal; Verzeichnisse und die Checkout-Wurzel selbst
werden nicht exklusiv gegen neue Einträge gebunden.

Die kritische Reihenfolge ist deterministisch herstellbar:

1. die erste explizite Attestationsprüfung nach dem Lease-Acquire an der
   vorhandenen TCS-Barriere pausieren,
2. eine neue Datei oder ein neues Unterverzeichnis im Checkout anlegen,
3. die Attestation freigeben,
4. nach `WriteGeneration` und vor der nächsten Attestation über den
   vorhandenen `BeforePointerPublishedAsync`-Seam den neuen Eintrag löschen,
5. die zweite Statusprüfung und den Pointer-Publish abwarten.

Der neue Eintrag war nie gelockt, wird von `CopySource` dennoch in die
Generation übernommen, kann danach gelöscht werden, und ein erneuter Git-
Status sieht wieder Clean. Manifest/Inventory/Readback bestätigen nur die
dadurch selbst erzeugte Generation; sie liefern keinen unabhängigen Nachweis,
dass ihr Inhalt aus dem attestierten Checkout stammt. Damit kann ein
untrusted transienter Eintrag in einer erfolgreich publizierten Generation
landen. Für Workspace-Open gilt derselbe Namespace-Bypass: Ein neu erzeugtes,
vom SDK-Projektglob erfasstes Source-File kann nach der ersten Attestation
gelesen werden; der abschließende Status kann nach seiner Entfernung wieder
Clean sein.

Der aktuelle Race-Test prüft nur das Überschreiben der bereits vorhandenen
`Program.cs` und beweist daher lediglich den FileShare-Fall, nicht den
geforderten „kein untrusted Generation-/Snapshot-/Registry-Lease“-Vertrag.
Die Korrektur braucht eine echte unveränderliche Tree-/Git-Materialisierung
oder eine Namespace-Sperre, die Create/Delete/Rename ebenso ausschließt, und
einen deterministischen Race-Test mit neuem Eintrag plus Assertion auf
Rollback/Unavailable bzw. ohne untrusted Generation, Snapshot oder Lease.

### MAJOR-003 — Cache-Refresh und Cache-Reuse materialisieren vor jeder Lease-Bindung

`ExternalSourceRepositoryCacheMaterializer.Materialize` (Zeilen 13–61)
prüft Ownership nur vor und nach dem Kopieren. Die beiden direkten Aufrufer
sind `ExternalSourceRepositoryCacheRefresh` (Zeile 193) und
`ExternalSourceRepositoryCacheReuse` (Zeile 86). Beide erzeugen bzw. füllen
den Checkout, bevor ein `ExternalSourceCheckoutHandle` und damit dessen
Materialisierungs-Lease existiert; bei Reuse wird der Handle erst in
`CreateCheckout` nach dem Copy erzeugt.

Während `CopyExpectedFile` kann daher ein bestehender Zielinhalt geändert
oder ein neuer Eintrag angelegt werden, ohne dass ein Ownership-/Content-Lease
die Operation bindet. Die nachgelagerte Ownership-Prüfung beweist nur den
Marker und nicht die unveränderte, aus der Current-Generation materialisierte
Dateimenge. Ein persistenter Fehler wird teilweise später entdeckt, aber ein
Mutation-then-revert-Fenster ist weder verhindert noch durch eine immutable
Attestation erfasst. Refresh und Reuse benötigen dieselbe Lease-/Snapshot-
Grenze vor dem ersten Copy bis zum Abschluss der jeweils relevanten
Attestation, Validierung und Materialisierung.

Dieser Befund ist mit MAJOR-002 als ein Trust-/Materialisierungs-Paket zu
beheben. Die Tests müssen für Refresh und Reuse die Mutation während des
Copies deterministisch anhalten und beweisen, dass kein untrusted Checkout,
Snapshot, Cache-Current oder Registry-Lease entsteht.

### MAJOR-004 — Partielle Lease-Akquisition leakt Handles bei Cancellation und unsicheren Daten

`ExternalSourceCheckoutMaterializationLease.TryAcquire` sammelt bereits
geöffnete `FileStream`s in `lockedFiles` (Zeilen 32–46), fängt aber nur
Ausnahmen ab, für die `ExternalSourceRepositoryFailurePolicy.IsFileSystemException`
(Zeilen 23–27) `true` liefert. `OperationCanceledException` und
`InvalidDataException` (zum Beispiel ein Reparse-/unsicherer Eintrag nach
bereits gelockten Dateien oder ein ungültiger Ownership-Marker) werden dort
nicht erfasst. Es gibt keinen `finally`, der die bis dahin gesammelten
Handles freigibt.

Reproduktion: während `WalkFiles` nach mindestens einem Callback die
Cancellation auslösen oder den nächsten Eintrag als Reparse/ungültig
einbringen. Die Methode wirft bzw. verlässt den Pfad mit den vorherigen
Handles noch offen. Der aufrufende Handle hat dann keinen gültigen Lease-
Besitz, aber `ownership.TryCleanup` kann auf Windows wegen der offenen
Handles fehlschlagen. Die vorhandenen Cancellation- und 1314-Regressionen
erzwingen diese partielle-Akquisition-Reihenfolge nicht.

Die Korrektur muss jeden nicht erfolgreich übernommenen Lease über einen
ausnahmesicheren Cleanup zurückrollen, Cancellation unverändert weitergeben
und eine deterministische Regression auf erfolgreichen Cleanup bzw. typed
Failure ohne offene Handles ergänzen.

### MINOR-001 — Verfügbarer Dirty-Transport wird zu Unverified abgeschwächt

In `ExternalSourceRepositoryAcquirer.ValidateCheckout` (Zeilen 392–399)
führt ein verfügbares Transportresultat mit `CheckoutTrust.Dirty` über den
generischen `CheckoutValidationResult.Failure`-Pfad. Der Aufrufer verwendet
anschließend in den Zeilen 175–178 die `FailureAfterCleanup`-Überladung ohne
Trust-Argument. `ExternalSourceRepositoryCacheRefresh` macht in den Zeilen
215–223 dasselbe für `!IsVerifiedTransport`.

Dadurch wird `Dirty` in genau dem verfügbaren-but-dirty Fall als
`RepositoryCheckoutUnverified` weitergereicht. Der Status bleibt zwar
Unavailable und erhält keinen Trusted-Bypass; die geforderte typisierte
Propagation durch Acquirer/Refresh/Provider/Selection/Tool ist aber nicht
erfüllt. Der aktuelle Regressionstest prüft nur `IsAvailable=false` und
beweist daher diesen Pfad nicht. Die Korrektur muss den ursprünglichen
`CheckoutTrust` einschließlich Cleanup-/Degraded-Metadaten erhalten und eine
available+Dirty-Matrix ergänzen.

## Erhaltene Verträge / approved-Anteile

- Die Statusabfrage verwendet weiterhin `--ignored=all`; normale modified-,
  untracked-, ignored- und malformed Records werden im geprüften Pfad
  fail-closed typisiert.
- Die Test-Fassade ergänzt fehlende Produktionsattestations nicht mehr.
  `Success(revision)` ohne Attestation wird in
  `Acquirer_MissingProductionAttestation_IsRejectedAndCleaned` abgewiesen;
  positive Doubles liefern `ForTesting` explizit.
- Für bereits vorhandene Dateien hält der Handle den Lease über Attestation,
  Cache-Publish, Workspace-Open und Snapshot-Lifetime; die bestehenden
  Publish-/Workspace-Race-Tests blockieren deren Überschreiben. Rollback,
  Pointer-/CurrentChanged-, Last-good-/Degraded-/Unavailable-, Cancellation-,
  Credential-Isolation-, 1314-/Reparse- und gewöhnliche statische
  Decompilation-Fallbacks bleiben im geprüften Stand sichtbar.
- Der positive Clean-/Verified- und Cache-Reuse-Pfad bleibt grundsätzlich
  erhalten; die Kritik betrifft die fehlende Unveränderlichkeitsgrenze, nicht
  eine pauschale Sperre erfolgreicher statischer Fallbacks.

## Gates und Audits

### Build und Tests

| Gate | Ergebnis |
|---|---:|
| `dotnet build --no-restore` | **0 Warnungen / 0 Fehler** |
| fokussierte Status-/Attestation-Tests | **20 bestanden / 0 Skips** |
| fokussierter Snapshot-Materializer | **3 bestanden** |
| FastTests, `Category!=Stress` | **2182 bestanden, 2 bekannte Skips, 2184 gesamt** |
| IntegrationTests, `Category!=Stress` | **370 bestanden, 1 Fehler, 371 gesamt** |
| Stress | **nicht ausgeführt** |

Der eine Integrationsfehler ist reproduzierbar
`McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults` in
`src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs:244`:
`Safeguard-Live-Score 4,163934426229508` liegt unter dem unveränderten
Korridor `>= 5.0`. Das ist der bekannte globale Baseline-Korridor und kein
neuer Step-038-Source-Befund; der Gate-Lauf ist dennoch rot.

Die beiden bekannten Reparse-Skips sind
`AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains` und
`PublishAsync_ActualReparseEntryFailsClosed`; beide melden unter Windows
`ERROR_PRIVILEGE_NOT_HELD (1314)`. Stress wurde nicht ausgeführt.

### MCP und scoped Audits

- `find_symbol` fand die betroffenen Produktionssymbole; Bodies und
  Feature-Kontexte wurden mit MCP gelesen. `find_references`/`get_impact`
  bestätigten insbesondere die Cache-Materializer-Aufrufer in Refresh und
  Reuse sowie die Acquirer-/Snapshot-Kanten.
- `get_violations` findet im Produktionsscope
  `src/AiNetLinter/Mcp/Assemblies` nur den bekannten
  `MaxDirectoryChildren`-Befund; FastTests liefern dort keine Violations.
- `find_duplicates`, `scopeDir=src/AiNetLinter/Mcp/Assemblies`,
  `scopeType=production`, `minTokens=20`: Clone exact **0 Cluster bei 418
  Methoden**, Clone near **0 Cluster bei 418 Methoden**. Der Structural-Lauf
  meldet **4** manuell geprüfte Near-Kandidaten; sie sind semantisch
  verschieden und erzeugen keinen neuen direkt zu behebenden DRY-Fund.
- `find_dead_code`, Production/Assemblies, `private_internal`, `high`,
  `members`: **0** High-Confidence-Kandidaten bei 170 Symbolen.
- `find_magic_values` im vollständigen Production/Assemblies-Scope meldet
  **105 Treffer / 103 Einträge in 66 Dateien**, überwiegend bestehende
  Diagnose-/Konstanten-/Lokalisierungswerte. Der `changedOnly=true`-Lauf ist
  am sauberen Review-HEAD erwartungsgemäß leer; daraus wird kein neuer
  Magic-Value-Tech-Debt abgeleitet.

### Safeguard: Threshold 8 ehrlich

| Scope | Score | Ergebnis bei Threshold 8 |
|---|---:|---|
| global | **4,163934426229508/10** | **FAIL** |
| `src/AiNetLinter/Mcp/Assemblies` | **4,283950617283951/10** | **FAIL** |

Der globale Safeguard-Korridor bleibt wegen vier bekannter Baseline-Funde
rot: `Assemblies`-Verzeichnis 66 statt höchstens 30 Kinder,
`DaemonHostCommand`-Footprint 3097 statt höchstens 2500,
`AssemblyAnalysisToolRegistrations` 2622 statt höchstens 2500 sowie das
Task-Verzeichnis 46 statt höchstens 30. Threshold 8 wird nicht als PASS
umdeklariert und kein Baseline-Fund dem Step zugerechnet.

### Isolation / Leaks

Während der Prüfung liefen nach den Gates keine `testhost`-/`vstest`-Prozesse.
Im Workspace war jedoch bereits vor dem Testlauf (CreationTime 07:31, Gates
ab 08:02) ein ignorierter, eindeutig benannter Testpfad
`temp/external-source-acquirer-cleanup-state-84e07a96783a4ae5afbd3831e0fa30aa`
mit Ownership-Marker und `BaselineMini.slnx` vorhanden. Er ist nicht dem
Kritikerlauf zurechenbar und wurde wegen der gesperrten destruktiven
Shell-Aktion nicht entfernt. Deshalb darf der Review nicht „keine Temp-Leaks“
behaupten; der Korrektur-Step muss den Cleanup-/Lease-Fall nachweisbar
beheben und den Pfad anschließend in einer frischen Testumgebung erneut
prüfen. Netzwerk-, Credential- und Assembly-Ladeaktionen wurden nicht
ausgeführt.

## Nächste Aktion

Ein neuer, größerer Correction-Step mit `corrects: step-038` soll gemeinsam:

1. die Statusgrammar einschließlich Lone-CR und aller unerwarteten Framing-
   Fälle fail-closed machen,
2. eine echte unveränderliche Checkout-/Namespace-Bindung durch
   Cache-Copy, Hash/Manifest/Readback, Pointer-Publish, Workspace-Open und
   Snapshot-Lifetime herstellen, einschließlich Refresh/Reuse vor der
   Handle-Erzeugung,
3. partiellen Lease-Cleanup bei Cancellation, InvalidData/Reparse und
   Ownership-Verlust ausnahmesicher schließen,
4. Dirty unverfälscht bis Provider/Selection/Tool propagieren und
5. deterministische lokale Tests für neue Dateien/Directories, Delete/
   Rename, transienten Mutation-then-Revert, Cache/Workspace/Generation/
   Snapshot/Registry-Lease, Cleanup und available+Dirty ergänzen.

Danach sind Build, beide vollständigen Nicht-Stress-Gates, die bekannten
1314-Skips, Threshold-8-Safeguard und scoped Audits erneut mit den tatsächlich
ausgeführten Werten zu dokumentieren. `tech-debt.md` bleibt unverändert:
Die Audit-Kandidaten sind bestehend oder innerhalb dieses Korrekturpakets
direkt zu beheben, kein neuer separater Tech-Debt-Eintrag.
