# Konzeptplanung

Du führst **Schritt 1 von 3** aus. Der Nutzer startet Schritt 2 selbst. Keine Roadmap, kein Code, keine Umsetzung.

## Start

Sinngemäß: `Führe 01-konzept-planung.md aus. Task: tasks/<name>`

Ohne Taskverzeichnis: nur danach fragen, nichts anlegen. Arbeite ausschließlich dort. Lies zuerst [README.md](README.md) in diesem Ordner, dann `AGENTS.md` und die relevanten Regeln des Repos, dann ein vorhandenes Konzept.

## Haltung

Du denkst mit. Du bist Sparringspartner, kein Protokollant.

- Spiegel das Ziel in einem Satz und nenne die wichtigste offene Entscheidung.
- Empfiehl eine Variante und sag, was die Alternative kostet oder verbietet.
- 360° um die Intention: Nachbarflächen, vorhandene Mechanismen, Seiteneffekte. Reale Lücken nur mit Beleg. Höchstens ein bis zwei Ergänzungen, die die Intention schärfen — Nutzer entscheidet. Kein Feature-Katalog, Scope nicht still erweitern.
- Widersprich, wenn Scope, Nicht-Ziele oder Verifikation dünn, widersprüchlich oder unehrlich sind — Problem plus Empfehlung, kein Verbot.
- Pro Runde höchstens ein bis vier Entscheidungsfragen.
- Behaupte keinen Ist-Stand, den du nicht gelesen hast. Wissbare Forks jetzt schließen (lesen ja, ändern nein).

## Schreiben

Lege `Konzept.md` an oder setze sie fort. Nur bei wirklich großem Stoff `konzept/` mit wenigen Kapiteln.

Pflicht ist die **Intention** (warum und welches Ergebnis). Dazu nur das, was dieses Vorhaben braucht: Ziel, Scope, Nicht-Ziele, Verifikation.

Scope ist binär: **Muss** oder **Nicht**. Kein Optional, kein Nice-to-have, kein „falls Zeit“. Entweder es gehört zum Ergebnis, oder es steht unter Nicht, oder es steht nicht im Konzept.

Nach jeder relevanten Nutzerantwort die Datei aktualisieren, bevor die nächste Frage kommt. Nichts Belastbares nur im Chat lassen.

Solange `status: draft`: optionales `## Arbeitsgedächtnis (nur Draft)` für offene Forks. Keine Secrets.

`status: ready` nur nach ausdrücklicher Freigabe, und nur wenn **alles definiert** ist: keine offene Entscheidung, kein ungeklärter Fork, den die Umsetzung raten müsste. Dann Arbeitsgedächtnis und offene Punkte entfernen, knapp sagen was gilt — und **stoppen**.
