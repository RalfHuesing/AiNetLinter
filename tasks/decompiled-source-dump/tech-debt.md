# Task-lokale Tech-Debt-Queue

## TD-001

- Schweregrad: P2
- Ursache: Cache-Hit verliert den persistierten Projektdateipfad.
- Scope/Fundstelle: `AssemblyAnalysisSession.cs:163`, `AssemblyRoslynWorkspaceFactory.cs:116`.
- Evidenz: `RefreshGenerationAsync` übergibt `cached.ProjectFilePath` nicht; die Workspace-Fabrik kann dadurch einen synthetischen, nicht zwingend existierenden `.csproj`-Pfad erzeugen.
- Disposition: `fixed`
- Nächster Schritt: Projektpfad des Cache-Hits bis zur Workspace-Erzeugung erhalten und Regressionstest ergänzen.
- Log-Anker: `execution-log.md`, Epic 1 Reviewer, P2-/P3-Triage.
- attempts: 0

## TD-002

- Schweregrad: P2
- Ursache: Fehlende Regressionsevidenz für negative Cache-/Staging-/Lock-Pfade.
- Scope/Fundstelle: `AssemblyDecompilationAdapter`, `AssemblyDecompilationCache.PointerPublishing`, Cache-/Session-Tests.
- Evidenz: Es fehlen Tests für Timeout-/Decompiler-Abbruch ohne Cache-Publish, Pointer-Fehler unter Dateisperre und Projektpfad-Erhaltung beim Cache-Hit.
- Disposition: `fixed`
- Nächster Schritt: gezielte Regressionstests nach Abschluss der P1-Korrekturen ergänzen.
- Log-Anker: `execution-log.md`, Epic 1 Reviewer, P2-/P3-Triage.
- attempts: 0

Die im Implementiererbericht genannten bestehenden Near-Duplicate-/Low-Confidence-Dead-Code-Hinweise sind nicht durch Epic 1 eingeführt und bleiben außerhalb dieser task-scope Queue.

## TD-003

- Schweregrad: P3
- Ursache: Unvollständiges Cleanup nach Entfernung der alten Body-Auflösung.
- Scope/Fundstelle: `GetSymbolBodyTool.cs:117,188-196`, `AssemblyReferenceResolver.cs:367`.
- Evidenz: `RenderSingleSymbolRequest.Lease` wurde übergeben, aber nicht gelesen; `FailedResolution` erhielt einen ungenutzten `canonicalPath`.
- Disposition: `fixed`
- Nächster Schritt: Abgeschlossen im kritischen Review. Parameter-/Request-Verträge bereinigt und mit 0 Warnungen kompiliert.
- Log-Anker: `execution-log.md`, Epic 2 Reviewer, P3 `BODY-RESOLVER-CLEANUP-RESIDUE`.
- attempts: 0

## TD-004

- Schweregrad: P2
- Ursache: FastTests-Ausführung blockierte durch verwaiste Testprozesse mit DLL-Dateisperren und MaxLineCount-Verletzung.
- Scope/Fundstelle: `AssemblyAnalysisSessionTests.cs:1` (531 Zeilen > 500), Hintergrund-Prozesse `testhost`/`AiNetLinter.FastTests`.
- Evidenz: Vorherige abgebrochene Testläufe hinterließen Test-Hosts, die `AiNetLinter.dll` blockierten; MSBuild wiederholte Kopierversuche in Endlosschleifen. `AssemblyAnalysisSessionTests` überschritt zudem das Limit von 500 Zeilen.
- Disposition: `fixed`
- Nächster Schritt: Prozess-Tree bereinigt; Testklasse in Partial-Klassen `AssemblyAnalysisSessionTests.cs` (342 Zeilen) und `AssemblyAnalysisSessionTests.Resilience.cs` (217 Zeilen) aufgeteilt. Volllauf grün (2.444 passed, 2 skipped, 0 failed in 1m 38s).
- Log-Anker: `execution-log.md`, Kritisches Review.
- attempts: 0
