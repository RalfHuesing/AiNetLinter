---
name: implement
description: Implementiere oder ändere AiNetLinter-Code in einem zusammenhängenden Paket mit MCP-first-Semantik, pragmatischem Produktionsqualitätsmaßstab und gezielter Verifikation.
---

# Implementieren in AiNetLinter

## Zweck

Dieser Skill ist der Standard für Feature-Implementierungen, Fehlerbehebungen
und Refactorings in diesem Repository. Ein Agent übernimmt Verständnis,
Planung, Umsetzung und Selbstprüfung in einer zusammenhängenden Session.

Mehrstufige Orchestrierung, Step-Dateien und separate Planer-/Coder-Zyklen gibt
es nur, wenn der Nutzer sie ausdrücklich verlangt.

## Verbindliche Projektregeln

- Lies `AGENTS.md` und die für die Aufgabe relevanten Dateien unter
  `.agents/rules/`.
- Lies `.agents/rules/AiNetLinter-McpWorkflow.mdc` vor semantischen Fragen zu
  C# und halte dessen MCP-first-Regel ein. Verwende aktuelle Toolschemas,
  `targetType` und den absoluten `targetPath`; `rg` ergänzt die semantische
  Analyse, ersetzt sie aber nicht.
- Konsultiere bei Änderungen an CLI, Konfiguration, Regeln oder
  MCP-Verträgen die einschlägige Dokumentation und synchronisiere die in den
  Projektregeln genannten Dateien.
- Verändere keine externen Source-Repositories oder untersuchten Assemblies.
- Halte bestehende Architekturverbote, Testregeln, Warnungsfreiheit und
  Commit-Konventionen ein. Eine Aufgabe hebt diese Regeln nicht stillschweigend
  auf.

## Vorgehen

1. Verstehe Ziel, betroffene Bereiche, Muss-Kriterien und Non-Goals. Lies den
   relevanten Code und suche vorhandene Patterns, Helfer, Aufrufer und Tests.
2. Forme daraus ein zusammenhängendes, vertikal testbares Paket. Teile nur,
   wenn ein anderer Fachvertrag, ein unabhängiger Risikobereich oder ein
   echter Kontext-/Komplexitätsgrund das erfordert. Erzeuge keine künstlichen
   Mini-Pakete für einzelne Assertions, Dokumentationszeilen oder triviale
   Helper.
3. Implementiere direkt und fokussiert. Keine ungeplanten Generalisierungen,
   globalen Refactorings oder neuen Abstraktionen nur wegen einer zufälligen
   Ähnlichkeit.
4. Führe während der Arbeit gezielte Tests und Analysen aus. Bei einer
   eigenständigen Aufgabe müssen vor dem finalen Hand-off die in `AGENTS.md`
   vorgeschriebenen Build- und Nicht-Stress-Test-Gates ausgeführt werden,
   sofern der Nutzer nicht ausdrücklich nur eine Analyse oder einen Entwurf
   verlangt. Wird der Skill als Teil eines vom Orchestrator gesteuerten Epics
   aufgerufen, führst du die für dieses Epic nötige gezielte Verifikation aus;
   die vollständigen Abschluss-Gates koordiniert der Orchestrator einmal nach
   dem letzten Codezustand. Explizite konzeptspezifische Prüfungen dürfen in
   keinem Modus still entfallen. Schließe jede ausgeführte Prüfung mit einem
   konkreten Verifikationsnachweis ab.
5. Prüfe selbst gegen Ziel, Muss-Kriterien, Non-Goals, relevante Rules und die
   tatsächlichen unterstützten Betriebsannahmen. Berichte Abweichungen offen.

## Gezielter Qualitätscheck

Bei einer nicht-trivialen Codeänderung prüfe vor dem Hand-off den betroffenen
Bereich mit den verfügbaren AiNetLinter-MCP-Tools auf `find_duplicates`,
`find_dead_code` und `find_magic_values`. Lies dafür zuerst
`.agents/rules/AiNetLinter-McpWorkflow.mdc` und verwende die aktuellen
Toolschemas, `targetType=project` und den absoluten `targetPath`.

Behebe sichere, hochkonfidente und scope-nahe Befunde proaktiv, wenn die
Korrektur verhaltensneutral bleibt und keine Architekturentscheidung erfordert:
beispielsweise einen exakten Helper-Klon, eindeutig unreferenzierten privaten
Code oder einen wiederholten fachlichen Wert mit klarer gemeinsamer Identität.
Prüfe vor dem Entfernen von Code Referenzen und mögliche indirekte Nutzung über
Serialisierung, Reflection oder Tests.

Kein breiter Cleanup-Sweep wegen einer einzelnen Ähnlichkeit. Unklare,
architekturabhängige oder außerhalb des geänderten Bereichs liegende Befunde
werden für den separaten Abschluss-Audit bzw. den Nutzerbericht notiert.

## Standard-Betriebsmodell

AiNetLinter ist standardmäßig ein lokales Entwickler-Hilfstool mit einem
MCP-Stdio-Server auf dem Entwicklerrechner:

- Normale MCP-Aufrufe innerhalb eines Daemons können parallel stattfinden;
  In-Process-Zustand muss deshalb korrekt und bounded sein.
