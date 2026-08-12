---
status: open
type: step-plan
task: speedup-tests
step: 018
corrects: null
title: "Duplicate-Detection-Toolkohorte auf die In-Memory-Testplattform migrieren"
epic: EPIC-4
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5.6-sol
created_by_model_knowledge_cutoff: nicht ausgewiesen
created_at: 2026-08-13
related_to:
  - step-015
  - step-016
  - step-017
---

# Step 018: Duplicate-Detection-Toolkohorte auf die In-Memory-Testplattform migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — nach Scanner- und Engine-Migration ist die zugehörige
  direkte Tool-Dispatch-Kohorte noch im Legacy-Projekt offen.
- **Konzept-Referenz:** `konzept.md` Abschnitt „Scope / Muss-Haben" (breites MCP-Toolverhalten ohne
  Prozess), Technische Leitplanken §1/§2/§5 und §8/§9 (Component-Snapshots, Strangler-Kohorte,
  Ledger und produktseitiger Coverage-Audit).

## Aktueller Projektzustand (JIT-Kontext)

`step-017` ist `approved`: Duplicate-Detection-Scanner und -Engines liegen vollständig als
Component-Verträge in `AiNetLinter.FastTests` und verwenden `RoslynTestSolutionFactory` mit
virtuellen Pfaden. MCP-`find_references`, `get_call_tree`, `get_impact` und der Datei-/Typ-
Abhängigkeitsgraph zeigen für `DuplicateDetectionTool.ExecuteAsync` genau 19 direkte Testaufrufe in
zwei Legacy-Klassen (zehn Clone- und neun Refactoring-Drift-Verträge) sowie nur die zwei produktiven
Registrierungsaufrufe. Damit bilden die beiden Klassen eine geschlossene Toolkohorte über denselben
Load-State-, Mode-, Argument-, Scanner-Fehler- und Response-/Sufficiency-Dispatch.

Beide Legacy-Klassen duplizieren denselben lokalen `AdhocWorkspace`-/`MetadataReference`-/
`SourceFileCatalog`-Builder, schreiben virtuelle Roslyn-Dokumente zusätzlich auf die Platte und
räumen Temp-Verzeichnisse über best-effort `catch` auf. Das ist für die geprüften direkten
Toolverträge nicht Teil des Vertrags. Der vorhandene `RoslynTestSolutionFactory`-Besitzer und die
internen `SourceFileCatalog(Solution, ...)`-/`McpCodeGraphServer`-Einstiege reichen aus; eine neue
Produkt-Seam ist nicht erforderlich. Die kleineren noch pending Scannerklassen
(`DependencyGraphScannerTests`, `FindSymbolScannerTests`, `MetricsTreeRoslynScannerTests`,
`PatternDetectScannerTests`, `SafeguardScannerTests`) sind fachlich unabhängige Folgekohorten und
werden nicht vorgezogen, weil sonst die bereits zusammenhängend migrierte Duplicate-Detection-
Familie halb offen bliebe.

## Intention

