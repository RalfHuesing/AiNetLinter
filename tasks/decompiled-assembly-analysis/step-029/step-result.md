---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 029
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_at: 2026-08-29
code_commit_hash: 82692da054136dd39f6a37d110926bb95b5d796c
status_after: done (pending audit)
blocker_category: n/a
---

# Step 029 – Cache-backed Initial Acquisition/Reuse

## Ergebnis

Der Cache wird in `ExternalSourceRepositoryAcquirer.AcquireAsync` vor dem
unveränderten Clone-/Write-through-Pfad versucht. Ein Cache-Hit liest Current,
Manifest und unabhängiges Inventory ausschließlich über die bestehende strikte,
bounded Reader-Fassade. Danach reserviert der Reuse-Pfad einen neuen
request-eigenen Checkout mit dem bestehenden Ownership-/Cleanup-Vertrag und
materialisiert nur den validierten `content`-Baum.

Die persistente Generation bleibt cache-eigen: `GenerationPath` wird niemals an
`ExternalSourceCheckoutHandle` übergeben. Manifest, Inventory, Current-Pointer
und der persistente Ownership-Marker werden nicht in den Request-Checkout
kopiert; der neue Checkout erhält seinen eigenen Marker und seine eigene Lease.
Die Read-back-Validierung prüft weiterhin Cache-Key, kanonische URL,
SolutionPath, geladene Revision, Generation, Inventar, Dateipfade, Größen,
Hashes und Reparse-Sicherheit.

Miss, fehlender/ungültiger Current, ungültige Generation und Materialisierungs-
fehler werden bounded behandelt. Nach erfolgreicher idempotenter Bereinigung
fällt der Acquirer in den bisherigen Clone-/Write-through-Pfad zurück. Kann ein
partieller eigener Checkout nicht sicher bereinigt werden, endet der Vorgang
fail-closed mit der bestehenden typed Cleanup-Failure-Semantik; der persistente
Current bleibt unangetastet. Cancellation wird nicht als Cache-Miss
interpretiert, sondern unverändert weitergereicht.

Die Nachweiskorrektur aus Step 030 verwendet für die validen Reuse-Hits einen
separaten lokalen Publisher und Reader auf demselben isolierten Cache-Root
sowie in den Acquirer-Tests einen `RecordingCacheWriter`. Vor dem Hit wird
`Current.Manifest.GenerationName` konkret gesnapshotet; nach Hit, Dispose und
im Parallelfall wird derselbe Wert wieder gelesen. `cacheWriter.Request` bleibt
`null`, `transport.CallCount` bleibt `0`, und der request-owned Checkout bleibt
vom persistenten `GenerationPath` getrennt und wird unabhängig bereinigt.

Es wurden weder Fetch, Refresh, Policy/Intervall, Cache-Konfiguration,
Retention/GC, Telemetrie, Host-/MCP-Wiring, Provider-/Snapshot-/Registry-
Verträge, Assembly-Cache-Code, Remote-/Git-/Netzwerkzugriffe noch
Assembly.Load/ALC-/Reflection-Ausführung eingeführt. `task-state.md`, Roadmap
und `tech-debt.md` blieben unverändert.

## Geänderte Dateien

- [ExternalSourceRepositoryAcquirer.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs) – Cache-first-Integration; der bisherige Clone-/Write-through-Pfad bleibt erhalten.
- [ExternalSourceRepositoryCacheWriter.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs) – lokale Writer-Implementierung bedient zusätzlich die interne Reader-Fassade.
- [ExternalSourceRepositoryCacheStorage.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs) – der bestehende bounded Copy-/Hash-Primitiv ist für die Materialisierung intern wiederverwendbar.
- [IExternalSourceRepositoryCacheReader.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/IExternalSourceRepositoryCacheReader.cs) – interne Reader-Seam für den strikt validierten Current.
- [ExternalSourceRepositoryCacheReuse.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReuse.cs) – Cache-Key-Erzeugung, Cache-Hit, Lease-Lifetime, Cleanup und typed Fallback.
- [ExternalSourceRepositoryCacheMaterializer.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheMaterializer.cs) – bounded, hash-/größenvalidierte Kopie in den neuen Checkout ohne persistente Metadaten.
- [CheckoutValidationResult.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/CheckoutValidationResult.cs) – aus dem Acquirer ausgelagerter bestehender Validierungsdatensatz zur Einhaltung der 500-Zeilen-Grenze.
- [ExternalSourceRepositoryCacheAcquirerTests.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs) – deterministische Reuse-, Ownership-, Fallback-, Cancellation- und Concurrent-Request-Tests.
- [step-result.md](C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-analysis/step-029/step-result.md) – dieser Nachweis.

