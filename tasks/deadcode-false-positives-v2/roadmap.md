# Roadmap: Dead-Code-Advisories mit kleinem, prüfbarem Signal

Verbindlicher Zielvertrag: [Konzept.md](Konzept.md). Punkte strikt nacheinander bearbeiten. Jeder Umsetzungspunkt umfasst seine passenden xUnit-v3-Tests, das nach `.agents/rules/AiNetLinter-Richtlinien.mdc` fällige Incremental Gate, das Abhaken und einen eigenen Commit. Belegte Bugs zuerst mit einem isolierten Rot-Test reproduzieren. Keine Planner-Dateien ändern und keine Integrations- oder Stresstests ohne ausdrücklichen Auftrag starten.

- [x] **1 — Kandidaten auf Typen und gewöhnliche Methoden verengen**
  - Intention: Die breite Liste ungelesener Daten-Member und `test_only`-Meldungen aus der Standardanalyse entfernen.
  - Scope: In `DeadCodeAdvisoryScanner`, `DeadCodeFilters`, `DeadCodeUsageIndex` und zugehörigen FastTests nur explizite Typen und gewöhnliche Methoden aus produktiven Projekten als Kandidaten zulassen. Testreferenzen als Nutzung zählen; unbekannte Projekt-/Referenzrollen unterdrücken Kandidaten. Ganze tote Typen weiter nur einmal melden. Bestehende `closed_solution`-/`external_library`-Policy und Solution-weite Referenzprüfung erhalten.
  - Nicht: Feld-/Property-Leserkennung verbessern, Testprojekt-Deklarationen melden oder Ausgabeformate bereits umstellen.
  - Abnahme: Tests belegen einen referenzlosen Typ und eine tote Überladung als Treffer; Tests.Support, nur aus Tests referenzierter Produktionscode, Wire-Properties, Konstanten und private Felder fehlen. Partielle Referenzabdeckung erzeugt keinen sicheren Negativbefund. Incremental Gate grün.

- [x] **2 — Methodengruppen und Kontraktmethoden richtig behandeln**
  - Intention: Statisch gebundene Einstiege nicht für tot erklären und gefährliche Einzelhinweise zu Verträgen vermeiden.
  - Scope: Semantische Referenzerfassung in `DeadCodeUsageIndex` für aufgelöste Methodengruppen einschließlich Delegate-Konvertierung und Überladungen korrigieren. Interface-Member, implizite und explizite Implementierungen sowie Overrides aus der Einzelmethodenliste ausschließen. Benötigte konservative Schutzsignale aus `DeadCodeIndirectUsage` und verwandten Klassen für Typen/Methoden erhalten.
  - Nicht: `MapGet`-/`MapPost`-Namenslisten, Framework-Sonderfälle oder eine allgemeine Interprocedural-Analyse hinzufügen.
  - Abnahme: Ein Rot-Test für den belegten `AuthJwtSigningDisabled`/`MapPost`-Fall wird grün; Tests decken aufgelöste Überladung, mehrdeutige Bindung, `JsonConverter.Read`/`Write` und Interface-Slots ab. Eine gewöhnliche unreferenzierte Nicht-Kontraktmethode bleibt Kandidat. Incremental Gate grün.

- [x] **3 — Einstiegspunkt-Attribute konfigurierbar schützen**
  - Intention: Bekannte statische und JS-Einstiege sowie Projekt-Plugins ohne projektspezifische Engine-Regeln schützen.
  - Scope: `DeadCodeWhitelist`, `DeadCodeConfig`, Konfigurationsnormalisierung und Tests um `DeadCode.EntryPointAttributes` ergänzen. Die zwei festen Defaults `System.Runtime.CompilerServices.ModuleInitializerAttribute` und `Microsoft.JSInterop.JSInvokableAttribute` behalten; zusätzliche vollqualifizierte Attributtypen additiv und per semantischer Typidentität auswerten. Die markierte Methode und ihren deklarierenden Typ schützen. Konfiguration und Beispiel in `Docs/linter/configuration.md` und `ainetlinter-rules.json` synchronisieren.
  - Nicht: Semantic Kernel, MudBlazor oder andere Drittanbieterattribute fest einbauen.
  - Abnahme: Tests belegen beide Defaults, ein konfiguriertes `Microsoft.SemanticKernel.KernelFunctionAttribute`, die additive Wirkung und ein gleichnamiges fremdes Attribut ohne Schutz. Incremental Gate grün.

- [x] **4 — `verify` auf Dead-Code-Kurzmeldung begrenzen**
  - Intention: Das Quality-Gate auch bei vielen Kandidaten knapp und eindeutig halten.
  - Scope: Projektion/Formatter in `VerifyAdvisoryProjector` und `VerifyTool` so ändern, dass Dead Code nur Status, beobachtete Zahl, Abdeckung/Ursache und Abrufhinweis erhält. Keine Dead-Code-Einzelzeilen, `next=review_now`, Confidence- oder `test_only`-/`undecidable`-Zähler veröffentlichen. `evidence: returned=X/Y` zählt danach Gate-Verstöße und die weiterhin einzeln ausgegebenen anderen Advisories, keine Dead-Code-Kandidaten. Gate-Evidenz und andere Advisory-Kategorien einschließlich Budgetvorrang erhalten; relevante Formatter-FastTests und `Docs/mcp/tools.md` anpassen.
  - Nicht: Gate-Verdict, Score, `violationCount` oder andere Advisory-Kategorien fachlich ändern.
  - Abnahme: Vertragstests prüfen die Kurzmeldung mit vorhandenem/fehlendem Snapshot, vollständigem/partiellem/nicht verfügbarem Scan und null beobachteten Kandidaten; Gate-Zahlen und andere Advisories bleiben korrekt. Incremental Gate grün.

