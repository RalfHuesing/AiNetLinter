---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 028
epic: EPIC-04
step_type: correction
reviewed_by: kritiker-agent
reviewed_by_model: gpt-5
knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T19:33:12+02:00
verdict: approved
tech_debt_ids: []
---

# Review Step 028: Deterministische Read-back-/Lock-Lifetime-Nachweise

## Verdict

**approved**

Commit `83e52560e0ce2bedd2dd4fd1f5c19b93d20b36e4` erfüllt den Step-028-Plan,
die beiden MAJOR-Findings aus Step 027 und die bestehenden Cache-/Publish-
Invarianten. Es gibt keine Findings und kein neues Tech-Debt.

## Planprüfung

| Planbereich | Befund |
|---|---|
| Race-Test | Erfüllt. Zwei unabhängige Theory-Fälle prüfen `hasPreviousCurrent=true/false`. A publiziert den Pointer, wird abgebrochen und startet B. B wartet kontrolliert vor dem Pointer-Publish. Der `AfterLeaseReleasedAsync`-Hook prüft den bereits bereinigten Generationstand, gibt dann B frei und wartet auf dessen Pointer-Signal. B wird erfolgreich `current`; A bleibt abgebrochen und entfernt, ein vorheriger Current bleibt erhalten. |
| Determinismus | Erfüllt. Die Reihenfolge wird ausschließlich mit `TaskCompletionSource` (`RunContinuationsAsynchronously`), `SemaphoreSlim` und asynchronem `WaitAsync` mit 15-s-Bound kontrolliert. Keine neuen `Thread.Sleep`, `Task.Delay`, blockierenden `.Wait()`, `.Result` oder `GetAwaiter().GetResult()`. |
| Regression gegen Step 026 | Erfüllt. In `FinalizePublishAsync` liegt Restore/Generation-Cleanup vor `Dispose` des Leases. Würde die alte fehlerhafte Reihenfolge zurückkehren, liefe der Hook nach vorzeitiger Lease-Freigabe noch vor dem Cleanup; die Generationszählung in beiden Race-Varianten würde den zusätzlichen A-Stand deterministisch erkennen und fehlschlagen. |
| Malformed-Input | Erfüllt. Pointer, Manifest und Inventory werden unabhängig mit je sechs bzw. acht realen Fällen geprüft: Oversize, ungültiges UTF-8, Trunkierung, deterministisches Wachstum/TOCTOU, unbekannte und doppelte Felder sowie unbekannte/doppelte Datei-Felder, wo der Parser sie akzeptiert. Jeder Fall erwartet `false`, ein Ergebnis ohne Current und den Fehlercode `PublishFailed`; danach muss derselbe gültige Current unverändert lesbar sein. |
| Limit-Matrix | Erfüllt. Reale bounded Fixtures decken `MaxInventoryEntries + 1`, deklarierte und kumulative `MaxInventoryBytes`, `MaxFileLength + 1`, `MaxRelativePathLength + 1` und `fileCount`-Mismatch ab. Die JSON-Strukturen bleiben bis zum jeweils zuständigen Limitpfad gültig; es gibt echte Fail-closed- und Current-Erhalt-Assertions, nicht nur eine Falltabelle. Manifest-/Pointer-JSON-Limits werden durch die jeweilige Oversize-Matrix ebenfalls erreicht. |
| Content-Read-back | Erfüllt. Separate Tests weisen Content-Wachstum und Trunkierung/Hash-Abweichung zurück. Bounded Stream-Lesen prüft initiale und nachträgliche Länge, harte Byte-Grenze und striktes UTF-8; der Hash-Pfad prüft exakte Länge, EOF und Hash. |

## Race-Bewertung

| Vorheriger Current | Kontrollierte Sequenz | Nachweise |
|---|---|---|
| vorhanden | gültigen Previous anlegen; A publiziert Pointer und wird canceliert; B wird gestartet, bleibt vor seinem Pointer blockiert; A finalisiert unter demselben Key; erst nach tatsächlicher Lease-Freigabe gibt der Hook B frei und wartet auf dessen Pointer-Publish | B liefert Erfolg und ist `current`; A-Generation fehlt; B-Generation und genau eine Nicht-B-Generation bleiben; der Previous ist damit erhalten. |
| nicht vorhanden | identische Sequenz ohne Seed-Generation | B liefert Erfolg und ist `current`; A-Generation fehlt; genau die B-Generation bleibt. |

Die kritische Legacy-Regression ist ebenfalls deterministisch abgesichert: Bei
`Dispose` vor Restore/Cleanup würde die Assertion in
`ExternalSourceRepositoryCacheWriterReadBackTests.cs:103-108` die A-Generation
noch sehen, bevor B überhaupt freigegeben wird. Damit wird die frühere falsche
Lock-Freigabe als Testfehler reproduzierbar und nicht durch Thread-Scheduling
beobachtet. Die aktuelle Produktionsreihenfolge ist in
`ExternalSourceRepositoryCacheWriter.cs:118-136` nachvollziehbar.

## Malformed-Input- und Limit-Matrix

Die Fälle sind pro Theory-Ausführung in einem frischen `TestTempDirectory`
isoliert. Der Read-Stream-Seam öffnet nur das jeweils adressierte Pointer-,
Manifest- oder Inventory-Objekt kontrolliert; andere Reads laufen über den
normalen FileStream. Dadurch werden die drei Artefakte unabhängig geprüft.

