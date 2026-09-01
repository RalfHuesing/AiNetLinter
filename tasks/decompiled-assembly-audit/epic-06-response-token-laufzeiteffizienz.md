# Epic 6 – Response-, Token- und Laufzeiteffizienz

## Findings – Bug

### E6-BUG-01 – `MaxResponseBytes` ist kein globales CallToolResult-Budget

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:120-128` – `FitsResponseBudget` prüft Text und Structured Content getrennt.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:19-30` – `Enrich` wird vor der getrennten Messung ausgeführt.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/McpToolResults.cs:205-225` – `Text<T>` erzeugt beide Content-Kanäle in einem Ergebnis.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/Responses/InspectAssemblyResponseBuilder.cs:78-89` und `FindAssemblyExtensionsResponseBuilder.cs:56-63` – finale Budgetprojektion.
- Befund: `FitsResponseBudget` verlangt `textBytes <= 8192` **und** `structuredBytes <= 8192`, misst aber nicht die Summe des CallToolResult. `Text<T>` liefert Text und Structured Content gemeinsam; `Enrich` ergänzt dabei weitere Metadaten in beide Darstellungen.
- Redigierte MCP-Evidence: Bei den großen, referenzerweiterten `inspect_assembly`-Abfragen lagen die kanalweisen Werte jeweils unter 8192 Byte, die Summe aber bei `LOCAL-01=11585`, `LOCAL-02=11340` und `LOCAL-03=13271` Byte. Ein kleines `LOCAL-03`-Ergebnis lag bereits bei `4773 + 6546 = 11319` Byte. Bei `find_assembly_extensions` lag das große `LOCAL-03`-Ergebnis bei `3009 + 7809 = 10818` Byte. Die Werte sind Text-/Structured-Nutzdaten ohne zusätzlichen Transport-/Envelope-Overhead.
- Auswirkung: Falls `MaxResponseBytes` – wie Name, Konzept und globale Budgetsemantik nahelegen – das gesamte Response-Artefakt begrenzen soll, können beide Kanäle zusammen deutlich größer als das Limit werden. Gleichzeitig trimmt die Projektion wiederholt, obwohl ein Kanal noch ungenutztes Budget haben kann. Agenten sehen keinen eindeutigen globalen Budgetwert.
- Empfehlung: Eine einzige finale Budgetfunktion über das tatsächlich ausgelieferte Ergebnis einführen. Sie sollte Text, Structured Content und den für die serialisierte Hülle relevanten Overhead gemeinsam messen; alternativ muss der Vertrag ausdrücklich in getrennte Kanalbudgets umbenannt und dokumentiert werden. Die bestehende 4-KiB-Dokumentations-/8-KiB-Code-Differenz aus `E1-BUG-02` wird hier nicht erneut bewertet.
- Abgrenzung: Die gemessene Summe ist eine belastbare Untergrenze, keine Behauptung über den exakten Transport-Overhead. Die sichtbaren Listen stammen aus demselben Payload; ein eigenständiger Text-vs-Structured-Inhaltsverlust wurde in diesem Scope nicht nachgewiesen.

