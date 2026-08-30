---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 036
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-30T05:03:00+02:00
code_commit_hash: 377b5360b757566c1a5d4695349ebab3f0e2e712
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 036: Gitea-Source-of-Truth mit Clean-Checkout und transparentem degraded Refresh-Vertrag

## Zusammenfassung

Step 036 schließt die Source-Policy-Grenze zwischen Gitea, besitzgeschütztem
Staging-Checkout, Cache-Refresh und Assembly-Selection. Der neue immutable
Vertrag trennt `Verified`, `Degraded` und `Unavailable` sowie
`Clean`, `Dirty` und `Unverified` als typisierte Zustände. Ein verifiziertes
Ergebnis benötigt weiterhin eine sichere Revision; ein `Degraded`-Ergebnis
trägt ausschließlich den validierten `LastGoodRevision` und keinen neuen
Checkout oder `ExternalSourceSnapshot`.

Der Git-Transport führt im eigenen Checkout einen nicht-interaktiven
`git status --porcelain=v1 --untracked-files=all`-Check vor Fetch/Reset und
erneut danach aus. Nur der vorhandene Ownership-Marker wird als erwartetes
untracked Artefakt ignoriert. Dirty, unvollständige oder nicht auswertbare
Statusdaten werden fail-closed als `Dirty` bzw. `Unverified` abgewiesen;
Credentials und Rohprozessausgaben gelangen nicht in Diagnosen. Revision,
Ownership, Solution-Pfad, Cleanup, Cancellation, Timeout und die bestehende
1314-/Reparse-Semantik bleiben erhalten.

Stale-Refresh-Fehler propagieren den vom Reader validierten alten Commit als
sichtbares `Degraded`-Metadatum, lassen den alten `current`-Pointer unverändert
und erzeugen weder einen neuen Success-Checkout, Snapshot noch Registry-Lease.
Ein sicher frischer `CurrentChanged`-Stand darf weiterhin wiederverwendet
werden. Ohne validierten Last-good-Commit bleibt der Fehler `Unavailable`.
Provider und Selection geben den Zustand bis zu `ProviderDegraded` weiter;
die Assembly-Tool-Grenze bleibt für diesen Zustand offen und nutzt den
statischen Decompilation-Fallback. `ConfigurationFailure` bleibt unabhängig
von Diagnosen terminal.

## Geänderte Dateien

Produktionsvertrag und Git-/Refresh-Pfad:

- `src/AiNetLinter/Configuration/ExternalSourceConfiguration.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryResultState.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceProviderResult.cs`
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceRepositoryAcquirer.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCheckoutStatus.cs`
- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryFailurePolicy.cs`
- `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs`
- `src/AiNetLinter/Mcp/Assemblies/UnavailableExternalSourceProvider.cs`
- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`

Fokussierte Regressionen und bestehende Vertragsanpassungen:

- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryCheckoutStatusTests.cs` (neu)
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportDegradedTests.cs` (neu)
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceProviderContractTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisConfigurationFailureTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs`

Die neuen beziehungsweise grenznahen Dateien bleiben unter den lokalen
Grenzen: `ExternalSourceRepositoryAcquirer.cs` 479, Transport 483,
Cache-Refresh 410, `ExternalSourceRepositoryCacheRefreshTests.cs` 496 und
`AssemblyAnalysisToolSupportTests.cs` 482 Zeilen.

## Commits

Code-, Test- und Policy-Commit:

- `377b5360b757566c1a5d4695349ebab3f0e2e712` — `feat: Sichere Gitea-Source-Health und Refreshes ab [decompiled-assembly-analysis]`
- Branch: `main`
- Push: nicht ausgeführt

Dieser Result-Nachweis und die Codemap werden in einem separaten
Dokumentationscommit abgelegt. Der Step wartet danach auf einen frischen,
separaten Kritiker gemäß Orchestrator-Ablauf.

## Verifikation

- Fokussierte Status-/Refresh-/Provider-/Fallback-Tests: **54 bestanden,
  0 übersprungen, 54 gesamt**.
- `dotnet build`: **0 Warnungen, 0 Fehler**.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  --no-build --no-restore`: **2.165 bestanden, 2 übersprungen, 2.167
  gesamt**.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  --no-build --no-restore`: **370 bestanden, 0 übersprungen, 370 gesamt**.
- `dotnet run --project src/AiNetLinter -- --config "rules.json" --path "."`:
  **Exit 0**, 3 bestehende Strukturverletzungen (2×
  `MaxDirectoryChildren`, 1× `DaemonHostCommand`-Footprint).
- Stress-Tests: **nicht ausgeführt**.

Die beiden bekannten FastTest-Skips sind unverändert:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide werden auf Windows wegen `Win32 ERROR_PRIVILEGE_NOT_HELD (1314)` beim
Erzeugen des realen Reparse-Falls übersprungen. Es wurde keine globale
Reparse-Sperre ergänzt. Die Tests verwenden `TestTempDirectory`, lokale
Fakes und keine echten Netzwerk-, Credential- oder Assembly-Ladeaktionen.

## MCP- und Qualitätsnachweis

