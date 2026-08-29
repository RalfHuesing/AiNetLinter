# Step-032 Ergebnis: Validated Refresh/Fetch in neue Cache-Generation

## Status

Der Step-032-Vertrag ist implementiert und lokal verifiziert. Änderungen bleiben
auf den injizierten Cache-/Acquirer-/Transport-Pfad, dessen Test-Doubles und
diese Ergebnisdatei begrenzt. `task-state.md`, `roadmap.md` und `tech-debt.md`
wurden nicht geändert.

## Vertragsabdeckung

- `ExternalSourceRepositoryCacheRefreshPolicy` entscheidet anhand des bereits
  validierten Manifest-`CreatedUtc`. Der benannte Default beträgt 60 Minuten;
  `TimeProvider` ist injizierbar. Exakt `now == CreatedUtc + 60 Minuten` ist
  stale. Zukünftige und nicht als UTC markierte Zeiten fail-closed.
- Ein frischer Current benutzt ausschließlich den vorhandenen
  `ExternalSourceRepositoryCacheReuse`-Pfad. Fetch, Clone und Publish werden
  dabei nicht aufgerufen.
- Ein stale Current wird in einen neuen, ownership-markierten
  request-eigenen Checkout materialisiert. Der persistente Generation-Pfad
  bleibt unverändert; der Fetch läuft nur auf dem neuen Checkout und wird genau
  einmal aufgerufen.
- `IGiteaRepositoryTransport.FetchDefaultBranchAsync` verwendet den bestehenden
  Git-Prozess-Executor, Credential-/Environment-Isolation, bounded Output,
  Timeout, Cancellation und die vorhandene typisierte HTTP-/Git-Fehlerlogik.
  Die Sequenz ist `fetch --no-tags origin`, `reset --hard origin/HEAD`, danach
  `rev-parse --verify HEAD`. Credentials werden vor Reset/HEAD verworfen und
  nicht in Argumente oder Diagnosen geschrieben.
- Der Fetch-Checkout prüft vorhandene Ownership-, Pfad- und Reparse-Grenzen.
  Die bestehenden 1314-/Reparse-Fallbacks und Cleanup-Helfer bleiben die
  Sicherheitsgrenze.
- Ein erfolgreicher, vollständig validierter Refresh schreibt über den
  vorhandenen Writer eine neue Generation mit Manifest/Inventory-Read-back und
  schaltet den Current-Pointer atomar um. `ExpectedCurrentGeneration` ist für
  Refresh-Publishes verpflichtend; `null` behält die bisherige Clone-Semantik.
  Ein verschwundener oder geänderter Pointer wird als typed
  `CurrentChanged` behandelt.
- Refresh-Fetch-, Integritäts-, Publish- und Cancellation-Fehler geben weder
  stale Current zurück noch starten einen Clone-Retry. Alte Generation und
  alter Pointer bleiben unverändert; der neue Checkout und unvollständige
  Staging-Reste werden bounded bereinigt. Bei einem Current-Race wird der neue
  Current höchstens erneut vollständig gelesen und nach derselben Policy
  wiederverwendet; es gibt keinen zweiten Remote-Fetch.

## Deterministische Nachweise

Die neue Refresh-Komponentensuite enthält acht Tests für Policy-Grenze,
Fresh-Reuse, erfolgreichen Stale-Fetch, Fetch-/Integritäts-/Publish-Fehler,
Cancellation und Current-Race. Der erfolgreiche Refresh weist zwei
Generationen, neue Revision, unveränderten alten Content, neuen request-eigenen
Handle und leeres Staging nach. Fehler- und Cancellation-Tests weisen alten
Pointer, eine alte Generation und leeres Staging nach. Der Race-Test weist
neue Revision, `CurrentChanged`, unveränderten neueren Current und genau einen
Fetch nach.

Der Transport-Doppeltest weist drei Prozessaufrufe, Default-Branch-/Remote-HEAD-
Argumente, Credential-Isolation, Credential-Cleanup, HEAD-Validierung und
Caller-Cancellation nach. Es gibt keine echten Remote-, Gitea- oder Git-
Netzwerkzugriffe, kein `Assembly.Load`/ALC/Reflection und keinen Fremdcheckout-
Restore/Build/Test.

## Verifikation

| Lauf | Ergebnis |
|---|---:|
| `dotnet build --no-restore` | 0 Warnungen, 0 Fehler |
| fokussierte Refresh-/Acquirer-/Reuse-/Transport-Suite | 69 bestanden, 1 Skip, 70 gesamt |
| `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | 2.071 bestanden, 2 Skips, 2.073 gesamt |
| `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370 bestanden, 0 Skips, 370 gesamt |
| Stress | nicht ausgeführt |

Die zwei FastTest-Skips sind unverändert die echten Reparse-/Symlink-Tests
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
und
`ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`.
Beide melden transparent Win32 `ERROR_PRIVILEGE_NOT_HELD (1314)`; der Skip ist
kein Sicherheitsnachweis und wird unter privilegierten Bedingungen ohne Skip
wiederholt. Nach den Läufen waren keine `testhost`, `vstest` oder Test-`dotnet`
Prozesse aktiv. Drei vorhandene idle `dotnet MSBuild.dll`-Node-Reuse-Prozesse
blieben unangetastet und wurden nicht gelöscht.

## MCP-/DRY-/MagicValues-/DeadCode-Audit

- `get_feature_context` für Refresh meldet 353 Zeilen, 8 zugeordnete Tests und
  0 Violations; der Acquirer liegt mit 484 gemeldeten Zeilen/445 Codezeilen
  ebenfalls unter der Grenze und hat 0 Violations. Der scoped
  `get_violations`-Lauf meldet 0 Violations.
- Scoped `find_duplicates` meldet 0 Produktionscluster bei 368 Methoden und 0
  Testcluster bei 140 Methoden. Die frühere lokale Duplizierung der Cleanup-
  Projektion wurde durch Wiederverwendung des bestehenden Acquirer-Helfers
  beseitigt; kein globales Refactoring wurde vorgenommen.
- `find_magic_values` über den geänderten ExternalSource-Scope meldet 9
  erwartete Kandidaten in 9 Vorkommen: Test-/Temp-Identifier, vorhandene
  Fehlermeldungen, bestehende Cache-/Git-Konstanten und der neue benannte
  `CurrentChanged`-Diagnosecode. Keine Secrets wurden eingeführt oder in
  Pfade/Keys/Manifest/Diagnosen geschrieben; kein globaler Magic-Value-Sweep.
- `find_dead_code` meldet 0 High-Confidence-Treffer bei 67 Symbolen in 29
  Dokumenten.
- Der scoped `safeguard`-Lauf bleibt wegen drei Bestandsbefunden FAIL
  (Score 5,87/10): 56 Einträge im bestehenden Assemblies-Ordner, 40 Einträge
  im bestehenden Task-Ordner und der bestehende DaemonHost-Footprint
  2.975 > 2.500. Diese Befunde liegen außerhalb des Step-032-Scope und wurden
  nicht verändert.

## Offene Risiken / Folgearbeit

- Die Synchronisation ist wie geplant prozesslokal; der Pointer-
  `ExpectedCurrentGeneration`-Check schützt den vorgesehenen Writer-/Race-Pfad,
  ist aber keine allgemeine Cross-Process-Lease-Garantie.
- Retention/GC, Invalidierung, Telemetrie, Dirty-/Health-/degraded-Policy,
  Konfiguration und Host-/MCP-Wiring bleiben ausdrücklich Folgepakete.
