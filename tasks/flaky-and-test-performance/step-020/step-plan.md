---
status: open
type: step-plan
task: flaky-and-test-performance
step: 020
corrects: null
title: "EPIC-08: Abschluss-Validierung — Vorher/Nachher-Messung + DoD-Checkliste"
epic: EPIC-08
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: Claude Sonnet 5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-08T22:00:00+02:00
related_to: [step-016, step-017, step-019]
---

# Step 020: EPIC-08 — Abschluss-Validierung & Vorher/Nachher-Doku

## Bezug

- **Task:** `flaky-and-test-performance`
- **Epic:** `EPIC-08` aus `roadmap.md` — letztes offenes Epic. Alle
  Vorgänger-Epics (01-07) sind abgehakt/obsolet/verworfen. EPIC-08 selbst
  ist rein Verifikations-/Doku-Arbeit, keine strukturelle Änderung mehr:
  vollen Testlauf mit dem jetzt optimierten Setup mehrfach laufen lassen,
  Median bilden, mit der ~90s-Baseline aus `konzept.md` vergleichen, DoD
  aus `konzept.md` §"Definition of Done" Punkt für Punkt durchgehen und im
  Step-Ergebnis dokumentieren, Self-Lint grün bestätigen.
- **Konzept-Referenz:** `konzept.md` §"Definition of Done / Erfolgskriterien"
  (alle 6 Punkte, siehe unten) und §"Ziel" Punkt 1 (Performance,
  "deutlich besser als jetzt", kein festes Zeitbudget).

## Aktueller Projektzustand (JIT-Kontext)

- **Alle Vorgänger-Epics geschlossen:** EPIC-01 (Spike, negativ), EPIC-02
  (Category-Traits, 1193 Unit + 132 Integration = 1325 Total, projektweit
  vom Kritiker verifiziert), EPIC-03 (Fixture-Sharing, `step-016`, Median
  102,69s → 97,75s, −4,8%), EPIC-04 (Fast-Path `--filter Category=Unit`
  in `AGENTS.md` dokumentiert, ~23-24s), EPIC-05 (obsolet, nicht
  umgesetzt), EPIC-06 (Flaky-Fix, `step-019`, `Task.WhenAny`-Wartemuster
  statt Poll-Loop), EPIC-07 (verworfen, `ConsoleTestCollection`
  wiederhergestellt — wird für Parallelisierungsschutz zwingend
  gebraucht).
- **Wichtige Diskrepanz, die dieser Step aufklären/dokumentieren muss:**
  Die in `step-016` gemessene Baseline nach EPIC-03 lag bei **97,75s
  Median** (10-Lauf-Basis nicht dokumentiert, vermutlich wenige Läufe).
  Die 10 vollen Testläufe aus `step-019` (nach EPIC-06, selbes
  optimierte Setup, andere Session) zeigen dagegen Laufzeiten von
  **2m49s bis 3m05s (169-185s)** — fast doppelt so lang wie die
  EPIC-03-Zahl und auch deutlich über der ursprünglichen ~90s-Baseline
  aus `konzept.md`. Mögliche Ursachen (nicht abschließend geklärt, nicht
  Aufgabe dieses Steps zu klären): unterschiedliche Systemlast zwischen
  den Sessions, `dotnet build-server`-Warmup-Unterschiede, oder die
  step-019-Läufe liefen in einer Umgebung mit mehr Hintergrundlast
  (Runs 9/10 zeigten zusätzlich TD-010-Symptome). Dieser Step **misst
  frisch nach**, dokumentiert beide historischen Zahlen als Kontext, und
  behandelt die eigene frische Messung als maßgeblich für den
  Abschluss-Vergleich — spekuliert nicht über die Ursache der Diskrepanz.
- **TD-010 (hoch, offen):** reproduzierbar (3× in `step-019`) hängen/
  timeouten unabhängige MCP-Integrationstests (`McpServerCommandErrorHandlingTests`,
  `McpServerCommandJsonRpcFramingTests`, vereinzelt `ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames`)
  unter voller Testlast — nicht Teil des `LoadState`-Flaky-Fixes aus
  EPIC-06, root cause ungeklärt. Das ist das zentrale operative Risiko
  für diesen Step, da EPIC-08 selbst mehrere volle Testläufe braucht.
  **Konsequenz für die Planung:** mehr Wiederholungen einkalkulieren als
  ein einfacher 3-5-Lauf-Median bräuchte, TD-010-Symptome als dokumentierte
  Ausreißer behandeln (nicht in den Median einrechnen, aber nicht
  verschweigen), harte Obergrenze pro Lauf setzen, damit ein Hang den
  Step nicht unbegrenzt blockiert.
