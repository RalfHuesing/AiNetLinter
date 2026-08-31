# Linse 6 – MCP-Komposition, Tool-Verträge und Agentenverträglichkeit

**Review-Urteil:** `approved` mit zwei nicht-blockierenden Befunden (`S2`, `S3`); kein `S0`/`S1`-Befund.

## Linse, Scope und Revision

- **Linse:** MCP-Komposition, Tool-Registrierung, aktuelle Tool-Schemas, Wire-/Fehlerverträge, Session-/Generation-Status und Agentenverträglichkeit.
- **Geprüfter Scope:** `src/AiNetLinter/Mcp/` einschließlich Target-Auflösung, Tool-Registrierung, Assembly-Dispatcher, Symbolgraph-/Assembly-Tools, Lease-/Referenzexpansion, Health-Response, Fehlerpolicy und MCP-Server-Komposition; zugeordnete Fast-/Integration-Tests sowie die relevanten Dokumentationsstellen.
- **Verwendete Revision:** `c942350d7478ebb6c1f9aae7c6979bc9dc3d8090` (`main`).
- **Working Tree:** Vor dem Schreiben dieses Reports waren parallele Änderungen an `tasks/decompiled-assembly-analysis-audit/code-map.md` und `reports/04-checkout-security.md` sichtbar. Sie wurden nicht verändert. Die Produktions-, Test-, Konfigurations- und Dokumentationsquellen waren für diese Prüfung unverändert.
- **MCP-Initialisierung:** `get_file_tree` zuerst als Summary gegen den Projekt-Target ausgeführt; danach `get_index_scope` und symbolische Abfragen mit `targetType` und absolutem `targetPath`. Der C#-Index meldete 845 vollständig abgedeckte Dateien.
- **Nicht geprüft:** `Stress`-Tests, privilegierte Reparse-/Symlink-Laufzeitpfade, entfernte Transport-/Clientvarianten, nicht reproduzierbare Maximal-Wiregrößen, externe Providerverfügbarkeit und Änderungen außerhalb der genannten Revision. Vermutete Status- oder Budgetabweichungen ohne reproduzierbaren Lauf sind als Abdeckungsgrenze behandelt.

## Executive Summary

### Befunde

1. **MCP-L6-001 – Ungültige Positionsspalte wird als interner Workspace-Fehler ausgegeben.** `find_references` akzeptiert `Datei:Zeile:Spalte`, validiert die Spalte aber nicht vor `SyntaxTree.FindToken`. Eine Spalte `0` führt reproduzierbar zu `isError=true` und `WORKSPACE_DIAGNOSTIC`, obwohl ein fehlerhaftes Nutzerargument nach der Fehlerpolicy recoverable mit `IsError=false` sein soll. **S2 / U2 / Beweissicherheit hoch / umgebungsunabhängig.**
2. **MCP-L6-002 – Health-Response-Builder überschreitet das aktive Agenten-Kontextlimit.** `get_violations` meldet `AIContextFootprint` mit 2502 gegenüber 2500 in `GetServerHealthResponseBuilder`. Das ist eine bestätigte Struktur-/Agentenverträglichkeitswarnung, kein nachgewiesener Wire- oder Laufzeitfehler. **S3 / U2 / Beweissicherheit hoch / umgebungsunabhängig.**

### Bestätigte Erwartungen

- Die zentrale Tool-Fabrik registriert die Toolmenge konsistent; `WiringToolCollectionContractTests` bestätigt 29 Tools, die Target-Matrix und explizite Annotationen.
- Zielgebundene Tools verlangen `targetType` und `targetPath`; `get_server_health` ist die dokumentierte Ausnahme mit Aggregation oder optionalem vollständigem Target-Block. `AnalysisTargetResolver` akzeptiert nur `project`/`assembly`, absolute Pfade und bei Assemblys vorhandene `.dll`-Dateien.
- Assembly-Dispatcher, Lease und Antwort-Enrichment tragen Herkunft, Generation, Status und Vollständigkeit bis in Text- und Structured-Content-Projektionen. Referenzexpansion ist pro Lease gecacht und wird bei den Assembly-Handlern vor dem Handleraufruf ausgeführt.
- Read-only-Tools werden mit `ReadOnly=true`, `Destructive=false`, `Idempotent=true` und `OpenWorld=false` registriert. Die relevanten Schema- und JSON-RPC-Tests bestätigen Legacy-/Modern-Discovery sowie Objektform von `structuredContent`.
- Health-Aggregation, zielgebundene Health-Abfrage und begrenzte Diagnose-Samples sind in den vorhandenen E2E-Tests abgedeckt; die Assembly-Health-Klasse lief isoliert mit 4/4 Tests erfolgreich.