### E6-BUG-02 – Irreduzible feste Metadaten können das Budget trotz Trimming überschreiten

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **mittel-hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:15-57` – Trim-Schleifen enden mit dem zuletzt projizierten Kandidaten, auch wenn dieser weiterhin nicht passt.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:63-90,130-203,205-254` – nur Referenz-/Session-/Diagnose-/Member-/Typ-/Namespace-/Extension-Listen werden entfernt.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:131-168` – feste Payloadfelder wie Assembly-Pfad, Identität, Origin und Status bleiben bestehen.
- Befund: Die Budgetprojektion hat keinen terminalen Fallback, wenn nach dem Entfernen aller optionalen Listenelemente `FitsResponseBudget` weiterhin `false` liefert. Lange feste Metadaten werden weder begrenzt noch in einen kontrollierten Budgetfehler umgewandelt.
- Auswirkung: Für zulässige, aber ungewöhnlich lange Pfad-/Identitäts-/Metadatenwerte ist kein garantierter globaler Response-Budgetvertrag ableitbar. `TruncatedBy=responseBudget` kann dann zusammen mit einem noch übergroßen Ergebnis zurückkehren; ein nachgelagerter `Enrich`-Schritt prüft das Ergebnis nicht erneut gegen einen globalen Gesamtwert.
- Empfehlung: Feste Darstellungswerte mit eigenen Zeichen-/Bytebudgets projizieren, optionale Metadaten explizit weglassen können und einen maschinenlesbaren terminalen Zustand wie `budgetExceeded`/`irreducibleBudget` ausgeben. Der Fallback muss selbst bounded sein.
- Abgrenzung/offene Unsicherheit: Die normale Matrix mit üblichen lokalen Pfaden reproduzierte diesen irreduziblen Fall nicht. Ob die jeweils unterstützte Laufzeit-/Dateisystempfadlänge einen praktisch relevanten Extremfall zulässt, ist noch zu verifizieren; der Kontrollfluss garantiert ihn derzeit jedenfalls nicht.

## Findings – Optimierung

### E6-OPT-01 – Einzelweises Trimming serialisiert den Payload wiederholt

- Priorität: **P2**
- Größe: **L**
- Vertrauen: **hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:15-57,60-118` – Schleifen entfernen jeweils genau ein Listenelement.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:120-128` – jede Iteration formatiert Text und serialisiert Structured Content erneut.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:19-61` – `Enrich`/JSON-Klonpfad ist Teil der Messung.
- Befund: Die Reihenfolge ist deterministisch und semantisch nachvollziehbar, aber ein übergroßer Payload wird durch viele Einzelentfernungen in `O(n)` Projektionen auf die jeweils anwachsende Responsegröße reduziert. Jede Probe erzeugt erneut Text und JSON.
- Redigierte MCP-Evidence: Die großen Limits `maxResults=1000`/`maxMembers=1000` führten trotz kleiner sichtbarer Ergebnisse zu `shownCount=3..7` Typen beziehungsweise `shownCount=6` Extensions; bei der Referenzerweiterung wurden gleichzeitig bis zu 4039 Sessions gezählt. Die MCP-Abfragen belegen den Projektionsdruck, nicht eine konkrete Laufzeitmessung.
- Auswirkung: Worst-Case-Latenz und AI-Context-Verbrauch wachsen mit der Zahl der zu entfernenden Elemente, obwohl am Ende nur wenige Items sichtbar sind. Die gleiche Arbeit wird für Text- und Structured-Budgetproben wiederholt.
- Empfehlung: Fixed-Overhead zuerst kalkulieren, Quoten pro Liste in einem Pass bestimmen und – falls eine exakte Messung erforderlich bleibt – per bounded/binary search eine Zielgröße ermitteln. Den finalen Payload erst einmal vollständig materialisieren und danach einmal anreichern/serialisieren.
- Abgrenzung: Die sichtbare Reihenfolge sowie die Truncation-Marker werden nicht als fachlicher Fehler bewertet; es geht um die Projektionstechnik und vermeidbare Wiederholungen.

### E6-OPT-02 – Query-Limits begrenzen die vorgelagerte Analyse- und DTO-Arbeit nicht

- Priorität: **P2**
- Größe: **L**
- Vertrauen: **hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs:58-95` – vollständiges Sammeln/Sortieren vor `Take(options.MaxResults)`.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisService.cs` im `ToTypeDto`-Pfad – Member werden vor `Take(options.MaxMembers)` vollständig in DTOs überführt.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyDecompilationAdapter.cs:26-42,151-229` – Snapshot-/Typbaumaufbau und Auswahl nach Decompilation-Budgets.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisSession.cs:167-195` – Snapshotaufbau vor der konkreten Query-Projektion.
- Befund: `maxResults` und `maxMembers` sind überwiegend Ausgabegrenzen. Die Services materialisieren und sortieren erst alle passenden Typen/Extensions beziehungsweise Member; die Session erstellt vorher den bounded Decompilation-Snapshot mit eigenen Grenzwerten (`MaxTypes=2000`, `MaxMembers=20000`).
- Redigierte MCP-Evidence: `LOCAL-03` meldete bei `inspect_assembly` `totalTypes=380`, zeigte mit kleinem Limit einen Typ und mit großem Limit wegen Response-Budget nur drei. Bei `find_assembly_extensions` standen `totalExtensions=65` einem kleinen Ergebnis von eins und einem großen Ergebnis von sechs gegenüber. Die kleinen und großen Aufrufe hatten damit dieselbe vorgelagerte Analysebasis; eine Laufzeitmessung wurde bewusst nicht durchgeführt.
- Auswirkung: Kleine Agentenabfragen sparen nicht proportional Analyse-, Sortier-, DTO- oder Snapshotkosten. Das vergrößert Latenz und Speicherbedarf und ist bei referenzerweiternden Routen besonders teuer.
- Empfehlung: Filter und stabile Sortierung so früh wie semantisch möglich anwenden, begrenzte Auswahlstrukturen/Streaming verwenden und Query-Ausgabegrenzen von Snapshotgrenzen trennen. Gesamtzahlen sollten aus Metadaten kommen, ohne alle sichtbaren DTOs vorab zu materialisieren.
- Abgrenzung: Die Decompilation bleibt metadata-only/signature-only; dieser Befund behauptet keine Ausführung externer Assemblies.

