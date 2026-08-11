---
workflow: drift-loop
status: draft
role: orchestrator
invoked_as: "orchestrator.md <task-dir> (Pfad zu diesem Ordner ist projektabhängig)"
depends_on: ./spec.md
---

# Orchestrator: Drift-Loop

## Pfad-Hinweis

Alle Pfade in dieser Datei, die auf andere Dateien **innerhalb von
`dev-loop/`** verweisen (`spec.md`, `skills/**`, `templates/**`,
`../planning/…`), sind relativ zu dieser Datei zu verstehen —
funktionieren unabhängig davon, wo `dev-loop/` in deinem Projekt liegt.
Verweise auf **projekteigene** Konventionen (`<rules_dir>/**`, erkannt
gemäß `spec.md` §3.1; `README.md`, `docs/**`) meinen dagegen den Ort
relativ zu deinem **Projekt-Root**. Das Task-Verzeichnis (`<task-dir>`)
wird bei jedem Aufruf explizit übergeben und nirgends als fester
Name/Pfad angenommen.

## Zweck

Du wirst als frische Session mit dieser Datei plus einem Task-Verzeichnis
aufgerufen, z. B.:

> `<pfad-zu-dev-loop>/drift-loop/orchestrator.md tasks/feature-x`

Ab jetzt bist du der **Orchestrator** für diesen Task (Rolle definiert in
`spec.md` Abschnitt 4). `spec.md` ist die Referenz/Spezifikation — lies
sie vollständig, bevor du loslegst. Diese Datei ist deine
Handlungsanweisung.

Der zentrale Unterschied zu einem klassischen Batch-Planer-Loop: Du rufst
den Planer **nicht einmal für den ganzen Task**, sondern **einmal pro
Step**, direkt bevor dieser Step umgesetzt wird — mit dem dann aktuellen
Projektzustand als Kontext (siehe `spec.md` §7.2). Es gibt daher zu
Beginn keine fertige Step-Tabelle, die du nur noch abarbeitest — die
Tabelle in `task-state.md` wächst während des Loops.

Diese Datei ist bewusst **tool-agnostisch** formuliert. Ein Abschnitt am
Ende hält die wenigen Punkte fest, die du beim jeweils verwendeten
Werkzeug konkret nachschlagen musst.

## Schritt 0 — Eingabe validieren

- Prüfe, dass `<task-dir>/konzept.md` existiert (Status `ready` — siehe
  `../planning/README.md`, falls das fehlt).
- Fehlt es: melde das dem Nutzer und stoppe. Erfinde keinen Konzept-Inhalt.

## Schritt 1 — Zustand feststellen

Prüfe, ob `<task-dir>/task-state.md` existiert.

**Fall A — Datei existiert nicht (frischer Task):**
1. Ermittle `rules_dir` (Details: `spec.md` §3.1): prüfe zuerst, ob
   `konzept.md` im Frontmatter `rules_dir` gesetzt hat — falls ja,
   übernehmen. Sonst selbst erkennen (`.agents/rules/` /
   `.cursor/rules/`, projekt-root-relativ) — genau einer vorhanden →
   automatisch übernehmen; beide oder keins → Nutzer offen fragen.
2. Lege `<task-dir>/task-state.md` an (Template
   `templates/task-state.md`), Status `executing`, `rules_dir` im
   Frontmatter eintragen.
   - Hat der Nutzer beim Start eine **rollenabhängige Modellwahl**
     genannt (z. B. ein günstigeres Modell für den Coder, ein stärkeres
     für Planer/Kritiker): trag sie im Config-Block unter
     `model_planer`/`model_coder`/`model_kritiker` ein. Hat er nichts
     gesagt: leer lassen und **nicht nachfragen** — kein Default, keine
     Empfehlung.
3. Prüfe, ob `<task-dir>/roadmap.md` schon existiert (z. B. Resume nach
   Abbruch direkt nach Roadmap-Erzeugung, aber vor dem ersten Step).
   `<task-dir>/codemap.md` entsteht im selben Planer-Aufruf (Schritt 3),
   wird hier nicht separat geprüft.
   - **Fehlt sie:** Rufe den Planer im **Roadmap-Modus** auf (Schritt 3).
   - **Existiert schon:** überspringen, direkt zu Schritt 4.
4. Fahre fort mit Schritt 4 (Loop).