- [ ] **5 — Detailabruf und Snapshot-Fortsetzung sichern**
  - Intention: Details ohne erneuten teuren Scan aus einem passenden Verify-Snapshot abrufen und Teilscans ehrlich ausweisen.
  - Scope: `GetVerifyAdvisoriesTool` und `VerifyAdvisoryPageStore` so anpassen, dass ein Verify-Token exakt dessen Snapshot öffnet und ein tokenloser Solution-Abruf einen noch gültigen vollständigen Snapshot nur bei passender Solution-Version, Konfiguration und Scope wiederverwendet. Sonst neu scannen. Detailseiten auf verbleibende Kandidaten, kopierbare `h:`-IDs, Gegenprüfhinweis und Pagination begrenzen; `scanCompleteness`/`listCompleteness` getrennt halten. FastTests für Token, Staleness, Paging und partiellen Null-Treffer; MCP-Dokumentation aktualisieren.
  - Nicht: Teilscan als vollständig ausgeben oder für Pagination neu scannen.
  - Abnahme: Token-Fortsetzung und passender tokenloser Abruf verwenden denselben Snapshot; Änderung an Solution oder Konfiguration erzwingt neuen Scan. Budgetabbruch mit null Treffern bleibt sichtbar `partial`. Seiten enthalten keine `undecidable`-Details. Incremental Gate grün.

- [ ] **6 — Überflüssigen Dead-Code-Vertrag entfernen und Dokumentation abschließen**
  - Intention: Nach der Verengung keine toten Datenmodelle, Optionen oder Dokumentationsversprechen zurücklassen.
  - Scope: Ausschließlich für die alte Feld-/Property-, `test_only`-, `undecidable`- oder Confidence-Analyse benötigte Heuristiken, Modelle, Optionen und Tests entfernen; gemeinsame Nutzungs- und Abdeckungslogik erhalten. `Docs/mcp/dead-code.md`, `Docs/mcp/tools.md`, `Docs/linter/configuration.md`, `Docs/linter/cli.md`, `ainetlinter-rules.json` und betroffene Agentenregeln gegen den implementierten Vertrag synchronisieren.
  - Nicht: Neue Analysearten ergänzen oder die in [Konzept.md](Konzept.md) bewusst ausgeschlossenen Member zurückholen.
  - Abnahme: `test_only`-, `undecidable`- und Confidence-Status/Zähler werden nicht mehr veröffentlicht; keine ungenutzten exklusiven Analysepfade bleiben. Dokumentation und Konfiguration passen zur tatsächlichen Ausgabe. Incremental Gate grün.

- [ ] **7 — Planner-Gegenprobe und Abschlussgate**
  - Intention: Signalqualität und technische Abnahme auf dem großen Zielprojekt belegen.
  - Scope: Den Planner-Snapshot lesend nachprüfen: belegte Methoden-/Typfunde wie `SqlDateTimeMapping` bleiben, belegte Fehlalarmklassen verschwinden; Abweichungen als konkrete Befunde unmittelbar unter diesem Punkt in der Roadmap festhalten. Danach das Abschlussgate aus `.agents/rules/AiNetLinter-Richtlinien.mdc` ausführen.
  - Nicht: Planner-Code ändern, eine feste Kandidatenzahl erzwingen oder Integrations-/Stresstests ohne Auftrag starten.
  - Abnahme: Die Planner-Gegenprobe beruht auf `scanCompleteness=complete`, ihre Befunde sind nachvollziehbar dokumentiert; Abschlussgate besteht mit `verdict=pass`, `score=10.0`, `violationCount=0` und grünem FastTests-Lauf.

- [ ] **Audit — Konzept, Anwendung und Commits lesend abgleichen**
  - Intention: Vor Übergabe bestätigen, dass die verengte Analyse ein nutzbares Signal liefert und der Scope eingehalten wurde.
  - Scope: Konzept gegen Diff, Tests, MCP-Content und Planner-Gegenprobe prüfen; Commit-Grenzen, Dokumentationsabgleich und bekannte bewusste Verluste kontrollieren. Befunde mit Belegen melden; höchstens einen gezielten Korrekturpunkt eröffnen, wenn die Abnahme sonst scheitert.
  - Nicht: Im Audit Produktionscode ändern oder Schritt 3 eigenständig starten.
  - Abnahme: Jeder Muss-Punkt belegt, jeder Nicht-Punkt eingehalten, keine offenen roten Gates oder ungeklärten Abweichungen; erst dann Audit-Checkbox schließen.
