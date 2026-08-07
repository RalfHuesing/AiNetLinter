---
status: in_progress
type: step-plan
task: flaky-and-test-performance
step: 017
corrects: null
title: "EPIC-04 Fast-Path-Befehl etablieren + Doku"
epic: EPIC-04
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: gemini-3.6-flash
created_at: 2026-08-07T18:15:00+02:00
---

# Step 017: EPIC-04 Fast-Path-Befehl etablieren + Doku

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-04` aus `roadmap.md` — Fast-Path-Befehl etablieren + Doku.
- **Konzept-Referenz:** `konzept.md` §"Wie" Schritt 4, §"Definition of Done" Punkt "dokumentierter Fast-Path-Befehl".

## Intention

Nach Abschluss von EPIC-02 (Kategorisierung aller ~1325 Testmethoden) wird der Fast-Path-Testbefehl `dotnet test --filter Category=Unit` nun verifiziert, zeitmäßig vermessen und im Projekt (z.B. in `AGENTS.md`) dokumentiert.

## Konkrete Änderungen

1. **Verifikation & Leistungsmessung:**
   - Mehrmaliges (mind. 3x) Ausführen von `dotnet test --filter Category=Unit` und Notieren der Ausführungszeiten.
   - Gegenprüfung mit `dotnet test` (Voll-Lauf).

2. **Dokumentation:**
   - Sicherstellen, dass in `AGENTS.md` (und `roadmap.md` Tech-Stack-Notiz) `dotnet test --filter Category=Unit` als primärer Entwicklungs-Fast-Path ausgewiesen ist.

## Definition of Done

- [ ] Fast-Path-Testbefehl `dotnet test --filter Category=Unit` mehrfach erfolgreich und grün ausgeführt.
- [ ] Zeiten im Result-Dokument festgehalten.
- [ ] Dokumentation in `AGENTS.md` und `roadmap.md` überprüft/aktualisiert.
- [ ] `step-017/step-result.md` verfasst.