### E6-OPT-03 – Referenz-Session-Arbeit amplifiziert sich über die sichtbare Referenzgrenze hinaus

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceResolver.cs:18-20,65-129` – `MaxReferenceDepth=8` und `MaxReferenceNodes=128` begrenzen die Graphknoten.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyReferenceSessionExpander.cs:23-60,89-163` – alle geordneten Referenzen werden besucht; nach dem Node-Limit können weitere Boundary-Sessions/Diagnosen entstehen.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:56-74` – erst die Projektion begrenzt sichtbare Referenzen auf 32 und Sessions auf 32.
- Befund: Die sichtbare Grenze `MaxReferences=32`/`MaxReferenceSessions=32` ist keine gleichwertige Arbeitsgrenze. Der Expander verarbeitet weitere Referenzkanten, sammelt Sessions und Diagnosen und projiziert erst danach. Besonders relevant ist `TryAddNodeBoundary`: Nach Erreichen des Node-Limits wird weiterhin für nachfolgende Kanten Boundary-Status materialisiert.
- Redigierte MCP-Evidence: Große `inspect_assembly`-Aufrufe meldeten für `LOCAL-01`, `LOCAL-02`, `LOCAL-03` insgesamt `4039`, `1482` und `1519` Reference-Sessions, während wegen der Antwortbudgets nur jeweils eine Session sichtbar war. Große Extension-Abfragen zeigten dieselbe Größenordnung. Referenzlisten lagen trotz `MaxReferences=32` vor der finalen Budgetprojektion bei 203, 159 beziehungsweise 137 Gesamt-Referenzen.
- Auswirkung: CPU, Speicher und Diagnosevolumen wachsen deutlich über das hinaus, was ein Agent im Response nutzen kann. Die Arbeit konkurriert direkt mit nützlichen Typ-/Extensiondaten um das Response-Budget.
- Empfehlung: Einen separaten Hard-Cap für besuchte Kanten/Sessions/Boundary-Einträge einführen, nach dem Cap aggregieren statt pro Restkante zu materialisieren und die verbleibenden Mengen nur als bounded Counters ausgeben. `MaxReferenceNodes`, Session-Cap und Response-Cap sollten eine gemeinsame Kostenstrategie erhalten.
- Abgrenzung: Die erzwungene Referenzerweiterung von `find_assembly_extensions` ist der bereits dokumentierte `E1-BUG-01`; hier wird ausschließlich die daraus messbare Arbeits-/Payload-Kostenfolge bewertet.

### E6-OPT-04 – Diagnose-Samples sind prefix-/root-first statt byte-effizient und repräsentativ

- Priorität: **P3**
- Größe: **S**
- Vertrauen: **mittel-hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:179-204` – root-first-Aufteilung zwischen Root- und Transitivdiagnosen.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.cs:206-244` – deduplizierendes Prefix-Scanning und `break`, sobald das Diagnose-Bytebudget überschritten würde.
- Befund: Die Auswahl bevorzugt Rootdiagnosen und beendet die Kandidatenschleife beim ersten nicht mehr passenden normalisierten Sample. Ein späteres, kürzeres oder bislang nicht vertretenes Sample wird nicht mehr geprüft. Das ist deterministisch, nutzt das Budget aber nicht zwingend vollständig und kann transitive Signale verlieren.
- Redigierte MCP-Evidence: Positive große Abfragen lieferten `totalDiagnostics=195` beziehungsweise `200`, aber jeweils nur ein sichtbares Sample bei `MaxDiagnostics=50` und `MaxDiagnosticBytes=4096`; die resultierenden Diagnosesamples waren zusätzlich durch `responseBudget` markiert. Damit ist die Auswahlentscheidung im Live-Payload nicht als repräsentative Stichprobe überprüfbar.
- Auswirkung: Bei hoher Diagnosezahl erhält der Agent eine stabile, aber möglicherweise redundant-frühe Auswahl; die bereits vorhandenen Root-/Transitivcounts bleiben erhalten, die Ursachenabdeckung kann aber schlechter sein.
- Empfehlung: Nach der fachlich gewünschten Priorität eine bounded Round-Robin-/Coverage-Auswahl durchführen und bei Byteüberlauf Kandidaten überspringen statt sofort abzubrechen. Counts, Deduplizierung und `samplesTruncated` müssen unverändert sichtbar bleiben.
- Abgrenzung/offene Unsicherheit: Root-first kann eine bewusste Prioritätsentscheidung sein. Daher Optimierung statt Bug; eine Severity-/Code-Coverage-Semantik ist aktuell nicht erkennbar.

## Findings – Missing Feature

### E6-MF-01 – Maschinenlesbare Budgettelemetrie fehlt

- Priorität: **P2**
- Größe: **M**
- Vertrauen: **hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:131-168` – Payloads enthalten Counts/Truncation, aber keine Bytebudgets oder Istwerte.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:256-268` – `MarkResponseBudget` ergänzt nur Truncation-Gründe.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisResponse.cs:19-30` – finale Messung bleibt intern.
- Befund: Ein Agent kann erkennen, dass `responseBudget` beteiligt war, aber nicht, welches Budget, welcher Kanal oder welcher Anteil die Reduktion ausgelöst hat. Die intern gemessenen Text-/Structured-Bytes werden nicht als bounded Diagnosedaten zurückgegeben.
- Redigierte MCP-Evidence: In den positiven großen Abfragen lagen Structured Content und Text jeweils unter 8192 Byte, ihre Summe jedoch darüber; die Payloads enthielten trotzdem nur den allgemeinen `responseBudget`-Grund. `FALSE-01` blieb als kleiner recoverable Negativpfad ohne Assembly-Payload unterhalb aller geprüften Limits.
- Auswirkung: Automatisierte Folgeabfragen können nicht datengetrieben zwischen stärkerem `maxResults`, fehlender Referenzerweiterung, Diagnose-Reduktion oder anderem Content-Modus wählen. Ursachenanalyse benötigt statische Codekenntnis.
- Empfehlung: Ein optionales, selbst boundedes `responseBudget`-Objekt mit Limit, Text-/Structured-/Gesamt-Istwert, sichtbaren/gesamten Counts und finalen Trim-Ursachen einführen. Keine unredigierten Pfade oder externen Identitäten in diese Telemetrie aufnehmen.

