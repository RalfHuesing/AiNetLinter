---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 036
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-30T05:25:11+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 036: Gitea-Source-of-Truth mit Clean-Checkout und transparentem degraded Refresh-Vertrag absichern

## Verdict

- [ ] **approved**
- [x] **issues** — ein gemeinsames Korrekturpaket für denselben Source-Policy-Vertrag ist erforderlich
- [ ] **blocked**

Der Step ist wegen zweier MAJOR-Verstöße gegen den Source-of-Truth-/Clean-
Checkout-Vertrag und eines MINOR-Verlusts im typisierten Trust-Zustand nicht
freigabefähig. Die getesteten positiven Pfade, die Degraded-Weitergabe und die
Abschlussgates sind ansonsten grün.

## Geprüft

- [x] vollständiger Step-Plan, Step-Result sowie beide Codercommits
- [x] alle 26 geänderten Dateien und bestehende Vertrags-/Testabhängigkeiten
- [x] Rules-Konformität und projektgebundenes AiNetLinter-MCP
- [x] Logik-, Ownership-, Cancellation-, Reparse-, Cache- und Pointer-Race-Pfade
- [x] Provider-/Selection-/Assembly-Tool-Projektion und statischer Fallback
- [x] scoped DRY-, Magic-Value-, Dead-Code- und Safeguard-Prüfungen
- [x] Build-/Fast-/Integration-Gates und Testhost-/Temp-Leaks

## Befund

### Plan-Erfüllung

Die Commits `377b5360` (Code/Tests) und `39fb9fba` (Dokumentation) ändern die
im Result genannten 26 Dateien. Das immutable Health-Modell mit
`Verified`/`Degraded`/`Unavailable`, der typisierte Checkout-Trust, die
Statusabfrage vor Fetch/Reset und danach, die Last-good-Metadaten, die
CurrentChanged-Wiederverwendung sowie die sichtbare Provider-/Selection-
Weitergabe sind im vorgesehenen Umfang vorhanden. Die Tests decken die
positiven Clone-/Refresh-/Reuse-Pfade, Cleanup/Cancellation, Pointer-Races,
ConfigurationFailure und den statischen Decompilation-Fallback ab.

Die zentrale Zusicherung „nur sauber verifizierter Commit darf Source oder
Cachegeneration liefern“ ist aber nicht vollständig erfüllt: ignorierte lokale
Dateien werden als clean akzeptiert, und zwischen der letzten Git-Prüfung und
Source-/Cache-Materialisierung besteht ein ungeschütztes TOCTOU-Fenster.

### Rules-Konformität

Die semantischen Abfragen wurden mit absolutem `projectRoot` ausgeführt:
`get_feature_context`, `find_symbol`/`get_symbol_body`,
`find_references`/`get_impact` und `safeguard` für den geänderten Assembly-
Scope. Es gibt keinen direkten Violation-Befund in den geänderten Symbolen.
Die 26 geänderten Dateien liegen innerhalb der geltenden Datei-/Testgrenzen.

Der scoped DRY-Audit findet keine neue tokenbasierte Duplikation in der
Assembly-Produktion (`0/385`); die vier strukturellen Kandidaten sind
semantisch getrennte Result-/Mapping-Helfer oder bestehender Code. Der
Dead-Code-Audit findet `0` High-Confidence- und `35` Low-Confidence-Treffer
in der Produktions-Assembly, ausschließlich bestehende Interop-/Property-
Kandidaten; im Testscope gibt es `0` Treffer. Der changed-only-
Magic-Value-Lauf am sauberen Review-HEAD liefert erwartungsgemäß `0`; der
vor dem Doku-Commit dokumentierte geänderte Scope hatte `6` Treffer, ohne
neuen unbereinigten direkten Befund.

`safeguard` bleibt mit `5,6607565/10` global und `5,7631579/10` für
`src/AiNetLinter/Mcp/Assemblies` bei Threshold 8 **FAIL**. Die drei Befunde
(`Mcp/Assemblies`-Directory-Children, `DaemonHostCommand`-Footprint und
Task-Directory-Children) sind bestehende Baseline-Verstöße und kein neuer
direkter Step-036-Fund.

### Logische Korrektheit

Die Transportsequenz prüft den Status vor der mutierenden Fetch-/Reset-
Sequenz, prüft ihn danach erneut und liest anschließend die HEAD-Revision.
Dirty/unverified Ergebnisse werden in den getesteten Gitea-Pfaden nicht als
Source oder Cachegeneration veröffentlicht. Ein fehlgeschlagener stale Refresh
erhält den alten Pointer und Last-good-Commit nur als sichtbare Degraded-
Metadaten; ohne Last-good bleibt der Zustand Unavailable. Der
`CurrentChanged`-Pfad kann einen sicheren frischen Current wiederverwenden,
andernfalls gibt es keinen neuen Snapshot-/Lease-Pfad. Provider-Degraded wird
vom Assembly-Tool mit sicherer Diagnose und statischem Fallback verarbeitet;
ConfigurationFailure bleibt terminal, und NoMatch/Ambiguous/
ProviderUnavailable/Capability-Fallbacks bleiben positiv.

