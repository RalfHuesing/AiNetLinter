# 01 – C#-Kontext und Exploration

**Priorität:** P1  
**Zielstatus:** umsetzen  
**Abhängigkeiten:** 02 und 04

## Ziel und Nutzen

Ein Agent soll für ein C#-Symbol oder eine Änderung mit möglichst wenigen MCP-Aufrufen einen belastbaren Arbeitskontext erhalten. Der Kontext bündelt Deklaration, relevante Beziehungen und für Änderungen wichtige Hinweise.

## Ausgangslage

AiNetLinter verfügt bereits über get_feature_context, Symbol-/Referenztools und get_impact mit change-context-Daten. Diese Fähigkeiten sollen konsistent zusammenspielen. Es wird kein zweiter Index und kein zweites Symbolmodell eingeführt.

## Empfohlener Umfang

Die erste Version erweitert bestehende Verträge, statt zwingend ein neues öffentliches Tool zu schaffen.

Für ein bekanntes Symbol sollen optional enthalten sein:

- stabile Symbolidentität und Deklaration;
- Datei, Position, Namespace und relevante Metadaten;
- direkte Caller und Callees mit begrenzter Tiefe;
- betroffene oder zugeordnete Tests;
- relevante Linter-Violations;
- optionaler, budgetierter Quellcodeausschnitt;
- Analysegeneration und Vollständigkeitsstatus;
- Herkunft und Vertrauensgrad der Beziehungen.

Für eine Änderung kommen hinzu:

- geänderte Dateien und erkannte Symbole;
- relevante Call-Sites und Abhängigkeiten;
- betroffene Tests;
- relevante Violations;
- empfohlene Testkommandos, sofern sicher ableitbar.

## Nicht im ersten Umfang

- freie natürliche Sprache als primäre Symbolauflösung;
- unbeschränkte Projektzusammenfassungen;
- Gleichsetzung ähnlich benannter Symbole;
- Quellcodeausgabe ohne Größenbudget;
- Laufzeit- oder Reflection-Beziehungen ohne Inferenzmarkierung.

## Konzeptioneller Antwortaufbau

    snapshot
      analysisGeneration
      freshness
    subject
      symbolId
      displayName
    declaration
    relationships
      callers
      callees
    tests
    violations
    source
    completeness

Der konkrete öffentliche JSON-Vertrag muss an die bestehenden AiNetLinter-Modelle angepasst werden. Feldduplikate und parallele Namenskonventionen sind zu vermeiden.

## Semantische Regeln

- Symbolauflösung erfolgt zuerst über stabile Roslyn-Identität, nicht über Displaynamen.
- Partial Types und Accessors werden auf den fachlich korrekten Besitzer normalisiert.
- Beziehungen erhalten Richtung, Zielidentität, Position und begrenzte Traversaltiefe.
- Bei unbekanntem Symbol wird kein unsicherer Treffer als eindeutige Antwort ausgegeben.
- Leere Listen und nicht angeforderte Bereiche bleiben unterscheidbar.

## Akzeptanzkriterien

- Ein bekannter Symbolname liefert Deklaration, vorhandene Caller, Callees, Tests und Violations in einer gebündelten Antwort.
- Quellcode kann optional aktiviert werden und bleibt immer begrenzt.
- Jeder Datenbereich ist als vollständig, leer, nicht angefordert oder ausgelassen erkennbar.
- Ein nicht existentes Symbol erzeugt eine hilfreiche Fehlermeldung ohne erfundene Beziehungen.
- Bestehende Einzeltools liefern weiterhin kompatible Antworten.
- Ergebnisse aus einem veralteten Snapshot tragen den Status sichtbar im Envelope.

## Verifikation

- UnitTests für Modellkombination, leere Bereiche, symbolische Eindeutigkeit und Traversalgrenzen.
- ComponentTests mit kleinen Roslyn-Solutions für Partial Types, Accessors, Overloads und Testbeziehungen.
- IntegrationTest über MCP-JSON einschließlich großer Antwort und Truncation.
- MCP-Abfrage gegen AiNetLinter selbst mit bekanntem Symbol und kontrollierter Änderung.

