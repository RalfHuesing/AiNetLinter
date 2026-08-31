---
status: ready
---

# Konzept: Findings aus dem Audit zur Analyse dekompilierter Assemblies beheben

## 1. Ziel und Nutzen

Alle im Audit gefundenen und im aktuellen Code noch relevanten Befunde zur
Analyse dekompilierter Assemblies werden in einem zusammenhängenden Paket
behoben. Die Reihenfolge ist verbindlich:

1. korrekte und fehlertolerante Funktion,
2. verlässliche, für AI-Agenten gut interpretierbare Antworten,
3. Ressourcen-, Wartbarkeits- und Strukturqualität.

Danach sollen insbesondere `find_symbol`, `find_references`, `get_call_tree`,
`inspect_assembly`, `find_assembly_extensions` und `get_server_health` ihre
Grenzen, Herkunft, Vollständigkeit, Trunkierung und Fehler so ausweisen, dass
ein Agent ohne implizite Annahmen sinnvoll weiterarbeiten kann. Externe
Assemblies bleiben metadata-only: Es gibt weder Runtime-Laden noch Ausführung.

## 2. Geltungsbereich und betroffene Bereiche

Der Arbeitsumfang liegt primär in diesen Komponenten:

- `src/AiNetLinter/Mcp/AnalysisToolCall.cs` und den Assembly-Navigationstools:
  Dispatch, Referenzexpansion, Positionsauflösung und Batch-Summaries.
- `src/AiNetLinter/Configuration/ExternalSourceMappingValidator.cs` sowie
  `src/AiNetLinter/Mcp/Assemblies/ExternalSource/Repository/`:
  URL-Vertrag, Credentials, Checkout-Ownership, Snapshot-Materialisierung und
  persistenter Repository-Cache.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/` und
  `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/`:
  Session-Freshness, Cache-Generationen, Registry und Diagnoseprojektion.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/`:
  Health-Payload und dessen AIContextFootprint.
- Zugeordnete Fast- und Integrationstests sowie die betroffenen API-,
  Konfigurations- und Begründungsdokumente.

Nicht bestätigt bzw. nicht Teil dieses Umfangs sind die im Audit ebenfalls
geprüften, aber nicht reproduzierten Git-Transport-, Dead-Code-, Clone- und
Magic-Value-Kandidaten sowie der verworfene Root-Routing-Probe.

## 3. Vollständige Finding-Matrix

