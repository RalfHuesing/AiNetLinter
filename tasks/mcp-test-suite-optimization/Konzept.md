---
status: ready
execution_mode: autonomous
open_questions: []
---

# Konzept: Aussagekräftigere und schnellere MCP-Test-Suite

## Ziel und fachliche Bewertung

Das Vorhaben ist sinnvoll, sofern es eine **Neuzuordnung von Verträgen zu
Testebenen** bleibt und nicht pauschal E2E-Tests löscht. Die MCP-Integrationstests
enthalten eine große Menge an Detailprüfungen für Argumente, Feldpfade,
Trunkierungstexte und Toolmetadaten. Diese sind als direkte Tool- oder
Kompositionstests schneller, präziser lokalisierbar und weniger störanfällig.

Die bisherige Begründung wird in zwei Punkten eingegrenzt:

- `ReadOnlyMcpHostFixture` ist eine Assembly-Fixture mit lazy erzeugtem,
  gemeinsam genutztem Host. Viele Aufrufe erzeugen also zusätzliche RPC-Last,
  aber **nicht je Test einen neuen Prozess**.
- Direkte `ExecuteAsync`-Tests belegen weder JSON-RPC-Binding noch die
  Fehlerabbildung des MCP-Adapters. Ein Toolvertrag darf deshalb erst aus dem
  Stdio-Pfad verlagert werden, wenn die verbleibenden Wire-Tests seine
  Adapterklasse weiterhin abdecken.

Die erwartete Laufzeit- und Robustheitsverbesserung ist plausibel, aber ohne
vergleichbare Messung nicht quantifizierbar. Insbesondere ist die frühere
Behauptung einer bestimmten Prozess- oder prozentualen Reduktion kein
Akzeptanzkriterium.

## Ist-Zustand und Source of Truth

Die Quelle der Wahrheit sind die aktuellen Tests und ihre Verträge, nicht ihre
heutige Dateiorganisation:

- `McpServerArgumentValidationE2ETests` mit den Partial-Dateien enthält viele
  Stdio-Prüfungen für fehlende, unbekannte und typfalsche Argumente sowie
  Feldpfade.
- `McpServerCommandFindSymbolTests`,
  `McpServerCommandFindReferencesTests` und `McpServerCommandMissHintTests`
  prüfen Ergebnisse und Textdarstellung über dieselbe Shared-Fixture.
- `McpHandshakeToolRegistrationTests` prüft die echte Binärdatei inklusive
  Handshake und `tools/list`; `WiringToolCollectionContractTests` prüft
  unabhängig davon die 33 registrierten Tools, Schemas und Annotationen
  in-process.
- `McpServerCommandGetImpactTests` und `McpServerCommandContractTests`
  starten für einzelne Git-/Fehlerszenarien zusätzliche Hosts. Wesentliche
  Git-Impact- und Trunkierungsverträge existieren bereits direkt in
  `GetImpactToolIntegrationTests`.
- `McpProcessHost` setzt einen Standard-Call-Timeout von 180 Sekunden und
  schützt Prozessstarts über `SubprocessLifetimeBudget`. Diese Infrastruktur
  ist ein Symptom teurer Prozesse, aber kein hinreichender Beweis dafür, dass
  jeder Timeout verkleinert werden kann.

## Zielbild: Testpyramide mit explizitem Adaptervertrag

| Vertragstyp | Primäre Testebene | Verbleibender Stdio-Nachweis |
|---|---|---|
| Eingabegrenzen, Feldpfade, fachliche Fehlercodes | FastTests oder In-Process-Integration | Repräsentative Matrix für Binding und Fehlerabbildung |
| Toolliste, Schema, Beschreibung, Annotationen | FastTests über die Tool-Collection | Ein `tools/list`-Handshake gegen die echte Binärdatei |
| Roslyn-, Git- und Assembly-Verhalten | In-Process-Integration gegen reale Fixture-Workspaces | Ein repräsentativer erfolgreicher Source- und Assembly-Pfad, soweit nicht bereits Wire-abgedeckt |
| JSON-RPC, stdout/stderr, CLI, EOF, Daemon/IPC | IntegrationTests mit echtem Prozess | Vollständig erhalten |

