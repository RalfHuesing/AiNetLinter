---
status: done (Korrektur ausstehend)
type: step-plan
task: 11_epic-projektregistry-und-daemon
step: 004
corrects: step-003
title: "Produktions-Kalt-Load, Erstzugriffs-Dedupe und leasegeschuetzte Overview korrigieren"
epic: EPIC-A
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: GPT-5
created_by_model_knowledge_cutoff: nicht deklariert
created_at: 2026-08-23T20:59:35+02:00
related_to: ["step-003/step-review.md", "step-003/step-plan.md", "step-003/step-result.md"]
---

# Step 004: Produktions-Kalt-Load, Erstzugriffs-Dedupe und leasegeschuetzte Overview korrigieren

## Bezug

- **Task:** `11_epic-projektregistry-und-daemon`
- **Epic:** `EPIC-A` — Fix-Modus fuer genau die drei MAJOR-Findings aus
  `step-003/step-review.md`; die Roadmap wird in diesem Korrektur-Step nicht
  veraendert.
- **Korrekturquelle:** `step-003/step-review.md` Findings 1–3.
- **Konzept-/Vertragsbezug:** `Konzept.md` A.4 (kanonischer Key, genau ein
  Erstzugriff/Load und Lease-Lifetime), A.5 (Root-/Loader-Fehlervertraege) und
  A.7 (zweistufiger Load-Zustand, `PROJECT_LOAD_FAILED`, FAILED-Marker und
  Retry-Semantik).

## Aktueller Projektzustand (JIT-Kontext)

Die semantische MCP-Pruefung des aktuellen Codes ergibt:

- `McpServerCommand.CreateResidentInstance` setzt den produktiven
  `LoadFunc` auf `TryLoadSolutionAsync`. Diese Methode loggt beim Exception-Fall
  nur eine Warnung und liefert `null`; `McpCodeGraphServer.LoadState` erkennt
  dadurch zwar `LoadFailed`, aber `LastLoadError` kann die urspruengliche
  Produktionsmeldung nicht aus dem faulted Task ableiten. Der vorhandene
  `ProjectToolCall.LoadFailedResult` besitzt bereits den zentralen
  `PROJECT_LOAD_FAILED`-/Hint-Text und soll wieder mit dem Originalfehler
  gespeist werden.
- `ProjectRegistry.FindAdoptable` entfernt einen fehlgeschlagenen Entry vor der
  Fehlerantwort. `InsertResident` ruft `options.InstanceFactory` dagegen vor
  dem Registry-Lock auf; zwei Misses desselben kanonisierten Roots koennen
  deshalb zwei Instanzen samt Hintergrund-Load erzeugen. `Lease` ist bewusst
  synchron, `ProjectEntry` zaehlt aktive Leases atomar, und der eigentliche
  Solution-Load laeuft ueber den asynchronen `McpCodeGraphServer.LoadTask`.
- `OverviewResourceRegistration` hat das URI-Template und den gemeinsamen
  Root-Guard bereits, ruft in `BuildTemplatedResult` aber direkt
  `FindSnapshot`/`BuildResult` ohne Lease auf. `Snapshots()` bleibt fuer die
  read-only Health-Aggregation benoetigt; nur der Resource-Rendering-Pfad muss
  auf einen leasegebundenen Snapshot umgestellt werden.
- Bestehende Tests decken den Fehlervertrag mit einem kuenstlich faultenden
  Server ab, synchronisieren den zweiten Dedupe-Caller aber nicht belastbar,
  und pruefen bei Overview bislang keine Lease-Lifetime waehrend des Renderings.

### Anti-Loop- und Entscheidungsabgleich

Der Step-003-Plan bleibt in seinen Grundentscheidungen gueltig: synchroner
`Lease`-Einstieg, kanonischer Root, keine Registry-Sperre waehrend eines
Solution-Loads, keine negative Fehler-Caches und ein URI-Template fuer
Overview. Korrigiert wird nur die unvollstaendige Umsetzung:

- Fuer Dedupe wird eine per-Key-Reservation/Single-Flight-Struktur unter dem
  bestehenden Registry-Lock reserviert, deren Factory-Kick-off ausserhalb des
  Locks erfolgt. Ein globales `async`-Umbauen von `Lease` oder ein Warten auf
  `LoadTask` ist keine Alternative.