## Kriterienabdeckung

### A – Strict Reader und Cache-Identität

Die lokale Writer-Implementierung stellt die bestehende strikte Reader-Fassade
über `IExternalSourceRepositoryCacheReader` bereit. Der Reuse-Pfad erzeugt den
Cache-Key erneut aus URL und SolutionPath und akzeptiert nur das Ergebnis des
Readers. Dadurch bleiben die bestehenden Prüfungen für Current-Pointer,
Manifest, Generation, kanonische URL, SolutionPath, geladene Revision und
Cache-Key unverändert aktiv. Die unabhängige Inventory-Validierung prüft
zusätzlich Datei-Mengen, Pfade, Größen und Hashes gegen Manifest und Content.

Die Cache-Acquirer-Tests prüfen URL-, SolutionPath-, Revision- und
Inventory-Mismatch jeweils als Clone-Fallback. Der bestehende Step-028-
Read-back-Satz deckt zusätzlich beschädigte/fehlende Pointer-, Manifest-,
Inventory- und Content-Dateien, Bounded-Reads, Limits sowie Reparse-Fälle ab.

### B – Materialisierung, Ownership und Lifetime

`ExternalSourceRepositoryCheckoutReservation.TryCreate` erzeugt für jeden Hit
einen neuen kontrollierten Checkout. `ExternalSourceRepositoryCacheMaterializer`
liest ausschließlich aus der bereits validierten persistenten Generation, nutzt
`WalkFiles` und `CopyFile` bounded und verifiziert nach jeder Datei Länge und
SHA-256-Hash gegen das Manifest. Der SolutionPath wird im neuen Checkout
aufgelöst und als reguläre, reparse-freie Datei geprüft.

Der Handle referenziert ausschließlich den neuen Checkout und die geladene
Revision. Die Tests prüfen: Handle-Pfad ist ungleich `GenerationPath`, eigener
Ownership-Marker ist vorhanden, Dispose entfernt nur den Request-Checkout,
die persistente Generation bleibt lesbar, vier parallele Hits erhalten vier
unabhängige Checkout-Pfade und die Test-Fixtures bleiben voneinander isoliert.

### C – Bounded Fallback und Cancellation

Bei Reader-Miss oder ungültigem/fehlendem Current läuft der unveränderte
Clone-/Write-through-Pfad. Bei einer Materialisierungsabweichung wird der
partielle Checkout vor dem Fallback idempotent bereinigt. Ein Cleanup-Fehler
wird nicht verschleiert, sondern führt typed und fail-closed zu
`RepositoryCleanupFailed`; dadurch wird kein unkontrollierter Checkout-Leak
akzeptiert und die persistente Generation nicht verändert.

Der Cancellation-Test bricht nach dem strict Reader und vor der Materialisierung
ab, erwartet exakt den ursprünglichen CancellationToken, keinen Transportaufruf
und keinen Checkout-Rest. Die vorhandenen Acquirer-/Cache-Tests decken zudem
Cancellation während Transport, Publish und Cleanup weiter ab.

Es gibt keine Cross-Process-Garantie. Der bestehende per-Key-Lock des Writers
bleibt prozesslokal; der read-only Reuse erzeugt pro Request eine eigene Lease
und führt keine globale Synchronisations- oder Refresh-Policy ein.

## Verifikation

