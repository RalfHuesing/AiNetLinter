---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 024
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T14:08:16+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 024: Erfolgreiches Acquirer→Snapshot-/Workspace-Wiring

## Verdict

- [ ] approved
- [x] issues
- [ ] blocked

Die Implementierung seit Commit `428cc4b328cacf53285c1837af3d2fd309c2ec0e` erfüllt Provider-, Materialisierungs-, Identity-, Fallback-, Sicherheits- und Testkriterien, hat aber einen reproduzierbaren MAJOR-Fehler im Registry-Cleanup bei einer Snapshot-Dispose-Exception.

## Geprüfter Umfang

Geprüft wurden der Commit-Diff einschließlich der neuen Provider-/Materializer-/Ownership-Implementierung, die geänderten lokalen Tests, die Step-024-Vorgaben, Konzept-/Roadmap-Grenzen sowie die unveränderten Acquirer-, Registry-, Transport- und Native-Invarianten. Es wurden keine Produktionsdateien geändert.

## Findings

### MAJOR-001: Registry-Dispose kann nach einer Cleanup-Exception verbleibende Snapshots leaken

- **Datei/Zeilen:** `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs:55-76`, insbesondere `:73`; verstärkt durch `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs:193-218`.
- **Reproduktion:** Eine Registry mit mindestens zwei verschiedenen Snapshots anlegen und beim ersten Snapshot einen Workspace- oder Owner-Dispose-Fehler auslösen. `ExternalSourceSnapshot.Dispose()` versucht Workspace und Checkout zwar beide, wirft danach aber die Exception. Die Schleife in `SourceSnapshotRegistry.Dispose()` bricht bei `remaining[0].Dispose()` ab; der Registry-Disposed-Schalter ist zu diesem Zeitpunkt bereits gesetzt (`:58`), sodass ein weiterer Registry-Dispose (`:56-59`) den nicht erreichten Snapshot nicht erneut entsorgt.
- **Auswirkung:** Der verbleibende Snapshot samt Roslyn-Workspace und ggf. Checkout bleibt undisposed. Damit ist die in Abnahmekriterium 4 geforderte Leak-Freiheit auch bei Cleanup-Exceptions nicht erfüllt; die Registry-Lifetime kann eine Ressource dauerhaft verlieren.
- **Korrekturscope/Priorität:** **MAJOR**, ein gebündelter Ownership-Korrekturschritt innerhalb Step 024. `SourceSnapshotRegistry.Dispose()` muss alle bereits entnommenen Snapshots auch nach einzelnen Dispose-Fehlern weiter entsorgen und Fehler erst nach dem vollständigen Durchlauf aggregiert weitergeben; Duplicate-/Lease-Semantik bleibt unverändert. Ergänzend ein deterministischer Registry-Test mit fehlerhaftem erstem und erfolgreich zu entsorgendem zweitem Snapshot sowie idempotentem Folge-Dispose.

Dies ist kein neues Tech-Debt außerhalb des Scopes, sondern ein offener Abnahmekriteriumsfehler. `tasks/decompiled-assembly-analysis/tech-debt.md` bleibt daher unverändert.

## Plan-Erfüllung

