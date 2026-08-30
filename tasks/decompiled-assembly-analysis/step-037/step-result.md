---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 037
epic: EPIC-04
step_type: single
corrects: step-036 Review c7efaae4
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_at: 2026-08-30T06:30:00+02:00
code_commit_hash: 093f9d7a8060dd1ac3898845a472ff93b68a2b37
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 037: Gebundene Checkout-Attestation für Clean-Source und Materialisierung

## Zusammenfassung

Step 037 schließt den Trust-Vertrag aus dem Step-036-Review: Ein Source-
Checkout wird nur nach Ownership-Prüfung, sicherer erwarteter Revision und
vollständiger Status-/Inhalts-Attestation als `Clean`/`Verified` weitergegeben.
`git status --porcelain=v1 --untracked-files=all --ignored=all` wird
fail-closed ausgewertet. Nur der eigene Ownership-Marker ist ein erlaubtes
untracked Artefakt; ignored, andere untracked, Änderungen, malformed oder
nicht auswertbare Statusdaten sind kein Clean-Zustand.

Die Attestation bindet den konkreten Checkout-Pfad und die erwartete Revision
an erneute Status-/HEAD-Prüfungen. Bei Cache-Publish wird sie vor der Kopie,
nach dem Vor-Publish-Hook und nach Pointer-/After-Publish-Hook einschließlich
Readback geprüft. Bei Workspace-Materialisierung erfolgt die Prüfung vor und
nach `OpenSolutionAsync`; die Cache-Reuse-Strecke validiert zusätzlich
Manifest-Dateimenge, Länge und SHA-256. Eine Mutation im fokussierten Race-Seam
erzeugt einen typisierten `UnsafeSource`-/`Unverified`-Fehler, keinen Snapshot,
keine neue Generation und keinen neuen Registry-Lease.

`Dirty` bleibt über Transport, Acquirer, Refresh, Provider und Selection
typisiert erhalten. `Unverified` wird nicht als Ersatz für Dirty verwendet.
Stale-Refresh, `CurrentChanged`, Last-good/Degraded/Unavailable, Cleanup,
Cancellation und der statische Decompilation-Fallback bleiben erhalten.
Unzusammenhängende Host-/MCP-Health-, Retention-, GC-, Invalidierungs- und
globalen Resultatänderungen wurden nicht geöffnet.

## Geänderte Dateien

### Produktionscode (17 Dateien einschließlich einer neuen Attestation-Datei)

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceCheckoutAttestation.cs` (neu)
- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceProviderResult.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReuse.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryResultState.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs`
- `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs`
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`
- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`

### Regressionen und Test-Seams (7 Dateien)

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCheckoutAttestationTests.cs` (neu)
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheTestSupport.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryCheckoutStatusTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs`

Die Architekturgrenze wurde in `tasks/decompiled-assembly-analysis/codemap.md`
aktualisiert. `tech-debt.md` und `roadmap.md` blieben unverändert; es wurde
kein neuer direkt notwendiger Schuldposten erzeugt.

## Verifikation

- Fokussierter Attestation-/Status-/Transport-Lauf: **36 bestanden,
  0 übersprungen, 36 gesamt**.
- `dotnet build --no-restore`: **0 Warnungen, 0 Fehler**.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  --logger "console;verbosity=minimal"`: **2.174 bestanden,
  2 übersprungen, 2.176 gesamt**.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  --logger "console;verbosity=minimal"`: **370 bestanden,
  0 übersprungen, 370 gesamt**.
- Stress-Tests: **nicht ausgeführt**.

Die beiden bekannten FastTest-Skips sind unverändert:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide werden auf Windows wegen `Win32 ERROR_PRIVILEGE_NOT_HELD (1314)` beim
Erzeugen des realen Reparse-Falls übersprungen. Es wurde keine globale
Reparse-Sperre ergänzt. Die neuen Regressionen verwenden lokale Fakes,
`TestTempDirectory` und fokussierte Callback-Seams; sie verwenden kein echtes
Netzwerk, keine Credentials und laden keine Assemblies.

## Trust-, Race- und Cleanup-Nachweis

- **Status-/Ignore-Attestation:** Die Statusargumente erfassen tracked,
  untracked und ignored Dateien. Der Parser erlaubt exakt den
  `.ainetlinter-owner`-Marker und klassifiziert alle übrigen Statuszeilen als
  `Dirty`; ungültige Statuscodes oder unparsebare Zeilen werden `Unverified`.
- **Cache-Race:** Der Test
  `CachePublish_MutationBeforePointerPublishFailsClosed` mutiert den
  Checkout im `BeforePointerPublishedAsync`-Seam. Die zweite Attestation
  liefert `Unverified`; der Publish endet typisiert als `UnsafeSource`, der
  neue `current`-Pointer und die Generation werden nicht veröffentlicht bzw.
  zurückgerollt.
- **Workspace-Race:**
  `Provider_MutationAfterMaterializationFailsClosedWithoutSnapshot` mutiert
  nach dem simulierten Materialisieren. Der Provider liefert kein Snapshot-
  Ergebnis und keinen erfolgreichen Lease; die fehlerhafte Ownership wird
  bereinigt.