### E6-MF-02 – Namespace-Trimming hat keine feldspezifischen Gesamt- und Truncationwerte

- Priorität: **P3**
- Größe: **S**
- Vertrauen: **hoch**
- Betroffene Dateien/Symbole/Zeilen:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisModels.cs:131-149` – `InspectAssemblyPayload` enthält keine `TotalNamespaces`-/`NamespacesTruncated`-Felder.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponseLimits.Budget.cs:177-203` – `TryRemoveLastNamespace` entfernt Einträge ohne feldspezifische Markierung.
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/InspectAssemblyFormatter.cs:47-51` – Text zeigt nur die aktuell verbleibende Namespace-Liste.
- Befund: Wenn das globale Trimming Namespaces erreicht, bleibt lediglich der allgemeine Top-Level-Grund `responseBudget`. Gesamtzahl und Ausmaß der Namespace-Reduktion sind nicht maschinenlesbar und im Text nicht separat erkennbar.
- Auswirkung: Ein Agent kann eine gekürzte Namespace-Liste nicht von einer vollständig kurzen Liste unterscheiden. Das schwächt progressive Nachabfragen und die Interpretation der Typabdeckung.
- Empfehlung: `TotalNamespaces`, `ShownNamespaces` und `NamespacesTruncated` beziehungsweise einen feldspezifischen `responseBudget`-Grund ergänzen; Text und Structured Content aus derselben Projektion ableiten.

## Evidence-/Scope-Abschnitt

### Scope und Redaction

Geprüft wurde ausschließlich Epic 6: globale Assembly-Response-Budgets, Reduktions-/Trimmreihenfolge, Text-vs-Structured-Content-Konsistenz, Diagnose-Sample-Auswahl, Referenzlimits, Worst-Case-Payloads, AI-context footprint sowie vermeidbare Arbeit und Latenz.

Die lokale Matrix wurde nur zur Zuordnung der opaken Labels verwendet. In diesem Bericht erscheinen ausschließlich `GIT-01`, `LOCAL-01`, `LOCAL-02`, `LOCAL-03` und `FALSE-01`. Konkrete externe Assembly-Namen, Namespaces, Pfade, URLs, Hashes und dekompilierte Inhalte sind weder enthalten noch für die Evidence wiederholt. Assembly-Abfragen waren metadata-only; es wurde keine externe Assembly geladen, instanziiert oder ausgeführt.

`GIT-01` war für diesen Epic-6-Spotcheck nicht erforderlich: Die Response-/Budgetfragen sind durch die lokalen positiven Fälle und den negativen Nicht-.NET-Fall abgedeckt. Der bestehende Provider-/Origin-Befund bleibt in seinem bisherigen Epic und wird nicht dupliziert.

### Read-only gelesene Nachweise (keine ausgeführten Änderungen)

- `C:/Daten/Entwicklung/Ralf/AiNetLinter/AGENTS.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinterRichtlinien.mdc`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinter.mdc`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/rules/AiNetLinter-McpWorkflow.mdc`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-audit/Konzept.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-audit/roadmap.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/tasks/decompiled-assembly-audit/code-map.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/.agents/skills/implement/SKILL.md`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/agent-api.md:355-375,419-435`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/integration.md:313-332,365-371,475-498`
- `C:/Daten/Entwicklung/Ralf/AiNetLinter/Docs/configuration.md:35-40`
- Response-/Assembly-Produktionscode unter `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` und `src/AiNetLinter/Mcp/Assemblies/Analysis/`, insbesondere die oben pro Finding genannten Symbole.
- Read-only Tests zur Vertrags-/Abdeckungsprüfung:
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolTests.ResponseBudget.cs:18-139`
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisDispatcherCapabilityTests.ResponseBudget.cs:18-40`
  - `C:/Daten/Entwicklung/Ralf/AiNetLinter/src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisDispatcherCapabilityTests.cs:100-139,228-323`

