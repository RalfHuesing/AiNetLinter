---
workflow: drift-loop
status: draft
applies_to: "<task-dir>/* (frei wählbarer Ort, siehe Pfad-Hinweis)"
---

# Workflow: Drift-Loop (Roadmap → JIT-Plan → Code → Kritik)

## Pfad-Hinweis

Verweise in diesem Dokument auf andere Dateien **innerhalb von
`dev-loop/`** (`orchestrator.md`, `skills/**`, `templates/**`,
`../planning/…`) sind relativ zu diesem Ordner (`drift-loop/`) zu
verstehen — funktionieren unabhängig davon, wo `dev-loop/` in einem
Projekt liegt. Verweise auf **projekteigene** Konventionen
(`<rules_dir>/**`, erkannt gemäß §3.1; `README.md`, `docs/**`) meinen den
Ort relativ zum **Projekt-Root**, unabhängig von `dev-loop`s eigenem
Standort. `tasks/<name>/` ist als Konvention/Beispiel zu lesen, nicht als
feste Vorgabe — das Task-Verzeichnis kann irgendwo liegen, es wird bei
jedem Aufruf explizit übergeben.

## 1. Intention

Dieser Workflow setzt eine vom Nutzer in `konzept.md` festgehaltene
Absicht **schrittweise und selbstkorrigierend** um: Der Planer plant
immer nur den **nächsten** Step — mit vollem Zugriff auf den
tatsächlichen, aktuellen Projektzustand statt auf eine Prognose von vor
dem ersten Commit. Ein grobes `roadmap.md` (Epics, keine Detail-Steps)
dient dabei als Gedächtnis/Anker: was ist geplant, was ist erledigt, was
kam neu dazu.

**Warum das existiert:** Vorab-Planung aller Schritte vor jeglichem Code
hat einen strukturellen blinden Fleck — der Planer kann beim Planen von
Step 5 nicht wissen, was Step 2 tatsächlich gebaut hat, weil Step 2 zum
Planungszeitpunkt noch nicht existiert. Typisches Symptom: mehrere Steps
erschaffen unabhängig voneinander ähnliche Strukturen (z. B. mehrere
generische Dialoge statt eines wiederverwendeten), weil kein Step vom
vorherigen tatsächlichen Ergebnis wusste. `drift-loop` behebt das an der
Wurzel, indem jeder Step erst geplant wird, wenn der vorherige bereits
real existiert.

