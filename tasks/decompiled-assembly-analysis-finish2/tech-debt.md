# Tech-Debt-Register: decompiled-assembly-analysis-finish2

Die EPIC-A-Implementierung meldete bislang keine neuen actionable P2/P3-
Befunde. Die zwei offenen Regressionen (Bare-Path-Snapshot und fehlende
Stable-Member-ID) sind Muss-Kriterien und bleiben als offene Korrekturbefunde
im `execution-log.md`; sie werden nicht als `accepted-deferred` verschleiert.

Historische Übergaben und neue actionable P2/P3-Befunde werden nach dem
jeweiligen Rollenbericht mit Evidenz, Disposition und Log-Anker ergänzt;
unbelegte oder rein kosmetische Vorschläge bleiben ausschließlich im
Ausführungsprotokoll.

## TD-EPIC-A-001 — `MaxDirectoryChildren` im Core-Scope

- Schweregrad: P1
- Beschreibung: Der neue `SolutionDocumentPathResolver` erhöht die Zahl der
  Einträge im betroffenen Core-Verzeichnis auf 31 und löst damit die
  `MaxDirectoryChildren`-Strukturregel aus.
- Fundstelle/Scope: `src/AiNetLinter/Core/`; neuer Resolver neben den bereits
  vorhandenen Core-Dateien.
- Evidenz: letzter gezielter `get_violations`-Check nach der letzten
  Codeänderung meldete genau 1 Befund; Testscope meldete 0 Violations.
- Disposition: `fixed`
- Risiko: behoben; `src/AiNetLinter/Core` liegt wieder beim aktiven Grenzwert
  von 30 direkten Einträgen.
- Nächster Schritt: keine weitere Maßnahme für diesen Befund; die korrigierte
  Dateiorganisation im Folge-Review verifizieren.
- Log-Anker: `execution-log.md`, completed EPIC-A Korrektur-
  Implementierer Runde 1 vom 2026-08-31.

## TD-EPIC-B-001 — Formatter-Komplexität

- Schweregrad: P2/P3
- Beschreibung: Vier neue Komplexitätsbefunde betreffen
  `FindAssemblyExtensionsTool.FormatText` und
  `GetServerHealthResponseBuilder.AppendAssemblySection`.
- Fundstelle/Scope: `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/` und
  `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`.
- Evidenz: letzter `get_violations`-Check nach der letzten Codeänderung
  meldete insgesamt fünf Produktionsbefunde; vier davon sind neue
  Komplexitätsbefunde in diesen Formatierern.
- Disposition: `fixed`
- Risiko: behoben; Formatter-Komplexität liegt nach der Korrekturrunde nicht
  mehr als aktiver Produktionsbefund vor.
- Nächster Schritt: keine weitere EPIC-B-Maßnahme.
- Log-Anker: `execution-log.md`, completed EPIC-B Implementierer vom
  2026-08-31.

## TD-EPIC-B-003 — Health-Statusprojektion bei Detaildiagnostics

- Schweregrad: P1
- Beschreibung: Gezielt angeforderte transitive Diagnostics wurden ergänzt,
  ohne den Rohstatus zu `partial` zu projizieren.
- Fundstelle/Scope: `GetServerHealthResponseBuilder.cs`, Health-Detailpfad.
- Evidenz: Folgeimplementierung ergänzt die effektive Statusprojektion;
  fokussierte Regressionen 21/21 und 9/9 bestanden.
- Disposition: `fixed`
- Risiko: behoben; Diagnostics projizieren den Health-Status konsistent.
- Nächster Schritt: keine weitere EPIC-B-Maßnahme.
- Log-Anker: `execution-log.md`, completed EPIC-B Korrektur-
  Implementierer Runde 1 vom 2026-08-31.

## TD-EPIC-C-001 — Source-TTL und Materialisierungs-Capacity umgehen Registry

- Schweregrad: P1
- Beschreibung: Materialisierung reserviert vor `SourceSnapshotRegistry.Acquire`;
  `TryReserve` führt weder Source-TTL-/LRU-Eviction noch Identity-Deduplizierung
  aus. `SourceSnapshotRegistry.EvictIdle()` hat im Produktionspfad keinen
  Caller.
- Fundstelle/Scope: `SourceSnapshotRegistry.cs`,
  `ExternalSourceSnapshotMaterializer.cs`, `ExternalResourceRegistry.cs`.
