---
status: done (pending audit)
type: step-plan
task: 04_repositoryweite-hybridsuche-und-kontextbudget
step: 002
corrects: step-001
title: "Step-001 Findings korrigieren"
epic: EPIC-01
estimated_risk: medium
step_type: single
items: []
created_by: orchestrator
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-21T13:02:00+02:00
related_to:
  - step-001/step-review.md
---

# Step 002: Step-001 Findings korrigieren

## Bezug

- **Task:** `04_repositoryweite-hybridsuche-und-kontextbudget`
- **Epic:** `EPIC-01` aus `roadmap.md`
- **Korrektur von:** `step-001` gemäß `step-001/step-review.md`

## Intention

Die drei eindeutigen MAJOR-Findings aus dem Review von Step-001 werden mechanisch behoben. Der bestehende Suchvertrag, die Legacy-Semantik und die übrige Step-001-Implementierung bleiben unverändert.

## Findings und konkrete Fix-Anweisungen

1. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs:189-192` — **[MAJOR] [Plan/Logik/Konzept]** Der neue Hauptscanner prüft vor dem Lesen nur `IsSearchExcludedRelativePath(...)`, `.min.*` und Include-/Exclude-Filter. Die bestehende Generated-Policy für Dateinamen wie `.g.cs` und `.AssemblyAttributes.cs` aus `SourceFileCatalog.IsGeneratedPath` wird nicht angewendet; eine solche Datei unterhalb eines normalen Source-Verzeichnisses wird daher als Treffer ausgeliefert. **Fix:** vor `TryReadLines` die gemeinsame/äquivalente Generated-Dateipolicy einschließlich der Dateinamenssuffixe anwenden und einen Regressionstest für einen generierten Dateinamen außerhalb von `obj`/`bin` ergänzen; die Completeness-/Legacy-Semantik dabei unverändert halten.

2. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternScanner.cs:77-89` und `src/AiNetLinter/Baseline/FileSystemExclusionHelpers.cs:29-47` — **[MAJOR] [Logik]** `SafeEnumerateFilesWithErrors` materialisiert die vollständige rekursive Enumeration, bevor der erste Cancellation-Check in `ScanFiles` erreicht wird; der `Task.Run`-Aufruf verwendet zusätzlich `CancellationToken.None`. Bei einer Cancellation während eines großen oder blockierten Repository-Walks wird deshalb weiterhin der gesamte Dateibaum enumeriert und erst danach ein abgebrochenes Ergebnis markiert. **Fix:** Cancellation in die Enumeration/Iteration durchreichen und zwischen den Enumerationseinheiten prüfen, sodass der Walk nach Cancellation zeitnah beendet, `scanCompleted=false` und `truncatedBy` weiterhin deterministisch gesetzt werden.

3. `src/AiNetLinter/Mcp/Tools/Analysis/SearchPatternLegacyFileHitScanner.cs:45-59` — **[MAJOR] [Rules]** Die neu angelegte Legacy-Route fängt `IOException`, `UnauthorizedAccessException` und `RegexMatchTimeoutException` ab und liefert lediglich `false`. Das verletzt `AiNetLinter.mdc#agent-resilience/EnforceNoSilentCatch` sowie die im Step-Plan referenzierte Regel „keine stillen Catch-Blöcke“; der bestehende `GetFilesWithHits`-Aufrufer erhält weder Logging noch einen sichtbaren Status und kann dadurch einen falschen „kein Nicht-C#-Treffer“-Hinweis erzeugen. **Fix:** den Fehlerpfad so umgestalten, dass die Legacy-Kompatibilität erhalten bleibt, aber Fehler mindestens über einen sichtbaren/auswertbaren Status oder projektkonformes Logging nachvollziehbar werden; Regex-Timeouts dürfen nicht still als „kein Treffer“ verschwinden.

## Tests

- [ ] Regressionstest für generierte Dateinamen außerhalb von `obj`/`bin` ergänzen.
- [ ] Cancellation während der Dateisystemenumeration prüfen.
- [ ] Legacy-Fehler-/Regex-Timeout-Pfad ohne stillen Catch prüfen.
- [ ] Betroffene schnelle und vollständige Nicht-Stress-Testläufe aus `roadmap.md` grün ausführen.

## Definition of Done

- [ ] Alle drei Review-Findings sind mechanisch behoben.
- [ ] Bestehender Step-001-Vertrag bleibt unverändert.
- [ ] Build und vorgeschriebene Nicht-Stress-Tests sind grün.
- [ ] `step-002/step-result.md` ist geschrieben und der Step-Plan auf `done (pending audit)` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#agent-resilience/EnforceNoSilentCatch`
- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests`

## Notes

Keine zusätzlichen Änderungen außerhalb der drei Findings aus `step-001/step-review.md`.
