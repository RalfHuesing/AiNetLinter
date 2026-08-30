---
status: done (pending audit)
type: step-plan
task: decompiled-assembly-analysis
step: 036
corrects: null
title: "Gitea-Source-of-Truth mit Clean-Checkout und transparentem degraded Refresh-Vertrag absichern"
epic: EPIC-04
estimated_risk: high
step_type: single
items: []
created_by: planer
created_by_model: gpt-5 (Codex)
created_by_model_knowledge_cutoff: nicht angegeben
created_at: 2026-08-30T05:30:00+02:00
related_to:
  - ../step-035/step-plan.md
  - ../step-035/step-result.md
  - ../step-035/step-review.md
  - ../step-034/step-review.md
  - ../step-033/step-review.md
---

# Step 036: Gitea-Source-of-Truth mit Clean-Checkout und transparentem degraded Refresh-Vertrag absichern

## Bezug und Bündelungsentscheidung

- **Task:** `decompiled-assembly-analysis`
- **Epic:** `EPIC-04`; Gitea bleibt die Source of Truth, der lokale Cache
  bleibt eine validierte und besitzgeschützte Zwischenstufe.
- **Konzept-Referenzen:** `Konzept.md` — „Uncommitted Source und nicht
  gebauter Arbeitsstand“, „Gitea als gemeinsame Wahrheit“, „Staleness und
  atomarer Session-Wechsel“ sowie „Vertrauensstufen“.
- **Vorgänger:** Step 035 ist mit `5c830e44`, `8182b992` und Review
  `c4ee413c` genehmigt. Die diagnoseunabhängige terminale
  `ConfigurationFailure`-Grenze sowie die strikte CacheRoot-/URI-/UNC-
  Matrix werden nicht erneut geöffnet.

Die nächste fachliche Lücke liegt an einer zusammenhängenden
Source-Policy-Grenze: Der aktuelle Transport kennt zwar den geladenen
Commit, der Acquirer erzwingt Ownership und der Refresh verwirft bei einem
Fehler den neuen Checkout, aber ein stale-Refresh-Fehler wird anschließend
nur als generisches `ProviderUnavailable` sichtbar. Gleichzeitig gibt es
noch keinen expliziten Clean-/Unverified-Zustand für einen manipulierten
oder nicht vertrauenswürdig materialisierten Checkout.

Dirty-/Unbuilt-Abgrenzung und transparente Fallback-/Health-Semantik werden
deshalb in einem vertikalen Vertrag gebündelt. Das Paket führt keinen
lokalen Checkout-Modus ein: In der aktuellen Architektur ist ein lokaler
Dirty-/Unbuilt-Stand gerade keine zulässige externe Quelle. Es wird nur die
bestehende besitzgeschützte Staging-Grenze so ausgebaut, dass ein nicht
verifizierbarer Stand fail-closed bleibt und ein bekannter alter Cache-Stand
als `degraded` ausschließlich als letzter guter Nachweis sichtbar wird.
Ein isolierter Status-/Assertion-Fix würde diese Transport-, Cache-,
Provider- und Selection-Grenze nicht schließen.

## Split-Gate und Kontextbudget

Der Step hat genau einen Primärvertrag und drei logisch gekoppelte Schichten:

1. **Checkout-Trust:** Nur ein besitzgeschützter, sauber verifizierter
   Staging-Checkout mit nachgewiesener HEAD-Revision darf als Gitea-Source
   weitergegeben oder veröffentlicht werden. Dirty, unverified und lokale
   Arbeitsstände werden nicht zu einer Source of Truth.
2. **Refresh-Health:** Ein fehlgeschlagener Refresh eines zuvor validierten
   `current`-Generationsstands liefert `degraded` mit sicherem
   Last-good-Commit/Diagnose, aber keinen neuen Source-Snapshot und keinen
   stillschweigenden „aktuellen“ alten Match. Ohne validen Last-good-Stand
   bleibt der Zustand `unavailable`.