- Fuer den Kalt-Load wird der bestehende faulted-Task-Vertrag genutzt
  (`throw;` nach dem Warn-Log), statt einen zweiten Fehlerzustand neben
  `LoadState`/`LastLoadError` einzufuehren.
- Der CodeMap-Eintrag fuer `ProjectSnapshot` als leasefreier, read-only Blick
  bleibt fuer Health/Snapshots gueltig. Die Overview darf diesen Pfad jedoch
  nicht mehr direkt verwenden; sie erhaelt einen Snapshot nur innerhalb eines
  aktiven Leases. Das ist eine begruendete Einschraenkung des bisherigen
  Overview-Aufrufers, keine stille Ruecknahme der Health-Entscheidung.

## Intention

Nach diesem Fix liefert ein echter Produktions-Kalt-Load-Fehler eine
`PROJECT_LOAD_FAILED`-Antwort mit Originalmeldung und Restore-/Retry-Hint.
Der FAILED-Marker bleibt bis zur fertigen Fehlerantwort erhalten und wird erst
danach fuer einen frischen Retry freigegeben. Pro kanonischem Root entsteht bei
konkurrierenden Erstzugriffen genau eine residente Instanz; die Registry sperrt
dabei nie den eigentlichen Solution-Load. Auch die Overview liest und rendert
innerhalb eines Leases und verwendet dieselben Root-, Loader-, Loading- und
LoadFailed-Vertraege wie die Tools.

## Konkrete Änderungen

### 1. Produktions-Kalt-Load und FAILED-Marker — `src/AiNetLinter/Commands/McpServerCommand.cs`, `src/AiNetLinter/Mcp/McpCodeGraphServer.cs`, `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs`, `ProjectEntry.cs`, `ProjectLease.cs`, `ProjectToolCall.cs`

- **Was:** `TryLoadSolutionAsync` behaelt das Warn-Log fuer nicht-stornierende
  Ladefehler, propagiert danach aber die Originalexception mit `throw;`. Ein
  erfolgreicher Katalog mit `HasLoadingErrors` bleibt wie bisher ein geladener
  Zustand; `OperationCanceledException` behaelt die bestehende
  Cancellation-Semantik. `McpCodeGraphServer.LoadState` bleibt aus
  `_loadTask`/`_catalog` abgeleitet, und `LastLoadError` liest weiterhin die
  faulted-Task-Meldung bzw. Refresh-Fehler.
- **Was:** Ein vorhandener `LoadFailed`-Entry wird beim naechsten Lease nicht
  vorzeitig aus `projects` entfernt. Der Lease darf den Fehlerzustand adoptieren,
  damit `ProjectToolCall.LoadFailedResult` die Antwort mit Originalmeldung,
  Solution-Kontext und bestehendem Restore-/Retry-Hint erzeugt. Die Entfernung
  des FAILED-Markers erfolgt ueber den eindeutigen Entry/Lease-Release-Pfad
  erst nach dem Antwortaufbau; bei mehreren gleichzeitig offenen
  Fehlerantwort-Leases erst nach deren letztem Release. Ein Eviction-Tick darf
  einen busy FAILED-Entry nicht vor dieser Antwort entfernen. Der anschliessende
  neue Lease-Aufruf erzeugt eine frische Instanz; es entsteht kein negatives
  Caching.
- **Was:** Die Failure-Formatierung bleibt zentral. Falls Resource und Tool dafuer
  einen gemeinsamen Descriptor/Formatter benoetigen, wird der bestehende
  `LoadFailedResult`-/`RecoverHint`-Inhalt in eine kleine gemeinsame interne
  Hilfsstruktur verschoben; keine voneinander abweichenden Fehlertexte.
- **Warum:** Der Fehler darf nicht in `null` verloren gehen, und der Marker muss
  die Zustandsfolge Loading → Fehlerantwort → Retry abbilden. Ein expliziter
  paralleler Fehler-State waere hier unnoetig und wuerde den vorhandenen
  `LoadTask`-Vertrag duplizieren.

