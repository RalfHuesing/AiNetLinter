---
status: done
type: step-review
task: flaky-and-test-performance
step: 020
epic: EPIC-08
step_type: single
reviewed_by: kritiker
reviewed_by_model: Claude Sonnet 5
reviewed_by_model_knowledge_cutoff: 2026-01
reviewed_at: 2026-08-08T23:45:00+02:00
verdict: approved
tech_debt_ids: [TD-010, TD-011, TD-012]
---

# Review Step 020: EPIC-08 — Abschluss-Validierung & Vorher/Nachher-Doku

## Verdict

- [x] **approved** — alle vier Prüfebenen ok (Konzept-Treue mit dokumentiertem, nicht-blockierendem offenen Punkt — siehe unten)
- [ ] **issues**
- [ ] **blocked**

**Revision (2026-08-08T23:45):** Ursprünglich `issues` (siehe Historie unten). Nach
Klarstellung durch den Nutzer, dass die `step-020`-Messung (200s Median) auf einem
**Notebook** lief, während `konzept.md`-Baseline (~90s), `step-016` (97,75s) und
`step-019` (~175s) alle auf dem **Arbeits-PC** (32 Kerne @ 5,5 GHz) liefen, ist die
`step-020`-Zahl nicht direkt mit den drei Referenzpunkten vergleichbar — die
Hardware-Differenz ist eine hinreichende, plausible Erklärung für einen Großteil der
beobachteten Diskrepanz. DoD-Punkt 2 wird deshalb nicht mehr als eindeutig verfehltes
`MAJOR`-Finding gewertet, sondern als „nicht abschließend vergleichbar gemessen"
klassifiziert (Details unten unter „Konzept-Treue"). Der Nutzer hat zusätzlich
explizit entschieden, **keine weiteren Messungen** mehr durchzuführen (auch nicht
nachträglich auf dem Arbeits-PC) — der Task soll auf Basis der vorliegenden Daten
abgeschlossen werden. Ein `issues`-Verdict, das einen neuen Mess-/Fix-Step auslöst,
wäre damit nicht zielführend.

## Geprüft

- [x] Plan-Erfüllung: alle im `step-plan.md` genannten Änderungen erfolgt
- [x] Rules-Konformität: `<rules_dir>/**` eingehalten
- [x] Logische Korrektheit: Code macht was er soll, nicht nur „grün"
- [x] Konzept-Treue: passt die Umsetzung zu `konzept.md` (Scope, Non-Goals, Muss-Haben) — mit dokumentiertem offenen Punkt, kein Blocker (siehe unten)
- [x] Build: selbst nachgeprüft, grün (0 Warnungen, 0 Fehler)
- [x] Tests: selbst nachgeprüft — 3 von 8 vollen Läufen valide grün (1325/1325), 5 TD-010-Ausreißer korrekt aus dem Median ausgeschlossen

## Befund

### Plan-Erfüllung

Alle 6 Aktivitäten aus `step-plan.md` wurden durchgeführt, exakt wie geplant:

- **Aktivität 1** (frische Median-Messung, mind. 5 valide Läufe, Deckel 8): **teilweise erfüllt** —
  der Deckel von 8 Läufen wurde ausgeschöpft, aber nur 3 statt der geforderten
  mindestens 5 validen Läufe erreicht. Der Plan sieht diesen Fall explizit vor
  ("Werden bei 8 Läufen weniger als 5 valide erreicht, ist das selbst ein
  dokumentationswürdiger Befund … kein Blocker für diesen Step, aber explizit
  im Ergebnis als Risiko benennen") — der Coder hat genau das getan, keine
  Plan-Abweichung im engeren Sinn, sondern ein im Plan antizipiertes
  Randszenario, korrekt behandelt. Die Ausreißer-Klassifikation (5×, exakt
  die im Plan/TD-010 benannten Testklassen `McpServerCommandErrorHandlingTests`,
  `McpServerCommandJsonRpcFramingTests`, keine Berührung der EPIC-06-Zieltests)
  ist selbst nachvollziehbar korrekt.
- **Aktivität 2** (Vorher/Nachher-Vergleich): **erfüllt** — alle drei
  historischen Referenzpunkte (90s, 97,75s, 169-185s) korrekt referenziert,
  Diskrepanz benannt, nicht spekulativ aufgelöst, plankonform.
- **Aktivität 3** (Fast-Path-Verifikation): **erfüllt** (Ausführung); Ergebnis
  selbst mit Zielabweichung, siehe „Sonstige Beobachtungen" und TD-011.
- **Aktivität 4** (Flaky-Test-Bestätigung): **erfüllt** — 6 der 8 Läufe liefen
  vollständig durch, in allen 6 waren beide EPIC-06-Zieltests grün; kumuliert
  mit `step-019` ergibt das 14 vollständige Läufe, mehr als die geforderten 10.
- **Aktivität 5** (Category-Trait-Vollständigkeit): **erfüllt** — 1193 + 132 = 1325,
  keine Lücke.
- **Aktivität 6** (Build + Self-Lint): **erfüllt** — Build grün 0 Warnungen,
  Self-Lint `OK`.

`step-020/step-plan.md`-Status korrekt von `open` → `done (pending audit)`
gesetzt. Kein Code-/Testcode-Diff (plankonform, reine Doku-Aktivität). Der
DoD-Checklisten-Punkt "Falls die Prüfung eine echte Lücke aufdeckt: nicht im
selben Step nachbessern, als offenen Punkt dokumentieren" wurde korrekt
befolgt — der Coder hat DoD-Punkt 2 explizit als nicht erfüllt markiert und
keinen Fixversuch unternommen. Das ist exakt richtig für **diesen** Step;
der eigentliche Befund liegt auf Ebene 4 (Konzept-Treue), nicht Ebene 1.

### Rules-Konformität

Keine einschlägige Regelverletzung in Produktionscode (keiner geändert).
Commit-Subject 74 Zeichen (2 über der 72-Grenze aus
`AiNetLinterRichtlinien.mdc`/`spec.md` §10.3) — bereits im Plan selbst als
unvermeidbar dokumentiert (alle drei Vorschläge lagen über der Grenze wegen
des langen Task-Suffixes), analog TD-002-Muster. Kein neuer Regelverstoß,
kein neuer TD-Eintrag nötig (Plan-Vorgabe „kein neuer TD-Eintrag, da bereits
bekanntes Muster" — korrekt befolgt).

### Logische Korrektheit

Nachvollzogen: Median der 3 validen Läufe (186s, 200s, 227s) = 200s, korrekt
berechnet (kein Trimmen, wie im Plan vorgegeben). Die Ausreißer-Klassifikation
ist stichhaltig — alle 5 Ausreißer zeigen ausschließlich TD-010-Symptome in
exakt den vorab benannten Testklassen, kein Übergriff auf andere Tests. Die
Prozess-Kill-Hygiene (gezielte PID statt Massen-Kill, `tasklist`-Verifikation)
ist plausibel beschrieben. Die DoD-Abgleichs-Tabelle ist in sich konsistent
und rechnet korrekt (1193 Unit + 132 Integration = 1325 Gesamt, deckt sich mit
allen drei validen vollen Läufen).

### Konzept-Treue (Ebene 4)

**Revidiert nach Nutzer-Klarstellung zur Mess-Hardware.** Ursprünglich hatte ich
die monoton steigende Kurve (90s → 97,75s → ~175s → 200s) als strukturellen
Regressions-Befund und `MAJOR`-Finding gewertet (Severity-Gating der
Kritiker-Skill: „ein Muss-Haben-Punkt aus `konzept.md` fehlt"). Nach
Klarstellung, dass `step-020` (200s) auf einem **Notebook** gemessen wurde,
während `konzept.md`-Baseline (~90s), `step-016` (97,75s) und `step-019`
(~175s) alle auf dem **Arbeits-PC** (32 Kerne @ 5,5 GHz) liefen, ist der
`step-020`-Datenpunkt gegenüber den drei Referenzwerten **nicht sauber
vergleichbar** — ein Hardware-Wechsel dieser Größenordnung ist eine
hinreichende, plausible Erklärung für einen erheblichen Teil des Sprungs
`step-019` (~175s) → `step-020` (200s) sowie für die Fast-Path-Diskrepanz
(96s statt 23-24s, siehe unten/TD-011). Ein `MAJOR`-Finding allein auf Basis
der `step-020`-Zahl ist damit nicht mehr haltbar; ich werte DoD-Punkt 2 neu
als **„nicht abschließend vergleichbar gemessen"** statt „nicht erfüllt".

**Was davon unberührt bleibt (kein Freispruch, sondern bewusst offen
dokumentiert):** `step-016` (97,75s) und `step-019` (~175s) liefen laut
Nutzer-Klarstellung **beide** auf dem Arbeits-PC — hier bleibt eine reale,
same-hardware Verschlechterung von ca. +79 % zwischen EPIC-03- und
EPIC-06-Abschluss ungeklärt, unabhängig vom Notebook-Thema. Diese Diskrepanz
wurde bereits im `step-020`-Plan selbst als offene, nicht aufzulösende Frage
benannt und war schon beim `step-019`-Review (`step-019/step-review.md`,
`tech_debt_ids: […, TD-010]`) bekannt, ohne dort als Blocker gewertet worden
zu sein. Sie bleibt ein echter, dokumentierter Risiko-/Tech-Debt-Punkt (siehe
TD-012), wird aber **nicht** zu einem Kritiker-Finding erhoben, weil (a) der
Nutzer explizit entschieden hat, keine weitere Messung mehr durchzuführen —
eine Root-Cause-Klärung wäre aber ohne kontrollierte Gegenmessung auf
identischer Hardware nicht seriös möglich —, und (b) dieselbe Diskrepanz
bereits im `step-019`-Review bekannt war und dort nicht blockiert hat; das
`step-020`-Review würde sonst rückwirkend einen bereits `approved`-Step in
Frage stellen, was nicht Aufgabe dieses Reviews ist.

Kein Non-Goal aus `konzept.md` verletzt, keine Scope-Abweichung. Die übrigen
5 DoD-Punkte sind erfüllt bzw. — bei Punkt 3 (Fast-Path) — im Kern erfüllt
(siehe „Sonstige Beobachtungen"). Damit sind 5 von 6 DoD-Punkten sauber
erfüllt, der sechste (Punkt 2) ist mangels vergleichbarer Messgrundlage nicht
abschließend zu beurteilen — kein Beleg für eine echte Regression, aber auch
kein Beleg für die geforderte spürbare Verbesserung. Auf ausdrücklichen
Wunsch des Nutzers wird der Task auf dieser Datenlage abgeschlossen, ohne
weitere Messung nachzufordern.

### Build-/Test-Status

```
dotnet build                                            → grün, 0 Warnungen, 0 Fehler (3.45s)
dotnet test (8× voll)                                   → 3/8 valide grün (1325/1325), 5/8 TD-010-Ausreißer (dokumentiert, korrekt klassifiziert)
dotnet test --filter Category=Unit                      → grün, 1193/1193, 96s (siehe TD-011)
dotnet test --filter Category=Unit/Integration --list   → 1193 + 132 = 1325, keine Lücke
Self-Lint                                                → OK
```

## Sonstige Beobachtungen / MINOR / NITPICK

- **DoD-Punkt 2 (Performance) — offener, dokumentierter Punkt, kein Blocker:**
  Weder ist mit den vorliegenden Daten eine „spürbare Verbesserung" gegenüber
  der ~90s-Konzept-Baseline belastbar nachgewiesen, noch ist eine echte
  Regression bewiesen — die `step-020`-Zahl (200s, Notebook) ist mit den
  Referenzwerten (Arbeits-PC) nicht direkt vergleichbar. Unabhängig davon
  bleibt die same-hardware-Diskrepanz `step-016` (97,75s) → `step-019`
  (~175s, beide Arbeits-PC) ungeklärt. Auf explizite Nutzer-Entscheidung hin
  keine weitere Messung — der Task wird auf dieser Datenlage abgeschlossen.
  Empfehlung für die Zukunft (kein Auftrag an einen Folge-Step): sollte die
  Performance-Frage später erneut relevant werden, müsste jede Vergleichs-
  messung auf **derselben** Hardware wie die `konzept.md`-Baseline erfolgen,
  sonst bleibt jeder Vorher/Nachher-Vergleich methodisch unbrauchbar.
- **Fast-Path-Diskrepanz (Aktivität 3):** `dotnet test --filter Category=Unit` lief in 96s statt der in `AGENTS.md`/`step-017` dokumentierten ~23-24s (rund 4× langsamer). Das eigentliche DoD-Kriterium ("spürbar schnellerer Fast-Path, deckt alle Unit-Aspekte ab") ist dem Wortlaut nach weiterhin erfüllt (96s vs. 200s Vollauf-Median ist noch immer eine spürbare, gut 50%ige Verkürzung, und die Testabdeckung ist lückenlos nachgewiesen). Die konkrete Zahl in `AGENTS.md` ist aber jetzt nicht mehr reproduzierbar — plausibel ebenfalls größtenteils hardwarebedingt (Notebook vs. Arbeits-PC), nicht verifiziert. Siehe `TD-011`.

## Tech-Debt-Einträge aus diesem Review

- `TD-010` (siehe `tech-debt.md`) — bestehender Eintrag um die `step-020`-Beobachtung ergänzt: Häufigkeit der Symptome von 2/10 (`step-019`) auf 5/8 (`step-020`) gestiegen.
- `TD-011` (siehe `tech-debt.md`) — neu: `AGENTS.md`/`step-017`-Fast-Path-Zeitangabe (~23-24s) ist mit aktueller Messung (96s) nicht mehr reproduzierbar; plausibel z. T. hardwarebedingt (siehe TD-012), Dokumentation dennoch veraltet/irreführend.
- `TD-012` (siehe `tech-debt.md`) — neu: `step-020`-Performance-Messung erfolgte auf anderer Hardware (Notebook) als alle Referenzmessungen (Arbeits-PC), macht den Task-Abschluss-Vorher/Nachher-Vergleich methodisch nicht sauber; zusätzlich bleibt die same-hardware-Diskrepanz `step-016`→`step-019` (beide Arbeits-PC) ungeklärt — auf Nutzer-Wunsch keine weitere Messung, Punkt bleibt offen dokumentiert.