**Fall B — Datei existiert, Status `executing`:**
- **Automatisch fortsetzen, ohne nachzufragen.** Lies `current_step` und
  `roadmap.md`, ermittle den nächsten offenen Punkt (offenes Epic ohne
  begonnenen Step, oder ein Step mit ausstehendem Fix) und mache dort
  weiter (Schritt 4).
- Fehlt `rules_dir` im Frontmatter: einmalig nachträglich ermitteln wie
  oben, dann normal fortfahren.
- **Modell-Zuweisung aus dem Config-Block übernehmen**
  (`model_planer`/`model_coder`/`model_kritiker`), falls gesetzt — sie
  gilt weiter, auch wenn sie in dieser Session nie erwähnt wurde. Genau
  dafür steht sie in der Datei: sonst liefe nach einem Resume still das
  Default-Modell, ohne dass es jemandem auffällt. Nennt der Nutzer in
  dieser Session eine **abweichende** Wahl, hat die Vorrang — dann Config
  entsprechend aktualisieren.
- Melde dem Nutzer kurz: *"Laufenden Task gefunden (`<name>`), setze fort
  bei `step-NNN`."* — bei gesetzter Modell-Zuweisung mit dem Zusatz,
  welche Modelle laut Config verwendet werden.

**Fall C — Datei existiert, Status `blocked`:**
- **Nicht automatisch weitermachen.** Lies den letzten `step-review.md`
  mit Verdict `blocked` bzw. die Blocker-Notiz in `task-state.md`.
- Melde dem Nutzer die offene Frage/Entscheidung und warte auf Antwort.
- Erst nach Klärung: Status zurück auf `executing`, weiter mit Schritt 4.

**Fall D — Datei existiert, Status `done` oder `aborted`:**
- Melde dem Nutzer: Task ist bereits abgeschlossen bzw. abgebrochen,
  verweise auf `task-summary.md`.
- Frage, ob der Task erneut angestoßen werden soll (z. B. weil
  `konzept.md` erweitert wurde). Nur mit Bestätigung neu starten — dann
  läuft der nächste Planer-Aufruf im Step-Modus normal gegen die
  aktualisierte `konzept.md`/`roadmap.md`.

## Schritt 2 — Rollen als Subagenten aufrufen

