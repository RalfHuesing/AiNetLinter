---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 037
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-30T06:50:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 037: Checkout-Attestation bis Cache-Publish und Workspace-Materialisierung binden

## Verdict

- [ ] **approved**
- [x] **issues** — ein gebündeltes Korrekturpaket für denselben Checkout-Trust-/Materialisierungsvertrag ist erforderlich
- [ ] **blocked**

Der Step ist wegen zweier MAJOR-Verstöße gegen den Primärvertrag nicht
freigabefähig. Zusätzlich ist die positive Testinfrastruktur noch zu permissiv
und beweist den produktiven Attestation-Vertrag nicht vollständig. Die
Ignoriert-Datei-Erkennung, die typisierte Dirty-Weitergabe, Cleanup/
Cancellation, Last-good/Degraded/Unavailable, CurrentChanged-Reuse, positive
Fallbacks sowie die 1314-/Reparse- und Credential-Isolation-Invarianten sind
ansonsten im geprüften Stand erhalten.

## Geprüft

- [x] vollständiger Step-037-Plan, Result, Codemap sowie Step-036-Plan, Result und Review
- [x] beide Codercommits `093f9d7a` und `04e37bea` sowie der unveränderte Arbeitsbaum
- [x] alle geänderten Produktions-/Testdateien und die relevanten Acquirer-, Refresh-, Provider-, Selection- und Tool-Pfade
- [x] projektgebundenes AiNetLinter-MCP mit absolutem `projectRoot`: `get_feature_context`, `find_symbol`/`get_symbol_body`, `find_references`/`get_impact`, `get_test_context`, `get_violations` und `safeguard`
- [x] Statusparser, Git-Argumente, Ownership, Attestation, Cache-Kopie, Manifest/Inventory, Pointer-Publish und Workspace-Öffnung
- [x] Dirty-/Unverified-Projektion, Cleanup/Cancellation, Last-good/Degraded/Unavailable, CurrentChanged und statischer Decompilation-Fallback
- [x] scoped DRY-, Structural-, Magic-Value-, Dead-Code- und Safeguard-Audits
- [x] Build-, Fast- und Integration-Gates sowie Testhost-/VSTest-/Temp-Leaks

## Befund

### Plan-Erfüllung

`--ignored=all` ist im Statusaufruf vorhanden; normale `modified`, `untracked`,
`!!`- und unparsebare Statussätze werden im getesteten Pfad typisiert
abgewiesen. `Dirty` wird aus einem nicht verfügbaren Transportresultat über den
Acquirer bis zum Provider erhalten. Cache-Readback, Pointer-Race, Cleanup,
Last-good/Degraded, CurrentChanged-Reuse und die positiven Fallbacks bleiben
sichtbar.

Die zentrale Zusicherung, dass nur ein ownership-validierter, inhaltlich an die
attestierte Revision gebundener Checkout als Source, Cachegeneration, Snapshot
oder `Verified` weitergegeben wird, ist aber nicht erfüllt. Wiederholte
Status-/HEAD-Prüfungen sind keine unveränderliche Source-Repräsentation und
schließen die Zeitfenster zwischen Prüfung und Kopie/Öffnung/Erfolg nicht.
Außerdem verwirft der Parser bestimmte leere Records, statt sie fail-closed als
unbekannt zu behandeln.

### Rules- und Qualitätsnachweis

Die semantischen MCP-Abfragen liefen projektgebunden mit dem absoluten
Projektroot. `get_violations` findet im Produktionsscope ausschließlich den
bekannten `MaxDirectoryChildren`-Befund für `src/AiNetLinter/Mcp/Assemblies`.
`get_test_context` weist für `ExternalSourceRepositorySourcePolicy` keine
direkte Testdatei aus.

Der Exact-DRY-Audit im Assembly-Produktionsscope findet **0/406** Cluster; der
ergänzende Structural-Audit findet die im Result dokumentierten **5** bekannten
Kandidaten. `find_dead_code` meldet **0 High-Confidence**-Kandidaten. Der
`changedOnly=true`-Magic-Value-Lauf ist am sauberen Review-HEAD mangels Git-Diff
leer; ein Vollscan zeigt nur bestehende Kandidaten und keinen neuen direkt
behebbaren Trust-Wert.

| Scope | Score | Ergebnis |
|---|---:|---|
| global | **5,66235294117647/10** | FAIL bei Threshold 8,00; 3 bekannte Baseline-Verstöße |
| `src/AiNetLinter/Mcp/Assemblies` | **5,7727272727272725/10** | FAIL bei Threshold 8,00; dieselben Baseline-Verstöße |

Es wurde kein neues direktes Tech-Debt aufgenommen.

## Findings

### 1. Leere Status-Records werden als clean verworfen statt fail-closed

