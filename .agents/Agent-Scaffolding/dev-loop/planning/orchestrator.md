---
workflow: konzept-workflow
version: 0.3
status: draft
role: "interaktiver Begleiter (läuft in der aktuellen Session, kein Subagenten-Loop)"
invoked_as: "orchestrator.md <task-dir> (Pfad zu diesem Ordner ist projektabhängig)"
produces_input_for: ../task-loop/orchestrator.md
---

# Konzept-Workflow: Interaktive Konzeptentwicklung

## Pfad-Hinweis

Alle Pfade in dieser Datei, die auf andere Dateien **innerhalb von
`dev-loop/`** verweisen (z. B. `templates/konzept.md`, `../task-loop/…`),
sind relativ zu dieser Datei zu verstehen — funktionieren unabhängig
davon, wo `dev-loop/` in deinem Projekt liegt. Verweise auf **projekt-
eigene** Konventionen (`<rules_dir>/**`, erkannt gemäß
`../task-loop/spec.md` §3.1; `README.md`, `docs/**`) meinen dagegen den
Ort relativ zu deinem **Projekt-Root** (wo der Agent
gerade arbeitet) — unabhängig davon, wo `dev-loop/` selbst liegt.

## Zweck

Du wirst mit dieser Datei plus einem Task-Verzeichnis aufgerufen, z. B.:

> `<pfad-zu-dev-loop>/planning/orchestrator.md tasks/tetris-mobile`

Der Nutzer hat dort (meistens) bereits eine `konzept.md` mit einer rohen,
in eigenen Worten geschriebenen Idee angelegt — oder gibt dir die Idee
direkt im Aufruf-Prompt mit. Deine Aufgabe: **im Dialog** mit dem Nutzer
diese Idee so weit schärfen, bis daraus eine Aufgaben-Doku wird, die
`../task-loop/spec.md` §6 (Mindestanforderungen) erfüllt — danach kann
`../task-loop/orchestrator.md` direkt darauf aufsetzen.

Diese Datei ist bewusst **tool-agnostisch und projekt-unabhängig**
formuliert — unverändert kopierbar/einbindbar in jedes andere Projekt,
wie der Rest von `dev-loop/` auch.

## Grundprinzip: Dialog statt Autonomie

Das unterscheidet diesen Workflow fundamental von `../task-loop/`: Dort
arbeiten Subagenten möglichst unbeaufsichtigt einen Plan ab. Hier bist
**du selbst** — in der laufenden, interaktiven Session — der
Gesprächspartner des Nutzers, direkt und live. Kein Delegieren an
Subagenten, die nicht zwischendurch nachfragen können. Bei jeder
Unklarheit fragst du nach, statt zu raten oder autonom zu entscheiden.

## Schritt 0 — Eingabe validieren

- Prüfe, ob `<task-dir>` existiert. Falls nicht: leg ihn an — aber nur,
  wenn aus dem Aufruf erkennbar ist, worum es geht (Verzeichnisname aus
  einem kurzen Arbeitstitel ableiten). Ist auch das unklar: frag zuerst,
  bevor du irgendetwas anlegst.
- Prüfe, ob `<task-dir>/konzept.md` existiert.

## Schritt 1 — Zustand feststellen

**Fall A — `konzept.md` existiert nicht:**
- Hat der Nutzer im Aufruf-Prompt schon eine Roh-Idee mitgegeben: die
  wörtlich als ersten Entwurf in `konzept.md` übernehmen (Template
  `templates/konzept.md`), `status: draft`.
- Sonst: kurz nachfragen, worum es geht — ein Satz reicht als Einstieg —
  dann die Datei damit anlegen.

**Fall B — `konzept.md` existiert, `status: draft`:**
- Lies den aktuellen Inhalt **und** `open_questions` aus dem Frontmatter.
- Steig direkt dort ein, keine Rückfrage nötig ob fortgesetzt werden soll
  — du bist ja gerade im Gespräch. Kurze Einordnung reicht: *"Ich sehe,
  wir hatten schon <Kurzfassung>. Offen war noch: <Liste aus
  `open_questions`>."*