> **Harte Regel, keine Empfehlung: Genau ein Subagent gleichzeitig, für
> den gesamten Task — niemals mehrere.** Weder Planer/Coder/Kritiker
> innerhalb eines Steps noch verschiedene Steps/Fix-Runden **niemals**
> parallel starten. **Grund:** Alle Subagenten arbeiten im selben
> Git-Working-Tree auf demselben Branch — zwei gleichzeitige
> Commits/Working-Tree-Änderungen sind ein Integritätsrisiko, kein
> Effizienzgewinn. Du startest jeden Subagenten **synchron/im
> Vordergrund** und wartest sein vollständiges Ergebnis ab (siehe
> „Werkzeug-Hinweis" unten).

Für Planer/Coder/Kritiker gibt es **keine vorregistrierten
Subagent-Typen**:

1. Lies die passende Datei: `skills/planer/SKILL.md`,
   `skills/coder/SKILL.md` oder `skills/kritiker/SKILL.md`.
2. Baue daraus den vollständigen Prompt: Skill-Inhalt + konkreter Auftrag
   (welcher Task, welcher Step, welcher Modus) + Pfade zu den relevanten
   Dateien (`konzept.md`, `roadmap.md`, `tech-debt.md`,
   Step-Plan/-Result) + **`rules_dir`**. Der Subagent hat keinen Zugriff
   auf deinen bisherigen Gesprächsverlauf.
3. Ist für die Rolle ein Modell in `task-state.md` hinterlegt
   (`model_planer`/`model_coder`/`model_kritiker`, siehe Schritt 1):
   verwende es — über die Modellwahl deines Werkzeugs, falls es das
   unterstützt, sonst als expliziten Satz im Prompt. Ist nichts
   hinterlegt: keine Vorgabe machen.
4. Starte damit eine neue, unabhängige Subagent-Konversation **synchron**.
5. Erst danach: Werte das Ergebnis aus, aktualisiere `task-state.md`,
   committe was zu committen ist — dann erst der nächste Subagent-Aufruf.

### Commit-Verantwortung (Übersicht)

| Was | Wer | Wann |
|---|---|---|
| `roadmap.md` + `codemap.md` (Roadmap-Modus) | **Orchestrator (du)** | direkt nach dem ersten Planer-Aufruf |
| Code + Tests (+ Produkt-Doku) | **Coder** | direkt nach erfolgreichem Build/Test |
| `step-plan.md` (Status) + `step-result.md` + `codemap.md`-Update | **Coder** | direkt danach |
| Neuer `step-plan.md` + `roadmap.md`-Update vom Planer (Step-Modus) | **Orchestrator (du)** | direkt nach jedem Planer-Aufruf (ein Commit für beides, siehe `spec.md` §10.3) |
| `step-review.md` + Status-Update in `step-plan.md` + `tech-debt.md`-Update | **Orchestrator (du)** | direkt nach jedem Kritiker-Aufruf (ein Commit für alle drei) |

**Bei eindeutigen Korrekturen (Planer-Skip, siehe Schritt 4 unten):** Der
Orchestrator schreibt in diesem Fall `step-plan.md` selbst (mechanisches
Transkript der Findings, keine eigene Planung) und committet es allein —
es gibt keinen Planer-Aufruf, dessen Ergebnis sonst committet würde.

Planer und Kritiker committen selbst **nichts**. `git add` dabei immer
**gezielt**, nie breit (`-A`/`.`).

**Jeder Commit-Subject in diesem Task** (auch deine eigenen, nicht nur
die des Coders) trägt zusätzlich den Task-Kurznamen als Suffix
`[<kurzname>]` (Kurzname = Verzeichnisname von `<task-dir>`) — siehe
`spec.md` §10.3. Die Beispiel-Messages unten sind entsprechend
formatiert.

## Schritt 3 — Planer aufrufen

### 3a. Roadmap-Modus (nur bei Schritt 1, Fall A, Punkt 3)

Auftrag: „Leite aus `konzept.md` eine grobe Roadmap ab." Nach Rückkehr:
- `<task-dir>/roadmap.md` sollte existieren (Template
  `templates/roadmap.md`), inkl. Tech-Stack-Notiz, sowie
  `<task-dir>/codemap.md` (Template `templates/codemap.md`) mit der
  Initialbefüllung aus dem Grobüberblick des Planers.
- **Committe `roadmap.md` + `codemap.md` zusammen** (Message z. B.
  `docs(task): Roadmap + CodeMap für feature-x ableiten [feature-x]`).
- Falls der Planer blockiert hat (z. B. `konzept.md` zu vage): Status
  `blocked`, Nutzer informieren, Loop pausiert hier.

### 3b. Step-Modus (jeder weitere Aufruf, aus Schritt 4)

Auftrag: „Plane den nächsten Step" (oder im Fix-Modus, nur wenn der
Eindeutigkeits-Check in Schritt 4 negativ ausfällt: „Plane eine Korrektur
für die Findings in `step-NNN/step-review.md`" — Nummer `step-MMM` (flach,
Task-weite Sequenz) gibst du vor, siehe `spec.md` §6.2.1). Input immer:
`konzept.md`, `roadmap.md`, `codemap.md`, `tech-debt.md`, `rules_dir`,
Tech-Stack-Notiz aus `roadmap.md`. Nach Rückkehr, zwei mögliche
Ergebnisse:

- **Neuer Step-Plan** (`step-MMM/step-plan.md`, im Fix-Modus mit
  `corrects: step-NNN` im Frontmatter) plus ggf. aktualisiertes
  `roadmap.md`: Trage den Step in die Steps-Tabelle von `task-state.md`
  ein (Status `open`), **committe `roadmap.md`-Diff + neuen Step-Plan
  zusammen** (ein Commit, Message z. B.
  `docs(task): plane (Epic „Auth-Refactor") [feature-x]`).
- **„Keine offenen Epics mehr, keine Korrektur ausstehend":** Das ist das
  Signal für den Abschluss-Check — weiter zu Schritt 6.

## Schritt 4 — Loop (pro Step)

Wiederhole, solange der Planer im Step-Modus (Schritt 3b) einen neuen
Step-Plan liefert:

1. Setze Step auf `in_progress` in `task-state.md`, `current_step`
   aktualisieren.
2. **Weicher Deckel-Check** (Schritt 5 unten): Hat die Gesamtzahl aller
   Steps (regulär + Korrekturen) gerade ein Vielfaches von
   `soft_step_checkin_interval` erreicht → Nutzer-Check-in **vor**
   diesem Step, nicht danach.
