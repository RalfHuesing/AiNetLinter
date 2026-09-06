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

### Agenten-Schnittstelle und Defaults

Der normale Aufruf soll weiterhin nur ein Ziel-Symbol sowie `targetType` und
`targetPath` benötigen. Ein Agent soll keine Antwortqualität verlieren, weil
er optionale Detailparameter vergessen hat.

- Der kompakte Core ist der sichere Ziel-Default; bis zum bestandenen
  Kompatibilitäts-Gate bleibt der heutige Default als Fallback erhalten.
- Gesamtbudget, kompakte Statusdarstellung, deterministische Auswahl und
  Fehler-/Vollständigkeitsstatus werden serverseitig automatisch angewandt.
- Die bestehenden Bereichsflags und Limits bleiben aus Kompatibilitätsgründen
  erhalten; neue unabhängige Schalter für jede denkbare Datenquelle werden
  nicht empfohlen.
- Falls Source oder Relations später als One-Shot-Erweiterung bestätigt werden,
  soll dafür höchstens ein kleines Profil (`core` als Default, danach gezielte
  optionale Profile) statt einer langen Liste boolescher Parameter eingeführt
  werden. Beide Erweiterungen sind aber erst nach einer Messung der
  verdichteten Core-Antwort Kandidaten, keine Zusage.
- Ein frei wählbares Antwortbudget ist für den ersten Scope nicht erforderlich.
  Der sichere Server-Default gilt automatisch; spätere fortgeschrittene
  Überschreibungen müssten gekappt und klar als Spezialfall dokumentiert sein.

Die Textdarstellung ist eine kurze Agenten-Zusammenfassung, nicht der Ort für
lange Erklärungen. Erfolgreich leere Bereiche werden mit einer knappen
Statuszeile dargestellt, z. B. `Violations: 0`. Bei einem Fehler genügt im Text
`Violations: nicht verfügbar (Scan fehlgeschlagen)`; Fehlercode, Status,
Auslassungsgrund und technische Details gehören in `StructuredContent`.

### Explizite Bewertung der möglichen Erweiterungen

Die folgenden Entscheidungen gelten für den ersten fachlichen Scope. Die
Messwerte stammen aus der aktuellen Codebasis und sind nur Evidenz für Kosten-
und Auswahlmechanismen. Sie sind kein Defaultprofil für beliebige
AiNetLinter-Nutzerprojekte.

| Kandidat | Bereits vorhanden oder Lücke | Agentennutzen | Tokenbedarf | Laufzeit-/Komplexitätskosten | Fehlinterpretationsrisiko | Entscheidung |
|---|---|---|---|---|---|---|
| Testzuordnung verdichten | vorhanden, aber zu ausführlich | hoch: Testrelevanz bleibt sichtbar | stark sinkend durch Counts, wenige Dateien und keine vollständigen Methodennamenlisten | niedrig bis mittel; Auswahl- und Truncationregeln müssen deterministisch werden | mittel: statische Zuordnung darf nicht wie Abdeckung wirken | **Core, Default** |
| Caller verdichten | vorhanden | hoch für Änderungsradius | sinkend durch Gesamtzahl plus kleine Auswahl | niedrig | mittel bei beliebiger Auswahl; stabile Priorisierung erforderlich | **Core, Default** |
| Callee-Zusammenfassung | echte Lücke im Feature-Context; get_call_tree existiert bereits | mittel bis hoch für lokales Verständnis | klein bei direkten Callees und hartem Top-N; stark wachsend bei Tiefe | mittel wegen neuer Auswahl-/Budgetlogik; Roslyn-Auflösung ist vorhanden | hoch bei dynamischer Bindung, Extension-/Delegate-Aufrufen und scheinbarer Vollständigkeit | **Kandidat; nur begrenzte direkte Summary, nicht automatisch zugesagt** |
| Source-Preview | echte Lücke im Feature-Context; get_symbol_body existiert bereits | hoch bei Prepare-Edit, geringer bei reiner Impact-Frage | mittel bis hoch, abhängig von Bodygröße | mittel; Fensterung, Zeilenangaben und Gesamtbudget nötig | mittel bis hoch bei abgeschnittenem Body oder fehlendem Kontext | **Kandidat; erst gegen Callees messen** |
| Violations-Status und Kurzsummary | Violations sind vorhanden; Fehler-/Kostenstatus ist die Lücke | hoch, weil „leer“ und „nicht geprüft“ sicher unterscheidbar werden | sehr klein; leere Ergebnisse nur als `Violations: 0` | mittel bis hoch wegen aktuellem vollständigem uncached Linterlauf | hoch, wenn Scanfehler weiterhin als null erscheinen | **Core, Default; Scanstrategie separat verbessern oder transparent als teuer markieren** |
| Snapshot-/Freshness-Envelope | Loading/Staleness existieren serverseitig, aber nicht einheitlich in dieser Payload | hoch für sichere Editentscheidungen | klein | mittel; gemeinsame Statusmodelle und Tests | hoch ohne expliziten Snapshotstatus | **Core, Default** |
| Gesamtbudget und Gesamtvollständigkeit | bereichsweise Limits existieren; Gesamtvertrag fehlt | sehr hoch, weil der Agent Antwortgröße und Auslassungen beurteilen kann | Envelope kostet wenig, verhindert aber unbounded Wachstum | mittel; Projektion muss alle Bereiche gemeinsam priorisieren | hoch ohne Gesamtstatus trotz einzelner Truncationflags | **Core, Default** |
| Multi-Symbol-Kontext | echte Lücke | unklar; kann bei Refactorings helfen, ist aber ohne Relevanzauftrag mehrdeutig | potenziell sehr hoch | hoch; gemeinsame Priorisierung und Snapshotbindung | sehr hoch durch vermischte Relevanz | **vorerst verwerfen** |
| Viele neue Einzelparameter | nicht erforderlich; heutige Flags/Limits existieren bereits | gering; Agenten vergessen optionale Parameter häufig | kein direkter Antwortgewinn | hoch durch API-, Kompatibilitäts- und Testmatrix | hoch durch widersprüchliche Kombinationen | **verwerfen; Core-Default, später höchstens ein Profilparameter** |

Die Matrix ist absichtlich asymmetrisch: „echte Lücke“ bedeutet nicht
automatisch „Default“. Eine Erweiterung kommt nur in den Default, wenn ihr
Nutzen in repräsentativen Aufgaben den zusätzlichen Token-, Laufzeit- und
Fehlerkosten nachweisbar rechtfertigt.

### Scope-Grenzen und Rückwärtskompatibilität

Die beiden Hauptrisiken — endlose Konzeptarbeit und eine große Änderung ohne
Mehrwert — werden durch einen bewusst kleinen ersten Scope getrennt behandelt.

Der erste Scope umfasst ausschließlich:

- Verdichtung der bereits vorhandenen Core-Bereiche Tests, Caller und
  Violations;
- ein gemeinsames Gesamtbudget, eine maschinenlesbare Vollständigkeit und
  einen sichtbaren Snapshot-/Freshness-Status;
- korrekte Teilfehlersemantik, insbesondere beim Violations-Scan;
- automatisierte Vertrags-, Größen-, Laufzeit- und Agentennutzenmessung.

Nicht Bestandteil dieses ersten Scopes sind Source-Integration,
Callee-Traversierung, Multi-Symbol-Kontext, eine neue Explore-API oder eine
grundlegende Neugestaltung des Linter-/Cache-Systems. Source und Callees werden
in der Evaluation nur als begrenzte Kandidaten verglichen. Wenn der
Violations-Scan danach weiterhin unverhältnismäßig teuer ist, wird diese
Optimierung als eigenes, klar abgegrenztes Problem behandelt und nicht in den
Kontextvertrag hineingemischt.

Die aktuelle öffentliche Beschreibung und die bestehenden FastTests machen den
heutigen Detailgrad teilweise beobachtbar: Dokumentation nennt den
vollständigen typisierten Payload und die fünf Dimensionen; Tests prüfen unter
anderem Abschnittsnamen, Beispiel-Testmethoden, Counts und Truncation. Eine
stille Reduktion der sichtbaren Testmethoden oder anderer Listen im bisherigen
Default ist deshalb nicht automatisch rückwärtskompatibel.

Daraus folgt für die sichere Erprobung:

- bestehende Eingabeparameter, Aliase und die fünf vorhandenen semantischen
  Bereiche bleiben erhalten;
- Envelope-, Status- und Budgetfelder werden additiv eingeführt;
- `core-compact` wird zunächst als messbare, klar erkennbare Variante gegen
  den aktuellen Default bewertet und nicht ungeprüft als Ersatz ausgerollt;
- eine Umstellung des Defaultverhaltens erfolgt erst nach bestandenem
  Kompatibilitäts- und Nutzen-Gate. Ohne dieses Gate bleibt der aktuelle
  Default bestehen, auch wenn `core-compact` intern kleiner ist.

Das ist absichtlich eine Sicherheitsbremse, kein Plädoyer für eine dauerhafte
Parameterflut. Wenn die Variante den Nutzen nachweist, kann ein einzelnes
Profil (`core` bzw. `standard`) die Übergangs- oder Fallbacksemantik tragen;
unabhängige Schalter pro Datenquelle werden weiterhin nicht eingeführt. Die
automatisierte Eval selbst braucht dafür keinen öffentlichen Parameter: Die
kompakte Projektion kann zunächst intern bzw. in der Test-/Eval-Schicht gegen
den bestehenden Output verglichen werden. Ein öffentliches Übergangsprofil
kommt nur hinzu, wenn ein echter Agentenpilot dafür erforderlich ist.

### Stop-Regeln gegen Konzept- und Scope-Drift

