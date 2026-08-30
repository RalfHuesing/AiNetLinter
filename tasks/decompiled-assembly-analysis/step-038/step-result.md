---
status: done (pending review)
type: step-result
task: decompiled-assembly-analysis
step: 038
epic: EPIC-04
step_type: single
corrects: step-037 Review 078c3e15
coded_by: frischer Coder-Agent
coded_by_model: gpt-5 (Codex)
coded_at: 2026-08-30
code_commit_hash: 170b446c6038952dbf2790fe030c5ac2051832ff
status_after: done (pending review)
blocker_category: n/a
---

# Result Step 038: Vertrauensgebundene Checkout-Materialisierung

## Zusammenfassung

Step 038 korrigiert die drei Review-Befunde aus Step 037. Eine Git-Trust-
Attestation ist nur noch gültig, wenn die Porcelain-Auswertung strukturell
vollständig und nicht leer ist. Der leere erfolgreiche Git-Output `""` bleibt
der einzige leere Clean-Fall; leere Records, zusätzliche Records, ungültige
Statuscodes sowie mehrzeilige oder nicht sauber gerahmte Ausgaben werden
`Unverified`. Ignored-, fremde Untracked- und Dirty-Einträge bleiben
fail-closed `Dirty`. Diagnosen bleiben typisiert und secret-frei.

Die neue `ExternalSourceCheckoutMaterializationLease` öffnet alle regulären
Checkout-Dateien und den Ownership-Marker mit exklusiver Windows-Schreib-
freigabe (`FileShare.Read`). Der Lease ist an die Ownership-Token-Lifetime
gebunden und wird über Attestation, Cache-Copy, Pointer-/Publish-Finalisierung,
Workspace-Öffnung und Snapshot-Lifetime gehalten. Cleanup und Cancellation
werden bis zur letzten Materialisierungsnutzung verzögert. Nach dem Lease-
Aufbau bleiben die vorhandenen Status-/Manifest-/Hash-Rechecks aktiv, sodass
auch neu auftauchende Einträge oder geänderte Inventories fail-closed bleiben.

Die Test-Fassade ergänzt keine fehlenden Produktionsattestations mehr. Jeder
positive Fake liefert die Attestation explizit; der Missing-Attestation-Test
prüft den typisierten `RepositoryCheckoutUnverified`-Fehler und Cleanup. Dirty,
Unverified, Degraded und Unavailable bleiben über Acquirer, Refresh, Provider
und Selection getrennt. Last-good, CurrentChanged, positive Verified-/Success-
Verträge und die gewöhnlichen statischen Decompilation-Fallbacks bleiben
erhalten. Host-/MCP-Health, Retention/GC, globale Resultate und ein globaler
Reparse-Sweep wurden nicht geöffnet.

## Geänderte Dateien

### Produktionscode (8 Dateien)

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceCheckoutAttestation.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceCheckoutMaterializationLease.cs` (neu)
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterLifecycle.cs` (neu)
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceSnapshotMaterializer.cs`
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs`

