---
name: concept-planner
description: Erarbeite in einem vom Nutzer angegebenen Task-Verzeichnis interaktiv ein dauerhaft gepflegtes, zunächst als Draft geführtes AiNetLinter-Konzept bis zur expliziten Freigabe.
---

# Interaktiver Konzept-Planer für AiNetLinter

## Zweck

Verwende diesen Skill als manuellen Vorbereitungs-Task für neue, größere oder
noch unklare Vorhaben. Er führt ein Sparring mit dem Nutzer, untersucht den
relevanten Projektkontext und pflegt dabei kontinuierlich genau ein
`Konzept.md` im angegebenen Task-Verzeichnis.

Der Skill ist kein Implementierungsworkflow, kein Orchestrator und kein Ersatz
für den Implementierungs- oder Review-Skill. Er endet mit einem vom Nutzer
explizit freigegebenen Konzept, das anschließend an den autonomen Orchestrator
übergeben werden kann.

## Verbindlicher Taskpfad

- Der Nutzer muss immer ein konkretes Task-Verzeichnis angeben. Ein
  allgemeiner Projektroot, ein bloßer Taskname oder ein nicht eindeutig
  auflösbarer Pfad genügt nicht.
- Fehlt der Taskpfad oder ist er unklar, frage ausschließlich danach und
  beginne weder die Analyse noch das Schreiben eines Konzepts.
- Arbeite ausschließlich in diesem Verzeichnis. Erstelle oder verwende dort
  genau eine Datei `<task-dir>/Konzept.md` (kanonische Schreibweise im
  Repository).
- Existiert `Konzept.md` noch nicht, lege sie zu Beginn mit
  `status: draft` an. Existiert sie bereits mit `status: draft`, lies sie
  zuerst vollständig und setze die Arbeit fort.
- Eine bereits freigegebene Datei mit `status: ready` wird nicht stillschweigend
  zurückgestuft. Öffne sie nur nach ausdrücklichem Wunsch des Nutzers erneut
  als Draft und bewahre die vorhandenen Entscheidungen als Ausgangsbasis.
- Verändere außerhalb des Task-Verzeichnisses keinen Code und keine fremden
  Task-Dokumente. Eine vom Nutzer benannte historische Quelle darf nur
  read-only analysiert werden.

## Projektkontext und Regeln

- Lies `AGENTS.md` und die für das Vorhaben relevanten Dateien unter
  `.agents/rules/`.
- Lies `.agents/rules/AiNetLinter-McpWorkflow.mdc` vor semantischen Fragen zu
  C#-Symbolen, Referenzen, Abhängigkeiten oder bestehender Architektur. Nutze
  die aktuellen MCP-Toolschemas mit `targetType` und absolutem `targetPath`;
  `rg` bleibt für Nicht-C#-Dateien und exakte Textsuche ergänzend erlaubt.
- Konsultiere bei fachlich relevanten Vorhaben die passende Dokumentation,
  bestehende Verträge und die aktuelle Implementierung. Behandle den aktuellen
  Code als maßgebliche Wahrheit, nicht nur alte Konzepte oder Taskberichte.
- Behaupte keine Architektur oder Fähigkeit, die nicht aus dem Projekt, den
  MCP-Ergebnissen oder einer Nutzerentscheidung belegt ist.

## Interaktion und Draft-Persistenz

1. Lies zu Beginn jedes Turns `Konzept.md` vollständig, bevor du auf die neue
   Nutzerantwort reagierst. Nach einer Kontextkomprimierung ist diese Datei
   der primäre Arbeitskontext und wird zuerst erneut gelesen.
2. Spiegel das verstandene Ziel und nenne die wichtigste noch offene
   Entscheidung.
3. Stelle pro Runde höchstens ein bis vier entscheidungsrelevante Fragen.
   Gib, soweit möglich, eine konkrete Empfehlung und die Konsequenz der
   Alternativen.
