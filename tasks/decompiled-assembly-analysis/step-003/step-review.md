---
status: done
type: step-review
task: decompiled-assembly-analysis
step: 003
epic: EPIC-02
step_type: single
reviewed_by: kritiker
reviewed_by_model: gpt-5 (Codex)
reviewed_by_model_knowledge_cutoff: nicht angegeben
reviewed_at: 2026-08-28T13:42:46+02:00
verdict: issues
tech_debt_ids: []
---

# Review Step 003: Statische Assembly-Session mit Fingerprint, Decompilation und Roslyn-Snapshot

## Verdict

- [ ] **approved** — alle vier Prüfebenen ok
- [x] **issues** — Korrektur-Step `step-<MMM>` anlegen (`corrects: step-003`)
- [ ] **blocked** — Nutzer-Entscheidung nötig (siehe Frage unten)

## Geprüft

- [ ] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [ ] Rules-Konformität: referenzierte Rules eingehalten
- [ ] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [ ] Konzept-Treue: passt die Umsetzung zu `Konzept.md` (Scope, Non-Goals, Muss-Haben)
- [x] Build: selbst nachgeprüft, grün
- [ ] Tests: selbst nachgeprüft, nicht vollständig grün

## Befund

### Plan-Erfüllung

Die Paketgrenze, der statische Decompiler-Adapter, der Resolver, der synthetische Roslyn-Snapshot, Generationen und die Origin-Weitergabe sind im Code-Commit `0704b763` vorhanden; die CodeMap wurde im Doku-Commit `e6a39f08` aktualisiert. Der Plan ist jedoch nicht abnahmefähig erfüllt: Der vorgeschriebene Integration-Gate-Lauf scheitert mit zwei Tests, die Cache-/Manifest-Validierung und der harte Limitvertrag haben reproduzierbare Lücken, und die neue Context-Fabrik führt die tatsächliche Assembly-Identität nicht weiter. Die Produktdokumentation bleibt entsprechend der im Plan und in EPIC-06 vorgesehenen Scope-Grenze aufgeschoben und ist hier kein eigener Fund.

Die Testaussagekraft reicht für die positiven Standardpfade, deckt aber weder atomare Crash-/Race-Semantik noch beschädigte leere Partial-Manifeste, Version-Mismatch-Referenzen, die tatsächliche Assembly-Identität des synthetischen Compilations-Symbols noch einen Limit-Bypass über den Whole-Module-Fallback ab.

### Rules-Konformität

Der statische Pfad verwendet keinen `Assembly.Load`, keine `AssemblyLoadContext`-Instanz und keine Reflection-Ausführung; die Regeln zu statischer Analyse und zur Trennung der Assembly-Abstraktionen sind damit im Kern eingehalten. Es bestehen aber zwei produktive Verstöße gegen die im Plan referenzierte `AiNetLinter.mdc`: `AssemblyDecompilationManifest` überschreitet `MaxPublicMembersPerType` (20 statt 15), und der Catch in `AssemblyReferenceResolver` verwirft eine lokale Enumerierungs-Ausnahme ohne Log oder sichtbare Diagnose, obwohl `EnforceNoSilentCatch` genau das verbietet.

### Logische Korrektheit

Der normale Roslyn-/Toolpfad funktioniert für die getesteten DLLs, inklusive Filterung, Extension-Suche, Origin-Hinweis und fehlender Referenzen im positiven Partial-Fall. Die Cache-Publikation ist bei bestehendem Eintrag nicht crash-atomar, beschädigte/inhaltlich leere Partial-Manifeste können als aktuelle leere Snapshots adoptiert werden, und die Cache-Generation wird vor der vollständigen Workspace-Prüfung veröffentlicht. Außerdem können die Begrenzungen durch den Whole-Module-Fallback umgangen werden; der Resolver kennzeichnet Kandidaten vor erfolgreicher MetadataReference-Erzeugung als aufgelöst und akzeptiert den ersten gleichnamigen DLL-Kandidaten ohne Versionsprüfung. Die synthetische Compilation liefert zudem nicht automatisch die echte Assembly-Version an den bestehenden Inspect-Vertrag weiter.

