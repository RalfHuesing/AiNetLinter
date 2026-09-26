# Analysegrenzen und Interpretation

[Dokumentationsindex](index.md) · [Konfiguration](linter/configuration.md) · [MCP-Verträge](mcp/tools.md)

## Was ein Befund belegt

| Mechanismus | Implementierte Aussage | Grenze |
| --- | --- | --- |
| Roslyn-Symbolgraph | Statisch auflösbare Deklarationen, Typreferenzen, Call-Sites und Hierarchien im geladenen Snapshot | Dynamische Aufrufe, externe Consumer und Markup verlangen weitere Evidenz. |
| Größen-/Komplexitätsregeln | Konfigurierte Grenzwerte für Dateien, Methoden, Typen, Parameter und Kopplung werden überschritten | Ein Zahlenwert bestimmt weder die fachliche Verantwortung noch die richtige Zerlegung. |
| AI-Context-Footprint | Eigene/transitiv referenzierte Quelltypen tragen zum Zeilenumfang eines Typs bei | Keine Messung tatsächlicher Tokens oder Vorhersage einer Agentenfehlerrate. |
| `StaticTestSentinel` | Statische Präsenzzuordnung über Testnamen, `typeof` oder `@covers`, mit konfigurierten Ausnahmen | Keine Laufzeitabdeckung, kein Testlauf und kein Beweis für ausreichende Assertions. |
| `DuplicateCode` | Token-/N-Gram-Ähnlichkeit von Methoden; `exact`-Cluster werden als Verstoß gemeldet | Ähnlicher Text beweist keine fachliche Austauschbarkeit. |
| `find_duplicates(mode="structural")` | Strukturähnlichkeit als Kandidatencluster | Keine automatische Lint-Violation oder sichere Refactoring-Anweisung. |
| `CompoundSuppressions` | Konfigurierte Metrikbedingungen können Limits lockern, Meldungen unterdrücken oder Severity ändern | Konfigurationsentscheidung, kein Nachweis fachlicher Harmlosigkeit. |
| Dead-Code-Advisories | API-Policy, Referenzrollen und erkannte Bindungskanäle ergeben Kandidaten oder Unentscheidbarkeit | Keine globale Erreichbarkeitsanalyse; [konkrete Grenzen](mcp/dead-code.md). |
| Assembly-Analyse | Metadaten und dekompilierte Roslyn-Dokumente | Keine Ausführung des Zielprogramms; Dekompilat und Diagnoseabdeckung berücksichtigen. |

## Restore-Erkennung

`ProjectRestoreState` erkennt fehlende/veraltete Restore-Daten. `PROJECT_NOT_RESTORED` benennt das betroffene Projekt. Der Linter führt keinen automatischen Restore aus. Bei erkanntem Restore-Problem unterdrückt die Phantom-Dependency-Prüfung Folgefehler für unauflösbare `using`-Direktiven dieses Projekts; der Konfigurationsschalter muss für diese Regel aktiv sein.

Belege: [ProjectRestoreState](../src/AiNetLinter/Baseline/ProjectRestoreState.cs), [GlobalConfig](../src/AiNetLinter/Configuration/GlobalConfig.cs).

## Befunde verwenden

Grenzwerte und Ausnahmen im effektiven Regel-Snapshot nachsehen. Vor einer Änderung betroffene Bodies, Referenzen und Verträge prüfen. Ein leeres Ergebnis bei unvollständigem Snapshot oder partiellem Scan ist kein Negativbeweis. Gate, statische Testzuordnung und Advisories beantworten unterschiedliche Fragen; ihre Ergebnisse nicht ineinander umdeuten.
