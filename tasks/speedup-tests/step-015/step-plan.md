---
status: done
type: step-plan
task: speedup-tests
step: 015
corrects: null
title: "Duplicate-Detection-Scanner auf die In-Memory-Testplattform migrieren"
epic: EPIC-4
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5.6-sol Medium
created_by_model_knowledge_cutoff: 2024-06
created_at: 2026-08-12
related_to: [step-013, step-014]
---

# Step 015: Duplicate-Detection-Scanner auf die In-Memory-Testplattform migrieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-4` aus `roadmap.md` — nach der freigegebenen Skeleton-/Filterkohorte sind die
  In-Memory-Scanner- und Toolkohorten noch offen; dieser Step schliesst genau den
  `DuplicateDetectionScanner`-Teil.
- **Konzept-Referenz:** `konzept.md` §1 „Testebenen und erlaubte Abhaengigkeiten", §2
  „Gemeinsame Testplattform", §7 „Sparsame Verifikation", §8 „Strangler-Migration" und §9
  „Grosse Drift-Loop-Steps".

## Aktueller Projektzustand (JIT-Kontext)

`DuplicateDetectionScanner.ScanAsync` akzeptiert bereits eine geladene Roslyn-`Solution` und ist
damit die gesuchte objektbasierte Ausfuehrungs-Seam; Produktcode muss fuer diese Kohorte nicht
umgebaut werden. Die sieben Legacy-Tests unter
`src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionScannerTests.cs` bauen dagegen je Test einen
eigenen `AdhocWorkspace`, erzeugen BCL-Referenzen erneut und schreiben Quelldateien nur deshalb in
ein Temp-Verzeichnis, weil `DuplicateDetectionEngine` pfadlose Dokumente bewusst ignoriert und
`scopeDir` gegen `Document.FilePath` prueft.

Mit `RoslynTestSolutionFactory` und dem bereits nach `AiNetLinter.FastTests` uebernommenen
`TestHelper.BuildCalibratedMethod`/`CalibratedBaseStatements` existieren die passenden Seams schon.
Der Factory fehlt lediglich eine optionale, rein virtuelle Pfadkalibrierung fuer `Solution.FilePath`
und `Document.FilePath`; sie soll erweitert, nicht durch einen scannerlokalen Workspace-Builder
dupliziert werden. Die Tests bleiben unabhaengige Component-Szenarien und brauchen weder eine
serialisierende Collection noch `PreparedSolutionFixture`, weil sie unterschiedliche kleine
Solutions aufbauen und der teure Referenzsatz bereits gecacht ist.

Der Coverage-Audit zeigt zwei schwache Legacy-Vertraege: Der bisherige `ExactThreshold`-Test hat
nur eine Methode und erzeugt gar keinen Cluster; der `NearThreshold`-Test erzeugt ebenfalls keinen
Cluster. Beide beweisen daher keine Bucket-Filterwirkung. Auch der bisherige `maxResults`-Test
findet nur einen Cluster und beweist keine Trunkierung. Diese Assertions werden bei der Migration
mit kalibrierten Positiv-/Negativdaten aussagekraeftig gemacht, ohne den produktiven Vertrag oder
die Mindestabdeckung abzuschwaechen.

`step-014` ist `approved`; die Korrektur von `step-013` ist damit abgeschlossen. Im
Tech-Debt-Index existiert kein `auto_fixable: ja`-Eintrag im hier beruehrten Scanner-/Factorybereich.
Die fremde Aufgabe `tasks/magic-values-in-mcp` bleibt vollstaendig unangetastet.

## Intention