| Lauf | Ergebnis |
|---|---:|
| `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"` | 34 bestanden, 1 Skip, 35 gesamt, 0 Fehler |
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2.060 bestanden, 2 Skips, 2.062 gesamt, 0 Fehler |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370 bestanden, 0 Skips, 370 gesamt, 0 Fehler |
| Stress-Kategorie | nicht ausgeführt |

Der Fokus-Skip ist
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`;
beim Erzeugen des echten Symlinks fehlte `ERROR_PRIVILEGE_NOT_HELD` / Win32
1314. Der zusätzliche Fast-Gate-Skip ist
`ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`;
auch hier verhinderte `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 die echte
Reparse-Erzeugung. Beide Fälle sind echte, capabilitybedingte Reparse-/Symlink-
Skips; es wurde kein Fake-Reparse verwendet. Nach den Läufen waren keine
`testhost.exe`-/`vstest.console.exe`- oder Test-`dotnet.exe`-Prozesse aktiv.
Das Repository-Temp-Verzeichnis enthielt keine neuen Testverzeichnisse. Ein
bereits vorhandener Default-Cache-Rest unter
`src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source` mit neun Dateien
wurde nur inspiziert und nicht gelöscht.

## MCP-, DRY-, MagicValues- und DeadCode-Befunde

- Alle folgenden C#-Semantik- und Audit-Aufrufe verwendeten das absolute
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` wurde nur für
  Text-/Dateisuche eingesetzt.
- `get_violations(scopeFilter="ExternalSourceRepository")`: 0 Violations in
  24 Dateien.
- `safeguard(scopeFilter="ExternalSourceRepository")`: 5,79/10, FAIL bei
  Threshold 8,00; die drei bestehenden Befunde liegen außerhalb des
  Reuse-Codes (`src/AiNetLinter/Mcp/Assemblies` mit zu vielen Einträgen,
  `DaemonHostCommand`-Footprint und `tasks/decompiled-assembly-analysis` mit
  zu vielen Einträgen). Kein neuer Reuse-Befund.
- `find_duplicates(mode="clone", minTokens=20, similarityThreshold="near",
  scopeDir="src/AiNetLinter/Mcp/Assemblies", scopeType="production")`:
  0 Cluster bei 350 gescannten Methoden.
- Derselbe scoped Aufruf mit
  `scopeDir="src/AiNetLinter.FastTests/Mcp/Assemblies", scopeType="tests"`:
  0 Cluster bei 124 gescannten Methoden. Es wurde kein solutionweiter
  DRY-/Structural-/Refactoring-Drift-Sweep dokumentiert oder durchgeführt.
- `find_magic_values(scopeFilter="src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository",
  includeTests=false)`: 7 bestehende Werte im begrenzten Produktionsscope;
  kein neuer produktiver Reuse-Wert.
- `find_magic_values(scopeFilter="src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs",
  includeTests=true)`: 34 absichtliche Fixture-/Fallwerte in der betroffenen
  Cache-Acquirer-Testdatei.
- `find_dead_code(scopeFilter="ExternalSourceRepository", includeTests=true,
  mode="members")`: 0 unreferenzierte Symbole bei 24 Dokumenten und 55
  Symbolen.
- Die direkten MCP-Prüfungen umfassten Feature-Kontext für Acquirer und
  Local-Writer, Bodies der drei betroffenen Tests, References/Impact für
  `IExternalSourceRepositoryCacheReader.TryReadCurrent` sowie Test-Kontext
  für den Acquirer; die Abfragen blieben auf diesen Scope begrenzt.

## Offene Risiken

- Die Prozessgrenze bleibt bewusst: Kein Cross-Process-Lock und keine
  Retention-/Invalidierungs-/Refresh-Policy sind Bestandteil dieses Steps.
- Echte Reparse-/Symlink-Fälle konnten auf diesem Host wegen Win32 1314 nur
  transparent übersprungen werden.
- Der Fallback setzt erfolgreiche Cleanup voraus. Bei einem Cleanup-Fehler
  wird korrekt fail-closed abgebrochen; die bestehende Generation bleibt
  erhalten, ein manueller Betreiber-/Umgebungsfehler kann jedoch einen
  unbereinigten, nicht weiterverwendeten Request-Pfad hinterlassen.
