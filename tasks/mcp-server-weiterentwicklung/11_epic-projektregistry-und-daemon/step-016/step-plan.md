---
status: done
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 016
corrects: null
title: "Tech-Debt-Pflegepaket: TD-008, TD-001, TD-003, TD-010 fixen; TD-004 als Akzeptanz verankern"
epic: EPIC-B
estimated_risk: medium
step_type: batch
items:
  - id: item-01
    title: "TD-008: Suite-weites Daemon-Endpoint-Cleanup/Gating-Fixture"
    source: "tech-debt.md#TD-008"
  - id: item-02
    title: "TD-001: Defekte rules.json im Registry-Pfad → deterministischer Fehlervertrag"
    source: "tech-debt.md#TD-001"
  - id: item-03
    title: "TD-003: Loader-cwd-Fallback schließen (Defense-in-Depth-Guard)"
    source: "tech-debt.md#TD-003"
  - id: item-04
    title: "TD-010: Stale Doku-Zeile AMBIGUOUS_SOLUTION streichen"
    source: "tech-debt.md#TD-010"
  - id: item-05
    title: "TD-004: Nutzerentscheid „Überlauf erlaubt" — Verhalten dokumentieren + Vertragstest"
    source: "tech-debt.md#TD-004"
created_by: orchestrator
created_by_model: stealth/ox-alpha (openrouter)
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-24T18:05:00+02:00
related_to:
  - tech-debt.md
  - task-summary.md
---

