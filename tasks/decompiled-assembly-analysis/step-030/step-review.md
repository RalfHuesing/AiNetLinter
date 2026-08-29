---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 030
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T21:38:36+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 030: Cache-Reuse-Nachweise und Step-029-Result korrigieren

## Verdict

- [ ] **approved** — die Cache-Reuse-Nachweise sind vollständig und alle Gates grün
- [x] **issues** — ein gebündelter Korrektur-Scope für Testdatei-Limit, Resultzahlen und Auditnachweis ist erforderlich
- [ ] **blocked** — keine Nutzerentscheidung erforderlich

Der Commit `e9bf802505c1fb1ea706ed639effe1b3469c4b3` korrigiert die beiden
ursprünglichen Step-029-Nachweislücken bei den validen Hits tatsächlich:
Publisher und Reader sind getrennt, der Acquirer nutzt den
`RecordingCacheWriter`, und Current-/Ownership-Assertions sind konkret.
Der Commit führt jedoch eine neue `MaxLineCount`-Violation in der geänderten
Testdatei ein. Dadurch ist das vollständige Integration-Gate rot; außerdem
sind die in den Result-Dateien dokumentierten Integration- und Auditwerte für
den aktuellen Commit nicht mehr reproduzierbar. Ein gemeinsamer
Korrektur-Scope muss die Testdatei wieder regelkonform halten, danach die
Resultate mit den tatsächlich ausgeführten Läufen aktualisieren und alle
Nachweise erneut ausführen. Es wurde weder Produktionscode geändert noch ein
Fix vorgenommen.

## Geprüft

- [ ] Plan-Erfüllung: Die Publish-/Reader-/Current-/Ownership-Nachweise sind erfüllt, aber die geforderte grüne Abschlussverifikation und der aktuelle Result-/Auditnachweis sind nicht erfüllt.
- [ ] Rules-Konformität: Der geänderte Testcode verletzt `MaxLineCount`; Produktionscode und die übrigen geprüften Regeln bleiben ohne neue Violation.
- [x] Logische Korrektheit: Die drei validen Reuse-Tests beobachten den konkreten Vertrag; ein Produktionsfehler wurde nicht nachgewiesen.
- [x] Konzept-Treue: Der Commit bleibt im Initial-Reuse-/Ownership-Scope; Refresh, Fetch, Provider-/Snapshot-/Host-Wiring, EPIC-05 und Assembly-Ausführung wurden nicht erweitert.
- [x] Build: selbst nachgeprüft, grün.
- [ ] Tests: Fokus und Fast-Gate grün, das vollständige Integration-Gate reproduziert zwei Fehler.

## Befund

### Plan-Erfüllung

1. **Result-Identität — erfüllt:** `step-029/step-result.md` enthält den
   vollständigen geprüften Step-029-Codehash
   `82692da054136dd39f6a37d110926bb95b5d796c`.
2. **Test-/Auditnachweis — nicht erfüllt:** Der Fokuslauf und der Fast-Lauf
   stimmen mit den dokumentierten Zahlen überein, der aktuelle
   Integration-Lauf endet aber mit 368 bestanden, 2 Fehlern und 370 Tests.
   Zudem meldet der aktuelle scoped MCP-Lauf eine `MaxLineCount`-Violation
   und `safeguard` 2,79/10 statt der im Result dokumentierten 0 Violations
   und 5,79/10.
3. **Publish-/Reader-Beobachtung — erfüllt:** Die beiden Acquirer-Hit-Tests
   publizieren zunächst mit einem lokalen Publisher, lesen über eine zweite
   lokale Writer-Instanz und verwenden am Acquirer den vorhandenen
   `RecordingCacheWriter`. Ein unerwarteter Publish-Aufruf würde dessen
   `Request` setzen; nach dem Hit bleibt er `null`, der Transport bleibt bei
   CallCount 0.