Alle semantischen Abfragen wurden mit dem projektgebundenen MCP und dem
absoluten `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt.

### Feature-Kontext und Symbolgraph

Die finalen Feature-Kontexte meldeten für die neuen beziehungsweise geänderten
Produktionssymbole 0 direkte Violations:

| Symbol | deklarierte Zeilen | Code-Lines | AI-Context-Footprint | direkte Aufrufer / Tests |
|---|---:|---:|---:|---:|
| `ExternalSourceRepositorySourcePolicy` | 174 | 153 | 510 | 23 / 0 |
| `ExternalSourceRepositoryResultState` | 19 | 16 | 48 | 30 / 0 |
| `ExternalSourceProviderResult` | 58 | 45 | 384 | 41 / 0 |
| `IExternalSourceRepositoryAcquirer` | 6 | 6 | 25 | 4 / 0 |
| `ExternalSourceRepositoryAcquirer` | 467 | 429 | 1.942 | 24 / 22 |
| `GiteaGitRepositoryTransport` | 472 | 425 | 815 | 16 / 11 |
| `ExternalSourceRepositoryCacheRefresh` | 379 | 342 | 1.227 | 3 / 9 |
| `AssemblySourceSelectionOrchestrator` | 99 | 88 | 1.119 | 14 / 9 |
| `AssemblySourceSelectionScope` | 66 | 53 | 700 | 20 / 0 |
| `AssemblyAnalysisToolSupport` | 149 | 134 | 2.173 | 18 / 9 |

`get_symbol_body` prüfte die finalen Bodies von Source-Policy, Cache-Refresh
und Selection-Scope. `find_references` und `get_impact` für die
Source-Policy liefen mit Tiefe 2 über 14 besuchte Knoten und 80 Call-Sites,
ohne Trunkierung oder Clamp.

### Scoped Qualitätsaudits

- `find_duplicates`, Produktionsscope
  `src/AiNetLinter/Mcp/Assemblies`, `mode=clone`, `minTokens=20`,
  `similarityThreshold=near`: **385 Methoden, 0 Cluster, nicht trunciert**.
- Ergänzender Structural-Audit im selben Scope mit `minTokens=10`:
  **442 Methoden, 4 Kandidatencluster**. Die verbleibenden Kandidaten sind
  ein absichtlich paralleler Provider-/Transport-Resultatvertrag und
  bestehende semantisch getrennte Failure-/Native-/Session-Helper; die neue
  Health-State-Entscheidung wurde in der gemeinsamen Policy zentralisiert.
- `find_magic_values`, Produktionsscope, `changedOnly=true`,
  `includeSuppressed=false`: **6 Treffer in 6 eindeutigen Einträgen**, davon
  5 Constant-Kandidaten. Es bleiben nur bereits vorhandene Git-/Staging-
  Vertragswerte in geänderten Dateien sichtbar; neue Status-/Diagnosewerte
  sind in fokussierten Konstanten beziehungsweise begründeten internen
  Vertragsausnahmen zentralisiert. Keine neuen Secret-, URL- oder Pfadwerte
  in Diagnosen.
- `find_dead_code`, Produktionsscope Assemblies,
  `private_internal`, `confidence=both`, `mode=both`: **63 Dokumente,
  162 Symbole, 35 Low-Confidence-Kandidaten, 0 High-Confidence**, davon
  1 bestehende Property und 34 bestehende Native-Interop-Felder.
- Derselbe Dead-Code-Audit für die fokussierten Assembly-Tests:
  **18 Dokumente, 42 Symbole, 0 Kandidaten**.
- `get_violations` im Produktionsscope meldete nur den bestehenden
  `MaxDirectoryChildren`-Befund für `src/AiNetLinter/Mcp/Assemblies`;
  die betroffenen Symbole selbst blieben violation-frei.

### Safeguard

Der Safeguard wurde ohne Threshold-Trick mit `minScore=8` ausgeführt:

| Scope | Score | Ergebnis |
|---|---:|---|
| global | **5,66/10** | FAIL, Threshold 8,00, 3 Verstöße, 846 Klassen |
| `src/AiNetLinter/Mcp/Assemblies` | **5,76/10** | FAIL, Threshold 8,00, 3 Verstöße, 76 Klassen |

Der globale Wert entspricht damit der dokumentierten Baseline 5,66/10. Die
verbleibenden Befunde sind die bestehende Assemblies-Verzeichnisgröße, der
bestehende `DaemonHostCommand`-Footprint und das bestehende Task-Verzeichnis;
kein globaler Safeguard-Repair war Teil dieses Steps. Der Integration-Dogfood-
Safeguard lief im finalen Gate erfolgreich durch.

## Abweichungen, Risiken und Kritiker-Übergabe

- Die neuen kleinen Vertragsdateien wurden bewusst getrennt, weil die
  gemeinsame Resultat-State-Datei sonst den bestehenden transitiven
  AIContext-Footprint von `AssemblyAnalysisToolRegistrations` über die Grenze
  gedrückt hätte. Danach blieben die bekannten globalen Strukturwerte bei
  5,66/10 und die produktiven Grenzdateien unter ihrem Limit.
- `ConfigurationFailure`, Host-/MCP-Health, Retention/GC, Invalidation,
  transitive Referenzen und EPIC-05 wurden nicht geöffnet. `tech-debt.md`
  blieb unverändert; TD-001 bis TD-005 wurden nicht umetikettiert und es
  wurde kein neuer direkt notwendiger Eintrag erzeugt.
- Das verbleibende Host-Risiko sind die beiden 1314-Reparse-Skips. Ein echter
  Git-/Gitea-Server, echte Credentials und Assembly-Load wurden absichtlich
  nicht verwendet.
- Für den frischen Kritiker sind insbesondere die Last-good-/CurrentChanged-
  Race-Invariante, die Cleanup-Priorität bei Cancel/Publish-Fehlern, die
  secret-freie Statusdiagnose und der sichtbare statische Decompilation-
  Fallback zu prüfen.