Das Konzept gilt für den ersten Scope als ausreichend, sobald die Eval-Matrix,
der Kompatibilitätsvertrag und die Muss-/Akzeptanzkriterien bestätigt sind.
Weitere Fragen zu Source, Callees oder einem anderen Budgetprofil blockieren
diesen Scope nicht, sondern werden durch die Messung entschieden.

Der erste Scope wird nicht erweitert, wenn:

- `core-compact` keinen klaren Nutzlastgewinn gegenüber dem aktuellen Default
  erzielt (vorläufiges Ziel: mindestens 30 Prozent Medianreduktion auf den
  Standardfällen);
- eine schädliche Fehlentscheidung häufiger wird oder ein bestehendes
  semantisches Signal ohne sichtbare Ersatzinformation verloren geht;
- die Änderung nur durch zusätzliche unabhängige Parameter erklärbar wäre;
- der Nutzen nur in der AiNetLinter-eigenen Codebasis auftritt.

In diesen Fällen wird die Erweiterung verworfen oder als separates Problem
behandelt. Ein größeres Budget ist kein automatischer Ausweg aus einer
schlechten Priorisierung.

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
- Gemessen wird nach der fachlichen Auswahl, Formatierung und JSON-
  Serialisierung: `UTF-8(TextContent)` plus `UTF-8(StructuredContent.RawText)`.
  Damit wird die tatsächlich erzeugte Nutzlast begrenzt; eine
  modellabhängige Token-Schätzung ist kein Vertragsbestandteil.
- Die Messung umfasst auch die in `StructuredContent` enthaltenen Envelope-
  und Budgetdaten. Transportabhängiger JSON-RPC-Framing-Overhead wird nicht
  künstlich in die Tooldaten kopiert, sondern in Integrationstests mit einem
  Sicherheitsabstand gegen die reale MCP-Antwort überprüft.
- Das Budget wird nach einer festen Prioritätsreihenfolge durch Projektion der
  sichtbaren Einträge eingehalten. Eine Projektion darf nicht nur den Text
  kürzen, wenn dadurch der strukturierte Vertrag seine Zähler oder Statusdaten
  verliert.
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

### Token- und Antwortökonomie

Informationsdichte ist wichtiger als Vollständigkeitsrhetorik. Ein leerer
Bereich darf keine wiederholte Erklärung erzeugen; ein Fehler darf nicht durch
einen langen Diagnoseabsatz zusätzliche Tokens verbrauchen. Die strukturierte
Antwort ist die kanonische Quelle für Counts, Status und Gründe. Die
Textdarstellung wiederholt diese Daten nur so weit, wie ein text-only MCP-
Client sie für die nächste Agentenentscheidung benötigt.

Für den ersten Core-Entwurf wird daher folgende Verdichtung empfohlen:

- Tests: Gesamtzahlen und wenige repräsentative Testdateien; vollständige
  Testmethodenlisten nicht standardmäßig ausgeben. Zusätzlich muss die Zahl
  der ausgegebenen Methodennamen separat begrenzt werden; `maxTests` begrenzt
  aktuell nur sichtbare Testdateien und kann deshalb trotz eines kleinen
  Dateilimits noch viele Methodennamen liefern.
- Violations: Gesamtzahl, Anzahl direkt am Symbol und wenige priorisierte
  Beispiele; nicht jede dateiweite Meldung als Langtext wiederholen.
- Caller: kleine deterministische Auswahl plus Gesamtzahl.
- Deklaration und Metriken: vollständig, aber kompakt.
- Source und Callees: zunächst nur als Messkandidaten behandeln, nicht aus
  Gewohnheit in den Default aufnehmen. Eine erste Callee-Hypothese darf nur
  direkte, Roslyn-sichere Beziehungen mit sehr kleinem Top-N und explizitem
  Gesamtcount prüfen; `get_call_tree` bleibt für Tiefe und Vollanalyse.

Diese Auswahl ist eine fachliche Hypothese für die Evaluation, kein Ersatz für
die bestehende semantische Genauigkeit. Bei knappen Budgets sollen Counts und
Status erhalten bleiben, während zuerst Detailzeilen und Methodennamen entfallen.

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

### Automatisierte Budget-Kalibrierung

Der Budgetwert soll nicht durch wiederholte manuelle Nutzungs-Audits bestimmt
werden. Ein automatisierter Evaluationslauf soll einmalig und später als
Regressionstest mehrere repräsentative Fälle messen:

- reale Symbole aus der AiNetLinter-Codebasis mit kleinem, mittlerem und hohem
  Caller-/Test-/Violation-Aufkommen;
- kontrollierte synthetische Fälle für viele Caller, viele Tests, viele
  Violations und große Bodies;
- Standard-Core sowie jedes später bestätigte optionale Profil;
- UTF-8-Nutzlast von Text und StructuredContent, Truncation-Gründe,
  Pflichtfelder, Determinismus und Laufzeit.

Der Lauf soll Verteilung und Worst-Case ausgeben (mindestens Anzahl Fälle,
Minimum, Median, p95 und Maximum) und maschinenlesbar prüfen, dass keine
Antwort das harte Budget überschreitet. Der Defaultwert wird nach dieser
Kalibrierung einmal festgelegt und anschließend als Vertrags-/Regressionstest
geschützt. Die bestehende UTF-8-Budgetlogik anderer MCP-Tools ist dafür ein
Wiederverwendungsanker; sie wird nicht als neues allgemeines Explore-Feature
geplant.

Der konkrete Budgetwert wird nicht im Konzept geraten und nicht durch den
Nutzer gegen beliebige Fremdrepositories abgenommen. Während der Umsetzung
prüft ein endlicher, versionierter Kandidatenlauf zunächst beispielsweise
`8`, `16`, `24`, `32` und `48 KiB` kombinierte Nutzlast. Diese Werte sind
technische Testkandidaten, keine neuen Nutzerparameter. Für jeden Kandidaten
werden alle synthetischen Fälle, die wenigen festen Dogfood-Fälle und die
`core-compact`-Projektion ausgeführt. Der kleinste Kandidat, der alle harten
Gates und die Nutzenschwellen erfüllt, wird automatisch als Defaultwert
ausgewiesen. Falls kein Kandidat besteht, wird kein neuer kompakter Default
freigegeben; der bestehende Default bleibt erhalten und der Lauf meldet den
Befund maschinenlesbar.

Damit ist „gut“ bzw. „schlecht“ kein manuelles Bauchurteil:

- **Sofortiger Fehlschlag:** Budgetüberschreitung, nichtdeterministische
  Auswahl, fehlender Pflichtstatus, Scanfehler als leeres Ergebnis oder ein
  ungültiger Snapshot-/Vollständigkeitsstatus.
- **Bestandenes Core-Gate:** mindestens 30 Prozent Medianreduktion der
  kombinierten UTF-8-Nutzlast auf Standardfällen, mindestens 95 Prozent
  korrekte Agentenentscheidungen insgesamt und mindestens 90 Prozent je
  Szenarioklasse; sicherheitsrelevante Fehlentscheidungen sind nicht zulässig.
- **Laufzeit-Gate:** `core-compact` darf die warmen p95-Laufzeiten gegenüber
  dem heutigen Default nicht wesentlich erhöhen; als Startgrenze gelten 10
  Prozent. Die absolute Kostenverteilung des Violations-Scans wird separat
  ausgewiesen und nicht durch kleinere Antwortbytes verschleiert.
- **Randfälle:** Truncation ist akzeptabel, wenn Gesamtzahlen, Status,
  Unsicherheit und ein sinnvoller Folgeaufruf erhalten bleiben. Eine kürzere
  Antwort ohne diese Signale gilt als schlechter, nicht als effizienter.

Der Evaluationslauf erzeugt daraus einen maschinenlesbaren Bericht mit
gewähltem Budget, verworfenen Kandidaten, Gate-Ergebnissen je Szenarioklasse
und den Verteilungen für Bytes, Modelltokens, Laufzeit und Folgeaufrufe. Die
Umsetzung braucht dadurch keinen wiederkehrenden manuellen Usage-Audit über
fremde Repositories.

Die Evaluation soll neben Bytes auch die fachliche Nützlichkeit prüfen:
Kann ein Agent anhand der Defaultantwort entscheiden, ob er editieren,
`get_symbol_body`, `get_call_tree` oder `get_impact` nachladen muss? Die
Optimierung darf nicht bloß die Bytezahl senken und dabei die für eine sichere
Änderung nötigen Signale entfernen.

Zusätzlich soll der Evaluationslauf den Tokenverbrauch mit mindestens einer
repräsentativen Modell-Tokenisierung erfassen. Diese Zahl bleibt eine
Messgröße und wird nicht zum öffentlichen Budgetvertrag, weil MCP-Clients und
Modelle unterschiedliche Tokenizer und Darstellungen verwenden. Für typische
Understand-, Prepare-Edit- und Refactoring-Fälle sollen automatisierte
Agenten-Evals außerdem prüfen, ob der Kontext die richtige nächste Aktion
ermöglicht und unnötige Folgeaufrufe reduziert.

### Konkrete Eval-Matrix

Die Evaluation vergleicht mindestens vier Antwortvarianten auf demselben
Snapshot und für dasselbe Ziel-Symbol:

| Variante | Zweck |
|---|---|
| `baseline-current` | heutiger Vertrag als Referenz für Inhalt, Nutzlast und Laufzeit |
| `core-compact` | verdichtete bestehende Bereiche mit Gesamtbudget und Statussemantik |
| `core-compact-callees` | `core-compact` plus begrenzte direkte Callee-Summary |
| `core-compact-source` | `core-compact` plus begrenztes Source-Preview |

Die Varianten werden nicht nur gegen das eigene Repository ausgeführt. Die
synthetischen Solutions bilden eine Matrix aus unabhängigen Achsen:

| Achse | mindestens zu prüfende Ausprägungen | Wofür sie wichtig ist |
|---|---|---|
| Projektverteilung | produktionsdominiert/testarm, ungefähr 40/60, sehr testdicht | verhindert einen test- oder produktionsspezifischen Default |
| Projektgröße | klein, mittel, groß | prüft Skalierung und residente Solution-Kosten |
| Caller-Fan-out | 0–2, 3–10, hoch | prüft Caller-Auswahl und Gesamtcount |
| Callee-Fan-out | 0, wenige, hoch; zusätzlich Tiefe 2+ | trennt direkte Summary von unkontrolliertem Call-Tree-Wachstum |
| Testzuordnung | keine, wenige Dateien/Methoden, viele Dateien/Methoden | prüft Datei- und Methodennamenlimits unabhängig voneinander |
| Violations | 0, niedrig, mittel, hoch; zusätzlich absichtlicher Scanfehler | trennt leeres Ergebnis, Truncation, Fehler und Laufzeitkosten |
| Bodygröße | klein, mittel, groß und sehr groß | prüft Source-Fenster und Budgetprojektion |
| Symbolform | Methode, Typ, Konstruktor, Property/Accessor, generisch, Partial/Overload | verhindert, dass die Verdichtung nur für eine Symbolform korrekt ist |

Jede Kombination muss nicht vollständig kartesisch erzeugt werden. Der
Evaluationslauf verwendet eine reproduzierbare Abdeckung aller Einzelachsen
und gezielte Stresskombinationen, insbesondere testdicht plus hoher Fan-out
und violation-dicht plus großer Body. So bleibt die Matrix aussagekräftig,
ohne eine unwartbare Zahl an Fällen zu erzeugen.

#### Aufgabenbezogene Nutzensmessung

Für jedes Ziel-Symbol werden mindestens drei standardisierte Agentenaufgaben
mit einer kleinen, versionierten Gold-Spezifikation geprüft:

1. **Understand:** Der Agent soll Zweck, Deklaration, relevante Metriksignale
   und unmittelbare Beziehungen benennen.
2. **Prepare-Edit:** Der Agent soll entscheiden, ob er den Symbol-Body,
   Caller/Impact, Call-Tree oder keinen weiteren Aufruf benötigt, bevor er eine
   lokale Änderung vorbereitet.
3. **Refactoring:** Der Agent soll den erwarteten Änderungsradius und die
   statische Testrelevanz einschätzen und zwischen sicherem Wissen und
   fehlender Laufzeitabdeckung unterscheiden.

Die Gold-Spezifikation enthält pro Aufgabe nur die minimal erforderlichen
Signale, erwartete Folgeaufrufe und verbotene Behauptungen. Sie wird aus der
synthetischen Solution erzeugt und für die Dogfood-Fälle manuell klein gehalten;
es ist kein laufendes manuelles Nutzungs-Audit erforderlich. Bewertet werden:

- korrekte Symbol- und Snapshot-Interpretation;
- korrekte Entscheidung `editierbar`, `Folgeaufruf nötig` oder
  `nicht sicher entscheidbar`;
- erkannte Caller-/Test-/Violation-Gesamtlage trotz Truncation;
- keine Behauptung von Laufzeitabdeckung aus statischer Zuordnung;
- keine Behauptung von Vollständigkeit bei fehlenden oder abgeschnittenen
  Bereichen;
- Zahl unnötiger Folgeaufrufe und Zahl schädlicher Fehlentscheidungen.

Für die agentische Nutzensmessung wird ein festgelegtes Modell mit festgelegter
Toolbeschreibung und festgelegter Aufgabenreihenfolge verwendet. Das Modell
ist ein reproduzierbares Messinstrument, nicht Teil des öffentlichen
Featurevertrags. Zusätzlich werden die entscheidenden Felder regelbasiert
geprüft, damit ein zufälliges sprachliches Urteil keinen Vertragsfehler
verdeckt.

#### Messgrößen und harte Gates

| Messbereich | Kennzahlen | harte Bedingung bzw. Ziel |
|---|---|---|
| Vertragskorrektheit | Symbolauflösung, Snapshotstatus, Bereichsstatus, Counts, Truncationgründe, Determinismus | 100 % der Fälle; insbesondere kein Scanfehler als `0` |
| Gesamtbudget | UTF-8-Bytes von Text plus StructuredContent nach Serialisierung | 0 Budgetüberschreitungen; 100 % Pflichtfelder trotz Kürzung |
| Tokenökonomie | Modelltoken, Bytes je Bereich, Redundanz zwischen Text und StructuredContent | `core-compact` muss gegenüber `baseline-current` deutlich sinken; der konkrete Zielwert wird aus der Matrix berichtet |
| Laufzeit | End-to-End p50/p95 getrennt nach warmem und kaltem Lauf, Anteil des Violations-Scans | keine optionale Erweiterung ohne messbaren Nutzen; mehrsekündige Scan-Kosten müssen sichtbar und begründet sein |
| Agentennutzen | Gold-Entscheidung korrekt, unnötige Folgeaufrufe, schädliche Fehlentscheidungen | `core-compact` darf die Baseline-Nützlichkeit nicht verschlechtern; Zielwert ist mindestens 95 % korrekte Entscheidungen auf den Standardfällen |
| Generalisierung | Ergebnis je synthetischer Verteilung und je Dogfood-Fall | kein Default wird allein aus dem Repository begründet; kein einzelner Projekttyp darf die Gesamtbewertung verdecken |

„Schädliche Fehlentscheidung“ hat Vorrang vor Tokenersparnis: Dazu zählen
beispielsweise ein Edit trotz unbekanntem Snapshot, ein Scanfehler als
violations-frei oder eine behauptete Vollabdeckung trotz statischer
Testzuordnung. Solche Fälle werden separat ausgewiesen und dürfen nicht durch
einen guten Durchschnitt kompensiert werden.

#### Ableitung des harten Defaultbudgets

Das Budget wird nicht als frei wählbarer Agentenparameter modelliert. Für jede
Kandidatenvariante werden die Verteilungen von Nutzlast, Tokens und Laufzeit
ausgegeben. Danach gilt folgende Auswahlregel:

1. Zuerst werden alle Werte verworfen, die eines der harten Vertrags-Gates
   verletzen.
2. Unter den verbleibenden Budgets wird der kleinste Wert gewählt, bei dem
   die Standardfälle mindestens 95 % korrekte Agentenentscheidungen liefern
   und kein Muss-Signal systematisch abgeschnitten wird.
3. Für die verbleibenden Randfälle wird sichtbare Truncation akzeptiert, wenn
   Counts, Status und ein sinnvoller Folgeaufruf erhalten bleiben.
4. Der gewählte Wert erhält einen festen Sicherheitsabstand zur technischen
   MCP-Nutzlastgrenze und wird danach als Regression geschützt.

Damit wird nicht versucht, jede Antwort vollständig zu machen. Es wird der
kleinste empirisch ausreichende Budgetwert gewählt, der die sichere
Agentenentscheidung erhält. Falls `core-compact` diesen Wert nicht erreicht,
ist das ein Befund gegen die Verdichtung, nicht ein Anlass für ein beliebig
größeres Budget.

#### Entscheidung über Source und Callees

`core-compact-source` oder `core-compact-callees` darf nur dann in den Default
aufgenommen werden, wenn die Variante gegenüber `core-compact` auf den
synthetischen Standardfällen und den Dogfood-Fällen mindestens eines der
folgenden Ergebnisse nachweist, ohne die harten Gates zu verletzen:

- mindestens fünf Prozentpunkte mehr korrekte Aufgabenentscheidungen;
- mindestens zehn Prozent weniger unnötige Folgeaufrufe;
- oder ein klarer Gewinn bei einer zuvor häufigen schädlichen
  Fehlentscheidung.

Bleibt der Gewinn darunter, bleibt die Information im bestehenden Spezialtool
(`get_symbol_body` bzw. `get_call_tree`) und wird nicht aus Bequemlichkeit in
den Core kopiert. Direkte Callees werden dabei höchstens als Count plus wenige
Namen geprüft; eine Traversierungstiefe größer eins ist kein Defaultkandidat.

Als externer Designanker passt dazu die offizielle OpenAI-Empfehlung, mit dem
kleinsten Vertrag zu starten, Toolbeschreibungen und Ausgabeformat gegen
repräsentative Beispiele zu optimieren und Genauigkeit, Tokenverbrauch sowie
End-to-End-Latenz gemeinsam zu benchmarken. Das ist eine Leitlinie für die
Evaluation, kein Ersatz für AiNetLinter-spezifische Messdaten.

### Generalisierung über Projekte hinweg

AiNetLinter ist ein allgemeines MCP-Tool und darf nicht auf die Verteilung oder
die Antwortformen dieses Repositories optimiert werden. Die AiNetLinter-
Codebasis dient nur als:

- technische Smoke-Test- und Dogfood-Quelle;
- realer Fall für die Messmechanik;
- Quelle für Beispiele verschiedener Symbol- und Beziehungsgrößen.

Sie ist keine normative Stichprobe für Defaultwerte. Die automatisierte
Kalibrierung muss deshalb synthetische, reproduzierbare Solutions mit mehreren
Verteilungen enthalten, mindestens:

- ungefähr 40 % Produktionscode / 60 % Testcode als ein testdichter Fall;
- produktionsdominierte und testarme Solutions;
- sehr testdichte Solutions;
- kleine, mittlere und große Projekte;
- Symbole mit wenig und hohem Caller-/Callee-Fan-out;
- wenig, mittlerem und hohem Violations-Aufkommen.

