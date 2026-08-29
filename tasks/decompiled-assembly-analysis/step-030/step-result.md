---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 030
corrects: step-029
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_at: 2026-08-29
step_029_code_commit_hash: 82692da054136dd39f6a37d110926bb95b5d796c
status_after: done (pending audit)
blocker_category: n/a
---

# Step 030 – Cache-Reuse-Nachweise und Step-029-Result korrigieren

## Ergebnis

Der Step korrigiert ausschließlich den Nachweis des bereits implementierten
Step-029-Reuse-Vertrags und verstärkt die drei direkten validen Reuse-Tests.
Der Produktionscode blieb unverändert; eine interne test-only Seam war nicht
nötig, weil der Acquirer bereits getrennte `cacheWriter`-/`cacheReader`-
Parameter besitzt.

Die validen Hit-Tests bauen die persistente Generation zunächst mit einem
`cachePublisher` über erfolgreichem `PublishAsync(source.Request)` auf und
verwenden danach eine zweite `LocalExternalSourceRepositoryCacheWriter`-
Instanz als lokalen Reader auf demselben isolierten Cache-Root. Die beiden
Acquirer-Tests verwenden zusätzlich den vorhandenen `RecordingCacheWriter`.
Damit ist ein unerwarteter Publish-Aufruf über `Request` beobachtbar; nach
dem Hit bleibt `Request == null`, während `transport.CallCount == 0` bleibt.

Vor jedem Hit wird der konkrete Wert
`Current.Manifest.GenerationName` über den lokalen Reader als
`currentGenerationBefore` gespeichert. Nach dem Hit, nach dem Dispose des
request-owned Checkouts und im Parallelfall wird exakt derselbe String erneut
gelesen und verglichen. Jeder Hit bleibt vom persistenten
`published.GenerationPath` getrennt, trägt den eigenen Ownership-Marker und
liefert den erwarteten `SolutionPath`. Nach Dispose ist nur der neue Checkout
entfernt; die persistente Generation bleibt vorhanden und lesbar.

## Geänderte Dateien

- [ExternalSourceRepositoryCacheAcquirerTests.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs) – getrennte Publisher-/Reader-Fixtures, Recording-Writer sowie Current-/Ownership-/Cleanup-Assertions für Single-, Direkt- und Parallel-Hits.
- [step-029/step-result.md](C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-analysis/step-029/step-result.md) – geprüfter Step-029-Code-Hash, reale Testzahlen, konkrete 1314-Skips und ausschließlich scoped Audits.
- [step-030/step-result.md](C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-analysis/step-030/step-result.md) – Kriterien-, Verifikations-, Audit- und Risikonachweis dieses Korrektur-Steps.

Nicht geändert wurden Produktionscode, `task-state.md`, `roadmap.md`,
`tech-debt.md`, `ExternalSourceRepositoryCacheWriterTests.cs` sowie Refresh-,
Fetch-, Policy-, Config-, Retention/GC-, Health-, Host-/MCP-, Provider-,
Snapshot-, Registry-, Transport-, Native- und EPIC-05-Code.

## Kriterienabdeckung

1. **Result-Identität — erfüllt:** `step-029/step-result.md` enthält den
   vollständigen Code-Commit
   `82692da054136dd39f6a37d110926bb95b5d796c`; kein Platzhalter bleibt.
2. **Reproduzierbare Testzahlen — erfüllt:** Der exakte Fokusfilter, Build,
   beide vollständigen Nicht-Stress-Gates, die realen Zählungen und beide
   konkreten Win32-1314-Skip-Namen sind dokumentiert; Stress wurde nicht
   ausgeführt.
3. **Scoped Audits — erfüllt:** Dokumentiert sind nur MCP-/DRY-/MagicValues-
   /DeadCode-/Safeguard-Aufrufe mit absolutem `projectRoot` und dem
   `ExternalSourceRepository`-Produktions-/Testscope. Kein solutionweiter
   Audit oder globaler Sweep wurde ausgeführt oder behauptet.
4. **Publish-Vertrag — erfüllt:** Jeder Acquirer-Hit folgt auf einen
   erfolgreichen Publish des isolierten Publishers, liest über den separaten
   Reader und verwendet den `RecordingCacheWriter`; `Request == null` und
   `transport.CallCount == 0` werden nach dem Hit geprüft.
5. **Current-Unveränderlichkeit — erfüllt:** Der konkrete
   `Manifest.GenerationName`-String wird vor dem Hit sowie nach Single-Hit,
   Dispose und parallelen Hits exakt verglichen.
6. **Request-Ownership — erfüllt:** Hit-Checkouts sind von
   `published.GenerationPath` verschieden, besitzen Marker und exakten
   Solution-Pfad, werden beim Dispose entfernt, während die persistente
   Generation und ihr Current lesbar bleiben.
7. **Fallback-Regression — erfüllt:** Invaliditäts-, Missing-Current-,
   Missing-Artifact-, Materialisierungs- und Cancellation-Tests blieben im
   Fokuslauf enthalten und unverändert; es gab keine Produktionsänderung.
8. **Scope-/Arbeitsbaum-Disziplin — erfüllt:** Keine neue Runtime-Seam,
   keine neue produktive Runtime-Cachegeneration, kein Netzwerk-/Git-Zugriff,
   kein Stresslauf, keine Änderung außerhalb der drei Result-/Testdateien.

