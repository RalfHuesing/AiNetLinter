---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 027
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T18:27:04+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 027: Fail-closeden Generation-Publish und Testisolation korrigieren

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step 028 erforderlich; gemäß Review-Auftrag wurde keine Plan-Datei angelegt
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [ ] Plan-Erfüllung: zwei deterministische Abnahmelücken, siehe Findings
- [x] Rules-Konformität: keine neuen Lint-Verstöße; Build und Testisolation regelkonform
- [ ] Logische Korrektheit: Produktionspfad ist plausibel korrigiert, die Race-/Read-back-Abnahme ist aber nicht vollständig beweiskräftig
- [x] Konzept-Treue: Cache-Publish-Scope, In-Process-Grenze und Non-Goals eingehalten
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: selbst nachgeprüft, grün; zwei transparente Win32-1314-Skips

## Befund

### Plan-Erfüllung

1. **Same-Key-Lifetime:** erfüllt. `PublishAsync` hält den Lease durch Publish, generation-aware Pointer-Prüfung, Rollback und Generation-/Temp-Pointer-Cleanup; erst danach wird `CacheKeyLockLease.Dispose()` ausgeführt.
2. **Deterministisches A/B-Race:** teilweise erfüllt. Der Test deckt beide Zustände mit und ohne vorherige Current-Generation ab, erzwingt aber die kritische Interleaving-Reihenfolge nicht deterministisch (Finding 1).
3. **Unabhängige Read-back-Prüfung:** im Produktionspfad erfüllt. Manifest und Inventar werden getrennt gelesen/abgeglichen, die erwartete kanonische Solution-Datei wird aus dem Read-Request verlangt, und die tatsächliche Content-Menge wird gegen das Inventar geprüft.
4. **Bounded UTF-8-/JSON-Read:** im Produktionspfad erfüllt, als Regressionstest aber teilweise belegt. Strict UTF-8, Byte-Grenzen, Wachstumserkennung, Hash-Bounds und unknown/duplicate-property-Ablehnung sind implementiert; die geforderte Testmatrix deckt diese Fälle nicht vollständig ab (Finding 2).
5. **Content-Hashing und Pfadschutz:** erfüllt. Datei-Wachstum und Trunkierung werden erkannt; Safe-Path-, Root-, Ownership-Marker- und Reparse-Prüfungen bleiben aktiv.
6. **Testisolation:** erfüllt. Schreibende Acquirer-/Provider-/Cancellation-Pfade injizieren einen lokalen Writer unter `TestTempDirectory` oder verwenden den Recording-Writer. Der bestehende Default-Cache blieb während der Prüfung bei 9 Dateien/9 Verzeichnissen.
7. **Bestehende Verträge und Scope:** erfüllt. Runtime-Default, Acquirer-Fail-open bei Cache-Publish-Fehlern, typed/geheimnisfreie Publish-Diagnostik, Ownership-/Transport-/Cancellation-Semantik und die ehrliche prozessinterne Lock-Grenze blieben erhalten.
8. **Verifikation:** erfüllt für Build, fokussierten Lauf und beide vollständigen Nicht-Stress-Läufe; die fachliche DoD-Anforderung „mit deterministischen Tests belegt“ ist wegen Findings 1 und 2 noch nicht erfüllt.

### Rules-Konformität

Die MCP-first-Vorgabe wurde für C#-Semantik eingehalten: absoluter `projectRoot`, Feature-/Symbol-/Body-, Referenz- und Impact-Prüfungen sowie Violations-/Safeguard-/Audit-Abfragen wurden verwendet; `rg` blieb auf Text- und Dateisuche beschränkt. Der scoped Violations-Check meldete 0 Verstöße in den Cache-Produktionsdateien und 0 Verstöße in den betroffenen Fast-Tests. Es wurden keine Produktionsfixes und keine Änderungen an `task-state.md`, `roadmap.md`, `codemap.md` oder `tech-debt.md` vorgenommen.