**Akzeptanzkriterien Finding 1:**

1. Ein kaputtes `.slnx` ueber den produktiven `TryLoadSolutionAsync`-Pfad
   fuehrt nach dem Loading-Zustand zu `PROJECT_LOAD_FAILED`; die Antwort enthaelt
   die echte Exception-Meldung, den Solution-Pfad und den bestehenden
   Restore-/Retry-Hint. Eine blosse Warnung oder ein generischer
   `null`-Fallback gilt nicht als bestanden.
2. Die FAILED-Instanz bleibt fuer die laufende Fehlerantwort resident und wird
   nicht durch `FindAdoptable` oder den Eviction-Tick vorzeitig ersetzt.
   Erst nach dem Release der Antwort-Leases ist der naechste Aufruf ein echter
   Retry mit neuer Instanz/neu gestartetem Load.
3. Cancellation und ein erfolgreicher Load mit Workspace-Diagnosen veraendern
   ihr bisheriges Verhalten nicht; `LastLoadError` enthaelt bei einem
   faulted Produktions-Load die urspruengliche Meldung.

### 2. Exakte Erstzugriffs-Deduplizierung ohne Registry-Lock waehrend des Loads — `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs` (+ ggf. kleine interne Reservation in `src/AiNetLinter/Mcp/Projects/`)

- **Was:** `Lease`/`TryAdoptOrCreate` reservieren den kanonisierten Key unter
  `gate` atomar. Existiert bereits ein residenter Entry, wird er wie bisher
  adoptiert. Existiert noch keiner, wird genau eine per-Key-Reservation (z. B.
  eine synchron auswertbare `Lazy`-Creation oder gleichwertige
  Single-Flight-Struktur) registriert; die Definition wird geladen und die
  `InstanceFactory` genau einmal ausserhalb von `gate` ausgefuehrt.
- **Was:** Nach dem Factory-Kick-off wird die erzeugte Instanz unter `gate`
  als `ProjectEntry` publiziert und der erste Lease geoeffnet. Konkurrierende
  Caller warten hoechstens auf die synchrone Creation/Reservation, niemals auf
  `McpCodeGraphServer.LoadTask` oder `SourceFileCatalog.LoadAsync`, und
  adoptieren danach denselben publizierten Entry/Server. Ein konkurrierender
  loser Server darf nie den von einem Gewinner-Lease verwendeten Server
  disposten; bei Creation-/Loader-Fehlern wird die Reservation fuer diese
  Zugriffswelle mit demselben Resultat beendet, ohne einen negativen residenten
  Entry zu hinterlassen.
- **Was:** Die Registry-Sperre umfasst ausschliesslich Lookup, Reservation,
  Publish/Adopt und Eviction-Entscheidungen. Der laufende Hintergrund-Load darf
  parallel dazu andere Roots bedienen. Bestehende Busy-/Pending-Eviction- und
  InFlight-Lease-Semantik wird wiederverwendet, nicht durch einen zweiten
  Zustandsmechanismus ersetzt.
- **Warum:** Factory-vor-dem-Lock dedupliziert nicht; Factory-im-Lock waere
  zwar eine einfache Reservation, koennte aber den Registry-Lock in Konstruktion
  oder Load-Kick-off hineinziehen. Die per-Key-Reservation trennt die
  Exklusivitaet der Instanz-Erzeugung von der Lebensdauer des Solution-Loads.

**Akzeptanzkriterien Finding 2:**

1. Zwei deterministisch synchronisierte parallele `Lease`-Aufrufe fuer denselben
   Root (einschliesslich `C:\\`/`/`-Schreibweisen) fuehren exakt einmal die
   `InstanceFactory` aus, erzeugen exakt einen Server/Background-Load und liefern
   beiden Callern Leases auf `ReferenceEquals`-dieselbe residente Instanz.
2. Der Test blockiert den ersten Creation-Kick-off vor dessen Abschluss und
   stellt sicher, dass der zweite Caller bereits in derselben Reservation-Welle
   wartet; ein zeitliches "vielleicht kommt der zweite Caller spaeter"
   reicht nicht als Dedupe-Nachweis.