Die Testfälle sollen nur generische Roslyn-/Projektstrukturen und keine
AiNetLinter-spezifischen Namen oder Pfade verwenden. Ein Budget gilt erst dann
als belastbar, wenn die Antwortauswahl und die Statussemantik in allen Profilen
funktionieren. Ein besserer Wert im eigenen Repository allein ist kein
Begründungsnachweis.

Die aktuelle Codebasis liefert außerdem eine wichtige Kostenwarnung: Sie ist
violation-frei. Dadurch ist die Violations-Payload dort klein, obwohl der
vollständige uncached Scan die Antwortzeit dominieren kann. Ein Evaluationsset
mit nur dieser Codebasis würde deshalb genau die falsche Optimierung fördern
und muss synthetische Solutions mit niedriger, mittlerer und hoher
Violationsdichte sowie Scanfehlern enthalten.

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

## 14. Verifizierter aktueller Stand

Die Implementierung und ein Live-MCP-Aufruf gegen dieses Repository bestätigen:

- `FeatureContextOptions` kennt aktuell nur die fünf bestehenden Bereiche sowie `maxCallers` und `maxTests`; Profile, Callees, Source, Gesamtbudget und ein Envelope sind noch nicht Bestandteil des Vertrags.
- `FeatureContextPayload` besteht aktuell aus `Declaration`, `Metrics`, `Callers`, `Tests` und `Violations`. Snapshot-/Generation-/Freshness- und Gesamtvollständigkeitsdaten fehlen in der Payload.
- `McpToolResults.Text(text, payload)` liefert Markdown und serialisiertes `StructuredContent` additiv. Ein Gesamtbudget muss daher die tatsächlich ausgelieferte Kombination berücksichtigen, nicht nur eine der beiden Darstellungen.
- `FeatureContextScanner.CollectViolationsAsync` startet `LinterEngine.RunAsync(..., noCache: true, cacheTtlMinutes: 0, ...)`. Ein Fehler wird dort derzeit zu `ViolationsReportDto(0, 0, [], false)`, worauf der Formatter „Keine Linter-Verstoesse“ ausgibt. Das ist fachlich nicht von „erfolgreich leer“ unterscheidbar.
- Der Live-Aufruf für `T:AiNetLinter.Mcp.Tools.FeatureContext.GetFeatureContextTool` lieferte die erwarteten fünf Bereiche; bei `maxCallers: 5` wurden 5 von 13 Callern und bei `maxTests: 5` alle 12 statisch zugeordneten Tests ausgegeben. Ein gemeinsamer Snapshot-/Gesamtbudget-Hinweis war nicht vorhanden.

Die aktuelle Dokumentation beschreibt `get_feature_context` weiterhin als
Composite-One-Shot mit fünf Dimensionen und „vollständigem“ typisiertem
Payload. Bestehende FastTests prüfen unter anderem konkrete Abschnittsnamen,
Testmethodennamen, Counts und Truncation. Damit ist eine Reduktion der
Detaillisten im bisherigen Default eine beobachtbare Vertragsänderung und
muss vor einer Umstellung ausdrücklich gegen Kompatibilität und Nutzwert
geprüft werden.

Diese Befunde bestätigen die im Draft angenommene Priorität: Vertrags- und Kostenkontrolle vor zusätzlicher Datenmenge.

### 14.1 Explorative Größenmessung gegen die aktuelle Codebasis

Ein Live-Aufruf gegen das eigene Repository mit einem bekannten Methodensymbol
zeigt, dass die bestehende Antwort bereits vor einer Erweiterung unnötig groß
werden kann. Bei deaktiviertem Violations-Scan ergaben sich für denselben
Aufruf ungefähr folgende UTF-8-Nutzlasten aus Text plus StructuredContent:

| Variante | Nutzlast |
|---|---:|
| aktueller Default | ca. 20,0 KB |
| ohne Test-Zuordnung | ca. 8,4 KB |
| ohne Caller | ca. 14,9 KB |
| nur Deklaration und Metriken | ca. 3,4 KB |
| nur Deklaration | ca. 1,8 KB |

Der Haupttreiber ist die statische Test-Zuordnung: Die Antwort enthält viele
Testdateien und Methodennamen. `maxTests` begrenzt die Zahl sichtbarer Dateien,
aber nicht die Zahl der Methodennamen innerhalb der ausgewählten Dateien. Die
Messung bestätigt damit nicht, dass mehr Daten fehlen, sondern dass vorhandene
Daten zunächst besser priorisiert und komprimiert werden müssen.

Die Reihenfolge der nächsten fachlichen Arbeit wird deshalb revidiert:

1. bestehende Bereiche verdichten und redundante Textwiederholung reduzieren;
2. Gesamtbudget, Status und Fehlersemantik stabilisieren;
3. automatisiert messen, welche Information nach der Verdichtung pro Token den
   größten Agentennutzen liefert;
4. erst danach entscheiden, ob ein kleiner Source-Preview oder eine begrenzte
   Callee-Zusammenfassung in den Default gehört.

Source und Relations sind damit keine zugesagten Folgefeatures. Sie bleiben
Kandidaten, die sich gegen die verdichtete Core-Antwort beweisen müssen.

Eine zweite MCP-Probe mit sechs Symbolen aus Produktions- und Testcode dieses
Repositories lag — weiterhin ohne Violations-Scan — zwischen ungefähr 2,7 KB
und 13,0 KB kombinierter Text-/StructuredContent-Nutzlast. Die Spannweite
ändert sich sichtbar mit Fan-out und Testzuordnung. Diese Werte bestätigen die
Messbarkeit und die Kostenhebel, sind aber ausdrücklich kein allgemeiner
Defaultwert und keine repräsentative Projektverteilung.

Die Live-MCP-Aufrufe sind für diese Konzeptphase sinnvoll, weil sie zeigen, was
ein realer Client tatsächlich erhält. Die dauerhafte Verifikation darf daraus
aber keinen manuellen Nutzungsprozess machen: Sie muss als generischer
automatisierter Test-/Eval-Lauf mit synthetischen Solutions und wenigen
Dogfood-Fällen in der bestehenden C#-Testinfrastruktur verankert werden.

### 14.2 Parallel durchgeführte Messungen mit unterschiedlichen Ausschnitten

Vier getrennte Messagenten haben parallel unterschiedliche Fragen untersucht.
Die Zahlen dienen der Priorisierung der Messhypothesen, nicht der Ableitung
eines projektspezifischen Defaults.

- Bei fünf produktionsnahen Symbolen lagen vollständige Antworten ohne
  Violations-Scan ungefähr zwischen 3,8 KB und 16,2 KB. Die aggregierte
  Antwortgröße betrug etwa 53,5 KB; ohne Testzuordnung etwa 26,4 KB. Das
  entspricht in diesem Ausschnitt einer Reduktion von ungefähr 51 Prozent.
- In einem testlastigen Ausschnitt enthielt ein Symbol 22 Testdateien und 170
  zugeordnete Testmethoden. Die Antwort sank beim Weglassen der Tests von
  ungefähr 28,3 KB auf 0,7 KB. Das ist ein starker Hinweis auf den Nutzen der
  Verdichtung, aber wegen der direkten Testdichte des eigenen Repositories
  keine allgemeine Verteilungsannahme.
- Bei einem Symbol mit acht direkten Callees wuchs ein gezielter Call-Tree
  mit Tiefe 1 und Top-10 auf etwa 2,9 KB strukturierte Antwort bzw. 1,1 KB
  Text. Tiefe 2 und Top-10 lagen bereits bei etwa 18,0 KB strukturiert bzw.
  6,4 KB Text. Daraus folgt: eine direkte Callee-Summary kann ein begrenzter
  Kandidat sein; Traversierungstiefe gehört nicht in den Default.
- Vier gemessene Symbol-Bodies lagen zwischen etwa 1,2 KB und 3,2 KB. Ein
  Zeilenlimit von 10 reduzierte sie auf ungefähr 0,5 KB und markierte die
  Auslassung. Ein Source-Preview ist daher technisch budgetierbar, aber nicht
  kostenlos.
- Der Violationsbereich selbst war in der aktuellen, violation-freien
  Codebasis nur ungefähr 86 Bytes groß und erhöhte die Antwort dort insgesamt
  um rund 265 Bytes. Der uncached vollständige Linterlauf dauerte dagegen
  ungefähr fünf Sekunden. Die Priorität lautet deshalb nicht „Violations
  stärker ausgeben“, sondern „Ergebnisstatus korrekt und Scan-Kosten sichtbar
  machen“.

Die Messungen bestätigen drei konkrete Modellierungsregeln: Erstens müssen
Tests sowohl nach Dateien als auch nach Methodennamen begrenzt werden.
Zweitens muss das Gesamtbudget die Antwort als Kombination aus Text und
StructuredContent behandeln. Drittens muss Antwortgröße neben Laufzeit und
Agentennutzen bewertet werden; eine kleine Antwort mit einem unverhältnismäßig
teuren Unterlauf ist kein guter One-Shot-Kontext.

## 15. Festgelegte Entscheidungen und spätere Nachweise

- Der erste Scope verdichtet ausschließlich die vorhandenen Core-Bereiche und
  ergänzt Gesamtbudget, Vollständigkeit, Freshness und Teilfehlersemantik.
- Der numerische Budgetwert wird während der Umsetzung automatisch aus einem
  endlichen Kandidatenlauf gewählt. Der Nutzer muss keine Fremdrepositories
  manuell prüfen.
- Die automatischen Gates sind Bestandteil des Verifikationsvertrags:
  Budget-/Vertragsfehler führen zum Fehlschlag; der Core benötigt mindestens
  30 Prozent Medianreduktion, 95 Prozent korrekte Standardentscheidungen und
  90 Prozent je Szenarioklasse ohne sicherheitsrelevante Fehlentscheidung.
