---
status: done (pending audit)
type: step-plan
task: 03_get-impact-zum-diff-kontext-erweitern
step: 006
corrects: step-004
epic: EPIC-3+EPIC-4
estimated_risk: low
step_type: single
items: []
created_by: orchestrator
created_by_model: stealth/ox-alpha
created_by_model_knowledge_cutoff: unbekannt
created_at: 2026-08-23T00:05:00+02:00
related_to: [step-004]
---

# Step 006: Korrektur step-004 — Quoting des Mehrklassen-Filters

> Mechanisches Transkript des Findings aus `step-004/step-review.md` (Verdict
> `issues`, genau ein MAJOR-Finding, Datei+Zeile-genau mit Fix-Anweisung —
> Planer-Skip gemäß spec §6.2.1). Keine Ergänzungen über das Finding hinaus.

## Finding (Quelle)

`src/AiNetLinter/Mcp/Tools/TestContext/TestRecommendationBuilder.cs:62-65`
— [MAJOR] [Logik]: Der deduplizierte Mehrklassen-Befehl (`--filter A|B`)
enthält einen unquotierten `|` → bricht als Shell-Zeile in cmd/PowerShell/bash.
Widerspricht dem eigenen XML-Doc („direkt ausführbare Befehle") und der
Tool-Description („kopierbare Filterbefehle"); trifft genau den neuen
Dedup-Normalfall, Einzelklassenfall korrekt.

**Fix-Anweisung aus dem Review (1:1):**
Filterwert nur bei >1 Klasse in doppelte Anführungszeichen (Bestands-Asserts
`GetTestContextToolTests.cs:162,347` bleiben dann unangetastet grün),
Erwartungs-String in `TestCoverageBatchScannerTests.cs:105-109` anpassen.

## Konkrete Änderungen

### Datei 1: `src/AiNetLinter/Mcp/Tools/TestContext/TestRecommendationBuilder.cs` (Zeile 62-65)

- **Was:** Beim Zusammenbau des Filterwerts prüfen, ob die Vereinigung mehr als
  eine Trefferklasse umfasst. Ja → Filterausdruck in doppelte Anführungszeichen
  setzen (`dotnet test ... --filter "FullyQualifiedName~A|FullyQualifiedName~B"`).
  Nein → Ausgabe exakt wie bisher (eine Klasse, keine Quotes), damit die
  Bestands-Asserts in `GetTestContextToolTests.cs:162,347` unverändert grün
  bleiben.
- **Warum:** Review-Finding MAJOR/Logik — Befehl muss direkt ausführbar sein.

### Datei 2: `src/AiNetLinter.FastTests/Core/TestCoverageBatchScannerTests.cs` (Zeile 105-109)

- **Was:** Erwartungs-String des Mehrklassen-Befehls an das gequotete Format
  anpassen (nur diese Assertion).
- **Warum:** Review-Anweisung; sonst keine Teständerungen.

## Tests

- [ ] `TestCoverageBatchScannerTests` Mehrklassenfall: Befehl mit Quotes, assertiert direkte Ausführbarkeit (kein unquoting Pipe-Leak)
- [ ] Bestands-Tests `GetTestContextToolTests` (Z.162/Z.347-Erwartungen) bleiben UNVERÄNDERT grün
- [ ] Volles Gate: `dotnet build` + beide `Category!=Stress`-Läufe grün

## Definition of Done

- [ ] Finding behoben, keine weiteren Änderungen (Scope = Finding)
- [ ] Build + beide Gates grün
- [ ] Code-Commit (`fix:` … `[03_get-impact-zum-diff-kontext-erweitern]`, Refs step-005)
- [ ] `step-005/step-result.md`; Status→`done (pending audit)`
- [ ] CodeMap nur falls berührter Bereich neu/verändert (hier: Eintrag existiert bereits — Prüfung genügt)

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc#5-qualitätsdrift-prävention` — Symptom-Fixing-Verbot (Ursache im Builder, nicht im Test wegradieren)
- `.agents/rules/AiNetLinter.mdc#grenzwerte-produktion` — unverändert einhalten

## Notes

- Kettenbudget: erste Korrektur in dieser Kette (`corrects: step-004`), Budget 3.
- Der Rest von step-004 ist laut Review approved-würdig; nach diesem Fix gilt
  der Step zusammen mit der Korrektur als geschlossen (spec §6.2.1).