3. Ein anderer Root bleibt waehrend eines blockierten Hintergrund-Loads
   leasebar. Es gibt keinen `.Wait()`/`.Result`-Zugriff auf den Solution-Load,
   keinen Registry-Lock ueber `await`/IO und keine Disposition einer von einem
   anderen Caller adoptierten Instanz.
4. Nach einem fehlgeschlagenen Creation-/Loader-Resultat bleibt der bisherige
   Fehlervertrag erhalten und ein spaeterer, nicht mehr konkurrierender Aufruf
   darf einen neuen Versuch starten.

### 3. Leasegeschuetzte Overview mit einheitlichen Vertraegen — `src/AiNetLinter/Mcp/OverviewResourceRegistration.cs`, `src/AiNetLinter/Mcp/Projects/ProjectRegistry.cs`, `ProjectToolCall.cs`/`ProjectLease.cs` falls fuer gemeinsame Formatter erforderlich

- **Was:** `BuildTemplatedResult` verwendet nach dem gemeinsamen
  `GuardRequiredAbsoluteRoot` nicht mehr `FindSnapshot` als Einstieg, sondern
  den Registry-Lease-Pfad. Der Lease bleibt bis nach Snapshot-Erzeugung,
  `BuildResult` und `BuildOverviewText` aktiv. Der Snapshot wird dabei unter
  Registry-Schutz aus genau dem geleasten Entry gewonnen; ein Snapshot ohne
  Lease bleibt ausschliesslich fuer die bestehende Health-Aggregation zulaessig.
- **Was:** Root- und Loader-Fehler werden fuer die Resource ueber denselben
  Guard-/Error-/Hint-Baustein wie bei den Tools als `McpException` formatiert:
  fehlender Root → `PROJECT_ROOT_REQUIRED`, relativer Root →
  `PROJECT_ROOT_INVALID`, fehlende/ungueltige Definitionsdatei sowie fehlende
  Solution/Rules → die jeweiligen `ProjectErrorCodes` inklusive des
  kopierfaehigen Definitionsdatei-/Restore-Hints. Ein unbekannter Root darf
  nicht nur einen verkuerzten Sondertext erhalten.
- **Was:** Bei `Loading` rendert die Resource den vorhandenen Overview-Status
  innerhalb des Leases. Bei `LoadFailed` wird statt eines leasefreien,
  veralteten Snapshots derselbe `PROJECT_LOAD_FAILED`-Code mit Originalmeldung
  und Restore-/Retry-Hint wie beim Tool ausgegeben; der Release nach dem
  Antwortaufbau aktiviert die oben beschriebene Retry-Semantik. Das
  URI-Template `ainetlinter://overview?projectRoot=<url-encoded>` bleibt
  unveraendert.
- **Warum:** `FindSnapshot` schützt weder Snapshot noch Rendering vor TTL/LRU-
  Eviction. Ein gemeinsamer leasegebundener Resource-Wrapper verhindert, dass
  der referenzierte Server zwischen Statuslese und Antwortaufbau dispost wird,
  und verhindert Drift zwischen Tool- und Resource-Fehlervertraegen.

**Akzeptanzkriterien Finding 3:**

1. Ein Resource-Read hält `InFlightCount > 0` vom Lease-Erwerb bis nach
   `BuildOverviewText`/Antwortaufbau; ein parallel ausgelöster TTL-/LRU-Tick
   darf den Entry in dieser Zeit weder entfernen noch den Server disposten.
   Der Test muss die Rendering-Phase deterministisch blockieren oder einen
   gleichwertigen testbaren Lease-Wrapper verwenden, nicht nur zwei zufällige
   Threads starten.
2. Resource und Tool liefern bei fehlendem, relativem, unbekanntem oder
   definitionsseitig fehlerhaftem Root denselben Error-Code sowie denselben
   wesentlichen Message-/Hint-Vertrag. Ein Loading-Read zeigt weiterhin den
   Loading-Status; ein fehlgeschlagener Read liefert `PROJECT_LOAD_FAILED` mit
   Ursprungsmeldung und Retry-Hint.
3. Ein URL-kodierter absoluter Root adressiert denselben kanonischen Key wie
   seine alternative Pfadschreibweise; die ausgegebene Resource-URI bleibt
   kanonisch URL-kodiert.
