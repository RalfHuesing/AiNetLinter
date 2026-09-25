# Roadmap: Kompakter Abruf von Verify-Advisories

Verbindlicher Vertrag: [Konzept.md](Konzept.md). Die Punkte laufen seriell.
Prüfzeitpunkt, Gate-Reihenfolge, Testauswahl und Commits folgen den
Projektregeln; ein erwartungsgemäß roter Regressionstest ist der
ausdrücklich beauftragte Zwischennachweis vor Produktionscode.

- [x] **1. Produktunabhängigen Rot-Vertrag nachweisen**
  - Intention: Der neue MCP-Abruf und seine Ausgabegrenzen sind durch
    synthetische xUnit-v3-Tests beschrieben, bevor Produktcode geändert wird.
  - Scope: Den bestehenden Rot-Test in
    `VerifyToolContractE2ETests.cs` vom bloßen Discovery-Check auf
    `get_verify_advisories(targetPath, category="dead_code")` umstellen.
    Kleine Fixture: Aufruf ohne vorheriges `verify`, alle Kandidaten unter
    64 KiB, Gesamt-/Usage-Zähler, eindeutige und über Symboltools
    navigierbare IDs. Große Fixture: mindestens 714 Kandidaten mit
    realistischen Namen und Pfaden, 64-KiB-Grenze, nur ganze Einträge,
    `shown + truncatedBy = candidates` und sichtbarer Kürzungshinweis.
    Den gezielten Testlauf und die konkrete erwartete Rot-Ursache belegen.
  - Nicht: Kein Produktionscode, keine Abhängigkeit von AiNetLinter,
    SqlToAi, KnowHowToAI oder SAN als untersuchter Produkt-Solution.
  - Abnahme: Tests kompilieren; mindestens der neue Tool-Vertrag läuft
    gezielt rot, nachdem seine Fixture-/Vorbedingungen bestanden haben.
    Keine bestehenden Tests werden abgeschwächt.
  - Nachweis: `dotnet build AiNetLinter.slnx` kompiliert beide Tests erfolgreich. `dotnet test src/AiNetLinter.IntegrationTests/AiNetLinter.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~GetVerifyAdvisories_"` führte beide gezielt rot mit `Unknown tool: 'get_verify_advisories'` aus. Vor der Vertragsassertion bestätigt `verify` jeweils den bestandenen synthetischen Changes-Scan: 31 Kandidaten im kleinen Fall und mindestens 714 im Größenfall; beide Aufrufe des Advisory-Tools erfolgen davor. Der Lösungsscan des neuen Tools darf zusätzlich unveränderte Baseline-Kandidaten enthalten. Der Größenfall prüft einmalige Spaltenbezeichnungen, kompakte vollständige Einträge, eindeutige IDs sowie Budget und Kürzungszähler.

- [x] **2. MCP-Abruf und kompakte Darstellung implementieren**
  - Intention: Ein eigener Aufruf liefert bei Bedarf die Dead-Code-Liste
    aus einem neuen Scan, ohne das Gate oder Magic Values erneut auszuführen.
  - Scope: Tool registrieren; nur `category=dead_code` akzeptieren;
    dieselbe Solution-weite Dead-Code-Erkennung, Policy und Handoff-Logik
    wie `verify(solution)` verwenden. Kandidaten deterministisch ordnen,
    nach Projekt und Datei gruppieren, einmalige Spaltenlegende und nur
    die Felder des Konzepts ausgeben. Antwort auf 65.536 UTF-8-Bytes
    begrenzen, vollständige Einträge projizieren und Zähler sowie
    Kürzungshinweis konsistent halten. Konfigurations-, Scan- und
    Größenfehler eindeutig und ohne falschen Clean-Claim ausgeben.
    `verify` ausschließlich um den Hinweis bei Dead-Code-Trunkierung
    ergänzen. Rot-Tests grün stellen und fokussierte Tests für ungültige
    Kategorie, leere Liste, `partial`, Linked Files, Content-only und
    Verify-Gate-Semantik ergänzen.
  - Nicht: Kein Cache, Cursor, Paging, Filter, weitere Advisory-Kategorie,
    Dead-Code-Heuristik oder geändertes Verify-Gate.
  - Abnahme: Kleine Fixture ist vollständig und navigierbar; große
    Fixture bleibt innerhalb von 64 KiB und meldet jede Auslassung;
    Tool funktioniert ohne vorheriges `verify`. Relevante Tests und
    projektseitige Gates bestehen, abgesehen vom ausdrücklich roten
    Zwischenstand aus Punkt 1.

- [x] **3. Agentenvertrag und Dokumentation synchronisieren**
  - Intention: Ein Agent entdeckt das Tool und versteht den sparsamen
    Aufruf, die Kürzungsgrenze und die Pflicht zur Einzelprüfung.
  - Scope: Kurze, prägnante MCP-Toolbeschreibung; `Docs/mcp/tools.md`
    und `.agents/rules/AiNetLinter-McpWorkflow.mdc` an den implementierten
    Vertrag anpassen.
  - Nicht: Keine neue Kategorie, keine zusätzliche Bedienoberfläche und
    keine Änderungen an `README.md`.
  - Abnahme: Tooltext, Dokumentation und Agentenregel nennen dieselben
    Parameter, denselben 64-KiB-/Trunkierungsvertrag und dieselben
    nutzbaren Symbol-Folgeaufrufe; keine widersprüchliche aktuelle
    Anleitung bleibt stehen.

- [ ] **4. Abschlussaudit**
  - Intention: Unabhängig prüfen, ob der freigegebene Vertrag vollständig
    und ohne unnötige Ausgabe- oder Gate-Änderungen umgesetzt ist.
  - Scope: Read-only-Diff- und Vertragsaudit gegen [Konzept.md](Konzept.md),
    Rot-/Grün-Nachweise, vollständiges Abschlussgate gemäß Projektregeln,
    Test der Antwortgröße und Status/Zähler bei vollständiger wie
    gekürzter Liste. Nur konkrete Abweichungen als Korrekturauftrag
    zurückgeben; danach höchstens ein gezielter Korrekturpunkt.
  - Nicht: Keine neuen Features oder stillen Konzeptänderungen im Audit.
  - Abnahme: Konzept-Muss und -Nicht sind belegt; Build, Solution-Verify
    und FastTests sind grün, die ausdrücklich beauftragten gezielten
    Integrationstests ebenfalls. Offene Grenzen sind benannt.
