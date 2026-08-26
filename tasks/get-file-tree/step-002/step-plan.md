---
status: open
type: step-plan
task: get-file-tree
step: 002
corrects: null
title: "Veraltete Hotspots-Erwartungen auf sechs Fixture-Dokumente ausrichten"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: GPT-5 (Codex)
created_by_model_knowledge_cutoff: nicht im Systemkontext angegeben
created_at: 2026-08-26T22:38:10+02:00
related_to: [step-001]
---

# Step 002: Veraltete Hotspots-Erwartungen auf sechs Fixture-Dokumente ausrichten

## Bezug

- **Task:** `get-file-tree`
- **Epic:** `EPIC-01` aus `roadmap.md` — die Verifikation des bereits
  implementierten projektgebundenen Zugriffs wird durch einen veralteten
  Fixture-Zähler blockiert.
- **Konzept-Referenz:** `Konzept.md`, „Tests und Verifikation der späteren
  Implementierung“ sowie „Definition of Done / Erfolgskriterien“ — bestehende
  Verifikation darf den gültigen aktuellen Projektbestand nicht widersprechen.

## Aktueller Projektzustand (JIT-Kontext)

- `SymbolGraphMiniSolutionSpec.Documents` in
  `src/AiNetLinter.FastTests/Fixtures/SymbolGraphMiniSolutionSpec.cs:17-107`
  enthält sechs Dokumente; `Records.cs` mit `GreetingRecord` ist seit Commit
  `1ee21426` ausdrücklich gewünschter Bestand und bleibt unverändert.
- Die beiden Hotspots-Tests
  `ExecuteAsync_ScopeFilterMatchesProjectName_ReturnsAllFiles` und
  `ExecuteAsync_ScopeFilterWithForwardSlashPath_MatchesFiles` in
  `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetHotspotsToolTests.cs`
  erwarten in den Assertions an den Zeilen 111 und 123 noch den veralteten
  Text `Gescannt: 5 .cs-Dateien`.
- Der gezielte Lauf dieser beiden Tests bestätigt den reinen
  Erwartungsmismatch: Die Implementierung liefert jeweils
  `Gescannt: 6 .cs-Dateien`; beide und nur diese beiden Tests schlagen in dem
  angefragten Slice fehl.

## Intention

Die zwei exakten Hotspots-Erwartungen werden auf den bewusst gültigen
Fixture-Bestand von sechs C#-Dokumenten aktualisiert. Der Step verändert weder
die Record-Funktionalität noch den Fixture-Inhalt, lockert keine Tests allgemein
und ändert keine Produktionslogik; danach kann der vorgeschriebene vollständige
Fast-/Integration-Gate erneut gegen den aktuellen Bestand laufen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter.FastTests/Mcp/Tools/FileStructure/GetHotspotsToolTests.cs` (Zeilen 103-124)

- **Was:** In `ExecuteAsync_ScopeFilterMatchesProjectName_ReturnsAllFiles`
  und `ExecuteAsync_ScopeFilterWithForwardSlashPath_MatchesFiles` jeweils nur
  die exakte erwartete Dokumentzahl im Text von `5` auf `6` ändern.
- **Warum:** Beide Tests prüfen weiterhin die vollständige Scope-Erfassung und
  die Forward-Slash-Normalisierung; `6` ist die konkrete Anzahl des aktuellen,
  absichtlich um `Records.cs` erweiterten Fixtures. Keine Assertion wird
  entfernt, verallgemeinert oder abgeschwächt.

## Tests

- [ ] Gezielter `GetHotspotsToolTests`-Lauf für beide Scope-Tests ist grün.
- [ ] `dotnet build` ist grün.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` ist grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
  ist grün.

## Definition of Done

- [ ] Beide und nur die zwei veralteten Dokumentzahl-Erwartungen sind auf `6`
  ausgerichtet.
- [ ] `Records.cs`, der Record-Filter und die Produktionslogik bleiben
  unverändert.
- [ ] Es gibt keine allgemeine Testlockerung und keine Scope-Ausweitung.
- [ ] Build- und vollständige Nicht-Stress-Gates aus `roadmap.md` sind grün.
- [ ] Commit auf dem aktuellen Branch mit deutschem Conventional-Commit und
  Task-Suffix `[get-file-tree]`.
- [ ] `step-002/step-result.md` ist geschrieben und der Step nach Review
  abgeschlossen.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#4 Updates & Tests` — xUnit-v3-
  Verifikation und vollständige Gate-Befehle einhalten.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5 Qualitätsdrift-Prävention` — den
  konkreten Ursache-/Bestandsabgleich beheben, ohne Assertions auszukommentieren
  oder allgemein abzuschwächen.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc#Verbindliche Priorität` — die
  semantische Fixture-/Test-Einordnung erfolgte zuerst über den AiNetLinter-MCP;
  konkrete Text-/Zeilenänderungen bleiben auf die genannte C#-Datei begrenzt.

## Bekannte Ausnahmen

Keine.

## Notes

- Dieser Step ist kein Kritiker-Korrektur-Step: Step 001 besitzt noch kein
  `step-review.md` mit einem `issues`-Verdict. Die Nutzerklärung des
  Inhalts-Blockers begründet deshalb den regulären Folgeschritt mit
  `related_to: [step-001]`; `corrects` bleibt `null`.
- `roadmap.md` wird nicht geändert. Der Abgleich betrifft ausschließlich eine
  veraltete Testannahme als Voraussetzung für die weitere Verifikation und
  kein neues oder abgeschlossenes Epic.