4. Die read-only Health-Snapshot-Aggregation und ihre Lease-unabhaengige
   Semantik bleiben unberuehrt; nur Overview-Rendering wird leasegebunden.

## Tests

Entwicklung und Fehlersuche laufen gezielt und gefiltert; keine Testklasse wird
zur Behebung eines Nebenlaeufigkeitsproblems global serialisiert.

- [ ] `ColdLoadFault_AnswersLoadingThenProjectLoadFailed` erweitern oder durch
  einen Produktionsvertragstest ergaenzen: `McpServerCommand.TryLoadSolutionAsync`
  mit kaputter Solution statt `FaultingLoadServer`; Originalexception,
  `PROJECT_LOAD_FAILED`, Restore-Hint sowie Marker-/Retry-Folge pruefen.
- [ ] Den direkten Kalt-Load-Vertragstest auf die neue Propagation umstellen:
  Warnung darf weiterhin geloggt werden, aber der Await muss die
  Originalexception beobachten; Cancellation separat unveraendert pruefen.
- [ ] `Lease_ParallelCallersOnSameRoot_CreateExactlyOneInstance` mit einer
  belastbaren Reservation-Barriere nachschaerfen: zweiter Caller ist vor
  Freigabe des ersten Kick-offs in derselben Welle, Factory-Aufrufzahl exakt 1,
  beide Server identisch; den Test `Lease_DuringRunningBackgroundLoad_OtherRootsStayServiceable`
  als Lock-Hygiene-Anker beibehalten.
- [ ] FAILED-Marker-Tests an die Antwort-Lifetime anpassen: Marker bleibt bis
  nach `PROJECT_LOAD_FAILED`, busy failed entries werden nicht vom Tick
  entfernt, danach startet der naechste Aufruf genau einen frischen Load.
- [ ] Overview-Contracttests fuer Loading, Failed, Definitions-/Root-Fehler und
  identische Message-/Hint-Vertraege gegen den Tool-Pfad ergaenzen.
- [ ] Deterministischer Overview-Lease-/Eviction-Test mit blockierter
  Rendering-Phase; pruefen, dass Disposition erst nach Lease-Release erfolgt.
- [ ] Gezielte Iteration: `dotnet test src/AiNetLinter.FastTests --filter Category=Unit`
  sowie die betroffenen Integrationstests mit Testnamenfilter; bei Fehlern
  TRX-Diagnose statt blindem Wiederholungslauf.
- [ ] Abschluss dieses Steps genau einmal: `dotnet build`, danach
  `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
  `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`.

## Definition of Done

- [ ] Alle drei Findings aus `step-003/step-review.md` sind durch die obigen
  Akzeptanzkriterien und Tests abgedeckt; keine „Sonstigen Beobachtungen“ oder
  Tech-Debt-Eintraege werden in diesen Fix aufgenommen.
- [ ] Produktions-Kalt-Load propagiert die Originalmeldung, liefert den
  vorgeschriebenen `PROJECT_LOAD_FAILED`-/Restore-Hint und ermoeglicht den
  Retry erst nach der Fehlerantwort.
- [ ] Konkurrierende Erstzugriffe deduplizieren pro kanonischem Root exakt eine
  residente Instanz, ohne den Registry-Lock ueber den Solution-Load zu halten.
- [ ] Overview-Snapshot und Rendering sind leasegeschuetzt und die Root-,
  Loading-, Loader- und Failed-Vertraege sind mit den Tools konsistent.
- [ ] Build und beide Nicht-Stress-Testprojekte sind gruene Abschluss-Gates;
  der vollstaendige Nicht-Stress-Stack wird in diesem Step genau einmal als
  Abschlusslauf ausgefuehrt.
- [ ] Vor dem zukuenftigen Commit: AiNetLinter-MCP-Quality-Gates fuer die
  geaenderten Scopes ausfuehren — `get_violations` ohne Verstoss,
  `safeguard` bestanden und `metrics_lookup` vor/nach fuer die umgebauten
  Methoden/Typen innerhalb der Regeln.
- [ ] `step-004/step-result.md` mit Abweichungen, Test-/Build-Nachweisen und
  MCP-Quality-Gates schreiben; danach den Status dieses Plans auf
  `done (pending audit)` setzen.
- [ ] Der spaetere Commit erfolgt auf dem aktuellen Branch mit deutschem
  imperativem Conventional-Commit; dieser Plan selbst fuehrt keinen Commit aus.

## Rules-Refs

- `.agents/rules/AiNetLinter.mdc#agent-resilience` — kein Blocking auf
  `Task`-Loads; `catch` muss loggen und sichtbar propagieren; `async`-Pfade
  muessen await-basiert bleiben.
