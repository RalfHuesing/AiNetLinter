---
status: open
type: step-plan
task: speedup-tests
step: 017
corrects: null
title: "Duplicate-Detection-Engine-Kohorte auf die In-Memory-Testplattform migrieren"
epic: EPIC-4
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5.6-sol Medium
created_by_model_knowledge_cutoff: 2024-06
created_at: 2026-08-12
related_to: [step-015, step-016]
---

# Step 017: Duplicate-Detection-Engine-Kohorte auf die In-Memory-Testplattform migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — nach den beiden freigegebenen Duplicate-Detection-
  Scannern ist die gemeinsame Core-Engine-Familie der nächste geschlossene In-Memory-Schnitt.
- **Konzept-Referenz:** `konzept.md` §1 „Testebenen und erlaubte Abhängigkeiten", §2
  „Gemeinsame Testplattform", §3 „Laden und Ausführen trennen", §7 „Sparsame Verifikation",
  §8 „Strangler-Migration" und §9 „Große Drift-Loop-Steps".

## Aktueller Projektzustand (JIT-Kontext)

`step-016` ist `approved`. `DuplicateDetectionScannerTests` und `RefactoringDriftScannerTests`
liegen damit beide als Component-Tests in `AiNetLinter.FastTests` und verwenden
`RoslynTestSolutionFactory` mit deterministischen virtuellen Solution-/Dokumentpfaden. Die darunter
liegende `DuplicateDetectionEngine` ist bereits eine reine `Solution`-Seam: `ScanAsync` und das in
der Partial-Datei implementierte `FindSimilarToAsync` laden weder MSBuild noch Dateien. Eine
Produktcode- oder Factory-Erweiterung ist für diesen Step nicht erforderlich.

Die beiden noch `pending` geführten Engine-Zeilen bilden eine gemeinsame Produktfamilie:
`DuplicateDetectionEngineTests` ist auf zwei Partial-Dateien mit insgesamt 17 Clone-, Threshold-,
Cluster- und False-Positive-Verträgen verteilt; `RefactoringDriftEngineTests` enthält sieben
Verträge für Helper-Fingerprint, Caller-Ausschluss, Near-Schwelle und deterministische Sortierung.
Alle drei Legacy-Dateien erzeugen dagegen eigene `AdhocWorkspace`-/`MetadataReference`-Sätze und
schreiben virtuelle Quellen in Temp-Verzeichnisse. Die bestehende Factory kann denselben
Pfadvertrag ohne IO liefern; die beiden vorhandenen Scanner-Migrationen sind das lokale Muster.

Der produktseitige Coverage-Audit zeigt einen nicht-trivialen, bisher nicht direkt benannten Zweig:
`FindCandidateMethods` behandelt neben normalen Methoden auch `LocalFunctionStatementSyntax`,
während die 17 Engine-Testnamen nur Methodenvarianten ausweisen. Ein gezielter Component-Vertrag
für zwei ausreichend lange, strukturell identische lokale Funktionen schließt diese Lücke und
muss bei Entfernen des Local-Function-Zweigs rot werden. Weitere Assertions werden nicht
zusammengelegt oder abgeschwächt.

Die 19 Legacy-Verträge in `DuplicateDetectionToolTests` und
`DuplicateDetectionToolRefactoringDriftTests` bleiben bewusst außerhalb dieses Engine-Schnitts.
Die vollständigen Referenz- und Call-Tree-Ergebnisse zeigen, dass sie direkt
`DuplicateDetectionTool.ExecuteAsync` prüfen: fehlende Solution, Mode-/Argument-Dispatch,
Scanner-Fehlerdurchleitung, Structured Content, Textantworten sowie Sufficiency-/Truncation-
Semantik. Diese Server-/Tool-Antwortverträge sind eine eigene spätere Kohorte und werden nicht
als vermeintliche Engine-Verträge mitverschoben.

Im Tech-Debt-Index gibt es keinen Eintrag im berührten Core/DuplicateDetection-Bereich. Die
offenen `auto_fixable: ja`-Einträge TD-003 und TD-004 betreffen andere Dateien und werden nicht
angehängt. Die fremden Aufgaben `tasks/validate-file` und `tasks/magic-values-in-mcp` bleiben
unangetastet.

## Intention

