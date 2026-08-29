---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 031
corrects: step-030
title: "Step-030-Gatebefunde und Nachweise korrigieren"
epic: EPIC-04
estimated_risk: medium
step_type: single
items: []
created_by: planer
created_by_model: gpt-5
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T21:56:07+02:00
context_budget:
  read_first_files: 10
  max_initial_files: 12
  read_first_includes_rules: true
  purpose: "Cache-Reuse-Quality-Gate aus Step 030 reproduzierbar abschließen"
related_to:
  - ../step-030/step-plan.md
  - ../step-030/step-result.md
  - ../step-030/step-review.md
  - ../step-029/step-result.md
  - ../step-029/step-review.md
  - ../codemap.md
  - ../tech-debt.md
---

# Step 031: Step-030-Gatebefunde und Nachweise korrigieren

## Bezug

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — der implementierte Cache-Reuse-Vertrag braucht
  einen belastbaren grünen Abschlussnachweis.
- **Korrektur:** `corrects: step-030`; der Review weist einen echten
  Testdatei-Regelverstoß, zwei davon abhängige Integrationsfehler und
  veraltete Result-/Audit-Evidenz aus.
- **Konzept-Referenz:** `Konzept.md` — eine persistente Generation bleibt
  cache-eigen; Reuse liefert einen getrennten request-owned Checkout.

Step 031 ist ein größeres, aber kontextstabiles Qualitätspaket. Die drei
Befunde bleiben zusammen, weil sie denselben Abschluss- und
Nachweisvertrag betreffen: Ein Teststrukturfehler macht den CLI-Dogfood-
Lauf rot, derselbe aktuelle Violationszustand drückt den Live-Safeguard-
Score unter den Korridor, und die beiden Result-Dateien bilden den Zustand
nicht wahrheitsgemäß ab.

## Aktueller Projektzustand (JIT-Kontext)

Step 030 hat den fachlichen Reuse-Nachweis erweitert, aber sein Review
`2510db5e` hat ihn nicht freigegeben. Die Produktionsänderung aus Step 029
liegt in `e9bf8025`; Step 031 plant ausdrücklich keine weitere
Produktionsänderung.

Der aktuelle C#-Befund ist konkret:

- Der Linter meldet in
  `src/AiNetLinter.FastTests/Mcp/Assemblies/`
  `ExternalSourceRepositoryCacheAcquirerTests.cs` 501 Zeilen bei einem
  Limit von 500 (`MaxLineCount`). Die Datei enthält den Acquirer-/Fallback-
  Anteil einer bereits über drei Dateien verteilten Partialklasse
  `ExternalSourceRepositoryCacheWriterTests`.
- Die drei fachlich zusammengehörigen Cache-Hit-Tests stehen dort neben
  Fallback-, Cancellation- und Cache-Read-Tests:
  `Acquirer_ValidCacheHitCreatesIndependentCheckoutWithoutTransportOrPublish`,
  `CacheReuse_ValidCurrentReturnsRequestOwnedCheckout` und
  `Acquirer_ConcurrentCacheHitsCreateIndependentLeases`.
- Die gemeinsame `SourceFixture` und der `RecordingCacheWriter` liegen
  derzeit als private Hilfstypen in
  `ExternalSourceRepositoryCacheWriterTests.cs`. Die beiden Reader-Doubles
  und die direkten Current-/Ownership-Assertions liegen in der zu langen
  Acquirer-Datei.
- Die vorhandenen Produktions-Seams reichen aus: Acquirer, Cache-Reuse,
  Reader- und Writer-Port sind bereits getrennt. Die MCP-Abfragen ergaben
  keinen nachgewiesenen Produktionsfehler und keinen Anlass, den
  Cache-Reuse-Vertrag neu zu entwerfen.

### Reproduzierte Integrationsfehler

Der vollständige Nicht-Stress-Integration-Lauf aus dem Step-030-Review
meldete **368 bestanden, 0 Skips, 2 Fehler, 370 gesamt**. Beide Fehler
wurden anschließend mit dem fokussierten TRX-Lauf
`TestResults/step031-failing-integration.trx` isoliert:

| Test | Konkrete Ursache | In-Scope-Korrektur |
|------|------------------|-------------------|
| `CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess` (`CliRepositoryDogfoodTests.cs:32`) | Der CLI-Prozess endet mit Exit-Code 1. Seine vier ausgegebenen Befunde enthalten den neuen harten Befund `MaxLineCount` für `ExternalSourceRepositoryCacheAcquirerTests.cs` (501 > 500); die drei `MaxDirectoryChildren`-/`AIContextFootprint`-Befunde sind bestehende Warn-/Strukturbefunde. | Cache-Reuse-Tests und gemeinsame Test-Helfer thematisch aufteilen; Regel, CLI-Assertion und Testfilter nicht abschwächen. |
| `McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults` (`McpLiveRepositoryTests.cs:244`) | Der Live-Safeguard liefert `2,652253349573691`, die Testassertion verlangt mindestens `5,0`. Der aktuelle Violationszustand enthält denselben neuen `MaxLineCount`-Befund neben den drei bestehenden strukturellen Befunden. | Nach dem Teststruktur-Split den Live-Test und den vollständigen Lauf erneut ausführen. Bei weiterem Fehlschlag die dann konkrete Ursache untersuchen; Score-Assertion, Filter und bekannte Skipsemantik bleiben unverändert. |

Damit ist die minimal nötige Korrektur für beide bekannten Fehler die
architektonische Entzerrung der Teststruktur. Eine externe Blockade ist
aktuell nicht nachgewiesen. Ein nach dem Split verbleibender Fehler darf
nicht als „grün“ dokumentiert werden; nur ein tatsächlich reproduzierter
externer Host-/Umgebungsfehler darf als Blocker mit Log, Testname und
Reproduktionskommando dokumentiert werden.

## Split-Gate

**Primärer Vertrag:** reproduzierbarer grüner Quality-Gate-Nachweis für den
genehmigten Cache-Reuse-Vertrag.

**Drei gekoppelte Schichten:**

1. **Teststruktur und Regelgrenze:** Die Cache-Hit-/Reuse-Tests werden in
   eine eigene nicht-partielle Testklasse verschoben. Gemeinsame Fixture-,
   Reader-/Writer-Double- und Assertion-Implementierungen werden einmalig
   geteilt; die überlange Datei bleibt unter 500 vom Linter gezählten
   Zeilen.
2. **Integration-Gate-Ursache:** Der CLI-Exit-Code-Fehler und der
   Safeguard-Korridor werden nach der strukturellen Korrektur mit den
   unveränderten Assertions reproduzierbar nachgeprüft. Es gibt keinen
   Produktionsumbau.
3. **Result-/Audit-Evidenz:** `step-029/step-result.md` und
   `step-030/step-result.md` werden erst nach den neuen Läufen mit den
   tatsächlichen Zählungen, Fehlern, Skips, Commit-Referenzen und strikt
   scoped Auditwerten berichtigt.

Nicht enthalten sind Refresh/Fetch/Policy/Config/Retention/GC/Health,
Host-/MCP-Wiring, Provider-/Snapshot-/Registry-/Transport-/Native-Ausbau,
EPIC-05, eine Regelabschwächung, ein Stresslauf oder ein globaler
DRY-/MagicValues-/DeadCode-Sweep.

## Kontextbudget und Einstiegsdateien

Der Coder-Agent ist neu zu starten. Die drei Rule-Dateien sind zwingender
Rollenrahmen und werden vollständig gelesen; `read_first` bleibt dennoch
auf zehn zentrale Dateien begrenzt. Der Initialkontext darf höchstens zwölf
Dateien umfassen.

### `read_first` — genau 10 Dateien

