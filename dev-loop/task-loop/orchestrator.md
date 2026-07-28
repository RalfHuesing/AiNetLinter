---
workflow: initial-workflow
version: 0.3
status: draft
role: orchestrator
invoked_as: "orchestrator.md <task-dir> (Pfad zu diesem Ordner ist projektabhängig)"
depends_on: ./spec.md
---

# Orchestrator: Initial-Workflow

## Pfad-Hinweis

Alle Pfade in dieser Datei, die auf andere Dateien **innerhalb von
`dev-loop/`** verweisen (`spec.md`, `skills/**`, `templates/**`,
`../planning/…`), sind relativ zu dieser Datei zu verstehen —
funktionieren unabhängig davon, wo `dev-loop/` in deinem Projekt liegt.
Verweise auf **projekteigene** Konventionen (`<rules_dir>/**`, erkannt
gemäß `spec.md` §3.1; `README.md`, `docs/**`) meinen dagegen den Ort
relativ zu deinem **Projekt-Root** (wo der Agent gerade arbeitet) —
unabhängig davon, wo
`dev-loop/` selbst liegt. Das Task-Verzeichnis (`<task-dir>`) wird bei
jedem Aufruf explizit übergeben und nirgends als fester Name/Pfad
angenommen — es kann irgendwo in deinem Projekt liegen.

## Zweck

Du wirst als frische Session mit dieser Datei plus einem Task-Verzeichnis
aufgerufen, z. B.:

> `<pfad-zu-dev-loop>/task-loop/orchestrator.md tasks/audit-2026-07-24`

Ab jetzt bist du der **Orchestrator** für diesen Task (Rolle definiert in
`spec.md` Abschnitt 4). Diese Datei ist deine Handlungsanweisung —
`spec.md` ist die Referenz/Spezifikation dahinter, lies sie vollständig,
bevor du loslegst.

Diese Datei ist bewusst **tool-agnostisch** formuliert — kein bestimmtes
Coding-Agent-Tool vorausgesetzt, funktioniert unverändert mit jedem
Werkzeug, das Subagenten/Sub-Konversationen mit isoliertem Kontext
starten kann. Ein Abschnitt am Ende hält die wenigen Punkte fest, die
du beim jeweils verwendeten Werkzeug konkret nachschlagen musst.

## Schritt 0 — Eingabe validieren

- Prüfe, dass `<task-dir>` existiert und mindestens eine `.md`-Datei enthält.
- Fehlt beides: melde das dem Nutzer und stoppe. Erfinde keinen Task-Inhalt.

## Schritt 1 — Zustand feststellen

Prüfe, ob `<task-dir>/task-state.md` existiert.

**Fall A — Datei existiert nicht (frischer Task):**
1. Ermittle `rules_dir` (Details: `spec.md` §3.1): prüfe zuerst, ob
   `<task-dir>/konzept.md` existiert und dort im Frontmatter `rules_dir`
   gesetzt hat — falls ja, übernehmen, keine erneute Erkennung/Rückfrage.
   Sonst selbst erkennen: `.agents/rules/` und `.cursor/rules/`
   (projekt-root-relativ) prüfen — genau einer vorhanden → automatisch
   übernehmen; beide oder keins vorhanden → Nutzer offen fragen (auch ein
   dritter, hier nicht gelisteter Pfad oder „keine Konventionen" sind
   gültige Antworten).
2. Lege `<task-dir>/task-state.md` an (Template
   `templates/task-state.md`), Status `executing`, `rules_dir` im
   Frontmatter eintragen.
3. Rufe die Planer-Rolle auf (siehe Schritt 3) mit dem Auftrag "Plane den
   gesamten Task".
4. Fahre fort mit Schritt 4 (Loop).

**Fall B — Datei existiert, Status `executing`:**
- **Automatisch fortsetzen, ohne nachzufragen.** Lies `current_step` und die
  Steps-Tabelle, ermittle den nächsten offenen/unfertigen Step und mache dort
  weiter (Schritt 4).
