---
status: done
type: step-review
task: 11_epic-projektregistry-und-daemon
step: 003
epic: EPIC-A
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5
reviewed_by_model_knowledge_cutoff: nicht deklariert
reviewed_at: 2026-08-23T20:44:51+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 003: MCP-Wiring auf die Projektregistry

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — mindestens drei MAJOR-Findings; Korrektur-Step erforderlich (`corrects: step-003`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: beide finalen Commits per `git show` inhaltlich geprüft; die geplanten Wiring-/Migrationsbereiche und Testanker stichprobenartig gegen den aktuellen Stand abgeglichen.
- [x] Rules-Konformität: die im Plan referenzierten Rules-Refs sowie MCP-Quality-Gates geprüft.
- [x] Logische Korrektheit: die Kalt-Load-, Dedupe- und Resource-Lifetime-Pfade anhand der MCP-Symbolkörper geprüft.
- [x] Konzept-Treue: A.3, A.4, A.5, A.7 und die zugehörigen Review-/Self-Audit-Verträge gegen die Umsetzung geprüft.
- [x] Build: dokumentiertes Abschluss-Gate aus `step-result.md` verwendet, nicht wiederholt.
- [x] Tests: dokumentierte Nicht-Stress-Gates aus `step-result.md` verwendet, nicht wiederholt.

## Befund

### Plan-Erfüllung

Die meisten geplanten Änderungen sind in `ccf7b33a` und `790ce251` vorhanden: Registry-Wiring, harte MCP-Argumentgrenze, Flags, Definitionsdatei-Migration, Tool-Schemas, Health-/Reload-/Overview-Anker und die Dokuänderungen sind im Diff erkennbar. Die Kalt-Load-Abnahme, das exakt einmalige Load-Dedupe und der Lease-Schutz der Overview-Resource sind jedoch trotz vorhandener Tests nicht verlässlich erfüllt; die drei Abweichungen sind unten als MAJOR-Findings beschrieben.

### Rules-Konformität

Die planbezogenen Rules-Refs sind in der Stichprobe eingehalten: `get_violations` für `src/AiNetLinter` meldet 0 Verstöße, `safeguard` liefert 10,00/10, und die geprüften Produktionssymbole liegen innerhalb der Metrikbudgets; daraus ergibt sich kein separates Rules-Finding.

### Logische Korrektheit

Die Lease-Lifetime der Tool-Lambdas ist über `ProjectToolCall.ExecuteAsync` strukturell gehalten, aber der Produktionspfad für fehlgeschlagene Solution-Loads verliert die Ursprungsmeldung, die Registry kann bei parallelen Misses doppelte Instanzen erzeugen, und die Overview liest einen Snapshot ohne Lease während Eviction möglich bleibt.

### Konzept-Treue (Ebene 4)

Die Umsetzung weicht an drei entscheidenden Stellen von den verbindlichen A.4-/A.5-/A.7-Verträgen ab: `PROJECT_LOAD_FAILED` muss Ursprungsmeldung plus Restore-Hint tragen, parallele Erst-Calls müssen genau einen Load teilen, und jeder Tool-/Resource-Call einschließlich Overview muss leasegeschützt sein.

### Build-/Test-Status

Die in `step-result.md` dokumentierten Abschluss-Gates sind grün; sie wurden gemäß Effizienzvorgabe nicht erneut ausgeführt.

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1678 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (350 Tests, 0 Fehler)
dotnet run --project src/AiNetLinter -- --sync-agent-rules-only → grün
```

MCP-Stichproben: `get_violations` Scope `src/AiNetLinter` → 0, `safeguard` Scope `src/AiNetLinter` → 10,00/10; `get_feature_context` für `TryLoadSolutionAsync`, `ProjectRegistry.InsertResident`, `ProjectToolCall.ExecuteAsync` und `OverviewResourceRegistration.BuildTemplatedResult` bestätigte die unten beschriebenen Pfade.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Commands/McpServerCommand.cs:199-216`, `src/AiNetLinter/Mcp/McpCodeGraphServer.cs:159-174`, `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:145-163` — **[MAJOR] [Logik/Konzept-Treue]** Der produktive Kalt-Load-Delegat `TryLoadSolutionAsync` fängt den ursprünglichen Load-Fehler ab und liefert `null`; `LastLoadError` kann deshalb nur den Fehler eines faulted Tasks oder eines Refreshes liefern. Beim nächsten Hit entfernt `ProjectRegistry` den FAILED-Marker bereits und startet einen neuen Entry. Damit erhält der reale MCP-Pfad nicht zuverlässig `PROJECT_LOAD_FAILED` mit Ursprungsmeldung und Restore-Hint, wie A.7 und der Step-Plan verlangen; der vorhandene Contract-Test verwendet dagegen einen künstlich faultenden Server und deckt diesen Produktionspfad nicht ab. **Fix:** den ursprünglichen Fehler im Kalt-Load-Zustand erhalten (faulted Task oder expliziter Fehlerwert), ihn in `LastLoadError`/`LoadFailedResult` ausgeben und den FAILED-Marker erst nach der vorgeschriebenen `PROJECT_LOAD_FAILED`-Antwort für den Retry entfernen.

2. `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs:165-195` — **[MAJOR] [Logik/Konzept-Treue]** `InsertResident` ruft `options.InstanceFactory(definition)` vor dem Registry-Lock auf. Zwei parallele Misses desselben kanonisierten Keys können daher beide Server samt Hintergrund-Load erzeugen; erst danach wird eine Instanz als `retired` verworfen. Das verletzt den A.4-/Review-1-Vertrag „genau ein Load pro Root“ und der Dedupe-Test synchronisiert den Eintritt des zweiten Callers nicht belastbar. **Fix:** die Prüfung/Reservation und den nicht-blockierenden Factory-Kick-off so serialisieren, dass pro Key nur eine Instanz erzeugt wird, ohne den Registry-Lock über den eigentlichen Solution-Load zu halten; konkurrierende Aufrufer müssen dieselbe residente Instanz adoptieren.

3. `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs:87-107` — **[MAJOR] [Plan/Konzept-Treue]** `BuildTemplatedResult` validiert den Root und ruft `FindSnapshot`/`BuildResult` direkt auf, eröffnet aber keinen `ProjectLease` und hält dadurch `InFlightCount` während des Resource-Calls nicht > 0. Eine TTL-/LRU-Eviction kann den referenzierten Server zwischen Snapshot und Rendering disposen. Das verfehlt die explizite Step-Intention, auch die Overview-Resource leasegeschützt an den Registry-Key zu binden. **Fix:** den Resource-Handler über einen Lease-Pfad führen und den Lease bis nach `BuildOverviewText`/Antwortaufbau halten; dabei Loading-, LoadFailed- und Root-Fehlerverträge konsistent mit den Tools behandeln.