- **Kriterium 1:** Provider ruft den Acquirer mit Cancellation auf, projiziert nicht verfügbare Acquirer-Ergebnisse über `ExternalSourceProviderFailureProjection.FromUnavailableAcquisition` und rethrowt Cancellation; ein Snapshot wird erst nach Owner- und Identity-Prüfung verfügbar zurückgegeben.
- **Kriterium 2:** Der Materializer verwendet ausschließlich `checkout.SolutionPath` und den zentralen `SourceFileCatalogLoader.CreateMSBuildWorkspace()`-Pfad; die gezielte Suche fand keine Assembly-/ALC-/Reflection-Ausführung und keinen Restore-/Build-/Test-Aufruf.
- **Kriterium 3:** `SourceSnapshotIdentity.Create(mapping, checkout.LoadedRevision)` bindet die kanonische Mapping-URL, die bereits geladene Revision und den kanonisierten repository-relativen SolutionPath; im Provider gibt es kein HEAD- oder alternatives Resolving.
- **Kriterium 4:** Snapshot-Reihenfolge und direkte Snapshot-Idempotenz sind korrekt, aber der Registry-Dispose-Fehler aus MAJOR-001 verletzt die Ausnahme- und Leak-Garantie.
- **Kriterium 5:** Der Commit ändert keine HTTP-/Git-, Secret-/Argument-/WorkingDirectory- oder `CREATE_SUSPENDED`→Job→Resume/`KILL_ON_JOB_CLOSE`-Implementierung; der bestehende 1314-/Reparse-Testpfad bleibt erhalten.
- **Kriterium 6:** Die neuen Tests sind lokal und deterministisch, verwenden `IsolatedFixtureLease`/`TestTempDirectory`, erzeugen keinen Remote-/Gitea-/Git-/Netzwerkzugriff und prüfen Erfolg, Identity, Acquirer-Failure, Materializer-Failure, Cancellation sowie normale Lifetime-/Idempotenzpfade. Ein Ausnahmefall für Registry-Cleanup fehlt und ist wegen MAJOR-001 nachzuholen.
- **Kriterium 7:** Host-Wiring, Orchestrator, Refresh, Cache/Manifest/Generation/atomic Source-of-Truth, dirty/unbuilt, Credentials, Transport/Native und EPIC-05 bleiben außerhalb des Diffs.
- **Kriterium 8:** Build, fokussierte Tests und beide vollständigen Nicht-Stress-Gates sind grün; MCP-/DRY-/MagicValues-/DeadCode-Prüfungen zeigen keinen neuen lokalen Regel-, Clone-, Magic- oder High-Confidence-Dead-Code-Fund. Die Registry-Ausnahme ist ein semantischer Ownership-Fund, kein Linter-Fund.

## Rules-Konformität

Die geänderten Produktions- und Testdateien haben keine MCP-Lint-Violations; die einzige package-weite Meldung ist die bestehende `MaxDirectoryChildren`-Warnung für `Mcp/Assemblies` mit 41 Einträgen und liegt außerhalb dieses Steps. Die bestehenden Regeln zu zentralem Workspace-Pfad, sicherem Fallback, Cancellation, Test-Fixtures und fehlender Fremdcode-Ausführung werden eingehalten.

## Logische Korrektheit

Provider-Failure-Projection, Cancellation, kein partieller verfügbarer Snapshot, Identity-Bindung, zentraler Design-Time-Workspace sowie Workspace-vor-Checkout-Dispose sind korrekt implementiert; die Ausnahmebehandlung der Registry stoppt jedoch vor dem vollständigen Ressourcen-Durchlauf.

## Konzept-Treue

Der Acquirer→Snapshot-Vertrag bleibt read-only, revisionsgebunden und ohne Fremdcodeausführung; die beabsichtigte besitzgebundene Snapshot-/Registry-Lifetime wird erst nach Behebung von MAJOR-001 auch unter Cleanup-Exceptions vollständig erreicht.

## Build-/Test-Status

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~GiteaExternalSourceProviderTests"
  5 bestanden, 0 übersprungen, 0 Fehler

dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~ExternalSourceSnapshotMaterializerTests"
  2 bestanden, 0 übersprungen, 0 Fehler

dotnet build
  erfolgreich; 0 Warnungen, 0 Fehler

dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  1.999 bestanden, 1 übersprungen, 0 Fehler, 2.000 gesamt
  Skip: echter Reparse-/Symlink-Fall wegen ERROR_PRIVILEGE_NOT_HELD (1314)

dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  370 bestanden, 0 übersprungen, 0 Fehler