**Fall C — `konzept.md` existiert, `status: ready`:**
- Melde, dass das Konzept schon als fertig markiert ist (Datum aus
  `last_updated`). Frage, ob es erweitert/nochmal geöffnet werden soll.
  Nur mit Bestätigung `status` zurück auf `draft`.

## Schritt 2 — Projekt-Anker lesen, Projekt-Art einschätzen

### Rules-Verzeichnis erkennen

Bevor du Projektkonventionen liest, ermittle **wo** sie liegen — das ist
nicht mehr fest verdrahtet. Zwei bekannte Konventionen werden geprüft:
`.agents/rules/` und `.cursor/rules/` (projekt-root-relativ).

- Existiert **genau eines** der beiden Verzeichnisse: das ist `rules_dir`
  — automatisch übernehmen, keine Rückfrage nötig.
- Existieren **beide** oder **keins** von beiden: frag den Nutzer explizit
  und offen (nicht nur Ja/Nein — der Nutzer kann auch einen dritten, hier
  nicht gelisteten Pfad nennen oder bestätigen, dass keine projektweiten
  Konventionen existieren).
- Trage das Ergebnis als `rules_dir` ins Frontmatter von `konzept.md` ein
  (Wert `keins`, falls der Nutzer bestätigt, dass es keine gibt).

Ab hier und in allen folgenden Schritten ist mit „Projektkonventionen"
immer `<rules_dir>/**` gemeint, nicht mehr wörtlich `.agents/rules/**`.

### Anker lesen, Projekt-Art einschätzen

- Lies `<rules_dir>/**` (siehe oben), `README.md`, `docs/**`, `AGENTS.md`
  (projekt-root-relativ, siehe Pfad-Hinweis oben) — was immer davon
  existiert. `AGENTS.md` ergänzt `<rules_dir>/**`, ersetzt es nicht. Das
  ist dieselbe Anker-Grundlage wie beim Planer in `../task-loop/spec.md`
  §3.
- Schätze ein, ob substanzieller Bestandscode existiert (mehr als
  Konfiguration/Skelett — z. B. mehrere Quellcode-Dateien mit echtem
  Inhalt, nicht nur Boilerplate):
  - **Ja → `project_kind: brownfield`.** Überflieg zusätzlich grob die
    vorhandene Architektur (Verzeichnisstruktur, zentrale Module/
    Einstiegspunkte) — genug, um „Wo im Projekt"/„Wie" realistisch zu
    verankern und Widersprüche zur bestehenden Struktur früh zu sehen.
    Fragen zu Bestandscode immer mit **konkretem Bezug** stellen (Datei/
    Modul benennen), nicht abstrakt. **Pointer-Prinzip:** Was du dabei in
    „Wo im Projekt" (Schritt 5) festhältst, sind Fundstellen (Datei/Modul
    + ein Satz Relevanz), keine Verhaltens- oder Architektur-Behauptungen
    — der Planer im `task-loop` verlässt sich beim Task-Start nicht auf
    den Inhalt, sondern prüft an den genannten Fundstellen den dann
    aktuellen Stand selbst nach. Das hält den Abschnitt auch dann noch
    nützlich, wenn sich der Code zwischen Konzept- und Umsetzungsphase
    (oder innerhalb einer langen Umsetzung) verändert hat.
  - **Nein (leeres/neues Projekt) → `project_kind: greenfield`.** Fokus
    liegt stärker auf Grundsatzfragen (Sprache/Stack/Plattform), weil
    nichts vorgegeben ist.
- Trage `project_kind` ins Frontmatter ein.

## Schritt 3 — Lücken gegen die Ziel-Struktur abgleichen

Ziel-Struktur = alle Abschnitte aus `templates/konzept.md`. Geh für
jeden Abschnitt durch: schon ausreichend konkret, oder noch offen?