3. **Selection-/Fallback-Grenze:** Provider und Selection tragen den
   Health-Zustand bis zur bestehenden Assembly-Tool-Grenze. `degraded` bleibt
   ein sichtbarer statischer Decompilation-Fallback; `ConfigurationFailure`
   bleibt der einzige bereits definierte terminale Config-Pfad. Host-/MCP-
   Health-Wiring wird nicht vorgezogen.

`max_initial_files: 12`

Der Coder liest vor dem ersten Edit genau die zehn `read_first`-Dateien.
Zwei Dateien sind als fokussierter `read_on_demand`-Kontext für die
Transport-/Refresh-Regressionen vorgesehen. Provider-, Selection- und
Assembly-Tool-Tests werden erst nach den projektgebundenen MCP-Abfragen und
nur in den jeweils betroffenen Ausschnitten geöffnet; dadurch bleibt der
Kontext unter dem vorgegebenen Limit.

### `read_first` (10 Dateien)

1. `tasks/decompiled-assembly-analysis/codemap.md`
2. `tasks/decompiled-assembly-analysis/step-035/step-plan.md`
3. `tasks/decompiled-assembly-analysis/step-035/step-result.md`
4. `tasks/decompiled-assembly-analysis/step-035/step-review.md`
5. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
6. `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`
7. `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`
8. `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs`
9. `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`
10. `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`

### `read_on_demand` (2 Dateien)

- `src/AiNetLinter.FastTests/Mcp/Assemblies/GiteaGitRepositoryTransportTests.cs`
- `src/AiNetLinter.FastTests/Mcp/Assemblies/ExternalSourceRepositoryCacheRefreshTests.cs`

Die vom Coder vor dem Edit auszuführenden MCP-Abfragen verwenden immer den
absoluten `projectRoot` `C:/Daten/Entwicklung/Ralf/AiNetLinter`. Die reale
Zeilenlage ist verbindlich zu prüfen: `ExternalSourceRepositoryAcquirer.cs`
liegt mit 499 Zeilen bereits an der Produktionsgrenze und darf nicht durch
weitere Validierungslogik über die Grenze wachsen; der neue Checkout-Policy-
Helper ist bei Bedarf eine eigene, fokussierte Datei.

## Aktueller Projektzustand (JIT-Kontext)

- `GiteaGitRepositoryTransport` führt beim Refresh `fetch`,
  `reset --hard origin/HEAD` und `rev-parse HEAD` aus. Ein expliziter
  `git status --porcelain`-Nachweis des Staging-Checkouts fehlt. Die
  Prozessgrenze ist injizierbar; echte Netzwerkzugriffe sind daher für die
  Regressionen nicht erforderlich.
- `ExternalSourceRepositoryCacheMaterializer` materialisiert aus einer
  validierten Generation in einen neuen, besitzmarkierten Checkout. Die
  Cache- und Ownership-Invarianten sind vorhanden; ein unbesitzter oder
  manipuliert materialisierter Checkout darf nicht weitergereicht werden.
- `ExternalSourceRepositoryAcquisitionResult` und
  `ExternalSourceProviderResult` unterscheiden aktuell nur verfügbar bzw.
  nicht verfügbar. Bei einem stale-Refresh-Fehler geht die Information über
  den validierten alten `current`-Stand verloren.
- `ExternalSourceRepositoryCacheRefresh` kennt beim Fehler noch den
  validierten `readResult.Manifest.LoadedRevision`, gibt ihn aber nicht als
  Last-good-Nachweis weiter. Der alte `current`-Pointer bleibt durch den
  Publish-Vertrag erhalten und darf nicht als neuer erfolgreicher Refresh
  ausgegeben werden.
- `AssemblySourceSelectionScope` kennt derzeit `Matched`, `NoMatch`,
  `Ambiguous`, `ProviderUnavailable` und `ConfigurationFailure`. Die
  bestehende `AssemblyAnalysisToolSupport`-Grenze fällt bei nicht terminalen
  Providerzuständen in die statische Decompilation zurück und fügt die
  Providerdiagnosen bereits dem Kontext hinzu.
