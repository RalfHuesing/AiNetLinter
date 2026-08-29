---
status: issues (review)
type: step-result
task: decompiled-assembly-analysis
step: 029
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_at: 2026-08-29
code_commit_hash: 82692da054136dd39f6a37d110926bb95b5d796c
status_after: issues
blocker_category: n/a
---

# Step 029 – Cache-backed Initial Acquisition/Reuse

## Ergebnis

Step 029 führte die cache-backed Initial Acquisition hinter dem bestehenden
Acquirer ein. Ein Cache-Hit liest Current, Manifest und unabhängiges Inventory
über den bounded Reader, validiert die Generation und erzeugt anschließend
einen neuen request-owned Checkout. Die persistente Generation, ihr Current,
Manifest, Inventory und der persistente Ownership-Marker bleiben außerhalb
der Request-Lease.

Bei Cache-Miss, fehlendem oder ungültigem Current, ungültigen Artefakten und
kontrollierten Materialisierungsfehlern wird der eigene Request-Pfad bereinigt
und der bestehende Clone-/Write-through-Pfad verwendet. Cancellation wird
nicht als Cache-Miss interpretiert. Cleanup-Fehler bleiben typed und
fail-closed. Refresh, Fetch, Policy, Config, Retention/GC, Host-/MCP-Wiring,
Provider-/Snapshot-/Registry-Ausbau und Assembly-Ausführung waren nicht Teil
dieses Steps.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs` – Cache-first-Aufruf vor dem bestehenden Clone-Pfad.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs` – lokale Writer-/Reader-Anbindung.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheStorage.cs` – Wiederverwendung des bounded Copy-/Hash-Primitivs.
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceRepositoryCacheReader.cs` – interner Reader-Port.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReuse.cs` – Cache-Hit, Request-Lease, Cleanup und Fallback.
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheMaterializer.cs` – validierte Materialisierung in den neuen Checkout.
- `src/AiNetLinter/Mcp/Assemblies/CheckoutValidationResult.cs` – Validierungsdatensatz.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs` – Reuse-, Ownership-, Fallback- und Cancellation-Regressionen.
- `tasks/decompiled-assembly-analysis/step-029/step-result.md` – dieser Nachweis.

Nicht geändert wurden `task-state.md`, `roadmap.md`, `tech-debt.md` und
die späteren Refresh-/Fetch-/Policy-/Config-/Retention-/GC-/Health-/Host-/
MCP-/Provider-/Snapshot-/Registry-/Transport-/Native-/EPIC-05-Bereiche.

## Kriterienabdeckung

- **Strict Reader und Cache-Identität:** Der Acquirer erzeugt den Cache-Key
  erneut aus kanonischer URL und SolutionPath und akzeptiert nur die strikt
  validierte Reader-Antwort. Current, Generation, Manifest, Inventory,
  Revision, Dateimenge, Größen und Hashes werden fail-closed geprüft.
- **Request-Ownership:** `ExternalSourceRepositoryCheckoutReservation` und
  der Materializer erzeugen eine getrennte Lease. `GenerationPath` wird nicht
  als Handle-Eigentum verwendet; Dispose betrifft nur den neuen Checkout.
- **Fallback/Cancellation:** Miss, fehlende Artefakte und kontrollierte
  Materialisierungsfehler fallen nach sicherem Cleanup in den bestehenden
  Clone-/Write-through-Pfad zurück. Cancellation bleibt als Cancellation
  sichtbar und erzeugt keinen Clone-Aufruf.
- **Bewusste Nachweislücke:** Die damaligen validen Hit-Tests prüften den
  fachlichen Hit und die Ownership, beobachteten aber noch nicht direkt einen
  initialen Publish über einen separaten Reader/Recording-Writer und
  verglichen den Current-Generation-Namen nicht vor und nach dem Hit. Diese
  Lücke wurde in Step 030 als Review-Finding festgehalten.

## Verifikation am Step-029-Commit

| Lauf | Tatsächliches Ergebnis |
|---|---:|
| `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests\|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests\|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"` | 34 bestanden, 1 Skip, 35 gesamt, 0 Fehler |
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2060 bestanden, 2 Skips, 2062 gesamt, 0 Fehler |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370 bestanden, 0 Skips, 370 gesamt, 0 Fehler |
| Stress-Kategorie | nicht ausgeführt |

Die beiden echten Reparse-/Symlink-Tests wurden ausschließlich wegen
`ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 beim Erzeugen des echten Links
übersprungen:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Es wurde kein Fake-Reparse und keine abgeschwächte Assertion verwendet.

## Scoped MCP-/Audit-Nachweise

Alle folgenden Abfragen verwendeten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`. Es wurde kein
solutionweiter DRY-, Magic-Value- oder Dead-Code-Sweep dokumentiert.

- `get_violations(scopeFilter="ExternalSourceRepository")`: 0 Violations in
  24 Dateien.
- `safeguard(scopeFilter="ExternalSourceRepository")`: 5,79/10 bei
  Threshold 8,00, FAIL; drei bestehende Directory-/Footprint-Befunde
  außerhalb des neuen Reuse-Codes.
- `find_duplicates(mode="clone", minTokens=20,
  similarityThreshold="near", scopeDir="src/AiNetLinter/Mcp/Assemblies",
  scopeType="production")`: 0 Cluster bei 350 Methoden.
- Derselbe scoped Clone-Aufruf mit
  `scopeDir="src/AiNetLinter.FastTests/Mcp/Assemblies", scopeType="tests"`:
  0 Cluster bei 122 Methoden.
- `find_magic_values(scopeFilter="src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository",
  includeTests=false)`: 7 bestehende Werte in 7 Einträgen über 16 Dateien.
- `find_magic_values(scopeFilter="src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs",
  includeTests=true)`: 34 absichtliche Fixture-/Fallwerte.
- `find_dead_code(scopeFilter="ExternalSourceRepository", includeTests=true,
  mode="members")`: 0 unreferenzierte Symbole bei 24 Dokumenten und 55
  Symbolen.

Die semantischen Abfragen umfassten Feature-/Symbol-/Body-/References-/Impact-
und Test-Kontext für Acquirer, Cache-Reuse, Reader, Writer, Storage,
Materializer und Reservation; sie blieben auf diesen Cache-/Acquirer-Scope
begrenzt.

## Leak- und Review-Status

Nach den Läufen wurden keine aktiven Testprozesse, keine neuen Repository-
Temp-Verzeichnisse und keine Request-Checkout-Reste festgestellt. Ein
vorhandener Default-Cache-Rest mit neun Dateien wurde nur inspiziert und
nicht gelöscht. Die verbleibenden Nachweislücken sind im Review
`c0abdcdf` dokumentiert; sie wurden in Step 030 korrigiert, ohne den
Produktionsvertrag zu ändern.