Der endgültige Wire-Satz muss mindestens diese unabhängigen Adapterklassen
abdecken:

1. erfolgreicher, target-gebundener Toolaufruf mit strukturiertem Ergebnis;
2. `tools/list` nach echtem Initialize-Handshake mit dem erwarteten
   stabilen Toolbestand;
3. unbekanntes Tool (Protokollfehler), unbekanntes Argument,
   fehlender `targetPath` und falscher JSON-Typ einschließlich der jeweils
   vereinbarten MCP-Fehlerform (`IsError`, Code und `fieldPath`);
4. JSON-RPC-Framing, stderr-Disziplin, EOF-/CLI-Lebenszeit und vorhandene
   Daemon-/Mehrprozess-Verträge.

Die Matrix darf wenige parametrische Fälle enthalten. Sie darf aber nicht auf
einen einzigen beliebigen Fehlerfall reduziert werden, weil die gegenwärtigen
Verträge unterschiedliche Transport- und Fehlerformen zeigen.

## Muss-Kriterien

- Jeder entfernte E2E-Testfall erhält vor seiner Entfernung einen benannten
  Nachfolger auf der passenden Ebene. Der Nachfolger übernimmt den fachlichen
  Vertrag (Ergebnis, Fehlercode, Feldpfad, Trunkierungs- oder
  Formatierungssemantik), nicht nur einen ähnlich klingenden Happy Path.
- Jede einmalige Adaptersemantik bleibt im Stdio-Satz. Die Abdeckung wird vor
  dem Löschen anhand der obigen Matrix und der vorhandenen Roh-Wire-,
  Lifecycle- und Daemon-Tests geprüft.
- Detailtests für `find_symbol`, `find_references`, `get_impact` und weitere
  Tools verwenden den bestehenden jeweiligen Tooltest bzw. eine neue,
  klar zugeordnete Testklasse. Keine neue allgemeine Testabstraktion ohne
  nachgewiesenen gemeinsamen Vertrag.
- Git- und Roslyn-Szenarien bleiben gegen echte isolierte Workspaces geprüft.
  Eine Verlagerung in-process darf weder Git-Diff-, Workspace- noch
  Trunkierungsverhalten abschwächen.
- Temporäre Workspaces verwenden ausschließlich `TestTempDirectory` bzw. die
  bestehenden Fixture-Mechanismen und bleiben parallel sicher.
- Timeouts, Prozessbudget und Retries werden erst nach Messung und nur dort
  reduziert, wo der verbleibende Bedarf technisch belegt ist. Keine globale
  Verschärfung allein als Aufräumfolge.
- Produktionscode und öffentliche MCP-Verträge bleiben unverändert, sofern
  eine Korrektur nicht zur Erhaltung eines bestehenden Testvertrags zwingend
  erforderlich wird.

## Akzeptanzkriterien

- Die verbliebenen Stdio-Tests erfüllen die Adapter-Matrix und beinhalten
  einen echten Binär-/Handshake-Nachweis; der direkte Collection-Test ist
  hierfür kein Ersatz.
- Alle bisher exklusiven, fachlich relevanten Assertions sind entweder in
  einem benannten direkten Test erhalten oder bewusst als redundant
  dokumentiert und durch einen gleichwertigen verbleibenden Test abgedeckt.
- Testnamen, Kategorien und Verzeichnisstruktur lassen erkennen, ob ein Test
  Wire-, In-Process-Integration oder schnelle Komponentenlogik absichert.
