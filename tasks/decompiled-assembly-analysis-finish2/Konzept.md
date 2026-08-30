---
status: ready
---

# Konzept: Abschluss der dekompilierten Assembly-Analyse

## Ziel

Der Nachfolge-Task schließt die verbleibenden Lücken der dekompilierten Analyse externer Assemblies und übernimmt die noch relevanten Tech-Debt-Punkte aus der vorherigen Assembly-Arbeit. Alle in diesem Konzept genannten Arbeitspakete sind verbindlicher Umfang; es gibt keine optionalen Punkte. Das Ergebnis soll für externe Assemblies dieselben belastbaren, begrenzten und nachvollziehbaren Analyseverträge bieten wie für die quellcodebasierte Projektanalyse, ohne die beiden Herkunftsmodelle zu vermischen.

Der Task soll anschließend autonom implementierbar sein. Das Konzept ist für die autonome Umsetzung freigegeben.

## Ausgangspunkt und Leitplanken

Der Projektroot ist als Quellcode-Projekt geladen und bleibt die maßgebliche Quelle für Architektur- und Implementierungsentscheidungen. `targetType=assembly` ist ein bewusst separater MCP-Pfad: Er analysiert PE-/Metadaten und dekompilierte In-Memory-Dokumente und wird nur verwendet, um den External-Assembly-Vertrag reproduzierbar zu prüfen.

Die lokal gebaute eigene DLL ist dabei lediglich ein kontrollierbarer Surrogat-Testfall. Sie beweist nicht das Verhalten jeder Drittanbieter-DLL; deshalb werden zusätzlich synthetische Mehr-DLL-Fixtures und echte Fehler-/Grenzfälle benötigt.

Bereits umgesetzt und nicht erneut als offene Fehlerbehebung einzuplanen sind die Cancellation-Korrektur bei der Assembly-Provider-Erzeugung, die Assembly-Identität in `get_type_hierarchy`, die Absicherung leerer In-Memory-Pfade, der Duplicate-Rollback-Lifecycle, die zentrale Partiality-Statusprojektion, die source-lokale MCP-Bootstrap-/Schema-Abweichung sowie die früheren Befunde zu Expansion-Diagnostics, negativen Dispatcher-Routen, Daemon-Resident-Count und `get_file_tree`-Unsupported-Pfaden. Dafür bleiben Regressionstests und ein aktueller Live-/Abschlussnachweis erforderlich. Die ursprüngliche Session-Expansion ist ebenfalls umgesetzt; die neue Cross-Assembly-Navigation aus BEF-04 ist davon als weitergehender Funktionsumfang zu unterscheiden.

## Befundinventar aus beiden historischen Task-Verzeichnissen

### External-Assembly-Findings

- BEF-01 Ressourcenerschöpfung, Konfiguration, LRU und Sub-Scoping: verbindlicher Umfang in C.
- BEF-02 Diagnose-/Referenz-Token-Explosion: verbindlicher Umfang in B.
- BEF-03 leerer `relativeTo`-Pfad: verbindlicher Umfang in A.
- BEF-04 Cross-Assembly-Typauflösung und Call-Tracing: verbindlicher Umfang in D.
- BEF-05 tolerante `find_references`-Analyse bei Decompiler-/Compile-Fehlern: verbindlicher Umfang in D.
- BEF-06 kompakter Healthcheck: verbindlicher Umfang in B.
- BEF-07 Zugriff auf generierte In-Memory-Dateien: verbindlicher Umfang in A.
- BEF-08 Signaturen mit Parametern: verbindlicher Umfang in A.
- BEF-09 Filter für große Klassenstrukturen: verbindlicher Umfang in E.
- BEF-10 korrekte Metrikgröße für In-Memory-Dokumente: verbindlicher Umfang in E.
- BEF-11 positive Befunde zu Metriken, Hierarchie und Routing: als verbindliche Regression geschützt, nicht als neues Feature erweitert.

### Historische P1-/P2-Befunde und Übergaben

- Cancellation-Propagation und Assembly-Symbolidentität wurden behoben und erhalten Regressionstests.
- Die ursprüngliche transitive Session-Expansion wurde über den echten Dispatcher-/Host-Pfad integriert und erhält Regressionstests. BEF-04 erweitert diesen Bestand um die fachliche Suche und Navigation über Referenzgrenzen.
- Der fehlerhafte Duplicate-Rollback bei Snapshot-/Registry-Dispose wurde behoben und erhält einen Fehler- und Race-Regressionstest.
- Die widersprüchliche Partiality-Projektion zwischen Payload, Header und Metadaten wurde behoben und erhält einen Statuskanal-Regressionstest.
- Die zwischen Quellregistrierung und installierter MCP-Registry festgestellte Schema-/Deployment-Abweichung wurde über den source-lokalen Bootstrap aufgelöst und wird im Abschluss-E2E erneut geprüft; eine globale Installation wird nicht eigenmächtig verändert.
- Expansion-Diagnosen im Extensions-Tool, negative Dispatcher-/Tool-Routen und der kanonische pfadbezogene `get_file_tree`-Unsupported-Vertrag sind behoben und bleiben Regressionsthemen.