| ID | Priorität | Aktueller Befund | Zielzustand und vorgesehene Richtung |
| --- | --- | --- | --- |
| `ASM-001` | P0 Funktion | `AssemblyAnalysisDispatcher.ExecuteAsync` expandiert Referenzen vor jedem Assembly-Handler; dadurch werden bei `includeReferences=false` trotz dokumentiertem Root-only-Default Child-Leases und Referenzdiagnosen erzeugt. | Referenzexpansion wird eine explizite Fähigkeit des jeweiligen Routes/Handlers. Root-only überspringt Expansion vollständig; `includeReferences=true` behält die bestehenden harten Grenzen und sichtbare Navigationsdiagnosen. Regressionstests prüfen beide Modi und alle betroffenen Assembly-Tools. |
| `CHK-001` | P0 Funktion | Nach erfolgreichem externem Checkout kann die Cancellation-Prüfung vor dem lokalen Ownership-Binding abbrechen. Der zurückgegebene Handle liegt dann nicht in der lokalen Variable und wird nicht freigegeben; Checkout/Ownership-Marker können zurückbleiben. | Der Rückgabewert des Acquirers wird unmittelbar in eine lokale Ownership-Struktur überführt oder bei Abbruch an dieser Grenze explizit verworfen. Cancellation bleibt für den Aufrufer eine Cancellation, hinterlässt aber keinen Checkout und keinen Ownership-Marker. Ein deterministischer Test-Double deckt genau diese Boundary ab. |
| `EXTSRC-01` | P0/P1 Vertrag | Loader-Validierung akzeptiert absolute HTTP(S)-URLs mit Userinfo, Query oder Fragment, während die Laufzeit-Policy sie ablehnt. | Eine gemeinsame, zentrale Normalisierung/Policy wird von Konfiguration und Laufzeit verwendet. Erlaubt bleiben nur die explizit unterstützten absoluten HTTP(S)-Repository-URLs ohne Credentials in der URL und ohne Query/Fragment; Normalisierung und Fehlermeldung bleiben deterministisch. |
| `MCP-L6-001` | P0 Funktion | `Datei:Zeile:Spalte` akzeptiert Spalte `0`, negative Werte sowie zu große Werte; `FindToken` kann danach `ArgumentOutOfRangeException` auslösen und den Tool-Call fälschlich als `WORKSPACE_DIAGNOSTIC`/`isError=true` melden. | Zeile und Spalte werden gegen `SourceText` validiert, bevor Roslyn angesprochen wird. Ungültige Positionen liefern den recoverable Fehlercode `INVALID_ARGUMENT`, `isError=false`, mit Format-/Bereichshinweis; Workspace- oder Roslyn-Fehler bleiben davon getrennt. Tests decken 0, negative, überlange und gültige Grenzwerte ab. |
| `ASM-002` | P0 Funktion/Agentennutzen | Erwartete Nicht-Treffer einzelner Referenz-Sessions werden als globale Diagnostics gesammelt. Ein Symbol, das nur im Root gefunden wird, kann dadurch fälschlich als `partial` erscheinen. | Erwartete Session-Nicht-Treffer werden intern von echten Lade-, Expansions-, Session- und Trunkierungsdiagnosen unterschieden und nicht als globale Unvollständigkeit gezählt. Ein echter Fehler bleibt sichtbar. Ein Multi-Session-Test prüft Root-Treffer plus erwartete Nicht-Treffer und den Gegenfall ohne Treffer. |
| `ASM-003` | P0 Agentennutzen | `AssemblyFindSymbolTool` überschreibt bei mehreren Patterns die Navigation des vorherigen Patterns; Trunkierung oder Diagnostics des ersten Patterns gehen verloren. | Die Batch-Antwort aggregiert Navigation und Diagnostics aller Patterns deterministisch, ohne frühere Trunkierungsinformationen zu verlieren. Die bestehende Structured-Response-Form bleibt kompatibel; mindestens `partial`/Truncated und die zugehörige Diagnose des ersten begrenzten Patterns müssen im Gesamtergebnis sichtbar bleiben. |
| `EXTSRC-03` | P1 Funktion/Diagnose | Ein live gemappter Checkout wird zwar gefunden, aber source-backed Assembly-Antworten können weiterhin auf decompiled/`sourcePath=none`/`snapshot=none` fallen. Die Materialisierung verwirft unerwartete Workspace-Ursachen zu stark und gibt teils nur eine generische Diagnose aus. | Der source-backed Pfad wird mit einer reproduzierbaren, gültig restaurierten Fixture end-to-end abgesichert. Die Materialisierung surfacet eine sichere, nicht geheime Ursache (z. B. fehlende/veraltete Assets, keine Projekte, ungültige Solution, Workspace-Diagnostic) und entscheidet nachvollziehbar zwischen source-backed Ergebnis und decompiled Fallback. Kein stiller Statuswechsel; ein Checkout muss vor der Materialisierung restauriert sein. |
| `EXTSRC-02` | P1 Sicherheit/Vertrag | Der produktive MCP-/Daemon-Entry verdrahtet keinen Credential Resolver. Geschützte Remotes scheitern dadurch nicht früh und eindeutig; Credentials dürfen keinesfalls in URL, Argumenten, Logs oder Diagnostics landen. | Der Authentifizierungsvertrag wird explizit und fail-closed umgesetzt. Dieses Paket unterstützt nur öffentliche Remotes; geschützte Remotes werden früh als recoverable, nicht unterstützter/authentifizierungsbedürftiger Zugriff gemeldet. Es gibt keinen Credential Resolver und keine Geheimnisse im MCP-Vertrag. |
| `F-05-01` | P1 Ressourcen | Erfolgreich veröffentlichte Generationen von Assembly-Decompilation- und External-Source-Caches werden nicht aufgeräumt; nur unveröffentlichte Generationen werden entfernt. | Nach erfolgreichem Pointer-Switch erfolgt eine sichere Retention-/Sweep-Phase. Aktuelle, noch geleaste bzw. innerhalb einer definierten Grace-Zeit benötigte Generationen bleiben erhalten; alte Generationen werden deterministisch begrenzt. Pointer-, Lease-, Race-, Reparse-Point- und Pfadschutz bleiben wirksam. Wiederholte erfolgreiche Refreshes werden als Integration/Komponententest geprüft. |
| `F-05-02` | P1 Ressourcen | Die statische `ConcurrentDictionary` der Cache-Key-Locks wächst monoton; ein Lease gibt nur das Semaphore frei, entfernt den unbenutzten Key aber nicht. | Ein ref-counted, race-sicherer Key-Lock-Halter entfernt und disposiert sich nach dem letzten Waiter/Publisher per Compare-and-remove. Ein neuer Waiter darf nicht zwischen Remove und Lock-Übernahme verloren gehen. Multi-Key-, Parallelitäts- und Reclamation-Tests beweisen die Lebensdauer. |
| `F-05-03` | P1 Freshness | Resident Assembly-Reuse vergleicht nur den Root-SHA. Änderungen an Source-Mapping, Snapshot oder Abhängigkeiten können bei unveränderten Root-Bytes übersehen werden. | Die Reuse-Identität umfasst mindestens Root-Inhalt und die für die Analyse maßgebliche Source-/Referenzidentität. Änderungen an Snapshot, Source-Projekt oder aufgelöster Dependency-Fingerprint invalidieren deterministisch; unveränderte Identität reused weiter. Die zusätzliche Fingerprint-Arbeit ist Teil des beschlossenen Freshness-Vertrags. |
| `MCP-WIRE-001` | P1 Agentennutzen | Das 4-KiB-Limit wird auf Diagnose-Samples angewendet, aber dieselben Samples können mehrfach in StructuredContent, Summary, Referenzprojektionen und Text erscheinen. Die vollständige serialisierte MCP-Payload wird nicht gemessen. | Ein maximaler JSON-Fixture-Test misst die komplette `structuredContent`-Payload. Der dokumentierte 4-KiB-Wert gilt als harte globale Grenze der serialisierten Assembly-StructuredContent-Payload; eine benannte Budgetierung und Deduplizierung verhindert Überschreitung. Text und StructuredContent behalten dieselbe inhaltliche Diagnoseauswahl. |
| `UX-001` | P2 Wartbarkeit/Agentennutzen | `AssemblyAnalysisRegistry` überschreitet als zusammenhängender Typ das konfigurierte Struktur-/Kontextziel; Ownership, Generation, Source-Project-Leases, Retirement und Disposal liegen eng gekoppelt. | Nach den P0/P1-Verhaltensfixes wird die Registry scope-nah in kleine, klar verantwortliche Kollaboratoren/Fassaden zerlegt. Public-/Internal-Verträge, Lock-Reihenfolgen, Generationen und Lease-Ownership bleiben unverändert. Der Refactor darf keine zusätzliche Partial-Datei-Drift oder versteckte Zuständigkeit erzeugen. |
| `MCP-L6-002` | P2 Wartbarkeit | `GetServerHealthResponseBuilder` liegt knapp über dem AIContextFootprint-Limit und zieht mehrere große Domänenabhängigkeiten direkt ein. | Health-Projektion, Diagnoseprojektion und Markdown-Formatierung werden über schmale, stabile Datenprojektionen getrennt. Der Wire-Vertrag bleibt unverändert; bestehende Health-Tests und die Metrik-/Violation-Prüfung müssen nach dem Refactor grün sein. |

