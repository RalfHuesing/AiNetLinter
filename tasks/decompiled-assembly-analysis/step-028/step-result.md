---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 028
epic: EPIC-04
step_type: single
coded_by: coder
coded_by_model: gpt-5
coded_at: 2026-08-29
code_commit_hash: siehe Abschluss-Commit
status_after: done (pending audit)
blocker_category: n/a
---

# Step 028 – Testlücken aus Step 027

## Ergebnis

Das Step-028-Test-/Nachweispaket ist implementiert. Es ergänzt einen deterministischen, per-Aufruf konfigurierbaren Test-Hook für den Publish-Pfad und eine bounded Read-back-Matrix für Pointer, Manifest und Inventory. Der öffentliche Runtime-Pfad übergibt weiterhin `null` an die Seam; Runtime-Default und Cache-Publish-Semantik bleiben unverändert.

Es wurden weder `task-state.md` noch Roadmap oder `tech-debt.md` geändert. Es wurden keine Assembly-Ladepfade, Remote-Zugriffe, Sleeps, unbounded Testdaten oder neuen Temp-/Cache-Builder eingeführt.

## Geänderte Dateien

- [ExternalSourceRepositoryCacheWriter.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs) – interne per-Aufruf-Publish-Seam; Hooks laufen an definierten Stellen, der Finalisierungs-Hook erst nach `Dispose` des Lock-Leases.
- [ExternalSourceRepositoryCacheModels.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheModels.cs) – interne per-Read-Call-Stream-Factory mit `null` als unverändertem Runtime-Default.
- [ExternalSourceRepositoryCacheReadSupport.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReadSupport.cs) – bounded Reads können test-only einen kontrollierten Stream öffnen; die bisherige `FileStream`-Implementierung bleibt Default.
- [ExternalSourceRepositoryCacheReader.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs) – Weitergabe der optionalen Stream-Factory an Pointer-, Manifest- und Inventory-Reads.
- [ExternalSourceRepositoryCacheWriterReadBackTests.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs) – deterministischer Race-Test, 22 malformed-input-Fälle und 6 Limit-Fälle.
- [ExternalSourceRepositoryTestSupport.cs](C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryTestSupport.cs) – wiederverwendbare kontrollierte Streams und kleine JSON-/Byte-Fixtures; kein neuer Cache- oder Temp-Builder.
- [step-result.md](C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-analysis/step-028/step-result.md) – dieser Nachweis.

## Kriterienabdeckung

### A – Deterministischer Publish-Race-Nachweis

Der Test `PublishAsync_CancellationAfterPointerPublishDoesNotRollbackConcurrentPublish` läuft in zwei Varianten: mit und ohne vorhandenen vorherigen Current.

Die per-Aufruf-Seams erzwingen diese Reihenfolge ohne Scheduler-Annahmen:

1. Publish A veröffentlicht seinen Pointer.
2. A cancelt sich selbst und startet Publish B mit einer neueren Generation.
3. B wartet vor seiner Pointer-Veröffentlichung an einem `SemaphoreSlim`.
4. A gibt die Lock-Lease tatsächlich frei; erst danach prüft der `AfterLeaseReleasedAsync`-Hook den Generation-Stand.
5. Der Hook gibt B über das Semaphore frei und wartet über eine `TaskCompletionSource` auf Bs Pointer-Veröffentlichung.
6. A wird als `Cancelled` beendet; B wird erfolgreich und bleibt Current.

Damit ist die kritische A/B-Interleaving-Reihenfolge pro Testaufruf reproduzierbar. Die Assertions prüfen insbesondere: Bs Current bleibt lesbar, Bs Generation bleibt vorhanden, As fehlgeschlagene Generation wird entfernt und ein vorheriger Current bleibt erhalten. Bei vorherigem Current sind nach Abschluss genau zwei Generationen vorhanden, ohne vorher genau eine; ohne vorherigen Current genau eine. Die frühere fehlerhafte Lock-Freigabe würde den beobachteten Zwischenstand bzw. den finalen Current-/Generation-Satz verletzen. Die Produktionssemantik wurde nicht für den Test umgebaut; die Seam ist intern, nicht statisch und im Runtime-Aufruf deaktiviert.