### Konzept-Treue (Ebene 4)

Die bewusst deferreden Non-Goals — Registry-/Daemon-Lifecycle, transitive Sessions, Gitea-Mapping und Capability-Matrix — wurden nicht unzulässig vorgezogen. Die Umsetzung weicht aber bei den Muss-Haves „atomar und valide veröffentlichte Generation“, „sichtbare Fehler-/Partial-Grenzen“, „harte Decompilergrenzen“ und der tatsächlichen Assembly-Identität vom Konzept ab; insbesondere kann ein inkompatibler oder leerer Cacheeintrag einen scheinbar erfolgreichen, aber inhaltsleeren Roslyn-Stand liefern.

### Build-/Test-Status

```text
dotnet build → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress → grün (1865 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → ROT (358 erfolgreich, 2 Fehler, 360 gesamt)
```

Die zwei Integrationsfehler sind reproduzierbar im selben Lauf:

- `McpProcessArchitectureGuardTests.RunnerAndProcessCallsites_StayWithinMcpOwners` — `Expected: 3`, `Actual: 5`.
- `LoadedFixtureTests.SourceFileCatalogLoads_UseLoadedFixtureAsOnlyIntegrationTestCallsite` — ein generierter Pfad unter `bin/Debug/net10.0/cache/assembly/.../source/*.cs` erscheint in der erwarteten Quellliste.

## Findings (nur bei `issues`)

1. `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationCache.cs:26,140-152` und `src/AiNetLinter.IntegrationTests/Mcp/McpServerAllToolsE2ETests.cs:44-57` — **[CRITICAL] [Logik/Plan]** Der Standard-Cache schreibt bei einem Assembly-Tool-Aufruf dekompilierte `.cs`-Dateien unter `AppContext.BaseDirectory`; im In-Process-Integrationstest ist das `src/AiNetLinter.IntegrationTests/bin/Debug/net10.0`. Die bestehenden Architektur- und Quellkatalog-Gates enumerieren dieses Verzeichnis rekursiv und sehen dadurch generierte `Process.Start(`-/`SourceFileCatalog.LoadAsync(`-Quellen als Testquellen. Das erklärt beide Gate-Fehler und macht den behaupteten grünen Abschlusslauf falsch. **Fix:** den Assembly-Cachepfad im Testpfad deterministisch auf ein `TestTempDirectory`-Root injizieren oder die betreffenden Rohdatei-Scans zentral über den bestehenden `SourceFileCatalog.IsGeneratedPath`-/`bin`-Ausschluss führen; anschließend den vollständigen Integration-Gate-Lauf aus einem sauberen Cache wiederholen.

2. `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs:176-197` — **[MAJOR] [Rules]** `AssemblyDecompilationManifest` besitzt 20 öffentliche Properties und verletzt die referenzierte Regel `MaxPublicMembersPerType` mit Limit 15. Die Unschärfe im Coder-Result ist keine Ausnahme im Plan; es handelt sich um neu hinzugefügten Produktionscode. **Fix:** das Manifest-DTO so aufteilen oder kapseln, dass jeder Typ höchstens 15 öffentliche Member hat, ohne JSON-Felder zu verlieren; die vollständige Manifest-Serialisierung und den Linterlauf danach erneut prüfen.

3. `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationCache.cs:54-61,84-99,114-137` und `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSession.cs:203-229` — **[MAJOR] [Logik/Konzept]** Cache-/Manifest-Publikation erfüllt den Atomizitäts- und Integritätsvertrag nicht vollständig. Bei einem vorhandenen Eintrag wird zunächst `targetDirectory` nach `*.retired-*` verschoben und erst danach das temporäre Verzeichnis publiziert; ein Crash oder konkurrierender Writer kann damit ein sichtbares Loch bzw. einen fälschlichen Publish-Fehler erzeugen. `IsManifestCompatible` prüft nur Schlüssel/Status, adoptiert aber ein `partial`-Manifest ohne Dokumente oder mit beliebigen innerhalb des Entry-Roots liegenden Dateien als gültig; zusätzlich wird der Cache vor dem Workspace-Aufbau veröffentlicht. **Fix:** eine Windows-geeignete atomare Replace-/Pointer-Strategie mit Retry/Reread für konkurrierende Writer verwenden, Manifest und Dokumentliste vollständig validieren (nichtleere eindeutige `.cs`-Dateien unter `source/`, Existenz, Status-/Error-Konsistenz, Fingerprint-/Referenzdaten) und erst nach erfolgreicher Workspace-/Compilation-Validierung eine vollständige Generation als `complete` veröffentlichen; ungültige/abgebrochene Generationen dürfen nicht als aktueller Snapshot adoptiert werden.