- **Dirty-Propagation:**
  `DirtyTransportTrustIsPreservedThroughAcquirerAndProvider` prüft den
  Transport-zu-Provider-Pfad. `Dirty` bleibt `Dirty`; kein SourceSnapshot,
  keine Cachegeneration und kein Registry-Lease wird aus dem untrusted Stand
  erzeugt.
- **Cleanup/Cancellation/Pointer-Race:** Die bestehenden Cache-Writer-
  Regressionen für Cancellation nach Pointer-Publish und concurrent Publish
  laufen im vollständigen Fast-Gate grün. Fehlerpfade führen über die
  bestehende Ownership-/Reservation-Cleanup-Kette; ein vorheriger Current-
  Pointer bleibt bei fehlgeschlagenem Publish erhalten.
- **Leak-Prüfung:** Nach den vollständigen Gates liefen keine `testhost`- oder
  `vstest`-Prozesse. Die drei verbliebenen `dotnet`-Prozesse waren explizit
  als wiederverwendete MSBuild-Node-Reuse-Prozesse (`MSBuild.dll
  /nodeReuse:true`) identifiziert, nicht als Testhosts.

## MCP- und Qualitätsnachweis

Alle projektbezogenen MCP-Abfragen wurden mit absolutem
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Für
`ExternalSourceCheckoutAttestation` lief der Feature-Kontext nach dem letzten
Edit mit 0 direkten Violations: **207 Code-Lines / 500**, **513
AI-Context-Footprint / 2.500**, **23 Referenzen** und **3 statisch zugeordnete
Regressionstests**. `find_symbol`, `get_symbol_body`, `find_references` und
`get_impact` bestätigten die Attestation-Grenze und ihre Consumer.

### Scoped Audits

- `find_duplicates`, Produktionsscope
  `src/AiNetLinter/Mcp/Assemblies`, `mode=clone`, `minTokens=20`,
  `similarityThreshold=exact`: **406 Methoden gescannt, 0 Cluster**.
- Ergänzender Structural-Audit im selben Scope mit `minTokens=10`:
  **468 Methoden, 5 Kandidatencluster**. Das sind Prüfempfehlungen, keine
  DuplicateCode-Verstöße. Die neuen ähnlichen Stellen sind der
  Provider-/Transport-Resultatvertrag sowie der Provider-Wrapper um den
  Materializer; sie haben unterschiedliche Verantwortungen und bleiben
  bewusst getrennt. Die übrigen drei Cluster sind bestehende Failure-,
  Session- und Native-Helper.
- `find_magic_values`, Produktionsscope, `changedOnly=true`,
  `includeSuppressed=false`: **8 Vorkommen in 7 eindeutigen Einträgen über
  16 Dateien**. Sichtbar bleiben nur bestehende Exception-/Git-Argumente
  (`--single-branch`, `--no-tags`, `--hard`, `rev-parse`, `--verify`); kein
  neues Secret, keine URL und kein neuer Diagnosewert wurde ungeschützt
  eingeführt.
- `find_dead_code`, Produktionsscope Assemblies,
  `private_internal`, `confidence=high`, `mode=members`: **64 Dokumente,
  166 Symbole, 0 unreferenzierte Symbole**.
- `get_violations` im Produktionsscope meldete ausschließlich den bestehenden
  `MaxDirectoryChildren`-Befund für `src/AiNetLinter/Mcp/Assemblies`.

### Safeguard

Der Safeguard wurde ehrlich mit `minScore=8` ausgeführt:

| Scope | Score | Ergebnis |
|---|---:|---|
| global | **5,66235294117647/10** | FAIL, Threshold 8,00, 3 Verstöße, 850 Klassen |
| `src/AiNetLinter/Mcp/Assemblies` | **5,7727272727272725/10** | FAIL, Threshold 8,00, 3 Verstöße, 77 Klassen |

Die drei globalen/scoped sichtbaren Baseline-Funde sind die übergroße
Assemblies-Verzeichnisstruktur, der bestehende `DaemonHostCommand`-
AIContext-Footprint (2.975 > 2.500) und das bestehende Task-Verzeichnis.
Der direkte Produktionsscope enthält keine neue Trust-/Materialisierungs-
Violation. Der zuvor um 14 Einheiten überschrittene Footprint von
`AssemblyAnalysisToolRegistrations` wurde durch eine fokussierte State-
Projektion zurück auf den Baseline-Wert **2.500 / 2.500** gebracht. Der
Integration-Live-Safeguard bestand im vollständigen Gate wieder.

## Commit und Kritiker-Übergabe

Implementierung und Tests wurden mit folgendem deutschen Conventional Commit
gesichert:

- `093f9d7a8060dd1ac3898845a472ff93b68a2b37` — `feat: Binde Checkout-Attestation an Publish und Materialisierung [decompiled-assembly-analysis]`
- Branch: `main`
- Push: nicht ausgeführt

Der nächste Schritt ist ein **frischer separater Kritiker** auf diesem Commit.
Er soll insbesondere die Status-/Ignore-Abdeckung, die Attestation-Bindung
über Cache-Publish und Workspace-Öffnung, Dirty-vs-Unverified-Propagation,
Cleanup bei Cancel/Pointer-Race sowie die unveränderte statische
Decompilation-Fallback-Grenze prüfen. Stress-Tests bleiben ausgeschlossen.