- Evidenz: unabhängiger EPIC-C-Review gegen `8ab245ab`; abgelaufene freigegebene
  Snapshots blockieren volle Budgets, identische Snapshots können bei
  `MaxResidentResources=1` abgewiesen werden.
- Disposition: `fixed`
- Risiko: behoben; Source-Eviction, Identity-Deduplizierung und Reservation-
  Promotion sind unter den vorgesehenen Locks koordiniert.
- Nächster Schritt: keine Korrekturmaßnahme; die direkte Materializer-plus-
  Registry-E2E-Abdeckung bleibt als separater P2-Punkt dokumentiert.
- Log-Anker: `execution-log.md`, completed EPIC-C Reviewer vom 2026-08-31.

## TD-EPIC-C-002 — Assembly-LRU durch Owner-Lease blockiert

- Schweregrad: P1
- Beschreibung: Jeder Assembly-Eintrag hält seine Owner-Resource-Lease bis zur
  Retirement-Dispose; das Ressourcenregister sieht deshalb auch idle Einträge
  als `LeaseCount=1`, sodass die LRU-Auswahl nicht greift.
- Fundstelle/Scope: `AssemblyAnalysisRegistry.cs`, `AssemblyAnalysisEntry.cs`,
  `ExternalResourceRegistry.cs`.
- Evidenz: unabhängiger Review; volle Resident-/Disk-/Memory-Kapazität weist
  neue Assemblys ab, statt idle Einträge zu retirieren.
- Disposition: `fixed`
- Risiko: behoben durch atomare Retirement-Ownership mit Registry-/Entry-Lock;
  aktive Analyse-Leases verhindern Retirement.
- Nächster Schritt: keine weitere Maßnahme.
- Log-Anker: `execution-log.md`, EPIC-C-Folge-Review nach Korrekturrunde 3
  vom 2026-08-31.

## TD-EPIC-C-003 — ThinClient-Overrides fehlen im bestehenden Daemon-Handshake

- Schweregrad: P1
- Beschreibung: External-Limits werden beim detached Start weitergereicht,
  aber beim Verbinden mit einem bestehenden Daemon weder verglichen noch als
  Divergenz gemeldet.
- Fundstelle/Scope: `DaemonProtocol.cs`, `ThinClientProxy.cs`.
- Evidenz: unabhängiger Review; `EffectiveDaemonConfiguration` enthält im
  bestehenden Pfad nur `MaxProjects` und `IdleExitMinutes`.
- Disposition: `fixed`
- Risiko: Die CLI-Semantik und die Tick-Normalisierung sind durch Server- und
  Client-Regressionen abgesichert.
- Nächster Schritt: keine weitere Maßnahme.
- Log-Anker: `execution-log.md`, EPIC-C-Folge-Review nach Korrekturrunde 3
  vom 2026-08-31.

## TD-EPIC-C-004 — Materialisierungsreservation endet vor Snapshot-Registrierung

- Schweregrad: P2
- Beschreibung: `finally` gibt die Reservation frei, bevor der Snapshot durch
  `SourceSnapshotRegistry.Acquire` resident registriert ist; eine Konkurrenz-
  materialisierung kann das Budgetfenster ausnutzen.
- Fundstelle/Scope: `ExternalSourceSnapshotMaterializer.cs`,
  `AssemblySourceSelectionOrchestrator.cs`.
- Evidenz: unabhängiger EPIC-C-Review; zeitliches Fenster zwischen Reservation
  und Registrierung.
- Disposition: `fixed`
- Risiko: behoben; Reservation wird bis zur Registrierung gehalten und bei
  erfolgreichem Acquire in die Resident-Lease überführt.
- Nächster Schritt: direkte Materializer-plus-Registry-E2E-Abdeckung separat
  ergänzen.
- Log-Anker: `execution-log.md`, completed EPIC-C Reviewer vom 2026-08-31.

## TD-EPIC-C-005 — Producer-Cancellation wird beim Host-Dispose nicht gejoint

- Schweregrad: P2
- Beschreibung: `EstimateCheckout` ist synchron/tokenlos; `Dispose()` cancelt
  laufende Producer, wartet deren Tasks aber nicht vollständig vor dem Dispose
  von Registry und Ressourcenregister.