1. `.agents/rules/AiNetLinter.mdc`
2. `.agents/rules/AiNetLinterRichtlinien.mdc`
3. `.agents/rules/AiNetLinter-McpWorkflow.mdc`
4. `tasks/decompiled-assembly-analysis/step-030/step-plan.md`
5. `tasks/decompiled-assembly-analysis/step-030/step-result.md`
6. `tasks/decompiled-assembly-analysis/step-030/step-review.md`
7. `tasks/decompiled-assembly-analysis/step-029/step-result.md`
8. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`
9. `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs`
10. `src/AiNetLinter.IntegrationTests/Cli/CliRepositoryDogfoodTests.cs`

Als zwei optionale Initialdateien sind zulässig:

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs`
- `src/AiNetLinter.IntegrationTests/Mcp/McpLiveRepositoryTests.cs`

Alles Weitere wird Just-in-Time nach semantischem MCP-Befund gelesen,
insbesondere `ExternalSourceRepositoryTestSupport.cs`,
`ExternalSourceRepositoryAcquirerTests.cs`,
`ExternalSourceRepositoryAcquirer.cs`,
`ExternalSourceRepositoryCacheReuse.cs` und der vorhandene
`step031-failing-integration.trx`-Nachweis.

## Intention

Die Teststruktur soll den bestehenden Cache-Reuse-Nachweis fachlich lesbar
und regelkonform abbilden, ohne Assertions, Ownership oder den
Produktionsvertrag zu verändern. Danach müssen die zwei roten
Integrationsstellen erneut grün nachgewiesen oder als tatsächlich externer
Blocker reproduzierbar belegt werden. Erst dann werden die beiden alten
Result-Dateien auf Evidenzbasis korrigiert.

## Konkrete Änderungen

### 1. Cache-Hit-/Reuse-Tests entkoppeln

**Betroffene Datei:**
`src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`

- Die drei validen Cache-Hit-/Reuse-Tests in eine neue
  `ExternalSourceRepositoryCacheReuseTests.cs` mit einer normalen, nicht
  partiellen Testklasse verschieben.
- Die verbleibenden Acquirer-, Fallback- und Cancellation-Szenarien in
  ihrer bestehenden Partialklasse belassen. Ihre `[Fact]`-/`[Theory]`-
  Semantik, Kategorien, Cancellation- und Win32-1314-Skipsemantik sowie
  sämtliche Assertions bleiben unverändert.
- Keine vierte Partialdatei für
  `ExternalSourceRepositoryCacheWriterTests` anlegen. Der Split darf die
  bestehende `MaxPartialClassFiles`-Situation nicht weiter verschärfen.
- Der fokussierte Testfilter muss nach dem Klassen-Split sowohl die neue
  Reuse-Klasse als auch die verbliebenen Acquirer-Tests erfassen. Der Coder
  dokumentiert den tatsächlich ausgeführten Filter wörtlich im Result.

### 2. Gemeinsame Fixture- und Test-Doubles zentralisieren

**Neue Datei:**
`src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheTestSupport.cs`

- Die bislang privaten, bereits vorhandenen Implementierungen von
  `SourceFixture` und `RecordingCacheWriter` aus
  `ExternalSourceRepositoryCacheWriterTests.cs` einmalig als cache-
  spezifische, intern sichtbare Supporttypen herauslösen.
- Die beiden bestehenden Reader-Doubles
  `FixedCacheReader` und `CancellingCacheReader` aus der überlangen
  Acquirer-Datei in dieselbe Supportdatei verschieben.
- `ReadCurrentGenerationName` und `AssertRequestOwnedCheckout` als
  gemeinsame, klar benannte Testassertions-Helfer zentralisieren. Die
  bestehende `MutateCache`-Hilfe bleibt beim Fallback-Test, sofern sie dort
  ausschließlich benötigt wird.
- Die Testwerte `RepositoryUrl`, `Revision`, `OtherRevision` und
  `SolutionPath` nur einmalig in einem cache-spezifischen Testdatenhalter
  definieren. Bestehende Writer-/Read-back-Tests verwenden danach diesen
  Halter statt private Kopien.
- Die vorhandenen Dispose-/Lease-/Marker-/Manifest-Details der Fixture
  unverändert übernehmen. Es dürfen weder Produktionsverzeichnisse noch
  `AppContext.BaseDirectory` als neue Testablage eingeführt werden.

### 3. Writer-/Read-back-Referenzen an den gemeinsamen Support anschließen