Die folgende Korrektur ist dennoch zwingend, bevor der Source-Policy-Vertrag
als abgeschlossen gelten kann.

## Findings

### 1. Ignorierte lokale Dateien passieren das Clean-Gate und gelangen in die Cachegeneration

- **Severity:** MAJOR
- **Ebene:** Logik / Konzept-Treue / Source-of-Truth
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs:41-66,69-86`; `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs:153-204,206-214`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs:15-43,86-132`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs:152-165,226-246`
- **Was:** Das Clean-Gate ruft nur `git status --porcelain=v1 --untracked-files=all` auf. Ignorierte untracked Dateien werden von diesem Aufruf nicht ausgegeben und `HasUnexpectedChanges` sieht deshalb einen leeren Output als clean an. Die anschließende Cachekopie läuft dagegen über alle regulären Dateien des Checkouts und filtert nur den Ownership-Marker, nicht Git-ignorierte Dateien. Ein lokales, von `.gitignore` erfasstes Source-/Projektfile kann daher trotz `ExternalSourceCheckoutTrust.Clean` in Snapshot und Cachegeneration gelangen.
- **Reproduktion:** In einem reservierten Checkout mit einem getrackten `.gitignore`-Eintrag wie `*.cs` eine lokale `src/Injected.cs` anlegen. Der in `ExternalSourceRepositoryCheckoutStatus` gebaute Statusaufruf liefert keinen Statussatz; `ExecuteAsync` liefert `null` statt `RepositoryCheckoutDirty`. `CopySource` nimmt `src/Injected.cs` anschließend in das Manifest auf, weil `WalkFiles` keine Ignore-Information abfragt. Der gleiche Git-Semantiknachweis ist im aktuellen Repository reproduzierbar: `git status --porcelain=v1 --untracked-files=all` ist leer, während `git check-ignore -v src/AiNetLinter/bin/Debug/net10.0/AiNetLinter.deps.json` `.gitignore:10:[Bb]in/` meldet und die Datei existiert.
- **Auswirkung:** Bei einem stale Refresh bleiben ignorierte lokale Artefakte nach `reset --hard` erhalten und können als externe Source bzw. als neue Generation mit dem alten/vermeintlich verifizierten Commit publiziert werden. Damit ist „Gitea als einzige Source of Truth“ nicht garantiert.
- **Nächste Aktion:** Das Status-Gate muss ignorierte Einträge mit erfassen, z. B. über die explizite Git-Option `--ignored=all`, und jeden Statussatz außer dem eng begrenzten Ownership-Marker fail-closed als Dirty behandeln. Eine fokussierte lokale Regression muss eine ignorierte Datei vor und nach Refresh zurückweisen und darf weder Snapshot noch Generation veröffentlichen.

### 2. Nach der letzten Git-Prüfung bleibt ein ungeschütztes Source-/Cache-Materialisierungsfenster

- **Severity:** MAJOR
- **Ebene:** Logik / Ownership-Lifetime / Materialisierung und Publish
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs:153-224`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:166-233`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs:365-386`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs:23-50`; `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs:47-79`
- **Was:** Nach dem Fetch-/Reset-Status und `rev-parse HEAD` attestiert der Transport den Checkout als verifiziert. Der Acquirer prüft danach nur noch Metadaten, Ownership, Reparse-Sicherheit und den Solution-Pfad. Der Cache-Writer prüft ebenfalls Ownership/Reparse und kopiert anschließend den gesamten Checkout; der Snapshot-Materializer prüft nur `IsDisposed` und öffnet die Solution. Der Ownership-Marker schützt den Pfad nicht exklusiv vor einer lokalen Mutation. Eine Änderung eines getrackten Files zwischen der letzten Git-Prüfung und `CopySource` oder `OpenSolutionAsync` wird deshalb weder an der Commit-Identität noch am Trust-Zustand sichtbar.
- **Reproduktion:** Einen Transport mit gültiger Revision und cleanem Status attestieren lassen und danach, vor `PublishCache` bzw. während `MaterializeAsync`, `src/Program.cs` ändern. `ValidateCheckout` und `ValidateSourceCheckout` liefern weiterhin Erfolg; `WriteGeneration` übernimmt den geänderten Inhalt bei unverändertem `LoadedRevision`, oder der Snapshot liest ihn direkt. Die bestehenden Tests modellieren die Änderung nur innerhalb des Fakes vor dessen `Success`-Rückgabe und prüfen kein Mutation-after-validation-Fenster.
- **Auswirkung:** Eine externe Source oder Cachegeneration kann Inhalt liefern, der nicht dem verifizierten Gitea-Commit entspricht. Die Pointer-/Publish-Race-Tests schützen nur den Generation-Pointer, nicht die Bindung des Inhalts an den verifizierten Commit.
- **Nächste Aktion:** Das Korrekturpaket muss die Materialisierungsquelle an eine unveränderliche Repräsentation des verifizierten HEAD binden (oder eine nachweislich exklusive Checkout-Lifetime mit erneuter Status-/HEAD-Prüfung unmittelbar vor und nach dem Kopieren durchsetzen). Jede erkannte Drift muss den Checkout bereinigen, ohne Snapshot, Generation oder Registry-Lease zu veröffentlichen. Ergänze eine deterministische Mutation-after-validation-Regression für Cache und Provider.

### 3. Acquirer verliert den typisierten Dirty-Trust bei einem Transportfehler

- **Severity:** MINOR
- **Ebene:** Result-Vertrag / Diagnose- und Health-Projektion
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs:62-65`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:158-164`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs:103-121`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs:109-132`
- **Was:** Der transportnahe Status setzt bei einem Dirty-Checkout korrekt `CheckoutTrust.Dirty`. Sobald der Acquirer das nicht verfügbare Transportresultat über `FailureAfterCleanup` in ein Acquisition-Result projiziert, wird der Trust-Wert nicht mitgeführt; der Result-Konstruktor setzt für jedes nicht verfügbare Ergebnis pauschal `CheckoutTrust.Unverified`. Damit sind Dirty und Unverified am explizit eingeführten Acquisition-Trust-Contract nicht mehr unterscheidbar, obwohl beide typisiert erkannt wurden.
- **Reproduktion:** Ein `ExternalSourceRepositoryTransportResult` mit `isAvailable: false`, `checkoutTrust: ExternalSourceCheckoutTrust.Dirty` und `InvalidResponse` an `ExternalSourceRepositoryAcquirer` liefern. Der Transportwert ist `Dirty`, das von `AcquireAsync` zurückgegebene Ergebnis hat deterministisch `CheckoutTrust.Unverified`. Fail-closed bleibt die Sicherheitsentscheidung, aber die neue Trust-Projektion ist semantisch falsch.
- **Nächste Aktion:** `FailureAfterCleanup` und die Acquisition-Failure-Fabrik müssen den typisierten Trust als sicheren Wert übernehmen; Pfad-/Cleanup-/unverifizierte Fehler dürfen explizit `Unverified` setzen, der Statusfehler muss `Dirty` erhalten. Ergänze direkte Policy-/Result-Tests für alle drei Trustwerte und die Health-Invarianten. Der neue `ExternalSourceRepositorySourcePolicy` hat aktuell keine direkt zugeordnete Testdatei; diese Lücke muss im selben Korrekturpaket geschlossen werden.

## Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (2165 bestanden, 2 Skips, 2167 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (370 bestanden, 0 Skips, 370 gesamt)
```

