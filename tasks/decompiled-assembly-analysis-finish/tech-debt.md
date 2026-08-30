# Task-lokales Tech-Debt-Register

Dieses Register enthält nur actionable Minor-/P2-/P3-Befunde mit bewusster
Disposition. P0-/P1-Befunde bleiben im `roadmap.md`-Blocker und im
`execution-log.md`; kosmetische oder unbelegte Vorschläge werden dort nur im
Bericht festgehalten.

## TD-001 — Windows-Git-Prozess-Tests stabilisieren

- Schweregrad: P2
- Scope: `ExternalSourceGitProcessExecutorTests`
- Evidenz: breiterer Epic-2-Integrationslauf mit zwei Fehlern wegen
  Windows-Zugriffsrechten bzw. Prozess-Timeouts; im Epic-3-Korrekturlauf trat
  derselbe parallele Git-Prozess-Timeout erneut auf, während der isolierte
  Wiederholungstest bestand. Test-/Produktionsbereich lag außerhalb des
  jeweiligen Feature-Diffs.
- Disposition: `accepted-deferred`
- Nächster Schritt: Testumgebung und Prozessberechtigungen isoliert prüfen und
  den Git-Prozess-Test deterministisch machen, ohne den Assembly-Analyse-Scope
  auszuweiten.
- Log-Anker: `execution-log.md` — Epic-2-Review abgeschlossen und
  Epic-3-Korrekturrunde-3-Implementierer abgeschlossen

## TD-002 — Bestehenden ProjectRegistry-FastTest prüfen

- Schweregrad: P2
- Scope: `ProjectRegistryTests`, vollständiger FastTests-Non-Stress-Lauf
- Evidenz: Epic-3-Implementierer meldete 2216/2219 erfolgreiche Tests; der
  Fehler lag in einem unveränderten ProjectRegistry-Test außerhalb des Epic-
  3-Diffs, zwei bekannte Reparse-Tests wurden übersprungen.
- Disposition: `accepted-deferred`
- Nächster Schritt: beim Abschluss-Gate reproduzieren; bei erneutem Auftreten
  die Testisolierung bzw. Windows-Umgebungsabhängigkeit separat beheben.
- Log-Anker: `execution-log.md` — Epic-3-Implementierer abgeschlossen

## TD-003 — Diagnostische Magic-Value-Kandidaten bewerten

- Schweregrad: P3
- Scope: Assembly-Analysis-Scope
- Evidenz: `find_magic_values` meldete sieben diagnostische/Identifier-
  Kandidaten ohne sichere scope-nahe Refactoring-Korrektur.
- Disposition: `accepted-deferred`
- Nächster Schritt: erst im Abschluss-Audit prüfen, ob fachlich identische
  Werte tatsächlich eine gemeinsame Konstante benötigen; Diagnosecodes,
  Identifier und Wire-Verträge nicht pauschal zentralisieren.
- Log-Anker: `execution-log.md` — Epic-3-Implementierer abgeschlossen und
  Epic-3-Review abgeschlossen

## TD-004 — Snapshot-Eviction unter konkurrierendem Acquire serialisieren

- Schweregrad: P2
- Scope: `SourceSnapshotRegistry.EvictIdle`
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass Ressourcen vor
  der Snapshot-Sperre entfernt werden; ein paralleles `Acquire` kann dadurch
  einen Lease erwerben, bevor der Snapshot trotzdem entfernt und disposed
  wird. Aktuell wurde kein produktiver Aufrufer gefunden.
- Disposition: `accepted-deferred`
- Nächster Schritt: bei einer späteren Lifecycle-Härtung Eviction und Acquire
  unter einer gemeinsamen Ownership-/Lease-Entscheidung serialisieren und
  einen Race-Test ergänzen.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-1-Review

## TD-007 — Assembly-Analyse-Footprint weiter aufteilen

- Schweregrad: P2
- Scope: `AssemblyAnalysisRegistry`
- Evidenz: Der Epic-4-Implementierer hat `AssemblyAnalysisToolSupport`, die
  Source-/Configuration-Supportpfade und `GetServerHealthTool` mit kleinen
  Factory-/Facade-Splits unter das `AIContextFootprint`-Limit gebracht. Die
  Registry bleibt im aktuellen MCP-Nachweis mit `3594 > 2500` auffällig; ihr
  Lease-Vertrag zieht `McpCodeGraphServer`, `ExternalResourceRegistry` und
  `ExternalResourceLease` in den transitiven Kontext.
- Disposition: `accepted-deferred`
- Nächster Schritt: nur in einem eigenständigen Verantwortlichkeits-
  Refactoring prüfen, ob der Registry-Kontext ohne Middleman, Vertragsdrift
  oder neue Footprint-Verstöße weiter zerlegt werden kann; dabei Safeguard und
  get_violations erneut bewerten.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-2-Implementierer
  abgeschlossen