- **Self-Lint-Befehl:** `dotnet run --project src/AiNetLinter -- --config rules.json --path .`
  (TD-001-konformer Ersatz für die fehlende `--self-lint`-Option, bereits
  in `step-019` so verwendet, weiterhin gültig).
- **Keine Code-Änderung erwartet:** Alle Muss-Haben-Punkte aus `konzept.md`
  wurden bereits in EPIC-01–07 umgesetzt. Dieser Step ist Verifikation +
  Dokumentation, kein Refactoring. Falls die Messung/Prüfung eine echte
  Lücke aufdeckt (z. B. ein DoD-Punkt tatsächlich nicht erfüllt), gehört
  die Korrektur **nicht** in diesen Step, sondern wird als Finding im
  Step-Ergebnis vermerkt und läuft ggf. über einen Kritiker-`issues`-
  Verdict in einen Fix-Step — dieser Plan selbst ändert keinen Code.

## Intention

Nach diesem Step ist die Aufgabe `flaky-and-test-performance` inhaltlich
vollständig abgenommen: eine frische, robuste Median-Messung des vollen
Testlaufs liegt vor (mit expliziter Behandlung bekannter TD-010-Ausreißer),
verglichen gegen die historische ~90s-Baseline aus `konzept.md`. Alle 6
Punkte aus `konzept.md` §"Definition of Done" sind einzeln durchgegangen
und im Step-Ergebnis mit Status (erfüllt/nicht erfüllt, Beleg) dokumentiert.
Self-Lint läuft grün. Das ist der letzte reguläre Step des Tasks — nach
`approved` sind keine offenen Epics mehr vorhanden (siehe Roadmap-Abgleich
des nächsten Planer-Aufrufs).

## Konkrete Änderungen

Dieser Step ändert **keinen Produktions- oder Testcode**. Die "Änderung"
besteht aus Mess-/Verifikationsdurchläufen und der Dokumentation der
Ergebnisse in `step-020/step-result.md`. Kein Commit-Diff im klassischen
Sinn außer dem Doku-Commit selbst (siehe DoD).

### Aktivität 1: Frische Median-Messung des vollen Testlaufs

- **Was:** In einer sauberen Umgebung (`dotnet build-server shutdown` vor
  dem ersten Lauf, wie in `step-019` etabliert) mindestens **5** volle
  `dotnet test`-Läufe sequenziell durchführen und je Lauf Dauer + Ergebnis
  (grün/Fehlschlag/Hang) protokollieren.
  - Tritt in einem Lauf ein TD-010-Symptom auf (Hang oder Fehlschlag in
    einem der bekannten unabhängigen MCP-Integrationstests, **nicht**
    in den EPIC-06-Zieltests) — Lauf als **dokumentierten Ausreißer**
    markieren (Root-Cause-Verweis TD-010), aus der Median-Berechnung
    ausschließen, aber **zusätzlichen** Lauf nachholen, bis mindestens
    5 valide (nicht durch TD-010 beeinträchtigte) Läufe vorliegen.
  - **Deckel:** maximal 8 Läufe insgesamt (Kosten-/Zeit-Grenze). Werden
    bei 8 Läufen weniger als 5 valide erreicht, ist das selbst ein
    dokumentationswürdiger Befund (TD-010 wäre dann schwerwiegender als
    bisher angenommen) — kein Blocker für diesen Step, aber explizit im
    Ergebnis als Risiko benennen, nicht stillschweigend mit weniger
    Datenpunkten weiterrechnen.
  - Ein Hang, der nach ca. 6 Minuten (deutlich über dem bisher
    beobachteten Maximum von ~3 Minuten für einen gesunden Lauf) noch
    nicht terminiert ist: Prozess gezielt beenden, als Hang-Ausreißer
    zählen, nicht länger warten.
  - Median über die validen Läufe bilden, dokumentieren.