- `AssemblyAnalysisSessionStatus.Degraded` und
  `GetServerHealthTool` modellieren andere Grenzen: Session-/Daemon-Health
  ist nicht automatisch externe Repository-Health. Eine Vermischung würde
  den MCP-/Host-Scope unkontrolliert erweitern.
- Die Pläne, Resultate und Reviews von Step 033, 034 und 035 wurden für die
  aktuelle Grenze erneut geprüft. Die CacheRoot-/Options-/URI-Verträge und
  die diagnoseunabhängige `ConfigurationFailure`-Terminalität sind
  abgeschlossene Voraussetzungen, nicht Teil einer erneuten
  Korrekturschleife.
- Die Produktionsdateien liegen bei ungefähr 431 Zeilen für den Git-
  Transport, 384 für den Refresh und 499 für den Acquirer. Die bestehenden
  Transport-/Refresh-Testdateien liegen bei 487 bzw. 423 Zeilen. Neue
  Status-/Policy-Logik und neue Matrizen werden deshalb in fokussierte
  Helper-/Testdateien extrahiert, nicht in die bereits grenznahen Monolithen
  kopiert.

## Intention

Nach diesem Step ist der externe Gitea-Pfad hinsichtlich Checkout-Trust und
Refresh-Health explizit: Nur ein eigener, sauber verifizierter Commit kann
eine externe Source liefern oder eine Cachegeneration veröffentlichen.
Scheitert die Aktualisierung eines validierten alten Stands, bleibt dieser
als Last-good-Nachweis erhalten, wird aber nicht still als aktuell verwendet;
Selection und Assembly-Tool zeigen `degraded` und halten den statischen
Decompilation-Fallback mit sicheren Diagnosen offen.

## Konkrete Änderungen

### 1. Immutable Checkout-Trust und Health-Resultate

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs`
  (neu, fokussierter Helper)
- `src/AiNetLinter/Mcp/Assemblies/IGiteaRepositoryTransport.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`

- Führe einen kleinen, internen Trust-/Health-Vertrag ein, der zwischen
  `Verified`, `Degraded` und `Unavailable` unterscheidet und die
  Checkout-Prüfung `Clean`/`Dirty`/`Unverified` nicht über freie Strings
  verteilt. Die Namen müssen an den bestehenden
  `AssemblyAnalysisSessionStatus`-Vertrag angelehnt, aber nicht mit ihm
  vermischt werden.
- Erweitere die Transport-/Acquisition-/Provider-Ergebnisse immutable um
  den verifizierten Zustand und optional den letzten guten Commit. Die
  Factory-Invarianten bleiben streng: `Verified` benötigt gültige Revision,
  Checkout und Snapshot; `Degraded` trägt keinen neuen Snapshot/Checkout;
  `Unavailable` trägt keinen Last-good-Wert, wenn kein validierter alter
  Stand existiert.
- Zentralisiere die neue Status-/Diagnoseabbildung in der Policy-Datei.
  Bestehende `ExternalSourceRepositoryFailurePolicy`-Methoden werden nur
  wiederverwendet; sie wird nicht durch weitere heterogene Zustände zu einer
  God-Class erweitert. Ein stabiler Diagnosecode für degraded muss
  geheimnisfrei bleiben und darf weder Repository-URL mit Credentials noch
  Pfade ausgeben.
- Ein „unbuilt“-Stand wird nicht über Zeitstempel, `bin`-/`obj`-Heuristiken
  oder lokale Dateiinhalte erraten. Ohne ausdrücklich unterstützten
  Build-/Binary-Nachweis ist eine lokale Arbeitskopie kein Provider-Input;
  der Gitea-Commit bleibt die einzige akzeptierte Source-Identität dieses
  Steps.

### 2. Clean-/Unverified-Gate im besitzgeschützten Git-Refresh

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/GiteaGitRepositoryTransport.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositorySourcePolicy.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquirer.cs`