- `.agents/rules/AiNetLinter.mdc#Kurz-Stil` und `#Grenzwerte` — bestehende
  Registry-/Lease-Strukturen erweitern, Methoden-/Parameter-/Kopplungsbudgets
  vor und nach dem Refactoring per MCP pruefen.
- `.agents/rules/AiNetLinterRichtlinien.mdc#1` — C#-Symbole vor Aenderungen
  und Impact semantisch ueber den projektgebundenen AiNetLinter-MCP pruefen;
  Doku nur gegen implementiertes Verhalten verifizieren.
- `.agents/rules/AiNetLinterRichtlinien.mdc#4` — xUnit-v3-Tests,
  `TestTempDirectory`, keine zwangsserialisierenden Collections, gefilterte
  Iteration und genau ein vollstaendiger Nicht-Stress-Abschlusslauf.
- `.agents/rules/AiNetLinterRichtlinien.mdc#5` — Result-/Fehlervertraege
  explizit halten, Zero-Warning-Gate einhalten und keine Step-/Finding-IDs in
  Produktionskommentaren einfuehren.

## Bekannte Ausnahmen

- Keine neue Ausnahme fuer diesen Fix. Die bereits dokumentierte
  Console-Warnung beim synchronen Disposal faulted Loads (TD-005) und der
  nicht injizierbare ConfigLoader-Diagnosekanal bleiben ausserhalb dieses
  Finding-Scopes und werden nicht erweitert.

## Code-Skizze (optional)

```csharp
// Fehler: Warnung behalten, den Task aber faulted lassen.
catch (Exception ex) when (ex is not OperationCanceledException)
{
    console.WriteError($"[WARN]: ... {ex.Message}");
    throw;
}

// Dedupe: nur Reservation/Publish unter gate; Creation und Load-Kick-off ausserhalb.
var reservation = ReserveCanonicalKey(key); // Registry-Lock kurz halten
var creation = reservation.GetOrCreate();    // kein Warten auf server.LoadTask
return PublishOrAdopt(key, creation);       // Registry-Lock erneut kurz halten

// Overview: derselbe leasegebundene Lebenszyklus wie beim Tool.
using var lease = registry.Lease(projectRoot).Lease!;
var snapshot = registry.SnapshotFor(lease);
return BuildResult(snapshot);                // Lease endet erst danach
```

## Notes

- Die Produktionsregression muss den echten Kompositionspfad mit
  `TryLoadSolutionAsync` pruefen; ein nur kuenstlich faultender
  `McpCodeGraphServer` bestaetigt die Review-Luecke nicht.
- Die Dedupe-Reservation darf nur die synchrone Erstellung/Publizierung
  single-flight machen. Sie darf weder `McpCodeGraphServer.LoadTask` synchron
  auswerten noch die Registry-Sperre ueber `SourceFileCatalog.LoadAsync`
  halten. Der bestehende `ProjectEntry.InFlightCount` bleibt der einzige
  Eviction-Schutz fuer aktive Aufrufe.
- Fuer die Overview muss ein gemeinsamer Formatter fuer Tool-/Resource-Fehler
  bevorzugt werden; eine zweite, leicht abweichende Interpretation von
  `ProjectErrorCodes` ist nicht zulaessig. Die Resource darf bei Loading ihren
  lesbaren Status rendern, muss bei LoadFailed aber den Fehlervertrag wie ein
  Tool ausgeben.
- Roadmap, `Docs/ROADMAP.md`, Meilenstein-Doku, Drift-Audit und sonstige
  Beobachtungen aus dem Step-003-Review sind in diesem Fix-Modus nicht Scope.
