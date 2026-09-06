---
status: draft
title: C#-Kontext-Exploration als One-Shot-Arbeitskontext
created: 2026-09-06
updated: 2026-09-06
---

# C#-Kontext-Exploration als One-Shot-Arbeitskontext

## 1. Ziel

AiNetLinter soll einem Coding-Agenten für ein bekanntes C#-Symbol oder eine konkrete Änderung mit möglichst einem MCP-Aufruf einen brauchbaren Arbeitskontext liefern.

„Alles Relevante“ bedeutet dabei nicht den vollständigen Projektgraphen. Es bedeutet:

> Für ein klar definiertes Ziel, ein definiertes Antwortprofil und ein hartes Budget werden die relevanten Informationen priorisiert geliefert. Auslassungen, Unsicherheit und weitere sinnvolle Vertiefungen werden sichtbar gemacht.

Folgeaufrufe bleiben für Spezialfälle erlaubt. Das Feature soll unnötige Folgeaufrufe bei normalen Understand-, Prepare-Edit- und Refactoring-Aufgaben vermeiden.

## 2. Abgrenzung

Dieses Konzept ist keine Planung eines neuen allgemeinen Explore-Tools. AiNetLinter besitzt bereits get_feature_context als residenten One-Shot-Call. Das Konzept beschreibt:

- den belegten Ist-Zustand;
- die tatsächlichen Lücken;
- den Nutzen möglicher Ergänzungen;
- einen begrenzten Zielvertrag für die Weiterentwicklung.

Roslyn bleibt die einzige semantische Wahrheitsschicht. Es wird kein konkurrierender Parser, Index oder Symbolgraph eingeführt.

## 3. Bestehender Ist-Zustand

get_feature_context löst ein Ziel-Symbol über den bestehenden Roslyn-Resolver auf und aggregiert aktuell:

1. Symbol und Deklaration;
2. Methoden-/Typmetriken einschließlich Regelgrenzwerten;
3. direkte Caller;
4. statische Test-Zuordnung;
5. dateibezogene Linter-Violations.

Bereits vorhanden sind außerdem:

- primärer Parameter symbolIdentifier mit kompatiblen Aliasen;
- standardmäßig aktivierte fünf Bereiche;
- maxCallers und maxTests mit Default 10 und serverseitigem Cap 50;
- Gesamtzahlen und bereichsweise Truncation-Indikatoren;
- strukturierter Payload zusätzlich zum Markdown-Bericht;
- Loading-, Solution-not-loaded-, Symbol-not-found- und Recoverable-Fehlerpfade;
- get_impact als separater Change-/Impact-Kontext;
- get_symbol_body für gezielten Quelltext;
- find_references und get_call_tree für gezielte Beziehungsanalyse.

Diese Fähigkeiten sind Baseline. Sie dürfen nicht als neue Funktion doppelt geplant werden.

## 4. Gap-Analyse

| Bereich | Ist-Zustand | Tatsächliche Lücke | Nutzen | Bewertung |
|---|---|---|---|---|
| Deklaration | Roslyn-Auflösung, Art, Zugriff, Position, Parameter, DocCommentId | kein gemeinsamer Snapshot-/Vollständigkeitsrahmen | hoch | beibehalten |
| Metriken | Methoden-/Typmetriken und Grenzwertchecks | kein Gesamtbudget über alle Bereiche | hoch | Budgetvertrag ergänzen |
| Caller | direkte Call-Sites, Gesamtzahl, Limit, Truncation | keine Callees; Auswahl und Gesamtreihenfolge nicht als Vertrag definiert | Caller hoch, Callees mittel/hoch | Caller behalten, Callees optional |
| Tests | statische Zuordnung mit Dateien, Methoden, Kategorie und MatchReason | Zuordnung ist kein Laufzeitabdeckungsbeweis | hoch für Planung | behalten, korrekt benennen |
| Violations | vollständiger Linterlauf, danach dateibezogene Filterung, Limit 50 | potenziell teuer; Teilfehler können aktuell wie leerer Bericht aussehen | hoch | Fehler- und Kostensemantik härten |
| Quellcode | nicht enthalten; get_symbol_body separat | Standardverständnis braucht oft zweiten Aufruf | hoch bei kleinen Ausschnitten | optionales Source-Profil |
| Callees | nicht enthalten | unmittelbare Wirkung des Symbols fehlt | mittel/hoch, aber schnell wachsend | begrenztes Relations-Profil |
| Freshness | Server kennt Loading/Staleness/Refresh | Feature-Payload weist Snapshot nicht aus | hoch für korrekte Entscheidungen | gemeinsamer Envelope |
| Vollständigkeit | IsTruncated je Bereich | kein Gesamtstatus und kein Gesamtbudget | sehr hoch | gemeinsamer Envelope |
| Mehrere Symbole | ein Ziel-Symbol pro Aufruf | kein Multi-Symbol-Kontext | mittel, aber unklare Relevanz | vorerst nicht |
| Change-Kontext | get_impact vorhanden | keine sinnvolle Ergänzung in diesem Tool | hoch, aber Doppelung vermeiden | getrennt lassen |
| Freie Sprache | primär symbolIdentifier | unscharfe Anfrage kann falsche Relevanz erzeugen | niedrig für Version 1 | nicht als Einstieg |