Nach diesem Step liegt die vollständige Core-Engine-Familie der Duplicate Detection als schnelle,
rein in-memory ausgeführte Component-Kohorte in `AiNetLinter.FastTests`. Sie nutzt die vorhandene
Factory und den bereits etablierten kalibrierten FastTests-Helper, entfernt lokalen Workspace-,
Referenz- und Temp-Datei-Eigenbau und belegt zusätzlich die Local-Function-Erkennung.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Core/DuplicateDetection/DuplicateDetectionEngineTests.cs` (neu)

- **Was:** Die acht Ground-Truth-/Cluster-Verträge der gleichnamigen Legacy-Datei als
  `Category=Component` übernehmen. Solutions über `RoslynTestSolutionFactory.CreateSolution`
  mit einem festen virtuellen Solution-Pfad und `ProjectSpec` erzeugen und den zurückgegebenen
  `RoslynTestSolution`-Owner deterministisch entsorgen. Bereits vorhandene kalibrierte Standard-
  Methoden aus `AiNetLinter.FastTests.TestHelper` wiederverwenden; nur die für Near-/Fuzzy-
  Varianten notwendige szenariolokale Mutationslogik behalten.
- **Was:** Einen neuen Vertrag aufnehmen, der zwei ausreichend lange identische lokale Funktionen
  als Cluster erkennt und damit den expliziten `LocalFunctionStatementSyntax`-Zweig belastbar
  abdeckt.
- **Warum:** Die breite Engine-Matrix gehört auf die Component-Ebene; der neue Fall ist ein
  produktseitig gefundener fachlicher Zweig und keine kosmetische Zeilenabdeckung.

### Datei 2: `src/AiNetLinter.FastTests/Core/DuplicateDetection/DuplicateDetectionEngineFalsePositiveTests.cs` (neu)

- **Was:** Die neun False-Positive-, Normalisierungs-, Threshold-, Leer- und Scope-Verträge als
  zweite Partial-Datei derselben FastTests-Klasse übernehmen. Die gemeinsame Factory-/Pfadhelper-
  Logik in genau einer der beiden Partial-Dateien halten. `obj`, `tests/Fixtures` und weitere
  Pfadvarianten ausschließlich als virtuelle Dokumentpfade modellieren; keine Verzeichnisse oder
  Quelldateien auf Platte anlegen.
- **Warum:** Die bestehende Dateiteilung hält die Klasse unter dem Dateilimit und die virtuelle
  Pfadidentität prüft genau den Enginevertrag ohne Dateisystemkosten.

### Datei 3: `src/AiNetLinter.FastTests/Core/DuplicateDetection/RefactoringDriftEngineTests.cs` (neu)

- **Was:** Alle sieben Legacy-Verträge als `Category=Component` übernehmen und ihre Solutions
  ebenfalls über `RoslynTestSolutionFactory` mit virtuellen Pfaden erzeugen. Die Symbolauflösung
  weiterhin aus dem erzeugten Roslyn-Dokument/Compilation-Modell vornehmen; den lokalen
  `IDisposable`-/Temp-Verzeichnis-/Workspace-/Referenz-Builder entfernen. Den vorhandenen
  `TestHelper.BuildCalibratedMethod` dort verwenden, wo er den identischen Standardfall ausdrückt;
  sortierungs- oder score-spezifische Varianten bleiben lokal.
- **Warum:** Clone- und Drift-Modus teilen Fingerprint-Sammlung und Jaccard-Berechnung in derselben
  Partial-Engine; ihre direkten Core-Verträge bilden deshalb einen kohärenten Step, ohne die
  getrennte MCP-Dispatch-Schicht einzubeziehen.

### Datei 4: Legacy-Quellen unter `src/AiNetLinter.Tests/Core/DuplicateDetection/`

- **Was:** Nach einmaligem grünen Legacy-Baseline-Lauf und Alt-/Neu-Abgleich
  `DuplicateDetectionEngineTests.cs`, `DuplicateDetectionEngineFalsePositiveTests.cs` und
  `RefactoringDriftEngineTests.cs` physisch löschen.
- **Warum:** Beide Ledger-Kohorten müssen am Step-Ende geschlossen migriert sein; parallele oder
  auskommentierte Alt-Kopien widersprechen der Strangler-Invariante.

### Datei 5: `tasks/speedup-tests/test-migration-ledger.md`

- **Was:** Die Zeilen `DuplicateDetectionEngineTests` und `RefactoringDriftEngineTests` von
  `pending` auf `migrated` setzen, die realen FastTests-Abdeckungsorte und die gezielte
  Verifikationsevidenz eintragen.
- **Warum:** Ledger, Legacy-Bestand und Zielbestand müssen atomar konsistent bleiben.

### Datei 6: `tasks/speedup-tests/codemap.md`

- **Was:** Nach der Umsetzung die neuen FastTests-Engineorte, die Factory-/Helper-Wiederverwendung
  und die obsolet gewordenen Legacy-Quellen als Pointer nachführen; die beiden pending
  `DuplicateDetectionTool*Tests` weiterhin ausdrücklich als getrennte Tool-Dispatch-Kohorte
  markieren.
- **Warum:** Folgende EPIC-4-Schritte sollen Engine- und Toolvertrag nicht erneut vermischen und
  keinen weiteren lokalen Workspace-/Temp-Datei-Builder erzeugen.

## Tests

- [ ] Vor der Legacy-Löschung einmalig Baseline:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~DuplicateDetectionEngineTests|FullyQualifiedName~RefactoringDriftEngineTests"`
- [ ] `dotnet build`
- [ ] Gezielter Engine-/Scanner-/Factory-/Guard-Lauf:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~DuplicateDetectionEngineTests|FullyQualifiedName~RefactoringDriftEngineTests|FullyQualifiedName~DuplicateDetectionScannerTests|FullyQualifiedName~RefactoringDriftScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Gezielter Ledger-/Legacy-Gate-Lauf:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
- [ ] Kein `Category!=Stress`-Vollprofil: `step-017` ist keine Epic-Grenze. Kein Stresslauf.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Sämtliche 24 vorhandenen Engine-Verträge liegen als getrennte Component-Tests im
  Fast-Projekt; der neue Local-Function-Vertrag ergänzt die Matrix, ohne Altassertions zu ersetzen.