- Der heutige Default bleibt während der Evaluations- und Kompatibilitätsphase
  erhalten. Eine spätere Umstellung auf den kompakten Core ist nur nach
  bestandenem Gate zulässig.
- Source, Callees, Multi-Symbol-Kontext und eine Linter-/Cache-Neugestaltung
  sind keine Bestandteile dieses ersten Scopes. Sie bleiben bewusst
  zurückgestellte oder separat zu behandelnde Themen.
- Die bestehenden Aufrufparameter und semantischen Kernbereiche bleiben
  kompatibel; eine Parameterflut wird nicht eingeführt.

Der konkrete Budgetwert und eine eventuelle spätere Default-Umstellung waren
ursprünglich als Ergebnisse der automatisierten Umsetzungsevaluation und nicht
als offene Konzeptentscheidungen eingeordnet. Diese Freigabeentscheidung ist
durch den nachfolgenden Red-Team-Audit aufgehoben. Das Konzept ist erneut ein
Draft; der Orchestrator darf auf dieser Grundlage nicht gestartet werden.

## Arbeitsgedächtnis (nur Draft)

### Übergabestatus

Das Konzept wurde auf ausdrücklichen Nutzerwunsch nach einem unabhängigen
Red-Team-Audit erneut geöffnet. Die vorhandenen Entscheidungen bleiben als
Ausgangsbasis erhalten, sind aber nicht mehr freigegeben. In einer späteren
Planungssession sind insbesondere Budgetvertrag, Teilfehler-/Cancellation-
Semantik, Snapshot/Freshness, Rückwärtskompatibilität und Eval-Methodik zu
schärfen.

### Audit 1: Executive Summary

**Entscheidung: Conditional-Go für die Produktidee, No-Go für die Umsetzung auf
Basis des bisherigen `status: ready`.**

Die Grundrichtung ist tragfähig: Ein kleiner, deterministischer und ehrlich
begrenzter Arbeitskontext kann Agentenarbeit verbessern. Das Konzept ist aber
noch nicht implementierungsreif, weil vier Grundlagen nicht belastbar
definiert sind:

- Server-Antwortgröße, MCP-Transportgröße und tatsächlich modell-sichtbarer
  Kontext werden vermischt.
- Fehler-, Abbruch- und Freshness-Semantik widersprechen teilweise dem heutigen
  Code.
- Der geplante erste Scope kombiniert Protokollumbau, Snapshot-Architektur,
  Kompaktionsalgorithmus und eine wissenschaftlich noch nicht belastbare
  Agentenevaluation.
- Die numerischen Promotion-Gates sind derzeit Zielwerte ohne empirische oder
  statistische Herleitung.

Empfehlung: Zunächst einen kleinen Truthfulness-/Contract-Slice definieren.
Erst danach soll ein opt-in `compact-v1` end-to-end evaluiert werden.

### Audit 2: Top fünf Risiken

| Schwere | Befund und Evidenz | Relevanz | Empfehlung | Blockiert ersten Scope? |
|---|---|---|---|---|
| **Blocker** | **Das Budget misst nicht zuverlässig den modell-sichtbaren Kontext.** `[Code-verifiziert]` `McpToolResults.Text<T>` erzeugt verschiedenes Markdown in `Content` und JSON in `StructuredContent`. `[Dokumentations-verifiziert]` Text-only-Clients dürfen Structured Content ignorieren. Die MCP-Spezifikation empfiehlt gerade deshalb einen Text-Fallback für Structured Content; sie garantiert nicht, dass jeder Host beide Repräsentationen identisch in den Modellkontext übernimmt. | `UTF8(Text)+UTF8(StructuredContent.RawText)` ist ein definierbares internes Datenbudget, aber weder exakte JSON-RPC-Transportgröße noch Tokenverbrauch des Agenten. Dadurch kann die automatische Budgetwahl auf der falschen Zielgröße optimieren. | Drei Kennzahlen trennen: vollständig serialisierte `CallToolResult`-Bytes nach allen Wrappern; Bytes je Repräsentation; modell-/host-spezifische Prompt-Tokens nur in der Evaluation. Text-only, structured-aware und dual-rendering als getrennte Clientszenarien testen. | **Ja** |
| **Blocker** | **Partial-Failure und Cancellation sind aktuell sicherheitsrelevant falsch.** `[Code-verifiziert]` `CollectViolationsAsync` fängt pauschal `Exception` und gibt einen leeren Violationsreport zurück. Damit wird auch `OperationCanceledException` als `0 Violations` maskiert. Bei fehlender Quelldatei entsteht ebenfalls ein leerer Report statt `notApplicable`/`unavailable`. | Ein abgebrochener oder fehlgeschlagener Linterlauf kann als geprüfte Regelkonformität erscheinen. Das widerspricht dem zentralen Konzeptversprechen „Unsicherheit explizit“. | Cancellation immer weiterwerfen. Pro Bereich expliziten Status einführen: `notRequested`, `complete`, `truncated`, `unavailable`, `failed`; dazu stabiler `reasonCode`. „Keine Treffer“ nur nach erfolgreicher Prüfung. | **Ja** |
| **Blocker** | **Freshness und Hard Budget sind nicht atomar definiert.** `[Code-verifiziert]` Ein Toolaufruf erhält zwar eine immutable `Solution`, aber keine zugehörige Generation oder Snapshot-ID. Bei Refresh-Problemen wird der letzte gute Stand behalten. Der Degraded-Hinweis wird erst nach der Toolantwort außen vorangestellt. | Ein innerhalb des Tools geprüftes Budget kann durch den äußeren Warntext überschritten werden. Gleichzeitig kann Structured Content „frisch“ aussehen, während nur der Text eine Degraded-Warnung enthält. | Snapshot-Metadaten atomar zusammen mit der `Solution` erfassen. Budgetierung erst auf dem endgültigen Resultat nach Degraded-/Warn-Wrappern durchführen oder dafür einen exakt reservierten Overhead verwenden. | **Ja** |
| **Blocker für Default-Umschaltung** | **Die Eval-Gates sind nicht statistisch belastbar.** `[Konzept-verifiziert]` 30 %, 95 %, 90 %, 5 Prozentpunkte und 10 % sind ohne Stichprobengröße, Varianz, Konfidenzintervall oder Fehlertoleranz festgelegt. `[Externe Primärquelle]` OpenAI empfiehlt reale Verteilungen, kontinuierliche Evaluation und die Kalibrierung automatischer Verfahren mit menschlichen Urteilen. Anthropic empfiehlt mehrere Trials und die Kombination aus deterministischen, Modell- und Human-Gradern. | Ein Kandidat kann zufällig gewinnen, obwohl er fachlich schlechter ist. „Keine sicherheitskritische Fehlentscheidung beobachtet“ belegt bei kleinen Stichproben nicht, dass die Fehlerrate akzeptabel ist. | Gepaarte Baseline-/Kandidatenläufe, mehrere Trials, Konfidenzintervalle und vorab definierte Nichtunterlegenheitsgrenzen. Kernentscheidungen regelbasiert bewerten; Modellrichter nur für subjektive Qualität und gegen Human Labels kalibrieren. | **Nein** für Contract-Slice, **ja** für automatische Budgetwahl/Default |
| **Hoch** | **Der erste Scope ist kein einzelner Implementierungsslice.** `[Konzept-verifiziert]` Er umfasst DTO-Vertrag, Kompaktion, globales Budget, Snapshot/Freshness, Partial Failure, Performance, große Fixtures, Agententraces und automatische Budgetwahl; gleichzeitig werden Source und Callees als Kandidaten evaluiert, obwohl sie als Nichtziel gelten. | Zu viele unabhängige Fehlerursachen erschweren Review, Regressionserkennung und eine ehrliche Go/No-Go-Entscheidung. | In Truthfulness/Contract, opt-in Compact Projection und Agent Evaluation aufteilen. Source/Callees vollständig aus dem ersten Scope entfernen. | **Ja** |

### Audit 3: Verifizierte Stärken

| Schwere | Positiver Befund und Evidenz | Relevanz | Empfehlung | Blockiert? |
|---|---|---|---|---|
| **Niedrig – positiv** | **Die Progressive-Disclosure-Richtung ist gut begründet.** `[Externe Primärquelle]` Tool-Design-Empfehlungen unterstützen sinnvolle Defaults, Filter, Pagination und hilfreiche Trunkierung. Long-Context-Forschung zeigt, dass mehr Kontext nicht automatisch besser genutzt wird. | Das unterstützt „kleinster hinreichender Kontext“, nicht jedoch die konkreten Byte-Grenzen. | Leitprinzip beibehalten, numerische Werte empirisch ermitteln. | Nein |
| **Niedrig – positiv** | **Ein gemeinsames deterministisches Bytebudget ist technisch machbar.** `[Code-/Test-verifiziert]` Die Assembly-Analyse besitzt bereits Projektion, Trunkierung und kombinierte UTF-8-Messung. | Es existiert wiederverwendbares Wissen und Testmuster. | Semantik übernehmen, nicht blind die payload-spezifische Implementierung verallgemeinern. | Nein |
| **Niedrig – positiv** | **Das Konzept schützt den bisherigen Default, wenn kein Kandidat alle Gates erfüllt.** `[Konzept-verifiziert]` | Das verhindert einen erzwungenen Rollout aufgrund einer fehlgeschlagenen Evaluation. | Beibehalten; nur der Promotion-Job soll dann scheitern, nicht notwendigerweise jeder normale CI-Lauf. | Nein |
| **Niedrig – positiv** | **Trunkierung und Bereichsstatus werden als fachliche Information erkannt.** `[Konzept-verifiziert]` | Das ist für Agenten wichtiger als bloß kleinere JSON-Strukturen. | Statusmodell präzisieren und zuerst unabhängig von Kompaktion umsetzen. | Nein |
| **Niedrig – positiv** | **Der bestehende Vertrag ist bereits brauchbar dokumentiert und komponentengetestet.** `[Test-verifiziert]` Flags, Deserialisierung und Limits werden geprüft; die fünf aktuellen Dimensionen sind dokumentiert. | Damit gibt es eine klare Baseline für Kompatibilitäts- und Größenvergleiche. | Bestehende Tests als Legacy-Contract einfrieren und um Live-MCP-Tests ergänzen. | Nein |