**Betroffene Dateien:**

- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheWriterReadBackTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheAcquirerTests.cs`

Die entfernten privaten Typen und Konstanten werden durch die gemeinsamen
Supporttypen ersetzt. Das ist eine Extraktion vorhandener Implementierung,
keine zweite Fixture und keine semantische Testverkürzung. Die bestehende
Partialklasse bleibt bei ihren drei Dateien; die neue Reuse-Testklasse ist
separat und enthält keine Duplikate der Fixture-Logik.

### 4. Result-/Audit-Nachweise korrigieren

**Betroffene Dateien nach erfolgreicher Verifikation:**

- `tasks/decompiled-assembly-analysis/step-029/step-result.md`
- `tasks/decompiled-assembly-analysis/step-030/step-result.md`

Beide Dateien sind auf die tatsächlich ausgeführten Läufe zu bringen:

- Vollständiger Fast- und Integration-Lauf mit exakten
  passed/failed/skipped/total-Zahlen.
- Der aktuelle Zwischenbefund `368 bestanden, 0 Skips, 2 Fehler, 370`
  bleibt als historischer Fehlerbefund erhalten, darf aber nach einem
  später grünen Lauf nicht als Endstand stehen bleiben.
- Beide konkreten Fehlschläge, ihre Ursachen und die tatsächlich
  ausgeführten Reproduktionsfilter müssen genannt werden. Ein
  fokussierter Zwei-Test-Lauf mit `0/2/2` ersetzt niemals das vollständige
  370er-Integration-Gate.
- Die beiden bekannten Win32-1314-Skips werden nur mit ihrem echten
  Testnamen und ihrer unveränderten Begründung dokumentiert. Stress bleibt
  nicht ausgeführt.
- Commit-/Hashangaben werden nicht erfunden: Der Coder trägt den tatsächlich
  erzeugten Teststruktur-Commit ein und lässt die historische
  Step-029-Produktionsreferenz nachvollziehbar.
- `get_violations`, `safeguard`, `find_duplicates`, `find_magic_values` und
  `find_dead_code` werden nach der Änderung erneut scoped ausgeführt. Nur
  diese Ergebnisse dürfen im Result stehen; insbesondere kein
  `scopeDir=src`- oder solutionweiter Audit-Claim.

## Testmatrix

| Stufe | Exakter Lauf / Nachweis | Erwartung für die Abnahme |
|------|--------------------------|---------------------------|
| Fokus | `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~ExternalSourceRepositoryCacheReuseTests|FullyQualifiedName~Acquirer_|FullyQualifiedName~ExternalSourceRepositoryAcquirerTests|FullyQualifiedName~ExternalSourceRepositoryCancellationTests"` | Alle Cache-Reuse-, Fallback-, Acquirer- und Cancellation-Tests grün; bekannte 1314-Skips ausschließlich transparent. Den realen Testfilter und Zähler notieren. |
| Build | `dotnet build` | 0 Fehler und 0 Warnungen bei `TreatWarningsAsErrors`. |
| Fast-Gate | `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` | Vollständig grün; nur die zwei bekannten 1314-Skips, falls erneut reproduziert. |
| Fehlerursache | `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~CliRepositoryDogfoodTests.RunLinterCli_OnWholeSolution_ReturnsSuccess|FullyQualifiedName~McpLiveRepositoryTests.LiveDogfood_Safeguard_ReturnsResults" --logger "trx;LogFileName=step031-failing-integration-after-split.trx"` | Beide bisher roten Tests reproduzierbar grün; Assertion und Filtersemantik nicht ändern. Bei Rot den exakten neuen Output sichern. |
| Integration-Gate | `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress` | 370/370 grün ohne Fehler und ohne unterdrückte Tests; keine Stressausführung. |
| Scope-Evidenz | MCP mit absolutem `projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` | Nachweis für Violations/Safeguard sowie scoped DRY, MagicValues und DeadCode liegt mit exaktem Scope und Zählern vor. |

