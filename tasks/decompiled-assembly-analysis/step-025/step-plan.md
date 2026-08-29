---
status: open
type: step-plan
task: decompiled-assembly-analysis
step: 025
corrects: step-024
title: "Registry-/Snapshot-Lifetime und exception-sicheres Multi-Owner-Cleanup korrigieren"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-29T14:14:42+02:00
related_to:
  - ../step-024/step-plan.md
  - ../step-024/step-result.md
  - ../step-024/step-review.md
  - ../roadmap.md
  - ../follow-up-strategy.md
  - ../Konzept.md
  - ../tech-debt.md
  - ../codemap.md
---

# Step 025: Exception-sicheres Multi-Owner-Cleanup der Snapshot-Registry

## Bezug und Split-Gate

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04` — Gitea-Source-of-Truth, Refresh und
  Fehlersemantik.
- **Korrektur:** Dieser Step korrigiert ausschließlich `MAJOR-001` aus
  `step-024/step-review.md`; der korrigierte Step bleibt fachlich der
  Provider-/Snapshot-Lifetime-Grenze zugeordnet.
- **Konzept-Referenz:** `Konzept.md`, insbesondere „Registry und
  Lebensdauer“, „Staleness und atomarer Session-Wechsel“ sowie der
  Fehler-/Sicherheitsvertrag für kontrollierte Ressourcenbereinigung.

Der primäre Vertrag ist die Registry-/Snapshot-Lifetime bei mehreren
Besitzern: Die Registry muss alle bereits residenten Snapshots versuchen zu
entsorgen, auch wenn ein einzelner Snapshot-Dispose fehlschlägt, und darf
keinen Cleanup-Fehler unsichtbar machen.

Das Split-Gate bleibt innerhalb eines größeren, vertikalen EPIC-04-Pakets:

- **Ein primärer Vertrag:** Registry-/Snapshot-Lifetime und
  exception-sicheres Multi-Owner-Cleanup.
- **Drei gekoppelte Schichten:** Registry-Dispose; gemeinsame
  Fehleraggregation und der bestehende Snapshot-/Owner-Vertrag; lokale
  Regressionen.
- **Maximal acht Abnahmekriterien:** acht Kriterien sind unten definiert.
- **Kein Mini-Sweep:** DRY-, MagicValues- und DeadCode-Prüfungen bleiben
  auf die berührte Cleanup-Grenze beschränkt.

## Aktueller Projektzustand (JIT-Kontext)

Der MCP-Kontext mit `projectRoot`
`C:/Daten/Entwicklung/Ralf/AiNetLinter` bestätigt:

- `SourceSnapshotRegistry.Dispose()` setzt in
  `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs:56-79`
  zuerst sein terminales Dispose-Flag, entnimmt die Snapshots unter dem
  Lock, leert die Registry und ruft anschließend `Dispose()` ohne
  per-Snapshot-Fehlerisolierung auf.
- `ExternalSourceSnapshot.Dispose()` in
  `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs:191-219`
  entsorgt Workspace vor Checkout-Owner und sammelt die beiden lokalen
  Fehler bereits. Diese Exception beendet derzeit den äußeren Registry-
  Durchlauf.
- Die Registry hat eigene Duplicate-/Lease-/Terminal-Tests in
  `SourceSnapshotRegistryTests.cs`; der vorhandene Idempotenztest deckt
  jedoch keinen fehlerhaften ersten Snapshot mit einem zweiten residenten
  Snapshot ab.
- `IExternalSourceCheckoutOwner` ist bereits die minimale interne
  Ownership-Grenze. Es ist kein neuer Acquirer-, Provider- oder Host-
  Vertrag erforderlich.

Der Plan erweitert die bestehende Ownership-Entscheidung aus Step 024; er
drehte sie nicht zurück und führt keine parallele Registry oder einen neuen
Lebenszyklus ein. Die Registry bleibt nach dem ersten Dispose-Aufruf
terminal. Der Unterschied ist ausschließlich, dass der erste Aufruf alle
entnommenen Snapshots vollständig abarbeitet, bevor ein Fehler nach außen
geht.

## Intention

Step 025 schließt die Ausnahme-Lücke zwischen der bereits eingeführten
Snapshot-Ownership und der umgebenden Registry. Ein Cleanup-Fehler eines
Snapshots darf die nachfolgenden Workspace-/Checkout-Besitzer nicht leaken
lassen. Das Ergebnis des vollständigen Durchlaufs bleibt sichtbar und ist
bei wiederholtem Registry-Dispose ohne erneute Cleanup-Arbeit bounded und
idempotent.

## Scope

### Schicht 1: Registry-Dispose

`src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs:56-79`

- Snapshot-Einträge weiterhin einmalig unter dem bestehenden Lock entnehmen
  und die Registry vor dem Cleanup leeren.
- Die entnommenen Snapshots in einer deterministischen Reihenfolge anhand
  von `Identity.StableValue` mit ordinalem Vergleich abarbeiten. Nicht im
  Lock disposen, damit Workspace-/Owner-Cleanup keine Registry-Reentrancy
  oder unnötige Lock-Haltezeit erzeugt.
- Jeden Snapshot in einem eigenen `try`/`catch (Exception)`-Block entsorgen,
  den Fehler sammeln und den Durchlauf für alle weiteren Snapshots
  fortsetzen.
- Erst nach dem letzten Versuch die gemeinsame Fehlerweitergabe aufrufen.
  Ein einzelner Fehler wird mit erhaltener Exception-Information
  weitergegeben; mehrere Fehler werden in der festgelegten Snapshot-Reihen-
  folge aggregiert. Kein Fehler darf still verworfen werden.
- `Interlocked.Exchange`, `ResidentCount`, `Acquire`, `Release`,
  Duplicate-Dispose und die terminale `ObjectDisposedException`-Semantik
  unverändert erhalten.

### Schicht 2: Gemeinsame Fehleraggregation und Ownership-Vertrag

`src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs:191-239`

- Die bisherige Einzelfall-/Zweifach-Fehlerlogik von
  `ExternalSourceSnapshot` in einen kleinen internen, von Registry und
  Snapshot gemeinsam verwendeten Aggregationspfad überführen. Dadurch
  wird die Einzelfehler- versus `AggregateException`-Semantik nicht an
  zwei Stellen unabhängig nachgebaut.
- Die bestehende Reihenfolge des Snapshot-Cleanups festhalten: zuerst
  Workspace, danach Checkout-Owner, auch wenn der erste Dispose-Aufruf
  fehlschlägt. Der Owner bleibt bis zur Snapshot-/Registry-Lifetime im
  Snapshot gebunden.
- Die bestehende Idempotenz des Snapshots beibehalten. Nach gesetztem
  Snapshot-Dispose-Flag gibt es keinen Retry; der Registry-Durchlauf sorgt
  dafür, dass jeder noch nicht entsorgte Snapshot beim ersten Registry-
  Cleanup trotzdem versucht wird.
- Keine neue öffentliche API und keine Änderung an
  `IExternalSourceCheckoutOwner`, `ExternalSourceCheckoutHandle` oder dem
  Provider-/Materializer-Vertrag einführen.

### Schicht 3: Lokale Regressionen

`src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`

- Die bestehende Registry-Testklasse um einen deterministischen Fall mit
  mindestens zwei unterschiedlichen Snapshot-Identitäten erweitern.
- Einen test-only `IExternalSourceCheckoutOwner` verwenden, der beim
  ersten Snapshot einen sichtbaren Fehler wirft und jeden Dispose-Versuch
  zählt; der zweite Snapshot verwendet einen zählenden, erfolgreich
  entsorgbaren Owner. Die Snapshots werden bewusst in einer von der
  StableValue-Reihenfolge abweichenden Acquire-Reihenfolge registriert,
  damit die deterministische Cleanup-Reihenfolge geprüft wird.
- Den ersten Registry-Dispose-Aufruf auf einen sichtbaren Fehler prüfen,
  gleichzeitig die Entsorgung des ersten und des zweiten Snapshots sowie
  genau einen Owner-Aufruf je Snapshot nachweisen.
- Den Registry-Dispose-Aufruf unmittelbar erneut ausführen und belegen,
  dass er ohne Exception, Retry oder unbounded Warte-/Schleifenpfad
  zurückkehrt; `ResidentCount` bleibt `0` und kein Owner wird ein zweites
  Mal aufgerufen.
- Einen gekoppelten Mehrfachfehler-Fall vorsehen, falls die gemeinsame
  Aggregationslogik sonst nicht direkt auf ihre stabile innere Reihenfolge
  geprüft wird: Die sichtbaren inneren Fehler müssen die ordinal sortierte
  Snapshot-Reihenfolge bewahren.
- Bestehende Lease-, Duplicate- und terminale Registry-Tests unverändert
  weiterführen. Es werden keine OS-Temp-Pfade, Prozesse, Netzwerke,
  Gitea- oder Fremdprojekt-Tests verwendet.

## Out-of-Scope

- Refresh, Fetch, persistenter Repository-Cache, Cache-/Manifest-
  Integrität, Generationen, korrupte Snapshots und atomare
  Source-of-Truth-Veröffentlichung.
- Dirty-/unbuilt-Checkout-Erkennung, Health-/Degraded-Policy,
  Source-Mapping, Snapshot-Identity und neue Fallback-Regeln.
- Acquirer-, Provider-, Materializer-, Orchestrator- oder
  `AssemblyAnalysisHostComposition`-Änderungen.
- Gitea-/HTTP-/Credential-/Transport-Verträge sowie Prozessbaum-, Handle-
  und Native-Interop-Semantik aus Steps 019 bis 023.
- MCP-Registrierung, Host-Wiring, transitive Referenzen, Capability-Matrix
  und EPIC-05.
- Änderung am privilegierten 1314-/Reparse-Test, an dessen Skip-Regel oder
  an der repository-spezifischen Fallback-Semantik.
- Globaler DRY-, MagicValues- oder DeadCode-Sweep. Die offenen
  Tech-Debt-Einträge TD-001 bis TD-003 sowie die erledigten TD-004/TD-005
  werden nicht in einen neuen Step umgewandelt.
- Roadmap-, Konfigurations-, README- oder Produktdokumentationsänderungen;
  der Review-Fund wird ausschließlich in Plan und Task-Status verfolgt.

## Architekturgrenze

Die `SourceSnapshotRegistry` bleibt der einzige Koordinator der residenten
Snapshot-Lifetime. Sie entnimmt Besitzer unter dem Lock, entsorgt sie
außerhalb des Locks und gibt den Gesamtfehler erst nach vollständigem
Durchlauf weiter. Der gemeinsame Aggregationspfad kennt nur geordnete
Exceptions und keine Roslyn-, Git-, Transport- oder MCP-Details.

`ExternalSourceSnapshot` bleibt Owner von genau seinem Workspace und seinem
optionalen `IExternalSourceCheckoutOwner`. Workspace vor Checkout und
idempotentes Snapshot-Cleanup sind unveränderliche Teilverträge. Die
Registry übernimmt keinen neuen individuellen Owner und versucht nach
einem terminalen Cleanup-Fehler keinen Retry.

## Kontextbudget

Der Coder liest zuerst höchstens die folgenden zwölf Dateien:

### `read_first` (12 Dateien)

1. `../step-024/step-review.md` — MAJOR-001, Reproduktion und verlangte
   Korrektur.
2. `../step-024/step-plan.md` — ursprünglicher Provider-/Snapshot-
   Ownership-Vertrag und Abnahmekriterium 4.
3. `../step-024/step-result.md` — implementierter Snapshot-/Owner-Stand
   und bisheriger Cleanup-Nachweis.
4. `../roadmap.md` — offene EPIC-04-Grenze und nachgelagerte Refresh-
   Pakete.
5. `../follow-up-strategy.md` — Split-Gate und kontextbegrenzte
   Vertikalpakete.
6. `../Konzept.md` — Registry-/Lebensdauer- sowie Fehler-/Sicherheits-
   Vertrag.
7. `../tech-debt.md` — Index und die für diesen Scope nicht zutreffenden
   offenen Einträge.
8. `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotRegistry.cs` — Registry-
   Dispose, Lease-, Duplicate- und Terminal-Semantik.
9. `src/AiNetLinter/Mcp/Assemblies/SourceSnapshotModels.cs` — Snapshot-
   Owner, Dispose-Reihenfolge und bisherige Fehleraggregation.
10. `src/AiNetLinter.FastTests/Mcp/Assemblies/SourceSnapshotRegistryTests.cs`
    — bestehende Registry-/Identity-Regressionen.
11. `src/AiNetLinter/Mcp/Assemblies/IExternalSourceCheckoutOwner.cs` —
    minimale Ownership-Schnittstelle für den Test-Doppel.
12. `src/AiNetLinter.FastTests/Fixtures/ExternalSourceSnapshotTestFactory.cs`
    — vorhandenes Snapshot-Testmuster, das nicht dupliziert werden soll.

### `read_on_demand`

- `SourceSnapshotRegistry`-Aufrufer und Lease-Konsumenten nur, wenn die
  MCP-Impact-Abfrage eine direkte Vertragsauswirkung zeigt.
- `ExternalSourceRepositoryAcquisitionModels.cs` nur, wenn der Test den
  bestehenden echten Checkout-Handle statt eines kleinen test-only Owners
  benötigt.
- Bestehende Snapshot-, Provider- und Host-Tests nur zum Abgleich der
  unveränderten Ownership-/Composition-Grenze.
- TestKit-Roslyn-Helfer nur, falls der vorhandene
  `AdhocWorkspace`-Aufbau der Registry-Regression erweitert werden muss.

### `out_of_scope`

- Alle Provider-/Acquirer-/Materializer-/Transport-/Native-Dateien, sofern
  sie nicht durch eine konkrete Impact-Frage als direkte Regression
  betroffen sind.
- Refresh-/Cache-/Manifest-/Generation-Dateien, MCP-Registrierungen,
  Host-Komposition und EPIC-05-Referenzauflösung.
- Externe Repositories, Remote-Zugriffe, Prozess- und Integrationstest-
  Fixtures ohne direkten Registry-/Snapshot-Lifetime-Bezug.

## Abnahmekriterien

1. `SourceSnapshotRegistry.Dispose()` entnimmt und leert die residenten
   Snapshots einmalig, versucht jeden entnommenen Snapshot genau einmal und
   hält während des eigentlichen Cleanup-Durchlaufs nicht den Registry-Lock.
2. Ein Fehler eines Snapshots stoppt den Durchlauf nicht; alle folgenden
   Snapshots werden weiter entsorgt und kein Cleanup-Fehler wird
   verschluckt.
3. Die Fehlerweitergabe ist deterministisch: Snapshots werden ordinal nach
   `Identity.StableValue` verarbeitet, ein einzelner Fehler wird mit seiner
   ursprünglichen Exception-Information weitergegeben und mehrere Fehler
   werden in dieser Reihenfolge aggregiert.
4. Der bestehende Snapshot-Ownership-Vertrag bleibt erhalten: Workspace
   wird vor Checkout-Owner entsorgt, beide Versuche bleiben auch bei einem
   Fehler sichtbar, und Snapshot- sowie Registry-Dispose bleiben terminal
   und idempotent.
5. Ein lokaler Regressionstest mit mindestens zwei unterschiedlichen
   Snapshots provoziert einen Fehler beim ersten Snapshot, weist die
   Entsorgung des zweiten Snapshots nach, macht das Ergebnis sichtbar und
   zeigt beim erneuten Registry-Dispose einen bounded, fehlerfreien Lauf
   ohne zweiten Owner-Aufruf.
6. Lease-, Duplicate- und `Acquire`-nach-Dispose-Semantik bleiben durch die
   bestehenden Registry-Regressionen grün; der Fix führt keine neue
   Registry oder Ownership-Schicht ein.
7. Die Regressionen bleiben vollständig lokal und deterministisch: kein
   OS-Temp-Pfad, kein Netzwerk, kein Gitea-/Git-Zugriff, kein Prozess und
   keine Ausführung fremder Repository-Artefakte.
8. Die gezielten MCP-/Qualitätsprüfungen, `dotnet build` sowie beide
   vollständigen Nicht-Stress-Testgates sind grün; Änderungen bleiben auf
   Registry-/Snapshot-Cleanup und lokale Regressionen begrenzt.

## Tests

Während der Implementierung:

```text
dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~SourceSnapshotRegistryTests"
dotnet test src/AiNetLinter.FastTests --filter Category=Unit
```

Vor dem Step-Abschluss zwingend:

```text
dotnet build
dotnet test src/AiNetLinter.FastTests --filter Category!=Stress
dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress
```

Stress-Tests werden nicht ausgeführt. Der bestehende privilegierte
1314-/Reparse-Skip bleibt unverändert und ist kein Teil dieser Regression.

## MCP-, DRY-, MagicValues- und DeadCode-Plan

Alle semantischen MCP-Abfragen verwenden
`projectRoot: C:/Daten/Entwicklung/Ralf/AiNetLinter`. `rg` wird nur für
Text, Dateinamen und exakte Bannwort-/Scopeprüfungen verwendet, nicht für
C#-Symbol-, Referenz- oder Impact-Schlüsse.

### MCP-Semantik

- Vor der Änderung `get_feature_context` und `get_symbol_body` für
  `SourceSnapshotRegistry.Dispose`, `ExternalSourceSnapshot.Dispose` und
  den gemeinsamen Fehlerpfad aufrufen; bei einem neuen Helper zuerst
  `find_symbol` verwenden.
- `find_references` und `get_impact` für
  `M:AiNetLinter.Mcp.Assemblies.SourceSnapshotRegistry.Dispose` sowie
  `M:AiNetLinter.Mcp.Assemblies.ExternalSourceSnapshot.Dispose` prüfen.
  Bei einem neuen internen Aggregator die direkten Aufrufer und den
  begrenzten Ownership-Impact separat verifizieren.
- Nach der Änderung `get_violations` auf die geänderten
  `Mcp/Assemblies`-Dateien begrenzen und mit `metrics_lookup` die
  Methodengröße/Komplexität des Registry-Dispose und des Helpers prüfen.
- `safeguard` nur auf den berührten Assemblies-Scope anwenden; bestehende
  Directory-/Footprint-Hinweise getrennt von neuen Befunden dokumentieren.
- `rg` ausschließlich auf den geänderten Dateien für verbotene neue
  Provider-/Transport-/Refresh-/Cache-/Manifest-/Host-Bezüge sowie
  `Assembly.Load`, `AssemblyLoadContext`, Reflection, Netzwerk und
  Prozesszugriffe einsetzen.

### DRY-Plan und Tech-Debt-Disposition

- `find_duplicates` gezielt auf `src/AiNetLinter/Mcp/Assemblies` und die
  betroffene Registry-Testdatei mit `minTokens=20` ausführen. Der Audit
  muss bestätigen, dass die Fehleraggregation nicht parallel in Registry
  und Snapshot nachgebaut wird und kein zweiter Snapshot-Builder entsteht.
- Nur ein tatsächlich innerhalb der Cleanup-Grenze liegender Exact- oder
  Refactoring-Drift-Fund wird im selben Paket behoben. Die offenen
  TD-001 bis TD-003 und die erledigten TD-004/TD-005 werden nicht neu
  bewertet oder in einen separaten Sweep umgewandelt. Ein neuer relevanter
  DRY-Fund innerhalb der Registry-/Cleanup-Grenze wird dagegen proaktiv in
  diesem Paket behandelt.

### MagicValues-Plan

- `find_magic_values` auf die geänderten Produktionsdateien begrenzen;
  Exception-/Cleanup-Semantik wird über benannte bestehende Verträge und
  nicht über neue globale Status-/Timeout-/Pfadkonstanten ausgedrückt.
- Testfehler-Marker nur einmalig oder als benannte testlokale Konstanten
  verwenden, falls sie mehrfach verglichen werden. Keine Änderung an
  Wire-, Config- oder Origin-Werten.

### DeadCode-Plan

- `find_dead_code` im Assemblies-Scope mit `private_internal` und beiden
  Vertrauensstufen ausführen. Ein neuer Aggregationshelper muss von
  Registry und Snapshot statisch referenziert sein.
- Bestehende Low-Confidence-Native-/ABI-Kandidaten oder unbeteiligte
  interne Aliase werden nicht gelöscht. Nur ein neu eingeführter,
  eindeutig unreferenzierter Cleanup-Pfad wäre innerhalb des Scopes zu
  entfernen.

## Risiken

- **Fehler wird maskiert:** Eine falsche Aggregation könnte die primäre
  Exception durch eine spätere Exception ersetzen. Einzelfehler werden
  deshalb mit `ExceptionDispatchInfo` weitergegeben; mehrere behalten ihre
  deterministische Reihenfolge.
- **Cleanup stoppt weiterhin:** Ein `catch` um den gesamten Durchlauf wäre
  unzureichend. Der Coder muss den `try`/`catch` pro Snapshot platzieren
  und erst nach dem letzten Versuch aggregieren.
- **Deadlock oder Reentrancy:** Snapshot-Dispose darf nicht unter dem
  Registry-Lock erfolgen. Entnahme und `Clear()` bleiben unter dem Lock,
  der sortierte Cleanup läuft außerhalb.
- **Ownership-Semantik driftet:** Ein Umbau darf Workspace-vor-Checkout,
  Snapshot-Idempotenz und die terminale Registry nicht verändern. Die
  lokale Regression und die bestehenden Registry-/Snapshot-Tests sichern
  diese Invarianten.
- **Unbounded Test:** Der Idempotenznachweis verwendet Zähler und den
  unmittelbaren zweiten Aufruf, nicht Sleeps, Retries oder fragile
  Zeitmessungen.

## Definition of Done

- Alle acht Abnahmekriterien sind durch Code, Regressionen und Nachweise
  belegt.
- Registry, gemeinsamer Fehlerpfad und lokale Tests bleiben innerhalb der
  drei gekoppelten Schichten; keine Out-of-Scope-Datei wird geändert.
- Der Coder dokumentiert im `step-result.md` die Cleanup-Reihenfolge,
  alle versuchten Snapshots, die sichtbare Fehlerform und den bounded
  Folge-Dispose.
- `dotnet build` sowie die beiden vollständigen Nicht-Stress-Gates sind
  grün; Stress bleibt ausgeschlossen.
- MCP-Impact-/Violation-Prüfungen sowie die begrenzten DRY-, MagicValues-
  und DeadCode-Audits sind ausgeführt und neue Befunde sind behandelt oder
  begründet außerhalb des Scopes belassen.
- Der Coder erstellt seinen Produktions-/Test-Commit und liefert den
  Result-/Statusnachweis; anschließend prüft ein neuer Kritiker den
  Korrektur-Step. Dieser Planer-Aufruf führt weder Coder noch Kritiker aus.
- Nach erfolgreichem Audit wird `step-plan.md` auf `done (pending audit)`
  und danach gemäß Orchestrator auf `done` fortgeschrieben.

## Coder-Hand-off

Starte als neuer Coder zuerst mit
`SourceSnapshotRegistry.Dispose()` und den oben genannten Snapshot-/Test-
Dateien. Behalte diese Invarianten:

- alle bereits entnommenen Snapshots werden genau einmal versucht;
- die Registry ist nach dem ersten Dispose terminal und leer;
- Workspace wird vor Checkout-Owner entsorgt;
- Fehler sind nach vollständigem Durchlauf sichtbar und bei mehreren
  Snapshots stabil geordnet;
- ein weiterer Registry-Dispose ist bounded und führt zu keinem Retry;
- Provider, Acquirer, Materializer, Host, Transport, Native-Prozesspfad,
  Refresh, Cache, Manifest, Source-of-Truth und EPIC-05 bleiben unverändert.

Verwende MCP zuerst für die genannten C#-Symbole mit dem absoluten
`projectRoot`; nutze `rg` nur für konkrete Text-/Scope-Prüfungen. Der
nächste sichere Einstiegspunkt für die Regression ist
`SourceSnapshotRegistryTests.cs`: Erweitere den vorhandenen AdhocWorkspace-
Aufbau um einen kleinen test-only Owner-Doppel und prüfe zuerst die
fehlerhafte erste Registry-Entsorgung, danach den unmittelbaren
Idempotenzaufruf. Kein neuer Snapshot-Builder neben dem vorhandenen Muster.

## Notes

Step 025 ist eine Korrektur innerhalb derselben EPIC-04-Ownership-Grenze
und kein neuer Refresh-/Cache-/Source-of-Truth-Schnitt. `step-024` bleibt
bis zur erneuten genehmigten Review fachlich offen; erst danach darf die
Roadmap den nachgelagerten Refresh-/Fetch-Vertrag aufnehmen.