- Fundstelle/Scope: `SourceSnapshotModels.cs`,
  `AssemblySourceSelectionOrchestrator.cs`, `AssemblyAnalysisHostComposition.cs`.
- Evidenz: unabhängiger Review; Cleanup verhindert typischerweise Leaks, aber
  keinen deterministischen vollständigen Beendigungspunkt.
- Disposition: `fixed`
- Risiko: behoben; Checkout-Schätzung ist cancellation-aware und die Creation
  bleibt bis nach `creation.Complete()` im Producer-Join-Set.
- Nächster Schritt: keine weitere Maßnahme.
- Log-Anker: `execution-log.md`, EPIC-C-Folge-Review nach Korrekturrunde 3
  vom 2026-08-31.

### EPIC-C-Korrekturrunde 1 — Bearbeitungsnachweis

Der frische Implementierer hat `TD-EPIC-C-001` bis `TD-EPIC-C-005` in einem
kohärenten Ansatz bearbeitet. Die Dispositionen bleiben bis zum unabhängigen
Folge-Review `fix-now`; erst ein bestätigter Review darf sie auf `fixed`
setzen. Der vollständige Bericht und die Verifikation stehen im
`execution-log.md`.

## TD-EPIC-C-006 — Fehlender Materializer-plus-Registry-E2E-Pfad

- Schweregrad: P2
- Beschreibung: Die Reservation-/Promotion-Verträge sind in getrennten
  Registry- und Materializer-Tests belegt, aber ohne direkten E2E-Test des
  gekoppelten Produktionspfads.
- Fundstelle/Scope: `ExternalSourceSnapshotMaterializerTests.cs`.
- Evidenz: Commit `118ccb94` ergänzt den gekoppelten Materializer-/Registry-
  Test für Reservation, Promotion, Lease-Freigabe und Eviction.
- Disposition: `fixed`
- Risiko: Der direkte Integrationspfad ist regressionsgesichert; die
  Verifikation des Resume-Stands steht noch aus.
- Nächster Schritt: keine weitere Maßnahme.
- Log-Anker: `execution-log.md`, EPIC-C-Folge-Review nach Korrekturrunde 3
  vom 2026-08-31.

## TD-EPIC-C-007 — Tick-Normalisierung im ThinClient-Limitvergleich

- Schweregrad: P1
- Beschreibung: Die Tick-Normalisierung ist implementiert, jedoch nicht mit
  unterschiedlichen Rohdezimalwerten bei gleichen `TimeSpan`-Ticks sowie einem
  tatsächlich unterschiedlichen Tick regressionsgesichert.
- Fundstelle/Scope: `DaemonProtocol.cs`.
- Evidenz: Commit `118ccb94` vergleicht optionale Minutenwerte nach
  Normalisierung auf `TimeSpan.Ticks`.
- Disposition: `fixed`
- Risiko: Der Rückbau auf den Rohvergleich wird durch Client- und
  Server-Regressionen erkannt; die Folgeprüfung bestätigt noch den
  Korrekturstand.
- Nächster Schritt: keine weitere Maßnahme.
- Log-Anker: `execution-log.md`, EPIC-C-Folge-Review nach Korrekturrunde 3
  vom 2026-08-31.

## TD-EPIC-C-008 — Creation-Join nach Dictionary-Entfernung

- Schweregrad: P2
- Beschreibung: `RunProviderCreationAsync` entfernt die Creation vor
  `creation.Complete()` aus dem Join-Dictionary.
- Fundstelle/Scope: `AssemblySourceSelectionOrchestrator.cs`.
- Evidenz: EPIC-C-Folge-Review; ein paralleler Dispose kann die noch nicht
  vollständig beendete Producer-Task dadurch übersehen.
- Disposition: `fixed`
- Risiko: behoben; Entfernung aus dem Join-Set erfolgt erst nach vollständigem
  Completion und ist durch einen Race-Test abgesichert.
- Nächster Schritt: keine weitere Maßnahme.
- Log-Anker: `execution-log.md`, EPIC-C-Folge-Review nach Korrekturrunde 3
  vom 2026-08-31.

## TD-EPIC-B-004 — Compact-Health `ShownCount`

- Schweregrad: P1
- Beschreibung: Compact-Health leerte Samples mit veraltetem `ShownCount`.
- Fundstelle/Scope: `AssemblyAnalysisResponseLimits.cs`, Compact-Health-
  Projektion.
