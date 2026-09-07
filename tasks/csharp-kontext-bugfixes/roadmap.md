status: executing
current_epic: csharp-feature-context-bugfixes
last_checkpoint: planning
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
- Status: in_progress

## Abschluss-Checkliste

- [ ] Fehlerstatus und Cancellation-Semantik
- [ ] Tokenisierte und abbrechbare Caller-Suche
- [ ] Deterministische Caller-/Violations-Reihenfolge und Truncation
- [ ] Begrenzte Testmethoden-Ausgabe
- [ ] Vorsichtige statische Referenzsemantik
- [ ] Strukturierte Freshness-/Degraded-Hinweise
- [ ] Live-MCP-Vertragstest
- [ ] Dokumentation synchronisiert
- [ ] Abschluss-Gates grün
