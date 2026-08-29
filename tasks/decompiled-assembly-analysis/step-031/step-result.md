---
status: done (pending review)
type: step-result
task: decompiled-assembly-analysis
step: 031
corrects: step-030
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_at: 2026-08-29
test_structure_commit: 552ef4d472bbd10a803d8458b14ff2d08c397912
status_after: done (pending review)
blocker_category: n/a
---

# Step 031 – Step-030-Gatebefunde und Nachweise korrigieren

## Ergebnis

Die Teststruktur bildet den bestehenden Cache-Reuse-Vertrag jetzt getrennt
und regelkonform ab. Die drei Cache-Hit-/Reuse-Tests liegen in der neuen,
nicht-partiellen `ExternalSourceRepositoryCacheReuseTests`-Klasse. Die
verbleibenden Acquirer-, Fallback- und Cancellation-Szenarien bleiben in
der bestehenden Partialklasse; diese behält ihre drei Dateien.

`ExternalSourceRepositoryCacheTestSupport.cs` enthält die einmalig extrahierte
cache-spezifische Fixture, den Recording-Writer, beide Reader-Doubles, den
Testdatenhalter sowie gemeinsame Current-/Ownership-/Read-Assertions. Die
Writer- und Read-back-Tests verwenden diese Supporttypen. `TestTempDirectory`,
Lease-/Marker-/Manifest-Details und die fachlichen Assertions wurden nicht
abgeschwächt.

Es wurde kein Produktionscode, keine Regelgrenze und kein Testfilter geändert.
Der Commit der Teststruktur ist
`552ef4d472bbd10a803d8458b14ff2d08c397912`.

## Geänderte Dateien

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheReuseTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheTestSupport.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs`
- `tasks/decompiled-assembly-analysis/step-029/step-result.md`
- `tasks/decompiled-assembly-analysis/step-030/step-result.md`
- `tasks/decompiled-assembly-analysis/step-031/step-result.md`

Nicht geändert wurden Produktionsdateien, `task-state.md`, `roadmap.md`,
`tech-debt.md`, Regeln, Integrationstest-Assertions und alle ausdrücklich
ausgeschlossenen Refresh-/Fetch-/Policy-/Config-/Retention-/GC-/Health-/
Host-/MCP-/Provider-/Snapshot-/Registry-/Transport-/Native-/EPIC-05-Bereiche.

## Kriterienabdeckung

1. **Teststruktur und Zeilengrenzen:** Die vom MCP gezählte betroffene
   Partialklasse besteht weiter aus genau drei Dateien. Die physischen
   Zeilenzahlen betragen:

   | Datei | Zeilen |
   |---|---:|
   | `ExternalSourceRepositoryCacheAcquirerTests.cs` | 335 |
   | `ExternalSourceRepositoryCacheReuseTests.cs` | 128 |
   | `ExternalSourceRepositoryCacheTestSupport.cs` | 192 |
   | `ExternalSourceRepositoryCacheWriterTests.cs` | 390 |
   | `ExternalSourceRepositoryCacheWriterReadBackTests.cs` | 394 |

   `get_violations(scopeFilter="ExternalSourceRepository")` meldet danach
   0 Violations in 26 Dateien. Die vorherige `MaxLineCount`-Violation
   (501 > 500) ist verschwunden.

2. **Reuse-Semantik:** Alle drei Tests prüfen weiterhin erfolgreichen
   Publish, getrennten Reader, `RecordingCacheWriter.Request == null`,
   `transport.CallCount == 0`, unveränderten Current-Generation-Namen,
   getrennte request-owned Checkouts, Ownership-Marker, SolutionPath,
   Dispose-Cleanup und Erhalt der persistenten Generation.

3. **Regressionen:** Der exakte neue Fokusfilter erfasst die neue
   Reuse-Klasse, die verbliebenen `Acquirer_`-Tests, die bestehende
   `ExternalSourceRepositoryAcquirerTests`-Klasse und die Cancellation-Tests.
   Er endete mit 51 bestandenen Tests, 1 Skip, 52 gesamt und 0 Fehlern.

4. **Integration-Ursachen:** Der CLI-Dogfood-Test und der Live-Safeguard-Test
   bestanden im fokussierten Lauf nach dem Split mit 2/2 Tests und 0 Fehlern.
   Der CLI-Exit-Code ist 0; der Safeguard-Korridor `score >= 5.0` bleibt
   unverändert.

5. **Vollständige Gates:** `dotnet build` endete mit 0 Warnungen und 0
   Fehlern. Der vollständige Fast-Lauf und der abschließende vollständige
   Integration-Lauf sind grün; Stress wurde nicht ausgeführt.

6. **Result-Korrekturen:** Step 029 weist nun seine historischen 34/1/35-,
   2060/2/2062- und 370/0/370-Stände sowie den damaligen scoped Audit aus.
   Step 030 weist nun den historischen Integration-Fehler 368/0/2/370,
   die beiden konkreten Gate-Ursachen und die damalige `MaxLineCount`-
   Violation aus. Beide Dokumente enthalten keine solutionweiten Audit-
   Behauptungen.

7. **Scoped Audits:** Die finalen MCP-Nachweise sind unten mit Scope,
   Parametern und Zählungen festgehalten. Es wurde kein globaler DRY-,
   Magic-Values- oder Dead-Code-Sweep ausgeführt.

8. **Arbeitsbaum-/Leak-Disziplin:** Der finale Arbeitsbaum enthält nur die
   fünf Teststrukturdateien und drei Result-Dateien. Testprozesse und
   Request-Checkouts wurden nach den Läufen nicht zurückgelassen.

## Exakte Verifikation

Der Fokuslauf wurde exakt so ausgeführt:

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheReuseTests|FullyQualifiedName~Acquirer_|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests" --logger "trx;LogFileName=step031-focus.trx" --no-restore
```

