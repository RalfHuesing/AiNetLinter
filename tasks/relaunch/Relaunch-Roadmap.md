# Relaunch – Arbeitsroadmap

Dieses Dokument hält die gemeinsame Diskussion fest. Es ist eine Arbeitsgrundlage und wird mit neuen Entscheidungen fortgeschrieben. Das ursprüngliche [Relaunch-Konzept](Relaunch-Konzept.md) bleibt unverändert.

## Grundintention

Ein Agent soll seinen eigentlichen Entwicklungstask abschließen und dabei ordentlich arbeiten können. Eindeutige technische Fehler dürfen ihn direkt stoppen. Hinweise auf mögliche Architektur- oder Qualitätsprobleme brauchen dagegen ein Review im Gesamtzusammenhang. Eine Kennzahl allein entscheidet nicht, ob Code umgebaut werden sollte.

Das neue lokale Werkzeug soll solche möglichen Problemherde deterministisch finden und für Agent und Nutzer nachvollziehbar aufbereiten. Es blockiert keinen Build und erzwingt keine automatische Korrektur. Die Entscheidung über ein Finding entsteht im Review.

Das Werkzeug beobachtet ein Repository über Zeit: Es findet mögliche Probleme, liefert die Grundlage für ein Review, hält dessen Entscheidung fest und legt einen akzeptierten Fall bei relevanten Codeänderungen erneut vor.

Den eigentlichen Audit führen Agent und Nutzer durch. Das Werkzeug liefert Befunde und Kontext, nimmt strukturierte Entscheidungen entgegen und verfolgt deren Gültigkeit über spätere Läufe.

## Arbeitsthese: Code für Agenten

Die heutigen Linter-Regeln und Entwicklungsabläufe sind über Jahrzehnte vor allem für Menschen entstanden. Agenten haben ähnliche Bedürfnisse, aber möglicherweise andere Schwerpunkte. Code muss für sie zuverlässig verständlich und mit möglichst wenig Such- und Kontextaufwand erschließbar sein; bloße „Schönheit“ ist kein Selbstzweck. Wenn ein Agent für eine kleine Funktion Hunderte Navigationsschritte braucht, kann das ein ernstes Qualitätsproblem sein. Diese These soll die Auswahl und spätere Bewertung der Audit-Regeln leiten; sie ist noch keine belegte Regel.

## Bisher festgehalten

- Etablierte Build-Linter sollen nur mit Regeln eingesetzt werden, bei denen ein Verstoß hinreichend eindeutig ist und eine direkte Korrektur keine eigentliche Ursache verdeckt.
- Das Review-Werkzeug bekommt eine Solution und eine Regelkonfiguration und schreibt sinnvoll strukturierte Markdown-Berichte für Agenten in einen Ausgabeordner.
- Es konzentriert sich auf wenige, konkrete Muster, die in Agenten-Workflows häufig zu Qualitätsproblemen führen. Eine lange Methode kann dabei ein sinnvoller Hinweis sein, aber auch fachlich genau richtig.
- **Reporting und Entscheidungsverlauf gehören zum ersten nutzbaren Stand.** Ein akzeptiertes Finding darf nicht bei jedem Lauf unverändert erneut als offene Arbeit erscheinen.
- Zu einem Finding werden der ursächliche Quellcode, seine damaligen Messwerte, die Entscheidung und der Verlauf gespeichert. So lassen sich spätere Änderungen vergleichen und Regeln anhand tatsächlicher Reviews beurteilen.
- Jedes gemeldete Finding erhält eine referenzierbare ID. Ein Agent meldet sein Review-Ergebnis strukturiert zu dieser ID zurück, beispielsweise `false-positive`; die Entscheidung soll nicht von frei formuliertem Text abhängen.
- Findings, Quellcode-Snapshots und Review-Entscheidungen gehören zum jeweils analysierten Repository und werden dort unter Versionskontrolle aufbewahrt. Das konkrete Speicherformat ist noch offen.
- Es gibt keinen Status „später beheben“ und keine automatische Wiedervorlage nach einer Frist. Ein nicht bearbeitetes und nicht beantwortetes Finding bleibt offen und erscheint beim nächsten Review wieder.
- Wird der für ein akzeptiertes Finding relevante Code geändert, kommt es erneut ins Review. Reine Formatierung und möglichst auch harmlose lokale Umbenennungen sollen keine erneute Meldung auslösen; fachliche Änderungen dürfen nicht übersehen werden.
- „In diesem Fall in Ordnung“ und „Fehlalarm der Regel“ sind unterschiedliche Review-Ergebnisse. Beide sind für die spätere Bewertung der Regeln wichtig.
- Spätere Auswertungen sollen zeigen, wie häufig eine Regel zu einer Änderung, bewusster Akzeptanz oder einem bestätigten Fehlalarm führt. Der Bau dieser Auswertung gehört noch nicht zum ersten Produktstand.
- Die Umsetzung beginnt in einem neuen Repository. Passende Code-Bausteine aus AiNetLinter können gezielt übernommen werden.
- Die konkrete Speicherung bleibt in Klärung. Im neuen Code wird sie von Anfang an über eine Schnittstelle vorgesehen; für den anfänglichen technischen Aufbau kann eine Implementierung noch nichts speichern. Ein nutzbarer Review-Durchlauf braucht später eine echte Speicherung der Entscheidungen.
- Die künftige Code-Navigation wird unabhängig evaluiert. Die Einstellung und Archivierung des bisherigen AiNetLinter ist die derzeitige Erwartung, noch kein vollzogener Schritt.

