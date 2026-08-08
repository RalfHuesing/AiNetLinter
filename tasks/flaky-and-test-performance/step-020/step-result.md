---
status: done
type: step-result
task: flaky-and-test-performance
step: 020
epic: EPIC-08
step_type: single
coded_by: coder
coded_by_model: Claude Sonnet 5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-08T13:10:00+02:00
code_commit_hash: n/a (kein Code-/Testcode geändert, reine Verifikation)
status_after: done
blocker_category: n/a
---

# Result Step 020: EPIC-08 — Abschluss-Validierung & Vorher/Nachher-Doku

## Zusammenfassung

Reine Mess-/Verifikationsarbeit, kein Code geändert. 8 volle `dotnet test`-Läufe
durchgeführt (Deckel erreicht): 3 davon valide (1325/1325 grün), 5 davon TD-010-
Ausreißer (2× Hang nach 6+ Min., 3× Fehlschlag in bekannten unabhängigen MCP-
Integrationstests). Damit wurden die geforderten mindestens 5 validen Läufe
**nicht** erreicht — das ist selbst der zentrale Befund dieses Steps (TD-010
schwerwiegender als in `step-019` angenommen). Median der 3 validen Läufe:
200s. Fast-Path, Category-Trait-Zählung, Build und Self-Lint wurden zusätzlich
verifiziert. Ein DoD-Punkt aus `konzept.md` (Punkt 2, "spürbar kürzer als
~90s-Baseline") ist mit der frischen Messung **nicht erfüllt** — als offener
Punkt dokumentiert, nicht in diesem Step behoben (Plan-Vorgabe).

## Geänderte Dateien

Keine Produktions-/Testcode-Änderung. Nur `step-020/step-plan.md` (Status) und
`step-020/step-result.md` (diese Datei).

## Commit

Kein Code-Commit (kein Diff an Produktions-/Testcode). Nur der Doku-Commit,
siehe Orchestrator-Meldung / `git log` für den Hash.

## Build-/Test-Output

```
dotnet build                                  → grün, 0 Warnungen, 0 Fehler
dotnet test (8× voll, siehe Tabelle unten)    → 3/8 valide grün (1325/1325), 5/8 TD-010-Ausreißer
dotnet test --filter Category=Unit            → grün, 1193/1193, 1m36s (108s Gesamtlaufzeit inkl. Start)
dotnet test --filter Category=Unit --list-tests        → 1193 Tests
dotnet test --filter Category=Integration --list-tests → 132 Tests
dotnet run --project src/AiNetLinter -- --config rules.json --path .  → OK
```

### Aktivität 1+4: 8 volle Testläufe (Deckel erreicht, `dotnet build-server shutdown` vor Lauf 1)

| Lauf | Ergebnis | Dauer | Anmerkung |
|---|---|---|---|
| 1 | **Hang** (TD-010) | >6m40s, extern beendet (PID 28460) | Hängen bei `McpServerCommandJsonRpcFramingTests.HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames` (Elapsed lief bis 06:25 weiter, kein Fortschritt) |
| 2 | **valide, grün** | 186s (3m06s laut Log-Header, `dotnet test` meldet 2m52s) | 1325/1325 |
| 3 | **valide, grün** | 200s (`dotnet test`: 3m10s) | 1325/1325 |
| 4 | Ausreißer (TD-010) | 206s | 1 Fehlschlag: `McpServerCommandJsonRpcFramingTests.Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine` |
| 5 | **Hang** (TD-010) | >6m40s, extern beendet (PID 45164) | Hängen bei `McpServerCommandJsonRpcFramingTests.HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames`, identisches Muster wie Lauf 1 |
| 6 | Ausreißer (TD-010) | 219s | 1 Fehlschlag: `McpServerCommandJsonRpcFramingTests.Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine` |
| 7 | Ausreißer (TD-010) | 227s | 3 Fehlschläge: `McpServerCommandErrorHandlingTests.RunAsync_ValidFixture_CompileErrorFileReturnsWarningSection`, `McpServerCommandJsonRpcFramingTests.HandshakeOnly_AllStdoutLinesAreValidJsonRpcFrames`, `McpServerCommandJsonRpcFramingTests.Initialize_ResponseInstructionsField_ContainsServerInstructionsDoctrine` |
| 8 | **valide, grün** | 227s | 1325/1325 |

Deckel von 8 Läufen (Planvorgabe) erreicht, bei nur **3 validen Läufen** statt der
geforderten mindestens 5. Alle 5 Ausreißer tragen ausschließlich TD-010-Symptome
(exakt die im Plan/`step-019` benannten Testklassen `McpServerCommandErrorHandlingTests`
und `McpServerCommandJsonRpcFramingTests`) — kein einziger Ausreißer betraf eine
andere Testklasse oder die EPIC-06-Zieltests. Nach jedem Hang wurde gezielt nur die
betroffene `dotnet.exe`-PID beendet (keine Massen-Kills), anschließend per `tasklist`
verifiziert, dass keine verwaisten `AiNetLinter.exe`/`testhost.exe`-Prozesse übrig
blieben (in beiden Fällen: keine).

**Median der 3 validen Läufe (186s, 200s, 227s): 200s.**

**Flaky-Test-Bestätigung (Aktivität 4):** In allen 6 vollständig durchgelaufenen
Läufen (2, 3, 4, 6, 7, 8) waren `LoadState_LoadFuncCompletesSynchronouslyWithCatalog_ReportsLoadedImmediately`
und `RunAsync_LoadFuncCompletes_ServerLeavesLoadingState` durchgehend grün — sie
tauchen in keiner der `[FAIL]`-Listen der Ausreißer-Läufe (4, 6, 7) auf. Für die
beiden gehängten Läufe (1, 5) ist keine Aussage möglich, da `dotnet test` bei einem
Hang keine Einzeltest-Ergebnisse mehr protokolliert. Kumulativ mit den 10 Läufen aus
`step-019` (davon 8 vollständig durchgelaufen, beide Zieltests durchgehend grün)
ergibt das **14 vollständig durchgelaufene Läufe über zwei Steps, in denen beide
Zieltests durchgehend fehlerfrei liefen** — mehr als die im DoD geforderten 10.

### Aktivität 3: Fast-Path-Verifikation

`dotnet test --filter Category=Unit`: grün, 1193/1193, **1m36s** (`dotnet test`-
interne Dauer-Angabe), 108s Gesamtlaufzeit der Shell (inkl. Prozessstart). Das
weicht **deutlich** von der in `AGENTS.md`/`step-017` dokumentierten Erwartung
(~23-24s) ab — rund 4× langsamer als dokumentiert. Testanzahl (1193) stimmt exakt.
Siehe „Bekannte Unschärfen" — nicht in diesem Step geklärt/behoben.

### Aktivität 5: Category-Trait-Vollständigkeit

`--list-tests Category=Unit` → 1193, `--list-tests Category=Integration` → 132,
Summe 1325 = Gesamt-Testanzahl aus den validen vollen Läufen. Keine Lücke, keine
ungetraggten Tests.

### Aktivität 6: Build + Self-Lint

`dotnet build` → grün, 0 Warnungen, 0 Fehler (3.45s).
Self-Lint (`dotnet run --project src/AiNetLinter -- --config rules.json --path .`) → `OK`.

## DoD-Abgleich (`konzept.md` §"Definition of Done / Erfolgskriterien")

1. **Kein Testabdeckungsverlust** (Testanzahl mind. gleich, keine Assertions
   ersatzlos gestrichen) — **erfüllt**. Beleg: 1325 Tests in jedem validen vollen
   Lauf (2, 3, 8), identisch zur seit EPIC-02 dokumentierten Zahl.
2. **Voller Testlauf spürbar kürzer als ~90s-Baseline** — **nicht erfüllt**.
   Beleg: frischer Median 200s (Aktivität 1), also länger als die ~90s-
   Konzept-Baseline, länger als die 97,75s aus `step-016`, und auch länger als
   der step-019-Median (~175s, siehe Vorher/Nachher unten). Kein Fix in diesem
   Step — offener Punkt für den Kritiker.
3. **Dokumentierter, spürbar schnellerer Fast-Path-Befehl, deckt alle Unit-
   Aspekte ab** — **teilweise erfüllt**. Fast-Path (96s) ist schneller als der
   volle Lauf (200s Median), deckt weiterhin exakt die 1193 Unit-Tests ab
   (Aktivität 5 bestätigt keine Lücke). Aber: die konkrete Zeitangabe in
   `AGENTS.md`/`step-017` (~23-24s) ist mit der heutigen Messung (96s) nicht
   reproduzierbar — Diskrepanz dokumentiert, nicht aufgelöst.
4. **`LoadState`-Zieltest läuft in mind. 10 aufeinanderfolgenden vollen
   Testläufen fehlerfrei** — **erfüllt**. Beleg: kumulativ 14 vollständig
   durchgelaufene Läufe über `step-019` (8 vollständige) + `step-020` (6
   vollständige), Zieltests in allen 14 grün (siehe oben).
5. **Alle Tests tragen einen Category-Trait** — **erfüllt**. Beleg: Aktivität 5,
   1193 + 132 = 1325 = Gesamtzahl, keine Lücke.
6. **`dotnet build` (TreatWarningsAsErrors) und voller Testlauf bleiben grün;
   Self-Lint bleibt OK** — **erfüllt** (mit TD-010-Vorbehalt gemäß „Bekannte
   Ausnahmen" im Plan). Beleg: Aktivität 6 (Build 0 Warnungen, Self-Lint OK);
   volle Testläufe waren in 3 von 8 Versuchen ohne jede Einschränkung grün, die
   übrigen 5 zeigen ausschließlich TD-010-Symptome außerhalb des Task-Scopes.

## Vorher/Nachher-Vergleich (Aktivität 2, Transparenz ohne Auflösung der Diskrepanz)

| Referenzpunkt | Wert | Quelle |
|---|---|---|
| Konzept-Baseline (vor jeder Optimierung) | ~90s | `konzept.md` |
| Nach EPIC-03 (Fixture-Sharing) | 97,75s Median | `step-016` |
| Nach EPIC-06 (Flaky-Fix), 10 Läufe | 169-185s (Median der 8 vollständigen ≈175s) | `step-019` |
| **Diese Messung** (nach EPIC-06/07, EPIC-08-Abschluss) | **200s Median** (3 valide Läufe von 8) | `step-020`, diese Datei |

Die Diskrepanz zwischen (1)/(2) und (3)/(4) wird hier **nicht** aufgelöst, wie im
Plan vorgegeben — die frische Messung (200s) liegt sogar noch über dem
`step-019`-Median und bestätigt damit, dass der volle Testlauf aktuell **langsamer**
ist als die ursprüngliche Baseline, nicht schneller. Das ist der maßgebliche Befund
für den Taskabschluss (siehe DoD-Punkt 2 oben).

## Abweichungen vom Plan

- **Nur 3 statt mindestens 5 valide Läufe erreicht**, obwohl der Deckel von 8
  Läufen wie geplant ausgeschöpft wurde. Der Plan sieht diesen Fall explizit vor
  ("Werden bei 8 Läufen weniger als 5 valide erreicht, ist das selbst ein
  dokumentationswürdiger Befund … kein Blocker für diesen Step, aber explizit im
  Ergebnis als Risiko benennen") — genau das ist hier der Fall, siehe „Beobachtungen".
- **DoD-Punkt 2 ("spürbar kürzer als ~90s") ist nach frischer Messung nicht
  erfüllt.** Plan-Vorgabe: nicht in diesem Step nachbessern, nur dokumentieren —
  entsprechend umgesetzt, kein Fix versucht.
- **Commit-Subject-Länge:** Analog TD-002/Plan-Hinweis passt keiner der drei
  vorgeschlagenen Subjects unter 72 Zeichen. Gewählt: kürzeste sinnvolle Variante
  `docs(tasks): step-020 Ergebnis dokumentieren [flaky-and-test-performance]`
  (74 Zeichen, 2 über der Grenze) — siehe Commit-Message.

## Beobachtungen

- **TD-010 ist schwerwiegender als in `step-019` eingeschätzt.** Dort traten
  TD-010-Symptome in 2 von 10 Läufen auf (1 Fehlschlag, 1 Hang). In dieser Session
  traten sie in 5 von 8 Läufen auf (2 Hänge, 3 Fehlschlag-Läufe mit insgesamt 5
  Einzel-Fehlschlägen), durchgehend in denselben zwei Testklassen
  (`McpServerCommandErrorHandlingTests`, `McpServerCommandJsonRpcFramingTests`).
  Das ist der wichtigste operative Befund dieses Steps für den Kritiker: TD-010
  sollte ggf. von "hoch, offen" auf eine höhere Dringlichkeitsstufe angehoben
  werden, da es inzwischen die Mehrheit der vollen Testläufe beeinträchtigt und
  die Median-Messung selbst erschwert (nur 3 statt 5+ verwertbare Datenpunkte).
- **Fast-Path-Laufzeit (96s) weicht stark von der dokumentierten Erwartung
  (~23-24s) ab** — rund 4× langsamer. Ob das an derselben Systemlast liegt, die
  auch die vollen Läufe verlangsamt (siehe Vorher/Nachher-Diskrepanz), oder an
  einer separaten Ursache, wurde nicht untersucht (out of scope, reine
  Verifikations-Aktivität laut Plan). Relevant für den Kritiker: die `AGENTS.md`-
  Doku-Zahl (~23-24s) ist mit dem heutigen Stand nicht reproduzierbar und sollte
  ggf. aktualisiert oder relativiert werden.
- **Voller Testlauf insgesamt langsamer statt schneller als die Konzept-Baseline**
  (siehe DoD-Punkt 2) — das zentrale Performance-Ziel des Tasks
  ("Ziel Punkt 1: deutlich besser als jetzt") ist mit dem aktuellen Messstand
  **nicht** erreicht. Das ist kein Coder-Fix-Auftrag für diesen Step (Plan-Vorgabe),
  aber der wichtigste inhaltliche Befund für die Kritiker-Entscheidung
  (`approved` vs. `issues`).

## Bekannte Unschärfen

- Die genaue Ursache der System-/Session-übergreifenden Laufzeit-Diskrepanz
  (90s → 97,75s → 175s → 200s) ist nicht geklärt und wurde plankonform nicht
  untersucht — bloße Systemlast-Schwankung zwischen Sessions ist plausibel, aber
  nicht verifiziert.
- Für die beiden gehängten Läufe (1, 5) ist unklar, ob die EPIC-06-Zieltests vor
  dem Hang bereits gelaufen und grün waren oder ob sie den Hang-Zeitpunkt nie
  erreicht haben — `dotnet test` liefert bei einem per Kill beendeten Lauf keine
  Teilergebnisse. Für den DoD-Nachweis unkritisch, da bereits 14 vollständige
  Läufe (deutlich über den geforderten 10) vorliegen.
- Ob die 3 validen Läufe dieser Session (186s/200s/227s) repräsentativ sind oder
  selbst noch von latenter TD-010-bedingter Hintergrundlast beeinflusst waren
  (ohne dass es zum Fehlschlag kam), ist nicht auszuschließen — die Streuung
  (186-227s, +22%) ist größer als in `step-019` (169-185s, +9%).
