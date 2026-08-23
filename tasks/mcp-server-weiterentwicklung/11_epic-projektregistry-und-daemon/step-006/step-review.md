---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 006
epic: EPIC-A
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-23T23:53:27+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 006: Race-Interleavings in den Abnahmetests deterministisch verankern

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — mindestens ein MAJOR-Finding; Korrektur-Step erforderlich (`corrects: step-006`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: der Cold-Load-Test nutzt keinen künstlichen Zusatz-Lease und beide Tests verwenden deterministische lokale Barrieren; der Dedupe-Test weist den geforderten nicht publizierten Loser jedoch nicht nach und der Cold-Load-Test bindet die vollständige Originalmeldung nicht direkt.
- [x] Rules-Konformität: die referenzierten Regeln aus `.agents/rules/AiNetLinter.mdc` und `AiNetLinterRichtlinien.mdc` sind eingehalten; MCP-first-Symbolprüfung, async-/Barrier-Testmuster, Parallelität und test-only Seam wurden geprüft.
- [x] Logische Korrektheit: das Loading→Fault→Release-Interleaving ist deterministisch; der Lookup→Reservation-Anker lässt Other-Root-Aufrufe vor der Fortsetzung zu, deckt aber die Loser-Disposal-Abnahme nicht ab.
- [x] Konzept-Treue: die A.7-FAILED-Reihenfolge ist belastbar angelegt, A.8/A.4 verlangt beim Dedupe-Rennen aber zusätzlich den expliziten Nachweis des nicht publizierten Losers sowie der vollständigen Fehlernachricht.
- [x] Build: den grünen Abschlusslauf aus `step-006/step-result.md` verwendet, nicht wiederholt.
- [x] Tests: den dokumentierten grünen Nicht-Stress-Abschlusslauf aus `step-006/step-result.md` verwendet, nicht wiederholt; beide betroffenen Tests zusätzlich gezielt ausgeführt, je 1/1 grün.

## Befund

### Plan-Erfüllung

Der Cold-Load-Test gibt den Initial-Lease vor dem Loading-Aufruf frei und steuert den Fault über `BeforeLeaseRelease` so, dass die Loading-Antwort vor dem Fault und die Freigabe im relevanten Interleaving liegen. Der anschließende Fehleraufruf prüft `PROJECT_LOAD_FAILED`, Solution-Pfad, einen Retry-Hinweis und eine neue Instanz beim Folge-Lease.

Der Registry-Test erreicht den Lookup-Punkt vor der Reservation, lässt den zweiten Ziel-Caller publishen und wartet dessen Abschluss ebenso wie den Other-Root-Caller vor der Fortsetzung des ersten Callers ab. Er weist für den Ziel-Root eine Factory-/Load-Ausführung und gemeinsame Server-Identität nach, erzeugt bzw. identifiziert aber keinen nicht publizierten Loser und prüft daher dessen Disposal nicht.

Die Abschlussläufe und MCP-Gates sind im Step-Result dokumentiert; die Codemap enthält die neuen Seam-/Testanker-Pointer. Der Step ist deshalb trotz grüner Läufe nicht abnahmefähig.

### Rules-Konformität

Die MCP-first-Vorgabe wurde eingehalten: `find_symbol`, `get_feature_context`, `get_symbol_body`, `find_references`, `get_impact` und `metrics_lookup` wurden für Registry-, Seam- und Testpfade verwendet. `get_violations` meldet 0 Verstöße in allen drei geänderten Projekt-Scopes; `safeguard` liefert jeweils 10,00/10 bei Threshold 8,00. Die gezielten Metriken liegen innerhalb der Grenzwerte (`TryAdoptOrCreate` 33 LOC/CC 5/CogC 5, Registry-Test 55 LOC/CC 3/CogC 4, Cold-Load-Test 34 LOC/CC 1/CogC 0). Es gibt kein Rules-Finding.

Das lokale `ManualResetEventSlim` dient ausschließlich der deterministischen Testbarriere; die Prüfung ergab keine globale Testserialisierung und keinen Solution-Load unter dem Registry-Lock.

### Logische Korrektheit

Der Cold-Load-Harness hält nach dem Initial-Lease keinen künstlichen Zusatz-Lease offen. `BeforeLeaseRelease` löst den blockierten Load und wartet auf das veröffentlichte Fault-Ereignis, bevor die Loading-Lease-Freigabe fortgesetzt wird. Dadurch kann die korrigierte FAILED-Markierung den ersten Folgeaufruf von einem frischen Retry unterscheiden.

Die Assertion in `McpServerCommandContractTests.cs:72-78` leitet `originalMessage` jedoch aus einem Warnlog-Suffix nach `LastIndexOf(": ")` ab; `originalException.Message` wird nur auf Nichtleere geprüft. Ein Vertrag, der nur einen Suffix oder eine anders gebildete Fehlermeldung zurückgibt, könnte den Test passieren, obwohl die vollständige Ursprungsmeldung nicht im `PROJECT_LOAD_FAILED`-Result steckt.

Beim Registry-Test blockiert der erste Caller vor der Reservation. Der zweite Caller übernimmt danach die atomare Reservation/Lazy und publiziert die einzige Zielinstanz; folglich bleiben `factory.InstancesCreated == 1` und `factory.ServersDisposed == 0` bis zur Registry-Disposal erwartbar. Die abschließende Disposal-Zählung von 1 belegt nur die residente Gewinnerinstanz, nicht die exakt einmalige Disposal eines nicht publizierten Verlierers. Der Test verankert damit die neue Single-Flight-Struktur, aber nicht den im Finding geforderten Loser-Pfad bzw. die Regression gegen die frühere getrennte Lookup→Reservation-Struktur.

### Konzept-Treue (Ebene 4)

Das Loading→Fault→Release-Verhalten entspricht dem FAILED-Marker-/Retry-Vertrag aus A.7; der Nachweis der Originalmeldung ist aber nicht direkt an die vom Harness eingefangene Ursprungsmeldung gekoppelt. Der Dedupe-Test erfüllt Other-Root-Servicefähigkeit und Reference-Identity, lässt jedoch den in A.8 ausdrücklich verlangten Nachweis „Factory/Load/Dispose des nicht publizierten Losers exakt einmal“ offen. Damit fehlen zwei abnahmerelevante Assertions im Step-Scope.

### Build-/Test-Status

Die dokumentierten Abschlussgates aus `step-006/step-result.md` wurden verwendet:

```text
dotnet build → grün (0 Warnungen, 0 Fehler; Nachweis aus step-result.md)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1681 Tests, 0 Fehler; Nachweis aus step-result.md)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (351 Tests, 0 Fehler; Nachweis aus step-result.md)
```

Gezielte Stichproben:

```text
dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner → grün (1 Test, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~ProductionColdLoad_BrokenSlnx_ReturnsOriginalLoadFailedContract → grün (1 Test, 0 Fehler)
```

## MCP-Quality-Gates

- `get_violations`: 0 Verstöße in `src/AiNetLinter`, `src/AiNetLinter.FastTests` und `src/AiNetLinter.IntegrationTests`.
- `safeguard`: 10,00/10 in allen drei Scopes bei Threshold 8,00, jeweils PASS.
- `metrics_lookup`: geänderte Produktions-/Test-Symbole innerhalb der dokumentierten LOC-, Komplexitäts- und Parametergrenzen.
- `get_feature_context`, `get_symbol_body`, `find_references` und `get_impact`: Registry-Reservation, test-only Seam, Lease-Freigabe und beide Testanker semantisch geprüft; die aktuelle Ziel-Reservation teilt eine Lazy und publiziert eine gemeinsame Instanz.

## Findings (nur bei `issues`)

1. `src/AiNetLinter.FastTests/Mcp/Projects/ProjectRegistryTests.cs:160-196` — **[MAJOR] [Plan/Logik/Konzept-Treue]** `Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner` pausiert zwar den ersten Caller vor der Reservation, lässt den zweiten Caller unter der korrigierten Step-005-Logik aber dieselbe Reservation/Lazy übernehmen. Dadurch entsteht im getesteten Pfad nur eine Zielinstanz; `factory.ServersDisposed == 0` vor und `== 1` nach `registry.DisposeAsync()` belegt nur die Disposal des publizierten Gewinners. Es wird kein nicht publizierter Loser erzeugt oder identifiziert, keine Loser-Disposal exakt einmal geprüft und die frühere getrennte Lookup→Reservation-Struktur kann so nicht regressionsstark widerlegt werden. **Fix:** Den kontrollierten Race-Harness so erweitern, dass der konkurrierende Publish und die Fortsetzung des ersten Callers den nicht publizierten Server eindeutig identifizieren; Factory-/Load-/Dispose-Zähler müssen die alte Mehrfach-Creation samt Loser und den korrigierten Einmal-Pfad unterscheiden, die gemeinsame residente Instanz sowie genau eine Disposal des Losers prüfen und den Other-Root-Call weiterhin vor dem Release der Barriere abschließen lassen. Die Prüfung darf keine globale Testserialisierung oder ein Warten auf `LoadTask` einführen.
2. `src/AiNetLinter.IntegrationTests/Mcp/McpServerCommandContractTests.cs:72-78` — **[MAJOR] [Plan/Logik/Konzept-Treue]** Der Test prüft die Originalmeldung nicht gegen `originalException.Message`, sondern extrahiert ein potenziell verkürztes Warnlog-Suffix über `LastIndexOf(": ")`; `originalException.Message` wird nur mit `NotEmpty` abgesichert. Ein `PROJECT_LOAD_FAILED`-Result mit lediglich diesem Suffix könnte dadurch grün bleiben, obwohl die vertraglich geforderte vollständige Ursprungsmeldung fehlt. **Fix:** Die Warnung separat als Warnung prüfen und im Fehlerresult direkt `Assert.Contains(originalException.Message, failedText, StringComparison.Ordinal)` (oder eine äquivalente exakte Vertragsassertion) verwenden; den vollständigen Restore-/Retry-Hint ebenfalls an einer stabilen, vertraglich relevanten Zeichenfolge festmachen.