| Artefakt | Fälle | Fail-closed-/Erhaltprüfung |
|---|---|---|
| Pointer/`current` | Oversize, invalid UTF-8, trunciert, gewachsen, unbekanntes Feld, doppeltes Feld | `TryReadCurrent` scheitert mit Diagnose; Pointerbytes bleiben unverändert; nach Wiederherstellung ist der vorherige gültige Current lesbar. |
| Manifest | Oversize, invalid UTF-8, trunciert, gewachsen, unbekanntes Feld, doppeltes Feld, unbekanntes Datei-Feld, doppeltes Datei-Feld | Die unabhängige Manifest-Validierung scheitert; ein gültiger Current wird nach Restaurierung nicht beschädigt. |
| Inventory | Oversize, invalid UTF-8, trunciert, gewachsen, unbekanntes Feld, doppeltes Feld, unbekanntes Datei-Feld, doppeltes Datei-Feld | Die unabhängige Inventory-Validierung scheitert; Pointer und gültige Generation bleiben erhalten. |
| Inventory-Limits | Entry-Count, deklarierte Gesamtbytes, kumulative Gesamtbytes, Dateilänge, relativer Pfad, `fileCount` | Jede Fixture erreicht einen strukturellen Parser-/Limitpfad und wird mit `PublishFailed` abgewiesen; danach bestätigt ein gültiger Read-back denselben Current. |

## Regeln, Produktionssemantik und Scope

- Der öffentliche Runtime-Pfad übergibt weiterhin keine Test-Seam; Runtime-
  Default, Cache-/Manifest-/Pointer-/Ownership-/Reparse-/Acquirer-Verträge und
  der Prozess-interne Same-Key-Lease bleiben unverändert.
- Die neue Produktionsoberfläche ist auf interne, per-Call injizierbare
  `Func<Task>`-Hooks und den optionalen Read-Stream-Seam begrenzt. Es gibt
  keinen globalen Lock, keine Cross-Process-Garantie, keine ALC-/Reflection-
  oder Plugin-Semantik und keine Scope-Ausweitung.
- Die Testunterstützung nutzt ausschließlich `TestTempDirectory`; es wurden
  keine neuen persistenten AppContext-Generationen erzeugt. Vorhandene
  Cache-Reste wurden nicht gelöscht.

## MCP-/DRY-/Magic-Values-/Dead-Code-Prüfung

Alle semantischen MCP-Abfragen verwendeten absolut:
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`.

| Prüfung | Ergebnis |
|---|---|
| Symbole, Bodies, References, Impact | Writer-, Finalize-, Reader-, ReadSupport- und betroffene Test-Symbole geprüft; Aufrufer und Auswirkungen stimmen mit dem per-Call-Seam überein. |
| `get_violations` | 0 Violations im produktiven Cache-Scope und 0 im betroffenen Test-Scope. |
| `safeguard` | Produktiv 5,75/10, Tests 6,50/10; ausschließlich bestehende Footprint-/Verzeichnisbefunde außerhalb des Step-028-Codes (50 Assemblies, Daemon-Footprint, Task-Verzeichnis). Kein neuer Step-Befund. |
| `find_duplicates` | Exact: 0 Cluster bei 345 Produktions- bzw. 112 Testmethoden. Strukturell: 3 bestehende produktive und 1 bestehender Test-Kandidatencluster, sämtlich außerhalb der geänderten Cache-Read-back-/Race-Fixtures und ohne Konsolidierungsbedarf. |
| `find_magic_values` | Scoped mit Tests: 62 Treffer in 49 Einträgen/12 Dateien; absichtliche Test-Identifikatoren, Fixture-Präfixe und bestehende Vertragskonstanten, kein neuer produktiver Magic-Value-Befund. |
| `find_dead_code` | Kein hochsicherer unreferenzierter Code im Produktions-Cache-Scope (25 Symbole/9 Dokumente) oder betroffenen Test-Scope (4 Symbole/2 Dokumente). |

## Verifikation, Skips und Leaks

- `dotnet build`: grün, 0 Compiler-Warnungen, 0 Fehler.
- Fokussiert: `ExternalSourceRepositoryCacheWriterTests`, 46 gesamt,
  45 erfolgreich, 0 Fehler, 1 Skip.
- Vollständiger Fast-Gate `Category!=Stress`: 2048 gesamt, 2046 erfolgreich,
  0 Fehler, 2 Skips.
- Vollständiger Integrations-Gate `Category!=Stress`: 370 gesamt, 370
  erfolgreich, 0 Fehler, 0 Skips.
- Stress wurde nicht ausgeführt.
- Die drei Reparse-/Symlink-Skips im Fast-/fokussierten Lauf sind die bekannten
  transparenten Windows-`ERROR_PRIVILEGE_NOT_HELD`-Fälle (Win32 1314), keine
  stillen Sicherheitsannahmen.
- Nach dem Lauf: `temp` leer, 0 `.ainet-test-owner-*`-Marker, 0 aktive
  `testhost.exe`/`vstest.console.exe`/`dotnet.exe`-Prozesse. Die während des
  Laufs entstandenen drei idle MSBuild-Node-Reuse-Prozesse wurden exakt
  identifiziert und beendet; bestehende Cache-Dateien blieben bei 9 Dateien in
  4 Generationen unverändert.

## Geprüfte Dateien und Folgeaktion

Der Review bezieht Commit `83e52560e0ce2bedd2dd4fd1f5c19b93d20b36e4` mit den
geänderten Produktionsdateien
`ExternalSourceRepositoryCacheWriter.cs`,
`ExternalSourceRepositoryCacheReader.cs`,
`ExternalSourceRepositoryCacheReadSupport.cs` und
`ExternalSourceRepositoryCacheModels.cs` sowie die Read-back-Testdatei,
Testunterstützung und das Step-Ergebnis ein. `task-state.md`, `roadmap.md`,
`codemap.md` und `tech-debt.md` wurden nicht geändert.

Keine Korrektur ist erforderlich. Nach Übernahme dieses Review-Commits kann
der Dev-Loop mit dem nächsten geplanten Step fortfahren.