- Mehrere Daemon-Prozesse mit gemeinsamem Cache sind kein versprochenes
  Szenario, solange die Aufgabe das nicht ausdrücklich fordert.
- Ein bösartiger lokaler Administrator ist nicht Teil des Standard-
  Bedrohungsmodells. Beschädigung durch Absturz, Cancellation, stale Cache und
  fehlerhafte Eingaben bleibt trotzdem zu behandeln.
- Credentials dürfen niemals in Diagnosen, Logs oder Toolantworten landen.
- Bei unsicherer Source-Zuordnung ist eine sichtbare Decompilation oder ein
  kontrollierter Fehler besser als eine unbelegte Behauptung von Originalquelle.

Erfinde keine stärkeren Garantien als dieses Modell oder die Aufgabe verlangt.
Wenn eine Anforderung ein anderes Betriebs- oder Bedrohungsmodell voraussetzt,
benenne das vor der Umsetzung.

## Qualitätsmaßstab und Stoppregel

„Produktionssicher“ bedeutet hier: kein bekanntes Fehlverhalten im
unterstützten Normalbetrieb, keine stillen Fehlklassifikationen an den
fachlichen Grenzen, keine Credential-Leaks, keine offensichtlichen
Ressourcen-/Prozess-Leaks und ausreichende Regressionstests für die geänderte
Logik.

- P0/P1-Probleme müssen vor dem Abschluss behoben werden: Datenkorruption,
  Secret-Leak, Crash im Normalfall, Prozess-/Handle-Leak, falsche
  Source-of-Truth oder reproduzierbar falsches Muss-Verhalten.
- P2/P3-Punkte wie theoretische Risiken außerhalb des Betriebsmodells,
  zusätzliche Evidenz ohne geforderten Vertrag, Stil und kosmetisches DRY
  werden berichtet, blockieren aber keinen normalen Abschluss.
- Nach einer gezielten Korrekturrunde nicht zusammenhängender neuer Findings
  nicht automatisch in eine Endlosschleife wechseln. Verbleibende Risiken mit
  Evidenz und Empfehlung an den Nutzer melden.

## Hand-off

### Verifikationsnachweis

Vor dem Hand-off führe die für den aktuellen Scope erforderlichen Tests und
MCP-Prüfungen aus. Liste jede tatsächlich ausgeführte Prüfung einzeln mit
folgendem Nachweis auf:

- Check oder Tool und vollständige relevante Parameter beziehungsweise Filter;
- Scope/Target;
- Ergebnis und wesentliche Fehlermeldung oder Befundzusammenfassung;
- ausdrücklicher Hinweis, dass die Prüfung nach der letzten Codeänderung
  ausgeführt wurde.

Behaupte keine Prüfung als erfolgreich, die nicht ausgeführt wurde. Kennzeichne
fehlende, übersprungene, fehlgeschlagene oder wegen fehlender Capability nicht
ausführbare Prüfungen ausdrücklich und nenne die Konsequenz. Ein fehlgeschlagener
Check oder ein offener P0/P1-Befund verhindert den Hand-off nicht: Übergib den
vollständigen aktuellen Arbeitsstand mit seinem ehrlichen Nachweis, damit der
Orchestrator ihn als Zwischenstand sichern und gezielt reviewen lassen kann.

Ein weiterer Agent erhält keinen künstlichen Step-Archivbestand. Übergib kurz:
geänderte Dateien und Symbole, getroffene Entscheidungen, ausgeführte
Prüfungen, offene Risiken und den nächsten sinnvollen Einstiegspunkt. Der
weitere Agent prüft den tatsächlichen Code und Diff selbst. In einem
orchestrierten Epic gehören die finalen solutionweiten Gates nicht in den
einzelnen Implementierer-Hand-off, sondern in den Abschluss des
Orchestrators.

Wenn dieser Skill als Implementierer-Rolle vom Orchestrator aufgerufen wird,
erstelle oder ändere keinen Git-Commit. Liefere den vollständigen Epic-Stand im
Working Tree; der Orchestrator sichert ihn unmittelbar nach deinem terminalen
Hand-off in einem eigenen Implementierungs-Checkpoint, auch ohne vorheriges
`approved` des unabhängigen Reviews. Dieser Commit dokumentiert bewusst einen
unreviewten Zwischenstand und darf offene Findings, rote Checks oder Risiken
enthalten. Nach Review und eventuellen Korrekturen erstellt der Orchestrator
weitere Status-/Abschluss-Checkpoints. Bei direkter Nutzung ohne Orchestrator
gilt eine Commit-Anweisung des Nutzers; ohne eine solche Anweisung entscheidet
der aufrufende Workflow.

## Abschlussmeldung

Berichte knapp: Ergebnis, wichtige Designentscheidungen, geänderte Bereiche,
Build-/Testbefunde, bekannte Restrisiken, offene Tech Debt mit einer passenden
Disposition (`accepted-deferred`, `rejected/not-applicable` oder
`promoted-to-project-debt`) und ob etwas bewusst nicht umgesetzt wurde. Der
Orchestrator persistiert den Bericht in `execution-log.md`; schreibe selbst
keine `roadmap.md`, `execution-log.md`, `task-state.md` oder Step-Dateien,
sofern der Nutzer das nicht ausdrücklich verlangt.