## 5. Produktentscheidung: Was heißt „alles Relevante“?

Relevanz ist nicht absolut. Sie hängt von Ziel, Aufgabe, Tiefe, Beweissicherheit und Budget ab.

Der One-Shot-Call soll deshalb folgende Aussage garantieren:

- Das Ziel-Symbol wurde eindeutig aufgelöst oder die Auflösung ist fehlgeschlagen.
- Der Core-Kontext wurde nach definierten Regeln gesammelt.
- Die enthaltenen Ergebnisse sind innerhalb des gewählten Profils priorisiert.
- Nicht angeforderte, nicht verfügbare und wegen des Budgets ausgelassene Daten sind unterscheidbar.
- Die Antwort macht keine Vollständigkeitsbehauptung über den gesamten Projektzusammenhang.

„Ein Call reicht“ ist für Standardaufgaben ein Qualitätsziel, keine Pflicht, jeden Spezialfall in eine einzige riesige Antwort zu pressen.

## 6. Zielprofile

### Core

Der Default und die Weiterentwicklung des heutigen get_feature_context:

- eindeutige Symbolidentität und Deklaration;
- Metriken und Grenzwertinformationen;
- direkte Caller;
- statische Test-Zuordnung;
- dateibezogene Violations;
- Snapshot-/Freshness-Status;
- Gesamtbudget und Vollständigkeit.

### Relations

Optional zuschaltbar:

- direkte Callees;
- begrenzte Beziehungstiefe;
- nur priorisierte und budgetierte Ziele;
- Herkunft und Vertrauensgrad jeder nicht rein semantischen Beziehung.

Relations darf keine vollständige Caller-/Callee-Traversierung anbieten.

### Source

Optional zuschaltbar:

- Quellcode des Zielsymbols;
- höchstens wenige ausgewählte, bereits priorisierte Nachbarsymbole;
- harte Begrenzung pro Ausschnitt und für die Gesamtantwort;
- stabile Datei-/Zeilenangaben.

Komplette Dateien und alle Bodies der Nachbarschaft bleiben außerhalb des Profils.

### Change

Der bestehende get_impact-Change-/Impact-Kontext bleibt fachlich eigenständig. Eine spätere Integration darf nur gemeinsame Envelope- und Budgetmodelle nutzen, nicht dieselben Daten doppelt ausgeben.

## 7. Antwort- und Budgetvertrag

Der öffentliche Vertrag soll konzeptionell folgende Bereiche besitzen:

    snapshot
      analysisGeneration
      freshness
      lastSuccessfulRefresh
    request
      profile
      effectiveLimits
    subject
      symbolId
      displayName
      declaration
    context
      metrics
      callers
      callees
      tests
      violations
      source
    completeness
      isComplete
      returnedCounts
      totalCounts
      omitted
      reasons

Der tatsächliche Vertrag muss bestehende DTOs und MCP-Konventionen wiederverwenden.

Regeln:

