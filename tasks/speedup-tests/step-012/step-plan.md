---
status: open
type: step-plan
task: speedup-tests
step: 012
corrects: null
title: "EPIC-3 Teil 3 — Renderer-Kohorte nach AiNetLinter.FastTests migrieren und Unit-Profil verifizieren"
epic: EPIC-3
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: "gpt-5.6-sol Medium"
created_by_model_knowledge_cutoff: "nicht ausgewiesen"
created_at: 2026-08-12
related_to: [step-010, step-011]
---

# Step 012: EPIC-3 Teil 3 — Renderer-Kohorte nach AiNetLinter.FastTests migrieren und Unit-Profil verifizieren

## Bezug

- **Task:** `speedup-tests`
- **Epic:** `EPIC-3` aus `roadmap.md` — nach den `approved`en Checker- und Parser-Teilen aus
  step-010/step-011 ist nur noch die Renderer-Kohorte offen; dieser Step schliesst EPIC-3 bei
  erfolgreichem Review ab.
- **Konzept-Referenz:** `konzept.md` §7 „Sparsame Verifikation waehrend der Umsetzung" und §9
  „Sinnvolle Kohorten" Punkt 2 (reine Checker-/Parser-/Renderer-Tests in
  `AiNetLinter.FastTests`; an der Epic-Grenze das vollstaendige betroffene Profil ausfuehren).

## Aktueller Projektzustand (JIT-Kontext)

- Im Ledger sind fuer EPIC-3 genau die zwei Dateien
  `src/AiNetLinter.Tests/Mcp/Tools/CallTreeMermaidRendererTests.cs` und
  `MetricsTreeRendererTests.cs` noch `pending`. Zusammen enthalten sie acht
  `[Trait("Category", "Unit")]`-Facts. Beide bauen `MetricsTreeNode`-Objekte ausschliesslich im
  Speicher auf und rufen interne statische Renderer auf; sie verwenden weder `TestHelper` noch
  TestKit, `SourceFileCatalog`, MSBuild, Dateisystem, Prozesse oder das echte Repository.
- `AiNetLinter.FastTests` referenziert das Produktprojekt bereits und besitzt seit step-004 die
  erforderliche `InternalsVisibleTo`-Freigabe. Die bestehenden FastTests-Dependency- und
  Kategorienguards werden wiederverwendet; Projektdateien, Fixtures, Helper und Produkt-Seams sind
  fuer diesen Move nicht anzupassen.
- Der produktseitige Coverage-Audit zeigt, dass die vorhandenen Tests Root-/Kantenformatierung,
  Sortierrichtung, Top-N am Root, Einrueckung sowie Mermaid-Escaping abdecken. Der dokumentierte
  rekursive Vertrag „Top-N pro Ebene" beider Renderer ist bislang nur am Root belegt. Je Renderer
  kommt deshalb ein gezielter verschachtelter Vertragsfall hinzu; die produktive Implementierung
  ist bereits rekursiv und soll nicht vorsorglich veraendert werden.
- Im Tech-Debt-Index gibt es keinen Eintrag fuer diese Renderer-Dateien oder denselben Bereich.
  Insbesondere liegen die beiden `auto_fixable: ja`-Eintraege in `.agents/rules/AiNetLinter.mdc`
  beziehungsweise `IntegrationTests/Platform` und werden nicht epic-uebergreifend angehaengt.
- Die CodeMap-Entscheidungen aus step-010/step-011 werden nicht umgedreht: die Renderer werden als
  dritter fachlich geschlossener Teil in denselben FastTests-Strangler-Zielpfad migriert.

## Intention

Nach diesem Step liegen alle reinen Checker-, Parser- und Renderer-Vertraege aus EPIC-3 in der
schnellen Unit-Assembly. Die zwei Legacy-Rendererklassen sind physisch entfernt, das Ledger zeigt
die realen Zielpfade, und zwei neue rekursive Tests schliessen die beim Coverage-Audit gefundene
Top-N-pro-Ebene-Luecke. Als Epic-Grenznachweis laeuft einmal das vollstaendige betroffene Unit-Profil,
nicht das lange Task-End-Gate aller Korrektheits- oder Spezialprofile.

