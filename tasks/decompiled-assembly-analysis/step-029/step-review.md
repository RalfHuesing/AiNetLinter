---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 029
epic: EPIC-04
step_type: single
reviewed_by: kritiker-agent
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T20:50:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 029: Cache-backed Initial Acquisition/Reuse

## Verdict

- [ ] **approved** — die Produktionsgrenzen sind plausibel, aber die dokumentierte Abnahme ist nicht vollständig belastbar
- [x] **issues** — ein zusammenhängender Korrektur-Scope für Testnachweis und Verifikationsdokumentation ist erforderlich; gemäß Review-Auftrag wurde kein Korrektur-Step angelegt
- [ ] **blocked** — keine Nutzerentscheidung erforderlich

Commit `82692da054136dd39f6a37d110926bb95b5d796c` ist gegen den Step-029-Plan
und das Konzept nicht freigabefähig. Die Cache-Reuse-Implementierung verwendet
den bestehenden bounded Reader, erzeugt eine neue request-owned Lease und fällt
bei kontrollierten Cache-/Materialisierungsfehlern in den bestehenden Acquirer-
Pfad zurück. Die beiden unten genannten Abnahmelücken verhindern jedoch das
geforderte `approved`: Die Zahlen und Audit-Behauptungen im Step-Ergebnis sind
nicht mit den reproduzierten Läufen vereinbar, und der zentrale Cache-Hit-Test
beobachtet weder einen Publish-Aufruf noch die unveränderte Current-Generation
direkt.

Der Korrekturscope soll als ein zusammenhängender Nachweis-/Test-Scope geplant
werden. Dieser Review-Auftrag erlaubt ausschließlich die Review-Dokumentation;
es wurden weder Produktionsfixes noch Änderungen an `task-state.md`,
`roadmap.md`, `codemap.md` oder `tech-debt.md` vorgenommen.

## Geprüft

- [ ] Plan-Erfüllung: Reuse-, Ownership-, Fallback-, Cancellation- und Concurrency-Code sind vorhanden; Kriterium 4 und die dokumentierte Kriterium-8-Abnahme sind nur teilweise beweiskräftig.
- [x] Rules-Konformität: Im betroffenen C#-Scope wurden keine neuen AiNetLinter-Violations festgestellt; MCP- und Audit-Prüfungen erfolgten mit absolutem `projectRoot`, Textsuche ausschließlich mit `rg`.
- [x] Logische Korrektheit: Reader, Materializer, Ownership-Grenze, Cleanup und Prozessgrenze wurden über Bodies, References, Impact und Tests geprüft; kein eigenständiger Produktionsfehler wurde nachgewiesen.
- [x] Konzept-Treue: Der Produktionsscope bleibt auf initiale Cache-Reuse begrenzt; Fetch/Refresh/Policy/Config/GC/Health/Host/MCP/EPIC-05/Assembly-Load und Cross-Process-Garantien wurden nicht eingeführt.
- [x] Build: selbst nachgeprüft, grün.
- [x] Tests: selbst nachgeprüft, grün; die zwei echten Win32-1314-Reparse-Skips bleiben transparent.

## Befund

### Plan-Erfüllung

1. **Strict Reader und Identität — erfüllt:** `AcquireAsync` versucht Reuse erst
   nach Mapping-/SolutionPath-Validierung. Der injizierte Port delegiert beim
   Produktions-Writer ausschließlich auf die bestehende strikte Reader-Fassade;
   Current, Generation, Manifest, unabhängig gelesenes Inventory, Key, URL,
   SolutionPath, Revision, Content-Menge, Größen, Hashes und Reparse-Grenzen
   werden dort fail-closed und bounded geprüft.
2. **Neue Request-Lease — erfüllt:** `ExternalSourceRepositoryCacheReuse`
   reserviert für jeden Hit einen neuen Checkout unter der Staging-Wurzel. Der
   Materializer kopiert nur erwarteten Content; GenerationPath, Current,
   Manifest, Inventory und persistenter Marker werden nicht zum Handle-Eigentum.
3. **Handle-/Snapshot-Lifetime — erfüllt:** Der Handle erhält den neuen
   Checkout-Pfad und die Manifest-Revision; Dispose bereinigt die neue Lease,
   während die veröffentlichte Generation nach dem Hit lesbar bleibt.
