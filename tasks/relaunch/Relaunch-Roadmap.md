# Relaunch – Arbeitsroadmap

Dieses Dokument hält die gemeinsame Diskussion fest. Es ist eine Arbeitsgrundlage und wird mit neuen Entscheidungen fortgeschrieben. Das ursprüngliche [Relaunch-Konzept](Relaunch-Konzept.md) bleibt unverändert.

## Grundintention

Ein Agent soll seinen eigentlichen Entwicklungstask abschließen und dabei ordentlich arbeiten können. Eindeutige technische Fehler dürfen ihn direkt stoppen. Hinweise auf mögliche Architektur- oder Qualitätsprobleme brauchen dagegen ein Review im Gesamtzusammenhang. Eine Kennzahl allein entscheidet nicht, ob Code umgebaut werden sollte.

Das neue lokale Werkzeug soll solche möglichen Problemherde deterministisch finden und für Agent und Nutzer nachvollziehbar aufbereiten. Es blockiert keinen Build und erzwingt keine automatische Korrektur. Die Entscheidung über ein Finding entsteht im Review.

## Bisher festgehalten

- Etablierte Build-Linter sollen nur mit Regeln eingesetzt werden, bei denen ein Verstoß hinreichend eindeutig ist und eine direkte Korrektur keine eigentliche Ursache verdeckt.
- Das Review-Werkzeug bekommt eine Solution und eine Regelkonfiguration und schreibt sinnvoll strukturierte Markdown-Berichte für Agenten in einen Ausgabeordner.
- Es konzentriert sich auf wenige, konkrete Muster, die in Agenten-Workflows häufig zu Qualitätsproblemen führen. Eine lange Methode kann dabei ein sinnvoller Hinweis sein, aber auch fachlich genau richtig.
- **Reporting und Entscheidungsverlauf gehören zum ersten nutzbaren Stand.** Ein akzeptiertes Finding darf nicht bei jedem Lauf unverändert erneut als offene Arbeit erscheinen.
- Zu einem Finding werden der ursächliche Quellcode, seine damaligen Messwerte, die Entscheidung und der Verlauf gespeichert. So lassen sich spätere Änderungen vergleichen und Regeln anhand tatsächlicher Reviews beurteilen.
- Wird der für ein akzeptiertes Finding relevante Code geändert, kommt es erneut ins Review. Reine Formatierung und möglichst auch harmlose lokale Umbenennungen sollen keine erneute Meldung auslösen; fachliche Änderungen dürfen nicht übersehen werden.
- „In diesem Fall in Ordnung“ und „Fehlalarm der Regel“ sind unterschiedliche Review-Ergebnisse. Beide sind für die spätere Bewertung der Regeln wichtig.
- Die künftige Code-Navigation wird unabhängig evaluiert. Die Einstellung und Archivierung des bisherigen AiNetLinter ist die derzeitige Erwartung, noch kein vollzogener Schritt.

## Roadmap

1. **Review-Vertrag festlegen:** Was ist ein Finding, welche Entscheidungen kann ein Review treffen, und wann wird ein bereits entschiedener Fall wieder offen? Dabei den gesamten Ablauf vom ersten Fund bis zum erneuten Review beschreiben.
2. **Ersten vollständigen Durchlauf bauen:** Analyse, Markdown-Bericht, strukturierte Review-Entscheidung, Speicherung und erneuter Lauf mit Unterdrückung unveränderter akzeptierter Findings gehören zusammen in den ersten nutzbaren Stand.
3. **Änderungen und Verlauf belastbar machen:** Identität eines Findings über Läufe hinweg, Vergleich des relevanten Codes, Quellcode-Snapshots und nachvollziehbare Historie festlegen. Besonders prüfen: Umbenennung, Verschiebung, Aufteilung und fachliche Erweiterung einer Methode.
4. **Regeln anhand der Reviews schärfen:** Mit wenigen typischen Problemen beginnen. Erfassen, welche Findings zu Änderungen, bewusster Akzeptanz oder einer Korrektur der Regel führen. Schwellwerte und Regeln anhand dieser Daten weiterentwickeln.
5. **Einführung und Ablösung planen:** Die passende lokale Schnittstelle wählen, bestehende harte Regeln einordnen und erst danach die Rolle des bisherigen AiNetLinter und seine Archivierung entscheiden.

## Offene Entscheidungen

- Eine lokale CLI oder ein MCP-Server als primäre Schnittstelle; es soll zunächst nur einen Weg geben.
- Ort und Format des dauerhaften Review-Verlaufs sowie die Art, wie ein Agent eine Entscheidung strukturiert einträgt.
- Genaue Grenze zwischen harter technischer Regel und Review-Hinweis.
- Welche wenigen Regeln den ersten vollständigen Durchlauf tragen.
- Wie der Vergleich relevante Änderungen sicher erkennt, ohne bei bloßer Formatierung oder harmlosen Umbenennungen ständig neu zu melden.

## Leitplanken für die weitere Diskussion

- Ein Finding ist ein Anlass zur Prüfung, kein Refactoring-Auftrag.
- Eine akzeptierte Ausnahme bleibt sichtbar und auswertbar; sie ist kein dauerhaft unsichtbarer Kommentar im Quellcode.
- Gespeicherte Entscheidungen gelten für den damaligen Codezustand. Späterer Drift muss erkennbar sein.
- Messwerte helfen bei der Kalibrierung. Sie ersetzen nicht die fachliche Beurteilung eines konkreten Falls.