### B – Bounded malformed-input-Matrix

| Artefakt | Oversize | ungültiges UTF-8 | Trunkierung | Wachstum/TOCTOU | unbekanntes JSON-Feld | doppeltes JSON-Feld | unbekanntes Datei-Feld | doppeltes Datei-Feld |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Pointer | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | – | – |
| Manifest | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Inventory | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

Alle 22 Matrix-Fälle lesen zunächst einen gültigen Current ein, verändern nur das geprüfte Artefakt bzw. simulieren den Fehler über eine per-Aufruf-Stream-Factory und erwarten fail-closed: `TryReadCurrent` liefert `false`, keinen Current und einen Diagnostic-Code. Bei Manifest- und Inventory-Fehlern wird zusätzlich geprüft, dass der vorhandene Pointer unverändert bleibt; beim Pointer-Fall wird der gültige Pointer nach der kontrollierten Mutation restauriert und bytegenau verifiziert. Danach wird das konkrete Artefakt restauriert und derselbe Current erfolgreich erneut gelesen.

Die Oversize-/Growth-Fälle verwenden wenige echte Bytes mit kontrollierter logischer Länge; es werden keine 4-KB-, 16-MB- oder 1-GB-Dateien erzeugt. Invalid-UTF8- und Truncation-Fälle verwenden kleine rohe Byte-Arrays. Unbekannte und doppelte Felder werden sowohl auf Root-Ebene als auch – wo der Vertrag es unterstützt – in Manifest-/Inventory-Dateieinträgen geprüft.

Die ergänzende Limit-Matrix umfasst sechs Fälle:

- `MaxInventoryEntries` überschritten (10.001 minimale Einträge),
- deklariertes `totalBytes` über `MaxInventoryBytes`,
- kumulierte Dateigröße über `MaxInventoryBytes` mit fünf Einträgen,
- Dateilänge über `MaxFileLength`,
- Pfadlänge über `MaxRelativePathLength`,
- `fileCount`-Mismatch.

Auch diese Fälle sind bounded, fail-closed und prüfen den unveränderten Pointer sowie den erfolgreichen Read-back nach Wiederherstellung. Die bestehenden Tests für gekoppelte Manifest-/Content-Trunkierung, erwarteten Solution-Pfad sowie Content-Wachstum/Trunkierung bleiben erhalten.

### C – DRY und Scope

Die bestehenden `SourceFixture`, `TestTempDirectory`, Read-back-Assertions und Support-Infrastruktur werden weiterverwendet. Kontrollierte Streams und rohe JSON-Fixtures liegen im bereits vorhandenen `ExternalSourceRepositoryTestSupport`; sie kapseln nur die Matrixdaten und bauen keinen alternativen Cache-/Temp-Lifecycle.

Außerhalb des Test-Hooks wurden keine Cache-Key-, Manifest-, Writer- oder Reader-Regeln geändert. Es gab keinen Reuse-/Fetch-/Refresh-/Konfigurations-/Health-/Retention-/Host-/MCP-/Provider-/Snapshot-/Registry-/Transport-/Native- oder EPIC-05-Ausbau.

## Verifikation

| Lauf | Ergebnis |
|---|---:|
| Fokussierter Filter `FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests` | 45 bestanden, 1 Skip, 46 gesamt |
| `dotnet build` | 0 Warnungen, 0 Fehler |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2.046 bestanden, 2 Skips, 2.048 gesamt |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370 bestanden, 0 Skips |
| Stress-Kategorie | nicht ausgeführt |