- **Warum:** Kern-Anforderung von EPIC-08 ("vollen Testlauf ... mehrfach
  laufen lassen, Median bilden"); die Wiederholungs-/Ausreißer-Logik ist
  die direkte Umsetzung des im Planungsauftrag genannten TD-010-Risikos.

### Aktivität 2: Vorher/Nachher-Vergleich dokumentieren

- **Was:** Den neuen Median gegen drei Referenzpunkte stellen und die
  Diskrepanz zwischen ihnen benennen (nicht auflösen — reine
  Transparenz):
  1. `konzept.md`-Baseline: ~90s (vor jeglicher Optimierung dieses Tasks).
  2. `step-016`-Zwischenmessung nach EPIC-03: 97,75s Median (vor EPIC-06/07).
  3. `step-019`-Beobachtung nach EPIC-06 (10 Läufe): 169-185s, deutlich
     höher als (1) und (2).
  - Der in diesem Step frisch gemessene Median ist die für den
    Abschluss-Vergleich **maßgebliche** Zahl.
- **Warum:** `konzept.md` §"Definition of Done" verlangt "messbar besser,
  mit Vorher/Nachher-Zahl im Ergebnis dokumentiert" — ohne die
  Diskrepanz transparent zu machen, wäre eine einzelne Zahl irreführend.

### Aktivität 3: Fast-Path-Verifikation

- **Was:** `dotnet test --filter Category=Unit` einmal ausführen, Dauer +
  Testanzahl protokollieren, mit der in `AGENTS.md`/`step-017`
  dokumentierten Erwartung (~23-24s, 1193 Tests) abgleichen.
- **Warum:** DoD-Punkt "dokumentierter, spürbar schnellerer Fast-Path-
  Befehl existiert und deckt weiterhin alle Unit-Aspekte ab" — muss zum
  Abschluss noch einmal aktiv verifiziert werden, nicht nur aus `step-017`
  übernommen werden.

### Aktivität 4: Flaky-Test-Bestätigung (kein Zusatzaufwand, Nebenprodukt von Aktivität 1)

- **Was:** In jedem der validen vollen Läufe aus Aktivität 1 explizit
  bestätigen, dass `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
  und `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState`
  (`McpServerCommandLoadingStateTests.cs`) grün waren.
- **Warum:** DoD-Punkt verlangt "läuft in mindestens 10 aufeinanderfolgenden
  vollen Testläufen fehlerfrei durch" — `step-019` lieferte bereits 10
  Läufe mit beiden Zieltests durchgehend grün. Die hier zusätzlich
  gesammelten validen Läufe (mind. 5) zählen kumulativ dazu und werden im
  Ergebnis explizit als Fortsetzung der `step-019`-Evidenzkette
  referenziert (Gesamtzahl grüner Nachweis-Läufe über beide Steps).

### Aktivität 5: Category-Trait-Vollständigkeit gegenprüfen

- **Was:** `dotnet test --filter Category=Unit --list-tests` +
  `dotnet test --filter Category=Integration --list-tests` (oder
  äquivalente Zählung) durchführen, Summe mit der Gesamt-Testanzahl aus
  Aktivität 1 abgleichen (erwartet: 1193 + 132 = 1325 = Total, keine Lücke).
- **Warum:** DoD-Punkt "Alle Tests tragen einen Category-Trait" — EPIC-02
  wurde vom Kritiker bereits projektweit strukturell verifiziert
  (`step-015`), diese Zählung ist der schnelle numerische Re-Check zum
  Taskabschluss, kein erneuter Vollscan.

### Aktivität 6: Build + Self-Lint

- **Was:** `dotnet build` (grün, 0 Warnungen) und
  `dotnet run --project src/AiNetLinter -- --config rules.json --path .`
  (`OK`) einmal frisch ausführen und Output im Ergebnis festhalten.
- **Warum:** DoD-Punkt "`dotnet build` (TreatWarningsAsErrors) und der
  volle Testlauf bleiben grün; Self-Lint bleibt OK".

## Tests

- [ ] Mindestens 5 valide volle `dotnet test`-Läufe (Median-Basis, siehe
      Aktivität 1) — TD-010-Ausreißer dokumentiert, nicht in den Median
      eingerechnet.
- [ ] `dotnet test --filter Category=Unit` — 1 Lauf, grün, Dauer notiert.
- [ ] `dotnet build` — grün, 0 Warnungen.
- [ ] Self-Lint (`dotnet run --project src/AiNetLinter -- --config rules.json --path .`) — `OK`.

Kein neuer Testcode — dieser Step schreibt/ändert keine `[Fact]`/`[Theory]`-
Methoden, er führt nur bestehende Läufe aus und dokumentiert.

## Definition of Done

- [ ] Aktivitäten 1-6 durchgeführt, alle Ergebnisse (Dauer, Median,
      Ausreißer, Testzahlen, Build-/Lint-Output) in `step-020/step-result.md`
      protokolliert.
- [ ] Alle 6 Punkte aus `konzept.md` §"Definition of Done" einzeln
      aufgelistet mit Status (erfüllt/nicht erfüllt) + Beleg-Verweis
      (welche Aktivität/welcher frühere Step das belegt).
- [ ] Diskrepanz zwischen den drei historischen Zeit-Referenzpunkten
      (Konzept-Baseline ~90s, `step-016` 97,75s, `step-019` 169-185s) und
      der neuen Messung transparent benannt, nicht aufgelöst/spekuliert.
- [ ] `dotnet build` grün.
- [ ] Voller Testlauf (mindestens die 5 validen Läufe) grün, TD-010-
      Ausreißer sauber von echten Regressionen unterschieden.
- [ ] Self-Lint `OK`.
- [ ] Falls die Prüfung eine echte Lücke gegenüber einem DoD-Punkt
      aufdeckt: **nicht** im selben Step nachbessern — als offenen Punkt
      im Step-Ergebnis dokumentieren, Kritiker entscheidet über
      `issues`-Verdict und Fix-Step.
- [ ] Kein Commit-Diff an Produktions-/Testcode (reiner Doku-Commit
      mit `step-020/step-result.md`).
- [ ] Commit auf `main` (Conventional Commit, Subject ≤ 72 Zeichen inkl.
      `[flaky-and-test-performance]`-Suffix, z. B.
      `docs(tasks): EPIC-08 Abschluss-Validierung dokumentieren [flaky-and-test-performance]`
      — 87 Zeichen, **über der 72-Grenze**, siehe TD-002: alternative
      kürzere Vorschläge für den Coder:
      `docs(tasks): step-020 Abschluss-Validierung [flaky-and-test-performance]`
      (73 Zeichen, noch 1 drüber) oder
      `docs(tasks): step-020 Ergebnis dokumentieren [flaky-and-test-performance]`
      (74 Zeichen) — **keiner der Vorschläge passt unter 72** wegen der
      langen Task-Suffix-Klammer; Coder wählt die kürzeste sinnvolle
      Variante und dokumentiert die Abweichung analog TD-002, kein neuer
      TD-Eintrag nötig, da bereits bekanntes Muster).
- [ ] `step-020/step-plan.md` Status von `open` → `in_progress` →
      `done (pending audit)`.

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` — Self-Lint-Pflicht vor
  Task-Abschluss, Zero-Warning-Direktive, `### Commit-Vorschlag`-Block-
  Pflicht, sparsame Kommentare (relevant falls der Coder im Zuge dieses
  Steps doch Kommentare in Ergebnis-Dokumenten formuliert — gilt aber
  primär für Code, nicht für `step-result.md`-Prosa).
- `.agents/rules/AiNetLinter.mdc` — nicht einschlägig für diesen Step
  (keine Code-Änderung, keine Metrik-Prüfung an Produktionscode nötig).

## Bekannte Ausnahmen

- **TD-010-Symptome** (Hänger/Timeouts in `McpServerCommandErrorHandlingTests`,
  `McpServerCommandJsonRpcFramingTests`, ggf. `ToolCallSequence_AllStdoutLinesAreValidJsonRpcFrames`)
  gelten in diesem Step explizit **nicht** als Step-Fehlschlag, solange sie
  wie in Aktivität 1 beschrieben behandelt (dokumentiert, aus dem Median
  ausgeschlossen, nachgeholter Ersatzlauf) werden. Ein Fehlschlag/Hang in
  `McpServerCommandLoadingStateTests.cs` selbst wäre dagegen ein echter
  Befund (EPIC-06-Regression) und **kein** hier abgedeckter Ausnahmefall.

## Notes

- Dieser Step ist bewusst **kein** Batch (`step_type: single`) — EPIC-08
  ist inhaltlich eine einzige zusammenhängende Verifikations-/Doku-Aktivität,
  keine Sammlung unabhängiger Mini-Änderungen. Die Aktivitäten 1-6 sind
  Teilschritte **eines** Vorhabens, nicht `items` im Batch-Sinn.
- Kein `auto_fixable: ja`-Tech-Debt-Eintrag im Index vorhanden (TD-002/
  TD-005/TD-006/TD-008/TD-009/TD-010 sind alle `auto_fixable: nein` oder
  ohne den Marker) — daher kein opportunistisches Anhängen an diesen Step.
- Falls nach `approved` dieses Steps tatsächlich **keine** offenen Epics
  mehr in `roadmap.md` stehen: Der nächste Planer-Aufruf meldet das dem
  Orchestrator statt einen weiteren Step zu planen (siehe
  `skills/planer/SKILL.md` Step-Modus Schritt 1 Punkt 3) — das beendet
  den Loop. Die `task-summary.md`-Erstellung ist danach Orchestrator-
  Sache, nicht Teil dieses Step-Plans.
- Für die Median-Berechnung: einfacher Median über die (mindestens 5)
  validen Lauf-Dauern, keine Ausreißer-Bereinigung über die TD-010-Fälle
  hinaus (z. B. kein Trimmen des schnellsten/langsamsten validen Laufs —
  das wäre eine zusätzliche, hier nicht vorgesehene Statistik-Entscheidung).
