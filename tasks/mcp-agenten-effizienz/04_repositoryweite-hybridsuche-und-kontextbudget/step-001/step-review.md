---
status: done
type: step-review
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 001
epic: EPIC-01
step_type: single
reviewed_by: kritiker
reviewed_by_model: GPT-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-21
verdict: issues
tech_debt_ids: []
---

# Review Step 001: Strukturierte repositoryweite Suche mit Legacy-Kompatibilität und Kontextbudget

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step erforderlich (`corrects: step-001`)
- [ ] **blocked** — Nutzer-Entscheidung nötig

## Geprüft

- [x] Plan-Erfüllung: Commit-Diff `a166eb38`, aktuelle Arbeitskopie, Step-Result und CodeMap geprüft
- [x] Rules-Konformität: referenzierte `.agents/rules/**` geprüft; projektinterne Violations-Abfrage für den SearchPattern-Scope war grün
- [x] Logische Korrektheit: Structured-Content-Vertrag, Legacy-Pfad, MatchRanges/Spalten, Scope/Filter, Budgets, Completeness, Cancellation und Datei-I/O geprüft
- [x] Konzept-Treue: Scope, Non-Goals und spätere Roslyn-/Dokumentations-/`rg`-Grenzen geprüft
- [x] Build: selbst nachgeprüft, grün
- [x] Tests: fokussierte Scanner-, Tool-, Contract- und Raw-Wire-Tests selbst nachgeprüft, grün; der im Step-Result dokumentierte vollständige Nicht-Stress-Gate wurde nicht erneut gestartet

## Befund

### Plan-Erfüllung

Die geplanten Scanner-, Record-, Formatter-, Tool-, Registrierung-, Fixture- und Teständerungen sind im Commit vorhanden; Legacy-Text, Structured Content als Top-Level-Objekt, `GetFilesWithHits`, Budgets, Filter und CodeMap sind abgedeckt. Die im aktuellen HEAD nachgezogenen Step-Dokumentationsänderungen enthalten Commit-Referenz und Result-Status. Die Abnahme ist dennoch nicht vollständig erfüllt, weil die generierte-Datei-Policy nicht vollständig in den neuen Repository-Scan übernommen wurde und Cancellation während der Dateisystemenumeration nicht rechtzeitig wirksam wird.

### Rules-Konformität

Die referenzierten Architektur-, Windows-, Test-, DRY-, Nullable-, Größen- und Naming-Regeln sind überwiegend eingehalten; `dotnet build` blieb warnungsfrei und die projektinterne Violations-Abfrage meldete 0 Verstöße im SearchPattern-Scope. In der neu angelegten Legacy-Hilfsklasse verletzen die Catch-Blöcke für `IOException`, `UnauthorizedAccessException` und `RegexMatchTimeoutException` jedoch `EnforceNoSilentCatch`: sie geben ausschließlich `false` zurück, ohne Logging oder einen für den Aufrufer sichtbaren Fehler-/Statuspfad.

### Logische Korrektheit

Der Structured-Content-Vertrag ist als Objekt mit deterministischen MatchRanges, unverändertem `lineText`, Kontext, Scope/Snapshot und getrennten sichtbaren/gesamten Zählern umgesetzt; Plain-/Regex-Matches, `maxResults`, `maxFiles`, `maxResponseBytes`, Regex-Timeouts, Binär-/UTF-8-/unlesbare Dateien sowie der unbudgetierte Legacy-Miss-Hint-Pfad sind nachvollziehbar. Die drei Findings unten bleiben offen: generierte Dateinamen werden nicht ausgeschlossen, Cancellation wird erst nach vollständiger Enumeration geprüft, und die neue Legacy-Hilfsklasse verschluckt erwartbare Datei-/Regex-Fehler regelwidrig.

### Konzept-Treue (Ebene 4)