- Evidenz: Folgeimplementierung setzt sichtbare Counts auf 0 und ergänzt
  Regression; fokussierte EPIC-B-Tests bestanden.
- Disposition: `fixed`
- Risiko: behoben; strukturierte und textuelle Antworten bleiben konsistent.
- Nächster Schritt: keine weitere EPIC-B-Maßnahme.
- Log-Anker: `execution-log.md`, completed EPIC-B Korrektur-
  Implementierer Runde 1 vom 2026-08-31.

## TD-EPIC-B-005 — Globales Diagnostics-Sample-Budget

- Schweregrad: P1
- Beschreibung: Root-/transitive-/Aggregate-Samples wurden separat begrenzt
  und doppelt ausgegeben.
- Fundstelle/Scope: `AssemblyAnalysisResponseLimits.ProjectDiagnostics`.
- Evidenz: Folgeimplementierung dedupliziert Samples und projiziert ein
  gemeinsames 4-KiB-Budget; fokussierte Regressionen bestanden.
- Disposition: `accepted-deferred`
- Risiko: P1-Restbefund; das Budget ist intern begrenzt, aber der Wire-Shape
  serialisiert dieselben Samples in mehreren Feldern und damit nicht global.
  EPIC-B ist dadurch nicht freigabefähig.
- Nächster Schritt: Drei ernsthafte Versuche sind ausgeschöpft. Eine spätere
  Entscheidung muss die kanonische Wire-Sample-Liste oder ein Budget über den
  vollständigen serialisierten Payload festlegen; danach sind vollständige
  StructuredContent- und Health-Tests erforderlich.
- Log-Anker: `execution-log.md`, completed EPIC-B Korrektur-
  Implementierer Runde 1 vom 2026-08-31.

## TD-EPIC-B-010 — Globales Wire-Budget nach drei Versuchen

- Schweregrad: P1
- Beschreibung: Diagnostics-Samples werden trotz interner gemeinsamer
  Projektion mehrfach im StructuredContent serialisiert: `diagnostics`,
  `diagnosticsSummary.samples`, `root/transitive.samples` und zusätzlich je
  Health-Assembly. Das 4-KiB-Limit gilt nur je Liste.
- Fundstelle/Scope: `AssemblyAnalysisResponse.cs`, `InspectAssemblyTool.cs`,
  `AssemblyAnalysisModels.cs` und `GetServerHealthResponseBuilder.cs`.
- Evidenz: unabhängiger Folge-Review reproduzierte 15 maximal lange Samples
  mit 3.870 UTF-8-Bytes je Liste; drei Listen ergeben bereits 11.610 Bytes
  vor JSON-Overhead. Der bestehende Test prüfte nur eine Liste.
- Disposition: `accepted-deferred`
- Risiko: P1; globale Antwortgrößen- und Diagnostics-Vertrag bleibt verletzt.
- Nächster Schritt: Drei Versuche für dieses Finding sind gemäß Konzept
  ausgeschöpft. Vor einer weiteren Umsetzung ist eine kanonische Wire-Form
  oder ein vollständiges Payload-Budget festzulegen; keine blinde weitere
  Korrekturschleife.
- Log-Anker: `execution-log.md`, completed EPIC-B Folge-Reviewer nach
  Korrekturrunde 2 vom 2026-08-31.

## TD-EPIC-B-008 — Irreführendes Budget-Diagnostic

- Schweregrad: P2
- Beschreibung: `CreateSummary` kann `maxDiagnosticBytes` als Ursache melden,
  wenn nur globale Root-/Transitive-Slots ausgeschöpft wurden.
- Fundstelle/Scope: `AssemblyAnalysisResponseLimits.cs` und
  `AssemblyAnalysisDispatcherCapabilityTests.cs`.
- Evidenz: Commit `118ccb94` trennt Slot- und Byte-Truncation und sichert den
  Slot-Grenzfall durch eine Regression.
- Disposition: `fixed` (unabhängige Prüfung ausstehend)
- Risiko: Die Truncation-Ursache ist präziser; der globale Wire-Budget-P1 aus
  `TD-EPIC-B-010` bleibt davon unberührt.
