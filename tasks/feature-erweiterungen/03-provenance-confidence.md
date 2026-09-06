# 03 – Provenance und Confidence für Beziehungen

**Priorität:** P1  
**Zielstatus:** umsetzen  
**Abhängigkeiten:** 01 und bestehende Symbolgraph-/Heuristikmodelle

## Ziel und Nutzen

Agenten und nachgelagerte Tools müssen erkennen können, ob eine Beziehung direkt aus Roslyn stammt oder aus einer kontrollierten Heuristik abgeleitet wurde. Unsicherheit soll sichtbar sein, ohne sichere semantische Ergebnisse unnötig zu verwässern.

## Herkunftsklassen

Empfohlenes Mindestmodell:

- RoslynSemantic: durch Symbol-, Syntax- oder SemanticModel-Auswertung belegt;
- RoslynPattern: Roslyn-basierte, fachlich begrenzte Mustererkennung;
- FrameworkInference: frameworkbezogene Inferenz mit expliziter Evidenz;
- ConventionInference: Namens-, Ordner- oder Konventionsannahme;
- TextualFallback: reine Textsuche oder schwacher Fallback;
- Unresolved: erwartete Beziehung konnte nicht sicher aufgelöst werden.

Die Namen dürfen an bestehende AiNetLinter-Verträge angepasst werden. Entscheidend ist die Trennung der Bedeutungen.

## Beziehungseigenschaften

Soweit öffentlich ausgegeben, enthält eine Beziehung:

- Quelle und Ziel als stabile Symbolidentitäten;
- Beziehungstyp;
- origin;
- confidence im dokumentierten Wertebereich;
- konkrete Evidenz, etwa Symbol-ID, Attribut, Invocation oder Registrierungsstelle;
- Position im Quelltext;
- optionalen Grund für verbleibende Unsicherheit.

## Confidence-Regeln

- Roslyn-semantic darf nicht durch eine pauschale niedrige Heuristikschwelle verborgen werden.
- Confidence ist ein dokumentierter Vertrauensgrad, keine Wahrscheinlichkeit ohne empirische Grundlage.
- Schwellenwerte werden anhand positiver und negativer Testfälle festgelegt.
- Niedrige Confidence darf als Hinweis erscheinen, aber keine Diagnose mit hoher Sicherheit auslösen.
- Nicht auflösbare Beziehungen sind kein negativer Beweis.

## Ownership und Lebenszeit

Die Analysekomponente, die eine Beziehung erzeugt, besitzt ihre Evidenz. Beim Zusammenführen in MCP-Ergebnisse darf Provenance nicht verloren gehen. Serialisierung, Caching und Truncation erhalten die Herkunft oder melden deren Auslassung ausdrücklich.

## Akzeptanzkriterien

- Sichere C#-Referenzen tragen nachvollziehbare Roslyn-Herkunft.
- Jede neue heuristische Kante ist als solche markiert.
- Sichere und inferierte Beziehungen können getrennt gefiltert werden.
- Inferierte Kanten enthalten Evidenz und dokumentierten Confidence-Wert.
- Fehlende Auflösung ist von einer sicher negativen Beziehung unterscheidbar.
- Bestehende origin-, confidence-, generation-, status- und completeness-Felder werden wiederverwendet, wo sie fachlich passen.

## Verifikation

- Roslyn-Tests für Overloads, Partial Types, Accessors und generische Symbole.
- Positive und negative Tests für DI- und Routing-Inferenz.
- Serialisierungs- und Truncation-Tests, die Provenance erhalten.
- Regressionstest: Eine heuristische Beziehung darf nicht als sichere Referenz erscheinen.

