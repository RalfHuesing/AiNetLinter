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
code_commit_hash: siehe Abschluss-Commit
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
| Fokussierter Reuse-/Acquirer-/Cache-/Cancellation-Filter | 89 bestanden, 2 Skips, 91 gesamt |
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2.056 bestanden, 2 Skips, 2.058 gesamt |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370 bestanden, 0 Skips, 370 gesamt |
| Stress-Kategorie | nicht ausgeführt |

Die beiden FastTests-Skips sind die bekannten echten Windows-
Reparse-/Symlink-Prüfungen mit Win32 `ERROR_PRIVILEGE_NOT_HELD` (1314). Es
wurden keine neuen Testhosts, Prozesse, Temp-Verzeichnisse oder Cache-Leases
zurückgelassen. Nach den Läufen waren keine `testhost.exe`-/
`vstest.console.exe`- oder Test-`dotnet.exe`-Prozesse aktiv; das Repository-
Temp-Verzeichnis enthielt keine Testverzeichnisse. Ein bereits vorhandener
Default-Cache-Rest unter `src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source`
mit neun Dateien wurde nur inspiziert und nicht gelöscht.

## MCP-, DRY-, MagicValues- und DeadCode-Befunde

- Semantische MCP-Abfragen wurden mit absolutem
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt: Feature-
  Kontext, Symbol-Bodies, References, Impact, Test-Kontext, Violations und
  Safeguard für Acquirer, Cache-Reader/-Writer/-Storage, Reservation,
  PathGuard und die betroffenen Tests.
- Der scoped `get_violations`-Lauf meldete nach der Auslagerung 0 Violations.
  Der Acquirer liegt bei 490 Zeilen; der StaticTestSentinel für den neuen
  Reuse-Typ ist durch den direkten Reuse-Test ebenfalls erfüllt.
- Der vorgeschriebene solutionweite Drift-Audit lief mit
  `find_duplicates(mode=clone, scopeDir=src, minTokens=20)`. In den scoped
  Cache-Produktions- und Testbereichen gab es keine exact/near-Cluster. Ein
  fuzzy Produktions-Treffer betrifft den bewusst getrennten Assembly-Cache-
  `WritePointer`; ein fuzzy Test-Treffer betrifft zwei unterschiedliche
  Fallback-Szenarien. Kein Reuse-bezogenes DRY-Refactoring ist erforderlich.
  Der zusätzliche structural-Scan lieferte nur Prüfungsempfehlungen, keine
  Violations; ähnliche Reader-Validierer und der Assembly-Cache bleiben wegen
  der Out-of-Scope-Grenzen getrennt.
- `find_magic_values` für die vier neuen Produktionsdateien meldete 0 Treffer.
  Die einzeln geprüften bestehenden Acquirer-/Writer-Dateien meldeten je einen
  bereits vorhandenen Localization-Kandidaten; es entstand kein neuer
  Produktions-Magic-Value. Der breitere ExternalSourceRepository-Testscope
  enthält die erwarteten Fixture-Präfixe, URLs und Security-Testwerte.
- `find_dead_code` mit `scopeFilter=ExternalSourceRepository` und Tests meldete
  0 unreferenzierte Symbole im Scope.
- `safeguard` meldete Score 5,65/10 und drei bestehende Befunde außerhalb des
  geänderten Codes: zu viele Einträge in `src/AiNetLinter/Mcp/Assemblies`, das
  bestehende `DaemonHostCommand`-Footprint und zu viele Einträge im
  Task-Verzeichnis. Der neue Reuse-StaticTestSentinel ist kein Befund mehr.

## Offene Risiken

- Die Prozessgrenze bleibt bewusst: Kein Cross-Process-Lock und keine
  Retention-/Invalidierungs-/Refresh-Policy sind Bestandteil dieses Steps.
- Echte Reparse-/Symlink-Fälle konnten auf diesem Host wegen Win32 1314 nur
  transparent übersprungen werden.
- Der Fallback setzt erfolgreiche Cleanup voraus. Bei einem Cleanup-Fehler
  wird korrekt fail-closed abgebrochen; die bestehende Generation bleibt
  erhalten, ein manueller Betreiber-/Umgebungsfehler kann jedoch einen
  unbereinigten, nicht weiterverwendeten Request-Pfad hinterlassen.