## Verifikation

| Lauf | Ergebnis |
|---|---:|
| `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"` | 34 bestanden, 1 Skip, 35 gesamt, 0 Fehler |
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2060 bestanden, 2 Skips, 2062 gesamt, 0 Fehler |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370 bestanden, 0 Skips, 370 gesamt, 0 Fehler |
| Stress-Kategorie | nicht ausgeführt |

Der Fokus-Skip ist
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`;
beim Erzeugen des echten Symlinks fehlte `ERROR_PRIVILEGE_NOT_HELD` / Win32
1314. Der zweite Skip im vollständigen Fast-Gate ist
`ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`;
auch dieser echte Reparse-Fall scheiterte capabilitybedingt mit
`ERROR_PRIVILEGE_NOT_HELD` / Win32 1314. Es wurde kein Fake-Reparse und keine
abgeschwächte Assertion verwendet.

## MCP-, DRY-, MagicValues-, DeadCode- und Safeguard-Nachweise

Alle folgenden Aufrufe erhielten exakt
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` wurde nur für
Text-/Dateisuche eingesetzt:

- `get_violations(scopeFilter="ExternalSourceRepository")`: 0 Violations in
  24 Dateien.
- `safeguard(scopeFilter="ExternalSourceRepository")`: 5,79/10 bei Threshold
  8,00, FAIL. Die drei bestehenden Befunde sind der überfüllte
  `src/AiNetLinter/Mcp/Assemblies`-Ordner, der bestehende
  `DaemonHostCommand`-Footprint und der überfüllte
  `tasks/decompiled-assembly-analysis`-Ordner; keiner betrifft die neue
  Reuse-Beobachtung.
- `find_duplicates(mode="clone", minTokens=20,
  similarityThreshold="near", scopeDir="src/AiNetLinter/Mcp/Assemblies",
  scopeType="production")`: 0 Cluster bei 350 Methoden.
- Derselbe `find_duplicates`-Aufruf mit
  `scopeDir="src/AiNetLinter.FastTests/Mcp/Assemblies", scopeType="tests"`:
  0 Cluster bei 124 Methoden. Ein solutionweiter DRY-/Structural- oder
  Refactoring-Drift-Sweep wurde nicht ausgeführt.
- `find_magic_values(scopeFilter="src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository",
  includeTests=false)`: 7 bestehende Werte im begrenzten Produktionsscope;
  kein neuer produktiver Reuse-Wert.
- `find_magic_values(scopeFilter="src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs",
  includeTests=true)`: 34 absichtliche Fixture-/Fallwerte in der betroffenen
  Testdatei.
- `find_dead_code(scopeFilter="ExternalSourceRepository", includeTests=true,
  mode="members")`: 0 unreferenzierte Symbole bei 24 Dokumenten und 55
  Symbolen.

Die direkten MCP-Semantikabfragen umfassten den Feature-Kontext des Acquirers
und des Local-Writers, Bodies der drei betroffenen Testmethoden sowie
References/Impact für `IExternalSourceRepositoryCacheReader.TryReadCurrent`.
Die Abfragen blieben auf den Cache-/Acquirer-Scope begrenzt.

## Ownership-/Transport-/Publish-Nachweis

- **Single Acquirer-Hit:** separater Local-Reader liefert den gesnapshotteten
  Current-Namen; `result.IsAvailable`, `LoadedRevision == Revision`,
  `transport.CallCount == 0` und `cacheWriter.Request == null` sind erfüllt.
  Der Checkout ist nicht `published.GenerationPath`, besitzt Marker und
  Solution-Pfad; nach Dispose ist er weg und Generation/Current behalten den
  identischen Namen.
- **Direkter Cache-Reuse:** separater Reader, erfolgreicher Publisher,
  request-owned Checkout, Marker, Revision und Solution-Pfad sind geprüft;
  der Current-Name bleibt vor/nach Hit und nach Dispose identisch. Die
  persistente Generation bleibt bestehen.
- **Vier parallele Hits:** vier unterschiedliche request-owned Checkout-Pfade
  mit eigenen Markern und Solution-Pfaden, `transport.CallCount == 0`,
  `cacheWriter.Request == null` und identischer Current-Generation-Name vor,
  nach den Hits und nach dem Dispose aller Handles. Danach existieren keine
  `checkout-*`-Verzeichnisse unter der request-eigenen Staging-Wurzel.

## Leaks und offene Risiken

Nach den Verifikationsläufen waren keine `testhost.exe`-,
`vstest.console.exe`- oder Test-`dotnet.exe`-Prozesse aktiv. Es wurden keine
neuen Test-Temp-Verzeichnisse, persistenten Test-Cachegenerationen,
Ownership-Marker oder Request-Checkouts zurückgelassen. Ein bereits
vorhandener Default-Cache-Rest unter
`src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source` wurde nur
inspiziert und nicht gelöscht.

Offen bleiben ausschließlich die bewusst gesetzten Grenzen: Der
Cross-Process-Lock ist out of scope; Refresh/Fetch/Policy/Retention/GC und
Health-/Host-/Provider-/Snapshot-/Registry-Ausbau sind nicht Teil dieses
Steps. Die beiden echten Reparse-/Symlink-Sicherheitsfälle bleiben auf diesem
Host wegen Win32 1314 transparent übersprungen.