Ergebnis: 51 bestanden, 1 Skip, 52 gesamt, 0 Fehler. Der Skip ist
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`;
beim Erzeugen des echten Directory-Symlinks wurde
`ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 gemeldet.

Weitere tatsächlich ausgeführte Läufe:

| Lauf | Ergebnis |
|---|---:|
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --logger "trx;LogFileName=step031-fast-gate.trx" --no-restore` | 2060 bestanden, 2 Skips, 2062 gesamt, 0 Fehler |
| `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess\|FullyQualifiedName~McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults" --logger "trx;LogFileName=step031-failing-integration-after-split.trx" --no-restore` | 2 bestanden, 0 Skips, 2 gesamt, 0 Fehler |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --logger "trx;LogFileName=step031-integration-gate-rerun.trx" --no-restore` | 370 bestanden, 0 Skips, 370 gesamt, 0 Fehler |
| Stress-Kategorie | nicht ausgeführt |

Die zwei Fast-Gate-Skips sind:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide wurden ausschließlich wegen `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314
beim Erzeugen eines echten Symlink-/Reparse-Falls übersprungen. Es wurde kein
Fake-Reparse verwendet.

Der erste vollständige Integration-Lauf nach dem Struktur-Split endete mit
369 bestanden, 0 Skips, 1 Fehler und 370 gesamt. Der einzelne Fehler war ein
unabhängiger Timeout in
`ExternalSourceGitProcessExecutorTests.ExecuteAsync_UsesRealProcessStartInfoAndIsolatesEnvironment`
bei `ExternalSourceGitProcessExecutor.WaitForOutputAsync` (Zeile 196);
die Zieltests waren in diesem Lauf nicht fehlerhaft. Der anschließende
identische vollständige Nicht-Stress-Lauf ist mit 370/0/370 grün. Der
Fehlerversuch ist im TRX `TestResults/step031-integration-gate.trx` belegbar;
der grüne Abschluss im TRX `TestResults/step031-integration-gate-rerun.trx`.

## Vorher-/Nachher-Gates

| Zustand | CLI-Dogfood | Live-Safeguard | Vollständige Integration |
|---|---:|---:|---:|
| Step-030-Commit | Exit 1 wegen `MaxLineCount` 501 | `2,652253349573691 < 5,0` | 368 bestanden, 2 Fehler, 370 gesamt |
| Step-031-Fokus nach Split | Exit 0 | 2/2 grün, unveränderte Assertion | – |
| Step-031-Abschluss | Exit 0 | bestanden | 370 bestanden, 0 Skips, 370 gesamt |

Der fokussierte Fehlerlauf vor der Korrektur ist
`TestResults/step031-failing-integration.trx`; der fokussierte grüne Lauf
nach der Korrektur ist `TestResults/step031-failing-integration-after-split.trx`.

## Finale scoped MCP-/Audit-Nachweise

Alle folgenden Aufrufe verwendeten exakt
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`.

