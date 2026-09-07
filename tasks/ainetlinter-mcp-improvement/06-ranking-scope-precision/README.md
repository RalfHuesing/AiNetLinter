# 06 – Ranking, Scope & Precision

## Ziel

Ein Agent soll aus Ergebnissen keine riskante fachliche Entscheidung ableiten, weil Ranking, Scope oder Heuristik die falschen Treffer nach vorne stellt.

## Scope

- Produktion/Test/Artefakte deterministisch und sichtbar unterscheiden;
- Scope-Filter auf Score, Top-Befunde und Completeness gleichermaßen anwenden;
- Kandidaten, Heuristik und echte Violations visuell trennen;
- Partial-Klassen, Attribute, DI/Reflection und lokale Sprachkonstrukte konservativ behandeln;
- Security-/Refactoring-Empfehlungen nur bei belastbarer Kategorie ausgeben.

Gefährliche Präzisionsfehler gehören in die erste Lieferung: Produktions-/Test-Scope, scoped Top-Befunde, versteckte Null-Kategorien, Dead-Code-Confidence, Magic-Value-Kategorien und Duplicate-Scope. Eine vollständige Neuentwicklung der zugrunde liegenden Heuristiken ist dagegen kein Muss.

## Ausgangsbefunde

`find_dead_code`, `find_duplicates`, `find_magic_values`, `pattern_detect`, `safeguard` sowie Ranking-/Scope-Teile aus `get_impact`, `get_feature_context` und `combo-impact-lint`.

## Voraussichtliche Quellbereiche

`Tools/DeadCode/*`, `DuplicateDetection/*`, `MagicValues/*`, `PatternDetect/*`, `Safeguard/*`, `Metrics*`, `TestCoverageScanner` und gemeinsame Scope-/Path-Filter.

## Abhängigkeiten

Die grundlegende Identitäts- und Completeness-Semantik aus 02/03; Änderungen an Regeln selbst sind kein implizites Ziel.

## Abnahme

- Tests werden nicht als Produktions-Impact verkauft;
- scoped Quality-Gates zeigen nur scoped Top-Befunde;
- leere Pattern-Kategorien sind sichtbar oder ausdrücklich als nicht geprüft markiert;
- Kandidatenlisten enthalten Confidence und keine Löschanweisung ohne Einschränkung.
