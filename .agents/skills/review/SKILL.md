---
name: review
description: Prüfe einen aktuellen AiNetLinter-Diff unabhängig auf Produktionsrisiken, Regelverstöße und Muss-Kriterien, ohne automatisch Scope oder Review-Schleifen zu vergrößern.
---

# Review in AiNetLinter

## Zweck

Dieser Skill ist für einen unabhängigen Review eines aktuellen Code-Diffs
gedacht. Er bewertet, ob die Änderung im unterstützten AiNetLinter-
Betriebsmodell produktionssicher ist. Er ist kein allgemeiner Architektur-
Rundumsweep und erzeugt keine automatische Korrekturschleife.

## Kontext und Regeln

- Lies `AGENTS.md` sowie die relevanten Dateien unter `.agents/rules/`.
- Lies `.agents/rules/AiNetLinter-McpWorkflow.mdc` vor semantischen C#-
  Abfragen. Verwende für Symbole, Referenzen, Impact und Violations zuerst
  das passende AiNetLinter-MCP-Tool mit aktuellem Schema,
  `targetType` und absolutem `targetPath`; nutze `rg` nur ergänzend für
  konkrete Text- und Diff-Arbeit.
- Prüfe den tatsächlichen aktuellen Diff und den umgebenden Code. Werte einen
  übergebenen Verifikationsnachweis zuerst auf Vollständigkeit, Scope,
  Ergebnis und Frische aus; vertraue ihm nicht blind, aber wiederhole die
  Prüfung auch nicht automatisch.
- Prüfe nur die Nutzeranforderung, den vereinbarten Scope, relevante Rules,
  betroffene Aufrufer und die direkt nötigen Regressionen. Ein umfassender
  globaler Audit ist nur bei ausdrücklichem Auftrag oder echtem Abschluss-
  risiko sinnvoll.

## Standard-Betriebsmodell

Für AiNetLinter gilt standardmäßig: lokales Entwickler-Hilfstool mit MCP-
Stdio-Server, mögliche Parallelität innerhalb eines Daemons, kein
versprochenes gemeinsames Cache-Szenario mehrerer Daemons und kein
Bedrohungsmodell eines bösartigen lokalen Administrators.

Trotzdem müssen normale Eingabefehler, Cancellation, Abstürze, stale Cache,
Credential-Leaks, falsche Source-Herkunft und offensichtliche Ressourcen-
oder Prozess-Leaks korrekt behandelt werden. Fordere keine formale Garantie
gegen Angreifer- oder Mehrprozess-Szenarien, die der Task nicht unterstützt.

## Prüfreihenfolge

1. **Ziel und Scope:** Ist die geforderte Funktion vollständig und sind keine
   expliziten Non-Goals umgesetzt?
2. **Regeln und Architektur:** Gibt es einen konkreten Verstoß gegen eine
   relevante Projektregel oder eine bestehende Architekturgrenze?
3. **Logik:** Funktionieren Erfolgs-, Fehler-, Cancellation- und relevante
   Grenzfälle im unterstützten Betriebsmodell? Sind Tests aussagekräftig?
4. **Integration:** Sind Aufrufer, Toolvertrag, Origin-/Fehlerstatus,
   Ownership und Dokumentation konsistent, soweit sie durch den Diff berührt
   werden?

## Verifikationsnachweise

Wenn der Orchestrator einen Implementiererbericht mitgibt, prüfe den darin
enthaltenen Nachweis gegen den aktuellen Diff:

- Check/Tool, Scope/Target und Ergebnis müssen konkret benannt sein.
- Die Prüfung muss nach der letzten Codeänderung erfolgt sein. In einem
  sequenziellen Orchestrator-Lauf gilt ein Nachweis als frisch, wenn zwischen
  Hand-off und Review kein Produktions- oder Testcode geändert wurde.