Nach diesem Step laeuft die vollstaendige `DuplicateDetectionScannerTests`-Kohorte als echte
Component-Kohorte in `AiNetLinter.FastTests`, ohne MSBuild, Platte oder selbstgebauten
`AdhocWorkspace`. Die Testplattform kann dafuer deterministische virtuelle Pfade vergeben, und der
Coverage-Audit belegt Bucket-Filterung, Optionsprioritaet, Pfad-Scope, Trunkierung und Leermenge mit
nicht-tautologischen Erwartungen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.TestKit/RoslynTestSolutionFactory.cs`

- **Was:** Den bestehenden Factory-Einstieg schmal um einen optionalen virtuellen Solution-Pfad
  erweitern (Overload oder Parameter-Record, ohne bestehende Aufrufer zu brechen). Wenn ein Pfad
  gesetzt ist, erhalten die `Solution` und alle Dokumente deterministische, normalisierte virtuelle
  Pfade aus Solution-Verzeichnis, Projektname und relativem Dokumentnamen; ohne Pfadangabe bleibt
  das heutige Verhalten unveraendert. Der Factory-Vertrag bleibt rein in-memory: keine Dateien oder
  Verzeichnisse anlegen.
- **Warum:** `DuplicateDetectionEngine` benoetigt valide `Document.FilePath`-Werte und einen
  Solution-Root fuer Scope-/Ausschlusslogik. Die zentrale Factory soll diese wiederkehrende
  Roslyn-Kalibrierung bereitstellen, statt den Legacy-Workspace-Builder in den neuen Testbestand zu
  kopieren.

### Datei 2: `src/AiNetLinter.FastTests/Platform/RoslynTestSolutionFactoryTests.cs`

- **Was:** Einen gezielten Component-Vertrag ergaenzen, der bei gesetztem virtuellem Solution-Pfad
  `Solution.FilePath` und verschachtelte `Document.FilePath`-Werte prueft sowie nachweist, dass auf
  der Platte nichts materialisiert wird. Bestehende Factory-Vertraege unveraendert erhalten.
- **Warum:** Die neue Pfad-Seam ist allgemeine Testplattform-Infrastruktur und braucht einen eigenen
  regressionsfesten Vertrag, bevor Scanner darauf aufbauen.

### Datei 3: `src/AiNetLinter.FastTests/Mcp/Tools/DuplicateDetectionScannerTests.cs` (neu)

- **Was:** Alle sieben Legacy-Vertraege in den passenden Namespace der Fast-Assembly uebernehmen,
  als `Category=Component` markieren und Solutions ausschliesslich ueber
  `RoslynTestSolutionFactory` mit virtuellen Pfaden aufbauen. Den vorhandenen FastTests-`TestHelper`
  fuer die kalibrierten Methoden wiederverwenden; keine neue gemeinsame Helperdatei, keine
  Collection und kein Temp-Verzeichnis einfuehren.
- **Was:** Beim produktseitigen Coverage-Audit die drei tautologischen/zu schwachen Faelle
  kalibrieren: Bucket-Filter muessen einen tatsaechlich vorhandenen, unterhalb des gewaehlten
  Mindest-Buckets liegenden Cluster ausschliessen; `maxResults=1` muss bei mehr als einem
  qualifizierenden Cluster `ShownClusters.Count == 1`, den ungekapp­ten `TotalClusters`-Wert und
  `Truncated == true` beweisen. Zusaetzlich die bestehenden Vertraege fuer Input-vor-Config,
  Forward-Slash-`scopeDir`, exakten Cluster und leere Solution erhalten.
- **Warum:** Damit wird nicht nur der bestehende Test verschoben, sondern die reale Scanner-Grenze
  gegen ihre entscheidenden Branches geprueft und die bisherige falsche `Unit`-/Dateisystem-
  Infrastruktur entfernt.

### Datei 4: `src/AiNetLinter.Tests/Mcp/Tools/DuplicateDetectionScannerTests.cs`

- **Was:** Nach erfolgreichem Alt-/Neu-Vergleich die Legacy-Testklasse physisch loeschen.
- **Warum:** Das Strangler-Ziel erlaubt keine auskommentierte oder doppelt weiterlaufende Alt-Kopie;
  die Kohorte muss am Step-Ende geschlossen migriert sein.

### Datei 5: `tasks/speedup-tests/test-migration-ledger.md`

- **Was:** Den Eintrag `DuplicateDetectionScannerTests` von `pending` auf `migrated` setzen, den
  neuen Abdeckungsort eintragen und die gezielte Verifikationsevidenz aktualisieren.
- **Warum:** Ledger und realer Legacy-/Zielbestand muessen atomar konsistent bleiben.

### Datei 6: `tasks/speedup-tests/codemap.md`

- **Was:** Nach der Umsetzung die reale virtuelle-Pfad-Seam der Factory, den neuen FastTests-
  Scannerort und die obsolet gewordene Legacy-Quelle als Pointer nachfuehren.
- **Warum:** Folgende EPIC-4-Steps muessen die vorhandene Scanner-Teststruktur wiederverwenden und
  duerfen keinen zweiten `AdhocWorkspace`-/Temp-Pfad-Builder einfuehren.

## Tests

- [ ] Vor der Legacy-Loeschung einmalig Baseline:
  `dotnet test src/AiNetLinter.Tests --filter FullyQualifiedName~DuplicateDetectionScannerTests`
- [ ] `dotnet build`
- [ ] Gezielter Fast-/Factory-/Guard-Lauf:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~DuplicateDetectionScannerTests|FullyQualifiedName~RoslynTestSolutionFactoryTests|FullyQualifiedName~FastTestsDependencyGuardTests|FullyQualifiedName~TestCategoryProfileGuardTests"`
- [ ] Gezielter Ledger-/Legacy-Gate-Lauf:
  `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter "FullyQualifiedName~TestMigrationLedgerConsistencyTests|FullyQualifiedName~LegacyProjectBuildGateTests"`
