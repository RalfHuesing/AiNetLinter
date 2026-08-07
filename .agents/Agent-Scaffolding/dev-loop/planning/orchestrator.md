---
workflow: konzept-workflow
status: draft
role: "interaktiver Begleiter (läuft in der aktuellen Session, kein Subagenten-Loop)"
invoked_as: "orchestrator.md <task-dir> (Pfad zu diesem Ordner ist projektabhängig)"
produces_input_for: ../drift-loop/orchestrator.md
---

# Konzept-Workflow: Interaktive Konzeptentwicklung

## Pfad-Hinweis

Alle Pfade in dieser Datei, die auf andere Dateien **innerhalb von
`dev-loop/`** verweisen (z. B. `templates/konzept.md`, `../drift-loop/…`),
sind relativ zu dieser Datei zu verstehen — funktionieren unabhängig
davon, wo `dev-loop/` in deinem Projekt liegt. Verweise auf **projekt-
eigene** Konventionen (`<rules_dir>/**`, erkannt gemäß
`../drift-loop/spec.md` §3.1; `README.md`, `docs/**`) meinen dagegen den
Ort relativ zu deinem **Projekt-Root** (wo der Agent
gerade arbeitet) — unabhängig davon, wo `dev-loop/` selbst liegt.

## Zweck

Du wirst mit dieser Datei plus einem Task-Verzeichnis aufgerufen, z. B.:

> `<pfad-zu-dev-loop>/planning/orchestrator.md tasks/tetris-mobile`

Der Nutzer hat dort (meistens) bereits eine `konzept.md` mit einer rohen,
in eigenen Worten geschriebenen Idee angelegt — oder gibt dir die Idee
direkt im Aufruf-Prompt mit. Deine Aufgabe: **im Dialog** mit dem Nutzer
diese Idee so weit schärfen, bis daraus eine Aufgaben-Doku wird, die
`../drift-loop/spec.md` §3.2 (Mindestanforderungen) erfüllt — danach kann
`../drift-loop/orchestrator.md` direkt darauf aufsetzen.

Diese Datei ist bewusst **tool-agnostisch und projekt-unabhängig**
formuliert — unverändert kopierbar/einbindbar in jedes andere Projekt,
wie der Rest von `dev-loop/` auch.

## Grundprinzip: Dialog statt Autonomie

Das unterscheidet diesen Workflow fundamental von `../drift-loop/`: Dort
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

Bevor du Projektkonventionen liest, ermittle **wo** sie liegen — der Ort
ist projektabhängig, nicht fest verdrahtet. Zwei bekannte Konventionen
werden geprüft: `.agents/rules/` und `.cursor/rules/`
(projekt-root-relativ).

- Existiert **genau eines** der beiden Verzeichnisse: das ist `rules_dir`
  — automatisch übernehmen, keine Rückfrage nötig.
- Existieren **beide** oder **keins** von beiden: frag den Nutzer explizit
  und offen (nicht nur Ja/Nein — der Nutzer kann auch einen dritten, hier
  nicht gelisteten Pfad nennen oder bestätigen, dass keine projektweiten
  Konventionen existieren).
- Trage das Ergebnis als `rules_dir` ins Frontmatter von `konzept.md` ein
  (Wert `keins`, falls der Nutzer bestätigt, dass es keine gibt).

Ab hier und in allen folgenden Schritten ist mit „Projektkonventionen"
immer `<rules_dir>/**` gemeint — der ermittelte Pfad, nicht wörtlich
`.agents/rules/**`.

### Anker lesen, Projekt-Art einschätzen

- Lies `<rules_dir>/**` (siehe oben), `README.md`, `docs/**`, `AGENTS.md`
  (projekt-root-relativ, siehe Pfad-Hinweis oben) — was immer davon
  existiert. `AGENTS.md` ergänzt `<rules_dir>/**`, ersetzt es nicht. Das
  ist dieselbe Anker-Grundlage wie beim Planer in `../drift-loop/spec.md`
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
    — der Planer im `drift-loop` verlässt sich beim Task-Start nicht auf
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

## Schritt 3a — Aktiv nach bestehenden Patterns/Mängeln suchen (nur brownfield)

Nur relevant bei `project_kind: brownfield` (bei `greenfield` gibt es
noch keinen Bestandscode, den man dafür durchsuchen könnte). Ergänzt den
einmaligen groben Überblick aus Schritt 2 um eine **pro Thema** aktive
Suche — Schritt 2 reicht allein nicht, weil er vor dem eigentlichen
Gespräch über konkrete Features passiert und daher nicht wissen kann,
wonach im Detail zu suchen ist.

**Wann:** Bevor du auf ein vom Nutzer beschriebenes neues Feature/eine
neue Fähigkeit antwortest bzw. die nächste Fragerunde (Schritt 4)
formulierst — nicht nur einmal zu Beginn.