4. **Current-Unveränderlichkeit — erfüllt:** Der konkrete
   `Manifest.GenerationName`-Wert wird vor dem Hit und danach im Single-,
   Direct-Reuse- und Parallelfall erneut aus dem Reader gelesen; nach Hit,
   Dispose und Parallel-Cleanup wird derselbe Wert erwartet.
5. **Request-Ownership — erfüllt:** Jeder Hit erhält einen neuen Checkout
   mit eigenem Marker und SolutionPath. Die Assertions grenzen ihn gegen
   `published.GenerationPath` ab und prüfen, dass Dispose nur den Request-
   Checkout entfernt, während Generation und Current erhalten bleiben.
6. **Fallback-/Cancellation-Regression — erfüllt:** Die fokussierten
   Invaliditäts-, Missing-Current-, Missing-Artifact-, Materialisierungs-,
   Cancellation- und bestehenden Acquirer-Contracts bestehen; es wurden
   keine Assertions abgeschwächt und keine Produktionspfade verändert.
7. **Scope-/Arbeitsbaumdisziplin — teilweise erfüllt:** Der Commit enthält
   nur die geplante Testdatei und die beiden Result-Dateien; kein
   Produktionscode, Refresh, Fetch, Config, Retention/GC, Health, Host-/MCP-,
   Provider-/Snapshot-/Registry- oder EPIC-05-Code wurde geändert. Die
   geänderte Testdatei überschreitet aber das geltende Zeilenlimit.

### Rules-Konformität

Der aktuelle scoped `get_violations`-Lauf mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` meldet genau eine
Violation:

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs:1` — `MaxLineCount`, 501 Zeilen bei maximal 500.

Damit verletzt der Commit die im Step-Plan referenzierte C#-Qualitätsregel
und lässt den Dogfood-/Safeguard-Nachweis rot werden. Die Violation betrifft
Testcode, nicht eine neue Produktionsänderung. Die bestehenden
`MaxDirectoryChildren`- und `AIContextFootprint`-Befunde im Safeguard liegen
außerhalb des Step-Scopes und sind kein neues Finding dieses Reviews.

Die statischen MCP-Semantikabfragen für Acquirer, Cache-Reuse, Reader-Port,
die drei Testmethoden sowie References, Testkontext und Impact wurden mit dem
absoluten Projektroot ausgeführt. Für den geänderten Testkörper bestätigen die
MCP-Bodies die realen Assertions; die Textsuche blieb auf `rg` begrenzt.

### Logische Korrektheit

Der ursprüngliche Step-029-Fund zur fehlenden Publish-/Current-Beobachtung ist
im Testcode behoben:

- `ExternalSourceRepositoryCacheAcquirerTests.cs:102-135` baut eine
  Generation mit `cachePublisher.PublishAsync(source.Request)` auf, prüft den
  Erfolg, liest Current über einen separaten `cacheReader`, injiziert einen
  `RecordingCacheWriter`, prüft `Request == null`, `transport.CallCount == 0`,
  den unveränderten Generation-Namen, den getrennten Checkout und den Cleanup.
- `ExternalSourceRepositoryCacheAcquirerTests.cs:138-167` prüft denselben
  Generation-/Lease-Vertrag für den direkten `CacheReuse`-Aufruf.
- `ExternalSourceRepositoryCacheAcquirerTests.cs:377-415` prüft vier
  unabhängige parallele Checkouts, vier Marker, `Request == null`,
  `CallCount == 0`, identischen Current-Namen vor/nach den Hits und nach dem
  Dispose aller Handles sowie den Erhalt der persistenten Generation.
- `ExternalSourceRepositoryCacheAcquirerTests.cs:443-455` kapselt nur die
  direkte Current- und Request-Ownership-Prüfung; der Reader-Port wird laut
  MCP-Referenzen produktiv in `CacheReuse` und in den Tests verwendet.

Die neue Testdatei-Überlänge ist kein Cache-Logikfehler, aber ein realer
Abnahmefehler: Der vollständige Integration-Lauf scheitert in
`CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess` an
`MaxLineCount`; zusätzlich scheitert
`McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults` am Safeguard-
Korridor mit einem im Lauf ausgegebenen Score von 2,652253349573691 unter
5,0. Beide Fehler sind mit dem aktuellen Commit reproduziert.