Die Tests bestätigen die derzeitige kanalweise <=8-KiB-Prüfung und gemeinsame sichtbare Samples; sie prüfen keinen kombinierten CallToolResult-Budgetwert, keinen irreduziblen Fixfeld-Fallback, keine proportional frühe Analysebegrenzung und keine repräsentative Sample-Auswahl.

### Relevante aktuelle Konstanten und Verträge

- `AssemblyAnalysisResponseLimits`: `MaxResponseBytes=8192`, `MaxDiagnosticBytes=4096`, `MaxDiagnosticCharacters=256`, `MaxDiagnostics=50`, `MaxReferences=32`, `MaxReferenceSessions=32`, `MaxSessionDiagnostics=3`.
- `AssemblyReferenceResolver`: `MaxReferenceDepth=8`, `MaxReferenceNodes=128`.
- `AssemblyDecompilationOptions`: `MaxAssemblyBytes=64 MiB`, `MaxTypes=2000`, `MaxMembers=20000`, `MaxDocumentCharacters=2,000,000`, Default-Timeout 30 Sekunden.
- Die öffentlich gelesene Dokumentation nennt zusätzlich einen 4-KiB-Responsevertrag. Diese bestehende Inkonsistenz ist `E1-BUG-02`; Epic 6 bewertet daran nur die aktuelle kanalweise/gesamte Budgetwirkung und zählt die Dokumentationsabweichung nicht erneut.

### AI-context footprint

Der aktuelle MCP-Metriklauf für die betroffenen Produktionssymbole ergab:

| Symbol | LOC | AI-context footprint | Bewertung |
|---|---:|---:|---|
| `AssemblyAnalysisResponseLimits` | 498 | 941 | knapp unter LOC-Limit 500; kein eigener neuer Befund |
| `InspectAssemblyResponseBuilder` | 75 | 2450 | 50 unter dem projektweiten Limit 2500 |
| `FindAssemblyExtensionsResponseBuilder` | 86 | 2463 | 37 unter dem projektweiten Limit 2500 |
| `AssemblyAnalysisResponse` | 125 | 2500 | exakt am Limit |
| `AssemblyReferenceSessionExpander` | 135 | 2513 | gemeldeter Überhang; bestehender Epic-3-Befund, hier nicht doppelt als Finding gezählt |

Die Zahlen sprechen gegen weiteres ungezieltes Anwachsen der gemeinsamen Response-/Expander-Symbole. Eine spätere Umsetzung sollte Budgetmessung, Projektion und Telemetrie so schneiden, dass der AI-context footprint nicht weiter in zentrale Monolithen verschoben wird.

## Tatsächlich ausgeführte MCP-Abfragen

Alle target-gebundenen MCP-Aufrufe wurden mit dem aktuellen Schema, `targetType` und einem absoluten `targetPath` ausgeführt. In diesem Bericht sind die konkreten Pfadwerte aus dem Matrixkontext redigiert; die jeweilige Label-Spalte bezeichnet exakt den verwendeten absoluten Matrixpfad.

### Projektgebundene Struktur-/Metrikabfragen