- **Severity:** MAJOR
- **Ebene:** Git-Status-Attestation / Parser / Source-of-Truth
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs:74-100`; `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryCheckoutStatusTests.cs:17-58`
- **Was:** `AssessStatus` verwendet `statusOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)`. Dadurch wird ein unerwarteter leerer Record entfernt: `"\n"` und `"\n?? .ainetlinter-owner"` werden wie ein leerer bzw. erlaubter Status behandelt und liefern `null`, also clean. Nur `""` mit erfolgreichem Git-Exit ist als echter clean-Output zulässig; zusätzliche Records sind keine explizit erlaubte Git-Statussemantik.
- **Reproduktion:** Den vorhandenen `RecordingGitExecutor` mit Exitcode `0` und Standardausgabe `"\n"`, `"\n?? .ainetlinter-owner"` oder einer mehrzeiligen Ausgabe mit leerem Record speisen. `ExternalSourceRepositoryCheckoutStatus.ExecuteAsync` liefert jeweils `null` statt `Unverified`. Die aktuelle Matrix prüft zwar `??`, `!!`, modified, untracked und malformed, aber weder den erlaubten Ownership-Marker noch leere Records bei Exitcode `0`.
- **Auswirkung:** Eine beschädigte oder unerwartete Prozessausgabe kann ein Clean-Gate passieren. Der nachfolgende Transport-/Attestation-Pfad kann den Checkout damit als verifiziert behandeln, obwohl die gesamte Ausgabe nicht deterministisch parsebar war.
- **Nächste Aktion:** Nur die exakt leere Ausgabe als clean akzeptieren; leere Records innerhalb einer nichtleeren Ausgabe als `Unverified` abweisen. Eine lokale Matrix muss Ownership-Marker, CRLF, mehrere echte Statuszeilen, führende/leere Records und unerwartete Zeichen getrennt abdecken und weiterhin secret-freie Diagnosen prüfen.

### 2. Die Attestation bindet den Inhalt nicht bis Copy/Open/Publish und Erfolg

- **Severity:** MAJOR
- **Ebene:** TOCTOU / Materialisierung / Cache-Publish / Verified-Lifetime
- **Ort:** `src/AiNetLinter/Mcp/Assemblies/ExternalSourceCheckoutAttestation.cs:95-127`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs:154-192`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs:157-199`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs:29-56`; `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs:93-121`
- **Was:** Die Transport-Attestation prüft später erneut nur Git-Status und HEAD. `CopyFile` öffnet jede Datei zwar während des Kopierens mit `FileShare.Read` und hasht die Bytes, die es kopiert, aber es vergleicht sie nicht mit einer commitgebundenen Blob-/Snapshot-Repräsentation und hält keine exklusive Checkout-Lifetime. Im Cache-Writer liegt zwischen der Attestation in Zeile 172 und `TryPublishPointer` in Zeile 174 ein ungeschütztes Fenster; nach der Attestation in Zeile 186 folgt noch die Readback-/Erfolgsstrecke. Der Workspace wird zwischen den Prüfungen geöffnet, und ein nach der letzten Prüfung mutierter Checkout bleibt als `Verified`-Snapshot/Handle verwendbar.
- **Reproduktion:** Eine gleichlange getrackte Datei nach einer erfolgreichen Attestation, aber vor `CopySource`, auf untrusted Bytes wechseln und vor dem zweiten Statusaufruf wiederherstellen. Die Generation enthält dann die untrusted Bytes, während Status und HEAD clean wirken; der Pointer kann veröffentlicht werden. Analog kann eine Mutation während `OpenSolutionAsync` nach dem Öffnen wiederhergestellt werden, bevor der zweite Statuscheck läuft; der Roslyn-Snapshot behält den bereits eingelesenen untrusted Inhalt. Eine Mutation nach dem letzten Check vor Rückgabe lässt den Erfolg ebenfalls ungeschützt.
- **Testlücke:** `CachePublish_MutationBeforePointerPublishFailsClosed` mutiert in `BeforePointerPublishedAsync` vor der Attestation in Zeile 172 und lässt die Mutation bestehen. `Provider_MutationAfterMaterializationFailsClosedWithoutSnapshot` mutiert innerhalb des Materializers und lässt sie bis zur Prüfung in `MaterializeVerifiedAsync` bestehen. Keiner der beiden Tests erzwingt deterministisch `Attestation → Mutation → Copy/Open → erneute Prüfung` in dem kritischen, ungeschützten Zwischenraum; es gibt keinen Seam nach der Attestation direkt vor Pointer-Publish oder nach der letzten Prüfung vor Erfolg.
- **Auswirkung:** Ein sauberer Status-/HEAD-Nachweis kann mit einer fremden Cachegeneration, einem fremden Workspace-Snapshot oder einem nachträglich mutierten als `Verified` weitergegebenen Checkout verbunden werden. Das verletzt den Primärvertrag trotz der vorhandenen Rechecks.
- **Nächste Aktion:** Das Korrekturpaket muss eine unveränderliche, an die erwartete Revision gebundene Materialisierungsrepräsentation oder eine nachweislich exklusive Checkout-Lifetime bis Copy/Open/Pointer/Erfolg einführen. Die Schutzgrenze muss Cachegeneration, Workspace, Snapshot und Registry-Lease umfassen. Ergänze lokale deterministische Race-Tests mit expliziten Barrieren nach jeder Attestation; bei Drift müssen typed Failure, Generation-/Pointer-Rollback, kein Snapshot/Lease und Checkout-Cleanup nachgewiesen werden, während Last-good/Degraded und CurrentChanged-Reuse erhalten bleiben.