## Konkrete Änderungen

### Verschiebung: `src/AiNetLinter.Tests/Mcp/Tools/CallTreeMermaidRendererTests.cs` → `src/AiNetLinter.FastTests/Mcp/Tools/CallTreeMermaidRendererTests.cs`

- **Was:** Datei mit unveraendertem Namen verschieben und den Namespace von
  `AiNetLinter.Tests.Mcp.Tools` auf `AiNetLinter.FastTests.Mcp.Tools` aendern. Die vier vorhandenen
  Tests, Traits, Helper-Methode und Assertions unveraendert erhalten. Im Ziel einen fuenften Fact
  `Render_NestedTopN_AppliesLimitAtEveryLevel` (oder semantisch gleichwertiger Name) ergaenzen: ein
  sichtbares Kind mit mehr als `topN` Enkeln muss nur die erlaubten Enkel plus korrekt verknuepften
  Overflow-Knoten rendern; der ausgeschlossene Enkel darf nicht vorkommen.
- **Warum:** Die Klasse ist bereits ein reiner Unit-Vertrag. Der zusaetzliche Fall belegt die in
  `CallTreeMermaidRenderer` dokumentierte rekursive Top-N-Kappung statt nur die Root-Ebene.

### Verschiebung: `src/AiNetLinter.Tests/Mcp/Tools/MetricsTreeRendererTests.cs` → `src/AiNetLinter.FastTests/Mcp/Tools/MetricsTreeRendererTests.cs`

- **Was:** Datei mit unveraendertem Namen verschieben und den Namespace von
  `AiNetLinter.Tests.Mcp.Tools` auf `AiNetLinter.FastTests.Mcp.Tools` aendern. Die vier vorhandenen
  Tests und Assertions unveraendert erhalten. Im Ziel einen fuenften Fact
  `Render_NestedTopN_AppliesLimitAtEveryLevel` (oder semantisch gleichwertiger Name) ergaenzen: ein
  verschachtelter Knoten mit mehr als `topN` Kindern muss seine sortierte sichtbare Teilmenge und
  die korrekte `... und N weitere`-Zeile mit passender Einrueckung ausgeben.
- **Warum:** Der rekursive Top-N-Vertrag ist nicht durch den bestehenden Root-Level-Test abgedeckt;
  der neue Fall schliesst diese produktseitig gelesene, nicht-triviale Coverage-Luecke.

### `tasks/speedup-tests/test-migration-ledger.md` — zwei Renderer-Zeilen aktualisieren

- **Was:** `CallTreeMermaidRendererTests` und `MetricsTreeRendererTests` von `pending` auf
  `migrated` setzen, als neuen Abdeckungsort jeweils den existierenden Pfad unter
  `src/AiNetLinter.FastTests/Mcp/Tools/` eintragen und `last_updated` aktualisieren. Die alten
  Quelldateien nach dem Move physisch entfernen; keine Parallelkopien oder Skips belassen.
- **Warum:** Der Ledger-Konsistenzguard verlangt fuer migrierte Eintraege einen existierenden neuen
  Abdeckungsort und fuer `pending`e Eintraege weiterhin eine Legacy-Deklaration.

### `tasks/speedup-tests/codemap.md` — Renderer-Kohorte auf den Zielzustand nachführen

- **Was:** Einen Pointer auf `src/AiNetLinter.FastTests/Mcp/Tools/*RendererTests.cs` als Ziel der
  zwei migrierten Klassen ergaenzen. Den vorhandenen Planning-Pointer auf die beiden Legacy-Dateien
  als durch step-012 obsolet markieren, nicht loeschen; den Produkt-Renderer-Pointer beibehalten.
- **Warum:** Der naechste JIT-Planer soll EPIC-3 als vollstaendig umgesetzt erkennen und direkt den
  tatsaechlichen Einstieg fuer EPIC-4 finden, ohne diese Kohorte erneut zu planen.

## Tests

- [ ] Vor dem Move die Legacy-Vergleichsbasis einmal erfassen:
  `dotnet test src/AiNetLinter.Tests --filter "FullyQualifiedName~CallTreeMermaidRendererTests|FullyQualifiedName~MetricsTreeRendererTests"`
  → acht bestehende Tests gruen. Falls der Move im Working Tree bereits vorliegt, die unveraenderte
  Step-Start-Basis wie in step-011 in einem temporaeren, danach wieder entfernten Worktree messen.