- Das Gesamtbudget gilt für StructuredContent und Textdarstellung zusammen.
- Sicherheits- und Transportlimits sind nicht durch Nutzerparameter überschreibbar.
- Die Priorität lautet standardmäßig: Symbolidentität, Deklaration, sichere Beziehungen, Tests/Violations, inferierte Beziehungen, Quellcode, entfernte Nachbarschaft.
- Gleicher Snapshot, gleiche Anfrage und gleiche Optionen liefern deterministische Auswahl und Reihenfolge.
- Truncation nennt mindestens Bereich, Gesamtzahl, ausgegebene Anzahl und Grund.
- Ein Fortsetzungstoken ist nur sinnvoll, wenn die zweite Seite snapshot- und anfragegebunden reproduzierbar ist.
- Versteckte Folgeabfragen durch den Server sind nicht Teil dieses Konzepts.

## 8. Korrektheits- und Kostenrisiken

### Linterlauf

Der aktuelle Violations-Teil führt pro Feature-Context einen vollständigen Linterlauf ohne Cache aus und filtert anschließend auf die Zieldatei. Vor einer Erweiterung muss geklärt werden, ob:

- ein vorhandener, snapshotkonsistenter Diagnosezustand wiederverwendet werden kann;
- der Lauf gezielt begrenzt werden kann;
- die Kosten gemessen und als Status ausgewiesen werden müssen.

Eine One-Shot-MCP-Antwort ist nur dann sinnvoll, wenn sie nicht durch einen unverhältnismäßig teuren Unterlauf erkauft wird.

### Teilfehler

Ein fehlgeschlagener Violations-Scan darf nicht wie „keine Violations“ aussehen. Erforderlich ist eine Unterscheidung zwischen:

- erfolgreich geprüft und leer;
- erfolgreich geprüft und begrenzt;
- nicht geprüft;
- Prüfung fehlgeschlagen;
- Daten aus veraltetem Snapshot.

### Testzuordnung

Die bestehende Testzuordnung ist statisch. Sie ist ein nützlicher Hinweis für Testplanung, aber kein Beweis für tatsächliche Abdeckung oder ausreichende Regression. Diese Semantik muss im Namen und in der Beschreibung erhalten bleiben.

### Callees und Source

Callees und Source haben hohen lokalen Nutzen, können aber bei zentralen Symbolen stark anwachsen. Beide Bestandteile gehören daher nicht ungeprüft in den Default. Sie benötigen ein eigenes Profil oder klar begrenzte Optionen.

## 9. Nicht-Ziele

- kein neues allgemeines Explore-Tool;
- keine vollständigen Caller-/Callee-Graphen;
- keine Einbettung aller Bodies oder kompletter Nachbarschaftsdateien;
- keine unbeschränkte Projektzusammenfassung;
- kein Multi-Symbol-Kontext ohne klaren Auftrag;
- keine primäre freie Sprachsuche;
- keine Laufzeitabdeckung aus statischer Testzuordnung;
- keine sicheren Beziehungen aus Reflection, dynamischer Bindung oder schwacher Namensähnlichkeit;
- keine versteckten Folgeaufrufe;
- keine Änderung an Quellcode oder Projektdateien durch den Read-only-Kontext.

## 10. Muss-Kriterien

- Der bestehende Core-Nutzen bleibt rückwärtskompatibel.
- Das Feature besitzt ein hartes Gesamtbudget für die gesamte Antwort.
- Snapshot/Freshness und Gesamtvollständigkeit sind maschinenlesbar.
- Leere, nicht angeforderte, ausgelassene und fehlgeschlagene Bereiche sind unterscheidbar.
- Sichere Roslyn-Beziehungen und inferierte Beziehungen sind getrennt gekennzeichnet.
- Source und Relations sind optional und begrenzt.
- Fehler im Teilbereich werden nicht als fachlich leeres Ergebnis ausgegeben.
- Kein neues paralleles Symbolmodell oder Index wird eingeführt.

## 11. Akzeptanzkriterien

- Für ein bekanntes C#-Symbol ist der Core-Kontext in einem MCP-Aufruf arbeitsfähig.
- Eine Standardantwort überschreitet das definierte Gesamtbudget nicht.
- Bei vielen Callern, Tests oder Violations werden Gesamtzahlen und Auslassungsgründe geliefert.
- Ein Agent kann anhand der Antwort entscheiden, ob ein Folgeaufruf fachlich notwendig ist.
- Ein optionales Relations- oder Source-Profil kann die Antwort vergrößern, bleibt aber innerhalb desselben Gesamtbudgets.
- Ein fehlgeschlagener Unterbereich ist von einem leeren Unterbereich unterscheidbar.
- Gleicher Snapshot und gleiche Anfrage liefern dieselbe Auswahlreihenfolge.
- Bestehende Aufrufer des Tools und bestehende Einzeltools bleiben kompatibel.

