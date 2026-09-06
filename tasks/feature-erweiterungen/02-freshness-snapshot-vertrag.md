# 02 – Freshness- und Snapshot-Vertrag

**Priorität:** P1  
**Zielstatus:** umsetzen  
**Abhängigkeiten:** bestehender Solution-/Refresh-Lebenszyklus

## Ziel und Nutzen

Jedes relevante MCP-Ergebnis muss erkennen lassen, auf welchem Analysezustand es beruht. Ein Agent darf nicht raten müssen, ob ein Ergebnis aktuell, während eines Refreshs erzeugt oder nur ein letzter bekannter Stand ist.

## Zustandsmodell

- loading: Solution oder Projektkontext wird initial geladen;
- fresh: Analyse entspricht dem geprüften Quellstand;
- updating: Änderungen werden verarbeitet;
- stale: Quellstand und Analysegeneration weichen ab;
- degraded: Refresh oder Watcher ist fehlgeschlagen, ein letzter guter Stand kann vorhanden sein.

Ein Zustand ist eine Aussage über den konkreten Zielkontext, nicht über den gesamten Daemon.

## Snapshot-Daten

Der gemeinsame Envelope soll enthalten, soweit verfügbar:

- Analysegeneration oder gleichwertige monotone Identität;
- Zeitpunkt des letzten erfolgreichen Refreshs;
- Quell-Fingerprint oder eindeutige Änderungsinformation;
- betroffene/geänderte Dateien;
- Zustand und optionale Statusdetails;
- Kennzeichnung eines letzten guten, aber veralteten Snapshots.

Der Fingerprint darf keine geheimen Inhalte nach außen geben. Ein Hash wird nur verwendet, wenn Lebensdauer und Vergleichssemantik klar sind.

## Aktualisierungsregeln

- Vor einer Analyse wird der vorhandene Staleness-Mechanismus genutzt.
- Ein teilweiser Refresh gilt nicht stillschweigend als vollständig erfolgreich.
- Nach fehlgeschlagenem Refresh darf der letzte gute Snapshot nur als stale oder degraded angeboten werden.
- Health darf keine vollständige fachliche Analyse vortäuschen.
- Watcher-Ereignisse sind Arbeitshinweise und ersetzen nicht die Prüfung des tatsächlichen Quellstands.

## Fallbacks

- Kein Snapshot: klarer Loading-/Not-Ready-Status, keine erfundenen Daten.
- Änderungen vorhanden: alter Kontext nur mit Stale-Hinweis.
- Einzelne Dateien fehlerhaft: übrige Ergebnisse mit betroffenen Dateien und unvollständigem Status.
- Watcher dauerhaft gestört: degraded, aber explizite On-Demand-Prüfung bleibt möglich.

## Akzeptanzkriterien

- get_feature_context, get_impact, find_references und weitere relevante Project-Tools liefern einen einheitlichen Snapshot-/Freshness-Envelope.
- Tests können loading, fresh, updating, stale und degraded reproduzieren.
- Ein alter Snapshot ist maschinenlesbar von einem frischen Ergebnis unterscheidbar.
- Ein teilweise fehlgeschlagener Refresh erzeugt keine globale Freshness-Behauptung.
- Mehrere Sessions auf demselben Projekt sehen konsistente Generationen.
- Der Daemon bleibt bei Refresh-Fehlern wiederverwendbar.

## Verifikation

- Zustandsautomat als FastTest.
- IntegrationTests mit Dateiänderung zwischen zwei MCP-Aufrufen.
- Test für Refresh-Fehler und Wiederaufnahme nach Fehlerbehebung.
- Test für konkurrierende Sessions und Generationen.