# Step 016: Tech-Debt-Pflegepaket nach Task-Abschluss

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon` — Task ist seit dem globalen
  Review (`done`, `25032eaa`) abgeschlossen. Dieser Step ist ein vom Nutzer
  ausdrücklich beauftragtes Pflegepaket auf Basis der Tech-Debt-Entscheidungen
  vom 2026-08-24; er erweitert den Fachumfang NICHT.
- **Konzept-Referenz:** Kein neues Konzept-Kapitel. TD-001/TD-003 dienen der
  Determinismus-Philosophie („kein Rumraten", Richtlinien), TD-004 setzt eine
  explizite Kapazitätsentscheidung des Nutzers um.

## Aktueller Projektzustand (JIT-Kontext)

- Build/Teststand: FastTests 1726/1726, IntegrationTests 357/359 (2 Ausfälle =
  TD-008-Kontamination, isoliert grün) auf Commit `7a1431d9`.
- TD-008-Ursache empirisch belegt: überlebende Diagnose-/Fremd-Daemons halten
  den benutzergebundenen Pipe-Endpunkt `ainetlinter.analyzer.v1.<username>`
  fest; parallele Integrationsläufe kollidieren dann in
  `AcquireEndpointAsync`/Doppelstart-Guards.
- TD-001-Betroffene Stelle: `ProjectInstanceFactory`/`ConfigLoader` — defekte
  (lesbare, aber ungültige) `rules.json` fällt im Registry-Pfad stumm auf
  Defaults zurück statt mit deterministischem Fehlervertrag zu scheitern.
- TD-003-Stelle: `ProjectDefinitionLoader.Load` (`ProjectDefinitionLoader.cs:20`)
  kombiniert `projectRoot ?? string.Empty` mit dem Definitionsdateinamen —
  bei `null`/leer wird cwd-relativ aufgelöst. Der Produktionsaufrufer
  (`ProjectRegistry.cs:432`) kanonisiert zwar via `Path.GetFullPath`, der
  Loader selbst hat aber keinen Guard.
- TD-010-Stelle: `Docs/agent-api.md:834` beschreibt `AMBIGUOUS_SOLUTION` als
  aktiv; ein Emitter existiert nicht mehr (seit EPIC-A-Wiring).
- TD-004-Faktenlage: Bei nur-busy Registern wächst der Bestand über
  `MaxProjects`; der TTL-Tick reklamiert den Überhang nicht. Nutzerentscheid
  (2026-08-24): **Überlauf ist erlaubte, gewollte Semantik** — kein Fix am
  Verhalten, aber Dokumentation + fester Contract-Test.

## Intention

Die vier zur Bereinigung freigegebenen Tech-Debt-Einträge schließen und die
TD-004-Entscheidung unverlierbar machen. Danach sind alle Einträge der
Priorität `mittel` erledigt oder entschieden; verbleiben nur bewusst
getragene `niedrig`-Einträge (TD-002/005/006/007/009).

## Konkrete Änderungen

### item-01 — TD-008: Endpoint-Cleanup/Gating-Fixture

- **Was:** Eine wiederverwendbare xUnit-Fixture (IntegrationTests), die pro
  Testlauf sicherstellt, dass keine fremden AiNetLinter-Daemons den Endpunkt
  halten: vor Suite-/Klassenstart gezielt aufräumen (nur Prozesse der eigenen
  EXE/Pipe identifizieren und sauber beenden — NIEMALS blinde
  Namens-Matches auf fremde Prozesse) bzw. Kollision transparent als Skip
  mit Begründung melden. Die bestehende step-014-Lösung (env-Pinning +
  kurzer Idle-Exit für bewusste Daemon-Läufe) bleibt unangetastet — die
  Fixture ergänzt das suite-weite Sicherheitsnetz.
- **Warum:** Belegt Ursache der beiden Flaky-Ausfälle; macht Vollstackläufe
  wieder verlässlich grün ohne Collection-Serialisierung.

### item-02 — TD-001: deterministischer Fehlervertrag für defekte rules.json

- **Was:** Im Registry-Load-Pfad eine lesbare, aber gegen das Schema verstoßende
  `rules.json` nicht mehr stumm auf Defaults zurückfallen lassen, sondern mit
  einem klaren Fehlercode (im Stil der bestehenden
  `PROJECT_DEFINITION_INVALID`-Familie, z. B. eigener Code für
  „rules.json ungültig") + kopierfähiger Bauanleitung scheitern. Batch-Pfad
  unverändert lassen, sofern dort bereits ein harter Vertrag gilt.
- **Warum:** Determinismus-Vorgabe des Nutzers: kein stilles Zurückraten.

### item-03 — TD-003: Loader-cwd-Fallback schließen

- **Was:** In `ProjectDefinitionLoader.Load` einen Guard voranstellen:
  `projectRoot` null/whitespace → sofortige `Failure(PROJECT_ROOT_REQUIRED …)`
  mit wörtlichem Self-Service-Template (Stil wie die bestehenden
  Root-Fehlerverträge). Keine Verhaltensänderung für gültige absolute Roots.
- **Warum:** Ankerregel (Auflösung nie relativ zum cwd) auch auf Loader-Ebene
  erzwingen, unabhängig von Aufruferdisziplin.

### item-04 — TD-010: Stale Doku-Zeile entfernen

- **Was:** Die `AMBIGUOUS_SOLUTION`-Zeile in `Docs/agent-api.md:834` streichen;
  prüfen, ob die umliegende Fehlertabelle sonst noch einen toten Code nennt
  (wenn ja: ebenfalls entfernen und im Result nennen).
- **Warum:** Doku-Objektivität §1; trivial.

### item-05 — TD-004: Überlauf-Semantik festnageln

- **Was:** KEINE Verhaltensänderung. (a) Kommentar/XML-Doc an der betroffenen
  Stelle (`ProjectRegistry` Soft-Cap/Eviction): Überlauf bei nur-busy
  Registern ist laut Nutzerentscheid 2026-08-24 gewollt; der TTL-Tick
  räumt erst, wenn Slots frei werden. (b) Ein Contract-Test fixiert das
  Verhalten: alle Slots busy → neuer Key wird trotzdem registriert (Bestand
  > MaxProjects möglich); sobald ein Slot freiwird, greift Eviction wieder.
  (c) Status von TD-004 in `tech-debt.md` auf „erledigt (Akzeptanz,
  Nutzerentscheid 2026-08-24)" mit Verweis auf den Step.
- **Warum:** Entscheidung darf später nicht als Bug „repariert" werden.

## Tests

- [ ] item-01: Fixture greift (Simulation eines hängenden Daemons blockiert
      den Lauf nicht dauerhaft; reguläre Läufe unverändert)
- [ ] item-02: defekte rules.json → deterministischer Fehlercode + Template,
      kein Default-Fallback (Unit)
- [ ] item-03: Load(null)/Load(\"\")/Load(\"   \") → PROJECT_ROOT_REQUIRED (Unit)
- [ ] item-04: kein Test (reine Doku)
- [ ] item-05: Überlauf-Contract-Test (Unit, injizierbare Clock wie bestehende
      Eviction-Tests)
- [ ] Vollständiger Nicht-Stress-Stack GENAU EINMAL vor Abschluss; Entwicklung
      gefiltert; `Category=Stress` niemals

## Definition of Done

- [ ] Alle fünf Items umgesetzt; Verhalten nur wo vorgesehen geändert
      (item-02/item-03 = neue Fehlerverträge, item-01 = Testinfrastruktur,
      item-05 = Dokumentation/Tests)
- [ ] Build 0 Warnungen / 0 Fehler; beide Suiten ohne Stress grün — mit der
      TD-008-Fixture dürfen die bisherigen Kontaminationsausfälle nicht mehr
      auftreten; falls doch: isoliert klassifizieren und im Result erklären
- [ ] MCP-Quality-Gates vor jedem Commit (`get_violations`, `safeguard`);
      falls MCP-Tools nicht exponiert: stdio-JSON-RPC-Session gegen die
      gebaute EXE wie in step-014/015 — keine erfundenen Toolergebnisse
- [ ] `tech-debt.md`: TD-001/003/008/010 auf `Status: erledigt` (Verweis auf
      step-016), TD-004 auf „erledigt (Akzeptanz)“ — Indexzeilen entsprechend
- [ ] Commit(s) Conventional Commit, Deutsch, imperativ,
      Suffix `[11_epic-projektregistry-und-daemon]`
- [ ] `step-016/step-result.md` geschrieben
- [ ] `status` in diesem Plan auf `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §1 (MCP-first, Doku-Objektivität),
  §3 (Testkategorien/TRX), §5 (Zero-Warning, kein Symptom-Fixing)
- `.agents/rules/AiNetLinter.mdc` (Grenzwerte: sealed, Footprint, Options-Records)

## Bekannte Ausnahmen

- Falls trotz item-01 einzelne Kontaminationen bleiben (z. B. Fremdprozesse
  außerhalb der eigenen EXE-Identifikation): nicht kaschieren — isoliert
  klassifizieren und im Result mit Ursache dokumentieren.

## Notes

- item-02 sorgfältig gegen das BESTEHende ConfigLoader-Verhalten abgrenzen:
  Nur der stille Default-Fallback im Registry-Pfad ist das Problem; wo bereits
  harte Fehler gelten, nichts doppelt bauen.
- item-05 bewusst ohne Verhaltensänderung — der Wert liegt in Doc+Test+Status,
  nicht in neuem Code.
- Kein Drift-Audit (step-015 war der taskweite Audit-Lauf). Stress nie.