## 12. Verifikation

### FastTests

- DTO-/Envelope-Zustände;
- Budgetberechnung und Priorisierung;
- deterministische Tie-Breaks;
- leer, nicht angefordert, ausgelassen, fehlgeschlagen;
- Profiloptionen;
- Provenance-Erhalt bei Truncation.

### ComponentTests

- Overloads, Partial Types, Accessors und generische Symbole;
- direkte Caller und optionale Callees;
- statische Testzuordnung mit korrekter Unsicherheitssemantik;
- Violations auf Symbol und Datei;
- Quellcodegrenzen bei kleinen und großen Bodies.

### IntegrationTests

- MCP-JSON-Vertrag für Core, Relations und Source;
- Dateiänderung zwischen zwei Aufrufen;
- stale/degraded Snapshot;
- Teilfehler beim Linter-/Diagnosepfad;
- große Solutions und Gesamtbudget;
- konkurrierende Sessions und Snapshotkonsistenz.

### Abschluss

    dotnet build
    dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
    dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress

Zusätzlich sind die relevanten MCP-Abfragen gegen das eigene Repository und der vorgeschriebene Audit auszuführen.

## 13. Belegte Implementierungsanker

- [GetFeatureContextTool.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/GetFeatureContextTool.cs>): One-Shot-Ausführung und Fehlerpfade.
- [FeatureContextScanner.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextScanner.cs>): Aggregation der fünf Bereiche, Limits und Violations-Filterung.
- [FeatureContextModels.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextModels.cs>): aktueller Options- und Payload-Vertrag.
- [FeatureContextFormatter.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextFormatter.cs>): Markdown-Ausgabe und bereichsweise Truncation-Hinweise.
- [AnalysisToolRegistrations.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Registration/AnalysisToolRegistrations.cs>): öffentliches Schema, Defaults und Caps.
- [ChangeContextResponseModels.cs](<C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/SymbolGraph/ChangeContextResponseModels.cs>): bestehender Change-/Impact-Kontext.

## 14. Offene Entscheidungen

1. Soll Source zuerst in get_feature_context integriert werden oder zunächst nur als klarer Folgeaufruf über get_symbol_body bestehen bleiben? Empfehlung: zuerst Gesamtbudget/Freshness/Fehlersemantik, danach Source-Profil.
2. Soll Relations Callees direkt im bestehenden Tool anbieten oder zunächst über get_call_tree als Folgeaufruf bleiben? Empfehlung: erst nach Messung typischer Antwortgrößen entscheiden.
3. Welches effektive Gesamtbudget ist für Markdown und StructuredContent angemessen? Der Wert muss anhand realer MCP-Transport- und Agentenlimits festgelegt werden.
4. Soll das Profil explizit vom Agenten gesetzt werden oder soll der Server aus wenigen Optionen ein Profil ableiten? Empfehlung: explizite, kleine Optionen mit sicherem Core-Default.
5. Wie wird ein teurer oder fehlgeschlagener Violations-Teil transparent gemacht, ohne den gesamten Core-Kontext unbrauchbar zu machen?

## Arbeitsgedächtnis (nur Draft)

- Nutzerziel: Der Agent soll für Standardaufgaben mit einem Call den relevanten Kontext erhalten; 500-kB-Antworten sind nicht akzeptabel.
- Bestätigte Beobachtung: get_feature_context ist bereits ein echter One-Shot-Call mit fünf Kernbereichen.
- Neue Abgrenzung: Teilkonzept 01 ist eine Gap-Analyse und Vertrags-/Qualitätsschärfung, kein neues allgemeines Context-Tool.
- Wichtigste vorläufige Priorität: Gesamtbudget, Vollständigkeit, Freshness und Teilfehlersemantik vor Callees oder Source.
- Bestehende Features sollen nur dann erweitert werden, wenn der zusätzliche Agentennutzen die zusätzlichen Tokens, Laufzeit und Komplexität rechtfertigt.
- Status bleibt draft, bis die offenen Entscheidungen und der genaue erste Umsetzungsscope ausdrücklich bestätigt sind.