- Prüfe im request-eigenen, ownership-validierten Checkout den
  nicht-interaktiven Git-Status vor der mutierenden Refresh-Sequenz und
  verifiziere nach dem Fetch/Reset weiterhin die erwartete Revision und den
  cleanen Zustand. Ein Dirty- oder nicht auswertbarer Status wird typisiert
  fail-closed behandelt; es darf weder als Source noch als Cachegeneration
  veröffentlicht werden.
- Clone und Refresh behalten die vorhandene Credential-Isolation,
  Cancellation-, Timeout-, 1314- und Reparse-Semantik. Der Statusprozess
  darf keine Credentials oder Rohprozessausgaben in Diagnosen tragen.
- Verstärke die gemeinsame Acquirer-/Refresh-Validierung für Ownership,
  Solution-Pfad, geladene Revision und Trust-Zustand. Da der Acquirer bei
  499 Zeilen liegt, werden wiederverwendbare Prüfungen bei Bedarf in den
  neuen Helper verschoben; kein bloßes Zeilenumbruch-Refactoring und kein
  zweiter konkurrierender Validator.
- Ein lokaler Dirty-/Unbuilt-Checkout erhält keinen alternativen
  `SourceSnapshot`-Pfad. Nur die vom Acquirer reservierte Staging-Wurzel und
  die bestehenden Ownership-Marker dürfen in diesen Vertrag eintreten.

### 3. Last-good/degraded durch Refresh und Provider propagieren

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryCacheRefresh.cs`
- `src/AiNetLinter/Mcp/Assemblies/ExternalSourceRepositoryAcquisitionModels.cs`
- `src/AiNetLinter/Mcp/Assemblies/GiteaExternalSourceProvider.cs`
- `src/AiNetLinter/Mcp/Assemblies/IExternalSourceProvider.cs`

- Bewahre beim stale-Refresh den zuvor durch den Cache-Reader validierten
  Last-good-Commit nur als immutable Metadatum am Fehlerresultat. Jede
  Fehlerstrecke — Transport, Checkout-Validierung, Materialisierung,
  Publish-/Pointer-Race und Cleanup — muss den neuen Checkout aufräumen und
  konsistent `Degraded` oder `Unavailable` liefern.
- Ein `CurrentChanged`-Race darf weiterhin einen inzwischen frischen
  `current`-Stand wiederverwenden. Nur wenn dieser sichere Reuse nicht
  gelingt, wird der alte Stand als `Degraded` markiert; er wird nicht als
  neuer `Success`-Checkout ausgegeben und nicht in die Registry gelegt.
- Die Provider-Projektion übernimmt Health, Last-good-Revision und die
  geheimnisfreien Diagnosen. Ein `Degraded`-Providerresultat besitzt keinen
  `ExternalSourceSnapshot`; damit bleibt der statische Fallback
  fail-closed gegen stale Originalquellen.
- Die bisherige positive `Success`-/Cache-Reuse- und Ownership-Lifetime
  bleibt unverändert. Cleanup-Fehler und Cancellation behalten ihre
  bisherige Priorität und dürfen nicht durch die neue Diagnoseinformation
  verschluckt werden.

### 4. Selection-/Assembly-Grenze und fokussierte Regressionen

**Dateien:**

- `src/AiNetLinter/Mcp/Assemblies/AssemblySourceSelectionOrchestrator.cs`
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisToolSupport.cs`
  (read-only gegen das bestehende Gate; nur falls die bestehende
  Diagnoseaggregation den neuen Status nicht bereits sichtbar macht)
- fokussierte neue oder bestehende FastTest-Dateien unter
  `src/AiNetLinter.FastTests/Mcp/Assemblies/` und
  `src/AiNetLinter.FastTests/Mcp/Tools/AssemblyAnalysis/`

- Ergänze einen eindeutigen `ProviderDegraded`-/äquivalenten
  Selection-Zustand mit Last-good-Nachweis. `ConfigurationFailure` bleibt
  diagnoseunabhängig terminal; `ProviderDegraded` darf dagegen nicht vor
  `AssemblyAnalysisService.CreateContextAsync` abbrechen.