- Fehlt `rules_dir` im Frontmatter (Alt-Task von vor dieser Konvention):
  einmalig nach obigem Verfahren nachträglich ermitteln und ergänzen,
  dann normal fortfahren — keine Sonderbehandlung darüber hinaus.
- Melde dem Nutzer kurz und knapp: *"Laufenden Task gefunden (`<name>`),
  setze fort bei `step-NNN`."* — das genügt, keine Rückfrage nötig.

**Fall C — Datei existiert, Status `blocked`:**
- **Nicht automatisch weitermachen.** Lies den letzten `step-review.md` mit
  Verdict `blocked` bzw. die Blocker-Notiz in `task-state.md`.
- Melde dem Nutzer die offene Frage/Entscheidung und warte auf Antwort.
- Erst nach Klärung durch den Nutzer: Status zurück auf `executing`, weiter
  mit Schritt 4.

**Fall D — Datei existiert, Status `done` oder `aborted`:**
- Melde dem Nutzer: Task ist bereits abgeschlossen (`done`) bzw. abgebrochen
  (`aborted`), verweise auf `task-summary.md`.
- Frage, ob der Task erneut angestoßen werden soll (z. B. weil neue Punkte
  in der Aufgaben-Doku ergänzt wurden). Nur mit Bestätigung neu starten.

## Schritt 2 — Rollen als Subagenten aufrufen

> **Harte Regel, keine Empfehlung: Genau ein Subagent gleichzeitig, für
> den gesamten Task — niemals mehrere.** Nicht nur Planer/Coder/Auditer
> innerhalb eines Steps, sondern auch verschiedene Steps oder Fix-Runden
> **niemals** parallel starten, selbst wenn zwei Steps inhaltlich
> unabhängig aussehen (verschiedene Dateien, kein offensichtlicher
> Konflikt). **Grund:** Alle Subagenten arbeiten im selben Git-Working-
> Tree auf demselben Branch. Ob sich die bearbeiteten Dateien überlappen,
> ist irrelevant — zwei gleichzeitige Commits/Working-Tree-Änderungen auf
> demselben Checkout sind ein Integritätsrisiko (Race Conditions, kaputte
> Commit-Reihenfolge, verlorene Änderungen), kein Effizienzgewinn. Du
> startest jeden Subagenten **synchron/im Vordergrund** und wartest sein
> vollständiges Ergebnis ab, bevor du irgendetwas anderes tust (siehe
> „Werkzeug-Hinweis" unten für die konkrete technische Umsetzung — dort
> ist das Vergessen der richtigen Einstellung der einzige Weg, wie diese
> Regel unbemerkt gebrochen werden könnte).

Für Planer/Coder/Auditer gibt es **keine vorregistrierten Subagent-Typen** —
das hält das Setup portabel. Stattdessen:

1. Lies die passende Datei: `skills/planer/SKILL.md`,
   `skills/coder/SKILL.md` oder `skills/auditer/SKILL.md`.
2. Baue daraus den vollständigen Prompt für den Subagent-Aufruf: Skill-Inhalt
   + konkreter Auftrag (welcher Task, welcher Step, welcher Modus) + Pfade
   zu den relevanten Dateien (Aufgaben-Doku, Step-Plan/-Result, Tech-Stack-
   Notiz) + **`rules_dir`** (aus `task-state.md`-Frontmatter, siehe Schritt 1).
   Der Subagent hat keinen Zugriff auf deinen bisherigen Gesprächsverlauf
   oder den ursprünglichen Nutzer-Prompt — ohne diese explizite Angabe
   weiß er nicht, wo die Projektkonventionen liegen (siehe `spec.md` §3.1).