### Konzept-Treue (Ebene 4)

Die Testkorrektur bleibt dem Konzept treu: Die persistente Generation bleibt
cache-eigen, der Reuse erzeugt eine getrennte request-owned Lease, Current und
Generation werden nicht verändert, und es gibt keine neue Source-of-Truth-,
Refresh-, Fetch-, Health-, Host- oder Cross-Process-Semantik. Es wurden keine
Non-Goals umgesetzt; die Abweichung liegt ausschließlich in der fehlenden
Regel-/Gate-Erfüllung und im veralteten Nachweistext.

### Build-/Test-Status

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests" → grün (34 Tests bestanden, 1 Skip, 35 gesamt, 0 Fehler)
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (2060 Tests bestanden, 2 Skips, 2062 gesamt, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → rot (368 Tests bestanden, 0 Skips, 2 Fehler, 370 gesamt)
```

Die beiden echten Reparse-/Symlink-Skips sind:

- `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
- `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`

Beide wurden ausschließlich wegen `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314
beim Erzeugen eines echten Symlinks übersprungen. Es wurde kein Fake-Reparse
verwendet und keine Assertion abgeschwächt. Stress wurde nicht ausgeführt.

## Findings

1. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs:1` — **[CRITICAL] [Rules/Plan]** Die geänderte Testdatei hat 501 Zeilen und verletzt damit `MaxLineCount` (Limit 500). Das macht den vollständigen Integration-/Dogfood-Nachweis rot: Der CLI-Dogfood-Test meldet die konkrete Violation, und der Safeguard-Test fällt mit 2,652253349573691 unter den Korridor 5,0. **Fix:** Die neuen Reuse-Assertions unverändert erhalten, die Testdatei innerhalb des bestehenden Test-Scope durch eine mechanische Verkürzung oder eine thematisch passende Testaufteilung wieder auf höchstens 500 vom Linter gezählte Zeilen bringen; anschließend den Fokuslauf, Build, beide Nicht-Stress-Gates und den scoped Violation-/Safeguard-Nachweis erneut ausführen.
2. `tasks/decompiled-assembly-analysis/step-029/step-result.md:119-157` und `tasks/decompiled-assembly-analysis/step-030/step-result.md:86-124` — **[MAJOR] [Plan]** Die Result-Dokumentation ist für den geprüften Commit nicht aktuell reproduzierbar. Der aktuelle Integration-Lauf ist 368/0/2/370 statt 370/0/0/370; `get_violations(scopeFilter="ExternalSourceRepository")` meldet 1 `MaxLineCount`-Violation statt 0; `safeguard(scopeFilter="ExternalSourceRepository")` meldet 2,79/10 statt 5,79/10. Der Fokuslauf 34/1/35, der Fast-Lauf 2060/2/2062, die beiden 1314-Skips und die scoped Duplicate-/MagicValues-/DeadCode-Ausgaben wurden dagegen reproduziert. **Fix:** Im selben gebündelten Korrektur-Scope zunächst den Testdatei-Limitfehler beheben, danach beide Result-Dateien ausschließlich mit den tatsächlich erneut ausgeführten Zahlen, Skipnamen, Audit-Scope und Auditwerten aktualisieren; keinen grünen Integration-Lauf behaupten, solange er nicht erneut grün ist.

Die beiden ursprünglichen Step-029-MAJOR-Findings sind damit unterschiedlich
zu bewerten: Das Finding zur konkreten Publish-/Current-Beobachtung ist
behoben; das Finding zur belastbaren Result-/Auditdokumentation bleibt wegen
der aktuellen Abweichungen offen. Die zwei oben genannten Findings gehören zu
einem einzigen gebündelten Korrekturpaket und sollen nicht in Mini-Steps
aufgeteilt werden.

## MCP-/DRY-/MagicValues-/DeadCode-Befunde

- **MCP:** `get_feature_context`/`get_symbol_body` für Acquirer, Cache-Reuse,
  Reader-Port und die drei Reuse-Tests sowie `find_references`,
  `get_test_context` und symbolischer `get_impact` wurden mit
  `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ausgeführt. Acquirer-
  Kontext und Testzuordnung zeigen 38 statisch zugeordnete Tests in neun
  Dateien; der Reader-Port hat drei direkte Referenzen einschließlich des
  produktiven Reuse-Aufrufs. Ein Commit-`get_impact`-Aufruf lieferte keinen
  verwertbaren Git-Diff; die konkreten Symbol-/Body-/Referenzabfragen waren
  verfügbar und maßgeblich für die Prüfung.
- **Violations/Safeguard:** `get_violations(scopeFilter="ExternalSourceRepository")`
  meldete 1 `MaxLineCount`-Violation in der geänderten Testdatei.
  `safeguard(scopeFilter="ExternalSourceRepository")` meldete 2,79/10 bei
  Threshold 8,00 und vier Befunden: den neuen `MaxLineCount`-Befund sowie die
  drei bestehenden Directory-/Footprint-Befunde außerhalb der Reuse-Logik.
- **DRY/Refactoring-Drift:** Die beiden erlaubten scoped
  `find_duplicates(mode="clone", minTokens=20, similarityThreshold="near")`-
  Läufe meldeten 0 Cluster bei 350 Produktionsmethoden und 0 Cluster bei 124
  Testmethoden. Es wurde kein solutionweiter Sweep gestartet und kein neuer
  DRY-/Refactoring-Drift-Tech-Debt gefunden.
- **MagicValues:** Der Produktionsscope meldete 7 bestehende Werte in 7
  eindeutigen Einträgen über 16 Dateien. Der betroffene Testscope meldete 35
  Treffer in 34 eindeutigen Einträgen; sie sind Fixture-, Fall- und
  Diagnosetexte. Kein neuer produktiver Magic-Value-Fund wurde festgestellt.
- **DeadCode:** `find_dead_code(scopeFilter="ExternalSourceRepository",
  includeTests=true, mode="members")` meldete 0 unreferenzierte Symbole bei
  24 Dokumenten und 55 Symbolen. `tech-debt.md` erhält keinen neuen Eintrag;
  TD-001 bis TD-005 bleiben unverändert.

## Leak-/Scope-Bewertung

Die Commit-Grenze enthält exakt die geplante Testdatei und die beiden
Result-Dateien; Produktionscode, `task-state.md`, `roadmap.md`, `codemap.md`
und `tech-debt.md` wurden nicht geändert. Die Reuse-Tests verwenden
`TestTempDirectory`, getrennte Reader-/Publisher-Roots und request-owned
Checkout-Leases. Die direkten Assertions prüfen nach Dispose keine
`checkout-*`-Verzeichnisse, erhalten die persistente Generation und ändern
keinen Current-Pointer. Nach den Läufen waren keine `testhost`-/`vstest`- oder
neu gestarteten Test-`dotnet`-Prozesse sowie keine aktuellen Repository- oder
OS-Temp-Verzeichnisse mit den Testpräfixen vorhanden. Es wurden keine Sleeps,
Remote-/Git-Netzwerkaktionen, neuen AppContext-Cachegenerationen oder
Cross-Process-Ansprüche eingeführt.

## Folgeaktion

Einen einzigen gebündelten Korrektur-Step für die beiden Findings anlegen:
Testdatei-Limit ohne Abschwächung der Reuse-Assertions korrigieren, den
aktuellen Fokus-/Build-/Fast-/Integration-Lauf erneut ausführen und danach
`step-029/step-result.md` sowie den Step-030-Nachweis auf die realen Zahlen
und scoped Auditwerte bringen. Bis dahin kein `approved`; keine
Produktionsänderung und keine Änderung an `tech-debt.md`.