- Die vorhandene Tool-Unterstützung soll den neuen Zustand über die
  bestehenden sicheren Providerdiagnosen im statischen Kontext sichtbar
  machen. `McpToolResults`, Host-Komposition und ein neues MCP-Resultatobjekt
  werden nicht global umgebaut. Falls ein zusätzlicher Text erforderlich
  ist, wird ausschließlich ein stabiler code-/statusbasierter, secret-freier
  Diagnosezusatz verwendet.
- Ergänze lokale Regressionen für clean/dirty/unverified Git-Status,
  Refresh-Fehler mit validiertem Last-good-Stand, fehlenden Last-good-
  Ständen, `CurrentChanged`-Race, Cleanup/Cancellation sowie die
  Provider-zu-Selection-Weitergabe. Die positiven `NoMatch`, `Ambiguous`,
  `ProviderUnavailable`, `RepositoryCapabilityUnavailable` und statischen
  Decompilation-Fallbacks bleiben aktive Gegenproben.
- Die 487-zeilige Transport-Testdatei und die 482-zeilige
  `AssemblyAnalysisToolSupportTests`-Datei werden nicht mit einer zweiten
  Inline-Matrix überladen. Neue Statusfälle kommen in fokussierte Testdateien;
  gemeinsame Fakes und Matrixwerte werden lokal zentralisiert.

## Scope

- Expliziter Clean-/Dirty-/Unverified-Trustvertrag für den bereits
  besitzgeschützten Gitea-Staging-Checkout.
- Immutable `Verified`-/`Degraded`-/`Unavailable`-Healthsemantik für
  Acquisition, Provider und Assembly-Selection.
- Last-good-Commit als sicherer Diagnose-/Statusnachweis bei fehlgeschlagenem
  stale Refresh, ohne alten Stand still als aktuell zu analysieren.
- Erhalt des statischen Decompilation-Fallbacks, der Cache-/Pointer-/Lease-
  Ownership, der Cancellation-/Cleanup- und der repository-spezifischen
  1314-/Reparse-Regel.
- Lokale, isolierte Regressionen und nur innerhalb dieser Vertragsgrenze
  erforderliche DRY-/MagicValues-/DeadCode-Korrekturen.

## Out of Scope

- Kein lokaler Checkout-Modus, keine konkurrierende Source of Truth und keine
  heuristische Erkennung eines unbuilt-Binaries über Zeitstempel,
  `bin`/`obj`, Dateiinhalte oder fehlende Buildmanifeste. Binary-/Source-
  Fingerprint-Parität und ein späterer expliziter Local-Origin-Vertrag bleiben
  eigene Folgeentscheidungen.
- Kein `GetServerHealthTool`, kein `McpCodeGraphServer`-/Daemon-/Host-/MCP-
  Wiring, kein neues öffentliches Resultat-Schema und keine SDK-Bindung.
  Der Health-Zustand bleibt innerhalb des bestehenden Provider-/Selection- /
  Tool-Fallbacks sichtbar.
- Keine Retention, Garbage Collection, explizite Cache-Invalidierung oder
  Telemetrie.
- Keine transitiven Referenzen, keine gemeinsame Capability-Matrix und keine
  EPIC-05-Arbeit.
- Keine Änderung an `AssemblyAnalysisSession`-Lifecycle oder dessen bereits
  eigenem `Degraded`-Status; keine globale Änderung an `McpToolResults`.
- Keine neuen echten Netzwerk-/Gitea-/Credential-Tests, kein Assembly-Load,
  keine Reflection-Ausführung und keine globale Reparse-/Win32-Aktion.
- Keine globalen DRY-, MagicValues-, DeadCode- oder Safeguard-Sweeps.
  `TD-001` bis `TD-003` bleiben unverändert; breite bestehende Befunde
  werden nicht als Step-036-Ziel umetikettiert.
- Kein Stress-Test und keine Produktionsänderung, kein Testlauf sowie keine
  Coder-/Kritikerarbeit während dieses Planer-Schritts.

## Architekturgrenze