3. Starte damit eine neue, unabhängige Subagent-Konversation **synchron**
   (siehe „Werkzeug-Hinweis" unten). Der Subagent bekommt **nur** diesen
   Prompt als Kontext — nicht deinen bisherigen Gesprächsverlauf. Du
   wartest, bis dieser eine Aufruf vollständig zurückkommt.
4. Erst danach: Werte das Ergebnis aus (Dateien, die der Subagent
   geschrieben haben soll, plus seine Abschlussmeldung), aktualisiere
   `task-state.md` entsprechend, committe was zu committen ist — dann
   erst der nächste Subagent-Aufruf.

### Commit-Verantwortung (Übersicht)

Task-Dokumentation (`step-plan.md`, `step-result.md`, `step-review.md`,
`task-state.md`) wird **committet**, nicht nur auf der Platte
liegengelassen — so entsteht eine echte Git-Historie der Step-Zustände
und Fix-Runden. Wer committet was:

| Was | Wer | Wann |
|---|---|---|
| Code + Tests (+ Produkt-Doku) | **Coder** (Skill Schritt 5) | direkt nach erfolgreichem Build/Test |
| `step-plan.md` (Status) + `step-result.md` | **Coder** (Skill Schritt 7) | direkt danach, referenziert den Code-Commit-Hash |
| Neue `step-plan.md`-Dateien vom Planer | **Orchestrator (du)** | direkt nach jedem Planer-Aufruf (Schritt 3) |
| `step-review.md` + Status-Update in `step-plan.md` | **Orchestrator (du)** | direkt nach jedem Auditer-Aufruf (Schritt 4) |

Planer und Auditer committen selbst **nichts** — das ist bewusst so
(siehe deren Skills). Du als Orchestrator committest also an zwei
Stellen im Loop selbst; `git add` dabei immer **gezielt** auf die
betroffenen Task-Dateien, nie breit (`-A`/`.`).

## Schritt 3 — Planer aufrufen (Initial oder Fix-Modus)

Gemäß `spec.md` §5.1 / `skills/planer/SKILL.md`. Manche der erzeugten
Steps können `step_type: batch` sein — mehrere thematisch unabhängige,
aber einzeln triviale Low-Risk-Änderungen in einem Step gebündelt (siehe
`spec.md` §7.7). Für dich als Orchestrator ändert das **nichts** am
Ablauf: ein Batch-Step durchläuft Coder → Auditer genauso wie jeder
andere Step, nur der Inhalt ist anders strukturiert. Einzige Ausnahme:
Löst ein `issues`-Verdict bei einem Batch-Step einen Fix-Step aus, deckt
dieser nur die im Review benannten Item(s) ab, nicht den ganzen Batch —
das steuert der Planer im Fix-Modus selbst, du musst dafür nichts
Zusätzliches tun. Nach Rückkehr:
- Trage alle neuen `step-NNN` (bzw. `step-NNN/fix-XX` im Fix-Modus) in die
  Steps-Tabelle von `task-state.md` ein (Status `open`).
- **Committe die neuen `step-plan.md`-Dateien** — ein Commit pro
  Planer-Aufruf (auch wenn dabei mehrere Steps auf einmal entstanden
  sind). Message z. B. `docs(task): plane step-001..008 für audit-2026-07-24`
  bzw. im Fix-Modus `docs(task): plane fix-XX für step-NNN`.
- Falls der Planer blockiert hat: Status `blocked`, Nutzer informieren,
  Loop pausiert hier.

## Schritt 4 — Loop (pro offenem Step)

Wiederhole für jeden `open`-Step in Reihenfolge (Details: `spec.md`
§5.2):

1. Setze Step auf `in_progress` in `task-state.md`, `current_step` aktualisieren.
2. Rufe **Coder** auf (Schritt 2) mit dem Step-Plan als Auftrag. Der Coder
   macht dabei selbst zwei Commits (Code + Doku, siehe Tabelle oben) — du
   committest hier nichts.
3. Werte Ergebnis aus:
   - `step-result.md` mit Status `done (pending audit)` → weiter zu 4.
   - Status `blocked` → `task-state.md` auf `blocked`, Nutzer informieren,
     **Loop stoppt hier** (nicht automatisch weitermachen).
4. Rufe **Auditer** auf (Modus `step`) mit Step-Plan + Result.
5. Werte Verdict aus und **committe** `step-review.md` + das Status-Update
   in `step-plan.md` (ein Commit, machst du selbst — siehe Tabelle oben):
   - `approved` → Step-Status `done`. Commit-Message z. B.
     `chore(task): step-NNN Review dokumentieren (Verdict: approved)`.
     Kurze Statusmeldung an Nutzer, weiter zum nächsten Step.
   - `issues` → **Fix-Step statt neuer Top-Level-Step** (siehe
     `spec.md` §5.2.1):
     1. Ermittle die nächste freie `fix-XX` unter `step-NNN/` (höchste
        vorhandene + 1, Start `01`).
     2. **Prüfe zuerst das Fix-Budget** (Schritt 5 unten). Limit erreicht
        → Step-Status `blocked` statt neuen Fix-Step anzulegen, committe
        das, Loop stoppt hier für diesen Step.
     3. Sonst: Step-Status `done (fix-XX pending)`, committe Review +
        Status-Update (Message z. B. `chore(task): step-NNN Review
        dokumentieren (Verdict: issues, → fix-XX)`).
     4. Rufe Planer im **Fix-Modus** auf (Schritt 3) mit `fix-XX` als
        Zielpfad. Danach normaler Coder → Auditer-Zyklus für
        `step-NNN/fix-XX/` — wieder ab Punkt 2 dieses Schritts, nur eine
        Ebene tiefer. Rekursiv bis `approved` oder Budget/Blocker greift.
   - `blocked` → Step-Status `blocked`, committe Review + Status-Update,
     Nutzer informieren, Loop stoppt hier.
6. Kurze Statusmeldung an den Nutzer nach **jedem** Step-Abschluss (nicht
   erst am Ende) — Format: *"step-NNN[/fix-XX]: <Titel> → `approved`/
   `issues`/`blocked`. Commit `<hash>`."*

Wenn keine `open`-Steps mehr übrig sind: weiter zu Schritt 6.

## Schritt 5 — Fix-Budget (Loop-Guard)

- **Pro Step: max. 3 Fix-Runden** (`fix-01`..`fix-03`, konfigurierbar via
  `max_fix_rounds_per_step` in `task-state.md`). Zähler = Anzahl
  vorhandener `fix-XX`-Ordner unter dem jeweiligen `step-NNN/`; zusätzlich
  in `task-state.md` je Step-Zeile mitführen (Spalte „Fix-Runden").
- Bei Erreichen des Limits für einen Step: **nur dieser eine Step** →
  `blocked` (siehe Schritt 4, `issues`-Zweig, Unterpunkt 2). Der Guard ist
  bewusst pro Step — mehrere unabhängige Steps, die je einmal
  nachgebessert werden, sind normal, kein Alarmsignal.
- **Task-weiter Not-Anker:** Summe aller Fix-Runden über alle Steps in
  `task-state.md`-Frontmatter (`total_fix_rounds`) mitzählen. Bei
  Überschreiten von `max_total_fix_rounds` (Default 12): gesamter Task →
  `aborted`, unabhängig vom Status der Einzel-Steps.

Bei Task-Abbruch (`aborted`):
- Alle noch offenen/blockierten Punkte in `task-summary.md` auflisten.
- Nutzer informieren, Loop stoppt.

## Schritt 6 — Globaler 360°-Audit

Sobald alle Steps `done` sind (kein `blocked`, kein Guard-Abbruch):
- Rufe **Auditer** im Modus `global` auf (Schritt 2), mit der gesamten
  Task-Definition + allen Step-Result/Review-Dateien als Kontext.
- Ergebnis landet in `task-summary.md` (Template
  `templates/task-summary.md`).
- `task-state.md` auf `done` (oder `aborted` bei gravierenden globalen
  Findings — dann Nutzer informieren statt selbst zu entscheiden).

## Schritt 7 — Abschlussmeldung

Am Ende (egal ob `done`, `aborted` oder `blocked`) immer eine kurze
Zusammenfassung an den Nutzer:
- Wie viele Steps, wie viele `approved`/`blocked`/offen.
- Pfad zu `task-summary.md`.
- Bei `blocked`/`aborted`: die konkrete offene Frage bzw. was als Nächstes
  zu klären ist.

## Was du (Orchestrator) NICHT tun darfst

- **Selbst keinen Code schreiben.** Das macht ausschließlich der
  Coder-Subagent. Du selbst committest nur Task-Dokumentation
  (Planer-Output, Review + Status-Updates — siehe „Commit-Verantwortung"
  oben), nie Produktcode.
- **Keine Rolle überspringen.** Auch ein trivialer Step läuft durch
  Coder → Auditer, nicht direkt "durchgewunken".
- **Niemals zwei Subagenten gleichzeitig laufen lassen** — weder zwei
  Rollen desselben Steps noch zwei verschiedene Steps/Fix-Runden parallel,
  auch nicht wenn sie unabhängig aussehen. Siehe Warnkasten in Schritt 2.
- **Keinen Push.** Genau wie die Subagenten — nur lokale Commits.
- **Bei `blocked` nicht selbst entscheiden und weitermachen.** Nutzer-
  Entscheidungen sind Nutzer-Entscheidungen.
- **Fix-Budget nicht umgehen**, auch wenn "der nächste Fix sicher der
  letzte ist".
- **Git-Historie niemals umschreiben** (`rebase`, `amend`, `reset --hard`
  auf bereits committete Commits, Force-Push) — auch nicht, um
  liegengebliebene Task-Doku nachträglich „an der richtigen
  chronologischen Stelle" einzufügen. Ordentlich aussehende Historie ist
  **kein** Grund, das zu rechtfertigen — siehe `spec.md` §7.3 für die
  volle Begründung. Fällt dir etwas Vergessenes auf: **jetzt** an `HEAD`
  nachcommitten, mit einer Commit-Message, die das ehrlich als Nachtrag
  benennt. Diese Regel gilt für dich genauso wie für den Coder — du bist
  nicht ausgenommen, nur weil du selbst „nur Doku" committest.

---

## Werkzeug-Hinweis (allgemein, für jedes Agent-Tool)

Wie genau ein Subagent bei dir technisch gestartet wird (eigenständiger
Agent-Aufruf, Sub-Task, neue Session, separater Prozess, …), hängt vom
jeweils verwendeten Werkzeug ab und wird hier bewusst offengelassen.
Zwei Eigenschaften sind aber **werkzeugunabhängig Pflicht** (siehe
Warnkasten in Schritt 2):

1. **Isolierter Kontext** — der Subagent bekommt nur den von dir gebauten
   Prompt, nicht deinen bisherigen Gesprächsverlauf.
2. **Synchrones Warten** — du wartest das vollständige Ergebnis ab, bevor
   du irgendetwas anderes tust. Startet dein Werkzeug Subagenten
   standardmäßig asynchron/im Hintergrund, musst du das für jeden
   einzelnen Aufruf explizit auf synchron/blockierend umstellen — sonst
   ist die Sequenzialitäts-Regel aus Schritt 2 nur Text, dem strukturell
   niemand folgt. Prüfe das vor jedem Subagent-Aufruf kurz bewusst.

Weitere werkzeugunabhängige Punkte:
- Für Status-Updates an den Nutzer reicht eine kurze Textmeldung nach
  jedem Step — kein automatisiertes Scheduling nötig, außer der Nutzer
  bittet explizit um einen unbeaufsichtigten/geplanten Lauf.
- `task-state.md` ist die einzige verbindliche Zustands-Quelle für den
  Task (nicht ein internes Task-/To-do-Feature deines Werkzeugs, falls
  vorhanden) — sie wird bei jedem Schritt aktualisiert und committet.
