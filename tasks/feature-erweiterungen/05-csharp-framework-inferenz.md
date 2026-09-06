# 05 – Kontrollierte C#-Framework-Inferenz

**Priorität:** P2  
**Zielstatus:** später nach P1  
**Abhängigkeiten:** 02, 03, 04 sowie belastbare Roslyn-Muster und Messdaten

## Ziel und Nutzen

Bestimmte Beziehungen sind für Agenten wertvoll, obwohl sie nicht als direkter Methodenaufruf vorliegen. Dazu zählen beispielsweise DI-Registrierungen oder ASP.NET-Routen. Diese Beziehungen sollen ergänzt werden, ohne unbeweisbare Vollständigkeit zu behaupten.

## Empfohlene Reihenfolge

1. Dependency Injection: Registrierung zu Konstruktorparameter und Serviceimplementierung.
2. ASP.NET Core: Attribut-Routen und klar erkennbare Minimal-API-Mappings.
3. Danach weitere Frameworks oder Messaging-Muster, jeweils als eigenes Teilpaket.

## Technische Leitplanken

- Erkennung erfolgt auf Roslyn-Syntax und SemanticModel, nicht auf allgemeinem Regex-Scanning.
- Extension Methods, Attribute und konkrete Symboltypen werden berücksichtigt.
- Ordner- oder Namenskonventionen sind nur ergänzende Evidenz.
- Jede Beziehung wird mit FrameworkInference oder passender Herkunft markiert.
- Grenzen wie Reflection, dynamische Registrierung und Source Generators werden genannt.
- Framework-Erkennung ist opt-in oder klar begrenzt, wenn sie die Antwortkosten stark erhöht.

## Nicht-Ziele

- keine vollständige Laufzeit-Routenabdeckung;
- keine automatische Auflösung dynamischer DI-Fabriken;
- keine Interpretation beliebiger Stringkonfiguration als sichere Route;
- keine frameworkübergreifende Universalheuristik;
- keine Diagnose allein aufgrund einer schwachen Konvention.

## Akzeptanzkriterien

- Jede erkannte DI- oder Routing-Beziehung zeigt konkrete Roslyn-Evidenz.
- Falsch-positive Fälle sind durch negative Testfälle abgedeckt.
- Dynamisch nicht erkennbare Fälle werden als unbekannt oder unvollständig gemeldet.
- Framework-Inferenz kann von sicheren Symbolgraph-Beziehungen getrennt abgefragt werden.
- Antwortgröße und Laufzeit bleiben innerhalb der zentralen Budgets.
- Nicht passende Projekte erhalten keine unnötige Fehler- oder Warnflut.

## Verifikation

- Repräsentative C#-Fixtures je Frameworkmuster.
- Negative Fixtures für ähnlich benannte, aber nicht verbundene Typen.
- Tests für Attribute, Extension Methods, Generics, Partial Types und verschachtelte Scopes.
- IntegrationTests mit deaktivierter, aktivierter und fehlerhafter Inferenz.
- Messung von Precision und Recall auf einem festgelegten Fixture-Set vor einer Freigabe.