4. **Cache-first ohne Doppelarbeit — teilweise belegt:** Der Produktionspfad
   ruft aus `ExternalSourceRepositoryCacheReuse` weder Transport noch Writer-
   Publish auf. Der Hit-Test prüft aber nur Transport-CallCount, alte
   Generationsexistenz und erneute Lesbarkeit; er verifiziert nicht direkt, dass
   `current` denselben Generation-Namen behält und kein Publish-Double aufgerufen
   wurde (Finding 2).
5. **Fail-closed Fallback und Cancellation — erfüllt:** Reader-Miss,
   invalidierte Artefakte und kontrollierte Materialisierungsfehler bereinigen
   die eigene Lease und gelangen in den bestehenden Clone-/Write-through-Pfad;
   Cleanup-Fehler bleiben typed/fail-closed. Cancellation wird als
   `OperationCanceledException` mit dem ursprünglichen Token weitergeworfen.
6. **Bestehende Fehlersemantik — erfüllt:** Die vorhandenen typed HTTP-/Git-/
   Credential-/Process-/Native-/1314-/Reparse- und Handle-Cleanup-Tests bleiben
   grün und die geänderte Acquirer-Integration lässt deren Zuständigkeiten
   unverändert.
7. **Deterministische Isolation — erfüllt:** Die neuen Cache-/Staging-Roots
   verwenden `TestTempDirectory`; vorhandene Acquirer-Fabriken verwenden
   isolierte Writer-Roots. Der Default-Cache blieb bei neun vorbestehenden
   Dateien ohne Owner-Marker; im Repository-Temp-Root verblieben keine
   Testverzeichnisse. Die 305 älteren OS-Temp-Verzeichnisse stammen aus früheren
   Läufen (ältestes 2026-07-28, jüngstes 2026-08-07), kein solches Verzeichnis
   wurde in den letzten zehn Minuten erzeugt.
8. **Verifikation und Audit — teilweise erfüllt:** Build und beide vollständigen
   Nicht-Stress-Gates sind reproduziert grün. Die im Step-Ergebnis dokumentierten
   Fokus-/Fast-Zahlen und der behauptete solutionweite Drift-Audit stimmen jedoch
   nicht mit dem ausgeführten scoped Nachweis überein (Finding 1).

### Rules-Konformität

Der scoped `get_violations`-Lauf für `ExternalSourceRepository` meldete null
Violations. Die separate `safeguard`-Abfrage bleibt wegen bereits bestehender
Warnungen außerhalb des Step-029-Codes auf FAIL (Score 5,79/10): 54 Einträge im
Assemblies-Verzeichnis, der bestehende `DaemonHostCommand`-Footprint und 37
Einträge im Task-Verzeichnis. Diese Befunde sind keine neuen Cache-Reuse-
Violations und werden nicht als Tech-Debt dieses Reviews angelegt.