Die zwei Skips sind unverändert die dokumentierten Reparse-Point-Fälle wegen
`Win32 ERROR_PRIVILEGE_NOT_HELD (1314)`:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

`Category=Stress` wurde nicht ausgeführt. Nach den Läufen blieben keine
Verzeichnisse unter `temp` sowie keine `testhost`-/`vstest.console`-Prozesse
zurück. Drei normale `dotnet MSBuild.dll`-Node-Reuse-Prozesse waren resident;
sie sind keine Testhost-Leaks.

## Reproduzierbare MCP-/Impact-Werte

- `get_impact` auf `377b5360^` erfasst **26 geänderte Dateien**, **146 geänderte Symbole** (100 angezeigt wegen Tool-Limit), **601 Call-Sites** und **51** statisch zugeordnete Testassoziationen; der Violation-Teil bleibt leer.
- `get_test_context` findet für `ExternalSourceRepositoryCacheRefresh` 9 fokussierte Component-Tests und für `GiteaGitRepositoryTransport` 11 fokussierte Component-Tests. Für die zentrale neue `ExternalSourceRepositorySourcePolicy` gibt es keine direkte statische Testzuordnung.
- `find_duplicates` im Assembly-Produktionsscope: tokenbasiert **0/385**, strukturell **4** Kandidaten; keiner ist eine neue direkt zu behebende Duplikation.
- `find_dead_code`: Produktion **0 high / 35 low confidence**, Testscope **0**; die Produktionskandidaten sind bestehende Interop-/Aliasfälle.
- `find_magic_values`: Coder-Lauf auf dem geänderten Vor-Commit **6**; ein Lauf am sauberen Review-HEAD mit `changedOnly=true` ist erwartungsgemäß **0**. Kein neuer ununterdrückter direkter Befund.
- `safeguard`: global **5,6607565/10**, Assembly-Scope **5,7631579/10**, beide bei Threshold 8 **FAIL** wegen derselben drei Baseline-Befunde; keiner betrifft direkt die Step-036-Dateien.

## Nächste gebündelte Aktion

Keinen Audit-only-Mini-Step anlegen. Die drei Befunde gehören in ein einziges
größeres Korrekturpaket für den Step-036-Source-Policy-Vertrag: vollständiges
Clean-Gate inklusive ignorierter Dateien, driftfeste Bindung von
Materialisierung/Cache-Publish an den verifizierten Commit, Trust-Propagation
bis zum Acquisition-Result sowie direkte Policy-/Mutation-Regressionen.
Nach dieser Korrektur sind beide vollständigen Nicht-Stress-Gates, die
Reparse-1314-Skip-Erklärung, Ownership-/Temp-Leak-Prüfung und die scoped
MCP-Qualitätsnachweise erneut auszuführen.