### Minor-, Nitpick- und Audit-Befunde

- Die ungenaue Zählung alter Wiring-/Filesystem-Tests im Implementiererbericht wird in den aktiven Abschlussnachweisen korrigiert; die tatsächliche Testaufteilung wird einmal belastbar angegeben.
- Leerzeilen-/Whitespace-Befunde aus `git diff --check` werden in allen vom Task berührten Dateien bereinigt.
- Niedrig-konfidente Dead-Code-Kandidaten werden mit Referenz-, Reflection-, Serialization- und Testprüfung bewertet. Nur sicher tote, scope-nahe Kandidaten werden entfernt; der Rest bleibt mit Begründung als Tech Debt dokumentiert.
- Magic-Value-Kandidaten werden fachlich einzeln bewertet. Diagnosecodes, Stable IDs und Wire-/CLI-Werte werden nur bei identischer Semantik zentralisiert.
- Scope-nahe Footprint-Warnungen, insbesondere `AssemblyAnalysisRegistry`, werden als Tech-Debt-Arbeit erneut mit Safeguard und `get_violations` geprüft.
- Die im historischen Audit gemeldeten scope-nahen `MaxLineCount`-Befunde in `WiringContractTests.cs` und `McpServerAllToolsE2ETests.cs` werden geprüft und bei Task-Berührung bereinigt oder mit Ursache und nächstem Ansatz als Tech Debt festgehalten.
- Bekannte Duplikatcluster ohne sicheren scope-nahen Fix werden nicht künstlich refaktoriert; sie werden im Audit als geprüft, außerhalb des sicheren Umfangs oder als Tech Debt ausgewiesen.

## Geplanter Umfang

### A. Stabile In-Memory- und Dateipfade

- `get_call_tree`, `get_symbol_body` und `dependency_graph` dürfen bei dekompilierten In-Memory-Dokumenten nicht an einem leeren `relativeTo` scheitern.
- `get_file_skeleton` muss generierte dekompilierte Dokumente über die Assembly-Session adressieren können; ein physischer Projektpfad darf nicht vorausgesetzt werden.
- Methodensignaturen mit Parametern, etwa `Document.Save(bool)`, müssen im Assembly-Kontext konsistent aufgelöst werden.
- Stable Symbol IDs und Assembly-Identität müssen über alle Assembly-Tools hinweg konsistent bleiben; ungültige oder fremde IDs müssen kontrolliert als fachlicher Fehler erscheinen.

### B. Begrenzte Diagnostik und Antwortverträge

- `inspect_assembly`, `find_assembly_extensions` und `get_server_health` müssen Diagnose- und Referenzdaten aggregieren, begrenzen und strukturiert ausgeben. Einzelne Root-/transitive Meldungen dürfen weder Token-Explosionen noch unlesbare Einzeilen erzeugen.
- Strukturierte Antworten müssen Counts, Vollständigkeit, Truncation und repräsentative Samples liefern; Textdarstellung und strukturierte Nutzdaten müssen dieselben Grenzen einhalten.
- Die bestehende Statuslogik `complete` plus Diagnostics zu `partial` bleibt erhalten und wird durch Regressionstests geschützt.
- `get_server_health` liefert standardmäßig kompakte Metadaten und Zähler; vollständige Diagnostics bleiben nur über eine explizite, begrenzte Detailoption verfügbar.

### C. Ressourcen, Konfiguration und Lebensdauer

- Die Limits für Disk, Memory, Parallelität, Resident Resources und Idle-TTL müssen als ein validiertes Optionsmodell konfigurierbar sein und für alle externen Assembly-/Snapshot-Registries tatsächlich gelten.
- Die bestehende LRU-/TTL-Eviction wird auf Verhalten unter Last geprüft; harte, nicht überschreibbare Defaults dürfen nicht der einzige Schutz gegen viele Assemblies bleiben.
- Acquire/Eviction muss Ownership und Synchronisation so definieren, dass kein gerade erworbener Snapshot durch konkurrierende Eviction entfernt oder disposed wird.
- Materialisierung muss Ressourcenbudgets möglichst vorab oder streaming-/rollback-sicher berücksichtigen, damit vollständiges Checkout-/Workspace-Materialisieren nicht ungeschützt temporäre Spitzen erzeugt.
- Concurrent Provider-Erzeugung muss Producer- und wartende Consumer-Cancellation klar trennen: der Abbruch eines Wartenden darf die gemeinsame Erzeugung nicht ungewollt abbrechen; Producer-Abbruch und Dispose müssen deterministisch sein.