Die Non-Goals werden eingehalten: kein RAG, kein LLM-Ranking, kein Semantic Kernel, keine Produktionsabhängigkeit von `rg`, kein Cursor-/Session-State und kein vorgezogener Roslyn-Enrichment- oder umfassender Dokumentationsscope. Die Must-haves für generische Scope-Sicherheit, strukturierte Ergebnisse und Budgets sind weitgehend getroffen; die Standardausschluss- und Cancellation-Anforderungen sind wegen der Findings noch nicht vollständig konzeptgetreu.

### Build-/Test-Status

`dotnet build --no-restore` → grün, 0 Warnungen, 0 Fehler.

`dotnet test src/AiNetLinter.FastTests --no-build --filter FullyQualifiedName~SearchPatternScannerTests` → grün (7 Tests, 0 Fehler).

`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~SearchPatternToolTests` → grün (17 Tests, 0 Fehler).

`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~McpServerCommandContractTests` → grün (14 Tests, 0 Fehler).

`dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~McpServerCommandJsonRpcFramingTests` → grün (7 Tests, 0 Fehler).

Der Coder dokumentiert zusätzlich einen grünen Abschluss-Gate-Lauf mit 1553 FastTests und 336 IntegrationTests; dieser wurde für den Review nicht erneut ausgeführt.

## Findings

1. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs:189-192` — **[MAJOR] [Plan/Logik/Konzept]** Der neue Hauptscanner prüft vor dem Lesen nur `IsSearchExcludedRelativePath(...)`, `.min.*` und Include-/Exclude-Filter. Die bestehende Generated-Policy für Dateinamen wie `.g.cs` und `.AssemblyAttributes.cs` aus `SourceFileCatalog.IsGeneratedPath` wird nicht angewendet; eine solche Datei unterhalb eines normalen Source-Verzeichnisses wird daher als Treffer ausgeliefert. **Fix:** vor `TryReadLines` die gemeinsame/äquivalente Generated-Dateipolicy einschließlich der Dateinamenssuffixe anwenden und einen Regressionstest für einen generierten Dateinamen außerhalb von `obj`/`bin` ergänzen; die Completeness-/Legacy-Semantik dabei unverändert halten.

2. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs:77-89` und `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs:29-47` — **[MAJOR] [Logik]** `SafeEnumerateFilesWithErrors` materialisiert die vollständige rekursive Enumeration, bevor der erste Cancellation-Check in `ScanFiles` erreicht wird; der `Task.Run`-Aufruf verwendet zusätzlich `CancellationToken.None`. Bei einer Cancellation während eines großen oder blockierten Repository-Walks wird deshalb weiterhin der gesamte Dateibaum enumeriert und erst danach ein abgebrochenes Ergebnis markiert. **Fix:** Cancellation in die Enumeration/Iteration durchreichen und zwischen den Enumerationseinheiten prüfen, sodass der Walk nach Cancellation zeitnah beendet, `scanCompleted=false` und `truncatedBy` weiterhin deterministisch gesetzt werden.

3. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFileHitScanner.cs:45-59` — **[MAJOR] [Rules]** Die neu angelegte Legacy-Route fängt `IOException`, `UnauthorizedAccessException` und `RegexMatchTimeoutException` ab und liefert lediglich `false`. Das verletzt `AiNetLinter.mdc#agent-resilience/EnforceNoSilentCatch` sowie die im Step-Plan referenzierte Regel „keine stillen Catch-Blöcke“; der bestehende `GetFilesWithHits`-Aufrufer erhält weder Logging noch einen sichtbaren Status und kann dadurch einen falschen „kein Nicht-C#-Treffer“-Hinweis erzeugen. **Fix:** den Fehlerpfad so umgestalten, dass die Legacy-Kompatibilität erhalten bleibt, aber Fehler mindestens über einen sichtbaren/auswertbaren Status oder projektkonformes Logging nachvollziehbar werden; Regex-Timeouts dürfen nicht still als „kein Treffer“ verschwinden.