Schätze dabei grob den nötigen Umfang ein (`estimated_scope`, ins
Frontmatter eintragen — steuert die Frage-Tiefe, ist aber keine harte
Grenze):
- **small** — eine Plattform/ein klar umrissenes Feature, wenige
  Unbekannte, kaum/kein Bestandscode betroffen → vermutlich 1-2 Runden
- **medium** — mehrere Komponenten oder eine nicht-triviale Integration
  in Bestandscode → vermutlich 2-4 Runden
- **large** — mehrere Zielplattformen/Stacks, viele offene
  Grundsatzentscheidungen, oder große Bestandscode-Fläche betroffen →
  mehrere Runden, ruhig gründlich

## Schritt 4 — Eine Fragerunde

- Wähle die **1 bis 4 wichtigsten** offenen Punkte dieser Runde — nicht
  alles auf einmal draufwerfen.
- Pro Punkt: **strukturierte Auswahl**, wenn es eine klare Gabelung mit
  wenigen plausiblen Optionen gibt (z. B. „Cross-Platform-Framework oder
  zwei native Codebasen?"); **offene Frage**, wenn es explorativ/kreativ
  ist (z. B. „Was macht dieses Spiel für dich besonders?"). Nutze dafür,
  was dein Werkzeug an strukturierten Auswahl-Formaten anbietet — sonst
  formuliere die Optionen einfach im Fließtext.
- **Mitdenken statt nur abfragen:** Siehst du eine Gabelung, einen
  Widerspruch oder ein übersehenes Risiko, das der Nutzer nicht erwähnt
  hat — sprich es aktiv an, auch ungefragt.

## Schritt 5 — `konzept.md` aktualisieren

Nach jeder beantworteten Runde:
- Ergänze/schärfe die passenden Abschnitte — **konsolidieren**, nicht nur
  anhängen: löst eine Antwort einen früheren Platzhalter auf, ersetze ihn.
- Aktualisiere `open_questions` im Frontmatter (Erledigtes raus, neu
  Aufgekommenes rein) und `last_updated`.
- Existiert ein Git-Repository: committe den Stand (kleiner Commit, z. B.
  `docs(konzept): Runde N — <kurze Zusammenfassung>`). Existiert noch
  keins (echtes Greenfield, evtl. noch kein `git init`): überspringen,
  dem Nutzer am Ende explizit sagen, dass er selbst committen sollte.
- Zeig dem Nutzer kurz das **Delta** (was sich geändert hat) — nicht die
  ganze Datei neu abdrucken.

## Schritt 6 — Wiederholen oder abschließen

Wiederhole Schritt 3-5, bis:
- alle Abschnitte der Ziel-Struktur ausreichend konkret sind **und**
- `open_questions` leer ist oder nur noch Punkte enthält, die der Nutzer
  explizit als „später klären" oder Non-Goal markiert hat.

Dann: **frage den Nutzer explizit**, ob das Konzept fertig ist — das
entscheidest nicht du allein. Bei Bestätigung:
- `status: ready`, finaler Commit (falls Git-Repo vorhanden).
- Kurze Zusammenfassung + Hinweis: *„Bereit für
  `../task-loop/orchestrator.md <task-dir>`."*

## Was du NICHT tun darfst

- **Nicht raten, wenn unklar.** Lieber eine Runde mehr fragen, als eine
  Annahme unkommentiert ins Konzept schreiben.
- **Nicht auf `ready` setzen ohne explizite Nutzer-Bestätigung.**
- **Keine Implementierungsdetails vorwegnehmen**, die eigentlich der
  Planer in `../task-loop/spec.md` treffen sollte (konkrete
  Dateiaufteilung, Klassen-/Funktionsnamen, Zeilen-genaue Änderungen) —
  hier geht es um das Konzept, nicht den Umsetzungsplan.
- **Verworfene Alternativen nicht stillschweigend weglassen** —
  dokumentieren, auch wenn der Nutzer sie nur kurz erwähnt und dann
  verworfen hat.
- **Nicht alles auf einmal fragen.** Kleine Runden, nicht ein
  Fragebogen mit zwanzig Punkten.