### D. Mehrere Assemblies und tolerante Analyse

- `find_symbol` erhält eine explizite, rückwärtskompatible Möglichkeit, Referenzen einzubeziehen und Typen in referenzierten DLLs zu finden; die Referenzanalyse wird implementiert, bleibt für einzelne Aufrufe aber ausdrücklich anforderbar und begrenzt.
- `get_call_tree` und abhängige Symbol-/Referenzpfade können – innerhalb von Limits und mit Herkunftskennzeichnung – über referenzierte Assemblies traversieren.
- `find_references` liefert bei Compile-/Decompilerfehlern ein kontrolliertes partielles Ergebnis mit Diagnostics statt pauschal null Treffer oder einem unklaren Fehler.
- Die Mehr-Assembly-Suche braucht Subscoping, Tiefen-/Anzahl-Limits und klare Ownership der Sessions/Leases, damit Referenzexpansion nicht den Resident-Bestand sprengt.

### E. Kleinere, abgegrenzte Befunde

- Große Klassen-/Member-Antworten erhalten serverseitig wirksame Kind-/Name-Filter und eine begrenzte Ausgabe.
- Metrics für In-Memory-Dokumente verwenden eine definierte SourceText-basierte Semantik oder kennzeichnen `unknown`; `0` darf nicht fälschlich eine echte Dateigröße vortäuschen.
- Positive Live-Befunde zu Routing, Hierarchie und stabilen Metrics werden als Regressionstests konserviert, ohne daraus zusätzliche Produktfeatures abzuleiten.

## Muss-Kriterien

1. Kein Assembly-Analyse-Tool wirft bei gültiger dekompilierter In-Memory-Session wegen eines leeren physischen Basis- oder `relativeTo`-Pfads.
2. Generierte dekompilierte Dokumente sind über `get_file_skeleton` erreichbar oder liefern eine fachlich präzise, dokumentierte Unsupported-Antwort.
3. Assembly-Diagnostik und Referenzen sind in strukturierten und textuellen Antworten begrenzt, vollständigkeitsmarkiert und reproduzierbar.
4. Ressourcenlimits sind konfigurierbar, werden in der Assembly-Komposition verdrahtet und werden durch Tests für TTL, LRU, Parallelität, Lease und konkurrierende Eviction belegt.
5. Mehr-Assembly-Referenzanalyse ist verbindlich implementiert, für einzelne Aufrufe ausdrücklich anforderbar, begrenzt und herkunftssicher; der Standardpfad bleibt unverändert.
6. Tolerante Referenzsuche und definierte Fehlerantworten bleiben auch bei fehlerhaften oder unvollständigen Assemblies nutzbar.
7. Jeder historische Befund ist im Befundinventar als umzusetzen, regressionszusichern, zu bereinigen oder als begründete Tech Debt weiterzuführen markiert; kein Befund wird stillschweigend verworfen.
8. Die vollständige Build- und Test-Gate sowie der projektweite Audit sind vor Task-Abschluss grün beziehungsweise mit nachvollziehbar dokumentierten, nicht kausalen Umgebungsbefunden abgeschlossen.

## Akzeptanzkriterien

- Synthetische Fixtures decken mindestens eine Root-Assembly mit zwei Referenz-DLLs, einen Typ ausschließlich in einer Referenz-DLL, fehlende/zyklische Referenzen und beschädigte oder unvollständige Metadaten ab.
- Für die Assembly-Tools existieren Tests für leere In-Memory-Pfade, generierte Dokumente, Parametermethoden, unbekannte Stable IDs, Root-/transitive Diagnostics und Antwort-Truncation.
- Ein Test mit mehr als dem bisherigen Resident-Limit zeigt kontrollierte Eviction beziehungsweise eine verständliche Kapazitätsantwort; absichtlich hohe Parallel-Last wird ausschließlich als gezielter Stress-Test ausgeführt.
- Ein Race-Test entscheidet und dokumentiert, ob Snapshot-Acquire oder Eviction gewinnt, ohne Use-after-dispose oder Lease-Leak.
- Provider-Creation-Tests decken erfolgreichen Join, wartenden Consumer-Abbruch, Producer-Abbruch und Dispose während der Erzeugung ab.
- `find_symbol(includeReferences=false)` behält den bisherigen Standardvertrag; `true` liefert begrenzte Treffer mit Assembly-Herkunft. Beide Verhaltensweisen sind implementiert und getestet.
- `find_references` kennzeichnet partielle Ergebnisse und Diagnostics eindeutig, statt einen leeren Trefferbestand als vollständige Aussage erscheinen zu lassen.
- Health-/Inspect-Antworten bleiben unter einem festgelegten, getesteten Größenbudget; Counts und Truncation sind maschinenlesbar.
- Die beiden vollständigen Nicht-Stress-Testläufe und `dotnet build` werden am Ende ausgeführt; der gezielte AiNetLinter-Audit wird nach der Implementierung wiederholt.