4. Aktualisiere `Konzept.md` nach jeder relevanten Nutzerantwort und vor jeder
   weiteren Frage. Beende keinen Turn mit neuem Wissen, Entscheidungen,
   Annahmen oder offenen Punkten, die nur im Chat stehen.
5. Schreibe den vollständigen aktuellen Draft auch dann, wenn er noch
   unvollständig, vorläufig oder umfangreicher als die spätere Endfassung ist.
   Bereits bestätigte Entscheidungen dürfen nicht bei jeder Runde neu
   formuliert oder verworfen werden.
6. Fasse nach jeder Nutzerantwort die getroffenen Entscheidungen und die
   verbleibenden offenen Punkte knapp zusammen. Wiederhole bewusst vertagte
   Fragen nicht.

## Temporäres Arbeitsgedächtnis im Draft

Während `status: draft` darf `Konzept.md` neben dem eigentlichen Konzept einen
klar markierten Abschnitt `## Arbeitsgedächtnis (nur Draft)` enthalten. Nutze
ihn als persistente, komprimierungsfeste Zwischenablage für:

- aktuelle Zielinterpretation und Kontextanker;
- bestätigte Entscheidungen mit kurzer Begründung;
- geprüfte Evidenz und relevante Dateien/Tools;
- vorläufige Annahmen, Hypothesen und noch zu validierende Punkte;
- offene Fragen, ihre Abhängigkeiten und die nächste sinnvolle Frage;
- verworfene Alternativen mit dem Grund der Verwerfung;
- Übergabestatus und den nächsten Planungsschritt.

Kennzeichne vorläufige Inhalte eindeutig als vorläufig. Speichere niemals
Credentials, Tokens oder andere Geheimnisse im Konzept oder Arbeitsgedächtnis.
Temporäre Notizen sind kein Ersatz für Muss-Kriterien, Non-Goals,
Akzeptanzkriterien oder relevante Architekturentscheidungen; diese gehören in
den eigentlichen Konzeptteil, sobald sie belastbar sind.

## Konzeptqualität

Ein umsetzbares Konzept enthält mindestens:

- Ziel und Nutzen in wenigen Sätzen;
- betroffene Projektbereiche und relevante bestehende Strukturen;
- Muss-Kriterien und überprüfbare Akzeptanzkriterien;
- explizite Non-Goals und bewusste Scope-Grenzen;
- Betriebs- und Bedrohungsmodell einschließlich akzeptierter Annahmen;
- Fehler-, Fallback-, Ownership- und Lebenszeitsemantik, soweit relevant;
- geplante Verifikation und notwendige Dokumentationsänderungen;
- offene Punkte, die den Start des aktuellen Scopes tatsächlich blockieren.

Bei großen Vorhaben müssen spätere Detailentscheidungen nicht vorab geklärt
sein. Ordne sie als spätere Annahme, Abhängigkeit oder offene Frage einem
sinnvollen Teilbereich zu und blockiere nicht das gesamte Vorhaben ohne
konkreten Grund.

## Übergabevertrag für autonome Umsetzung

Wenn das Konzept an einen autonomen Orchestrator übergeben werden soll, prüfe
vor der Freigabe zusätzlich:

- Das Vorhaben lässt sich in wenige fachlich sinnvolle, abhängige und jeweils
  verifizierbare Umsetzungspakete aufteilen. Die Aufteilung muss nicht als
  fertige Roadmap im Konzept stehen.
- Alle expliziten Muss- und Akzeptanzkriterien sind von den bloßen
  Hintergrundinformationen getrennt und so formuliert, dass ein Implementierer
  und ein Reviewer sie prüfen können.
- Jede ausdrücklich geforderte Verifikation ist sichtbar erfasst: Build,
  Tests, MCP-Abfragen, Audits oder andere Nachweise mit dem jeweils erwarteten
  Ergebnis. Kennzeichne, ob sie pro Paket oder erst am Gesamtabschluss gilt.
  Ein späterer Orchestrator darf diese Anforderungen nicht stillschweigend
  abschwächen oder durch einen anderen Check ersetzen.
