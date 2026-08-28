---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 004
epic: EPIC-02
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28T16:20:00+02:00
code_commit_hash: 639f0fc47c8f90897db12c868ecd1295f608ad1a
status_after: done (pending audit)
---

# Result Step 004: Assembly-Session-Fundament korrigieren: Cache, Limits, Referenzen und Identität

## Zusammenfassung

Die Wiederaufnahme von Step 004 ist umgesetzt. Das Assembly-Subsystem verwendet gekapselte Manifestgruppen mit unverändertem flachem JSON-Wire-Format, immutable Generationen mit atomarem Current-Pointer, vollständige Validierung vor Cache-Adoption und Workspace-Installation sowie statische PE-Identität und Referenzpfade. Die Typ-/Member-/Komplexitätsbudgets gelten für vollständige verschachtelte Typbäume ohne Whole-Module-Fallback.

Die sechs gate-blockierenden DRY-Duplikate wurden in gemeinsame TestKit-Helfer überführt. Die beiden bestätigten DeadCode-Funde wurden entfernt. Die 49 eindeutigen MagicValues-Kandidaten des Ausgangsaudits (58 Treffer) wurden fachlich bearbeitet: Diagnosecodes, Cache-/Schema-/Encoding-/Bufferwerte, JSON-Membernamen und Statusdarstellung sind zentralisiert; kontextgebundene Decompiler-Marker und einzelne erklärende Fehlermeldungen bleiben lokal.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Assemblies/` — Cachevertrag, Diagnosecodes, Status-/Cleanup-Helfer, Manifest-Converter, Sessionvalidierung, Decompiler-Budgets, Referenzauflösung und Roslyn-Workspace.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` — gemeinsame Statusdarstellung und sichtbare `ResolvedPath`-Referenzangaben.
- `src/AiNetLinter.TestKit/AssemblyTestHelper.cs`, `McpTestResultText.cs`, `TestWaiter.cs` — gemeinsame Test-/MCP-Helfer für Assembly-Emission, Ergebnistext und Polling.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/` — Nutzung der gemeinsamen Assembly-Testhilfe sowie Cache-Schema-Diagnosetest.
- `src/AiNetLinter.FastTests/Mcp/WiringContractTests.cs`, `WiringFilesystemContractTests.cs` — Nutzung gemeinsamer Text- und Wait-Helfer.
- `src/AiNetLinter.FastTests/Core/Checkers/MaxInheritanceDepthTests.cs`, `Output/MarkdownBuilderTests.cs` — bestätigten unreferenzierten Code entfernt.

## DRY-, MagicValues- und DeadCode-Befunde

- `EmitAssembly` in `AssemblyAnalysisSessionTests` und `AssemblyAnalysisToolTests` verwendet `AssemblyTestHelper.EmitAssembly`.
- `TextOf` und `WaitForConditionAsync` in beiden Wiring-Testklassen verwenden `McpTestResultText` bzw. `TestWaiter`.
- `GetSemanticContext` und das private Feld `_` in den beiden benannten FastTests hatten keine indirekte Nutzung und wurden entfernt.
- Diagnosecodes sind über `AssemblyDiagnosticCodes` mit `nameof`-Schlüsseln zentralisiert; Compilerdiagnose-IDs für absichtlich bodylose Decompilerdeklarationen sind ebenfalls benannt.
- Cachepfade, Dateinamen, Schema-/Encodingwerte, Synthetic-Projectname und File-Buffergröße sind in `AssemblyCacheContract` gebündelt. JSON-Feldnamen werden aus den internen Manifest-/DTO-Membern mit `nameof` und CamelCase abgeleitet.
- Die abschließende MCP-MagicValues-Wiederholung konnte wegen `Transport closed` des Audit-Servers nicht ausgeführt werden. Die vorherigen MCP-Audits bestätigten nach der Konsolidierung 45 verbleibende, überwiegend kontextgebundene Kandidaten; der lokale vollständige Linterlauf meldete danach `OK`. Verbleibende Marker wie `class <`/`CompilerGenerated` beschreiben die syntaktische ILSpy-Ausgabe, während einmalige deutsche Kontext-/Fehlermeldungen keine gemeinsame fachliche Konstante bilden.

## Commit

- **Code-Commit-Hash:** `639f0fc47c8f90897db12c868ecd1295f608ad1a`
- **Message:**
  ```
  fix: Assembly-Drift beheben [decompiled-assembly-analysis]

  Refs: tasks/decompiled-assembly-analysis/step-004
  ```
- **Branch:** `main`
- **Push:** nein (lokal)
- **Doku-Commit:** folgt als separater zweiter Commit.

## Build-/Test-Output

- `dotnet clean` → grün, 0 Warnungen, 0 Fehler.
- `dotnet build` → grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` → grün, 1.868/1.868 Tests.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` → grün, 360/360 Tests.
- Relevante gezielte Assembly-Tests → grün, Session 9/9 und Tool 10/10.
- `dotnet run --project src/AiNetLinter -- --path AiNetLinter.slnx --config rules.json --no-cache` → `OK`.
- `find_duplicates`-Audit gemäß Drift-Loop: keine der sechs Gate-Duplikatgruppen verblieb; strukturelle Assembly-Kandidaten wurden als semantisch unterschiedliche Methoden triagiert.
- `find_dead_code`-Audit: keine hochkonfidenten unreferenzierten privaten/internen Kandidaten im geprüften FastTest- bzw. Assembly-Bereich.
- `get_violations`/`safeguard`-Audits vor dem letzten MCP-Transportabbruch: Assembly- und TestKit-Bereiche ohne Regelverstöße bzw. mit bestandenem Score.

## Abweichungen vom Plan

Die Wiederaufnahme erweitert den ursprünglichen Step ausschließlich um die ausdrücklich freigegebenen DRY-, MagicValues- und DeadCode-Gates. Dafür wurden die sechs Testduplikate in das vorhandene TestKit-Muster überführt und keine Unterdrückungen eingesetzt.

Für metadata-only Decompilation werden Memberkörper deaktiviert. Compiler-generierte verschachtelte Zustandsautomaten mit ILSpy-Namen wie `<DisposeAsync>d__59` werden vor der Roslyn-Prüfung entfernt; die erwarteten bodylosen Memberdiagnosen `CS0073` und `CS0501` werden als Folge dieser Darstellungsform erkannt. Nicht parsbarer Quelltext bleibt ein Fehler, andere semantische Compilation-Probleme bleiben als `partial` sichtbar. Diese Anpassung war erforderlich, damit die echte MCP-Selbstanalyse auch den angeforderten Typ `McpCodeGraphServer` ohne Reflection-Ausführung liefern kann.

Der abschließende MCP-Audit-Aufruf war wegen eines nicht erreichbaren Audit-Transports nicht möglich; deshalb wurde der bereits vorliegende Auditstand mit dem lokalen Linterlauf und den vollständigen Build-/Test-Gates gegengeprüft. Es wurden keine Änderungen an Roadmap, Task-State, früheren Steps, Reviews oder `tech-debt.md` vorgenommen und kein Tech-Debt-Eintrag angelegt.

## Beobachtungen

Die neue Pointer-/Generation-Struktur lässt alte, nicht referenzierte Generationen bis zu einer späteren Bereinigung unberührt. Cache-Lesezugriffe adoptieren ausschließlich die Generation des validierten Current-Pointers. Bei Workspace-/Publish-Fehlern bleibt ein vorhandener last-good Snapshot erhalten.

Der vollständige Integration-Lauf wurde nach Bereinigung des generierten Assembly-Caches aus einem sauberen Zustand durchgeführt; der zuvor beobachtete E2E-Fehler durch stale bzw. nicht parsbare Decompiler-Ausgabe trat im Abschlusslauf nicht mehr auf.

## Bekannte Unschärfen

Die MCP-Auditverbindung meldete bei wiederholten Aufrufen `Transport closed`; eine neue Endausgabe von `find_magic_values`, `get_violations` und `safeguard` nach dem letzten kleinen Code-Refactoring war dadurch nicht abrufbar. Der relevante vorherige Auditstand, der lokale Linterlauf und die beiden vollständigen Gates sind grün. Die verbleibenden MagicValues sind fachlich als Cachevertrag, Decompiler-Syntaxmarker oder einmalige Kontextmeldungen eingeordnet.

Es wurden keine weiteren Steps geplant.