## 4. Architektur- und Vertragsentscheidungen

- **Metadata-only und keine versteckten Seiteneffekte:** Die Assembly wird nie
  geladen oder ausgeführt. Ein impliziter Restore oder ein beliebiger Prozess-
  bzw. Netzwerk-Seiteneffekt wird nicht eingeführt. Die bestehende
  Restore-Erkennung soll stattdessen eine konkrete, sichere Diagnose liefern.
- **Explizite Assembly-Fähigkeiten:** Der Dispatcher entscheidet nicht mehr
  pauschal über Referenzexpansion. Ein Handler fordert Expansion an, wenn sein
  Vertrag sie benötigt; `includeReferences=false` bleibt Root-only und erzeugt
  keine Child-Leases.
- **Fail-closed bei externen Quellen:** Ungültige oder nicht unterstützte
  URL-/Auth-Zustände werden früh, recoverable und ohne Geheimnisse gemeldet.
  Dieses Paket verarbeitet nur öffentliche Remotes; ein Credential Resolver ist
  ausdrücklich nicht Bestandteil des Scopes. URLs, Exceptions, Logs und
  MCP-Diagnostics werden auf mögliche Secrets geprüft und redigiert.
- **Kein impliziter Restore:** Ein externer Checkout muss vor der
  Source-Materialisierung restauriert sein. Fehlen oder veralten die
  Restore-Artefakte, liefert der Pfad eine konkrete Diagnose und darf nicht
  eigenmächtig Netzwerk, Restore-Prozess oder zusätzliche Seiteneffekte starten.