Die neue Entscheidung liegt zwischen dem besitzgeschützten Git-/Cache-
Checkout und der bestehenden Provider-/Selection-Grenze. Transport und
Checkout-Policy liefern nur einen immutable verifizierten Zustand oder einen
fail-closed Fehler. Refresh darf bei einem nachgewiesen validierten alten
`current` dessen Commit als `degraded`-Nachweis behalten, aber weder den alten
Generation-Pointer umdeuten noch einen stale Snapshot als aktuelle Source
registrieren. Provider und Selection reichen den Zustand bis zum bereits
vorhandenen Assembly-Fallback; Host-Health, Retention und transitive
Auflösung bleiben außerhalb.

## Invarianten

1. Nur ein reservierter, ownership-validierter und clean verifizierter
   Checkout mit sicherer HEAD-Revision und validiertem Solution-Pfad kann
   `Verified` werden, einen Snapshot erzeugen oder eine Generation
   veröffentlichen.
2. Dirty, unverified, unbesitzte oder lokale Arbeitsstände werden nie als
   externe Source of Truth akzeptiert; sie erhalten keinen Registry-Lease und
   keinen Source-backed Assembly-Match.
3. Ein stale-Refresh-Fehler verändert den bestehenden `current`-Pointer nicht
   und gibt den neuen Checkout nie als Erfolg zurück. Bei validiertem altem
   Stand entsteht `Degraded` mit Last-good-Commit, sonst `Unavailable`.
4. `Degraded` trägt keinen neuen `ExternalSourceSnapshot` und wird nicht als
   aktueller externer Source-Match analysiert; der statische Decompilation-
   Fallback bleibt der sichere Analysepfad.
5. Ein erfolgreich validierter Clone, Fetch/Reset/HEAD und Cache-Reuse bleibt
   `Verified`; bestehende Cache-, Pointer-, Lease-, Cleanup- und
   Cancellation-Invarianten werden nicht gelockert.
6. `NoMatch`, `Ambiguous`, `ProviderUnavailable` und
   `RepositoryCapabilityUnavailable` behalten ihre bisherige Fallback- und
   Diagnosebedeutung; nur der neue `ProviderDegraded`-Zustand macht den
   Last-good-Fall explizit.
7. Health-/Trust-Diagnosen enthalten keine Credentials, Roh-URLs,
   Dateipfade oder Prozessausgaben; `IsError`- und `Recoverable`-Semantik
   des bestehenden Toolvertrags bleibt unverändert.
8. Tests nutzen ausschließlich `TestTempDirectory` und injizierbare Fakes;
   keine globale Serialisierung, kein Netzwerk und kein Stresslauf.

## Abnahmekriterien (8)

1. Die transportnahe Matrix unterscheidet clean, dirty/untracked und nicht
   auswertbaren Git-Status deterministisch. Dirty/unverified Checkoutdaten
   werden fail-closed mit stabilem Diagnosecode behandelt; Credentials und
   Rohprozessausgaben erscheinen nicht in den Diagnosen.
2. Der Acquirer akzeptiert weiterhin ausschließlich den eigenen, validierten
   Staging-Checkout mit gültigem Ownership-Marker, Solution-Pfad und
   geladener Revision. Ein lokaler oder unbuilt Arbeitsstand besitzt keinen
   alternativen Source-/Snapshot-Pfad.
3. Ein Fehler beim stale Refresh eines validierten `current` liefert
   `Degraded` samt Last-good-Commit/Diagnose, räumt den neuen Checkout auf,
   lässt den alten Pointer unverändert und erzeugt weder neuen Snapshot noch
   Registry-Lease. Ohne validierten Last-good-Stand bleibt der Zustand
   `Unavailable`.
4. Ein erfolgreicher Refresh sowie ein sicherer `CurrentChanged`-Reuse
   bleiben `Verified`; die erwartete Revision, Cachegeneration, Ownership und
   Cleanup-Lifetime werden weiterhin atomar nachgewiesen.
5. Provider-Projektion und Selection tragen `Verified`/`Degraded`/
   `Unavailable` deterministisch weiter. `Degraded` erreicht den statischen
   Assembly-Fallback mit sichtbarer, sicherer Diagnose und wird nicht als
   terminale `ConfigurationFailure` fehlklassifiziert.