### Abdeckungsgrenzen

- Der vollständige Integration-Lauf war nicht grün: 377 Tests, 41 Fehlschläge, 336 Erfolge. Die beobachteten Fehler waren überwiegend MCP-Prozess-/Transportabbrüche unter paralleler Ausführung; zusätzlich schlug ein Daemon-PID-Vergleich fehl. Die isolierte Assembly-Health-Klasse war grün, daher wird daraus kein zusätzlicher Produktbefund abgeleitet.
- Der Build-Gate `dotnet build --no-restore` scheiterte beim Kopieren einer Test-Assembly an einer Dateisperre durch einen laufenden Analyseprozess (`MSB3027/MSB3021`). Der Produktions-Build war im Testlauf erfolgreich; die Ursache ist der lokalen Prozessumgebung zugeordnet.
- Ein möglicher Divergenzpunkt bleibt unbelegt: Referenzdiagnosen liegen lease-lokal (`AssemblyAnalysisLease.ReferenceExpansionDiagnostics`), während die Registry-Health-Snapshots nur den Context-Status und Context-Diagnosen lesen. Ohne reproduzierbaren Folgeaufruf wird dies nicht als Befund gewertet.
- Das dokumentierte Diagnose-Samplebudget wurde statisch nachvollzogen, aber nicht als globales serialisiertes JSON-Bytebudget unter maximaler Referenzanzahl gemessen. Eine mögliche Mehrfachprojektion ist daher nur Coverage-Limit.

## Befund MCP-L6-001

### Titel und Komponente

**Ungültige Positionsspalte wird als interner Workspace-Fehler statt als recoverabler Eingabefehler behandelt**

Komponente: gemeinsamer Symbol-Identifikatorpfad von `FindReferencesTool` und `SymbolIdentifierResolver`; betroffen ist mindestens der Positionszweig von `find_references`.

- **Erwartetes Verhalten:** Ein syntaktisch parsebarer, aber ungültiger Positionswert (hier Spalte `0`) wird vor der Syntaxbaumabfrage validiert und als `INVALID_ARGUMENT`/recoverable mit `IsError=false` zurückgegeben. Der Agent erhält eine konkrete Korrekturhilfe; der Server meldet keinen internen Workspace-Fehler.
- **Beobachtetes Verhalten:** `TryParsePosition` akzeptiert jede Ganzzahl für Zeile und Spalte. `ResolveByPositionAsync` berechnet anschließend `line.Start + (column - 1)` ohne Spaltenbereichsprüfung. Bei `:1:0` wirft `FindToken` eine `ArgumentOutOfRangeException`; der äußere Catch mappt sie auf `McpToolResults.CompilationError`, also `WORKSPACE_DIAGNOSTIC` und `IsError=true`.
- **Auswirkung:** Ein korrigierbarer Agenten- oder Clientfehler sieht wie ein serverseitiger Workspace-/Compile-Fehler aus. Dadurch kann ein Agent unnötig retryen, die falsche Ursache diagnostizieren oder einen gültigen Workspace als defekt einstufen. Die Fehlerschnittstelle verletzt die im Code dokumentierte Trennung zwischen recoverable Eingabefehlern und echter Malfunction.
- **Schweregrad:** `S2`.
- **Umfang:** `U2` – gemeinsamer Positionsresolver innerhalb mehrerer Symbolgraph-Pfade; in diesem Audit direkt reproduziert für `find_references`.
- **Beweissicherheit:** `hoch`.
- **Umgebungsabhängigkeit:** keine; tritt bei einem gültigen Projekt-Target und der festen ungültigen Positionsspalte reproduzierbar auf.