### 3. Positive Tests können fehlende Transport-Attestations weiterhin still ergänzen

- **Severity:** MINOR
- **Ebene:** Testvertrag / Beweiskraft der Verified-Pfade
- **Ort:** `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs:78-90`; `src/AiNetLinter/Mcp/Assemblies/ExternalSourceCheckoutAttestation.cs:145-164`; mehrere positive Fakes unter `src/AiNetLinter.FastTests/Mcp/Assemblies/`
- **Was:** `ExternalSourceRecordingTransport` ersetzt bei jedem verfügbaren Ergebnis ohne Attestation automatisch die fehlende Attestation durch `ExternalSourceCheckoutAttestation.ForTesting`. Viele positive Fakes liefern weiterhin nur `ExternalSourceRepositoryTransportResult.Success(Revision)`. Dadurch können die Tests erfolgreich bleiben, obwohl sie den produktiven `FromTransport`-/Status-/HEAD-Vertrag nicht liefern; die Test-Attestation ist standardmäßig clean und prüft keinen echten Git-Status.
- **Auswirkung:** Die Tests beweisen zwar die nachgelagerten Ownership-/Lifecycle-Pfade, schließen aber keinen alten unsicheren Testvertrag aus. Insbesondere kann ein fehlendes Attestation-Feld in einer neuen Transportintegration durch die Testhilfe verborgen werden.
- **Nächste Aktion:** Den Auto-Inject entfernen oder auf einen expliziten, sichtbar benannten Testadapter begrenzen. Positive Transportpfade müssen eine bewusst erzeugte `FromTransport`-Attestation bzw. eine lokale Status-/HEAD-Fake-Sequenz liefern; zusätzlich direkte Policy-/Result-Matrizen für Clean, Dirty und Unverified ergänzen. Das gehört in dasselbe Trust-/Materialisierungspaket, nicht in einen Audit-only-Step.

## Bewahrte Invarianten

Die unabhängige Prüfung bestätigt im aktuellen Code und in den grünen Tests:

- ignorierte Dateien werden über `--ignored=all` grundsätzlich erfasst; `!!`-Einträge und sonstige nicht erlaubte Statuszeilen erzeugen keinen Clean-Trust;
- Dirty bleibt im tatsächlich getesteten Transportfehlerpfad Dirty und wird nicht über Acquirer/Provider zu Unverified degradiert;
- Cache-Publish-, Pointer-Race-, Cancellation-, Cleanup-, Last-good-/Degraded- und CurrentChanged-Pfade erhalten ihre vorgesehenen Zustände;
- positive Cache-/Clone-/Refresh-/Reuse-, NoMatch-, Ambiguous-, Capability- und statische Decompilation-Fallbacks bleiben aktiv;
- Credential-Isolation, secret-freie Diagnosen sowie die bestehenden 1314-Reparse-Skips bleiben erhalten.

## Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (2174 bestanden, 2 Skips, 2176 gesamt)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (370 bestanden, 0 Skips, 370 gesamt)
```

Die zwei Skips sind unverändert die dokumentierten Reparse-Point-Fälle wegen
`Win32 ERROR_PRIVILEGE_NOT_HELD (1314)`:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

`Category=Stress` wurde nicht ausgeführt. Nach den Läufen war `temp` leer; es
blieben keine `testhost`- oder `vstest.console`-Prozesse zurück. Drei normale
`dotnet MSBuild.dll`-Node-Reuse-Prozesse waren resident und sind keine
Testhost-Leaks.

## Nächste gebündelte Aktion

Keinen Audit-only-Mini-Step anlegen. Die drei Befunde gehören in ein größeres
Korrekturpaket für denselben Checkout-Trust-/Materialisierungsvertrag:

1. Statusausgabe ohne stillschweigend verworfene Records fail-closed parsen.
2. Eine echte commitgebundene unveränderliche oder exklusive Materialisierungs-
   und Publish-Grenze für Cache, Workspace, Snapshot und Lease herstellen.
3. Die Tests vom permissiven Attestation-Auto-Inject lösen und deterministische
   Status-/Dirty-/Unverified-/Mutation-after-validation-Matrizen ergänzen.

Danach sind beide vollständigen Nicht-Stress-Gates, die 1314-Skip-Erklärung,
Ownership-/Temp-Leak-Prüfung sowie die scoped MCP-Qualitätsnachweise erneut
auszuführen. Der Step bleibt bis dahin `issues`.