| ID | MCP-Tool und vollständige relevante Parameter | Redigiertes Ergebnis |
|---|---|---|
| P1 | `get_index_scope(targetType=project, targetPath=<absoluter Projektpfad>)` | 886 C#-Dateien, Scope vollständig, nicht gekürzt. |
| P2 | `get_file_tree(targetType=project, targetPath=<absoluter Projektpfad>, root=src/AiNetLinter/Mcp/Tools/AssemblyAnalysis, view=tree, treeDepth=3, maxResults=200, includeMetadata=true, includeLineCount=true)` | 18/18 Einträge gezeigt, 95,8 KiB Ergebnis, nicht gekürzt; Response-Limit-/Builder-/Formatter-Dateien lokalisiert. |
| P3 | `find_symbol(targetType=project, targetPath=<absoluter Projektpfad>, namePatterns=[AssemblyAnalysisResponseLimits, AssemblyAnalysisService, InspectAssemblyResponseBuilder, FindAssemblyExtensionsResponseBuilder, AssemblyAnalysisResponse, McpToolResults, InspectAssemblyFormatter, AssemblyAnalysisToolRegistrations], includeReferences=false, maxResults=100)` | Aktuelle Produktionssymbole und Dateien gefunden; keine Assembly-Identität ausgegeben. |
| P4 | `get_feature_context(targetType=project, targetPath=<absoluter Projektpfad>, symbolIdentifier=AssemblyAnalysisResponseLimits, includeCallers=true, includeTests=true, includeMetrics=true, includeViolations=true, maxCallers=50, maxTests=50)` | Typ 498 LOC, Footprint 941/2500, 47 Caller, 0 gemeldete Violations; Budget-/Responsepfade als direkte Consumer sichtbar. |
| P5 | `metrics_lookup(targetType=project, targetPath=<absoluter Projektpfad>, symbolIdentifiers=[AssemblyAnalysisResponseLimits, AssemblyAnalysisService, InspectAssemblyResponseBuilder, FindAssemblyExtensionsResponseBuilder, AssemblyAnalysisResponse, AssemblyReferenceSessionExpander, AssemblyDecompilationAdapter])` | Werte wie im AI-context-footprint-Abschnitt; `AssemblyReferenceSessionExpander` 2513/2500. |
| P6 | `get_symbol_body(targetType=project, targetPath=<absoluter Projektpfad>, symbolIdentifier=<jeweils relevantes Produktionssymbol>, maxBodyLines=300..450)` | Budgetprojektion, getrennte Byteprüfung, Enrichment, Service-`Take`-Positionen und Expander-Grenzpfade bestätigt; Inhalte wurden nur zur lokalen Analyse gelesen. |

### Redigierte Assembly-Abfragen – `inspect_assembly`

Effektive gemeinsame Parameter für jede Zeile: `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad des Labels>`, `namespace=null`, `typeName=null`, `memberName=null`, `memberNames=null`, `publicOnly=true`. Kleine Abfragen nutzten `includeReferences=false`, `maxResults=1`, `maxMembers=1`; große Abfragen `includeReferences=true`, `maxResults=1000`, `maxMembers=1000`; nicht gesetzte optionale Filter blieben im MCP-Request weggelassen. Angegeben sind nur redigierte Counts und Bytebudgets.

| ID | Label | Ergebnis: Counts / Status | Text / Structured / Summe UTF-8 | Truncation/Diagnosen |
|---|---|---|---:|---|
| A1 | LOCAL-01 klein | `totalTypes=48`, `shownCount=1`, `types=1`, `members=1/4`, refs sichtbar 0, sessions sichtbar 0, Gesamt-Refs 203 | 1996 / 4333 / 6329; alle <=8192 | `partial`; `maxResults`, `responseBudget`; 100 Diagnosen gesamt, 1 Sample; zusätzlich `maxDiagnostics`, `messageLength`, `maxDiagnosticBytes` |
| A2 | LOCAL-01 groß | `totalTypes=48`, `shownCount=7`, `types=7`, `members=4/62`, refs sichtbar 1, sessions sichtbar 1, Gesamt-Refs 203, Gesamt-Sessions 4039 | 3626 / 7959 / 11585; Summe >8192 | `partial`; `responseBudget`; 195 Diagnosen gesamt, 1 Sample |
| A3 | LOCAL-02 klein | `totalTypes=48`, `shownCount=1`, `types=1`, `members=1/11`, refs sichtbar 0, sessions sichtbar 0, Gesamt-Refs 159 | 1924 / 4234 / 6158; alle <=8192 | `partial`; `maxResults`, `responseBudget`; 100 Diagnosen gesamt, 1 Sample |
| A4 | LOCAL-02 groß | `totalTypes=48`, `shownCount=4`, `types=4`, `members=11/41`, refs sichtbar 1, sessions sichtbar 1, Gesamt-Refs 159, Gesamt-Sessions 1482 | 3415 / 7925 / 11340; Summe >8192 | `partial`; `responseBudget`; 195 Diagnosen gesamt, 1 Sample |
| A5 | LOCAL-03 klein | `totalTypes=380`, `shownCount=1`, `types=1`, `members=1/1`, refs sichtbar 0, sessions sichtbar 0, Gesamt-Refs 137 | 4773 / 6546 / 11319; Summe >8192 | `partial`; `maxResults`, `responseBudget`; 100 Diagnosen gesamt, 1 Sample |
| A6 | LOCAL-03 groß | `totalTypes=380`, `shownCount=3`, `types=3`, `members=1/7`, refs sichtbar 1, sessions sichtbar 1, Gesamt-Refs 137, Gesamt-Sessions 1519 | 5346 / 7925 / 13271; Summe >8192 | `partial`; `responseBudget`; 200 Diagnosen gesamt, 1 Sample |
| A7 | FALSE-01 klein | kein Assembly-Payload, kein `analysis`, recoverable Structured-Diagnose | 329 / 366 / 695; alle <=8192 | kontrollierter `WORKSPACE_DIAGNOSTIC`-Negativpfad, kein Snapshot |
| A8 | FALSE-01 groß | wie A7; Limits ändern den Negativpfad nicht | 329 / 366 / 695; alle <=8192 | kontrollierter `WORKSPACE_DIAGNOSTIC`-Negativpfad, kein Snapshot |