### Regressionen und Test-Fassade (9 Dateien)

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTestTransport.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheConfigurationTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCheckoutAttestationTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryCheckoutStatusTests.cs`
- `src/AiNetLinter.IntegrationTests/Mcp/Assemblies/ExternalSourceSnapshotMaterializerTests.cs`

`tasks/decompiled-assembly-analysis/codemap.md` wurde wegen der neuen
Lease-/Lifecycle-Grenze aktualisiert. `tech-debt.md` und `roadmap.md` blieben
unverändert; es entstand kein neuer direkt notwendiger Vertragsschuldposten.

## Verifikation

- Fokussierter Status-Lauf: **16 bestanden, 0 übersprungen, 16 gesamt**.
- Fokussierter Attestation-/Acquirer-/Cache-Lauf: **93 bestanden,
  2 übersprungen, 95 gesamt**.
- Fokussierter Workspace-Materializer-Lauf: **3 bestanden, 0 übersprungen,
  3 gesamt**.
- `dotnet build --no-restore`: **0 Warnungen, 0 Fehler**.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  --no-restore`: **2.182 bestanden, 2 übersprungen, 2.184 gesamt**.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  --no-restore`: **370 bestanden, 0 übersprungen, 1 fehlgeschlagen,
  371 gesamt**.
- Stress-Tests: **nicht ausgeführt**.

Die beiden bekannten FastTest-Skips betreffen
`AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains` und
`PublishAsync_ActualReparseEntryFailsClosed`. Beide werden auf Windows wegen
`Win32 ERROR_PRIVILEGE_NOT_HELD (1314)` beim Erzeugen eines realen Reparse-
Eintrags übersprungen. Es wurde keine globale Reparse-Sperre ergänzt.

Der einzige Integration-Fehler ist der bestehende Live-Korridor
`McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults`: Der globale
Safeguard erreicht **4,163934426229508/10** statt des Korridor-Minimums 5,00.
Der Dogfood-Lauf selbst ist grün; die vier gemeldeten Befunde sind außerhalb
des Step-038-Pakets: `src/AiNetLinter/Mcp/Assemblies` mit 66 statt höchstens
30 Directory-Einträgen, `DaemonHostCommand.cs` mit 3.097 statt höchstens
2.500 Footprint, `AssemblyAnalysisToolRegistrations.cs` mit 2.622 statt
höchstens 2.500 Footprint sowie `tasks/decompiled-assembly-analysis` mit 46
statt höchstens 30 Einträgen. Dieser globale Host-/MCP-/Verzeichnis-Sweep
wurde wegen des Step-038-Out-of-Scope-Vertrags nicht als Nebenfix verändert.

## Trust-, Race- und Cleanup-Nachweis

- **Status:** Die Regressionen decken `""`, lone/leading/inner empty records,
  CRLF-Framing, den einzigen Ownership-Marker, mehrere Records, `!!`, fremdes
  Untracked, Ignored, Modified und malformed/multiline Statusdaten ab. Ein
  Statusrecord wird nicht stillschweigend übersprungen.
- **Cache-Publish:**
  `CachePublish_MaterializationLeaseBlocksMutationUntilPublishCompletes`
  hält eine TCS-Barriere in der ersten Attestation, versucht genau dann eine
  echte Datei-Mutation und beobachtet die durch den Lease erzwungene
  `IOException`. Erst nach Freigabe der Barriere endet Copy, Pointer-Publish,
  Readback und Cleanup; der Publish ist erfolgreich und der Current-Stand
  lesbar.
- **Workspace:**
  `MaterializeAsync_HoldsCheckoutLeaseAcrossOpenAndSnapshotLifetime` erzwingt
  dieselbe Reihenfolge vor `OpenSolutionAsync` und während des lebenden
  Snapshots. Mutationen sind in beiden Phasen blockiert; nach
  `snapshot.Dispose()` und Checkout-Dispose ist der temporäre Checkout
  bereinigt und der Pfad verschwunden.
- **Dirty-/Unverified-Propagation:** Ignored, fremdes Untracked und Dirty
  erzeugen keinen verifizierten Snapshot, keine neue Cachegeneration und
  keinen untrusted Registry-Lease. Ein fehlendes Produktionsattestation-Feld
  wird nicht durch den Fake ergänzt, sondern als `RepositoryCheckoutUnverified`
  abgewiesen. Provider-Mutation bleibt `Unverified` und erzeugt keinen
  Snapshot.
- **Cleanup/Cancellation/Pointer-Races:** Die vorhandenen Cancellation-after-
  Pointer-, Concurrent-Publish-, Last-good-, Degraded-/Unavailable- und
  CurrentChanged-Regressionen liefen im vollständigen Fast-Gate grün. Die
  neue Lease-Freigabe liegt nach der Publish-Finalisierung und nach der letzten
  Snapshot-Nutzung; Fehlerpfade behalten die Ownership-/Reservation-Cleanup-
  Kette.
- Alle neuen Tests sind lokal und `TestTempDirectory`-isoliert. Es wurden
  keine echten Netzwerke, Credentials oder Assembly-Ladungen verwendet.

## MCP- und Qualitätsnachweis

Alle projektbezogenen MCP-Abfragen verwendeten das absolute
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`. `get_feature_context`,
`find_symbol`, `get_symbol_body`, `find_references` und `get_impact` wurden für
die Attestation-, Lease-, Handle-, Cache- und Materializer-Grenzen ausgeführt.

