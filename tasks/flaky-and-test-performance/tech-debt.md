---
task: flaky-and-test-performance
type: tech-debt-log
maintained_by: kritiker
last_updated: 2026-08-07T10:35:00+02:00
---

# Tech-Debt-Log: flaky-and-test-performance

Append-only. Jeder Eintrag ist eine vom Kritiker während eines
Step-Reviews beobachtete, aber bewusst **nicht** gefixte Auffälligkeit
außerhalb des Scopes des jeweiligen Steps (Architektur, Anti-Pattern,
Duplikation, Konsistenz) — siehe `../spec.md` §8.3/§9.

**Priorität ist reine Sortierhilfe für den Menschen, kein Auslöser.**

## Index

| ID | Bereich / Datei | Priorität | Kurzfassung |
|---|---|---|---|
| TD-001 | `src/AiNetLinter/Cli/` + `konzept.md` | mittel | Konzept-/roadmap.md verweisen auf `--self-lint` als Self-Lint-Befehl, CLI-Option existiert nicht. |
| TD-002 | `tasks/.../step-*` + `.agents/.../coder/SKILL.md` §Schritt-5 | niedrig | Subject-Längen-Disziplin: 72-Zeichen-Grenze wird in mehreren Schritten überschritten; Plan-DoD-Vorgaben teilweise ungenau. |

## Einträge

### TD-001 — Fehlende CLI-Option `--self-lint` (Konzept-/CLI-Diskrepanz) [Priorität: mittel]

- **Gefunden in:** step-001 (Kritiker-Review vom 2026-08-07T10:00:00+02:00)
- **Ort:** `tasks/flaky-and-test-performance/roadmap.md:26`, `tasks/flaky-and-test-performance/step-001/step-plan.md:60,131,151` (Plan/DoD-Referenz), `src/AiNetLinter/Cli/CliOptionFactory.cs` (fehlend — verifiziert per `grep "--self-lint"` über `src/AiNetLinter/Cli/`, kein Treffer)
- **Befund:** Sowohl das Konzept des Tasks (`konzept.md` implizit über die `roadmap.md`-Linie 26) als auch der `step-001/step-plan.md` (Definition of Done) referenzieren `dotnet run --project src/AiNetLinter -- --self-lint` als Self-Lint-Verifikation. Die aktuelle CLI in `CliOptionFactory.cs` kennt diese Option nicht (vorhandene Optionen reichen von `--config` über `--playbook` bis `--mcp-server` — kein `--self-lint`). Der Coder hat im `step-result.md` korrekt dokumentiert und mit `dotnet run --project src/AiNetLinter -- --config rules.json --path .` (Ausgabe `OK`) semantisch identisch ersetzt. Die **Konzept-Spec ist also veraltet** (oder die CLI-Option wurde bei einem früheren Refactoring vergessen).
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-001 (Spike zur Fixture-Sharing-Mechanik). Die Diskrepanz betrifft zwei Ebenen — eine CLI-Option nachrüsten *und* das Konzept/roadmap/Plan-DoD korrigieren — beides ist Orchestrator-/Nutzer-Entscheidung, kein Spike-Fix.
- **Vorschlag:** Nutzer entscheidet eine der beiden Richtungen: (a) CLI-Option `--self-lint` als Convenience-Alias für `--config rules.json --path .` (oder mit hartcodiertem Self-Path) in `CliOptionFactory.cs` nachrüsten und in `Docs/configuration.md` dokumentieren; oder (b) Verweise in `roadmap.md` und ggf. Konzept/Plan-DoD auf den existierenden Befehl korrigieren. Variante (a) ist die weniger invasive (Spec-treu) — die Spec war vermutlich Absicht, nicht Versehen.
- **Status:** offen
- **Nutzer**: NICHT UMSETZEN! OUT OF SCOPE!

### TD-002 — Subject-Längen-Disziplin bei Code-/Doku-Commits (Skills/Plan-Genauigkeit) [Priorität: niedrig]