Der fokussierte Zwei-Test-Lauf ist Diagnose und kein Ersatz für den
vollständigen Integration-Gate-Lauf. Falls der Safeguard nach Entfernung
des neuen `MaxLineCount`-Befunds weiterhin unter 5,0 liegt, wird der
vollständige Output analysiert; weder der Korridor noch der Linterfilter
werden gelockert.

## MCP-, DRY-, MagicValues- und DeadCode-Plan

Alle projektgebundenen Aufrufe verwenden den absoluten Root
`C:/Daten/Entwicklung/Ralf/AiNetLinter`.

### Vor dem Split

- `get_class_structure` für die bestehende Partialklasse sowie
  `get_feature_context`/`get_test_context` für
  `ExternalSourceRepositoryCacheReuse` und
  `ExternalSourceRepositoryAcquirer` ausführen.
- Mit `find_symbol`, anschließend `find_references` und `get_impact`, die
  produktiven Aufrufpfade und die statische Testzuordnung sichern. Bei
  Mehrdeutigkeit den von `find_symbol` gelieferten vollqualifizierten
  Methodennamen verwenden.
- `get_symbol_body` nur für die betroffenen Produktionsmethoden und die
  drei Reuse-Testmethoden lesen; keine Produktionsänderung daraus ableiten.

### Nach dem Split

- `get_violations(scopeFilter="ExternalSourceRepository")` ausführen und
  den neuen `MaxLineCount`-Befund sowie alle weiterhin bestehenden,
  außerhalb des Teststruktur-Splits liegenden Befunde getrennt ausweisen.
- `safeguard(scopeFilter="ExternalSourceRepository", minScore=8,
  maxViolations=20)` erneut ausführen. Den exakten Score und die vier bzw.
  danach verbleibenden Befunde dokumentieren, ohne daraus einen globalen
  Qualitätsclaim abzuleiten.
- `find_duplicates(mode="clone", minTokens=20,
  similarityThreshold="near", scopeDir="src/AiNetLinter/Mcp/Assemblies",
  scopeType="production")` und den entsprechenden Testscope
  `src/AiNetLinter.FastTests/Mcp/Assemblies` ausführen. Neue gemeinsame
  Helpers dürfen keine Testduplikation erzeugen.
- `find_magic_values` separat für den betroffenen Produktionsscope
  `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepository` und den
  betroffenen Testscope ausführen. Absichtliche Fixture-Identitäten,
  Marker und Falltexte werden nicht blind umgebaut.
- `find_dead_code(scopeFilter="ExternalSourceRepository",
  includeTests=true, mode="members")` ausführen. Kein globaler Sweep und
  kein neuer `tech-debt.md`-Eintrag ohne direkt nachgewiesenen, in-scope
  Befund.

## Abnahmekriterien (8)

1. Der Step liefert genau einen primären, reproduzierbaren grünen
   Cache-Reuse-Quality-Gate-Vertrag und bleibt auf die drei oben genannten
   gekoppelten Schichten begrenzt.
2. `ExternalSourceRepositoryCacheAcquirerTests.cs` liegt nach dem Split
   unter der vom Linter gezählten Grenze von 500 Zeilen; die neue
   Reuse-Datei bleibt ebenfalls regelkonform, und es entsteht keine vierte
   Partialdatei.
3. Alle drei Cache-Hit-/Reuse-Tests behalten ihre fachliche Semantik:
   erfolgreicher Publish, getrennter Reader, Recording-Writer ohne
   Publish-Aufruf, unveränderter Current-Name, request-owned Checkout und
   Cleanup/persistente Generation werden weiterhin direkt geprüft.
4. Fallback-, Cancellation-, Transport- und bestehende
   Win32-1314-Skipsemantik bleiben unverändert und bestehen im Fokus- und
   vollständigen Fast-Lauf.
5. Der CLI-Dogfood-Test und der Live-Safeguard-Test bestehen nach der
   strukturellen Korrektur mit unveränderten Regeln, Assertions und
   Filtern; andernfalls liegt ein vollständiger reproduzierbarer externer
   Blockernachweis vor.
