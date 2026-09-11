---
status: ready
execution_mode: autonomous
open_questions: []
---

# MCP-Agentenvertrag restlos härten

## Freigabe- und Umsetzungsvertrag

Dieses Konzept wurde nach einer unterbrochenen Teilumsetzung gegen den aktuellen
Repository-Stand neu geschnitten. Der spätere Orchestrator setzt ausschließlich
den hier beschriebenen Rest-Scope seriell bis zum Release-Gate um. Bereits
erledigte Grundlagen werden durch Regressionstests geschützt, aber nicht ohne
reproduzierbaren Gegenbefund neu gebaut.

`execution_mode: autonomous` erlaubt alle fachlichen Implementierungsentscheidungen
innerhalb dieses Scopes ohne Zwischenfreigabe. Es gibt keine offene
Nutzerentscheidung. Der Status bleibt bis zur erneuten ausdrücklichen Freigabe
`draft`; der Orchestrator startet nicht automatisch.

Es gilt ein harter Schnitt: Nach Abschluss existiert nur der aktuelle öffentliche
MCP-Vertrag. Keine Legacy-, Alias-, Dual-Read-, Dual-Write- oder Feature-Flag-
Pfade und keine leeren Platzhalter für ersetzte Felder bleiben erhalten.

## Ziel und Erfolg

Der AiNetLinter-MCP-Server liefert programmierenden Agenten widerspruchsfreie,
strukturierte und budgetierbare Antworten mit möglichst hoher fachlicher
Informationsdichte. Ein Agent kann Status, Scope, Herkunft, Vollständigkeit,
Analysequalität und den nächsten Schritt ohne Textparsing zuverlässig auswerten
und Source- sowie Assembly-Aufrufe sicher verketten.

Der Resttask ist erfolgreich, wenn:

1. die bereits umgesetzten Contract-v2-, Validierungs-, Scope- und Namespace-
   Invarianten unverändert gelten;
2. alle unten belegten Restabweichungen behoben sind;
3. ersetzte generische oder doppelte Vertragspfade entfernt sind;
4. Dokumentation, registrierte Schemas und Runtimeverhalten denselben Endzustand
   beschreiben;
5. Dogfood, MCP-Quality-Gate, Build und beide Nicht-Stress-Testsuiten grün sind.

Nicht die kleinste Antwort ist das Ziel. Jedes ausgegebene Byte muss aber eine
relevante Entscheidung, Evidenz oder eine ausführbare Recovery unterstützen.

## Ausgangslage und belastbarer Ist-Stand

### Bereits umgesetzt und zu bewahren

Die folgenden Arbeiten sind committed und gelten als Ausgangsbasis:

| Bereich | Commit | Bewahrte Invariante |
|---|---|---|
| Status und Analysequalität | `e8f5d619` | Contract v2 trennt Operation, Response-Completeness und Analysequalität; Fehlerprojektion ist atomar. |
| Vor-Dispatch-Validierung | `a87e7f23` | Enum-, Feld- und Indexpfade werden vor teurer Analyse validiert. |
| Scope und Generated | `e306387b` | Gemeinsamer Scopeinput und Generated-/Unknown-Klassifikation für Symbol-, Graph-, Klassen- und Composite-Tools. |
| Namespace-Drilldown | `80cd5d89` | Präfixauflösung, Counts, Empty/Truncated und exakter Budgetretry sind korrigiert. |

`93fa150a` hat anschließend Feature-, Test- und Klassenkontext refaktoriert und
weitere Scopetests ergänzt. Deshalb werden die Scopeinvarianten zu Beginn gezielt
revalidiert; dieser Commit ist kein Grund, Slice 03 neu umzusetzen.

### Startvoraussetzungen

- Der spätere Task beginnt auf einem bekannten Git-Stand. Fremde oder laufende
  Working-Tree-Änderungen werden weder übernommen noch bereinigt oder
  mitcommittet. Bei Überschneidungen mit dem Rest-Scope wird zuerst der
  tatsächlich gewünschte Baseline-Commit geklärt.
- Der aktuell mit dieser Agentensitzung verbundene AiNetLinter-MCP-Server basiert
  ausdrücklich auf dem Code-Stand **vor Beginn der Konzeptumsetzung**. Seine
  Wireantworten, `tools/list`-Schemas, Versionsfelder und Laufzeiteffekte dürfen
  weder als aktueller Produktbefund noch als Verifikationsnachweis dieses Tasks
  gewertet werden. Er darf während der Planung und Umsetzung ausschließlich als
  semantisches Read-only-Werkzeug zur Analyse des aktuellen Source-Standes
  dienen.