### Konkrete Reproduktion

MCP-Aufruf mit redigierten Parametern:

```text
find_references({
  targetType: "project",
  targetPath: "<absolute-project-root>",
  symbolIdentifier: "src/AiNetLinter/Mcp/AnalysisTarget.cs:1:0",
  maxResults: 5,
  depth: 1,
  includeReferences: false
})
```

Beobachtete strukturierte Felder:

```text
isError: true
content[0].text:
  [ERROR]: WORKSPACE_DIAGNOSTIC: Unerwarteter Fehler in find_references:
  Specified argument was out of the range of valid values. (Parameter 'position')
  context: src/AiNetLinter/Mcp/AnalysisTarget.cs:1:0
```

### Belege

- MCP-Symbol `SymbolIdentifierResolver.TryParsePosition`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/SymbolIdentifierResolver.cs:46-58`; die Methode prüft nur Segmentanzahl und `int.TryParse`.
- MCP-Symbol `FindReferencesTool.ResolveSymbolCoreAsync`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:107-128`; der Positionszweig wird nach erfolgreichem Parse direkt aufgerufen.
- MCP-Symbol `FindReferencesTool.ResolveByPositionAsync`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:155-165`; Zeilenvalidierung ist vorhanden, Spaltenvalidierung fehlt vor `FindToken(position)`.
- MCP-Symbol `FindReferencesTool.ExecuteAsync`: `src/AiNetLinter/Mcp/Tools/SymbolGraph/FindReferencesTool.cs:79-84`; unerwartete Exceptions werden zu `CompilationError`.
- MCP-Symbol `McpToolResults.Recoverable`/`BuildResult`: `src/AiNetLinter/Mcp/McpToolResults.cs:55-73`; dokumentiert `IsError=false` für ungültige Argumente und erzeugt dieses Feld auch tatsächlich.
- MCP-Symbol `McpToolResults.CompilationError`: `src/AiNetLinter/Mcp/McpToolResults.cs:185-196`; setzt den Code `WORKSPACE_DIAGNOSTIC` und ist für echte Malfunction vorgesehen.
- Der direkte MCP-Aufruf oben wurde mit `targetType` und absolutem `targetPath` ausgeführt; der Output wurde auf die redigierten Felder reduziert.

### Tests und Gegenprüfung

- `dotnet test src/AiNetLinter.FastTests --filter "FullyQualifiedName~WiringToolCollectionContractTests|FullyQualifiedName~AssemblyAnalysisDispatcherCapabilityTests|FullyQualifiedName~AssemblyAnalysisRouteTests|FullyQualifiedName~AssemblyAnalysisToolTests|FullyQualifiedName~AnalysisTargetResolverTests|FullyQualifiedName~AnalysisToolCallTests" --no-restore` → 45/45 erfolgreich.
- Die vorhandenen Tests `WiringToolCollectionContractTests`, `AssemblyAnalysisRouteTests` und `McpServerCommandJsonRpcFramingTests` bestätigen Registrierung, Routing und Wire-Objektform, enthalten aber keinen negativen Positionsfall mit Spalte `0`.

### Nicht umgesetzte Remediation-Hypothese

Vor der Positionsberechnung sollte eine gemeinsame Spaltenprüfung gegen `SourceText`/Zeilenbreite positive, nullgroße und überlange Werte abweisen und `McpToolResults.Recoverable(LinterErrorCodes.InvalidArgument, ...)` verwenden. Die Logik sollte für alle Nutzer des gemeinsamen Positionsresolvers gelten. Es wurde keine Änderung umgesetzt.

**Disposition:** `promoted-to-project-debt`; Audit-only, kein Fix.

## Befund MCP-L6-002

### Titel und Komponente

**Health-Response-Builder überschreitet das aktive Agenten-Kontextlimit**

Komponente: `GetServerHealthResponseBuilder` und seine transitiven MCP-Health-Abhängigkeiten.

- **Erwartetes Verhalten:** Die transitive kontextwirksame Größe eines zentralen Tool-Bausteins bleibt unter dem aktiven `MaxAIContextFootprint`-Grenzwert von 2500, oder die Verantwortung wird so geschnitten, dass relevante Invarianten für Agenten in kleineren Einheiten navigierbar bleiben.
- **Beobachtetes Verhalten:** Der gezielte MCP-Violation-Check meldete genau eine Warnung: `GetServerHealthResponseBuilder (2502 > 2500)`; als größte transitive Abhängigkeiten wurden `ExternalResourceRegistry (470)`, `McpCodeGraphServer (448)` und `SourceSnapshotIdentity (310)` ausgewiesen.
- **Auswirkung:** Änderungen oder Reviews an Health-Status, Diagnoseprojektion und Session-Metadaten benötigen mehr transitiven Kontext als die aktive Agentenleitplanke vorsieht. Das erhöht das Risiko, dass Agenten relevante Status-/Wire-Invarianten übersehen. Ein konkreter Laufzeitfehler wurde nicht nachgewiesen.
- **Schweregrad:** `S3`.
- **Umfang:** `U2` – eine konkrete Response-Komponente mit mehreren transitiven Abhängigkeiten.
- **Beweissicherheit:** `hoch` – MCP-Violation mit Datei, Zeile und strukturierten Detailwerten.
- **Umgebungsabhängigkeit:** keine; der Verstoß ist snapshot-/konfigurationsgebunden und nicht von Transport oder Betriebssystem abhängig.

### Konkrete Reproduktion

MCP-Aufruf mit redigierten Parametern:

```text
get_violations({
  targetType: "project",
  targetPath: "<absolute-project-root>",
  scopeFilter: "src/AiNetLinter/Mcp",
  maxResults: 100,
  includeSnippet: true,
  contextLines: 0
})
```

Strukturierte Rückgabe:

```text
violations[0].filePath: src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs
violations[0].lineNumber: 17
violations[0].ruleName: AIContextFootprint
violations[0].details: GetServerHealthResponseBuilder (2502 > 2500)
```

Begründung: Der MCP-Check meldete im Scope 283 Dateien genau eine Warnung und keine Fehler. Die Zahl 2502 liegt um zwei Zeilen über dem aktiven Grenzwert 2500; damit ist der Befund unabhängig von einer Interpretation der Health-Wire-Ausgabe reproduzierbar.

### Belege, Tests und Gegenprüfung

- `get_violations` mit `targetType: project`, `targetPath: <absolute-project-root>`, `scopeFilter: src/AiNetLinter/Mcp` → 1 Warnung, 0 Fehler; Datei/Zeile und Abhängigkeitsaufschlüsselung wie oben.
- `src/AiNetLinter/Mcp/Tools/ServerMaintenance/GetServerHealthResponseBuilder.cs:17` ist der betroffene Typ.
- `rules.json:154` setzt `MaxAIContextFootprint` auf 2500; die aktuelle Violation bestätigt die Überschreitung im selben Projekt-Target.
- Die vorhandenen Health-E2E-Tests und `WiringToolCollectionContractTests` liefen in den gezielten Slices erfolgreich; sie prüfen Funktion/Wire-Vertrag, nicht die Abwesenheit dieser strukturellen Warnung.

### Nicht umgesetzte Remediation-Hypothese

Die Health-Antwort könnte nach Verantwortungen in kleinere Formatter-/Projection-Fassaden geschnitten werden; alternativ könnten schmale Interfaces die drei größten transitiven Abhängigkeiten entkoppeln. Vor einer Umsetzung wäre zu prüfen, ob Status-, Generation- und Diagnoseinvarianten weiterhin in einer klaren Grenze bleiben. Es wurde keine Änderung umgesetzt.

**Disposition:** `promoted-to-project-debt`; nicht als funktionaler MCP-Ausfall eingestuft.

## Registrierung, Schema und Session-Verträge ohne zusätzlichen Befund

- `src/AiNetLinter/Mcp/Composition/McpServerToolCollectionFactory.cs:10-20` bündelt die Registrierungen; `SymbolGraphToolRegistrations` und `AssemblyAnalysisToolRegistrations` werden gemeinsam in die Toolcollection aufgenommen.
- `src/AiNetLinter/Mcp/Tools/McpToolRegistrationOptions.cs:9-24,38-54` trennt projektgebundene, projekt-/assembly-fähige, assembly-only, Health- und Feedback-Verträge und setzt die Annotationen zentral.
- `src/AiNetLinter/Mcp/Registration/SymbolGraphToolRegistrations.cs:61-211` bestätigt die aktuellen Defaults `maxResults`, `depth`, `topN`, `includeReferences` und die Capability-Matrix; `get_impact` bleibt projektgebunden.
- `src/AiNetLinter/Mcp/Registration/AssemblyAnalysisToolRegistrations.cs:20-120` registriert `inspect_assembly` und `find_assembly_extensions` assembly-only mit `namespace`, Typ-/Member-Filtern und Limits.
- `src/AiNetLinter/Mcp/Registration/ServerMaintenanceToolRegistrations.cs:64-115` bestätigt den optionalen Target-Block von `get_server_health`, Aggregation ohne Target sowie `includeDiagnostics`/`maxDiagnostics`.
- `src/AiNetLinter/Mcp/AnalysisTargetResolver.cs` normalisiert und validiert Target-Typ und absoluten Pfad; ungültige Paare werden vor dem Toolhandler recoverable zurückgegeben.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/References/AssemblyAnalysisLease.cs:47-73` hält Referenzsessions/-diagnosen lease-lokal und cached `ExpandReferencesAsync` pro Lease.
- `src/AiNetLinter/Mcp/Tools/AssemblyAnalysis/AssemblyAnalysisResponse.cs:20-63` ergänzt Status, Herkunft, Generation und Vollständigkeit für Text und Structured Content. Eine globale Wire-Bytezahl wurde nicht behauptet.
- `src/AiNetLinter/Mcp/Assemblies/Analysis/AssemblyAnalysisRegistry.SourceProjects.cs:202-253` bildet Registry-Health-Snapshots aus Sessionstatus, Herkunft, Generation und Context-Diagnosen.
- `Docs/agent-api.md:330,337,349-350,373,426` und `Docs/configuration.md:35` beschreiben Matrix, Assembly-Schemas, Health-Optionen, begrenzte Diagnosen und additive Structured-Content-Felder konsistent zur Registrierung.
- `.agents/rules/AiNetLinter-McpWorkflow.mdc:30,77,99` dokumentiert die Target-Paarpflicht sowie die Assembly-Abfragepfade konsistent.

