---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 026
epic: EPIC-04
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-29T17:11:00+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 026: Persistente Repository-Cache-Generation atomar veröffentlichen

## Verdict

- [ ] **approved** — nicht freigabefähig; die revidierten Write-through-Invarianten sind nicht vollständig erfüllt
- [x] **issues** — ein gebündelter Korrekturscope für Writer-/Reader-Integrität und Testisolation ist erforderlich
- [ ] **blocked** — keine Nutzerentscheidung erforderlich

Der geprüfte Commit `da9882f4657e7e363d6563a2772e76ca3b8d4237` ist gegen den
revidierten Step-026-Vertrag nicht approved. Die Kernidee ist erkennbar korrekt
und die normalen Erfolgsfälle sind grün, aber die Fehlerpfade bei konkurrierender
Cancellation sowie die fail-closed-/bounded-Anforderungen des Read-back sind nicht
vollständig erfüllt. Zusätzlich hinterlassen bestehende Acquirer-Tests durch den
neuen Default-Writer persistente Source-Cache-Generationen außerhalb ihres
Test-Temp-Verzeichnisses.

Der Korrekturscope soll als ein zusammenhängender Korrektur-Step geplant werden;
dieser Review-Auftrag erlaubt ausschließlich die Review-Dokumentation und legt
deshalb keinen neuen Korrektur-Step und keine Produktionsänderung an.

## Geprüft

- [x] Plan-Erfüllung: Key, Manifest-/Generation-Writer, Pointer-Publish und Acquirer-Hook sind vorhanden; die unten genannten Randbedingungen und Testbelege fehlen bzw. sind fehlerhaft.
- [x] Rules-Konformität: keine AiNetLinter-Violations im Cache-/Acquirer-Scope; Testisolation und reproduzierbare Belegführung sind jedoch nicht vollständig regelkonform.
- [x] Logische Korrektheit: Erfolgsfälle funktionieren, aber Cleanup nach publizierter Cancellation kann einen jüngeren Publish zurückrollen oder den Pointer löschen.
- [x] Konzept-Treue: der Commit bleibt im revidierten Write-through-Scope; die expliziten Read-back- und Same-Key-Invarianten werden an den Findings verletzt.
- [x] Build: selbst nachgeprüft, grün.
- [x] Tests: beide vollständigen Nicht-Stress-Suiten selbst nachgeprüft, grün; ein transparenter Win32-1314-Skip bleibt bestehen, die Cache-Leakage und die fehlenden Race-/Truncation-Tests verhindern jedoch die Freigabe.

## Befund

### Plan-Erfüllung

- **Credentialfreier deterministischer Key — erfüllt:** URL-Normalisierung, sicherer repository-relativer SolutionPath und eigene Schema-Version werden in den stabilen Hash eingebracht; physische Entry-Pfade verwenden nur den sicheren Key.
- **Manifest — teilweise erfüllt:** kanonische URL, SolutionPath, geladene Revision, Generation, UTC-Zeitstempel sowie bounded Inventar mit Länge und Inhaltshash werden geschrieben und gegen den aktuellen Content geprüft. Der Read-back akzeptiert aber ein gemeinsam verkürztes Manifest/Content-Set und liest bounded Metadaten nicht race-sicher bounded; siehe Finding 2.
- **Generation-Writer — teilweise erfüllt:** Generationen werden isoliert geschrieben, Ownership-Marker werden nicht kopiert und temporäre Pointer werden im normalen Fehlerpfad entfernt. Die Rollback-/Generation-Bereinigung läuft jedoch nach Freigabe des Same-Key-Locks; siehe Finding 1.
- **Atomic current-Publish — teilweise erfüllt:** temporärer Pointer und Replace/Move liegen im gleichen Entry-Verzeichnis und schützen den normalen Einzelprozess-Erfolgsfall. Die anschließende Fehlerbehandlung kann wegen Finding 1 einen konkurrierenden, bereits erfolgreichen Publish überschreiben; eine Cross-Process-Garantie wird im Code nicht behauptet.
- **Acquirer Write-through — erfüllt:** der Writer wird erst nach Clone-/Checkout-Prüfung aufgerufen, erhält denselben Handle ohne Ownership-Transfer, und ein Cache-Fehler bleibt als typed, geheimnisfreie Diagnose am gültigen Acquirer-Erfolg, sofern nicht die äußere Cancellation-Invariante greift.
- **Scope — erfüllt:** keine Refresh-/Reuse-/Config-/Host-/Snapshot-/Registry-/Assembly-Cache-Erweiterung und kein globaler Sweep wurden als Produktionsänderung eingeführt.
- **Testbeleg — teilweise erfüllt:** Key, Hash-/Inventar-Tampering, Pointer-Fehler, Vorzustand, Same-Key-Erfolg und echter Reparse-Fall sind abgedeckt; der Cancellation-after-pointer-Race und die selbstreferenzielle Inventar-Verkürzung sind nicht abgedeckt. Die bestehenden Acquirer-Tests isolieren den neuen Default-Writer nicht.