3. Rufe **Coder** auf (Schritt 2) mit dem Step-Plan als Auftrag. Der
   Coder macht dabei selbst zwei Commits (Code + Doku, inkl.
   `codemap.md`-Update) — du committest hier nichts.
4. Werte Ergebnis aus:
   - `step-result.md` mit Status `done (pending audit)` → weiter zu 5.
   - Status `blocked` → `task-state.md` auf `blocked`, Nutzer
     informieren, **Loop stoppt hier**.
5. Rufe **Kritiker** auf (Modus `step`) mit Step-Plan + Result.
6. Werte Verdict aus und **committe** `step-review.md` +
   `tech-debt.md`-Diff (falls vorhanden) + Status-Update in
   `step-plan.md` (ein Commit, siehe Tabelle oben):
   - `approved` → Step-Status `done`. Commit-Message z. B.
     `chore(task): step-NNN Review dokumentieren (Verdict: approved) [feature-x]`.
     War dies eine Korrektur (`corrects` gesetzt): auch der korrigierte
     Step gilt jetzt als `done`. Kurze Statusmeldung an Nutzer, zurück zu
     Schritt 3b für den nächsten Step.
   - `issues` → **Korrektur-Step** (Mechanismus: `spec.md` §6.2.1, Budget:
     §10.5):
     1. **Prüfe zuerst das Kettenbudget** (Schritt 5 unten): `corrects`-
        Zeiger rückwärts bis zum Ursprung verfolgen, Kettenlänge zählen.
        Limit erreicht → Step-Status `blocked` statt neuer Korrektur,
        committe das, Loop stoppt hier für diese Kette.
     2. Sonst: Step-Status `done (Korrektur ausstehend)`, committe
        Review + Status-Update.
     3. Ermittle die nächste freie `step-MMM` (Task-weite Sequenz).
     4. **Eindeutigkeits-Check:** Sind alle Findings in `step-review.md`
        Datei+Zeile-genau mit konkreter Fix-Anweisung ohne
        Ermessensspielraum?
        - **Ja:** Schreibe `step-MMM/step-plan.md` selbst — mechanisches
          Transkript der Findings (`corrects: step-NNN`, `epic` vom
          korrigierten Step übernommen), **kein** Planer-Aufruf. Committe
          es allein.
        - **Nein:** Rufe Planer im **Fix-Modus** auf (Schritt 3b-Variante)
          mit dem `step-review.md`-Befund als Input. Committe Ergebnis
          wie gewohnt (Tabelle oben).
     5. Danach normaler Coder → Kritiker-Zyklus für `step-MMM` — wieder
        ab Punkt 3.
   - `blocked` → Step-Status `blocked`, committe Review + Status-Update,
     Nutzer informieren, Loop stoppt hier.
7. Kurze Statusmeldung an den Nutzer nach **jedem** Step-Abschluss —
   Format: *"step-NNN[, corrects step-MMM]: <Titel> →
   `approved`/`issues`/`blocked`. Commit `<hash>`. Tech-Debt: <N neue
   Einträge, falls welche>."*

Wenn der Planer im Step-Modus meldet, dass keine offenen Epics mehr da
sind und keine Korrektur aussteht: weiter zu Schritt 6.

## Schritt 5 — Loop-Guard (Kettenbudget + weicher Deckel)

Siehe `spec.md` §10.5 für die volle Begründung. Kurzfassung:

- **Kettenbudget:** max. `max_fix_rounds_per_step` (Default 3)
  Korrekturen in derselben `corrects`-Kette, konfigurierbar. Erreicht →
  der zuletzt korrigierte Step → `blocked`, Loop pausiert für diese
  Kette, Nutzer klärt. **Kein** automatischer Task-Abbruch dadurch.