## Code-Map-Abgleich

Die aktuell sichtbaren L6-Navigationsfakten in `tasks/decompiled-assembly-analysis-audit/code-map.md` sind korrekt; ich habe die Datei nicht geändert. Insbesondere stimmen `code-map.md:11-14` mit den MCP-Funden überein: Routing über `AnalysisToolCall`, absolute Target-Validierung, Session-/Response-Kern und Symbolgraph-/Assembly-Navigation. Die Routingzeile verwendet aktuell „vor jedem Assembly-Tool-Handler“; das entspricht dem geprüften Dispatcher-Kontrollfluss. Die sichtbare Änderung an dieser Zeile stammt aus der parallelen Audit-Welle und wurde nicht von mir bearbeitet. Es gab für Linse 6 keinen veralteten Navigationsfakt, der eine eigene Code-Map-Korrektur erfordert.

## Mögliche Cross-Lens-Überschneidungen

| Überschneidung | Relevanz für Linse 6 |
|---|---|
| Symbol-/Source-Auflösung | MCP-L6-001 betrifft die gemeinsame Positionsauflösung; eine andere Linse sollte prüfen, ob dieselbe Koordinate in weiteren Symboltools denselben Fehlerpfad nutzt. |
| Assembly-Session, Referenzexpansion und Health | Der unbestätigte Lease-vs-Registry-Diagnosepunkt berührt Generation-/Lifecycle-Linsen; aus ihm wurde hier kein Befund abgeleitet. |
| Architektur/Refactoring | MCP-L6-002 ist eine strukturelle Agenten-Kontextwarnung; eine Refactoring-Linse kann Schnittgrenzen prüfen, ohne den Wire-Vertrag zu verändern. |
| Checkout-/externe Quelle | Herkunft, Snapshot-Identität und Trust werden vom MCP-Wire weitergegeben; Sicherheits- und Attestation-Linsen bewerten die Erzeugung dieser Werte. |

