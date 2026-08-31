# MCP-Live-/Vertragsaudit – neutrale Code-Map

## Scope

Gezielter Audit der MCP-Verträge für Projekt- und Assembly-Ziele im Bereich
Health, Dateibaum, Indexabdeckung und registrierter Tool-/Schema-Sicht.
Untersucht werden ausschließlich Wire-Verhalten, Zielverträge,
Fehlerklassifikation, Health-Projektion, Diagnosebegrenzung und
Token-/Antwortbudgets aus agentischer Sicht.

## Einstiegspunkte und Toolgruppen

- `get_server_health`: aggregierter Aufruf ohne Ziel sowie projekt- und
  assemblygebundene Varianten; Standard- und Diagnoseoptionen inklusive
  Grenz-/Fehlerparameter.
- `get_file_tree`: Root-`summary` und token-effizienter Root-`tree` gemäß
  MCP-Regel.
- `get_index_scope`: Indexabdeckung für das Projektziel sowie, sofern
  registriert und zulässig, das Assemblyziel.
- registrierte Tool-/Schema-Sicht über die aktuelle MCP-Oberfläche, soweit
  direkt verfügbar.

## Anonymisierte Zielklassen

- `project target`: der im Projektroot registrierte Projektverbund.
- `repo-provided assembly`: eine lokal erzeugte bzw. bereitgestellte Assembly
  aus dem Arbeitsbestand.
- `installed vendor assembly A/B`: zwei installierte externe Assembly-Ziele,
  soweit lokal verfügbar.

## Bekannte Grenzen

- Es werden keine Produktions- oder Testquellen geändert und keine Builds oder
  Tests ausgeführt.
- Hersteller-, Produkt-, Repository-, URL- und konkrete DLL-Namen erscheinen
  nicht in Arbeitsbericht oder Committext; lokale absolute Pfade bleiben auf
  MCP-Aufrufe beschränkt.
- Assembly-Namen, externe Symbolnamen und Namespaces werden nicht als Bericht-
  evidenz verwendet.
- Ein Befund wird nur aus live beobachtetem Verhalten abgeleitet; ältere
  Task-Berichte werden nicht ungeprüft übernommen.
