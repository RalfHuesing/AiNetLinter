status: done
current_epic: csharp-feature-context-bugfixes
last_checkpoint: final-verification
current_debt_item: none
debt_attempts: 0

# Ausführungsstand

Primäraufgabe: Klare Fehlerkorrekturen im C#-Feature-Kontext

## Arbeitspaket: csharp-feature-context-bugfixes

- Ziel: Die sechs im Konzept beschriebenen Fehler im projektgebundenen
  `get_feature_context`-Vertrag korrigieren.
- Abhängigkeiten: Konzept `Konzept.md` mit `status: ready`.
- Bereiche: Feature-Context-Scanner, DTOs, Formatter, Caller-Suche,
  Projektantwort-Wrapper, FastTests, Live-MCP-Vertragstests und betroffene Doku.
- Muss-/Akzeptanzkriterien: Konzept §§3 und 7 vollständig erfüllen; erfolgreicher
  leerer Legacy-Report bleibt kompatibel.
- Verifikation: gezielte Implementiererprüfungen, Review, Abschluss-Audit,
  `dotnet build`, vollständige Nicht-Stress-FastTests und IntegrationTests,
  mindestens ein Live-MCP-Vertragstest mit Text-/StructuredContent-Parität und
  Degraded-Fall.
- Status: done

## Abschluss-Checkliste

- [x] Fehlerstatus und Cancellation-Semantik
- [x] Tokenisierte und abbrechbare Caller-Suche
- [x] Deterministische Caller-/Violations-Reihenfolge und Truncation
- [x] Begrenzte Testmethoden-Ausgabe
- [x] Vorsichtige statische Referenzsemantik
- [x] Strukturierte Freshness-/Degraded-Hinweise
- [x] Live-MCP-Vertragstest
- [x] Dokumentation synchronisiert
- [x] Abschluss-Gates grün
