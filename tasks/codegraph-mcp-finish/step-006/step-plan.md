---
status: done
type: step-plan
task: codegraph-mcp-finish
step: 006
title: "Volllauf-Laufzeitmessung formal dokumentieren (F.6)"
epic: EPIC-01
estimated_risk: low
step_type: single
items: []
created_by: planer
created_by_model: claude-sonnet-5
created_by_model_knowledge_cutoff: 2026-01
created_at: 2026-08-03
related_to: [step-001]
---

# Step 006: Volllauf-Laufzeitmessung formal dokumentieren (F.6)

## Bezug

- **Task:** `codegraph-mcp-finish`
- **Epic:** `EPIC-01` aus `roadmap.md` — letzter offener Punkt (F.6). F.1-F.4
  sind vollständig approved (step-001..step-005), F.5 ist als bewusste
  Randmitnahme (keine Flächenaktion) erledigt. F.6 ("Laufzeitmessung
  vorher/nachher dokumentieren") ist der einzige noch offene Block-F-Punkt
  — nach diesem Step ist EPIC-01 vollständig abgeschlossen.
- **Konzept-Referenz:** `Konzept.md` Muss-Haben F, Punkt 6 (Zeile 438-441):
  „Laufzeitmessung vorher/nachher dokumentieren (eine frühere Messung dazu
  ist nicht mehr aktuell nachvollziehbar, da der Volllauf seither erneut
  gewachsen ist) — ohne Zielprozentzahl, aber mit klarer Zahl, damit der
  nächste Task nicht wieder von 'gefühlt 8 Minuten' ausgehen muss.“ Sowie
  Definition of Done (Zeile 669-676): „Volllauf-Laufzeit vorher/nachher
  gemessen und in `result.md`/`summary.md` dokumentiert (keine harte
  Zielzahl, aber eine belegte Verbesserung).“

## Aktueller Projektzustand (JIT-Kontext)

- **Bisherige Zahlen sind informell, nicht formal dokumentiert:**
  `roadmap.md` (EPIC-01-Zeile) nennt „F.1-Ergebnis (step-001): Volllauf von
  ~8 Min. auf ~1 m 35–41 s reduziert (informelle Messung, formale
  F.6-Dokumentation steht noch aus)“. Der „~8 Min.“-Vorher-Wert stammt aus
  `Konzept.md` (Zeile 35-36) und aus der Tech-Stack-Notiz in `roadmap.md`
  selbst (Zeile 29-30: „Volllauf, aktuell ~8 Min., genau das, was Block F
  verkürzen soll“) — ebenfalls keine belegte Einzelmessung, sondern eine
  grobe Schätzung aus der Zeit vor step-001. Es existiert **keine**
  dedizierte Performance-Doku-Datei im Projekt (`Docs/` enthält keine
  Datei zu Testlaufzeiten, geprüft per Grep über `Docs/**`) — die einzige
  Stelle, die laut Konzept-DoD tatsächlich verlangt wird, ist
  `step-result.md` (und später `task-summary.md`, das erst beim
  Task-Abschluss vom globalen Kritiker geschrieben wird, nicht hier).
- **Testzahl aktuell stabil:** `dotnet test AiNetLinter.slnx --no-build`
  lief in step-004 und step-005 identisch mit 1186 Tests, 0 Fehlern (siehe
  `step-005/step-result.md` „Build-/Test-Output“) — seit der informellen
  F.1-Messung in step-001 sind F.2-F.5 dazugekommen, keine davon hat laut
  den jeweiligen `step-result.md`/`step-review.md` die Testzahl oder
  strukturelle Parallelität verändert (reine Boilerplate-/
  Organisations-Refactorings, Non-Goal „keine Änderung an
  Testinhalten/Assertions“ durchgehend eingehalten).
- **Bekannte Fußangel für die Messung selbst:** `Konzept.md` „Entdeckte
  Mängel/Redundanzen“ dokumentiert eine wiederkehrende Datei-Sperre durch
  hängende `AiNetLinter.exe`-/`testhost.exe`-Prozesse nach abgebrochenen
  Läufen — die Tech-Stack-Notiz in `roadmap.md` verlangt deshalb bereits
  projektweit: „Vor jedem Build/Test in diesem Task: offene
  `AiNetLinter.exe`-/`testhost.exe`-Prozesse prüfen und ggf. beenden“. Für
  eine Zeitmessung ist das doppelt wichtig — hängende Prozesse verfälschen
  die Wall-Clock-Zeit über MSBuild-Datei-Locking hinaus nicht direkt, aber
  ein fehlgeschlagener Build durch Datei-Sperre würde die Messung
  unbrauchbar machen.
- **`TestResults/latest.trx`** wird laut `AiNetLinterRichtlinien.mdc` §3
  bei jedem `dotnet test`-Lauf automatisch überschrieben und enthält
  Start-/Endzeitstempel — geeignet als zweite, von der Shell-Wall-Clock
  unabhängige Quelle zur Cross-Verifikation der gemessenen Zeit.
- **Kein Code wird in diesem Step geändert** — reiner Mess-/Dokumentations-
  Step, wie schon in F.5-analoger Weise bereits in `roadmap.md` als „für
  einen Folge-Step“ vorgesehen. Es gibt keine bestehende Infrastruktur zum
  Wiederverwenden (kein Timing-Harness im Projekt außerhalb des ohnehin
  deaktivierten `EnablePerformanceProfiling`-Linter-Feature, das etwas
  anderes misst — Linter-Phasenlaufzeit, nicht Testsuite-Laufzeit — und
  hier nicht einschlägig ist).

## Intention

Die in `roadmap.md`/`Konzept.md` mehrfach zitierte, aber nie formal belegte
Verbesserung „~8 Min. → ~1 m 35–41 s“ wird durch eine reproduzierbare,
frisch gefahrene Messung ersetzt bzw. bestätigt und in `step-result.md`
dokumentiert (Methode, Rohzahlen, Vergleich). Das ist der letzte offene
Punkt von Block F — nach diesem Step ist `EPIC-01` vollständig
abgeschlossen und die Roadmap wird das beim nächsten Step-Modus-Aufruf
entsprechend abhaken.

## Konkrete Änderungen

Kein Produktions- oder Testcode wird geändert. Der Step besteht
ausschließlich aus Mess-Kommandos und deren Dokumentation.

### Schritt 1: Umgebung bereinigen

- **Was:** Vor der Messung alle offenen `AiNetLinter.exe`-/
  `testhost.exe`-Prozesse prüfen (z. B.
  `Get-Process AiNetLinter,testhost -ErrorAction SilentlyContinue`) und
  beenden, falls vorhanden.
- **Warum:** Bekannte Datei-Sperren-Falle (siehe „Aktueller
  Projektzustand“) — eine durch Sperre verfälschte oder fehlgeschlagene
  Messung ist unbrauchbar.

### Schritt 2: Build (einmalig, nicht Teil der Zeitmessung selbst)

- **Was:** `dotnet build AiNetLinter.slnx` frisch fahren, grün + 0
  Warnungen verifizieren, bevor die eigentliche Testlauf-Messung beginnt
  (`--no-build` in Schritt 3 setzt das voraus).
- **Warum:** Die Messung soll reine Testlaufzeit erfassen, nicht
  Build-Zeit vermischen (`--no-build`-Konvention ist bereits in der
  Tech-Stack-Notiz als „Abschluss-Verifikation“ festgelegt).

### Schritt 3: Volllauf mindestens zweimal zeitgestoppt fahren

- **Was:** `dotnet test AiNetLinter.slnx --no-build` **zweimal
  hintereinander** ausführen, jeweils mit Wall-Clock-Zeitstoppung (z. B.
  PowerShell `Measure-Command { dotnet test AiNetLinter.slnx --no-build }`).
  Nach jedem Lauf `TestResults/latest.trx` auslesen (Start-/Endzeitstempel
  bzw. `Times`-Element) als zweite, unabhängige Quelle.
- **Warum:** Eine Einzelmessung kann durch System-Rauschen (Caching,
  Hintergrundprozesse) verzerrt sein — zwei Läufe zeigen, ob das Ergebnis
  stabil ist (Ziel ist eine „belegte Verbesserung“ laut DoD, kein
  Zufallstreffer). Zwei Quellen (Shell-Stoppuhr + `.trx`) sind gegenseitige
  Plausibilitätsprüfung, keine doppelte Arbeit.

### Schritt 4: Vorher/Nachher-Vergleich dokumentieren

- **Was:** In `step-result.md` einen eigenen Abschnitt „Laufzeitmessung
  (F.6)“ ergänzen mit: Messmethode (Kommando + Werkzeug), beide
  Einzelmessungen (Wall-Clock + `.trx`-Wert), daraus abgeleiteter
  repräsentativer Wert (z. B. Mittelwert oder „beide Läufe X:YY, stabil“),
  Testzahl/Fehlerzahl je Lauf, und ein Vergleich gegen den in `Konzept.md`
  dokumentierten Vorher-Wert (~8 Min., vor step-001) **und** gegen die
  informelle step-001-Messung (~1 m 35–41 s) — mit Einordnung, ob sich der
  Wert seit step-001 verändert hat (durch F.2-F.5 wurden keine
  parallelitätsrelevanten Strukturen berührt, daher Erwartung: im selben
  Bereich, keine Regression).
- **Warum:** Genau das ist der Kern des DoD-Punkts F.6 — eine belegte,
  nachvollziehbare Zahl statt einer wiederholten Schätzung.

## Tests

Keine — dieser Step ändert keinen Code, es gibt nichts Neues zu testen.
Der Volllauf selbst (Schritt 3) **ist** die durchzuführende Aktion, nicht
ein zusätzlich zu schreibender Test.

## Definition of Done

- [ ] Offene `AiNetLinter.exe`-/`testhost.exe`-Prozesse vor Messbeginn
      geprüft/beendet
- [ ] `dotnet build AiNetLinter.slnx` grün, 0 Warnungen (einmalig, vor der
      Messung)
- [ ] `dotnet test AiNetLinter.slnx --no-build` mindestens zweimal
      zeitgestoppt gefahren, beide grün, identische Testzahl wie
      step-005-Baseline (1186) — jede Abweichung der Testzahl wird
      erklärt, nicht stillschweigend übernommen
- [ ] `TestResults/latest.trx` nach jedem Lauf als zweite Zeitquelle
      ausgelesen
- [ ] Abschnitt „Laufzeitmessung (F.6)“ in `step-006/step-result.md`
      mit Methode, Rohzahlen, Vergleich vorher/nachher
- [ ] Commit auf aktuellem Branch (Conventional Commit, Suffix
      `[codegraph-mcp-finish]`) — reiner Doku-Commit, da kein Code
      geändert wird
- [ ] `step-006/step-result.md` geschrieben
- [ ] `status` in `step-plan.md` von `in_progress` auf
      `done (pending audit)` gesetzt

## Rules-Refs

- `.agents/rules/AiNetLinterRichtlinien.mdc` §3 (Build & Test) — Pflicht,
  `TestResults/latest.trx` als Diagnosequelle zu nutzen statt Läufe blind
  zu wiederholen; hier zusätzlich als Zeitquelle verwendet, nicht nur zur
  Fehlerdiagnose. §5 (Zero-Warning-Direktive) — Build muss vor der Messung
  grün mit 0 Warnungen sein.
- `.agents/rules/AiNetLinterRichtlinien.mdc` §4 (Testsuite-Parallelität
  bewahren) — nicht direkt Gegenstand dieses Steps (keine Collection-
  Änderung), aber relevant für die Einordnung der Zahl: falls die Messung
  eine unerwartete Verlangsamung gegenüber step-001 zeigt, ist das ein
  Hinweis auf eine neue Regression in genau diesem Bereich (Prüfpunkt für
  den Kritiker, kein Auslöser für eine Code-Änderung in diesem Step selbst
  — das wäre Scope-Erweiterung).

## Bekannte Ausnahmen

Keine.

## Notes

- **Kein Fix-Step, falls die Zahl „schlechter“ aussieht als erwartet:**
  Sollte die formale Messung deutlich von der informellen
  step-001-Schätzung abweichen (z. B. weil in der Zwischenzeit doch eine
  neue `ConsoleTestCollection`-Mitgliedschaft oder ein neuer
  Subprozess-Test dazugekommen ist), ist das ein **Beobachtungsergebnis**,
  kein Auftrag, in diesem Step nachzubessern — Scope ist „messen und
  dokumentieren“, nicht „erneut optimieren“. Eine echte Regression würde
  der Kritiker als Konzept-Treue-Fund (Ebene 4) markieren; ein neuer Fix
  wäre dann ein eigenes, vom Planer im nächsten Step-Modus-Aufruf
  einzuplanendes Epic/Step, nicht Teil dieses Steps.
- **`roadmap.md` wird in diesem Step nicht vom Coder angefasst** — die
  finale Abhak-Markierung von `EPIC-01` (jetzt vollständig abgeschlossen,
  da F.1-F.6 danach alle erledigt sind) ist Aufgabe des Planers beim
  nächsten Step-Modus-Aufruf (Schritt 1 „Roadmap abgleichen“), analog zu
  jedem vorherigen Step in diesem Epic.
- **Nach diesem Step ist EPIC-01 vollständig fertig.** Der nächste
  Step-Modus-Aufruf plant den ersten Step von `EPIC-02`
  (Einheit-011-Abschluss, Muss-Haben A aus `Konzept.md`) — nicht Teil
  dieses Plans, nur als Kontext für den Coder/Kritiker, falls im Review
  auf den Gesamtfortschritt Bezug genommen wird.