- **Gefunden in:** step-003 (Kritiker-Review vom 2026-08-07T10:35:00+02:00), Muster aus step-001 (94-Zeichen-Review-Commit `71ab96b`) und step-002 (74-Zeichen-Doku-Commit `79d3d6d`) bestätigt die Wiederholungs-Charakteristik.
- **Ort:** `tasks/flaky-and-test-performance/step-003/step-plan.md:259-260` (Plan-DoD mit falscher Längenangabe "71 Zeichen"), `tasks/flaky-and-test-performance/step-003/step-result.md:53` (Doku-Commit-Subject 91 Zeichen), `67fb86b` (Code-Commit-Subject 85 Zeichen); `skills/coder/SKILL.md:97` ("Subject ≤ 72 Zeichen inkl. Suffix"), `spec.md` §10.3 Z. 481-482 (Suffix zählt gegen 72-Grenze), `spec.md` §10.7 (History-Reset absolut verboten).
- **Befund:** Die 72-Zeichen-Grenze für Commit-Subjects (siehe `skills/coder/SKILL.md` §Schritt-5 + `spec.md` §10.3) wird in diesem Task mehrfach überschritten — bislang ohne Code-/Test-Impact, aber als wiederkehrende Stil-Abweichung. Konkrete Schritte:
  - `step-001` Review-Commit `71ab96b` (`docs(task): step-001 Review dokumentieren (Verdict: approved) [flaky-and-test-performance] …`): 490 Zeichen **bzw. nur-Subject 94 Zeichen** (Body ohne Blankline an Subject angeflanscht — separates Format-Issue, siehe unten) — **Subject 94 Zeichen, 22 über Grenze**, approved.
  - `step-002` Doku-Commit `79d3d6d` (`docs(tasks): step-002 Result dokumentieren [flaky-and-test-performance]`): 71 Zeichen, **knapp unter Grenze** — approved.
  - `step-003` Code-Commit `67fb86b` (`chore(tests): Metrics-Tests mit Category-Traits versehen [flaky-and-test-performance]`): **85 Zeichen, 13 über Grenze** — Plan-DoD hatte fälschlich "71 Zeichen, unter 72" vorgegeben; Coder hat die Abweichung dokumentiert, aber **nicht** amendet (per `spec.md` §10.7 verboten).
  - `step-003` Doku-Commit `03b04f4` (`docs(tasks): step-003 Result und Status 'done (pending audit)' [flaky-and-test-performance]`): **91 Zeichen, 19 über Grenze** — approved mit MINOR, da Coder keine Korrektur-Möglichkeit hat.
  - **Zusatzbefund (sekundär):** Mehrere step-001/step-002/step-003-Planungs- und Review-Commits haben Subject + Body **ohne Leerzeile** als einen einzigen Long-Line committet (z. B. `4e3044d` = 413 Zeichen in `%s`, `1aafb19` = 519 Zeichen) — `git log --oneline` zeigt dann den ganzen Text inkl. Bullet-Liste; das ist ein separates Commit-Format-Issue (fehlende Trennung Subject/Body) und wird hier nur als Beobachtung vermerkt.
- **Warum nicht sofort gefixt:** Außerhalb des Scopes von step-003 (rein additives Attribut auf Klassen-Ebene, Subject-Länge ist eine Commit-Message-Frage). Eine nachträgliche Korrektur der bereits committeten Subjects ist per `spec.md` §10.7 absolut verboten (`git commit --amend`, `git rebase`, `git reset --hard/--soft` auf bereits committete Commits, `git filter-branch`/`filter-repo`, Force-Push — alle ausnahmslos verboten). Die Korrektur kann erst beim nächsten Code-Commit desselben Tasks oder in einem separaten Rule-Update-Step erfolgen.
- **Vorschlag (zwei alternative Richtungen — Orchestrator-/Nutzer-Entscheidung):**
  - **(a) Planer-Disziplin + Skill-Präzisierung:** Planer gibt in `step-plan.md` DoD einen **konkreten Subject-String** mit korrekter Längenangabe vor (z. B. `chore(tests): Metrics-Traits [flaky-and-test-performance]` = 56 Zeichen) und verzichtet auf Body-Daten, die der Coder selbst gut formulieren kann. `skills/coder/SKILL.md` §Schritt-5 könnte um die explizite Empfehlung "bei absehbarer Subject-Länge >60 Zeichen, im Plan-DoD alternative kürzere Subject-Vorschläge auflisten" ergänzt werden. Coder akzeptiert den Subject-Vorschlag, ggf. mit leichter Anpassung.
  - **(b) Regel-Lockerung für Doku-Commits:** `AiNetLinterRichtlinien.mdc` §4 könnte um eine explizite Ausnahme "Für `docs(...)`- und `chore(task)`-Commits gilt eine gelockerte Obergrenze von 100 Zeichen, da der Subject hier primär Doku-/Audit-Funktion hat und keine Code-Änderung beschreibt" ergänzt werden. Vorteil: pragmatisch, kein zusätzlicher Planer-Aufwand, keine künstlich verkürzten Subject-Strings. Nachteil: Regel-Ausnahme → schwerer zu merken, könnte für Nicht-Doku-Commits als Präzedenz missbraucht werden.
  - **Empfehlung:** Variante (a) — sie ist Spec-treu, ändert die Regel nicht und verlagert die Disziplin in den Planer-Aufruf, wo sie hingehört (Längenvorgabe ist Planer-Wissen, nicht Coder-Wissen).
- **Status:** offen