Für A1–A6 waren Origin/Confidence/Trust/Generation/Status/Content-Mode redigiert konsistent: decompiled, medium, untrusted, Generation 1, partial, signature-only/on-demand. Es wurden keine konkreten Identitäten wiederholt.

### Redigierte Assembly-Abfragen – `find_assembly_extensions`

Effektive Parameter: `targetType=assembly`, `targetPath=<absoluter redigierter Matrixpfad des Labels>`, `receiverType=null`, `extensionName=null`, `namespace=null`. Kleine Abfragen nutzten `maxResults=1`, große `maxResults=1000`. Der öffentliche Aufruf besitzt keinen separaten `includeReferences`-Parameter und expandiert in der bestehenden Route Referenzen; dieser bestehende Contract-Befund ist `E1-BUG-01`.

| ID | Label | Ergebnis: Counts / Status | Text / Structured / Summe UTF-8 | Truncation/Diagnosen |
|---|---|---|---:|---|
| X1 | LOCAL-01 klein | `totalExtensions=0`, shown 0, refs sichtbar 16, sessions sichtbar 1, Gesamt-Refs 203, Gesamt-Sessions 4039 | 1313 / 7885 / 9198; Summe >8192 | `partial`; `responseBudget`; 195 Diagnosen gesamt, 1 Sample |
| X2 | LOCAL-01 groß | wie X1; `maxResults` änderte den bereits leeren Extension-Satz nicht | 1313 / 7885 / 9198; Summe >8192 | `partial`; `responseBudget` |
| X3 | LOCAL-02 klein | `totalExtensions=0`, shown 0, refs sichtbar 16, sessions sichtbar 1, Gesamt-Refs 159, Gesamt-Sessions 1482 | 1315 / 7892 / 9207; Summe >8192 | `partial`; `responseBudget`; 195 Diagnosen gesamt, 1 Sample |
| X4 | LOCAL-02 groß | wie X3 | 1315 / 7892 / 9207; Summe >8192 | `partial`; `responseBudget` |
| X5 | LOCAL-03 klein | `totalExtensions=65`, shown 1, refs sichtbar 15, sessions sichtbar 1, Gesamt-Refs 137, Gesamt-Sessions 1519 | 1406 / 7925 / 9331; Summe >8192 | `partial`; `maxResults`, `responseBudget`; 200 Diagnosen gesamt, 1 Sample |
| X6 | LOCAL-03 groß | `totalExtensions=65`, shown 6, refs sichtbar 1, sessions sichtbar 1, Gesamt-Refs 137, Gesamt-Sessions 1519 | 3009 / 7809 / 10818; Summe >8192 | `partial`; `responseBudget`; 200 Diagnosen gesamt, 1 Sample |

Auch X1–X6 trugen redigiert konsistent decompiled/medium/untrusted/Generation-1/partial/signature-only/on-demand. `FALSE-01` wurde für Extensions nicht erneut abgefragt, weil der negative Nicht-.NET-Inspect-Pfad bereits den relevanten kontrollierten Fehlervertrag und die Budgetneutralität abdeckt.

## Text-vs-Structured-Content, Reduktionsreihenfolge und Abgrenzungen

- Die sichtbaren Typ-/Member-/Extensionelemente werden aus demselben Payload für Formatter und Structured Content erzeugt. Die gesichteten Response-Budgettests prüfen, dass sichtbare Signaturen im Text bleiben; ein separater inhaltlicher Divergenzbefund entstand nicht.
- Die aktuelle Reduktionsreihenfolge lautet für Inspect/Extensions: letzte Reference-Session, letzte Referenz, letzte Diagnose und anschließend die fachlichen Listenelemente; die Reihenfolge ist im Code nachvollziehbar und wird als Kosten-/Observability-Thema, nicht als eigenständiger Semantik-Bug, bewertet.
- Root-/Transitivdiagnosen werden vor der Auswahl dedupliziert und mit globalem Bytebudget normalisiert. Die offene Optimierung betrifft die Repräsentativität und das vorzeitige `break`, nicht die bereits vorhandenen Gesamtcounts.
- `E1-BUG-02` (Dokumentations-/Code-Bytegrenze), `E1-BUG-01` (verdeckte Referenzerweiterung) und `E5-BUG-05` (Response-Budget-Markierung bei Begleitlisten) werden nicht als neue Epic-6-Bugs dupliziert. Ihre messbaren Kosten-/Budgetfolgen sind oben ausdrücklich abgegrenzt.
- Der vorhandene AI-context-footprint-Überhang von `AssemblyReferenceSessionExpander` ist ein früherer Epic-Befund. Epic 6 dokumentiert die aktuelle Budgetpfad-Nähe der Response-Symbole, ohne denselben Überhang erneut zu zählen.

