---
status: done
type: step-result
task: decompiled-assembly-analysis
step: 025
corrects: step-024
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_at: 2026-08-29
code_commit_hash: siehe finalen Commit (Abschlussantwort)
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 025: Exception-sicheres Multi-Owner-Cleanup

## Zusammenfassung

`SourceSnapshotRegistry.Dispose()` entnimmt die residenten Snapshots weiterhin
einmalig unter dem bestehenden Lock, leert die Registry und entsorgt danach
außerhalb des Locks in ordinaler `Identity.StableValue`-Reihenfolge. Jeder
Snapshot wird in einem eigenen Fehlerpfad versucht; ein fehlerhafter Cleanup
stoppt die weiteren Snapshots nicht. Erst nach dem vollständigen Best-Effort-
Durchlauf wird der Fehler sichtbar weitergegeben.

Der gemeinsame interne `DisposeFailureAggregator` erhält die bestehende
Fehlersemantik: genau ein Fehler wird mit `ExceptionDispatchInfo` und damit
erhaltener Exception-Information weitergegeben, mehrere Fehler werden in der
Versuchsreihenfolge als `AggregateException` aggregiert. `ExternalSourceSnapshot`
entsorgt weiterhin zuerst Workspace und danach Checkout-Owner und versucht den
zweiten Owner auch bei einem Workspace-Fehler. Snapshot- und Registry-Flags
bleiben terminal und wiederholte Dispose-Aufrufe führen zu keiner weiteren
Cleanup-Arbeit.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` — stabile
  Sortierung, per-Snapshot-Exception-Isolierung und abschließende Aggregation.
- `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs` — gemeinsamer
  Aggregationspfad bei unveränderter Workspace-vor-Checkout-Reihenfolge.
- `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs` —
  zwei lokale Regressionen für Fortsetzung/Idempotenz und stabile
  Mehrfachfehler-Reihenfolge; bestehender Snapshot-Builder um optionalen
  test-only Owner ergänzt.
- `tasks/decompiled-assembly-analysis/step-025/step-result.md` — dieser
  Nachweis.

Nicht geändert wurden Provider, Acquirer, Materializer, Host-Wiring,
Orchestrator, Transport-/Native-Pfade, Refresh/Cache/Manifest/Source-of-Truth,
`task-state.md`, `roadmap.md`, `codemap.md` und `tech-debt.md`.

## Ownership- und Aggregationsergebnis

- Die Registry setzt das Dispose-Flag vor der Entnahme, leert ihre Map unter
  Lock und hält den Lock während keiner Snapshot-Entsorgung.
- Alle entnommenen Snapshots werden genau einmal versucht; ein Fehler im ersten
  Snapshot lässt den zweiten Snapshot nicht aus und leakt keinen Owner.
- Die Tests registrieren `Omega` vor `Alpha`, prüfen aber die durch
  `StableValue` vorgegebene Reihenfolge `Alpha`, `Omega`.
- Der Einzelfehler `alpha` bleibt als `InvalidOperationException` sichtbar;
  zwei Fehler erscheinen als `AggregateException` mit `alpha`, danach
  `omega`.
- Registry und Snapshots sind nach dem ersten Dispose terminal; der direkte
  Folge-Dispose bleibt fehlerfrei, `ResidentCount` bleibt `0` und jeder
  test-only Owner wird genau einmal aufgerufen.
- Workspace-vor-Checkout sowie die lokale Snapshot-Idempotenz wurden nicht
  verändert. Provider-/Materializer-/Lease-/Duplicate-Verträge bleiben
  unverändert.

## Teststatus

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SourceSnapshotRegistryTests"
  5 bestanden, 0 übersprungen, 0 Fehler

dotnet build
  erfolgreich; 0 Warnungen, 0 Fehler

dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  2.001 bestanden, 1 übersprungen, 0 Fehler; 2.002 gesamt
  Skip: bestehender echter Reparse-/Symlink-Fall wegen Win32
        ERROR_PRIVILEGE_NOT_HELD (1314)

dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  370 bestanden, 0 übersprungen, 0 Fehler

Stress-Kategorie
  nicht ausgeführt
```