4. `src/AiNetLinter/Mcp/Assemblies/AssemblyDecompilationAdapter.cs:137-164,170-183,193-195` — **[MAJOR] [Logik/Konzept]** Die behaupteten harten Typ-/Member-/Komplexitätsgrenzen sind nicht wirksam: gezählt werden nur Methoden und Felder der Top-Level-Typen, dekompiliert wird anschließend jedoch der vollständige Top-Level-Typ einschließlich verschachtelter Typen und aller Mitglieder. Überschreitet ein einzelner Typ das Limit, liefert `ApplyMemberLimit` eine leere Auswahl und `AddModuleDocumentIfRequired` dekompiliert stattdessen das gesamte Modul; die Whole-Module-Ausgabe prüft die Zeichenlänge erst nach dem vollständigen Decompiler-Aufruf. **Fix:** die Auswahl-/Decompilationseinheiten so begrenzen, dass kein Fallback die Limits umgehen kann, verschachtelte Typen und alle relevanten Member in die Budgetberechnung einbeziehen, die Whole-Module-Decompilation bei Limitüberschreitung unterlassen und jede Limit-/Abbruchentscheidung als sichtbare Diagnose (`partial`/`failed`) ausgeben.

5. `src/AiNetLinter/Mcp/Assemblies/AssemblyReferenceResolver.cs:63-85,91-105,157-181` — **[MAJOR] [Rules/Logik/Konzept]** Die Referenzauflösung ist weder vollständig sichtbar noch identitätstreu: Eine Ausnahme beim Enumerieren lokaler DLLs wird in Zeile 171-175 ohne Log/Diagnose verschluckt und verletzt `EnforceNoSilentCatch`; außerdem wird `Resolved=true` bereits vor erfolgreicher `MetadataReference.CreateFromFile`-Erzeugung gesetzt, und der erste gleichnamige Kandidat wird ohne Abgleich von Version/Kultur ausgewählt. Damit kann die Antwort eine nicht eingebundene oder falsche Dependency als „aufgelöst“ melden. **Fix:** den Fallback mit einer strukturierten sichtbaren Diagnose versehen, Kandidaten anhand der statisch gelesenen Assembly-Identität (mindestens Name, Version und Kultur) verifizieren, `Resolved=true` erst nach erfolgreicher MetadataReference-Erzeugung setzen und die tatsächlich verwendeten Referenzpfade im Manifest/Sessionzustand mitführen.

6. `src/AiNetLinter/Mcp/Assemblies/AssemblyReferenceResolver.cs:111-129`, `src/AiNetLinter/Mcp/Assemblies/AssemblyAnalysisSessionModels.cs:143-151` und `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisContextFactory.cs:34-38` — **[MAJOR] [Logik/Konzept]** Die Session liest die echte Assembly-Identität zwar aus PE-Metadaten und schreibt sie ins Manifest, verwirft sie aber beim Aufbau des `AssemblyContext`: Die Factory nimmt stattdessen `generation.Snapshot.Compilation.Assembly.Identity` aus dem synthetischen Roslyn-Projekt. Dieses Projekt kennt nur den Assembly-Namen und erzeugt ohne dekompilierte Assembly-Attribute nicht die Version/Kultur/Public-Key-Identität der Ziel-DLL; der bestehende `inspect_assembly`-Identitätsvertrag kann dadurch falsche Versionsdaten ausgeben. **Fix:** die `AssemblyReferenceResolution.Identity` unverändert in `AssemblySessionGeneration` und `AssemblyContext` transportieren und für Payload/Text verwenden; die synthetische Compilation-Identität nur für den Roslyn-Quellgraphen verwenden.

