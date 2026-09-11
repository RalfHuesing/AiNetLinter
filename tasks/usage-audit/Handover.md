# Übergabe: `tasks/usage-audit`

Stand: 2026-09-11. Diese Übergabe wurde auf ausdrücklichen Wunsch des Nutzers
für einen Modellwechsel erstellt.

## Sofortiger Einstieg

Diese Datei zuerst vollständig lesen, danach `Konzept.md`,
`.agents/skills/project-orchestrator/SKILL.md`,
`.agents/rules/AiNetLinterRichtlinien.mdc` und die einschlägigen Rollen lesen.
Nicht neu beginnen: Der Arbeitsbaum enthält bereits Slice-15-Änderungen und
eine nachträglich entstandene Slice-14-Datei.

Weiter strikt seriell arbeiten: genau ein Slice/Agent, danach Review,
Verifikation und Commit. Nutzeränderungen und Nutzercommits nicht verwerfen.

## Git- und Slice-Stand

Aktueller Branch ist `main`, HEAD ist `d397ec99`.

Committed sind:

1. `2eae771d feat: führe kompakte MCP-Handoffs ein`
2. `078386bb fix: sichere Source-Handoffs nach Lebenszykluswechseln`
3. `a38c181e fix: sichere Assembly-Handoffs über Referenzen hinweg`
4. `baaf0482 refactor: vereinheitliche MCP-Navigationsverträge`
5. `59b4cc7f refactor: kürze MCP-Statusantworten`
6. `d6de717e refactor: entferne Handoff-IDs aus MCP-Texten`
7. `bbff1bc4 refactor: entdupliziere MCP-Follow-up-Metadaten`
8. `360cb89d feat: klassifiziere MCP-Dokumentbereiche`
9. `5e2734ad feat: erweitere FindSymbol um Scoping und Ranking`
10. `ef662627 feat: vereinheitliche Symbolgraph-Scopes`
11. `fd051709 feat: begrenze Dependency-Graphen nach Scope`
12. `b1b2459c feat: schärfe MCP-Testevidenz`
13. `c26ce431 feat: normalisiere Call-Graph-Domain`
14. `d397ec99 feat: vereinheitliche Call-Graph-Antworten`

Der Nutzercommit `5aa4ab5a docs: ergänze 360-Grad-Usage-Audit und
Befundklassifikation` lag bereits vor der Slice-Arbeit vor und ist zu bewahren.

## Aktueller Arbeitsbaum

Uncommitted Slice 15:

- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextResponseBudget.cs`
  (neu)
- `src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext/FeatureContextResponseBudgetTests.cs`
  (neu)
- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextModels.cs`
- `src/AiNetLinter/Mcp/Tools/FeatureContext/GetFeatureContextTool.cs`
- `src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs`
- `src/AiNetLinter/Mcp/Registration/McpArgumentValidationFilter.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext/GetFeatureContextToolCapTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Wiring/WiringToolCollectionContractTests.cs`
- `src/AiNetLinter/Output/LinterErrorCodes.cs`

Zusätzlich untracked, aber auftragsbezogen aus Slice 14:

- `src/AiNetLinter/Mcp/Tools/SymbolGraph/AssemblyCallGraphBuilder.cs`

Diese Datei nicht löschen oder übergehen. Vor jeder Änderung `git status
--short`, `git diff --stat` und `git ls-files --others --exclude-standard`
prüfen. Der vollständige Slice-14-Assembly-Pfad muss mit dieser Datei erhalten
bleiben.

## Verifikationsstand

- Slice 12: zuletzt 62/62 fokussierte Tests grün, Build 0 Warnungen/Fehler.
- Slice 13: Call-Graph-Fokus zuletzt 20/20 grün, Build 0/0.
- Slice 14: gemeldet 52/52 CallTree, 7/7 Assembly-Routen, später 24/24
  CallTree, 1/1 Assembly-Regression und 8/8 Assembly-Navigation; Build 0/0,
  `git diff --check` sauber.
- Slice 15: neue Budgettests 4/4 grün; FeatureContext-Suite 23/30 grün.
  Sieben bestehende Cap-Erwartungen schlagen wegen der neuen Budgetprojektion
  und müssen fachlich aktualisiert oder als echte Regression repariert werden.
- Build nach Slice-15-Zwischenstand kompilierte ohne Fehler; erneut vollständig
  ausführen.
- `git diff --check` war zuletzt Exit 0, mit bekannten LF/CRLF-Hinweisen.
- MCP-Linterläufe für die geänderten Scopes waren mehrfach sehr langsam oder
  hängend. Erneut gezielt versuchen; bei erneutem Hänger kontrolliert beenden
  und dokumentieren.