Der Workflow ist gedacht für Arbeiten, die **mehrere aufeinander
aufbauende Änderungen** erfordern und bei denen der Weg dorthin sich
plausibel ändern kann, während man ihn geht (Features mit unklarer
Feinstruktur, Refactorings mit unbekanntem Umfang, alles wo „Step 3 hängt
davon ab, was Step 1 tatsächlich ergibt" zutrifft). Für triviale
Einzeländerungen ist er Overkill — die direkt im Editor machen.

## 2. Anwendung (aus Nutzersicht)

1. **Verzeichnis anlegen:** z. B. `tasks/<kurzname>/`
2. **Konzept dokumentieren:** `<task-dir>/konzept.md` — siehe §3.2 für die
   Mindestanforderungen. Existiert noch keins, nur eine grobe Idee: erst
   `../planning/orchestrator.md` nutzen, um sie im Dialog zu schärfen —
   dessen Exit-Kriterium ist genau §3.2 hier.
3. **Workflow starten:** `orchestrator.md <task-dir>` aufrufen (siehe
   [`README.md`](README.md)).
4. **Warten / Beobachten:** Der Orchestrator meldet sich nach jedem Step.
5. **Ergebnis reviewen:** `<task-dir>/task-summary.md` lesen, außerdem
   `<task-dir>/tech-debt.md` — Findings, die während des Loops bewusst
   **nicht** gefixt wurden, aber festgehalten sind (siehe §9).
   - Alle Epics grün → fertig
   - Epics/Steps `blocked` → Nutzer entscheidet, Loop kann fortgesetzt werden
   - `aborted` → Loop-Limit erreicht, offene Punkte im Summary dokumentiert

## 3. Voraussetzungen

Damit der Workflow sinnvoll laufen kann, müssen folgende Anker im Projekt
vorhanden sein. Der Planer **muss** diese lesen, bevor er plant.

- **Projektkonventionen:** `<rules_dir>/**` — Coding-Style, Architektur,
  Sicherheitsleitplanken, Test-Konventionen. Wo genau `rules_dir` liegt,
  wird nicht angenommen, sondern erkannt — siehe §3.1.
- **Projektdokumentation:** `README.md`, `docs/**` (oder vergleichbar),
  `AGENTS.md` (Projekt-Root, falls vorhanden) — was macht die Anwendung,
  wie wird sie gebaut/getestet. `AGENTS.md` ist speziell für Agenten
  gedacht (Build-/Test-Commands, Konventionen, Ort tieferer Doku) —
  anders als `rules_dir` (§3.1) keine Entweder-Oder-Erkennung, sondern
  einfach mitlesen, wenn vorhanden; ersetzt `rules_dir` nicht, ergänzt es.
- **Projekt selbst:** Build-/Test-Konfigurationen, CI-Pipelines — daraus
  leitet der Planer ab, welche Build-/Test-Commands gelten (§10.4).
- **Git-Repository:** Commits pro Step sind Standard (siehe §10.3).

Fehlen Anker, fragt der Planer nach oder blockiert mit `blocked`.

### 3.1 Rules-Verzeichnis-Erkennung

`.agents/rules/**` ist nicht fest verdrahtet — Projekte nutzen
unterschiedliche Konventionen. Geprüft werden zwei Kandidaten
(projekt-root-relativ): `.agents/rules/` und `.cursor/rules/`.

- **Genau einer existiert:** automatisch als `rules_dir` übernehmen,
  keine Rückfrage.
- **Beide oder keins existieren:** Nutzer explizit und offen fragen
  (nicht nur Ja/Nein — ein dritter, hier nicht gelisteter Pfad ist
  möglich, ebenso die Bestätigung, dass keine projektweiten Konventionen
  existieren).
- **Wann erkannt wird:**
  - In `../planning/orchestrator.md` (Schritt 2), falls die Planungsphase
    vorausgeht — Ergebnis landet in `konzept.md`s Frontmatter (`rules_dir`).
  - Im Orchestrator dieses Workflows zu Beginn (siehe `orchestrator.md`
    Schritt 1): zuerst prüfen, ob `<task-dir>/konzept.md` bereits
    `rules_dir` gesetzt hat — falls ja, übernehmen statt neu zu fragen.
    Sonst eigene Erkennung nach obigem Verfahren.
- **Persistenz:** Ergebnis landet im Frontmatter von
  `<task-dir>/task-state.md` (`rules_dir`) — einmal ermittelt, gilt es
  für den gesamten Task, auch über Resumes hinweg.
- **Weitergabe an Subagenten:** Planer/Coder/Kritiker laufen isoliert
  (kein Zugriff auf den ursprünglichen Nutzer-Prompt oder die
  Orchestrator-Session) — der Orchestrator **muss** `rules_dir` explizit
  in jeden Subagent-Prompt aufnehmen (siehe `orchestrator.md` Schritt 2).
  Ohne diese explizite Weitergabe hat kein Subagent eine Chance, das
  richtige Verzeichnis zu kennen.

### 3.2 Mindestanforderungen an die Aufgaben-Doku (`konzept.md`)

Der Planer akzeptiert freie Markdown-Struktur, aber `konzept.md` **muss**
folgendes enthalten (sonst blockt der Planer im Roadmap-Modus, §11):

- **Was** soll erreicht werden (Ziel in 2-5 Sätzen)
- **Warum** (Kontext, Hintergrund, Constraints)
- **Wo** im Projekt (Dateien, Module, Features)
- **Wie** (konkretes Vorgehen, Code-Skizzen, Referenzen) — so detailliert
  wie möglich
- **Definition of Done** (welche Tests müssen grün sein, welche Doku
  aktualisiert werden, welche Commits/Branches)

Optional aber hilfreich: Severity/Priorisierung der einzelnen Punkte,
sowie explizite **Non-Goals** — der Kritiker prüft auf Ebene 4 (§8.2)
gegen genau diese Angaben.

## 4. Rollen

| Rolle | Wer | Aufgabe |
|---|---|---|
| **Nutzer** | — | Definiert Konzept, startet Workflow, reviewt Summary + Tech-Debt-Log, klärt Blocker |
| **Orchestrator** | Root-Session | Führt Loop aus, ruft Subagents, pflegt Task-State |
| **Planer** | Subagent | Zwei Modi: **Roadmap-Modus** (einmalig, leitet Epics aus `konzept.md` ab) und **Step-Modus** (JIT, plant genau den nächsten Step) |
| **Coder** | Subagent | Setzt genau einen Step um, schreibt `step-result.md`, committet |
| **Kritiker** | Subagent | Prüft letzten Coder-Output gegen Plan, Rules, Logik **und Konzept-Treue** — vermerkt zusätzlich Architektur-/Anti-Pattern-Beobachtungen als Tech-Debt (nie als Fix-Step) |

Die drei Subagent-Rollen (Planer/Coder/Kritiker) werden vom Orchestrator
per Agent-Aufruf mit jeweils eigenem Prompt gestartet (siehe
`skills/<rolle>/SKILL.md`).

**Warum keine vierte Rolle für Roadmap-Erzeugung:** Roadmap-Modus ist ein
zweiter Modus des Planers (analog zum Fix-Modus, §6.2.1), keine eigene
Rolle — vermeidet unnötige Rollen-Fragmentierung (siehe
`docs/references.md`, „When to use multi-agent systems"). Aus demselben
Grund gibt es auch keinen separaten „Architektur-Kritiker" neben dem
normalen Kritiker: eine Rolle prüft in einem Durchgang alle vier Ebenen
(§8), das ist das Evaluator-Optimizer-Muster, nicht Rollen-pro-Kriterium.

## 5. Artefakte (Überblick)

- **`<task-dir>/konzept.md`** — Eingabe des Workflows (§3.2). Wird von
  keinem Subagenten geschrieben, nur gelesen; Änderungen macht der Nutzer
  (ggf. über `../planning/orchestrator.md`).
- **`<task-dir>/task-state.md`** — Zustand und Step-Tabelle des Tasks.
  Einziger Schreiber: der **Orchestrator**. Die Tabelle existiert zu
  Beginn nicht vollständig, sie wächst pro Step-Modus-Aufruf um eine
  Zeile.
- **`<task-dir>/roadmap.md`** — grobe Epics (nicht Detail-Steps),
  abgeleitet aus `konzept.md`, mit `[ ]`/`[x]`-Checkliste. Einziger
  Schreiber: der **Planer**. Wird laufend angepasst (Epics ergänzt,
  umformuliert, abgehakt) — kein starres Vorab-Dokument. Enthält
  zusätzlich die **Tech-Stack-Notiz** (§10.4) und einen **Regel-Index**
  (eine Zeile Kurzbeschreibung pro Datei in `<rules_dir>/**`, kein
  Volltext) — der isolierte Step-Modus-Planer liest ihn, statt bei jedem
  Step erneut alle Regeldateien komplett zu lesen, siehe §7.2. Siehe §7.
- **`<task-dir>/tech-debt.md`** — Append-only-Log für Architektur-/
  Anti-Pattern-Beobachtungen, die der Kritiker sieht, aber bewusst nicht
  in einen Fix-Step umwandelt. Einziger Schreiber: der **Kritiker**. Wird
  vom Planer bei jedem Step-Modus-Aufruf gelesen (Kontext, keine
  automatische Umsetzung). Siehe §9.
- **`<task-dir>/step-NNN/{step-plan,step-result,step-review}.md`** —
  je Step; bei Nachbesserungen zusätzlich `step-NNN/fix-XX/…` mit
  denselben drei Dateien (§6.2.1). Schreiber: Planer (`step-plan.md`),
  Coder (`step-result.md` + Status im Plan), Kritiker (`step-review.md`).
- **`<task-dir>/task-summary.md`** — Abschlussbericht des globalen
  Kritikers (§8.4).

## 6. Phasen

**Nebenläufigkeit — strikt verboten:** Der gesamte Loop ist **rein
seriell**, ohne Ausnahme. Genau ein Subagent (Planer/Coder/Kritiker)
läuft zu jedem Zeitpunkt, egal ob innerhalb eines Steps
(Rollen-Reihenfolge) oder über Steps/Fix-Runden hinweg — auch dann, wenn
zwei Steps inhaltlich unabhängig aussehen (verschiedene Dateien, kein
offensichtlicher Konflikt). Grund: alle Subagenten arbeiten auf demselben
Git-Working-Tree und demselben Branch; Dateiüberlappung ist irrelevant,
parallele Commits/Working-Tree-Änderungen auf demselben Checkout sind ein
Integritätsrisiko, keine Effizienzsteigerung. Der Orchestrator wartet
jeden Subagenten vollständig ab, bevor der nächste startet.

### 6.1 Initialisierung (einmalig pro Task)

- Orchestrator erstellt `<task-dir>/task-state.md`, Status `executing`
  (inkl. erkanntem `rules_dir`, siehe §3.1)
- Orchestrator ruft Planer im **Roadmap-Modus** auf: liest `konzept.md` +
  Projekt-Anker, leitet grobe Epics ab, schreibt `<task-dir>/roadmap.md`
  (inkl. Tech-Stack-Notiz und Regel-Index — siehe Template)
- Orchestrator committet `roadmap.md`

### 6.2 Loop (JIT, ein Step nach dem anderen)

Solange `roadmap.md` offene Epics enthält oder ein Fix aussteht:

1. Orchestrator ruft Planer im **Step-Modus** auf (siehe §7). Output:
   genau ein neuer `<task-dir>/step-NNN/step-plan.md`, plus ggf.
   aktualisiertes `roadmap.md` (abgehakte/neue/umformulierte Epics).
2. Orchestrator committet die Roadmap-Änderung + den neuen Step-Plan
   (ein Commit, §10.3), setzt den Step auf `in_progress`.
3. Orchestrator ruft **Coder** auf: Coder implementiert, schreibt
   `step-result.md`, macht Code-Commit + Doku-Commit; Step steht danach
   auf `done (pending audit)`.
4. Orchestrator ruft **Kritiker** auf (Modus `step`, siehe §8).
   - **`approved`** → Orchestrator setzt Step auf `done` (etwaige
     `MINOR`-Findings stehen in „Sonstige Beobachtungen"). Etwaige
     Tech-Debt-Einträge sind bereits in `tech-debt.md` (Kritiker-Output),
     Orchestrator committet `step-review.md` + `tech-debt.md`-Diff +
     Status-Update zusammen.
   - **`issues`** → Fix-Step `step-NNN/fix-XX/`, siehe §6.2.1. Der äußere
     Step geht dabei auf `done (fix-XX pending)`.
   - **`blocked`** → Loop pausiert, Nutzer klärt.
5. Zurück zu Punkt 1, bis Planer im Step-Modus meldet: keine offenen
   Epics mehr, kein Fix ausstehend.

### 6.2.1 Fix-Steps (Nachbesserung innerhalb eines Steps)

Ein `issues`-Verdict erzeugt **keinen neuen Top-Level-Step**, sondern
einen Fix-Step *innerhalb* des betroffenen Steps: `step-NNN/fix-XX/`, mit
denselben drei Dateien (`step-plan.md`, `step-result.md`,
`step-review.md`) wie ein normaler Step.

**Warum ein eigener Namensraum statt `step-(N+1)`:** `step-(N+1)` ist in
diesem Workflow der Platz für den **nächsten geplanten Step** aus der
Roadmap. Eine Nachbesserung dort einzuhängen würde zwei verschiedene
Dinge vermischen — „Epic-Fortschritt" und „Korrektur an step-NNN" — und
die Step-Nummerierung gegenüber `roadmap.md`/`task-state.md`
unleserlich machen. Der eigene Unterordner hält beides getrennt und
macht außerdem das **pro Step** gezählte Fix-Budget (§10.5) direkt
ablesbar.

Ablauf:
1. Orchestrator ermittelt die nächste freie `fix-XX` unter `step-NNN/`
   (höchste vorhandene + 1, Start bei `01`).
2. Orchestrator ruft den **Planer im Fix-Modus** auf (siehe
   `skills/planer/SKILL.md`): Input ist der `step-review.md`-Befund
   (Abschnitt „Findings"), nicht `konzept.md`/`roadmap.md` als Ganzes.
   Output: `step-NNN/fix-XX/step-plan.md`. **`roadmap.md` wird in diesem
   Modus nicht angefasst** — ein Fix ändert nichts an den Epics.
3. Danach normaler Coder → Kritiker-Zyklus, Ergebnisse landen in
   `step-NNN/fix-XX/step-result.md` / `step-review.md`.
4. `approved` → gesamter `step-NNN` (inkl. aller Fix-Runden) geht auf
   `done`. `issues` → nächste `fix-XX`, sofern Budget nicht erschöpft
   (§10.5). `blocked` → Loop pausiert, Nutzer klärt.

Ein Fix-Step betrifft ausschließlich den Scope des ursprünglichen
Findings — keine Ausweitung auf andere Teile des Steps oder des Tasks.
„Sonstige Beobachtungen" und `tech-debt.md`-Einträge sind **nicht**
Scope einer Fix-Runde.

**Sonderfall Batch-Step (`step_type: batch`, siehe §10.6):** Betrifft das
`issues`-Verdict nur eines oder mehrere, aber nicht alle Items eines
Batches, plant der Fix-Step **ausschließlich die konkret beanstandeten
Item(s)** — nicht den gesamten Batch neu. Bereits `approved` Items
desselben Batches sind für die Fix-Runde nicht im Scope; die
Kritiker-Findings referenzieren dafür die Item-ID zusätzlich zu
Datei:Zeile.

### 6.3 Abschluss-Check

Wenn der Planer meldet, dass `roadmap.md` keine offenen Epics mehr hat
und kein Fix aussteht:
- Orchestrator ruft **Kritiker** im Modus `global` auf (siehe §8.4)
- Ergebnis in `<task-dir>/task-summary.md`
- Task-State auf `done` (oder `aborted` bei gravierenden Findings)

Findet der globale Kritiker dabei einen neuen, keinem bestehenden Step
zuordenbaren Punkt, erzeugt das **keinen** automatischen Fix- oder
Folge-Step: Der Punkt landet im Summary (bzw. als Tech-Debt-Eintrag,
§8.3), der Nutzer entscheidet, ob daraus ein neues Epic oder ein neuer
Task wird. Das ist dieselbe Linie wie §8.3 — kein Subagent erweitert den
Scope des Tasks eigenmächtig.

## 7. Planer — zwei Modi

### 7.1 Roadmap-Modus (einmalig, Schritt 6.1)

Der Planer liest `konzept.md` + alle Anker (§3) und leitet daraus **grobe
Epics** ab — nicht datei-genaue Steps. Das macht der Step-Modus, und zwar
erst kurz bevor ein Epic tatsächlich drankommt, mit dann aktuellem
Codestand. Faustregel für Epic-Granularität: ein Epic entspricht
ungefähr einem Cluster mehrerer Steps, nicht einem einzelnen Step.

Die **Tech-Stack-Notiz** (Build-/Test-/Lint-Commands, Code-Style, §10.4)
landet hier in `roadmap.md` — es gibt in diesem Workflow keinen „ersten
Step", der sie sonst tragen würde, und Coder wie Kritiker bekommen sie
bei jedem Aufruf von dort. Aus derselben Lektüre von `<rules_dir>/**`
entsteht zusätzlich der **Regel-Index** (Kurzbeschreibung pro Datei,
siehe `skills/planer/SKILL.md` Schritt 2a) — Grundlage für die gezielte
Regel-Lektüre im Step-Modus (§7.2).

### 7.2 Step-Modus (JIT, jeder weitere Aufruf)

Vor dem eigentlichen Planen eines neuen Steps, in dieser Reihenfolge:

1. **Roadmap abgleichen:** letzten `step-result.md`/`step-review.md`
   lesen, `roadmap.md` entsprechend aktualisieren (Epic abhaken, falls
   der letzte Step es abgeschlossen hat; neues Epic ergänzen, falls der
   Kritiker oder der Coder in „Beobachtungen"/Findings einen bisher
   fehlenden Muss-Haben-Punkt aus `konzept.md` identifiziert hat —
   **nicht** aber wegen Tech-Debt-Einträgen, siehe §9).
2. **Tatsächlichen Projektzustand lesen** — nicht auf den Stand von vor
   dem letzten Step verlassen. Das ist der eigentliche Zweck von JIT: der
   Planer sieht, was wirklich existiert, bevor er entscheidet, was als
   Nächstes gebaut wird (verhindert das in §1 beschriebene
   Duplikations-Problem).
3. **Index von `tech-debt.md` lesen** (nicht die ganze Datei): Kontext für
   „gibt es schon eine bekannte Schwachstelle in dem Bereich, den ich
   jetzt plane" — Volltext nur zu den Einträgen, deren Index-Zeile den
   aktuellen Bereich berührt. Gleiche Begründung wie beim Regel-Index
   (Punkt 4): Die Datei wächst append-only über den Task, wird aber bei
   jedem Step-Modus-Aufruf gelesen. **Nie** Auslöser für einen
   automatisch generierten Step, siehe §9.
4. **Regel-Index aus `roadmap.md` konsultieren** (siehe §5): gezielt nur
   die 1-2 zum aktuellen Step passenden Regeldateien vollständig lesen,
   statt bei jedem isolierten Step-Modus-Aufruf `<rules_dir>/**`
   komplett neu zu lesen (Kosten) oder ganz zu überspringen (Risiko).
   Details: `skills/planer/SKILL.md` Schritt 4a.
5. Nächsten Step planen: entweder das nächste offene Epic aus
   `roadmap.md` (oder ein Teil davon, falls das Epic zu groß für einen
   Step ist — Epic bleibt dann offen, bis ein späterer Step-Modus-Aufruf
   es abschließt) — **oder**, falls ein `issues`-Verdict vorliegt, der
   entsprechende Fix (Fix-Modus, §6.2.1).

Für den Step-Plan selbst gelten `templates/step-plan.md`, das
`related_to`-Pointer-Prinzip und `step_type: batch` für triviale
Low-Risk-Häufungen **innerhalb eines Epics** — siehe §10.6.

**Meldung „fertig":** Sind nach Schritt 1 alle Epics abgehakt und kein Fix
ausstehend, meldet der Planer das explizit an den Orchestrator statt
einen weiteren Step zu planen — das ist das Signal für §6.3.

## 8. Kritiker — vier Prüfebenen + Tech-Debt

### 8.1 Ebenen 1-3

- **Ebene 1: Plan-Erfüllung** — sind alle im Plan genannten Änderungen
  erfolgt? Tests vorhanden und grün? Commit passend (Scope, Message)?
- **Ebene 2: Rules-Konformität** — hält der Code die **im Step-Plan unter
  „Rules-Refs" zitierten** `<rules_dir>/**`-Dateien ein? Geprüft wird
  gegen diese vom Planer kuratierte Auswahl, nicht gegen `<rules_dir>/**`
  als Ganzes — das ist eine bewusste Grenze dieser Ebene, keine Lücke.
  Bei Verstoß: Datei + Zeile + Regel + Soll-Zustand.
- **Ebene 3: Logische Korrektheit** — macht der Code, was er soll? Sind
  die Tests wirklich aussagekräftig? Übersehene Edge-Cases?

**Severity-Gating (gilt für alle vier Ebenen):**
- **`CRITICAL`:** Bricht Build/Tests, echte Logikfehler, Security-Lücken,
  Kern-Anforderung komplett verfehlt, **oder** ein explizites Non-Goal
  aus `konzept.md` wurde umgesetzt.
- **`MAJOR`:** Explizite Rules-Verletzung im Produktionscode, verfehlte
  Abnahme-Kriterien, **oder** ein Muss-Haben-Punkt aus `konzept.md` fehlt.
- **`MINOR` / `NITPICK`:** Kosmetische Punkte, Stilfragen.

Verdict-Regel: `issues` **ausschließlich** bei mindestens einem
`CRITICAL`- oder `MAJOR`-Finding; das löst einen Fix-Step aus (§6.2.1).
`MINOR`/`NITPICK` führt nie zu `issues`, sondern wandert in „Sonstige
Beobachtungen" eines `approved`-Reviews.

Bei `step_type: batch` wird **jedes Item einzeln** durch alle vier Ebenen
geprüft — Batching spart Orchestrierungs-Overhead, nicht Prüftiefe pro
Item (§10.6).

### 8.2 Ebene 4 — Konzept-Treue

Zusätzliche Prüfung: Weicht die Umsetzung erkennbar von `konzept.md` ab —
Scope überschritten, ein explizites Non-Goal umgesetzt, ein Muss-Haben-
Punkt trotz Gelegenheit ausgelassen? Ein Fund auf dieser Ebene wird
**genauso behandelt wie ein Fund auf Ebene 1-3**: `CRITICAL`/`MAJOR` →
`issues` → Fix-Step, `MINOR` → „Sonstige Beobachtungen". Das ist die
zentrale Anti-Drift-Prüfung dieses Workflows — sie blockiert bewusst,
anders als die Tech-Debt-Beobachtungen unten.

### 8.3 Tech-Debt-Beobachtungen (nie `issues`, eigener Kanal)

Sieht der Kritiker während der Prüfung ein Architektur-/Anti-Pattern-
Problem, das **außerhalb des Scopes dieses Steps** liegt (z. B. eine neu
gebaute Struktur dupliziert eine bereits bestehende, die stattdessen
hätte wiederverwendet/generalisiert werden sollen) — das ist **kein**
Finding, löst **keinen** Fix-Step aus, unabhängig davon wie gravierend es
aussieht. Stattdessen: ein Eintrag in `<task-dir>/tech-debt.md` (Template
siehe `templates/tech-debt.md`), mit einer **Priorität** (`hoch`/
`mittel`/`niedrig` — bewusst deutsch und nicht `CRITICAL`/`MAJOR`/`MINOR`,
um jede Verwechslung mit den blockierenden Findings aus §8.1/8.2
auszuschließen: Tech-Debt-Priorität ist reine Sortierhilfe für den
Menschen, nie ein automatischer Auslöser).

**Warum ein eigener Kanal statt Blocken:** Ein Kritiker, der einen
größeren Umbau vorschlagen will, darf das nicht selbst entscheiden
(§11) — und das gilt konsequent zu Ende gedacht nicht nur für *große*
Umbauten, sondern für **jede** Beobachtung außerhalb des Step-Scopes. Das
verhindert unkontrolliertes Scope-Wachstum des Loops (das „immer weiter
perfektionieren"-Risiko, siehe `../README.md` für die Einordnung) und
hält die Entscheidung „jetzt fixen oder liegen lassen" beim Nutzer.

**Sichtbarkeit statt Automatik:** `tech-debt.md`-Einträge werden vom
Planer gelesen (§7.2 Schritt 3), aber **nie** automatisch in ein Epic
oder einen Step verwandelt. Will der Nutzer einen Eintrag angehen: manuell
ein neues Epic in `roadmap.md` ergänzen (mit Verweis auf die Tech-Debt-ID)
oder einen neuen Task starten.

### 8.4 Global-Modus (Abschluss, §6.3)

Am Ende des Tasks bekommt der Kritiker `konzept.md`, `roadmap.md`,
`tech-debt.md` und **alle** Step-Results/-Reviews plus den Projekt-Code
als Input. Er prüft: Passt das Gesamtergebnis zur ursprünglichen
Intention aus `konzept.md`? Sind Seiteneffekte übersehen worden? Läuft
Build/Test? Ist `roadmap.md` vollständig abgehakt?

Er fasst außerdem `tech-debt.md` im Summary zusammen (Anzahl Einträge
nach Priorität), statt selbst neue Architektur-Funde zu suchen — das
passiert kontinuierlich schon pro Step (§8.3), ein zusätzlicher globaler
„Konsistenz-Sweep" wäre hier Doppelarbeit.

## 9. Tech-Debt-Log — Konventionen

- Datei: `<task-dir>/tech-debt.md`, Template `templates/tech-debt.md`
- Append-only, ein Eintrag (`TD-NNN`) pro Beobachtung
- Pflichtfelder pro Eintrag: Fundort (Step + Datei:Zeile), Befund, Warum
  nicht sofort gefixt, Priorität (`hoch`/`mittel`/`niedrig`), Vorschlag
  (grobe Richtung, kein Detailplan), Status (`offen` — Änderung auf
  `erledigt`/`verworfen` ist **manuell**, macht kein Subagent automatisch)
- **Index-Tabelle am Dateianfang** (ID, Bereich/Datei, Priorität,
  Kurzfassung in einem Halbsatz): Der Kritiker schreibt Index-Zeile und
  Volltext-Eintrag immer zusammen; der Planer liest im Step-Modus nur den
  Index und den Volltext gezielt (§7.2 Punkt 3). Gleiches Prinzip wie der
  Regel-Index in `roadmap.md` (§5) und aus demselben Grund: eine Datei,
  die über den Task wächst, aber bei jedem Step-Modus-Aufruf erneut
  gelesen wird, darf nicht als Volltext in jeden dieser Kontexte fließen.
  Ein Eintrag ohne Index-Zeile ist für den Planer faktisch unsichtbar.
- Kein Fix-Budget, kein Loop-Guard nötig — da nie automatisch in Arbeit
  überführt, kann diese Datei den Loop nicht blockieren oder verlängern

## 10. Konventionen

### 10.1 Status-Header (YAML-Frontmatter)

Jede Datei in `<task-dir>/` und `<task-dir>/step-NNN/` beginnt mit
YAML-Frontmatter. Das `status`-Feld ist die Quelle der Wahrheit für
„wer ist dran" — nicht ein internes Task-/To-do-Feature des jeweils
verwendeten Werkzeugs.

### 10.2 Epic- und Schritt-Größe

Epic-Größe (Planer, Roadmap-Modus): grob genug, dass sie beim Start des
Tasks sinnvoll geschätzt werden kann, aber erkennbar mehr als ein
einzelner Step.

Schritt-Größe (Planer, Step-Modus): keine fixe Obergrenze — der Planer
balanciert zwischen „in einem Commit committbar", „in einer Review-Runde
prüfbar", „in sich geschlossen" und „kleiner als das ganze Epic". Für die
Sonderform Sammel-Step (mehrere einzeln triviale Low-Risk-Änderungen)
siehe §10.6.

### 10.3 Git-Strategie

- Alles auf dem **aktuellen Branch** (kein hartcodierter Branch — der
  Nutzer arbeitet, wo er arbeitet)
- Conventional Commits, deutsche Imperativ-Form (sofern Projekt-Rules
  nichts anderes vorgeben — siehe `skills/coder/SKILL.md`)
- **Task-Doku wird mitcommittet, nicht nur auf der Platte belassen** —
  jeder Step hinterlässt eine nachvollziehbare Commit-Historie seiner
  Zustände. Pro Step entstehen dabei mehrere kleine Commits statt einem
  großen:
  1. **Code-Commit** (Coder): Code + Tests + ggf. Produkt-Doku
  2. **Doku-Commit** (Coder): `step-plan.md`-Status + `step-result.md`
  3. **Planungs-Commit** (Orchestrator, nach jedem Planer-Aufruf): der
     neue `step-plan.md` **zusammen mit** etwaigen Änderungen an
     `roadmap.md` — beide entstehen im selben Planer-Aufruf und gehören
     damit in denselben Commit
  4. **Review-Commit** (Orchestrator, nach jedem Kritiker-Aufruf):
     `step-review.md` + Status-Update in `step-plan.md` + etwaige neue
     Einträge in `tech-debt.md`

  Grund für mehrere statt eines Commits: `step-result.md` referenziert
  den Hash des Code-Commits — der kann erst *nach* dem Code-Commit
  bekannt sein, ein einziger gemeinsamer Commit wäre also nur per
  nachträglichem Amend möglich, was hier bewusst vermieden wird (siehe
  `skills/coder/SKILL.md` Schritt 5-7).
- **Kein Push durch den Workflow** — der Nutzer pusht selbst, wenn er
  bereit ist. Der Workflow macht nur lokale Commits.
- **Git-Historie ist geschriebene Vergangenheit — nach einem Commit wird
  er nie wieder verändert.** Das gilt absolut, für jeden Commit,
  unabhängig davon, wie unwichtig er wirkt, wie unpassend platziert er
  aussieht, wie lang sein Subject ist, oder ob er schon von anderen
  Dateien per Hash referenziert wird. **Ausnahmslos verboten:**
  `git commit --amend`, `git rebase` (auch `-i`), `git reset --hard` auf
  bereits committete Commits, `git filter-branch`/`filter-repo`,
  Force-Push. Das gilt für **jede** Rolle, insbesondere den
  Orchestrator selbst — nicht nur für den Coder (dessen spezifisches
  „kein Amend fürs Code-Ergebnis" oben ist nur ein Sonderfall dieser
  allgemeinen Regel, nicht deren ganzer Umfang).
  - **Fällt auf, dass etwas vergessen wurde** (z. B. Task-Doku, die vor
    Einführung einer neuen Regel liegen geblieben ist): einfach **jetzt**
    committen, an der aktuellen `HEAD`-Position, mit einer ehrlichen
    Commit-Message, die das als Nachtrag kennzeichnet (z. B.
    `chore(task): Nachtrag — vor Doku-Commit-Regel liegen gebliebene
    Step-Dateien committen`).
  - **Nie versuchen, die Historie so aussehen zu lassen, als wäre der
    Commit schon immer an der „richtigen" chronologischen Stelle
    gewesen.** Ein Rebase zu diesem Zweck schreibt jeden nachfolgenden
    Commit-Hash um — auch wenn der Inhalt dabei unverändert bleibt,
    werden dadurch alle bereits geschriebenen Commit-Hash-Referenzen in
    `step-result.md`/`step-review.md`/`task-state.md` ungültig
    (verwaiste, nicht mehr erreichbare Hashes), und Zeitstempel/
    Reihenfolge werden für jeden, der die Historie später liest,
    unzuverlässig.

### 10.4 Build/Test-Erkennung

Der Planer leitet Build-/Test-Commands **aus dem Projekt** ab:
- `.csproj`/`.sln` → `dotnet build` / `dotnet test`
- `pyproject.toml`/`pytest.ini` → `pytest`
- `package.json` mit `test`-Script → `npm test` / `pnpm test`
- `Cargo.toml` → `cargo build` / `cargo test`
- `go.mod` → `go build ./...` / `go test ./...`
- etc.

Sie sind **nicht** im Workflow hartcodiert. Festgehalten werden sie in
der Tech-Stack-Notiz in `roadmap.md` (§7.1) — Coder und Kritiker
bekommen sie von dort bei jedem Aufruf mitgegeben.

### 10.5 Loop-Guard (Fix-Budget)

- **Max 3 Fix-Runden pro Step** (`step-NNN/fix-01` .. `fix-03`). Der
  Guard ist bewusst **pro Step**, nicht pro Task: dass über einen langen
  Task hinweg mehrere Steps je einmal nachgebessert werden müssen, ist
  normal und kein Alarmsignal. Ein einzelner Step, der auch nach 3
  Fix-Runden nicht grün wird, ist das eigentliche Alarmsignal — meist
  stimmt dann der Step-Scope oder der Ansatz nicht.
- Bei Erreichen des Limits für einen Step: dieser Step → `blocked` (Loop
  pausiert, Nutzer klärt).
- **Zusätzlicher Task-weiter Not-Anker:** Bei insgesamt mehr als
  `max_total_fix_rounds` (Default 12) Fix-Runden über alle Steps des
  Tasks hinweg → gesamter Task auf `aborted`, unabhängig vom Status der
  Einzel-Steps. Schutz gegen systemische Probleme (z. B. eine falsche
  Tech-Stack-Notiz), die sich durch viele Steps zieht.
- Konfigurierbar pro Task via `<task-dir>/config.md` (Felder
  `max_fix_rounds_per_step`, Default 3; `max_total_fix_rounds`,
  Default 12).
- Das Budget bezieht sich ausschließlich auf Fix-Steps aus
  `issues`-Verdicts (§8.1/8.2). Der Tech-Debt-Kanal (§8.3/§9) hat kein
  eigenes Budget, weil er nie automatisch in Arbeit überführt wird.
  `blocked` aus Infrastruktur-/Tooling-Gründen (§11) verbraucht ebenfalls
  nichts.

### 10.6 Step-Referenzen (`related_to`) und Micro-Batches

**`related_to` — Pointer, kein Cache.** `related_to` im Frontmatter von
`step-plan.md` verweist auf andere Steps, von denen dieser Step abhängt —
im Fix-Modus auf den auslösenden `step-review.md` (§6.2.1), sonst auf
einen früheren Step desselben Tasks. Ein Eintrag sagt nur *wo
nachschauen*, nie *was dort steht* — er behauptet nichts über den Inhalt
des referenzierten Steps. Coder und Kritiker lesen bei nicht-leerem
`related_to` deshalb den **aktuellen** Stand nach (`step-result.md` + die
tatsächlichen Dateien), bevor sie sich darauf verlassen — nie die
ursprüngliche Plan-Beschreibung ungeprüft übernehmen.

**Micro-Batches.** Mehrere einzeln triviale Änderungen würden bei
strikter 1-Step-pro-Änderung-Regel jede für sich einen vollen
Planer→Coder→Kritiker-Zyklus samt eigener Commits durchlaufen — Overhead,
der in keinem Verhältnis zur Änderung steht.

- **Geltungsbereich:** gebündelt wird nur **innerhalb des gerade
  geplanten Epics**, nie epic-übergreifend — der Planer sieht im
  Step-Modus ohnehin nur diesen Ausschnitt. Ein Epic, das selbst aus
  mehreren trivialen Low-Risk-Einzeländerungen besteht (z. B. „alle
  veralteten Kommentare in Modul X entfernen"), darf als ein
  `step_type: batch`-Step geplant werden.
- **Eligibility:** nur Änderungen, die der Planer mit
  `estimated_risk: low` einstuft. `medium`/`high` wird **nie** gebatcht,
  auch nicht, wenn es sich thematisch anbieten würde.
- **Deckelung (doppeltes Limit, pro Batch):** max. `max_batch_items`
  (Default **8**) und max. `max_batch_diff_lines` geschätzte Diff-Zeilen
  (Default **40**). Würde eine der beiden Grenzen überschritten: neuer
  Batch-Step statt Erweiterung. Beide konfigurierbar über
  `<task-dir>/config.md`. Grund für die doppelte Grenze: „50 Dateien mit
  je 1 Zeile" und „1 Datei mit 50 Zeilen" haben dieselbe Item-Zahl, aber
  sehr unterschiedliche Review-Last.
- **Struktur:** `step_type: batch` im Frontmatter (statt `single`, dem
  impliziten Default) plus `items`-Liste. Jedes Item bekommt im Step-Plan
  eine eigene Unterüberschrift, im Result eine eigene Zeile, im Review
  einen eigenen Prüf-Absatz. Coder und Kritiker behandeln jedes Item
  einzeln, mit derselben Sorgfalt wie einen eigenständigen Step.
- **Ein Commit pro Batch, nicht pro Item** (Commit-Body listet die Items
  auf) — das ist der eigentliche Overhead-Gewinn.
- **Fix-Budget bleibt geteilt, nicht pro Item:** ein Batch-Step hat
  dasselbe Budget wie jeder andere Step (§10.5), unabhängig von der
  Item-Zahl. Ein Batch, der ungewöhnlich viele verschiedene Items durch
  Fix-Runden schleift, ist selbst ein Signal, dass der Batch (oder die
  Risiko-Einstufung dahinter) fragwürdig war.

### 10.7 Artefakt-Umfang — Darstellung kürzen, nie die Prüfung

Die Task-Artefakte werden überwiegend von **Agenten** gelesen, nicht vom
Menschen: `step-result.md` vom Kritiker und vom Planer, `step-review.md`
vom Planer beim nächsten Step-Modus-Aufruf — und am Task-Ende lädt der
globale Kritiker (§6.3) `konzept.md` + `roadmap.md` + `tech-debt.md` +
**alle** Step-Results und -Reviews gleichzeitig. Das ist der größte
Einzel-Kontext des ganzen Workflows; jeder überflüssige Absatz wird dort
noch einmal vollständig mitbezahlt.

Daraus zwei Regeln:

- **Verdict-abhängiger Umfang bei `step-review.md`:** Bei `approved` je
  Prüfebene ein Satz, leere Abschnitte weglassen statt „Keine."
  schreiben. Bei `issues`/`blocked` unverändert ausführlich — dort führt
  der Inhalt zu einer Handlung (Fix-Step bzw. Nutzer-Entscheidung), dort
  ist Kürze schädlich.
- **Grüne Build-/Test-Outputs einzeilig** in `step-result.md` und
  `step-review.md` (Command + Ergebnis, ggf. Testzahl). Bei rot:
  gekürzter Fehler-Output, denn der wird tatsächlich gelesen.

**Die Grenze dieser Regel:** Sie betrifft ausschließlich, was
aufgeschrieben wird. Die vier Prüfebenen (§8) laufen in jedem Fall
vollständig und in gleicher Tiefe — ein knapperes `approved`-Review ist
kein flüchtigeres Review. Muss beim Kürzen etwas
Entscheidungsrelevantes wegfallen, war es kein `approved`-Fall.

Nicht betroffen ist `step-plan.md`: Der Plan ist die Leitplanke für den
Coder (der bewusst nicht selbst plant, §4) und darf ruhig ausführlich
sein — bis hin zur optionalen Code-Skizze. Ein unpräziser Plan kostet
eine Fix-Runde, und die ist teurer als jeder dort eingesparte Absatz.

### 10.8 Modell-Zuweisung pro Rolle (optional)

Der Nutzer kann beim Start pro Rolle ein Modell vorgeben (typischer Fall:
ein günstigeres/schnelleres Modell für den Coder, der nur einen fertigen
Plan ausführt, und ein stärkeres für Planer und Kritiker, die urteilen
müssen). Der Orchestrator hält die Wahl im Config-Block von
`task-state.md` fest (`model_planer`/`model_coder`/`model_kritiker`) und
gibt sie bei jedem Subagenten-Aufruf mit (`orchestrator.md` Schritt 2).

Persistiert wird sie, weil ein Task in einer **neuen Session** fortgesetzt
werden kann und `orchestrator.md` Schritt 1 Fall B dabei ohne Rückfrage
weiterläuft — stünde die Zuweisung nur im ursprünglichen Prompt, liefen
die Subagenten nach einem Resume still auf dem Default-Modell.

Die Werte sind **freier Text** und werden nie validiert: welche Modelle
existieren, weiß nur der Nutzer und sein Werkzeug. Ohne Angabe macht der
Orchestrator keine Vorgabe und fragt auch nicht nach.

## 11. Edge-Cases & Failure-Modes

| Situation | Verhalten |
|---|---|
| Coder schreibt kein `step-result.md` | Step bleibt auf `in_progress`, nach Timeout → `blocked` |
| Coder committet nicht | `step-result.md` fehlt der Commit-Hash → `blocked` |
| Kritiker findet Verstoß gegen `<rules_dir>` oder `konzept.md` | Fix-Step mit konkretem Fix-Plan (§8.1/8.2, §6.2.1) |
| Kritiker sieht Architektur-/Anti-Pattern-Problem außerhalb des Step-Scopes | **Kein** Fix-Step — Eintrag in `tech-debt.md` (§8.3), Loop läuft ungebremst weiter |
| Kritiker will einen größeren Umbau vorschlagen | **Nicht erlaubt** — entweder Tech-Debt-Eintrag (§8.3) oder `blocked`, der Nutzer entscheidet |
| Build/Test schlägt fehl (Code-Ursache) | Coder fixt im selben Step; falls nicht möglich → `blocked` |
| Build/Test schlägt fehl wegen fehlender/nicht erreichbarer Infrastruktur oder Tooling (DB down, Tool fehlt, …) | Sofort `blocked` (Blocker-Art: `infrastructure`), **kein** Fix-Versuch verbraucht — siehe `skills/coder/SKILL.md` Schritt 4a. Wie jedes `blocked`: kein Fix-Step, keine Anrechnung aufs Fix-Budget (§10.5) |
| Planer erkennt beim Roadmap-Abgleich: Epic ist durch frühere Steps bereits obsolet | Epic in `roadmap.md` als „obsolet — <Grund>" markieren statt löschen (Nachvollziehbarkeit), nicht mehr weiterplanen |
| Planer erkennt: `konzept.md` selbst ist zu vage für Roadmap-Modus | Blockt sofort mit Begründung (§3.2) |
| Rules-Verzeichnis mehrdeutig oder keins gefunden | Nutzer wird gefragt, siehe §3.1 |
| Nutzer ergänzt während des Loops `konzept.md` | Manueller Eingriff: Loop pausieren, Planer gleicht beim nächsten Step-Modus-Aufruf automatisch gegen die aktualisierte `konzept.md` ab (Schritt 1 in §7.2 liest sie ohnehin jedes Mal neu) |
| Diskspace/Git-Konflikt/was auch immer | `blocked`, Nutzer klärt |

## 12. Deliverables

Am Ende eines erfolgreichen Loops existieren:
- `<task-dir>/roadmap.md` — vollständig abgehakt
- `<task-dir>/tech-debt.md` — alle während des Loops gesammelten,
  bewusst nicht gefixten Beobachtungen
- `<task-dir>/task-summary.md`, `<task-dir>/task-state.md`
- `<task-dir>/step-NNN/{step-plan,step-result,step-review}.md` je Step
  (plus `fix-XX/…` bei Nachbesserungen)
- Mehrere Commits in Git pro Step (Code, Doku, Planung, Review — siehe
  §10.3), alle lokal, nicht gepusht — zusammen eine vollständige,
  lesbare Historie aller Step-Zustände und Fix-Runden
