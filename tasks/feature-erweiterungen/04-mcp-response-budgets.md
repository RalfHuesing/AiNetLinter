# 04 – MCP-Response-Budgets und Vollständigkeit

**Priorität:** P1  
**Zielstatus:** umsetzen  
**Abhängigkeiten:** 01 und bestehende Assembly-/MCP-Budgetmodelle

## Ziel und Nutzen

MCP-Antworten müssen praktisch nutzbar, deterministisch begrenzt und trotzdem informativ sein. Große Projekte dürfen weder unbounded Antworten erzeugen noch wichtige Informationen stillschweigend verlieren.

## Vertrag

Jedes gebündelte Ergebnis erhält, soweit relevant:

- angewendetes Budget;
- isComplete;
- ausgelassene Bereiche oder Elemente;
- Anzahl gefundener und ausgegebener Elemente;
- Grund der Begrenzung;
- optional ein Fortsetzungstoken oder eine reproduzierbare Fortsetzungsanfrage.

Das Budget gilt für die gesamte MCP-Antwort. Sicherheits- und Transportlimits haben Vorrang vor Nutzerlimits.

## Priorisierung

Default-Reihenfolge für C#-Kontext:

1. eindeutige Symbolidentität und Deklaration;
2. direkte sichere Beziehungen;
3. relevante Violations und Tests;
4. inferierte Beziehungen mit Provenance;
5. Quellcodeausschnitte;
6. entfernte oder schwach relevante Nachbarschaft.

Für Impact werden geänderte Symbole, Call-Sites und betroffene Tests vor optionalen Zusatzdateien priorisiert.

## Determinismus

- Gleicher Snapshot, gleiche Anfrage und gleiche Optionen liefern gleiche Reihenfolge.
- Tie-Breaking erfolgt über stabile Symbol-ID und Dateiposition.
- Limits werden vor Serialisierung angewendet.
- Nicht angeforderte Bereiche werden nicht wegen freien Budgets automatisch ergänzt.
- Sensible Inhalte und Konfigurationswerte bleiben ausgeschlossen.

## Fortsetzung

Ein Fortsetzungstoken ist nur erforderlich, wenn eine sinnvolle zweite Seite reproduzierbar erzeugt werden kann. Es muss an Snapshot, Anfrage, Optionen und Ablauf gebunden sein. Ist das nicht sicher möglich, wird die Auslassung präzise zusammengefasst statt scheinbares Paging anzubieten.

## Akzeptanzkriterien

- Keine relevante Project-MCP-Antwort überschreitet ihr effektives Serverlimit.
- Truncation ist maschinenlesbar und menschenverständlich.
- Vollständig, leer, nicht angefordert und ausgelassen bleiben unterscheidbar.
- Auswahl und Reihenfolge sind bei identischem Snapshot deterministisch.
- Große Lösungen bleiben innerhalb definierter Laufzeit- und Speichergrenzen.
- Ein Fortsetzungstoken kann nicht auf einen anderen Snapshot angewendet werden.

## Verifikation

- UnitTests für Budgetgrenzen, Priorisierung und stabile Tie-Breaks.
- Grenzwerttests für leere, kleine und extrem große Ergebnisse.
- IntegrationTests mit großen künstlichen Solutions.
- MCP-Regressionstest für bestehende Continuation-/Envelope-Verträge.

