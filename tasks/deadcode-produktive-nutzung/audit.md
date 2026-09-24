# Audit: Dead Code nach produktiver Nutzung beurteilen

> **Aktueller Implementierungsstand:** Das damalige Audit bezog sich auf die
> Policy mit Pflichtwert `unknown`; diese Evidenz bleibt historisch. Aktuell
> verwendet ein fehlender Wert `closed_solution`. Gültig sind ausschließlich
> `closed_solution` und `external_library`; explizites `unknown` ist ungültig
> und wird nicht migriert.

## Ergebnis

Keine belegten Mängel gegen Muss-Kriterien oder Akzeptanz. Die drei Slices
liegen in den angeforderten Commits `2d0000c5`, `ed2cc5c9`, `570a663c` und
`7018dc11`; die Roadmap-Checkboxen für die Punkte 1–3 sind gesetzt.

## Geprüfte Evidenz

- Konzept und alle Roadmap-Punkte stimmen bei Referenzrollen, API-Policy,
  Verify-Projektion, Advisory-Ranking, Antwortbudget und Gegenprüfung überein.
- Die Roslyn-Implementierung klassifiziert jede Referenz anhand ihres eigenen
  Dokuments, zählt doppelte Fundstellen projektbezogen höchstens einmal und
  hält die Suche solutionweit. Sie unterscheidet `production`, `test` und
  `unknown`, einschließlich Interface-/Override-Symbolen und Linked Files.
  Siehe `DeadCodeAdvisoryScanner.cs` sowie die Tests in
  `DeadCodeReferenceRoleTests.cs`.
- `external_library` schützt die effektiv extern sichtbare Symbol- und
  Typkette. Das Preflight prüft die Kandidatenprojekte vor dem Gate und erzeugt
  bei fehlender oder ungültiger Entscheidung einen Content-only-Fehler ohne
  Teilergebnis. Projekt-Overrides und First-Match-Verhalten sind mit Tests
  belegt. Die mitgelieferte Konfiguration setzt `closed_solution`.
- `verify` ruft den Scanner für beide Scopes auf. Der Formatter erhält
  Kandidatenzähler, Test-only- und Unreferenced-Zähler sowie Trunkierung und
  Status auch dann, wenn Einträge gekürzt werden. Dead Code steht im
  Advisory-Ranking vor Magic Values; der Gate-Verdict bleibt davon unabhängig.
  Hinweis, Testreferenzzahl, Gegenprüfungstext und kopierbares Handoff-Handle
  sind implementiert. Linked Files mit gleicher Dokumentations-ID werden
  projektbezogen aufgelöst.
- FastTests belegen unter anderem Test-only, produktive Cross-Project- und
  Friend-Nutzung, Interface-/Basismethoden, unbekannte Rollen, API-Sichtbarkeit,
  Preflight, Whitelist/Suppression, Razor-Evidenz, beide Deklarationsscopes,
  Linked-File-Handoffs, Trunkierung und Content-only-Fehler.
- Der aktuelle Laufnachweis `TestResults/FastTests.trx` enthält
  `2705/2705` bestandene Tests; `TestResults/IntegrationTests.trx` enthält
  `243/243`. Die Integrationstests rufen `verify` gegen den gestarteten
  Testprozess auf und prüfen die neue Dead-Code-Antwort sowohl im
  `changes`- als auch im `solution`-Scope. Der Changes-Vertrag prüft zudem
  Kandidatenpriorität und `symbolIdentifier=h:...`.
- Die aktive MCP-Session ist geladen, liefert bei `verify(solution)` aber
  noch die ältere Antwort ohne `deadCode`-Abschnitt. Dieser Live-Aufruf wird
  daher nicht als Beleg für das neue Antwortformat gewertet. Der E2E-Test des
  aktuellen Codes liefert den dafür nötigen Laufzeitbeleg. `verify(changes)`
  im sauberen Working Tree ist erwartungsgemäß `incomplete` mit Recovery auf
  `solution`. `verify(solution)` im aktiven MCP meldet weiterhin
  `pass`, Score `10.0` und `0` Verstöße.
- Konfigurations-, CLI- und MCP-Dokumentation sowie MCP-Agentenregel nennen
  beide Scopes, Referenzsemantik, API-Werte, Fehlerverhalten, Priorisierung und
  die Grenze zwischen Advisory und Löschentscheidung.

## Findings

Keine.