## Coverage-/Limitations-Tabelle

| Bereich | Abdeckung | Ergebnis / Grenze |
|---|---|---|
| Tool-Registrierung und Inventory | hoch | 29-Tool-Contract, Capability-Matrix und Annotationen in FastTests bestätigt. |
| Aktuelle Parameter-Schemas | hoch | Registrierungsquellen, MCP-Toolinventar und `McpServerAssemblyHealthE2ETests` geprüft; keine Schemaabweichung gefunden. |
| Target-Auflösung | hoch | Projekt-/Assembly-Paarpflicht, absolute Pfade, `.dll`-Existenz und Unsupported-Pfade semantisch geprüft. |
| Text-/Structured-Content-Wire | hoch für Normalpfade | Framing-/Discovery-Tests und Response-Enrichment geprüft; maximale serialisierte Diagnosegröße nicht gemessen. |
| Recoverable-vs-Malfunction-Fehlervertrag | hoch für L6-001 | Ungültige Positionsspalte live reproduziert; weitere ungültige Koordinaten und alle Consumer des gemeinsamen Resolvers nicht vollständig ausgeführt. |
| Session-/Generation-Status | mittel bis hoch | Lease-, Referenz- und Registry-Pfade sowie Assembly-Health-E2E geprüft; parallele Integration war wegen Prozessabbrüchen nicht vollständig belastbar. |
| Health-Diagnoseprojektion | mittel | Root-/transitive Summaries und Limits statisch/E2E geprüft; lease-lokale Folgeeffekte nicht als reproduzierbarer Befund bestätigt. |
| Agentenverträglichkeit | hoch für L6-002 | `get_violations` liefert einen eindeutigen `AIContextFootprint`-Warnbefund; Laufzeitwirkung ist nicht gemessen. |
| Externe/entfernte Umgebungen | niedrig | Keine verlässliche Aussage über entfernte Provider, alternative MCP-Clients oder Netzwerkfehler. |
| Stress und privilegierte Dateisystemtests | nicht geprüft | Laut Vorgabe ausgeschlossen bzw. lokale Berechtigung fehlte; daraus keine Befunde abgeleitet. |

## Verifikation

- `dotnet test src/AiNetLinter.FastTests --filter Category!=Stress --no-restore` → 2276 gesamt, 2274 erfolgreich, 2 umgebungsbedingt übersprungen.
- `dotnet test src/AiNetLinter.IntegrationTests --filter Category!=Stress --no-restore` → 377 gesamt, 336 erfolgreich, 41 fehlgeschlagen; überwiegend parallele Prozess-/Transportabbrüche, daher als Coverage-Limit gewertet.
- `dotnet test src/AiNetLinter.IntegrationTests --filter "FullyQualifiedName~McpServerAssemblyHealthE2ETests" --no-restore --logger "trx;LogFileName=linse6-assembly-health.trx"` → 4/4 erfolgreich.
- `dotnet build --no-restore` → fehlgeschlagen beim Kopieren einer durch einen laufenden Analyseprozess gesperrten Test-Assembly; Produktionsprojekt kompilierte zuvor erfolgreich.
- Kein Commit erstellt; keine Source-, Test-, Konfigurations-, Dokumentations- oder fremden Reportdateien geändert.