Stress-Kategorie: nicht ausgeführt.
```

Die Nachlaufprüfung fand keine `external-source-*`-Temp-Verzeichnisse, keinen Testhost und keine testseitig verbliebenen Fremdprozesse. Sichtbar waren nur bereits laufende AiNetLinter-MCP/Daemon-Instanzen sowie normale dotnet-MSBuild-Node-Reuse-Prozesse; sie wurden nicht beendet.

## MCP-/Qualitätsprüfung

- `get_feature_context`/`get_symbol_body` wurden mit absolutem `projectRoot` für Provider, Acquirer, Materializer, Snapshot, Registry und Workspace-Loader ausgeführt. Die geänderten Symbol-/Dateiscopes meldeten 0 Violations.
- `find_references`/`get_impact` bestätigten die Provider-/Materializer-Aufrufkette; der Provider-Impact war vollständig mit 13 Callsites, der exakte Materializer-Reference-Scan mit 14 Callsites. Der generische `Dispose`-Resolver expandierte erwartbar auf 1.614 Dispose-Aufrufe und wurde nicht als Beleg gegen die konkrete Snapshot-Implementierung verwendet.
- `safeguard` im Assemblies-Scope: 5,83/10, PASS bei `minScore=0`; die drei Hinweise betreffen bestehende Directory-/Footprint-Grenzen. Der aktuelle scoped `get_violations`-Scan zeigt nur die bestehende `MaxDirectoryChildren`-Warnung.
- DRY: solutionweiter `find_duplicates`-Scan mit `minTokens=20` fand 27 bestehende Cluster; kein neuer relevanter package-lokaler Exact-Clone. Produktionsscope `Mcp/Assemblies`: 0 Exact-Cluster bei 270 Methoden; lokaler Testscope: 0 bei 70 Methoden. Der strukturelle Scope-Scan fand fünf bestehende, unabhängige Kandidaten; `ThrowDisposeFailures` zeigte keinen Refactoring-Drift-Kandidaten.
- MagicValues: keine neuen Produktionskandidaten; sechs bestehende Localization-Kandidaten in `SourceSnapshotModels.cs`, Test-Literale nur lokal. DeadCode: Assemblies-Scope 36 Low-/0 High-Confidence-Kandidaten, ausschließlich bestehende Native-/dynamische Kandidaten; Workspace-Loader 0; kein neuer High-Confidence-Fund.
- Gezieltes `rg` fand in den neuen Produktions-/Testdateien keine Assembly.Load-, AssemblyLoadContext-, Reflection-, Restore-, Build-, Test-, HTTP-Client- oder Process-Aufrufe; der einzige Treffer war der ausdrücklich erlaubte 1314-Reparse-Gate im Testsupport.

## Ownership-, Fallback- und Ressourcenbewertung

Der Provider verwirft den Checkout bei jedem Fehler vor erfolgreicher Snapshot-Rückgabe, gibt Cancellation als `OperationCanceledException` weiter und exponiert keine Materializer-Exceptiondetails oder Pfade. Der Materializer öffnet den validierten Checkout-Pfad zentral als Design-Time-Solution und erzeugt keinen partiellen Erfolg bei Workspace-Diagnose, leerer Solution oder fehlgeschlagenem Öffnen. `ExternalSourceSnapshot.Dispose()` ist idempotent und entsorgt Workspace vor Checkout auch nach einem Workspace-Fehler; die normale Snapshot-/Registry-Lifetime ist durch die fokussierten Tests bestätigt. MAJOR-001 bleibt die einzige offene Ressourcenlücke im Ausnahmefall der Registry mit mehreren Snapshots.

## Geänderte Dateien durch den Kritiker

- Diese Review-Dokumentation: `tasks/decompiled-assembly-analysis/step-024/step-review.md`.
- `tasks/decompiled-assembly-analysis/tech-debt.md`, `task-state.md`, `roadmap.md` und `codemap.md` wurden nicht geändert.

## Folgeaktion

Kein `approved`. Der Coder korrigiert MAJOR-001 als ein zusammenhängendes Registry-Ownership-Paket und ergänzt den deterministischen Mehrfach-Snapshot-/Cleanup-Exception-Test. Danach ist Step 024 erneut vollständig zu reviewen; erst dann darf die geplante EPIC-04-Folgearbeit zu Refresh/Fetch, persistentem Cache und atomarer Source-of-Truth beginnen.