- `get_violations(scopeFilter="ExternalSourceRepository")`: 0 Violations
  in 26 Dateien.
- `safeguard(scopeFilter="ExternalSourceRepository", minScore=8,
  maxViolations=20)`: Score 5,7444444444444445/10, Threshold 8,00, FAIL;
  3 bestehende Befunde außerhalb der Cache-Teststruktur:
  `MaxDirectoryChildren` in `src/AiNetLinter/Mcp/Assemblies` mit 54
  Einträgen, `AIContextFootprint` für `DaemonHostCommand` mit 2975 > 2500
  samt `ProjectRegistry` 445, `McpCodeGraphServer` 444 und
  `DaemonPipeConnection` 288, sowie `MaxDirectoryChildren` im Task-Ordner
  mit 39 Einträgen. Kein `MaxLineCount`-Befund bleibt.
- `find_duplicates(mode="clone", minTokens=20,
  similarityThreshold="near", scopeDir="src/AiNetLinter/Mcp/Assemblies",
  scopeType="production")`: 0 Cluster bei 350 Methoden.
- Derselbe Clone-Aufruf mit
  `scopeDir="src/AiNetLinter.FastTests/Mcp/Assemblies", scopeType="tests"`:
  0 Cluster bei 124 Methoden.
- `find_magic_values(scopeFilter="src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository",
  includeTests=false)`: 7 bestehende Treffer in 7 eindeutigen Einträgen
  über 16 Dateien.
- `find_magic_values(scopeFilter="src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCache",
  includeTests=true)`: 89 Treffer in 75 eindeutigen Einträgen über 5 Dateien.
  Die Werte sind bestehende Fixture-, Fall-, Marker- und Diagnosetexte; es
  wurde kein globaler Magic-Value-Sweep gestartet.
- `find_dead_code(scopeFilter="ExternalSourceRepository", includeTests=true,
  mode="members")`: 0 unreferenzierte Symbole bei 26 Dokumenten und 58
  Symbolen.

Die semantischen Vorprüfungen erfolgten mit `find_symbol`,
`get_feature_context`, `get_class_structure`, `get_symbol_body`,
`find_references`, `get_impact` und `get_test_context` für Cache-Reuse,
Acquirer, Partialklasse und die drei Reuse-Methoden. Der zunächst mit einem
ungültigen Wert `detailLevel="full"` aufgerufene `get_impact` wurde danach
mit dem erlaubten `detailLevel="callers"` erfolgreich wiederholt.

## Ownership-, Isolation- und Leak-Nachweis

Die drei Reuse-Tests prüfen getrennte Publisher-/Reader-Instanzen auf einem
isolierten Cache-Root. Jeder Reuse-Hit liefert eine eigene request-owned
Lease; die persistente Generation und ihr Current bleiben erhalten. Nach
Dispose sind die Checkouts entfernt. Die verwendeten Fixtures basieren auf
`TestTempDirectory`.

Nach dem Abschlusslauf wurden festgestellt:

- keine aktiven `testhost.exe` oder `vstest.console.exe`;
- drei aktive `dotnet.exe` ausschließlich als MSBuild-Nodes mit
  `/nodeReuse:true`, kein zurückgebliebener Testprozess;
- 0 Verzeichnisse im Repository-`temp`;
- 9 Dateien im bestehenden
  `src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source`-Rest, nur
  inspiziert und nicht gelöscht;
- keine neuen persistenten Testgenerationen, Ownership-Marker oder
  Request-Checkouts.

## Offene Risiken

- Der scoped Safeguard bleibt wegen der drei bestehenden Directory-/Footprint-
  Befunde unter dem Threshold; diese Befunde sind nicht Teil von Step 031
  und wurden weder gefiltert noch verändert.
- Die beiden echten Reparse-/Symlink-Sicherheitsfälle bleiben auf diesem Host
  wegen Win32 1314 übersprungen; der Skip ist kein Sicherheitsnachweis.
- Der Cross-Process-Lock sowie Refresh, Fetch, Policy, Retention/GC,
  Health, Host-/Provider-/Snapshot-/Registry-Ausbau bleiben bewusst außerhalb
  des Steps.