Die Regeln zur MCP-first-Semantik wurden eingehalten: Feature-/Symbol-/Body-,
References-, Impact-, Test-Kontext-, Violations- und Safeguard-Abfragen nutzten
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter`; `rg` wurde nur für Text-
und Dateisuche verwendet. Stress wurde nicht ausgeführt.

### Logische Korrektheit

Der Acquirer konstruiert den Cache-Key erneut aus kanonischer URL und
repository-relativem SolutionPath und akzeptiert nur das Ergebnis des bestehenden
bounded Readers. Der Materializer löst ausschließlich sichere Pfade im
validierten `generation/content` auf, kopiert mit den bestehenden Length-/Hash-
Primitiven und prüft die Ownership am Anfang und Ende. Der Handle referenziert
nicht `GenerationPath`; seine idempotente Bereinigung bleibt auf den neuen
Checkout begrenzt.

Miss, fehlender/ungültiger Current, fehlende Artefakte und Copy-/Hash-Abweichung
führen nach eigener Lease-Bereinigung zum vorhandenen Clone-/Write-through-Pfad.
Ein Cancellation-Fall wird nicht in einen Miss oder Clone umgedeutet. Die vier
parallelen Hits liefern unterschiedliche Checkout-Pfade; die Synchronisation
bleibt bewusst prozesslokal und behauptet keine Cross-Process-Garantie.

Die verbleibende logische Abnahmeschwäche ist kein nachgewiesener Fehler im
Produktionszweig, sondern die fehlende direkte Testbeobachtung der
„kein Publish / Current unverändert“-Garantie.

### Konzept-Treue (Ebene 4)

Die geänderten Produktionsdateien bleiben im vorgesehenen Cache-/Acquirer-
Initial-Reuse-Scope. Es gibt keinen Fetch, Refresh, Staleness-/Retention-/GC-
Mechanismus, keine Config-/Health-/Dirty-/Unbuilt-Erweiterung, kein Host-/MCP-
oder Provider-/Snapshot-/Registry-Redesign, keine AssemblyCache-
Vereinheitlichung und keinen Assembly.Load-/ALC-/Reflection-Pfad. Die bewusst
fehlende Cross-Process-Garantie bleibt korrekt.

Die Dokumentation muss allerdings den tatsächlich ausgeführten scoped Audit
beschreiben; der dort behauptete solutionweite Scan wäre für den von Plan und
Auftrag ausgeschlossenen globalen Sweep nicht der passende Nachweis (Finding 1).

### Build-/Test-Status

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests" → grün (34 bestanden, 1 Skip, 35 gesamt, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (2060 bestanden, 2 Skips, 2062 gesamt, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (370 bestanden, 0 Skips, 370 gesamt, 0 Fehler)
```