6. Bestehende positive `NoMatch`, `Ambiguous`, `ProviderUnavailable`,
   `RepositoryCapabilityUnavailable` und statische Decompilation-Fallbacks
   bleiben grün; `ConfigurationFailure` bleibt unabhängig von leerer
   Diagnoseliste terminal und `IsError == false`.
7. Die neuen und angepassten Testdateien bleiben unter den projektspezifischen
   Zeilengrenzen, verwenden keine ad-hoc Temp-Pfade oder Netzwerke, und
   direkte DRY-/MagicValues-/DeadCode-Korrekturen bleiben auf dieses Paket
   beschränkt. Der breite Magic-Values-Befund wird nicht als globaler Sweep
   bearbeitet.
8. `dotnet build`,
   `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress` und
   `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
   laufen grün. `Category=Stress` wird nicht ausgeführt; das Resultat nennt
   exakte Zahlen, bekannte Win32-1314-Skips, Leaks, scoped MCP-/Qualitäts-
   nachweise und den unverändert ehrlichen Safeguard-Befund.

## Teststrategie

Der Coder startet nach MCP-/Zeilenprüfung mit fokussierten Unit-/Component-
Gruppen:

- neue Checkout-Trust-Tests für clean/dirty/unverified Status und
  Credential-/Prozessdiagnose-Isolation;
- Cache-Refresh-Tests für `Degraded` mit Last-good-Revision, fehlenden
  Last-good-Daten, Pointer-Erhalt, `CurrentChanged`, Cleanup und Cancellation;
- Provider-/Selection-/Assembly-Tool-Tests für Statusweitergabe, fehlenden
  Snapshot/Lease bei degraded und den statischen Fallback;
- bestehende positive Cache-, NoMatch-, Ambiguous-, ProviderUnavailable-,
  Capability- und ConfigurationFailure-Regressionen.

Danach folgen ausschließlich die beiden vollständigen Nicht-Stress-Gates und
`dotnet build`. Alle Testdoubles bleiben lokal/in-memory; `TestTempDirectory`
ist der einzige Testroot. Die zwei bekannten Win32-1314-Reparse-Skips bleiben
hosttransparent und werden weder simuliert noch als globale Sperre behandelt.

## MCP-, DRY-, Magic-Values- und Dead-Code-Disposition

Vor und nach jedem Edit fragt der Coder die betroffenen C#-Symbole über das
projektgebundene MCP mit
`projectRoot=C:/Daten/Entwicklung/Ralf/AiNetLinter` ab: zuerst
`get_feature_context`/`get_symbol_body`, anschließend gezielt
`find_references`, `get_impact` und bei den Statusketten
`dependency_graph`. Für Tests wird `get_test_context` verwendet; nach dem
Patch folgen scoped `get_violations` und `safeguard` sowie die direkten
Qualitätsabfragen.

Der relevante Symbolumfang ist:

- `IGiteaRepositoryTransport` und
  `ExternalSourceRepositoryTransportResult`;
- `ExternalSourceRepositoryAcquisitionResult` und der neue
  `ExternalSourceRepositorySourcePolicy`-Helper;
- `ExternalSourceRepositoryCacheRefresh` und
  `ExternalSourceRepositoryAcquirer`;
- `ExternalSourceProviderResult`,
  `ExternalSourceProviderFailureProjection` und
  `GiteaExternalSourceProvider`;
- `AssemblySourceSelectionOrchestrator` sowie
  `AssemblySourceSelectionScope`;
- `AssemblyAnalysisToolSupport` nur zur Bestätigung des bestehenden
  terminalen Config-Gates und der Diagnoseaggregation.

Der scoped Produktionsaudit auf `src/AiNetLinter/Mcp/Assemblies` ergab keine
Duplikat-Cluster und keinen hochkonfidenten Dead Code. Der Magic-Value-Audit
meldete 109 Treffer über den gesamten Assemblies-Bereich, überwiegend
einmalige bereits fachlich getrennte Konstanten; daraus wird kein globaler
Sweep. Innerhalb des Steps werden neue Status-/Diagnosewerte und wiederholte
Git-Status-/Cleanup-Entscheidungen zentralisiert, aber nur wenn sie direkt
dieser Source-Policy-Grenze dienen. `TD-001` bis `TD-003` bleiben wegen ihrer
separaten Architekturgrenze und `auto_fixable: nein` offen.

## Definition of Done für den Folge-Coder

- [ ] Vorabprüfung mit MCP, aktuelle Zeilen-/Dateigrenzen und fokussierte
      Testzuordnung sind reproduzierbar dokumentiert.
- [ ] Clean-/Dirty-/Unverified-Trust ist am besitzgeschützten Staging-
      Checkout fail-closed und ohne Secret-/Rohprozess-Leak abgesichert.
- [ ] `Verified`, `Degraded` und `Unavailable` sind immutable über
      Acquisition, Provider und Selection modelliert; `Degraded` trägt keinen
      neuen Snapshot.
- [ ] Stale-Refresh-Fehler bewahren den validierten Last-good-Commit nur als
      sichtbaren Nachweis, verändern `current` nicht und räumen den neuen
      Checkout sicher auf.
- [ ] Der statische Decompilation-Fallback sowie positive NoMatch-,
      Ambiguous-, ProviderUnavailable-, Capability- und ConfigurationFailure-
      Regressionen bleiben erhalten.
- [ ] Keine Host-/MCP-Health-, Retention-/GC-, Invalidation-, Transitiv- oder
      EPIC-05-Erweiterung ist in den Diff geraten.
- [ ] Build und beide Nicht-Stress-Gates sind mit exakten Zahlen und
      bekannten Skips dokumentiert; Stress wurde nicht ausgeführt.
- [ ] Coder-Commit und danach ein frischer, separater Kritiker gemäß
      Orchestrator-Ablauf; erledigte Agenten werden geschlossen.

## Regelreferenzen

- `.agents/rules/AiNetLinter-McpWorkflow.mdc` — projektgebundenes MCP vor
  ergänzender Textsuche, absolute Ziele, Symbol-/Impact-Grenzen und scoped
  Qualitätsnachweise.
- `.agents/rules/AiNetLinter.mdc` — C#-Zeilen-/Komplexitätsgrenzen,
  strukturierte Resultate, sichere Fehlerpfade und Vermeidung von
  DuplicateCode/MagicValues.
- `.agents/rules/AiNetLinterRichtlinien.mdc` — monolithische Architektur,
  Ownership-/Cleanup-/Testisolation, Result-Pattern, Dokumentationswahrheit,
  DRY-/MagicValues-/DeadCode-Disposition und Nicht-Stress-Gates.
- `.agents/Agent-Scaffolding/dev-loop/drift-loop/orchestrator.md` — genau
  ein frischer Coder und danach ein frischer Kritiker seriell, Statuspflege,
  kein Agent-Reuse und keine Audit-/Mini-Steps.

## Sicherer Handoff

Der nächste Übergabepunkt ist ein neuer Coder-Agent auf Branch `main`. Er
liest zuerst die zehn `read_first`-Dateien und führt die MCP-Abfragen auf den
aktuellen Symbolen aus. Danach öffnet er die beiden Testdateien und weitere
Testausschnitte nur bedarfsgebunden.

Die Implementierung beginnt an der Transport-/Checkout-Trust-Grenze und
arbeitet anschließend über Cache-Refresh und Provider bis zur Selection.
Dabei bleibt `ConfigurationFailure` unangetastet, `Degraded` erhält keinen
Source-Lease und der bestehende statische Fallback wird nicht zu einem
erfolgreichen externen Match umgedeutet. Der Coder führt weder Stress noch
globale Audits aus. Nach seinem Abschluss wird der frische Coder geschlossen
und für die Prüfung ein neuer, separater Kritiker gestartet.

Der Planer hat in diesem Schritt keine Produktionsänderung, keinen Testlauf
und keine Coder-/Kritikerarbeit ausgeführt.