- [ ] Kein `Category!=Stress`-Vollprofil: `step-015` ist keine Epic-Grenze. Kein Stresslauf.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Alle sieben `DuplicateDetectionScannerTests` sind als Component-Vertraege im Fast-Projekt
  vorhanden; kein Vertrag ist stillschweigend entfallen.
- [ ] Die Bucket- und Trunkierungsfaelle besitzen echte positive Ausgangsdaten und koennen bei
  ignorierter Filterung/Kappung rot werden.
- [ ] Die Scanner-Kohorte referenziert weder `AdhocWorkspace`-Eigenbau noch Temp-Dateisystem,
  `SourceFileCatalog.LoadAsync`, MSBuild oder externe Prozesse.
- [ ] Die Legacy-Klasse ist geloescht und ihr Ledger-Eintrag zeigt auf den realen neuen
  Abdeckungsort; Ledger-Guard und Legacy-Build-Gate sind gruen.
- [ ] Build und die unter „Tests" genannten gezielten Filter sind gruen; kein Vollprofil und kein
  Stresslauf wurde fuer diesen Zwischenstep ausgefuehrt.
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch, imperativ, mit
  `[speedup-tests]`-Suffix)
- [ ] `step-015/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte (Produktion)` — `#nullable enable`,
  kleine Methoden/Parameterobjekt bei einer Factory-Erweiterung und keine duplizierten Builder.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4. Updates & Tests` — xUnit-v3-Abdeckung, keine
  zwangsserialisierende Collection ohne reale Exklusivitaet und MCP-/Dogfood-Nachweise nur in der
  C#-Testinfrastruktur.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5. Qualitätsdrift-Prävention` — Assertions nicht
  abschwaechen, Ursache tautologischer Tests beheben und keine Task-/Step-IDs in Codekommentaren.

## Bekannte Ausnahmen

- Keine. `TD-001` betrifft den echten MCP-Framing-Subprozess und liegt ausserhalb dieses
  in-memory Scanner-Steps.

## Notes

- Nicht in Scope: `DuplicateDetectionToolTests`,
  `DuplicateDetectionToolRefactoringDriftTests`, `RefactoringDriftScannerTests`, die beiden
  `DuplicateDetectionEngine*Tests` sowie andere MCP-Scanner/-Tools. Sie bleiben `pending` und werden
  erst in spaeteren JIT-Steps anhand des dann aktuellen Bestands geplant.
- Die virtuelle Pfad-Seam darf keine echten Dateien erzeugen und keine produktive Lade-/Refresh-
  Semantik vortaeuschen. Sie dient nur Roslyn-Komponenten, deren fachlicher Vertrag Pfadwerte
  auswertet, aber keine Platte liest.
- Der Coder wird adaptiv durch den Orchestrator gewaehlt; der Plan trifft dazu keine Vorgabe.