## TD-008 — Expansion-Diagnosen im Extensions-Tool ausweisen

- Schweregrad: P2
- Scope: `FindAssemblyExtensionsTool`
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass das Tool nur aus
  `lease.Context` baut und Expansion-Diagnosen nicht übernimmt; ein
  fehlgeschlagener Child-Lease kann dadurch als `complete` erscheinen.
- Disposition: `fixed`
- Nächster Schritt: im Review und im Abschluss-Gate die gemeinsame
  Diagnoseprojektion regressionsfrei bestätigen.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-2-Review

## TD-009 — Negative Expansion-Routen über den Dispatcher testen

- Schweregrad: P2
- Scope: `AssemblyAnalysisRouteTests`, Dispatcher-/Tool-Antwortpfad
- Evidenz: Der unabhängige Epic-3-Review bestätigte erfolgreiche physische
  und Source-Project-Expansion, aber Missing-/Cycle-/Limit-Fälle nur auf
  Resolver-Ebene statt im vollständigen Dispatcher-/Tool-Antwortpfad.
- Disposition: `fixed`
- Nächster Schritt: die neuen Dispatcher-/Tool-Routentests im Abschluss-Gate
  als frischen Nachweis weiterverwenden; keine Resolver-Testduplikate
  ergänzen.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-2-Review

## TD-010 — Daemon-Session-ResidentCount deterministisch machen

- Schweregrad: P2
- Scope: `DaemonHostMcpContractTests.RunMcpSessionAsync_RegisteredAssemblyToolsReuseCompositionAcrossSessions`
- Evidenz: Der unabhängige Korrekturrunden-3-Review wiederholte den zuvor
  fehlgeschlagenen Test gezielt; `composition.Sessions.ResidentCount` erwartete
  `1`, erhielt `2`. Die benachbarte Snapshot-Registry-Assertion war grün,
  der Testcode unverändert und der Befund betrifft die bereits bewertete
  Assembly-Session-/Transitivroute, nicht den aktuellen Snapshot-Rollback.
- Disposition: `fixed`
- Nächster Schritt: im Gesamtabschluss die getrennten Projekt- und Assembly-
  Resident-Assertions als konsistente Verträge weiterverwenden.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-3-Review

## TD-011 — Pfadbezogenen Unsupported-Status für get_file_tree nutzen

- Schweregrad: P2
- Scope: `AnalysisToolCall`, `get_file_tree`-Assembly-Route
- Evidenz: Der unabhängige Epic-4-Review stellte fest, dass die Assembly-
  Route `UnsupportedAssemblyTarget()` ohne Zielpfad verwendet, während die
  übrigen projektgebundenen Routen den pfadbezogenen Status liefern. Die
  Antwort bleibt fail-closed, ist aber nicht vollständig konsistent zum
  dokumentierten Unsupported-Vertrag.
- Disposition: `fixed`
- Nächster Schritt: im Abschluss-Review und bei den Abschluss-Gates die
  kanonische Pfadprojektion und den gezielten Route-Test weiterverwenden.
- Log-Anker: `execution-log.md` — Epic-4-Implementierung

## TD-005 — Source-Ressourcen vor Materialisierung budgetieren

- Schweregrad: P2
- Scope: `ExternalSourceSnapshotMaterializer`, `SourceSnapshotModels`
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass Source-Ressourcen
  erst nach vollständiger Materialisierung budgetiert werden und ein
  Schätzfehler auf `1,1` zurückfällt; transiente Disk-/Memory-Spitzen sind
  dadurch nicht geschützt.
- Disposition: `accepted-deferred`
- Nächster Schritt: eine belastbare Vorab-Schätzung oder reservierbare
  Streaming-/Rollback-Budgets definieren, ohne die Snapshot-Semantik zu
  verändern.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-1-Review

## TD-006 — Creation-Barrier-Cancellation mit Consumer-Semantik absichern

- Schweregrad: P2
- Scope: `AssemblySourceSelectionOrchestrator` Creation Barrier
- Evidenz: Der unabhängige Epic-3-Review stellte fest, dass die Barrier das
  Token des ersten Aufrufers verwendet; dessen Cancellation beendet nur den
  Completion-Task. Der vorhandene Test deckt ausschließlich den erfolgreichen
  Join ab.
- Disposition: `accepted-deferred`
- Nächster Schritt: Cancellation-/Abbruchsemantik für den Produzenten und
  wartende Consumer explizit festlegen und mit einem gezielten Test absichern.
- Log-Anker: `execution-log.md` — Epic-3-Korrekturrunde-1-Review