Die 19 bestehenden Tool-Dispatch-Verträge wechseln als eine vollständige Component-Kohorte nach
`AiNetLinter.FastTests`, ohne Assertions oder agentensichtbare Antwortverträge abzuschwächen. Ein
schmaler, FastTests-lokaler Testkontext bündelt ausschließlich die von beiden Klassen gemeinsam
benötigte Besitzerkette aus `RoslynTestSolution`, `SourceFileCatalog` und
`McpCodeGraphServer`; dadurch entfallen Platten-IO, per-Test-Referenzaufbau und stilles Temp-Cleanup,
ohne eine allgemeine MCP-Testplattform vorwegzunehmen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolTestContext.cs` (neu)

- **Was:** Einen kleinen `sealed`/`IDisposable`-Testkontext anlegen, der aus einer festen virtuellen
  Solution-Datei und einem `ProjectSpec` die `RoslynTestSolutionFactory` verwendet, daraus den
  internen `SourceFileCatalog` und den direkten `McpCodeGraphServer` erzeugt und Server sowie
  Workspace deterministisch in Besitzerreihenfolge entsorgt. Der Kontext exponiert nur den für die
  Tests benötigten Serverzustand; er schreibt keine Dateien und verwendet keine eigene
  `MetadataReference`-Liste.
- **Warum:** Beide Zielklassen brauchen exakt dieselbe direkte Tool-Hülle. Der lokale gemeinsame
  Besitzer verhindert Copy/Paste und Workspace-Leaks, ohne einen vorsorglichen generischen Helper
  im `TestKit` oder eine produktive API einzuführen.

### Datei 2: `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolTests.cs` (neu) und Legacy-Datei gleichen Namens (löschen)

- **Was:** Alle zehn Clone-/Argument-/Structured-Content-/Truncation-/Sufficiency-Verträge
  unverändert inhaltlich nach FastTests übernehmen, auf `[Trait("Category", "Component")]` und den
  neuen Testkontext umstellen. Konstruktor, Temp-Verzeichnis, lokaler Ad-hoc-Builder, eigene
  `MetadataReference`-Erzeugung und best-effort-Cleanup entfallen; der bestehende kalibrierte
  `TestHelper.BuildCalibratedMethod` bleibt die Quelle der Clone-Methoden.
- **Warum:** Die Assertions prüfen direkten Tool-Dispatch über vorbereitete Roslyn-Snapshots, nicht
  Dateisystem oder MSBuild, und gehören damit in die Component-Ebene. Nach grünem Alt-/Neu-Abgleich
  darf keine Legacy-Kopie verbleiben.

### Datei 3: `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionToolRefactoringDriftTests.cs` (neu) und Legacy-Datei gleichen Namens (löschen)

- **Was:** Alle neun Mode-/Helper-Symbol-/Fehlerdurchreichungs-/Response-Verträge inhaltlich
  erhalten, als `Component` kategorisieren und denselben besitzenden Kontext verwenden. Die
  kalibrierten Stub-/Helper-/Drift-Quellen bleiben szenariolokal; Temp-Pfad- und Ad-hoc-Infrastruktur
  entfallen.
- **Warum:** Clone- und Refactoring-Drift-Modus sind zwei Dispatch-Zweige desselben öffentlichen
  Toolvertrags und werden gemeinsam migriert; eine Trennung in weitere Steps wäre eine künstlich
  halbe Kohorte.

### Datei 4: `src/AiNetLinter/Mcp/Tools/DuplicateDetection/DuplicateDetectionTool.cs` (Coverage-Audit, normalerweise unverändert)

- **Was:** Die 19 Zielverträge nochmals gegen `ExecuteAsync`, `ExecuteCloneAsync`,
  `ExecuteRefactoringDriftAsync`, Argumentvalidierung sowie Clone-/Drift-Responsebuilder abgleichen.
  Die vorhandenen Assertions decken die nicht-trivialen kohortenspezifischen Dispatch-Zweige ab;
  produktiven Code nur ändern, wenn der Audit einen reproduzierbaren Defekt zeigt, und dann den
  engsten neuen Component-Regressionstest ergänzen. Generische Prozess-/Loading-/Retry-Verträge
  bleiben EPIC-6 und werden hier nicht durch einen künstlichen Subprozessfall dupliziert.
- **Warum:** Das Konzept verlangt einen produktseitigen Coverage-Audit, erlaubt aber weder
  spekulative Produktrefactorings noch das Vermischen mit der späteren MCP-Prozesskohorte.

### Datei 5: `tasks/speedup-tests/test-migration-ledger.md`

- **Was:** Beide Legacy-Zeilen von `pending` auf `migrated` setzen, die neuen FastTests-Pfade,
  `Component`-Ebene und die gezielte Alt-/Neu-/Guard-Evidenz eintragen.
- **Warum:** Ledger und physischer Bestand müssen am Step-Ende konsistent sein.

### Datei 6: `tasks/speedup-tests/codemap.md`

- **Was:** Den Planungszeiger für `step-018` nach Umsetzung als real markieren, die beiden neuen
  Tooltest-Zielorte und den lokalen Besitzerkontext als Pointer ergänzen und die Legacy-Pointer als
  obsolet kennzeichnen.
- **Warum:** Der nächste JIT-Planer braucht den tatsächlichen Abschluss der Duplicate-Detection-
  Familie und darf die entfernten Legacy-Quellen nicht erneut einplanen.

## Tests

- [ ] Vor der Löschung einmalig Legacy-Baseline:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests"`
- [ ] `dotnet build`
- [ ] Zielkohorte plus bereits migrierte direkte Unterbauten und Fast-Guards:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~DuplicateDetectionToolTests|FullyQualifiedName~DuplicateDetectionToolRefactoringDriftTests|FullyQualifiedName~DuplicateDetectionScannerTests|FullyQualifiedName~RefactoringDriftScannerTests|FullyQualifiedName~DuplicateDetectionEngineTests|FullyQualifiedName~RefactoringDriftEngineTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Ledger- und Legacy-Build-Invarianten:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
- [ ] Kein `Category!=Stress`-Vollprofil: `step-018` schließt nur eine EPIC-4-Teilkohorte, keine
  Epic-/Architekturgrenze.