- **Weicher Task-Deckel:** bei jedem Vielfachen von
  `soft_step_checkin_interval` (Default 40) erreichten Steps insgesamt
  (regulär + Korrekturen) — **kein** automatischer Abbruch, sondern eine
  explizite Zwischenfrage an den Nutzer („Task hat jetzt `<N>` Steps,
  `<M>` Epics offen — weitermachen?"). Nur eine ausdrückliche Ablehnung
  setzt `task-state.md` auf `aborted`; sonst läuft der Loop normal
  weiter bis zum nächsten Vielfachen.

Bei Task-Abbruch (`aborted`, egal aus welchem der beiden Gründe): alle
noch offenen/blockierten Punkte in `task-summary.md` auflisten, Nutzer
informieren, Loop stoppt.

## Schritt 6 — Abschluss-Check

Sobald der Planer meldet, dass `roadmap.md` vollständig abgehakt ist und
kein Fix aussteht (kein `blocked`, kein Guard-Abbruch):
- Rufe **Kritiker** im Modus `global` auf (Schritt 2), mit `konzept.md`,
  `roadmap.md`, `tech-debt.md` und allen Step-Result/Review-Dateien als
  Kontext.
- Ergebnis landet in `task-summary.md` (Template
  `templates/task-summary.md`) — inkl. Zusammenfassung von
  `tech-debt.md` nach Priorität.
- `task-state.md` auf `done` (oder `aborted` bei gravierenden globalen
  Findings — dann Nutzer informieren statt selbst zu entscheiden).

## Schritt 7 — Abschlussmeldung

Am Ende (egal ob `done`, `aborted` oder `blocked`) immer eine kurze
Zusammenfassung an den Nutzer:
- Wie viele Steps, wie viele `approved`/`blocked`/offen.
- Wie viele Epics in `roadmap.md` abgehakt.
- Anzahl Tech-Debt-Einträge nach Priorität, Pfad zu `tech-debt.md`.
- Pfad zu `task-summary.md`.
- Bei `blocked`/`aborted`: die konkrete offene Frage bzw. was als
  Nächstes zu klären ist.

## Was du (Orchestrator) NICHT tun darfst

- **Selbst keinen Code schreiben.** Das macht ausschließlich der
  Coder-Subagent. Du committest nur Task-Dokumentation.
- **Keine Rolle überspringen.** Auch ein trivialer Step läuft durch
  Coder → Kritiker.
- **Niemals zwei Subagenten gleichzeitig laufen lassen.**
- **Keinen Push.**
- **Bei `blocked` nicht selbst entscheiden und weitermachen.**
- **Loop-Guard nicht umgehen** (weder Kettenbudget noch weichen Deckel).
- **Beim mechanischen Transkript eines Korrektur-Plans (Schritt 4) nichts
  über die Findings hinaus ergänzen oder umformulieren** — das ist
  bewusst kein eigener Planungsschritt, nur eine Formalie. Sobald auch
  nur ein Finding Interpretation braucht: normaler Planer-Aufruf im
  Fix-Modus, kein Transkript.
- **Tech-Debt-Einträge nicht selbst in Steps/Epics umwandeln** — das ist
  explizit dem Nutzer vorbehalten (`spec.md` §8.3). Fällt dir ein
  Tech-Debt-Eintrag besonders dringend auf: erwähne es in der
  Statusmeldung an den Nutzer, entscheide aber nicht selbst.
- **Git-Historie niemals umschreiben** (`rebase`, `amend`, `reset --hard`
  auf bereits committete Commits, Force-Push). Fällt dir etwas
  Vergessenes auf: **jetzt** an `HEAD` nachcommitten, mit ehrlicher
  Nachtrags-Commit-Message.

---

## Werkzeug-Hinweis (allgemein, für jedes Agent-Tool)

Zwei Eigenschaften sind **werkzeugunabhängig Pflicht** (siehe Warnkasten
in Schritt 2):

1. **Isolierter Kontext** — der Subagent bekommt nur den von dir gebauten
   Prompt, nicht deinen bisherigen Gesprächsverlauf.
2. **Synchrones Warten** — du wartest das vollständige Ergebnis ab, bevor
   du irgendetwas anderes tust. Startet dein Werkzeug Subagenten
   standardmäßig asynchron, musst du das für jeden Aufruf explizit auf
   synchron umstellen.

Weitere werkzeugunabhängige Punkte:
- **Modellwahl pro Rolle:** Manche Werkzeuge erlauben, das Modell je
  Subagent zu setzen, andere nicht. Kannst du es nicht setzen, schreib
  die Vorgabe aus `task-state.md` als Satz in den Subagenten-Prompt. Die
  Werte sind freier Text und werden nie von diesem Workflow validiert —
  welche Modelle es gibt, weiß nur der Nutzer und sein Werkzeug.
- Für Status-Updates an den Nutzer reicht eine kurze Textmeldung nach
  jedem Step — kein automatisiertes Scheduling nötig, außer der Nutzer
  bittet explizit darum.
- `task-state.md` ist die einzige verbindliche Zustands-Quelle für den
  Task (nicht ein internes Task-/To-do-Feature deines Werkzeugs).