**Was:**
1. **Pattern-Reuse-Check:** Durchsuche den Code aktiv (grep/Suche) nach
   bereits bestehenden, ähnlichen Strukturen zu dem, was der Nutzer gerade
   beschreibt — nicht nur aus dem Gedächtnis/Kontext urteilen. Findest du
   etwas Passendes: schlag vor, es wiederzuverwenden oder zu
   generalisieren, statt einen Neubau ins Konzept zu schreiben. Klassisches
   Beispiel: Nutzer will einen neuen Bestätigungsdialog, es existieren
   bereits zwei ähnliche generische Dialoge — Hinweis statt drittem Dialog.
2. **Mängel-Check:** Fällt dir beim Lesen der dafür relevanten Stellen
   zusätzlich ein tatsächlicher Mangel auf (unabhängig vom gerade
   besprochenen Thema) — bewerte gegen `<rules_dir>/**`, falls vorhanden
   (konkrete Regel zitieren). Ohne `rules_dir` nur bei offensichtlichen,
   unstrittigen Fällen hinweisen (z. B. klar erkennbares Duplikat im
   gelesenen Code) — bei subtileren Architektur-Fragen ohne kodifizierte
   Regeln zurückhaltend bleiben, da kein Maßstab für „Mangel" definiert ist.

**Wie ansprechen:** Einmal, konkret, mit Fundstelle — dann dem Nutzer die
Entscheidung überlassen („reicht dir die bestehende Lösung, oder soll
trotzdem neu gebaut werden?", „soll das mit in den Scope?"). Kein
wiederholtes Nachbohren, wenn der Nutzer bewusst ablehnt oder verschiebt
— siehe „Was du NICHT tun darfst" unten.

**Ergebnis:** Jeder Fund (angenommen, verschoben oder abgelehnt) landet in
`konzept.md` unter „Entdeckte Mängel/Redundanzen" (siehe Schritt 5) — auch
abgelehnte, damit dieselbe Frage nicht in einer späteren Runde erneut
aufkommt.

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
- **Funde aus Schritt 3a einpflegen:** Jeder Pattern-Reuse-/Mängel-Fund
  bekommt einen Eintrag unter „Entdeckte Mängel/Redundanzen" — unabhängig
  von der Nutzer-Entscheidung. Wird ein Fund angenommen: zusätzlich in
  „Scope > Muss-Haben" aufnehmen, mit Verweis zurück auf den Eintrag
  (Pointer-Prinzip, nicht doppelt ausformulieren).
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
- alle Abschnitte der Ziel-Struktur ausreichend konkret sind,
- `open_questions` leer ist oder nur noch Punkte enthält, die der Nutzer
  explizit als „später klären" oder Non-Goal markiert hat, **und**
- **„Nice-to-Have" leer ist.** Diese Sektion ist ein Zwischenspeicher für
  den Dialog, keine dauerhafte dritte Scope-Kategorie — die
  Umsetzungs-Loop (`../drift-loop/`) leitet Epics ausschließlich aus
  Muss-Haben ab (siehe `../drift-loop/skills/planer/SKILL.md`
  Roadmap-Modus Schritt 3); ein Punkt, der nur in Nice-to-Have steht,
  wird von keinem Subagenten je umgesetzt und bleibt für immer liegen.
  Vor `status: ready` muss daher jeder Punkt aufgelöst sein: entweder
  hochgestuft nach „Muss-Haben" (der Nutzer entscheidet sich jetzt aktiv
  dafür) oder verschoben nach „Non-Goals" mit Begründung „nicht jetzt" (der
  Nutzer entscheidet sich aktiv dagegen). Frag das im Zweifel explizit ab,
  genau wie bei offenen `open_questions` — keine dritte, unentschiedene
  Zwischenkategorie darf in die Umsetzung wandern.

Dann: **frage den Nutzer explizit**, ob das Konzept fertig ist — das
entscheidest nicht du allein. Bei Bestätigung:
- `status: ready`, finaler Commit (falls Git-Repo vorhanden).
- Kurze Zusammenfassung + Hinweis: *„Bereit für
  `../drift-loop/orchestrator.md <task-dir>`."*

## Was du NICHT tun darfst

- **Nicht raten, wenn unklar.** Lieber eine Runde mehr fragen, als eine
  Annahme unkommentiert ins Konzept schreiben.
- **Nicht auf `ready` setzen ohne explizite Nutzer-Bestätigung.**
- **Keine Implementierungsdetails vorwegnehmen**, die eigentlich der
  Planer in `../drift-loop/spec.md` treffen sollte (konkrete
  Dateiaufteilung, Klassen-/Funktionsnamen, Zeilen-genaue Änderungen) —
  hier geht es um das Konzept, nicht den Umsetzungsplan.
- **Verworfene Alternativen nicht stillschweigend weglassen** —
  dokumentieren, auch wenn der Nutzer sie nur kurz erwähnt und dann
  verworfen hat.
- **Nicht alles auf einmal fragen.** Kleine Runden, nicht ein
  Fragebogen mit zwanzig Punkten.
- **Bei einem abgelehnten oder verschobenen Fund aus Schritt 3a nicht
  erneut nachbohren.** Einmaliger Hinweis ist genug Sicherheitsnetz —
  der Fund bleibt trotzdem dokumentiert (siehe Schritt 5), wiederholtes
  Ansprechen nervt nur und widerspricht Schritt 4s „kleine Runden".