### Audit 4: Kritische falsche oder unbelegte Annahmen

| Schwere | Annahme, Evidenz und Bewertung | Relevanz | Empfehlung | Blockiert? |
|---|---|---|---|---|
| **Hoch** | **„Direct callers“ seien tatsächliche Aufrufer.** `[Code-verifiziert]` Die Implementierung übernimmt Roslyn-Referenzstellen aus `SymbolFinder.FindReferencesAsync` und begrenzt sie anschließend. Das umfasst statische Referenzen, nicht ausschließlich ausgeführte Aufrufe. | Methodengruppen, Delegates, `nameof`, Konstruktionen und andere Referenzformen können fachlich anders zu bewerten sein. Runtime-Dispatch ist nicht bewiesen. | In `references`/`callSites` umbenennen und `relationKind` aus Syntax/`IOperation` ergänzen. „Caller“ nur für nachgewiesene Invocation-/Object-Creation-Stellen verwenden. | **Ja**, wenn Caller fachlicher Gold-Standard werden |
| **Hoch** | **Ein fixiertes Modell, Prompt und Tool-Layout machten die Evaluation reproduzierbar.** `[Externe Quelle]` Agenten- und Modellgrader bleiben nondeterministisch; mehrere Trials und menschliche Kalibrierung sind erforderlich. | Einzelne Läufe können Gewinner und Verlierer vertauschen; Provider-Updates erzeugen zusätzliche zeitliche Drift. | Modell-Snapshot, Harness-Version und Prompt hashen; mehrere gepaarte Läufe und Unsicherheitsintervalle verwenden. | Ja für Promotion |
| **Mittel** | **Die „kleinster Vertrag“-Aussage sei eine konkrete offizielle OpenAI-Empfehlung.** `[Externe Quellenprüfung]` Die geprüften offiziellen OpenAI-Evalleitlinien unterstützen aufgabenspezifische, realitätsnahe und kalibrierte Evals, aber nicht den behaupteten spezifischen Byte-/Payload-Vertrag. Die nähere Primärquelle ist Anthropic. | Eine falsche Quellenzuschreibung schwächt die Begründung, obwohl die Richtung plausibel ist. | Quellenbehauptung präzisieren: „durch Tool-Design- und Long-Context-Evidenz gestützte Architekturhypothese“. | Nein |
| **Hoch** | **Synthetische Lösungen plus wenige Dogfood-Fälle repräsentierten eine universelle Default-Verteilung.** `[Architekturannahme]` Die Matrix deckt Formen ab, aber keine nachgewiesene Nutzungshäufigkeit und kaum reale Repository-Topologien. | Ein universeller Default kann auf kleinen Fixtures gewinnen und in Multi-TFM-, Generator- oder Monorepo-Szenarien verlieren. | Synthetische Grenzfälle mit mehreren versioniert gepinnten realen Fixture-Repositories kombinieren. | Ja für universellen Default |
| **Hoch** | **Eine rein interne Compact-Variante reiche für den realen MCP-Nutzenbeleg.** `[Code-/Protokollableitung]` Host und Client entscheiden, wie Text und Structured Content weitergereicht werden. | Interne Projektion misst nicht die tatsächliche Clientdarstellung, Toolfolge oder Tokenisierung. | Nach dem Contract-Slice einen expliziten opt-in `compact-v1`-Pfad für echte Client-/Host-Evaluation anbieten. | Ja für End-to-End-Nachweis |
| **Hoch** | **`0 Violations` bedeute immer erfolgreich geprüft.** `[Code-verifiziert]` Fehler und fehlende Quelldateien können heute denselben leeren Report erzeugen. | Dies kann sicherheitsrelevante Fehlentscheidungen auslösen. | Ergebnisstatus von Treffermenge trennen. | Ja |

### Audit 5: Technische Machbarkeit für C# und Roslyn

| Thema | Einstufung | Schwere / Evidenz | Empfehlung | Blockiert? |
|---|---|---|---|---|
| Gemeinsames Hard Budget | **Technisch gut lösbar, mit relevanter Komplexität** | **Hoch.** `[Code-verifiziert]` Assembly-Projektion beweist Machbarkeit. Der äußere Degraded-Wrapper wird heute aber später angewandt. | Endgültige `CallToolResult`-Serialisierung budgetieren; zusätzlich Einzelgrößen ausweisen. | Ja |
| Text/Structured-Content-Konsistenz | **Serverseitig lösbar; Client-Sichtbarkeit clientabhängig** | **Hoch.** Unterschiedliche Repräsentationen existieren absichtlich. | Beide aus einem kanonischen DTO projizieren und semantische Parität testen; Clientszenarien getrennt messen. | Ja |
| Deterministische Priorisierung | **Technisch gut lösbar** | **Mittel.** Call Sites werden vor `Take` nicht sichtbar stabil sortiert; Violations nur nach Zeile. | Vollständige Tie-Breaker: normalisierter Pfad, Zeile, Spalte, Symbol-/Rule-ID, Message. Wiederholungstests mit permutierter Eingangsreihenfolge. | Ja für reproduzierbares Budget |
| Extension Methods, Overloads, statisch gebundene Generics | **Technisch gut lösbar** | **Mittel.** Roslyn kann die im aktiven Compilation-Kontext gebundenen Symbole auflösen. | SymbolKey/OriginalDefinition plus konkrete Substitutionen getrennt ausweisen. | Nein |
| Partial Types | **Technisch lösbar, mit relevanter Komplexität** | **Mittel.** Symbolreferenz ist möglich, aber Declaration-, Source- und Dateiprovenienz können mehrere Orte haben. | Mehrere Deklarationsorte explizit modellieren; keinen zufälligen Hauptteil als vollständig ausgeben. | Nein |
| Virtual/Interface Dispatch | **Nur statisch vollständig, zur Laufzeit heuristisch** | **Hoch.** Compile-time-Referenzen und Implementierungen sind analysierbar; tatsächliche Runtime-Ziele nicht allgemein. | `staticReference`, `implementationCandidate` und `runtimeTargetUnknown` unterscheiden. | Ja, falls „vollständige Caller“ behauptet werden |
| Delegates | **Heuristisch** | **Hoch.** Zuweisungen sind statisch auffindbar; Invocation-Target erfordert Flow-Analyse und bleibt bei Mutation unvollständig. | Nicht als sichere Caller-Beziehung verkaufen; Provenienz/Confidence angeben. | Nein, wenn korrekt deklariert |
| `dynamic`, Reflection, String-DI/Convention Routing | **Im allgemeinen ersten Scope ungeeignet** | **Hoch.** `[Plausible Sprach-/Architekturgrenze]` Die Laufzeitbeziehung ist nicht vollständig aus dem Roslyn-Symbolgraphen ableitbar. | Explizit `notObservableStatically`; keine Vollständigkeitsbehauptung. | Ja für harte Vollständigkeitsgarantie |
| Source Generators und Conditional Compilation | **Konfigurationsabhängig** | **Hoch.** Analysiert wird die geladene Solution/Compilation, nicht automatisch jede TFM-, Symbol- oder Generatorvariante. | `project`, `TFM/configuration` und Generated-Source-Provenienz im Snapshot ausweisen. | Ja für projektweite Completeness |
| Source Preview | **Technisch lösbar, aber semantisch/sicherheitlich riskant** | **Mittel.** `get_symbol_body` besitzt bereits begrenzte Körperausgabe; standardmäßiges Einbetten erhöht jedoch unbeabsichtigte Codeoffenlegung und Trunkierungsrisiken. | Außerhalb des ersten Scopes lassen; später explizit opt-in mit Herkunft und Trunkierungsmarkern. | Nein |
| Snapshot/Freshness Envelope | **Technisch lösbar, mit relevanter Architekturarbeit** | **Hoch.** Der Server hat Refresh-/Last-good-Zustand, aber keine atomare Analyse-Generation in der Feature-Antwort. | `serverInstanceId`, monotone Generation, `capturedAt`, `freshness`, `degradedReason` zusammen mit der Solution erfassen. | Ja |
| Violations-Teilfehler/Cancellation | **Technisch leicht lösbar, fachlich zwingend** | **Blocker.** Broad Catch maskiert Cancellation. | `OperationCanceledException` weiterwerfen; Fehler taxonomisieren. | Ja |
| Assembly-Ziele | **Derzeit nicht unterstützt** | **Mittel.** `[Schema-/Dokumentations-verifiziert]` `get_feature_context` ist projektbezogen. | Im Konzept ausdrücklich `project only` festhalten; Assembly-Verhalten nicht implizit offenlassen. | Nein |
| Parallele Sessions | **Grundsätzlich lösbar** | **Mittel.** Immutable Solutions helfen, Registry-/Refresh-Metadaten müssen jedoch zum Snapshot passen. | Concurrency-Test auf Generation, Degraded State und finalen Budgetwert ergänzen. | Ja für Freshness-Vertrag |
| Violations mit `noCache: true` | **Funktional, aber potentieller Latenzblocker** | **Hoch.** Jeder Feature-Aufruf startet den Linter ohne Cache. | Absolute Warm-/Cold-Latenz messen. Entweder Snapshot-Diagnostik wiederverwenden oder Violations bei Überschreitung als verzögert/unavailable kennzeichnen. | Ja für Anspruch „effizienter One-Shot“ |