- `get_violations` im Produktionsscope meldet ausschließlich den bestehenden
  `MaxDirectoryChildren`-Befund von `src/AiNetLinter/Mcp/Assemblies`; der
  Testscope meldet **0 Violations**.
- Produktionsgrenzen nach dem Edit: Acquirer **443 Codezeilen / 2.458
  Footprint**, Cache-Writer **438 Codezeilen**, Materializer **110
  Codezeilen**, Checkout-Status **135 Codezeilen**. Der Acquirer bleibt damit
  unter dem 2.500-Footprint-Korridor und wurde nicht weiter aufgebläht.
- `find_duplicates`, Produktionsscope `src/AiNetLinter/Mcp/Assemblies`,
  Clone-Modus, `minTokens=20`, exact: **418 Methoden, 0 Cluster**.
- Ergänzender Structural-Audit im selben Scope, `minTokens=10`: **481
  Methoden, 4 Near-Cluster**. Die Kandidaten sind Result-Konstruktoren,
  Failure-Code-Normalisierung, Session-Statusauflösung und zwei getrennte
  Native-/Git-Handle-Helfer; sie haben unterschiedliche Verantwortungen und
  wurden nicht künstlich zusammengezogen.
- `find_magic_values`, Produktionsscope, `changedOnly=true`,
  `includeSuppressed=false`: **8 Treffer in 8 Einträgen über 6 Dateien**.
  Das sind bestehende user-facing Exception-/Diagnose-Strings, keine neuen
  Secrets, URLs oder ungeschützten Trustwerte.
- `find_dead_code`, Produktionsscope Assemblies,
  `private_internal`, `confidence=high`, `mode=members`: **66 Dokumente,
  170 Symbole, 0 unreferenzierte Symbole**.

Der Safeguard wurde mit `minScore=8` ehrlich ausgeführt:

| Scope | Score | Ergebnis |
|---|---:|---|
| global | **4,163934426229508/10** | FAIL, Threshold 8,00, 4 bekannte Befunde, 854 Klassen |
| `src/AiNetLinter/Mcp/Assemblies` | **4,283950617283951/10** | FAIL, Threshold 8,00, 4 bekannte Befunde |

Die neuen Trust-/Materialisierungsdateien haben keine direkten Violations.
Die Safeguard-Grenze wird bewusst nicht durch Änderungen an Host-/MCP-Health,
globalem Directory-Layout oder bestehendem Footprint schöngerechnet.

## Commit und Kritiker-Übergabe

Die Implementierung wurde mit folgendem deutschen Conventional Commit gesichert:

- `170b446c6038952dbf2790fe030c5ac2051832ff` — `fix: Binde Checkout-Lease bis zur Materialisierung [decompiled-assembly-analysis]`
- Branch: `main`
- Push: nicht ausgeführt

Die abschließende Dokumentation wird separat als deutscher `docs:`-Commit mit
demselben Suffix gesichert. Danach wird ein neuer, unabhängiger Kritiker auf
dem aktuellen Repository-Stand gestartet. Er soll insbesondere Parser-
Vollständigkeit, Lock-/Ownership-Lifetime über Copy/Open/Publish, die echte
Race-Reihenfolge, Missing-Attestation-Fail-Closed, Dirty-vs-Unverified sowie
Cleanup/Cancellation und die statischen Fallbacks prüfen.