- Ist der Nachweis vollständig, erfolgreich, passend und frisch, wiederhole
  denselben Test oder dieselbe MCP-Prüfung nicht lediglich zur Bestätigung.
- Führe eine Prüfung nur erneut aus, wenn der Nachweis fehlt, fehlgeschlagen
  oder unvollständig ist, der Scope nicht passt, danach Code geändert wurde
  oder eine konkrete fachliche Gegenhypothese besteht.
- Bei einer Wiederholung nenne im Ergebnis den konkreten Anlass, den exakten
  Scope und das Ergebnis. Eigene notwendige MCP-Abfragen zur unabhängigen
  Beurteilung von Symbolen, Referenzen oder Logik sind davon unberührt.

Nutze für jeden Befund konkrete Datei-/Zeilenangaben, reproduzierbare
Begründung und eine klare Korrekturempfehlung. Trenne echte Fehlfunktion von
nicht bewiesener zusätzlicher Absicherung.

## Priorisierung

- **P0:** Secret-Leak, Datenkorruption, unkontrollierter Prozess-/Handle-Leak,
  falsche Source-of-Truth oder ein Crash im normalen unterstützten Betrieb.
- **P1:** reproduzierbar falsches Verhalten, fehlendes Muss-Kriterium,
  konkreter Produktions-Rule-Verstoß oder eine relevante Regression.
- **P2:** theoretisches Risiko außerhalb des vereinbarten Modells,
  zusätzliche Evidenz, die kein Muss-Kriterium verlangt, oder begrenzte
  Robustheitslücke ohne beobachtbares Fehlverhalten.
- **P3:** Stil, kosmetisches DRY, Benennung und sonstige nicht-funktionale
  Verbesserung.

Nur P0/P1 sind blockierende Findings. Eine explizite Projektregel oder ein
explizites Muss-Kriterium wird nicht als P2 heruntergestuft; begründe die
Einstufung konkret.

Bei `issues` gruppiere Findings nach technischer Ursache statt nach einzelnen
Symptomen. Gib für jede Ursache eine kurze, stabile Ursachensignatur aus
betroffener Invariante, Bereich oder Symbol und Fehlerbild an. Verwende bei
einem Folge-Review eine bereits übergebene Signatur wieder, wenn es dieselbe
Ursache ist, und kennzeichne nachvollziehbar, wenn sie sich fachlich geändert
hat. Die Signatur dient der Zykluserkennung; sie ist kein künstlicher Task-
oder Step-Bezeichner.

Für relevante P2/P3-Funde und nicht blockierende Verbesserungsvorschläge gib
zusätzlich eine knappe Dispositionsempfehlung an: `fix-now`,
`accepted-deferred`, `rejected/not-applicable` oder
`promoted-to-project-debt`. Der Orchestrator entscheidet die endgültige
Disposition und persistiert den vollständigen Bericht; der Reviewer legt keine
Protokolldatei an.

## Ergebnis

Gib zuerst ein klares Urteil: `approved`, `issues` oder `blocked`.

- `approved`: kein P0/P1; P2/P3 werden höchstens knapp als Restrisiko
  erwähnt.
- `issues`: nur bei mindestens einem belegten P0/P1, mit gebündelten Findings
  nach fachlicher Ursache statt einem separaten Punkt pro Symptom.
- `blocked`: nur wenn eine Nutzerentscheidung, fehlende Infrastruktur oder
  ein nicht auflösbarer Widerspruch tatsächlich notwendig ist.

Ändere keinen Code, erstelle keinen Commit und lege keine Task-/Step-Dateien
an, sofern der Nutzer nicht ausdrücklich darum bittet. In einem orchestrierten
Workflow liefert der Reviewer ausschließlich sein Urteil; der Orchestrator
entscheidet über Korrektur und Commit. Schlage keinen vollständigen Umbau vor,
wenn ein begrenzter Fix genügt. Nach einer Korrektur prüfe die betroffene
Invariante erneut; starte nicht selbständig weitere Review-Runden.
