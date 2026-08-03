---
status: done (pending audit)
type: step-plan
task: codegraph-mcp-finish
step: 007
title: "Einheit-011-Abschluss: Prozess-/Volllauf-Verifikation + Kritiker-Review der 6 lokalen Commits nachholen"
epic: EPIC-02
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: []
---

# Step 007: Einheit-011-Abschluss (Muss-Haben A) — Verifikation + nachgeholtes Kritiker-Review

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-02` aus `roadmap.md` — Einheit-011-Abschluss (Muss-Haben
  A): offene Prozesse bereinigen, Volllauf frisch fahren, Kritiker-Review
  für die 6 lokalen 011-Commits nachholen. **Der Push-Teil des Epics ist
  bereits erledigt** (siehe „Aktueller Projektzustand" unten) — dieser
  Step deckt nur noch Prozess-Check + frischer Volllauf + das
  nachgeholte Review ab.
- **Konzept-Referenz:** `Konzept.md` „Scope > Muss-Haben A" (Zeilen
  167-183), „Bereits umgesetzt" Abschnitt „Codiert, aber nicht
  reviewt/nicht gepusht — Einheit 011" (Zeilen 96-124), „Entdeckte
  Mängel/Redundanzen" Abschnitt „Lokaler Build aktuell rot" (Zeilen
  559-577).

## Aktueller Projektzustand (JIT-Kontext)

**Wichtigster Fund dieses Planungslaufs — weicht vom Stand in
`Konzept.md` ab:**

- `Konzept.md` beschreibt „11 Commits lokal ohne Push" (8
  `codegraph-mcp-server`-Einheiten 009-011, 2 externe
  `.agents/Agent-Scaffolding`-Squash-Merges, 1 `docs(rules)`-Commit) als
  offenen Zustand, `git log -1` = `59c2f5e`. **Das ist nicht mehr
  aktuell.** Verifiziert per `git merge-base --is-ancestor`: alle 6
  Einheit-011-Commits (`4bcd5ab`, `075a8a0`, `af41a6b`, `1201840`,
  `a530b4f`, `8a663c7`) sind bereits **Vorfahren von `origin/main`** —
  sie wurden zu einem nicht mehr rekonstruierbaren Zeitpunkt (vermutlich
  beim Push von step-001..004 dieses Tasks, da `origin/main` aktuell bei
  `877bef9` = step-004-Review-Commit steht) bereits gepusht, **ohne dass
  ein formales Kritiker-Review für diese 6 Commits stattgefunden hat.**
  Der Push ist damit faktisch bereits geschehen — er kann nicht
  rückgängig gemacht werden, ohne `origin/main` zu verändern (destruktiv,
  außerhalb dieses Workflows). Konsequenz für diesen Step: **kein
  Push-Schritt mehr nötig oder möglich** — die einzige noch offene
  Handlung aus Muss-Haben A ist das **nachgeholte Kritiker-Review** der
  bereits gepushten Commits, plus die davor liegende Verifikation
  (Prozess-Bereinigung, frischer Volllauf).
- **Ordnung wichtig für den Orchestrator:** Aktuell ist der lokale
  `main`-Branch 11 Commits vor `origin/main` (alles Task-Doku- und
  Refactor-Commits aus step-005/step-006 dieses Tasks, nicht Teil von
  Einheit 011). Diese 11 Commits sind ein **separates** Push-Thema, nicht
  Scope von `EPIC-02` — falls/wenn sie gepusht werden sollen, ist das
  gemäß der Session-Sicherheitsregel eine Aktion, die eine explizite
  Nutzer-Bestätigung braucht (nicht automatisch vom Coder auszuführen).
  Dieser Step-Plan enthält **keinen** Push-Auftrag.
- **Prozess-Check:** `tasklist` zum Planungszeitpunkt zeigt **keine**
  laufenden `AiNetLinter.exe`-/`testhost.exe`-Prozesse — der in
  `Konzept.md` beschriebene Sperr-Zustand (PID 35664/35908) ist bereits
  historisch. Trotzdem bleibt die Prüfung Teil dieses Steps (Tech-Stack-
  Notiz-Konvention, und step-006 hat gezeigt, dass ein hängender
  `testhost.exe` auch zwischen zwei Läufen derselben Session neu
  entstehen kann).
- **Volllauf-Stand:** step-006 (approved, selbes Datum) hat bereits zwei
  frische Volllauf-Messungen dokumentiert (1186 Tests, 0 Fehler, ~1 m
  35-40 s) — das war aber ein reiner F.6-Messschritt, kein inhaltliches
  Review der Einheit-011-Änderungen. Für Muss-Haben A wird in diesem
  Step trotzdem ein eigener, dediziert für dieses Review dokumentierter
  Lauf verlangt (Build + Volllauf), damit der Kritiker eine für **dieses**
  Review gültige, selbst nachvollzogene Grün-Bestätigung hat statt sich
  auf einen fremden Step zu verlassen.
- **`rules.json`-`PathOverride`-Stand:** aktuell **14** Einträge mit
  `MaxAIContextFootprint: 2700` (per `grep -c` verifiziert), nicht 13 wie
  in `Konzept.md` Zeile 272 angegeben. Die Differenz ist für dieses
  Review zu klären (z. B. ob ein 14. Eintrag durch einen späteren,
  unabhängigen Commit hinzukam) — reine Beobachtung für den Kritiker,
  kein Blocker für diesen Step, aber im Review explizit zu vermerken.
  Der strukturelle Fix (Reduktion der Liste) ist **nicht** Scope dieses
  Steps, sondern `EPIC-03` (`ILinterEngineConfig`-Refactor).
- **Kein `units/011/plan.md`/`result.md` mehr verfügbar** — der
  Quell-Ordner `tasks/codegraph-mcp-server/` wurde bereits vor
  Task-Beginn gelöscht (verifiziert: `tasks/` enthält nur noch
  `codegraph-mcp-finish/` und `test-optimierung/`). Die einzige
  Referenzquelle für „was war geplant" ist `Konzept.md` selbst
  (Zeilen 96-124) plus die Commit-Diffs/-Messages der 6 Commits.
- **Commit-Inhalt der 6 Commits (verifiziert per `git show`/`git diff`):**
  27 geänderte Dateien, u. a. `McpCodeGraphServerOptions.cs` (neu),
  `McpServerOptionsBuilder.cs` (neu, Fluent-API), `McpServerOptionsFactory.cs`
  (schlanker gemacht), `McpCodeGraphServer.cs` (Konstruktor auf
  Options-Record umgestellt), `McpTestClient.cs` + `McpTestClientRetryOptions.cs`
  (Retry-Logik gegen TD-019-Flake) plus zugehörige neue Testklassen
  (`McpCodeGraphServerConstructorTests.cs`, `McpServerOptionsBuilderTests.cs`,
  `McpTestClientParallelTests.cs`, `McpTestClientRetryTests.cs`),
  `rules.json` (die 9 neuen `PathOverride`-Einträge), sowie ein
  Tech-Debt-Doku-Commit im inzwischen gelöschten
  `tasks/codegraph-mcp-server/tech-debt.md`.

## Intention

Dieser Step schließt die letzte offene Handlung aus Muss-Haben A ab, die
tatsächlich noch aussteht: ein reguläres, vollständiges Kritiker-Review
(alle vier Prüfebenen: Plan-Erfüllung so gut wie ohne Original-Plan-Doku
rekonstruierbar / Rules-Konformität / Logische Korrektheit / Konzept-Treue)
für die 6 bereits gepushten, aber nie geprüften Einheit-011-Commits —
inklusive einer expliziten Bewertung der 9-Datei-`PathOverride`-Erweiterung
als akzeptierten Pragmatik-Fix (Nutzer-Entscheidung, siehe `Konzept.md`
Zeile 121-124) und der TD-019-Restunschärfe (Retry-Logik ist Absicherung,
kein bewiesener Fix — im Review als akzeptierte Restunschärfe zu
vermerken, kein Blocker). Da der Push bereits erfolgt ist, ist dieser
Step ein reiner Verifikations-/Review-Step ohne neuen Produktionscode.

## Konkrete Änderungen

Kein Produktions- oder Testcode wird in diesem Step geändert (analog zu
step-006). Stattdessen:

### Verifikationsschritt 1: Prozess-Bereinigung

- **Was:** Vor jedem Build/Test offene `AiNetLinter.exe`-/
  `testhost.exe`-Prozesse prüfen (`Get-Process AiNetLinter,testhost` bzw.
  `tasklist`) und bei Fund per `Stop-Process -Force` beenden.
- **Warum:** Bekannte Datei-Sperren-Falle (Tech-Stack-Notiz, `Konzept.md`
  „Entdeckte Mängel"), Vorbedingung für einen aussagekräftigen Build.

### Verifikationsschritt 2: Frischer Build + Volllauf

- **Was:** `dotnet build AiNetLinter.slnx` (muss grün, 0 Warnungen),
  danach `dotnet test AiNetLinter.slnx --no-build` (Volllauf, muss grün
  sein). Ergebnis (Testzahl, Fehleranzahl, Laufzeit) im `step-result.md`
  festhalten.
- **Warum:** Muss-Haben A verlangt explizit „Volllauf frisch fahren,
  nicht nur den Coder-Bericht aus `units/011/result.md` übernehmen" —
  dieser Bericht existiert nicht mehr, die einzige verlässliche Quelle
  ist ein in diesem Step selbst gefahrener Lauf.

### Verifikationsschritt 3: Review-Grundlage für die 6 Commits zusammenstellen

- **Was:** Im `step-result.md` eine kompakte, referenzierbare Übersicht
  der 6 Commits (`4bcd5ab`, `075a8a0`, `af41a6b`, `1201840`, `a530b4f`,
  `8a663c7`) mit: betroffene Dateien, Kernaussage pro Commit (TD-009/
  TD-014/TD-019 laut `Konzept.md`-Zuordnung), und dem verifizierten
  `PathOverride`-Zählstand (14, nicht 13) zusammenstellen — **keine**
  inhaltliche Bewertung/Freigabe durch den Coder, das bleibt dem
  Kritiker vorbehalten.
- **Warum:** Der Kritiker braucht in seinem eigenen Review-Aufruf einen
  klaren Ausgangspunkt (`git show`/`git diff` über die 6 Commits), ohne
  die verlorene `units/011`-Planungsdoku ersetzen zu müssen.

**Hinweis an den Kritiker (nicht Teil der Coder-Aufgabe, aber zentral für
diesen Step):** Das eigentliche inhaltliche Review dieses Steps ist die
Prüfung der 6 Commits selbst (Diff-Inhalt, nicht nur die Zusammenfassung
aus Verifikationsschritt 3) gegen `<rules_dir>/**` und `Konzept.md`
Muss-Haben A — nicht nur die Coder-Verifikationsschritte 1-3. Konkret zu
prüfen: (a) Konstruktor-Record-Umstellung (`McpCodeGraphServerOptions`)
sauber und vollständig an allen 64 laut Commit-Message migrierten
Call-Sites, (b) `McpServerOptionsBuilder`/`-Factory`-Aufteilung entspricht
dem Gegenmuster „dünner Dispatch" aus `Konzept.md` TD-005, (c) Retry-Logik
in `McpTestClient` ist keine verdeckte Symptombekämpfung eines echten
Bugs, (d) die 9 neuen `PathOverride`-Einträge sind wie in `Konzept.md`
Zeile 121-124 als bewusste Pragmatik zu akzeptieren (nicht zu blocken),
mit Vermerk der abweichenden Zählung (14 statt 13) als Beobachtung, (e)
TD-019-A3-Nachweis-Unschärfe explizit als akzeptierte Restunschärfe
dokumentieren, kein Blocker.

## Tests

- [ ] `dotnet build AiNetLinter.slnx` — grün, 0 Warnungen
- [ ] `dotnet test AiNetLinter.slnx --no-build` — Volllauf grün, Testzahl
  im `step-result.md` festgehalten

## Definition of Done

- [ ] Prozess-Bereinigung durchgeführt und dokumentiert
- [ ] Frischer Build + Volllauf durchgeführt, Ergebnis dokumentiert
- [ ] Review-Grundlage (Commit-Übersicht) für die 6 Einheit-011-Commits
  im `step-result.md` zusammengestellt
- [ ] Build-Command aus Tech-Stack-Notiz (`roadmap.md`) grün
- [ ] Test-Command aus Tech-Stack-Notiz grün
- [ ] Commit auf aktuellem Branch (Conventional Commit, reiner
  Doku-Commit wie step-006, da kein Code geändert wird)
- [ ] `step-007/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
  `done (pending audit)` gesetzt
- [ ] Kritiker-Review deckt explizit den Inhalt der 6 Einheit-011-Commits
  ab (nicht nur die Coder-Verifikationsschritte dieses Steps) — siehe
  „Hinweis an den Kritiker" oben
- [ ] **Kein Push** in diesem Step — weder der (bereits erfolgte)
  Einheit-011-Push noch der aktuell separat ausstehende Push der 11
  lokalen Task-Doku-Commits. Falls der Nutzer nach `approved`-Verdict
  einen Push der aktuell lokalen Commits wünscht, ist das eine eigene,
  von ihm explizit zu bestätigende Aktion außerhalb dieses Step-Plans.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` — Build/Test-Pflichten
  (Zero-Warning-Direktive, Prozess-Bereinigung vor Build/Test),
  Commit-Vorschlag-Pflicht; relevant für die Verifikationsschritte und
  den abschließenden Doku-Commit dieses Steps.
- `.agents/rules/AiNetLinter.mdc` — `AIContextFootprint`-Grenzwert (2500)
  und `PathOverride`-Mechanismus; relevant für die Kritiker-Bewertung der
  9-Datei-`PathOverride`-Erweiterung aus Einheit 011.

## Bekannte Ausnahmen

- TD-019 (Retry-Logik gegen parallelen MCP-Init-Flake): Der A3-Nachweis
  ist laut `Konzept.md` Zeile 178-182 nicht abschließend (Flake auch ohne
  Retry-Loop nicht deterministisch reproduzierbar) — im Review als
  akzeptierte Restunschärfe zu vermerken, **kein** Blocker für
  `approved`.

## Notes

- Dieser Step ist ungewöhnlich für den Drift-Loop: Der „Coder"-Anteil ist
  auf Verifikation/Dokumentation beschränkt, das inhaltliche Review
  betrifft Code, der **nicht** in diesem Step geschrieben wurde. Der
  Orchestrator sollte dem Kritiker beim Aufruf explizit mitgeben, dass
  sich das Review auf die 6 in „Aktueller Projektzustand" genannten
  Commits bezieht, nicht nur auf die Diffs dieses Steps (die selbst nur
  aus `step-007/step-result.md` bestehen).
- Kein neuer Tech-Debt-Eintrag durch diesen Step erwartet, außer der
  Kritiker findet bei der inhaltlichen Prüfung der 6 Commits etwas
  Neues (z. B. zur 14-vs-13-`PathOverride`-Diskrepanz) — dann normaler
  Tech-Debt-Log-Eintrag wie gewohnt.
- Nach `approved`-Verdict dieses Steps ist `EPIC-02` inhaltlich
  abgeschlossen (der Push-Teil ist bereits erledigt, siehe oben) — der
  Planer wird das beim nächsten Roadmap-Abgleich entsprechend abhaken.
