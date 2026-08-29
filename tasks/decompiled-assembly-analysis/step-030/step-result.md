---
status: issues (review)
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
code_commit_hash: e9bf802505c1fb1ea706ed639effe1b3469c4b3
status_after: issues
blocker_category: n/a
---

# Step 030 – Cache-Reuse-Nachweise und Step-029-Result korrigieren

## Ergebnis

Step 030 korrigierte ausschließlich die Nachweise für den bereits
implementierten Cache-Reuse-Vertrag. Die validen Hit-Tests publizieren zuerst
über einen lokalen Publisher, lesen anschließend über eine getrennte lokale
Reader-Instanz und verwenden im Acquirer einen `RecordingCacheWriter`.
Damit bleiben ein unerwarteter Publish-Aufruf und der Transport-CallCount
beobachtbar. Der konkrete Current-Generation-Name wird vor dem Hit sowie
nach Hit und Dispose verglichen; die request-owned Checkouts bleiben von der
persistenten Generation getrennt.

Produktionscode, Regeln, Filter und fachliche Assertions wurden in Step 030
nicht geändert. Die Testdatei wurde dabei jedoch 501 Zeilen lang und verletzte
`MaxLineCount`; dadurch waren die beiden abhängigen Integration-Gates rot.

## Geänderte Dateien im Step-030-Commit

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs` – Recording-Writer, getrennter Reader sowie Current-/Ownership-/Cleanup-Assertions.
- `tasks/decompiled-assembly-analysis/step-029/step-result.md` – damalige Zahlen und scoped Nachweise.
- `tasks/decompiled-assembly-analysis/step-030/step-result.md` – dieser Nachweis.

Nicht geändert wurden Produktionscode, `task-state.md`, `roadmap.md`,
`tech-debt.md`, `ExternalSourceRepositoryCacheWriterTests.cs` sowie
Refresh, Fetch, Policy, Config, Retention/GC, Health, Host-/MCP-, Provider-,
Snapshot-, Registry-, Transport-, Native- und EPIC-05-Code.

## Kriterienabdeckung

- **Publish-/Reader-Nachweis:** Die zwei Acquirer-Hit-Tests bauen eine
  Generation mit erfolgreichem `PublishAsync` auf, lesen Current über einen
  separaten Reader und injizieren den `RecordingCacheWriter`. Nach dem Hit
  bleiben `Request == null` und `transport.CallCount == 0`.
- **Current-/Ownership-Nachweis:** Der Generation-Name bleibt vor und nach
  Single-Hit, Direct-Reuse, Dispose und Parallel-Hits gleich. Jeder Hit
  erhält einen getrennten request-owned Checkout mit eigenem Marker und
  SolutionPath; die persistente Generation bleibt bestehen.
- **Regressionen:** Fallback-, Missing-Artifact-, Materialisierungs-,
  Cancellation- und bestehende Acquirer-Tests blieben unverändert im
  Fokuslauf enthalten.
- **Nicht erfüllt:** Die funktionalen Nachweise waren erfüllt, aber die
  Testdatei überschritt die bestehende Grenze. Die beiden davon abhängigen
  Integration-Gates wurden mit dem Step-030-Commit rot reproduziert.

## Verifikation am Step-030-Commit

| Lauf | Tatsächliches Ergebnis |
|---|---:|
| `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests\|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests\|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"` | 34 bestanden, 1 Skip, 35 gesamt, 0 Fehler |
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2060 bestanden, 2 Skips, 2062 gesamt, 0 Fehler |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 368 bestanden, 0 Skips, 2 Fehler, 370 gesamt |
| `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess\|FullyQualifiedName~McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults"` | 0 bestanden, 0 Skips, 2 Fehler, 2 gesamt |
| Stress-Kategorie | nicht ausgeführt |

Die beiden Fast-Gate-Skips blieben echte Reparse-/Symlink-Fälle und wurden
ausschließlich wegen `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 beim Erzeugen
des Links übersprungen:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Die zwei Integration-Fehler waren:

1. `CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess`
   endete mit Exit-Code 1, weil
   `ExternalSourceRepositoryCacheAcquirerTests.cs` mit 501 Zeilen den
   `MaxLineCount`-Befund ausgab.
2. `McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults` erhielt
   den Score `2,652253349573691` statt des unveränderten Korridors `>= 5,0`.
   Der aktuelle Violation-Zustand enthielt dabei denselben neuen
   `MaxLineCount`-Befund sowie drei bestehende Struktur-/Footprint-Befunde.

Der fokussierte Fehlerlauf ist in
`TestResults/step031-failing-integration.trx` abgelegt. Es wurde kein
Fehler ausgeblendet und keine Assertion abgeschwächt.

## Scoped MCP-/Audit-Nachweise am Step-030-Commit

Alle Abfragen verwendeten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; es wurde kein
solutionweiter Audit ausgeführt oder behauptet.

- `get_violations(scopeFilter="ExternalSourceRepository")`: 1 Violation in
  `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs:1`,
  `MaxLineCount`, 501 statt maximal 500.
- `safeguard(scopeFilter="ExternalSourceRepository")`:
  2,7857142857142856/10 bei
  Threshold 8,00, FAIL; vier Befunde insgesamt: die neue `MaxLineCount`-
  Violation sowie drei bestehende Directory-/Footprint-Befunde außerhalb
  der Reuse-Logik.
- `find_duplicates(mode="clone", minTokens=20,
  similarityThreshold="near", scopeDir="src/AiNetLinter/Mcp/Assemblies",
  scopeType="production")`: 0 Cluster bei 350 Methoden.
- Derselbe scoped Clone-Aufruf mit
  `scopeDir="src/AiNetLinter.FastTests/Mcp/Assemblies", scopeType="tests"`:
  0 Cluster bei 124 Methoden.
- `find_magic_values(scopeFilter="src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository",
  includeTests=false)`: 7 bestehende Werte in 7 Einträgen über 16 Dateien.
- `find_magic_values(scopeFilter="src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs",
  includeTests=true)`: 35 Treffer in 34 eindeutigen, absichtlichen
  Fixture-/Fallwerten.
- `find_dead_code(scopeFilter="ExternalSourceRepository", includeTests=true,
  mode="members")`: 0 unreferenzierte Symbole bei 24 Dokumenten und 55
  Symbolen.

## Leak- und Übergabestatus

Die Cache-/Staging-Fixtures verwendeten `TestTempDirectory`; nach den
damaligen Läufen wurden keine aktuellen Testprozesse, Test-Temp-Verzeichnisse
oder Request-Checkout-Reste festgestellt. Ein vorhandener Default-Cache-Rest
wurde nur inspiziert und nicht gelöscht. Der Review `2510db5e` stufte den
Step wegen der Testdatei-Grenze, der zwei abhängigen Gate-Fehler und der
veralteten Nachweiswerte als `issues` ein. Step 031 behebt diese drei eng
gekoppelten Nachweislücken ohne Produktionsänderung.