Die zwei Skips sind die bekannten echten Windows-Reparse-/Symlink-Prüfungen mit Win32-Fehler 1314. Ein erster paralleler FastTests-Volllauf hatte einen isolierten, nicht cachebezogenen `MruStateStoreTests.DisposeAfterEmptyOrCorruptRead_WritesValidEmptyArray`-Fehler; der Einzeltest und der anschließende vollständige Wiederholungslauf waren grün. Dieses Verhalten bleibt als offenes Stabilitätsrisiko dokumentiert, nicht als Step-028-Produktionsfehler.

## MCP-, DRY-, MagicValues- und DeadCode-Befunde

- Semantische MCP-Abfragen wurden mit absolutem `projectRoot` ausgeführt: Feature-Kontext, Symbol-Bodies, Impact/References, Test-Kontext und Violations für Writer/Reader/ReadSupport/Models/Support.
- Der scoped Violations-Lauf meldete in den geänderten Cache-Produktions- und Testbereichen keine Violations.
- Der vorgeschriebene Drift-Audit über `find_duplicates` wurde ausgeführt. Cache-Produktionsklone: 345 Methoden, 0 Cluster; Cache-Testklone: 112 Methoden, 0 Cluster. Die solutionweite Ausgabe enthielt nur bestehende, nicht betroffene Cluster. Strukturelle Treffer waren ebenfalls nicht neu im Cache-Paket; ein Test-Treffer war ein bestehender, semantisch unabhängiger Support-Vergleich.
- Der scoped MagicValues-Lauf meldete 49 vorhandene/konfigurations- bzw. testbezogene Werte. Die neuen Fallnamen und Fixture-Werte sind absichtliche, bounded Matrix-Identifikatoren; es entstand kein neuer Produktions-Magic-Value.
- Der scoped DeadCode-Lauf meldete keinen High-/Low-Dead-Code: Produktion 25 Symbole, Tests 5 Symbole, Support 6 Symbole.
- `safeguard` meldete Score 5,928571 / FAIL wegen drei bestehenden Directory-/Footprint-Warnungen außerhalb der geänderten Dateien: `src/AiNetLinter/Mcp/Assemblies`-Kinderzahl, bestehendes `DaemonHostCommand.cs`-Footprint und `tasks/decompiled-assembly-analysis`-Kinderzahl. Kein geänderter Cache-Code war Ursache; kein globaler Sweep wurde durchgeführt.

## Leak- und Scope-Nachweis

Nach den Läufen waren keine aktiven passenden `testhost.exe`, `vstest.console.exe` oder Test-`dotnet.exe`-Prozesse vorhanden. Owner-Marker und temporäre Testverzeichnisse unter Repository/Temp lagen bei null. Ein vorhandener Default-Cache-Rest unter `src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source` wurde nur inspiziert und nicht gelöscht; vorbestehende Reste wurden ausdrücklich nicht bereinigt.

Die Tests enthalten keine `Assembly.Load`-/ALC-/Reflection-Ausführung, keinen Restore fremder Checkouts, kein Netzwerk/Remote/Git und keine Sleeps. `task-state.md`, Roadmap und `tech-debt.md` blieben unverändert. Der Arbeitsbaum enthält nach dem Abschluss-Commit keine uncommitteten Änderungen.

## Offene Risiken

- Die Lock-Koordination bleibt wie bisher prozesslokal; ein Cross-Process-Cache-Lock war nicht Teil dieses Steps.
- Die echten Reparse-/Symlink-Fälle konnten auf diesem Host wegen Win32 1314 nur transparent übersprungen werden.
- Der deterministische Test ist eine reproduzierbare Regression-Oracle für die frühere fehlerhafte Freigabe-Reihenfolge; die absichtlich fehlerhafte Produktionsversion wurde nicht in den Arbeitsbaum zurückgebaut.
- Der einmalige parallele MruStateStore-Fehler ist außerhalb des Cache-Scope, wurde reproduzierbar durch Einzeltest und Vollwiederholung eingegrenzt und sollte separat beobachtet werden.