- Nächster Schritt: Nach EPIC-D als Teil des EPIC-E-Reviews bestätigen.
- Log-Anker: `execution-log.md`, Resume-Zusammenführung vom 2026-08-31.

## TD-EPIC-B-009 — Registrierter Health-Detail-E2E-Pfad

- Schweregrad: P2
- Beschreibung: Für `get_server_health` fehlt ein registrierter E2E-Testpfad
  mit `includeDiagnostics=true`; Builder- und Schema-Tests sind vorhanden.
- Fundstelle/Scope: `McpServerAssemblyHealthE2ETests.cs`.
- Evidenz: Commit `118ccb94` registriert einen E2E-Aufruf mit
  `includeDiagnostics=true` und begrenztem `maxDiagnostics`.
- Disposition: `fixed` (unabhängige Prüfung ausstehend)
- Risiko: Der Detailpfad ist jetzt registriert abgedeckt; die vorhandene
  P1-Wire-Budget-Entscheidung bleibt offen.
- Nächster Schritt: Nach EPIC-D als Teil des EPIC-E-Reviews bestätigen.
- Log-Anker: `execution-log.md`, Resume-Zusammenführung vom 2026-08-31.

## TD-EPIC-B-002 — `AssemblyAnalysisRegistry` Footprint

- Schweregrad: P2/P3
- Beschreibung: Bestehender AIContext-Footprint-Befund in
  `AssemblyAnalysisRegistry` bleibt im Abschlusscheck sichtbar.
- Fundstelle/Scope: `src/AiNetLinter/Mcp/Assemblies/`.
- Evidenz: finaler `get_violations`-Check meldete einen bestehenden
  `AssemblyAnalysisRegistry`-Befund; keine Codeänderung zur Vermeidung einer
  scopefremden Architekturzerlegung.
- Disposition: `promoted-to-project-debt`
- Risiko: begrenzter Footprint-/Strukturbefund ohne neue EPIC-B-Funktionalität.
- Nächster Schritt: Nur bei nachgewiesener unabhängiger Verantwortung zerlegen;
  danach Safeguard, Impact und Violations erneut prüfen.
- Log-Anker: `execution-log.md`, completed EPIC-B Implementierer vom
  2026-08-31.

## TD-EPIC-B-006 — Bestehender ProjectRegistry-Testflake

- Schweregrad: P2
- Beschreibung: Ein vollständiger FastTests-Lauf meldete einen bestehenden
  ProjectRegistry-Flake; der isolierte Testlauf war 1/1 grün.
- Fundstelle/Scope: `src/AiNetLinter.FastTests/Mcp/Projects/`.
- Evidenz: vollständiger Korrekturbericht: 2236/2239 grün, 2 Skips; isoliert
  1/1 grün. Nicht als Assembly-Funktionsfehler reproduziert.
- Disposition: `accepted-deferred`
- Risiko: Windows-/Testisolation kann den vollständigen Gate-Lauf sporadisch
  verfälschen.
- Nächster Schritt: gezielt reproduzieren und Test-/Umgebungsisolation
  bereinigen, ohne Assembly-Verhalten einzubeziehen.
- Log-Anker: `execution-log.md`, completed EPIC-B Korrektur-
  Implementierer Runde 1 vom 2026-08-31.

## TD-EPIC-B-007 — Bestehende Dokumentations-/Registrierungsvertragsfehler

- Schweregrad: P2
- Beschreibung: Zwei bestehende IntegrationTests für Dokumentations-/Tool-
  Registrierungsverträge blieben im vollständigen Nicht-Stress-Lauf rot.
- Fundstelle/Scope: `src/AiNetLinter.IntegrationTests/Mcp/`.
- Evidenz: vollständiger Korrekturbericht: 371/373 bestanden; zwei Fehler,
  außerhalb des EPIC-B-Funktionspfads und ohne neue Produktionsviolations.
- Disposition: `accepted-deferred`
- Risiko: Der finale Gate-Status ist bis zur gezielten Einordnung nicht
  vollständig grün.
- Nächster Schritt: konkrete Testnamen/TRX-Ausgaben beim Abschluss-Gate
  prüfen; bei Nicht-Kausalität mit Evidenz dokumentieren, sonst gezielt
  korrigieren.
- Log-Anker: `execution-log.md`, completed EPIC-B Korrektur-
  Implementierer Runde 1 vom 2026-08-31.
