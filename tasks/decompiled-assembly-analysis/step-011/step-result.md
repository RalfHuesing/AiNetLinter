---
status: done (pending audit)
type: step-result
task: decompiled-assembly-analysis
step: 011
corrects: step-010
epic: EPIC-03
step_type: single
coded_by: coder
coded_by_model: gpt-5 (Codex)
coded_by_model_knowledge_cutoff: nicht angegeben
coded_at: 2026-08-28
code_commit_hash: 6e38b4c2
status_after: done (pending audit)
blocker_category: n/a
---

# Result Step 011: Support-/Lease-Regressionen und Orchestrator-Testzuordnung korrigieren

## Zusammenfassung

Die bestehende Support-Testklasse ist über `// @covers
AssemblySourceSelectionOrchestrator` statisch dem Orchestrator zugeordnet. Der
interne Support-Overload kann einen nicht besitzenden Scope-Beobachter direkt
nach `ResolveAsync` informieren; die bestehende `using`-Lebensdauer und der
Result-Vertrag bleiben unverändert.

Matched, NoMatch und Ambiguous werden jetzt an der gemeinsamen Support-Grenze
geprüft. Die Tests beobachten einen lebendigen Lease während Factory und
Result-Builder sowie die Freigabe nach normalem Rückweg, Cancellation nach
Provider-Snapshot-Erwerb und Result-Builder-Fehler. Snapshot-Ownership bleibt
bei der Registry; der direkte Provider-Cancellation-Test sowie die bisherigen
Fallback-, Diagnose- und Deduplizierungsassertions bleiben erhalten.

## Änderungen

- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs` —
  optionale interne `Action<AssemblySourceSelectionScope>`-Beobachtung nach
  der Scope-Auflösung ergänzt.
- `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupportTests.cs` —
  explizite `@covers`-Zuordnung, Support-Grenztests für Matched/NoMatch/
  Ambiguous, Lease-Lifetime, Cancellation und Builder-Fehler; Testdatei auf
  499 Zeilen innerhalb des aktiven Limits konsolidiert.
- `tasks/decompiled-assembly-analysis/step-011/step-plan.md` — Status auf
  `done (pending audit)` gesetzt.

## Commits

- **Code-/Test-Commit:** `6e38b4c2`
- **Message:** `test: Lease-Regressionen korrigieren [decompiled-assembly-analysis]`
- **Doku-Commit:** folgt nach diesem Result und dem Statuswechsel.
- **Branch:** `main`
- **Push:** nein

## Tests

- MCP `get_test_context` für `T:AiNetLinter.Mcp.Assemblies.AssemblySourceSelectionOrchestrator` — 8 Tests in 1 Datei über `Explicit @covers Comment`.
- MCP `get_violations` für Orchestrator und Support-Testdatei — 0 Violations.
- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~AssemblyAnalysisToolSupportTests" --no-restore` — grün, 8/8.
- `dotnet build` — grün, 0 Warnungen, 0 Fehler.
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` — grün, 1919/1919.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` — grün, 360/360, Dauer 2 m 31 s.
- Stress-Tests wurden nicht ausgeführt.

## Abweichungen vom Plan

- Der erste vollständige Integration-Lauf vor der abschließenden lokalen
  Konsolidierung meldete ausschließlich `MaxLineCount` für die betroffene
  Testdatei (531 Zeilen) und endete mit 359/360. Die direkt betroffene
  Testdatei wurde daraufhin ohne Fachlogikänderung auf 499 Zeilen reduziert;
  der abschließende vollständige Lauf war vollständig grün.
- Ein Repository-Template unter dem in der Coder-Skill referenzierten Pfad
  `tasks/decompiled-assembly-analysis/templates/step-result.md` existiert
  nicht. Das Result folgt deshalb dem etablierten Format von Step 010.

## Beobachtungen

- Die optionale Beobachtung ist intern und nicht besitzend. Der Support-Overload
  behält die Lease-Freigabe über `using`; die zusätzlichen Dispose-Aufrufe in
  den Tests prüfen nur Idempotenz.
- Der Cancellation-Test bricht den Token erst nach Erstellung des verfügbaren
  Provider-Ergebnisses ab. Dadurch wird ein erworbener Lease geprüft, während
  der kontrollierte Decompilation-Fehler an die bestehende Result-Semantik
  geht.
- Die Registry bleibt nach den Support-Aufrufen Eigentümer des residenten
  Snapshots; geprüft wird ausschließlich die Lease-Freigabe und kein vorzeitiger
  Snapshot-Dispose.
- Es wurden keine Änderungen an Orchestrator-, Factory-, Registry-, MCP-,
  Daemon-, Session-, Task-State-, CodeMap- oder Tech-Debt-Dateien vorgenommen.

## Bekannte Unschärfen

- Die Tests beobachten den Scope über die zulässige interne Naht; der
  öffentliche MCP-/Daemon-Payload enthält weiterhin keine zusätzliche
  Selection-Evidence.
- Die Testdatei liegt mit 499 Zeilen knapp unter dem Limit von 500 Zeilen.
  Eine weitergehende Aufteilung wäre außerhalb dieses Korrektur-Steps.

## Auditstatus

`done (pending audit)` — der nachgelagerte Kritiker-/Drift-Audit steht noch aus.