## Offene Unsicherheiten

- Die MCP-Abfragen wurden ohne Builds/Tests und ohne Laufzeitprofiling ausgeführt. Counts und Bytewerte belegen Payloaddruck; sie sind keine Millisekundenmessung.
- Die Summen der Tabellen enthalten Text- und Structured-Nutzdaten, aber keine unbekannte MCP-Transporthülle. Der globale Budgetbefund bleibt dadurch konservativ.
- Der irreduzible Fixed-Metadata-Fall wurde statisch aus dem Kontrollfluss abgeleitet und in der normalen Matrix nicht reproduziert.
- Es ist nicht geklärt, ob Root-first-Diagnosesamples fachlich absichtlich priorisiert werden. Für die Empfehlung wurden deshalb keine Severity-Annahmen gemacht.
- Die 4-KiB-/8-KiB-Differenz und die Referenzexpansion sind bestehende Epic-Befunde; eine Umsetzung sollte ihre Verträge gemeinsam mit der globalen Budgetentscheidung neu festlegen.

## Audit-Grenzen und Verifikation

- Keine Produktionscode-, Test-, Konfigurations- oder Produktdokumentationsänderung.
- Keine Builds, Tests oder Commits.
- Die einzige neue Datei ist dieser Epic-6-Bericht; die einzige weitere erlaubte Änderung ist die Epic-6-Ergänzung in `code-map.md`.

## Finale Spotchecks nach letzter Code-Map-Änderung

Nach der letzten Änderung an `code-map.md` wurden die wichtigsten Fälle erneut mit demselben aktuellen MCP-Schema, `targetType=assembly` und absoluten, hier redigierten Matrixpfaden ausgeführt. Es gab danach keine weitere Code-Map-Änderung.

| Aufruf | Vollständige relevante Parameter | Redigiertes Ergebnis |
|---|---|---|
| `inspect_assembly` – LOCAL-01 groß | `publicOnly=true`, `includeReferences=true`, `maxResults=1000`, `maxMembers=1000`, keine Namespace-/Typ-/Memberfilter | `isError=false`, `totalTypes=48`, `shownCount=7`, Gesamt-Refs 203, Gesamt-Sessions 4039, 195 Diagnosen gesamt/1 Sample, `partial`, Text 3626 / Structured 7959 / Summe 11585 Byte; kanalweise <=8192, kombiniert >8192. |
| `inspect_assembly` – LOCAL-03 groß | `publicOnly=true`, `includeReferences=true`, `maxResults=1000`, `maxMembers=1000`, keine Namespace-/Typ-/Memberfilter | `isError=false`, `totalTypes=380`, `shownCount=3`, Gesamt-Refs 137, Gesamt-Sessions 1519, 200 Diagnosen gesamt/1 Sample, `partial`, Text 5346 / Structured 7925 / Summe 13271 Byte; kanalweise <=8192, kombiniert >8192. |
| `find_assembly_extensions` – LOCAL-03 groß | `maxResults=1000`, `receiverType=null`, `extensionName=null`, `namespace=null` | `isError=false`, `totalExtensions=65`, `shownCount=6`, Gesamt-Refs 137, Gesamt-Sessions 1519, 200 Diagnosen gesamt/1 Sample, `partial`, Text 3009 / Structured 7809 / Summe 10818 Byte; kanalweise <=8192, kombiniert >8192. |
| `inspect_assembly` – FALSE-01 groß | `publicOnly=true`, `includeReferences=true`, `maxResults=1000`, `maxMembers=1000`, keine Namespace-/Typ-/Memberfilter | `isError=false`, kein Assembly-Payload und kein `analysis`, recoverable `WORKSPACE_DIAGNOSTIC` ohne Snapshot, Text 329 / Structured 366 / Summe 695 Byte; alle Limits eingehalten. |

Damit sind die maßgeblichen Worst-Case-/Budgetbeobachtungen nach der letzten Code-Map-Änderung erneut bestätigt; externe Assembly-Identitäten wurden auch in dieser Abschlussrunde nicht in den Bericht übernommen.