Der fokussierte Test sowie beide neuen Regressionen sind vollständig lokal
und deterministisch. Es wurden kein Netzwerk, kein Gitea-/Git-Zugriff, kein
Prozess und kein fremder Checkout ausgeführt. Nach dem Lauf blieben keine
`external-source-*`-Testverzeichnisse und keine `testhost`-/`vstest`-Prozesse
zurück.

## MCP- und Qualitätsbefunde

- `get_feature_context` und `get_symbol_body` bestätigten die geänderten
  Registry-/Snapshot-Methoden; beide Dateien haben 0 symbolbezogene
  Violations. `find_symbol` löst den neuen internen Aggregator auf.
- `find_references`/`get_impact` bestätigen für den Aggregator genau zwei
  produktive Aufrufer: Registry und Snapshot. Die allgemeine Dispose-Suche
  expandiert wegen des überladenen Methodennamens erwartungsgemäß breit und
  wurde nicht als enger Ownership-Impact interpretiert.
- Metriken bleiben innerhalb der Regeln: Registry-Dispose 32 Codezeilen,
  zyklomatisch 5, kognitiv 5; Snapshot-Dispose 25/4/3; der Aggregator ist
  ein kurzer zentraler Pfad. `get_violations` findet auf beiden geänderten
  Produktionsdateien 0 Befunde.
- Vollständiger Safeguard: 5,645/10, PASS bei Threshold 0; sichtbar bleiben
  nur bestehende Warnungen zu `Mcp/Assemblies`-Verzeichnisgröße,
  `DaemonHostCommand`-Footprint und `tasks`-Verzeichnisgröße. Der zuvor
  durch den Footprint-Schwellenübertritt beeinflusste Live-Test besteht nach
  der kompakten Aggregator-Fassung wieder.
- DRY: solutionweiter `find_duplicates` mit `minTokens=20` meldet 27
  bestehende Cluster; Produktionsscope `Mcp/Assemblies` 0 Exact-/Near-
  Clone-Cluster, Registry-Testscope 0. Der strukturelle Assemblies-Scan
  meldet fünf bestehende Kandidaten; die Cache-Datei-/Verzeichnis-Helper,
  typed Result-Konstruktionen sowie Failure-Policy-Methoden sind außerhalb
  dieses Registry-/Snapshot-Pakets. Der Refactoring-Drift-Check für den
  Aggregator meldet keine Kandidaten.
- MagicValues: Produktionsscope meldet ausschließlich die sechs bereits
  vorhandenen Lokalisierungs-Exceptiontexte in `SourceSnapshotModels.cs`;
  der Testscope meldet nur die bestehenden URL-/Revision-Testwerte. Die
  wiederverwendeten Testfehler-Marker sind als testlokale Konstanten benannt.
- DeadCode: Assemblies-Scope meldet 36 Low-Confidence-Kandidaten und 0
  High-Confidence-Kandidaten, ausschließlich bestehende Native-/dynamische
  Felder und unbeteiligte Low-Confidence-Symbole; kein neuer Cleanup-Pfad ist
  unreferenziert.
- Die gezielte `rg`-Prüfung der geänderten Dateien findet keine neuen
  `Assembly.Load`, `AssemblyLoadContext`, Reflection-, Restore-, Build-,
  Test-, Netzwerk- oder Prozesszugriffe. Die gefundenen Gitea-Strings sind
  ausschließlich bestehende lokale Identity-Testwerte.

## Offene Risiken

- Der privilegierte echte Reparse-/Symlink-Test bleibt auf diesem Host wegen
  Win32 1314 übersprungen; die bestehende transparente Capability-Grenze wurde
  nicht verändert.
- Die drei bestehenden Safeguard-Warnungen bleiben bewusst außerhalb dieses
  Steps. Es wurde kein globaler DRY-, MagicValues- oder DeadCode-Sweep
  ausgelöst.
- `step-024` bleibt bis zur erneuten Kritikerprüfung fachlich offen; dieser
  Step korrigiert ausschließlich dessen MAJOR-001.

## Commit

Der Commit enthält Produktionsänderungen, lokale Regressionen und diesen
Result-Nachweis. Kein Push.