Der fokussierte Skip ist
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
wegen `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 beim Erzeugen eines echten
Symlinks. Im vollständigen Fast-Lauf kommt der bekannte
`ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`
mit demselben 1314-Grund hinzu. Es wurde kein Stress-Test gestartet.

Nach den Läufen waren keine `testhost.exe`-/`vstest.console.exe`-Prozesse und
keine Test-`dotnet.exe`-Prozesse aktiv. Drei persistente `dotnet.exe`-Prozesse
waren ausschließlich vorbestehende MSBuild-Node-Reuse-Prozesse; kein neuer
Testprozess blieb zurück. Der Default-Cache blieb bei neun Dateien ohne
Owner-Marker.

## Findings (nur bei `issues`)

1. `tasks/decompiled-assembly-analysis/step-029/step-result.md:111-159` — **[MAJOR] [Plan]** Die dokumentierte Verifikation ist nicht reproduzierbar und beschreibt den Audit-Scope falsch. Der exakte Plan-Filter lief mit 34 bestanden, 1 Skip und 35 gesamt, nicht mit den dokumentierten 89/2/91; der vollständige Fast-Gate lief mit 2060 bestanden, 2 Skips und 2062 gesamt, nicht mit 2056/2/2058. Der Integration-Gate stimmt mit 370/0/370 überein, die zwei widersprechenden Zählungen bleiben aber ein falscher Abnahmebeleg. Zusätzlich behaupten die Zeilen 140-148 einen `scopeDir=src` solutionweiten Drift-Audit, obwohl der Step globale DRY-/MagicValues-/DeadCode-Sweeps ausdrücklich ausschließt; der reproduzierte Review-Nachweis ist scoped auf `src/AiNetLinter/Mcp/Assemblies` und den betroffenen Testbereich. Auch der dort festgehaltene Safeguard-Score 5,65/10 ist mit der aktuellen scoped Abfrage (5,79/10 bei denselben drei bestehenden Warnungen) nicht reproduzierbar. **Fix:** `step-result.md` in einem zusammenhängenden Korrekturscope auf die tatsächlich ausgeführten Commands und Zahlen korrigieren, die beiden 1314-Skip-Testnamen samt Grund nennen, den Audit auf die erlaubten Cache-/Acquirer-Produktions- und Testpfade begrenzen und den geprüften Commit-Hash `82692da054136dd39f6a37d110926bb95b5d796c` eintragen. Die Korrektur muss keine globalen Audits nachholen und darf keine Produktionsdateien oder Task-State-Dokumente ändern.

2. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs:107-136,374-405` — **[MAJOR] [Plan/Logik]** Der zentrale Hit-Nachweis beweist „ohne Publish/current-Änderung“ nicht vollständig. `Acquirer_ValidCacheHitCreatesIndependentCheckoutWithoutTransportOrPublish` verwendet denselben `LocalExternalSourceRepositoryCacheWriter` zum initialen Publish und als Reader; ein erneuter `PublishAsync`-Aufruf würde deshalb nicht über einen Recording-Writer beobachtet. Nach Dispose wird nur geprüft, dass die alte Generation existiert und irgendein Current erneut lesbar ist; der konkrete Current-Generation-Name wird nicht mit dem Vorzustand verglichen. Der Paralleltest prüft ebenfalls nur Lesbarkeit, nicht die unveränderte Generation. Der Produktionscode ruft zwar im Reuse-Zweig keinen Writer auf, aber Kriterium 4 und die geforderte reale Testevidenz verlangen diesen Nachweis explizit. **Fix:** Den Hit-Test mit einem separaten isolierten Reader und einem `RecordingCacheWriter` als `cacheWriter` verdrahten, den Current-Generation-Namen vor dem Reuse snapshotten und nach dem Hit, nach Dispose und nach parallelen Hits identisch erwarten; zusätzlich muss `RecordingCacheWriter.Request` leer bleiben und Transport-CallCount null sein. Die bereits vorhandenen Assertions für neuen Checkout, eigenen Marker, persistente Generation und unabhängige Lease bleiben bestehen.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- **MCP:** Feature-/Symbol-/Body-Prüfungen für Acquirer, Reader, Writer, Storage, Materializer, Reuse, Reservation und PathGuard sowie References, symbolischer Impact und Test-Kontext waren verfügbar. `ExternalSourceRepositoryCacheReuse.TryAcquire` wird vom Acquirer und den neuen Cache-Tests aufgerufen; der Acquirer hat 38 statisch zugeordnete Tests in neun Dateien.
- **Violations/Safeguard:** `get_violations(scopeFilter=ExternalSourceRepository)` meldete 0. `safeguard` meldete 5,79/10 und nur die drei bestehenden Directory-/Footprint-Warnungen außerhalb des neuen Reuse-Codes.
- **DRY/Refactoring-Drift:** Scoped `find_duplicates` mit `mode=clone`, `minTokens=20` ergab 0 Exact-/Near-Cluster in Produktion (350 Methoden) und Tests (122 Methoden). Der zusätzliche `mode=refactoring-drift`-Check für `ExternalSourceRepositoryCacheStorage.CopyFile` ergab 0 Kandidaten. Der scoped Structural-Scan lieferte nur fachfremde bzw. bestehende Prüfungskandidaten; kein neuer Reuse-Clone und kein neuer Tech-Debt-Befund.
- **MagicValues:** Die vier neu hinzugekommenen Produktionsdateien meldeten jeweils 0 Treffer. Im breiteren bestehenden ExternalSourceRepository-Produktionsscope wurden 7 bestehende Contract-/Diagnose-/Pfadwerte und in der neuen Cache-Acquirer-Testdatei 34 absichtliche Fixture-/Fallwerte gefunden; kein neuer produktiver Magic-Value-Befund.
- **DeadCode:** `find_dead_code` mit `scopeFilter=ExternalSourceRepository`, Tests eingeschlossen, prüfte 24 Dokumente und 55 Symbole und meldete 0 unreferenzierte Symbole.

## Tech-Debt-Einträge aus diesem Review

Keine. Die bestehenden Safeguard-/Directory-/Footprint-Warnungen liegen außerhalb
des Step-029-Scopes; DRY-, MagicValues- und DeadCode-Prüfungen ergaben keinen
neuen in-scope Tech-Debt-Befund.

## Folgeaktion

Einen einzelnen Korrektur-Scope für die beiden MAJOR-Nachweislücken anlegen:
Testisolation um einen beobachtbaren Writer ergänzen und Current-/Publish-
Unverändertheit direkt assertionsfähig machen; anschließend `step-result.md`
auf die reproduzierten Fokus-/Fast-/Integration-Zahlen, die echten 1314-Skips,
den scoped Audit und den geprüften Commit-Hash korrigieren. Danach den exakten
Fokus-Filter, Build und beide vollständigen Nicht-Stress-Gates erneut ausführen.
Bis dahin kein `approved`.