- **Leases besitzen Ressourcen:** Ein erfolgreich übergebener Checkout-Handle
  hat ab der Acquirer-Rückgabe genau einen Owner. Cancellation und jede
  Materialisierungsfehlerbahn müssen diesen Owner eindeutig freigeben.
- **Cache-Sicherheit vor Aufräumquote:** Kein Cleanup löscht die aktuelle,
  geleaste oder noch geschützte Generation. Löschen bleibt auf sichere
  Generation-Verzeichnisse unter dem berechneten Cache-Root beschränkt; Reparse-
  Points, Pointer-Races und Pfadwechsel führen zum Überspringen mit Diagnose,
  nicht zum Löschen außerhalb des Roots.
- **Recoverable versus fatal:** Nutzerfehler, erwartete Nicht-Treffer,
  fehlende optionale Referenzen und geschützte/nicht materialisierbare externe
  Quellen bleiben `isError=false`, sofern der Serverprozess weiterarbeiten
  kann. Interne Workspace-/Invariant-Verletzungen behalten ihre bestehenden
  Fehlercodes und `isError=true`.
- **Kompatibilität:** Bestehende Tool-Namen, Target-Verträge,
  Standardlimits und die additive StructuredContent-Strategie bleiben
  erhalten. Neue Diagnosen werden nur dort ergänzt, wo sie die Ursache
  nachvollziehbar machen und keine Secrets enthalten.

## 5. Muss-Kriterien

- Kein Assembly-Tool erzeugt bei `includeReferences=false` Referenz-Sessions
  oder transitive Referenzdiagnosen.
- Jede erfolgreich akquirierte externe Checkout-Ressource wird auch bei
  Cancellation, Materialisierungsfehlern und Fallback exakt einmal freigegeben.
- Loader und Runtime akzeptieren und normalisieren denselben URL-Vertrag.
- Ungültige Positionsangaben führen reproduzierbar zu recoverable
  `INVALID_ARGUMENT` statt zu einem Workspace-Diagnostic.
- Echte Unvollständigkeit bleibt sichtbar; erwartete Nicht-Treffer und reine
  Leermengen werden nicht als `partial` missklassifiziert.
- Batch-Navigation verliert keine Diagnostics oder Trunkierungsgründe.
- Source-backed Erfolg, decompiled Fallback und Materialisierungsfehler sind
  anhand von Origin, Snapshot, Source-Pfad, Status und Diagnose unterscheidbar.
- Persistente Caches behalten nur sicher benötigte Generationen und die
  Lock-Tabelle kann unbenutzte Keys wieder freigeben.
- Änderungen an Source-/Dependency-Identität werden nicht durch Root-only-Reuse
  verdeckt.
- Die vollständige dokumentierte MCP-Wire-Grenze ist durch einen
  Serialisierungsregressionstest belegt.
- Nach den Refactorings liegen die betroffenen Typen innerhalb der geltenden
  Struktur-/Kontextregeln oder es gibt eine dokumentierte, messbare Ausnahme.