- [ ] `dotnet build src/AiNetLinter.FastTests` → gruen.
- [ ] `dotnet build src/AiNetLinter.Tests` → gruen; das quarantinierte Legacy-Projekt bleibt trotz
  der entfernten Renderer-Dateien kompilierbar.
- [ ] Enger Kohortennachweis nach dem Move:
  `dotnet test src/AiNetLinter.FastTests --no-build --filter "FullyQualifiedName~CallTreeMermaidRendererTests|FullyQualifiedName~MetricsTreeRendererTests"`
  → zehn Tests gruen (acht migrierte Bestandsvertraege plus zwei neue rekursive Coverage-Faelle).
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter FullyQualifiedName~TestMigrationLedgerConsistencyTests`
  → alle Ledger-Konsistenzregeln gruen.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --no-build --filter FullyQualifiedName~LegacyProjectBuildGateTests`
  → gruen; weitere `pending`-Eintraege halten das Legacy-Projekt in Solution und Build-Gate.
- [ ] **EPIC-3-Grenzgate:**
  `dotnet test src/AiNetLinter.FastTests --no-build --filter Category=Unit` → das vollstaendige bis
  hier betroffene Unit-Profil einschliesslich Dependency- und Kategorienguards gruen.

Kein `Category!=Stress`-Volllauf beider Zielprojekte und keine Dogfood-/Performance-/Stress-Profile
in diesem Step: `konzept.md` §7 verlangt an einer Epic-Grenze nur das bis dahin betroffene Profil;
AGENTS.md verlangt die zwei vollstaendigen `Category!=Stress`-Gates erst vor Task-Beendigung, und
das Task-Ende liegt in EPIC-7.

## Definition of Done

- [ ] Alle „Konkrete Änderungen" umgesetzt
- [ ] Die beiden gezielten Projekt-Builds, die engen Kohorten-/Guard-Läufe und das Unit-Profilgate
  aus „Tests" sind gruen
- [ ] Acht bestehende Renderer-Testfaelle bleiben erhalten und zwei rekursive Top-N-Faelle kommen
  hinzu; keine der beiden Legacy-Testklassen existiert parallel zur FastTests-Zielklasse
- [ ] Ledger und CodeMap bilden den realen Zielzustand ab
- [ ] Commit auf aktuellem Branch (Conventional Commit auf Deutsch mit Suffix `[speedup-tests]`)
- [ ] `step-012/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` „architecture", „test-coverage" und „Projekt-Overrides" — der
  Namespace muss dem neuen Zielordner entsprechen, die Renderer-Vertraege muessen nach der
  Migration weiter ein Abdeckungssignal liefern, und fuer das Testprojekt gelten die definierten
  Testgrenzen.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 „Windows-Umgebung & Tool-Regeln", §4 „Updates &
  Tests" und §5 „Qualitaetsdrift-Praevention" — Windows-kompatible Befehle, keine kuenstliche
  Collection-Serialisierung, keine abgeschwaechten Assertions und keine Task-/Step-IDs in
  C#-Kommentaren.

## Bekannte Ausnahmen

Keine.

## Notes

- Produktklassen und Projektdateien sind Non-Goals. Schlaegt einer der neuen rekursiven Tests
  unerwartet fehl, zuerst den Renderer-Vertrag gegen den gelesenen Produktcode klaeren und die
  kleinste ursachengerechte Aenderung dokumentieren; keine angrenzenden MCP-Tooltests vorziehen.
- Die vorhandenen Kommentare in `CallTreeMermaidRenderer.cs` und `MetricsTreeRenderer.cs` sind
  ID-frei und beschreiben den Formatvertrag. Fuer die neuen Tests sind keine C#-Kommentare noetig.
- EPIC-4 (Filter-/Scanner-/Tool-Kohorte und objektbasierte Seams) bleibt vollstaendig ausserhalb
  dieses Steps; insbesondere wird TD-005 zur In-Memory-Testprojekterkennung nicht nebenbei behoben.
