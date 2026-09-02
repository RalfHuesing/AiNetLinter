# Roadmap: Konsolidierte Optimierung der dekompilierten Assembly-Analyse

Primäraufgabe: Behebe und konsolidiere die dekompilierte Assembly-Analyse gemäß dem freigegebenen Konzept.

Status: executing  
Current epic: Paket 2 – Korrekturrunde 1/5
Letzter Commit: Paket-2-Review-Checkpoint `51ac63ba`
Current debt item: `DOC-GET-IMPACT-INCLUDE-REFERENCES-SCOPE`
Debt attempts: 1
Blocker: keiner

## Epics

### Paket 1: Kritische Korrektheits-Bugs & Stabilität

- Ziel: Laufzeitfehler, Cache-Datenverlust und Resolver-/Proxy-Fehler in der Assembly-Pipeline beheben.
- Abhängigkeiten: freigegebenes `Konzept.md`; keine vorgelagerten Epics.
- Betroffene Bereiche: Body-Auflösung, Decompilation-Cache, Framework-Referenzen, Stable-ID-Auflösung, Daemon-Health-Routing.
- Muss-/Akzeptanzkriterien: Top-Level-Typen und Accessors liefern stabile Bodies oder typisierte `unavailable`-Ergebnisse; Publishing löscht kein erfolgreiches Cache-Verzeichnis; bekannte Framework-Assemblies werden versions-tolerant vereinheitlicht; Skeleton-IDs bleiben für Folgeabfragen auflösbar; Projekt-Health funktioniert im Daemon-Proxy.
- Verifikation: gezielte Tests für Resolver, Cache-Concurrency, Framework-Unification und Daemon-Health; nach letzter Codeänderung `get_violations` für den Scope.
- Status: done

### Paket 2: Tool-Verträge, Schemas & Entwicklerergonomie

- Ziel: Assembly-Tool-Schemas, Routing, Ausgabeformate, Defaults und Dokumentation an den vereinbarten Capability-Vertrag angleichen.
- Abhängigkeiten: Paket 1.
- Betroffene Bereiche: Assembly-/SymbolGraph-/FileStructure-/Maintenance-Registrierungen, Formatter, Instructions und relevante Dokumentation.
- Muss-/Akzeptanzkriterien: `includeReferences`, Assembly-`get_impact`, `filePath`-Alias, `metrics_tree`-Default, Assembly-Header, wahrheitsgetreue Hinweise/Trunkierungsflags und die 13-Tool-Capability-Matrix sind konsistent umgesetzt.
- Verifikation: Schema-/Dispatcher-/Ergonomie-Tests, Capability-Matrix-Prüfung und gezielter `get_violations`-Nachweis.
- Status: in_progress

### Paket 3: Token-Budget, Response-Limits & Dogfooding

- Ziel: Antwortdichte, Signature-Only-Kompilierung, transitive Context-Footprints und Referenz-Session-Lebenszeit verbessern.
- Abhängigkeiten: Paket 1 und Paket 2.
- Betroffene Bereiche: Assembly-Formatter, Stub-Erzeugung, Assembly-Navigation/Registry/Session-Komponenten.
- Muss-/Akzeptanzkriterien: Große Assembly-Antworten erhalten Typen und Member unter dem Response-Limit; Signature-Only erzeugt keine unnötige Fehlerflut; die betroffenen Produktionsklassen erfüllen `AIContextFootprint <= 2500`; temporäre Referenz-Sessions belasten den Speicher nicht unbegrenzt.
- Verifikation: Budget-/Großassembly-Tests, gezielter Linter-/MCP-Nachweis für `AIContextFootprint` und passende Regressionstests.
- Status: open

### Paket 4: Test-Matrix, Regressionen & Nachweise

- Ziel: Die vereinbarten Fast-/Integration-Regressionen ergänzen und den Gesamtstand abschließend nachweisen.
- Abhängigkeiten: Paket 1, Paket 2 und Paket 3.
- Betroffene Bereiche: FastTests, IntegrationTests und Abschlussnachweise.
- Muss-/Akzeptanzkriterien: Die spezifizierte Testmatrix deckt Core-Fixes, Tool-Verträge, Daemon-/Assembly-Routen und Response-Budgets ab.
- Verifikation: `dotnet build`; vollständige Nicht-Stress-Läufe beider Testprojekte; sauberer `get_violations`- und `safeguard`-Durchlauf über die eigene Solution; konzeptspezifische Prüfungen aus den Paketen.
- Status: open

## Abschluss-Checkliste aus dem Konzept

- [ ] `dotnet build` erfolgreich und warnungsfrei.
- [ ] `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` vollständig grün.
- [ ] `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` vollständig grün.
- [ ] Sauberer `get_violations`-Durchlauf über die eigene Solution.
- [ ] `safeguard`-Score über die eigene Solution mindestens 8.0/10.
- [ ] Paket-spezifische Resolver-, Cache-, Unification-, Tool-, Budget- und Daemon-Prüfungen ausgeführt.

Tech-Debt-Queue: siehe `tech-debt.md`.
