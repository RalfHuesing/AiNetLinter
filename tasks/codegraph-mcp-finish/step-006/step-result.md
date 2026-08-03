---
status: done
type: step-result
task: codegraph-mcp-finish
step: 006
epic: EPIC-01
step_type: single
coded_by: coder
coded_by_model: claude-sonnet-5
coded_by_model_knowledge_cutoff: 2026-01
coded_at: 2026-08-03
code_commit_hash: n/a
status_after: done
blocker_category: n/a
---

# Result Step 006: Volllauf-Laufzeitmessung formal dokumentieren (F.6)

## Zusammenfassung

Reiner Mess-/Dokumentations-Step, kein Code geändert. Vor der Messung
wurden offene `AiNetLinter.exe`-/`testhost.exe`-Prozesse geprüft (vor
Run 1: keine; vor Run 2: ein hängender `testhost.exe` gefunden und
beendet). `dotnet build AiNetLinter.slnx` lief einmalig grün mit 0
Warnungen. Danach `dotnet test AiNetLinter.slnx --no-build` zweimal
zeitgestoppt gefahren (PowerShell `Measure-Command`), jeweils mit
`TestResults/latest.trx`-Zeitstempeln als zweite Quelle gegengeprüft.
Beide Läufe: 1186 Tests, 0 Fehler — identisch zur step-004/-005-Baseline.
Ergebnis siehe Abschnitt „Laufzeitmessung (F.6)" unten.

## Geänderte Dateien

Keine Produktions-/Testcode-Datei geändert (wie im Plan vorgesehen).
Einzige neue Datei: `tasks/codegraph-mcp-finish/step-006/step-result.md`
(dieses Dokument).

## Commit

- **Code-Commit-Hash:** entfällt — kein Code geändert, daher nur ein
  Doku-Commit (Hash siehe `git log`, referenziert `step-006` direkt).
- **Branch:** main
- **Push:** nein (lokal)

## Laufzeitmessung (F.6)

**Methode:** Vor jedem Lauf `Get-Process AiNetLinter,testhost` geprüft
(Run 2: ein hängender `testhost.exe` gefunden, per `Stop-Process -Force`
beendet). Einmaliger `dotnet build AiNetLinter.slnx` (grün, 0 Warnungen,
3.06s) vor der eigentlichen Messung. Danach zwei Läufe von
`dotnet test AiNetLinter.slnx --no-build`, je per PowerShell
`Measure-Command` zeitgestoppt (Wall-Clock) und zusätzlich per
`TestResults/latest.trx` (`<Times start=... finish=.../>`) gegengeprüft.

| Lauf | Wall-Clock (Measure-Command) | dotnet-Testframework-Dauer | TRX-Duration (Start→Finish) | Tests | Fehler |
|---|---|---|---|---|---|
| Run 1 | 00:01:40.28 | 1 m 38 s | 00:01:39.53 | 1186 | 0 |
| Run 2 | 00:01:35.67 | 1 m 33 s | 00:01:35.02 | 1186 | 0 |

Alle drei Quellen je Lauf (Shell-Stoppuhr, dotnet-eigene Dauerangabe,
`.trx`-Zeitstempel) stimmen pro Lauf auf unter 1 Sekunde überein — keine
Diskrepanz, die auf eine verfälschte Messung hindeutet. Beide Läufe
liegen im selben Bereich (Differenz ~4.6s, plausibles System-Rauschen,
kein Trend).

**Repräsentativer Wert:** ~1 m 35–40 s (Mittelwert Wall-Clock: 1 m 38.0 s).

**Vergleich:**
- **Gegen `Konzept.md`-Vorher-Wert (~8 Min., vor step-001):** klare,
  belegte Verbesserung — Faktor ~4.9x schneller (8 Min. → ~1 m 38 s im
  Mittel).
- **Gegen informelle step-001-Messung (~1 m 35–41 s):** die formale
  Messung bestätigt den informellen Wert nahezu exakt (Run 2 mit
  1 m 35.67 s liegt am unteren Rand, Run 1 mit 1 m 40.28 s knapp über dem
  damaligen oberen Rand von 1 m 41 s — beide innerhalb der ursprünglich
  beobachteten Bandbreite). Kein Hinweis auf Regression durch F.2-F.5:
  wie in der JIT-Kontext-Analyse erwartet, haben die zwischenzeitlichen
  Steps (reine Boilerplate-/Config-Konsolidierung, keine
  Parallelitäts-relevanten Strukturänderungen) die Laufzeit nicht
  messbar verändert.

**Testzahl-Abgleich:** beide Läufe 1186 Tests, 0 Fehler — identisch zur
step-004/-005-Baseline, keine Abweichung zu erklären.

## Build-/Test-Output

```
dotnet build AiNetLinter.slnx           → grün, 0 Warnungen (3.06s)
dotnet test AiNetLinter.slnx --no-build → grün (Run 1: 1186 Tests, 0 Fehler, 1m40.28s Wall-Clock)
dotnet test AiNetLinter.slnx --no-build → grün (Run 2: 1186 Tests, 0 Fehler, 1m35.67s Wall-Clock)
```

## Abweichungen vom Plan

Keine — Plan 1:1 umgesetzt. Vor Run 2 wurde zusätzlich ein hängender
`testhost.exe`-Prozess gefunden (nicht vor Run 1), der laut Plan-Kontext
genau die bekannte Fußangel ist — wurde vor der Messung beendet, hat
also keine der beiden Messungen verfälscht. Das ist keine Planabweichung,
sondern die im Plan bereits vorgesehene Vorsichtsmaßnahme, die hier
tatsächlich einmal gegriffen hat.

## Beobachtungen

- Der hängende `testhost.exe`-Prozess nach Run 1 (vor dessen eigenem
  Start noch nicht vorhanden) bestätigt die in `Konzept.md` dokumentierte
  Datei-Sperren-Fußangel als real und aktuell reproduzierbar — kein
  Handlungsbedarf in diesem Step (reine Messung), aber ein Hinweis für
  den Kritiker, dass die Prozess-Prüfungs-Konvention in der
  Tech-Stack-Notiz weiterhin notwendig bleibt und nicht nur historisch
  ist.
- Keine weiteren Beobachtungen außerhalb des Scopes.

## Bekannte Unschärfen

- Zwei Läufe statt einer größeren Stichprobe (wie im Plan als
  Mindestanzahl vorgegeben) — die Übereinstimmung von Wall-Clock- und
  TRX-Werten pro Lauf sowie die Nähe zur step-001-Bandbreite geben aber
  keinen Anlass, eine höhere Varianz zu vermuten, die weitere Läufe
  rechtfertigen würde.
- Die Messung lief auf der aktuellen Entwicklungsmaschine unter
  ansonsten normaler Last (keine dedizierte isolierte Messumgebung) —
  wie schon bei der informellen step-001-Messung ist das eine
  Wall-Clock-Messung im laufenden Entwicklungsbetrieb, kein
  Laborbenchmark. Für den DoD-Zweck („belegte Verbesserung, keine
  Zielprozentzahl") ausreichend.