## Definition of Done

- [ ] Alle 19 historischen Tool-Dispatch-Verträge laufen in `AiNetLinter.FastTests` als
  `Component`; Assertions und Response-Semantik sind erhalten.
- [ ] Die beiden Legacy-Testdateien sind physisch gelöscht; Ledger und Codemap zeigen ausschließlich
  die neuen Abdeckungsorte.
- [ ] Kein Test dieser Kohorte erzeugt ein Temp-Verzeichnis, schreibt Quelldateien auf Platte,
  baut lokale `MetadataReference`n oder verwendet `SourceFileCatalog.LoadAsync`/MSBuild/Prozesse.
- [ ] Der gemeinsame Testkontext entsorgt `McpCodeGraphServer` und `RoslynTestSolution`
  deterministisch und bleibt lokal bei der Duplicate-Detection-Toolkohorte.
- [ ] Produktseitiger Coverage-Audit dokumentiert; eine gefundene nicht-triviale Lücke besitzt den
  engsten Component-Regressionstest, andernfalls bleibt Produktcode unverändert.
- [ ] `dotnet build` und alle unter „Tests" genannten gezielten Filter sind grün; kein Stress- oder
  Vollprofil wurde ausgeführt.
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch mit `[speedup-tests]`)
- [ ] `step-018/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Projekt-Overrides` — nullable-fähige, kleine,
  versiegelte Helfertypen; Testmethodenlimit und Duplicate-Code-Regel beachten.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4-Updates--Tests` — xUnit-v3, keine neue
  serialisierende Collection und gezielte Verifikation der MCP-Kohorte.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5-Qualitätsdrift-Prävention` — keine abgeschwächten
  Assertions, kein stilles Cleanup und keine Task-/Step-Referenzen in C#-Kommentaren.

## Bekannte Ausnahmen

- Keine. Die offene generische MCP-Prozess-/Loading-/Retry-Migration gehört zu EPIC-6 und ist kein
  fehlender Teil dieses direkten Component-Dispatch-Schnitts.

## Notes

- Die beiden produktiven Registrierungsaufrufe von `DuplicateDetectionTool.ExecuteAsync` bleiben
  unverändert; Registrierung/Binding ist bereits durch die Integration-MSE repräsentativ geschützt
  und wird später mit der MCP-Prozesskohorte vertieft.
- Den Kontext nicht ins `AiNetLinter.TestKit` heben: Er hat aktuell ausschließlich zwei
  FastTests-Konsumenten und enthält MCP-produktinterne Typen; das Konzept verbietet vorsorgliche
  TestKit-Abstraktionen.
- Bei den No-Solution-Verträgen genügt ein direkt erzeugter, deterministisch entsorgter Server ohne
  Katalog. Die allgemeine Loading-State-Orchestrierung nicht in diesen Step ziehen.