### Audit 6: Budget- und Eval-Methodik

#### Befunde

| Schwere | Befund und Evidenz | Relevanz | Empfehlung | Blockiert? |
|---|---|---|---|---|
| **Hoch** | Bytebudget und Agentenkosten werden konzeptionell zu eng gekoppelt. `[Konzept-verifiziert]` | UTF-8-Bytes sind deterministisch, Tokens und Modellsichtbarkeit jedoch host- und tokenizerabhängig. | Bytes als Server-/Transportvertrag, Tokens als separate Evalmetrik. | Ja |
| **Hoch** | Die Aufgaben `Understand`, `Prepare-Edit` und `Refactoring` sind überlappend und lassen ihre korrekte Folgeaktion nicht eindeutig ableiten. | Ein Grader kann Schreibstil statt tatsächliche Entscheidung belohnen. | Szenarien über erwartete Aktion und benötigte Fakten definieren. | Ja |
| **Hoch** | Prozentgates haben keine Unsicherheitsbehandlung. | Kleine Datensätze erzeugen Scheingenauigkeit. | Gepaarte Auswertung, mehrere Trials, Konfidenzintervalle und vorab festgelegte Nichtunterlegenheitsgrenze. | Ja für Promotion |
| **Mittel** | Ein LLM-as-Judge wäre für symbolische Fakten unnötig und potentiell verzerrt. `[Externe Primärquelle]` Modellgrader sind skalierbar, aber nondeterministisch und müssen kalibriert werden. | Symbol, Count, Status und Toolfolge sind deterministisch prüfbar. | Regelgrader für Kernentscheidungen; Modellgrader nur für Erklärungsqualität. | Nein |

#### Empfohlener Evaluationsaufbau

1. **Primärziel:** korrekte Taskentscheidung und korrektes Endergebnis. Größe,
   Toolanzahl und Latenz sind Sekundärmetriken.
2. **Szenarioklassen:** Antwort allein möglich; `get_symbol_body` zwingend;
   Impact/Referenzanalyse zwingend; Tests/Violations separat nötig; Symbol
   mehrdeutig; Bereich fehlgeschlagen/stale/partiell; Negativfall mit tatsächlich
   nutzlosem Folgeaufruf.
3. **Gold Standard:** keine vollständige Musterprosa, sondern erwartete bzw.
   erlaubte Aktion, zwingende Fakten, verbotene Behauptungen, erlaubte
   Folgewerkzeuge, Sicherheitsklassifikation und task-spezifische
   Erfolgsprüfung.
4. **Unnötiger Folgeaufruf:** nur dann zählen, wenn bereits alle erforderlichen
   Signale vorlagen, das weitere Tool keine notwendige Information lieferte und
   der Task ohne den Aufruf korrekt abschließbar war.
5. **Schädliche Fehlentscheidung:** deterministisch definieren, etwa falsches
   Symbol, `keine Violations` bei `failed/unavailable`, Ignorieren von
   Trunkierung oder Nutzung eines stale Snapshot ohne Kennzeichnung.
6. **Stichprobe:** Pilot zur Varianzschätzung; anschließend erforderliche Fall-
   und Trial-Anzahl bestimmen. Baseline und Kandidat gepaart ausführen und die
   Reihenfolge randomisieren.
7. **Fixture-Mix:** synthetische Grenzfälle plus mehrere gepinnte reale
   Repositories; Multi-TFM, Generated Code, kaputte Compilation, große Fan-outs,
   unterschiedliche Testframeworks und Unicode-/Pfadfälle einschließen.
8. **Automatisierung:** pro PR deterministische Contract-/Serialisierungs-/
   Trunkierungs-/Cancellation-Tests; stabiler Performance-Slice; Agenteneval
   nightly oder manuell; Promotion nur nach Contract- und statistischen Gates.
9. **Budgetauswahl:** zuerst Pflichtanteil und Größenverteilung bestimmen. Der
   kleinste qualifizierte Kandidat gewinnt. Falls keiner qualifiziert ist,
   bleibt der heutige Default unverändert.
10. **Source/Callee-Gates:** erst später separat untersuchen. `+5 Prozentpunkte`
    und `-10 % Folgeaufrufe` sind derzeit unbelegte Zielwerte.

### Audit 7: Rückwärtskompatibilität

| Schwere | Befund und Evidenz | Relevanz | Empfehlung | Blockiert? |
|---|---|---|---|---|
| **Hoch** | Die aktuellen Listen, Feldformen, Markdown-Abschnitte und `null`-Semantiken sind beobachtbares Verhalten. `[Test-/Dokumentations-verifiziert]` | Eine kürzere Liste mit neuem Count ist für bestehende Konsumenten nicht automatisch kompatibel. | Legacy-Default unverändert einfrieren. Neue Statusfelder additiv ergänzen. | Ja |
| **Hoch** | Ein interner Evaluationspfad prüft keine realen MCP-Clients. | Text-only- und Structured-aware-Clients können unterschiedliche Ergebnisse sehen. | Für den Pilot ist ein expliziter öffentlicher Opt-in wie `responseProfile: "compact-v1"` sinnvoll. | Ja für End-to-End-Pilot |
| **Mittel** | Eine automatische spätere Default-Umschaltung ist trotz identischem Input-Schema eine Verhaltensänderung. | Agenten und Parser können auf Detailmenge und Markdown-Struktur angewiesen sein. | Version, Release Notes, Deprecation-Fenster und Rückfallmöglichkeit vorsehen. | Ja für Default-Umschaltung |
| **Mittel** | Additive JSON-Felder sind überwiegend kompatibel, zusätzlicher Text dagegen weniger sicher. | Textparser können Abschnittspositionen oder Formulierungen erwarten. | Legacy-Markdown im Default nicht verändern; neue Metadaten primär strukturiert ergänzen. | Nein |

Zwingend zu erhalten sind zunächst bestehende Input-Aliase und Flags,
Declaration- und Metrics-Felder, bestehende Caller-/Test-/Violation-Einträge im
Legacy-Profil, die derzeitige Bedeutung von `null` bei nicht angeforderten
Bereichen und die dokumentierten Markdown-Abschnitte. Counts, Status und
`isTruncated` sind gute Ergänzungen, aber kein kompatibler Ersatz für heute
ausgelieferte Einträge.

### Audit 8: Fehlende Punkte

| Schwere | Fehlender Punkt | Evidenz / Relevanz | Empfehlung | Blockiert? |
|---|---|---|---|---|
| **Blocker** | Cancellation-Vertrag | `[Code-verifiziert]` Abbruch kann als leerer Violationsreport erscheinen. | Expliziter Abschnitt im Konzept samt Testpflicht. | Ja |
| **Hoch** | Budget nach äußeren MCP-Wrappern | `[Code-verifiziert]` Degraded Header entsteht nach der Feature-Projektion. | Budgetgrenze auf endgültige Resultatstruktur beziehen. | Ja |
| **Hoch** | Absolute Latenzgrenze | Ein relatives `p95 <= +10 %` akzeptiert auch eine bereits unbrauchbar langsame Baseline. | Zusätzlich absolute Warm-/Cold-Ziele und Timeouts definieren. | Ja für One-Shot-Anspruch |
| **Hoch** | Configuration-/TFM-/Generator-Provenienz | Roslyn-Antworten gelten nur für die geladene Compilation. | Snapshot muss analysierte Konfiguration ausweisen. | Ja für Completeness |
| **Hoch** | Stabile Tie-Breaker | Aktuelle Reihenfolgen sind nicht vollständig normiert. | Deterministische Sortierspezifikation und Wiederholungstests. | Ja |
| **Mittel** | Source-Preview-Datenschutz | Codekörper können Secrets, proprietäre Logik oder generierte Inhalte enthalten. | Source weiterhin separat und opt-in halten; keine ungefragte Aufnahme. | Nein |
| **Mittel** | Große reale Fixture-Repositories | Synthetische Matrix bildet Verteilungen nicht ab. | Gepinnte OSS-Fixtures mit Lizenz-, Commit- und Toolchain-Metadaten. | Ja für Default-Promotion |
| **Mittel** | Fehlercodes und Diagnosefähigkeit | Freitextgründe sind schlecht stabil testbar. | Stabile Codes wie `source_unavailable`, `analysis_failed`, `stale_snapshot`, `budget_truncated`. | Ja |
| **Mittel** | Verhalten bei Assembly-Zielen | `get_feature_context` unterstützt diese derzeit nicht. | Explizit als Nichtziel festhalten. | Nein |
| **Mittel** | Release-/Rollback-Plan | Automatische Default-Auswahl allein beschreibt keinen sicheren Rollout. | Opt-in, Feedback soweit zulässig und dokumentierter Rollback auf Legacy. | Ja für Default |

### Audit 9: Minimal sinnvoller erster Scope

Der kleinste belastbare erste Scope ist noch keine automatische
Kontextkompaktion, sondern eine verlässliche Wahrheits- und Snapshot-Schicht.

#### Phase 1 – Truthfulness/Contract

- Legacy-Ausgabe unverändert lassen.
- Cancellation korrekt propagieren.
- Violations und andere Bereiche mit explizitem Status statt mehrdeutigem
  Leerwert versehen.
- Atomaren Snapshot-Identifier und Freshness-/Degraded-Metadaten ausgeben.
- Caller als Referenzen/Call Sites korrekt benennen oder klassifizieren.
- Alle Sammlungen vollständig deterministisch sortieren.
- Component- und Live-MCP-Tests für Fehler, Cancellation, Freshness und
  Text/Structured-Parität ergänzen.

