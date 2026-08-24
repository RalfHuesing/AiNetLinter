---
status: open
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 015
corrects: null
title: "Task-weites Drift-Audit: Duplicates, Magic Values, Dead Code — prüfen und bereinigen"
epic: EPIC-B
estimated_risk: medium
step_type: single
items: []
created_by: orchestrator
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T14:20:00+02:00
related_to:
  - step-013/step-result.md
  - step-014/step-review.md
  - tech-debt.md
---

# Step 015: Task-weites Drift-Audit — Duplicates, Magic Values, Dead Code

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** EPIC-B (Abschlusspflege) — explizite Nutzervorgabe vom 2026-08-24:
  ein Task-weiter Tech-Debt-Audit (`find_duplicates`, `find_magic_values`,
  `find_dead_code`) mit anschließender Bereinigung, über den AiNetLinter-MCP-
  Server. EPIC-A hatte seinen Audit in step-008; EPIC-B bisher nur den
  einmaligen `find_duplicates`-Lauf in step-013 — die beiden anderen Werkzeuge
  laufen für dieses Epic hier erstmals.
- **Konzept-Referenz:** Kein neues Konzept-Kapitel — Qualitätsdrift-Prävention
  gemäß `.agents/rules/AiNetLinterRichtlinien.mdc` §5.

## Aktueller Projektzustand (JIT-Kontext)

- Beide Epics sind fachlich `done` und approved (step-008, step-014); der
  Baum ist grün: Build 0/0, FastTests 1726/1726, IntegrationTests isoliert
  vollständig grün.
- `tech-debt.md` führt TD-001 bis TD-008; TD-002-ähnlicher Test-Helper-Overlap
  wurde in step-013 bereits als bewusster No-op dokumentiert, TD-007/TD-008
  sind Abdeckungsasymmetrie bzw. Suite-Kontamination (beides kein Audit-Gegenstand,
  aber bei Funden zu berücksichtigen, die dieselben Stellen betreffen).
- Der Coder von step-014 konsolidierte beim MCP-Gate eine doppelte Frame-Hilfsklasse;
  das ist der letzte bekannte Duplicate-Fund im Produktionscode.

## Intention

Mit den drei Drift-Werkzeugen einen vollständigen, taskweiten Snapshot über
`src/` ziehen, ergiebige Produktionsfunde sofort bereinigen und alles Übrige
nachvollziehbar als Tech-Debt oder bewussten No-op dokumentieren. Nach diesem
Step ist die Qualitätsdrift-Bilanz des Tasks abgeschlossen; das globale Review
kann auf einem sauberen Stand laufen.

## Konkrete Änderungen

1. **Audit-Läufe (AiNetLinter-MCP-Server, Scope `src`, Produktionscode zuerst):**
   - `find_duplicates` (Parameter wie step-013: `minTokens=20`,
     `similarityThreshold="exact"`, `mode="clone"`) — EPIC-B-Rerun ist laut
     Nutzervorgabe ausdrücklich gewollt (taskweit statt nur einmalig).
   - `find_magic_values` über `src/AiNetLinter` (Produktion) und stichprobenartig
     über die Daemon-/Registry-Testordner.
   - `find_dead_code` über `src/AiNetLinter`.
   - Falls der Server im Projektmodus „lädt noch“ antwortet: zuerst
     `get_server_health`, nicht blind wiederholen (Richtlinien §1).
2. **Bewertung je Fund:** Produktionscode mit echtem Bereinigungsgewinn → fixen
   (DRY-Konsolidierung, benannte Konstante, Entfernung toten Codes). Test-Code:
   Helper-Überlappungen und bewusst redundante Contract-Tests sind kein Makel —
   dokumentieren statt mechanisch deduplizieren. Alles Nicht-Fixbare mit
   Begründung in `tech-debt.md` (fortlaufende TD-ID) oder als bewusster No-op
   im Step-Result.
3. **Bereinigung umsetzen:** Fixes ausschließlich für Funde aus den drei
   Läufen; keine Gelegenheits-Refactorings außerhalb der Fundliste. Verhalten
   bleibt unverändert (reine Struktur-/Konstanten-/Totentcode-Maßnahmen).
4. **Keine Doku-Sync-Pflicht** außer Codemap-Zeile falls sich Modulbestand
   ändert (Dateien gelöscht/hinzugefügt).

## Tests

- [ ] Build grün nach allen Bereinigungen (0 Warnungen, 0 Fehler)
- [ ] Vollständiger Nicht-Stress-Stack GENAU EINMAL vor Step-Abschluss:
      FastTests `Category!=Stress` und IntegrationTests `Category!=Stress`
- [ ] Falls reine Konstanten-/Dead-Code-Fixes: betroffene Unit-Klassen gezielt
      gefiltert zusätzlich zum Schlusslauf
- [ ] `Category=Stress` niemals ausführen

## Definition of Done

- [ ] Alle drei Werkzeuge ausgeführt und Ergebnisse im Step-Result dokumentiert
      (je Werkzeug: Fundanzahl, bewertete Funde, Entscheidung fix/TD/No-op)
- [ ] Ergiebige Produktionsfunde bereinigt; Verhalten unverändert
- [ ] Neue TD-Einträge mit Indexzeile + Volltext in `tech-debt.md`
- [ ] MCP-Quality-Gates vor jedem Commit (`get_violations`, `safeguard`)
- [ ] Build + beide Suiten ohne Stress grün (genau ein Vollstack)
- [ ] Commit(s) Conventional Commit, Deutsch, imperativ,
      Suffix `[11_epic-projektregistry-und-daemon]`
- [ ] `step-015/step-result.md` geschrieben
- [ ] `status` in diesem Plan auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §5 (Qualitätsdrift-Prävention:
  DRY/Magic Values/Dead Code, Zero-Warning), §3 (Testkategorien), §1
  (MCP-first, get_server_health bei „lädt noch")
- `.agents/rules/AiNetLinter.mdc` (aktive Grenzwerte bleiben maßgeblich)

## Bekannte Ausnahmen

- `ProjectRegistryTests.Lease_AtomicLookupAndReservation_CreatesAndDisposesOnlyTheWinner`
  bleibt der bekannte timingabhängige Volllast-Test (isolat grün; im Result
  dokumentieren, nicht jagen).
- Suite-Kontamination durch überlebende Fremd-Daemons = TD-008; einzelne
  Kontaminationsausfälle im Vollstack werden wie in step-014 klassifiziert
  (isolierter Nachlauf entscheidet), nicht als Regression gewertet.

## Notes

- Dies ist KEIN Korrektur-Step (`corrects: null`) — eigenständiger Pflege-Step
  auf Nutzeranweisung, bevor das globale Review startet.
- TD-007/TD-008 werden hier NICHT bearbeitet (Nutzerentscheid vorbehalten);
  nur wenn ein Audit-Fund dieselbe Stelle betrifft, darf die Lösung ihn mitabdecken.
- Der Kritiker prüft diesen Step normal über alle vier Ebenen; sein Fokus wird
  auf „Fundliste vollständig? Fix-Triage begründet? Verhalten unverändert?" liegen.