Die zwei Fast-Test-Skips sind ausschließlich echte Reparse-/Symlink-Capability-Skips mit transparentem `ERROR_PRIVILEGE_NOT_HELD` (Win32 1314). Der verbleibende `testhost`-Prozess nach dem Integration-Lauf gehörte laut Prozesspfad zu `SAN.smart.Planner.Platform`, nicht zu AiNetLinter; kein AiNetLinter-`testhost`/`vstest`-Prozess blieb zurück. Im Repository-Temp-Root blieb nur ein leerer, nicht markierter, dem Step-027-Präfix nicht zuordenbarer Ordner; kein neuer Test-Owner-Marker oder Cache-Leak wurde festgestellt.

### Logische Korrektheit

Der aktuelle Produktionspfad ist gegenüber den drei Step-026-MAJOR-Findings sachgerecht geändert: Der Lease umfasst die gesamte Finalisierung; `TryPublishPointer` prüft den erwarteten vorherigen Pointer unter dem prozessinternen Lease; Rollback restauriert/löscht nur, wenn der Pointer noch auf die fehlgeschlagene Generation zeigt; fremde Current-Generationen werden nicht gelöscht. Exceptions und Cancellation werden typed zurückgegeben, der Acquirer entzieht einen validen Checkout-Erfolg nicht wegen eines Cache-Publish-Fehlers.

`ReadBoundedText` verwendet einen einzelnen strikt UTF-8-dekodierenden Stream mit harter Byte-Grenze und Wachstumskontrolle. Manifest, Inventar und Content werden unabhängig gebunden; `ExpectedSolutionPath` ist Pflichtanker. Die Hash-Leseoperation ist length-bounded und erkennt Überlänge/Trunkierung. Im Produktionscode bestehen keine `FileInfo`-/`ReadAllText`-Lücke und kein falscher Cross-Process-Lock-Anspruch. Die verbleibende logische Abnahmeschwäche ist die fehlende deterministische Testbeobachtung an den kritischen Race-/Read-back-Grenzen, nicht ein nachgewiesener Fehler dieser Produktionspfade.

### Konzept-Treue (Ebene 4)

Commit `c5d64c42` bleibt auf Generation-Publish, Read-back und Testisolation begrenzt. Es gibt keinen neuen Reuse-, Fetch-, Refresh-, Config-, Health-, Retention-/GC-, Host-/MCP-, Assembly-Cache- oder EPIC-05-Schnitt. Step-024/025-Workspace-/Checkout-/Registry-Ownership sowie Git-, HTTP-, Credentials- und Native-Prozess-Invarianten bleiben unverändert; die Änderungen an Provider-/Transport-Testdateien dienen ausschließlich der Writer-Injektion.

### Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~GiteaExternalSourceProviderTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests" → grün (57 bestanden, 2 übersprungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (2019 bestanden, 2 übersprungen, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (370 bestanden, 0 übersprungen, 0 Fehler)
```

Stress-Tests wurden nicht gestartet.

## Findings

1. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs:22-61` — **[MAJOR] [Plan/Logik]** Der neue A/B-Konkurrenztest startet B im `afterPointerPublished`-Callback und wartet anschließend erst nach A's Rückkehr auf B. Dadurch wird nicht erzwungen, dass B genau zwischen einer verfrühten Lease-Freigabe und A's Rollback/Cleanup läuft; selbst die fehlerhafte Step-026-Reihenfolge kann je nach Scheduling vor A's Cleanup wieder in eine grüne Ausführung fallen. Damit ist Kriterium 2 und die DoD-Forderung eines deterministischen Regressionsbeweises nicht erfüllt. **Fix:** einen internen, test-only Synchronisations-Seam für den Finalisierungsabschnitt ergänzen, der B nach dem Pointer-Publish starten lässt und die Beobachtung vor A's Cleanup/Lease-Dispose deterministisch blockiert; mit `TaskCompletionSource`/Semaphore ohne Sleeps beide Varianten mit und ohne Previous-Current prüfen und erst nach dem Cleanup B fortsetzen.

2. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs:134-160` — **[MAJOR] [Plan]** Die neue Metadata-Regressionsprüfung testet Oversize und ungültiges UTF-8 nur am Current-Pointer sowie eine verkürzte Manifest-JSON (`"{"`). Es fehlen deterministische Nachweise für Manifest- und Inventar-Oversize/ungültiges UTF-8/Trunkierung, Wachstum während eines bounded Metadata-Reads sowie unbekannte und doppelte JSON-Eigenschaften; ebenso fehlt eine gezielte Matrix für Inventar-Dateizahl-, Gesamtgrößen- und Pfadgrenzen. Der Parser weist diese Fälle zwar fail-closed zurück (`ExternalSourceRepositoryCacheReader.cs:196-231`, `ExternalSourceRepositoryCacheReadSupport.cs:233-365`), aber Kriterium 4, die fokussierte Read-back-Testanforderung und die TOCTOU-Abnahme sind damit nicht vollständig belegt. **Fix:** einen kleinen internen deterministischen Read-Stream-Seam oder gleichwertigen kontrollierten Fixture-Mechanismus einführen und die Pointer-/Manifest-/Inventar-Fälle tabellarisch für Byte-Limit, Wachstum, Trunkierung, invalid UTF-8, unknown/duplicate properties sowie Entry-/Total-/Path-Limits ausführen; gültige Generationen und Hash-Growth/Truncation regressiv beibehalten.

## Sonstige Beobachtungen / MINOR / NITPICK

- `tasks/decompiled-assembly-analysis/step-027/step-result.md:6-7` weicht im Metadatenkopf vom Step-Plan ab: Dort steht `epic: EPIC-05` und `step_type: correction`, während der Step-027-Plan `EPIC-04` und `step_type: single` vorgibt. Das ändert den geprüften Produktionsscope nicht, sollte aber vor der Orchestrierung des Korrektur-Steps bereinigt werden.

## MCP-/Audit-Befunde

- AiNetLinter-MCP-Server: geladen, Version 1.0.154, Projektroot absolut auf `C:\Daten\Entwicklung\Ralf\AiNetLinter`.
- Feature-/Symbol-/Body-/Referenz-/Impact-Prüfungen deckten `PublishAsync`, `PublishGeneration`, `TryValidatePublishedGeneration`, `TryPublishPointer`, `RestorePreviousCurrent`, `TryDeleteGeneration`, `ReadBoundedText`, `ValidateInventory` und die Acquirer-Writer-Aufrufkette ab.
- `get_violations`: 0 im Cache-Produktionsscope; 0 im betroffenen ExternalSourceRepository-Testscope.
- `safeguard`: 5,70/10, FAIL wegen bereits bestehender Warnungen zu Directory-Children, `DaemonHostCommand`-Footprint und Task-Verzeichnis; kein neuer cache-publish-spezifischer Befund und kein Tech-Debt-Eintrag aus diesem Review.
- Scoped Drift-Audit: keine Exact-/Near-Clone-Cluster in Cache-Produktion (345 Methoden) oder betroffenen Tests (95 Methoden); strukturell 4 bestehende, fachfremde Produktionskandidaten und 0 Testkandidaten; keine Refactoring-Drift-Kandidaten für `ReadBoundedText` oder `ValidateInventory`.
- `find_magic_values`: 5 Produktions- und 27 Testtreffer, überwiegend zentralisierte Cache-Identifier/Diagnosekonstanten bzw. absichtlich eindeutige Test-Fixture-Literale; kein neuer blockierender DRY-/Magic-Values-Befund.
- `find_dead_code`: 0 unreferenzierte Symbole im Cache-Produktionsscope (24 geprüft) und 0 im Testscope (5 geprüft).

## Geprüfte Commit-Dateien

Commit `c5d64c42` änderte 17 Dateien: die Cache-/Read-back-Produktionsdateien unter `src/AiNetLinter/Mcp/Assemblies/`, die Writer-/Acquirer-/Provider-/Cancellation-Testdateien unter `src/AiNetLinter.FastTests/Mcp/Assemblies/` sowie `tasks/decompiled-assembly-analysis/step-027/step-result.md`. Review-Dokumentation ist die einzige zusätzliche Änderung dieses Durchgangs.

## Folgeaktion

Korrektur-Step 028 anlegen (`corrects: step-027`), ausschließlich die beiden MAJOR-Test-/Abnahmelücken schließen, danach denselben fokussierten Lauf sowie Build und beide vollständigen Nicht-Stress-Gates erneut ausführen. Bis dahin kein `approved`.