## 6. Akzeptanz- und Verifikationskriterien

Die Implementierung gilt erst als abnahmefähig, wenn alle folgenden Gruppen
erfüllt sind:

### Funktion und Sicherheit

- FastTests für Dispatcher-Fähigkeiten, Positionsgrenzen, Resolver-Diagnosen,
  Batch-Aggregation, URL-Konsistenz, Ownership-Cancellation, Freshness und
  Lock-Reclamation.
- IntegrationTests mit einer echten temporären Checkout-/Cache-Struktur für
  wiederholte erfolgreiche Generationen, Pointerwechsel, parallele Leases und
  sichere Cleanup-Grenzen.
- Eine source-backed IntegrationFixture mit gültiger Solution und bewusst
  kontrolliertem Restore-Zustand; zusätzlich Tests für fehlende/veraltete
  Restore-Artefakte und eine verständliche Diagnose.
- Tests stellen sicher, dass URLs/Exceptions/Diagnostics keine Credential- oder
  Token-Bestandteile ausgeben.

### Agentenvertrag

- StructuredContent und Markdown behalten konsistente Herkunfts-, Status-,
  Completeness- und Truncation-Aussagen.
- `find_symbol` über mehrere Patterns signalisiert jede relevante Begrenzung.
- Das maximale JSON-Fixture wird tatsächlich serialisiert und bytegenau gegen
  das beschlossene Budget geprüft.
- `get_server_health` enthält nach der Projektion weiterhin dieselben Felder
  und wird durch `get_violations`/Metrikprüfung ohne neue relevante
  Strukturverletzung bestätigt.

### Abschlusslauf

- `dotnet build`
- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress`
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress`
- gezielte MCP-Nachprüfung der betroffenen Symbole, Metriken und Violations.

Stress-Tests werden nicht automatisch als Abschlusskriterium ausgeführt. Bei
prozess-/Transportfehlern in IntegrationTests werden laufende Test-Daemons
bereinigt und der Lauf isoliert wiederholt; ein grüner FastTest-Lauf allein
reicht nicht als Abschluss.

## 7. Dokumentation und Synchronisation

Abhängig von den Entscheidungen und dem tatsächlich geänderten Vertrag werden
mindestens geprüft und bei Bedarf aktualisiert:

- `Docs/agent-api.md`: `includeReferences`, Completeness/Diagnostics,
  Positionsfehler, externe Source-/Auth-/Fallback-Semantik und Wire-Limits.
- `Docs/configuration.md`: gemeinsamer URL-Vertrag, geschützte Remotes und
  Restore-Voraussetzung, sofern sie Teil des produktiven Vertrags werden.
- `Docs/rationale.md`: nur falls die Restore-/Side-Effect-Entscheidung geändert
  wird.
- `README.md` und `Docs/ROADMAP.md`: nur bei sichtbarer Nutzer- oder
  Meilensteinänderung.
- `rules.json` und daraus generierte Agentenregeln: nur wenn Schwellenwerte oder
  Regelkonfiguration tatsächlich geändert werden; danach Agent-Rules-Sync.

## 8. Non-Goals und Betriebsmodell

- Keine automatische Ausführung unbekannter Assemblies, keine Reflection-
  Runtime-Route und kein AssemblyLoadContext.
- Kein allgemeiner Cache-Neubau und keine globale Registry-Neuarchitektur über
  die hier betroffenen Ownership-, Freshness- und Größenprobleme hinaus.
- Keine Aufweichung von Pfad-, Reparse-Point-, Ressourcen- oder
  Cancellation-Schutz zugunsten scheinbar besserer Verfügbarkeit.
- Keine Credentials in Konfigurationsdateien, Repository-URLs, CLI-Argumenten,
  Exceptions, Logs, Markdown oder StructuredContent.
- Keine pauschale Unterdrückung von Diagnostics, um `partial` oder Payload-
  Limits künstlich zu vermeiden.