- Sämtliche Runtime-, Wire-, Schema- und Dogfood-Nachweise laufen über die
  vorhandene C#-Testinfrastruktur gegen In-Process- oder Prozesshosts, die aus
  dem jeweils aktuellen Taskstand gebaut werden. Ein formales Release ist dafür
  nicht erforderlich.
- Historische Tasktexte, alte Rohantworten und externe Assemblyfälle sind keine
  Source of Truth. Jeder Restbefund erhält am Start einen aktuellen Red-Test;
  ist er gegen den frischen Build nicht reproduzierbar, wird kein Ersatzproblem
  erfunden und nur der Erhaltungsnachweis dokumentiert.

### Belegter Rest-Scope

| ID | Aktuelle Evidenz | Verbindliche Zielrichtung |
|---|---|---|
| R1 Budgetprojektion | `AssemblyAnalysisWireBudgetProjection` mutiert generisch JSON und nutzt `McpUtf8BudgetTrimmer`; `FindSymbolResponseBudget` kann einen zu kleinen Envelope ablehnen, ohne einen exakt ausführbaren `minimumResponseBytes`-Wert zu liefern. | Toolnahe Auswahl vollständiger Facheinheiten, finale kombinierte UTF-8-Messung und exakt wiederholbarer Mindestwert; keine generische Payloadmutation. |
| R2 Assembly-Referenzscope | `AssemblyFindReferencesTool` erweitert bei jeder Assembly-Handoff-ID Referenzen auch dann, wenn `includeReferences=false` angefragt wurde. Angefragter und effektiver Scope sind nicht eindeutig sichtbar. | Plain Identifier bleiben root-only. Eine explizite Handoff-ID aus einer Referenz-Assembly darf nur ihre exakt adressierte Owner-Assembly öffnen; sie öffnet keine Geschwister oder transitive Closure. Erst `includeReferences=true` erlaubt die bounded Closure. |
| R3 Text und IDs | `FeatureContextFormatter` schreibt `DocCommentId` in Markdown; Compositepfade können Status oder Next redundant darstellen. | Kanonische Handoff-ID genau einmal im StructuredContent; Text enthält Fachbefund und höchstens eine Status-/Next-Darstellung. |
| R4 Target-Health | Zielgebundene Health-Antworten projizieren weiterhin Daemonmodus, Connection, PID, Uptime und Version. | Target-Health enthält ausschließlich Daten des adressierten Projekts oder der adressierten Assembly; globale Daemonaggregate erscheinen nur im globalen Health-Call. |
| R5 Populationen | File Tree und Index Scope unterscheiden inzwischen physische Dateien und Roslyn-Dokumente, aber Sammelfelder wie `excludedCount` machen Einheit und Ursache nicht überall eindeutig; Text und StructuredContent sind nicht vollständig selbsterklärend. | Benannte Populationen und getrennte Ausschlussursachen/-einheiten; kein Neuaufbau bereits eindeutiger Counts. |
| R6 Restbereinigung | Der harte Schnitt ist noch nicht abgeschlossen; generische Trimmer, mögliche Doppelmodelle, veraltete Beispiele und Architekturdrift müssen nach den Fachänderungen erneut geprüft werden. | Nur tatsächlich ersetzte Pfade löschen, Owner schärfen, Dokumentation auf Endzustand bringen und alle Gates schließen. |

## Source of Truth und betroffene Bereiche

Fachliche Wahrheit liefern in dieser Reihenfolge:

1. Roslyn-Symbole, geladene `Solution`-/Assembly-Snapshots und metadata-only
   Assemblyanalyse;
2. die normalisierten Contract-, Scope-, Handoff- und Wiremodelle unter
   `src/AiNetLinter/Mcp/`;
3. toolnahe Domänenergebnisse und Budgetprojektoren für Auswahl, Reihenfolge und
   Counts;
4. die per C#-Contracttest beziehungsweise taskaktuellem Prozesshost
   publizierten `tools/list`-Schemas;
5. Contract-, Fast-, Integration- und Dogfood-Tests.

Dokumentation beschreibt den implementierten Vertrag, ersetzt ihn aber nicht.
Voraussichtlich betroffen sind:

- `src/AiNetLinter/Mcp/Wire/` und die toolnahen Response-Budgetklassen;
- `src/AiNetLinter/Mcp/Assemblies/Analysis/` und
  `src/AiNetLinter/Mcp/Tools/SymbolGraph/` für Assemblyrouting und Handoffs;
- Feature-/Testformatter und gemeinsame Navigationstexte;
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`;
- File-Tree- und Index-Scope-Modelle, Renderer und Registrierungen;
- passende Tests unter beiden MCP-Testbäumen;
- `Docs/agent-api.md`, `Docs/integration.md`, bei tatsächlicher Betroffenheit
  `Docs/configuration.md`, eingebettete Guides, Server-Instructions,
  `src/AiNetLinter/Mcp/IsErrorPolicy.md` und die MCP-Workflowregel.

Lokale Datei- oder Typnamen darf der Orchestrator an eine inzwischen bessere
Architektur anpassen. Diese Liste ist keine Erlaubnis für fachfremde Änderungen.

## Verbindliche Fachsemantik

### Erhaltungsvertrag

- Jede zielgebundene Response verwendet Contract v2 mit genau einem Statusowner.
- `operation=ok` besitzt keinen Fehlercode. Fehler haben `operation=error`,
  `completeness=not_applicable`, einen Code und `isError=true`.
- Response-Completeness und Analysequalität bleiben getrennte Achsen. Empty,
  Partial und Truncated werden nicht ineinander umgedeutet.
- `scopeType=all|production|tests` und `includeGenerated=false|true` werden vor
  Limits und Budget angewandt. Seed-Ausnahmen und notwendige Graphbrücken sind
  markiert.
- Exakter Namespace-Drilldown liefert ehrliche Vollmengen-/Shown-Counts und eine
  ausführbare Budget-Recovery.
- Validierung läuft vor Lease, Decompilation oder fachlicher Analyse und nennt
  den konkreten JSON-Feld- beziehungsweise Indexpfad.

### Budget und Projektion

- Scanner erzeugen vollständige Domänenergebnisse. Toolnahe Projektoren wählen
  deterministisch gerankte, vollständige semantische Einheiten; Formatter
  rendern exakt dieselbe Auswahl.
- Gemessen werden finaler Text plus StructuredContent inklusive Navigation in
  UTF-8. Größere Budgets erweitern die Evidenz monoton.
- Jeder gültige Default kann mindestens die fachliche Mindestprojektion
  darstellen. Ein kleineres Budget liefert `RESPONSE_BUDGET_TOO_SMALL` mit
  `fieldPath=$.maxResponseBytes`, `requestedBytes` und einem
  `minimumResponseBytes`, der beim identischen Request und Snapshot exakt
  erfolgreich ist.
- Gemeinsame Wirelogik darf messen und gemeinsame Fehlerdaten erzeugen, aber
  keine unbekannten JSON-Arrays, Strings oder „größten Sektionen“ fachblind
  kürzen.

### Assemblyscope und Handoff-Lebenszeit

`includeReferences` steuert Suchbreite, nicht die Gültigkeit einer expliziten
Identität:

| Requestform | `includeReferences=false` | `includeReferences=true` |
|---|---|---|
| Plain Identifier auf Root-Target | ausschließlich Rootassembly | Root plus bounded Referenzclosure |
| Kanonische Root-Handoff-ID | ausschließlich Rootassembly | Root plus bounded Referenzclosure |
| Kanonische Handoff-ID aus Referenz-Assembly | ausschließlich die durch die ID belegte Owner-Assembly; keine Closure | Owner plus bounded Referenzclosure gemäß Toolvertrag |

Das Öffnen der exakt adressierten Owner-Assembly ist keine implizite
Referenzsuche. Die Response spiegelt `requestedIncludeReferences`, den
effektiven Suchmodus (`root_only`, `symbol_owner_only` oder
`bounded_reference_closure`), untersuchte Assemblyzahlen, Limits und partielle
Diagnostics. Cachekeys enthalten Handoff-Owner, Suchmodus und alle weiteren
semantikverändernden Requestdimensionen.

Handoffs bleiben bei unverändertem Target und Inhalt über Eviction und Neustart
gültig. Falsches Target, veralteter Snapshot und ungültige Form bleiben
disjunkte Fehler. IDs werden in Fehlertexten weder vollständig noch mehrfach
gespiegelt.

### Text, Health und Populationen

- Eine kanonische Handoff-ID steht ausschließlich im dafür vorgesehenen
  StructuredContent-Feld `id`; gleichwertige `docCommentId`-/`symbolId`-Kopien
  und ID-Ausgaben im Markdown entfallen. Response-lokale Graph-IDs bleiben als
  solche eindeutig benannt.
- `ok + complete` benötigt keinen Navigationsfooter. Empty, Partial, Truncated
  und Error erhalten höchstens eine kurze Statuszeile und genau eine
  strukturierte Recovery; Text wiederholt sie höchstens einmal.
- Globaler Health darf begrenzte Daemonaggregate liefern. Zielgebundener Health
  liefert weder PID, Connection-/Daemon-Keys, globale Uptime/Version noch Daten
  anderer Targets.
- Populationen nennen Einheit und Bezugsmenge, beispielsweise
  `physicalFileCount`, `roslynDocumentCount`, `excludedFileCount` und
  `skippedDirectoryCount`. Nutzerpatterns, Defaultverzeichnisse, Reparse-Points
  und unlesbare Teilbäume werden nicht in einem mehrdeutigen Sammelcount
  vermischt.

## Muss-Kriterien

1. Die umgesetzten Contract-v2-, Validierungs-, Scope- und Namespace-
   Invarianten bleiben vollständig grün.
2. Alle budgetierten Resttools besitzen eine toolfachliche Mindestprojektion,
   exakte Retrywerte und monotone kombinierte UTF-8-Budgets.
3. Kein generischer JSON-/Array-/Stringtrimmer oder nachträglicher
   fachunabhängiger Payloadmutator bleibt im Produktionspfad.
4. Assembly-Routing unterscheidet Root, expliziten Symbol-Owner und bounded
   Closure deterministisch und spiegelt Request sowie effektiven Scope.
5. Handoff-IDs erscheinen genau einmal strukturiert und nie im regulären Text.
6. Composite- und Health-Texte enthalten weder redundante Status-/Next-Zeilen
   noch zielsfremde Betriebsinformationen.
7. File-/Dokumentpopulationen und Ausschlusseinheiten sind maschinenlesbar und
   im Text eindeutig, ohne bereits klare Modelle unnötig umzubauen.
8. Ersetzte DTOs, Adapter, Formatter, Tests und Beispiele sind entfernt;
   Dokumentation und Runtime veröffentlichen nur den Endzustand.
9. Dogfood, Auditorprüfung, MCP-Quality-Gate, Build und beide Nicht-Stress-
   Testsuiten sind ohne offene auftragsbezogene Findings grün.

## Akzeptanzkriterien

### Baseline und Budget

1. Ein aus dem aktuellen Taskstand gebauter In-Process- oder Prozesshost
   veröffentlicht Contract v2 sowie die erwarteten Scopeparameter; Source,
   `tools/list` und Runtime stimmen überein. Der bereits verbundene alte
   MCP-Server ist von diesem Nachweis ausgeschlossen.
2. Bestehende Contract-, Validation-, Scope- und Namespace-Regressionen bleiben
   grün; eine Negativsuche findet keinen v1-Producer, -Parser oder Beispielpfad.
3. Default, 512 Bytes, exakt gemeldetes Minimum, Werte dazwischen und Maximum
   sind durch Contracttests abgedeckt.
4. Der gemeldete Mindestwert führt beim identischen Request und Snapshot exakt
   zum Erfolg; Text plus StructuredContent bleiben innerhalb dieses Werts.
5. Größeres Budget fügt nur ganze Facheinheiten hinzu und entfernt keine zuvor
   sichtbare Evidenz.
6. `McpUtf8BudgetTrimmer`, generische Assembly-JSON-Kürzung und äquivalente
   Produktions-/Testpfade sind entfernt.

### Assemblyrouting

7. Plain- und Root-Handoff-Calls mit `includeReferences=false` öffnen und
   durchsuchen keine Referenz-Closure.
8. Ein Handoff aus einer Referenz-Assembly mit `false` öffnet höchstens seine
   exakt adressierte Owner-Assembly und durchsucht weder Rootgeschwister noch
   transitive Referenzen.
9. `includeReferences=true` nutzt ausschließlich die dokumentierte bounded
   Closure und weist Limits, untersuchte/gesamte Assemblies sowie partielle
   Diagnostics aus.
10. False/True sowie unterschiedliche Handoff-Owner besitzen getrennte
    Cache-/Ausführungspfade; Aufrufreihenfolge kontaminiert den effektiven Scope
    nicht.
11. Ein referenzierter Treffer kann nach Eviction und Serverneustart sicher zu
    Body und References verkettet werden, ohne als Root umetikettiert zu werden.
12. Fehlende Referenzen liefern vorhandene lokale Evidenz mit partieller
    Analysequalität statt Silent-Empty oder Complete.

### Signalqualität und Populationen

13. Keine reguläre Source- oder Assembly-Textantwort enthält eine Handoff-ID;
    StructuredContent enthält pro fachlicher Identität genau ein kanonisches
    `id`-Feld.
14. Feature-/Testkontext und andere betroffene Composites stellen denselben
    Status und Next-Schritt jeweils höchstens einmal dar.
15. Zielgebundener Health enthält nur das angefragte Target; globaler Health
    behält begrenzte globale Aggregate.
16. File Tree und Index Scope benennen jede Population und Ausschlusseinheit;
    insbesondere sind übersprungene Verzeichnisse keine ausgeschlossenen
    Dateien oder Roslyn-Dokumente.

### Architektur, Dokumentation und Release

17. Source- und Assemblypfade verwenden gemeinsame Contractmodelle, aber
    toolnahe Fachprojektoren; es gibt keine zweite gleichwertige Status-, Scope-
    oder Fehlerhierarchie.
18. Im Änderungsscope bleiben keine toten Adapter, alten DTO-Felder,
    pass-through Middlemen, fachlosen Magic Values oder unterdrückten
    Architekturverstöße zurück.
19. Produktdokumentation, Guides, Server-Instructions, `tools/list` und Tests
    verwenden dieselben Felder, Enums, Defaults und Beispiele und enthalten
    keine Migrationshistorie.
20. Synthetische Dogfoodfälle decken Budgetretry, False/True/Owner-only-
    Assemblyscope, Handoff-Lebenszeit, ID-Freiheit, Target-Health und Populationen
    ab; alle Abschlussgates sind grün.
21. Kein Test, Snapshot, Log, Konzept oder Dokument enthält vertrauliche externe
    Assemblyidentitäten, Pfade oder Rohantworten.

## Non-Goals

- Keine erneute Implementierung der bereits grünen Slices 01 bis 04 ohne
  aktuellen reproduzierbaren Gegenbefund.
- Keine Kompatibilitätsschicht für Contract v1 und keine Migrationsdokumentation.
- Keine neue Lint-Capability-Matrix und kein allgemeiner Neuaufbau fachfremder
  Lint-, Metrik-, Call-Graph-, Test- oder Decompilationslogik.
- Keine Änderung externer MCP-Clients oder Modellprompts.
- Kein modellabhängiges Tokenbudget; die Servergrenze bleibt kombinierte UTF-8-
  Bytes.
- Keine unbounded Referenzanalyse, kein Ausführen und kein dynamisches Laden
  untersuchter Assemblies.
- Keine externen Auditfälle als Fixtures und kein Publish/Deployment.
- Kein Zwischenrelease, nur um den alten verbundenen MCP-Server für diesen Task
  zu aktualisieren. Ein Release kommt erst nach abgeschlossenem Release-Gate
  und auf ausdrücklichen Nutzerwunsch infrage.
- Keine automatischen Stressläufe.

## Serieller Umsetzungsplan

Für jeden Implementierungsslice gilt: aktuellen Baseline- und Working-Tree-
Stand prüfen, Restbefund reproduzierbar rot machen, kleinste fachliche Lösung
umsetzen, fokussierte Tests grün ausführen, ersetzte Pfade löschen, Diff und
`git diff --check` prüfen, read-only reviewen und ausschließlich eigene Dateien
committen. Keine zwei Rollen, Builds, Tests oder MCP-Prüfungen laufen parallel.

### Slice 01 – Baseline und testlokale Runtime synchronisieren

- In-Process- und bei Wire-/Lifecycle-Fragen den vorhandenen C#-Prozesshost aus
  dem aktuellen Taskstand bauen; Source, `tools/list` und Runtime damit
  gegeneinander prüfen. Den bereits verbundenen alten MCP-Server nicht für
  Verhaltens-, Schema- oder Versionsprüfungen aufrufen.
- Die vier erledigten Bereiche fokussiert revalidieren, insbesondere die durch
  `93fa150a` berührten Scopepfade.
- R1 bis R6 mit aktuellen Red-Tests beziehungsweise belastbaren Negativsuchen
  bestätigen. Nicht reproduzierbare Punkte werden aus dem aktiven
  Implementierungsscope gestrichen, ohne Ersatzarbeit zu erfinden.

**Exit:** AK 1–2 sind belegt; Baseline und Restmatrix sind eindeutig. Reine
Verifikation erzeugt keinen künstlichen Commit.

### Slice 02 – Fachliche Budgetprojektion abschließen

- Assembly-, Symbol-, Feature-, Test-, Klassen- und sonstige im Red-Test
  betroffene Budgetpfade auf toolnahe Mindestprojektionen umstellen.
- Finale Navigation in Messung und Mindestwertberechnung einbeziehen.
- Generischen Trimmer und generische Assembly-Payloadmutation samt ersetzten
  Tests vollständig löschen.

**Exit:** AK 3–6 und Muss 2–3 sind grün; Defaultantworten bleiben
handlungsfähig und jeder Retrywert ist ausführbar.

### Slice 03 – Assembly-Suchbreite und Handoffs trennen

- Root-, Symbol-Owner- und Closure-Modus explizit modellieren.
- Auflösung, Lease-/Sessionöffnung, Counts, Diagnostics und Cachekeys an den
  effektiven Modus binden.
- Reihenfolge-, Eviction-, Neustart-, Missing-Reference- und Cross-Target-
  Regressionen ergänzen.

**Exit:** AK 7–12 und Muss 4 sind grün; `false` öffnet nie still eine Closure,
referenzierte Handoffs bleiben trotzdem direkt nutzbar.

### Slice 04 – Text-, ID- und Health-Signal härten

- Handoff-IDs und redundante Status-/Next-Ausgaben aus allen betroffenen
  Textpfaden entfernen; genau ein strukturiertes `id` behalten.
- Globalen und zielgebundenen Health-Payload samt Formatter trennen.
- Lange oder vertrauliche Eingaben in Fehlern begrenzen.

**Exit:** AK 13–15 und Muss 5–6 sind grün; Text enthält nur Fachsignal des
angefragten Scopes.

### Slice 05 – Populationen und Architektur bereinigen

- Nur noch mehrdeutige Populationen und Ausschlussfelder präzisieren; bereits
  eindeutige Counts beibehalten.
- Nach allen Fachänderungen konkurrierende Modelle, tote Adapter, alte Tests,
  generische Mutatoren und fachlose Magic Values entfernen.
- Zu große Verantwortungsbereiche fachlich zerlegen, nicht durch Suppressionen
  oder pass-through Dateien kosmetisch verschieben.

**Exit:** AK 16–18 und Muss 7–8 sind grün; jede Zahl besitzt erkennbare Einheit
und Owner.

### Slice 06 – Endzustandsdokumentation, Dogfood, Auditor und Release

- Dokumentation, Guides, Server-Instructions und Schemas ausschließlich auf den
  implementierten Endzustand synchronisieren.
- Dogfoodmatrix ausschließlich in der vorhandenen C#-Testinfrastruktur gegen
  den frischen Build beziehungsweise deren Prozesshost ausführen.
- Den projektspezifischen Auditor genau einmal seriell auf vollständigen
  Änderungsscope anwenden; belastbare Findings beheben und gezielt nachprüfen.
- Abschlussgates ausführen und vollständigen Diff sowie Git-Status prüfen.

**Exit:** AK 19–21, alle Muss-Kriterien und das Release-Gate sind grün; kein
auftragsbezogenes Finding und kein ersetzter Vertragspfad verbleiben. Ein
anschließendes echtes Release ist ein separater, ausdrücklich beauftragter
Schritt und kein Bestandteil dieses Tasks.

## Verifikation und Release-Gate

Fokussierte Tests richten sich nach dem jeweiligen Slice und verwenden nur aus
dem Taskstand gebaute Test-/Prozesshosts. Vor Taskabschluss laufen strikt
seriell:

```powershell
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
git diff --check
```

Danach folgen gegen den finalen Source-Scope:

- `get_violations` mit null offenen auftragsbezogenen Verstößen;
- `safeguard(minScore: 10)` mit 10,0/10;
- gezielte `find_dead_code`- und `find_magic_values`-Prüfungen;
- Negativsuchen nach Contract v1, alten Aliasfeldern, generischen Trimmern und
  ersetzten DTO-/Formatterpfaden;
- Prüfung aller 9 Muss- und 21 Akzeptanzkriterien.

Die Bezeichnung Release-Gate meint die Freigabereife des Repository-Standes,
nicht das Publizieren eines Releases. Der bereits verbundene alte MCP-Server
wird auch hier nicht als Runtime-Orakel verwendet.

Stress-Tests laufen nur auf ausdrückliche Anforderung. Unklare oder
abgebrochene Testläufe werden per TRX diagnostiziert, nicht blind wiederholt.

## Dokumentationsbedarf

- `Docs/agent-api.md` beschreibt Status-/Analyseachsen, Budgets, exakten
  Retrywert, Assembly-Suchmodi, Handoff-Chaining und Populationseinheiten.
- `Docs/integration.md`, Guides, Server-Instructions und MCP-Workflowregel werden
  nur dort geändert, wo sie denselben öffentlichen Vertrag erklären.
- `Docs/configuration.md`, `ainetlinter-rules.json` und READMEs werden nur bei
  tatsächlicher inhaltlicher Betroffenheit angepasst; `README.md` nur nach
  ausdrücklichem Auftrag.
- Dokumentation zeigt ausschließlich den finalen Ist-Zustand, keine Task-
  Historie, Versionsmigration oder vertrauliche Auditdaten.

## Risiken und Gegenmaßnahmen

- **Alter verbundener MCP-Prozess verfälscht Befunde:** Seine Toolantworten nur
  zur semantischen Source-Navigation verwenden; alle Vertragsbelege aus
  taskaktuell gebauten C#-Test-/Prozesshosts gewinnen.
- **Budgetfix verliert Evidenz:** ganze Facheinheiten, monotone Tests und exakter
  Retry am finalen Wirepayload.
- **`false` bricht referenzierte Handoffs:** expliziten Owner-only-Modus statt
  impliziter Closure oder pauschaler Ablehnung verwenden.
- **Cache kontaminiert Suchbreite:** effektiven Modus und Handoff-Owner in Keys,
  Counts und Tests aufnehmen.
- **Breite Architekturhärtung wächst aus dem Scope:** nur durch Reständerungen
  berührte oder nachweislich konkurrierende Pfade bearbeiten.
- **Fremde parallele Änderungen geraten in Commits:** Baseline und Pfadliste pro
  Slice prüfen, nur eigene Pfade stagen, bei Überschneidung stoppen.
- **Vertrauliche Evidenz gelangt ins Repository:** ausschließlich kleine,
  synthetische, repositoryeigene Fixtures persistieren.

## Verworfene Alternativen

- Das ursprüngliche Zehn-Slice-Konzept unverändert fortsetzen: würde vier
  erledigte Slices erneut planen und einen veralteten Unterbrechungsstand als
  Wahrheit behandeln.
- `includeReferences=false` für referenzierte Handoffs pauschal ablehnen:
  schützt Root-only, verschlechtert aber sicheres Agenten-Chaining unnötig.
- Bei einer Handoff-ID mit `false` still die gesamte Closure öffnen: verletzt
  Requestsemantik, Kostenkontrolle und Cacheisolation.
- Budgetdefault nur erhöhen oder einen generischen Trimmer behalten: verdeckt
  fehlerhafte Mindestprojektionen und kann Fachwert nicht beurteilen.
- Populationen vollständig neu modellieren: unnötiger Umbau; der aktuelle Stand
  braucht nur präzise Einheiten und Ausschlussursachen.
- Restprobleme nur im Text korrigieren: StructuredContent, Schemas und Runtime
  blieben widersprüchlich.

## Offene Entscheidungen

Keine. Die erneute Nutzerfreigabe des bereinigten Gesamtvertrags steht noch aus,
ist aber keine fachliche Designfrage.

## Abschlussbedingung

Der Task ist abgeschlossen, wenn alle reproduzierbaren Restbefunde behoben,
alle ersetzten Pfade gelöscht, die vier erledigten Grundlagen unverändert
abgesichert und Dogfood, Auditor sowie sämtliche Release-Gates grün sind. Der
aktuelle Draft darf erst nach ausdrücklicher Freigabe auf `status: ready`
gesetzt werden; anschließend wird der Orchestrator separat mit diesem
Task-Verzeichnis gestartet.