## Vorläufige Richtung

- Ein lokal laufender MCP-Server ist derzeit gegenüber einer reinen CLI bevorzugt: Der Agent kann Findings direkt abrufen und Entscheidungen strukturiert zurückmelden. Die endgültige Wahl der primären Schnittstelle steht noch aus.
- Maßstab für den Neustart ist der neue Review-Ablauf, nicht ein bestimmter Anteil neu geschriebener Codezeilen.
- Für die repo-eigenen Daten erscheinen maschinenlesbare Textdateien pro Finding als gute Ausgangsbasis. SQLite wäre lokal bequem, verursacht als versionierte Binärdatei aber schwierige Diffs und Merges. Die Wahl ist noch offen; der konkrete Vorschlag steht im [Reporting- und Speicherentwurf](Reporting-Speicherentwurf.md).
- Eine gemeldete Behebung sollte erst als erledigt gelten, wenn ein vollständiger erneuter Scan bei gleicher Regel den Befund nicht mehr findet. Wiederholte Scans desselben unveränderten Findings sollten für spätere Statistiken nicht als neue Fälle zählen.

## Roadmap

1. **Review-Vertrag und Schnittstelle festlegen:** Was ist ein Finding, welche Entscheidungen kann ein Review treffen, und wann wird ein bereits entschiedener Fall wieder offen? Den primären Zugang für Agenten wählen und den Ablauf vom ersten Fund bis zum erneuten Review beschreiben.
2. **Neues Repository aufsetzen:** Nur benötigte Analyse-Bausteine übernehmen und den Anschluss für die spätere Speicherung bereits vorsehen.
3. **Ersten vollständigen Durchlauf bauen:** Analyse, Markdown-Bericht, strukturierte Review-Entscheidung über die Finding-ID, Speicherung im Repository und erneuter Lauf mit Unterdrückung unveränderter akzeptierter Findings gehören zusammen in den ersten nutzbaren Stand.
4. **Änderungen und Verlauf belastbar machen:** Identität eines Findings über Läufe hinweg, Vergleich des relevanten Codes, Quellcode-Snapshots und nachvollziehbare Historie festlegen. Besonders prüfen: Umbenennung, Verschiebung, Aufteilung und fachliche Erweiterung einer Methode.
5. **Regeln anhand der Reviews schärfen:** Mit wenigen typischen Problemen beginnen. Erfassen, welche Findings zu Änderungen, bewusster Akzeptanz oder einer Korrektur der Regel führen. Schwellwerte und Regeln anhand dieser Daten weiterentwickeln.
6. **Einführung und Ablösung planen:** Bestehende harte Regeln einordnen und erst danach die Rolle des bisherigen AiNetLinter und seine Archivierung entscheiden.

## Offene Entscheidungen

- Name des Werkzeugs für das neue Repository. Er soll AI und .NET erkennen lassen. `AiNetAudit` gefällt als Wortstamm, klingt allein aber so, als würde das Produkt den Audit selbst durchführen. `AiNetAuditTool` und `AiNetAuditKit` machen die unterstützende Rolle deutlicher; die Entscheidung steht aus. `CodeRadar` wurde verworfen. Eine exakte Google-Suche am 27.09.2026 zeigte für `AiNetAudit` und `AiNetAuditTool` keine klaren organischen Treffer; das ersetzt keine Prüfung von Marken oder Paketnamen.
- MCP-Server als primäre Schnittstelle bestätigen oder eine lokale CLI wählen; zunächst soll es nur einen primären Weg geben.
- Konkretes, Git-taugliches Speicherformat für den repo-bezogenen Verlauf und genaue Menge der zulässigen Review-Ergebnisse.
- Definition der späteren Kennzahlen: Regel-Treffer pro Scan, verschiedene Findings und erneut geöffnete Fälle sind unterschiedliche Größen.
- Stabilität einer Finding-ID über Codeänderungen, Umbenennungen und Verschiebungen hinweg.
- Genaue Grenze zwischen harter technischer Regel und Review-Hinweis.
- Welche wenigen Regeln den ersten vollständigen Durchlauf tragen.
- Wie der Vergleich relevante Änderungen sicher erkennt, ohne bei bloßer Formatierung oder harmlosen Umbenennungen ständig neu zu melden.
- Welche Teile des bisherigen AiNetLinter für den neuen Ablauf sinnvoll wiederverwendet werden können.

## Leitplanken für die weitere Diskussion

- Ein Finding ist ein Anlass zur Prüfung, kein Refactoring-Auftrag.
- Eine akzeptierte Ausnahme bleibt sichtbar und auswertbar; sie ist kein dauerhaft unsichtbarer Kommentar im Quellcode.
- Gespeicherte Entscheidungen gelten für den damaligen Codezustand. Späterer Drift muss erkennbar sein.
- Messwerte helfen bei der Kalibrierung. Sie ersetzen nicht die fachliche Beurteilung eines konkreten Falls.