#### Phase 2 – Compact Pilot

- Opt-in `compact-v1`.
- Gemeinsame kanonische Projektion für Text und Structured Content.
- Exaktes Budget am finalen Resultat.
- Counts, Trunkierung und Follow-up-Hinweise je Bereich.
- Keine Source Preview, keine Callees, keine automatische Default-Umschaltung.

#### Phase 3 – Agent Evaluation und Promotion

- Reale Host-/Client-Varianten.
- Gepaarte Multi-Trial-Evaluation.
- Statistisch definierte Promotion-Gates.
- Erst dann Entscheidung über den Default.

Die einzelne Änderung mit dem besten Risiko-/Nutzen-Verhältnis ist, den
pauschalen Exception-Catch bei Violations durch korrekte Cancellation- und
Statussemantik zu ersetzen. Sie beseitigt eine potenziell sicherheitsrelevante
Falschaussage, ohne den öffentlichen Detailumfang zu verändern.

### Audit 10: Konkrete Konzeptänderungen für die nächste Session

1. Budgetvertrag in `responseDataBytes` der vollständig serialisierten
   `CallToolResult`, diagnostische `textBytes`/`structuredBytes` und eine
   getrennte Evalmetrik `modelVisibleTokens` aufteilen.
2. Truthfulness/Contract, Compact Pilot und Default Promotion als getrennte
   Meilensteine definieren.
3. Source und Callees aus allen Kandidaten und Gates des ersten Scopes
   entfernen.
4. „Direct callers“ durch statische Referenz-/Call-Site-Beziehungen ersetzen
   und `relationKind` festlegen.
5. Pro Bereich mindestens `notRequested`, `complete`, `truncated`,
   `unavailable`, `failed` definieren; Treffermenge vom Status trennen.
6. Festlegen, dass `OperationCanceledException` nie zu einer partiellen
   Erfolgsantwort konvertiert wird.
7. Atomare Snapshotfelder für Instanz, Generation, Erfassungszeit, analysierte
   Konfiguration und Degraded State definieren.
8. Legacy als unveränderten Default festschreiben und einen versionierten
   opt-in Compact-Pfad für reale Evaluation zulassen.
9. Numerische Gates als vorläufige Hypothesen markieren; Stichproben-/Trial-
   Ermittlung, gepaarte Tests, Konfidenzgrenzen und regelbasierte Gold-
   Entscheidungen festlegen; absolute Latenzgrenze ergänzen.
10. Eval-Matrix um Multi-TFM, Preprocessor-Konfiguration, Source Generators,
    Generated Code, kaputte Compilation, Linked Files, Records/Accessors,
    Interface/Virtual/Delegate/Dynamic/Reflection und mehrere Testframeworks
    ergänzen.

### Audit 11: Entscheidung und Wiederfreigabebedingungen

**Status bleibt `draft`.** Eine erneute Freigabe ist erst sinnvoll, wenn:

- Server-/Transportbudget und Modellkontext getrennt sind;
- Cancellation, Partial Failure und Freshness einen widerspruchsfreien Vertrag
  besitzen;
- die äußere Degraded-Anreicherung Teil der Budget- und Paritätsprüfung ist;
- Caller nicht länger mit statischen Referenzen gleichgesetzt werden;
- der erste Scope auf den Contract-/Truthfulness-Slice reduziert ist;
- die Evaluation reale Fixtures, mehrere Trials, regelbasierte
  Gold-Entscheidungen und statistische Promotion-Gates besitzt;
- ein Legacy-kompatibler opt-in-Pilotpfad festgelegt ist.

### Geprüfte Evidenzanker

- `src/AiNetLinter/Mcp/Tools/FeatureContext/GetFeatureContextTool.cs`
- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextScanner.cs`
- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextModels.cs`
- `src/AiNetLinter/Mcp/Tools/FeatureContext/FeatureContextFormatter.cs`
- `src/AiNetLinter/Mcp/McpToolResults.cs`
- `src/AiNetLinter/Mcp/Projects/ProjectToolCall.cs`
- `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs`
- `src/AiNetLinter.FastTests/Mcp/Tools/FeatureContext/GetFeatureContextToolTests.cs`
- `Docs/agent-api.md`
- [MCP Tools Specification 2025-06-18](https://modelcontextprotocol.io/specification/2025-06-18/server/tools)
- [OpenAI Evaluation Best Practices](https://developers.openai.com/api/docs/guides/evaluation-best-practices)
- [Anthropic: Writing Effective Tools for Agents](https://www.anthropic.com/engineering/writing-tools-for-agents)
- [Anthropic: Demystifying Evals for AI Agents](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents)
- [Lost in the Middle](https://arxiv.org/abs/2307.03172)

### Nächster Planungsschritt

In der nächsten Session zuerst Phase 1 (Truthfulness/Contract) als kleinsten
Scope schärfen. Danach den Budgetvertrag und erst anschließend die Eval-
Methodik behandeln. Source, Callees und Default-Promotion bis dahin nicht
erneut in den ersten Scope aufnehmen.

### Planer-Einordnung nach dem Frontier-Audit

Diese Einordnung ergänzt den Audit als Diskussionsgrundlage und ersetzt oder
verändert dessen Befunde nicht.

Das bestehende System funktioniert. Das Vorhaben ist eine Optimierung eines
brauchbaren Werkzeugs, keine grundlegende Rettung einer unbrauchbaren
Architektur. Der Frontier-Audit hat wichtige Risiken sichtbar gemacht, aber
aus einer breiten Prüfbitte fast jede denkbare Unsicherheit als Blocker
behandelt. Dadurch ist der Text für eine Umsetzung deutlich größer geworden
als der ursprünglich zu lösende Nutzen rechtfertigt.

Die Befunde sind daher unterschiedlich zu gewichten:

- **Unmittelbar realer Fehler:** Der pauschale Exception-Catch beim
  Violations-Scan kann Fehler und Cancellation als leeres Ergebnis darstellen.
  Das ist ein sinnvoller, kleiner und separat verifizierbarer Fix, aber kein
  Grund, die gesamte Kontextarchitektur neu zu entwerfen.
- **Reales Änderungsrisiko:** Eine Verdichtung des heutigen Defaults kann
  beobachtbares Verhalten verändern. Der aktuelle Default sollte deshalb bis
  zu einem konkreten Nachweis unverändert bleiben.
- **Valide, aber nicht blockierende Modellfrage:** Bytes, StructuredContent
  und tatsächlich modell-sichtbare Tokens sind unterschiedliche Größen. Für
  eine erste technische Projektion genügt eine klar benannte Byte-Messung als
  Engineering-Signal; eine vollständige Host-/Client-/Tokenizer-Forschung ist
  dafür nicht erforderlich.
- **Überdehnung des Scopes:** Gepinnte externe Repositories, Human-Grading,
  statistische Promotion, vollständige Caller-/Callee-Taxonomien, Multi-TFM-
  und Generator-Matrizen sowie eine automatische Default-Promotion sind
  wertvolle spätere Prüfungen, aber keine notwendige Vorbedingung für eine
  kleine reversible Verbesserung.

Für eine spätere Wiederaufnahme gilt daher die engere Empfehlung:

1. Den bestehenden Default nicht verändern.
2. Keine Source-, Callee-, Multi-Symbol- oder Snapshot-Großarchitektur in den
   ersten Änderungsscope aufnehmen.
3. Zuerst höchstens eine kleine, intern oder opt-in prüfbare Projektion der
   vorhandenen Antwort gegen den Legacy-Output vergleichen.
4. Nur wenn diese Projektion einen klaren Nutzen bei vertretbarem Risiko zeigt,
   über einen produktiven Default oder weitere Datenquellen sprechen.
5. Den Violations-Cancellation-/Fehlerstatus bei Bedarf als eigenes kleines
   Thema behandeln, nicht als versteckte Voraussetzung für alles andere.

Der numerische Budgetwert muss erst existieren, wenn eine konkrete Projektion
gemessen werden kann. Vorher ist jede Zahl — auch ein Kandidatenraster — nur
eine Hypothese. Nach Beginn einer Umsetzung kann die Codebasis den Wert
automatisch aus festen synthetischen Fixtures und wenigen Dogfood-Fällen
ermitteln. Der Nutzer muss daraus keine Fremdrepository-Audits ableiten.

### Einordnung der Modellwahl

Das Ergebnis ist nicht ausschließlich ein Modellproblem. Ein Frontier-Modell
optimiert eine breit formulierte Auditaufgabe konsequent und listet deshalb
auch seltene Randfälle, zukünftige Betriebsfragen und wissenschaftliche
Absicherungen auf. Das ist für einen Red-Team-Review nützlich, aber als
Erstentwurf eines kleinen Features leicht überdimensioniert.

Für dieses Vorhaben wäre ein pragmatischer Modellmix sinnvoll:

- schnelles Modell für einen begrenzten ersten Entwurf und lokale
  Code-/MCP-Messungen;
- Frontier-Modell nur für einen gezielten Red-Team-Audit mit der Vorgabe
  „maximal fünf echte Blocker und keine neue Roadmap“;
- danach wieder ein kompaktes Modell zur Konsolidierung in einen kleinen,
  umsetzbaren Scope.

Der Lernpunkt ist daher nicht, dass das schnelle Modell automatisch besser
gewesen wäre. Wahrscheinlich hätte es weniger Randfälle ausgearbeitet und
damit schneller zu einer Entscheidung geführt. Die entscheidende Steuerung
ist der Auftrag: „kleinste reversible Änderung, bestehendes Verhalten
schützen, nach einem automatischen Testlauf abbrechen“, nicht „alle denkbaren
Risiken vollständig untersuchen“.