- [ ] Die Kohorte referenziert weder lokalen `AdhocWorkspace`-/`MetadataReference`-Eigenbau noch
  Temp-Dateisystem, `SourceFileCatalog.LoadAsync`, MSBuild, externe Prozesse oder eine
  zwangsserialisierende Collection.
- [ ] Der Local-Function-Test wird bei Entfernen der produktiven Local-Function-Erkennung rot und
  prüft ein echtes Cluster oberhalb der konfigurierten Token-/Similarity-Schwellen.
- [ ] `DuplicateDetectionToolTests` und `DuplicateDetectionToolRefactoringDriftTests` sind
  unverändert `pending`; kein Load-State-, Mode-/Argument-, Response- oder Sufficiency-Vertrag ist
  in die Engine-Kohorte verschoben worden.
- [ ] Die drei Legacy-Dateien sind gelöscht und beide Ledger-Zeilen zeigen auf die realen neuen
  Abdeckungsorte; Ledger-Guard und Legacy-Build-Gate sind grün.
- [ ] Build und die unter „Tests" genannten gezielten Filter sind grün; kein Vollprofil und kein
  Stresslauf wurde für diesen Zwischenstep ausgeführt.
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, mit
  `[speedup-tests]`-Suffix)
- [ ] `step-017/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte (Produktion)` — `#nullable enable`,
  bestehende Partial-Dateiteilung beibehalten und duplizierte Solution-Builder vermeiden.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4. Updates & Tests` — xUnit-v3-Abdeckung, keine
  zwangsserialisierende Collection ohne reale Exklusivität und gezielte C#-Testinfrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention` — Assertions nicht
  abschwächen, vorhandene Factory-/Helper-Strukturen wiederverwenden und keine Task-/Step-IDs in
  Codekommentaren hinterlassen.

## Bekannte Ausnahmen

- Keine. TD-001 betrifft den echten MCP-Framing-Subprozess; TD-003 und TD-004 liegen außerhalb
  der berührten Dateien.

## Notes

- MCP-Aktualitätsprüfung: `get_server_health` meldete die nach `step-016` gestartete, geladene
  Solution mit 473 C#-Dateien und `RefreshCount = 0`; `find_symbol` fand ausschließlich
  `src/AiNetLinter.FastTests/Mcp/Tools/RefactoringDriftScannerTests.cs`, nicht die gelöschte
  Legacy-Datei. `get_symbol_body`, `find_references`, `get_call_tree`, `get_impact` und
  `dependency_graph` zeigten ebenfalls die aktuellen FastTests-Konsumenten. Für diesen Plan wurde
  daher kein C#-Fallback-Read benötigt. Die anfangs verwendeten vollständigen Documentation-
  Comment-IDs wurden von `find_references` nicht aufgelöst; qualifizierte Symbolnamen lieferten
  danach vollständige Resultate. Das ist eine Identifier-Formatfrage, kein Staleness-Signal.
- Nicht in Scope: `DuplicateDetectionToolTests`,
  `DuplicateDetectionToolRefactoringDriftTests`, alle weiteren MCP-Scanner/-Tools und
  MSBuild-/Prozesskohorten.
- Virtuelle Pfade sind Roslyn-Pfadidentität und dürfen nicht materialisiert werden. Der Owner des
  `RoslynTestSolution` muss pro Test deterministisch entsorgt werden.