6. Beide vollständigen Nicht-Stress-Gates sowie `dotnet build` sind mit
   den tatsächlich ausgeführten Zahlen dokumentiert; kein Stresslauf und
   keine unterdrückte Fehlermeldung wurde verwendet.
7. Step-029- und Step-030-Result enthalten ausschließlich ausgeführte
   Testzahlen, konkrete Fehler/Skips, Commitreferenzen und scoped
   MCP-/DRY-/MagicValues-/DeadCode-Evidenz; globale Audit-Claims und
   falsche grüne Zahlen sind entfernt.
8. Der Diff enthält nur die geplante Teststruktur-/Fixture-Entzerrung,
   die beiden Result-Korrekturen und den Status-/Plan-Nachweis; kein
   Produktionscode, keine Roadmap und kein Tech-Debt werden außerhalb eines
   direkt nachgewiesenen in-scope Testfixture-Befunds verändert.

## Risiken und Gegenmaßnahmen

- **FQN-/Filterdrift:** Eine neue Testklasse ändert vollqualifizierte
  Testnamen. Deshalb muss der Fokusfilter beide Klassen/Methodengruppen
  abdecken und seine tatsächliche Ausgabe im Result stehen.
- **Ownership-Drift in Fixtures:** `TestTempDirectory`, Publish-/Read-
  Roots, Marker, Generation und Dispose-Reihenfolge werden unverändert
  übernommen. Der Coder prüft dies vor dem ersten Testlauf über MCP und
  danach über die Reuse-Assertions.
- **Partialklasse weiter aufblasen:** Keine neue Partialdatei; der
  fachliche Reuse-Schnitt wird als eigene Testklasse umgesetzt.
- **Verdeckte Duplikation:** Fixture, Recording-Writer, Reader-Doubles und
  Assertions werden nur einmal aus den vorhandenen Implementierungen
  extrahiert. Scoped `find_duplicates` entscheidet, ob ein direkter
  Supportbefund vorliegt.
- **Safeguard bleibt rot:** Die Assertion `score >= 5.0` bleibt bestehen.
  Nach dem Split sind neue Befunde erneut zu klassifizieren; ein bestehender
  struktureller Warnbefund ist weder zu verschweigen noch durch einen
  globalen Sweep in diesen Step zu ziehen.
- **Nachweisdrift:** Result-Dateien werden erst nach Build, Fokuslauf,
  vollständigen Gates und scoped Audits geändert. Konsolenausgabe und TRX
  sind die Belege, nicht frühere Resulttexte.

## Bekannte Ausnahmen

- Die beiden echten Reparse-/Symlink-Tests dürfen weiterhin ausschließlich
  wegen `ERROR_PRIVILEGE_NOT_HELD` / Win32 1314 übersprungen werden:
  `ExternalSourceRepositoryAcquirerTests.AcquireAsync_ActualReparseEntry_IsRejectedAndExternalSentinelRemains`
  und
  `ExternalSourceRepositoryCacheWriterTests.PublishAsync_ActualReparseEntryFailsClosed`.
- Ein Stresslauf wird nicht ausgeführt.
- Die im Safeguard sichtbaren bestehenden Directory-/Footprint-Befunde
  werden nur als scoped Kontext berichtet. Sie sind kein Freibrief für eine
  Testfilter- oder Regelabschwächung; der vollständige Live-Test muss den
  vereinbarten Korridor tatsächlich erfüllen.

## Definition of Done

- Alle acht Abnahmekriterien sind mit Dateien, Testausgaben und MCP-
  Ergebnissen belegbar.
- Der ursprüngliche Acquirer-Testfile-Befund ist durch eine sinnvolle
  thematische Aufteilung behoben, nicht durch Löschung von Assertions,
  Kommentarverarmung oder Zeilenkosmetik.
- Die zwei konkreten Integrationsfehler sind nach der Korrektur grün oder
  als reproduzierbarer externer Blocker mit vollständigem Log dokumentiert.
- `step-029/step-result.md` und `step-030/step-result.md` sind erst danach
  auf die tatsächlich ausgeführten Werte aktualisiert.
