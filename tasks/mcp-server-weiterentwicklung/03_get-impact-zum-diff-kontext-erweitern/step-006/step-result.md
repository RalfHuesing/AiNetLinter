---
status: done
type: step-result
task: 03_get-impact-zum-diff-kontext-erweitern
step: 006
epic: EPIC-3+EPIC-4
step_type: single
coded_by: coder
coded_by_model: stealth/ox-alpha
coded_by_model_knowledge_cutoff: unbekannt
coded_at: 2026-08-23T09:03:14+02:00
code_commit_hash: 4b53579a
status_after: done
blocker_category: n/a
---

# Result Step 006: Korrektur step-004 — Quoting des Mehrklassen-Filters

## Zusammenfassung

Das MAJOR-Finding aus dem step-004-Review ist behoben:
`TestRecommendationBuilder.BuildCommand` setzt den Filterwert ab mehr als
einer Trefferklasse in doppelte Anführungszeichen, sodass der `|`-Verbund
als Shell-Zeile in cmd/PowerShell/bash direkt ausführbar bleibt. Der
Einzelklassenbefehl ist byteidentisch zum vorherigen Stand (keine Quotes),
damit die Bestands-Asserts in `GetTestContextToolTests` unverändert grün
bleiben. Der Erwartungs-String des Mehrklassen-Befehlstests trägt die
Quotes. Keine weiteren Änderungen — Scope exakt das Finding.

## Geänderte Dateien

- `src/AiNetLinter/Mcp/Tools/TestContext/TestRecommendationBuilder.cs` —
  `BuildCommand`: Filterwert wird bei `classNames.Count > 1` mit `"`
 gewrappt (ein kurzer Why-Kommentar dazu); Einzelklassen-Zweig unangetastet.
- `src/AiNetLinter.FastTests/Core/TestCoverageBatchScannerTests.cs` — nur die
  eine Erwartung-Assertion im Test
  `BuildDotNetTestCommands_TwoHitClassesInSameProject_YieldOneDeduplicatedCommand`
  um die Anführungszeichen ergänzt (exakter Stringvergleich bleibt).

## Commit

- **Code-Commit-Hash:** `4b53579a`
- **Message:**
  ```
  fix: Mehrklassenfilter quoten [03_get-impact-zum-diff-kontext-erweitern]

  Der deduplizierte dotnet-test-Befehl fuer mehr als eine Trefferklasse
  enthielt einen unquotierten | und zerbrach als Shell-Zeile in eine Pipe.
  Ab zwei Klassen steht der Filterwert jetzt in doppelten Anfuehrungs-
  zeichen und ist damit in cmd, PowerShell und bash direkt ausfuehrbar;
  der Einzelklassenbefehl bleibt unveraendert unquotiert. Der Erwartungs-
  string des Mehrklassen-Befehlstests ist angepasst.

  Refs: tasks/mcp-server-weiterentwicklung/03_get-impact-zum-diff-kontext-erweitern/step-006
  ```
- **Branch:** main
- **Push:** nein (lokal)
- **Doku-Commit:** separater, zweiter Commit (Hash steht nicht hier
  drin — Selbstbezug, siehe `git log`).

## Build-/Test-Output

```
dotnet build                                                          → grün (0 Warnungen, 0 Fehler)
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress       → grün (1605 Tests, 0 Fehler)
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress → grün (348 Tests, 0 Fehler)
```

Schnelliteration vorab: beide betroffenen Testklassen per
FullyQualifiedName-Filter (16/16 grün), danach einmal der volle Gate-Lauf
oben — nicht wiederholt.

## Abweichungen vom Plan

Keine — Fix-Anweisung des Reviews 1:1 umgesetzt. Zwei Randnotizen zur
Umsetzungsform, keine Abweichungen vom Inhalt:

1. Die Quoting-Bedingung sitzt als eigene Zeile (`classNames.Count > 1 ?`
   `"{filterValue}"` : filterValue) statt wie im Review-Text skizziert als
   `$"--filter \"{filter}\""`-Stringvariante — funktional identisch, aber der
   Einzelklassen-Zweig bleibt so wörtlich derselbe Ausdruck wie vorher.
2. Der DoD-Punkt „Refs step-005" im Step-Plan ist ein Tippfehler; committet
   wurde gemäß Orchestrator-Vorgabe/SKILL mit `Refs: …/step-006`.

## Beobachtungen

- Die Bestands-Asserts (`GetTestContextToolTests.cs:162` Substring,
  `:347` RecommendedTestCommands-Contains) sind im Einzelklassenfall
  unberührt und liefen unverändert grün — genau wie vom Review prognostiziert.
- Der gequotete Befehl ist jetzt für cmd/PowerShell/bash kopierbar; ein
  echter Shell-Ausführungstest (Befehl tatsächlich in eine Shell pasten und
  laufen lassen) liegt außerhalb der Testinfrastruktur und wurde nicht
  geführt — abgesichert ist der Vertrag über den exakten Stringvergleich.
- Arbeitskopie war vor dem Step sauber (kein fremdes task-state.md-Gerumpel
  diesmal); add war strikt file-weise auf die zwei Plan-Dateien.

## Bekannte Unschärfen

- Die Quote-Regel deckt Klassennamen ohne eingebettete `"` ab — das ist bei
  FQN-Klassennamen aus Roslyn-Metadaten der Realität angemessen; ein Name
  mit `"` würde trotzdem einen kaputten Befehl liefern (vorher wie nachher).
- Der Kommentar in `BuildCommand` nennt das Why (Shell-Sonderzeichen),
  verzichtet aber bewusst auf Shell-Aufzählung — falls der Kritiker dort
  Präzision will: gern ergänzen.