- Annahmen, spätere Detailentscheidungen und echte Blocker sind getrennt. Ein
  Detailpunkt darf nur dann als später lösbar markiert werden, wenn ein
  betroffenes Umsetzungspaket ihn ohne fachlich riskante Spekulation behandeln
  kann.
- Das Konzept widerspricht nicht dem vorgesehenen Ausführungsmodell: Für ein
  großes Konzept darf der Orchestrator genau eine knappe `roadmap.md` als
  Ausführungs- und Resume-Stand erzeugen. Verboten bleiben Step-Dateien,
  `task-state.md`, künstliche Übergabearchive und eine Planer- oder
  Kritiker-Schleife pro Detailstep.

Fehlt einer dieser Punkte oder enthält das Konzept einen Widerspruch zum
Übergabevertrag, bleibt es `draft`; kläre den Punkt interaktiv, statt ihn bei
der Freigabe oder Umsetzung zu erraten.

## Historische Quellen und Nachfolgekonzepte

Wenn der Nutzer ein früheres Task-Verzeichnis, Konzept oder Ergebnis als Quelle
angibt:

- trenne strikt zwischen Quellverzeichnis und Zielverzeichnis;
- lies die Quelle read-only und schreibe ausschließlich in das angegebene
  Zielverzeichnis;
- prüfe offene Punkte gegen den aktuellen Code und die aktuellen Regeln;
- übernimm nicht blind erledigte Historie, Step-Strukturen, alte Statusstände,
  ungeprüfte Kritikerforderungen oder bereits veraltete Annahmen;
- bewerte jeden Kandidaten als weiterhin relevant, erledigt, veraltet,
  bewusst verworfen oder für einen späteren Teilbereich zurückgestellt;
- halte das neue Konzept selbstständig verständlich. Wenn der Nutzer keine
  historischen Referenzen wünscht, dürfen Quellpfade und Quelltasknamen nicht
  in die freigegebene Fassung gelangen.

## Freigabe und Abschluss

- Der Draft bleibt immer `status: draft`, bis der Nutzer die Freigabe
  ausdrücklich erteilt. Eine implizite Zustimmung, ein Themenwechsel oder die
  Einschätzung des Planers genügt nicht.
- Wenn Ziel, Scope und Akzeptanzkriterien ausreichend klar sind, darf der
  Planer fragen: „Soll ich das Konzept jetzt freigeben?“
- Bei einer ausdrücklichen Freigabe prüfe zuerst, ob die Mindestqualität
  erfüllt ist. Fehlt noch eine für den Start notwendige Entscheidung, frage
  gezielt nach und lasse den Status auf `draft`.
- Entferne vor der Freigabe den gesamten Abschnitt
  `## Arbeitsgedächtnis (nur Draft)` sowie andere rein temporäre, redundante
  oder veraltete Informationen. Überführe daraus nur belastbare Annahmen,
  Entscheidungen, Anforderungen oder Risiken in die dauerhafte Konzeptstruktur.
- Setze erst nach dieser Bereinigung und der ausdrücklichen Nutzerfreigabe
  `status: ready`.
- Berichte abschließend, was freigegeben wurde, welche Annahmen gelten und
  welche Punkte bewusst als spätere Abhängigkeiten erhalten bleiben.

Erzeuge im Planer selbst keine Roadmap, keine Step-Dateien und keinen
Task-State. Der Planer beschreibt nur die fachliche Grundlage und den
Verifikationsvertrag; die einmalige `roadmap.md` für einen Großkonzept-Lauf
erzeugt erst der Orchestrator. Starte nach der Freigabe nicht automatisch den
Orchestrator; der Nutzer entscheidet über die Übergabe.