- `step-031/step-result.md` wird vom Coder geschrieben; der Planer behauptet
  hier keine noch nicht ausgeführten Endzahlen.
- Der Coder erstellt einen deutschen Conventional Commit mit Suffix
  `[decompiled-assembly-analysis]`, pusht nicht und hinterlässt den
  Arbeitsbaum sauber.
- Danach folgt ein neuer Kritiker-Agent; Step 031 wird erst nach dessen
  Review auf `done`/`approved` gesetzt.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc` — `MaxLineCount`, Test-/Partialgrenzen,
  keine Produktionsänderung ohne Nachweis.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — Testisolation,
  vollständige Nicht-Stress-Gates, MCP-first, Resultwahrheit und
  Commit-/Arbeitsbaumregeln.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — absoluter
  `projectRoot`, C#-Semantik über MCP, scoped Violations/Safeguard und
  DRY-/MagicValues-/DeadCode-Abfragen.
- `.agents/Agent-Scaffolding/AGENTS.md` — deutschsprachige
  Dokumentation, Pfad- und Git-Konventionen.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` — neuer
  Agent je Rolle, flacher Step, kein Push und Statusübergabe.

## Exakte Coder-Anweisung

Starte einen **neuen Coder-Agenten** für `decompiled-assembly-analysis`.
Lies zuerst die zehn `read_first`-Dateien und den vollständigen
Rollenrahmen. Arbeite ausschließlich im Repository
`C:/Daten/Entwicklung/Ralf/AiNetLinter` und verwende für jede C#-Semantik-,
Impact-, Violations- und scoped Audit-Abfrage den absoluten
`projectRoot`.

Entzerr die drei Cache-Hit-/Reuse-Tests aus
`ExternalSourceRepositoryCacheAcquirerTests.cs` in eine eigene nicht-
partielle Reuse-Testklasse. Extrahiere die bereits vorhandenen Fixture-,
Recording-Writer-, Reader-Double- und Assertion-Implementierungen genau
einmal in cache-spezifischen Test-Support; ändere keine Produktionsdatei,
keine Regel, keinen Filter und keine fachliche Assertion. Halte die
bestehende Partialklasse bei ihren drei Dateien. Prüfe anschließend zuerst
den neuen Dateigrenzwert und den fokussierten Cache-/Acquirer-Lauf.

Führe danach `dotnet build`, den vollständigen Fast-Gate-Lauf und den
vollständigen Integration-Gate-Lauf mit `Category!=Stress` aus. Isoliere
die beiden bisher roten Integrationstests mit einem TRX, ohne ihre
Assertions abzuschwächen. Führe anschließend die MCP-Abfragen und die
beiden scoped Duplicate-Läufe sowie scoped MagicValues-/DeadCode-Prüfungen
aus. Aktualisiere `step-029/step-result.md` und
`step-030/step-result.md` ausschließlich mit den tatsächlich ausgeführten
Zahlen, Fehlern, Skips, Hashes und Audit-Scopewerten; schreibe danach
`step-031/step-result.md`. Bei verbleibendem Rot dokumentiere den exakten
Fehler als offen bzw. nur dann als externen Blocker, wenn Log und
Reproduktion das belegen.

Keine Änderungen an `roadmap.md`, `tech-debt.md`, Produktionscode oder
Out-of-Scope-EPICs; kein Stresslauf, kein Push. Übergabepunkt an den
Kritiker sind der Teststruktur-Diff, die vollständigen Logs/TRX, die
scoped MCP-Ausgaben, die drei Result-Dateien und ein sauberer Arbeitsbaum.

## Notes

`roadmap.md` wird nicht geändert. Der Epic-Stand ändert sich durch diesen
Fix-Step nicht; `task-state.md` ist die aktuelle Quelle für Step 031.
`tech-debt.md` bleibt unverändert, weil die scoped DRY-/MagicValues- und
DeadCode-Prüfungen keinen neuen direkt zu bearbeitenden Befund ergeben
haben. Der fokussierte Diagnose-TRX darf im ignorierten `TestResults/`
liegen, ist aber kein Ersatz für die vollständige Gate-Evidenz.