## Übernommenes Tech-Debt

- TD-001: Windows-Git-Prozesstest deterministisch isolieren oder als klar begrenztes Umgebungsproblem dokumentieren; keine Assembly-Scope-Erweiterung.
- TD-002: Bestehenden `ProjectRegistry`-FastTest-Fehler reproduzieren und Isolation/Windows-Umgebung bereinigen, falls er erneut auftritt; nicht mit Assembly-Verhalten vermischen.
- TD-003: Magic-Value-Kandidaten nur bei gleicher fachlicher Semantik zentralisieren; Diagnosecodes, Stable IDs und Wire-/CLI-Verträge nicht blind zusammenlegen.
- TD-004: Snapshot-Registry-Ownership zwischen Acquire und Eviction festlegen und mit Race-Test absichern.
- TD-005: Budgetierung der Source-Materialisierung vorab, streaming- oder rollback-sicher machen.
- TD-006: Creation-Barrier-Cancellation wie unter C beschrieben spezifizieren und testen.
- TD-007: `AssemblyAnalysisRegistry` nur dann weiter zerlegen, wenn eine echte unabhängige Verantwortung entsteht; danach Footprint, Safeguard und MCP-Vertrag erneut prüfen.

Die bereits als behoben dokumentierten TD-008 bis TD-011 werden nicht erneut als offene Implementierung geführt.

## Nicht-Ziele

- Keine allgemeine neue Roslyn-Analysearchitektur neben dem bestehenden Projekt-/Assembly-Dispatcher.
- Keine automatische Ausführung, Installation oder Vertrauensentscheidung für externe Assemblies.
- Keine unlimitierte transitive Referenzauflösung und kein globales Zusammenlegen von Projekt- und Assembly-Registry.
- Keine pauschale Zentralisierung aller Diagnose-Strings, Identifiers oder CLI-/MCP-Werte.
- Keine Erweiterung der Stress-Test-Suite ohne ausdrückliche Lasttest-Notwendigkeit.
- Keine Änderung der fachlichen Semantik des bereits funktionierenden quellcodebasierten Projektpfads außerhalb notwendiger gemeinsamer Vertragskorrekturen.

## Agentischer Ausführungsmodus ohne Blocker-Schleifen

Der autonome Implementierungsdurchlauf führt zuerst alle ursprünglichen Findings und Arbeitspakete in einer festen Reihenfolge aus. Für jedes einzelne Finding gibt es höchstens drei ernsthafte Lösungsversuche. Ein Versuch umfasst einen konkreten Änderungsansatz, die passende fokussierte Verifikation und die Bewertung des Ergebnisses.

1. Nach einem erfolgreichen Versuch wird der Punkt gezielt abgeschlossen und der nächste Punkt begonnen.
2. Nach einem fehlgeschlagenen Versuch wird der Ansatz bewertet; ein neuer Versuch muss eine begründete andere Hypothese oder Korrektur enthalten. Gleiches blind zu wiederholen zählt nicht als neuer Versuch.
3. Nach dem dritten erfolglosen Versuch wird der Punkt nicht weiter verfolgt. Der Code bleibt in einem funktionalen, kompilierbaren und für die Folgeschritte nutzbaren Zustand: eine unsichere Teiländerung wird entfernt oder auf den letzten bekannten funktionalen Vertrag zurückgeführt, statt einen kaputten Pfad liegenzulassen.
4. Der konkrete Rest, die Ursache, die drei Ansätze beziehungsweise deren Ergebnisse, das Risiko und der nächste sinnvolle Ansatz werden als Tech Debt dokumentiert. Danach läuft die Arbeit unverzüglich mit dem nächsten ursprünglichen Finding weiter.
5. Erst wenn alle ursprünglichen Findings bearbeitet wurden, wird die gesammelte Tech-Debt-Liste in derselben Reihenfolge abgearbeitet. Für jedes Tech-Debt-Item gelten erneut höchstens drei begründete Versuche. Nach drei Fehlschlägen bleibt es dokumentiert bestehen und das nächste Tech-Debt-Item beginnt.
6. Ein erschöpftes Drei-Versuche-Budget wird nicht durch globale Orchestrator-Schleifen, neue Rollen oder bloßes Wiederholen zurückgesetzt. Der Nutzer hebt alle darüber hinausgehenden künstlichen Orchestrator-Limits ausdrücklich auf; die einzige per-Finding-Grenze bleibt diese Drei-Versuche-Regel.
7. Kein Finding darf stillschweigend entfallen: es muss als abgeschlossen, regressionsgesichert, bereinigt oder begründet als Tech Debt weitergeführt sichtbar sein.