- Stress-Tests wurden nicht gestartet und bleiben ausdrücklich ausgeschlossen.

## Wiederholt korrigierte Risikostellen

Diese Bereiche waren in mehreren Review-/Korrekturrunden auffällig. Der neue
Agent soll sie gezielt gegen Konzept, Text, StructuredContent und Tests prüfen.

### Testevidenz (Slice 12)

- `TestCoverageScanner` verwendete zunächst nur die erste Testklasse einer
  Datei. Jetzt müssen alle passenden Testklassen dedupliziert und ordinal
  sortiert in Empfehlungen erscheinen.
- Typ-Namenskonventionen zählten zunächst unbeteiligte Klassen und zu große
  Counts. Typkonvention bleibt Klassen-/Count-Evidenz ohne erfundene Methoden.
- `TestContextFormatter` bezeichnete gemischte Evidenz zunächst als
  Testmethoden. „Testmethoden“ ist nur zulässig, wenn alle sichtbaren Treffer
  konkrete Methoden enthalten; sonst „Evidenztreffer“.
- `change-context` verlor Klassen-/Count-Evidenz zunächst in leeren
  `TestAssociationPayload`s. `evidenceKind`, `confidence`, `totalTestCount`
  und `testClassNames` sind jetzt Teil des Vertrags.

### Call-Graph (Slices 13–14)

- Incoming wurde zunächst falsch orientiert. Gültig ist immer `caller -> callee`;
  bei Incoming also `caller -> seed`.
- `DispatchKind` fehlte zunächst bei Incoming und `override` wurde durch die
  Reihenfolge vor `virtual` verschluckt.
- Call-Sites wurden zunächst nur innerhalb eines Add-Vorgangs dedupliziert;
  bei `both` muss die Deduplizierung über alle Expansionen gelten.
- Der Assembly-`includeReferences`-Pfad verwendete zunächst weiterhin den
  rekursiven MetricsTree; der Zielzustand ist Nodes/Edges mit gemeinsamen
  Renderern und Whole-Edge-Budget.

### Budget und Assembly-Envelope (Slice 14)

- `ApplyFinalResponseBudget` war zunächst No-op und verlor anschließend
  Assembly-Präfix, Diagnose-/Depth-Hinweise sowie `analysis`, `wireBudget`,
  Origin und Snapshot.
- Der allgemeine Assembly-Trim `budget/4` musste für CallTree-Responses
  umgangen werden, damit der Graph erst im CallGraph-Budget gekürzt wird.
- Der 250-Knoten-Hardcap stoppte zunächst nur die Ausgabe, nicht die Roslyn-
  Expansion; er muss die Queue/Fan-outs tatsächlich beenden.
- Der Hardcap gilt auch für den gemergten `includeReferences`-Graphen, nicht
  nur pro Quelle.
- Nach jeder finalen Kürzung müssen `wireBudget`-Zähler und `truncated`
  konsistent neu berechnet werden.

## Nächster Schritt: Slice 15 abschließen

1. Den aktuellen Diff nicht verwerfen.
2. Die sieben roten FeatureContext-Cap-Tests anhand des verbindlichen
   `maxResponseBytes`-Vertrags korrigieren oder echte Regressionen beheben.
3. Abschnittserhalt, deterministische Priorisierung, Text, StructuredContent,
   Navigation und `RESPONSE_BUDGET_TOO_SMALL` prüfen.
4. Frische Read-only-Review durchführen.
5. Fokussierte Tests, `dotnet build`, `git diff --check` und einen kurzen
   MCP-Linterversuch ausführen.
6. Slice 15 als eigenen deutschen Conventional Commit festschreiben.

Danach Slices 16–22 aus `Konzept.md` seriell umsetzen: TestContext-Budget und
Composite-Trimmer entfernen; feste Budgets der übrigen Symboltools; Status-/
Empty-/Population-Verträge; Origin/Namespace/Kind-Alternativen;
Toolbeschreibungen mit Vorher-/Nachher-Messung; Dokumentation einschließlich
der verschobenen `Docs/agent-api.md`-Anpassung; anschließend Cleanup, Dogfood,
Auditor und Abschlussgates.

## Verbindliche Abschlussgates

Vor Taskabschluss müssen erfolgreich sein:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
git diff --check
```

Zusätzlich muss die `auditor`-Rolle DRY, Refactoring-Drift, Dead Code und
Magic Values prüfen. Stress nur auf ausdrückliche Nutzeranforderung.

Diese Datei ergänzt `Konzept.md`; sie ersetzt weder Konzept noch Projektregeln.