### Rules-Konformität

Die semantischen MCP-Prüfungen meldeten für `ExternalSourceRepositoryCache` und
`ExternalSourceRepositoryAcquirer` keine Violations. Die bestehenden
PathGuard-/Root-/Ownership-/Reparse-/Cleanup-/Cancellation-/Credential-/HTTP-/
Git-/Process-/Native-Invarianten sowie Step-024/025-Snapshot-/Registry-Ownership
bleiben im Code und in den Nicht-Stress-Suiten unverändert beobachtbar.

Die Testregel „lokal/deterministisch und ohne Temp-/Cache-Leaks“ ist dagegen
verletzt: `ExternalSourceRepositoryAcquirer` erzeugt bei fehlender Injection in
`src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:35` einen
Produktions-Writer unter `AppContext.BaseDirectory/cache/source`; mehrere
bestehende FastTests verwenden genau diesen Default. Nach dem vollständigen
FastTest-Lauf waren dort ein Key-Verzeichnis und vier Generationen vorhanden.

### Logische Korrektheit

Der Commit validiert jede im Manifest aufgeführte Datei unabhängig über Länge und
Content-Hash und prüft Pointer, Generation, Key, Revision, SolutionPath und
Reparse-Zustand fail-closed. Das reicht für einzelne Inhaltsänderungen, nicht aber
für die in Finding 2 beschriebene gemeinsame Kürzung von Manifest und Content.

Der kritische Lebensdauerfehler liegt in der Reihenfolge von Lock-Freigabe und
Rollback: Nach einem Pointer-Publish löst eine Cancellation die äußere
`finally`-Behandlung aus. Der Cache-Key-Lock wird in
`ExternalSourceRepositoryCacheWriter.cs:85` freigegeben, bevor
`RestorePreviousCurrent` und `TryDeleteGeneration` in den Zeilen 88–97 laufen.
Ein zweiter Publish desselben Keys kann dazwischen erfolgreich `current` ersetzen;
der erste Publish kann danach den alten Pointer wiederherstellen oder bei keinem
Vorzustand den aktuellen Pointer löschen.

### Konzept-Treue (Ebene 4)

Der Scope bleibt auf den revidierten Write-through-Generation-Publish-Vertrag
begrenzt und behauptet keine Cross-Process-Synchronisation. Nicht erfüllt sind
aber zwei ausdrücklich geforderte Konzeptinvarianten: vollständiges, nicht nur
vom selbst veränderbaren Manifest bestimmtes Read-back-Inventar sowie unveränderter
`current` bei Fehler/Cancellation auch unter bounded Same-Key-Konkurrenz.

### Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheWriterTests|FullyQualifiedName~ExternalSourceRepositoryCacheAcquirerTests" --no-restore → grün (12 passed, 0 failed, 1 skipped)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore → grün (2013 passed, 0 failed, 2 skipped)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore → grün (370 passed, 0 failed, 0 skipped)
```

Der fokussierte Lauf hatte genau einen bekannten Skip:
`ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`
wegen Win32 `ERROR_PRIVILEGE_NOT_HELD` / 1314 beim echten Symlink. Im vollständigen
FastTest-Lauf kam ausschließlich der bereits bekannte analoge Skip aus
`ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
hinzu. Es wurde kein Stress-Test ausgeführt. Die `step-result.md`-Angabe von
45 passed/2 skipped/47 total ist mit dem hier ausgeführten exakten fokussierten
Filter nicht reproduzierbar; sie ändert die Suite-Ergebnisse nicht, ist aber kein
belastbarer Beleg für den fokussierten Umfang.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheWriter.cs:85-97,141` — **[MAJOR] [Logik] Same-Key-Rollback läuft außerhalb des Locks.** Wenn Publish A nach `TryPublishPointer` (Zeile 132) an der Cancellation-Prüfung in Zeile 141 abgebrochen wird, gibt A in Zeile 85 den per-Prozess-Key-Lock frei. Publish B kann danach `current` erfolgreich auf seine Generation setzen; A führt anschließend `RestorePreviousCurrent` aus und stellt den alten Pointer wieder her oder löscht bei fehlendem Vorzustand den Pointer von B. Damit sind „previous current bleibt unverändert bei Fehler/Cancellation“ und der bounded Concurrent-Publish-Vertrag verletzt; ein erfolgreicher B-Result kann ohne gültigen `current` zurückbleiben. **Fix:** Den Cache-Key-Lock bis zum vollständigen Rollback und zur Generation-/Pointer-Bereinigung halten, oder eine gleichwertige serialized cleanup phase garantieren; alle Pointer-Mutationen müssen denselben Lock umfassen. Einen deterministischen Test mit Cancellation unmittelbar nach Pointer-Publish und gleichzeitigem zweitem Publish ergänzen, jeweils mit und ohne vorherigem `current`, und danach den B-Pointer, alle Generationen und die Cleanup-Ergebnisse prüfen. Cross-Process-Synchronisation darf dabei weiterhin nicht zugesichert werden.

2. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheReader.cs:352-380,410-417` — **[MAJOR] [Logik/Konzept] Read-back ist weder vollständig unabhängig verankert noch race-sicher bounded.** `ValidateInventory` beweist nur, dass der aktuelle Content exakt der im aktuellen Manifest selbst deklarierten Menge entspricht. Werden alle Content-Dateien entfernt und `files` im Manifest auf `[]` gesetzt, ist `actualPaths == expected` und der Read-back akzeptiert die leere Generation, obwohl insbesondere der erforderliche `SolutionPath` fehlt; das ist der geforderte Zirkelschluss durch Manifest und kein fail-closed vollständiges Inventar. Zusätzlich prüft `ReadBoundedText` zunächst `FileInfo.Length`, ruft danach aber unbounded `File.ReadAllText` auf; ein Wachstum zwischen beiden Operationen kann die JSON-Grenze umgehen und übergroße Daten allokieren/parsen. **Fix:** Den Read-back-Vertrag um eine unabhängig geprüfte Vollständigkeits-Ankerbedingung ergänzen (mindestens kanonische `SolutionPath` zwingend als real geprüfte Inventardatei; für gemeinsame Manifest-/Content-Manipulation eine separate, nicht aus dem veränderbaren `files`-Array abgeleitete Generation-/Inventar-Integritätsbindung vorsehen) und dafür einen Test mit verkürztem Manifest plus verkürztem Content hinzufügen. Pointer- und Manifest-JSON ausschließlich über einen strict-UTF-8-Stream mit harter Maximalgröße lesen, Wachstum während des Lesens ablehnen und keine `File.ReadAllText`-Allokation hinter einer vorgelagerten `FileInfo`-Prüfung verwenden.

3. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs:35; src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryAcquirerTests.cs:33,78,104,124,186,229,262,288,319,359,377,398,415,438; src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaExternalSourceProviderTests.cs:86,184` — **[MAJOR] [Plan] Bestehende Acquirer-Tests schreiben in einen persistenten Produktions-Cache und erfüllen die Leak-/Isolation-Invariante nicht.** Die genannten Tests injizieren keinen Writer; der Konstruktor erzeugt deshalb den Default-Writer mit `AppContext.BaseDirectory/cache/source`. Nach dem geprüften vollständigen Lauf lagen unter `src/AiNetLinter.FastTests/bin/Debug/net10.0/cache/source` ein deterministisches Key-Verzeichnis und vier Generationen außerhalb von `TestTempDirectory`; dieser Zustand wird nicht durch die Testfixtures entfernt und kann nachfolgende Testläufe beeinflussen. **Fix:** Die betroffenen Testfabriken und direkten Konstruktoraufrufe auf einen pro Test/Fixure in `TestTempDirectory` liegenden `LocalExternalSourceRepositoryCacheWriter` oder einen deterministischen Recording-Writer umstellen; zusätzlich einen Cleanup-/Leak-Assert für Source-Cache, Testhosts und Temp-Roots ausführen. Die gezielt erzeugten Source-Cache-Generationen sind kein Commit-Inhalt, aber ihr Auftreten ist ein reproduzierbarer Befund gegen den Testvertrag.

## Pointer-/Manifest-/Ownership-/Cleanup-Bewertung

- **Key:** erfüllt. URL und SolutionPath werden normalisiert, Credentials/Query/Fragment werden abgewiesen, die physische Entry-Komponente ist ausschließlich der eigene lowercase SHA-256-Key einschließlich Schema-Version.
- **Manifest:** teilweise erfüllt. Felder, Bounds, Pfadnormalisierung, Hash-/Längenprüfung, Revision, SolutionPath, Key und Reparse-Prüfung sind vorhanden; Finding 2 verhindert die geforderte vollständige fail-closed-Bewertung.
- **Pointer/Atomicity:** normaler lokaler Einzelpublish ist korrekt staged und atomar über temporären Pointer sowie Replace/Move im selben Verzeichnis; Finding 1 macht die Fehler-/Cancellation-Atomicity unter Same-Key-Konkurrenz unzureichend. Eine falsche Cross-Process-Garantie wird nicht behauptet.
- **Ownership:** erfüllt. Der Cache-Writer verwendet den Checkout und dessen Ownership-Information nur lesend; der Acquirer-Handle bleibt beim Acquirer/Caller und die veröffentlichte Generation enthält keinen Ownership-Marker.
- **Cleanup:** teilweise erfüllt. Temporäre Pointer werden im lokalen Pointer-Helfer bereinigt und Generationen werden best effort entfernt; die Bereinigung nach Pointer-Cancellation ist wegen der Lock-Reihenfolge nicht serialized und kann einen neuen `current` beschädigen.

## MCP-/DRY-/MagicValues-/DeadCode-Ergebnis

- **MCP:** `get_feature_context`, `get_symbol_body`, `find_symbol`, `find_references`, symbolbasierter `get_impact`, `get_violations` und `safeguard` wurden mit absolutem `projectRoot` verwendet. Der Commit-Branch von `get_impact` meldete trotz vorhandenem Repository einen leeren Diff; dies wurde als Low-Severity-Observability-Feedback protokolliert, die symbolbasierten Auswertungen waren verfügbar.
- **Violations/Safeguard:** Cache-/Acquirer-Scope: 0 Violations. `safeguard` bestand mit 6,10/10; die drei Top-Warnungen betreffen bekannte Directory-/Footprint-Grenzen außerhalb des Step-026-Scope.
- **DRY:** `find_duplicates` exact/near/structural auf Solution-, Production- und Test-Scope zeigte keinen neuen Duplikationscluster im Cache-/Acquirer-Code. Der einzige solutionweite exact cluster und die strukturellen Kandidaten sind bestehende, außerhalb dieses Steps liegende Helper-/Test-/Assembly-Muster; keine künstliche Vereinheitlichung mit `AssemblyDecompilationCache`, kein globaler Sweep und kein neuer Tech-Debt-Eintrag.
- **MagicValues:** im Cache-/Test-Scope 28 Vorkommen/27 eindeutige Werte, überwiegend zentrale Schema-, Pointer-, Bounds-, Diagnose- und Fixture-Konstanten; kein neuer Secret-Kandidat und kein in-scope Refactoring-Befund. Im Acquirer-Scope nur die bestehende Lokalisierungsdiagnose.
- **DeadCode:** Cache-Produktion 18 Symbole in 5 Dokumenten, Cache-Tests 22 Symbole in 7 Dokumenten, Acquirer 2 Symbole in 1 Dokument; jeweils 0 Dead-Code-Kandidaten.

## Sonstige Beobachtungen / MINOR / NITPICK

Die Commit-Änderung umfasst ausschließlich die erwartete Cache-/Acquirer-Fläche
plus den Step-Result-Bericht; es gibt keinen festgestellten Out-of-Scope-
Produktionsdrift und keinen neuen `tech-debt.md`-Eintrag.

## Tech-Debt-Einträge aus diesem Review

Keine. Die DRY-/MagicValues-/DeadCode-Funde sind entweder unauffällig oder
bereits bestehende Muster außerhalb des revidierten Step-026-Scopes.

## Folgeaktion

Einen einzelnen Korrektur-Step für die drei Findings anlegen: Writer-Lock und
Cancellation-Rollback serialisieren, Reader-Integrität und bounded Stream-Read
schließen sowie die Acquirer-Testfixtures cache-isoliert machen. Danach die
gezielten Regressionstests einschließlich der beiden deterministischen
Interleavings und des verkürzten Inventars ausführen und anschließend erneut
Build sowie beide vollständigen Nicht-Stress-Suiten prüfen.