Die Regel dient der maximalen sicheren Lieferung ohne Blockierung. Sie ist kein Vorwand, Arbeitspakete frühzeitig auszulassen; sie verhindert ausschließlich Endlosschleifen an einem einzelnen Befund.

## Fehler-, Betriebs- und Sicherheitsmodell

Assembly-Sessions bleiben getrennt von Projekt-Sessions und werden über Leases, TTL/LRU und harte Ressourcenbudgets verwaltet. Jede Antwort kennzeichnet Herkunft, Vollständigkeit, Generation und relevante Diagnostics. Auflösungsfehler, fehlende Referenzen, beschädigte Metadaten, unbekannte IDs und Budgetüberschreitungen werden als begrenzte fachliche Fehler beziehungsweise partielle Ergebnisse ausgegeben; sie dürfen weder den MCP-Prozess destabilisieren noch unkontrolliert Daten materialisieren.

Die ausdrücklich anforderbare Referenzexpansion muss Tiefe, Anzahl, Zeit, Disk, Memory und Parallelität begrenzen. Die zulässigen externen Pfade und Vertrauensannahmen werden dokumentiert; eine Decompilation darf keinen Code ausführen. Dispose-/Lease-Ownership und Cancellation müssen auch bei Fehlern und Abbruch deterministisch sein.

## Betroffene Verträge und Dokumentation

Der Orchestrator soll bei tatsächlichen Vertragsänderungen mindestens README, `Docs/agent-api.md`, `Docs/integration.md` und `Docs/configuration.md` prüfen und aktualisieren; `Docs/ROADMAP.md` nur bei betroffenem Meilenstein. Der Assembly-/MCP-Workflow und Zieltypen werden in `.agents/rules/AiNetLinter-McpWorkflow.mdc` nur bei geänderten Tool-Verträgen angepasst. Es werden keine Produktionsreferenzen auf die Task-Verzeichnisse eingeführt.

Für die Konfiguration wird der vorhandene MCP-/External-Source-Settings-Mechanismus erweitert und über CLI-Overrides ergänzt. Da im aktuellen Stand kein allgemeines `appsettings*.json`-Muster für diese Limits belegt ist, wird kein neues, paralleles `appsettings`-Subsystem eingeführt.

## Verifikation

Die Implementierung wird iterativ mit fokussierten Assembly-/Registry-Tests geprüft. Danach folgen zwingend:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Zusätzlich werden die Assembly-Live-Szenarien gegen synthetische Fixtures wiederholt und der gezielte Audit auf DRY, Refactoring-Drift, Dead Code und Magic Values ausgeführt. Windows-spezifische Testbefunde werden getrennt von fachlichen Assembly-Befunden bewertet.

## Festgelegte Ausführungsvorgaben

- Die transitive Referenzsuche inklusive `find_symbol`, Call-Tree und tolerantem `find_references` wird in diesem Task vollständig umgesetzt. Sie bleibt pro Aufruf ausdrücklich anforderbar und streng limitiert; das ist eine Laufzeitsemantik, kein optionaler Taskumfang.
- Die Assembly-Ressourcenlimits werden über das vorhandene MCP-/External-Source-Settings-Modell und CLI-Overrides konfiguriert. Ein neues unverbundenes `appsettings`-Subsystem wird nicht eingeführt.
- Klassenfilter und korrekte In-Memory-Metrics werden im selben Abschluss-Task umgesetzt.
- Technische Detailbefunde, die erst während der Umsetzung sicher entschieden werden können, werden dort geklärt. Sie dürfen nicht dazu führen, dass ein ganzer Arbeitspaketblock übersprungen wird; nicht sinnvoll lösbare Reste folgen dem agentischen Ausführungsmodus und werden als Tech Debt dokumentiert.