- Ein Vorher-/Nachher-Vergleich desselben Nicht-Stress-Integrationstestlaufs
  dokumentiert Dauer, Anzahl neu gestarteter Hosts (soweit ermittelbar) und
  fehlgeschlagene/abgebrochene Läufe. Die Messung dient der Bewertung; wegen
  schwankender CI- und MSBuild-Last erhält sie kein künstliches Prozentziel.
- Beide Nicht-Stress-Testprojekte, der Solution-Build und die verbindlichen
  MCP-Quality-Gates sind grün. Die folgenden Gates gelten für die spätere
  Codeänderung:

  ```powershell
  dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
  dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
  dotnet build
  ```

  Danach müssen `safeguard` mit `minScore: 10`, `get_violations`,
  `find_dead_code` und `find_magic_values` für die Solution geprüft werden.

## Abgrenzung (Non-Goals)

- Kein pauschales Entfernen aller Fehler-, Registrierungs- oder
  Formatierungstests aus dem Stdio-Pfad.
- Keine Änderung des MCP-Protokolls, seiner Fehlersemantik, der CLI oder
  produktiver Analysealgorithmen zur bloßen Beschleunigung von Tests.
- Kein Ausführen der Stress-Kategorie als reguläres Release-Gate.
- Keine Performance-Zusage in Prozent und keine Änderung externer
  Dokumentation, solange sich kein veröffentlichter Vertrag ändert.

## Umsetzungsspielraum und Reihenfolge der Entscheidungen

Der Orchestrator darf die konkrete Zieltestklasse und parametrische Form
selbst wählen. Er entscheidet für jeden Kandidaten in dieser Reihenfolge:

1. Den aktuellen fachlichen und Adaptervertrag identifizieren.
2. Einen direkten Nachfolger als Rot-Test erstellen oder den bereits
   gleichwertigen Test nachweisen.
3. Prüfen, ob die Adapterklasse bereits durch den verbleibenden Wire-Satz
   gedeckt ist; andernfalls die Wire-Matrix ergänzen.
4. Erst dann den redundanten Stdio-Fall entfernen oder konsolidieren.
5. Nach der Konsolidierung messen und nur evidenzbasiert Infrastrukturwerte
   anpassen.

Das ist kein Auftrag, alle aufgezählten Dateien zu ändern: Ein Test bleibt
bestehen, wenn er eine einzigartige Adapter-, Lifecycle- oder
Workspacesemantik trägt.

## Risiken, Alternativen und Entscheidung

| Alternative | Konsequenz |
|---|---|
| Suite unverändert lassen | geringstes Umbau-Risiko, aber unnötige RPC-Last und schwerer lokalisierbare Fehler bleiben bestehen |
| Alle Detailtests direkt verlagern | schnellste Suite, aber hohes Risiko einer ungetesteten MCP-Binding- oder Fehlerabbildungsregression |
| Vertraglich geschichtete Konsolidierung | etwas mehr Analyseaufwand, erhält jedoch den echten Wire-Vertrag bei klarer, wartbarer Testpyramide |

Empfehlung: die vertraglich geschichtete Konsolidierung. Sie adressiert die
nachweisbare Redundanz, ohne die produktionsrelevante Grenze zwischen
Toollogik und MCP-Transport zu verwischen. Es bestehen keine offenen
Nutzerentscheidungen.

## Verifikation und Dokumentation

Die spätere Umsetzung folgt Red-Test-First für jede beobachtete Lücke. Bei
unklaren oder abgebrochenen Testläufen wird eine TRX-Datei erzeugt und
ausgewertet, statt den Lauf blind zu wiederholen. Neben den Release-Gates
werden die Diffs auf fehlende Testzuordnung und unbeabsichtigte
Protokolländerungen geprüft. Da der Scope keine öffentliche Vertragsänderung
vorsieht, sind Änderungen an `Docs/` und `ainetlinter-rules.json` nicht
erforderlich; sie werden nur bei tatsächlich geänderten öffentlichen
Beschreibungen nachgezogen.