Das Betriebsmodell behandelt lokale DLLs, lokale Solutions und explizit
gemappte externe Repository-Snapshots als untrusted input. Netzwerkzugriffe
und Checkout-Ressourcen sind budgetiert; externe Daten können fehlen, veraltet,
unvollständig oder nicht authentifiziert sein. Die Antwort muss dann sicher und
diagnostisch verwertbar bleiben, ohne den Serverprozess zu beschädigen.

## 9. Fehler-, Fallback- und Lebensdauermodell

- **Root-only:** Der Root-Snapshot bleibt nutzbar, wenn keine Referenzexpansion
  angefordert wurde. Nicht angeforderte Referenzen dürfen weder Fehler noch
  Statusverschlechterung verursachen.
- **Referenzexpansion:** Bei angeforderter Expansion werden harte Session-,
  Referenz- und Ergebnislimits eingehalten. Trunkierung wird als solche
  ausgewiesen; ein einzelner erwarteter Nicht-Treffer macht die Gesamtheit nicht
  automatisch `partial`.
- **Ungültige Eingabe:** Position, URL und andere Argumentfehler werden vor
  Workspace-/Roslyn-Aufrufen validiert und recoverable gemeldet.
- **Source-Auflösung:** Ein gültiger source-backed Snapshot wird bevorzugt. Bei
  nicht verfügbarer oder nicht vertrauenswürdiger Quelle bleibt der sichere
  decompiled Pfad möglich, aber Origin/Status/Diagnose müssen den Fallback
  erklären. Bei internem Materialisierungsfehler darf kein halbgebundener
  Checkout weiterleben.
- **Cancellation:** Der CancellationToken bricht den Aufruf ab. Bereits
  akquirierte Ressourcen werden in der Abbruchbahn synchron bzw. asynchron
  eindeutig an den Besitzer zurückgeführt.
- **Generationen:** Neue Generationen werden erst nach vollständiger Validierung
  und atomarem Pointer-Switch sichtbar. Cleanup läuft nachgelagert und darf
  aktive Nutzer alter Generationen nicht stören.
- **Resident Sessions:** Reuse wird nur bei gleicher vollständiger
  Analyseidentität erlaubt; diese umfasst Root-Inhalt, Source-Snapshot und die
  aufgelöste Dependency-Identität. Bei Identitätsänderung erfolgt ein sauberer
  Generation-/Lease-Wechsel.
- **Wire-Budget:** Die komplette serialisierte Assembly-StructuredContent-
  Payload bleibt einschließlich wiederholter Projektionen innerhalb des
  dokumentierten 4-KiB-Budgets. Deduplizierung und eine benannte globale
  Budgetprüfung sind dafür verbindlich.

## 10. Beschlossene Nutzerentscheidungen und spätere Detailentscheidungen

- **Geschützte externe Remotes:** Dieses Paket unterstützt nur öffentliche
  Remotes und bleibt bei geschützten Remotes fail-closed. Es wird kein
  Credential Resolver eingeführt; der Zugriff wird recoverable und ohne
  Geheimnisse diagnostiziert.
- **Restore-Verhalten:** Ein externer Checkout muss vor der
  Source-Materialisierung restauriert sein. Es gibt keinen versteckten Restore;
  fehlende oder veraltete Restore-Artefakte werden konkret diagnostiziert und
  führen zu einem sicheren Fallback, soweit möglich.
- **Freshness:** Source-Snapshot und aufgelöste Dependency-Fingerprints sind
  Teil der vollständigen Analyseidentität. Änderungen invalidieren Resident-
  Reuse deterministisch.
- **Wire-Limit:** Der dokumentierte 4-KiB-Wert gilt als harte globale Grenze
  für die komplette serialisierte Assembly-StructuredContent-Payload. Die
  Implementierung muss wiederholte Diagnose-Samples deduplizieren oder vor der
  Ausgabe budgetgerecht kürzen.

Die genaue Wahl der internen Kollaboratorgrenzen, Retention-/Grace-Strategie,
diagnostischen Detailtexte und der technisch passenden JSON-Projektion bleibt
dem Implementierungspaket überlassen, sofern Muss- und Akzeptanzkriterien,
Sicherheitsmodell und der hier beschlossene Außenvertrag unverändert bleiben.
