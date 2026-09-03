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
